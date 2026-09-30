namespace DeepseaOil.Logic
{
    /// <summary>
    /// 单个物理帧的环境检测结果，由执行器喂入逻辑层。
    /// </summary>
    public readonly struct WorldInfo
    {
        /// <summary>是否站在地面。</summary>
        public readonly bool Grounded;

        /// <summary>是否贴墙。</summary>
        public readonly bool TouchingWall;

        /// <summary>墙面所在方向：-1 在左，1 在右。</summary>
        public readonly int WallSide;

        public WorldInfo(bool grounded, bool touchingWall, int wallSide)
        {
            Grounded = grounded;
            TouchingWall = touchingWall;
            WallSide = wallSide;
        }
    }
}
