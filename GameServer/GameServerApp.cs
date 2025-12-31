using MirCommon;
using MirCommon.Network;
using MirCommon.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
// 类型别名
using Player = GameServer.HumanPlayer;

namespace GameServer
{
    // GameWorld类现在在GameWorld.cs中定义
    // 这里不再重复定义

    /// <summary>
    /// 临时的GameMap类
    /// </summary>
    public class GameMap
    {
        public int MapId { get; set; }
        public string Name { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }

        public void AddPlayer(Player player) { }
        public void RemovePlayer(uint playerId) { }
        public int GetPlayerCount() => 0;
        public Player[] GetPlayersInRange(int x, int y, int range) => Array.Empty<Player>();
    }

    /// <summary>
    /// 游戏服务器应用（处理内部服务器之间通讯）
    /// </summary>
    public class GameServerApp
    {
        private readonly IniFileReader _config;
        //private readonly ConfigManager _config;
        private TcpListener? _listener;
        private readonly List<GameClient> _clients = new();
        private readonly object _clientLock = new();
        private readonly GameWorld _world = GameWorld.Instance;
        private bool _isRunning = false;
        //private int _maxPlayers = 1000;
        private static uint _nextPlayerId = 1;
        private MirCommon.Database.DatabaseManager? _databaseManager;

        private string _addr = "127.0.0.1";
        private int _port = 7200;
        private string _name = "淡抹夕阳";
        private int _maxconnection = 4000;
        private string _baniplist = "banip.txt";
        private string _trustiplist = "trustip.txt";
        private string _dbServerAddress = "127.0.0.1";
        private int _dbServerPort = 8000;
        private string _serverCenterAddress = "127.0.0.1";
        private int _serverCenterPort = 6000;

        // ServerCenter连接
        private MirCommon.Network.ServerCenterClient? _serverCenterClient;
        private Task? _serverCenterTask;

        // DBServer连接
        private MirCommon.Database.DBServerClient? _dbServerClient;
        //private Task? _dbServerTask;

        // 保存从ServerCenter收到的进入信息，键为loginId
        private readonly Dictionary<uint, MirCommon.EnterGameServer> _enterInfoDict = new();
        private readonly object _enterInfoLock = new();

        // 保存GameClient引用，用于分发DBServer消息
        private readonly ConcurrentDictionary<uint, GameClient> _gameClients = new();
        private readonly object _gameClientsLock = new();

        public GameServerApp(IniFileReader config)
        {
            _config = config;
        }

        public async Task<bool> Initialize()
        {
            try
            {
                // 从INI文件的[游戏世界服务器]节读取配置
                string sectionName = "游戏世界服务器";
                _addr = _config.GetString(sectionName, "addr", "127.0.0.1");
                _port = _config.GetInteger(sectionName, "port", 7200);
                _name = _config.GetString(sectionName, "name", " 淡抹夕阳");
                _maxconnection = _config.GetInteger(sectionName, "maxconnection", 4000);
                _baniplist = _config.GetString(sectionName, "baniplist", "banip.txt");
                _trustiplist = _config.GetString(sectionName, "trustiplist", "trustip.txt");

                // 从INI文件的[数据库服务器]节读取DBServer配置
                string dbSectionName = "数据库服务器";
                _dbServerAddress = _config.GetString(dbSectionName, "addr", "127.0.0.1");
                _dbServerPort = _config.GetInteger(dbSectionName, "port", 8000);

                // 从INI文件的[服务器中心]节读取ServerCenter配置
                string scSectionName = "服务器中心";
                _serverCenterAddress = _config.GetString(scSectionName, "addr", "127.0.0.1");
                _serverCenterPort = _config.GetInteger(scSectionName, "port", 6000);

                // 加载所有本地配置文件（使用异步版本）
                LogManager.Default.Info("正在加载游戏配置文件...");
                if (!await ConfigLoader.Instance.LoadAllConfigsAsync())
                {
                    LogManager.Default.Error("配置文件加载失败");
                    return false;
                }

                #region DBServer长连接

                // 初始化DBServer连接
                LogManager.Default.Info("正在初始化DBServer连接...");
                LogManager.Default.Info($"DBServer地址: {_dbServerAddress}:{_dbServerPort}");
                
                _dbServerClient = new MirCommon.Database.DBServerClient(_dbServerAddress, _dbServerPort);
                if (!await _dbServerClient.ConnectAsync())
                {
                    LogManager.Default.Error("DBServer连接失败");
                    return false;
                }

                // 设置DBServer消息处理事件
                _dbServerClient.OnDbMessageReceived += HandleDbServerMessage;
                _dbServerClient.OnLogMessage += (msg) => LogManager.Default.Info(msg);

                LogManager.Default.Info("DBServer连接成功");

                // 启动DBServer监听
                LogManager.Default.Info("正在启动DBServer消息监听...");
                _dbServerClient.StartListening();
                //_dbServerTask = Task.Run(async () => await ProcessDBServerMessagesAsync());
                LogManager.Default.Info("DBServer消息监听已启动");

                #endregion

                #region ServerCenter长连接

                //// 向ServerCenter注册并保持连接
                //LogManager.Default.Info("正在向ServerCenter注册...");
                //_serverCenterClient = new MirCommon.Network.ServerCenterClient(_serverCenterAddress, _serverCenterPort);
                //if (await _serverCenterClient.ConnectAsync())
                //{
                //    bool registered = await _serverCenterClient.RegisterServerAsync("GameServer", _name, _addr, _port, _maxconnection);
                //    if (registered)
                //    {
                //        LogManager.Default.Info("ServerCenter注册成功");
                //        // 启动ServerCenter消息处理任务
                //        _serverCenterTask = Task.Run(async () => await ProcessServerCenterMessagesAsync());
                //        // 启动心跳任务
                //        _ = Task.Run(async () => await SendHeartbeatAsync());
                //    }
                //    else
                //    {
                //        LogManager.Default.Warning("ServerCenter注册失败");
                //    }
                //}
                //else
                //{
                //    LogManager.Default.Warning("无法连接到ServerCenter");
                //}

                #endregion

                if (!_world.Initialize())
                {
                    LogManager.Default.Error("游戏世界初始化失败");
                    return false;
                }

                // 初始化NPC系统
                NPCSystemInitializer.Initialize();

                LogManager.Default.Info($"监听端口: {_port}");
                LogManager.Default.Info($"最大玩家: {_maxconnection}");

                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error("初始化失败", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 与ServerCenter通讯子线程
        /// </summary>
        /// <returns></returns>
        public async Task StartServerCenterClient()
        {
            // 向ServerCenter注册并保持连接
            LogManager.Default.Info("正在向ServerCenter注册...");
            _serverCenterClient = new MirCommon.Network.ServerCenterClient(_serverCenterAddress, _serverCenterPort);
            if (await _serverCenterClient.ConnectAsync())
            {
                bool registered = await _serverCenterClient.RegisterServerAsync("GameServer", _name, _addr, _port, _maxconnection);
                if (registered)
                {
                    LogManager.Default.Info("ServerCenter注册成功");
                    // 启动ServerCenter消息处理任务线程
                    _serverCenterTask = Task.Run(async () => await ProcessServerCenterMessagesAsync());
                    // 启动心跳任务线程
                    _ = Task.Run(async () => await SendHeartbeatAsync());
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
        }

        /// <summary>
        /// 与传世客户端通讯
        /// </summary>
        /// <returns></returns>
        public async Task Start()
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            _isRunning = true;

            LogManager.Default.Info("游戏服务器已启动");

            _ = Task.Run(async () =>
            {
                while (_isRunning)  //接受新的客户端连接
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
            lock (_clientLock)
            {
                _clients.ForEach(c => c.Disconnect());
                _clients.Clear();
            }

            // 停止DBServer监听
            try
            {
                if (_dbServerClient != null)
                {
                    _dbServerClient.StopListening();
                    _dbServerClient.Disconnect();
                    _dbServerClient = null;
                }
            }
            catch { }

            // 从ServerCenter注销
            try
            {
                if (_serverCenterClient != null)
                {
                    _serverCenterClient.UnregisterServerAsync("GameServer", _name).GetAwaiter().GetResult();
                    _serverCenterClient.Disconnect();
                    _serverCenterClient = null;
                }
            }
            catch { }
        }

        public void Update()
        {
            _world.Update();
        }

        /// <summary>
        /// 处理一个新的客户端连接
        /// </summary>
        /// <param name="tcpClient"></param>
        /// <returns></returns>
        private async Task HandleClient(TcpClient tcpClient)
        {
            var client = new GameClient(tcpClient, this, _world, _dbServerAddress, _dbServerPort);

            lock (_clientLock)
            {
                if (_clients.Count >= _maxconnection)
                {
                    LogManager.Default.Warning("服务器已满");
                    tcpClient.Close();
                    return;
                }
                _clients.Add(client);
            }

            // 注册GameClient到分发字典
            RegisterGameClient(client);

            LogManager.Default.Info($"新玩家连接: {tcpClient.Client.RemoteEndPoint}");

            try
            {
                //单客户端消息处理循环
                await client.ProcessAsync();
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理玩家错误: {ex.Message}");
            }
            finally
            {
                // 从分发字典中移除
                UnregisterGameClient(client);
                
                lock (_clientLock)
                {
                    _clients.Remove(client);
                }
                LogManager.Default.Info($"玩家断开");
            }
        }

        public uint GeneratePlayerId() => Interlocked.Increment(ref _nextPlayerId);

        public void ListPlayers()
        {
            var players = _world.GetAllPlayers();
            Console.WriteLine($"\n========== 在线玩家 ({players.Length}) ==========");
            foreach (var player in players.Take(20))
            {
                Console.WriteLine($"  [{player.ObjectId}] {player.Name} - 地图:{player.MapId} 位置:({player.X},{player.Y}) 等级:{player.Level}");
            }
            if (players.Length > 20)
                Console.WriteLine($"  ... 还有 {players.Length - 20} 个玩家");
            Console.WriteLine("================================\n");
        }

        public void ListMaps()
        {
            var maps = _world.GetAllMaps();
            Console.WriteLine($"\n========== 地图列表 ({maps.Length}) ==========");
            foreach (var map in maps)
            {
                Console.WriteLine($"  [{map.MapId}] {map.Name} - 大小:{map.Width}x{map.Height} 玩家:{map.GetPlayerCount()}");
            }
            Console.WriteLine("================================\n");
        }

        public void ListMonsters()
        {
            var monsters = MonsterManagerEx.Instance.GetAllMonsters();
            Console.WriteLine($"\n========== 怪物列表 ({monsters.Count}) ==========");

            // 按地图分组显示
            var monstersByMap = monsters.GroupBy(m => m.MapId);

            foreach (var mapGroup in monstersByMap)
            {
                var map = _world.GetMap(mapGroup.Key);
                string mapName = map != null ? map.Name : $"地图{mapGroup.Key}";
                Console.WriteLine($"\n  [{mapGroup.Key}] {mapName} - 怪物数量: {mapGroup.Count()}");

                // 按怪物类型分组
                var monstersByType = mapGroup.GroupBy(m => m.GetDesc()?.Base.MonsterId ?? 0);
                foreach (var typeGroup in monstersByType.Take(10)) // 每种类型最多显示10个
                {
                    var monsterClass = MonsterManagerEx.Instance.GetMonsterClass(typeGroup.Key);
                    string monsterName = monsterClass != null ? monsterClass.Base.ClassName : $"怪物{typeGroup.Key}";
                    Console.WriteLine($"    {monsterName} (ID:{typeGroup.Key}) x{typeGroup.Count()}");
                }

                if (monstersByType.Count() > 10)
                {
                    Console.WriteLine($"    ... 还有 {monstersByType.Count() - 10} 种怪物类型");
                }
            }

            if (monstersByMap.Count() == 0)
            {
                Console.WriteLine("  当前没有怪物");
            }

            Console.WriteLine("================================\n");
        }

        public void SpawnMonster(int mapId, int monsterId)
        {
            try
            {
                var map = _world.GetMap(mapId);
                if (map == null)
                {
                    Console.WriteLine($"错误: 地图 {mapId} 不存在\n");
                    return;
                }

                var monsterClass = MonsterManagerEx.Instance.GetMonsterClass(monsterId);
                if (monsterClass == null)
                {
                    Console.WriteLine($"错误: 怪物ID {monsterId} 不存在\n");
                    return;
                }

                // 在地图中心附近生成怪物
                int centerX = map.Width / 2;
                int centerY = map.Height / 2;

                // 在中心点周围随机位置生成
                Random rand = new Random();
                int x = Math.Clamp(centerX + rand.Next(-10, 11), 0, map.Width - 1);
                int y = Math.Clamp(centerY + rand.Next(-10, 11), 0, map.Height - 1);

                // 创建怪物
                var monster = new MonsterEx();
                if (monster.Init(monsterClass, mapId, x, y))
                {
                    // 添加到地图
                    // map.AddObject(monster, (ushort)x, (ushort)y);

                    // 添加到怪物管理器
                    // MonsterManagerEx.Instance.AddMonster(monster);

                    Console.WriteLine($"成功生成怪物: {monsterClass.Base.ClassName} (ID:{monsterId}) 在地图 {mapId} 位置 ({x},{y})\n");
                }
                else
                {
                    Console.WriteLine($"错误: 怪物初始化失败\n");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"刷怪错误: {ex.Message}\n");
            }
        }

        public void ShowStatus()
        {
            Console.WriteLine($"运行时间: {_world.GetUptime()}");
            Console.WriteLine($"在线玩家: {_world.GetPlayerCount()}");
            Console.WriteLine($"地图数量: {_world.GetMapCount()}");
            Console.WriteLine($"更新次数: {_world.GetUpdateCount()}");
        }

        /// <summary>
        /// 处理ServerCenter消息
        /// </summary>
        private async Task ProcessServerCenterMessagesAsync()
        {
            if (_serverCenterClient == null)
            {
                LogManager.Default.Error("ServerCenterClient为null，无法启动消息处理任务");
                return;
            }

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

                LogManager.Default.Info($"ServerCenter连接状态: Connected={tcpClient.Connected}, Available={tcpClient.Available}");

                byte[] buffer = new byte[8192];
                int reconnectAttempts = 0;
                const int maxReconnectAttempts = 3;

                while (_isRunning)
                {
                    try
                    {
                        if (!_serverCenterClient._connected)
                        {
                            LogManager.Default.Warning("ServerCenter连接已断开，尝试重新连接...");
                            reconnectAttempts++;
                            if (reconnectAttempts > maxReconnectAttempts)
                            {
                                LogManager.Default.Error($"达到最大重连次数({maxReconnectAttempts})，停止ServerCenter消息处理");
                                break;
                            }

                            // 尝试重新连接
                            try
                            {
                                if (await _serverCenterClient.ConnectAsync())
                                {
                                    bool registered = await _serverCenterClient.RegisterServerAsync("GameServer", _name, _addr, _port, _maxconnection);
                                    if (registered)
                                    {
                                        LogManager.Default.Info("ServerCenter重新注册成功");
                                        reconnectAttempts = 0;

                                        // 重新获取TcpClient和NetworkStream
                                        tcpClient = clientField.GetValue(_serverCenterClient) as TcpClient;
                                        networkStream = streamField.GetValue(_serverCenterClient) as NetworkStream;
                                        if (tcpClient == null || networkStream == null)
                                        {
                                            LogManager.Default.Error("重新连接后无法获取TcpClient或NetworkStream");
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        LogManager.Default.Warning("ServerCenter重新注册失败");
                                        await Task.Delay(5000); // 等待5秒后重试
                                        continue;
                                    }
                                }
                                else
                                {
                                    LogManager.Default.Warning("无法重新连接到ServerCenter");
                                    await Task.Delay(5000); // 等待5秒后重试
                                    continue;
                                }
                            }
                            catch (Exception ex)
                            {
                                LogManager.Default.Error($"重新连接ServerCenter失败: {ex.Message}");
                                await Task.Delay(5000); // 等待5秒后重试
                                continue;
                            }
                        }

                        LogManager.Default.Debug("等待ServerCenter消息...");

                        //// 使用ReadAsync直接读取，它会阻塞直到有数据或连接关闭
                        //// 设置一个CancellationToken以便在服务器停止时取消
                        //var cts = new CancellationTokenSource();
                        //var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                        //// 设置读取超时
                        //var timeoutTask = Task.Delay(1000, linkedCts.Token);
                        //var readTask = networkStream.ReadAsync(buffer, 0, buffer.Length, linkedCts.Token);

                        //这里是子线程，阻塞没关系
                        var readTask = networkStream.ReadAsync(buffer, 0, buffer.Length);

                        //var completedTask = await Task.WhenAny(readTask, timeoutTask);                        
                        //if (completedTask == readTask && !readTask.IsCanceled)
                        //{
                        int bytesRead = await readTask;
                        LogManager.Default.Debug($"收到ServerCenter消息: {bytesRead}字节");
                        if (bytesRead > 0)
                        {
                            await ProcessServerCenterMessage(buffer, bytesRead);
                        }
                        else
                        {
                            // 连接关闭
                            LogManager.Default.Info("ServerCenter连接已关闭");
                            // 不立即退出，让重连逻辑处理
                            await Task.Delay(1000);
                        }
                        //}
                        //else if (completedTask == timeoutTask)
                        //{
                        //    // 超时，检查服务器是否还在运行
                        //    if (!_isRunning)
                        //    {
                        //        break;
                        //    }
                        //    // 继续循环
                        //    LogManager.Default.Debug("等待ServerCenter消息超时，继续等待...");
                        //}
                        //cts.Cancel();
                    }
                    catch (OperationCanceledException)
                    {
                        // 任务被取消，正常退出
                        LogManager.Default.Info("ServerCenter消息读取任务被取消");
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (_isRunning)
                        {
                            LogManager.Default.Error($"读取ServerCenter消息失败: {ex.Message}");
                            LogManager.Default.Error($"堆栈跟踪: {ex.StackTrace}");
                            // 不立即退出，让重连逻辑处理
                            await Task.Delay(1000);
                        }
                    }
                }

                LogManager.Default.Info("ServerCenter消息处理任务已停止");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理ServerCenter消息失败: {ex.Message}");
                LogManager.Default.Error($"堆栈跟踪: {ex.StackTrace}");
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

                // 打印payload的十六进制内容以便调试
                if (payload.Length > 0)
                {
                    string hexPayload = BitConverter.ToString(payload).Replace("-", " ");
                    LogManager.Default.Debug($"Payload十六进制: {hexPayload.Substring(0, Math.Min(100, hexPayload.Length))}...");
                }

                // 处理跨服务器消息：当wCmd是SCM_MSGACROSSSERVER时，w1是真正的命令
                if (wCmd == ProtocolCmd.SCM_MSGACROSSSERVER)
                {
                    // w1是真正的命令，w2是发送类型，w3是目标索引
                    LogManager.Default.Info($"处理跨服务器消息: 真实命令={w1:X4}, 发送类型={w2}, 目标索引={w3}");
                    await OnMASMsg((ushort)w1, (byte)w2, w3, payload);
                }
                else if (wCmd == ProtocolCmd.SCM_GETGAMESERVERADDR)
                {
                    // 处理获取游戏服务器地址消息
                    LogManager.Default.Info($"处理获取游戏服务器地址消息: w1={w1}, w2={w2}, w3={w3}, 数据长度={payload.Length}");
                    // 这里可以解析SCM_GETGAMESERVERADDR消息，但GameServer不需要处理这个
                    // 这个消息是ServerCenter发送给SelectCharServer的
                }
                else
                {
                    // 对于其他消息，直接传递wCmd
                    LogManager.Default.Info($"处理其他ServerCenter消息: Cmd={wCmd:X4}, w1={w1}, w2={w2}, w3={w3}");
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

                // 验证消息是否是发给本服务器的
                // 注意：wIndex是ServerCenter转发消息时设置的目标索引，应该是本服务器的索引
                // 但本服务器不知道自己的索引，所以这里不进行验证
                // 如果未来需要验证，可以添加GetServerIndex()方法获取本服务器的索引

                switch (wCmd)
                {
                    case ProtocolCmd.MAS_ENTERGAMESERVER:
                        LogManager.Default.Info($"处理MAS_ENTERGAMESERVER消息，数据长度={data.Length}字节");
                        await HandleEnterGameServerMessage(data, wIndex, senderServerType, senderServerIndex);
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
        /// 处理进入游戏服务器消息
        /// </summary>
        private async Task HandleEnterGameServerMessage(byte[] data, ushort wIndex, byte senderServerType, byte senderServerIndex)
        {
            try
            {
                LogManager.Default.Info($"开始处理进入游戏服务器消息: 数据长度={data.Length}字节, wIndex={wIndex}, 发送者类型={senderServerType}, 发送者索引={senderServerIndex}");

                // 消息格式: ENTERGAMESERVER结构体
                if (data.Length >= 64) // ENTERGAMESERVER结构体大小为64字节
                {
                    // 将字节数组转换为结构体
                    var enterInfo = BytesToStruct<MirCommon.EnterGameServer>(data);

                    // 检查账号字段是否为空
                    string account = enterInfo.GetAccount();
                    string name = enterInfo.GetName();
                    uint loginId = enterInfo.nLoginId;

                    LogManager.Default.Info($"收到ServerCenter发来的玩家进入游戏服务器通知: 账号='{account}', 角色名='{name}', 登录ID={loginId}");
                    LogManager.Default.Info($"详细结构体信息:");
                    LogManager.Default.Info($"  - 账号字节数组: {BitConverter.ToString(enterInfo.szAccount).Replace("-", " ")}");
                    LogManager.Default.Info($"  - 账号字符串: '{account}' (长度: {account?.Length ?? 0})");
                    LogManager.Default.Info($"  - 角色名字节数组: {BitConverter.ToString(enterInfo.szName).Replace("-", " ")}");
                    LogManager.Default.Info($"  - 角色名字符串: '{name}' (长度: {name?.Length ?? 0})");
                    LogManager.Default.Info($"  - 登录ID: {enterInfo.nLoginId}");
                    LogManager.Default.Info($"  - 选择角色ID: {enterInfo.nSelCharId}");
                    LogManager.Default.Info($"  - 客户端ID: {enterInfo.nClientId}");
                    LogManager.Default.Info($"  - 进入时间: {enterInfo.dwEnterTime}");
                    LogManager.Default.Info($"  - 选择角色服务器ID: {enterInfo.dwSelectCharServerId}");

                    // 保存进入信息，供客户端连接时使用
                    lock (_enterInfoLock)
                    {
                        _enterInfoDict[loginId] = enterInfo;
                        LogManager.Default.Info($"已保存进入信息到字典，登录ID={loginId}，当前字典大小={_enterInfoDict.Count}");

                        // 打印字典中的所有键，用于调试
                        LogManager.Default.Info($"当前字典中的登录ID: {string.Join(", ", _enterInfoDict.Keys)}");
                    }

                    // 发送确认响应给ServerCenter
                    await SendEnterGameServerAck();
                }
                else
                {
                    LogManager.Default.Error($"进入游戏服务器消息数据长度不足: {data.Length}字节");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理进入游戏服务器消息失败: {ex.Message}");
                LogManager.Default.Error($"堆栈跟踪: {ex.StackTrace}");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 发送进入游戏服务器确认响应
        /// </summary>
        private async Task SendEnterGameServerAck()
        {
            try
            {
                if (_serverCenterClient == null)
                    return;

                // 发送确认消息给ServerCenter
                // 使用MAS_ENTERGAMESERVER命令，设置result为SE_OK
                var ackData = new byte[64]; // ENTERGAMESERVER结构体大小
                // 设置result字段为SE_OK
                BitConverter.GetBytes((uint)MirCommon.SERVER_ERROR.SE_OK).CopyTo(ackData, 0);

                // 获取服务器索引（从ServerCenterClient中获取）
                byte serverIndex = _serverCenterClient.GetServerIndex();
                byte serverType = 6; // ST_GAMESERVER = 6

                // 构建sendType参数：高4位是服务器类型，低4位是服务器索引
                byte sendType = (byte)((serverType << 4) | (serverIndex & 0x0F));

                // 发送确认消息
                bool sent = await _serverCenterClient.SendMsgAcrossServerAsync(
                    clientId: 0,
                    cmd: ProtocolCmd.MAS_ENTERGAMESERVER,
                    sendType: sendType,
                    targetIndex: 0, // 发送给ServerCenter自己
                    binaryData: ackData
                );

                if (sent)
                {
                    LogManager.Default.Info($"已发送进入游戏服务器确认响应 (服务器类型={serverType}, 索引={serverIndex}, sendType=0x{sendType:X2})");
                }
                else
                {
                    LogManager.Default.Warning("发送进入游戏服务器确认响应失败");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送进入游戏服务器确认响应失败: {ex.Message}");
            }
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

        ///// <summary>
        ///// 将字节数组转换为结构体（处理大端序）
        ///// </summary>
        //private T BytesToStruct<T>(byte[] bytes) where T : struct
        //{
        //    int size = System.Runtime.InteropServices.Marshal.SizeOf<T>();
        //    if (bytes.Length < size)
        //        throw new ArgumentException($"字节数组长度不足: {bytes.Length} < {size}");

        //    // 注意：网络传输通常使用大端序，而Windows系统使用小端序
        //    // 这里需要根据实际情况处理字节序转换
        //    // 对于EnterGameServer和CHARDBINFO结构体，字段可能是大端序

        //    // 创建一个新的字节数组，用于存储转换后的数据
        //    byte[] convertedBytes = new byte[size];
        //    Array.Copy(bytes, 0, convertedBytes, 0, size);

        //    if (typeof(T) == typeof(MirCommon.EnterGameServer))
        //    {
        //        // EnterGameServer结构体字段布局：
        //        // byte szAccount[12] (偏移量0)
        //        // uint nLoginId (偏移量12)
        //        // uint nSelCharId (偏移量16)
        //        // uint nClientId (偏移量20)
        //        // uint dwEnterTime (偏移量24)
        //        // byte szName[32] (偏移量28)
        //        // uint dwSelectCharServerId (偏移量60)

        //        // 转换uint字段（4字节）从大端序到小端序
        //        // 注意：szAccount[12]是字节数组，不需要转换
        //        // uint字段的偏移量：12, 16, 20, 24, 60
        //        int[] uintOffsets = { 12, 16, 20, 24, 60 };
        //        foreach (int offset in uintOffsets)
        //        {
        //            if (offset + 4 <= convertedBytes.Length)
        //            {
        //                // 反转字节顺序（大端序 -> 小端序）
        //                Array.Reverse(convertedBytes, offset, 4);
        //            }
        //        }
        //    }
        //    else if (typeof(T) == typeof(MirCommon.Database.CHARDBINFO))
        //    {
        //        // CHARDBINFO结构体字段布局（根据DatabaseStructs.cs）：
        //        // uint dwClientKey (偏移量0) - 第一个字段！
        //        // char szName[20] (偏移量4)
        //        // uint dwDBId (偏移量24)
        //        // uint mapid (偏移量28)
        //        // ushort x (偏移量32)
        //        // ushort y (偏移量34)
        //        // ... 其他字段
                
        //        // 转换uint字段（4字节）从大端序到小端序
        //        // 注意：dwClientKey是第一个字段，偏移量0
        //        // 其他uint字段也需要转换
        //        // 根据结构体定义，需要转换的uint字段偏移量：0, 24, 28, 36, 40, 44, 48, 52, 56, 60, 64, 68, 72, 76, 80, 84, 88, 92, 96, 100, 104, 108, 112, 116, 120, 124, 128, 132

        //        int[] uintOffsets = { 0, 24, 28, 36, 40, 44, 48, 52, 56, 60, 64, 68, 72, 76, 80, 84, 88, 92, 96, 100, 104, 108, 112, 116, 120, 124, 128, 132 };
        //        foreach (int offset in uintOffsets)
        //        {
        //            if (offset + 4 <= convertedBytes.Length)
        //            {
        //                // 反转字节顺序（大端序 -> 小端序）
        //                Array.Reverse(convertedBytes, offset, 4);
        //            }
        //        }
                
        //        // 转换ushort字段（2字节）从大端序到小端序
        //        // ushort字段偏移量：32(x), 34(y), 46(wLevel)
        //        int[] ushortOffsets = { 32, 34, 46 };
        //        foreach (int offset in ushortOffsets)
        //        {
        //            if (offset + 2 <= convertedBytes.Length)
        //            {
        //                // 反转字节顺序（大端序 -> 小端序）
        //                Array.Reverse(convertedBytes, offset, 2);
        //            }
        //        }
        //    }

        //    IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
        //    try
        //    {
        //        System.Runtime.InteropServices.Marshal.Copy(convertedBytes, 0, ptr, size);
        //        return System.Runtime.InteropServices.Marshal.PtrToStructure<T>(ptr);
        //    }
        //    finally
        //    {
        //        System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
        //    }
        //}

        /// <summary>
        /// 发送心跳包到ServerCenter
        /// </summary>
        private async Task SendHeartbeatAsync()
        {
            while (_isRunning && _serverCenterClient != null)
            {
                try
                {
                    await Task.Delay(30000); // 每30秒发送一次心跳

                    if (_serverCenterClient == null || !_isRunning)
                        break;

                    // 发送心跳包
                    bool sent = await _serverCenterClient.SendHeartbeatAsync();
                    if (sent)
                    {
                        LogManager.Default.Debug("ServerCenter心跳发送成功");
                    }
                    else
                    {
                        LogManager.Default.Warning("ServerCenter心跳发送失败");
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"发送ServerCenter心跳失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 检查ServerCenter连接是否有效
        /// </summary>
        private bool IsServerCenterConnected()
        {
            return _serverCenterClient != null;
        }

        /// <summary>
        /// 获取服务器名称
        /// </summary>
        public string GetServerName()
        {
            return _name;
        }

        #region 迁移备份

        /// <summary>
        /// 处理进入游戏服务器消息（来自SelectCharServer）
        /// </summary>
        private async Task HandleEnterGameServerMessage(MirMsg msg, byte[] payload)
        {
            try
            {
                LogManager.Default.Debug($"收到跨服务器消息: MAS_ENTERGAMESERVER (0x{ProtocolCmd.MAS_ENTERGAMESERVER:X4})");
                LogManager.Default.Debug($"消息标志: 0x{msg.dwFlag:X8}");
                LogManager.Default.Debug($"消息参数: w1=0x{msg.wParam[0]:X4}, w2=0x{msg.wParam[1]:X4}, w3=0x{msg.wParam[2]:X4}");
                LogManager.Default.Debug($"消息数据长度: {payload.Length}字节");

                // 打印payload的十六进制内容以便调试
                if (payload.Length > 0)
                {
                    string hexPayload = BitConverter.ToString(payload).Replace("-", " ");
                    LogManager.Default.Debug($"Payload十六进制: {hexPayload.Substring(0, Math.Min(100, hexPayload.Length))}...");
                }

                // 解析EnterGameServer结构体
                if (payload.Length < System.Runtime.InteropServices.Marshal.SizeOf<MirCommon.EnterGameServer>())
                {
                    LogManager.Default.Error($"EnterGameServer消息数据长度不足: {payload.Length}字节, 需要至少{System.Runtime.InteropServices.Marshal.SizeOf<MirCommon.EnterGameServer>()}字节");
                    return;
                }

                // 将字节数组转换为EnterGameServer结构体
                var enterGameServer = BytesToStruct<MirCommon.EnterGameServer>(payload);

                // 检查账号字段是否为空
                string account = enterGameServer.GetAccount();
                string name = enterGameServer.GetName();
                uint loginId = enterGameServer.nLoginId;

                LogManager.Default.Info($"收到玩家进入游戏服务器通知: 账号='{account}', 角色名='{name}'");
                LogManager.Default.Debug($"详细结构体信息:");
                LogManager.Default.Debug($"  - 账号字节数组: {BitConverter.ToString(enterGameServer.szAccount).Replace("-", " ")}");
                LogManager.Default.Debug($"  - 账号字符串: '{account}' (长度: {account?.Length ?? 0})");
                LogManager.Default.Debug($"  - 角色名字节数组: {BitConverter.ToString(enterGameServer.szName).Replace("-", " ")}");
                LogManager.Default.Debug($"  - 角色名字符串: '{name}' (长度: {name?.Length ?? 0})");
                LogManager.Default.Debug($"  - 登录ID: {enterGameServer.nLoginId}");
                LogManager.Default.Debug($"  - 选择角色ID: {enterGameServer.nSelCharId}");
                LogManager.Default.Debug($"  - 客户端ID: {enterGameServer.nClientId}");
                LogManager.Default.Debug($"  - 进入时间: {enterGameServer.dwEnterTime}");
                LogManager.Default.Debug($"  - 选择角色服务器ID: {enterGameServer.dwSelectCharServerId}");

                // 保存进入信息，供OnVerifyString方法使用

                _enterInfoDict[loginId] = enterGameServer;
                LogManager.Default.Debug($"已保存进入信息到_enterInfo");
                LogManager.Default.Debug($"_enterInfo.GetAccount() = '{enterGameServer.GetAccount()}'");
                LogManager.Default.Debug($"_enterInfo.GetName() = '{enterGameServer.GetName()}'");
                LogManager.Default.Debug($"_enterInfo.nLoginId = {enterGameServer.nLoginId}");
                LogManager.Default.Debug($"_enterInfo.nSelCharId = {enterGameServer.nSelCharId}");

                // 发送确认响应
                SendEnterGameServerAck1();
                LogManager.Default.Debug($"已发送进入游戏服务器确认响应");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理进入游戏服务器消息失败: {ex.Message}");
                LogManager.Default.Error($"堆栈跟踪: {ex.StackTrace}");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 发送进入游戏服务器确认响应
        /// </summary>
        private void SendEnterGameServerAck1()
        {
            //客户端第一个弹框登录成功包需要返回给客户端？
            //try
            //{
            //    // 发送简单的确认消息
            //    var builder = new PacketBuilder();
            //    builder.WriteUInt32(1); // 成功标志
            //    builder.WriteUInt16(ProtocolCmd.MAS_ENTERGAMESERVER);
            //    builder.WriteUInt16(0);
            //    builder.WriteUInt16(0);
            //    builder.WriteUInt16(0);

            //    byte[] packet = builder.Build();
            //    _stream.Write(packet, 0, packet.Length);
            //    _stream.Flush();
            //}
            //catch (Exception ex)
            //{
            //    LogManager.Default.Error($"发送进入游戏服务器确认失败: {ex.Message}");
            //}
        }

        #endregion


        /// <summary>
        /// 根据登录ID获取进入信息
        /// </summary>
        public MirCommon.EnterGameServer? GetEnterInfo(uint loginId)
        {
            lock (_enterInfoLock)
            {
                if (_enterInfoDict.TryGetValue(loginId, out var enterInfo))
                {
                    LogManager.Default.Debug($"从字典获取进入信息成功，登录ID={loginId}");
                    return enterInfo;
                }
                else
                {
                    LogManager.Default.Debug($"字典中未找到进入信息，登录ID={loginId}，字典大小={_enterInfoDict.Count}");
                    return null;
                }
            }
        }

        /// <summary>
        /// 移除进入信息（客户端连接后）
        /// </summary>
        public void RemoveEnterInfo(uint loginId)
        {
            lock (_enterInfoLock)
            {
                if (_enterInfoDict.Remove(loginId))
                {
                    LogManager.Default.Debug($"已从字典移除进入信息，登录ID={loginId}");
                }
            }
        }


        /// <summary>
        /// 处理DBServer消息（事件处理器）
        /// </summary>
        private void HandleDbServerMessage(MirCommon.MirMsg msg)
        {
            try
            {
                LogManager.Default.Info($"收到DBServer消息: Cmd=0x{msg.wCmd:X4}({(DbMsg)msg.wCmd}), Flag=0x{msg.dwFlag:X8}, w1={msg.wParam[0]}, w2={msg.wParam[1]}, w3={msg.wParam[2]}, 数据长度={msg.data?.Length ?? 0}字节");

                // 如果dwFlag == 0，消息是发给服务器的（服务器级别消息）
                // 如果dwFlag != 0，消息是发给特定客户端的（客户端级别消息）
                if (msg.dwFlag == 0)
                {
                    // 服务器级别消息，由GameServerApp处理
                    LogManager.Default.Info($"处理服务器级别DBServer消息: Cmd=0x{msg.wCmd:X4}({(DbMsg)msg.wCmd})");
                    
                    // 特殊处理：DM_GETCHARDBINFO虽然是服务器级别消息，但需要路由到对应的GameClient
                    // 因为clientKey在CHARDBINFO结构体的dwClientKey字段中
                    if (msg.wCmd == (ushort)DbMsg.DM_GETCHARDBINFO)
                    {
                        // 尝试从消息数据中解析clientKey
                        if (msg.data != null && msg.data.Length >= 136) // CHARDBINFO结构大小
                        {
                            try
                            {
                                // 解析CHARDBINFO结构体获取clientKey
                                var charDbInfo = BytesToStruct<MirCommon.Database.CHARDBINFO>(msg.data);
                                uint clientKey = charDbInfo.dwClientKey;
                                
                                if (clientKey > 0)
                                {
                                    // 查找对应的GameClient
                                    GameClient? targetClient = FindGameClientById(clientKey);
                                    if (targetClient != null)
                                    {
                                        LogManager.Default.Info($"将DM_GETCHARDBINFO消息路由到GameClient: clientKey={clientKey}");
                                        targetClient.HandleDbServerMessage(msg);
                                        return;
                                    }
                                    else
                                    {
                                        LogManager.Default.Warning($"未找到对应的GameClient: clientKey={clientKey}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogManager.Default.Error($"解析CHARDBINFO结构体失败: {ex.Message}");
                            }
                        }
                    }
                    
                    // 其他服务器级别消息由GameServerApp处理
                    HandleServerLevelDbMessage(msg);
                }
                else
                {
                    // 客户端级别消息，根据dwFlag自动路由到对应的GameClient
                    uint clientId = msg.dwFlag;
                    LogManager.Default.Info($"处理客户端级别DBServer消息: 目标客户端ID={clientId}");
                    
                    // 查找对应的GameClient
                    GameClient? targetClient = FindGameClientById(clientId);
                    if (targetClient != null)
                    {
                        LogManager.Default.Info($"将DBServer消息路由到GameClient: clientId={clientId}");
                        targetClient.HandleDbServerMessage(msg);
                    }
                    else
                    {
                        LogManager.Default.Warning($"未找到对应的GameClient: clientId={clientId}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理DBServer消息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理服务器级别的DBServer消息
        /// </summary>
        private void HandleServerLevelDbMessage(MirCommon.MirMsg msg)
        {
            try
            {
                switch (msg.wCmd)
                {
                    case (ushort)DbMsg.DM_CREATEITEM:
                        HandleDBCreateItem(msg);
                        break;
                    // 其他服务器级别消息...
                    default:
                        LogManager.Default.Warning($"未处理的服务器级别DBServer消息: 0x{msg.wCmd:X4}({(DbMsg)msg.wCmd})");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理服务器级别DBServer消息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理DM_CREATEITEM消息
        /// </summary>
        private void HandleDBCreateItem(MirCommon.MirMsg msg)
        {
            try
            {
                LogManager.Default.Info($"处理DM_CREATEITEM消息");
                // TODO: 实现DM_CREATEITEM消息处理逻辑
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理DM_CREATEITEM消息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 注册GameClient到分发字典
        /// </summary>
        private void RegisterGameClient(GameClient client)
        {
            try
            {
                // 获取clientId（从GameClient中获取）
                uint clientId = client.GetId();
                if (clientId > 0)
                {
                    lock (_gameClientsLock)
                    {
                        _gameClients[clientId] = client;
                        LogManager.Default.Info($"已注册GameClient: clientId={clientId}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"注册GameClient失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从分发字典中移除GameClient
        /// </summary>
        private void UnregisterGameClient(GameClient client)
        {
            try
            {
                // 获取clientId（从GameClient中获取）
                uint clientId = client.GetId();
                if (clientId > 0)
                {
                    lock (_gameClientsLock)
                    {
                        if (_gameClients.TryRemove(clientId, out _))
                        {
                            LogManager.Default.Info($"已移除GameClient: clientId={clientId}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"移除GameClient失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 根据客户端ID查找GameClient
        /// </summary>
        private GameClient? FindGameClientById(uint clientId)
        {
            lock (_gameClientsLock)
            {
                if (_gameClients.TryGetValue(clientId, out var client))
                {
                    return client;
                }
                return null;
            }
        }

        /// <summary>
        /// 获取DBServerClient实例
        /// </summary>
        public MirCommon.Database.DBServerClient? GetDbServerClient()
        {
            return _dbServerClient;
        }
    }


    /// <summary>
    /// 客户端状态
    /// </summary>
    public enum ClientState
    {
        GSUM_NOTVERIFIED = 0,      // 未验证
        GSUM_WAITINGDBINFO = 1,    // 等待数据库信息
        GSUM_WAITINGCONFIRM = 2,   // 等待确认
        GSUM_VERIFIED = 3          // 已验证
    }

    /// <summary>
    /// 角色信息结构
    /// </summary>
    public class CharacterInfo
    {
        public int Id { get; set; }
        public byte Job { get; set; }
        public byte Sex { get; set; }
        public short Level { get; set; }
        public string MapName { get; set; } = string.Empty;
        public short X { get; set; }
        public short Y { get; set; }
        public byte Hair { get; set; }
        public uint Exp { get; set; }
        public ushort CurrentHP { get; set; }
        public ushort CurrentMP { get; set; }
        public ushort MaxHP { get; set; }
        public ushort MaxMP { get; set; }
        public ushort MinDC { get; set; }
        public ushort MaxDC { get; set; }
        public ushort MinMC { get; set; }
        public ushort MaxMC { get; set; }
        public ushort MinSC { get; set; }
        public ushort MaxSC { get; set; }
        public ushort MinAC { get; set; }
        public ushort MaxAC { get; set; }
        public ushort MinMAC { get; set; }
        public ushort MaxMAC { get; set; }
        public ushort Weight { get; set; }
        public ushort HandWeight { get; set; }
        public ushort BodyWeight { get; set; }
        public uint Gold { get; set; }
        public int MapId { get; set; }
        public uint Yuanbao { get; set; }
        public uint Flag1 { get; set; }
        public uint Flag2 { get; set; }
        public uint Flag3 { get; set; }
        public uint Flag4 { get; set; }
        public string GuildName { get; set; } = string.Empty;
        public uint ForgePoint { get; set; }
        public uint Prop1 { get; set; }
        public uint Prop2 { get; set; }
        public uint Prop3 { get; set; }
        public uint Prop4 { get; set; }
        public uint Prop5 { get; set; }
        public uint Prop6 { get; set; }
        public uint Prop7 { get; set; }
        public uint Prop8 { get; set; }
        public ushort Accuracy { get; set; }
        public ushort Agility { get; set; }
        public ushort Lucky { get; set; }
    }
}
