using UnityEngine;

namespace DeepseaOil.Prototype
{
    /// <summary>
    /// 一只白模敌人：建出刚体与视效、推进逻辑层、结算伤害与死亡。
    /// </summary>
    /// <remarks>
    /// <b>本类是这只敌人的组合根</b>，与 <c>PlayerController</c> 对玩家的角色对应：
    /// 环境事实（刚体、半径、配置）只在这里组装一次，逻辑层（<see cref="EnemyLogic"/>）不碰引擎类型。
    /// <para><b>不自己驱动 <c>FixedUpdate</c> 里的逻辑</b> —— 由 <c>EnemyDirector</c> 统一逐只驱动。
    /// 一个 unity 里"每个实例自己 Tick"会引出执行顺序问题（谁先读位置、谁后写速度），
    /// 而顺序在白模里必须是可预测的：先弄清谁在追谁，再一起算一步。</para>
    /// <para><b>"一帧内只结算一次"是刻意的</b>（见 <see cref="TakeDamage"/>）：落地结算会遍历落点半径内的
    /// 全部碰撞体，而同一个敌人可能被查到多次（多个碰撞体、多个父级刚体）。
    /// 没有这道锁，一颗球会把 3 点耐久一次打空 —— 现象是"说好的三下变成一下"。</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class EnemyActor : MonoBehaviour
    {
        private int _hp;
        private bool _dead;

        /// <summary>最近一次结算过的球编号。<c>int.MinValue</c> 保证第一颗球一定算命中。</summary>
        private int _lastBallId = int.MinValue;

        private EnemyConfig _config;
        private EnemyLogic _logic;
        private EnemyMotor _motor;
        private SpriteRenderer _body;
        private TextMesh _hpText;
        private Transform _target;

        /// <summary>
        /// 本帧实际喂进逻辑层的泥浆系数。
        /// </summary>
        /// <remarks>
        /// <b>刻意留一份，而不是"视效自己去查一次泥浆"。</b>查询是两处独立实现的同一条判据时，
        /// 颜色与速度迟早会在某一帧不一致（比如泥浆刚好在那一帧消失），
        /// 现象是"颜色变回来了但人还是慢的"—— 而两者看起来都"没错"。
        /// 一份数据、一个来源、两个消费者。
        /// </remarks>
        private float _slowMultiplier = 1f;

        /// <summary>
        /// 耐久上限。
        /// </summary>
        /// <remarks>
        /// 只给本类内部写日志用。**没有对外读口是刻意的**：白模里没有任何系统会去"读别人的耐久"
        /// （头顶那个数字是本类自己写的），凭空开一个只在将来才有用的读口等于留一段没人验的代码。
        /// </remarks>
        public int MaxHp => ThrowConstants.ENEMY_HP;

        /// <summary>是否已死（死亡后本帧内还能被查到，直到 <c>Destroy</c> 真正生效）。</summary>
        public bool IsDead => _dead;

        /// <summary>引擎当前速度（单位/秒）。<b>只给调试读数用</b>，不参与任何判定。</summary>
        /// <remarks>
        /// 读的是执行器的回读口（= <c>Rigidbody2D.velocity</c>），所以它回答的是
        /// "物理体这一帧真的在动吗"，而不是"账本以为它该动多少"。
        /// 两者不一致时（撞墙、被别的敌人顶住）只有这个数看得出来。
        /// </remarks>
        public Vector2 CurrentVelocity => _motor == null ? Vector2.zero : _motor.Velocity;

        /// <summary>当前是否处于被砸中后的禁足（禁足期间不转向）。</summary>
        public bool IsStunned => _logic != null && _logic.IsStunned;

        /// <summary>
        /// 组装一只敌人。依赖全部由参数给出（不留 inspector 字段），所以"忘了接线"这种失败模式不存在。
        /// </summary>
        /// <param name="position">出生位置。</param>
        /// <param name="config">数值。</param>
        /// <param name="target">追击目标（玩家）。</param>
        /// <param name="facing">初始朝向；用于镜像。</param>
        public void Initialize(Vector2 position, EnemyConfig config, Transform target, Vector2 facing)
        {
            _config = config;
            _target = target;
            _hp = ThrowConstants.ENEMY_HP;

            transform.position = new Vector3(position.x, position.y, 0f);

            BuildBody();

            _motor = gameObject.AddComponent<EnemyMotor>();
            _logic = new EnemyLogic(_motor, config);

            // 朝向先立起来：否则第一个朝向的左/右是"上一个物体留下的"。
            _motor.Facing = facing;

            BuildVisuals();
        }

        /// <summary>
        /// 结算一次命中。<b>唯一受伤入口</b>（球、将来的近战与陷阱都走这里）。
        /// </summary>
        /// <param name="damage">命中事实。</param>
        /// <remarks>
        /// <b>两道门控，挡的是不同的事：</b>
        /// <list type="number">
        /// <item><see cref="IsDead"/> —— 已死的目标不该被再打一次（会重复生成碎片、重复写速度）。</item>
        /// <item>"同一颗球只算一次" —— 落地结算按半径查碰撞体，同一个敌人可能被查到多次
        /// （多碰撞体、多父级），没有这道锁一颗球就能打空 3 点耐久，
        /// 现象是"说好的三下变成一下"。</item>
        /// </list>
        /// <para><b>判据是球编号，不是"本帧"。</b>见 <see cref="Damage.BallId"/>：
        /// 帧判据要求每帧有人复位标记，而那会把正确性绑死在脚本执行顺序上
        /// （<c>ThrowSpawner</c> 与 <c>EnemyDirector</c> 谁先跑 Unity 不保证），
        /// 先跑的那一帧伤害会被静默丢掉。球编号只增不减，比较它不需要任何复位。</para>
        /// <para><b>死亡那一帧也照常应用击退与停顿。</b>反正它马上被销毁，
        /// 但"死亡路径也要把状态写完整"这件事将来会用上（比如死亡动画期间尸体会被继续推）。</para>
        /// </remarks>
        public void TakeDamage(in Damage damage)
        {
            if (_dead) return;

            // 同一颗球打到过我就直接返回：这是"一颗球一点耐久"的锁（见上面第 2 条）。
            //
            // **判据是"不大于"而不是"不等于"。** 用 `==` 时它只挡"同一颗球被查到两次"，
            // 挡不住"编号比上一颗更小的球" —— 而那种球是真实存在的：
            // `ThrowSpawner._nextBallId` 在<b>域重载</b>（进 Play、退出 Play）后从 1 重新开始，
            // 而敌人可能是在重载之前生成的、`_lastBallId` 已经是个大数。
            // 那时一颗全新的球会被判成"我已经被它打过"，伤害被静默吃掉：
            // 敌人怎么砸都不掉耐久，而 Console 里连一条日志都没有。
            // <para>球编号只增不减，所以"不大于"就是"更旧或同一颗"，两者都该挡。</para>
            if (damage.BallId <= _lastBallId) return;

            _lastBallId = damage.BallId;

            _hp = Mathf.Max(0, _hp - 1);

            _slowMultiplier = damage.SlowMultiplier;

            _logic.SetSlowMultiplier(_slowMultiplier);
            _logic.ApplyKnockback(damage.Impulse, damage.Direction);

            // 禁足时长要带上步长：EnemyLogic 按"整帧"计数（见 IsStunned 的注释），
            // 而"秒 → 帧"的换算只有驱动方知道步长是多少。
            _logic.BeginStun(_config.StunSeconds, Time.fixedDeltaTime);

            if (_hp <= 0)
            {
                Die(damage.Direction);
                return;
            }

            // 数字当场刷新，不等下一次 Tick：受击与刷新之间隔着半个物理帧，
            // 那一帧里玩家看到的还是旧数字 —— 而"打中"这件事必须当帧可见。
            UpdateHpText();

            Debug.Log($"[白模] 敌人受损：剩余耐久 {_hp}/{MaxHp}");
        }

        /// <summary>
        /// 推进一个物理帧：算泥浆减速、刷视效，然后驱动逻辑层。
        /// </summary>
        /// <remarks>
        /// 由 <c>EnemyDirector</c> 调用，<b>不</b>用 <c>Update</c> ——
        /// 速度必须在一个物理帧里被提交一次，而不是每个渲染帧提交多次。
        /// <para>视效在物理帧刷而不是渲染帧刷：<b>颜色要跟逻辑层用的是同一份泥浆系数</b>
        /// （见 <see cref="_slowMultiplier"/> 的注释）。两者用不同频率更新就会出现一帧的不同步，
        /// 而那一帧正好是"泥浆刚消失"的时候。</para>
        /// </remarks>
        public void Tick(float now, float deltaTime)
        {
            if (_dead) return;

            // 先算"这一帧踩没踩在泥里"，再把它喂给逻辑层和视效 —— 同一个来源。
            _slowMultiplier = CurrentSlowFromMud();

            _logic.SetTarget(_target == null ? (Vector2?)null : TargetPosition());
            _logic.SetSlowMultiplier(_slowMultiplier);
            _logic.Tick(now, deltaTime);

            UpdateBodyColor();
        }

        /// <summary>选中时把接触判定半径画出来，便于对照 <see cref="ThrowConstants.ENEMY_CONTACT_RADIUS"/>。</summary>
        private void OnDrawGizmosSelected()
        {
            // 只在层级里选中它时才画：白模的敌人有好几只，全画会把场景视图糊满。
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, ThrowConstants.ENEMY_CONTACT_RADIUS);
        }

        private void BuildBody()
        {
            var body = gameObject.AddComponent<Rigidbody2D>();

            // 俯视角：没有"下落"。不关掉重力角色会一直往下掉。
            body.gravityScale = 0f;

            // 碰撞后不该被撞得打转（否则会带着旋转去撞墙，看起来像陀螺）。
            body.freezeRotation = true;

            // 白模的速度都在个位数，连续检测足够，不必开连续碰撞（那是给高速弹丸用的）。
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var collider = gameObject.AddComponent<CircleCollider2D>();
            collider.radius = _config.Radius;
        }

        private void BuildVisuals()
        {
            // 身体渲染器要留成字段：现在每帧按"闪不闪、踩不踩泥"改它的颜色。
            _body = gameObject.AddComponent<SpriteRenderer>();

            PrimitiveSprites.Configure(
                _body,
                PrimitiveSprites.Circle,
                ThrowConstants.ENEMY_BODY_COLOR,
                ThrowConstants.ENEMY_BODY_SORTING_ORDER,
                _config.Radius * 2f
                );

            BuildHpText();
            UpdateHpText();
            UpdateBodyColor();
        }

        /// <summary>
        /// 建敌人圆心的剩余耐久数字。
        /// </summary>
        /// <remarks>
        /// <b>为什么是 <c>TextMesh</c> 而不是血条或圆盘：</b>需求是"直接写数字"，而数字只有三条路 ——
        /// 导入字体资产（白模不导入美术）、引 TextMeshPro（白模不引依赖）、或自己拼 7 段笔画
        /// （约 60 行 + 自己排版）。<c>TextMesh</c> 是引擎自带的，三行代码。
        /// <para><b>位置与层级都跟着"读得出来"走：</b>数字压在敌人<b>圆心</b>
        /// （<see cref="ThrowConstants.ENEMY_HP_TEXT_OFFSET_Y"/> = 0），
        /// 排序层由 <see cref="ThrowConstants.ENEMY_HP_TEXT_SORTING_ORDER"/> 显式给定，
        /// 不依赖深度 —— 白模全是 z=0 的正交俯视，深度分不出先后。</para>
        /// <para>字体取不到时 <c>TextMesh</c> 会画成一堆方块。<b>所以这里不静默降级成"没字体的 TextMesh"</b>：
        /// 拿不到字体就干脆不建数字 —— 宁可"没有数字"，也不要"看起来像数字其实是豆腐块"。
        /// 但"拿不到"本身<b>也不报错</b>：数字只是读数，判定不依赖它，
        /// 而每一只敌人都报一条 Error 会把 Console 埋掉（见 <see cref="BuiltinFont"/>）。</para>
        /// </remarks>
        private void BuildHpText()
        {
            Font font = BuiltinFont();

            if (font == null) return;

            var go = new GameObject("耐久数字");

            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, ThrowConstants.ENEMY_HP_TEXT_OFFSET_Y, 0f);

            _hpText = go.AddComponent<TextMesh>();

            _hpText.font = font;
            _hpText.fontSize = 64;
            _hpText.characterSize = ThrowConstants.ENEMY_HP_TEXT_CHARACTER_SIZE;

            // 锚点居中是**配合偏移为 0** 用的：LowerCenter 会让数字从"给定位置"往上长，
            // 于是要把位置抬高一个偏移才落得回身体上；MiddleCenter 才是"给定的位置就是数字的中心"。
            _hpText.anchor = TextAnchor.MiddleCenter;
            _hpText.alignment = TextAlignment.Center;
            _hpText.color = Color.white;

            // 材质必须从字体上取：TextMesh 不会自己找，不给就是粉红方块。
            var textRenderer = _hpText.GetComponent<MeshRenderer>();

            textRenderer.sharedMaterial = font.material;

            // **sortingOrder 必须显式设。** TextMesh 走 MeshRenderer，而 MeshRenderer 和
            // SpriteRenderer 一样继承 Renderer、一样有 sortingOrder。白模全部是 z=0 的正交俯视，
            // 深度分不出先后 —— 不设这一句，数字就会和自己的身体抢先后（看起来像被挡掉或者闪）。
            // 曾经以为"MeshRenderer 没有 sortingOrder"，那是错的。
            textRenderer.sortingOrder = ThrowConstants.ENEMY_HP_TEXT_SORTING_ORDER;
        }

        /// <summary>
        /// 取一个能用的内置字体；一个都拿不到时返回 <c>null</c>（调用方跳过数字，不报错）。
        /// </summary>
        /// <remarks>
        /// <b>这不是"多写几个名字以防万一"，是修一个真实故障。</b>
        /// 2022.3 里 <c>Resources.GetBuiltinResource&lt;Font&gt;("Arial.ttf")</c> <b>会抛
        /// <c>ArgumentException</c></b>（"Arial.ttf is no longer a valid builtin font. Please use
        /// LegacyRuntime.ttf"），<b>而不是返回 <c>null</c></b> —— 所以原先那句
        /// "取不到字体就降级"的守卫永远到不了，它反而变成了每次生成敌人刷 5 条红错。
        /// 而 <c>BuildVisuals</c> 在 <c>Initialize</c> 里、即敌人刚被造出来时调用，
        /// 于是异常在每一只敌人的建体过程中抛出一次 —— Console 被刷满，真正的日志全被埋掉。
        /// <para>逐个名字 <c>try</c> 着试而不是只写一个名字：内置字体的可用名字<b>跨版本会变</b>
        /// （这正是本次故障的成因），而"哪个名字在哪个版本有效"这件事不该由白模的实现来断言。
        /// 试不出来的结果就是"没有数字"，而数字<b>只是读数</b> —— 耐久判定、颜色、击退都不依赖它，
        /// 所以这里静默跳过是正确的降级，不该报错。</para>
        /// </remarks>
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

        /// <summary>
        /// 内置字体的候选名，按"新版 → 旧版"排。
        /// </summary>
        /// <remarks>
        /// <c>LegacyRuntime.ttf</c> 在前：它是 2022.3 报错信息里点名的那个名字（也就是官方给的替代），
        /// 而且直接命中它就不必先吃一次异常。<c>Arial.ttf</c> 留着是给旧版本用的 ——
        /// 两个名字都试的组合在 2019～2022 各版本上都能拿到字体。
        /// </remarks>
        private static readonly string[] BuiltinFontNames =
        {
            "LegacyRuntime.ttf",
            "Arial.ttf",
        };

        /// <summary>刷新头顶数字。<b>只在真的变了才写</b>：给 <c>TextMesh.text</c> 赋值会重建网格。</summary>
        private void UpdateHpText()
        {
            if (_hpText == null) return;

            string text = EnemyVisual.HpText(_hp);

            if (_hpText.text == text) return;

            _hpText.text = text;
        }

        /// <summary>
        /// 刷新身体颜色：泥浆减速变深、被砸中闪烁，两者可叠加。
        /// </summary>
        /// <remarks>
        /// 闪烁相位用 <c>Time.time</c> 而不是自己累加：累加出来的相位会随帧率漂，
        /// 而"闪了几下"是玩家会数的东西。
        /// <para>判"该不该闪"用 <c>IsStunned</c>：闪烁与禁足是同一件事的两面
        /// （<see cref="ThrowConstants.ENEMY_STUN_DURATION"/> 到了，闪与禁足一起结束），
        /// 另立一个计时器只会让两者悄悄不同步。</para>
        /// </remarks>
        private void UpdateBodyColor()
        {
            if (_body == null) return;

            bool flashOn = _logic != null && _logic.IsStunned && EnemyVisual.IsFlashOn(Time.time);

            _body.color = EnemyVisual.BodyColor(_slowMultiplier, flashOn);
        }

        private void Die(Vector2 hitDirection)
        {
            _dead = true;

            // 速度交给碎片：尸体自己不该继续滑（它这一帧还在被物理推动，看起来像"死掉的敌人还在走"）。
            if (_motor != null) _motor.Move(Vector2.zero);

            var go = new GameObject("碎裂");

            // 保持世界坐标：碎片的初始位置与层级无关。
            go.transform.SetParent(null, true);

            var burst = go.AddComponent<ShatterBurst>();

            burst.Initialize(transform.position, hitDirection, ThrowConstants.ENEMY_BODY_COLOR);

            Debug.Log($"[白模] 敌人被打碎（打了 {MaxHp} 下）");

            Destroy(gameObject);
        }

        /// <summary>
        /// 本帧的泥浆减速系数。
        /// </summary>
        /// <remarks>
        /// 每次查询都 <c>FindObjectsOfType</c>：白模不引缓存，因为"缓存什么时候失效"本身就是
        /// 一个需要维护的状态（泥浆每 8 秒增删一批）。数量在个位数时这点开销可以忽略，
        /// 而缓存失效的正确性在同步上要花几十行 —— 不值。
        /// </remarks>
        private float CurrentSlowFromMud()
        {
            MudPatch[] patches = FindObjectsOfType<MudPatch>();

            return MudPatch.StrongestSlow(transform.position, patches);
        }

        private Vector2 TargetPosition()
        {
            Vector3 p = _target.position;

            return new Vector2(p.x, p.y);
        }
    }
}
