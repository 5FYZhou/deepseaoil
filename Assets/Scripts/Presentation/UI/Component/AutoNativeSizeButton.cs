using UnityEngine.UI;

namespace DeepseaOil.Presentation.UI
{
    /// <summary>
    /// 神秘代码之让按钮切换图片时调用SetNativeSize()改RectTransform适应图片宽高
    /// 因为美术给的psd，unity直接导入后按钮交互前后的尺寸不一样，只在编辑器里改不出大小变化
    /// SetNativeSize()又不会在图片变化时自动调用
    /// 可以正常作为Button组件使用
    /// </summary>
    public class AutoNativeSizeButton : Button
    {
        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);

            if (targetGraphic is Image image)
            {
                image.SetNativeSize();
            }
        }
    }
}
