using System;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 资源 Key 解析与类型匹配。**本类是 Data 层唯一的"路径语义转换点"**。
    /// Key 契约（详见 Docs/分层设计/数据层.md §5）：
    ///   配置表里存的是「相对 Assets/、带扩展名」的路径（Luban #path=unity 在导表期校验它真实存在）；
    ///   Resources.LoadAsync 需要「相对 Assets/Resources/、不带扩展名」的路径。
    ///   本类负责这两者之间的转换。
    /// 未来：量产切 Addressables 时，ResolvePath 返回 Address 地址，其余代码不动。
    /// </summary>
    internal sealed class AssetRegistry
    {
        private const string ResourcesRoot = "Assets/Resources/";

        /// <summary>
        /// 把 Key 转成 Resources.LoadAsync 可用的路径。
        /// 例："Assets/Resources/Icons/weapon.png" → "Icons/weapon"
        ///     "Icons/weapon.png"                 → "Icons/weapon"
        ///     "Icons/weapon"                     → "Icons/weapon"
        /// 边界：只做前缀与扩展名处理，不检查资源是否存在（那是加载的事）。
        /// </summary>
        public string ResolvePath(string key)
        {
            if (string.IsNullOrEmpty(key))
                return key;

            var path = key.Replace('\\', '/');

            // 去掉 "Assets/Resources/" 前缀（大小写不敏感，避免策划手写出大小写差异）
            if (path.StartsWith(ResourcesRoot, StringComparison.OrdinalIgnoreCase))
                path = path.Substring(ResourcesRoot.Length);
            else if (path.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase))
                path = path.Substring("Resources/".Length);

            path = path.TrimStart('/');

            // 去掉扩展名：Resources.Load 按「路径 + 类型」定位，不接受扩展名
            int dot = path.LastIndexOf('.');
            if (dot > 0)
                path = path.Substring(0, dot);

            return path;
        }

        /// <summary>
        /// 类型匹配检查。调用方：LoadScheduler 加载完成后。
        /// 边界：不做隐式转换；asset 为 null 或类型不匹配都返回 false。
        /// </summary>
        public bool IsTypeMatch(Type expected, UnityEngine.Object asset)
        {
            if (asset == null)
                return false;

            return expected.IsAssignableFrom(asset.GetType());
        }
    }
}
