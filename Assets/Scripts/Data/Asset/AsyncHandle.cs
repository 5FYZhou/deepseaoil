using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 异步资源句柄，由 AssetModule.LoadAsync&lt;T&gt; 创建并返回；必须是 class 不能是 struct（跨帧持有）；Complete 只能在主线程调用，续体在主线程内联执行。
    /// 故意不传 TaskCreationOptions.RunContinuationsAsynchronously：那个选项会让续体走线程池，await 之后调 Unity API 会崩。
    /// 用 TrySetResult 而非 SetResult：重复完成不抛异常，只记警告（降级路径不该因为框架内部时序问题炸掉调用方）。
    /// </summary>
    public sealed class AsyncHandle<T> where T : UnityEngine.Object
    {
        private readonly TaskCompletionSource<T> _tcs = new TaskCompletionSource<T>();

        public bool IsDone { get; private set; }
        public T Asset { get; private set; }

        public TaskAwaiter<T> GetAwaiter() => _tcs.Task.GetAwaiter();

        internal bool Complete(T asset)
        {
            if (IsDone)
            {
                Debug.LogWarning($"[Asset] AsyncHandle<{typeof(T).Name}> completed twice, ignored");
                return false;
            }

            Asset = asset;
            IsDone = true;
            _tcs.TrySetResult(asset);
            return true;
        }

        internal static AsyncHandle<T> Create() => new AsyncHandle<T>();

        internal static AsyncHandle<T> Completed(T asset)
        {
            var h = new AsyncHandle<T>();
            h.Complete(asset);
            return h;
        }
    }
}
