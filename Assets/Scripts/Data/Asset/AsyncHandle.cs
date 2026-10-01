using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 异步资源句柄。由 AssetModule.LoadAsync&lt;T&gt; 创建并返回。
    /// 调用时机：调用方 await 它拿资源本体。
    /// 边界：
    ///   - 必须是 class，不能是 struct（跨帧持有）
    ///   - Complete 只能在主线程调用；续体在主线程内联执行
    ///   - 故意不传 TaskCreationOptions.RunContinuationsAsynchronously：
    ///     那个选项会让续体走线程池，await 之后调用 Unity API 会崩。
    ///     Unity 的 Task 续体默认 posted 到 UnitySynchronizationContext（主线程）。
    ///   - 用 TrySetResult 而非 SetResult：重复完成不抛异常，只记警告
    ///     （资源降级路径不该因为框架内部时序问题炸掉调用方）
    /// </summary>
    public sealed class AsyncHandle<T> where T : UnityEngine.Object
    {
        private readonly TaskCompletionSource<T> _tcs = new TaskCompletionSource<T>();

        public bool IsDone { get; private set; }
        public T Asset { get; private set; }

        /// <summary>await 支持。调用方：var asset = await handle;</summary>
        public TaskAwaiter<T> GetAwaiter() => _tcs.Task.GetAwaiter();

        /// <summary>
        /// 完成句柄。调用方：AssetModule（加载完成 / 降级两条路径）。
        /// 边界：只允许 AssetModule 内部调用，调用方无法伪造完成。
        /// </summary>
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

        /// <summary>创建未完成的句柄。调用方：LoadAsync 未命中缓存时。</summary>
        internal static AsyncHandle<T> Create() => new AsyncHandle<T>();

        /// <summary>
        /// 创建已完成的句柄。调用方：LoadAsync 命中缓存时。
        /// 代价：命中路径也会分配一个句柄对象（一次 GC）。
        ///       换来调用方代码路径统一（命中与否都是 await，无分支）。
        /// </summary>
        internal static AsyncHandle<T> Completed(T asset)
        {
            var h = new AsyncHandle<T>();
            h.Complete(asset);
            return h;
        }
    }
}
