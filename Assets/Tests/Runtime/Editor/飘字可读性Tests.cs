// 伤害飘字可读性 · EditMode 测试
//
// 描边必须由 _OutlineWidth 浮点属性驱动：一旦被关键字门控，运行时设的宽度不生效，出包才暴露。

using System.IO;
using DeepseaOil.Data.Settings;
using NUnit.Framework;
using UnityEngine;

namespace DeepseaOil.Tests
{
    /// <summary>飘字契约：描边可运行时启用、提亮与保持段必须真的在生效</summary>
    [Category("Presentation")]
    public sealed class 飘字可读性Tests
    {
        private const string TmpSdfShaderPath = "Assets/TextMesh Pro/Shaders/TMP_SDF.shader";

        [Test]
        public void 描边_必须由浮点属性驱动且不得依赖shader关键字()
        {
            string source = File.ReadAllText(TmpSdfShaderPath);

            StringAssert.Contains("_OutlineWidth", source, "TMP_SDF 里找不到 _OutlineWidth：描边宽度无处可写");

            StringAssert.DoesNotContain(
                "OUTLINE_ON", source, "TMP_SDF 把描边挪进了关键字门控：运行时设宽度不生效，出包后描边静默消失");
        }

        [Test]
        public void 默认值_提亮描边与保持段必须真的在生效()
        {
            VisualFeelTuning feel = ScriptableObject.CreateInstance<VisualFeelTuning>();

            try
            {
                Assert.Greater(feel.popupWhiten, 0f, "popupWhiten 为 0：元素色不向白靠，飘字又回到「太淡」");
                Assert.Greater(feel.popupHold, 0f, "popupHold 为 0：满 alpha 段消失，飘字一出生就开始淡");
                Assert.LessOrEqual(feel.popupHold, 0.9f, "popupHold 顶到 1：淡出永远不开始");
                Assert.GreaterOrEqual(feel.popupOutlineWidth, 0.05f, "描边宽度过小：小字号下肉眼看不出描边");
                Assert.GreaterOrEqual(feel.popupFontScale, 1f, "字号倍数小于 1：飘字比原来更小");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(feel);
            }
        }
    }
}
