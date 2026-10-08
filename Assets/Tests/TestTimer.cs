using DeepseaOil.Logic.Services;
using DeepseaOil.Logic.Services.Time;
using UnityEngine;

/// <summary>计时器的<b>用法示例</b>（保留作参考，不被任何场景引用）。</summary>
/// <remarks>
/// <b>为什么不是 <c>MonoBehaviour</c></b>：计时器已脱单例（由 <c>GameRoot</c> 构造、进服务表驱动），
/// 场景里再挂一个自驱的组件去拿它，等于把刚收掉的"第二个驱动入口"又放回来 —— 那条纪律只写在
/// `AGENTS.md` 第 5 节与 `Docs/架构约束.md`（原先扫源码文本的 `收口一致性Tests` 已删）。
/// <para><b>为什么参数是 TimerManager 而不是自己去取</b>：这就是本轮的落点 —— 计时器从 <c>GameRoot.Timer</c> 出去，谁要用谁收参。</para>
/// </remarks>
public static class TestTimer
{
    private static TimerHandle _handle;

    /// <summary>四个入口各排一条，用来手测时间轮是否按预期触发。</summary>
    public static void ScheduleDemo(TimerManager timers)
    {
        if (timers == null)
        {
            Debug.LogWarning("[TestTimer] 没有计时器实例（应由 GameRoot.Timer 传入）。");

            return;
        }

        timers.Schedule(120f, () => Debug.Log("test scale"));

        _handle = timers.ScheduleUnscaled(3f, () => Debug.Log("test unscale"));

        timers.ScheduleRepeating(60f, () => Debug.Log("test ScheduleRepeating"));

        timers.ScheduleRepeatingUnscaled(10f, () => Debug.Log("test ScheduleRepeatingUnscaled"));
    }

    /// <summary>取消 <see cref="ScheduleDemo"/> 里那条非缩放的一次性 timer。</summary>
    public static bool CancelDemo(TimerManager timers)
    {
        if (timers == null) return false;

        bool cancelled = timers.Cancel(_handle);

        _handle = TimerHandle.Invalid;

        return cancelled;
    }

    /// <summary>读一次进度（UI 读条要用的那三件：已过 / 剩余 / 百分比）。</summary>
    public static bool TryGetProgress(TimerManager timers, out float progress, out float remaining)
    {
        progress = 0f;
        remaining = 0f;

        if (timers == null || !_handle.IsValid) return false;

        if (!timers.TryGetInfo(_handle, out TimerInfo info)) return false;

        progress = info.Progress;
        remaining = info.RemainingTime;

        return true;
    }
}
