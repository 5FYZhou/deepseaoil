namespace DeepseaOil.Data
{
    /// <summary>
    /// 手写的表元信息清单。**非生成物**，物理位置在 Assets/Scripts/Game/ 下
    /// （Assets/Scripts/Config/ 是生成物专用目录，导表时整目录镜像覆盖）。
    ///
    /// 命名空间是 <c>DeepseaOil.Data</c> 而非 <c>DeepseaOil.Config</c>：层的归属**按命名空间判定**
    /// （蓝图 §1），而本类是纯 Data 层概念（只被 StartupValidator 与 DataMetrics 使用）。
    /// 早先放在 <c>DeepseaOil.Config</c> 会让 Data 层多出一条指向该命名空间的依赖边，
    /// 而那条边在蓝图 §1.1 的依赖表里并不存在。目录与命名空间不一一对应是允许的。
    ///
    /// 为什么需要它：cfg.Tables 没有「表数量」之类的属性，生成物又不允许手改。
    /// 用它换来两件事：
    ///   1. DataMetrics.TableCount 有数可报
    ///   2. StartupValidator 的「关键表抽样」成为显式决策，而不是反射遍历全部
    /// 代价：加表后要同步加一行（见 ConfigWorkspace/AGENTS.md：新表必须登记进 __tables__.xlsx）。
    /// </summary>
    public static class TablesMeta
    {
        /// <summary>cfg.Tables 的属性名清单。加表时同步。</summary>
        public static readonly string[] Names =
        {
            "TbWeapon",
            "TbItem",
            "TbFish",
        };

        public static int Count => Names.Length;
    }
}
