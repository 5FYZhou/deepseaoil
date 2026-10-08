using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>异步句柄；Complete 主线程调</summary>
    /// <remarks>故意不传 TaskCreationOptions.RunContinuationsAsynchronously：续体走线程池，await 后调 Unity API 会崩</remarks>
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
