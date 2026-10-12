using UnityEngine.UI;

namespace DeepseaOil.Presentation.UI
{
    /// 切换图片时必须手动 SetNativeSize()：psd 导入后按钮交互前后的尺寸不一样，编辑器里改不出大小变化
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
