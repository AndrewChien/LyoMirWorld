using MirCommon;
using MirCommon.Network;
using MirCommon.Utils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static MirCommon.Utils.GameEncoding;

namespace SelectCharServer
{
    class Program
    {
        private static SelectCharServer? _server;
        private static bool _isRunning = true;

        static async Task Main(string[] args)
        {
            Console.Title = "MirWorld SelectChar Server - C# 版本";
            Console.WriteLine("===========================================");
            Console.WriteLine("   传世角色选择服务器 - C# 版本");
            Console.WriteLine("===========================================");
            Console.WriteLine();

            try
            {
                //var configManager = new ConfigManager("selectchar_config.json");
                //configManager.Load();

                //var logConfig = ServerConfigHelper.LoadLogConfig(configManager);
                //var logger = new Logger(logConfig.directory, logConfig.writeToConsole, logConfig.writeToFile);
                //LogManager.SetDefaultLogger(logger);

                // 使用INI配置文件
                var iniReader = new IniFileReader("config.ini");
                if (!iniReader.Open())
                {
                    Console.WriteLine("无法打开配置文件 config.ini");
                    return;
                }

                _server = new SelectCharServer(iniReader);

                if (await _server.Initialize())
                {
                    LogManager.Default.Info("角色选择服务器初始化成功");
                    await _server.Start();
                    _ = Task.Run(() => CommandLoop());
                    
                    while (_isRunning)
                    {
                        await Task.Delay(1000);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Fatal("严重错误", exception: ex);
            }
            finally
            {
                _server?.Stop();
                LogManager.Shutdown();
            }
        }

        private static void CommandLoop()
        {
            Console.WriteLine("输入命令 (help/exit):");
            while (_isRunning)
            {
                Console.Write("> ");
                string? input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input)) continue;

                switch (input.Trim().ToLower())
                {
                    case "help":
                        Console.WriteLine("help/status/connections/clear/exit");
                        break;
                    case "exit":
                    case "quit":
                        _isRunning = false;
                        break;
                    case "status":
                        _server?.ShowStatus();
                        break;
                    case "connections":
                        Console.WriteLine($"连接数: {_server?.GetConnectionCount()}");
                        break;
                    case "clear":
                        Console.Clear();
                        break;
                }
            }
        }
    }

    public class SelectCharServer
    {
        private readonly IniFileReader _config;
        //private readonly ConfigManager _config;
        private TcpListener? _listener;
        private readonly List<SelectCharClient> _clients = new();
        private readonly object _clientLock = new();
        private readonly Dictionary<uint, (uint loginId, uint selectId, string account)> _loginInfo = new();
        private DateTime _startTime;
        private bool _isRunning = false;

        private string _addr = "127.0.0.1";
        private int _port = 7100;
        private string _name = "淡抹夕阳";
        private int _maxconnection = 1024;
        private string _dbServerAddress = "127.0.0.1";
        private int _dbServerPort = 8000;
        
        // ServerCenter连接
        private string _serverCenterAddress = "127.0.0.1";
        private int _serverCenterPort = 6000;
        private MirCommon.Network.ServerCenterClient? _serverCenterClient;
        private Task? _serverCenterTask;
        private byte _serverCenterIndex = 0; // 在ServerCenter中的索引

        // 公共属性用于获取服务器索引
        public byte ServerCenterIndex => _serverCenterIndex;

        public SelectCharServer(IniFileReader config)
        {
            _config = config;
        }

        public async Task<bool> Initialize()
        {
            try
            {
                // 从INI文件的[选人服务器]节读取配置
                string sectionName = "选人服务器";
                _addr = _config.GetString(sectionName, "addr", "127.0.0.1");
                _port = _config.GetInteger(sectionName, "port", 7100);
                _name = _config.GetString(sectionName, "name", " 淡抹夕阳");
                _maxconnection = _config.GetInteger(sectionName, "maxconnection", 1024);
                
                // 从INI文件的[数据库服务器]节读取DBServer配置
                string dbSectionName = "数据库服务器";
                _dbServerAddress = _config.GetString(dbSectionName, "addr", "127.0.0.1");
                _dbServerPort = _config.GetInteger(dbSectionName, "port", 8000);
                
                // 从INI文件的[服务器中心]节读取ServerCenter配置
                string scSectionName = "服务器中心";
                _serverCenterAddress = _config.GetString(scSectionName, "addr", "127.0.0.1");
                _serverCenterPort = _config.GetInteger(scSectionName, "port", 6000);

                LogManager.Default.Info($"监听端口: {_port}");
                LogManager.Default.Info($"最大连接: {_maxconnection}");
                LogManager.Default.Info($"ServerCenter地址: {_serverCenterAddress}:{_serverCenterPort}");
                
                // 向ServerCenter注册并保持连接
                LogManager.Default.Info("正在向ServerCenter注册...");
                _serverCenterClient = new MirCommon.Network.ServerCenterClient(_serverCenterAddress, _serverCenterPort);
                if (await _serverCenterClient.ConnectAsync())
                {
                    bool registered = await _serverCenterClient.RegisterServerAsync("SelectCharServer", _name, _addr, _port, _maxconnection);
                    if (registered)
                    {
                        LogManager.Default.Info("ServerCenter注册成功");
                        // 从ServerCenterClient获取服务器索引
                        _serverCenterIndex = _serverCenterClient.GetServerIndex();
                        LogManager.Default.Info($"获取到服务器索引: {_serverCenterIndex}");
                        
                        // 启动ServerCenter消息处理任务
                        _serverCenterTask = Task.Run(async () => await ProcessServerCenterMessagesAsync());
                        // 启动ServerCenter消息处理任务 - 使用新的简单方法
                        //_serverCenterTask = Task.Run(async () => await ProcessServerCenterMessagesSimpleAsync());
                    }
                    else
                    {
                        LogManager.Default.Warning("ServerCenter注册失败");
                    }
                }
                else
                {
                    LogManager.Default.Warning("无法连接到ServerCenter");
                }
                
                _startTime = DateTime.Now;
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error("初始化失败", exception: ex);
                return false;
            }
        }

        public async Task Start()
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            _isRunning = true;

            LogManager.Default.Info("服务器已启动");

            _ = Task.Run(async () =>
            {
                while (_isRunning)
                {
                    try
                    {
                        var client = await _listener.AcceptTcpClientAsync();
                        _ = Task.Run(() => HandleClient(client));
                    }
                    catch (Exception ex)
                    {
                        if (_isRunning)
                            LogManager.Default.Error($"接受连接错误: {ex.Message}");
                    }
                }
            });
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();
            
            // 停止ServerCenter连接
            if (_serverCenterClient != null)
            {
                _serverCenterClient.Disconnect();
                _serverCenterClient = null;
            }
            
            lock (_clientLock)
            {
                _clients.ForEach(c => c.Disconnect());
                _clients.Clear();
            }
        }

        private async Task HandleClient(TcpClient tcpClient)
        {
            var client = new SelectCharClient(tcpClient, this, _dbServerAddress, _dbServerPort);

            lock (_clientLock)
            {
                if (_clients.Count >= _maxconnection)
                {
                    tcpClient.Close();
                    return;
                }
                _clients.Add(client);
            }

            LogManager.Default.Info($"新连接: {tcpClient.Client.RemoteEndPoint}");

            try
            {
                await client.ProcessAsync();
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理客户端错误: {ex.Message}");
            }
            finally
            {
                lock (_clientLock)
                {
                    _clients.Remove(client);
                }
            }
        }

        public void RegisterLogin(uint loginId, uint selectId, string account)
        {
            lock (_loginInfo)
            {
                _loginInfo[selectId] = (loginId, selectId, account);
                LogManager.Default.Info($"注册登录信息: LoginId={loginId}, SelectId={selectId}, Account={account}");
            }
        }
        
        /// <summary>
        /// 处理ServerCenter消息
        /// </summary>
        /// <param name="wCmd">命令</param>
        /// <param name="wType">发送类型</param>
        /// <param name="wIndex">目标索引</param>
        /// <param name="data">消息数据（字节数组）</param>
        public async Task OnMASMsg(ushort wCmd, byte wType, ushort wIndex, byte[] data)
        {
            try
            {
                LogManager.Default.Info($"收到ServerCenter消息: Cmd={wCmd:X4}, Type={wType}, Index={wIndex}, 数据长度={data.Length}字节");
                
                byte senderServerType = (byte)((wType >> 4) & 0x0F);
                byte senderServerIndex = (byte)(wType & 0x0F);
                
                LogManager.Default.Info($"发送者信息: 服务器类型={senderServerType}, 服务器索引={senderServerIndex}");
                
                
                switch (wCmd)
                {
                    case ProtocolCmd.MAS_ENTERSELCHARSERVER:
                        await HandleEnterSelectCharServer(data, wIndex, senderServerType, senderServerIndex);
                        break;
                        
                    case ProtocolCmd.MAS_ENTERGAMESERVER:
                        // MAS_ENTERGAMESERVER消息需要转发给对应的客户端
                        // 消息格式: ENTERGAMESERVER结构体（64字节）
                        // 需要从结构体中提取clientId（nClientId字段）
                        await HandleForwardToClient(wCmd, wType, wIndex, data, senderServerType, senderServerIndex);
                        break;
                        
                    case ProtocolCmd.MAS_RESTARTGAME:
                        await HandleRestartGame(data, wIndex, senderServerType, senderServerIndex);
                        break;
                        
                    default:
                        LogManager.Default.Warning($"未知的ServerCenter消息: {wCmd:X4}");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理ServerCenter消息失败: {ex.Message}");
            }
            
            await Task.CompletedTask;
        }
        
        /// <summary>
        /// 处理重启游戏消息
        /// </summary>
        private async Task HandleRestartGame(byte[] data, ushort wIndex, byte senderServerType, byte senderServerIndex)
        {
            try
            {
                // 消息格式: ENTERGAMESERVER结构体
                if (data.Length >= 64) // ENTERGAMESERVER结构体大小为64字节
                {
                    // 将字节数组转换为结构体
                    var enterInfo = BytesToStruct<MirCommon.EnterGameServer>(data);
                    
                    // 添加进入账号
                    uint selectId = enterInfo.nSelCharId;
                    var result = AddEnterAccount(enterInfo.nLoginId, enterInfo.GetAccount(), ref selectId);
                    
                    if (result == SERVER_ERROR.SE_OK)
                    {
                        LogManager.Default.Info($"重启游戏: {enterInfo.GetAccount()}/{enterInfo.GetName()}");
                    }
                    else
                    {
                        LogManager.Default.Warning($"重启游戏失败: {enterInfo.GetAccount()}, 错误码: {result}");
                    }
                }
                else
                {
                    LogManager.Default.Error($"重启游戏消息数据长度不足: {data.Length}字节");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理重启游戏消息失败: {ex.Message}");
            }
            
            await Task.CompletedTask;
        }
        
        /// <summary>
        /// 将字节数组转换为结构体
        /// </summary>
        private T BytesToStruct<T>(byte[] bytes) where T : struct
        {
            int size = System.Runtime.InteropServices.Marshal.SizeOf<T>();
            if (bytes.Length < size)
                throw new ArgumentException($"字节数组长度不足: {bytes.Length} < {size}");
                
            IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
            try
            {
                System.Runtime.InteropServices.Marshal.Copy(bytes, 0, ptr, size);
                return System.Runtime.InteropServices.Marshal.PtrToStructure<T>(ptr);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
            }
        }
        
        /// <summary>
        /// 处理进入选人服务器消息
        /// </summary>
        private async Task HandleEnterSelectCharServer(byte[] data, ushort wIndex, byte senderServerType, byte senderServerIndex)
        {
            try
            {
                LogManager.Default.Info($"开始处理进入选人服务器消息: 数据长度={data.Length}字节，wIndex={wIndex}，发送者类型={senderServerType}，发送者索引={senderServerIndex}");
                
                // 消息格式: EnterSelCharServer结构体（24字节）
                if (data.Length < 24) // EnterSelCharServer结构体大小为24字节
                {
                    // 可能是响应消息或其他类型的消息，不报错直接返回
                    LogManager.Default.Warning($"收到非EnterSelCharServer结构体消息: 数据长度={data.Length}字节，可能是响应消息");
                    // 打印前24字节用于调试
                    if (data.Length > 0)
                    {
                        string hex = BitConverter.ToString(data, 0, Math.Min(data.Length, 24)).Replace("-", " ");
                        LogManager.Default.Warning($"数据前{Math.Min(data.Length, 24)}字节: {hex}");
                    }
                    return;
                }
                
        // 将字节数组转换为结构体
        var enterInfo = BytesToStruct<MirCommon.EnterSelCharServer>(data);
        
        uint clientId = enterInfo.nClientId;
        uint loginId = enterInfo.nLoginId;
        string account = enterInfo.GetAccount();
        uint selectId = 0; // 初始为0
        
        // 从保留字段读取SelectId
        if (enterInfo.reserved != null && enterInfo.reserved.Length >= 4)
        {
            byte[] selectIdBytes = new byte[4];
            Array.Copy(enterInfo.reserved, 0, selectIdBytes, 0, 4);
            // 注意：LoginServer不再反转字节序，直接读取
            selectId = BitConverter.ToUInt32(selectIdBytes, 0);
            
            if (selectId == 0)
            {
                LogManager.Default.Warning("从保留字段读取的SelectId为0，将生成新的SelectId");
            }
            else
            {
                LogManager.Default.Info($"从保留字段读取SelectId: {selectId}");
            }
        }
        else
        {
            LogManager.Default.Warning("保留字段无效，将生成新的SelectId");
        }
        
        LogManager.Default.Info($"收到登录信息: ClientId={clientId}, LoginId={loginId}, Account={account}, SelectId={selectId}");
        
        // 添加进入账号
        var result = AddEnterAccount(loginId, account, ref selectId);
                
                if (result == SERVER_ERROR.SE_OK)
                {
                    // 注册登录信息到服务器
                    RegisterLogin(loginId, selectId, account);
                    
                    // 注意：不发送响应给ServerCenter，因为：
                    // 1. LoginServer不等待响应（从日志看，它发送消息后立即显示"选择服务器成功"）
                    // 2. 发送响应会导致消息被错误地转发给SelectCharServer自己
                    LogManager.Default.Info($"登录成功处理完成: {account}, SelectId={selectId}");
                }
                else
                {
                    // 失败，不发送错误响应
                    LogManager.Default.Warning($"添加进入账号失败: {account}, 错误码: {result}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理进入选人服务器消息失败: {ex.Message}");
            }
            
            await Task.CompletedTask;
        }
        
        /// <summary>
        /// 添加进入账号
        /// </summary>
        private SERVER_ERROR AddEnterAccount(uint loginId, string account, ref uint selectId)
        {
            try
            {
                // 生成SelectId（如果为0则随机生成）
                if (selectId == 0)
                {
                    Random rand = new Random();
                    selectId = (uint)rand.Next(100000000, 999999999);
                }
                
                // 检查是否已存在相同的(loginId, account)组合
                lock (_loginInfo)
                {
                    // 先检查是否已存在相同的(loginId, account)组合
                    foreach (var kvp in _loginInfo)
                    {
                        if (kvp.Value.loginId == loginId && kvp.Value.account == account)
                        {
                            // 如果已存在相同的(loginId, account)，返回失败
                            // 这表示同一个账号使用相同的loginId重复登录
                            LogManager.Default.Warning($"重复登录: LoginId={loginId}, Account={account}");
                            return SERVER_ERROR.SE_FAIL;
                        }
                    }
                    
                    // 添加登录信息
                    _loginInfo[selectId] = (loginId, selectId, account);
                    LogManager.Default.Info($"添加进入账号: LoginId={loginId}, SelectId={selectId}, Account={account}");
                    
                    return SERVER_ERROR.SE_OK;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"添加进入账号失败: {ex.Message}");
                return SERVER_ERROR.SE_FAIL; // 使用SE_FAIL代替SE_SERVERFULL
            }
        }

        public bool GetLoginInfo(uint selectId, out (uint loginId, uint selectId, string account) info)
        {
            lock (_loginInfo)
            {
                if (_loginInfo.TryGetValue(selectId, out info))
                {
                    _loginInfo.Remove(selectId);
                    LogManager.Default.Info($"删除登录信息: SelectId={selectId}");
                    return true;
                }
                return false;
            }
        }

        public void ShowStatus()
        {
            Console.WriteLine($"运行时间: {DateTime.Now - _startTime}");
            Console.WriteLine($"连接数: {GetConnectionCount()}");
        }

        public int GetConnectionCount()
        {
            lock (_clientLock) { return _clients.Count; }
        }
        
        /// <summary>
        /// 处理ServerCenter消息
        /// </summary>
        private async Task ProcessServerCenterMessagesAsync()
        {
            if (_serverCenterClient == null)
                return;
                
            try
            {
                LogManager.Default.Info("ServerCenter消息处理任务已启动");
                
                var clientType = _serverCenterClient.GetType();
                var clientField = clientType.GetField("_client", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var streamField = clientType.GetField("_stream", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (clientField == null || streamField == null)
                {
                    LogManager.Default.Error("无法访问ServerCenterClient的内部字段");
                    return;
                }
                
                var tcpClient = clientField.GetValue(_serverCenterClient) as TcpClient;
                var networkStream = streamField.GetValue(_serverCenterClient) as NetworkStream;
                
                if (tcpClient == null || networkStream == null)
                {
                    LogManager.Default.Error("无法获取ServerCenterClient的TcpClient或NetworkStream");
                    return;
                }
                
                byte[] buffer = new byte[8192];
                
                while (_isRunning && tcpClient.Connected)
                {
                    try
                    {
                        if (networkStream.DataAvailable)
                        {
                            int bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length);
                            if (bytesRead > 0)
                            {
                                await ProcessServerCenterMessage(buffer, bytesRead);
                            }
                        }
                        else
                        {
                            await Task.Delay(100);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_isRunning)
                        {
                            LogManager.Default.Error($"读取ServerCenter消息失败: {ex.Message}");
                            break;
                        }
                    }
                }
                
                LogManager.Default.Info("ServerCenter消息处理任务已停止");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理ServerCenter消息失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 处理ServerCenter消息
        /// </summary>
        private async Task ProcessServerCenterMessage(byte[] data, int length)
        {
            if (length < 12) return;
            
            try
            {
                // 解析消息头
                var reader = new PacketReader(data);
                uint clientId = reader.ReadUInt32();
                ushort wCmd = reader.ReadUInt16();
                ushort w1 = reader.ReadUInt16();
                ushort w2 = reader.ReadUInt16();
                ushort w3 = reader.ReadUInt16();
                byte[] payload = reader.ReadBytes(length - 12);
                
                // 记录原始数据长度
                LogManager.Default.Info($"收到ServerCenter消息: ClientId={clientId}, Cmd={wCmd:X4}, w1={w1}, w2={w2}, w3={w3}, 数据长度={payload.Length}字节");
                
                // 处理跨服务器消息：当wCmd是SCM_MSGACROSSSERVER时，w1是真正的命令
                if (wCmd == ProtocolCmd.SCM_MSGACROSSSERVER)
                {
                    // w1是原始命令（如MAS_ENTERSELCHARSERVER）
                    // w2是发送类型（包含发送者服务器类型和索引）
                    // w3是目标索引（应该是本服务器的索引）
                    ushort originalCmd = w1;
                    byte sendType = (byte)w2;
                    ushort targetIndex = w3;
                    
                    LogManager.Default.Info($"处理跨服务器消息: 原始命令={originalCmd:X4}, 发送类型={sendType:X2}, 目标索引={targetIndex}");
                    
                    // 调用OnMASMsg方法处理消息，传递字节数组
                    await OnMASMsg(originalCmd, sendType, targetIndex, payload);
                }
                else
                {
                    // 对于其他消息，直接调用OnMASMsg
                    await OnMASMsg(wCmd, (byte)w2, w3, payload);
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析ServerCenter消息失败: {ex.Message}");
            }
            
            await Task.CompletedTask;
        }
        
        /// <summary>
        /// 处理转发给客户端的ServerCenter消息
        /// </summary>
        private async Task HandleForwardToClient(ushort wCmd, byte wType, ushort wIndex, byte[] data, byte senderServerType, byte senderServerIndex)
        {
            try
            {
                // 对于MAS_ENTERGAMESERVER消息，需要从ENTERGAMESERVER结构体中提取clientId
                if (wCmd == ProtocolCmd.MAS_ENTERGAMESERVER)
                {
                    if (data.Length >= 64) // ENTERGAMESERVER结构体大小为64字节
                    {
                        var enterInfo = BytesToStruct<MirCommon.EnterGameServer>(data);
                        uint clientId = enterInfo.nClientId;
                        
                        LogManager.Default.Debug($"转发MAS_ENTERGAMESERVER消息给客户端: ClientId={clientId}, LoginId={enterInfo.nLoginId}, SelCharId={enterInfo.nSelCharId}");
                        
                        // 根据clientId查找对应的客户端
                        SelectCharClient? targetClient = null;
                        lock (_clientLock)
                        {
                            foreach (var client in _clients)
                            {
                                // 这里需要根据clientId匹配客户端
                                if (client._selectId == enterInfo.nSelCharId && client._loginId == enterInfo.nLoginId)
                                {
                                    targetClient = client;
                                    break;
                                }
                            }
                        }
                        
                        if (targetClient != null)
                        {
                            // 将字节数组转换为字符串传递给客户端
                            string dataStr = System.Text.Encoding.GetEncoding("GBK").GetString(data).TrimEnd('\0');
                            await targetClient.OnMASMsg(wCmd, wType, wIndex, dataStr);
                            LogManager.Default.Info($"已转发MAS_ENTERGAMESERVER消息给客户端: {enterInfo.GetAccount()}/{enterInfo.GetName()}");
                        }
                        else
                        {
                            LogManager.Default.Warning($"未找到对应的客户端: ClientId={clientId}, LoginId={enterInfo.nLoginId}, SelCharId={enterInfo.nSelCharId}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理转发给客户端的消息失败: {ex.Message}");
            }
            
            await Task.CompletedTask;
        }
        
        /// <summary>
        /// 将ServerCenter消息转发给指定的客户端（用于SCM_GETGAMESERVERADDR等消息）
        /// </summary>
        public async Task ForwardServerCenterMessageToClient(uint clientId, ushort wCmd, ushort w1, ushort w2, ushort w3, string data)
        {
            try
            {
                lock (_clientLock)
                {
                    // 查找对应的客户端
                    foreach (var client in _clients)
                    {
                        // 这里需要根据clientId找到对应的客户端
                        if (client._selectId == clientId)
                        {
                            // 调用客户端的OnSCMsg方法
                            _ = client.OnSCMsg(wCmd, w1, w2, w3, data);
                            LogManager.Default.Debug($"已转发ServerCenter消息给客户端: ClientId={clientId}, Cmd={wCmd:X4}");
                            return;
                        }
                    }
                    
                    LogManager.Default.Warning($"未找到对应的客户端: ClientId={clientId}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"转发ServerCenter消息给客户端失败: {ex.Message}");
            }
            
            await Task.CompletedTask;
        }
    }

    public class SelectCharClient
    {
        private readonly TcpClient _client;
        private readonly SelectCharServer _server;
        private readonly NetworkStream _stream;
        private string _account = string.Empty;
        internal uint _loginId = 0;
        internal uint _selectId = 0;
        private DateTime _lastActivity;
        private int _failCount = 0;
        private bool _verified = false;
        private bool _selected = false;
        private string _selectedCharName = string.Empty;
        private readonly string _dbServerAddress;
        private readonly int _dbServerPort;
        
        // 公共属性用于外部访问
        public uint LoginId => _loginId;
        public uint SelectId => _selectId;
        public string Account => _account;
        public bool Verified => _verified;

        public SelectCharClient(TcpClient client, SelectCharServer server, string dbServerAddress, int dbServerPort)
        {
            _client = client;
            _server = server;
            _dbServerAddress = dbServerAddress;
            _dbServerPort = dbServerPort;
            _stream = client.GetStream();
            _lastActivity = DateTime.Now;
        }

        public async Task ProcessAsync()
        {
            byte[] buffer = new byte[8192];

            while (_client.Connected)
            {
                try
                {
                    if ((DateTime.Now - _lastActivity).TotalMinutes > 5)
                        break;

                    if (_failCount >= 16)
                        break;

                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    _lastActivity = DateTime.Now;
                    await ProcessMessage(buffer, bytesRead);
                }
                catch { break; }
            }
        }

        private async Task ProcessMessage(byte[] data, int length)
        {
            try
            {
                int parsedSize = 0;
                int msgPtr = 0;
                
                do
                {
                    parsedSize = ParseSingleMessage(data, msgPtr, length - msgPtr);
                    if (parsedSize > 0)
                    {
                        msgPtr += parsedSize;
                    }
                } while (parsedSize > 0 && msgPtr < length);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析消息失败: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 解析单个消息
        /// </summary>
        private int ParseSingleMessage(byte[] data, int startIndex, int length)
        {
            char startChar = '#';
            char endChar = '!';
            int parsedSize = 0;
            int messageStart = -1;
            
            for (int i = 0; i < length; i++)
            {
                int currentIndex = startIndex + i;
                char currentChar = (char)data[currentIndex];
                
                if (currentChar == '*')
                {
                    // 心跳包响应
                    parsedSize = i + 1;
                }
                else if (currentChar == startChar)
                {
                    messageStart = currentIndex + 1; // '#'后面的位置
                }
                else if (currentChar == endChar)
                {
                    if (messageStart != -1)
                    {
                        // 处理从messageStart到currentIndex之间的消息
                        int encodedLength = currentIndex - messageStart;
                        byte[] encodedData = new byte[encodedLength];
                        Array.Copy(data, messageStart, encodedData, 0, encodedLength);
                        
                        // 检查第一个字符是否是数字（'0'-'9'），如果是则跳过
                        int decodeStart = 0;
                        if (encodedLength > 0 && encodedData[0] >= '0' && encodedData[0] <= '9')
                        {
                            decodeStart = 1;
                        }
                        
                        // 解码游戏消息
                        byte[] decoded = new byte[(encodedLength - decodeStart) * 3 / 4 + 4];
                        byte[] dataToDecode = new byte[encodedLength - decodeStart];
                        Array.Copy(encodedData, decodeStart, dataToDecode, 0, dataToDecode.Length);
                        int decodedSize = GameCodec.UnGameCode(dataToDecode, decoded);
                        
                        if (decodedSize >= 12) // 消息头至少12字节
                        {
                            var reader = new PacketReader(decoded);
                            uint dwFlag = reader.ReadUInt32();
                            ushort wCmd = reader.ReadUInt16();
                            ushort w1 = reader.ReadUInt16();
                            ushort w2 = reader.ReadUInt16();
                            ushort w3 = reader.ReadUInt16();
                            byte[] payload = reader.ReadBytes(decodedSize - 12);
                            
                            // 编码检测：根据客户端发送的数据自动选择编码
                            string dataStr = DetectAndDecodeString(payload, wCmd);

                            // 添加详细的调试日志
                            if (wCmd == ProtocolCmd.CM_QUERYSELECTCHAR)
                            {
                                LogManager.Default.Debug($"收到选择角色消息 - 原始字节: {BitConverter.ToString(payload)}");
                                LogManager.Default.Debug($"收到选择角色消息 - 最终解码: {dataStr}");
                                
                                // 记录两种编码的解码结果用于调试
                                string gbkStr = GetString(payload);
                                string utf8Str = System.Text.Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
                                LogManager.Default.Debug($"收到选择角色消息 - GBK解码: {gbkStr}");
                                LogManager.Default.Debug($"收到选择角色消息 - UTF8解码: {utf8Str}");
                            }
                            
                            LogManager.Default.Debug($"收到: Cmd={wCmd:X4}, Data={dataStr}");

                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await ProcessDecodedMessage(wCmd, dataStr);
                                }
                                catch (Exception ex)
                                {
                                    LogManager.Default.Error($"处理消息失败: {ex.Message}");
                                }
                            });
                        }
                        else
                        {
                            LogManager.Default.Warning($"解码后数据太小: {decodedSize}字节");
                        }
                        
                        messageStart = -1;
                    }
                    parsedSize = i + 1;
                }
            }
            
            return parsedSize;
        }

        /// <summary>
        /// 处理解码后的消息
        /// </summary>
        private async Task ProcessDecodedMessage(ushort wCmd, string dataStr)
        {
            switch (wCmd)
            {
                case ProtocolCmd.CM_QUERYCHARLIST:
                    await HandleQueryCharList(dataStr);
                    break;
                case ProtocolCmd.CM_QUERYCREATECHAR when _verified:
                    await HandleCreateChar(dataStr);
                    break;
                case ProtocolCmd.CM_QUERYDELCHAR when _verified:
                    await HandleDeleteChar(dataStr);
                    break;
                case ProtocolCmd.CM_QUERYUNDELCHAR when _verified:
                    await HandleRestoreChar(dataStr);
                    break;
                case ProtocolCmd.CM_QUERYDELCHARLIST when _verified:
                    await HandleQueryDelCharList();
                    break;
                case ProtocolCmd.CM_QUERYSELECTCHAR when _verified:
                    await HandleSelectChar(dataStr);
                    break;
                default:
                    _failCount++;
                    LogManager.Default.Warning($"未知消息: {wCmd:X4}");
                    break;
            }
        }

        private async Task HandleQueryCharList(string data)
        {
            try
            {
                // "*loginid/selectid"
                string[] parts = data.TrimStart('*').Split('/');
                if (parts.Length < 2) return;

                uint requestLoginId = uint.Parse(parts[0]);
                uint requestSelectId = uint.Parse(parts[1]);

                LogManager.Default.Info($"收到查询角色列表请求: LoginId={requestLoginId}, SelectId={requestSelectId}");
                
                // 检查服务器中是否有登录信息
                bool hasLoginInfo = _server.GetLoginInfo(requestSelectId, out var loginInfo);
                LogManager.Default.Info($"服务器登录信息检查: 存在={hasLoginInfo}");
                
                if (hasLoginInfo)
                {
                    LogManager.Default.Info($"找到登录信息: LoginId={loginInfo.loginId}, Account={loginInfo.account}");
                    
                    if (loginInfo.loginId == requestLoginId)
                    {
                        _verified = true;
                        _account = loginInfo.account;
                        _loginId = requestLoginId;
                        _selectId = requestSelectId;
                        LogManager.Default.Info($"账号 {_account} 通过验证");
                    }
                    else
                    {
                        LogManager.Default.Warning($"登录ID不匹配: 请求的LoginId={requestLoginId}, 存储的LoginId={loginInfo.loginId}");
                        SendMessage(0, ProtocolCmd.SM_QUERYCHR_FAIL, 0, 0, 0, null);
                        return;
                    }
                }
                else
                {
                    // 如果找不到登录信息，检查是否已经验证过（比如创建角色后重新查询）
                    if (!_verified || _selectId != requestSelectId || _loginId != requestLoginId)
                    {
                        // 未验证且找不到登录信息，发送失败消息并等待10秒后断开连接
                        LogManager.Default.Warning($"找不到登录信息: LoginId={requestLoginId}, SelectId={requestSelectId}");
                        SendMessage(0, ProtocolCmd.SM_QUERYCHR_FAIL, 0, 0, 0, null);
                        
                        // 等待10秒后断开连接
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(10000); // 等待10秒
                            LogManager.Default.Info($"等待10秒后断开连接: LoginId={requestLoginId}, SelectId={requestSelectId}");
                            Disconnect();
                        });
                        return;
                    }
                    // 如果已经验证过且ID匹配，继续使用当前账号
                }

                LogManager.Default.Info($"查询角色列表: {_account}");

                // 从DBServer获取角色列表
                LogManager.Default.Debug($"开始连接DBServer: {_dbServerAddress}:{_dbServerPort}");
                using var dbClient = new MirCommon.Database.DBServerClient(_dbServerAddress, _dbServerPort);
                if (!await dbClient.ConnectAsync())
                {
                    LogManager.Default.Error("无法连接到DBServer");
                    SendMessage(0, ProtocolCmd.SM_QUERYCHR_FAIL, 0, 0, 0, null);
                    return;
                }
                LogManager.Default.Debug($"已连接到DBServer");

                // 从配置读取服务器名称
                string serverName = "淡抹夕阳"; // 默认服务器名称
                LogManager.Default.Debug($"开始查询角色列表 - 账号: {_account}, 服务器: {serverName}");
                string? charListData = await dbClient.QueryCharListAsync(_account, serverName);
                
                if (string.IsNullOrEmpty(charListData))
                {
                    // 没有角色，返回空列表
                    charListData = "";
                    LogManager.Default.Info($"账号 {_account} 角色列表为空");
                }
                else
                {
                    LogManager.Default.Debug($"收到角色列表数据，长度: {charListData.Length}字符");
                    LogManager.Default.Debug($"角色列表数据: {charListData}");
                }

                // 数据格式: "name/class/hair/level/sex/"（*表示上次登录角色）
                // 需要从数据库查询上次登录的角色，并在其名字前加'*'
                
                // 计算角色数量
                int charCount = 0;
                if (!string.IsNullOrEmpty(charListData))
                {
                    // 计算有多少个角色（每个角色以"/"结尾，但最后一个可能没有）
                    // 简单方法：统计"name/class/hair/level/sex/"模式的数量
                    string[] segments = charListData.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    charCount = segments.Length / 5; // 每个角色有5个字段
                }
                
                SendMessage((uint)charCount, ProtocolCmd.SM_CHARLIST, 0, 0, 1, 
                    System.Text.Encoding.GetEncoding("GBK").GetBytes(charListData));
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"查询角色列表失败: {ex.Message}");
                SendMessage(0, ProtocolCmd.SM_QUERYCHR_FAIL, 0, 0, 0, null);
            }

            await Task.CompletedTask;
        }

        private async Task HandleCreateChar(string data)
        {
            try
            {
                // "*loginid/name/hair/class/sex"
                string[] parts = data.TrimStart('*').Split('/');
                if (parts.Length < 5) return;

                string name = parts[1];
                byte hair = byte.Parse(parts[2]);
                byte prof = byte.Parse(parts[3]);
                byte sex = byte.Parse(parts[4]);

                LogManager.Default.Info($"创建角色: {name}, 职业={prof}, 性别={sex}");

                // 连接到DBServer创建角色
                LogManager.Default.Debug($"开始连接DBServer创建角色: {_dbServerAddress}:{_dbServerPort}");
                using var dbClient = new MirCommon.Database.DBServerClient(_dbServerAddress, _dbServerPort);
                if (!await dbClient.ConnectAsync())
                {
                    LogManager.Default.Error("无法连接到DBServer");
                    SendMessage(0, ProtocolCmd.SM_CREATECHARFAIL, 0, 0, 0, null);
                    return;
                }
                LogManager.Default.Debug($"已连接到DBServer");

                // 从配置读取服务器名称
                string serverName = "淡抹夕阳"; // 默认服务器名称
                LogManager.Default.Debug($"开始创建角色 - 账号: {_account}, 服务器: {serverName}, 角色名: {name}, 职业: {prof}, 发型: {hair}, 性别: {sex}");
                var result = await dbClient.CreateCharacterAsync(_account, serverName, name, prof, hair, sex);
                
                LogManager.Default.Debug($"DBServer返回结果: {result}");
                
                // 当DBServer返回SE_OK时，发送SM_CREATECHAROK
                // 否则发送SM_CREATECHARFAIL（包括SE_SELCHAR_CHAREXIST等错误）
                if (result == SERVER_ERROR.SE_OK)
                {
                    LogManager.Default.Debug($"创建角色成功，发送SM_CREATECHAROK");
                    SendMessage(1, ProtocolCmd.SM_CREATECHAROK, 0, 0, 0, null);
                    // 重新发送角色列表
                    await HandleQueryCharList($"*{_loginId}/{_selectId}");
                }
                else
                {
                    LogManager.Default.Warning($"创建角色失败，错误码: {result}");
                    SendMessage(0, ProtocolCmd.SM_CREATECHARFAIL, 0, 0, 0, null);
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"创建角色失败: {ex.Message}");
                SendMessage(0, ProtocolCmd.SM_CREATECHARFAIL, 0, 0, 0, null);
            }

            await Task.CompletedTask;
        }

        private async Task HandleDeleteChar(string data)
        {
            try
            {
                string charName = data.Trim();
                LogManager.Default.Info($"删除角色: {charName}");
                
                // 连接到DBServer删除角色
                LogManager.Default.Debug($"开始连接DBServer删除角色: {_dbServerAddress}:{_dbServerPort}");
                using var dbClient = new MirCommon.Database.DBServerClient(_dbServerAddress, _dbServerPort);
                if (!await dbClient.ConnectAsync())
                {
                    LogManager.Default.Error("无法连接到DBServer");
                    SendMessage(0, ProtocolCmd.SM_QUERYCHR_FAIL, 0, 0, 0, null);
                    return;
                }
                LogManager.Default.Debug($"已连接到DBServer");

                // 从配置读取服务器名称
                string serverName = "淡抹夕阳"; // 默认服务器名称
                LogManager.Default.Debug($"开始删除角色 - 账号: {_account}, 服务器: {serverName}, 角色名: {charName}");
                bool success = await dbClient.DeleteCharacterAsync(_account, serverName, charName);
                
                LogManager.Default.Debug($"DBServer删除角色结果: {success}");
                
                if (!success)
                {
                    LogManager.Default.Warning($"删除角色失败");
                    SendMessage(0, ProtocolCmd.SM_QUERYCHR_FAIL, 0, 0, 0, null);
                    return;
                }

                LogManager.Default.Debug($"删除角色成功，发送SM_DELCHAROK");
                SendMessage(1, ProtocolCmd.SM_DELCHAROK, 0, 0, 0, null);
                await HandleQueryCharList($"*{_loginId}/{_selectId}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"删除角色失败: {ex.Message}");
                SendMessage(0, ProtocolCmd.SM_QUERYCHR_FAIL, 0, 0, 0, null);
            }

            await Task.CompletedTask;
        }

        private async Task HandleRestoreChar(string data)
        {
            try
            {
                string charName = data.Trim();
                LogManager.Default.Info($"恢复角色: {charName}");
                
                // 连接到DBServer恢复角色
                LogManager.Default.Debug($"开始连接DBServer恢复角色: {_dbServerAddress}:{_dbServerPort}");
                using var dbClient = new MirCommon.Database.DBServerClient(_dbServerAddress, _dbServerPort);
                if (!await dbClient.ConnectAsync())
                {
                    LogManager.Default.Error("无法连接到DBServer");
                    SendMessage(0, ProtocolCmd.SM_QUERYCHR_FAIL, 0, 0, 0, null);
                    return;
                }
                LogManager.Default.Debug($"已连接到DBServer");

                // 从配置读取服务器名称
                string serverName = "淡抹夕阳"; // 默认服务器名称
                LogManager.Default.Debug($"开始恢复角色 - 账号: {_account}, 服务器: {serverName}, 角色名: {charName}");
                bool success = await dbClient.RestoreCharacterAsync(_account, serverName, charName);
                
                LogManager.Default.Debug($"DBServer恢复角色结果: {success}");
                
                if (!success)
                {
                    LogManager.Default.Warning($"恢复角色失败");
                    SendMessage(0, ProtocolCmd.SM_QUERYCHR_FAIL, 0, 0, 0, null);
                    return;
                }

                LogManager.Default.Debug($"恢复角色成功，发送SM_UNDELCHAROK");
                SendMessage(1, ProtocolCmd.SM_UNDELCHAROK, 0, 0, 0, null);
                await HandleQueryCharList($"*{_loginId}/{_selectId}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"恢复角色失败: {ex.Message}");
                SendMessage(0, ProtocolCmd.SM_QUERYCHR_FAIL, 0, 0, 0, null);
            }

            await Task.CompletedTask;
        }

        private async Task HandleQueryDelCharList()
        {
            try
            {
                LogManager.Default.Info("查询已删除角色列表");
                
                // 从数据库服务器获取已删除角色列表
                // 数据格式: "name/class/hair/level/sex/yyyy-mm-dd hh:mm/"
                // 需要连接DBServer查询已删除角色
                string delCharData = "";
                SendMessage(0, ProtocolCmd.SM_DELCHARLIST, 0, 0, 1,
                    System.Text.Encoding.GetEncoding("GBK").GetBytes(delCharData));
            }
            catch
            {
                SendMessage(0, ProtocolCmd.SM_QUERYCHR_FAIL, 0, 0, 0, null);
            }

            await Task.CompletedTask;
        }

        private async Task HandleSelectChar(string data)
        {
            try
            {
                if (_selected) return;

                // "*loginid/charname"
                string[] parts = data.TrimStart('*').Split('/');
                if (parts.Length < 2) return;

                _selectedCharName = parts[1];
                _selected = true;

                // 添加调试日志，显示原始数据和解析后的角色名
                LogManager.Default.Info($"选择角色 - 原始数据: {data}");
                LogManager.Default.Info($"选择角色 - 解析后: {_selectedCharName}");
                
                // 检查角色名是否包含乱码
                byte[] nameBytes = System.Text.Encoding.GetEncoding("GBK").GetBytes(_selectedCharName);
                string utf8Name = System.Text.Encoding.GetEncoding("GBK").GetString(nameBytes);
                LogManager.Default.Info($"选择角色 - UTF8检查: {utf8Name}");
                
                // 尝试使用GBK重新编码和解码
                try
                {
                    byte[] gbkBytes = GameEncoding.GetBytes(_selectedCharName);
                    string gbkName = GameEncoding.GetString(gbkBytes);
                    LogManager.Default.Info($"选择角色 - GBK检查: {gbkName}");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Warning($"GBK编码检查失败: {ex.Message}");
                }

                // 连接到DBServer查询角色位置
                LogManager.Default.Debug($"开始连接DBServer查询角色位置: {_dbServerAddress}:{_dbServerPort}");
                using var dbClient = new MirCommon.Database.DBServerClient(_dbServerAddress, _dbServerPort);
                if (!await dbClient.ConnectAsync())
                {
                    LogManager.Default.Error("无法连接到DBServer");
                    SendMessage(0, ProtocolCmd.SM_QUERYCHR_FAIL, 0, 0, 0, null);
                    return;
                }
                LogManager.Default.Debug($"已连接到DBServer");

                // 从配置读取服务器名称
                string serverName = "淡抹夕阳"; // 默认服务器名称
                
                // 查询角色位置
                LogManager.Default.Debug($"开始查询角色位置 - 账号: {_account}, 服务器: {serverName}, 角色名: {_selectedCharName}");
                var positionResult = await dbClient.QueryMapPositionAsync(_account, serverName, _selectedCharName);
                if (positionResult == null)
                {
                    LogManager.Default.Error($"查询角色位置失败: {_selectedCharName}");
                    SendMessage(0, ProtocolCmd.SM_QUERYCHR_FAIL, 0, 0, 0, null);
                    return;
                }
                LogManager.Default.Debug($"查询角色位置成功 - 地图: {positionResult.MapName}, X: {positionResult.X}, Y: {positionResult.Y}");

                // 通过ServerCenter查询游戏服务器地址
                // 从配置读取ServerCenter地址
                string scSectionName = "服务器中心";
                string serverCenterAddress = "127.0.0.1";
                int serverCenterPort = 6000;
                
                // 尝试从配置读取
                try
                {
                    var iniReader = new IniFileReader("config.ini");
                    if (iniReader.Open())
                    {
                        serverCenterAddress = iniReader.GetString(scSectionName, "addr", "127.0.0.1");
                        serverCenterPort = iniReader.GetInteger(scSectionName, "port", 6000);
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Default.Warning($"读取ServerCenter配置失败: {ex.Message}");
                }
                
                // 连接到ServerCenter查询游戏服务器地址
                ServerAddr? gameServerAddr = null;
                ushort targetServerIndex = 0; // 目标服务器索引
                using var scClient = new MirCommon.Network.ServerCenterClient(serverCenterAddress, serverCenterPort);
                if (await scClient.ConnectAsync())
                {
                    LogManager.Default.Debug($"已连接到ServerCenter: {serverCenterAddress}:{serverCenterPort}");
                    
                    // 首先尝试使用GetGameServerAddrAsync方法，它返回正确的服务器索引
                    LogManager.Default.Debug($"开始查询游戏服务器地址 - 账号: {_account}, 角色: {_selectedCharName}, 地图: {positionResult.MapName}");
                    gameServerAddr = await scClient.GetGameServerAddrAsync(_account, _selectedCharName, positionResult.MapName);
                    
                    if (gameServerAddr != null)
                    {
                        LogManager.Default.Debug($"GetGameServerAddr成功 - 地址: {gameServerAddr.Value.GetAddress()}:{gameServerAddr.Value.nPort}");
                        LogManager.Default.Debug($"开始查找GameServer获取索引 - 服务器类型: GameServer, 服务器名: 淡抹夕阳");
                        var findResult = await scClient.FindServerAsync("GameServer", "淡抹夕阳");
                        if (findResult != null)
                        {
                            targetServerIndex = findResult.Value.Id.bIndex;
                            LogManager.Default.Debug($"FindServer成功 - 目标索引: {targetServerIndex}");
                        }
                        else
                        {
                            LogManager.Default.Warning("FindServer失败，无法获取服务器索引");
                        }
                    }
                    else
                    {
                        LogManager.Default.Warning("通过ServerCenter查询游戏服务器地址失败，尝试使用FindServer");
                        // 如果GetGameServerAddr失败，尝试使用FindServer查找游戏服务器
                        LogManager.Default.Debug($"开始查找GameServer - 服务器类型: GameServer, 服务器名: 淡抹夕阳");
                        var findResult = await scClient.FindServerAsync("GameServer", "淡抹夕阳");
                        if (findResult != null)
                        {
                            gameServerAddr = findResult.Value.addr;
                            targetServerIndex = findResult.Value.Id.bIndex;
                            LogManager.Default.Debug($"FindServer成功 - 地址: {gameServerAddr.Value.GetAddress()}:{gameServerAddr.Value.nPort}, 目标索引: {targetServerIndex}");
                        }
                        else
                        {
                            LogManager.Default.Warning("FindServer也失败了");
                        }
                    }
                    
                    // 通知游戏服务器有玩家即将进入
                    if (gameServerAddr != null)
                    {
                        // 创建EnterGameServer结构体
                        var enterGameServer = new MirCommon.EnterGameServer();
                        enterGameServer.SetAccount(_account);
                        enterGameServer.nLoginId = _loginId;
                        enterGameServer.nSelCharId = _selectId;
                        enterGameServer.nClientId = 0; // 客户端ID，这里用0，GameServer会分配新的
                        enterGameServer.dwEnterTime = (uint)Environment.TickCount;
                        enterGameServer.SetName(_selectedCharName);
                        enterGameServer.dwSelectCharServerId = 0; // SelectCharServer ID，这里用0
                        
                        // 将结构体转换为字节数组
                        byte[] enterGameServerBytes = StructToBytes(enterGameServer);
                        
                        // 记录EnterGameServer结构体的详细信息
                        LogManager.Default.Info($"EnterGameServer结构体信息:");
                        LogManager.Default.Info($"  - 账号: {_account}");
                        LogManager.Default.Info($"  - LoginId: {_loginId}");
                        LogManager.Default.Info($"  - SelCharId: {_selectId}");
                        LogManager.Default.Info($"  - 角色名: {_selectedCharName}");
                        LogManager.Default.Info($"  - 进入时间: {enterGameServer.dwEnterTime}");
                        LogManager.Default.Info($"  - 结构体大小: {enterGameServerBytes.Length}字节");
                        LogManager.Default.Info($"  - 结构体字节: {BitConverter.ToString(enterGameServerBytes)}");
                        
                        // 添加重要提示：客户端需要发送相同的LoginId
                        LogManager.Default.Info($"重要提示: 客户端需要发送相同的LoginId={_loginId}进行验证");
                        
                        // 通过ServerCenter发送跨服务器消息通知GameServer
                        // 使用MAS_ENTERGAMESERVER命令，发送类型为MST_SINGLE
                        // sendType参数包含发送者信息
                        // 高4位是发送者服务器类型，低4位是发送者索引
                        // SelectCharServer的服务器类型是4（ST_SELCHARSERVER）
                        byte senderServerType = 4; // ST_SELCHARSERVER
                        byte senderServerIndex = _server.ServerCenterIndex; // 在ServerCenter中的索引
                        byte sendType = (byte)(((senderServerType & 0x0F) << 4) | (senderServerIndex & 0x0F));
                        
                        // 重要：使用正确的clientId，这是SelectCharServer在ServerCenter中的连接ID
                        // 在ServerCenter中，clientId就是bIndex
                        uint clientId = senderServerIndex;
                        
                        LogManager.Default.Debug($"开始发送跨服务器消息到GameServer:");
                        LogManager.Default.Debug($"  - 命令: MAS_ENTERGAMESERVER (0x{MirCommon.ProtocolCmd.MAS_ENTERGAMESERVER:X4})");
                        LogManager.Default.Debug($"  - 发送类型: {sendType:X2} (发送者类型={senderServerType}, 发送者索引={senderServerIndex})");
                        LogManager.Default.Debug($"  - 目标索引: {targetServerIndex} (从FindServer获取的bIndex)");
                        LogManager.Default.Debug($"  - 客户端ID: {clientId} (使用服务器索引作为clientId)");
                        LogManager.Default.Debug($"  - 数据长度: {enterGameServerBytes.Length}字节");
                        
                        //// 调试：检查FindServer返回的索引
                        //if (findResult != null)
                        //{
                        //    LogManager.Default.Debug($"FindServer返回的Id信息: bType={findResult.Value.Id.bType}, bGroup={findResult.Value.Id.bGroup}, bIndex={findResult.Value.Id.bIndex}, dwId={findResult.Value.Id.dwId}");
                        //}
                        
                        bool notificationSent = await scClient.SendMsgAcrossServerAsync(
                            clientId: clientId, // 使用服务器索引作为clientId
                            cmd: ProtocolCmd.MAS_ENTERGAMESERVER, // 进入游戏服务器命令
                            sendType: sendType, // 发送类型：包含发送者信息
                            targetIndex: targetServerIndex, // 目标服务器索引
                            binaryData: enterGameServerBytes // 直接发送二进制数据
                        );
                        
                        if (notificationSent)
                        {
                            LogManager.Default.Info($"已发送玩家进入通知到GameServer: {_account}/{_selectedCharName}, 目标索引: {targetServerIndex}");
                            LogManager.Default.Debug($"发送成功 - 目标GameServer地址: {gameServerAddr.Value.GetAddress()}:{gameServerAddr.Value.nPort}");
                        }
                        else
                        {
                            LogManager.Default.Warning($"发送玩家进入通知失败");
                            LogManager.Default.Debug($"发送失败 - 请检查ServerCenter和GameServer的连接状态");
                        }
                    }
                    else
                    {
                        LogManager.Default.Warning("无法获取游戏服务器地址，无法发送进入通知");
                    }
                }
                else
                {
                    LogManager.Default.Warning($"无法连接到ServerCenter: {serverCenterAddress}:{serverCenterPort}");
                }
                
                // 如果ServerCenter查询失败，使用默认地址
                if (gameServerAddr == null)
                {
                    var defaultAddr = new ServerAddr();
                    defaultAddr.SetAddress("127.0.0.1");
                    defaultAddr.nPort = 7200;
                    gameServerAddr = defaultAddr;
                    LogManager.Default.Warning($"使用默认游戏服务器地址: {defaultAddr.GetAddress()}:{defaultAddr.nPort}");
                }
                
                // 记录查询到的位置信息和服务器地址
                LogManager.Default.Info($"角色位置 - 地图: {positionResult.MapName}, X: {positionResult.X}, Y: {positionResult.Y}");
                
                // 获取游戏服务器地址字符串
                string gameServerAddrStr = gameServerAddr.Value.GetAddress();
                LogManager.Default.Info($"游戏服务器地址: {gameServerAddrStr}:{gameServerAddr.Value.nPort}");
                
                // 发送选择角色成功消息，包含游戏服务器地址
                // 发送字符串格式的地址，如"127.0.0.1/7200"
                string serverAddrStr = $"{gameServerAddrStr}/{gameServerAddr.Value.nPort}";
                SendMessage(0, ProtocolCmd.SM_SELECTCHAROK, 0, 0, 0, 
                    System.Text.Encoding.GetEncoding("GBK").GetBytes(serverAddrStr));
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"选择角色失败: {ex.Message}");
                SendMessage(0, ProtocolCmd.SM_QUERYCHR_FAIL, 0, 0, 0, null);
            }

            await Task.CompletedTask;
        }

        private void SendMessage(uint dwFlag, ushort wCmd, ushort w1, ushort w2, ushort w3, byte[]? data)
        {
            try
            {
                if (!_client.Connected)
                {
                    LogManager.Default.Warning($"尝试发送消息但客户端已断开连接: Cmd={wCmd:X4}");
                    return;
                }

                // 使用游戏编码发送消息
                byte[] encoded = new byte[8192];
                int encodedSize = GameCodec.EncodeMsg(encoded, dwFlag, wCmd, w1, w2, w3, data, data?.Length ?? 0);
                
                if (encodedSize <= 0)
                {
                    LogManager.Default.Error($"编码消息失败: Cmd={wCmd:X4}, 编码大小={encodedSize}");
                    return;
                }
                
                _stream.Write(encoded, 0, encodedSize);
                _stream.Flush();
                
                // 记录发送的重要消息
                if (wCmd == ProtocolCmd.SM_CHARLIST || wCmd == ProtocolCmd.SM_SELECTCHAROK || 
                    wCmd == ProtocolCmd.SM_QUERYCHR_FAIL || wCmd == ProtocolCmd.SM_CREATECHAROK)
                {
                    string dataStr = data != null ? System.Text.Encoding.GetEncoding("GBK").GetString(data).TrimEnd('\0') : "";
                    LogManager.Default.Info($"已发送消息: Cmd={wCmd:X4}, Flag={dwFlag}, Data={dataStr}");
                }
                else
                {
                    LogManager.Default.Debug($"已发送消息: Cmd={wCmd:X4}, Flag={dwFlag}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送消息失败: Cmd={wCmd:X4}, 错误: {ex.Message}");
                
                // 如果发送失败，断开连接
                try
                {
                    Disconnect();
                }
                catch { }
            }
        }

        /// <summary>
        /// 检测并解码字符串，根据字节内容自动选择GBK或UTF-8编码
        /// </summary>
        private string DetectAndDecodeString(byte[] payload, ushort wCmd)
        {
            if (payload == null || payload.Length == 0)
                return string.Empty;

            // 对于选择角色消息（0067），根据日志显示客户端发送的是UTF-8编码
            if (wCmd == ProtocolCmd.CM_QUERYSELECTCHAR)
            {
                // 尝试UTF-8解码
                try
                {
                    string utf8Str = System.Text.Encoding.UTF8.GetString(payload).TrimEnd('\0');
                    // 检查解码后的字符串是否包含乱码字符
                    if (!ContainsGarbledCharacters(utf8Str))
                    {
                        return utf8Str;
                    }
                }
                catch
                {
                    // UTF-8解码失败，回退到GBK
                }
            }

            // 默认使用GBK解码
            return GetString(payload);
        }

        /// <summary>
        /// 检查字符串是否包含乱码字符（GBK解码UTF-8字节时产生的乱码）
        /// </summary>
        private bool ContainsGarbledCharacters(string str)
        {
            if (string.IsNullOrEmpty(str))
                return false;

            // 检查常见的乱码字符模式
            // 当UTF-8字节被当作GBK解码时，会产生这些字符
            foreach (char c in str)
            {
                // 检查是否在常见的中文字符范围之外，但又不是ASCII字符
                if (c > 127 && (c < 0x4E00 || c > 0x9FFF)) // 基本汉字范围：0x4E00-0x9FFF
                {
                    // 但排除一些常见的标点符号
                    if (c != '·' && c != '—' && c != '～' && c != '€')
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public void Disconnect()
        {
            try
            {
                _stream?.Close();
                _client?.Close();
            }
            catch { }
        }

        /// <summary>
        /// 将结构体转换为字节数组
        /// </summary>
        private byte[] StructToBytes<T>(T structure) where T : struct
        {
            int size = System.Runtime.InteropServices.Marshal.SizeOf(structure);
            byte[] bytes = new byte[size];
            IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
            try
            {
                System.Runtime.InteropServices.Marshal.StructureToPtr(structure, ptr, false);
                System.Runtime.InteropServices.Marshal.Copy(ptr, bytes, 0, size);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
            }
            return bytes;
        }
        
        /// <summary>
        /// 将字节数组转换为结构体
        /// </summary>
        private T BytesToStruct<T>(byte[] bytes) where T : struct
        {
            int size = System.Runtime.InteropServices.Marshal.SizeOf<T>();
            if (bytes.Length < size)
                throw new ArgumentException($"字节数组长度不足: {bytes.Length} < {size}");
                
            IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
            try
            {
                System.Runtime.InteropServices.Marshal.Copy(bytes, 0, ptr, size);
                return System.Runtime.InteropServices.Marshal.PtrToStructure<T>(ptr);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
            }
        }
        
        /// <summary>
        /// 处理ServerCenter消息
        /// </summary>
        public async Task OnMASMsg(ushort wCmd, byte wType, ushort wIndex, string data)
        {
            try
            {
                LogManager.Default.Debug($"客户端收到ServerCenter消息: Cmd={wCmd:X4}, Type={wType}, Index={wIndex}");
                
                switch (wCmd)
                {
                    case ProtocolCmd.MAS_ENTERGAMESERVER:
                        await HandleEnterGameServerMessage(data);
                        break;
                        
                    default:
                        LogManager.Default.Warning($"客户端未知的ServerCenter消息: {wCmd:X4}");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"客户端处理ServerCenter消息失败: {ex.Message}");
            }
            
            await Task.CompletedTask;
        }
        
        /// <summary>
        /// 处理进入游戏服务器消息
        /// </summary>
        private async Task HandleEnterGameServerMessage(string data)
        {
            try
            {
                // 消息格式: ENTERGAMESERVER结构体
                byte[] dataBytes = System.Text.Encoding.GetEncoding("GBK").GetBytes(data);
                if (dataBytes.Length >= 64) // ENTERGAMESERVER结构体大小为64字节
                {
                    // 将字节数组转换为结构体
                    var enterInfo = BytesToStruct<MirCommon.EnterGameServer>(dataBytes);
                    
                    // 检查是否匹配当前客户端
                    if (enterInfo.nSelCharId == _selectId && enterInfo.nLoginId == _loginId)
                    {
                        if (_verified && (SERVER_ERROR)enterInfo.result == SERVER_ERROR.SE_OK)
                        {
                            LogManager.Default.Info($"进入游戏服务器成功: {enterInfo.GetAccount()}/{enterInfo.GetName()}");
                            
                            // 从ServerCenter获取游戏服务器地址
                            string serverAddrStr = await GetGameServerAddressFromServerCenter();
                            
                            // 发送选择角色成功消息，包含游戏服务器地址
                            SendMessage(0, ProtocolCmd.SM_SELECTCHAROK, 0, 0, 0, 
                                System.Text.Encoding.GetEncoding("GBK").GetBytes(serverAddrStr));
                        }
                        else
                        {
                            SendMessage(0, ProtocolCmd.SM_QUERYCHR_FAIL, 0, 0, 0, null);
                        }
                    }
                }
                else
                {
                    LogManager.Default.Error($"进入游戏服务器消息数据长度不足: {dataBytes.Length}字节");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理进入游戏服务器消息失败: {ex.Message}");
            }
            
            await Task.CompletedTask;
        }
        
        /// <summary>
        /// 从ServerCenter获取游戏服务器地址
        /// </summary>
        private async Task<string> GetGameServerAddressFromServerCenter()
        {
            try
            {
                // 从配置读取ServerCenter地址
                string scSectionName = "服务器中心";
                string serverCenterAddress = "127.0.0.1";
                int serverCenterPort = 6000;
                
                // 尝试从配置读取
                try
                {
                    var iniReader = new IniFileReader("config.ini");
                    if (iniReader.Open())
                    {
                        serverCenterAddress = iniReader.GetString(scSectionName, "addr", "127.0.0.1");
                        serverCenterPort = iniReader.GetInteger(scSectionName, "port", 6000);
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Default.Warning($"读取ServerCenter配置失败: {ex.Message}");
                }
                
                // 连接到ServerCenter查询游戏服务器地址
                using var scClient = new MirCommon.Network.ServerCenterClient(serverCenterAddress, serverCenterPort);
                if (await scClient.ConnectAsync())
                {
                    LogManager.Default.Debug($"已连接到ServerCenter: {serverCenterAddress}:{serverCenterPort}");
                    
                    // 使用FindServer查找游戏服务器
                    LogManager.Default.Debug($"开始查找GameServer - 服务器类型: GameServer, 服务器名: 淡抹夕阳");
                    var findResult = await scClient.FindServerAsync("GameServer", "淡抹夕阳");
                    if (findResult != null)
                    {
                        string gameServerAddrStr = $"{findResult.Value.addr.GetAddress()}/{findResult.Value.addr.nPort}";
                        LogManager.Default.Debug($"FindServer成功 - 地址: {gameServerAddrStr}");
                        return gameServerAddrStr;
                    }
                    else
                    {
                        LogManager.Default.Warning("FindServer失败，使用默认地址");
                    }
                }
                else
                {
                    LogManager.Default.Warning($"无法连接到ServerCenter: {serverCenterAddress}:{serverCenterPort}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"从ServerCenter获取游戏服务器地址失败: {ex.Message}");
            }
            
            // 如果ServerCenter查询失败，使用默认地址
            return "127.0.0.1/7200";
        }
        
        /// <summary>
        /// 处理ServerCenter消息
        /// </summary>
        public async Task OnSCMsg(ushort wCmd, ushort w1, ushort w2, ushort w3, string data)
        {
            try
            {
                LogManager.Default.Debug($"客户端收到ServerCenter消息: Cmd={wCmd:X4}, w1={w1}, w2={w2}, w3={w3}");
                
                switch (wCmd)
                {
                    case ProtocolCmd.SCM_GETGAMESERVERADDR:
                        await HandleGetGameServerAddrMessage(w1, data);
                        break;
                        
                    default:
                        LogManager.Default.Warning($"客户端未知的ServerCenter消息: {wCmd:X4}");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"客户端处理ServerCenter消息失败: {ex.Message}");
            }
            
            await Task.CompletedTask;
        }
        
        /// <summary>
        /// 处理获取游戏服务器地址消息
        /// </summary>
        private async Task HandleGetGameServerAddrMessage(ushort result, string data)
        {
            try
            {
                if ((SERVER_ERROR)result == SERVER_ERROR.SE_OK && _verified)
                {
                    // 解析游戏服务器地址
                    byte[] dataBytes = System.Text.Encoding.GetEncoding("GBK").GetBytes(data);
                    if (dataBytes.Length >= 20) // FindServerResult结构体大小为20字节
                    {
                        var findResult = BytesToStruct<MirCommon.FindServerResult>(dataBytes);
                        
                        // 存储游戏服务器地址
                        // 注意：这里需要将游戏服务器地址发送给客户端
                        // 这里应该发送SM_SELECTCHAROK消息
                        string serverAddrStr = $"{findResult.addr.GetAddress()}/{findResult.addr.nPort}";
                        SendMessage(0, ProtocolCmd.SM_SELECTCHAROK, 0, 0, 0, 
                            System.Text.Encoding.GetEncoding("GBK").GetBytes(serverAddrStr));
                        
                        LogManager.Default.Info($"获取游戏服务器地址成功: {serverAddrStr}");
                    }
                }
                else
                {
                    SendMessage(0, ProtocolCmd.SM_QUERYCHR_FAIL, 0, 0, 0, null);
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理获取游戏服务器地址消息失败: {ex.Message}");
            }
            
            await Task.CompletedTask;
        }
    }
}
