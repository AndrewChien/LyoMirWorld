using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MirCommon.Network
{
    /// <summary>
    /// ServerCenter客户端，用于向ServerCenter注册服务器信息
    /// </summary>
    public class ServerCenterClient : IDisposable
    {
        private readonly string _serverCenterAddress;
        private readonly int _serverCenterPort;
        private TcpClient? _client;
        private NetworkStream? _stream;
        public bool _connected = false;
        private byte _serverIndex = 0; // 在ServerCenter中的索引
        private ServerId _serverId = new ServerId(); // 完整的服务器ID信息
        private string _serverName = string.Empty; // 服务器名称
        private ServerAddr _serverAddr = new ServerAddr(); // 服务器地址

        public ServerCenterClient(string address = "127.0.0.1", int port = 6000)
        {
            _serverCenterAddress = address;
            _serverCenterPort = port;
        }

        /// <summary>
        /// 获取连接状态
        /// </summary>
        public bool IsConnected()
        {
            return _connected;
        }

        /// <summary>
        /// 获取服务器索引
        /// </summary>
        public byte GetServerIndex()
        {
            return _serverIndex;
        }

        /// <summary>
        /// 获取服务器ID信息
        /// </summary>
        public ServerId GetServerId()
        {
            return _serverId;
        }

        /// <summary>
        /// 获取服务器名称
        /// </summary>
        public string GetServerName()
        {
            return _serverName;
        }

        /// <summary>
        /// 获取服务器地址
        /// </summary>
        public ServerAddr GetServerAddr()
        {
            return _serverAddr;
        }

        /// <summary>
        /// 连接到ServerCenter
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(_serverCenterAddress, _serverCenterPort);
                _stream = _client.GetStream();
                _connected = true;
                return true;
            }
            catch (Exception)
            {
                _connected = false;
                return false;
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            _connected = false;
            _stream?.Close();
            _client?.Close();
            _stream = null;
            _client = null;
        }

        /// <summary>
        /// 注册服务器
        /// </summary>
        public async Task<bool> RegisterServerAsync(string serverType, string serverName, string address, int port, int maxConnections)
        {
            if (!_connected) return false;

            try
            {
                // 创建REGISTER_SERVER_INFO结构体
                var registerInfo = new RegisterServerInfo
                {
                    szName = MirCommon.Utils.Helper.ConvertToFixedBytes(serverName,64),
                    Id = new ServerId
                    {
                        bType = GetServerTypeByte(serverType),
                        bGroup = 0, // 默认组
                        bId = 0,    // 由ServerCenter分配
                        bIndex = 0  // 由ServerCenter分配
                    },
                    addr = new ServerAddr()
                };
                
                // 正确设置地址（IP地址应该使用ASCII编码）
                registerInfo.addr.SetAddress(address);
                registerInfo.addr.nPort = (uint)port;

                // 将结构体转换为字节数组
                byte[] payload = StructToBytes(registerInfo);
                
                var builder = new PacketBuilder();
                builder.WriteUInt32(0); // dwFlag
                builder.WriteUInt16(ProtocolCmd.CM_REGISTERSERVER);
                builder.WriteUInt16(0); // w1
                builder.WriteUInt16(0); // w2
                builder.WriteUInt16(0); // w3
                builder.WriteBytes(payload);

                byte[] packet = builder.Build();
                await _stream!.WriteAsync(packet, 0, packet.Length);
                await _stream.FlushAsync();

                // 接收响应
                byte[] buffer = new byte[1024];
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead < 12) return false;

                var reader = new PacketReader(buffer);
                uint dwFlag = reader.ReadUInt32();
                ushort wCmd = reader.ReadUInt16();
                
                if (wCmd == ProtocolCmd.SM_REGISTERSERVEROK && dwFlag == 1)  //注意：这里的1是true的返回值强转的
                {
                    // 解析服务器索引
                    // 响应格式：dwFlag(4) + wCmd(2) + w1(2) + w2(2) + w3(2) + 数据
                    // 数据部分包含RegisterServerResult结构体
                    if (bytesRead >= 12 + 8) // 至少8字节的RegisterServerResult头部
                    {
                        reader.ReadUInt16(); // w1
                        reader.ReadUInt16(); // w2
                        reader.ReadUInt16(); // w3
                        
                        // 读取RegisterServerResult结构体
                        // 结构体格式：bType(1) + bGroup(1) + bIndex(1) + padding(1) + nDbCount(4)
                        byte bType = reader.ReadByte();
                        byte bGroup = reader.ReadByte();
                        byte bIndex = reader.ReadByte();
                        reader.ReadByte(); // padding
                        
                        // 保存完整的服务器信息
                        _serverIndex = bIndex;
                        _serverId.bType = bType;
                        _serverId.bGroup = bGroup;
                        _serverId.bIndex = bIndex;
                        _serverName = serverName;
                        _serverAddr.SetAddress(address);
                        _serverAddr.nPort = (uint)port;
                        
                        Console.WriteLine($"服务器注册成功，分配的索引: {_serverIndex}, 类型: {bType}, 组: {bGroup}");
                    }
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 查找服务器
        /// </summary>
        /// <param name="serverType">服务器类型</param>
        /// <param name="serverName">服务器名称</param>
        /// <returns>FindServerResult结构体，失败返回null</returns>
        public async Task<FindServerResult?> FindServerAsync(string serverType, string serverName)
        {
            if (!_connected) return null;

            try
            {
                // 使用字符串格式：serverType/serverName
                string queryData = $"{serverType}/{serverName}";
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(queryData);
                
                var builder = new PacketBuilder();
                builder.WriteUInt32(0); // clientId
                builder.WriteUInt16(ProtocolCmd.SCM_FINDSERVER);
                builder.WriteUInt16(0); // w1
                builder.WriteUInt16(0); // w2
                builder.WriteUInt16(0); // w3
                builder.WriteBytes(payload);

                byte[] packet = builder.Build();
                await _stream!.WriteAsync(packet, 0, packet.Length);
                await _stream.FlushAsync();

                // 接收响应
                byte[] buffer = new byte[1024];
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead < 12) return null;

                var reader = new PacketReader(buffer);
                uint dwFlag = reader.ReadUInt32(); // SE_OK or error
                ushort wCmd = reader.ReadUInt16();
                reader.ReadUInt16(); // w1
                reader.ReadUInt16(); // w2
                reader.ReadUInt16(); // w3
                
                if (wCmd == ProtocolCmd.SCM_FINDSERVER && dwFlag == 0) // 0表示成功
                {
                    // 响应包含自定义序列化的FindServerResult
                    // 格式: bType(1) + bGroup(1) + bIndex(1) + padding(1) + addr[16](16) + nPort(4) = 24字节
                    if (bytesRead - 12 >= 24)
                    {
                        // 手动解析自定义格式
                        byte bType = reader.ReadByte();
                        byte bGroup = reader.ReadByte();
                        byte bIndex = reader.ReadByte();
                        reader.ReadByte(); // padding
                        
                        // 读取地址字节数组
                        byte[] addrBytes = reader.ReadBytes(16);
                        
                        // 读取端口
                        uint nPort = reader.ReadUInt32();
                        
                        // 创建FindServerResult
                        var result = new FindServerResult
                        {
                            Id = new ServerId
                            {
                                bType = bType,
                                bGroup = bGroup,
                                bIndex = bIndex
                            },
                            addr = new ServerAddr
                            {
                                addr = addrBytes,
                                nPort = nPort
                            }
                        };
                        
                        return result;
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
        /// 发送跨服务器消息
        /// </summary>
        /// <param name="clientId">客户端ID</param>
        /// <param name="cmd">消息命令</param>
        /// <param name="sendType">发送类型</param>
        /// <param name="targetIndex">目标服务器索引</param>
        /// <param name="data">消息数据</param>
        public async Task<bool> SendMsgAcrossServerAsync(uint clientId, ushort cmd, byte sendType, ushort targetIndex, string data)
        {
            if (!_connected) return false;

            try
            {
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(data);
                
                var builder = new PacketBuilder();
                builder.WriteUInt32(clientId);
                builder.WriteUInt16(ProtocolCmd.SCM_MSGACROSSSERVER);
                builder.WriteUInt16(cmd); // w1 - 要转发的命令
                builder.WriteUInt16(sendType); // w2 - 发送类型
                builder.WriteUInt16(targetIndex); // w3 - 目标参数
                builder.WriteBytes(payload);

                byte[] packet = builder.Build();
                await _stream!.WriteAsync(packet, 0, packet.Length);
                await _stream.FlushAsync();
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 发送跨服务器消息
        /// </summary>
        /// <param name="clientId">客户端ID</param>
        /// <param name="cmd">消息命令</param>
        /// <param name="sendType">发送类型</param>
        /// <param name="targetIndex">目标服务器索引</param>
        /// <param name="binaryData">消息数据（字节数组）</param>
        public async Task<bool> SendMsgAcrossServerAsync(uint clientId, ushort cmd, byte sendType, ushort targetIndex, byte[] binaryData)
        {
            if (!_connected) return false;

            try
            {
                var builder = new PacketBuilder();
                builder.WriteUInt32(clientId);
                builder.WriteUInt16(ProtocolCmd.SCM_MSGACROSSSERVER);
                builder.WriteUInt16(cmd); // w1 - 要转发的命令
                builder.WriteUInt16(sendType); // w2 - 发送类型
                builder.WriteUInt16(targetIndex); // w3 - 目标参数
                builder.WriteBytes(binaryData);

                byte[] packet = builder.Build();
                await _stream!.WriteAsync(packet, 0, packet.Length);
                await _stream.FlushAsync();
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取服务器类型ID
        /// </summary>
        private ushort GetServerTypeId(string serverType)
        {
            return serverType.ToLower() switch
            {
                "servercenter" => 1,
                "databaseserver" => 1,  // ST_DATABASESERVER = 1
                "dbserver" => 1,        // 兼容DBServer写法
                "loginserver" => 2,     // ST_LOGINSERVER = 2
                "selectcharserver" => 4, // ST_SELCHARSERVER = 4
                "gameserver" => 6,      // ST_GAMESERVER = 6
                _ => 0
            };
        }

        /// <summary>
        /// 查询服务器信息
        /// </summary>
        public async Task<string?> QueryServerAsync(string serverType, string serverName)
        {
            if (!_connected) return null;

            try
            {
                string data = $"{serverType}/{serverName}";
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(data);
                
                var builder = new PacketBuilder();
                builder.WriteUInt32(0); // dwFlag
                builder.WriteUInt16(ProtocolCmd.CM_QUERYSERVER);
                builder.WriteUInt16(0); // w1
                builder.WriteUInt16(0); // w2
                builder.WriteUInt16(0); // w3
                builder.WriteBytes(payload);

                byte[] packet = builder.Build();
                await _stream!.WriteAsync(packet, 0, packet.Length);
                await _stream.FlushAsync();

                // 接收响应
                byte[] buffer = new byte[1024];
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead < 12) return null;

                var reader = new PacketReader(buffer);
                uint dwFlag = reader.ReadUInt32();
                ushort wCmd = reader.ReadUInt16();
                reader.ReadUInt16(); // w1
                reader.ReadUInt16(); // w2
                reader.ReadUInt16(); // w3
                byte[] responseData = reader.ReadBytes(bytesRead - 12);

                if (wCmd == ProtocolCmd.SM_QUERYSERVEROK && dwFlag == 1) //注意，这个1是True强转的
                {
                    return Encoding.GetEncoding("GBK").GetString(responseData).TrimEnd('\0');
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 注销服务器
        /// </summary>
        public async Task<bool> UnregisterServerAsync(string serverType, string serverName)
        {
            if (!_connected) return false;

            try
            {
                string data = $"{serverType}/{serverName}";
                byte[] payload = Encoding.GetEncoding("GBK").GetBytes(data);
                
                var builder = new PacketBuilder();
                builder.WriteUInt32(0); // dwFlag
                builder.WriteUInt16(ProtocolCmd.CM_UNREGISTERSERVER);
                builder.WriteUInt16(0); // w1
                builder.WriteUInt16(0); // w2
                builder.WriteUInt16(0); // w3
                builder.WriteBytes(payload);

                byte[] packet = builder.Build();
                await _stream!.WriteAsync(packet, 0, packet.Length);
                await _stream.FlushAsync();

                // 接收响应
                byte[] buffer = new byte[1024];
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead < 12) return false;

                var reader = new PacketReader(buffer);
                uint dwFlag = reader.ReadUInt32();
                ushort wCmd = reader.ReadUInt16();
                
                return wCmd == ProtocolCmd.SM_UNREGISTERSERVEROK && dwFlag == 1;//注意这个1是True强转的
            }
            catch
            {
                return false;
            }
        }


        /// <summary>
        /// 将结构体转换为字节数组
        /// </summary>
        private byte[] StructToBytes<T>(T structure) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            byte[] bytes = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            
            try
            {
                Marshal.StructureToPtr(structure, ptr, false);
                Marshal.Copy(ptr, bytes, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            
            return bytes;
        }

        /// <summary>
        /// 将字节数组转换为结构体
        /// </summary>
        private T BytesToStruct<T>(byte[] bytes) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            if (bytes.Length < size)
                throw new ArgumentException($"字节数组长度不足，需要{size}字节，实际{bytes.Length}字节");

            IntPtr ptr = Marshal.AllocHGlobal(size);
            
            try
            {
                Marshal.Copy(bytes, 0, ptr, size);
                return Marshal.PtrToStructure<T>(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        /// <summary>
        /// 获取服务器类型字节值
        /// </summary>
        private byte GetServerTypeByte(string serverType)
        {
            return serverType.ToLower() switch
            {
                "servercenter" => 1,
                "databaseserver" => 1,  // ST_DATABASESERVER = 1
                "dbserver" => 1,        // 兼容DBServer写法
                "loginserver" => 2,     // ST_LOGINSERVER = 2
                "selectcharserver" => 4, // ST_SELCHARSERVER = 4
                "gameserver" => 6,      // ST_GAMESERVER = 6
                _ => 0
            };
        }

        /// <summary>
        /// 获取游戏服务器地址
        /// </summary>
        /// <param name="account">账号</param>
        /// <param name="charName">角色名</param>
        /// <param name="mapName">地图名</param>
        /// <returns>ServerAddr结构体，失败返回null</returns>
        public async Task<ServerAddr?> GetGameServerAddrAsync(string account, string charName, string mapName)
        {
            if (!_connected) return null;

            try
            {
                // 创建EnterGameServer结构体
                var enterGameServer = new EnterGameServer();
                enterGameServer.SetAccount(account);
                enterGameServer.SetName(charName);
                enterGameServer.nLoginId = 0;
                enterGameServer.nSelCharId = 0;
                enterGameServer.nClientId = 0;
                enterGameServer.dwEnterTime = (uint)Environment.TickCount;
                enterGameServer.dwSelectCharServerId = 0;

                byte[] payload = StructToBytes(enterGameServer);
                
                var builder = new PacketBuilder();
                builder.WriteUInt32(0); // dwFlag
                builder.WriteUInt16(ProtocolCmd.SCM_GETGAMESERVERADDR);
                builder.WriteUInt16(0); // w1
                builder.WriteUInt16(0); // w2
                builder.WriteUInt16(0); // w3
                builder.WriteBytes(payload);

                byte[] packet = builder.Build();
                await _stream!.WriteAsync(packet, 0, packet.Length);
                await _stream.FlushAsync();

                // 接收响应
                byte[] buffer = new byte[1024];
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead < 12) return null;

                var reader = new PacketReader(buffer);
                uint dwFlag = reader.ReadUInt32();
                ushort wCmd = reader.ReadUInt16();
                ushort w1 = reader.ReadUInt16(); // 结果代码
                reader.ReadUInt16(); // w2
                reader.ReadUInt16(); // w3
                
                // 修改：检查w1是否为SE_OK，而不是dwFlag
                if (wCmd == ProtocolCmd.SCM_GETGAMESERVERADDR && w1 == (ushort)SERVER_ERROR.SE_OK)
                {
                    // ServerCenter发送的格式是：bType(1) + bGroup(1) + bIndex(1) + padding(1) + addr[16](16) + nPort(4)
                    if (bytesRead - 12 >= 24)
                    {
                        // 跳过bType, bGroup, bIndex, padding
                        reader.ReadByte(); // bType
                        reader.ReadByte(); // bGroup
                        reader.ReadByte(); // bIndex
                        reader.ReadByte(); // padding
                        
                        // 读取地址字节数组
                        byte[] addrBytes = reader.ReadBytes(16);
                        
                        // 读取端口
                        uint nPort = reader.ReadUInt32();
                        
                        // 创建ServerAddr
                        return new ServerAddr
                        {
                            addr = addrBytes,
                            nPort = nPort
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

        //[18:40:22.491] INFO    收到获取游戏服务器地址请求: 客户端ID=0, w1=0, w2=0, w3=0, 数据长度=9
        //[18:40:22.492] ERROR 获取游戏服务器地址请求格式错误: heartbeat
        /// <summary>
        /// 发送心跳包到ServerCenter
        /// </summary>
        public async Task<bool> SendHeartbeatAsync()
        {
            if (!_connected) return false;

            try
            {
                var builder = new PacketBuilder();
                builder.WriteUInt32(0); // dwFlag
                builder.WriteUInt16(ProtocolCmd.CM_QUERYSERVER); // 使用查询服务器命令作为心跳
                builder.WriteUInt16(0); // w1
                builder.WriteUInt16(0); // w2
                builder.WriteUInt16(0); // w3
                // 不发送任何数据，或者发送一个简单的字符串
                builder.WriteBytes(Encoding.GetEncoding("GBK").GetBytes("heartbeat"));

                byte[] packet = builder.Build();
                await _stream!.WriteAsync(packet, 0, packet.Length);
                await _stream.FlushAsync();

                // 心跳包通常不需要等待响应，或者可以简单检查连接是否正常
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
