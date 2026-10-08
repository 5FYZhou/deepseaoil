namespace DeepseaOil.Logic.Combat
{
    /// <summary>能被击退，接一次冲量；参数是速度(单位/秒)非格数，退几格由执行者换算</summary>
    public interface IKnockBackable
    {
        /// <summary>接一次击退冲量（速度，单位/秒），方向由执行者按格心→目标算好</summary>
        void ApplyKnockback(UnityEngine.Vector2 impulse);
    }
}
