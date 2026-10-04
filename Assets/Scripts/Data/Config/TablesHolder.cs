using System;
using System.IO;
using Luban.SimpleJSON;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 持有 Luban 生成的 cfg.Tables 实例。
    /// 调用时机：ConfigModule.Init 时构造一次。
    /// 边界：
    ///   - 构造时同步加载全部 JSON（启动时一次性完成）
    ///   - Loader 严格校验：文件不存在 / 为空 / 解析失败都抛异常
    ///   - 平台：File.ReadAllText 只对桌面端（Windows / macOS / Linux）有效。
    ///     Android / WebGL 的 StreamingAssets 在 APK 包内，必须改用 UnityWebRequest
    ///     —— 那会让 Init 变异步，牵动整条启动链。Jam 期不支持（该平台约束未登记在文档里）。
    /// </summary>
    internal sealed class TablesHolder
    {
        public cfg.Tables Tables { get; }

        public TablesHolder(string jsonRoot)
        {
            // Luban 生成的 Tables 构造函数接收一个 Loader：表名 → JSONNode
            // （生成目标是 cs-simple-json，所以是 Func<string, JSONNode>，不是 Newtonsoft 的 JObject）
            Tables = new cfg.Tables(file => LoadJson(jsonRoot, file));
        }

        /// <summary>
        /// Luban 调用的 Loader。输入：文件名（不含扩展名，如 "demo_tbweapon"）；输出：解析后的 JSON。
        /// </summary>
        private static JSONNode LoadJson(string root, string file)
        {
            string path = Path.Combine(root, file + ".json");

            if (!File.Exists(path))
                throw new FileNotFoundException($"[Config] JSON not found: {path}");

            string text = File.ReadAllText(path);

            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidDataException($"[Config] JSON is empty: {path}");

            return JSON.Parse(text);   // Luban.SimpleJSON.JSON
        }
    }
}
