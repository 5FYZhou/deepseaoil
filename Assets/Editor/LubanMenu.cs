// ---------------------------------------------------------------------------
// 配表工作流 · 唯一的人工入口（Unity Editor 菜单）
//
// 决策：工程内不留 .bat，导表入口统一为菜单，跨平台。
// 策划侧不需要开命令行、不需要双击 bat、不需要知道 Luban 命令。
// ---------------------------------------------------------------------------

using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeepseaOil.EditorTools
{
    public static class LubanMenu
    {
        const string MenuRoot = "Luban/";

        // 🔴 快捷键从 %#l（Ctrl/Cmd+Shift+L）改成 %#d —— %#l 与本机 Unity 自带绑定冲突。
        //    %#d：% = Ctrl/Cmd， # = Shift， d = 表格数据导入的 Data/导。
        //    macOS 自动映射为 Cmd+Shift+D。
        //    万一 %#d 也冲突：优先改这里，或在 UserSettings/Shortcuts.json 里解绑冲突项；
        //    实在不行把 " %#d" 整段删掉 —— 只用菜单项仍然完全可用（没有快捷键不影响功能）。
        [MenuItem(MenuRoot + "表格数据导入 %#d", false, 10)]
        public static void Export()
        {
            if (!GuardPlayMode()) return;

            var r = LubanImport.RunExport();
            if (r.Success)
            {
                EditorApplication.Beep();
            }
            else if (r.Launched)
            {
                // 报错原文已含表名 / 主键 / 字段 / 值，Console 里可直接定位；
                // 这里只把「看哪里」说清楚，不复述长文本。
                EditorUtility.DisplayDialog("表格数据导入失败（退出码 " + r.ExitCode + "）",
                    "工程目录未被修改。\n\n请查看 Console 里的 [Luban] 错误行" +
                    (string.IsNullOrEmpty(r.LogPath) ? "。" : "，完整日志：\n" + r.LogPath),
                    "知道了");
            }
        }

        [MenuItem(MenuRoot + "仅校验（不生成） %#k", false, 11)]
        public static void ValidateOnly()
        {
            var r = LubanImport.RunValidate();
            if (!r.Launched && !string.IsNullOrEmpty(r.ErrorMessage))
            {
                EditorUtility.DisplayDialog("无法校验", r.ErrorMessage, "知道了");
                return;
            }
            if (r.Success)
            {
                EditorUtility.DisplayDialog("校验通过", "所有表都通过校验，可以导入了。\n\n完整日志：\n" + r.LogPath, "好");
            }
            else
            {
                EditorUtility.DisplayDialog("校验未通过",
                    "请看 Console 里的 [Luban] 错误行（含表名 / 主键 / 字段 / 值）。\n\n完整日志：\n" + r.LogPath,
                    "知道了");
            }
        }

        [MenuItem(MenuRoot + "路径自检（不导入）", false, 30)]
        public static void Diagnose()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var line in LubanProject.DescribePaths())
                sb.AppendLine(line);

            var export = LubanProject.BuildExportCommand();
            var validate = LubanProject.BuildValidateCommand();

            sb.AppendLine();
            sb.AppendLine("--- 导出命令行（未转义参数表）---");
            sb.AppendLine(export.FileName + " " + string.Join(" ", export.Argv));
            sb.AppendLine("WorkingDirectory = " + export.WorkingDirectory);
            sb.AppendLine();
            sb.AppendLine("--- 仅校验命令行（未转义参数表）---");
            sb.AppendLine(validate.FileName + " " + string.Join(" ", validate.Argv));
            sb.AppendLine("WorkingDirectory = " + validate.WorkingDirectory);

            Debug.Log("[Luban] 路径自检\n" + sb);

            var pre = LubanProject.Precheck();
            if (!pre.Ok)
                Debug.LogError("[Luban] 预检未通过：" + pre.Message);
            else
                Debug.Log("[Luban] 预检通过：工具与配置都在。");
        }

        [MenuItem(MenuRoot + "焦点自动导入（默认关）", false, 31)]
        public static void ToggleAutoTrigger()
        {
            LubanAutoTrigger.Enabled = !LubanAutoTrigger.Enabled;
            bool on = LubanAutoTrigger.Enabled;
            Debug.Log("[Luban] 焦点自动导入：" + (on ? "开 —— 每次切回 Unity 都会全量重导一遍" : "关（推荐，手动导入）"));
        }

        [MenuItem(MenuRoot + "焦点自动导入（默认关）", true)]
        public static bool ToggleAutoTriggerValidate()
        {
            Menu.SetChecked(MenuRoot + "焦点自动导入（默认关）", LubanAutoTrigger.Enabled);
            return true;
        }

        /// <summary>Play 模式里导入会因脚本重编译而打断试玩，先问一句。</summary>
        static bool GuardPlayMode()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                return true;

            return EditorUtility.DisplayDialog("正在 Play 模式",
                "导入会触发脚本重编译，从而打断当前试玩。要继续吗？",
                "继续导入", "取消");
        }
    }
}
