using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 数值配置模块。Data 层的数值查询入口。
    /// 调用时机：Init 由 GameRoot.Awake 调用一次；查询随时。
    /// 边界：
    ///   - 所有查询同步返回
    ///   - 运行时不接受任何写操作
    ///   - 不感知资源（资源引用是字符串 Key，交给 AssetModule）
    ///   - Init 失败抛异常阻止游戏启动：带病数据不进运行时
    /// </summary>
    public static class ConfigModule
    {
        private static TablesHolder _holder;
        private static bool _ready;

        public static bool IsReady => _ready;

        /// <summary>
        /// 初始化。调用方：GameRoot.Awake，**必须早于 AssetModule.Init**。
        /// 边界：重复调用抛异常；任何失败都包成 ConfigLoadException 抛出。
        /// </summary>
        public static void Init(string jsonRoot)
        {
            if (_ready)
                throw new InvalidOperationException("[Config] ConfigModule.Init called twice");

            if (string.IsNullOrEmpty(jsonRoot))
                throw new ConfigLoadException("[Config] jsonRoot is null or empty");

            if (!System.IO.Directory.Exists(jsonRoot))
                throw new ConfigLoadException($"[Config] jsonRoot not found: {jsonRoot}");

            try
            {
                _holder = new TablesHolder(jsonRoot);
            }
            catch (Exception e)
            {
                // Luban 生成代码在 JSON 结构不符时抛 SerializationException；
                // 文件缺失 / 为空在 TablesHolder 内抛 IOException 系。
                // 统一包成 ConfigLoadException，让 GameRoot 能区分「配置问题」与「代码问题」。
                throw new ConfigLoadException($"[Config] load failed: {e.Message}", e);
            }

            if (!StartupValidator.Validate(_holder))
            {
                _holder = null;
                throw new ConfigLoadException("[Config] startup validation failed");
            }

            _ready = true;
            Debug.Log($"[Config] initialized, tables loaded from: {jsonRoot}");
        }

        /// <summary>默认初始化：从 StreamingAssets/Luban 读取。调用方：GameRoot.Awake。</summary>
        public static void InitFromStreamingAssets()
        {
            Init(System.IO.Path.Combine(Application.streamingAssetsPath, "Luban"));
        }

        // ─────────────────────────────────────────────
        // 查询接口（薄转发给 Luban 的 TbXxx，不做二次封装）
        // 边界：id 不存在时 Luban 的 Get 会抛异常；ref 校验已在导表期由 --strict 保证
        // ─────────────────────────────────────────────

        public static cfg.demo.Weapon GetWeapon(int id)
        {
            EnsureReady();
            return _holder.Tables.TbWeapon.Get(id);
        }

        public static cfg.demo.Item GetItem(int id)
        {
            EnsureReady();
            return _holder.Tables.TbItem.Get(id);
        }

        public static cfg.demo.Fish GetFish(int id)
        {
            EnsureReady();
            return _holder.Tables.TbFish.Get(id);
        }

        public static IReadOnlyList<cfg.demo.Weapon> GetAllWeapons()
        {
            EnsureReady();
            return _holder.Tables.TbWeapon.DataList;
        }

        // ─────────────────────────────────────────────
        // 逃生舱：特殊情况直接访问原始 Tables
        // 边界：只读；调用方不得跨帧持有该引用
        // ─────────────────────────────────────────────

        public static cfg.Tables Tables
        {
            get
            {
                EnsureReady();
                return _holder.Tables;
            }
        }

        private static void EnsureReady()
        {
            if (!_ready)
                throw new InvalidOperationException("[Config] accessed before Init");
        }
    }

    /// <summary>
    /// 配置加载异常。独立定义，让 GameRoot 能区分「配置问题（重新导表）」与「代码问题」。
    /// </summary>
    public class ConfigLoadException : Exception
    {
        public ConfigLoadException(string message) : base(message) { }
        public ConfigLoadException(string message, Exception inner) : base(message, inner) { }
    }
}
