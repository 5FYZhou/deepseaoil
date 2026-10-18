using DeepseaOil.Data;
using DeepseaOil.Presentation.Primitive;
using DeepseaOil.Presentation.Visual;
using UnityEngine;
using cfg.dso;

namespace DeepseaOil.Presentation.Projectile
{
    /// <summary>球/瓶罐本体：加载专属 Sprite 并管理视觉层级</summary>
    /// <remarks>层次由 BallDriver 建，勿在子物体上叠位。</remarks>
    public sealed class BallView : MonoBehaviour
    {
        private const string LitMaterialPath = "materials/M_CelLit_Default";

        private SpriteRenderer _renderer;

        // Sprite-Lit 只认全局光：不换 CelLit，球靠近火堆也不会亮
        private static Material _litMaterial;

        private Vector3 _baseScale = Vector3.one;

        public float Height { get; private set; }

        /// <summary>建子物体并配好 sprite；直径单位为米</summary>
        public void Initialize(BallType type, float diameterMeters, Color fallbackColor, SeedType seed = SeedType.None)
        {
            _renderer = CreateSprite();

            Sprite sprite = null;
            if (AssetModule.IsInitialized)
            {
                // 种子弹借水球当弹道外壳，贴图必须按种子种类单独取
                sprite = seed == SeedType.None
                    ? AssetModule.Load<Sprite>($"projectiles/Projectile_{type}")
                    : AssetModule.Load<Sprite>($"projectiles/Projectile_Seed_{seed}");
            }

            if (sprite != null)
            {
                _renderer.sprite = sprite;
                _renderer.color = Color.white;
                _renderer.sortingOrder = RenderOrder.YSortBandStart;

                // 保持美术切图的真实长宽比（胶囊瓶身比例）
                float aspect = sprite.rect.height / Mathf.Max(1f, sprite.rect.width);
                transform.localScale = new Vector3(diameterMeters, diameterMeters * aspect, 1f);
            }
            else
            {
                // 兜底：缺资源时回落到代码纯色圆点
                PrimitiveSprites.Configure(
                    _renderer,
                    PrimitiveSprites.Circle,
                    fallbackColor,
                    RenderOrder.YSortBandStart,
                    diameterMeters);
            }

            // 读回 localScale 当翻转基准会逐帧累乘，整个尺寸只在这里定一次
            _baseScale = transform.localScale;
        }

        /// <summary>摆位：height=当前弧高，0=贴地；rotationZ=迎风倾角，符号同时决定瓶腹朝向</summary>
        public void Apply(Vector2 groundPos, float height, float rotationZ)
        {
            Height = height;
            transform.position = new Vector3(groundPos.x, groundPos.y + height, 0f);
            transform.rotation = Quaternion.Euler(0f, 0f, rotationZ);

            // 瓶腹迎向投掷方向：翻转看倾角符号，仰角已保证它跟投掷方向同向
            float sign = rotationZ < 0f ? -1f : 1f;

            transform.localScale = new Vector3(Mathf.Abs(_baseScale.x) * sign, _baseScale.y, _baseScale.z);

            // Y-Sort 档位取贴地 y（不是弧高）：排序回答它落在场上哪里
            if (_renderer != null) _renderer.sortingOrder = RenderOrder.BallOrder(groundPos.y);
        }

        private SpriteRenderer CreateSprite()
        {
            var go = new GameObject("BallSprite");
            go.layer = RenderOrder.OverlayLayer;
            go.transform.SetParent(transform, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = SortingLayerConsts.Actor;

            if (_litMaterial == null) _litMaterial = Resources.Load<Material>(LitMaterialPath);

            if (_litMaterial != null) renderer.sharedMaterial = _litMaterial;

            return renderer;
        }
    }
}