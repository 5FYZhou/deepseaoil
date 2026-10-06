using DeepseaOil.Data;
using DeepseaOil.Logic;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Presentation.Effects;
using UnityEngine;

namespace DeepseaOil.Presentation.Actor
{
    /// <summary>
    /// 一只敌人：建出刚体与视效、推进逻辑层、结算伤害与死亡。<b>它是这只敌人的组合根</b>。
    /// </summary>
    /// <remarks>
    /// 环境事实（刚体、半径、配置、格子归属）只在这里组装一次，逻辑层（<see cref="EnemyLogic"/>）不碰引擎类型。
    /// <para><b>不自己驱动逻辑</b>：由 <c>CombatDirector</c> 统一逐只 <see cref="FixedTick"/>。
    /// "每个实例自己 Tick"会引出执行顺序问题（谁先读位置、谁后写速度），而顺序必须可预测。</para>
    /// <para><b>受伤只有一条路</b>：<see cref="TakeDamage"/>。它实现了 <see cref="IDamageable"/>，
    /// 于是格子系统不需要认识"敌人"这个类型 —— 它只知道"这一格上有个可结算的目标"。</para>
    /// <para><b>它把自己的脚底中心登记进 <see cref="EnemyCellRegistry"/></b>：格子按"人站在哪一格"结算，
    /// 而这件事只有敌人自己每帧知道（位置是它自己的）。</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class EnemyActor : MonoBehaviour, IDamageable
    {
        /// <summary>头顶耐久数字的字号（<c>TextMesh.characterSize</c>，世界单位量级）。</summary>
        /// <remarks>
        /// 它和 <c>TextMesh.fontSize</c> 是两件事：<c>fontSize = 64</c> 是<b>烘焙进图集的字号</b>
        /// （越大越清晰，不改显示大小），<c>characterSize</c> 才是显示大小。
        /// </remarks>
        private const float HpTextCharacterSize = 0.13f;

        /// <summary>头顶数字的垂直偏移；<c>0</c> = 压在圆心（锚点用 <c>MiddleCenter</c>）。</summary>
        private const float HpTextOffsetY = 0f;

        /// <summary>
        /// 内置字体的候选名，按"新版 → 旧版"排。
        /// </summary>
        /// <remarks>
        /// <c>LegacyRuntime.ttf</c> 在前：2022.3 里 <c>Resources.GetBuiltinResource&lt;Font&gt;("Arial.ttf")</c>
        /// <b>会抛 <c>ArgumentException</c></b>（而不是返回 null），所以逐个 <c>try</c> 着试；
        /// 直接命中第一个名字就不必先吃一次异常。
        /// </remarks>
        private static readonly string[] BuiltinFontNames = { "LegacyRuntime.ttf", "Arial.ttf" };

        private EnemySpec _spec;
        private EnemyLogic _logic;
        private EnemyMotor _motor;
        private SpriteRenderer _body;
        private TextMesh _hpText;
        private Transform _target;
        private GridLogic _grid;
        private EnemyCellRegistry _registry;

        private int _hp;
        private bool _dead;
        private bool _registered;
        private Vector3Int _currentCell;

        /// <summary>
        /// 本帧实际喂进逻辑层的减速系数。
        /// </summary>
        /// <remarks>
        /// <b>刻意留一份，而不是"视效自己去查一次格子"。</b>两处独立实现同一条判据时，
        /// 颜色与速度迟早会在某一帧不一致（比如泥浆刚好在那一帧消失），
        /// 现象是"颜色变回来了但人还是慢的"—— 而两者看起来都"没错"。一份数据、一个来源、两个消费者。
        /// </remarks>
        private float _slowMultiplier = 1f;

        /// <inheritdoc />
        public bool IsDead => _dead;

        /// <inheritdoc />
        /// <remarks>
        /// <b>取执行器的物理体位置，不取 <c>transform.position</c>：</b>后者会被刚体的位置积分覆盖，
        /// 而本属性同时喂给"伤害方向从哪算"与"我站在哪一格"两件事 ——
        /// 留着两个位置真值，它们迟早会在某一帧对不上（而那一帧没有任何报错）。
        /// </remarks>
        public Vector2 Position => _motor != null ? _motor.Position : (Vector2)transform.position;

        /// <summary>剩余耐久（只读，供调试读数）。</summary>
        public int Hp => _hp;

        /// <summary>当前是否处于受击（速度被外力接管的那一段）。</summary>
        public bool IsHurt => _logic != null && _logic.IsHurt;

        /// <summary>引擎当前速度（单位/秒）。<b>只给调试读数用</b>，不参与任何判定。</summary>
        /// <remarks>
        /// 读的是执行器的回读口（= <c>Rigidbody2D.velocity</c>），所以它回答的是
        /// "物理体这一帧真的在动吗"，而不是"账本以为它该动多少"。两者不一致时（撞墙、被顶住）
        /// 只有这个数看得出来。
        /// </remarks>
        public Vector2 EngineVelocity => _motor == null ? Vector2.zero : _motor.Velocity;

        /// <summary>
        /// 组装一只敌人。依赖全部由参数给出（不留 inspector 字段），所以"忘了接线"这种失败模式不存在。
        /// </summary>
        /// <param name="position">出生位置。</param>
        /// <param name="spec">表值。</param>
        /// <param name="target">追击目标（玩家）；可为 <c>null</c>（敌人随即滑停）。</param>
        /// <param name="facing">初始朝向，用于镜像。</param>
        /// <param name="grid">格子门面（查减速 + 世界 → 格）。</param>
        /// <param name="registry">敌人归属表；<c>null</c> 时不登记（敌人不会被格子结算）。</param>
        /// <param name="parent">层级父物体；<c>null</c> 时留在根下。</param>
        public void Initialize(
            Vector2 position,
            in EnemySpec spec,
            Transform target,
            Vector2 facing,
            GridLogic grid,
            EnemyCellRegistry registry,
            Transform parent)
        {
            _spec = spec;
            _target = target;
            _grid = grid;
            _registry = registry;
            _hp = spec.Hp;

            transform.position = new Vector3(position.x, position.y, 0f);

            if (parent != null) transform.SetParent(parent, true);

            BuildBody();

            _motor = gameObject.AddComponent<EnemyMotor>();

            // 建完刚体立刻固化物理参数：否则"造出来到第一次读位置"之间的那个物理步
            // 会用默认重力跑（偏差极小，但那是隐式答案）
            _motor.EnsureInitialized();

            _logic = new EnemyLogic(_motor, in spec, EnemyCharacterFactory.Build(in spec));

            // 朝向先立起来：否则第一个朝向的左/右是"上一个物体留下的"。
            _motor.Facing = facing;

            BuildVisuals();
            UpdateCell(force: true);
        }

        /// <summary>
        /// 结算一次命中。<b>唯一受伤入口</b>（格子状态转换、将来的近战与陷阱都走这里）。
        /// </summary>
        /// <param name="damage">命中事实。</param>
        /// <remarks>
        /// <b>扣血与击退是两件可选的事</b>（见 <see cref="Damage"/>）：
        /// <see cref="Damage.HasDamage"/> 为假时只推不扣，<see cref="Damage.HasKnockback"/> 为假时只扣不推。
        /// 白模里那两条路（<c>TakeDamage</c> 与 <c>TakeDirectDamage</c>）合并成了这一条。
        /// <para><b>死亡那一帧也照常应用状态</b>：反正它马上被销毁，但"死亡路径也要把状态写完整"
        /// 这件事将来会用上（比如死亡动画期间尸体会被继续推）。</para>
        /// <para><b>不需要"同一颗球只算一次"那道锁了</b>：球不再直接伤害敌人，
        /// 而格子系统按归属表结算，一个目标在一格里只出现一次。</para>
        /// </remarks>
        public void TakeDamage(in Damage damage)
        {
            if (_dead) return;

            if (damage.HasDamage)
            {
                // 耐久是整数，伤害是配置里的浮点数：四舍五入到整数。
                // 取整而不是"只要有伤害就扣 1"：将来出现 0.5 点伤害时行为才有意义。
                _hp = Mathf.Max(0, _hp - Mathf.RoundToInt(damage.Amount));
            }

            if (damage.HasKnockback && _logic != null)
            {
                // 只递交：冲量在下一次逻辑帧由状态效果层变成一次"进入受击"（见 EnemyLogic 的注释）。
                // "被撞多远 / 滑多久"因此归角色配置（hurtDecay），不归这里。
                _logic.ApplyKnockback(damage.Impulse, damage.Direction);
            }

            if (_hp <= 0)
            {
                Die(damage.Direction);
                return;
            }

            // 数字当场刷新，不等下一次 Tick：受击与刷新之间隔着半个物理帧，
            // 那一帧里玩家看到的还是旧数字 —— 而"打中"这件事必须当帧可见。
            UpdateHpText();
        }

        /// <summary>
        /// 推进一个物理帧：算减速、刷视效、上报所在格，然后驱动逻辑层。
        /// </summary>
        /// <remarks>
        /// 由 <c>CombatDirector</c> 调用，<b>不</b>用 <c>Update</c> ——
        /// 速度必须在一个物理帧里被提交一次，而不是每个渲染帧提交多次。
        /// <para>视效在物理帧刷而不是渲染帧刷：<b>颜色要跟逻辑层用的是同一份减速系数</b>
        /// （见 <see cref="_slowMultiplier"/> 的注释）。两者用不同频率更新就会出现一帧的不同步，
        /// 而那一帧正好是"泥浆刚消失"的时候。</para>
        /// </remarks>
        public void FixedTick(float now, float deltaTime)
        {
            if (_dead) return;

            // 先算"这一帧踩没踩在减速格里"，再把它喂给逻辑层和视效 —— 同一个来源。
            _slowMultiplier = _grid == null ? 1f : _grid.GetSlowMultiplier(Position);

            _logic.SetTarget(_target == null ? (Vector2?)null : TargetPosition());
            _logic.SetSlowMultiplier(_slowMultiplier);
            _logic.Tick(now, deltaTime);

            UpdateCell(force: false);
            UpdateBodyColor();
            UpdateSortingOrder();
        }

        /// <summary>选中时把接触判定半径之外的追击参数画出来，便于对照配置。</summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _spec.Radius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _spec.StopDistance);
        }

        private void OnDestroy()
        {
            if (_registry != null && _registered) _registry.Unregister(this);
        }

        // ─────────────────────────────────────────────
        // 组装
        // ─────────────────────────────────────────────

        /// <summary>建物理体：只建"形状"（刚体 ＋ 圆形碰撞体）。</summary>
        /// <remarks>
        /// <b>物理参数不在这里</b>：重力缩放 / 冻结旋转 / 连续碰撞检测是"敌人执行器"的事
        /// （<c>EnemyMotor.ApplyPhysics</c>）。收口前它们写在建物体的地方，
        /// 于是"敌人的物理长什么样"有两个可能的答案（这里 ＋ 执行器）。
        /// </remarks>
        private void BuildBody()
        {
            gameObject.AddComponent<Rigidbody2D>();

            var collider = gameObject.AddComponent<CircleCollider2D>();
            collider.radius = _spec.Radius;
        }

        private void BuildVisuals()
        {
            // 身体渲染器要留成字段：每物理帧按"闪不闪、踩不踩减速格"改它的颜色。
            _body = gameObject.AddComponent<SpriteRenderer>();

            PrimitiveSprites.Configure(
                _body,
                PrimitiveSprites.Circle,
                EnemyVisual.BodyColorNormal,
                RenderOrder.ActorOrder(Position.y),
                _spec.Radius * 2f);

            BuildHpText();
            UpdateHpText();
            UpdateBodyColor();
        }

        /// <summary>
        /// 建敌人圆心的剩余耐久数字。
        /// </summary>
        /// <remarks>
        /// <b>为什么是 <c>TextMesh</c>：</b>需求是"直接写数字"，而数字只有三条路 —— 导入字体资产、
        /// 引 TextMeshPro、或自己拼笔画。<c>TextMesh</c> 是引擎自带的，三行代码。
        /// <para>字体取不到时<b>干脆不建数字</b>：<c>TextMesh</c> 没有字体会画成一堆方块，
        /// 宁可"没有数字"，也不要"看起来像数字其实是豆腐块"。但"拿不到"本身也不报错 ——
        /// 数字只是读数，判定不依赖它。</para>
        /// </remarks>
        private void BuildHpText()
        {
            Font font = BuiltinFont();

            if (font == null) return;

            var go = new GameObject("耐久数字");

            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, HpTextOffsetY, 0f);

            _hpText = go.AddComponent<TextMesh>();

            _hpText.font = font;
            _hpText.fontSize = 64;
            _hpText.characterSize = HpTextCharacterSize;

            // 锚点居中是**配合偏移为 0** 用的：LowerCenter 会让数字从"给定位置"往上长，
            // 于是要把位置抬高一个偏移才落得回身体上；MiddleCenter 才是"给定的位置就是数字的中心"。
            _hpText.anchor = TextAnchor.MiddleCenter;
            _hpText.alignment = TextAlignment.Center;
            _hpText.color = Color.white;

            // 材质必须从字体上取：TextMesh 不会自己找，不给就是粉红方块。
            var textRenderer = _hpText.GetComponent<MeshRenderer>();

            textRenderer.sharedMaterial = font.material;

            // **sortingOrder 必须显式设。** TextMesh 走 MeshRenderer，而 MeshRenderer 和
            // SpriteRenderer 一样继承 Renderer、一样有 sortingOrder。全部是 z=0 的正交俯视，
            // 深度分不出先后 —— 不设这一句，数字就会和自己的身体抢先后。
            textRenderer.sortingOrder = RenderOrder.ActorOverlay;
        }

        /// <summary>取一个能用的内置字体；一个都拿不到时返回 <c>null</c>（调用方跳过数字，不报错）。</summary>
        private static Font BuiltinFont()
        {
            for (int i = 0; i < BuiltinFontNames.Length; i++)
            {
                try
                {
                    Font font = Resources.GetBuiltinResource<Font>(BuiltinFontNames[i]);

                    if (font != null) return font;
                }
                catch (System.ArgumentException)
                {
                    // 这个版本不认这个名字：试下一个。
                }
            }

            return null;
        }

        // ─────────────────────────────────────────────
        // 每帧刷新
        // ─────────────────────────────────────────────

        /// <summary>刷新头顶数字。<b>只在真的变了才写</b>：给 <c>TextMesh.text</c> 赋值会重建网格。</summary>
        private void UpdateHpText()
        {
            if (_hpText == null) return;

            string text = EnemyVisual.HpText(_hp);

            if (_hpText.text == text) return;

            _hpText.text = text;
        }

        /// <summary>
        /// 刷新身体颜色：减速变深、受击闪烁，两者可叠加。
        /// </summary>
        /// <remarks>
        /// 闪烁相位用 <c>Time.time</c> 而不是自己累加：累加出来的相位会随帧率漂，
        /// 而"闪了几下"是玩家会数的东西。
        /// <para>判"该不该闪"用受击状态（<c>IsHurt</c>）：闪烁与"被撞飞的那一段"是同一件事的两面
        /// （速度滑停到零，闪也结束），另立一个计时器只会让两者悄悄不同步。</para>
        /// </remarks>
        private void UpdateBodyColor()
        {
            if (_body == null) return;

            bool flashOn = IsHurt && EnemyVisual.IsFlashOn(Time.time, _spec.FlashHz);

            _body.color = EnemyVisual.BodyColor(_slowMultiplier, flashOn);
        }

        /// <summary>
        /// 按 y 刷新本体的渲染档位（Y-Sort）。
        /// </summary>
        /// <remarks>
        /// 与颜色一起在物理帧刷：两者都是"这一帧它在场上的哪里 / 什么状态"的读数，
        /// 分成两个频率就会在某一帧对不上（而那一帧恰好是"刚挪到别人前面"的时候）。
        /// <para>档位换算在地基（<c>YSort</c>），频带在 <c>RenderOrder</c> —— 本类只说"按我的 y 取档"。</para>
        /// </remarks>
        private void UpdateSortingOrder()
        {
            if (_body == null) return;

            _body.sortingOrder = RenderOrder.ActorOrder(Position.y);
        }

        /// <summary>把脚底中心上报给归属表；格没变时什么都不做。</summary>
        private void UpdateCell(bool force)
        {
            if (_registry == null || _grid == null) return;

            Vector3Int cell = _grid.WorldToCell(Position);

            if (!force && _registered && cell == _currentCell) return;

            _currentCell = cell;

            if (_registered) _registry.Move(this, cell);
            else
            {
                _registry.Register(cell, this);
                _registered = true;
            }
        }

        private Vector2 TargetPosition()
        {
            Vector3 p = _target.position;

            return new Vector2(p.x, p.y);
        }

        private void Die(Vector2 hitDirection)
        {
            _dead = true;

            // 速度交给碎片：尸体自己不该继续滑（它这一帧还在被物理推动，看起来像"死掉的敌人还在走"）。
            if (_motor != null) _motor.Move(Vector2.zero);

            // 死亡即刻摘掉归属：留着它会让这一格继续"有目标"，而结算方拿到的是一个正在销毁的对象。
            if (_registry != null && _registered)
            {
                _registry.Unregister(this);
                _registered = false;
            }

            // 碎裂走 EffectModule：表现层的统一出口（暂停会一起冻结、切场景会一起清）。
            EffectContext ctx = EffectContext.At(Position, hitDirection);
            ctx.Tint = EnemyVisual.BodyColorNormal;

            EffectModule.Play(EffectId.EnemyShatter, in ctx);

            Destroy(gameObject);
        }
    }
}
