using MirCommon;
using MirCommon.Database;
using MirCommon.Network;
using MirCommon.Utils;
using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
// 类型别名
using Player = GameServer.HumanPlayer;

namespace GameServer
{
    /// <summary>
    /// IOCP兼容的游戏客户端
    /// </summary>
    public class IocpGameClient : IDisposable
    {
        #region 字段
        private readonly Socket _socket;
        private readonly IocpGameServerApp _server;
        private readonly GameWorld _world;
        private readonly string _dbServerAddress;
        private readonly int _dbServerPort;
        private Player? _player;
        
        // 客户端状态管理
        private ClientState _state = ClientState.GSUM_NOTVERIFIED;
        private MirCommon.EnterGameServer _enterInfo = new MirCommon.EnterGameServer();
        private uint _clientKey = 0;
        private static uint _nextClientKey = 1;
        
        private int _gmLevel = 0;
        private bool _scrollTextMode = false;
        private bool _noticeMode = false;
        private bool _competlyQuit = false;
        private readonly System.Diagnostics.Stopwatch _hlTimer = System.Diagnostics.Stopwatch.StartNew();
        
        // 数据加载状态跟踪
        private bool _bagLoaded = false;
        private bool _equipmentLoaded = false;
        private bool _magicLoaded = false;
        private bool _taskInfoLoaded = false;
        private bool _upgradeItemLoaded = false;
        private bool _petBankLoaded = false;
        private bool _bankLoaded = false;
        
        // 接收缓冲区
        private readonly byte[] _receiveBuffer = new byte[8192];
        private int _receiveOffset = 0;
        private readonly object _receiveLock = new object();
        
        // 发送队列
        private readonly ConcurrentQueue<byte[]> _sendQueue = new ConcurrentQueue<byte[]>();
        private readonly object _sendLock = new object();
        private bool _isSending = false;
        
        // 处理标志
        private volatile bool _isProcessing = false;
        private volatile bool _isDisposed = false;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        #endregion
        
        #region 属性
        /// <summary>
        /// 获取客户端ID
        /// </summary>
        public uint GetId() => _clientKey;
        
        /// <summary>
        /// 获取客户端Key
        /// </summary>
        public uint GetClientKey() => _clientKey;
        
        /// <summary>
        /// 获取Socket
        /// </summary>
        public Socket Socket => _socket;
        
        /// <summary>
        /// 获取玩家对象
        /// </summary>
        public Player? Player => _player;
        #endregion
        
        #region 构造函数
        /// <summary>
        /// 创建IOCP兼容的游戏客户端
        /// </summary>
        public IocpGameClient(Socket socket, IocpGameServerApp server, GameWorld world, string dbServerAddress, int dbServerPort)
        {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _dbServerAddress = dbServerAddress;
            _dbServerPort = dbServerPort;
            _clientKey = Interlocked.Increment(ref _nextClientKey);
            
            // 配置Socket
            socket.NoDelay = true;
            socket.ReceiveBufferSize = 8192;
            socket.SendBufferSize = 8192;
            
            LogManager.Default.Info($"创建IOCP游戏客户端: ID={_clientKey}, 远程地址={socket.RemoteEndPoint}");
        }
        #endregion
        
        #region 公共方法
        /// <summary>
        /// 开始处理客户端消息
        /// </summary>
        public async Task ProcessAsync()
        {
            if (_isProcessing) return;
            
            _isProcessing = true;
            var token = _cancellationTokenSource.Token;
            
            try
            {
                LogManager.Default.Info($"开始处理IOCP客户端: ID={_clientKey}");
                
                // 启动发送任务
                var sendTask = Task.Run(() => SendProcessor(token), token);
                
                // 处理接收消息
                while (!token.IsCancellationRequested && _socket.Connected)
                {
                    try
                    {
                        // 接收数据
                        int bytesRead = await ReceiveDataAsync(token);
                        if (bytesRead == 0)
                        {
                            // 连接关闭
                            LogManager.Default.Info($"客户端连接关闭: ID={_clientKey}");
                            break;
                        }
                        
                        // 处理接收到的数据
                        await ProcessReceivedData(bytesRead);
                    }
                    catch (OperationCanceledException)
                    {
                        // 任务被取消
                        break;
                    }
                    catch (SocketException ex)
                    {
                        LogManager.Default.Error($"Socket错误 (ID={_clientKey}): {ex.Message}");
                        break;
                    }
                    catch (Exception ex)
                    {
                        LogManager.Default.Error($"处理客户端错误 (ID={_clientKey}): {ex.Message}");
                        break;
                    }
                }
                
                // 等待发送任务完成
                await sendTask;
            }
            finally
            {
                _isProcessing = false;
                OnDisconnect();
                LogManager.Default.Info($"结束处理IOCP客户端: ID={_clientKey}");
            }
        }
        
        /// <summary>
        /// 发送消息到客户端
        /// </summary>
        public void SendMessage(byte[] data)
        {
            if (_isDisposed || !_socket.Connected) return;
            
            try
            {
                _sendQueue.Enqueue(data);
                
                // 触发发送
                lock (_sendLock)
                {
                    if (!_isSending)
                    {
                        Task.Run(() => SendFromQueue());
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送消息到队列失败 (ID={_clientKey}): {ex.Message}");
            }
        }
        
        /// <summary>
        /// 发送游戏消息
        /// </summary>
        public void SendGameMessage(uint dwFlag, ushort wCmd, ushort w1 = 0, ushort w2 = 0, ushort w3 = 0, byte[]? payload = null)
        {
            try
            {
                var msg = GameMessageHandler.CreateMessage2(dwFlag, wCmd, w1, w2, w3);
                byte[] encoded = GameMessageHandler.EncodeGameMessageOrign(msg, payload);
                
                if (encoded.Length > 0)
                {
                    SendMessage(encoded);
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送游戏消息失败 (ID={_clientKey}): {ex.Message}");
            }
        }
        
        /// <summary>
        /// 发送简单游戏消息
        /// </summary>
        public void SendSimpleMessage(uint dwFlag, ushort wCmd, ushort w1 = 0, ushort w2 = 0, ushort w3 = 0, byte[]? payload = null)
        {
            SendGameMessage(dwFlag, wCmd, w1, w2, w3, payload);
        }
        
        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            try
            {
                _cancellationTokenSource.Cancel();
                
                if (_socket.Connected)
                {
                    _socket.Shutdown(SocketShutdown.Both);
                    _socket.Close();
                }
                
                LogManager.Default.Info($"断开IOCP客户端连接: ID={_clientKey}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"断开连接失败 (ID={_clientKey}): {ex.Message}");
            }
        }
        
        /// <summary>
        /// 处理DBServer消息（从GameServerApp分发）
        /// </summary>
        public void HandleDbServerMessage(MirCommon.MirMsg msg)
        {
            try
            {
                LogManager.Default.Info($"IOCP客户端收到转发的DBServer消息: Cmd=0x{msg.wCmd:X4}, Flag=0x{msg.dwFlag:X8}");
                
                // 调用现有的OnDBMsg方法处理消息
                _ = OnDBMsg(msg, msg.data?.Length ?? 0);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理DBServer消息失败 (ID={_clientKey}): {ex.Message}");
            }
        }
        #endregion
        
        #region 私有方法
        /// <summary>
        /// 接收数据
        /// </summary>
        private async Task<int> ReceiveDataAsync(CancellationToken token)
        {
            try
            {
                var buffer = new byte[4096];
                var receiveTask = _socket.ReceiveAsync(buffer, SocketFlags.None, token);
                
                // 设置超时
                var timeoutTask = Task.Delay(30000, token); // 30秒超时
                var completedTask = await Task.WhenAny(receiveTask.AsTask(), timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    LogManager.Default.Warning($"接收数据超时 (ID={_clientKey})");
                    return 0;
                }
                
                int bytesRead = await receiveTask;
                
                if (bytesRead > 0)
                {
                    lock (_receiveLock)
                    {
                        // 确保缓冲区足够大
                        if (_receiveOffset + bytesRead > _receiveBuffer.Length)
                        {
                            // 扩展缓冲区
                            byte[] newBuffer = new byte[Math.Max(_receiveBuffer.Length * 2, _receiveOffset + bytesRead)];
                            Array.Copy(_receiveBuffer, 0, newBuffer, 0, _receiveOffset);
                            Array.Copy(buffer, 0, newBuffer, _receiveOffset, bytesRead);
                            Array.Copy(newBuffer, _receiveBuffer, newBuffer.Length);
                            _receiveOffset += bytesRead;
                        }
                        else
                        {
                            Array.Copy(buffer, 0, _receiveBuffer, _receiveOffset, bytesRead);
                            _receiveOffset += bytesRead;
                        }
                    }
                }
                
                return bytesRead;
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"接收数据失败 (ID={_clientKey}): {ex.Message}");
                return 0;
            }
        }
        
        /// <summary>
        /// 处理接收到的数据
        /// </summary>
        private async Task ProcessReceivedData(int bytesRead)
        {
            lock (_receiveLock)
            {
                if (_receiveOffset == 0) return;
                
                // 处理缓冲区中的数据
                int processed = 0;
                while (processed < _receiveOffset)
                {
                    // 查找消息边界（以'#'开头，以'!'结尾）
                    int startIndex = -1;
                    int endIndex = -1;
                    
                    for (int i = processed; i < _receiveOffset; i++)
                    {
                        if (_receiveBuffer[i] == '#' && startIndex == -1)
                        {
                            startIndex = i;
                        }
                        else if (_receiveBuffer[i] == '!' && startIndex != -1)
                        {
                            endIndex = i;
                            break;
                        }
                    }
                    
                    if (startIndex == -1 || endIndex == -1)
                    {
                        // 没有完整的消息，等待更多数据
                        break;
                    }
                    
                    // 提取完整消息
                    int messageLength = endIndex - startIndex + 1;
                    byte[] message = new byte[messageLength];
                    Array.Copy(_receiveBuffer, startIndex, message, 0, messageLength);
                    
                    // 处理消息
                    _ = ProcessSingleMessage(message);
                    
                    processed = endIndex + 1;
                }
                
                // 移动剩余数据到缓冲区开头
                if (processed > 0)
                {
                    int remaining = _receiveOffset - processed;
                    if (remaining > 0)
                    {
                        Array.Copy(_receiveBuffer, processed, _receiveBuffer, 0, remaining);
                    }
                    _receiveOffset = remaining;
                }
            }
            
            await Task.CompletedTask;
        }
        
        /// <summary>
        /// 处理单个消息
        /// </summary>
        private async Task ProcessSingleMessage(byte[] message)
        {
            try
            {
                bool decodeSuccess = GameMessageHandler.DecodeGameMessageOrign(message, message.Length, out var msg, out var payload);
                
                if (!decodeSuccess)
                {
                    LogManager.Default.Warning($"解码消息失败 (ID={_clientKey})");
                    return;
                }
                
                LogManager.Default.Debug($"处理消息 (ID={_clientKey}): Cmd=0x{msg.wCmd:X4}");
                
                // 根据客户端状态处理消息
                if (_state != ClientState.GSUM_VERIFIED)
                {
                    await HandlePreVerifiedMessage(msg, payload);
                }
                else
                {
                    await HandleGameMessage(msg, payload);
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理单个消息失败 (ID={_clientKey}): {ex.Message}");
            }
        }
        
        /// <summary>
        /// 处理验证前的消息
        /// </summary>
        private async Task HandlePreVerifiedMessage(MirMsgOrign msg, byte[] payload)
        {
            switch (_state)
            {
                case ClientState.GSUM_NOTVERIFIED:
                    await HandleVerifyString(msg, payload);
                    break;
                    
                case ClientState.GSUM_WAITINGDBINFO:
                    // 等待数据库信息，忽略其他消息
                    LogManager.Default.Debug($"等待数据库信息状态，忽略消息 (ID={_clientKey})");
                    break;
                    
                case ClientState.GSUM_WAITINGCONFIRM:
                    await HandleWaitingConfirmMessage(msg, payload);
                    break;
            }
        }
        
        /// <summary>
        /// 处理验证字符串
        /// </summary>
        private async Task HandleVerifyString(MirMsgOrign msg, byte[] payload)
        {
            try
            {
                // 将消息数据转换为字符串
                string verifyString = Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
                LogManager.Default.Info($"处理验证字符串 (ID={_clientKey}): {verifyString}");
                
                _state = ClientState.GSUM_WAITINGDBINFO;
                
                // 模拟查询数据库
                await SimulateQueryDatabase();
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理验证字符串失败 (ID={_clientKey}): {ex.Message}");
            }
        }
        
        /// <summary>
        /// 处理等待确认消息
        /// </summary>
        private async Task HandleWaitingConfirmMessage(MirMsgOrign msg, byte[] payload)
        {
            if (msg.wCmd == GameMessageHandler.ClientCommands.CM_CONFIRMFIRSTDIALOG)
            {
                LogManager.Default.Info($"收到确认第一个对话框消息 (ID={_clientKey})");
                await HandleConfirmFirstDialog(msg, payload);
            }
            else
            {
                LogManager.Default.Debug($"等待确认状态，忽略非确认消息 (ID={_clientKey}): Cmd=0x{msg.wCmd:X4}");
            }
        }
        
        /// <summary>
        /// 处理确认第一个对话框
        /// </summary>
        private async Task HandleConfirmFirstDialog(MirMsgOrign msg, byte[] payload)
        {
            try
            {
                LogManager.Default.Info($"处理确认第一个对话框 (ID={_clientKey})");
                
                // 设置状态为已验证
                _state = ClientState.GSUM_VERIFIED;
                
                // 创建玩家对象
                await CreatePlayer();
                
                // 发送进入游戏成功消息
                SendEnterGameOk();
                
                LogManager.Default.Info($"客户端已验证并进入游戏 (ID={_clientKey})");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理确认第一个对话框失败 (ID={_clientKey}): {ex.Message}");
            }
        }
        
        /// <summary>
        /// 模拟查询数据库
        /// </summary>
        private async Task SimulateQueryDatabase()
        {
            try
            {
                LogManager.Default.Info($"模拟查询数据库 (ID={_clientKey})");
                
                // 模拟数据库查询延迟
                await Task.Delay(100);
                
                // 设置进入信息
                _enterInfo.nLoginId = _clientKey;
                _enterInfo.nSelCharId = _clientKey;
                _enterInfo.SetName($"Player_{_clientKey}");
                _enterInfo.SetAccount($"Account_{_clientKey}");
                
                // 发送第一个对话框
                SendFirstDlg("欢迎来到游戏服务器！");
                
                // 设置状态为等待确认
                _state = ClientState.GSUM_WAITINGCONFIRM;
                
                LogManager.Default.Info($"数据库查询完成，等待客户端确认 (ID={_clientKey})");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"模拟查询数据库失败 (ID={_clientKey}): {ex.Message}");
            }
        }
        
        /// <summary>
        /// 创建玩家对象
        /// </summary>
        private async Task CreatePlayer()
        {
            try
            {
                LogManager.Default.Info($"创建玩家对象 (ID={_clientKey})");
                
                // 创建玩家
                string account = _enterInfo.GetAccount();
                string playerName = _enterInfo.GetName();
                uint charId = _enterInfo.nSelCharId;
                
                _player = HumanPlayerMgr.Instance.NewPlayer(account, playerName, charId, null);
                
                if (_player == null)
                {
                    LogManager.Default.Error($"创建玩家对象失败 (ID={_clientKey})");
                    return;
                }
                
                // 设置发送消息委托
                _player.SetSendMessageDelegate((uint dwFlag, ushort wCmd, ushort w1, ushort w2, ushort w3, byte[]? payload) =>
                {
                    try
                    {
                        SendGameMessage(dwFlag, wCmd, w1, w2, w3, payload);
                    }
                    catch (Exception ex)
                    {
                        LogManager.Default.Error($"通过委托发送消息失败 (ID={_clientKey}): {ex.Message}");
                    }
                });



                // lyo：创建一个空玩家对象
                var createDesc = new CREATEHUMANDESC
                {
                    dbinfo = new CHARDBINFO(),
                    pClientObj = IntPtr.Zero
                };

                // 初始化玩家
                if (!_player.Init(createDesc))
                {
                    LogManager.Default.Error($"初始化玩家失败 (ID={_clientKey})");
                    HumanPlayerMgr.Instance.DeletePlayer(_player);
                    _player = null;
                    return;
                }
                
                // 添加到游戏世界
                _world.AddPlayer(_player);
                
                LogManager.Default.Info($"玩家创建成功 (ID={_clientKey}): {playerName}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"创建玩家对象失败 (ID={_clientKey}): {ex.Message}");
            }
        }
        
        /// <summary>
        /// 处理游戏消息
        /// </summary>
        private async Task HandleGameMessage(MirMsgOrign msg, byte[] payload)
        {
            try
            {
                LogManager.Default.Debug($"处理游戏消息 (ID={_clientKey}): Cmd=0x{msg.wCmd:X4}");
                
                // 根据消息命令处理
                switch (msg.wCmd)
                {
                    case GameMessageHandler.ClientCommands.CM_WALK:
                        await HandleWalkMessage(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_RUN:
                        await HandleRunMessage(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_SAY:
                        await HandleSayMessage(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_TURN:
                        await HandleTurnMessage(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_ATTACK:
                        await HandleAttackMessage(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_STOP:
                        await HandleStopMessage(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_TAKEONITEM:
                        await HandleTakeOnItem(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_TAKEOFFITEM:
                        await HandleTakeOffItem(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_DROPITEM:
                        await HandleDropItem(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_PICKUPITEM:
                        await HandlePickupItem(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_SPELLSKILL:
                        await HandleSpellSkill(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_QUERYTRADE:
                        await HandleQueryTrade(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_PUTTRADEITEM:
                        await HandlePutTradeItem(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_PUTTRADEGOLD:
                        await HandlePutTradeGold(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_QUERYTRADEEND:
                        await HandleQueryTradeEnd(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_CANCELTRADE:
                        await HandleCancelTrade(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_USEITEM:
                        await HandleUseItem(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_NPCTALK:
                        await HandleNPCTalk(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_BUYITEM:
                        await HandleBuyItem(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_SELLITEM:
                        await HandleSellItem(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_REPAIRITEM:
                        await HandleRepairItem(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_QUERYREPAIRPRICE:
                        await HandleQueryRepairPrice(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_QUERYMINIMAP:
                        await HandleQueryMinimap(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_VIEWEQUIPMENT:
                        await HandleViewEquipment(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_MINE:
                        await HandleMine(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_TRAINHORSE:
                        await HandleTrainHorse(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_SPECIALHIT_KILL:
                    case GameMessageHandler.ClientCommands.CM_SPECIALHIT_ASSASSINATE:
                    case GameMessageHandler.ClientCommands.CM_SPECIALHIT_HALFMOON:
                    case GameMessageHandler.ClientCommands.CM_SPECIALHIT_FIRE:
                    case GameMessageHandler.ClientCommands.CM_SPECIALHIT_POJISHIELD:
                        await HandleSpecialHit(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_LEAVESERVER:
                        await HandleLeaveServer(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_PING:
                        await HandlePing(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_QUERYTIME:
                        await HandleQueryTime(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_RIDEHORSE:
                        await HandleRideHorse(msg, payload);
                        break;
                    case GameMessageHandler.ClientCommands.CM_DROPGOLD:
                        await HandleDropGold(msg, payload);
                        break;
                    default:
                        LogManager.Default.Warning($"未处理的游戏消息 (ID={_clientKey}): Cmd=0x{msg.wCmd:X4}");
                        SendUnknownCommandResponse(msg.wCmd);
                        break;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理游戏消息失败 (ID={_clientKey}): {ex.Message}");
            }
        }
        
        /// <summary>
        /// 发送第一个对话框
        /// </summary>
        private void SendFirstDlg(string message)
        {
            try
            {
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(message);
                SendGameMessage(0, GameMessageHandler.ServerCommands.SM_FIRSTDIALOG, 0, 0, 0, payload);
                LogManager.Default.Info($"发送第一个对话框 (ID={_clientKey}): {message}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送第一个对话框失败 (ID={_clientKey}): {ex.Message}");
            }
        }
        
        /// <summary>
        /// 发送进入游戏成功消息
        /// </summary>
        private void SendEnterGameOk()
        {
            try
            {
                if (_player == null) return;
                
                var builder = new PacketBuilder();
                builder.WriteUInt32(_player.ObjectId);
                builder.WriteUInt16(ProtocolCmd.SM_ENTERGAMEOK);
                builder.WriteUInt16(0);
                builder.WriteUInt16(0);
                builder.WriteUInt16(0);
                builder.WriteInt32(_player.MapId);
                builder.WriteInt32(_player.X);
                builder.WriteInt32(_player.Y);
                
                byte[] packet = builder.Build();
                SendMessage(packet);
                
                LogManager.Default.Info($"发送进入游戏成功消息 (ID={_clientKey})");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送进入游戏成功消息失败 (ID={_clientKey}): {ex.Message}");
            }
        }
        
        /// <summary>
        /// 发送未知命令响应
        /// </summary>
        private void SendUnknownCommandResponse(ushort command)
        {
            try
            {
                SendGameMessage(0, GameMessageHandler.ServerCommands.SM_UNKNOWN_COMMAND, 0, 0, 0);
                LogManager.Default.Debug($"发送未知命令响应 (ID={_clientKey}): Cmd=0x{command:X4}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送未知命令响应失败 (ID={_clientKey}): {ex.Message}");
            }
        }
        
        /// <summary>
        /// 发送处理器
        /// </summary>
        private void SendProcessor(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && _socket.Connected)
                {
                    SendFromQueue();
                    Thread.Sleep(10); // 避免CPU占用过高
                }
            }
            catch (OperationCanceledException)
            {
                // 任务被取消
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送处理器错误 (ID={_clientKey}): {ex.Message}");
            }
        }
        
        /// <summary>
        /// 从队列发送消息
        /// </summary>
        private void SendFromQueue()
        {
            lock (_sendLock)
            {
                if (_isSending) return;
                _isSending = true;
            }
            
            try
            {
                while (_sendQueue.TryDequeue(out byte[]? data))
                {
                    if (data == null || data.Length == 0) continue;
                    
                    try
                    {
                        _socket.Send(data, SocketFlags.None);
                    }
                    catch (Exception ex)
                    {
                        LogManager.Default.Error($"发送数据失败 (ID={_clientKey}): {ex.Message}");
                        break;
                    }
                }
            }
            finally
            {
                lock (_sendLock)
                {
                    _isSending = false;
                }
            }
        }
        
        /// <summary>
        /// 处理数据库消息
        /// </summary>
        private async Task OnDBMsg(MirCommon.MirMsg pMsg, int datasize)
        {
            try
            {
                LogManager.Default.Info($"处理数据库消息 (ID={_clientKey}): Cmd=0x{pMsg.wCmd:X4}");
                
                switch (pMsg.wCmd)
                {
                    case (ushort)DbMsg.DM_GETCHARDBINFO:
                        // 处理角色数据库信息
                        LogManager.Default.Info($"收到角色数据库信息 (ID={_clientKey})");
                        break;
                    case (ushort)DbMsg.DM_QUERYITEMS:
                        // 处理物品查询结果
                        LogManager.Default.Info($"收到物品查询结果 (ID={_clientKey})");
                        break;
                    case (ushort)DbMsg.DM_QUERYMAGIC:
                        // 处理技能查询结果
                        LogManager.Default.Info($"收到技能查询结果 (ID={_clientKey})");
                        break;
                    case (ushort)DbMsg.DM_QUERYTASKINFO:
                        // 处理任务信息查询结果
                        LogManager.Default.Info($"收到任务信息查询结果 (ID={_clientKey})");
                        break;
                    default:
                        LogManager.Default.Warning($"未知的数据库消息 (ID={_clientKey}): Cmd=0x{pMsg.wCmd:X4}");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理数据库消息失败 (ID={_clientKey}): {ex.Message}");
            }
            
            await Task.CompletedTask;
        }
        
        /// <summary>
        /// 断开连接处理
        /// </summary>
        private void OnDisconnect()
        {
            try
            {
                // 玩家断开，从世界移除
                if (_player != null)
                {
                    LogManager.Default.Info($"玩家断开连接 (ID={_clientKey}): {_player.Name}");
                    
                    // 从地图移除
                    _world.RemovePlayer(_player.ObjectId);
                    
                    // 保存数据到数据库
                    SavePlayerDataToDB();
                    
                    // 清理资源
                    CleanupPlayerResources();
                    
                    _player = null;
                }
                
                LogManager.Default.Info($"客户端断开连接处理完成 (ID={_clientKey})");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"断开连接处理失败 (ID={_clientKey}): {ex.Message}");
            }
        }
        
        /// <summary>
        /// 保存玩家数据到数据库
        /// </summary>
        private void SavePlayerDataToDB()
        {
            if (_player == null) return;
            
            try
            {
                LogManager.Default.Info($"保存玩家数据 (ID={_clientKey}): {_player.Name}");
                // 这里实现保存玩家数据到数据库的逻辑
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"保存玩家数据失败 (ID={_clientKey}): {ex.Message}");
            }
        }
        
        /// <summary>
        /// 清理玩家资源
        /// </summary>
        private void CleanupPlayerResources()
        {
            if (_player == null) return;
            
            try
            {
                LogManager.Default.Info($"清理玩家资源 (ID={_clientKey}): {_player.Name}");
                // 这里实现清理玩家资源的逻辑
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"清理玩家资源失败 (ID={_clientKey}): {ex.Message}");
            }
        }
        
        #region 消息处理函数（占位符）
        private async Task HandleWalkMessage(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理行走消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        
        private async Task HandleRunMessage(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理奔跑消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        
        private async Task HandleSayMessage(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理说话消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        
        private async Task HandleTurnMessage(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理转向消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        
        private async Task HandleAttackMessage(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理攻击消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        
        private async Task HandleStopMessage(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理停止消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        
        private async Task HandleTakeOnItem(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理穿戴物品消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        
        private async Task HandleTakeOffItem(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理脱下物品消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        
        private async Task HandleDropItem(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理丢弃物品消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        
        private async Task HandlePickupItem(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理拾取物品消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        
        private async Task HandleSpellSkill(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理施放技能消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        
        private async Task HandleQueryTrade(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理查询交易消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        
        private async Task HandlePutTradeItem(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理放入交易物品消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        
        private async Task HandlePutTradeGold(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理放入交易金币消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        
        private async Task HandleQueryTradeEnd(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理查询交易结束消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        
        private async Task HandleCancelTrade(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理取消交易消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        
        private async Task HandleUseItem(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理使用物品消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        
        private async Task HandleNPCTalk(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理NPC对话消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        
        private async Task HandleBuyItem(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理购买物品消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        
        private async Task HandleSellItem(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理出售物品消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        
        private async Task HandleRepairItem(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理修理物品消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        
        private async Task HandleQueryRepairPrice(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理查询修理价格消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        
        private async Task HandleQueryMinimap(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理查询小地图消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        
        private async Task HandleViewEquipment(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理查看装备消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        
        private async Task HandleMine(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理挖矿消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        
        private async Task HandleTrainHorse(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理训练马匹消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        
        private async Task HandleSpecialHit(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理特殊攻击消息 (ID={_clientKey}): Cmd=0x{msg.wCmd:X4}");
            await Task.CompletedTask;
        }
        
        private async Task HandleLeaveServer(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理离开服务器消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        
        private async Task HandlePing(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理Ping消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        
        private async Task HandleQueryTime(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理查询时间消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        
        private async Task HandleRideHorse(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理骑马消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        
        private async Task HandleDropGold(MirMsgOrign msg, byte[] payload)
        {
            LogManager.Default.Debug($"处理丢弃金币消息 (ID={_clientKey})");
            await Task.CompletedTask;
        }
        #endregion
        
        #region IDisposable实现
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;
            
            if (disposing)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                
                try
                {
                    if (_socket.Connected)
                    {
                        _socket.Shutdown(SocketShutdown.Both);
                        _socket.Close();
                    }
                }
                catch { }
                
                // 清理玩家资源
                if (_player != null)
                {
                    try
                    {
                        CleanupPlayerResources();
                    }
                    catch { }
                }
            }
            
            _isDisposed = true;
        }
        #endregion
        #endregion
    }
}
