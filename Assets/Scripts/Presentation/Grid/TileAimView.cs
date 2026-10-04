using DeepseaOil.Data;
using UnityEngine;

namespace DeepseaOil.Presentation.Grid
{
    /// <summary>
    /// 瞄准格高亮：<b>整格</b>半透明色块，跟着吸附后的格子走。
    /// </summary>
    /// <remarks>
    /// <b>为什么画整格而不是画"落点圆"：</b>落点已经被吸附到格子中心，画一个圆反而让人以为
    /// 落点在圆心上、与格子无关。整格色块把"这一格会变成泥浆"这件事直接画出来。
    /// <para><b>它必须与球真正会落的格子一致。</b>所以格子由调用方用与投掷同一个吸附函数算好后传进来，
    /// 本类不做任何几何计算 —— 两处各算一次必然漂移，表现是"看着能扔到、其实扔不到"。</para>
    /// <para>色块由运行期生成的方块 sprite 构成，不依赖美术资源；正式美术到位后换成带描边的图即可。</para>
    /// </remarks>
    public sealed class TileAimView : MonoBehaviour
    {
        /// <summary>可用时的颜色（白，半透明：它是提示不是物体）。</summary>
        private static readonly Color AvailableColor = new Color(1f, 1f, 1f, 0.32f);

        /// <summary>不可用时的颜色（红）。</summary>
        private static readonly Color BlockedColor = new Color(1f, 0.25f, 0.2f, 0.42f);

        private SpriteRenderer _renderer;

        /// <summary>当前是否可见。</summary>
        public bool IsVisible => _renderer != null && _renderer.enabled;

        /// <summary>
        /// 建出高亮块。
        /// </summary>
        /// <param name="cellSize">格子边长（世界单位）：高亮块按它缩放。</param>
        /// <param name="sortingOrder">排序层；应低于球、高于地板与效果层。</param>
        public void Initialize(float cellSize, int sortingOrder)
        {
            _renderer = gameObject.AddComponent<SpriteRenderer>();

            PrimitiveSprites.Configure(
                _renderer,
                PrimitiveSprites.Square,
                AvailableColor,
                sortingOrder,
                cellSize);

            Hide();
        }

        /// <summary>摆到目标格并显示。</summary>
        /// <param name="cell">目标格（只用于诊断，摆位用 <paramref name="worldCenter"/>）。</param>
        /// <param name="worldCenter">该格中心的世界坐标。</param>
        /// <param name="available">当前是否可攻击（不可用时变红）。</param>
        public void Show(Vector3Int cell, Vector2 worldCenter, bool available)
        {
            if (_renderer == null) return;

            transform.position = new Vector3(worldCenter.x, worldCenter.y, 0f);

            _renderer.color = available ? AvailableColor : BlockedColor;
            _renderer.enabled = true;
        }

        /// <summary>隐藏（暂停 / 没有鼠标 / 拿不到格子时）。</summary>
        public void Hide()
        {
            if (_renderer == null) return;

            _renderer.enabled = false;
        }

        /// <summary>整体显隐（暂停时由组合根调）。</summary>
        public void SetVisible(bool visible)
        {
            if (_renderer == null) return;

            _renderer.enabled = visible;
        }
    }
}
