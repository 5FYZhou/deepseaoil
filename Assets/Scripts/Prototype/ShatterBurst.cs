using UnityEngine;

namespace DeepseaOil.Prototype
{
    /// <summary>
    /// 打碎：敌人耐久归零那一下飞出的几块碎片。
    /// </summary>
    /// <remarks>
    /// <b>它不是特效，是读数。</b>"三下打碎"这条规则如果没有可见的表现，就只能在 Console 里靠日志确认，
    /// 而白模的价值恰恰是"一眼看出发生了什么"。同 <c>LandingFlash</c>（落地瞬闪）：
    /// 需求书把"音效、粒子、拖尾"列为不做，本类不属于那一类 —— 它不表达情绪，只暴露"这个敌人死了"。
    /// <para><b>碎片的形状是确定性算出来的，不读随机数。</b>均匀扇形 ＋ 一个固定的俯仰系数：
    /// 三块碎片每次都在同样的相对位置上飞出去。随机数会让"同一个现象能否再出现一次"变成赌博，
    /// 而白模的排错全靠重现。
    /// </para>
    /// <para>生命周期与球一致（<c>Instantiate</c> / <c>Destroy</c>，不上对象池）——
    /// 白模不值得为几块碎片引一套池。</para>
    /// </remarks>
    public sealed class ShatterBurst : MonoBehaviour
    {
        private float _duration;
        private float _elapsed;

        /// <summary>
        /// 在敌人位置生成一次碎裂。
        /// </summary>
        /// <param name="origin">碎裂中心（敌人位置）。</param>
        /// <param name="hitDirection">受击方向（<b>已归一化</b>）：碎片沿它散开。</param>
        /// <param name="color">碎片颜色，通常取敌人身体色。</param>
        public void Initialize(Vector2 origin, Vector2 hitDirection, Color color)
        {
            transform.position = new Vector3(origin.x, origin.y, 0f);

            _duration = ThrowConstants.SHATTER_DURATION > 0f
                ? ThrowConstants.SHATTER_DURATION
                : 0.35f;

            Vector2 forward = hitDirection.sqrMagnitude > 0f ? hitDirection.normalized : Vector2.up;

            int count = Mathf.Max(1, ThrowConstants.SHATTER_PIECE_COUNT);
            float spread = ThrowConstants.SHATTER_SPREAD_DEGREES;
            float baseAngle = Mathf.Atan2(forward.y, forward.x);

            for (int i = 0; i < count; i++)
            {
                // 均匀铺在 [-spread/2, +spread/2] 上；count = 1 时最后一项是 0（正好沿受击方向）。
                float offset = count == 1
                    ? 0f
                    : -spread * 0.5f + spread * i / (count - 1);

                float radians = baseAngle + offset * Mathf.Deg2Rad;

                var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

                CreateShard(direction, color);
            }
        }

        private void CreateShard(Vector2 direction, Color color)
        {
            var go = new GameObject("碎片");

            // 保持世界坐标：碎片的位置与旋转都与父物体无关。
            go.transform.SetParent(transform, true);
            go.transform.localPosition = Vector3.zero;

            var renderer = go.AddComponent<SpriteRenderer>();

            // 用正圆而不是贴地形状：碎片是**飞在空中**的物体，压扁它会让它看起来像躺在地上。
            // Configure 里已经按"贴图直径 = 1 米"的约定写了 localScale，
            // 而 Scale 是相对父物体的，所以碎片会继承碎裂中心的位置并向外飞。
            PrimitiveSprites.Configure(
                renderer,
                PrimitiveSprites.Circle,
                color,
                ThrowConstants.SHATTER_SORTING_ORDER,
                ThrowConstants.SHATTER_PIECE_RADIUS_METERS * 2f
                );

            var shard = go.AddComponent<Shard>();
            shard.Initialize(direction, ThrowConstants.SHATTER_SPEED);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;

            if (_elapsed >= _duration) Destroy(gameObject);
        }

        /// <summary>
        /// 一块碎片的直线飞行。恒速、不衰减 —— 碎片是"散出去"，不是"滑出去"。
        /// </summary>
        private sealed class Shard : MonoBehaviour
        {
            private Vector2 _velocity;

            public void Initialize(Vector2 direction, float speed)
            {
                _velocity = direction * speed;
            }

            private void Update()
            {
                // 写位置而不是写刚体速度：碎片不需要碰撞、不需要被推，一个刚体只会让它去参与解算。
                transform.position += new Vector3(_velocity.x, _velocity.y, 0f) * Time.deltaTime;
            }
        }
    }
}
