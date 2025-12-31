using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace MirCommon.Network
{
    /// <summary>
    /// 零拷贝缓冲区
    /// </summary>
    public unsafe sealed class ZeroCopyBuffer : IDisposable
    {
        #region 内部结构
        /// <summary>
        /// 缓冲区块
        /// </summary>
        public sealed class BufferBlock : IDisposable
        {
            public IntPtr RawPointer { get; private set; }
            public byte[]? ManagedArray { get; private set; }
            public MemoryHandle MemoryHandle { get; private set; }
            public int Size { get; private set; }
            public bool IsPinned { get; private set; }
            public DateTime CreateTime { get; private set; }
            public DateTime LastUseTime { get; private set; }

            public BufferBlock(int size, bool useUnmanagedMemory = false)
            {
                Size = size;
                CreateTime = DateTime.UtcNow;
                LastUseTime = DateTime.UtcNow;

                if (useUnmanagedMemory)
                {
                    // 使用非托管内存（零GC压力）
                    RawPointer = Marshal.AllocHGlobal(size);
                    ManagedArray = null;
                    IsPinned = false;
                }
                else
                {
                    // 使用托管数组并固定内存
                    ManagedArray = new byte[size];
                    MemoryHandle = ManagedArray.AsMemory().Pin();
                    RawPointer = (nint)MemoryHandle.Pointer;
                    IsPinned = true;
                }
            }

            public void UpdateUseTime()
            {
                LastUseTime = DateTime.UtcNow;
            }

            public unsafe Span<byte> AsSpan()
            {
                if (ManagedArray != null)
                {
                    return new Span<byte>(ManagedArray);
                }
                else
                {
                    unsafe
                    {
                        void* pointer = (void*)RawPointer;
                        return new Span<byte>(pointer, Size);
                    }
                }
            }

            public unsafe Memory<byte> AsMemory()
            {
                if (ManagedArray != null)
                {
                    return new Memory<byte>(ManagedArray);
                }
                else
                {
                    // 对于非托管内存，创建包装器
                    unsafe
                    {
                        void* pointer = (void*)RawPointer;
                        return new UnmanagedMemoryManager<byte>(pointer, Size).Memory;
                    }
                }
            }

            public void Dispose()
            {
                if (IsPinned)
                {
                    MemoryHandle.Dispose();
                    ManagedArray = null;
                }
                else if (RawPointer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(RawPointer);
                    RawPointer = IntPtr.Zero;
                }
            }
        }

        /// <summary>
        /// 非托管内存管理器
        /// </summary>
        private sealed class UnmanagedMemoryManager<T> : MemoryManager<T> where T : unmanaged
        {
            private readonly unsafe T* _pointer;
            private readonly int _length;

            public unsafe UnmanagedMemoryManager(void* pointer, int length)
            {
                _pointer = (T*)pointer;
                _length = length;
            }

            public override unsafe Span<T> GetSpan()
            {
                return new Span<T>(_pointer, _length);
            }

            public override unsafe MemoryHandle Pin(int elementIndex = 0)
            {
                return new MemoryHandle(_pointer + elementIndex);
            }

            public override void Unpin()
            {
                // 非托管内存不需要unpin操作
            }

            protected override void Dispose(bool disposing)
            {
                // 内存由外部管理，这里不释放
            }
        }
        #endregion

        #region 常量
        private const int DEFAULT_BLOCK_SIZE = 8192;
        private const int MAX_BLOCK_SIZE = 65536;
        private const int MIN_BLOCK_SIZE = 1024;
        private const int MAX_POOL_SIZE = 1000;
        private const int CLEANUP_INTERVAL_MS = 60000; // 1分钟清理一次
        #endregion

        #region 字段
        private readonly ConcurrentDictionary<int, ConcurrentStack<BufferBlock>> _pool = new();
        private readonly bool _useUnmanagedMemory;
        private readonly object _cleanupLock = new();
        private DateTime _lastCleanupTime = DateTime.UtcNow;
        private volatile bool _isDisposed = false;
        #endregion

        #region 属性
        /// <summary>
        /// 总分配块数
        /// </summary>
        public int TotalBlocks { get; private set; }

        /// <summary>
        /// 当前池中块数
        /// </summary>
        public int PooledBlocks { get; private set; }

        /// <summary>
        /// 总分配字节数
        /// </summary>
        public long TotalBytes { get; private set; }

        /// <summary>
        /// 是否使用非托管内存
        /// </summary>
        public bool UseUnmanagedMemory => _useUnmanagedMemory;
        #endregion

        #region 构造函数
        /// <summary>
        /// 创建零拷贝缓冲区池
        /// </summary>
        /// <param name="useUnmanagedMemory">是否使用非托管内存（零GC压力）</param>
        public ZeroCopyBuffer(bool useUnmanagedMemory = false)
        {
            _useUnmanagedMemory = useUnmanagedMemory;
            TotalBlocks = 0;
            PooledBlocks = 0;
            TotalBytes = 0;
        }
        #endregion

        #region 公共方法
        /// <summary>
        /// 获取缓冲区（零拷贝）
        /// </summary>
        /// <param name="size">缓冲区大小</param>
        /// <returns>缓冲区块</returns>
        public BufferBlock Rent(int size = DEFAULT_BLOCK_SIZE)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(ZeroCopyBuffer));

            if (size <= 0)
                size = DEFAULT_BLOCK_SIZE;
            else if (size > MAX_BLOCK_SIZE)
                size = MAX_BLOCK_SIZE;

            // 对齐到最近的2的幂次方（优化内存分配）
            size = AlignToPowerOfTwo(size);

            // 尝试从池中获取
            if (_pool.TryGetValue(size, out var stack) && stack.TryPop(out var block))
            {
                var pooledBlocks = PooledBlocks;
                Interlocked.Decrement(ref pooledBlocks);
                PooledBlocks = pooledBlocks;
                block.UpdateUseTime();
                return block;
            }

            // 创建新块
            block = new BufferBlock(size, _useUnmanagedMemory);
            var totalBlocks = TotalBlocks;
            Interlocked.Increment(ref totalBlocks);
            TotalBlocks = totalBlocks;
            
            var totalBytes = TotalBytes;
            Interlocked.Add(ref totalBytes, size);
            TotalBytes = totalBytes;

            // 定期清理
            TryCleanup();

            return block;
        }

        /// <summary>
        /// 返回缓冲区到池中
        /// </summary>
        /// <param name="block">缓冲区块</param>
        public void Return(BufferBlock block)
        {
            if (_isDisposed || block == null)
                return;

            if (block.Size <= 0 || block.Size > MAX_BLOCK_SIZE)
            {
                // 无效大小的块直接释放
                block.Dispose();
                return;
            }

            // 检查池是否已满
            if (PooledBlocks >= MAX_POOL_SIZE)
            {
                block.Dispose();
                return;
            }

            var stack = _pool.GetOrAdd(block.Size, _ => new ConcurrentStack<BufferBlock>());
            stack.Push(block);
            var pooledBlocks = PooledBlocks;
            Interlocked.Increment(ref pooledBlocks);
            PooledBlocks = pooledBlocks;
            block.UpdateUseTime();
        }

        /// <summary>
        /// 创建内存段（避免拷贝）
        /// </summary>
        /// <param name="data">原始数据</param>
        /// <param name="offset">偏移量</param>
        /// <param name="count">数据长度</param>
        /// <returns>内存段</returns>
        public (BufferBlock? block, Memory<byte> memory) CreateSegment(byte[] data, int offset, int count)
        {
            if (data == null || count <= 0)
                return (null, Memory<byte>.Empty);

            var block = Rent(count);
            var span = block.AsSpan();

            // 只有需要时才拷贝数据
            if (offset == 0 && data.Length == count)
            {
                // 如果数据完全匹配，可以直接使用原始数组（避免拷贝）
                // 这里为了安全还是拷贝，实际可以根据情况优化
                data.AsSpan(0, count).CopyTo(span);
            }
            else
            {
                data.AsSpan(offset, count).CopyTo(span);
            }

            return (block, block.AsMemory().Slice(0, count));
        }

        /// <summary>
        /// 创建内存段（从Span）
        /// </summary>
        public (BufferBlock? block, Memory<byte> memory) CreateSegment(ReadOnlySpan<byte> data)
        {
            if (data.IsEmpty)
                return (null, Memory<byte>.Empty);

            var block = Rent(data.Length);
            data.CopyTo(block.AsSpan());
            return (block, block.AsMemory().Slice(0, data.Length));
        }

        /// <summary>
        /// 清理过期缓冲区
        /// </summary>
        /// <param name="maxAgeMinutes">最大年龄（分钟）</param>
        public void Cleanup(int maxAgeMinutes = 10)
        {
            if (_isDisposed)
                return;

            lock (_cleanupLock)
            {
                var now = DateTime.UtcNow;
                var maxAge = TimeSpan.FromMinutes(maxAgeMinutes);
                int removedCount = 0;

                foreach (var kvp in _pool)
                {
                    var stack = kvp.Value;
                    var tempList = new System.Collections.Generic.List<BufferBlock>();

                    // 收集过期块
                    while (stack.TryPop(out var block))
                    {
                        if (now - block.LastUseTime > maxAge)
                        {
                            block.Dispose();
                            removedCount++;
                        }
                        else
                        {
                            tempList.Add(block);
                        }
                    }

                    // 将未过期的块放回
                    foreach (var block in tempList)
                    {
                        stack.Push(block);
                    }
                }

                var pooledBlocks = PooledBlocks;
                Interlocked.Add(ref pooledBlocks, -removedCount);
                PooledBlocks = pooledBlocks;
                _lastCleanupTime = now;
            }
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public (int totalBlocks, int pooledBlocks, long totalBytes, bool useUnmanagedMemory) GetStatistics()
        {
            return (TotalBlocks, PooledBlocks, TotalBytes, _useUnmanagedMemory);
        }
        #endregion

        #region 私有方法
        /// <summary>
        /// 对齐到2的幂次方
        /// </summary>
        private static int AlignToPowerOfTwo(int size)
        {
            if (size <= MIN_BLOCK_SIZE)
                return MIN_BLOCK_SIZE;

            size--;
            size |= size >> 1;
            size |= size >> 2;
            size |= size >> 4;
            size |= size >> 8;
            size |= size >> 16;
            size++;

            return Math.Min(size, MAX_BLOCK_SIZE);
        }

        /// <summary>
        /// 尝试清理过期缓冲区
        /// </summary>
        private void TryCleanup()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastCleanupTime).TotalMilliseconds >= CLEANUP_INTERVAL_MS)
            {
                Cleanup();
            }
        }
        #endregion

        #region IDisposable实现
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            // 释放所有缓冲区
            foreach (var kvp in _pool)
            {
                while (kvp.Value.TryPop(out var block))
                {
                    block.Dispose();
                }
            }
            _pool.Clear();

            TotalBlocks = 0;
            PooledBlocks = 0;
            TotalBytes = 0;
        }
        #endregion
    }
}
