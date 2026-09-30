using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Logic.Pool
{
    public class Pool<T> where T : class
    {
        private readonly Stack<T> _pool = new();

        private readonly Func<T> _factory;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onRelease;

        public int Count => _pool.Count;

        public Pool(
            Func<T> factory,
            Action<T> onGet = null,
            Action<T> onRelease = null)
        {
            _factory = factory
                ?? throw new ArgumentNullException(nameof(factory));

            _onGet = onGet;
            _onRelease = onRelease;
        }

        public T Get()
        {
            T obj = _pool.Count > 0
                ? _pool.Pop()
                : _factory();

            _onGet?.Invoke(obj);

            return obj;
        }

        public void Release(T obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            _onRelease?.Invoke(obj);

            _pool.Push(obj);
        }

        public void Prewarm(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            for (int i = 0; i < count; i++)
            {
                _pool.Push(_factory());
            }
        }

        public void Clear()
        {
            _pool.Clear();
        }
    }
}