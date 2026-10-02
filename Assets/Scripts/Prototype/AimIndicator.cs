using UnityEngine;

namespace DeepseaOil.Prototype
{
    /// <summary>
    /// 落点指示器：一个压扁的圆环，跟着鼠标走，但离玩家的距离被夹在
    /// <see cref="ThrowConstants.MAX_THROW_DISTANCE"/> 以内。
    /// </summary>
    /// <remarks>
    /// <b>它画的必须就是球真正会落的那个点。</b>所以 clamp 用的是 <see cref="ThrowSpawner.ClampThrowPoint"/>
    /// —— 与投掷时同一个静态函数，不是"两份长得差不多的数学"。这是本文件唯一重要的契约：
    /// 指示器与落点一旦各算各的，就会差出"看着能扔到、其实扔不到"的手感硬伤。
    /// <para><b>屏幕点 → 世界点为什么不读相机的 z：</b><c>Camera.main.transform.position.z</c> 被
    /// Cinemachine 每帧驱动，依赖它等于让落点跟着相机插件走。正交相机下给一个足够大的常量深度即可
    /// （见 <see cref="ThrowConstants.CAMERA_PLANE_DEPTH"/>）。</para>
    /// <para>本组件由 <see cref="ThrowSpawner"/> 运行期建出，白模不依赖 prefab，也不需要 inspector 接线。</para>
    /// </remarks>
    public sealed class AimIndicator : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Camera _camera;

        /// <summary>当前指示的世界位置（已 clamp）。只读给调试用。</summary>
        public Vector2 WorldPoint { get; private set; }

        /// <summary>
        /// 建出环 sprite 并缓存相机。
        /// </summary>
        /// <param name="camera">用于屏幕点→世界点的相机；为 <c>null</c> 时本组件不更新（不会抛异常）。</param>
        public void Initialize(Camera camera)
        {
            _camera = camera;

            _renderer = gameObject.AddComponent<SpriteRenderer>();

            // 颜色不要纯白：指示器是"提示"而不是"物体"，纯白会在浅色地面上糊成一片。
            var color = new Color(1f, 1f, 1f, 0.55f);

            // 形状（透视压扁 + 上小下大）烘在贴图里，与球阴影、落地瞬闪共用同一个 builder。
            Sprite ring = PrimitiveSprites.GroundDiscOrRing(ThrowConstants.AIM_RADIUS_METERS, solid: false);

            PrimitiveSprites.ConfigureGround(
                _renderer,
                ring,
                color,
                ThrowConstants.AIM_SORTING_ORDER,
                ThrowConstants.AIM_RADIUS_METERS,
                ThrowConstants.AIM_RADIUS_METERS * 2f
                );
        }

        /// <summary>
        /// 跟随鼠标。由 <see cref="ThrowSpawner"/> 每帧调用（暂停时不调）。
        /// </summary>
        /// <param name="playerPos">玩家位置，作为 clamp 的圆心。</param>
        public void FollowMouse(Vector2 playerPos)
        {
            if (_camera == null) return;

            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return;

            Vector2 screen = mouse.position.ReadValue();

            // 屏幕点是 x/y 二维，z 是"离相机多远"；正交相机下取常量深度即落在世界 z=0 平面附近。
            Vector3 screenPoint = new Vector3(screen.x, screen.y, ThrowConstants.CAMERA_PLANE_DEPTH);
            Vector3 world = _camera.ScreenToWorldPoint(screenPoint);

            // out 距离在这里用不上（指示器只画点，"飞多久"是球自己的事），用弃元丢掉。
            WorldPoint = ThrowSpawner.ClampThrowPoint(
                playerPos,
                new Vector2(world.x, world.y),
                ThrowConstants.MAX_THROW_DISTANCE,
                out _
                );

            transform.position = new Vector3(WorldPoint.x, WorldPoint.y, 0f);
        }

        /// <summary>显隐。暂停、以及没有鼠标设备时藏起来。</summary>
        public void SetVisible(bool visible)
        {
            if (_renderer == null) return;

            _renderer.enabled = visible;
        }
    }
}
