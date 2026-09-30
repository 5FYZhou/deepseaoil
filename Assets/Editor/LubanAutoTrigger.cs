// ---------------------------------------------------------------------------
// 配表工作流 · 触发器 X：Unity 重新获得焦点时自动导表
//
// 🔴 默认【关闭】，需要的人自己在菜单里开。
//
// 为什么默认关（人的判断，别再改回默认开）：
//   focusChanged 的语义是「Unity 拿到焦点就重导一次」，它【不知道你有没有改表】，
//   也不看 .xlsx 的修改时间。结果是每切回 Unity 就跑一遍全量导表：
//     · 改表改到一半切回来看效果 → 校验当场炸一片红，打断工作；
//     · 什么都没改也照跑一遍（虽然落盘是内容比对的增量，不写文件，但 Console 会滚输出）。
//   「每次切窗口都全量重导」这种不区分意图的行为，信任成本高于省下的那一次点击。
//   手动导入是主流做法：点一下（Ctrl/Cmd+Shift+D，即菜单 Luban ▸ 表格数据导入）或按需求开自动。
//
// 保留它的理由：想开的人能开，且下面的安全闸门照旧生效，不会造成坏数据。
//
// 设计取舍（保持草案结论，不要回退）：
//   · 不做文件监听（FileWatcher）：策划每存一次都会触发（含半成品保存），
//     校验器会炸一片红，反而打断工作。只在「焦点回到 Unity」时触发一次。
//   · 不做内容级 diff / 快照账本：Luban 日志已记录全部产物变更，
//     表数据的版本历史交给 git 更合适。
//
// 与草案的偏差（有理由，勿回退）：
//   1. 默认值：草案是默认开，我们改成默认关（见上）。
//   2. 节流计时存 EditorPrefs 而不是 static 字段：static 字段会在域重载后丢失，
//      而「导表 → 生成代码变化 → 重新编译 → 域重载」几乎必然发生，
//      于是草案的节流会自己失效：切出去再切回来就能无限重导。
// ---------------------------------------------------------------------------

using System;
using UnityEditor;

namespace DeepseaOil.EditorTools
{
    [InitializeOnLoad]
    public static class LubanAutoTrigger
    {
        // 🔴 key 里带 v2：为了让「已经跑过旧版本、EditorPrefs 里存了 true」的机器
        //    也拿到"默认关"这个新行为——同一个 key 改默认值对已存在的键无效。
        //    （旧 key 是 "DeepseaOil.Luban.AutoTrigger.Enabled"，不删也无害：它已无人读取。）
        const string EnabledKey = "DeepseaOil.Luban.AutoTrigger.Enabled.v2";
        const string LastRunKey = "DeepseaOil.Luban.AutoTrigger.LastRunUnixSeconds";

        /// <summary>🔴 节流：30 秒内连续切换焦点不重复触发。</summary>
        public const double CooldownSeconds = 30.0;

        /// <summary>菜单「自动触发开关」读写它。🔴 默认关（理由见文件头）。</summary>
        public static bool Enabled
        {
            get { return EditorPrefs.GetBool(EnabledKey, false); }
            set { EditorPrefs.SetBool(EnabledKey, value); }
        }

        static LubanAutoTrigger()
        {
            // 域重载后可能重复订阅，先退再订，保证只挂一次
            EditorApplication.focusChanged -= OnFocusChanged;
            EditorApplication.focusChanged += OnFocusChanged;
        }

        // 🔴 签名必须是 Action<bool>：Unity 2022.3 的 focusChanged 是 Action<bool>，
        //    写成无参方法会 CS0123（No overload for 'OnFocusChanged' matches delegate 'Action<bool>'）。
        //    参数即「当前是否获得焦点」，与 EditorApplication.isFocused 同义。
        static void OnFocusChanged(bool focused)
        {
            if (!focused) return;                                         // 丢焦点的那次不管
            if (!EditorApplication.isFocused) return;                     // 与属性再核对一次
            if (!Enabled) return;
            if (EditorApplication.isCompiling) return;                    // 正在编译：跳过，不排队
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;  // 🔴 Play 模式中不触发
            if (LubanImport.IsRunning) return;                            // 🔴 上次未完成则跳过
            if (NowSeconds() - LastRunSeconds() < CooldownSeconds) return;
            if (!LubanImport.Precheck()) return;                          // 预检不过不写时间戳，下次焦点还能重试

            // 只有真正要启动了才记时间
            EditorPrefs.SetString(LastRunKey, ToInvariant(NowSeconds()));
            LubanImport.RunExport();
        }

        static double NowSeconds()
        {
            return (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        /// <summary>写与读都用不变文化，避免切系统区域设置后小数点变逗号、解析失败。</summary>
        static string ToInvariant(double v)
        {
            return v.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        }

        static double LastRunSeconds()
        {
            string raw = EditorPrefs.GetString(LastRunKey, "");
            double v;
            return double.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out v) ? v : 0.0;
        }
    }
}
