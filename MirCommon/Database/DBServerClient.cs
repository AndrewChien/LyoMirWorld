using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MirCommon.Network;

namespace MirCommon.Database
{
    /// <summary>
    /// DBServer客户端，用于向DBServer发送数据库操作请求
    /// </summary>
    public class DBServerClient : IDisposable
    {
        private readonly string _serverAddress;
        private readonly int _serverPort;
        private TcpClient? _client;
        private NetworkStream? _stream;
        private bool _connected = false;
        private CancellationTokenSource? _listeningCts;
        private Task? _listeningTask;

        // 添加事件用于通知DBServer消息
        public event Action<MirMsg>? OnDbMessageReceived;
        public event Action<string>? OnLogMessage;

        public DBServerClient(string address = "127.0.0.1", int port = 8000)
        {
            _serverAddress = address;
            _serverPort = port;
        }

        /// <summary>
        /// 连接到DBServer
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(_serverAddress, _serverPort);
                _stream = _client.GetStream();
                _connected = true;
                
                Log($"已连接到DBServer: {_serverAddress}:{_serverPort}");
                return true;
            }
            catch (Exception ex)
            {
                _connected = false;
                Log($"连接到DBServer失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 启动监听DBServer消息
        /// </summary>
        public void StartListening()
        {
            if (!_connected || _stream == null)
            {
                Log("无法启动监听：未连接到DBServer");
                return;
            }

            if (_listeningTask != null && !_listeningTask.IsCompleted)
            {
                Log("监听任务已在运行");
                return;
            }

            _listeningCts = new CancellationTokenSource();
            _listeningTask = Task.Run(async () => await ListenToDbServerAsync(_listeningCts.Token));
            Log("已启动DBServer消息监听");
        }

        /// <summary>
        /// 停止监听DBServer消息
        /// </summary>
        public void StopListening()
        {
            _listeningCts?.Cancel();
            _listeningTask = null;
            Log("已停止DBServer消息监听");
        }

        /// <summary>
        /// 监听DBServer消息
        /// </summary>
        private async Task ListenToDbServerAsync(CancellationToken cancellationToken)
        {
            if (_stream == null) return;

            byte[] buffer = new byte[8192];
            int reconnectAttempts = 0;
            const int maxReconnectAttempts = 3;

            while (!cancellationToken.IsCancellationRequested && _connected)
            {
                try
                {
                    if (!_client?.Connected ?? true)
                    {
                        Log("DBServer连接已断开，尝试重新连接...");
                        reconnectAttempts++;
                        if (reconnectAttempts > maxReconnectAttempts)
                        {
                            Log($"达到最大重连次数({maxReconnectAttempts})，停止监听");
                            break;
                        }

                        if (await ConnectAsync())
                        {
                            reconnectAttempts = 0;
                            continue;
                        }
                        else
                        {
                            await Task.Delay(5000, cancellationToken);
                            continue;
                        }
                    }

                    // 异步读取消息
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead > 0)
                    {
                        Log($"收到DBServer消息: {bytesRead}字节");
                        await ProcessReceivedData(buffer, bytesRead);
                    }
                    else if (bytesRead == 0)
                    {
                        // 连接正常关闭
                        Log("DBServer连接已关闭");
                        _connected = false;
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    // 任务被取消，正常退出
                    Log("DBServer监听任务被取消");
                    break;
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Log($"读取DBServer消息失败: {ex.Message}");
                        await Task.Delay(1000, cancellationToken);
                    }
                }
            }

            Log("DBServer监听任务已停止");
        }

        /// <summary>
        /// 处理接收到的数据
        /// </summary>
        private async Task ProcessReceivedData(byte[] data, int length)
        {
            try
            {
                // 查找消息边界 '#' 和 '!'
                int startIndex = -1;
                int endIndex = -1;
                
                for (int i = 0; i < length; i++)
                {
                    if (data[i] == '#')
                    {
                        startIndex = i + 1; // '#'后面的位置
                    }
                    else if (data[i] == '!')
                    {
                        endIndex = i; // '!'的位置
                        break;
                    }
                }
                
                if (startIndex == -1 || endIndex == -1 || startIndex >= endIndex)
                {
                    Log($"无效的DBServer消息格式，未找到#和!边界");
                    return;
                }
                
                // 提取'#'和'!'之间的编码数据
                int encodedLength = endIndex - startIndex;
                byte[] encodedData = new byte[encodedLength];
                Array.Copy(data, startIndex, encodedData, 0, encodedLength);
                
                // 解码游戏消息
                byte[] decoded = new byte[encodedLength * 3 / 4 + 4];
                int decodedSize = GameCodec.UnGameCode(encodedData, decoded);
                
                if (decodedSize < 12)
                {
                    Log($"解码后的DBServer消息太小: {decodedSize}字节");
                    return;
                }

                // 解析响应
                var reader = new PacketReader(decoded);
                uint dwFlag = reader.ReadUInt32();
                ushort wCmd = reader.ReadUInt16();
                ushort w1 = reader.ReadUInt16();
                ushort w2 = reader.ReadUInt16();
                ushort w3 = reader.ReadUInt16();
                byte[] msgData = reader.ReadBytes(decodedSize - 12);

                var msg = new MirMsg
                {
                    dwFlag = dwFlag,
                    wCmd = wCmd,
                    wParam = new ushort[3] { w1, w2, w3 },
                    data = msgData
                };

                Log($"解析DBServer消息: Cmd=0x{wCmd:X4}({(DbMsg)wCmd}), Flag=0x{dwFlag:X8}, w1={w1}, w2={w2}, w3={w3}, 数据长度={msgData.Length}字节");

                // 如果dwFlag == 0，消息是发给服务器的
                // 如果dwFlag != 0，消息是发给特定客户端的，应该由底层框架自动路由
                OnDbMessageReceived?.Invoke(msg);
            }
            catch (Exception ex)
            {
                Log($"处理DBServer消息失败: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 发送数据库消息并等待响应（保持向后兼容）
        /// </summary>
        private async Task<DbResponse?> SendDbMessageAsync(DbMsg msgType, byte[] payload, ushort wParam1 = 0, ushort wParam2 = 0, ushort wParam3 = 0, uint dwFlag = 0)
        {
            if (_stream == null) return null;

            try
            {
                // 使用GameCodec.EncodeMsg编码消息（包含'#'和'!'）
                // dwFlag用于标识消息目标（0=服务器，非0=客户端）
                byte[] encoded = new byte[8192];
                int encodedSize = GameCodec.EncodeMsg(encoded, dwFlag, (ushort)msgType, wParam1, wParam2, wParam3, payload, payload.Length);

                // 发送消息
                await _stream.WriteAsync(encoded, 0, encodedSize);
                await _stream.FlushAsync();

                // 接收响应
                byte[] buffer = new byte[8192];
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead < 3) return null; // 至少需要'#', 编码数据, '!'

                // 查找消息边界 '#' 和 '!'
                int startIndex = -1;
                int endIndex = -1;
                
                for (int i = 0; i < bytesRead; i++)
                {
                    if (buffer[i] == '#')
                    {
                        startIndex = i + 1; // '#'后面的位置
                    }
                    else if (buffer[i] == '!')
                    {
                        endIndex = i; // '!'的位置
                        break;
                    }
                }
                
                if (startIndex == -1 || endIndex == -1 || startIndex >= endIndex)
                {
                    return null;
                }
                
                // 提取'#'和'!'之间的编码数据
                int encodedLength = endIndex - startIndex;
                byte[] encodedData = new byte[encodedLength];
                Array.Copy(buffer, startIndex, encodedData, 0, encodedLength);
                
                // 解码游戏消息
                byte[] decoded = new byte[encodedLength * 3 / 4 + 4]; // 解码后最大可能的大小
                int decodedSize = GameCodec.UnGameCode(encodedData, decoded);
                
                if (decodedSize < 12) return null;

                // 解析响应
                var reader = new PacketReader(decoded);
                uint responseDwFlag = reader.ReadUInt32();
                ushort wCmd = reader.ReadUInt16();
                ushort responseW1 = reader.ReadUInt16();
                ushort responseW2 = reader.ReadUInt16();
                ushort responseW3 = reader.ReadUInt16();
                byte[] data = reader.ReadBytes(decodedSize - 12);

                Log($"收到DBServer响应: Cmd=0x{wCmd:X4}({(DbMsg)wCmd}), Flag=0x{responseDwFlag:X8}, w1={responseW1}, w2={responseW2}, w3={responseW3}, 数据长度={data.Length}字节");

                return new DbResponse
                {
                    dwFlag = responseDwFlag,
                    wCmd = wCmd,
                    data = data
                };
            }
            catch (Exception ex)
            {
                Log($"SendDbMessageAsync失败: {ex.Message}");
                return null;
            }
        }

        #region GameServer调用方法

        /// <summary>
        /// 发送查询技能消息
        /// </summary>
        public async Task SendQueryMagic(uint serverId, uint clientKey, uint charId)
        {
            if (!_connected) return;

            try
            {
                // 构建查询技能消息
                byte[] payload = new byte[12];
                BitConverter.GetBytes(serverId).CopyTo(payload, 0);
                BitConverter.GetBytes(clientKey).CopyTo(payload, 4);
                BitConverter.GetBytes(charId).CopyTo(payload, 8);
                
                // 将clientKey和charId分解为wParam参数
                // wParam1 = clientKey的低16位
                // wParam2 = clientKey的高16位
                // wParam3 = charId的低16位
                ushort wParam1 = (ushort)(clientKey & 0xFFFF);
                ushort wParam2 = (ushort)((clientKey >> 16) & 0xFFFF);
                ushort wParam3 = (ushort)(charId & 0xFFFF);
                
                // 客户端级别消息使用clientKey作为dwFlag
                await SendDbMessageAsync(DbMsg.DM_QUERYMAGIC, payload, wParam1, wParam2, wParam3, clientKey);
                Log($"已发送查询技能 DM_QUERYMAGIC 消息: serverId={serverId}, clientKey={clientKey}, charId={charId}, wParam1={wParam1}, wParam2={wParam2}, wParam3={wParam3}, dwFlag={clientKey}");
            }
            catch (Exception ex)
            {
                Log($"发送查询技能消息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送查询物品消息
        /// </summary>
        public async Task SendQueryItem(uint serverId, uint clientKey, uint charId, byte flag, int count)
        {
            if (!_connected) return;

            try
            {
                // 构建查询物品消息
                byte[] payload = new byte[13];
                BitConverter.GetBytes(serverId).CopyTo(payload, 0);
                BitConverter.GetBytes(clientKey).CopyTo(payload, 4);
                BitConverter.GetBytes(charId).CopyTo(payload, 8);
                payload[12] = flag;
                
                // 将clientKey和charId分解为wParam参数
                // wParam1 = clientKey的低16位
                // wParam2 = clientKey的高16位
                // wParam3 = charId的低16位
                ushort wParam1 = (ushort)(clientKey & 0xFFFF);
                ushort wParam2 = (ushort)((clientKey >> 16) & 0xFFFF);
                ushort wParam3 = (ushort)(charId & 0xFFFF);
                
                // 客户端级别消息使用clientKey作为dwFlag
                await SendDbMessageAsync(DbMsg.DM_QUERYITEMS, payload, wParam1, wParam2, wParam3, clientKey);
                Log($"已发送查询物品 DM_QUERYITEMS 消息: serverId={serverId}, clientKey={clientKey}, charId={charId}, flag={flag}, wParam1={wParam1}, wParam2={wParam2}, wParam3={wParam3}, dwFlag={clientKey}");
            }
            catch (Exception ex)
            {
                Log($"发送查询物品消息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送查询升级物品消息
        /// </summary>
        public async Task SendQueryUpgradeItem(uint serverId, uint clientKey, uint charId)
        {
            if (!_connected) return;

            try
            {
                // 构建查询升级物品消息
                byte[] payload = new byte[12];
                BitConverter.GetBytes(serverId).CopyTo(payload, 0);
                BitConverter.GetBytes(clientKey).CopyTo(payload, 4);
                BitConverter.GetBytes(charId).CopyTo(payload, 8);
                
                // 将clientKey和charId分解为wParam参数
                // wParam1 = clientKey的低16位
                // wParam2 = clientKey的高16位
                // wParam3 = charId的低16位
                ushort wParam1 = (ushort)(clientKey & 0xFFFF);
                ushort wParam2 = (ushort)((clientKey >> 16) & 0xFFFF);
                ushort wParam3 = (ushort)(charId & 0xFFFF);
                
                // 客户端级别消息使用clientKey作为dwFlag
                await SendDbMessageAsync(DbMsg.DM_QUERYUPGRADEITEM, payload, wParam1, wParam2, wParam3, clientKey);
                Log($"已发送查询升级物品 DM_QUERYUPGRADEITEM 消息: serverId={serverId}, clientKey={clientKey}, charId={charId}, wParam1={wParam1}, wParam2={wParam2}, wParam3={wParam3}, dwFlag={clientKey}");
            }
            catch (Exception ex)
            {
                Log($"发送查询升级物品消息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 查询任务信息
        /// </summary>
        public async Task QueryTaskInfo(uint serverId, uint clientKey, uint charId)
        {
            if (!_connected) return;

            try
            {
                // 构建查询任务信息消息
                byte[] payload = new byte[12];
                BitConverter.GetBytes(serverId).CopyTo(payload, 0);
                BitConverter.GetBytes(clientKey).CopyTo(payload, 4);
                BitConverter.GetBytes(charId).CopyTo(payload, 8);
                
                // 将clientKey和charId分解为wParam参数
                // wParam1 = clientKey的低16位
                // wParam2 = clientKey的高16位
                // wParam3 = charId的低16位
                ushort wParam1 = (ushort)(clientKey & 0xFFFF);
                ushort wParam2 = (ushort)((clientKey >> 16) & 0xFFFF);
                ushort wParam3 = (ushort)(charId & 0xFFFF);
                
                // 客户端级别消息使用clientKey作为dwFlag
                await SendDbMessageAsync(DbMsg.DM_QUERYTASKINFO, payload, wParam1, wParam2, wParam3, clientKey);
                Log($"SendQueryTaskInfo 已发送查询任务信息 DM_QUERYTASKINFO 消息: serverId={serverId}, clientKey={clientKey}, charId={charId}, wParam1={wParam1}, wParam2={wParam2}, wParam3={wParam3}, dwFlag={clientKey}");
            }
            catch (Exception ex)
            {
                Log($"发送查询任务信息消息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取角色数据库信息（GameServer使用）
        /// </summary>
        public async Task<byte[]?> GetCharDBInfoBytesAsync(string account, string serverName, string charName, uint clientKey = 0, uint charId = 0)
        {
            if (!_connected) return null;

            try
            {
                // data格式：account/serverName/charName
                string data = $"{account}/{serverName}/{charName}";
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(data);

                // 直接发送消息并等待响应
                ushort wParam1 = (ushort)(clientKey & 0xFFFF);
                ushort wParam2 = (ushort)((clientKey >> 16) & 0xFFFF);
                ushort wParam3 = (ushort)(charId & 0xFFFF);

                // 使用SendDbMessageAsync方法，它已经包含了等待响应的逻辑
                var response = await SendDbMessageAsync(DbMsg.DM_GETCHARDBINFO, payload, wParam1, wParam2, wParam3, 0);

                if (response != null && response.dwFlag == (uint)SERVER_ERROR.SE_OK && response.data != null)
                {
                    Log($"收到 DM_GETCHARDBINFO 响应: 数据长度={response.data.Length}字节");
                    return response.data;
                }
                else
                {
                    Log($"DM_GETCHARDBINFO 响应失败: dwFlag={response?.dwFlag ?? 0}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log($"GetCharDBInfoBytesAsync失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取角色数据库信息，等待返回（GameServer使用）
        /// </summary>
        public async Task<byte[]?> GetCharDBInfoBytesAsync2(string account, string serverName, string charName, uint clientKey = 0, uint charId = 0)
        {
            if (!_connected) return null;

            try
            {
                // data格式：account/serverName/charName
                string data = $"{account}/{serverName}/{charName}";//lyo：注意，此处使用字符串传data值
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(data);

                // 使用TaskCompletionSource来等待响应
                var tcs = new TaskCompletionSource<DbResponse?>();
                Action<MirMsg>? handler = null;

                handler = (msg) =>
                {
                    if (msg.wCmd == (ushort)DbMsg.DM_GETCHARDBINFO)
                    {
                        // DM_GETCHARDBINFO是服务器级别消息，dwFlag应该为0
                        // clientKey在CHARDBINFO结构体的dwClientKey字段中
                        Log($"立即收到 DM_GETCHARDBINFO 响应: 数据长度={msg.data?.Length ?? 0}字节");
                        if (msg.data != null && msg.data.Length >= 136) // CHARDBINFO结构大小
                        {
                            try
                            {
                                // 解析CHARDBINFO结构体获取clientKey
                                var charDbInfo = BytesToStruct<CHARDBINFO>(msg.data);
                                uint receivedClientKey = charDbInfo.dwClientKey;

                                if (receivedClientKey == clientKey)
                                {
                                    // 移除事件处理器
                                    OnDbMessageReceived -= handler;

                                    // 设置结果
                                    var response = new DbResponse
                                    {
                                        dwFlag = msg.dwFlag,
                                        wCmd = msg.wCmd,
                                        data = msg.data
                                    };
                                    tcs.SetResult(response);
                                }
                            }
                            catch (Exception ex)
                            {
                                Log($"解析CHARDBINFO结构体失败2: {ex.Message}");
                            }
                        }
                    }
                };

                // 注册事件处理器
                OnDbMessageReceived += handler;

                // 发送消息，设置clientKey和charId到wParam中
                // wParam1 = clientKey的低16位
                // wParam2 = clientKey的高16位
                // wParam3 = charId的低16位
                ushort wParam1 = (ushort)(clientKey & 0xFFFF);
                ushort wParam2 = (ushort)((clientKey >> 16) & 0xFFFF);
                ushort wParam3 = (ushort)(charId & 0xFFFF);

                // DM_GETCHARDBINFO是服务器级别消息，dwFlag = 0
                byte[] encoded = new byte[8192];
                int encodedSize = GameCodec.EncodeMsg(encoded, 0, (ushort)DbMsg.DM_GETCHARDBINFO, wParam1, wParam2, wParam3, payload, payload.Length);

                if (_stream != null)
                {
                    await _stream.WriteAsync(encoded, 0, encodedSize);
                    await _stream.FlushAsync();
                    Log($"已发送 DM_GETCHARDBINFO 消息: 账号={account}, 服务器={serverName}, 角色名={charName}, clientKey={clientKey}, charId={charId}");
                }
                else
                {
                    Log("无法发送消息: _stream为null");
                    return null;
                }

                // 等待响应，设置超时
                var timeoutTask = Task.Delay(10000); // 10秒超时
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask == tcs.Task)
                {
                    var response = await tcs.Task;
                    if (response != null && response.dwFlag == (uint)SERVER_ERROR.SE_OK && response.data != null)
                    {
                        Log($"收到 DM_GETCHARDBINFO 响应2: 数据长度={response.data.Length}字节");
                        return response.data;
                    }
                    else
                    {
                        Log($"DM_GETCHARDBINFO 响应失败2: dwFlag={response?.dwFlag ?? 0}");
                        return null;
                    }
                }
                else
                {
                    Log("等待 DM_GETCHARDBINFO 响应超时2");
                    // 移除事件处理器
                    OnDbMessageReceived -= handler;
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log($"GetCharDBInfoBytesAsync失败2: {ex.Message}");
                return null;
            }
        }


        #endregion

        #region LoginServer调用方法

        /// <summary>
        /// 检查账号密码（LoginServer使用）
        /// </summary>
        public async Task<bool> CheckAccountAsync(string account, string password)
        {
            if (!_connected) return false;

            try
            {
                // 格式：account/password
                string data = $"{account}/{password}";//lyo：注意，此处使用字符串传data值
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(data);
                var response = await SendDbMessageAsync(DbMsg.DM_CHECKACCOUNT, payload);
                return response != null && response.dwFlag == (uint)SERVER_ERROR.SE_OK;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 检查账号是否存在（LoginServer使用）
        /// </summary>
        public async Task<bool> CheckAccountExistAsync(string account)
        {
            if (!_connected) return false;

            try
            {
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(account);
                var response = await SendDbMessageAsync(DbMsg.DM_CHECKACCOUNTEXIST, payload);
                return response != null && response.dwFlag == (uint)SERVER_ERROR.SE_OK;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 创建账号（LoginServer使用）
        /// </summary>
        public async Task<bool> CreateAccountAsync(string account, string password, string name, string birthday,
                                                  string q1, string a1, string q2, string a2, string email,
                                                  string phoneNumber, string mobilePhoneNumber, string idCard)
        {
            if (!_connected) return false;

            try
            {
                byte[] payload = BuildRegisterAccountStruct(account, password, name, birthday, q1, a1, q2, a2, email,
                                                           phoneNumber, mobilePhoneNumber, idCard);
                var response = await SendDbMessageAsync(DbMsg.DM_CREATEACCOUNT, payload);
                return response != null && response.dwFlag == (uint)SERVER_ERROR.SE_OK;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 修改密码（LoginServer使用）
        /// </summary>
        public async Task<bool> ChangePasswordAsync(string account, string oldPassword, string newPassword)
        {
            if (!_connected) return false;

            try
            {
                // 格式：account/oldPassword/newPassword
                string data = $"{account}/{oldPassword}/{newPassword}";//lyo：注意，此处使用字符串传data值
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(data);
                var response = await SendDbMessageAsync(DbMsg.DM_CHANGEPASSWORD, payload);
                return response != null && response.dwFlag == (uint)SERVER_ERROR.SE_OK;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 检查角色名是否存在（LoginServer使用）
        /// </summary>
        public async Task<bool> CheckCharacterNameExistsAsync(string serverName, string charName)
        {
            if (!_connected) return false;

            try
            {
                // 格式：serverName/charName
                string data = $"{serverName}/{charName}";//lyo：注意，此处使用字符串传data值
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(data);
                var response = await SendDbMessageAsync(DbMsg.DM_CHECKCHARACTERNAMEEXISTS, payload);
                return response != null && response.dwFlag == (uint)SERVER_ERROR.SE_OK;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// RegisterAccount结构（331字节）
        /// </summary>
        private byte[] BuildRegisterAccountStruct(string account, string password, string name, string birthday,
                                                  string q1, string a1, string q2, string a2, string email,
                                                  string phoneNumber, string mobilePhoneNumber, string idCard)
        {
            byte[] buffer = new byte[331]; 

            int offset = 0;

            // 账号 (最大10字节)
            buffer[offset++] = (byte)Math.Min(account.Length, 10);
            Encoding.GetEncoding("GBK").GetBytes(account, 0, Math.Min(account.Length, 10), buffer, offset);
            offset += 10;

            // 密码 (最大10字节)
            buffer[offset++] = (byte)Math.Min(password.Length, 10);
            Encoding.GetEncoding("GBK").GetBytes(password, 0, Math.Min(password.Length, 10), buffer, offset);
            offset += 10;

            // 姓名 (最大20字节)
            buffer[offset++] = (byte)Math.Min(name.Length, 20);
            Encoding.GetEncoding("GBK").GetBytes(name, 0, Math.Min(name.Length, 20), buffer, offset);
            offset += 20;

            // 身份证 (最大19字节)
            buffer[offset++] = (byte)Math.Min(idCard.Length, 19);
            Encoding.GetEncoding("GBK").GetBytes(idCard, 0, Math.Min(idCard.Length, 19), buffer, offset);
            offset += 19;

            // 电话号码 (最大14字节)
            buffer[offset++] = (byte)Math.Min(phoneNumber.Length, 14);
            Encoding.GetEncoding("GBK").GetBytes(phoneNumber, 0, Math.Min(phoneNumber.Length, 14), buffer, offset);
            offset += 14;

            // 问题1 (最大20字节)
            buffer[offset++] = (byte)Math.Min(q1.Length, 20);
            Encoding.GetEncoding("GBK").GetBytes(q1, 0, Math.Min(q1.Length, 20), buffer, offset);
            offset += 20;

            // 答案1 (最大20字节)
            buffer[offset++] = (byte)Math.Min(a1.Length, 20);
            Encoding.GetEncoding("GBK").GetBytes(a1, 0, Math.Min(a1.Length, 20), buffer, offset);
            offset += 20;

            // 邮箱 (最大40字节)
            buffer[offset++] = (byte)Math.Min(email.Length, 40);
            Encoding.GetEncoding("GBK").GetBytes(email, 0, Math.Min(email.Length, 40), buffer, offset);
            offset += 40;

            // 问题2 (最大20字节)
            buffer[offset++] = (byte)Math.Min(q2.Length, 20);
            Encoding.GetEncoding("GBK").GetBytes(q2, 0, Math.Min(q2.Length, 20), buffer, offset);
            offset += 20;

            // 答案2 (最大20字节)
            buffer[offset++] = (byte)Math.Min(a2.Length, 20);
            Encoding.GetEncoding("GBK").GetBytes(a2, 0, Math.Min(a2.Length, 20), buffer, offset);
            offset += 20;

            // 生日 (最大10字节)
            buffer[offset++] = (byte)Math.Min(birthday.Length, 10);
            Encoding.GetEncoding("GBK").GetBytes(birthday, 0, Math.Min(birthday.Length, 10), buffer, offset);
            offset += 10;

            // 手机号码 (最大11字节)
            buffer[offset++] = (byte)Math.Min(mobilePhoneNumber.Length, 11);
            Encoding.GetEncoding("GBK").GetBytes(mobilePhoneNumber, 0, Math.Min(mobilePhoneNumber.Length, 11), buffer, offset);
            offset += 11;

            // 剩余85字节填充0（未知字段）
            for (int i = 0; i < 85; i++)
            {
                buffer[offset++] = 0;
            }

            return buffer;
        }

        #endregion

        #region SelectCharServer调用方法

        /// <summary>
        /// 查询角色地图位置（SelectCharServer使用，用于选择角色）
        /// </summary>
        public async Task<MapPositionResult?> QueryMapPositionAsync(string account, string serverName, string charName)
        {
            if (!_connected) return null;

            try
            {
                // account/serverName/charName
                string data = $"{account}/{serverName}/{charName}";
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(data);
                var response = await SendDbMessageAsync(DbMsg.DM_GETCHARPOSITIONFORSELCHAR, payload);
                if (response != null && response.dwFlag == (uint)SERVER_ERROR.SE_OK && response.data != null)
                {
                    // 解析地图位置数据
                    // 格式：mapName/x/y
                    string positionData = Encoding.GetEncoding("GBK").GetString(response.data).TrimEnd('\0');
                    string[] parts = positionData.Split('/');
                    if (parts.Length >= 3)
                    {
                        return new MapPositionResult
                        {
                            MapName = parts[0],
                            X = short.Parse(parts[1]),
                            Y = short.Parse(parts[2])
                        };
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 查询角色列表（SelectCharServer使用）
        /// </summary>
        public async Task<string?> QueryCharListAsync(string account, string serverName)
        {
            if (!_connected) return null;

            try
            {
                // 格式：account/serverName
                string data = $"{account}/{serverName}";//lyo：注意，此处使用字符串传data值
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(data);
                var response = await SendDbMessageAsync(DbMsg.DM_QUERYCHARLIST, payload);
                if (response != null && response.dwFlag == (uint)SERVER_ERROR.SE_OK && response.data != null)
                {
                    return Encoding.GetEncoding("GBK").GetString(response.data).TrimEnd('\0');
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 创建角色（SelectCharServer使用）
        /// </summary>
        public async Task<SERVER_ERROR> CreateCharacterAsync(string account, string serverName, string charName, byte prof, byte hair, byte sex)
        {
            if (!_connected) return SERVER_ERROR.SE_FAIL;

            try
            {
                // 格式：account/serverName/charName/prof/hair/sex
                string data = $"{account}/{serverName}/{charName}/{prof}/{hair}/{sex}";//lyo：注意，此处使用字符串传data值
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(data);
                var response = await SendDbMessageAsync(DbMsg.DM_CREATECHARACTER, payload);
                if (response != null)
                {
                    return (SERVER_ERROR)response.dwFlag;
                }
                return SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }

        /// <summary>
        /// 删除角色（SelectCharServer使用）
        /// </summary>
        public async Task<bool> DeleteCharacterAsync(string account, string serverName, string charName)
        {
            if (!_connected) return false;

            try
            {
                // 格式：account/serverName/charName
                string data = $"{account}/{serverName}/{charName}";//lyo：注意，此处使用字符串传data值
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(data);
                var response = await SendDbMessageAsync(DbMsg.DM_DELETECHARACTER, payload);
                return response != null && response.dwFlag == (uint)SERVER_ERROR.SE_OK;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 恢复角色（SelectCharServer使用）
        /// </summary>
        public async Task<bool> RestoreCharacterAsync(string account, string serverName, string charName)
        {
            if (!_connected) return false;

            try
            {
                // 格式：account/serverName/charName
                string data = $"{account}/{serverName}/{charName}";//lyo：注意，此处使用字符串传data值
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(data);
                var response = await SendDbMessageAsync(DbMsg.DM_RESTORECHARACTER, payload);
                return response != null && response.dwFlag == (uint)SERVER_ERROR.SE_OK;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        /// <summary>
        /// 数据库响应结构
        /// </summary>
        private class DbResponse
        {
            public uint dwFlag { get; set; }
            public ushort wCmd { get; set; }
            public byte[]? data { get; set; }
        }

        /// <summary>
        /// 地图位置结果
        /// </summary>
        public class MapPositionResult
        {
            public string MapName { get; set; } = string.Empty;
            public short X { get; set; }
            public short Y { get; set; }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            try
            {
                StopListening();
                _stream?.Close();
                _client?.Close();
                _connected = false;
                Log("已断开与DBServer的连接");
            }
            catch (Exception ex)
            {
                Log($"断开连接失败: {ex.Message}");
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

        /// <summary>
        /// 记录日志
        /// </summary>
        private void Log(string message)
        {
            OnLogMessage?.Invoke($"[DBServerClient] {DateTime.Now:HH:mm:ss:fff} {message}");
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
