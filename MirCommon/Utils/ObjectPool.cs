using System;
using System.Collections.Generic;

namespace MirCommon.Utils
{
    /// <summary>
    /// 简单对象池实现
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    public class ObjectPool<T> : IDisposable where T : class, new()
    {
        private readonly Stack<T> _pool;
        private readonly Func<T> _createFunc;
        private readonly int _maxSize;
        private int _createdCount;

        /// <summary>
        /// 创建对象池
        /// </summary>
        /// <param name="createFunc">创建对象的函数</param>
        /// <param name="initialSize">初始大小</param>
        /// <param name="maxSize">最大大小</param>
        public ObjectPool(Func<T>? createFunc = null, int initialSize = 10, int maxSize = 1000)
        {
            _createFunc = createFunc ?? (() => new T());
            _maxSize = maxSize;
            _pool = new Stack<T>(initialSize);
            _createdCount = 0;

            // 预创建一些对象
            for (int i = 0; i < initialSize; i++)
            {
                _pool.Push(_createFunc());
                _createdCount++;
            }
        }

        /// <summary>
        /// 从对象池获取对象
        /// </summary>
        public T? Get()
        {
            lock (_pool)
            {
                if (_pool.Count > 0)
                {
                    return _pool.Pop();
                }

                // 如果池为空且未达到最大大小，创建新对象
                if (_createdCount < _maxSize)
                {
                    _createdCount++;
                    return _createFunc();
                }

                // 达到最大大小，返回null
                return null;
            }
        }

        /// <summary>
        /// 将对象返回到对象池
        /// </summary>
        public void Return(T obj)
        {
            if (obj == null)
                return;

            lock (_pool)
            {
                // 如果池未满，将对象放回
                if (_pool.Count < _maxSize)
                {
                    _pool.Push(obj);
                }
                else
                {
                    // 池已满，丢弃对象
                    if (obj is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// 获取池中可用对象数量
        /// </summary>
        public int AvailableCount
        {
            get
            {
                lock (_pool)
                {
                    return _pool.Count;
                }
            }
        }

        /// <summary>
        /// 获取已创建对象总数
        /// </summary>
        public int CreatedCount
        {
            get
            {
                lock (_pool)
                {
                    return _createdCount;
                }
            }
        }

        /// <summary>
        /// 清理对象池
        /// </summary>
        public void Clear()
        {
            lock (_pool)
            {
                foreach (var obj in _pool)
                {
                    if (obj is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                _pool.Clear();
                _createdCount = 0;
            }
        }

        /// <summary>
        /// 获取池中所有对象（用于调试）
        /// </summary>
        public IEnumerable<T> GetAllObjects()
        {
            lock (_pool)
            {
                return _pool.ToArray();
            }
        }

        /// <summary>
        /// 释放对象池资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放对象池资源
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Clear();
            }
        }
    }
}
