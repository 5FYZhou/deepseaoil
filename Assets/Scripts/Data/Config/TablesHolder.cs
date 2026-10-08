using System;
using System.IO;
using Luban.SimpleJSON;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>持有 cfg.Tables，Init 时构造一次并同步加载全部 JSON</summary>
    /// <remarks>Loader 缺文件/空/解析失败一律抛异常。仅桌面端，Android/WebGL 须改用 UnityWebRequest。</remarks>
    internal sealed class TablesHolder
    {
        public cfg.Tables Tables { get; }

        public TablesHolder(string jsonRoot)
        {
            Tables = new cfg.Tables(file => LoadJson(jsonRoot, file));
        }

        /// <summary>Luban 的 Loader，入参文件名不含扩展名，出参解析后的 JSON</summary>
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
