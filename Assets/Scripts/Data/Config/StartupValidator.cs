using System;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>启动时抽样校验，调用时机 ConfigModule.Init 中；不重复 Luban 的 ref/path/range 校验，失败通过返回值告知</summary>
    internal static class StartupValidator
    {
        public static bool Validate(TablesHolder holder)
        {
            if (holder == null || holder.Tables == null)
            {
                Debug.LogError("[Config] Validate failed: Tables is null");
                return false;
            }

            var tables = holder.Tables;

            if (TablesMeta.Names.Length == 0)
            {
                Debug.LogWarning("[Config] TablesMeta.Names is empty: 跳过抽样校验");
                return true;
            }

            foreach (var name in TablesMeta.Names)
            {
                var prop = tables.GetType().GetProperty(name);
                if (prop == null)
                {
                    Debug.LogError($"[Config] TablesMeta 里登记了不存在的表：{name}（生成物里没有这个属性）");
                    return false;
                }

                try
                {
                    _ = prop.GetValue(tables);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Config] Validate failed on table {name}: {e.Message}");
                    return false;
                }
            }

            return true;
        }
    }
}
