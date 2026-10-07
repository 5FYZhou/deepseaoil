using System;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 资源 Key 解析与类型匹配，Data 层唯一的路径语义转换点；只做前缀与扩展名处理，不检查资源是否存在。
    /// Key 口径：配置表存「相对 Assets/、带扩展名」，Resources.LoadAsync 要「相对 Assets/Resources/、不带扩展名」，两种形式都容忍、前缀匹配大小写不敏感。
    /// 类型匹配不做隐式转换：asset 为 null 或类型不匹配都返回 false（不抛异常）。
    /// </summary>
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
