namespace DeepseaOil.Prototype
{
    /// <summary>
    /// 白模投掷的全部数值。**所有可调参数只能写在这里**，不允许散落在实现里。
    /// </summary>
    /// <remarks>
    /// 白模是临时验证物，数值不走 Luban（<c>ConfigWorkspace/</c>），也不进 <c>PlayerConfig</c> SO——
    /// 那样每次调一个数字都要导表或点资产，而白模的价值恰恰是"改个数、按 Play、看手感"。
    /// <para><b>命名契约：</b>下面「策划给定」一段的名字与数值与需求书《白模需求方案》第七节<b>逐字一致</b>，
    /// 不要重命名、不要合并。调参请改值，不要改名——名字是给策划对照表格用的。</para>
    /// <para>「后续阶段」一段在本阶段只有声明，没有消费者（泥浆、敌人、波次都还没做）。
    /// 提前声明的唯一理由是：让第二阶段只做功能，不回来动这份文件。</para>
    /// </remarks>
    public static class ThrowConstants
    {
        // ============================================================
        // 策划给定（需求书第七节，名字与值均不可改）
        // ============================================================

        /// <summary>球飞行总时长（秒）。这是"最远一投"的时长，近投按距离线性缩短，见 <see cref="BallData"/>。</summary>
        public const float FLIGHT_DURATION = 0.6f;

        /// <summary>抛物线视觉最高点（世界单位）。</summary>
        public const float MAX_HEIGHT = 2.0f;

        /// <summary>指示器离玩家的最大距离（世界单位）。</summary>
        public const float MAX_THROW_DISTANCE = 5.0f;

        /// <summary>水球色。</summary>
        public static readonly UnityEngine.Color WATER_BALL_COLOR = new UnityEngine.Color(0.20f, 0.55f, 1.00f, 1f);

        /// <summary>土球色。</summary>
        public static readonly UnityEngine.Color EARTH_BALL_COLOR = new UnityEngine.Color(0.55f, 0.36f, 0.18f, 1f);

        /// <summary>泥浆持续（秒）。后续模块：水球落地留泥浆圈。</summary>
        public const float MUD_DURATION = 8f;

        /// <summary>敌人耐久。后续模块：三下被打碎。</summary>
        public const int ENEMY_HP = 3;

        /// <summary>每波敌人数。后续模块：波次循环。</summary>
        public const int ENEMY_COUNT_PER_WAVE = 4;

        // ============================================================
        // 本阶段实数（需求书未给，由实现定；全部集中在此便于调参）
        // ============================================================

        /// <summary>
        /// 出手点相对地面的抬高量（世界单位）。**只影响视觉，不参与落点与飞行时长。**
        /// </summary>
        /// <remarks>
        /// 刻意选择，不是随手加的偏移：不抬高的话，起手的球会从玩家身体里钻出来。
        /// <para><b>它不写进 <see cref="BallData.Start"/> / <see cref="BallData.End"/></b> —— 那两个值
        /// 是"贴地的逻辑位置"，阴影贴的就是它们。抬高量只在画球时叠加上去（<c>BallDriver.ApplyAt</c>）。
        /// 曾经把抬高量烘进 Start/End，结果是阴影也跟着往上跑：阴影看起来"飘在球下面一点"、
        /// 还跟着抛物线上下起伏，而不是贴在地上走直线。</para>
        /// <para>代价是最高点变成 <c>MAX_HEIGHT + THROW_ORIGIN_HEIGHT</c>（2.2）。TestPhysics 的相机
        /// ortho size = 5（半高 5），2.2 仍在屏内 —— 这个数字与相机尺寸绑定，换相机后要重新对一遍。</para>
        /// </remarks>
        public const float THROW_ORIGIN_HEIGHT = 0.3f;

        /// <summary>
        /// 阴影/指示器的贴地微偏移（世界单位）。
        /// </summary>
        /// <remarks>
        /// 拉到 0 以下一点点，让它们稳稳压在地面装饰之上。取 <c>-0.01</c> 而不是 <c>-0.1</c>：
        /// 俯视角下视觉上分辨不出来，但足以避免和地面 sprite 抢 z 顺序。
        /// </remarks>
        public const float GROUND_VISUAL_OFFSET = -0.01f;

        /// <summary>
        /// 落点与出手点的最小距离（世界单位）。
        /// </summary>
        /// <remarks>
        /// 鼠标压在玩家身上时落点会等于出手点，那时方向向量为零、归一化产生 NaN，球要么不动要么闪成
        /// 一个非数坐标。给一个下限比到处判 NaN 便宜，顺带让"贴脸投"看起来像往脚前扔，而不是原地炸自己。
        /// </remarks>
        public const float MIN_THROW_DISTANCE = 0.4f;

        /// <summary>落地冲量强度：<b>相当于给质量为 1 的刚体增加的速度</b>（单位/秒），不是加速度。</summary>
        public const float IMPULSE_STRENGTH = 6f;

        /// <summary>落地冲量生效半径（世界单位）。</summary>
        public const float IMPULSE_RADIUS = 1.2f;

        /// <summary>
        /// 冲量强度换算成动量时的质量下限。
        /// </summary>
        /// <remarks>
        /// 冲量是"动量"而不是"速度"：同一个 <see cref="IMPULSE_STRENGTH"/> 给轻物更大的速度。
        /// 但质量趋于 0 时速度趋于无穷，所以按这个下限折算 —— 效果是"轻物最多获得 1 倍强度的速度"。
        /// </remarks>
        public const float IMPULSE_MASS_FLOOR = 0.2f;

        /// <summary>球本体的视觉半径（世界单位）。</summary>
        public const float BALL_RADIUS_METERS = 0.22f;

        /// <summary>阴影的视觉半径（世界单位）。</summary>
        public const float SHADOW_RADIUS_METERS = 0.20f;

        /// <summary>
        /// 指示器的<b>下沿</b>半径（世界单位），也是 sprite 的基准尺寸（sprite 直径 = 半径 × 2）。
        /// </summary>
        public const float AIM_RADIUS_METERS = 0.32f;

        /// <summary>
        /// 指示器竖直方向的压扁比例（1 = 不压扁）。
        /// </summary>
        /// <remarks>
        /// <b>注意 TestPhysics 的相机是纯俯视正交（<c>orthographic: 1</c>，旋转全零），
        /// 严格来说这种相机下地面上的圆投影出来还是正圆。</b>压扁是<b>风格选择</b>，
        /// 不是透视模拟：游戏里常见"地面标记画扁一点"的惯例，读起来更像贴在地上的圈。
        /// 要真实透视就得把相机改成有俯角 + 透视投影，那是另一个层面的决定。
        /// <para><c>1</c> = 不压扁（正圆）。想做出"摊在地上"的观感，往下调到 <c>0.5~0.7</c>。</para>
        /// </remarks>
        public const float AIM_VERTICAL_SQUASH = 0.6f;

        /// <summary>
        /// 指示器<b>上半弧</b>比下半弧再收窄的比例（0 = 上下对称的椭圆，即"视平线在正中"）。
        /// </summary>
        /// <remarks>
        /// 这就是"视平线不在屏幕中心"那件事的旋钮：它让上半弧更平、下半弧更圆，
        /// 于是视觉重心落到下半部，像一只手电筒斜照在地上的圈。
        /// <para><b>它是加在 <see cref="AIM_VERTICAL_SQUASH"/> 之上的</b>，两者独立：
        /// 竖直方向最终有两个半径 —— 下半弧是 <c>半径 × squash</c>，
        /// 上半弧是 <c>半径 × squash × (1 − taper)</c>。所以要先有压扁（squash &lt; 1），
        /// taper 才看得出"透视"；squash = 1 且 taper = 0 时就是一个正圆。</para>
        /// <para><b>为什么不产生断点：</b>两半各自是标准椭圆，在左右最宽点 <c>(±半径, 0)</c> 相接，
        /// 而那一点的切线对任何椭圆都是竖直的 → 接缝处切向连续。
        /// 曾经用"竖直半径随 x 变化"的写法，那种画法在几何上不是椭圆，左右会被扭出豁口
        /// （实测逐行宽度掉到 0），已废弃。</para>
        /// <para><c>0.10~0.20</c> 是"能看出来但不夸张"的量。</para>
        /// </remarks>
        public const float AIM_PERSPECTIVE_TAPER = 0.15f;

        /// <summary>指示器环的厚度，占半径的比例。越大环越粗。</summary>
        public const float AIM_RING_THICKNESS = 0.2f;

        /// <summary>
        /// 所有<b>贴地</b>圆环共用的线宽（<b>世界单位</b>，不是比例）。
        /// </summary>
        /// <remarks>
        /// 落点指示器与落地瞬闪都走 <c>PrimitiveSprites.GroundDiscOrRing</c>，环厚统一取这个值，
        /// 所以两者的线看起来一样粗。
        /// <para>用世界单位而不是比例，是因为两者半径差了三倍多（<see cref="AIM_RADIUS_METERS"/> ≈ 0.32
        /// 对 <see cref="IMPULSE_RADIUS"/> = 1.2）：按比例给的话，同一份观感在瞬闪上会变成一圈粗边。</para>
        /// </remarks>
        public const float GROUND_RING_THICKNESS_METERS = 0.07f;

        /// <summary>球到最高点时的阴影缩放（相对落地时）。球越高阴影越小，是"高度"唯一的视觉线索。</summary>
        public const float SHADOW_SCALE_AT_PEAK = 0.7f;

        /// <summary>取点用贴图的边长（像素）。运行期生成，不落盘。</summary>
        public const int POINT_TEXTURE_SIZE = 64;

        /// <summary>圆点 sprite 的像素密度：写死是为了让"运行期生成的 sprite"与 localScale 的换算稳定。</summary>
        public const float POINT_PIXELS_PER_UNIT = 64f;

        /// <summary>球与阴影的排序层。取 1000 是为了盖过一切场景装饰（当前工程里没有任何 sortingOrder，全默认 0）。</summary>
        public const int BALL_SORTING_ORDER = 1000;

        /// <summary>指示器的排序层。<b>必须小于球与阴影</b>：指示器画在球下面，不该盖住球。</summary>
        public const int AIM_SORTING_ORDER = 900;

        /// <summary>落地瞬闪的排序层。比球再高一层，落地那一下才不会被球本体的残影盖住。</summary>
        public const int LANDING_FLASH_SORTING_ORDER = 1100;

        /// <summary>
        /// 白模视效件所在的 layer。
        /// </summary>
        /// <remarks>
        /// 用 <c>0</c>（Default）：白模没有自己的层，也不该为了白模去改 <c>TagManager.asset</c>
        /// ——那会变成一处需要人工同步的工程设置。层内先后由 <c>sortingOrder</c> 决定，
        /// 三个排序层常量之间差 100，够插。
        /// </remarks>
        public const int OVERLAY_LAYER = 0;

        /// <summary>
        /// 落地瞬闪的持续时间（秒）。
        /// </summary>
        /// <remarks>
        /// 两个环共用它：外圈（效果范围）比内圈晚 <see cref="LANDING_FLASH_SECONDARY_DELAY"/> 出现，
        /// 但两者同时结束。
        /// </remarks>
        public const float LANDING_FLASH_DURATION = 0.15f;

        /// <summary>
        /// 落地瞬闪的外圈比内圈晚出现的时长（秒）。
        /// </summary>
        /// <remarks>
        /// <b>为什么需要它：</b>两个同心环如果同时出现，看起来就是**一个更粗的环** ——
        /// 于是"内圈是击退范围、外圈是泥浆范围"这件事反而读不出来。
        /// 错开一点点之后，先看到击退圈，再看到外圈铺开，两层意思都清楚了。
        /// <para>取 0.05 而不是更久：总时长只有 0.15，错开太久外圈就没时间被看清了。</para>
        /// </remarks>
        public const float LANDING_FLASH_SECONDARY_DELAY = 0.05f;

        /// <summary>
        /// 落地瞬闪<b>外圈</b>的颜色。
        /// </summary>
        /// <remarks>
        /// 灰白、与球种无关：外圈标的是"作用范围"，而"是哪颗球"已经由内圈的颜色表达了。
        /// 两圈同色会糊成一片，读不出是两层。
        /// </remarks>
        public static readonly UnityEngine.Color LANDING_FLASH_OUTER_COLOR = new UnityEngine.Color(0.90f, 0.90f, 0.90f, 1f);

        /// <summary>
        /// 把鼠标屏幕点投到世界平面时给的相机深度（世界单位）。
        /// </summary>
        /// <remarks>
        /// 正交相机下这个值只用于把"屏幕点"还原到某个平面上，给足量正值即可。不读
        /// <c>Camera.main.transform.position.z</c> 是有意的：那被 Cinemachine 每帧驱动，
        /// 依赖它等于让落点计算跟着相机插件走。
        /// </remarks>
        public const float CAMERA_PLANE_DEPTH = 100f;

        // ============================================================
        // 第二阶段：敌人、泥浆、玩家血量
        // ============================================================
        //
        // 与上面「本阶段实数」同一条规矩：可调数字只写在这里。敌人不新增第二种配置来源
        // （不走 Luban、也不新建 ScriptableObject）—— 那样每调一个数都要导表或点资产，
        // 而白模的价值恰恰是"改个数、按 Play、看手感"。

        /// <summary>敌人视觉半径（世界单位）。</summary>
        /// <remarks>
        /// 略小于玩家的碰撞盒半宽（0.7 × 0.5）：贴上来的时候看起来像敌人"压在"玩家身上，
        /// 而不是两个方块并排。碰撞体是正圆，半径也是它。
        /// </remarks>
        public const float ENEMY_RADIUS_METERS = 0.45f;

        /// <summary>敌人追击速度（单位/秒）。</summary>
        /// <remarks>
        /// 玩家 <c>moveSpeed = 8</c> 的 45%。刻意选在"追得上但追不紧"之间：
        /// 玩家停下来才会被贴上，一跑就能拉开 —— 这正是白模要验的"能不能甩掉"。
        /// <para>冲刺是 25，是它的 7 倍：最后一段路随时能甩开。</para>
        /// </remarks>
        public const float ENEMY_SPEED = 3.6f;

        /// <summary>敌人加速度（单位/秒²）—— 由 <c>ActorLogic.SteerTowards</c> 消费。</summary>
        /// <remarks>
        /// 0 → 满速约 0.26 秒。选这个量级是为了"起步看得出来"：零惯性角色（玩家）是当帧满速，
        /// 敌人如果也当帧满速，被击退后的"挣扎着追回来"就完全看不到了。
        /// </remarks>
        public const float ENEMY_ACCELERATION = 14f;

        /// <summary>
        /// 敌人击退滑行的衰减率（1/秒）—— 由 <c>ActorLogic.SteerTowards</c> 消费。
        /// </summary>
        /// <remarks>
        /// 每秒钟衰减到 1/e。击退距离 ≈ <see cref="ENEMY_KNOCKBACK_IMPULSE"/> / 本值，
        /// 所以它是"滑多远"的旋钮：调小 = 滑得更远（像冰面），调大 = 一推就停。
        /// <para><b>为什么必须是指数衰减而不是匀减速：</b>匀减速走到底会有一个"突然停住"的折点，
        /// 而指数衰减越滑越慢、自然停稳。</para>
        /// </remarks>
        public const float ENEMY_KNOCKBACK_DECAY = 10f;

        /// <summary>敌人停止逼近的距离（世界单位）。</summary>
        /// <remarks>
        /// 进入这个距离后目标速度归零，敌人停住而不是继续往玩家身上挤。
        /// 没有它的话敌人会一直给玩家刚体施加接触推力，表现为"贴着玩家抖动"。
        /// <para><b>必须严格小于 <see cref="ENEMY_CONTACT_RADIUS"/>，而且间隔要留得出来。</b>
        /// 这一条是<b>真的踩过</b>的：击退会把敌人推开，如果"停止距离"离"接触距离"太近，
        /// 敌人被推出去之后仍然落在停止距离之内，就<b>再也不会主动压回来</b> ——
        /// 现象是"被撞一次之后敌人黏在原地，玩家再也挨不到第二下"，
        /// 不报错、也不像 bug，只是整局只掉一次血。</para>
        /// <para>间隔取 0.4（接触 1.0 − 停止 0.6）：每次被撞开之后敌人都有一段"重新压上来"的过程，
        /// 而那正好落在 <see cref="PLAYER_INVULNERABLE_DURATION"/> 的窗口里。</para>
        /// </remarks>
        public const float ENEMY_STOP_DISTANCE = 0.6f;

        /// <summary>
        /// 敌人放弃追击的距离（世界单位）。
        /// </summary>
        /// <remarks>
        /// 半张地图的对角线（地图是 40×25）。实际战斗里永远不会超出 —— 这一条是**有界**而不是
        /// "永远追"：有了它，"追到地图另一头"这件事是可以被否定的，也让白模里能直接试出来
        /// "跑得足够远就能脱离"。
        /// </remarks>
        public const float ENEMY_CHASE_RANGE = 60f;

        /// <summary>
        /// 判定"敌人贴上了玩家"的圆心距（世界单位）。
        /// </summary>
        /// <remarks>
        /// 接触伤害只在 <c>FixedUpdate</c> 里定性判定（圆心距），不读接触点与法线：
        /// 玩家与敌人都只有一个圆/方碰撞体，判法与结论完全一致，而定性判法能用 EditMode 测试直接喂坐标。
        /// <para>取 1.0 略小于两者碰撞半径之和（0.45 + 0.35），所以是"几乎贴在身上"而不是
        /// "擦到就算" —— 玩家的碰撞盒半宽是 0.35。</para>
        /// </remarks>
        public const float ENEMY_CONTACT_RADIUS = 1.0f;

        /// <summary>
        /// 敌人被砸中后的<b>禁足帧数</b>（整物理帧）。<b>这才是"禁足多久"的真值。</b>
        /// </summary>
        /// <remarks>
        /// <see cref="ENEMY_STUN_DURATION"/> 只是它按固定步长（0.02 秒）换算出来的秒数说法，
        /// 给策划与文档看；<b>两者的真值是这一个</b>。
        /// <para><b>为什么"秒数"不能当真值：</b>"禁足还剩多久"是离散量（玩家数的是帧），
        /// 而秒数倒计时把它表示成连续量、每帧减一个 Δt。余数落在 0 与一个 Δt 之间时，
        /// <c>&gt; 0f</c> 与 <c>&gt; Δt</c> 会给出相差一帧的答案，两个方向都不对。
        /// 整数计数没有这个自由度，也不需要任何容差。</para>
        /// <para><b>但帧数对不上还有第二个原因，而且那个才是真正踩到的：递减的位置。</b>
        /// 曾经在帧首递减（<c>OnTick</c> 之前读），于是 <c>BeginStun(12)</c> 实际只禁足 11 帧 ——
        /// 这与浮点毫无关系（<c>0.24f / 0.02f</c> 单精度下正好是 <c>12</c>，残差为零），
        /// 纯粹是"在谁之前减"的顺序问题。修在 <c>EnemyLogic.Tick</c>：递减放到帧末。</para>
        /// <para>白模在这一处连错两次，教训是：<b>帧数对不上时先看计数在哪里被改，
        /// 不要先怀疑浮点。</b></para>
        /// </remarks>
        public const int ENEMY_STUN_FRAMES = 12;

        /// <summary>
        /// 敌人被砸中后的禁足时长（秒）。= <see cref="ENEMY_STUN_FRAMES"/> × 0.02。
        /// </summary>
        /// <remarks>
        /// <b>保留秒数只是为了"调参看手感"时读着直观，它不是真值。</b>
        /// 改禁足时长请改 <see cref="ENEMY_STUN_FRAMES"/>，然后把这个数同步成同一个值 ——
        /// 两者不一致不会报错，只会让文档与实现悄悄分家。
        /// <para>这段时间里敌人<b>既不转向也不衰减</b>（见 <c>EnemyLogic.OnTick</c> 的禁足分支），
        /// 所以冲量原样保留、由引擎把它推出去。这就是"被打飞的位移"：
        /// <c>ENEMY_KNOCKBACK_IMPULSE × 0.24 = 5.5 × 12 × 0.02 = 1.32 米</c>
        /// （禁足结束后惯性还在，玩家实际感受到的脱离距离约 3.5 米，见 W18 的上限断言）。</para>
        /// <para><b>旧版是 0.08 秒、而且"停顿期间照旧衰减"</b>：0.08 秒内每帧乘 0.819，
        /// 结束时速度只剩 2.47、总位移约 0.55 米 —— 看起来就像"击退根本没生效"。
        /// **不是冲量不够，是被停顿衰减掉了。**</para>
        /// <para>调大 ⇒ 打得飞更远更久；调到 0.5 秒以上会开始像"被打晕"，那是另一套机制（控制效果）。</para>
        /// </remarks>
        public const float ENEMY_STUN_DURATION = 0.24f;

        /// <summary>
        /// 敌人身体颜色。
        /// </summary>
        /// <remarks>
        /// 偏暖的红。与玩家（黄 <c>0.95, 0.85, 0.35</c>）、水球（蓝）、土球（棕）都能一眼分开。
        /// </remarks>
        public static readonly UnityEngine.Color ENEMY_BODY_COLOR = new UnityEngine.Color(0.86f, 0.30f, 0.28f, 1f);

        /// <summary>
        /// 敌人踩在泥浆里时的身体颜色（比 <see cref="ENEMY_BODY_COLOR"/> 明显更深）。
        /// </summary>
        /// <remarks>
        /// <b>它是一个独立常量，而不是"代码里乘个 0.4"。</b>乘出来的中间色没法断言、也没法单独调 ——
        /// 而"减速要看得出来"这件事在白模里是**唯一**的减速反馈（没有 buff 图标、没有特效）。
        /// <para>取 <see cref="ENEMY_BODY_COLOR"/> 的约 0.4 倍：亮度差看得出来，但仍读得出是同一个敌人
        /// （色相没变，只是压暗）。W17 会断言它的亮度严格低于身体色。</para>
        /// </remarks>
        public static readonly UnityEngine.Color ENEMY_SLOW_BODY_COLOR = new UnityEngine.Color(0.34f, 0.12f, 0.11f, 1f);

        /// <summary>
        /// 敌人被砸中时的闪烁频率（Hz）。
        /// </summary>
        /// <remarks>
        /// 一个周期是"亮 → 暗 → 亮"，所以每个状态持续 <c>1 / (2 × 本值)</c> 秒。
        /// 禁足 0.25 秒内正好走完一个完整周期。
        /// <para><b>不能调太高：</b>60 fps 下每个状态只剩 <c>30 / 本值</c> 帧 ——
        /// 取 8 就只有 3~4 帧，看起来是"闪频"（像显示器坏了）而不是"被砸了一下"。
        /// W17 把它上限定在 10。</para>
        /// </remarks>
        public const float ENEMY_FLASH_HZ = 4f;

        /// <summary>
        /// 敌人耐久数字相对敌人中心的<b>垂直</b>偏移（世界单位）。
        /// </summary>
        /// <remarks>
        /// <b>0 = 正中心</b>，也就是数字压在敌人圆形的中央。
        /// <para>这里曾经是 <c>0.34</c>（"压在身体上沿"），那是配合 <c>TextMeshAnchor.LowerCenter</c>
        /// 用的：锚点在下沿时，数字从 <c>位置 + 偏移</c> 往上长，所以必须给一个抬高量才落回身体上。
        /// 锚点改成 <c>MiddleCenter</c> 之后，数字以给定位置为<b>中心</b>摆放，偏移就该是 0 ——
        /// 留着 0.34 会让数字飘到身体上方（正是改之前看到的样子）。</para>
        /// </remarks>
        public const float ENEMY_HP_TEXT_OFFSET_Y = 0f;

        /// <summary>
        /// 敌人耐久数字的字号（<c>TextMesh.characterSize</c>，世界单位量级）。
        /// </summary>
        /// <remarks>
        /// 敌人身体直径是 <c>2 × 0.45 = 0.9</c> 米，而 <c>characterSize</c> 约等于字高 ——
        /// 0.13 让数字占圆直径的约 1/7，缩到 0.5 倍屏幕时仍然读得出，也不会盖满身体。
        /// <para>它和 <c>TextMesh.fontSize</c> 是两件事：<c>fontSize = 64</c> 是<b>烘焙进图集的字号</b>
        /// （越大越清晰，不改显示大小），<c>characterSize</c> 才是显示大小。
        /// 曾经取 0.06（"≈0.24 米高"）—— 那是按"两行"估的，而数字只有一位，
        /// 实际远小于估算值，在屏幕上几乎看不清。</para>
        /// </remarks>
        public const float ENEMY_HP_TEXT_CHARACTER_SIZE = 0.13f;

        /// <summary>
        /// 土球砸中敌人时给的冲量（<b>动量</b>，不是速度）。
        /// </summary>
        /// <remarks>
        /// <b>它换算出来的<b>位移</b>由 <see cref="ENEMY_STUN_DURATION"/> 决定，不是由本值 ÷ 衰减率决定</b>
        /// （那是旧规则）：禁足期间不衰减，所以位移 ≈ 本值 × 禁足时长 = 5.5 × 0.25 ≈ 1.4 米，
        /// 加上禁足结束后的反向惯性合计约 1.8 米。
        /// <para><b>为什么不复用 <see cref="IMPULSE_STRENGTH"/>：</b>那个是"给任意质量的物理体的通用强度"，
        /// 单位与口径都不同（它按质量折算成动量）。这个只作用于<b>有速度账本的角色</b>，
        /// 两者将来会被分开调 —— 合成的早期最容易被"顺手复用一个常量"绑死。</para>
        /// <para>质量是 1（<c>Rigidbody2D</c> 默认），所以"动量"与"速度"数值相同；
        /// 但账本走的是 <c>AddImpulse</c>（直接加速度），所以改质量<b>不会</b>改击退距离 ——
        /// 要改距离请改本值或 <see cref="ENEMY_STUN_DURATION"/>。</para>
        /// </remarks>
        public const float ENEMY_KNOCKBACK_IMPULSE = 5.5f;

        /// <summary>
        /// 水球砸中敌人的击退强度，相对 <see cref="ENEMY_KNOCKBACK_IMPULSE"/> 的比例。
        /// </summary>
        /// <remarks>
        /// 取 1/3：土球打退、水球留泥 —— 区分靠<b>幅度</b>而不是"有或没有"，
        /// 水球因此仍有打击感，但把位置优势让给土球。取 0 会让水球打上去像没打中。
        /// </remarks>
        public const float WATER_KNOCKBACK_SCALE = 1f / 3f;

        /// <summary>泥浆圈半径（世界单位）。</summary>
        /// <remarks>
        /// 比球本体（0.22）与冲量半径（<see cref="IMPULSE_RADIUS"/> = 1.2）都大一圈：
        /// 它是水球落地"渗开的一片"，不是球的精确落点，视觉上偏大才对。
        /// </remarks>
        public const float MUD_RADIUS_METERS = 1.8f;

        /// <summary>
        /// 踩在泥浆里的速度系数（1 = 不受影响，0 = 完全定死）。
        /// </summary>
        /// <remarks>
        /// 0.45：敌人降到 1.62 单位/秒，比玩家慢到五分之一。
        /// <para><b>不做叠加乘算</b>（三片泥浆各自相乘会变成 1.5% 速度 = 几乎不动）：
        /// 重叠时取<b>最强</b>的一档，见 <c>MudPatch.StrongestSlow</c>。</para>
        /// </remarks>
        public const float MUD_SLOW_FACTOR = 0.45f;

        /// <summary>泥浆颜色。</summary>
        public static readonly UnityEngine.Color MUD_COLOR = new UnityEngine.Color(0.30f, 0.20f, 0.10f, 0.55f);

        /// <summary>
        /// 玩家血上限。
        /// </summary>
        /// <remarks>
        /// 与 <see cref="PLAYER_CONTACT_DAMAGE"/> 一起决定"能挨几下" = 10 下。
        /// 用 100 而不是 3 是为了让血条在整局里都有信息量，也给将来"不同伤害值的敌人"留出区分度。
        /// </remarks>
        public const float PLAYER_MAX_HP = 100f;

        /// <summary>敌人贴身一次造成的伤害。</summary>
        public const float PLAYER_CONTACT_DAMAGE = 10f;

        /// <summary>
        /// 玩家受击后的无敌时长（秒）。
        /// </summary>
        /// <remarks>
        /// 0.8 秒：它是"能不能从敌人手里逃出来"的唯一旋钮。太短（0.2）会让 4 只敌人把玩家锁死
        /// 连打十下，太长（2.0）会让玩家觉得敌人贴上来什么也没发生。
        /// <para><b>接触伤害是"贴住就重复结算"的，必须靠无敌帧挡</b>：没有它，每帧都会扣 10 点，
        /// 十帧内打空。而且因为 <c>FixedUpdate</c> 是 50 Hz，0.8 秒正好是 40 帧。</para>
        /// </remarks>
        public const float PLAYER_INVULNERABLE_DURATION = 0.8f;

        /// <summary>
        /// 玩家受击被推开时允许达到的速度上限（单位/秒）。
        /// </summary>
        /// <remarks>
        /// <b>它决定"被推开多远"，与球砸地面的力度是两件事。</b>白模里 <c>PlayerHealth</c> 每帧声明
        /// "本帧上限 = 本值"，并把 <see cref="PLAYER_KNOCKBACK_IMPULSE"/> 加进速度账本 ——
        /// 两者相等时玩家被推到本值，然后按 <c>SteerTowards</c> 的指数衰减滑停。
        /// <para><b>为什么不复用 <see cref="IMPULSE_STRENGTH"/>：</b>那个数是"给无生命物理体的冲量强度"，
        /// 语义是"砸一下有多猛"。玩家的受击是另一件事（受击手感的旋钮），
        /// 两者共用一个数会让"想让球把箱子推更远"顺带改掉玩家被撞飞的距离。
        /// 早期最容易被"顺手复用一个常量"绑死。</para>
        /// <para>12 ≈ 玩家移动速度 8 的 1.5 倍：被撞飞比跑步快，但比冲刺 25 慢 ——
        /// 所以"被撞开"的感觉明显，却不会让玩家觉得失去了控制。</para>
        /// </remarks>
        public const float PLAYER_KNOCKBACK_SPEED_LIMIT = 12f;

        /// <summary>
        /// 玩家受击被推开的冲量（<b>速度</b>，单位/秒）。
        /// </summary>
        /// <remarks>
        /// <b>取与 <see cref="PLAYER_KNOCKBACK_SPEED_LIMIT"/> 相等——这是刻意的，别只改这一个。</b>
        /// 玩家这一侧走的是 <c>PlayerLogic.AddImpulse</c> ＋ "当帧速度上限 = 本值"，
        /// 也就是"往上限里补，补满为止"：冲量小于上限时补不满，表现会变成
        /// "被撞了一下几乎不动"（而且不报错，只是手感不对）。
        /// <para>想改"被推开多远"改 <see cref="PLAYER_KNOCKBACK_SPEED_LIMIT"/>，两个一起改。</para>
        /// </remarks>
        public const float PLAYER_KNOCKBACK_IMPULSE = 12f;

        /// <summary>玩家白模色块的半径（世界单位）。只画一次，不跟随。</summary>
        public const float PLAYER_BODY_RADIUS_METERS = 0.5f;

        /// <summary>玩家白模色块的颜色；与场景里玩家精灵同色，方便把两者对上。</summary>
        public static readonly UnityEngine.Color PLAYER_BODY_COLOR = new UnityEngine.Color(0.95f, 0.85f, 0.35f, 1f);

        /// <summary>
        /// 玩家死后到重置之间的停顿（秒）。
        /// </summary>
        /// <remarks>
        /// 不是"死亡动画"，是让"打空了"这件事**看得出来**：没有这一拍，玩家会在同一帧被清场重置，
        /// 现象变成"敌人突然全都消失、我莫名回到了原点"。
        /// </remarks>
        public const float PLAYER_RETRY_DELAY = 1.2f;

        /// <summary>敌人被打碎时飞出的碎片数。</summary>
        /// <remarks>
        /// 3 块就够读出"碎了"：这是一个读数，不是特效。碎片几何形状由 <c>ShatterBurst.Initialize</c>
        /// 的确定性算法给出（均匀扇形 + 固定俯仰系数），<b>不读随机数</b> —— 随机数会让白模的
        /// 表现不可复现，而白模的问题排查全靠"同一个现象能不能再出现一次"。
        /// </remarks>
        public const int SHATTER_PIECE_COUNT = 3;

        /// <summary>碎片半径（世界单位）。</summary>
        public const float SHATTER_PIECE_RADIUS_METERS = 0.13f;

        /// <summary>碎片飞行速度（单位/秒）。</summary>
        public const float SHATTER_SPEED = 3.5f;

        /// <summary>碎片扇形的总张角（度）。</summary>
        public const float SHATTER_SPREAD_DEGREES = 140f;

        /// <summary>碎片从出现到消失的总时长（秒）。</summary>
        public const float SHATTER_DURATION = 0.35f;

        /// <summary>
        /// 敌人在玩家周围这个半径上生成（世界单位）。
        /// </summary>
        /// <remarks>
        /// 5 —— 正好是相机 ortho size 的一半宽度（视野 10×10），所以敌人一开始就在视野边缘偏内，
        /// **看得见**。选"看得见"而不是"从屏外走进来"，是因为白模的敌人本来就是"看得见的靶子"：
        /// 从屏外走近的话，前 2 秒玩家什么都看不到，无法判断它到底有没有生成。
        /// </remarks>
        public const float CHASE_SPAWN_RADIUS = 5f;

        /// <summary>开局到第一波之间的等待（秒）。</summary>
        /// <remarks>
        /// 给玩家一个"看清楚场上有什么"的窗口，也对上需求书里的备战段。
        /// </remarks>
        public const float CHASE_INITIAL_DELAY = 1.5f;

        /// <summary>清完一波到下一波之间的等待（秒）。</summary>
        /// <remarks>
        /// <b>波次循环是刻意做的</b>：它是"每波敌人数 = 4"这条常量唯一的验证方式 ——
        /// 只生成一次的话，"每波 4 只"与"一共 4 只"在白模里无法区分。
        /// </remarks>
        public const float CHASE_RESPAWN_DELAY = 2.5f;

        /// <summary>贴地泥浆盘的排序层。低于 <see cref="AIM_SORTING_ORDER"/>：地面装饰不得盖住瞄准反馈。</summary>
        public const int MUD_SORTING_ORDER = 100;

        /// <summary>敌人身体的排序层。高于所有贴地件。</summary>
        /// <remarks>
        /// 完整的排序层链：泥浆 100 &lt; 敌人身体 500 &lt; 玩家色块 550
        /// &lt; <see cref="ENEMY_HP_TEXT_SORTING_ORDER"/> 560 &lt; 瞄准环 900 &lt; 球 1000
        /// &lt; 落地瞬闪 1100 &lt; 碎片 1200。
        /// <para><b>曾经还有一个"受损核心"（510）的层，已经删掉</b> —— 耐久改成头顶数字之后
        /// 那颗深色圆盘不再存在。</para>
        /// </remarks>
        public const int ENEMY_BODY_SORTING_ORDER = 500;

        /// <summary>
        /// 敌人耐久数字的排序层：<b>高于身体和玩家色块，低于瞄准环</b>。
        /// </summary>
        /// <remarks>
        /// <b>这一条是补一个想当然的错误。</b>数字走 <c>TextMesh</c>（<c>MeshRenderer</c>），
        /// 我原先以为"<c>MeshRenderer</c> 没有 <c>sortingOrder</c>"，就直接靠深度排序 ——
        /// 结果是数字被自己的身体挡住，或者读到一半被压掉。
        /// 实际上 <c>MeshRenderer</c> <b>和</b> <c>SpriteRenderer</c> 一样继承 <c>Renderer</c>，
        /// 两者都有 <c>sortingOrder</c>，而且白模全部是 z=0 的正交俯视 —— 深度根本分不出先后，
        /// <b>排序层是唯一能决定谁压谁的东西</b>。
        /// <para>取 560：高于敌人身体 500（数字要压在它自己身上），也高于玩家色块 550
        /// （数字不应该被站在它前面的玩家盖掉），但低于瞄准环 900 与球 1000 ——
        /// 那两个是"我现在要打哪"的即时反馈，优先级更高。</para>
        /// <para>与身体的 500 只差 60 而不是 100：它们属于同一个物体（数字是身体的读数），
        /// 档位拉开反而会让人以为中间还能再插一层。</para>
        /// </remarks>
        public const int ENEMY_HP_TEXT_SORTING_ORDER = 560;

        /// <summary>
        /// 玩家白模色块的排序层。高于敌人身体（500）、低于瞄准环（900）。
        /// </summary>
        /// <remarks>
        /// 玩家在场景里自带一个 <c>sortingOrder = 10</c> 的精灵。本色块画在 550，因此盖在它上面 ——
        /// 这正是我们要的：白模色块取代它成为"我看得见的玩家"。等白模删掉，场景精灵自动恢复可见。
        /// </remarks>
        public const int PLAYER_BODY_SORTING_ORDER = 550;

        /// <summary>碎片的排序层。比球还高：碎片是"这一帧发生了什么"的最高优先级读数。</summary>
        public const int SHATTER_SORTING_ORDER = 1200;

        /// <summary>
        /// 落地冲量查询碰撞体时在 <see cref="IMPULSE_RADIUS"/> 之外多查的余量（世界单位）。
        /// </summary>
        /// <remarks>
        /// <b>它是纯优化余量，不是设计值。</b>结算改成"按点距判定"之后，查询只需要"别漏掉候选"，
        /// 判定另有精确的距离比较（见 <c>ThrowSpawner.ApplyKick</c>）。
        /// <para>为什么必需：<c>OverlapCircleAll</c> 查的是<b>碰撞体</b>，窄查询会漏掉
        /// "圆心在半径内、但碰撞体很大"的目标 —— 先过滤后判定，不能反过来。
        /// 取 2 米足以覆盖白模里任何合理的碰撞体（玩家 0.7×0.5、敌人半径 0.45）。</para>
        /// <para>调大它<b>不会</b>让作用范围变大（判定不看它），只会多几次无用的距离比较。</para>
        /// </remarks>
        public const float PUSH_QUERY_MARGIN = 2f;

        /// <summary>
        /// 玩家血量读数面板的宽度（像素）。
        /// </summary>
        /// <remarks>
        /// <b>它锚在屏幕右上角</b>（左上是 debugPanel 的地盘，见下），从右边往左撑开。
        /// <para>撑到 520 是因为那一行里除了血量还带了"最近敌人 距离@速度"两个数 ——
        /// 那两个数是"敌人在动吗"唯一可靠的读法（见 <c>EnemyDirector.NearestEnemyDistance</c>）。
        /// 宽度不够时 <c>GUI.Box</c> 会把字裁掉，而裁掉的那部分恰好是最右边的泥浆数。</para>
        /// <para><c>MovementDebugPanel</c> 的 <c>panelOrigin</c> 是 (8, 8)、<c>panelSize</c> 是 (440, 190)，
        /// 所以窗口宽小于 <c>440 + 520 + 24 ≈ 984</c> 时两者会重叠 —— 这是**已知且接受**的：
        /// 一行临时读数不值得为它写一套自动避让。Game 视图默认宽度远大于 984。</para>
        /// </remarks>
        public const float PLAYER_HUD_WIDTH = 520f;
    }
}
