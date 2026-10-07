namespace DeepseaOil.Logic.Combat
{
    /// <summary>能被击退：接一次冲量。</summary>
    /// <remarks><b>命名风格与 <c>ISlowable</c> / <c>IStunnable</c> 一致</b>（能力接口一律 <c>-able</c> 后缀）。
    /// 参数是<b>速度</b>（单位/秒），不是"格数"——"退几格"是配置口径，换算成冲量是执行者（<c>GridLogic</c>）的事。</remarks>
    public interface IKnockBackable
    {
        /// <summary>接一次击退冲量（速度，单位/秒）；方向由执行者按"格心 → 目标"算好。</summary>
        void ApplyKnockback(UnityEngine.Vector2 impulse);
    }
}
