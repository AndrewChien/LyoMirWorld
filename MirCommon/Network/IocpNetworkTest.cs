using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MirCommon.Utils;

namespace MirCommon.Network
{
    /// <summary>
    /// IOCP网络引擎测试程序
    /// </summary>
    public class IocpNetworkTest
    {
        private IocpNetworkEngine _serverEngine;
        private IocpNetworkEngine _clientEngine;
        private Socket _clientSocket;
        private bool _testCompleted = false;
        private readonly object _syncLock = new object();
        
        /// <summary>
        /// 运行IOCP网络引擎测试
        /// </summary>
        public async Task RunTest()
        {
            try
            {
                LogManager.Default.Info("开始IOCP网络引擎测试...");
                
                // 测试1：创建和销毁引擎
                await TestCreateAndDispose();
                
                // 测试2：启动和停止引擎
                await TestStartAndStop();
                
                // 测试3：监听和连接
                await TestListenAndConnect();
                
                // 测试4：发送和接收数据
                await TestSendAndReceive();
                
                // 测试5：并发连接测试
                await TestConcurrentConnections();
                
                LogManager.Default.Info("IOCP网络引擎测试完成！");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"IOCP网络引擎测试失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 测试1：创建和销毁引擎
        /// </summary>
        private async Task TestCreateAndDispose()
        {
            LogManager.Default.Info("测试1：创建和销毁引擎");
            
            try
            {
                // 创建引擎
                _serverEngine = new IocpNetworkEngine();
                LogManager.Default.Info("✓ 成功创建IOCP网络引擎");
                
                // 销毁引擎
                _serverEngine.Dispose();
                LogManager.Default.Info("✓ 成功销毁IOCP网络引擎");
                
                // 重新创建
                _serverEngine = new IocpNetworkEngine();
                LogManager.Default.Info("✓ 成功重新创建IOCP网络引擎");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"测试1失败: {ex.Message}");
                throw;
            }
            
            await Task.CompletedTask;
        }
        
        /// <summary>
        /// 测试2：启动和停止引擎
        /// </summary>
        private async Task TestStartAndStop()
        {
            LogManager.Default.Info("测试2：启动和停止引擎");
            
            try
            {
                // 启动引擎
                bool started = _serverEngine.Start(2); // 使用2个工作线程
                if (!started)
                {
                    throw new Exception("启动引擎失败");
                }
                LogManager.Default.Info("✓ 成功启动IOCP网络引擎");
                
                // 等待引擎初始化
                await Task.Delay(100);
                
                // 停止引擎
                _serverEngine.Stop();
                LogManager.Default.Info("✓ 成功停止IOCP网络引擎");
                
                // 重新启动
                started = _serverEngine.Start();
                if (!started)
                {
                    throw new Exception("重新启动引擎失败");
                }
                LogManager.Default.Info("✓ 成功重新启动IOCP网络引擎");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"测试2失败: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// 测试3：监听和连接
        /// </summary>
        private async Task TestListenAndConnect()
        {
            LogManager.Default.Info("测试3：监听和连接");
            
            try
            {
                // 启动客户端引擎
                _clientEngine = new IocpNetworkEngine();
                bool clientStarted = _clientEngine.Start(1);
                if (!clientStarted)
                {
                    throw new Exception("启动客户端引擎失败");
                }
                LogManager.Default.Info("✓ 成功启动客户端引擎");
                
                // 服务器监听端口
                string serverIp = "127.0.0.1";
                int serverPort = 8888;
                
                bool listenStarted = _serverEngine.StartListen(serverIp, serverPort, 4, 1);
                if (!listenStarted)
                {
                    throw new Exception("启动监听失败");
                }
                LogManager.Default.Info($"✓ 成功监听端口: {serverIp}:{serverPort}");
                
                // 注册服务器事件
                _serverEngine.OnConnection += OnServerConnection;
                _serverEngine.OnDataReceived += OnServerDataReceived;
                _serverEngine.OnDisconnection += OnServerDisconnection;
                
                // 注册客户端事件
                _clientEngine.OnConnection += OnClientConnection;
                _clientEngine.OnDataReceived += OnClientDataReceived;
                _clientEngine.OnDisconnection += OnClientDisconnection;
                
                // 客户端连接服务器
                await ConnectClientToServer(serverIp, serverPort);
                
                // 等待连接建立
                for (int i = 0; i < 50; i++) // 最多等待5秒
                {
                    if (_clientSocket != null && _clientSocket.Connected)
                    {
                        break;
                    }
                    await Task.Delay(100);
                }
                
                if (_clientSocket == null || !_clientSocket.Connected)
                {
                    throw new Exception("客户端连接服务器失败");
                }
                
                LogManager.Default.Info("✓ 客户端成功连接到服务器");
                
                // 等待一段时间让连接稳定
                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"测试3失败: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// 测试4：发送和接收数据
        /// </summary>
        private async Task TestSendAndReceive()
        {
            LogManager.Default.Info("测试4：发送和接收数据");
            
            try
            {
                if (_clientSocket == null || !_clientSocket.Connected)
                {
                    throw new Exception("客户端未连接");
                }
                
                // 重置测试完成标志
                _testCompleted = false;
                
                // 发送测试数据
                string testMessage = "Hello IOCP Server!";
                byte[] testData = Encoding.UTF8.GetBytes(testMessage);
                
                bool sent = _clientEngine.Send(_clientSocket, testData, 0, testData.Length);
                if (!sent)
                {
                    throw new Exception("发送数据失败");
                }
                
                LogManager.Default.Info($"✓ 客户端发送数据: {testMessage}");
                
                // 等待服务器接收数据
                for (int i = 0; i < 50; i++) // 最多等待5秒
                {
                    if (_testCompleted)
                    {
                        break;
                    }
                    await Task.Delay(100);
                }
                
                if (!_testCompleted)
                {
                    throw new Exception("服务器未收到数据");
                }
                
                LogManager.Default.Info("✓ 服务器成功接收数据");
                
                // 服务器回复数据
                string replyMessage = "Hello IOCP Client!";
                byte[] replyData = Encoding.UTF8.GetBytes(replyMessage);
                
                LogManager.Default.Info($"✓ 服务器回复数据: {replyMessage}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"测试4失败: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// 测试5：并发连接测试
        /// </summary>
        private async Task TestConcurrentConnections()
        {
            LogManager.Default.Info("测试5：并发连接测试");
            
            try
            {
                int connectionCount = 10;
                int successfulConnections = 0;
                
                LogManager.Default.Info($"开始并发连接测试，目标连接数: {connectionCount}");
                
                // 创建多个客户端连接
                var clients = new Socket[connectionCount];
                var tasks = new Task[connectionCount];
                
                for (int i = 0; i < connectionCount; i++)
                {
                    int clientIndex = i;
                    tasks[i] = Task.Run(async () =>
                    {
                        try
                        {
                            var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                            client.NoDelay = true;
                            
                            await client.ConnectAsync("127.0.0.1", 8888);
                            clients[clientIndex] = client;
                            
                            Interlocked.Increment(ref successfulConnections);
                            LogManager.Default.Debug($"客户端 {clientIndex + 1} 连接成功");
                            
                            // 发送测试数据
                            string message = $"Client {clientIndex + 1} Test";
                            byte[] data = Encoding.UTF8.GetBytes(message);
                            await client.SendAsync(data, SocketFlags.None);
                            
                            // 接收回复
                            byte[] buffer = new byte[1024];
                            int received = await client.ReceiveAsync(buffer, SocketFlags.None);
                            if (received > 0)
                            {
                                string reply = Encoding.UTF8.GetString(buffer, 0, received);
                                LogManager.Default.Debug($"客户端 {clientIndex + 1} 收到回复: {reply}");
                            }
                            
                            // 关闭连接
                            client.Shutdown(SocketShutdown.Both);
                            client.Close();
                        }
                        catch (Exception ex)
                        {
                            LogManager.Default.Error($"客户端 {clientIndex + 1} 连接失败: {ex.Message}");
                        }
                    });
                }
                
                // 等待所有任务完成
                await Task.WhenAll(tasks);
                
                LogManager.Default.Info($"并发连接测试完成，成功连接数: {successfulConnections}/{connectionCount}");
                
                if (successfulConnections == connectionCount)
                {
                    LogManager.Default.Info("✓ 所有并发连接测试成功");
                }
                else
                {
                    LogManager.Default.Warning($"部分并发连接失败: {connectionCount - successfulConnections} 个连接失败");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"测试5失败: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// 客户端连接服务器
        /// </summary>
        private async Task ConnectClientToServer(string ip, int port)
        {
            try
            {
                _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _clientSocket.NoDelay = true;
                
                await _clientSocket.ConnectAsync(ip, port);
                
                // 将客户端Socket绑定到客户端引擎
                LogManager.Default.Debug("客户端连接成功");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"客户端连接失败: {ex.Message}");
                throw;
            }
        }
        
        #region 服务器事件处理
        private void OnServerConnection(Socket socket, uint listenerId)
        {
            LogManager.Default.Info($"服务器: 新连接 - 远程地址: {socket.RemoteEndPoint}, 监听器ID: {listenerId}");
        }
        
        private void OnServerDataReceived(Socket socket, byte[] data, int length)
        {
            string message = Encoding.UTF8.GetString(data, 0, length);
            LogManager.Default.Info($"服务器: 收到数据 - 远程地址: {socket.RemoteEndPoint}, 数据: {message}");
            
            // 标记测试完成
            lock (_syncLock)
            {
                _testCompleted = true;
            }
            
            // 回复客户端
            try
            {
                string reply = "Server received: " + message;
                byte[] replyData = Encoding.UTF8.GetBytes(reply);
                _serverEngine.Send(socket, replyData, 0, replyData.Length);
                LogManager.Default.Debug($"服务器: 发送回复 - {reply}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"服务器发送回复失败: {ex.Message}");
            }
        }
        
        private void OnServerDisconnection(Socket socket)
        {
            LogManager.Default.Info($"服务器: 断开连接 - 远程地址: {socket.RemoteEndPoint}");
        }
        #endregion
        
        #region 客户端事件处理
        private void OnClientConnection(Socket socket, uint listenerId)
        {
            LogManager.Default.Info($"客户端: 新连接 - 远程地址: {socket.RemoteEndPoint}, 监听器ID: {listenerId}");
        }
        
        private void OnClientDataReceived(Socket socket, byte[] data, int length)
        {
            string message = Encoding.UTF8.GetString(data, 0, length);
            LogManager.Default.Info($"客户端: 收到数据 - 远程地址: {socket.RemoteEndPoint}, 数据: {message}");
        }
        
        private void OnClientDisconnection(Socket socket)
        {
            LogManager.Default.Info($"客户端: 断开连接 - 远程地址: {socket.RemoteEndPoint}");
        }
        #endregion
        
        /// <summary>
        /// 清理资源
        /// </summary>
        public void Cleanup()
        {
            try
            {
                if (_clientSocket != null)
                {
                    try
                    {
                        if (_clientSocket.Connected)
                        {
                            _clientSocket.Shutdown(SocketShutdown.Both);
                        }
                        _clientSocket.Close();
                    }
                    catch { }
                    _clientSocket = null;
                }
                
                if (_clientEngine != null)
                {
                    _clientEngine.Dispose();
                    _clientEngine = null;
                }
                
                if (_serverEngine != null)
                {
                    _serverEngine.Dispose();
                    _serverEngine = null;
                }
                
                LogManager.Default.Info("测试资源清理完成");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"清理资源失败: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// IOCP网络测试程序入口
    /// </summary>
    public class IocpNetworkTestProgram
    {
        public static async Task Main()
        {
            LogManager.Default.Info("=== IOCP网络引擎测试程序 ===");
            
            var test = new IocpNetworkTest();
            
            try
            {
                await test.RunTest();
                LogManager.Default.Info("=== 测试程序执行完成 ===");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"测试程序执行失败: {ex.Message}");
            }
            finally
            {
                test.Cleanup();
            }
            
            // 等待用户输入
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
        }
    }
}
