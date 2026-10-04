namespace DeepseaOil.Data
{
    /// <summary>
    /// 手写的表元信息清单。**非生成物**，物理位置在 <c>Assets/Scripts/Data/Config/</c> 下
    /// （<c>Assets/Scripts/Generated/Config/</c> 是生成物专用目录，导表时整目录镜像覆盖）。
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
            // 白模迁移新增（战斗切片）：投掷物 / 敌人 / 格子状态 / 玩家 / 波次 / 关卡初始格子
            "TbProjectile",
            "TbEnemy",
            "TbTileState",
            "TbPlayer",
            "TbWave",
            "TbTileInitial",
        };

        public static int Count => Names.Length;
    }
}
