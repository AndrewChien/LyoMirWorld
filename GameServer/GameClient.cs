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
using static Mysqlx.Expect.Open.Types.Condition.Types;

// 类型别名
using Player = GameServer.HumanPlayer;

namespace GameServer
{
    // 物品位置常量（item_db_flag）
    public enum ItemDataFlag
    {
        IDF_GROUND = 0,     // 地面
        IDF_BAG = 1,        // 背包
        IDF_EQUIPMENT = 2,  // 装备
        IDF_NPC = 3,        // NPC
        IDF_BANK = 4,       // 仓库
        IDF_CACHE = 5,      // 缓存
        IDF_PETBANK = 6,    // 宠物仓库（锻造）
        IDF_UPGRADE = 7,    // 升级
    }

    /// <summary>
    /// 游戏客户端连接
    /// </summary>
    public partial class GameClient
    {
        private readonly TcpClient _client;//已验证有效
        private readonly GameServerApp _server;
        private readonly GameWorld _world;
        private readonly NetworkStream _stream;//可发送，已验证有效
        private Player? _player;
        private readonly string _dbServerAddress;
        private readonly int _dbServerPort;

        // 客户端状态管理
        private ClientState _state = ClientState.GSUM_NOTVERIFIED;
        private MirCommon.EnterGameServer _enterInfo = new MirCommon.EnterGameServer();
        private uint _clientKey = 0;
        private uint _nextClientKey = 1;

        // 额外状态字段
        private int _gmLevel = 0;
        private bool _scrollTextMode = false;
        private bool _noticeMode = false;
        private bool _competlyQuit = false;
        private readonly System.Diagnostics.Stopwatch _hlTimer = System.Diagnostics.Stopwatch.StartNew();

        // 首次登录处理类型常量（EP_FIRSTLOGINPROCESS）
        public const int EP_FIRSTLOGINPROCESS = 1;

        // 数据加载状态跟踪
        private bool _bagLoaded = false;
        private bool _equipmentLoaded = false;
        private bool _magicLoaded = false;
        private bool _taskInfoLoaded = false;
        private bool _upgradeItemLoaded = false;
        private bool _petBankLoaded = false;
        private bool _bankLoaded = false;

        public GameClient(TcpClient client, GameServerApp server, GameWorld world, string dbServerAddress, int dbServerPort)
        {
            _client = client;
            _server = server;
            _world = world;
            _dbServerAddress = dbServerAddress;
            _dbServerPort = dbServerPort;
            _stream = client.GetStream();
            _clientKey = _nextClientKey++;
        }

        /// <summary>
        /// 获取客户端ID（getId）
        /// </summary>
        public uint GetId()
        {
            return _clientKey;
        }

        /// <summary>
        /// 单个传世客户端消息处理循环
        /// </summary>
        /// <returns></returns>
        public async Task ProcessAsync()
        {
            byte[] buffer = new byte[8192];
            var networkError = new NetworkError();

            while (_client.Connected)
            {
                try
                {
                    // 检查_stream是否已被释放
                    if (_stream == null || !_stream.CanRead)
                    {
                        LogManager.Default.Warning("NetworkStream已被释放或不可读，退出处理循环");
                        break;
                    }

                    //阻塞读取客户端消息
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        // 连接正常关闭
                        networkError.SetError(NetworkErrorCode.ME_SOCKETCLOSED, "连接正常关闭");
                        LogManager.Default.Info($"客户端连接正常关闭: {networkError.GetFullErrorMessage()}");
                        break;
                    }

                    //客户端接收消息处理
                    var result = await ProcessMessageWithErrorHandling(buffer, bytesRead, networkError);
                    if (!result.IsSuccess)
                    {
                        LogManager.Default.Warning($"处理消息失败: {result.ErrorMessage}");
                        // 根据错误类型决定是否断开连接
                        if (result.ErrorCode == NetworkErrorCode.ME_SOCKETCLOSED ||
                            result.ErrorCode == NetworkErrorCode.ME_CONNECTIONRESET ||
                            result.ErrorCode == NetworkErrorCode.ME_CONNECTIONABORTED)
                        {
                            break;
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    // NetworkStream已被释放，正常退出
                    LogManager.Default.Info("NetworkStream已被释放，退出处理循环");
                    break;
                }
                catch (SocketException ex)
                {
                    networkError.SetErrorFromSocketException(ex);
                    LogManager.Default.Warning($"网络错误: {networkError.GetFullErrorMessage()}");

                    // 根据错误类型决定是否断开连接
                    if (ex.SocketErrorCode == SocketError.ConnectionReset ||
                        ex.SocketErrorCode == SocketError.ConnectionAborted ||
                        ex.SocketErrorCode == SocketError.Shutdown)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    networkError.SetErrorFromException(ex);
                    LogManager.Default.Error($"处理客户端错误: {networkError.GetFullErrorMessage()}");
                    break;
                }
            }

            // 玩家断开，从世界移除
            OnDisconnect();
        }

        /// <summary>
        /// 传世客户端接收消息处理
        /// </summary>
        /// <param name="data"></param>
        /// <param name="length"></param>
        /// <param name="networkError"></param>
        /// <returns></returns>
        private async Task<NetworkResult> ProcessMessageWithErrorHandling(byte[] data, int length, NetworkError networkError)
        {
            /*
              关于状态变更：
            1、初始值：_state→GSUM_NOTVERIFIED（1）；
            2、客户端消息GSUM_NOTVERIFIED状态下触发：OnVerifyString()，其中_state→GSUM_WAITINGDBINFO（2），然后发送DM_GETCHARDBINFO到DB；
            3、数据库服务器DM_GETCHARDBINFO消息触发：向客户端发送第一个对话框，_state→GSUM_WAITINGCONFIRM（3）；
            4、客户端消息GSUM_WAITINGCONFIRM状态下触发：HandleConfirmFirstDialog()，接收第一个对话框，_state→GSUM_VERIFIED（4）；
            5、以后客户端消息均命中GSUM_VERIFIED状态下方法。
            */

            try
            {
                // 专门处理客户端状态切换消息（OnCodedMsg）
                if (_state != ClientState.GSUM_VERIFIED)
                {
                    switch (_state)
                    {
                        case ClientState.GSUM_NOTVERIFIED:
                            {
                                // 直接调用OnVerifyString处理验证字符串
                                string verifyString = Encoding.GetEncoding("GBK").GetString(data, 0, length).TrimEnd('\0');
                                await OnVerifyString(verifyString);
                                return NetworkResult.Success(length);
                            }
                        case ClientState.GSUM_WAITINGCONFIRM:
                            {
                                // 等待确认状态，只处理CM_CONFIRMFIRSTDIALOG消息
                                bool decodeSuccess = GameMessageHandler.DecodeGameMessageOrign(data, length, out var msg, out var payload);
                                if (decodeSuccess)
                                {
                                    LogManager.Default.Info($"等待确认状态，收到消息: 0x{msg.wCmd:X4} (十进制: {msg.wCmd})");
                                    if (msg.wCmd == GameMessageHandler.ClientCommands.CM_CONFIRMFIRSTDIALOG)
                                    {
                                        LogManager.Default.Info($"收到CM_CONFIRMFIRSTDIALOG消息，开始处理确认第一个对话框");
                                        await HandleConfirmFirstDialog(msg, payload);
                                        return NetworkResult.Success(length);
                                    }
                                    else
                                    {
                                        // 如果不是确认消息，忽略
                                        LogManager.Default.Info($"等待确认状态，忽略非确认消息: 0x{msg.wCmd:X4}");
                                        return NetworkResult.Success(length);
                                    }
                                }
                                else
                                {
                                    LogManager.Default.Warning("等待确认状态，解码消息失败，尝试检查是否为简单确认消息");

                                    // 尝试检查是否是简单的确认消息（可能不是编码消息）
                                    // 客户端可能发送的是简单的确认消息，而不是完整的编码消息
                                    if (length >= 2)
                                    {
                                        // 检查是否是CM_CONFIRMFIRSTDIALOG (0x3fa) 的简单形式
                                        // 0x3fa = 1018 (十进制)
                                        // 消息可能是简单的二进制格式
                                        ushort possibleCmd = 0;
                                        if (length >= 2)
                                        {
                                            possibleCmd = BitConverter.ToUInt16(data, 0);
                                            // 检查字节序
                                            if (possibleCmd != GameMessageHandler.ClientCommands.CM_CONFIRMFIRSTDIALOG && length >= 2)
                                            {
                                                // 尝试小端序
                                                possibleCmd = (ushort)((data[1] << 8) | data[0]);
                                            }
                                        }

                                        if (possibleCmd == GameMessageHandler.ClientCommands.CM_CONFIRMFIRSTDIALOG)
                                        {
                                            LogManager.Default.Info($"检测到简单确认消息: CM_CONFIRMFIRSTDIALOG (0x3fa)");
                                            // 创建一个假的MirMsgOrign来处理
                                            var fakeMsg = new MirMsgOrign
                                            {
                                                dwFlag = 0,
                                                wCmd = GameMessageHandler.ClientCommands.CM_CONFIRMFIRSTDIALOG,
                                                wParam = new ushort[3] { 0, 0, 0 },
                                                //data = new byte[4]
                                            };
                                            await HandleConfirmFirstDialog(fakeMsg, Array.Empty<byte>());
                                            return NetworkResult.Success(length);
                                        }
                                    }

                                    // 尝试检查是否是字符串形式的确认消息
                                    string messageStr = Encoding.GetEncoding("GBK").GetString(data, 0, length).TrimEnd('\0');
                                    LogManager.Default.Info($"原始消息内容: '{messageStr}' (长度: {messageStr.Length})");

                                    // 检查是否包含确认相关的字符串
                                    if (messageStr.Contains("confirm", StringComparison.OrdinalIgnoreCase) ||
                                        messageStr.Contains("ok", StringComparison.OrdinalIgnoreCase) ||
                                        messageStr.Contains("确认", StringComparison.OrdinalIgnoreCase))
                                    {
                                        LogManager.Default.Info($"检测到确认字符串消息，视为确认第一个对话框");
                                        var fakeMsg = new MirMsgOrign
                                        {
                                            dwFlag = 0,
                                            wCmd = GameMessageHandler.ClientCommands.CM_CONFIRMFIRSTDIALOG,
                                            wParam = new ushort[3] { 0, 0, 0 },
                                            //data = new byte[4]
                                        };
                                        await HandleConfirmFirstDialog(fakeMsg, Array.Empty<byte>());
                                        return NetworkResult.Success(length);
                                    }

                                    // 尝试手动解码消息（调试用）
                                    LogManager.Default.Info($"尝试手动解码消息: 长度={length}");
                                    string hexData = BitConverter.ToString(data, 0, Math.Min(length, 32));
                                    LogManager.Default.Info($"消息十六进制: {hexData}");

                                    // 检查是否是编码消息（以'#'开头，以'!'结尾）
                                    if (length >= 3 && data[0] == '#' && data[length - 1] == '!')
                                    {
                                        LogManager.Default.Info("检测到编码消息格式（#开头，!结尾），尝试手动解码");

                                        // 手动调用DecodeGameMessageOrign并捕获异常
                                        try
                                        {
                                            bool manualDecodeSuccess = GameMessageHandler.DecodeGameMessageOrign(data, length, out var manualMsg, out var manualPayload);
                                            if (manualDecodeSuccess)
                                            {
                                                LogManager.Default.Info($"手动解码成功: 命令=0x{manualMsg.wCmd:X4}");
                                                if (manualMsg.wCmd == GameMessageHandler.ClientCommands.CM_CONFIRMFIRSTDIALOG)
                                                {
                                                    LogManager.Default.Info($"收到CM_CONFIRMFIRSTDIALOG消息，开始处理确认第一个对话框");
                                                    await HandleConfirmFirstDialog(manualMsg, manualPayload);
                                                    return NetworkResult.Success(length);
                                                }
                                            }
                                            else
                                            {
                                                LogManager.Default.Warning("手动解码仍然失败");

                                                // 如果解码失败，但消息格式正确（#开头，!结尾），尝试直接处理为确认消息
                                                LogManager.Default.Info("解码失败，但消息格式正确，尝试直接处理为确认第一个对话框");
                                                var fakeMsg = new MirMsgOrign
                                                {
                                                    dwFlag = 0,
                                                    wCmd = GameMessageHandler.ClientCommands.CM_CONFIRMFIRSTDIALOG,
                                                    wParam = new ushort[3] { 0, 0, 0 },
                                                    //data = new byte[4]
                                                };
                                                await HandleConfirmFirstDialog(fakeMsg, Array.Empty<byte>());
                                                return NetworkResult.Success(length);
                                            }
                                        }
                                        catch (Exception decodeEx)
                                        {
                                            LogManager.Default.Error($"手动解码异常: {decodeEx.Message}");

                                            // 解码异常，但消息格式正确，尝试直接处理为确认消息
                                            LogManager.Default.Info("解码异常，但消息格式正确，尝试直接处理为确认第一个对话框");
                                            var fakeMsg = new MirMsgOrign
                                            {
                                                dwFlag = 0,
                                                wCmd = GameMessageHandler.ClientCommands.CM_CONFIRMFIRSTDIALOG,
                                                wParam = new ushort[3] { 0, 0, 0 },
                                                //data = new byte[4]
                                            };
                                            await HandleConfirmFirstDialog(fakeMsg, Array.Empty<byte>());
                                            return NetworkResult.Success(length);
                                        }
                                    }

                                    LogManager.Default.Warning("等待确认状态，无法识别消息格式，忽略");
                                    return NetworkResult.Success(length);
                                }
                            }
                        case ClientState.GSUM_WAITINGDBINFO:
                            // 该消息在DB消息中处理并继续变更状态，此处忽略消息
                            LogManager.Default.Debug("等待数据库信息状态，忽略消息");
                            return NetworkResult.Success(length);
                    }
                }

                // 4、已验证状态：处理游戏消息（ProcClientMsg）
                bool decodeSuccess2 = GameMessageHandler.DecodeGameMessageOrign(data, length, out var msg2, out var payload2);
                if (!decodeSuccess2)
                {
                    networkError.SetError(NetworkErrorCode.ME_FAIL, "解码游戏消息失败");
                    return NetworkResult.Failure(NetworkErrorCode.ME_FAIL, "解码游戏消息失败");
                }

                //处理切换为GSUM_VERIFIED状态之后的客户端消息
                await HandleGameMessage(msg2, payload2);
                return NetworkResult.Success(length);
            }
            catch (Exception ex)
            {
                networkError.SetErrorFromException(ex);
                return NetworkResult.FromException(ex);
            }
        }


        /// <summary>
        /// 从BDServer中接收消息的方法专用于OnVerifyString中
        /// 处理数据库消息（OnDBMsg）
        /// </summary>
        private async Task OnDBMsg(MirMsg pMsg, int datasize)
        {
            try
            {
                LogManager.Default.Info($"接收DB消息OnDBMsg: {(DbMsg)pMsg.wCmd}-{pMsg.wParam}-{pMsg.dwFlag}-{pMsg.data.Length}");

                // 验证消息是否属于当前客户端
                if (pMsg.wCmd != (ushort)DbMsg.DM_GETCHARDBINFO && !IsMessageForMe(pMsg)) //DM_GETCHARDBINFO需要处理，不判断归属
                {
                    LogManager.Default.Debug($"消息不属于本客户端，忽略: clientKey={_clientKey}, msgFlag={pMsg.dwFlag}");
                    return;
                }

                switch (pMsg.wCmd)
                {
                    case (ushort)DbMsg.DM_QUERYTASKINFO:
                        {
                            // 修复：检查clientKey匹配
                            // DBServer发送时：wParam1=clientKey低16位, wParam2=clientKey高16位, wParam3=charId低16位
                            uint key = (uint)((pMsg.wParam[1] << 16) | pMsg.wParam[0]);
                            if (key == _clientKey && _player != null)
                            {
                                var taskInfo = BytesToStruct<MirCommon.Database.TaskInfo>(pMsg.data);
                                _player.OnTaskInfo(taskInfo);
                                LogManager.Default.Debug($"处理任务信息消息: 任务ID={taskInfo.dwTaskId}, 状态={taskInfo.dwState}");
                                _taskInfoLoaded = true;
                                CheckAllDataLoaded();
                            }
                            else
                            {
                                LogManager.Default.Warning($"DM_QUERYTASKINFO clientKey不匹配: 期望={_clientKey}, 收到={key}");
                            }
                        }
                        break;
                    case (ushort)DbMsg.DM_QUERYUPGRADEITEM:
                        {
                            // 修复：clientKey由wParam[0]（低16位）和wParam[1]（高16位）组成
                            // wParam[2]是charId的低16位，用于检查是否有数据
                            uint key = (uint)((pMsg.wParam[1] << 16) | pMsg.wParam[0]);
                            if (key == _clientKey && pMsg.wParam[2] > 0)
                            {
                                if (_player != null)
                                {
                                    var dbItem = BytesToStruct<MirCommon.Database.DBITEM>(pMsg.data);
                                    _player.SetUpgradeItem(dbItem.item);
                                    LogManager.Default.Debug($"设置升级物品: 物品ID={dbItem.item.dwMakeIndex}");
                                    _upgradeItemLoaded = true;
                                    LogManager.Default.Debug($"升级物品数据加载完成");
                                    CheckAllDataLoaded();
                                }
                            }
                            else
                            {
                                LogManager.Default.Warning($"DM_QUERYUPGRADEITEM clientKey不匹配或charId为0: 期望={_clientKey}, 收到={key}, charId={pMsg.wParam[2]}");
                            }
                        }
                        break;
                    case (ushort)DbMsg.DM_QUERYMAGIC:
                        {
                            // 修复：移除标志位检查，直接使用wParam[2]的低8位作为技能索引
                            // DBServer发送时：wParam1=clientKey低16位, wParam2=clientKey高16位, wParam3=charId低16位
                            // 但这里需要的是技能索引，所以使用wParam[2]的低8位
                            uint key = (uint)((pMsg.wParam[1] << 16) | pMsg.wParam[0]);
                            if (key == _clientKey)  // 移除(pMsg.wParam[2] & 0x8000) == 0检查
                            {
                                if (_player != null)
                                {
                                    var magicDb = BytesToStruct<MirCommon.Database.MAGICDB>(pMsg.data);
                                    _player.SetMagic(magicDb, (byte)(pMsg.wParam[2] & 0xFF)); // 只使用低8位
                                    _player.SendMagicList();
                                    LogManager.Default.Debug($"设置技能: 技能ID={magicDb.wMagicId}, 等级={magicDb.btCurLevel}");
                                    _magicLoaded = true;
                                    LogManager.Default.Debug($"技能数据加载完成");
                                    CheckAllDataLoaded();
                                }
                            }
                            else
                            {
                                LogManager.Default.Warning($"DM_QUERYMAGIC clientKey不匹配: 期望={_clientKey}, 收到={key}");
                            }
                        }
                        break;
                    case (ushort)DbMsg.DM_QUERYITEMS:
                        {
                            if (pMsg.dwFlag == (ushort)SERVER_ERROR.SE_OK)
                            {
                                // 处理物品数据
                                // 数据格式：DBITEM数组（DBServer发送的是纯DBITEM数组，没有clientKey）
                                int itemCount = pMsg.wParam[2];
                                byte btFlag = (byte)pMsg.wParam[1];

                                // 计算DBITEM结构体大小
                                int dbitemSize = System.Runtime.InteropServices.Marshal.SizeOf<MirCommon.Database.DBITEM>();
                                int expectedSize = itemCount * dbitemSize;
                                
                                // 检查是否有物品数据
                                if (itemCount == 0)
                                {
                                    // 没有物品，直接调用OnDBItem处理空数组
                                    LogManager.Default.Info($"DM_QUERYITEMS: 没有物品数据，itemCount=0, btFlag={btFlag}");
                                    var emptyItems = Array.Empty<MirCommon.Database.DBITEM>();
                                    OnDBItem(emptyItems, 0, btFlag);
                                }
                                else if (datasize >= expectedSize)
                                {
                                    // 转换为DBITEM数组
                                    var dbItems = new MirCommon.Database.DBITEM[itemCount];
                                    for (int i = 0; i < itemCount; i++)
                                    {
                                        dbItems[i] = BytesToStruct<MirCommon.Database.DBITEM>(
                                            pMsg.data, i * dbitemSize, dbitemSize);
                                    }

                                    // 处理物品数据
                                    OnDBItem(dbItems, itemCount, btFlag);
                                }
                                else
                                {
                                    LogManager.Default.Error($"DM_QUERYITEMS数据大小不足: 期望{expectedSize}字节, 实际{datasize}字节");
                                    // 即使数据大小不足，也尝试处理空数组，避免卡住
                                    var emptyItems = Array.Empty<MirCommon.Database.DBITEM>();
                                    OnDBItem(emptyItems, 0, btFlag);
                                }
                            }
                            else
                            {
                                LogManager.Default.Error($"DM_QUERYITEMS返回错误: {pMsg.wParam[0]}");
                                // 即使返回错误，也尝试处理空数组，避免卡住
                                int itemCount = pMsg.wParam[2];
                                byte btFlag = (byte)pMsg.wParam[1];
                                var emptyItems = Array.Empty<MirCommon.Database.DBITEM>();
                                OnDBItem(emptyItems, 0, btFlag);
                            }
                        }
                        break;
                    case (ushort)DbMsg.DM_GETCHARDBINFO:
                        {
                            // DM_GETCHARDBINFO是服务器级别消息，dwFlag应该为0
                            // DBServer返回时，clientKey在CHARDBINFO结构体的dwClientKey字段中
                            // 首先检查数据库错误
                            if (pMsg.dwFlag != (ushort)SERVER_ERROR.SE_OK)
                            {
                                LogManager.Default.Error($"DM_GETCHARDBINFO返回失败: wParam[0]={pMsg.wParam[0]}, 数据长度={pMsg.data?.Length ?? 0}字节");
                                SendMsg2(0, ProtocolCmd.SM_ERRORDIALOG, 0, 0, 0, "读取数据库失败，请联系管理员解决！");
                                //Disconnect(2000);
                                break;
                            }

                            // 检查数据长度是否足够
                            if (pMsg.data == null || pMsg.data.Length < 136) // CHARDBINFO结构大小
                            {
                                LogManager.Default.Error($"DM_GETCHARDBINFO数据长度不足: 期望至少136字节, 实际={pMsg.data?.Length ?? 0}字节");
                                SendMsg2(0, ProtocolCmd.SM_ERRORDIALOG, 0, 0, 0, "角色数据格式错误！");
                                break;
                            }

                            // 解析角色数据库信息（CHARDBINFO）
                            var charDbInfo = BytesToStruct<MirCommon.Database.CHARDBINFO>(pMsg.data);
                            if (charDbInfo.dwClientKey != _clientKey)
                            {
                                LogManager.Default.Warning($"DM_GETCHARDBINFO clientKey不匹配: 期望={_clientKey}, 收到={charDbInfo.dwClientKey}");
                                break;
                            }

                            // 创建玩家对象（CREATEHUMANDESC）
                            var createDesc = new CREATEHUMANDESC
                            {
                                dbinfo = charDbInfo,
                                pClientObj = IntPtr.Zero 
                            };

                            // 从_enterInfo获取账号和角色名
                            string account = _enterInfo.GetAccount();
                            string playerName = _enterInfo.GetName();

                            if (string.IsNullOrEmpty(account))
                            {
                                account = "default_account";
                                LogManager.Default.Warning($"账号为空，使用默认账号: {account}");
                            }

                            if (string.IsNullOrEmpty(playerName))
                            {
                                playerName = "default_player";
                                LogManager.Default.Warning($"角色名为空，使用默认角色名: {playerName}");
                            }

                            // 检查角色是否已登录（FindbyName）
                            if (FindPlayerByName(playerName) != null)
                            {
                                LogManager.Default.Error($"角色已登录1: {playerName}");
                                SendMsg2(0, ProtocolCmd.SM_ERRORDIALOG, 0, 0, 0, "您登陆的角色已经登陆该服务器！");
                                //Disconnect(1000);
                                break;
                            }

                            // 创建新玩家（NewPlayer）
                            // 注意：这里需要根据实际实现调整HumanPlayerMgr.NewPlayer方法的参数
                            // 假设NewPlayer方法接受账号、角色名和角色ID
                            uint charId = charDbInfo.dwDBId;//接收从DB返回的ID值
                            _player = HumanPlayerMgr.Instance.NewPlayer(account, playerName, charId, null);//从数据库返回的角色信息创建角色
                            if (_player == null)
                            {
                                LogManager.Default.Error($"创建玩家对象失败: 账号={account}, 角色名={playerName}, 角色ID={charId}");
                                //Disconnect();
                                break;
                            }

                            // 设置发送消息委托，让HumanPlayer可以通过GameClient发送消息
                            _player.SetSendMessageDelegate((uint dwFlag, ushort wCmd, ushort w1, ushort w2, ushort w3, byte[]? payload) =>
                            {
                                try
                                {
                                    // 使用GameClient的SendMsg方法发送消息
                                    SendMsg2(dwFlag, wCmd, w1, w2, w3, payload);
                                }
                                catch (Exception ex)
                                {
                                    LogManager.Default.Error($"通过委托发送消息失败: {ex.Message}");
                                }
                            });

                            // 初始化玩家（Init）
                            if (!_player.Init(createDesc))
                            {
                                LogManager.Default.Error($"初始化玩家失败: {playerName}");
                                HumanPlayerMgr.Instance.DeletePlayer(_player);
                                _player = null;
                                SendMsg2(0, ProtocolCmd.SM_ERRORDIALOG, 0, 0, 0, "初始化失败！");
                                //Disconnect(1000);
                                break;
                            }

                            // 加载变量（LoadVars）
                            _player.LoadVars();

                            // 发送第一个对话框（SendFirstDlg）
                            SendFirstDlg(GameWorld.Instance.GetNotice());

                            // 设置状态为等待确认
                            _state = ClientState.GSUM_WAITINGCONFIRM;
                            LogManager.Default.Info($"已设置状态为GSUM_WAITINGCONFIRM，等待玩家确认第一个对话框: {playerName}");

                            // 查询其他数据
                            LogManager.Default.Info($"已设置状态为GSUM_WAITINGCONFIRM，等待玩家确认第一个对话框后查询其他数据: 角色ID={charId}");


                            //LogManager.Default.Info($"开始查询其他数据: 角色ID={charId}");
                            //// 异步查询所有数据
                            //_ = Task.Run(async () =>
                            //{
                            //    try
                            //    {
                            //        // 使用新的短连接
                            //        using var dbClient = new MirCommon.Database.DBServerClient(_dbServerAddress, _dbServerPort);
                            //        if (!await dbClient.ConnectAsync())
                            //        {
                            //            LogManager.Default.Error("无法连接到DBServer查询其他数据");
                            //            return;
                            //        }

                            //        ////使用长连接
                            //        //var dbClient = _server.GetDbServerClient();

                            //        uint serverId = 1; // 服务器ID，默认为1

                            //        // 1. 查询背包物品 (IDF_BAG = 1)
                            //        LogManager.Default.Debug($"查询背包物品: 角色ID={charId}");
                            //        await dbClient.SendQueryItem(serverId, _clientKey, charId, (byte)ItemDataFlag.IDF_BAG, 0);

                            //        // 2. 查询装备物品 (IDF_EQUIPMENT = 2)
                            //        LogManager.Default.Debug($"查询装备物品: 角色ID={charId}");
                            //        await dbClient.SendQueryItem(serverId, _clientKey, charId, (byte)ItemDataFlag.IDF_EQUIPMENT, 0);

                            //        // 3. 查询技能数据
                            //        LogManager.Default.Debug($"查询技能数据: 角色ID={charId}");
                            //        await dbClient.SendQueryMagic(serverId, _clientKey, charId);

                            //        // 4. 查询任务信息
                            //        LogManager.Default.Debug($"查询任务信息: 角色ID={charId}");
                            //        await dbClient.QueryTaskInfo(serverId, _clientKey, charId);

                            //        // 5. 查询升级物品 (IDF_UPGRADE = 7)
                            //        LogManager.Default.Debug($"查询升级物品: 角色ID={charId}");
                            //        await dbClient.SendQueryUpgradeItem(serverId, _clientKey, charId);

                            //        // 6. 查询宠物仓库物品 (IDF_PETBANK = 6)
                            //        LogManager.Default.Debug($"查询宠物仓库物品: 角色ID={charId}");
                            //        await dbClient.SendQueryItem(serverId, _clientKey, charId, (byte)ItemDataFlag.IDF_PETBANK, 0);

                            //        // 7. 查询仓库物品 (IDF_BANK = 4)
                            //        LogManager.Default.Debug($"查询仓库物品: 角色ID={charId}");
                            //        await dbClient.SendQueryItem(serverId, _clientKey, charId, (byte)ItemDataFlag.IDF_BANK, 0);

                            //        LogManager.Default.Info($"所有数据查询请求已发送: 角色ID={charId}");
                            //    }
                            //    catch (Exception ex)
                            //    {
                            //        LogManager.Default.Error($"查询其他数据失败: {ex.Message}");
                            //    }
                            //});

                            ////使用长连接
                            //var dbClient = _server.GetDbServerClient();
                            //// 服务器ID，默认为1
                            //uint serverId = 1;

                            //LogManager.Default.Info($"向DBServer发送 DM_QUERYMAGIC 查询技能数据：{serverId}-{_clientKey}-{_player.GetDBId()}");
                            //await dbClient.SendQueryMagic(serverId, _clientKey, _player.GetDBId());

                            //LogManager.Default.Info($"向DBServer发送 DM_QUERYITEMS 查询装备数据：{serverId}-{_clientKey}-{_player.GetDBId()}");
                            //await dbClient.SendQueryItem(serverId, _clientKey, _player.GetDBId(), (byte)ItemDataFlag.IDF_EQUIPMENT, 20);

                            //LogManager.Default.Info($"向DBServer发送 DM_QUERYUPGRADEITEM 查询升级物品数据：{serverId}-{_clientKey}-{_player.GetDBId()}");
                            //await dbClient.SendQueryUpgradeItem(serverId, _clientKey, _player.GetDBId());

                            //// 注意：Inventory类可能没有GetCountLimit方法，使用MaxSlots代替
                            //int bagLimit = 40; // 默认背包大小
                            //if (_player.GetBag() != null)
                            //{
                            //    // 尝试获取背包限制，如果Inventory类有MaxSlots属性则使用它
                            //    // 否则使用默认值
                            //    bagLimit = 40; // 默认值
                            //}
                            //LogManager.Default.Info($"向DBServer发送 DM_QUERYITEMS 查询背包数据：{serverId}-{_clientKey}-{_player.GetDBId()}");
                            //await dbClient.SendQueryItem(serverId, _clientKey, _player.GetDBId(), (byte)ItemDataFlag.IDF_BAG, bagLimit);

                            //LogManager.Default.Info($"向DBServer发送 DM_QUERYITEMS 查询仓库数据：{serverId}-{_clientKey}-{_player.GetDBId()}");
                            //await dbClient.SendQueryItem(serverId, _clientKey, _player.GetDBId(), (byte)ItemDataFlag.IDF_BANK, 100);

                            //LogManager.Default.Info($"向DBServer发送 DM_QUERYITEMS 查询宠物仓库数据：{serverId}-{_clientKey}-{_player.GetDBId()}");
                            //await dbClient.SendQueryItem(serverId, _clientKey, _player.GetDBId(), (byte)ItemDataFlag.IDF_PETBANK, 10);

                            //LogManager.Default.Info($"向DBServer发送 DM_QUERYTASKINFO 查询任务数据：{serverId}-{_clientKey}-{_player.GetDBId()}");
                            //await dbClient.QueryTaskInfo(serverId, _clientKey, _player.GetDBId());

                            //if (_player.IsFirstLogin)
                            //{
                            //    LogManager.Default.Info($"向客户端发送 EP_FIRSTLOGINPROCESS");
                            //    _player.AddProcess(EP_FIRSTLOGINPROCESS, 0, 0, 0, 0, 20, 1, null);
                            //}

                            LogManager.Default.Info($"已处理确认第一个对话框，等待数据库响应: {_player.Name}");
                        }
                        break;
                    case (ushort)DbMsg.DM_CREATEITEM:
                        {
                            // 处理创建物品
                            var createItem = BytesToStruct<MirCommon.CREATEITEM>(pMsg.data);
                            if (createItem.dwClientKey != _clientKey)
                                break;

                            OnCreateItem(createItem.item, createItem.wPos, createItem.btFlag);
                        }
                        break;
                    case (ushort)DbMsg.DM_QUERYCOMMUNITY:
                        {
                            uint dwKey = (uint)(pMsg.wParam[0] | (pMsg.wParam[1] << 16));
                            if (dwKey == _clientKey && _player != null)
                            {
                                // 处理社区信息
                                // 注意：这里需要根据实际实现调整
                                LogManager.Default.Debug($"处理社区信息");
                            }
                        }
                        break;
                    default:
                        // 未知消息，调用服务器处理
                        LogManager.Default.Debug($"未知数据库消息: 0x{pMsg.wCmd:X4}");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理数据库消息失败: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        //private async Task ProcessMessage(byte[] data, int length)
        //{
        //    try
        //    {
        //        int parsedSize = 0;
        //        int msgPtr = 0;
        //        do
        //        {
        //            parsedSize = GameMessageHandler.ParseSingleMessage(data, msgPtr, length - msgPtr,
        //                async (msg, payload) =>
        //                {
        //                    await HandleGameMessage(msg, payload);
        //                });
        //            if (parsedSize > 0)
        //            {
        //                msgPtr += parsedSize;
        //            }
        //        } while (parsedSize > 0 && msgPtr < length);
        //    }
        //    catch (Exception ex)
        //    {
        //        LogManager.Default.Error($"解析消息失败: {ex.Message}");
        //    }

        //    await Task.CompletedTask;
        //}

        /// <summary>
        /// 处理与传世客户端的消息
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="payload"></param>
        /// <returns></returns>
        private async Task HandleGameMessage(MirMsgOrign msg, byte[] payload)
        {
            try
            {
                // 记录接收到的消息（调试用）
                LogManager.Default.Debug($"处理客户端消息: 0x{msg.wCmd:X4} (十进制: {msg.wCmd}), Flag: 0x{msg.dwFlag:X8}");

                // 0xc02, 0xc03, 0x5eb2, 0x9999, 0x6a, 0x3ef, 0x8810, 0x8897, 0x0c00, 0x1000, 0xbc7, 0x43, 0x44, 0x42, 
                // CM_QUERYSTARTPRIVATESHOP, 0x6891, 0x40, 0xaaa, 0x40e, 0xbcd, 0x408, 0x407, 0x51, CM_DELETEGROUPMEMBER, 
                // CM_CHANGEGROUPMODE, 0X3F7, 0x3f5, 0x3f4, 0X3FD, 0x041f, 0x40f, 0x410, 0x411, 0x412, 0x40b, 0x40d, 0x40c, 
                // 0xbd2, 0xbd0, 0xbd1, 0xbcb, 0xbca, 0x8d00, 0x3f0, SM_DROPGOLD, 0x3ee, 0x409, 0x3f6, 0x45, 0x3ff, 0x400, 
                // 0x3f3, 0x3f2, 0x3f1, 0x52, CM_SPELLSKILL, CM_CANCELTRADE, CM_PUTTRADEGOLD, CM_QUERYTRADEEND, CM_PUTTRADEITEM, 
                // CM_QUERYTRADE, CM_TAKEONITEM, CM_TAKEOFFITEM, CM_DROPITEM, CM_PICKUPITEM, CM_STOP, CM_SAY, 0x3d3, CM_TURN, 
                // CM_WALK, CM_GETMEAL, 0xba0, CM_ATTACK, CM_RUN

                bool handled = false;

                switch (msg.wCmd)
                {
                    case GameMessageHandler.ClientCommands.CM_PUTITEMTOPETBAG: // 0xc02: 放入宠物仓库
                        await HandlePutItemToPetBag(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_GETITEMFROMPETBAG: // 0xc03: 从宠物仓库取出
                        await HandleGetItemFromPetBag(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_DELETETASK: // 0x5eb2: 删除任务
                        await HandleDeleteTask(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_GMCOMMAND: // 0x9999: GM命令测试
                        await HandleGMTestCommand(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_COMPLETELYQUIT: // 0x6a: 完全退出
                        await HandleCompletelyQuit(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_CUTBODY: // 0x3ef: 切割尸体
                        await HandleCutBody(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_PUTITEM: // 0x8810: 放入物品
                        await HandlePutItem(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_SHOWPETINFO: // 0x8897: 显示宠物信息
                        await HandleShowPetInfo(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_QUERYTIME: // 0x0c00: 查询时间
                        await HandleQueryTime(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_MARKET: // 0x1000: 市场消息
                        await HandleMarketMessage(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_MINE: // 0xbc7: 挖矿
                        await HandleMine(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_DELETEFRIEND: // 0x43: 删除好友
                        await HandleDeleteFriend(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_REPLYADDFRIEND: // 0x44: 回复添加好友请求
                        await HandleReplyAddFriendRequest(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_ADDFRIEND: // 0x42: 添加好友
                        await HandleAddFriend(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_CREATEGUILD: // 0x6891: 创建行会/输入确认
                        await HandleCreateGuildOrInputConfirm(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_RIDEHORSE: // 0x40: 骑马
                        await HandleRideHorse(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_REPLYADDTOGUILD: // 0xaaa: 回复加入行会请求
                        await HandleReplyAddToGuildRequest(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_INVITETOGUILD: // 0x40e: 邀请加入行会
                        await HandleInviteToGuild(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_ZUOYI: // 0xbcd: 作揖/切换聊天频道
                        await HandleZuoyi(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_TAKEBANKITEM: // 0x408: 从仓库取出
                        await HandleTakeBankItem(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_PUTBANKITEM: // 0x407: 放入仓库
                        await HandlePutBankItem(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_QUERYCOMMUNITY: // 0x51: 查询社区信息
                        await HandleQueryCommunity(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_REMOVEGUILDMEMBER: // 0x40f: 删除行会成员
                        await HandleDeleteGuildMember(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_EDITGUILDNOTICE: // 0x410: 编辑行会公告
                        await HandleEditGuildNotice(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_EDITGUILDTITLE: // 0x411: 编辑行会封号
                        await HandleEditGuildTitle(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_QUERYGUILDEXP: // 0x412: 查询行会经验
                        await HandleQueryGuildExp(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_QUERYGUILDINFO: // 0x40b: 请求行会信息
                        await HandleQueryGuildInfo(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_QUERYGUILDMEMBERLIST: // 0x40d: 请求行会成员列表
                        await HandleQueryGuildMemberList(msg, payload);
                        handled = true;
                        break;
                    // case 0x40c: // 请求行会首页（已废弃）
                    //     // 已废弃，不处理
                    //     handled = true;
                    //     break;
                    case GameMessageHandler.ClientCommands.CM_SPECIALHIT_POJISHIELD: // 0xbd2: 破击/破盾
                        await HandleSpecialHit(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_SPECIALHIT_HALFMOON: // 0xbd0: 半月
                        await HandleSpecialHit(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_SPECIALHIT_FIRE: // 0xbd1: 烈火
                        await HandleSpecialHit(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_SPECIALHIT_ASSASSINATE: // 0xbcb: 刺杀
                        await HandleSpecialHit(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_SPECIALHIT_KILL: // 0xbca: 攻杀
                        await HandleSpecialHit(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_QUERYHISTORYADDR: // 0x8d00: 查询历史地址
                        await HandleQueryHistoryAddress(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_SETMAGICKEY: // 0x3f0: 设置技能快捷键
                        await HandleSetMagicKey(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_USEITEM: // 0x3ee: 使用物品
                        await HandleUseItem(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_QUERYMINIMAP: // 0x409: 查询小地图
                        await HandleQueryMinimap(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_BUYITEM: // 0x3f6: 购买物品
                        await HandleBuyItem(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_SETBAGITEMPOS: // 0x45: 设置背包物品位置
                        await HandleSetBagItemPos(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_REPAIRITEM: // 0x3ff: 修理物品
                        await HandleRepairItem(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_QUERYREPAIRPRICE: // 0x400: 查询修理价格
                        await HandleQueryRepairPrice(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_SELECTLINK: // 0x3f3: 选择链接
                        await HandleSelectLink(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_NPCTALK: // 0x3f2: NPC对话/查看个人商店
                        await HandleNPCTalkOrViewPrivateShop(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_RESTARTGAME: // 0x3f1: 重启游戏
                        await HandleRestartGame(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_VIEWEQUIPMENT: // 0x52: 查看装备
                        await HandleViewEquipment(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_PING: // 0x3d3: Ping响应
                        await HandlePingResponse(msg, payload);
                        handled = true;
                        break;
                    case GameMessageHandler.ClientCommands.CM_TRAINHORSE: // 0xba0: 训练马匹
                        await HandleTrainHorse(msg, payload);
                        handled = true;
                        break;
                    default:
                        // 如果未在上述特定消息中处理，则使用原有的switch处理
                        handled = false;
                        break;
                }

                if (!handled)
                {
                    // 使用原有的switch处理其他消息
                    switch (msg.wCmd)
                    {
                        //case GameMessageHandler.ClientCommands.CM_ENTERGAME:
                        //    await HandleEnterGame(payload);
                        //    break;
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
                        case GameMessageHandler.ClientCommands.CM_GETMEAL:
                            await HandleGetMealMessage(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_STOP:
                            await HandleStopMessage(msg, payload);
                            break;
                        //case GameMessageHandler.ClientCommands.CM_CONFIRMFIRSTDIALOG: //不会命中，之前被拦截了，另行处理
                        //    await HandleConfirmFirstDialog(msg, payload);
                        //    break;
                        case GameMessageHandler.ClientCommands.CM_SELECTLINK:
                            await HandleSelectLink(msg, payload);
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
                        case GameMessageHandler.ClientCommands.CM_CHANGEGROUPMODE:
                            await HandleChangeGroupMode(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_QUERYADDGROUPMEMBER:
                            await HandleQueryAddGroupMember(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_DELETEGROUPMEMBER:
                            await HandleDeleteGroupMember(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_QUERYSTARTPRIVATESHOP:
                            await HandleQueryStartPrivateShop(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_ZUOYI:
                            await HandleZuoyi(msg, payload);
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
                        case GameMessageHandler.ClientCommands.CM_USEITEM:
                            await HandleUseItem(msg, payload);
                            break;
                        case GameMessageHandler.ClientCommands.CM_DROPGOLD:
                            await HandleDropGold(msg, payload);
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
                        case GameMessageHandler.ClientCommands.SM_UNKNOWN_COMMAND:
                            await HandleUnknown45(msg, payload);
                            break;
                        default:
                            Console.WriteLine($"未处理的消息命令: 0x{msg.wCmd:X4}");
                            // 记录未知消息但不中断连接
                            LogManager.Default.Info($"未知消息: 0x{msg.wCmd:X4} (十进制: {msg.wCmd})");
                            // 发送未知命令响应
                            SendUnknownCommandResponse(msg.wCmd);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理游戏消息失败: {ex.Message}");
                // 发送错误响应
                SendErrorMessage($"处理命令 0x{msg.wCmd:X4} 失败: {ex.Message}");
            }
        }

        private void SendEnterGameOk()
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
            _stream.Write(packet, 0, packet.Length);
            _stream.Flush();
        }

        // 发送消息的辅助方法（SendActionResult）
        private void SendActionResult(int x, int y, bool success)
        {
            if (_player == null) return;

            // if(bSuccess) sprintf(szMsg, "#+G/%d/%d!", x, y);
            //          else sprintf(szMsg, "#+FL/%d/%d!", x, y);
            string message = success ? $"+G/{x}/{y}!" : $"+FL/{x}/{y}!";

            // 编码消息并发送
            byte[] encodedMessage = GameMessageHandler.EncodeGameMessageOrign(
                GameMessageHandler.CreateMessage2(_player.ObjectId, 0),
                Encoding.GetEncoding("GBK").GetBytes(message));

            if (encodedMessage.Length > 0)
            {
                _stream.Write(encodedMessage, 0, encodedMessage.Length);
                _stream.Flush();
            }
        }

        private void SendStopMessage()
        {
            if (_player == null) return;

            GameMessageHandler.SendSimpleMessage2(_stream, _player.ObjectId,
                GameMessageHandler.ServerCommands.SM_STOP, 0, 0, 0);
        }

        /// <summary>
        /// 发送第一个对话框（SendFirstDlg），证实客户端读取正常
        /// </summary>
        private void SendFirstDlg(string message)
        {
            try
            {
                // 发送第一个对话框消息
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(message);
                GameMessageHandler.SendSimpleMessage2(_stream, 0, GameMessageHandler.ServerCommands.SM_FIRSTDIALOG, 0, 0, 0, payload);
                LogManager.Default.Info($"已发送第一个对话框: {message}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送第一个对话框失败: {ex.Message}");
            }

            //try
            //{
            //    byte[] payload = Encoding.GetEncoding("GBK").GetBytes(message);
            //    GameMessageHandler.SendSimpleMessage(_stream, 0, GameMessageHandler.ServerCommands.SM_FIRSTDIALOG, 0, 0, 0, payload);
            //    LogManager.Default.Info($"已发送第一个对话框: {message}");
            //}
            //catch (Exception ex)
            //{
            //    LogManager.Default.Error($"发送第一个对话框失败: {ex.Message}");
            //}
        }

        private void SendEquipItemResult(bool success, int pos, uint itemId)
        {
            ushort cmd = success ? GameMessageHandler.ServerCommands.SM_TAKEON_OK :
                GameMessageHandler.ServerCommands.SM_TAKEON_FAIL;
            GameMessageHandler.SendSimpleMessage2(_stream, itemId, cmd, 0, 0, 0);
        }

        private void SendUnEquipItemResult(bool success, int pos, uint itemId)
        {
            ushort cmd = success ? GameMessageHandler.ServerCommands.SM_TAKEOFF_OK :
                GameMessageHandler.ServerCommands.SM_TAKEOFF_FAIL;
            GameMessageHandler.SendSimpleMessage2(_stream, itemId, cmd, 0, 0, 0);
        }

        private void SendDropItemResult(bool success, uint itemId)
        {
            ushort cmd = success ? GameMessageHandler.ServerCommands.SM_DROPITEMOK : 
                GameMessageHandler.ServerCommands.SM_DROPITEMFAIL;
            GameMessageHandler.SendSimpleMessage2(_stream, itemId, cmd, 0, 0, 0);
        }

        private void SendPickupItemResult(bool success)
        {
            // 发送拾取结果
            if (success)
            {
                // 发送重量变化消息
                GameMessageHandler.SendSimpleMessage2(_stream, 0,
                    GameMessageHandler.ServerCommands.SM_WEIGHTCHANGED, 0, 0, 0);
            }
        }

        private void SendChatMessage(Player targetPlayer, string speaker, string message)
        {
            // 构建聊天消息
            byte[] payload = System.Text.Encoding.GetEncoding("GBK").GetBytes($"{speaker}:{message}");
            GameMessageHandler.SendSimpleMessage2(_stream, targetPlayer.ObjectId,
                GameMessageHandler.ServerCommands.SM_CHAT, 0, 0, 0, payload);
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
        //    // 对于EnterGameServer和LoginEnter结构体，字段可能是大端序

        //    // 创建一个新的字节数组，用于存储转换后的数据
        //    byte[] convertedBytes = new byte[size];
        //    Array.Copy(bytes, 0, convertedBytes, 0, size);

        //    // 假设结构体中的uint字段是大端序，需要转换为小端序
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
        //    else if (typeof(T) == typeof(MirCommon.LoginEnter))
        //    {
        //        // LoginEnter结构体字段布局（根据MirDefine.cs）：
        //        // byte szAccount[12] (偏移量0)
        //        // uint nLid (偏移量12)
        //        // uint nSid (偏移量16)
        //        // uint dwEnterTime (偏移量20)
        //        // uint nListId (偏移量24)

        //        // 转换uint字段（4字节）从大端序到小端序
        //        // 注意：szAccount[12]是字节数组，不需要转换
        //        // uint字段的偏移量：12, 16, 20, 24
        //        int[] uintOffsets = { 12, 16, 20, 24 };
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
        //        return System.Runtime.InteropServices.Marshal.PtrToStructure<T>(ptr)!;
        //    }
        //    finally
        //    {
        //        System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
        //    }
        //}

        /// <summary>
        /// 断开连接处理（OnDisconnect）
        /// </summary>
        public void OnDisconnect()
        {
            try
            {
                // 玩家断开，从世界移除
                if (_player != null)
                {
                    LogManager.Default.Info($"玩家断开连接: {_player.Name}");

                    // 执行断开连接逻辑（OnDisconnect）
                    // 1. 从地图移除
                    _world.RemovePlayer(_player.ObjectId);
                    var map = _world.GetMap(_player.MapId);
                    map?.RemovePlayer(_player.ObjectId);

                    // 2. 保存数据到数据库
                    SavePlayerDataToDB();

                    // 3. 清理资源
                    CleanupPlayerResources();

                    _player = null;
                }

                // 关闭网络连接
                Disconnect();
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"断开连接处理失败: {ex.Message}");
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
                // 这里实现保存玩家数据到数据库的逻辑
                // SavePlayerData
                LogManager.Default.Info($"保存玩家数据: {_player.Name}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"保存玩家数据失败: {ex.Message}");
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
                // 这里实现清理玩家资源的逻辑
                // CleanupPlayerResources
                LogManager.Default.Info($"清理玩家资源: {_player.Name}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"清理玩家资源失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送未知命令响应
        /// </summary>
        private void SendUnknownCommandResponse(ushort command)
        {
            try
            {
                // 发送未知命令响应
                GameMessageHandler.SendSimpleMessage2(_stream, 0,
                    GameMessageHandler.ServerCommands.SM_UNKNOWN_COMMAND, 0, 0, 0);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送未知命令响应失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送错误消息
        /// </summary>
        private void SendErrorMessage(string message)
        {
            try
            {
                // 发送错误消息
                byte[] payload = System.Text.Encoding.GetEncoding("GBK").GetBytes(message);
                GameMessageHandler.SendSimpleMessage2(_stream, 0,
                    GameMessageHandler.ServerCommands.SM_ERROR, 0, 0, 0, payload);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送错误消息失败: {ex.Message}");
            }
        }

        #region 处理客户端验证

        /// <summary>
        /// 处理验证字符串（OnVerifyString）
        /// </summary>
        private async Task OnVerifyString(string verifyString)
        {
            try
            {
                LogManager.Default.Info($"处理验证字符串: {verifyString}");

                // 检查是否是编码字符串（以"#"开头，以"!"结尾）
                if (verifyString.StartsWith("#") && verifyString.EndsWith("!"))
                {
                    // 解码字符串
                    string decodedString = DecodeVerifyString(verifyString);
                    if (string.IsNullOrEmpty(decodedString))
                    {
                        LogManager.Default.Warning($"解码验证字符串失败: {verifyString}");
                        //Disconnect();
                        return;
                    }

                    LogManager.Default.Info($"解码后的验证字符串: {decodedString}");

                    // 处理解码后的字符串
                    await ProcessDecodedString(decodedString);
                    return;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理验证字符串失败: {ex.Message}");
                //Disconnect();
            }
        }

        /// <summary>
        /// 解码验证字符串（ParseMessage）
        /// </summary>
        private string DecodeVerifyString(string encodedString)
        {
            try
            {
                // 编码字符串格式：以"#"开头，以"!"结尾
                // 例如："#5ll<kpqVuprMzpk^HMFIWFM?nooWvqpXnro?nprVuqrNyqq?vr=!"
                // ParseMessage函数逻辑：
                // 1. 检查字符串是否以"#"开头，以"!"结尾
                // 2. 如果"#"后面的第一个字符是数字（0-9），则跳过这个数字
                // 3. 然后调用_UnGameCode解码

                if (string.IsNullOrEmpty(encodedString) || encodedString.Length < 3)
                {
                    LogManager.Default.Warning($"编码字符串太短: {encodedString}");
                    return string.Empty;
                }

                // 去掉"#"和"!"，获取编码部分
                string encodedPart = encodedString.Substring(1, encodedString.Length - 2);

                // 检查"#"后面的第一个字符是否是数字
                // if( *pStart >= '0' && *pStart <= '9' )pStart++;
                if (encodedPart.Length > 0 && encodedPart[0] >= '0' && encodedPart[0] <= '9')
                {
                    // 跳过开头的数字字符
                    encodedPart = encodedPart.Substring(1);
                    LogManager.Default.Debug($"跳过开头的数字字符，剩余编码部分: {encodedPart}");
                }

                // 将字符串转换为字节数组
                byte[] encodedBytes = Encoding.GetEncoding("GBK").GetBytes(encodedPart);

                // 解码字节数组
                byte[] decodedBytes = new byte[encodedBytes.Length * 2]; // 足够大的缓冲区
                int decodedSize = MirCommon.GameCodecOrign.UnGameCodeOrign(encodedBytes, decodedBytes);

                if (decodedSize <= 0)
                {
                    LogManager.Default.Warning($"解码失败: 解码大小为{decodedSize}");
                    return string.Empty;
                }

                // 将解码后的字节数组转换为字符串
                string decodedString = Encoding.GetEncoding("GBK").GetString(decodedBytes, 0, decodedSize).TrimEnd('\0');

                LogManager.Default.Debug($"解码成功: 原始='{encodedString}', 解码后='{decodedString}'");
                return decodedString;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解码验证字符串失败: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 处理解码后的字符串
        /// </summary>
        private async Task ProcessDecodedString(string decodedString)
        {
            try
            {
                LogManager.Default.Debug($"处理解码后的字符串: '{decodedString}'");

                // 处理新老版本客户端的不同格式
                // 新版本（1.9）格式：***loginid/角色名字/selectcharid/20041118/0 (5个参数)
                // 老版本（1.8）格式：?selcharid/20050720/0 (3个参数) 或 selcharid/20050720/0

                string p = decodedString;
                bool isNewVersion = false;

                if (p.Length >= 3 && p[0] == '*' && p[1] == '*' && p[2] == '*')
                {
                    p = p.Substring(3);
                    isNewVersion = true;
                    LogManager.Default.Debug($"检测到以***开头");
                }
                //else
                //{
                //    // 尝试自动检测格式
                //    // 如果包含3个参数，可能是老版本
                //    // 如果包含5个参数，可能是新版本
                //    string[] testParams = p.Split('/');
                //    if (testParams.Length == 3)
                //    {
                //        isOldVersion = true;
                //        LogManager.Default.Debug($"自动检测为老版本（1.8）客户端格式，3个参数");
                //    }
                //    else if (testParams.Length == 5)
                //    {
                //        isNewVersion = true;
                //        LogManager.Default.Debug($"自动检测为新版本（1.9）客户端格式，5个参数");
                //    }
                //    else
                //    {
                //        LogManager.Default.Warning($"无法识别的验证字符串格式: '{decodedString}'");
                //        //Disconnect();
                //        return;
                //    }
                //}

                // 解析参数
                string[] paramsArray = p.Split('/');

                uint loginId = 0;
                string charName = "";
                uint selCharId = 0;
                string version = "";

                if (isNewVersion && paramsArray.Length == 5)
                {
                    // 新版本格式：loginid/角色名字/selectcharid/20041118/0
                    loginId = uint.Parse(paramsArray[0]);
                    charName = paramsArray[1];
                    selCharId = uint.Parse(paramsArray[2]);
                    version = paramsArray[3];
                    // paramsArray[4] 是 "0" (未知)

                    LogManager.Default.Info($"验证字符串解析成功: loginId={loginId}, charName={charName}, selCharId={selCharId}, version={version}");
                }
                //else if (isOldVersion && paramsArray.Length == 3)
                //{
                //    // 老版本格式：selcharid/20050720/0
                //    // 注意：老版本没有loginId和charName，只有selCharId
                //    selCharId = uint.Parse(paramsArray[0]);
                //    version = paramsArray[1];
                //    // paramsArray[2] 是 "0" (未知)

                //    LogManager.Default.Info($"验证字符串解析成功: selCharId={selCharId}, version={version}");

                //    loginId = selCharId; // 临时使用selCharId作为loginId
                //    charName = $"Player_{selCharId}"; // 临时角色名
                //}
                else
                {
                    LogManager.Default.Warning($"验证字符串参数数量错误: {paramsArray.Length}, 期望: 新版本5个参数或老版本3个参数");
                    //Disconnect();
                    return;
                }

                //设置状态为GSUM_WAITINGDBINFO__：验证成功后立即设置
                _state = ClientState.GSUM_WAITINGDBINFO;
                LogManager.Default.Info($"已设置状态为GSUM_WAITINGDBINFO，等待数据库返回角色信息");

                // 检查ServerCenter是否提供了进入信息
                // 从GameServerApp的字典中获取进入信息
                string account;
                string serverName = _server.GetServerName();

                // 从服务器获取进入信息
                LogManager.Default.Info($"开始从服务器获取进入信息，登录ID={loginId}");
                var enterInfo = _server.GetEnterInfo(loginId);
                if (enterInfo != null)
                {
                    // 使用从ServerCenter获取的账号
                    account = enterInfo.Value.GetAccount();
                    LogManager.Default.Info($"使用ServerCenter提供的账号: '{account}' (登录ID={loginId})");

                    // 保存进入信息到本地字段
                    _enterInfo = enterInfo.Value;
                    LogManager.Default.Info($"已设置_enterInfo: 账号='{_enterInfo.GetAccount()}', 角色名='{_enterInfo.GetName()}', 登录ID={_enterInfo.nLoginId}, 选择角色ID={_enterInfo.nSelCharId}");

                    // 验证角色名是否匹配
                    if (isNewVersion && _enterInfo.GetName() != charName)
                    {
                        LogManager.Default.Error($"角色名不匹配: ServerCenter提供='{_enterInfo.GetName()}', 客户端发送='{charName}'");
                        SendMsg2(0, ProtocolCmd.SM_ERRORDIALOG, 0, 0, 0, "您登陆的角色已经登陆该服务器！");
                        Disconnect(1000);
                        return;
                    }

                    //// 对于老版本，更新charName为ServerCenter提供的角色名
                    //if (isOldVersion)
                    //{
                    //    charName = _enterInfo.GetName();
                    //    LogManager.Default.Info($"老版本客户端，使用ServerCenter提供的角色名: '{charName}'");
                    //}

                    // 从服务器字典中移除进入信息（已经使用）
                    _server.RemoveEnterInfo(loginId);
                    LogManager.Default.Info($"已从服务器字典移除进入信息，登录ID={loginId}");
                }
                else
                {
                    // ServerCenter没有提供账号信息
                    if (isNewVersion)
                    {
                        // 新版本：使用备选方案
                        account = $"char_{loginId}";
                        LogManager.Default.Warning($"ServerCenter未提供账号，使用备选账号: {account}");
                    }
                    else
                    {
                        // 老版本：使用selCharId作为账号
                        account = $"old_{selCharId}";
                        LogManager.Default.Warning($"老版本客户端，ServerCenter未提供账号，使用selCharId作为账号: {account}");
                    }

                    // 设置进入信息
                    _enterInfo.nLoginId = loginId;
                    _enterInfo.nSelCharId = selCharId;
                    _enterInfo.SetName(charName);
                    _enterInfo.dwSelectCharServerId = 1; // 默认选择角色服务器ID
                    LogManager.Default.Info($"已设置进入信息: 登录ID={loginId}, 选择角色ID={selCharId}, 角色名={charName}");
                }

                // 查询数据库信息（SendQueryDbInfo）
                LogManager.Default.Info($"开始查询数据库信息1: 角色={charName}, 账号={account}, 服务器={serverName}");

                // 使用GameServerApp的DBServerClient
                var dbClient = _server.GetDbServerClient();
                if (dbClient == null)
                {
                    LogManager.Default.Error("无法获取DBServerClient");
                    SendMsg2(0, ProtocolCmd.SM_ERRORDIALOG, 0, 0, 0, "数据库错误！");
                    //Disconnect(1000);
                    return;
                }

                // 使用现有的GetCharDBInfoBytesAsync方法来查询数据库信息，传递clientKey以便DBServer能正确返回消息
                // 注意：这里charId参数传递0，因为此时还不知道角色ID，DBServer会从数据库查询并返回角色ID
                LogManager.Default.Info($"开始查询数据库信息: 角色={charName}, 账号={account}");

                byte[]? charData = await dbClient.GetCharDBInfoBytesAsync(account, serverName, charName, _clientKey, 0);//ChaDBid从数据库回传，这里给0
                if (charData != null)
                {
                    //// 查询数据库后设置状态为GSUM_WAITINGDBINFO
                    //_state = ClientState.GSUM_WAITINGDBINFO;
                    //LogManager.Default.Info($"已设置状态为GSUM_WAITINGDBINFO，等待数据库返回角色信息");

                    //// 直接处理数据库响应，而不是等待OnDBMsg
                    //// 创建一个临时的MirMsg来模拟OnDBMsg的调用
                    //var tempMsg = new MirMsg
                    //{
                    //    dwFlag = 0,
                    //    wCmd = (ushort)DbMsg.DM_GETCHARDBINFO,
                    //    wParam = new ushort[3] { (ushort)SERVER_ERROR.SE_OK, 0, 0 },
                    //    data = charData
                    //};
                    //// 调用OnDBMsg处理角色数据
                    //await OnDBMsg(tempMsg, charData.Length);
                }
                else
                {
                    LogManager.Default.Error($"查询数据库信息失败: {charName}");
                    SendMsg2(0, ProtocolCmd.SM_ERRORDIALOG, 0, 0, 0, "查询数据库信息失败！");
                    //Disconnect(1000);
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理解码后的字符串失败: {ex.Message}");
                //Disconnect();
            }

            await Task.CompletedTask;
        }

        #endregion

        /// <summary>
        /// 发送消息（SendMsg）
        /// </summary>
        private void SendMsg(uint dwFlag, ushort wCmd, ushort wParam1, ushort wParam2, ushort wParam3, string? message = null)
        {
            try
            {
                byte[]? payload = null;
                if (message != null)
                {
                    payload = Encoding.GetEncoding("GBK").GetBytes(message);
                }

                GameMessageHandler.SendSimpleMessage2(_stream, dwFlag, wCmd, wParam1, wParam2, wParam3, payload);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送消息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送消息（SendMsg）
        /// </summary>
        private void SendMsg2(uint dwFlag, ushort wCmd, ushort wParam1, ushort wParam2, ushort wParam3, string? message = null)
        {
            try
            {
                byte[]? payload = null;
                if (message != null)
                {
                    payload = Encoding.GetEncoding("GBK").GetBytes(message);
                }

                GameMessageHandler.SendSimpleMessage2(_stream, dwFlag, wCmd, wParam1, wParam2, wParam3, payload);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送消息失败: {ex.Message}");
            }
        }

        ///// <summary>
        ///// 发送消息（字节数组payload版本）
        ///// </summary>
        //private void SendMsg(uint dwFlag, ushort wCmd, ushort wParam1, ushort wParam2, ushort wParam3, byte[]? payload)
        //{
        //    try
        //    {
        //        GameMessageHandler.SendSimpleMessage(_stream, dwFlag, wCmd, wParam1, wParam2, wParam3, payload);
        //    }
        //    catch (Exception ex)
        //    {
        //        LogManager.Default.Error($"发送消息失败: {ex.Message}");
        //    }
        //}

        /// <summary>
        /// 发送消息（字节数组payload版本）
        /// </summary>
        private void SendMsg2(uint dwFlag, ushort wCmd, ushort wParam1, ushort wParam2, ushort wParam3, byte[]? payload)
        {
            try
            {
                GameMessageHandler.SendSimpleMessage2(_stream, dwFlag, wCmd, wParam1, wParam2, wParam3, payload);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送消息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 断开连接（带延迟）
        /// </summary>
        private void Disconnect(int delayMs = 0)
        {
            if (delayMs > 0)
            {
                Task.Delay(delayMs).ContinueWith(_ => Disconnect());
                return;
            }

            try
            {
                _stream?.Close();
                _client?.Close();
            }
            catch { }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
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
        /// 根据角色名查找玩家（FindbyName）
        /// </summary>
        private Player? FindPlayerByName(string charName)
        {
            try
            {
                // 使用HumanPlayerMgr查找玩家（FindbyName）
                return HumanPlayerMgr.Instance.FindByName(charName);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"查找玩家失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 处理数据库物品数据（OnDBItem）
        /// </summary>
        private void OnDBItem(MirCommon.Database.DBITEM[] pItemArray, int nCount, byte btFlag)
        {
            try
            {
                LogManager.Default.Info($"处理数据库物品数据: 数量={nCount}, 标志={btFlag}");

                if (_player == null)
                {
                    LogManager.Default.Error("玩家对象为空，无法处理物品数据");
                    return;
                }

                // 根据标志处理不同类型的物品
                switch (btFlag)
                {
                    case (byte)ItemDataFlag.IDF_BANK:
                        {
                            LogManager.Default.Info($"处理仓库物品: {nCount}个");
                            for (int i = 0; i < nCount; i++)
                            {
                                // 这里需要根据实际实现调整
                                // _player.AddBankItem(pItemArray[i].item, false);
                                LogManager.Default.Debug($"处理仓库物品: {i + 1}/{nCount}");
                            }
                            // 设置仓库数据加载完成
                            _bankLoaded = true;
                            LogManager.Default.Debug($"仓库数据加载完成");
                            CheckAllDataLoaded();
                        }
                        break;
                    case (byte)ItemDataFlag.IDF_BAG:
                        {
                            LogManager.Default.Info($"处理背包物品: {nCount}个");
                            _player.SetSystemFlag((int)MirCommon.SystemFlag.SF_BAGLOADED, true);
                            for (int i = 0; i < nCount; i++)
                            {
                                // 这里需要根据实际实现调整
                                // var item = pItemArray[i].item;
                                // _player.AddBagItem(pItemArray[i].item, true);
                                // pItemArray[i].item = item;
                                LogManager.Default.Debug($"处理背包物品: {i + 1}/{nCount}");
                            }
                            // 设置背包数据加载完成
                            _bagLoaded = true;
                            LogManager.Default.Debug($"背包数据加载完成");
                            // 发送背包信息和位置信息
                            SendBagItems(pItemArray, nCount);
                            CheckAllDataLoaded();
                        }
                        break;
                    case (byte)ItemDataFlag.IDF_EQUIPMENT:
                        {
                            LogManager.Default.Info($"处理装备物品: {nCount}个");
                            _player.SetSystemFlag((int)MirCommon.SystemFlag.SF_EQUIPMENTLOADED, true);
                            for (int i = 0; i < nCount; i++)
                            {
                                // 这里需要根据实际实现调整
                                // var item = pItemArray[i].item;
                                // _player.EquipItem(pItemArray[i].pos, pItemArray[i].item, true);
                                // pItemArray[i].item = item;
                                LogManager.Default.Debug($"处理装备物品: {i + 1}/{nCount}");
                            }
                            // 设置装备数据加载完成
                            _equipmentLoaded = true;
                            LogManager.Default.Debug($"装备数据加载完成");
                            // 发送装备信息
                            SendEquipments();
                            CheckAllDataLoaded();
                        }
                        break;
                    case (byte)ItemDataFlag.IDF_PETBANK:
                        {
                            LogManager.Default.Info($"处理宠物仓库物品: {nCount}个");
                            // 从DBITEM数组中提取Item数组
                            var items = new MirCommon.Item[nCount];
                            for (int i = 0; i < nCount; i++)
                            {
                                items[i] = pItemArray[i].item;
                            }
                            // 调用HumanPlayer的OnPetBank方法
                            _player.OnPetBank(items, nCount);
                            LogManager.Default.Debug($"处理宠物仓库物品: {nCount}个");
                            // 设置宠物仓库数据加载完成
                            _petBankLoaded = true;
                            LogManager.Default.Debug($"宠物仓库数据加载完成");
                            CheckAllDataLoaded();
                        }
                        break;
                    default:
                        LogManager.Default.Warning($"未知的物品标志: {btFlag}");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理数据库物品数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理创建物品（OnCreateItem）
        /// </summary>
        private void OnCreateItem(MirCommon.Item item, int pos, byte btFlag)
        {
            try
            {
                LogManager.Default.Info($"处理创建物品: 位置={pos}, 标志={btFlag}");

                if (_player == null)
                {
                    LogManager.Default.Error("玩家对象为空，无法创建物品");
                    return;
                }

                if (btFlag == (byte)ItemDataFlag.IDF_BAG)
                {
                    // 这里需要根据实际实现调整
                    // _player.AddBagItem(item);
                    LogManager.Default.Debug($"创建背包物品: 位置={pos}");
                }
                else
                {
                    // 这里需要根据实际实现调整
                    // _player.DropItem(item);
                    LogManager.Default.Debug($"创建掉落物品: 位置={pos}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理创建物品失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送背包物品（SendBagItems）
        /// </summary>
        private void SendBagItems(MirCommon.Database.DBITEM[] pItems, int count)
        {
            try
            {
                if (_player == null) return;

                LogManager.Default.Info($"发送背包物品: {count}个");

                // 限制数量
                if (count > 100) count = 100;

                // 创建ItemClient数组
                var items = new MirCommon.ItemClient[count];
                for (int i = 0; i < count; i++)
                {
                    // 将DBITEM.item转换为ItemClient
                    var dbItem = pItems[i];
                    items[i] = new MirCommon.ItemClient
                    {
                        baseitem = dbItem.item.baseitem,
                        dwMakeIndex = dbItem.item.dwMakeIndex,
                        wCurDura = dbItem.item.wCurDura,
                        wMaxDura = dbItem.item.wMaxDura
                    };
                    LogManager.Default.Debug($"转换物品 {i + 1}/{count}: ID={dbItem.item.dwMakeIndex}, 名称={dbItem.item.baseitem.szName}");
                }

                // 发送背包物品消息（使用SM_BAGINFO命令）
                // 注意：SM_BAGINFO命令需要发送ITEMCLIENT数组
                byte[] itemsData = StructArrayToBytes(items);
                LogManager.Default.Info($"发送背包物品数据: ItemClient数组大小={itemsData.Length}字节, 物品数量={count}, 每个ItemClient大小={System.Runtime.InteropServices.Marshal.SizeOf<MirCommon.ItemClient>()}字节");
                SendMsg2(_player.ObjectId, GameMessageHandler.ServerCommands.SM_BAGINFO, 0, 0, (ushort)count, itemsData);

                // 发送重量变化
                SendWeightChangedMessage();

                // 发送物品位置信息
                var itempos = new MirCommon.Database.BAGITEMPOS[count];
                var bagItemPos = new MirCommon.BAGITEMPOS[count];
                for (int i = 0; i < count; i++)
                {
                    itempos[i] = new MirCommon.Database.BAGITEMPOS
                    {
                        dwItemIndex = pItems[i].item.dwMakeIndex,
                        btFlag = pItems[i].btFlag,
                        wPos = pItems[i].wPos
                    };
                    
                    // 转换为HumanPlayer需要的BAGITEMPOS格式
                    bagItemPos[i] = new MirCommon.BAGITEMPOS
                    {
                        ItemId = pItems[i].item.dwMakeIndex,
                        wPos = pItems[i].wPos
                    };
                    
                    LogManager.Default.Debug($"设置物品位置 {i + 1}/{count}: ID={itempos[i].dwItemIndex}, 位置={itempos[i].wPos}, 标志={itempos[i].btFlag}");
                }

                _player.SetBagItemPos(bagItemPos, count);
                byte[] itemposData = StructArrayToBytes(itempos);
                SendMsg2(0, GameMessageHandler.ServerCommands.SM_SETITEMPOSITION, 0, 0, (ushort)count, itemposData);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送背包物品失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送装备信息（SendEquipments）
        /// </summary>
        private void SendEquipments()
        {
            try
            {
                if (_player == null) return;

                LogManager.Default.Info("发送装备信息");

                uint dwFeather = _player.GetFeather();
                var equipments = new MirCommon.EQUIPMENT[20];
                int count = _player.GetEquipments(equipments);

                SendMsg2(0, GameMessageHandler.ServerCommands.SM_EQUIPMENTS, 0, 0, 0, null, equipments);

                _player.SendFeatureChanged();
                _player.UpdateProp();
                _player.UpdateSubProp();
                _player.SendStatusChanged();

                // 发送进入游戏成功消息
                SendEnterGameOk();
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送装备信息失败: {ex.Message}");
            }
        }

        ///// <summary>
        ///// 发送消息（带数据）
        ///// </summary>
        //private void SendMsg(uint dwFlag, ushort wCmd, ushort wParam1, ushort wParam2, ushort wParam3, byte[]? payload, object? data = null)
        //{
        //    try
        //    {
        //        if (data != null)
        //        {
        //            // 将对象序列化为字节数组
        //            int size = System.Runtime.InteropServices.Marshal.SizeOf(data);
        //            byte[] dataBytes = new byte[size];
        //            IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
        //            try
        //            {
        //                System.Runtime.InteropServices.Marshal.StructureToPtr(data, ptr, false);
        //                System.Runtime.InteropServices.Marshal.Copy(ptr, dataBytes, 0, size);
        //            }
        //            finally
        //            {
        //                System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
        //            }

        //            GameMessageHandler.SendSimpleMessage(_stream, dwFlag, wCmd, wParam1, wParam2, wParam3, dataBytes);
        //        }
        //        else if (payload != null)
        //        {
        //            GameMessageHandler.SendSimpleMessage(_stream, dwFlag, wCmd, wParam1, wParam2, wParam3, payload);
        //        }
        //        else
        //        {
        //            GameMessageHandler.SendSimpleMessage(_stream, dwFlag, wCmd, wParam1, wParam2, wParam3);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        LogManager.Default.Error($"发送消息失败: {ex.Message}");
        //    }
        //}

        /// <summary>
        /// 发送消息（带数据）
        /// </summary>
        private void SendMsg2(uint dwFlag, ushort wCmd, ushort wParam1, ushort wParam2, ushort wParam3, byte[]? payload, object? data = null)
        {
            try
            {
                if (data != null)
                {
                    // 将对象序列化为字节数组
                    int size = System.Runtime.InteropServices.Marshal.SizeOf(data);
                    byte[] dataBytes = new byte[size];
                    IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
                    try
                    {
                        System.Runtime.InteropServices.Marshal.StructureToPtr(data, ptr, false);
                        System.Runtime.InteropServices.Marshal.Copy(ptr, dataBytes, 0, size);
                    }
                    finally
                    {
                        System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
                    }

                    GameMessageHandler.SendSimpleMessage2(_stream, dwFlag, wCmd, wParam1, wParam2, wParam3, dataBytes);
                }
                else if (payload != null)
                {
                    GameMessageHandler.SendSimpleMessage2(_stream, dwFlag, wCmd, wParam1, wParam2, wParam3, payload);
                }
                else
                {
                    GameMessageHandler.SendSimpleMessage2(_stream, dwFlag, wCmd, wParam1, wParam2, wParam3);
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送消息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从字节数组的指定偏移量转换为结构体
        /// </summary>
        private T BytesToStruct<T>(byte[] bytes, int offset, int size) where T : struct
        {
            if (offset + size > bytes.Length)
                throw new ArgumentException($"字节数组长度不足: {bytes.Length} < {offset + size}");

            byte[] slice = new byte[size];
            Array.Copy(bytes, offset, slice, 0, size);
            return BytesToStruct<T>(slice);
        }

        /// <summary>
        /// 发送重量变化消息
        /// </summary>
        private void SendWeightChangedMessage()
        {
            try
            {
                if (_player == null) return;

                // 发送重量变化消息
                GameMessageHandler.SendSimpleMessage2(_stream, 0,
                    GameMessageHandler.ServerCommands.SM_WEIGHTCHANGED, 0, 0, 0);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送重量变化消息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 将结构体数组转换为字节数组
        /// </summary>
        private byte[] StructArrayToBytes<T>(T[] array) where T : struct
        {
            if (array == null || array.Length == 0)
                return Array.Empty<byte>();

            int elementSize = System.Runtime.InteropServices.Marshal.SizeOf<T>();
            byte[] result = new byte[elementSize * array.Length];
            
            for (int i = 0; i < array.Length; i++)
            {
                IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(elementSize);
                try
                {
                    System.Runtime.InteropServices.Marshal.StructureToPtr(array[i], ptr, false);
                    System.Runtime.InteropServices.Marshal.Copy(ptr, result, i * elementSize, elementSize);
                }
                finally
                {
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
                }
            }
            
            return result;
        }

        /// <summary>
        /// 检查所有数据是否已加载（CheckAllDataLoaded）
        /// </summary>
        private void CheckAllDataLoaded()
        {
            try
            {
                if (_player == null) return;

                LogManager.Default.Debug($"检查数据加载状态: 背包={_bagLoaded}, 装备={_equipmentLoaded}, 技能={_magicLoaded}, 任务={_taskInfoLoaded}, 升级物品={_upgradeItemLoaded}, 宠物仓库={_petBankLoaded}, 仓库={_bankLoaded}");

                // 检查所有必需的数据是否已加载
                // 1. 背包数据 (_bagLoaded)
                // 2. 装备数据 (_equipmentLoaded)
                // 3. 技能数据 (_magicLoaded)
                // 4. 任务数据 (_taskInfoLoaded)
                // 5. 升级物品数据 (_upgradeItemLoaded)
                // 6. 宠物仓库数据 (_petBankLoaded)
                // 7. 仓库数据 (_bankLoaded)

                bool allDataLoaded = _bagLoaded && _equipmentLoaded && _magicLoaded &&
                                    _taskInfoLoaded && _upgradeItemLoaded &&
                                    _petBankLoaded && _bankLoaded;

                if (allDataLoaded)
                {
                    LogManager.Default.Info($"所有数据已加载完成: {_player.Name}");

                    // 所有数据加载完成，执行后续操作

                    // 1. 设置玩家状态为已加载完成
                    _player.SetSystemFlag((int)MirCommon.SystemFlag.SF_ALLDATALOADED, true);

                    // 2. 发送进入游戏成功消息（如果还没有发送）
                    if (!_player.GetSystemFlag((int)MirCommon.SystemFlag.SF_ENTERGAMESENT))
                    {
                        SendEnterGameOk();
                        _player.SetSystemFlag((int)MirCommon.SystemFlag.SF_ENTERGAMESENT, true);
                    }

                    // 3. 更新玩家属性
                    _player.UpdateProp();
                    _player.UpdateSubProp();

                    // 4. 发送状态变化
                    _player.SendStatusChanged();

                    // 5. 发送特征变化
                    _player.SendFeatureChanged();

                    // 6. 发送重量变化
                    SendWeightChangedMessage();

                    // 7. 如果是第一次登录，执行首次登录处理
                    if (_player.IsFirstLogin)
                    {
                        LogManager.Default.Info($"玩家第一次登录: {_player.Name}");
                        // 这里可以添加首次登录的特殊处理
                        // 例如：发送欢迎消息、赠送新手物品等
                    }

                    LogManager.Default.Info($"玩家数据加载完成: {_player.Name}");
                }
                else
                {
                    LogManager.Default.Debug($"数据尚未完全加载，等待更多数据...");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"检查数据加载状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理DBServer消息（从GameServerApp分发）
        /// </summary>
        public void HandleDbServerMessage(MirCommon.MirMsg msg)
        {
            try
            {
                LogManager.Default.Info($"GameClient收到转发的DBServer消息: Cmd=0x{msg.wCmd:X4}, Flag=0x{msg.dwFlag:X8}, w1={msg.wParam[0]}, w2={msg.wParam[1]}, w3={msg.wParam[2]}, 数据长度={msg.data?.Length ?? 0}字节");

                // 调用现有的OnDBMsg方法处理消息
                _ = OnDBMsg(msg, msg.data?.Length ?? 0);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"GameClient处理DBServer消息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取客户端Key
        /// </summary>
        public uint GetClientKey()
        {
            return _clientKey;
        }

        /// <summary>
        /// 检查消息是否属于当前客户端（IsMessageForMe）
        /// </summary>
        private bool IsMessageForMe(MirMsg pMsg)
        {
            try
            {
                // DBServer返回的消息使用dwFlag字段标识目标客户端
                // 如果dwFlag == 0，消息是发给服务器的
                // 如果dwFlag != 0，消息是发给特定客户端的，dwFlag就是clientKey
                
                // 对于客户端级别消息，dwFlag应该等于_clientKey
                if (pMsg.dwFlag != 0)
                {
                    return pMsg.dwFlag == _clientKey;
                }
                
                // 对于服务器级别消息（dwFlag == 0），需要进一步检查
                // 某些服务器级别消息可能包含clientKey在wParam中
                // 例如：DM_GETCHARDBINFO消息，clientKey在wParam1和wParam2中
                
                // 检查clientKey
                // DBServer发送时：wParam1=clientKey低16位, wParam2=clientKey高16位
                uint key = (uint)((pMsg.wParam[1] << 16) | pMsg.wParam[0]);
                return key == _clientKey;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"检查消息归属失败: {ex.Message}");
                return false;
            }
        }
    }
}
