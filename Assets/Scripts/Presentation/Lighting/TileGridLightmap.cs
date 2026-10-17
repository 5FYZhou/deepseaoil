using System.Collections.Generic;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Grid;
using UnityEngine;

namespace DeepseaOil.Presentation.Lighting
{
    /// <summary>仿泰拉瑞亚的瓦片网格光照场：光在格子上扩散，结果打成微型纹理推给全屏着色器</summary>
    /// <remarks>光斑形状由 4 邻域扩散决定，与 Light2D 的圆形衰减无关；连片火海天然融成一整块光区，灯具数量不再是开销项。</remarks>
    [DisallowMultipleComponent]
    public sealed class TileGridLightmap : MonoBehaviour
    {
        private const string TextureName = "TileGridLightmap_RT";

        // 黄金分割低差异步进：电弧逐档阶跃靠它铺开，换成伪随机会连着重播同一档
        private const float GoldenRatio = 0.6180339f;

        // 超过这个纹素数，逐帧两趟扫描会开始吃主线程预算：照建，但要在日志里留痕
        private const int TexelWarnThreshold = 96000;

        private static readonly int TileLightmapId = Shader.PropertyToID("_TileLightmap");
        private static readonly int TileLightmapParamsId = Shader.PropertyToID("_TileLightmapParams");
        private static readonly int CarryLightPosId = Shader.PropertyToID("_CarryLightPos");
        private static readonly int CarryLightColorId = Shader.PropertyToID("_CarryLightColor");

        private const int NoLight = 0;
        private const int FireLight = 1;
        private const int ElectricLight = 2;

        // √2：走对角必须按 a^√2 衰减（几何长度比 0.707 会小于 a²，对角项永远用不上）
        private const float DiagonalExponent = 1.41421356f;

        [Header("扩散配置")]
        [Tooltip("空气透过率：每跨一格保留的光强比例")]
        [Range(0.5f, 0.85f)]
        [SerializeField] private float airTransmission = 0.72f;

        [Tooltip("墙体透过率：光穿过阻挡格的剩余比例，决定阴影硬度")]
        [Range(0.0f, 0.4f)]
        [SerializeField] private float obstacleTransmission = 0.20f;

        [Tooltip("对角衰减指数：√2 时等值线接近正圆；取 0.707 会弱于两格正交的透光，菱形一点都消不掉")]
        [Range(1.0f, 2.0f)]
        [SerializeField] private float diagonalTransmissionExponent = DiagonalExponent;

        [Header("火光呼吸")]
        [Tooltip("呼吸频率（Hz）；超过 1.5Hz 就从呼吸变成闪烁")]
        [Range(0.1f, 3f)]
        [SerializeField] private float fireBreathHertz = 0.7f;

        [Tooltip("呼吸深度：光强在 (基准-深度) 与 基准 之间摆动")]
        [Range(0f, 0.4f)]
        [SerializeField] private float fireBreathDepth = 0.10f;

        [Tooltip("呼吸基准强度：与深度联手决定火光平均亮度；调高更容易过曝成平顶")]
        [Range(0.6f, 1.2f)]
        [SerializeField] private float fireBreathBase = 1f;

        [Tooltip("火光基色（HDR），与 TileLightDirector 光型表的火色同值；网格光场实际吃的是这一份")]
        [ColorUsage(false, true)]
        [SerializeField] private Color fireSourceColor = new Color(1f, 0.549f, 0.259f, 1f);

        [Header("电弧抽搐")]
        [Tooltip("电弧阶跃频率（Hz）；电弧本来就该抖，压到 5Hz 以下会变成呼吸")]
        [Range(1f, 40f)]
        [SerializeField] private float electricJitterHertz = 18f;

        [Tooltip("电光强度下限：下限+幅度超过 1 就会过曝到纯白")]
        [Range(0f, 1f)]
        [SerializeField] private float electricPulseFloor = 0.85f;

        [Tooltip("电光跳变幅度：每档在 下限 与 下限+幅度 之间硬阶跃")]
        [Range(0f, 0.5f)]
        [SerializeField] private float electricPulseDepth = 0.15f;

        [Tooltip("电光基色（HDR），与 TileLightDirector 光型表的电色同值")]
        [ColorUsage(false, true)]
        [SerializeField] private Color electricSourceColor = new Color(0.490f, 0.976f, 1f, 1f);

        [Header("随身光源")]
        [Tooltip("跟着玩家走的微弱照明，颜色决定暖度")]
        [ColorUsage(false, true)]
        [SerializeField] private Color carryLightColor = new Color(1f, 0.86f, 0.66f, 1f);

        [Tooltip("随身光源峰值强度；0 = 关闭。衰减速率跟随 Air Transmission")]
        [Range(0f, 1f)]
        [SerializeField] private float carryLightIntensity = 0.5f;

        [Header("道具自发光")]
        [Tooltip("泉眼等道具共用的呼吸频率（Hz）：只给一个慢节拍，避免各道具各自乱闪")]
        [Range(0.05f, 2f)]
        [SerializeField] private float steadyPulseHertz = 0.35f;

        private struct SteadyEmitter
        {
            public Vector2 Position;
            public Color Color;
            public float Intensity;
            public float PulseDepth;
        }

        // 道具光与火/电格同走一条扩散：进列表后逐帧映射到格心，因此天然有墙体遮挡
        private readonly List<SteadyEmitter> _steadyEmitters = new List<SteadyEmitter>();

        // 越界只报一次：该列表每帧重建，不设闩会把 Console 刷爆
        private bool _warnedSteadyOutside;

        // 唯一真源：发光类别与光阻标记都由格子事件写入，逐帧只读，不重复查表
        private byte[] _kindMap;
        private byte[] _blockedMap;

        // 扩散用 float：byte 逐格截断会在十余格后突然断成 0，形成可见的硬色阶断层
        private float[] _workR;
        private float[] _workG;
        private float[] _workB;

        private Color32[] _pixels;
        private Texture2D _texture;

        private GridLogic _grid;
        private GridGeometry _geometry;
        private BoundsInt _bounds;
        private int _width;
        private int _height;
        private int _texels;
        private bool _attached;
        private bool _wasLit;

        private bool _hasCarryLight;
        private Vector2 _carryPosition;

        /// <summary>光照图宽（一格一纹素）</summary>
        public int TexelWidth => _width;

        /// <summary>光照图高</summary>
        public int TexelHeight => _height;

        /// <summary>当前发光格数（火 + 电）</summary>
        public int EmitterCellCount { get; private set; }

        /// <summary>当前参与光阻的格数</summary>
        public int BlockedCellCount { get; private set; }

        /// <summary>绑定网格与几何并按格订阅状态变化；必须早于开局那批初始地块的灌入</summary>
        public void Attach(GridLogic grid, in GridGeometry geometry, in BoundsInt cellBounds)
        {
            if (_attached) return;

            if (grid == null || !geometry.IsValid || cellBounds.size.x <= 0 || cellBounds.size.y <= 0)
            {
                Debug.LogWarning("[Light] 网格光照场未接线（grid / 几何 / 地板层边界缺失）：本次不发光。", this);

                return;
            }

            _grid = grid;
            _geometry = geometry;
            _bounds = cellBounds;
            _width = cellBounds.size.x;
            _height = cellBounds.size.y;
            _texels = _width * _height;

            if (_texels > TexelWarnThreshold)
            {
                Debug.LogWarning($"[Light] 网格光照场 {_width}×{_height}（{_texels} 格）超过 {TexelWarnThreshold}：逐帧两趟扫描会明显吃帧。", this);
            }

            _kindMap = new byte[_texels];
            _blockedMap = new byte[_texels];
            _workR = new float[_texels];
            _workG = new float[_texels];
            _workB = new float[_texels];
            _pixels = new Color32[_texels];

            // linear: 必须为 true —— 工程是 Linear 空间，标成 sRGB 会在采样时把光强再解码一次，衰减全部走形
            _texture = new Texture2D(_width, _height, TextureFormat.RGBA32, false, true)
            {
                name = TextureName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            EventBus<TileStateChanged>.Subscribe(OnTileStateChanged);

            _attached = true;

            ClearAll();

            PushGlobals();
        }

        public void Detach()
        {
            if (!_attached) return;

            EventBus<TileStateChanged>.Unsubscribe(OnTileStateChanged);

            _attached = false;

            ClearAll();
        }

        /// <summary>每渲染帧推进一次呼吸采样与扩散；由 CombatRoot.RenderTick 驱动</summary>
        public void Tick(float time, float deltaTime)
        {
            if (!_attached) return;

            // 随身光走着色器全局量，不参与纹理烘图：即使没有任何火/电格，也必须每帧把它推上去
            PushCarryGlobals();

            // 一个发光格都没有且上一帧已经全黑：不必再刷一遍空纹理
            if (!HasEmitters && !_wasLit) return;

            float firePulse = fireBreathBase - fireBreathDepth + fireBreathDepth * Mathf.Sin(time * Mathf.PI * 2f * fireBreathHertz);
            float electricStep = Mathf.Repeat(Mathf.Floor(time * electricJitterHertz) * GoldenRatio, 1f);
            float electricPulse = electricPulseFloor + electricPulseDepth * electricStep;

            FillSources(firePulse, electricPulse, time);

            Propagate();

            Upload();
        }

        public void ClearAll()
        {
            for (int i = 0; i < _texels; i++)
            {
                _kindMap[i] = 0;
                _blockedMap[i] = 0;
                _workR[i] = 0f;
                _workG[i] = 0f;
                _workB[i] = 0f;
                _pixels[i] = default;
            }

            EmitterCellCount = 0;
            BlockedCellCount = 0;
            _hasCarryLight = false;
            _wasLit = false;
            _warnedSteadyOutside = false;

            PushCarryGlobals();

            if (_texture == null) return;

            _texture.SetPixels32(_pixels);
            _texture.Apply(false, false);
        }

        /// <summary>把随身光源推到某个世界坐标；每帧推一次，位置连续平滑</summary>
        public void SetCarryLightPosition(Vector2 world)
        {
            _hasCarryLight = carryLightIntensity > 0f;
            _carryPosition = world;
        }

        /// <summary>熄灭随身光源（玩家死亡/离场）</summary>
        public void ClearCarryLight()
        {
            _hasCarryLight = false;
        }

        /// <summary>登记一个常驻道具光源（泉眼）；光按格扩散，所以会吃到墙体遮挡</summary>
        /// <remarks>调用方每帧重建（参数改了立刻生效），因此越界警告必须只报一次；光照图未装配时不校验边界</remarks>
        public void AddSteadyEmitter(Vector2 world, Color color, float intensity, float pulseDepth)
        {
            if (intensity <= 0f) return;

            if (_attached && !_warnedSteadyOutside && CellToIndex(_geometry.WorldToCell(world)) < 0)
            {
                Debug.LogWarning($"[Light] 道具光源 {world} 落在光照图边界外：它不会照亮任何格子。", this);

                _warnedSteadyOutside = true;
            }

            _steadyEmitters.Add(new SteadyEmitter
            {
                Position = world,
                Color = color,
                Intensity = intensity,
                PulseDepth = Mathf.Clamp(pulseDepth, 0f, 0.9f),
            });
        }

        /// <summary>清空道具光源登记；组合根重装配时先调它，避免同一口泉被登记两次</summary>
        public void ClearSteadyEmitters()
        {
            _steadyEmitters.Clear();
        }

        /// <summary>当前登记的道具光源数</summary>
        public int SteadyEmitterCount => _steadyEmitters.Count;

        // 空转判据必须含道具光：只看火/电格会在"场上只有泉眼"时把整条扩散退化成不刷
        private bool HasEmitters => EmitterCellCount > 0 || _steadyEmitters.Count > 0;

        /// <summary>光照图的世界矩形：xy = 左下角世界坐标，zw = 宽高（世界单位）；IsValid 为 false 返回全零</summary>
        /// <remarks>角点取格心减半格：纹素中心必须落在格心上，否则整张光场偏移半格</remarks>
        public static Vector4 ComputeLightmapParams(in GridGeometry geometry, in BoundsInt cellBounds)
        {
            if (!geometry.IsValid) return Vector4.zero;

            float cellSize = geometry.CellSize;
            Vector2 center = geometry.CellCenter(cellBounds.min);
            Vector2 corner = center - new Vector2(cellSize * 0.5f, cellSize * 0.5f);

            return new Vector4(corner.x, corner.y, cellBounds.size.x * cellSize, cellBounds.size.y * cellSize);
        }

        private void OnDestroy()
        {
            Detach();

            // 复位全局量：纹理销毁后仍被材质引用会画成整片洋红
            Shader.SetGlobalTexture(TileLightmapId, Texture2D.blackTexture);
            Shader.SetGlobalVector(TileLightmapParamsId, Vector4.zero);
            Shader.SetGlobalVector(CarryLightPosId, Vector4.zero);
            Shader.SetGlobalVector(CarryLightColorId, Vector4.zero);

            if (_texture == null) return;

            if (Application.isPlaying) Destroy(_texture);
            else DestroyImmediate(_texture);

            _texture = null;
        }

        private void OnTileStateChanged(TileStateChanged evt)
        {
            if (!_attached) return;

            int index = CellToIndex(evt.Cell);

            if (index < 0) return;

            int kind = NoLight;

            if (TileLightDirector.TryGetContinuousLight(evt.State, out TileLightDirector.LightKind lightKind, out _, out _, out _))
            {
                kind = lightKind == TileLightDirector.LightKind.Fire ? FireLight : ElectricLight;
            }

            if (_kindMap[index] == 0 && kind != NoLight) EmitterCellCount++;
            else if (_kindMap[index] != 0 && kind == NoLight) EmitterCellCount--;

            _kindMap[index] = (byte)kind;

            // is_obstacle 是真源：以后加土墙/石壁只改表，这里零改动
            byte blocked = _grid != null && _grid.IsObstacle(evt.Cell) ? (byte)1 : (byte)0;

            if (_blockedMap[index] == 0 && blocked != 0) BlockedCellCount++;
            else if (_blockedMap[index] != 0 && blocked == 0) BlockedCellCount--;

            _blockedMap[index] = blocked;
        }

        private void FillSources(float firePulse, float electricPulse, float time)
        {
            float fireR = fireSourceColor.r * firePulse;
            float fireG = fireSourceColor.g * firePulse;
            float fireB = fireSourceColor.b * firePulse;

            float electricR = electricSourceColor.r * electricPulse;
            float electricG = electricSourceColor.g * electricPulse;
            float electricB = electricSourceColor.b * electricPulse;

            for (int i = 0; i < _texels; i++)
            {
                switch (_kindMap[i])
                {
                    case FireLight:
                        _workR[i] = fireR;
                        _workG[i] = fireG;
                        _workB[i] = fireB;
                        break;

                    case ElectricLight:
                        _workR[i] = electricR;
                        _workG[i] = electricG;
                        _workB[i] = electricB;
                        break;

                    default:
                        _workR[i] = 0f;
                        _workG[i] = 0f;
                        _workB[i] = 0f;
                        break;
                }
            }

            float steadyWave = Mathf.Sin(time * Mathf.PI * 2f * steadyPulseHertz);

            for (int e = 0; e < _steadyEmitters.Count; e++)
            {
                SteadyEmitter emitter = _steadyEmitters[e];

                // 道具与火/电可能落在同一格：逐通道取大，谁亮听谁的，不互相抹掉
                int index = CellToIndex(_geometry.WorldToCell(emitter.Position));

                if (index < 0) continue;

                float pulse = 1f - emitter.PulseDepth + emitter.PulseDepth * steadyWave;

                MaxInto(index, emitter.Color.r * emitter.Intensity * pulse, emitter.Color.g * emitter.Intensity * pulse, emitter.Color.b * emitter.Intensity * pulse);
            }
        }

        private void MaxInto(int index, float r, float g, float b)
        {
            if (r > _workR[index]) _workR[index] = r;
            if (g > _workG[index]) _workG[index] = g;
            if (b > _workB[index]) _workB[index] = b;
        }

        /// <summary>随身光源走着色器全局：世界坐标连续推进，衰减曲线与火光同一条</summary>
        /// <remarks>z 传每格的 -log2(空气透过率)，片元里 exp2(-距离×z) 就是 0.8^d，随身光的铺开范围随 Air Transmission 自动跟火光一致</remarks>
        private void PushCarryGlobals()
        {
            if (!_hasCarryLight || carryLightIntensity <= 0f)
            {
                Shader.SetGlobalVector(CarryLightPosId, Vector4.zero);
                Shader.SetGlobalVector(CarryLightColorId, Vector4.zero);

                return;
            }

            float falloffRate = Mathf.Log(1f / Mathf.Clamp(airTransmission, 0.01f, 0.99f), 2f);

            Shader.SetGlobalVector(CarryLightPosId, new Vector4(_carryPosition.x, _carryPosition.y, falloffRate, carryLightIntensity));
            Shader.SetGlobalVector(CarryLightColorId, carryLightColor);
        }

        /// <summary>8 邻域两趟扫描扩散：正向取左/下/左下/右下，反向取右/上/右上/左上，逐通道取大后再乘透过率</summary>
        /// <remarks>对角一步按 a^√2 衰减，等值线才近似欧氏圆；墙体衰减按目的格判定，故光穿墙后再跨一格还要再乘一次空气透过率，墙背后才形成硬边阴影。</remarks>
        private void Propagate()
        {
            float airDiagonal = DiagonalTransmission(airTransmission, diagonalTransmissionExponent);
            float obstacleDiagonal = DiagonalTransmission(obstacleTransmission, diagonalTransmissionExponent);

            for (int y = 0; y < _height; y++)
            {
                int row = y * _width;

                for (int x = 0; x < _width; x++)
                {
                    int i = row + x;
                    bool blocked = _blockedMap[i] != 0;
                    float axis = blocked ? obstacleTransmission : airTransmission;
                    float diagonal = blocked ? obstacleDiagonal : airDiagonal;

                    if (x > 0) Harvest(i - 1, i, axis);

                    if (y > 0) Harvest(i - _width, i, axis);

                    if (x > 0 && y > 0) Harvest(i - _width - 1, i, diagonal);

                    if (x + 1 < _width && y > 0) Harvest(i - _width + 1, i, diagonal);
                }
            }

            for (int y = _height - 1; y >= 0; y--)
            {
                int row = y * _width;

                for (int x = _width - 1; x >= 0; x--)
                {
                    int i = row + x;
                    bool blocked = _blockedMap[i] != 0;
                    float axis = blocked ? obstacleTransmission : airTransmission;
                    float diagonal = blocked ? obstacleDiagonal : airDiagonal;

                    if (x + 1 < _width) Harvest(i + 1, i, axis);

                    if (y + 1 < _height) Harvest(i + _width, i, axis);

                    if (x + 1 < _width && y + 1 < _height) Harvest(i + _width + 1, i, diagonal);

                    if (x > 0 && y + 1 < _height) Harvest(i + _width - 1, i, diagonal);
                }
            }
        }

        /// <summary>对角一步的透过率（按默认 √2 指数）</summary>
        /// <remarks>走对角等于走 √2 格，必须按 a^√2 衰减；取 a×0.707 会小于 a²，对角项永远输给两步正交，菱形一点都消不掉</remarks>
        public static float DiagonalTransmission(float axisTransmission)
        {
            return DiagonalTransmission(axisTransmission, DiagonalExponent);
        }

        private static float DiagonalTransmission(float axisTransmission, float exponent)
        {
            return Mathf.Pow(Mathf.Max(axisTransmission, 0.0001f), exponent);
        }

        private void Harvest(int from, int to, float trans)
        {
            float r = _workR[from] * trans;

            if (r > _workR[to]) _workR[to] = r;

            float g = _workG[from] * trans;

            if (g > _workG[to]) _workG[to] = g;

            float b = _workB[from] * trans;

            if (b > _workB[to]) _workB[to] = b;
        }

        private void Upload()
        {
            for (int i = 0; i < _texels; i++)
            {
                _pixels[i] = new Color32(ToByte(_workR[i]), ToByte(_workG[i]), ToByte(_workB[i]), 255);
            }

            _texture.SetPixels32(_pixels);
            _texture.Apply(false, false);

            _wasLit = HasEmitters;
        }

        private void PushGlobals()
        {
            if (_texture == null) return;

            Shader.SetGlobalTexture(TileLightmapId, _texture);
            Shader.SetGlobalVector(TileLightmapParamsId, ComputeLightmapParams(in _geometry, in _bounds));
        }

        private int CellToIndex(Vector3Int cell)
        {
            int x = cell.x - _bounds.min.x;
            int y = cell.y - _bounds.min.y;

            if (x < 0 || x >= _width || y < 0 || y >= _height) return -1;

            return y * _width + x;
        }

        private static byte ToByte(float value)
        {
            if (value <= 0f) return 0;

            if (value >= 1f) return 255;

            return (byte)(value * 255f + 0.5f);
        }
    }
}
