namespace DeepseaOil.Data
{
    /// <summary>掉落物的种类：领取时回答"这是什么东西"；加一种 = 一个枚举成员 + <see cref="DropCatalog"/> 一行取值 + 组件工厂一行。</summary>
    public enum DropType
    {
        /// <summary>水球（<c>= 0</c>）：领取后进玩家的水球账本。</summary>
        Water = 0,
    }
}
