using System;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 启动时抽样校验。
    /// 调用时机：ConfigModule.Init 中，TablesHolder 构造完成后。
    /// 边界：
    ///   - 只确认「数据能被加载进内存」，不重复 Luban 的 ref / path / range 校验（那些在导表期由 --strict 完成）
    ///   - 抽样而非全遍历：全表遍历会拖慢启动
    ///   - 不抛异常，失败通过返回值告知调用方
    /// </summary>
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

            // 抽样访问每张已登记的表：触发其构造与索引建立
            // 关键表清单来自手写 TablesMeta（加表时同步维护，见 Docs/框架设计/分层设计/数据层.md §1）
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
