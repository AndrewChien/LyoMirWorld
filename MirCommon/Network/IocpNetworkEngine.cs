using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MirCommon.Utils;

namespace MirCommon.Network
{
    /// <summary>
    /// IOCP完成端口网络引擎
    /// </summary>
    public class IocpNetworkEngine : IDisposable
    {
        #region Windows IOCP API
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateIoCompletionPort(IntPtr fileHandle, IntPtr existingCompletionPort, UIntPtr completionKey, uint numberOfConcurrentThreads);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetQueuedCompletionStatus(IntPtr completionPort, out uint lpNumberOfBytesTransferred, out UIntPtr lpCompletionKey, out IntPtr lpOverlapped, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool PostQueuedCompletionStatus(IntPtr completionPort, uint dwNumberOfBytesTransferred, UIntPtr dwCompletionKey, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);
        #endregion

        #region 常量定义
        private const int DEFAULT_WORKER_THREADS = 4;
        private const int DEFAULT_POST_ACCEPT_COUNT = 64;
        private const int DEFAULT_BUFFER_SIZE = 8192;
        private const int MAX_PENDING_CONNECTIONS = 1000;
        #endregion

        #region 内部类
        /// <summary>
        /// IOCP操作类型
        /// </summary>
        private enum IocpOperation
        {
            Accept,
            Receive,
            Send,
            Disconnect
        }

        /// <summary>
        /// IOCP操作上下文
        /// </summary>
        private class IocpContext : IDisposable
        {
            public Socket Socket { get; set; }
            public IocpOperation Operation { get; set; }
            public byte[] Buffer { get; set; }
            public int Offset { get; set; }
            public int BytesTransferred { get; set; }
            public SocketAsyncEventArgs EventArgs { get; set; }
            public object UserToken { get; set; }
            public DateTime CreateTime { get; set; }

            public IocpContext()
            {
                CreateTime = DateTime.UtcNow;
            }

            public void Dispose()
            {
                EventArgs?.Dispose();
                Buffer = null;
                UserToken = null;
            }
        }

        /// <summary>
        /// 监听器对象
        /// </summary>
        private class Listener
        {
            public Socket Socket { get; set; }
            public IPEndPoint EndPoint { get; set; }
            public uint Id { get; set; }
            public int PostAcceptCount { get; set; }
            public DateTime CreateTime { get; set; }

            public Listener()
            {
                CreateTime = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// 临时客户端对象
        /// </summary>
        private class TempClient
        {
            public Socket Socket { get; set; }
            public uint ListenerId { get; set; }
            public DateTime AcceptTime { get; set; }
            public bool PreDeleted { get; set; }
            public DateTime DeleteTime { get; set; }

            public TempClient()
            {
                AcceptTime = DateTime.UtcNow;
                PreDeleted = false;
            }

            public bool IsDeleteTimeout(int timeoutMs = 10000)
            {
                if (!PreDeleted) return false;
                return (DateTime.UtcNow - DeleteTime).TotalMilliseconds >= timeoutMs;
            }

            public void PreDelete(int timeoutMs = 10000)
            {
                PreDeleted = true;
                DeleteTime = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            }
        }
        #endregion

        #region 字段
        private IntPtr _completionPort = IntPtr.Zero;
        private readonly List<Thread> _workerThreads = new List<Thread>();
        private readonly ConcurrentDictionary<uint, Listener> _listeners = new ConcurrentDictionary<uint, Listener>();
        private readonly ConcurrentDictionary<IntPtr, IocpContext> _contexts = new ConcurrentDictionary<IntPtr, IocpContext>();
        private readonly ConcurrentQueue<TempClient> _acceptQueue = new ConcurrentQueue<TempClient>();
        private readonly ConcurrentQueue<Socket> _disconnectQueue = new ConcurrentQueue<Socket>();
        private readonly ObjectPool<SocketAsyncEventArgs> _eventArgsPool;
        private readonly ZeroCopyBuffer _zeroCopyBuffer;

        private volatile bool _isRunning = false;
        private volatile bool _isDisposed = false;
        private readonly object _syncLock = new object();

        // 统计信息
        private long _totalRecvBytes = 0;
        private long _totalSendBytes = 0;
        private long _totalRecvPackets = 0;
        private long _totalSendPackets = 0;
        private long _totalConnections = 0;
        private long _totalDisconnections = 0;
        #endregion

        #region 事件
        /// <summary>
        /// 新连接事件
        /// </summary>
        public event Action<Socket, uint> OnConnection;

        /// <summary>
        /// 断开连接事件
        /// </summary>
        public event Action<Socket> OnDisconnection;

        /// <summary>
        /// 数据接收事件
        /// </summary>
        public event Action<Socket, byte[], int> OnDataReceived;

        /// <summary>
        /// 错误事件
        /// </summary>
        public event Action<Socket, Exception> OnError;
        #endregion

        #region 构造函数
        /// <summary>
        /// 创建IOCP网络引擎
        /// </summary>
        public IocpNetworkEngine(bool useUnmanagedMemory = false)
        {
            _eventArgsPool = new ObjectPool<SocketAsyncEventArgs>(() =>
            {
                var args = new SocketAsyncEventArgs();
                args.Completed += OnIoCompleted;
                return args;
            }, 1000);

            _zeroCopyBuffer = new ZeroCopyBuffer(useUnmanagedMemory);
        }
        #endregion

        #region 公共方法
        /// <summary>
        /// 启动IOCP引擎
        /// </summary>
        /// <param name="workerThreads">工作线程数</param>
        /// <returns>是否成功</returns>
        public bool Start(int workerThreads = DEFAULT_WORKER_THREADS)
        {
            lock (_syncLock)
            {
                if (_isRunning) return true;
                if (_isDisposed) throw new ObjectDisposedException(nameof(IocpNetworkEngine));

                try
                {
                    // 创建完成端口
                    _completionPort = CreateIoCompletionPort(new IntPtr(-1), IntPtr.Zero, UIntPtr.Zero, 0);
                    if (_completionPort == IntPtr.Zero)
                    {
                        int error = Marshal.GetLastWin32Error();
                        LogManager.Default.Error($"创建完成端口失败，错误代码: {error}");
                        return false;
                    }

                    // 创建工作线程
                    for (int i = 0; i < workerThreads; i++)
                    {
                        var thread = new Thread(WorkerThreadProc)
                        {
                            Name = $"IOCP Worker {i + 1}",
                            IsBackground = true
                        };
                        thread.Start();
                        _workerThreads.Add(thread);
                    }

                    _isRunning = true;
                    LogManager.Default.Info($"IOCP网络引擎已启动，工作线程数: {workerThreads}");
                    return true;
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"启动IOCP引擎失败: {ex.Message}");
                    Stop();
                    return false;
                }
            }
        }

        /// <summary>
        /// 停止IOCP引擎
        /// </summary>
        public void Stop()
        {
            lock (_syncLock)
            {
                if (!_isRunning) return;

                _isRunning = false;

                // 通知所有工作线程退出
                for (int i = 0; i < _workerThreads.Count; i++)
                {
                    PostQueuedCompletionStatus(_completionPort, 0, UIntPtr.Zero, IntPtr.Zero);
                }

                // 等待工作线程结束
                foreach (var thread in _workerThreads)
                {
                    try
                    {
                        if (thread.IsAlive)
                            thread.Join(5000);
                    }
                    catch { }
                }
                _workerThreads.Clear();

                // 关闭所有监听器
                foreach (var listener in _listeners.Values)
                {
                    try
                    {
                        listener.Socket?.Close();
                    }
                    catch { }
                }
                _listeners.Clear();

                // 清理完成端口
                if (_completionPort != IntPtr.Zero)
                {
                    CloseHandle(_completionPort);
                    _completionPort = IntPtr.Zero;
                }

                // 清理资源池
                _eventArgsPool.Clear();
                _zeroCopyBuffer?.Dispose();

                LogManager.Default.Info("IOCP网络引擎已停止");
            }
        }

        /// <summary>
        /// 开始监听端口
        /// </summary>
        /// <param name="ipAddress">IP地址</param>
        /// <param name="port">端口</param>
        /// <param name="postAcceptCount">预投递Accept数量</param>
        /// <param name="listenerId">监听器ID</param>
        /// <returns>是否成功</returns>
        public bool StartListen(string ipAddress, int port, int postAcceptCount = DEFAULT_POST_ACCEPT_COUNT, uint listenerId = 0)
        {
            if (!_isRunning) return false;

            try
            {
                var endPoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);
                var listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true,
                    ReceiveBufferSize = DEFAULT_BUFFER_SIZE,
                    SendBufferSize = DEFAULT_BUFFER_SIZE
                };

                listenerSocket.Bind(endPoint);
                listenerSocket.Listen(MAX_PENDING_CONNECTIONS);

                // 将监听Socket绑定到完成端口
                if (!BindSocketToCompletionPort(listenerSocket, listenerId))
                {
                    listenerSocket.Close();
                    return false;
                }

                var listener = new Listener
                {
                    Socket = listenerSocket,
                    EndPoint = endPoint,
                    Id = listenerId,
                    PostAcceptCount = postAcceptCount
                };

                if (!_listeners.TryAdd(listenerId, listener))
                {
                    listenerSocket.Close();
                    return false;
                }

                // 预投递Accept操作
                for (int i = 0; i < postAcceptCount; i++)
                {
                    PostAccept(listenerId);
                }

                LogManager.Default.Info($"开始监听: {endPoint}, 监听器ID: {listenerId}");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"开始监听失败 {ipAddress}:{port}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="socket">目标Socket</param>
        /// <param name="data">数据</param>
        /// <param name="offset">偏移量</param>
        /// <param name="count">数据长度</param>
        /// <returns>是否成功投递发送操作</returns>
        public bool Send(Socket socket, byte[] data, int offset, int count)
        {
            if (!_isRunning || socket == null || !socket.Connected)
                return false;

            try
            {
                var context = new IocpContext
                {
                    Socket = socket,
                    Operation = IocpOperation.Send,
                    Buffer = data,
                    Offset = offset,
                    BytesTransferred = 0
                };

                var args = _eventArgsPool.Get();
                args.UserToken = context;
                args.SetBuffer(data, offset, count);

                // 保存上下文引用
                var handle = GCHandle.Alloc(context);
                _contexts.TryAdd(GCHandle.ToIntPtr(handle), context);

                if (!socket.SendAsync(args))
                {
                    // 同步完成
                    ProcessSend(args);
                }

                Interlocked.Add(ref _totalSendBytes, count);
                Interlocked.Increment(ref _totalSendPackets);
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送数据失败: {ex.Message}");
                OnError?.Invoke(socket, ex);
                return false;
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        /// <param name="socket">要断开的Socket</param>
        public void Disconnect(Socket socket)
        {
            if (socket == null || !socket.Connected)
                return;

            try
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }
            catch { }
        }

        /// <summary>
        /// 更新引擎状态（处理断开连接队列等）
        /// </summary>
        public void Update()
        {
            // 处理断开连接队列
            while (_disconnectQueue.TryDequeue(out var socket))
            {
                try
                {
                    OnDisconnection?.Invoke(socket);
                    Interlocked.Increment(ref _totalDisconnections);
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"处理断开连接事件失败: {ex.Message}");
                }
            }

            // 处理Accept队列
            while (_acceptQueue.TryDequeue(out var tempClient))
            {
                try
                {
                    if (tempClient.IsDeleteTimeout())
                    {
                        // 超时删除
                        tempClient.Socket?.Close();
                        continue;
                    }

                    if (tempClient.PreDeleted)
                    {
                        // 标记为预删除，等待超时
                        _acceptQueue.Enqueue(tempClient);
                        continue;
                    }

                    // 处理新连接
                    OnConnection?.Invoke(tempClient.Socket, tempClient.ListenerId);
                    Interlocked.Increment(ref _totalConnections);
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"处理新连接事件失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public (long recvBytes, long sendBytes, long recvPackets, long sendPackets, long connections, long disconnections) GetStatistics()
        {
            return (_totalRecvBytes, _totalSendBytes, _totalRecvPackets, _totalSendPackets, _totalConnections, _totalDisconnections);
        }
        #endregion

        #region 私有方法
        /// <summary>
        /// 将Socket绑定到完成端口
        /// </summary>
        private bool BindSocketToCompletionPort(Socket socket, uint completionKey)
        {
            var handle = socket.Handle;
            var result = CreateIoCompletionPort(handle, _completionPort, new UIntPtr(completionKey), 0);
            return result != IntPtr.Zero;
        }

        /// <summary>
        /// 投递Accept操作
        /// </summary>
        private void PostAccept(uint listenerId)
        {
            if (!_listeners.TryGetValue(listenerId, out var listener))
                return;

            try
            {
                var acceptSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                var args = _eventArgsPool.Get();

                var context = new IocpContext
                {
                    Socket = acceptSocket,
                    Operation = IocpOperation.Accept,
                    UserToken = listenerId
                };

                args.UserToken = context;
                args.AcceptSocket = acceptSocket;

                // 保存上下文引用
                var handle = GCHandle.Alloc(context);
                _contexts.TryAdd(GCHandle.ToIntPtr(handle), context);

                if (!listener.Socket.AcceptAsync(args))
                {
                    // 同步完成
                    ProcessAccept(args);
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"投递Accept操作失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 投递接收操作（使用零拷贝缓冲区）
        /// </summary>
        private void PostReceive(Socket socket)
        {
            if (!socket.Connected) return;

            try
            {
                var bufferBlock = _zeroCopyBuffer.Rent();
                var args = _eventArgsPool.Get();

                var context = new IocpContext
                {
                    Socket = socket,
                    Operation = IocpOperation.Receive,
                    Buffer = bufferBlock.AsSpan().ToArray(), // 临时兼容，后续优化
                    UserToken = bufferBlock // 保存缓冲区块引用
                };

                args.UserToken = context;
                args.SetBuffer(bufferBlock.AsSpan().ToArray(), 0, bufferBlock.Size);

                // 保存上下文引用
                var handle = GCHandle.Alloc(context);
                _contexts.TryAdd(GCHandle.ToIntPtr(handle), context);

                if (!socket.ReceiveAsync(args))
                {
                    // 同步完成
                    ProcessReceive(args);
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"投递接收操作失败: {ex.Message}");
                OnError?.Invoke(socket, ex);
            }
        }

        /// <summary>
        /// IO完成回调
        /// </summary>
        private void OnIoCompleted(object sender, SocketAsyncEventArgs args)
        {
            switch (args.LastOperation)
            {
                case SocketAsyncOperation.Accept:
                    ProcessAccept(args);
                    break;
                case SocketAsyncOperation.Receive:
                    ProcessReceive(args);
                    break;
                case SocketAsyncOperation.Send:
                    ProcessSend(args);
                    break;
                case SocketAsyncOperation.Disconnect:
                    ProcessDisconnect(args);
                    break;
                default:
                    LogManager.Default.Warning($"未知的Socket操作: {args.LastOperation}");
                    break;
            }
        }

        /// <summary>
        /// 处理Accept完成
        /// </summary>
        private void ProcessAccept(SocketAsyncEventArgs args)
        {
            var context = args.UserToken as IocpContext;
            if (context == null) return;

            try
            {
                if (args.SocketError == SocketError.Success)
                {
                    var acceptSocket = args.AcceptSocket;
                    if (acceptSocket != null && acceptSocket.Connected)
                    {
                        // 配置接受的Socket
                        acceptSocket.NoDelay = true;
                        acceptSocket.ReceiveBufferSize = DEFAULT_BUFFER_SIZE;
                        acceptSocket.SendBufferSize = DEFAULT_BUFFER_SIZE;

                        // 将接受的Socket绑定到完成端口
                        uint listenerId = (uint)context.UserToken;
                        if (BindSocketToCompletionPort(acceptSocket, listenerId))
                        {
                            // 添加到Accept队列
                            var tempClient = new TempClient
                            {
                                Socket = acceptSocket,
                                ListenerId = listenerId
                            };
                            _acceptQueue.Enqueue(tempClient);

                            // 继续投递新的Accept操作
                            PostAccept(listenerId);

                            // 为新连接投递接收操作
                            PostReceive(acceptSocket);
                        }
                        else
                        {
                            acceptSocket.Close();
                        }
                    }
                }
                else
                {
                    LogManager.Default.Warning($"Accept失败: {args.SocketError}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理Accept完成失败: {ex.Message}");
            }
            finally
            {
                // 清理资源
                CleanupContext(context);
                _eventArgsPool.Return(args);
            }
        }

        /// <summary>
        /// 处理接收完成（使用零拷贝缓冲区）
        /// </summary>
        private void ProcessReceive(SocketAsyncEventArgs args)
        {
            var context = args.UserToken as IocpContext;
            if (context == null) return;

            try
            {
                if (args.SocketError == SocketError.Success && args.BytesTransferred > 0)
                {
                    var socket = context.Socket;
                    var bufferBlock = context.UserToken as ZeroCopyBuffer.BufferBlock;

                    Interlocked.Add(ref _totalRecvBytes, args.BytesTransferred);
                    Interlocked.Increment(ref _totalRecvPackets);

                    // 使用零拷贝方式传递数据
                    if (bufferBlock != null)
                    {
                        var memory = bufferBlock.AsMemory().Slice(0, args.BytesTransferred);
                        
                        // 触发数据接收事件（传递Memory<byte>而不是byte[]）
                        OnDataReceived?.Invoke(socket, memory.Span.ToArray(), args.BytesTransferred);
                        
                        // 返回缓冲区块到池中
                        _zeroCopyBuffer.Return(bufferBlock);
                    }
                    else
                    {
                        // 回退到传统方式
                        OnDataReceived?.Invoke(socket, context.Buffer, args.BytesTransferred);
                    }

                    // 继续投递接收操作
                    PostReceive(socket);
                }
                else if (args.SocketError != SocketError.Success || args.BytesTransferred == 0)
                {
                    // 连接断开或错误
                    var socket = context.Socket;
                    _disconnectQueue.Enqueue(socket);
                    
                    // 返回缓冲区块
                    var bufferBlock = context.UserToken as ZeroCopyBuffer.BufferBlock;
                    if (bufferBlock != null)
                    {
                        _zeroCopyBuffer.Return(bufferBlock);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理接收完成失败: {ex.Message}");
                var socket = context.Socket;
                OnError?.Invoke(socket, ex);
                _disconnectQueue.Enqueue(socket);
                
                // 返回缓冲区块
                var bufferBlock = context.UserToken as ZeroCopyBuffer.BufferBlock;
                if (bufferBlock != null)
                {
                    _zeroCopyBuffer.Return(bufferBlock);
                }
            }
            finally
            {
                // 清理资源
                CleanupContext(context);
                _eventArgsPool.Return(args);
            }
        }

        /// <summary>
        /// 处理发送完成
        /// </summary>
        private void ProcessSend(SocketAsyncEventArgs args)
        {
            var context = args.UserToken as IocpContext;
            if (context == null) return;

            try
            {
                if (args.SocketError != SocketError.Success)
                {
                    LogManager.Default.Warning($"发送失败: {args.SocketError}");
                    var socket = context.Socket;
                    _disconnectQueue.Enqueue(socket);
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理发送完成失败: {ex.Message}");
            }
            finally
            {
                // 清理资源
                CleanupContext(context);
                _eventArgsPool.Return(args);
            }
        }

        /// <summary>
        /// 处理断开连接完成
        /// </summary>
        private void ProcessDisconnect(SocketAsyncEventArgs args)
        {
            var context = args.UserToken as IocpContext;
            if (context == null) return;

            try
            {
                var socket = context.Socket;
                _disconnectQueue.Enqueue(socket);
            }
            finally
            {
                // 清理资源
                CleanupContext(context);
                _eventArgsPool.Return(args);
            }
        }

        /// <summary>
        /// 工作线程处理函数
        /// </summary>
        private void WorkerThreadProc()
        {
            while (_isRunning)
            {
                try
                {
                    bool success = GetQueuedCompletionStatus(
                        _completionPort,
                        out uint bytesTransferred,
                        out UIntPtr completionKey,
                        out IntPtr overlapped,
                        1000); // 1秒超时

                    if (!success)
                    {
                        // 超时或错误，继续循环
                        continue;
                    }

                    if (overlapped == IntPtr.Zero && completionKey == UIntPtr.Zero && bytesTransferred == 0)
                    {
                        // 退出信号
                        break;
                    }

                    // 处理完成的操作
                    if (_contexts.TryRemove(overlapped, out var context))
                    {
                        try
                        {
                            var handle = GCHandle.FromIntPtr(overlapped);
                            handle.Free();

                            // 根据操作类型处理
                            switch (context.Operation)
                            {
                                case IocpOperation.Accept:
                                    // Accept操作由SocketAsyncEventArgs回调处理
                                    break;
                                case IocpOperation.Receive:
                                    // 接收操作由SocketAsyncEventArgs回调处理
                                    break;
                                case IocpOperation.Send:
                                    // 发送操作由SocketAsyncEventArgs回调处理
                                    break;
                                case IocpOperation.Disconnect:
                                    // 断开连接操作
                                    var socket = context.Socket;
                                    _disconnectQueue.Enqueue(socket);
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            LogManager.Default.Error($"工作线程处理完成状态失败: {ex.Message}");
                        }
                        finally
                        {
                            context.Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"工作线程异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 清理上下文资源
        /// </summary>
        private void CleanupContext(IocpContext context)
        {
            if (context == null) return;

            try
            {
                // 从上下文字典中移除
                var handle = GCHandle.Alloc(context);
                var handlePtr = GCHandle.ToIntPtr(handle);
                _contexts.TryRemove(handlePtr, out _);
                handle.Free();

                context.Dispose();
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"清理上下文资源失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            lock (_syncLock)
            {
                if (_isDisposed) return;

                Stop();

                if (disposing)
                {
                    // 清理托管资源
                    foreach (var context in _contexts.Values)
                    {
                        context.Dispose();
                    }
                    _contexts.Clear();

                    _eventArgsPool.Dispose();
                    _zeroCopyBuffer?.Dispose();
                }

                _isDisposed = true;
            }
        }
        #endregion
    }
}
