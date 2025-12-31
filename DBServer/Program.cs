using MirCommon;
using MirCommon.Network;
using MirCommon.Utils;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DBServer
{
    /// <summary>
    /// 数据库服务器主程序
    /// </summary>
    class Program
    {
        private static DBServer? _server;
        private static bool _isRunning = true;

        static async Task Main(string[] args)
        {
            Console.Title = "MirWorld Database Server - C# 版本";
            Console.WriteLine("===========================================");
            Console.WriteLine("   传世数据库服务器 - C# 版本");
            Console.WriteLine("===========================================");
            Console.WriteLine();

            try
            {
                // 从配置文件或参数读取配置
                //var config = LoadConfiguration(args);

                // 使用INI配置文件
                var iniReader = new IniFileReader("config.ini");
                if (!iniReader.Open())
                {
                    LogManager.Default.Error("无法打开配置文件 config.ini");
                    return;
                }

                _server = new DBServer(iniReader);
                
                if (await _server.Initialize())
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 数据库服务器初始化成功");
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 监听端口: {iniReader.GetInteger("数据库服务器", "port", 8000)}");
                    //Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 数据库: {iniReader.GetString("数据库服务器", "database", "MirWorldDB")}");
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 最大连接数: {iniReader.GetInteger("数据库服务器", "maxconnection", 4000)}");
                    Console.WriteLine();
                    
                    await _server.Start();
                    
                    // 控制台命令循环
                    _ = Task.Run(() => CommandLoop());
                    
                    // 保持运行
                    while (_isRunning)
                    {
                        await Task.Delay(1000);
                    }
                }
                else
                {
                    LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 数据库服务器初始化失败");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 错误: {ex.Message}");
                LogManager.Default.Error(ex.StackTrace);
            }
            finally
            {
                _server?.Stop();
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 数据库服务器已停止");
            }
        }

        //private static ServerConfig LoadConfiguration(string[] args)
        //{
        //    return new ServerConfig
        //    {
        //        ServerName = "DBServer",
        //        Port = 8000,
        //        DatabaseServer = "(local)",
        //        DatabaseName = "MirWorldDB",
        //        DatabaseUser = "sa",
        //        DatabasePassword = "123456",
        //        MaxConnections = 1024
        //    };
        //}

        private static void CommandLoop()
        {
            LogManager.Default.Info("输入命令 (help - 显示帮助, exit - 退出):");
            while (_isRunning)
            {
                Console.Write("> ");
                string? input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input))
                    continue;

                string[] parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    continue;

                string command = parts[0].ToLower();

                switch (command)
                {
                    case "help":
                        ShowHelp();
                        break;

                    case "exit":
                    case "quit":
                        LogManager.Default.Info("正在关闭服务器...");
                        _isRunning = false;
                        break;

                    case "status":
                        ShowStatus();
                        break;

                    case "connections":
                        ShowConnections();
                        break;

                    case "stats":
                        ShowStats();
                        break;

                    case "errors":
                        ShowErrorStats();
                        break;

                    case "clear":
                        Console.Clear();
                        break;

                    default:
                        LogManager.Default.Info($"未知命令: {command}，输入 help 查看帮助");
                        break;
                }
            }
        }

        private static void ShowHelp()
        {
            Console.WriteLine("可用命令:");
            Console.WriteLine("  help        - 显示此帮助信息");
            Console.WriteLine("  status      - 显示服务器状态");
            Console.WriteLine("  connections - 显示当前连接数");
            Console.WriteLine("  stats       - 显示性能统计信息");
            Console.WriteLine("  errors      - 显示错误统计信息");
            Console.WriteLine("  clear       - 清屏");
            Console.WriteLine("  exit/quit   - 退出服务器");
        }

        private static void ShowStatus()
        {
            if (_server != null)
            {
                Console.WriteLine("服务器状态:");
                Console.WriteLine($"  运行时间: {_server.GetUptime()}");
                Console.WriteLine($"  当前连接: {_server.GetConnectionCount()}");
                Console.WriteLine($"  处理消息数: {_server.GetProcessedMessageCount()}");
            }
        }

        private static void ShowConnections()
        {
            if (_server != null)
            {
                Console.WriteLine($"当前连接数: {_server.GetConnectionCount()}");
            }
        }
        
        private static void ShowStats()
        {
            if (_server != null)
            {
                Console.WriteLine(_server.GetPerformanceStats());
            }
        }

        private static void ShowErrorStats()
        {
            if (_server != null)
            {
                Console.WriteLine(_server.GetErrorStats());
            }
        }
    }

    /// <summary>
    /// 服务器配置
    /// </summary>
    public class ServerConfig
    {
        public string ServerName { get; set; } = "DBServer";
        public int Port { get; set; } = 5100;
        public string DatabaseServer { get; set; } = "(local)";
        public string DatabaseName { get; set; } = "MirWorldDB";
        public string DatabaseUser { get; set; } = "sa";
        public string DatabasePassword { get; set; } = "dragon";
        public int MaxConnections { get; set; } = 100;
    }

    /// <summary>
    /// 数据库服务器类
    /// </summary>
    public class DBServer
    {
        private readonly IniFileReader _config;
        //private readonly ServerConfig _config;
        private TcpListener? _listener;
        private readonly List<ClientConnection> _connections = new();
        private readonly object _connectionLock = new();
        private DateTime _startTime;
        private long _processedMessages = 0;
        private bool _isRunning = false;
        private AppDB_New? _appDB;

        private string _addr = "127.0.0.1";
        private int _port = 8000;
        private string _name = "db01";
        private int _maxconnection = 1024;
        private string _server = "(local)";
        private string _database = "MirWorldDB";
        private string _account = "sa";
        private string _password = "123456";
        private string _serverCenterAddress = "127.0.0.1";
        private int _serverCenterPort = 6000;

        public DBServer(IniFileReader config)
        {
            _config = config;
        }

        public async Task<bool> Initialize()
        {
            try
            {
                // 从INI文件的[数据库服务器]节读取配置
                string sectionName = "数据库服务器";
                _addr = _config.GetString(sectionName, "addr", "127.0.0.1");
                _port = _config.GetInteger(sectionName, "port", 8000);
                _name = _config.GetString(sectionName, "name", "db01");
                _maxconnection = _config.GetInteger(sectionName, "maxconnection", 1024);
                
                // 读取数据库类型配置
                string dbType = _config.GetString(sectionName, "dbtype", "sqlite");
                string sqlitePath = _config.GetString(sectionName, "sqlitepath", "MirWorldDB.sqlite");
                _server = _config.GetString(sectionName, "server", "(local)");
                _database = _config.GetString(sectionName, "database", "MirWorldDB");
                _account = _config.GetString(sectionName, "account", "sa");
                _password = _config.GetString(sectionName, "password", "123456");
                
                // 从INI文件的[服务器中心]节读取ServerCenter配置
                string scSectionName = "服务器中心";
                _serverCenterAddress = _config.GetString(scSectionName, "addr", "127.0.0.1");
                _serverCenterPort = _config.GetInteger(scSectionName, "port", 6000);

                // 创建AppDB_New实例（支持多种数据库）
                LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] 正在连接到数据库...");
                LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] 数据库类型: {dbType}");
                
                if (dbType.ToLower() == "sqlite")
                {
                    LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] SQLite文件路径: {sqlitePath}");
                }
                else
                {
                    LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] 服务器: {_server}, 数据库: {_database}");
                }
                
                _appDB = new AppDB_New(dbType, _server, _database, _account, _password, sqlitePath);
                if (_appDB.OpenDataBase() != SERVER_ERROR.SE_OK)
                {
                    LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 数据库连接失败");
                    return false;
                }

                // 启动健康检查
                _appDB.StartHealthCheck();
                LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] 数据库连接成功");
                LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] 健康检查已启动，检查间隔: 1分钟");
                
                // 向ServerCenter注册
                LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] 正在向ServerCenter注册...");
                using var scClient = new MirCommon.Network.ServerCenterClient(_serverCenterAddress, _serverCenterPort);
                if (await scClient.ConnectAsync())
                {
                    bool registered = await scClient.RegisterServerAsync("DBServer", _name, _addr, _port, _maxconnection);
                    if (registered)
                    {
                        LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] ServerCenter注册成功");
                    }
                    else
                    {
                        LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] ServerCenter注册失败");
                    }
                }
                else
                {
                    LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 无法连接到ServerCenter");
                }
                
                _startTime = DateTime.Now;
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 初始化失败: {ex.Message}");
                LogManager.Default.Error(ex.StackTrace);
                return false;
            }
        }

        public async Task Start()
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            _isRunning = true;

            LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] 服务器已启动，等待连接...");

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
                        {
                            LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 接受连接错误: {ex.Message}");
                        }
                    }
                }
            });
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();

            lock (_connectionLock)
            {
                foreach (var conn in _connections.ToList())
                {
                    conn.Close();
                }
                _connections.Clear();
            }

            _appDB?.Close();
            
            // 从ServerCenter注销
            try
            {
                using var scClient = new MirCommon.Network.ServerCenterClient(_serverCenterAddress, _serverCenterPort);
                if (scClient.ConnectAsync().GetAwaiter().GetResult())
                {
                    scClient.UnregisterServerAsync("DBServer", _name).GetAwaiter().GetResult();
                }
            }
            catch { }
        }

        private async Task HandleClient(TcpClient client)
        {
            var connection = new ClientConnection(client, this, _appDB!);
            
            lock (_connectionLock)
            {
                if (_connections.Count >= _maxconnection)
                {
                    LogManager.Default.Warning($"[{DateTime.Now:HH:mm:ss}] 连接已满，拒绝新连接");
                    client.Close();
                    return;
                }
                _connections.Add(connection);
            }

            LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] 新连接: {client.Client.RemoteEndPoint}");

            try
            {
                await connection.ProcessAsync();
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 处理客户端错误: {ex.Message}");
            }
            finally
            {
                lock (_connectionLock)
                {
                    _connections.Remove(connection);
                }
                LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] 连接断开: {client.Client.RemoteEndPoint}");
            }
        }

        public void IncrementMessageCount()
        {
            Interlocked.Increment(ref _processedMessages);
        }

        public TimeSpan GetUptime() => DateTime.Now - _startTime;
        public int GetConnectionCount()
        {
            lock (_connectionLock)
            {
                return _connections.Count;
            }
        }
        public long GetProcessedMessageCount() => Interlocked.Read(ref _processedMessages);
        
        /// <summary>
        /// 获取性能统计信息
        /// </summary>
        public string GetPerformanceStats()
        {
            var stats = new StringBuilder();
            stats.AppendLine("=== 数据库服务器性能统计 ===");
            stats.AppendLine($"运行时间: {GetUptime():hh\\:mm\\:ss}");
            stats.AppendLine($"当前连接数: {GetConnectionCount()}");
            stats.AppendLine($"处理消息数: {GetProcessedMessageCount()}");
            
            return stats.ToString();
        }

        /// <summary>
        /// 获取错误统计信息
        /// </summary>
        public string GetErrorStats()
        {
            var stats = new StringBuilder();
            stats.AppendLine("=== 数据库服务器错误统计 ===");
            
            if (_appDB != null)
            {
                var errorStats = _appDB.GetErrorStatistics();
                if (errorStats != null)
                {
                    stats.AppendLine($"总错误数: {errorStats.TotalErrors}");
                    stats.AppendLine($"连接错误数: {errorStats.ConnectionErrors}");
                    stats.AppendLine($"查询错误数: {errorStats.QueryErrors}");
                    stats.AppendLine($"超时错误数: {errorStats.TimeoutErrors}");
                    stats.AppendLine($"重试成功数: {errorStats.RetrySuccesses}");
                    stats.AppendLine($"重试失败数: {errorStats.RetryFailures}");
                    stats.AppendLine($"最后错误时间: {errorStats.LastErrorTime:yyyy-MM-dd HH:mm:ss}");
                    stats.AppendLine($"最后错误消息: {errorStats.LastErrorMessage}");
                }
                else
                {
                    stats.AppendLine("错误统计信息不可用");
                }
            }
            else
            {
                stats.AppendLine("数据库连接未初始化");
            }
            
            return stats.ToString();
        }
    }

    /// <summary>
    /// 客户端连接类
    /// </summary>
    public class ClientConnection
    {
        private readonly TcpClient _client;
        private readonly DBServer _server;
        private readonly NetworkStream _stream;
        private readonly AppDB_New _appDB;
        
        // 存储从请求中提取的clientKey和charId，用于响应时传回
        private uint _clientKey = 0;
        private uint _charId = 0;

        public ClientConnection(TcpClient client, DBServer server, AppDB_New appDB)
        {
            _client = client;
            _server = server;
            _appDB = appDB;
            _stream = client.GetStream();
        }

        public async Task ProcessAsync()
        {
            byte[] buffer = new byte[8192];

            while (_client.Connected)
            {
                try
                {
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                        break;

                    // 处理接收到的数据
                    await ProcessMessage(buffer, bytesRead);
                    _server.IncrementMessageCount();
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 处理消息错误: {ex.Message}");
                    break;
                }
            }
        }

        private async Task ProcessMessage(byte[] data, int length)
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
                    LogManager.Default.Warning($"[{DateTime.Now:HH:mm:ss}] 无效的消息格式，未找到'#'和'!'边界");
                    return;
                }
                
                // 提取'#'和'!'之间的编码数据
                int encodedLength = endIndex - startIndex;
                byte[] encodedData = new byte[encodedLength];
                Array.Copy(data, startIndex, encodedData, 0, encodedLength);
                
                // 检查第一个字符是否是数字（'0'-'9'），如果是则跳过
                int decodeStart = 0;
                if (encodedLength > 0 && encodedData[0] >= '0' && encodedData[0] <= '9')
                {
                    decodeStart = 1;
                }
                
                // 解码游戏消息
                byte[] decoded = new byte[(encodedLength - decodeStart) * 3 / 4 + 4]; // 解码后最大可能的大小
                byte[] dataToDecode = new byte[encodedLength - decodeStart];
                Array.Copy(encodedData, decodeStart, dataToDecode, 0, dataToDecode.Length);
                int decodedSize = GameCodec.UnGameCode(dataToDecode, decoded);
                
                if (decodedSize < 12) // 消息头至少12字节
                {
                    LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] 解码后数据太小: {decodedSize}字节");
                    return;
                }

                var reader = new PacketReader(decoded);
                uint dwFlag = reader.ReadUInt32();
                ushort wCmd = reader.ReadUInt16();
                ushort w1 = reader.ReadUInt16();
                ushort w2 = reader.ReadUInt16();
                ushort w3 = reader.ReadUInt16();
                byte[] payload = reader.ReadBytes(decodedSize - 12);

                // 保存clientKey和charId
                // 根据DBServerClient.cs中的编码方式：
                // wParam1 = clientKey的低16位
                // wParam2 = clientKey的高16位
                // wParam3 = charId的低16位
                _clientKey = (uint)((w2 << 16) | w1);
                _charId = (uint)w3;

                // 添加详细日志：接收到的数据包信息
                LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] [DBServer接收] 消息头: dwFlag={dwFlag}, wCmd={wCmd}({(DbMsg)wCmd}), w1={w1}, w2={w2}, w3={w3}");
                LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] [DBServer接收] 解析消息头中的 clientKey={_clientKey}, charId={_charId}");
                LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] [DBServer接收] 原始数据长度: {length}字节, 解码后长度: {decodedSize}字节, 负载长度: {payload.Length}字节");
                
                // 根据DbMsg枚举处理消息
                DbMsg msgType = (DbMsg)wCmd;
                await HandleDbMessage(msgType, payload, w1, w2, w3);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 处理数据库消息错误: {ex.Message}");
            }
        }


        /*
        - DM_CHECKACCOUNT - 检查账号密码
        - DM_CHECKACCOUNTEXIST - 检查账号是否存在
        - DM_CREATEACCOUNT - 创建账号
        - DM_CHANGEPASSWORD - 修改密码
        - DM_QUERYCHARLIST - 查询角色列表
        - DM_CREATECHARACTER - 创建角色
        - DM_DELETECHARACTER - 删除角色
        - DM_RESTORECHARACTER - 恢复角色
        - DM_GETCHARPOSITIONFORSELCHAR - 获取角色位置
        - DM_GETCHARDBINFO - 获取角色数据库信息
        - DM_PUTCHARDBINFO - 更新角色数据库信息
        - DM_QUERYITEMS - 查询物品
        - DM_UPDATEITEMS - 更新物品
        - DM_QUERYMAGIC - 查询魔法
        - DM_UPDATEMAGIC - 更新魔法
        - DM_EXECSQL - 执行SQL
        - DM_QUERYUPGRADEITEM - 查询升级物品
        - DM_QUERYTASKINFO - 查询任务信息
        - DM_DELETEDCHARLIST - 查询已删除角色列表
        - DM_CREATEITEM - 创建物品
        - DM_DELETEITEM - 删除物品
        - DM_UPDATEITEM - 更新物品
        - DM_UPDATEITEMPOS - 更新物品位置
        - DM_UPDATEITEMOWNER - 更新物品所有者
        - DM_UPDATEITEMPOSEX - 批量更新物品位置
        - DM_UPDATEITEMOWNEREX - 批量更新物品所有者
        - DM_UPDATEITEMEX - 批量更新物品
        - DM_UPDATECOMMUNITY - 更新社区信息
        - DM_QUERYCOMMUNITY - 查询社区信息
        - DM_BREAKFRIEND - 解除好友关系
        - DM_BREAKMARRIAGE - 解除婚姻关系
        - DM_BREAKMASTER - 解除师徒关系
        - DM_CACHECHARDATA - 缓存角色数据
        - DM_UPGRADEITEM - 升级物品
        - DM_RESTOREGUILDNAME - 恢复行会名称
        - DM_ADDCREDIT - 添加信用
        - DM_CHECKFREE - 检查空闲状态
        - DM_DELETEMAGIC - 删除魔法
        - DM_UPDATETASKINFO - 更新任务信息
        - DM_UPDATEACCOUNTSTATE - 更新账号状态
        - DM_UPDATEITEMPOSEX2 - 扩展的批量更新物品位置（新增）
        */
        private async Task HandleDbMessage(DbMsg msgType, byte[] payload, ushort w1, ushort w2, ushort w3)
        {
            try
            {
                switch (msgType)
                {
                    case DbMsg.DM_CHECKACCOUNT:
                        await HandleCheckAccount(payload);
                        break;
                    case DbMsg.DM_CHECKACCOUNTEXIST:
                        await HandleCheckAccountExist(payload);
                        break;
                    case DbMsg.DM_CREATEACCOUNT:
                        await HandleCreateAccount(payload);
                        break;
                    case DbMsg.DM_CHANGEPASSWORD:
                        await HandleChangePassword(payload);
                        break;
                    case DbMsg.DM_QUERYCHARLIST:
                        await HandleQueryCharList(payload);
                        break;
                    case DbMsg.DM_CREATECHARACTER:
                        await HandleCreateCharacter(payload);
                        break;
                    case DbMsg.DM_DELETECHARACTER:
                        await HandleDeleteCharacter(payload);
                        break;
                    case DbMsg.DM_RESTORECHARACTER:
                        await HandleRestoreCharacter(payload);
                        break;
                    case DbMsg.DM_GETCHARPOSITIONFORSELCHAR:
                        await HandleGetCharPositionForSelChar(payload);
                        break;
                    case DbMsg.DM_PUTCHARDBINFO:
                        await HandlePutCharDBInfo(payload);
                        break;
                    case DbMsg.DM_UPDATEITEMS:
                        await HandleUpdateItems(payload);
                        break;
                    case DbMsg.DM_UPDATEMAGIC:
                        await HandleUpdateMagic(payload);
                        break;
                    case DbMsg.DM_EXECSQL:
                        await HandleExecSql(payload);
                        break;
                    case DbMsg.DM_DELETEDCHARLIST:
                        await HandleDeletedCharList(payload);
                        break;
                    case DbMsg.DM_CREATEITEM:
                        await HandleCreateItem(payload);
                        break;
                    case DbMsg.DM_DELETEITEM:
                        await HandleDeleteItem(payload);
                        break;
                    case DbMsg.DM_UPDATEITEM:
                        await HandleUpdateItem(payload);
                        break;
                    case DbMsg.DM_UPDATEITEMPOS:
                        await HandleUpdateItemPos(payload);
                        break;
                    case DbMsg.DM_UPDATEITEMOWNER:
                        await HandleUpdateItemOwner(payload);
                        break;
                    case DbMsg.DM_UPDATEITEMPOSEX:
                        await HandleUpdateItemPosEx(payload);
                        break;
                    case DbMsg.DM_UPDATEITEMOWNEREX:
                        await HandleUpdateItemOwnerEx(payload);
                        break;
                    case DbMsg.DM_UPDATEITEMEX:
                        await HandleUpdateItemEx(payload);
                        break;
                    case DbMsg.DM_UPDATECOMMUNITY:
                        await HandleUpdateCommunity(payload);
                        break;
                    case DbMsg.DM_QUERYCOMMUNITY:
                        await HandleQueryCommunity(payload);
                        break;
                    case DbMsg.DM_BREAKFRIEND:
                        await HandleBreakFriend(payload);
                        break;
                    case DbMsg.DM_BREAKMARRIAGE:
                        await HandleBreakMarriage(payload);
                        break;
                    case DbMsg.DM_BREAKMASTER:
                        await HandleBreakMaster(payload);
                        break;
                    case DbMsg.DM_CACHECHARDATA:
                        await HandleCacheCharData(payload);
                        break;
                    case DbMsg.DM_UPGRADEITEM:
                        await HandleUpgradeItem(payload);
                        break;
                    case DbMsg.DM_RESTOREGUILDNAME:
                        await HandleRestoreGuildName(payload);
                        break;
                    case DbMsg.DM_ADDCREDIT:
                        await HandleAddCredit(payload);
                        break;
                    case DbMsg.DM_CHECKFREE:
                        await HandleCheckFree(payload);
                        break;
                    case DbMsg.DM_DELETEMAGIC:
                        await HandleDeleteMagic(payload);
                        break;
                    case DbMsg.DM_UPDATETASKINFO:
                        await HandleUpdateTaskInfo(payload);
                        break;
                    case DbMsg.DM_UPDATEACCOUNTSTATE:
                        await HandleUpdateAccountState(payload);
                        break;
                    case DbMsg.DM_UPDATEITEMPOSEX2:
                        await HandleUpdateItemPosEx2(payload);
                        break;
                    case DbMsg.DM_GETCHARDBINFO:    //GameServer使用
                        await HandleGetCharDBInfo(payload, w1, w2, w3);
                        break;
                    case DbMsg.DM_QUERYITEMS:    //GameServer使用
                        await HandleQueryItems(payload, w1, w2, w3);
                        break;
                    case DbMsg.DM_QUERYMAGIC:    //GameServer使用
                        await HandleQueryMagic(payload, w1, w2, w3);
                        break;
                    case DbMsg.DM_QUERYUPGRADEITEM:    //GameServer使用
                        await HandleQueryUpgradeItem(payload, w1, w2, w3);
                        break;
                    case DbMsg.DM_QUERYTASKINFO:    //GameServer使用
                        await HandleQueryTaskInfo(payload, w1, w2, w3);
                        break;
                    default:
                        LogManager.Default.Warning($"[{DateTime.Now:HH:mm:ss}] 未知数据库消息类型: {msgType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 处理{msgType}消息错误: {ex.Message}");
            }
        }

        private async Task HandleCheckAccount(byte[] payload)
        {
            // 解析账号密码
            string data = Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
            string[] parts = data.Split('/');//lyo：注意，此处使用字符串传data值
            if (parts.Length < 2) return;

            string account = parts[0];
            string password = parts[1];

            var result = _appDB.CheckAccount(account, password);
            SendDbResponse(DbMsg.DM_CHECKACCOUNT, result == SERVER_ERROR.SE_OK ? (uint)SERVER_ERROR.SE_OK : (uint)SERVER_ERROR.SE_FAIL);
        }

        private async Task HandleCheckAccountExist(byte[] payload)
        {
            string account = Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
            var result = _appDB.CheckAccountExist(account);
            SendDbResponse(DbMsg.DM_CHECKACCOUNTEXIST, result == SERVER_ERROR.SE_OK ? (uint)SERVER_ERROR.SE_OK : (uint)SERVER_ERROR.SE_FAIL);
        }
        
        private async Task HandleCreateAccount(byte[] payload)
        {
            try
            {
                // 解析完整的RegisterAccount结构
                // 格式：account/password/name/birthday/q1/a1/q2/a2/email/phoneNumber/mobilePhoneNumber/idCard
                string data = Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
                string[] parts = data.Split('/');//lyo：注意，此处使用字符串传data值

                if (parts.Length < 12)
                {
                    SendDbResponse(DbMsg.DM_CREATEACCOUNT, (uint)SERVER_ERROR.SE_FAIL);
                    return;
                }
                
                string account = parts[0];
                string password = parts[1];
                string name = parts[2];
                string birthday = parts[3];
                string q1 = parts[4];
                string a1 = parts[5];
                string q2 = parts[6];
                string a2 = parts[7];
                string email = parts[8];
                string phoneNumber = parts[9];
                string mobilePhoneNumber = parts[10];
                string idCard = parts[11];
                
                // 调用AppDB_New的CreateAccount方法
                var result = _appDB.CreateAccount(account, password, name, birthday, 
                    q1, a1, q2, a2, email, phoneNumber, mobilePhoneNumber, idCard);
                SendDbResponse(DbMsg.DM_CREATEACCOUNT, result == SERVER_ERROR.SE_OK ? (uint)SERVER_ERROR.SE_OK : (uint)SERVER_ERROR.SE_FAIL);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 创建账号错误: {ex.Message}");
                SendDbResponse(DbMsg.DM_CREATEACCOUNT, (uint)SERVER_ERROR.SE_FAIL);
            }
        }
        
        private async Task HandleChangePassword(byte[] payload)
        {
            string data = Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
            string[] parts = data.Split('/');//lyo：注意，此处使用字符串传data值
            if (parts.Length < 3) return;

            string account = parts[0];
            string oldPassword = parts[1];
            string newPassword = parts[2];

            var result = _appDB.ChangePassword(account, oldPassword, newPassword);
            SendDbResponse(DbMsg.DM_CHANGEPASSWORD, result == SERVER_ERROR.SE_OK ? (uint)SERVER_ERROR.SE_OK : (uint)SERVER_ERROR.SE_FAIL);
        }
        
        private async Task HandleQueryCharList(byte[] payload)
        {
            string data = Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
            string[] parts = data.Split('/');//lyo：注意，此处使用字符串传data值
            if (parts.Length < 2) return;

            string account = parts[0];
            string serverName = parts[1];

            var result = _appDB.GetCharList(account, serverName, out string charListData);
            if (result == SERVER_ERROR.SE_OK)
            {
                SendDbResponse(DbMsg.DM_QUERYCHARLIST, (uint)SERVER_ERROR.SE_OK, Encoding.GetEncoding("GBK").GetBytes(charListData));
            }
            else
            {
                SendDbResponse(DbMsg.DM_QUERYCHARLIST, (uint)SERVER_ERROR.SE_FAIL);
            }
        }
        
        private async Task HandleCreateCharacter(byte[] payload)
        {
            // 解析创建角色数据
            string data = Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
            string[] parts = data.Split('/');//lyo：注意，此处使用字符串传data值
            if (parts.Length < 6) return;

            string account = parts[0];
            string serverName = parts[1];
            string name = parts[2];
            byte job = byte.Parse(parts[3]);
            byte hair = byte.Parse(parts[4]);
            byte sex = byte.Parse(parts[5]);

            var result = _appDB.CreateCharacter(account, serverName, name, job, hair, sex);
            // SE_OK = 0, SE_SELCHAR_CHAREXIST = 200, SE_FAIL = 1
            SendDbResponse(DbMsg.DM_CREATECHARACTER, (uint)result);
        }
        
        private async Task HandleDeleteCharacter(byte[] payload)
        {
            string data = Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
            string[] parts = data.Split('/');//lyo：注意，此处使用字符串传data值
            if (parts.Length < 3) return;

            string account = parts[0];
            string serverName = parts[1];
            string name = parts[2];

            var result = _appDB.DelCharacter(account, serverName, name);
            SendDbResponse(DbMsg.DM_DELETECHARACTER, result == SERVER_ERROR.SE_OK ? (uint)SERVER_ERROR.SE_OK : (uint)SERVER_ERROR.SE_FAIL);
        }
        
        private async Task HandleRestoreCharacter(byte[] payload)
        {
            string data = Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
            string[] parts = data.Split('/');//lyo：注意，此处使用字符串传data值
            if (parts.Length < 3) return;

            string account = parts[0];
            string serverName = parts[1];
            string name = parts[2];

            var result = _appDB.RestoreCharacter(account, serverName, name);
            SendDbResponse(DbMsg.DM_RESTORECHARACTER, result == SERVER_ERROR.SE_OK ? (uint)SERVER_ERROR.SE_OK : (uint)SERVER_ERROR.SE_FAIL);
        }

        private async Task HandleGetCharPositionForSelChar(byte[] payload)
        {
            try
            {
                // 解析账号、服务器名、角色名
                string data = Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
                string[] parts = data.Split('/');//lyo：注意，此处使用字符串传data值
                if (parts.Length < 3)
                {
                    SendDbResponse(DbMsg.DM_GETCHARPOSITIONFORSELCHAR, (uint)SERVER_ERROR.SE_FAIL);
                    return;
                }

                string account = parts[0];
                string serverName = parts[1];
                string charName = parts[2];

                // 查询角色位置
                var result = _appDB.GetMapPosition(account, serverName, charName, out string mapName, out short x, out short y);
                if (result == SERVER_ERROR.SE_OK)
                {
                    // 格式：mapName/x/y
                    string positionData = $"{mapName}/{x}/{y}";
                    SendDbResponse(DbMsg.DM_GETCHARPOSITIONFORSELCHAR, (uint)SERVER_ERROR.SE_OK, Encoding.GetEncoding("GBK").GetBytes(positionData));
                }
                else
                {
                    SendDbResponse(DbMsg.DM_GETCHARPOSITIONFORSELCHAR, (uint)SERVER_ERROR.SE_FAIL);
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 查询角色位置错误: {ex.Message}");
                SendDbResponse(DbMsg.DM_GETCHARPOSITIONFORSELCHAR, (uint)SERVER_ERROR.SE_FAIL);
            }
        }
        
        private async Task HandlePutCharDBInfo(byte[] payload)
        {
            try
            {
                // 解析CHARDBINFO数据结构
                if (payload.Length < 4) return;
                
                // 解析账号、服务器名、角色名
                string data = Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
                string[] parts = data.Split('/');
                if (parts.Length < 3) return;
                
                string account = parts[0];
                string serverName = parts[1];
                string name = parts[2];
                
                // 提取角色数据（剩余部分）
                byte[] charData = new byte[0]; // 这里需要根据实际数据结构解析
                
                var result = _appDB.PutCharDBInfo(account, serverName, name, charData);
                SendDbResponse(DbMsg.DM_PUTCHARDBINFO, result == SERVER_ERROR.SE_OK ? (uint)SERVER_ERROR.SE_OK : (uint)SERVER_ERROR.SE_FAIL);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 更新角色数据错误: {ex.Message}");
                SendDbResponse(DbMsg.DM_PUTCHARDBINFO, (uint)SERVER_ERROR.SE_FAIL);
            }
        }
        
        private async Task HandleUpdateItems(byte[] payload)
        {
            try
            {
                // 解析UpdateItems数据结构
                // 格式：ownerId/flag/itemsData
                if (payload.Length < 5) return;
                
                uint ownerId = BitConverter.ToUInt32(payload, 0);
                byte flag = payload[4];
                
                // itemsData是剩余部分
                byte[] itemsData = new byte[payload.Length - 5];
                Array.Copy(payload, 5, itemsData, 0, itemsData.Length);
                
                var result = _appDB.UpdateItems(ownerId, flag, itemsData);
                SendDbResponse(DbMsg.DM_UPDATEITEMS, result == SERVER_ERROR.SE_OK ? (uint)SERVER_ERROR.SE_OK : (uint)SERVER_ERROR.SE_FAIL);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 更新物品数据错误: {ex.Message}");
                SendDbResponse(DbMsg.DM_UPDATEITEMS, (uint)SERVER_ERROR.SE_FAIL);
            }
        }
        
        private async Task HandleUpdateMagic(byte[] payload)
        {
            try
            {
                // 解析UpdateMagic数据结构
                // 格式：ownerId/magicData
                if (payload.Length < 4) return;
                
                uint ownerId = BitConverter.ToUInt32(payload, 0);
                
                // magicData是剩余部分
                byte[] magicData = new byte[payload.Length - 4];
                Array.Copy(payload, 4, magicData, 0, magicData.Length);
                
                var result = _appDB.UpdateMagic(ownerId, magicData);
                SendDbResponse(DbMsg.DM_UPDATEMAGIC, result == SERVER_ERROR.SE_OK ? (uint)SERVER_ERROR.SE_OK : (uint)SERVER_ERROR.SE_FAIL);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 更新魔法数据错误: {ex.Message}");
                SendDbResponse(DbMsg.DM_UPDATEMAGIC, (uint)SERVER_ERROR.SE_FAIL);
            }
        }
        
        private async Task HandleExecSql(byte[] payload)
        {
            string sql = Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
            var result = _appDB.ExecSqlCommand(sql, out System.Data.DataTable data);
            SendDbResponse(DbMsg.DM_EXECSQL, result == SERVER_ERROR.SE_OK ? (uint)SERVER_ERROR.SE_OK : (uint)SERVER_ERROR.SE_FAIL);
        }

        private async Task HandleDeletedCharList(byte[] payload)
        {
            try
            {
                // 解析账号和服务器名
                string data = Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
                string[] parts = data.Split('/');//lyo：注意，此处使用字符串传data值
                if (parts.Length < 2) return;

                string account = parts[0];
                string serverName = parts[1];

                var result = _appDB.GetDelCharList(account, serverName, out string delCharListData);
                if (result == SERVER_ERROR.SE_OK)
                {
                    SendDbResponse(DbMsg.DM_DELETEDCHARLIST, (uint)SERVER_ERROR.SE_OK, Encoding.GetEncoding("GBK").GetBytes(delCharListData));
                }
                else
                {
                    SendDbResponse(DbMsg.DM_DELETEDCHARLIST, (uint)SERVER_ERROR.SE_FAIL);
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 查询已删除角色列表错误: {ex.Message}");
                SendDbResponse(DbMsg.DM_DELETEDCHARLIST, (uint)SERVER_ERROR.SE_FAIL);
            }
        }

        private async Task HandleCreateItem(byte[] payload)
        {
            try
            {
                if (payload.Length < 12) return;
                
                uint ownerId = BitConverter.ToUInt32(payload, 0);
                byte flag = payload[4];
                ushort pos = BitConverter.ToUInt16(payload, 5);
                
                // itemData是剩余部分
                byte[] itemData = new byte[payload.Length - 7];
                Array.Copy(payload, 7, itemData, 0, itemData.Length);
                
                var result = _appDB.CreateItem(ownerId, flag, pos, itemData);
                SendDbResponse(DbMsg.DM_CREATEITEM, result == SERVER_ERROR.SE_OK ? (uint)SERVER_ERROR.SE_OK : (uint)SERVER_ERROR.SE_FAIL);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 创建物品错误: {ex.Message}");
                SendDbResponse(DbMsg.DM_CREATEITEM, (uint)SERVER_ERROR.SE_FAIL);
            }
        }

        private async Task HandleDeleteItem(byte[] payload)
        {
            try
            {
                if (payload.Length < 4) return;
                
                uint itemId = BitConverter.ToUInt32(payload, 0);
                
                var result = _appDB.DeleteItem(itemId);
                SendDbResponse(DbMsg.DM_DELETEITEM, result == SERVER_ERROR.SE_OK ? (uint)SERVER_ERROR.SE_OK : (uint)SERVER_ERROR.SE_FAIL);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 删除物品错误: {ex.Message}");
                SendDbResponse(DbMsg.DM_DELETEITEM, (uint)SERVER_ERROR.SE_FAIL);
            }
        }

        private async Task HandleUpdateItem(byte[] payload)
        {
            try
            {
                if (payload.Length < 8) return;
                
                uint ownerId = BitConverter.ToUInt32(payload, 0);
                byte flag = payload[4];
                ushort pos = BitConverter.ToUInt16(payload, 5);
                
                // itemData是剩余部分
                byte[] itemData = new byte[payload.Length - 7];
                Array.Copy(payload, 7, itemData, 0, itemData.Length);
                
                var result = _appDB.UpdateItem(ownerId, flag, pos, itemData);
                SendDbResponse(DbMsg.DM_UPDATEITEM, result == SERVER_ERROR.SE_OK ? (uint)SERVER_ERROR.SE_OK : (uint)SERVER_ERROR.SE_FAIL);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 更新物品错误: {ex.Message}");
                SendDbResponse(DbMsg.DM_UPDATEITEM, (uint)SERVER_ERROR.SE_FAIL);
            }
        }

        private async Task HandleUpdateItemPos(byte[] payload)
        {
            try
            {
                if (payload.Length < 8) return;
                
                uint itemId = BitConverter.ToUInt32(payload, 0);
                byte flag = payload[4];
                ushort pos = BitConverter.ToUInt16(payload, 5);
                
                var result = _appDB.UpdateItemPos(itemId, flag, pos);
                SendDbResponse(DbMsg.DM_UPDATEITEMPOS, result == SERVER_ERROR.SE_OK ? (uint)SERVER_ERROR.SE_OK : (uint)SERVER_ERROR.SE_FAIL);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 更新物品位置错误: {ex.Message}");
                SendDbResponse(DbMsg.DM_UPDATEITEMPOS, (uint)SERVER_ERROR.SE_FAIL);
            }
        }

        private async Task HandleUpdateItemOwner(byte[] payload)
        {
            try
            {
                if (payload.Length < 8) return;
                
                uint itemId = BitConverter.ToUInt32(payload, 0);
                byte flag = payload[4];
                ushort pos = BitConverter.ToUInt16(payload, 5);
                uint ownerId = BitConverter.ToUInt32(payload, 7);
                
                var result = _appDB.UpdateItemOwner(itemId, ownerId, flag, pos);
                SendDbResponse(DbMsg.DM_UPDATEITEMOWNER, result == SERVER_ERROR.SE_OK ? (uint)SERVER_ERROR.SE_OK : (uint)SERVER_ERROR.SE_FAIL);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 更新物品所有者错误: {ex.Message}");
                SendDbResponse(DbMsg.DM_UPDATEITEMOWNER, (uint)SERVER_ERROR.SE_FAIL);
            }
        }

        private async Task HandleUpdateItemPosEx(byte[] payload)
        {
            try
            {
                if (payload.Length < 6) return;
                
                byte flag = payload[0];
                ushort count = BitConverter.ToUInt16(payload, 1);
                
                byte[] itemPosData = new byte[payload.Length - 3];
                Array.Copy(payload, 3, itemPosData, 0, itemPosData.Length);
                
                var result = _appDB.UpdateItemPosEx(flag, count, itemPosData);
                SendDbResponse(DbMsg.DM_UPDATEITEMPOSEX, result == SERVER_ERROR.SE_OK ? (uint)SERVER_ERROR.SE_OK : (uint)SERVER_ERROR.SE_FAIL);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 批量更新物品位置错误: {ex.Message}");
                SendDbResponse(DbMsg.DM_UPDATEITEMPOSEX, (uint)SERVER_ERROR.SE_FAIL);
            }
        }

        private async Task HandleUpdateItemOwnerEx(byte[] payload)
        {
            try
            {
                if (payload.Length < 8) return;
                
                uint itemId = BitConverter.ToUInt32(payload, 0);
                byte flag = payload[4];
                ushort pos = BitConverter.ToUInt16(payload, 5);
                uint ownerId = BitConverter.ToUInt32(payload, 7);
                
                var result = _appDB.UpdateItemOwner(itemId, ownerId, flag, pos);
                SendDbResponse(DbMsg.DM_UPDATEITEMOWNEREX, result == SERVER_ERROR.SE_OK ? (uint)SERVER_ERROR.SE_OK : (uint)SERVER_ERROR.SE_FAIL);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 批量更新物品所有者错误: {ex.Message}");
                SendDbResponse(DbMsg.DM_UPDATEITEMOWNEREX, (uint)SERVER_ERROR.SE_FAIL);
            }
        }

        private async Task HandleUpdateItemEx(byte[] payload)
        {
            try
            {
                if (payload.Length < 8) return;
                
                uint ownerId = BitConverter.ToUInt32(payload, 0);
                byte flag = payload[4];
                ushort pos = BitConverter.ToUInt16(payload, 5);
                
                byte[] itemData = new byte[payload.Length - 7];
                Array.Copy(payload, 7, itemData, 0, itemData.Length);
                
                var result = _appDB.UpdateItem(ownerId, flag, pos, itemData);
                SendDbResponse(DbMsg.DM_UPDATEITEMEX, result == SERVER_ERROR.SE_OK ? (uint)SERVER_ERROR.SE_OK : (uint)SERVER_ERROR.SE_FAIL);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 批量更新物品错误: {ex.Message}");
                SendDbResponse(DbMsg.DM_UPDATEITEMEX, (uint)SERVER_ERROR.SE_FAIL);
            }
        }

        private async Task HandleUpdateCommunity(byte[] payload)
        {
            try
            {
                if (payload.Length < 5) return;
                
                uint ownerId = BitConverter.ToUInt32(payload, 0);
                string communityData = Encoding.GetEncoding("GBK").GetString(payload, 4, payload.Length - 4).TrimEnd('\0');
                
                var result = _appDB.UpdateCommunity(ownerId, communityData);
                SendDbResponse(DbMsg.DM_UPDATECOMMUNITY, result == SERVER_ERROR.SE_OK ? (uint)SERVER_ERROR.SE_OK : (uint)SERVER_ERROR.SE_FAIL);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 更新社区信息错误: {ex.Message}");
                SendDbResponse(DbMsg.DM_UPDATECOMMUNITY, (uint)SERVER_ERROR.SE_FAIL);
            }
        }

        private async Task HandleQueryCommunity(byte[] payload)
        {
            try
            {
                if (payload.Length < 4) return;
                
                uint ownerId = BitConverter.ToUInt32(payload, 0);
                
                var result = _appDB.QueryCommunity(ownerId, out string communityData);
                if (result == SERVER_ERROR.SE_OK)
                {
                    SendDbResponse(DbMsg.DM_QUERYCOMMUNITY, (uint)SERVER_ERROR.SE_OK, Encoding.GetEncoding("GBK").GetBytes(communityData));
                }
                else
                {
                    SendDbResponse(DbMsg.DM_QUERYCOMMUNITY, (uint)SERVER_ERROR.SE_FAIL);
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 查询社区信息错误: {ex.Message}");
                SendDbResponse(DbMsg.DM_QUERYCOMMUNITY, (uint)SERVER_ERROR.SE_FAIL);
            }
        }

        private async Task HandleBreakFriend(byte[] payload)
        {
            try
            {
                string data = Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
                string[] parts = data.Split('/');
                if (parts.Length < 2) return;

                string friend1 = parts[0];
                string friend2 = parts[1];

                // 需要双向解除好友关系
                var result1 = _appDB.BreakFriend(friend1, friend2);
                var result2 = _appDB.BreakFriend(friend2, friend1);
                
                SendDbResponse(DbMsg.DM_BREAKFRIEND, (result1 == SERVER_ERROR.SE_OK && result2 == SERVER_ERROR.SE_OK) ? 1u : 0u);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 解除好友关系错误: {ex.Message}");
                SendDbResponse(DbMsg.DM_BREAKFRIEND, (uint)SERVER_ERROR.SE_FAIL);
            }
        }

        private async Task HandleBreakMarriage(byte[] payload)
        {
            try
            {
                string data = Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
                string[] parts = data.Split('/');
                if (parts.Length < 2) return;

                string c1 = parts[0];
                string c2 = parts[1];

                // 需要双向解除婚姻关系
                var result1 = _appDB.DeleteMarriage(c1, c2);
                var result2 = _appDB.DeleteMarriage(c2, c1);
                
                SendDbResponse(DbMsg.DM_BREAKMARRIAGE, (result1 == SERVER_ERROR.SE_OK && result2 == SERVER_ERROR.SE_OK) ? 1u : 0u);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 解除婚姻关系错误: {ex.Message}");
                SendDbResponse(DbMsg.DM_BREAKMARRIAGE, (uint)SERVER_ERROR.SE_FAIL);
            }
        }

        private async Task HandleBreakMaster(byte[] payload)
        {
            try
            {
                string data = Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
                string[] parts = data.Split('/');
                if (parts.Length < 2) return;

                string teacher = parts[0];
                string student = parts[1];

                // 需要双向解除师徒关系
                var result1 = _appDB.DeleteStudent(teacher, student);
                var result2 = _appDB.DeleteTeacher(student, teacher);
                
                SendDbResponse(DbMsg.DM_BREAKMASTER, (result1 == SERVER_ERROR.SE_OK && result2 == SERVER_ERROR.SE_OK) ? 1u : 0u);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 解除师徒关系错误: {ex.Message}");
                SendDbResponse(DbMsg.DM_BREAKMASTER, (uint)SERVER_ERROR.SE_FAIL);
            }
        }

        private async Task HandleCacheCharData(byte[] payload)
        {
            try
            {
                SendDbResponse(DbMsg.DM_CACHECHARDATA, (uint)SERVER_ERROR.SE_OK);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 缓存角色数据错误: {ex.Message}");
                SendDbResponse(DbMsg.DM_CACHECHARDATA, (uint)SERVER_ERROR.SE_FAIL);
            }
        }

        private async Task HandleUpgradeItem(byte[] payload)
        {
            try
            {
                if (payload.Length < 8) return;
                
                uint makeIndex = BitConverter.ToUInt32(payload, 0);
                uint upgrade = BitConverter.ToUInt32(payload, 4);
                
                var result = _appDB.UpgradeItem(makeIndex, upgrade);
                SendDbResponse(DbMsg.DM_UPGRADEITEM, result == SERVER_ERROR.SE_OK ? (uint)SERVER_ERROR.SE_OK : (uint)SERVER_ERROR.SE_FAIL);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 升级物品错误: {ex.Message}");
                SendDbResponse(DbMsg.DM_UPGRADEITEM, (uint)SERVER_ERROR.SE_FAIL);
            }
        }

        private async Task HandleRestoreGuildName(byte[] payload)
        {
            try
            {
                string data = Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
                string[] parts = data.Split('/');
                if (parts.Length < 2) return;

                string name = parts[0];
                string guildName = parts[1];

                var result = _appDB.RestoreGuild(name, guildName);
                SendDbResponse(DbMsg.DM_RESTOREGUILDNAME, result == SERVER_ERROR.SE_OK ? (uint)SERVER_ERROR.SE_OK : (uint)SERVER_ERROR.SE_FAIL);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 恢复行会名称错误: {ex.Message}");
                SendDbResponse(DbMsg.DM_RESTOREGUILDNAME, (uint)SERVER_ERROR.SE_FAIL);
            }
        }

        private async Task HandleAddCredit(byte[] payload)
        {
            try
            {
                if (payload.Length < 8) return;
                
                uint count = BitConverter.ToUInt32(payload, 0);
                string name = Encoding.GetEncoding("GBK").GetString(payload, 4, payload.Length - 4).TrimEnd('\0');
                
                var result = _appDB.AddCredit(name, count);
                SendDbResponse(DbMsg.DM_ADDCREDIT, result == SERVER_ERROR.SE_OK ? (uint)SERVER_ERROR.SE_OK : (uint)SERVER_ERROR.SE_FAIL);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 添加信用错误: {ex.Message}");
                SendDbResponse(DbMsg.DM_ADDCREDIT, (uint)SERVER_ERROR.SE_FAIL);
            }
        }

        private async Task HandleCheckFree(byte[] payload)
        {
            try
            {
                SendDbResponse(DbMsg.DM_CHECKFREE, (uint)SERVER_ERROR.SE_OK);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 检查空闲状态错误: {ex.Message}");
                SendDbResponse(DbMsg.DM_CHECKFREE, (uint)SERVER_ERROR.SE_FAIL);
            }
        }

        private async Task HandleDeleteMagic(byte[] payload)
        {
            try
            {
                if (payload.Length < 6) return;
                
                uint ownerId = BitConverter.ToUInt32(payload, 0);
                ushort magicId = BitConverter.ToUInt16(payload, 4);
                
                var result = _appDB.DeleteMagic(ownerId, magicId);
                SendDbResponse(DbMsg.DM_DELETEMAGIC, result == SERVER_ERROR.SE_OK ? (uint)SERVER_ERROR.SE_OK : (uint)SERVER_ERROR.SE_FAIL);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 删除魔法错误: {ex.Message}");
                SendDbResponse(DbMsg.DM_DELETEMAGIC, (uint)SERVER_ERROR.SE_FAIL);
            }
        }

        private async Task HandleUpdateTaskInfo(byte[] payload)
        {
            try
            {
                if (payload.Length < 8) return;
                
                uint ownerId = BitConverter.ToUInt32(payload, 0);
                
                byte[] taskInfoData = new byte[payload.Length - 4];
                Array.Copy(payload, 4, taskInfoData, 0, taskInfoData.Length);
                
                var result = _appDB.UpdateTaskInfo(ownerId, taskInfoData);
                SendDbResponse(DbMsg.DM_UPDATETASKINFO, result == SERVER_ERROR.SE_OK ? (uint)SERVER_ERROR.SE_OK : (uint)SERVER_ERROR.SE_FAIL);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 更新任务信息错误: {ex.Message}");
                SendDbResponse(DbMsg.DM_UPDATETASKINFO, (uint)SERVER_ERROR.SE_FAIL);
            }
        }

        private async Task HandleUpdateAccountState(byte[] payload)
        {
            try
            {
                SendDbResponse(DbMsg.DM_UPDATEACCOUNTSTATE, (uint)SERVER_ERROR.SE_OK);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 更新账号状态错误: {ex.Message}");
                SendDbResponse(DbMsg.DM_UPDATEACCOUNTSTATE, (uint)SERVER_ERROR.SE_FAIL);
            }
        }

        private async Task HandleUpdateItemPosEx2(byte[] payload)
        {
            try
            {
                LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] 处理DM_UPDATEITEMPOSEX2消息，数据长度: {payload.Length}字节");
                SendDbResponse(DbMsg.DM_UPDATEITEMPOSEX2, (uint)SERVER_ERROR.SE_OK);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 处理DM_UPDATEITEMPOSEX2错误: {ex.Message}");
                SendDbResponse(DbMsg.DM_UPDATEITEMPOSEX2, (uint)SERVER_ERROR.SE_FAIL);
            }
        }

        /// <summary>
        /// GameServer使用
        /// </summary>
        /// <param name="payload"></param>
        /// <param name="w1"></param>
        /// <param name="w2"></param>
        /// <param name="w3"></param>
        /// <returns></returns>
        private async Task HandleGetCharDBInfo(byte[] payload, ushort w1, ushort w2, ushort w3)
        {
            string data = Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
            string[] parts = data.Split('/');//lyo：注意，此处使用字符串传data值
            if (parts.Length < 3) return;

            string account = parts[0];
            string serverName = parts[1];
            string name = parts[2];

            var result = _appDB.GetCharDBInfo(account, serverName, name, out byte[] charData);
            if (result == SERVER_ERROR.SE_OK)
            {
                // 使用CHARDBINFO结构序列化数据
                try
                {
                    if (charData.Length >= 4) // 至少包含dwClientKey字段
                    {
                        // 将_clientKey写入charData的前4个字节（dwClientKey字段）
                        byte[] clientKeyBytes = BitConverter.GetBytes(_clientKey);
                        Array.Copy(clientKeyBytes, 0, charData, 0, 4);

                        LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] [DBServer接收] DM_GETCHARDBINFO 设置dwClientKey={_clientKey}到CHARDBINFO数据中");
                    }

                    SendDbResponse(DbMsg.DM_GETCHARDBINFO, (uint)SERVER_ERROR.SE_OK, charData, w1, w2, w3);
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 序列化CHARDBINFO错误: {ex.Message}");
                    // 发送一个空的CHARDBINFO结构（136字节）
                    byte[] emptyCharData = new byte[136]; // CHARDBINFO结构大小
                    SendDbResponse(DbMsg.DM_GETCHARDBINFO, (uint)SERVER_ERROR.SE_FAIL, emptyCharData, w1, w2, w3);
                }
            }
            else
            {
                // 发送一个空的CHARDBINFO结构（136字节），而不是0字节数据
                // 这样GameServer就不会出现"字节数组长度不足"错误
                byte[] emptyCharData = new byte[136]; // CHARDBINFO结构大小
                SendDbResponse(DbMsg.DM_GETCHARDBINFO, (uint)SERVER_ERROR.SE_FAIL, emptyCharData, w1, w2, w3);
                LogManager.Default.Warning($"[{DateTime.Now:HH:mm:ss}] DM_GETCHARDBINFO查询失败: 账号={account}, 服务器={serverName}, 角色名={name}, 结果={result}");
            }
        }

        /// <summary>
        /// GameServer使用
        /// </summary>
        /// <param name="payload"></param>
        /// <param name="w1"></param>
        /// <param name="w2"></param>
        /// <param name="w3"></param>
        /// <returns></returns>
        private async Task HandleQueryItems(byte[] payload, ushort w1, ushort w2, ushort w3)
        {
            // 解析payload：serverId(4) + clientKey(4) + charId(4) + flag(1)
            if (payload.Length < 13)
            {
                LogManager.Default.Warning($"[{DateTime.Now:HH:mm:ss}] [DBServer接收] DM_QUERYITEMS payload长度不足: {payload.Length}字节");
                SendDbResponse(DbMsg.DM_QUERYITEMS, (uint)SERVER_ERROR.SE_FAIL, null, w1, w2, w3);
                return;
            }

            // 解析flag（payload[12]）
            byte flag = payload[12];

            // 使用_charId作为ownerId（从wParam解析的charId）
            uint ownerId = _charId;

            LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] [DBServer接收] DM_QUERYITEMS: ownerId={ownerId}, flag={flag}, clientKey={_clientKey}, charId={_charId}, payload长度={payload.Length}字节");

            // 添加详细日志：解析payload内容
            try
            {
                uint serverId = BitConverter.ToUInt32(payload, 0);
                uint clientKeyFromPayload = BitConverter.ToUInt32(payload, 4);
                uint charIdFromPayload = BitConverter.ToUInt32(payload, 8);
                LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] [DBServer接收] DM_QUERYITEMS payload解析: serverId={serverId}, clientKeyFromPayload={clientKeyFromPayload}, charIdFromPayload={charIdFromPayload}, flag={flag}");

                // 验证clientKey是否匹配
                if (clientKeyFromPayload != _clientKey)
                {
                    LogManager.Default.Warning($"[{DateTime.Now:HH:mm:ss}] [DBServer接收] DM_QUERYITEMS clientKey不匹配: payload中的clientKey={clientKeyFromPayload}, 消息头中的clientKey={_clientKey}");
                }

                // 使用payload中的charId作为ownerId，而不是消息头中的_charId
                // 因为消息头中的_charId可能不是完整的charId（只有低16位）
                ownerId = charIdFromPayload;
                LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] [DBServer接收] DM_QUERYITEMS 使用payload中的charId作为ownerId: {ownerId}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] [DBServer接收] DM_QUERYITEMS 解析payload失败: {ex.Message}");
            }

            var result = _appDB.QueryItems(ownerId, flag, out byte[] itemsData);
            Console.WriteLine($"DM_QUERYITEMS:{result}");

            // 添加详细日志：查询结果
            LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] [DBServer接收] DM_QUERYITEMS 查询结果: result={result}, itemsData长度={(itemsData?.Length ?? 0)}字节");

            if (result == SERVER_ERROR.SE_OK)
            {
                int dbitemSize = System.Runtime.InteropServices.Marshal.SizeOf<MirCommon.Database.DBITEM>();
                int itemCount = (itemsData?.Length ?? 0) / dbitemSize;

                LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] [DBServer接收] DM_QUERYITEMS 发送响应: itemCount={itemCount}, itemsData长度={(itemsData?.Length ?? 0)}字节");

                SendDbResponse(DbMsg.DM_QUERYITEMS, (uint)SERVER_ERROR.SE_OK, itemsData, w1, w2, w3);
            }
            else
            {
                LogManager.Default.Warning($"[{DateTime.Now:HH:mm:ss}] [DBServer接收] DM_QUERYITEMS 查询失败: result={result}");
                SendDbResponse(DbMsg.DM_QUERYITEMS, (uint)SERVER_ERROR.SE_FAIL, null, w1, w2, w3);
            }
        }

        /// <summary>
        /// GameServer使用
        /// </summary>
        /// <param name="payload"></param>
        /// <param name="w1"></param>
        /// <param name="w2"></param>
        /// <param name="w3"></param>
        /// <returns></returns>
        private async Task HandleQueryMagic(byte[] payload, ushort w1, ushort w2, ushort w3)
        {
            // 解析payload：serverId(4) + clientKey(4) + charId(4)
            if (payload.Length < 12)
            {
                LogManager.Default.Warning($"[{DateTime.Now:HH:mm:ss}] [DBServer接收] DM_QUERYMAGIC payload长度不足: {payload.Length}字节");
                SendDbResponse(DbMsg.DM_QUERYMAGIC, (uint)SERVER_ERROR.SE_FAIL, null, w1, w2, w3);
                return;
            }

            // 使用_charId作为ownerId（从wParam解析的charId）
            uint ownerId = _charId;

            LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] [DBServer接收] DM_QUERYMAGIC: ownerId={ownerId}, clientKey={_clientKey}, charId={_charId}");

            var result = _appDB.QueryMagic(ownerId, out byte[] magicData);
            if (result == SERVER_ERROR.SE_OK)
            {
                SendDbResponse(DbMsg.DM_QUERYMAGIC, (uint)SERVER_ERROR.SE_OK, magicData, w1, w2, w3);
            }
            else
            {
                SendDbResponse(DbMsg.DM_QUERYMAGIC, (uint)SERVER_ERROR.SE_FAIL, null, w1, w2, w3);
            }
        }

        /// <summary>
        /// GameServer使用
        /// </summary>
        /// <param name="payload"></param>
        /// <param name="w1"></param>
        /// <param name="w2"></param>
        /// <param name="w3"></param>
        /// <returns></returns>
        private async Task HandleQueryUpgradeItem(byte[] payload, ushort w1, ushort w2, ushort w3)
        {
            try
            {
                // 解析payload：serverId(4) + clientKey(4) + charId(4)
                if (payload.Length < 12)
                {
                    LogManager.Default.Warning($"[{DateTime.Now:HH:mm:ss}] [DBServer接收] DM_QUERYUPGRADEITEM payload长度不足: {payload.Length}字节");
                    SendDbResponse(DbMsg.DM_QUERYUPGRADEITEM, (uint)SERVER_ERROR.SE_FAIL, null, w1, w2, w3);
                    return;
                }

                // 使用_charId作为ownerId（从wParam解析的charId）
                uint ownerId = _charId;

                LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] [DBServer接收] DM_QUERYUPGRADEITEM: ownerId={ownerId}, clientKey={_clientKey}, charId={_charId}");

                // 调用QueryUpgradeItem方法查询升级物品
                var result = _appDB.QueryUpgradeItem(ownerId, out byte[] upgradeItemData);
                if (result == SERVER_ERROR.SE_OK)
                {
                    SendDbResponse(DbMsg.DM_QUERYUPGRADEITEM, (uint)SERVER_ERROR.SE_OK, upgradeItemData, w1, w2, w3);
                }
                else
                {
                    SendDbResponse(DbMsg.DM_QUERYUPGRADEITEM, (uint)SERVER_ERROR.SE_FAIL, null, w1, w2, w3);
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 查询升级物品错误: {ex.Message}");
                SendDbResponse(DbMsg.DM_QUERYUPGRADEITEM, (uint)SERVER_ERROR.SE_FAIL, null, w1, w2, w3);
            }
        }

        /// <summary>
        /// GameServer使用
        /// </summary>
        /// <param name="payload"></param>
        /// <param name="w1"></param>
        /// <param name="w2"></param>
        /// <param name="w3"></param>
        /// <returns></returns>
        private async Task HandleQueryTaskInfo(byte[] payload, ushort w1, ushort w2, ushort w3)
        {
            try
            {
                // 解析payload：serverId(4) + clientKey(4) + charId(4)
                if (payload.Length < 12)
                {
                    LogManager.Default.Warning($"[{DateTime.Now:HH:mm:ss}] [DBServer接收] DM_QUERYTASKINFO payload长度不足: {payload.Length}字节");
                    SendDbResponse(DbMsg.DM_QUERYTASKINFO, (uint)SERVER_ERROR.SE_FAIL, null, w1, w2, w3);
                    return;
                }

                // 使用_charId作为ownerId（从wParam解析的charId）
                uint ownerId = _charId;

                LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] [DBServer接收] DM_QUERYTASKINFO: ownerId={ownerId}, clientKey={_clientKey}, charId={_charId}");

                // 调用QueryTaskInfo方法查询任务信息
                var result = _appDB.QueryTaskInfo(ownerId, out byte[] taskInfoData);
                if (result == SERVER_ERROR.SE_OK)
                {
                    SendDbResponse(DbMsg.DM_QUERYTASKINFO, (uint)SERVER_ERROR.SE_OK, taskInfoData, w1, w2, w3);
                }
                else
                {
                    SendDbResponse(DbMsg.DM_QUERYTASKINFO, (uint)SERVER_ERROR.SE_FAIL, null, w1, w2, w3);
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] 查询任务信息错误: {ex.Message}");
                SendDbResponse(DbMsg.DM_QUERYTASKINFO, (uint)SERVER_ERROR.SE_FAIL, null, w1, w2, w3);
            }
        }

        private void SendDbResponse(DbMsg msgType, uint result, byte[]? data = null)
        {
            SendDbResponse(msgType, result, data, 0, 0, 0);
        }

        /// <summary>
        /// 带wparam的发送（适用于带wparam的请求）
        /// </summary>
        /// <param name="msgType"></param>
        /// <param name="result"></param>
        /// <param name="data"></param>
        /// <param name="wParam1"></param>
        /// <param name="wParam2"></param>
        /// <param name="wParam3"></param>
        private void SendDbResponse(DbMsg msgType, uint result, byte[]? data, ushort wParam1, ushort wParam2, ushort wParam3)
        {
            try
            {
                // 添加详细日志：发送前的数据包信息
                LogManager.Default.Debug($"[{DateTime.Now:HH:mm:ss}] [DBServer发送] 消息类型: {msgType}, 结果: {result}, 数据长度: {data?.Length ?? 0}字节");
                LogManager.Default.Debug($"[{DateTime.Now:HH:mm:ss}] [DBServer发送] 使用clientKey={_clientKey}, charId={_charId}");
                LogManager.Default.Debug($"[{DateTime.Now:HH:mm:ss}] [DBServer发送] 自定义wParam: w1={wParam1}, w2={wParam2}, w3={wParam3}");
                
                // 使用GameCodec.EncodeMsg编码消息（包含'#'和'!'）
                // 将clientKey和charId设置到wParam中，与DBServerClient.cs中的编码方式一致：
                // wParam1 = clientKey的低16位
                // wParam2 = clientKey的高16位
                // wParam3 = charId的低16位
                ushort w1 = (ushort)(_clientKey & 0xFFFF);
                ushort w2 = (ushort)((_clientKey >> 16) & 0xFFFF);
                ushort w3 = (ushort)(_charId & 0xFFFF);
                
                // 如果提供了自定义的wParam参数，则使用它们
                if (wParam1 != 0 || wParam2 != 0 || wParam3 != 0)
                {
                    w1 = wParam1;
                    w2 = wParam2;
                    w3 = wParam3;
                    LogManager.Default.Debug($"[{DateTime.Now:HH:mm:ss}] [DBServer发送] 使用自定义wParam参数");
                }
                else
                {
                    // 对于DM_GETCHARDBINFO消息，需要设置正确的wParam参数
                    // 因为GameClient期望在wParam中收到clientKey
                    if (msgType == DbMsg.DM_GETCHARDBINFO)
                    {
                        w1 = (ushort)(_clientKey & 0xFFFF);
                        w2 = (ushort)((_clientKey >> 16) & 0xFFFF);
                        w3 = 0; // charId在数据中，这里设为0
                        LogManager.Default.Debug($"[{DateTime.Now:HH:mm:ss}] [DBServer发送] DM_GETCHARDBINFO使用clientKey设置wParam: w1={w1}, w2={w2}, w3={w3}");
                    }
                }
                
                byte[] encoded = new byte[8192];
                int encodedSize = GameCodec.EncodeMsg(encoded, result, (ushort)msgType, w1, w2, w3, data, data?.Length ?? 0);
                
                // 添加详细日志：编码后的数据包信息
                LogManager.Default.Debug($"[{DateTime.Now:HH:mm:ss}] [DBServer发送] 编码后长度: {encodedSize}字节, w1={w1}, w2={w2}, w3={w3}");
                
                _stream.Write(encoded, 0, encodedSize);
                _stream.Flush();
                
                // 添加详细日志：发送成功
                LogManager.Default.Debug($"[{DateTime.Now:HH:mm:ss}] [DBServer发送] 发送成功: {msgType}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] [DBServer发送] 发送数据库响应失败: {ex.Message}");
            }
        }

        public void Close()
        {
            try
            {
                _stream?.Close();
                _client?.Close();
            }
            catch { }
        }
    }
}
