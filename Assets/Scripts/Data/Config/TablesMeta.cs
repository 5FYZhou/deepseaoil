namespace DeepseaOil.Data
{
    /// <summary>
    /// 手写的表元信息清单，**非生成物**（不进 Generated 目录：导表时整目录镜像覆盖）；判据来自这里，不来自生成行。
    /// cfg.Tables 没有「表数量」属性、生成物不许手改：用它换来 DataMetrics.TableCount 与显式「关键表抽样」（非反射遍历全表）。
    /// 代价：加表后必须同步加一行（见 ConfigWorkspace/AGENTS.md：新表必须登记进 __tables__.xlsx），漏一行不报错。
    /// </summary>
    public static class TablesMeta
    {
        public static readonly string[] Names =
        {
            "TbWeapon",
            "TbItem",
            "TbFish",
            "TbProjectile",
            "TbEnemy",
            "TbTileState",
            "TbPlayer",
            "TbWave",
            "TbTileInitial",
            "TbElementRule",
            "TbTileEffect",
        };

        public static int Count => Names.Length;
    }
}
