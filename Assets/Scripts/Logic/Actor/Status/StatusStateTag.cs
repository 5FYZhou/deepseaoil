namespace DeepseaOil.Logic
{
    /// <summary>状态效果层标签</summary>
    /// <remarks>Empty 为未进入状态哨兵，本层出门禁不写速度</remarks>
    public enum StatusStateTag
    {
        /// <summary>未进入状态哨兵</summary>
        Empty = 0,

        /// <summary>平常</summary>
        Normal,

        /// <summary>受击，外力接管速度、输入失效</summary>
        Hurt,
    }
}
