using System;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>资源 Key 解析与类型匹配</summary>
    /// <remarks>表里存相对 Assets/ 带扩展名，Resources.LoadAsync 用相对 Resources/ 无后缀</remarks>
    internal sealed class AssetRegistry
    {
        private const string ResourcesRoot = "Assets/Resources/";

        public string ResolvePath(string key)
        {
            if (string.IsNullOrEmpty(key))
                return key;

            var path = key.Replace('\\', '/');

            if (path.StartsWith(ResourcesRoot, StringComparison.OrdinalIgnoreCase))
                path = path.Substring(ResourcesRoot.Length);
            else if (path.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase))
                path = path.Substring("Resources/".Length);

            path = path.TrimStart('/');

            int dot = path.LastIndexOf('.');
            if (dot > 0)
                path = path.Substring(0, dot);

            return path;
        }

        public bool IsTypeMatch(Type expected, UnityEngine.Object asset)
        {
            if (asset == null)
                return false;

            return expected.IsAssignableFrom(asset.GetType());
        }
    }
}
