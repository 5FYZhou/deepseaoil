using System;
using System.IO;
using Luban.SimpleJSON;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 持有 Luban 生成的 cfg.Tables 实例（调用时机：ConfigModule.Init 时构造一次，构造时同步加载全部 JSON）。
    /// Loader 严格校验：文件不存在 / 为空 / 解析失败都抛异常 —— 少一行、坏一行要在启动期炸，不静默降级。
    /// 平台只支持桌面端：Android / WebGL 的 StreamingAssets 在包内，须改用 UnityWebRequest（会让 Init 变异步、
    /// 牵动整条启动链），Jam 期不支持 —— 该平台约束未登记在文档里。
    /// </summary>
    internal sealed class TablesHolder
    {
        public cfg.Tables Tables { get; }

        public TablesHolder(string jsonRoot)
        {
            Tables = new cfg.Tables(file => LoadJson(jsonRoot, file));
        }

        /// <summary>Luban 调用的 Loader：入参文件名不含扩展名（如 "demo_tbweapon"），出参解析后的 JSON（<c>Luban.SimpleJSON</c>，生成目标是 cs-simple-json）。</summary>
        private static JSONNode LoadJson(string root, string file)
        {
            string path = Path.Combine(root, file + ".json");

            if (!File.Exists(path))
                throw new FileNotFoundException($"[Config] JSON not found: {path}");

            string text = File.ReadAllText(path);

            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidDataException($"[Config] JSON is empty: {path}");

            return JSON.Parse(text);
        }
    }
}
