using Microsoft.Data.Sqlite;
using MirCommon;
using MirCommon.Database;
using MirCommon.Utils;
using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace DBServer
{
    /// <summary>
    /// 错误处理器，提供自动重连和错误恢复机制
    /// </summary>
    public class ErrorHandler
    {
        private readonly string _connectionString;
        private readonly DatabaseType _databaseType;
        private readonly int _maxRetryCount;
        private readonly TimeSpan _retryDelay;
        private readonly TimeSpan _connectionTimeout;
        private int _currentRetryCount;
        private DateTime _lastErrorTime;
        private readonly object _lock = new object();

        public ErrorHandler(string connectionString, int maxRetryCount = 3, TimeSpan? retryDelay = null, TimeSpan? connectionTimeout = null)
            : this(connectionString, DatabaseType.SqlServer, maxRetryCount, retryDelay, connectionTimeout)
        {
        }

        public ErrorHandler(string connectionString, DatabaseType databaseType, int maxRetryCount = 3, TimeSpan? retryDelay = null, TimeSpan? connectionTimeout = null)
        {
            _connectionString = connectionString;
            _databaseType = databaseType;
            _maxRetryCount = maxRetryCount;
            _retryDelay = retryDelay ?? TimeSpan.FromSeconds(5);
            _connectionTimeout = connectionTimeout ?? TimeSpan.FromSeconds(30);
            _currentRetryCount = 0;
            _lastErrorTime = DateTime.MinValue;
        }

        /// <summary>
        /// 执行数据库操作，支持自动重试
        /// </summary>
        public async Task<MirCommon.SERVER_ERROR> ExecuteWithRetryAsync(Func<Task<MirCommon.SERVER_ERROR>> operation, string operationName = "")
        {
            MirCommon.SERVER_ERROR lastError = MirCommon.SERVER_ERROR.SE_FAIL;
            
            for (int attempt = 1; attempt <= _maxRetryCount; attempt++)
            {
                try
                {
                    var result = await operation();
                    
                    if (result == MirCommon.SERVER_ERROR.SE_OK)
                    {
                        // 操作成功，重置重试计数
                        lock (_lock)
                        {
                            _currentRetryCount = 0;
                        }
                        return result;
                    }
                    
                    // 如果是业务逻辑错误，不重试
                    if (IsBusinessError(result))
                    {
                        return result;
                    }
                    
                    lastError = result;
                    LogManager.Default.Warning($"[{DateTime.Now:HH:mm:ss}] {operationName} 操作失败 (尝试 {attempt}/{_maxRetryCount}): {GetErrorDescription(result)}");
                }
                catch (SqlException sqlEx)
                {
                    lastError = MapSqlExceptionToServerError(sqlEx);
                    LogManager.Default.Warning($"[{DateTime.Now:HH:mm:ss}] {operationName} SQL异常 (尝试 {attempt}/{_maxRetryCount}): {sqlEx.Message}");
                    
                    // 如果是连接相关错误，尝试重连
                    if (IsConnectionError(sqlEx))
                    {
                        LogManager.Default.Warning($"[{DateTime.Now:HH:mm:ss}] 检测到连接错误，将在 {_retryDelay.TotalSeconds} 秒后重试...");
                    }
                }
                catch (MySqlException mySqlEx)
                {
                    lastError = MapMySqlExceptionToServerError(mySqlEx);
                    LogManager.Default.Warning($"[{DateTime.Now:HH:mm:ss}] {operationName} MySQL异常 (尝试 {attempt}/{_maxRetryCount}): {mySqlEx.Message}");
                    
                    // 如果是连接相关错误，尝试重连
                    if (IsMySqlConnectionError(mySqlEx))
                    {
                        LogManager.Default.Warning($"[{DateTime.Now:HH:mm:ss}] 检测到连接错误，将在 {_retryDelay.TotalSeconds} 秒后重试...");
                    }
                }
                catch (SqliteException sqliteEx)
                {
                    lastError = MapSqliteExceptionToServerError(sqliteEx);
                    LogManager.Default.Warning($"[{DateTime.Now:HH:mm:ss}] {operationName} SQLite异常 (尝试 {attempt}/{_maxRetryCount}): {sqliteEx.Message}");
                    
                    // 如果是连接相关错误，尝试重连
                    if (IsSqliteConnectionError(sqliteEx))
                    {
                        LogManager.Default.Warning($"[{DateTime.Now:HH:mm:ss}] 检测到连接错误，将在 {_retryDelay.TotalSeconds} 秒后重试...");
                    }
                }
                catch (Exception ex)
                {
                    lastError = MirCommon.SERVER_ERROR.SE_FAIL;
                    LogManager.Default.Warning($"[{DateTime.Now:HH:mm:ss}] {operationName} 异常 (尝试 {attempt}/{_maxRetryCount}): {ex.Message}");
                }

                // 如果不是最后一次尝试，等待后重试
                if (attempt < _maxRetryCount)
                {
                    await Task.Delay(_retryDelay);
                }
            }

            // 所有重试都失败
            lock (_lock)
            {
                _currentRetryCount++;
                _lastErrorTime = DateTime.Now;
            }
            
            LogManager.Default.Error($"[{DateTime.Now:HH:mm:ss}] {operationName} 操作失败，已达到最大重试次数 ({_maxRetryCount})");
            return lastError;
        }

        /// <summary>
        /// 同步执行数据库操作，支持自动重试
        /// </summary>
        public MirCommon.SERVER_ERROR ExecuteWithRetry(Func<MirCommon.SERVER_ERROR> operation, string operationName = "")
        {
            return ExecuteWithRetryAsync(() => Task.FromResult(operation()), operationName).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 检查连接是否健康
        /// </summary>
        public async Task<bool> CheckConnectionHealthAsync()
        {
            try
            {
                switch (_databaseType)
                {
                    case DatabaseType.SQLite:
                        using (var connection = new SqliteConnection(_connectionString))
                        {
                            await connection.OpenAsync();
                            using var command = new SqliteCommand("SELECT 1", connection);
                            command.CommandTimeout = (int)_connectionTimeout.TotalSeconds;
                            var result = await command.ExecuteScalarAsync();
                            return result != null && Convert.ToInt32(result) == 1;
                        }
                        
                    case DatabaseType.MySQL:
                        using (var connection = new MySqlConnection(_connectionString))
                        {
                            await connection.OpenAsync();
                            using var command = new MySqlCommand("SELECT 1", connection);
                            command.CommandTimeout = (int)_connectionTimeout.TotalSeconds;
                            var result = await command.ExecuteScalarAsync();
                            return result != null && Convert.ToInt32(result) == 1;
                        }
                        
                    case DatabaseType.SqlServer:
                    default:
                        using (var connection = new SqlConnection(_connectionString))
                        {
                            await connection.OpenAsync();
                            using var command = new SqlCommand("SELECT 1", connection);
                            command.CommandTimeout = (int)_connectionTimeout.TotalSeconds;
                            var result = await command.ExecuteScalarAsync();
                            return result != null && Convert.ToInt32(result) == 1;
                        }
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取错误统计信息
        /// </summary>
        public ErrorStatistics GetErrorStatistics()
        {
            lock (_lock)
            {
                return new ErrorStatistics
                {
                    CurrentRetryCount = _currentRetryCount,
                    LastErrorTime = _lastErrorTime,
                    MaxRetryCount = _maxRetryCount,
                    IsHealthy = _currentRetryCount == 0
                };
            }
        }

        /// <summary>
        /// 重置错误统计
        /// </summary>
        public void ResetErrorStatistics()
        {
            lock (_lock)
            {
                _currentRetryCount = 0;
                _lastErrorTime = DateTime.MinValue;
            }
        }

        /// <summary>
        /// 判断是否为业务逻辑错误（不需要重试）
        /// </summary>
        private bool IsBusinessError(MirCommon.SERVER_ERROR error)
        {
            switch (error)
            {
                case MirCommon.SERVER_ERROR.SE_LOGIN_ACCOUNTEXIST:
                case MirCommon.SERVER_ERROR.SE_LOGIN_ACCOUNTNOTEXIST:
                case MirCommon.SERVER_ERROR.SE_LOGIN_PASSWORDERROR:
                case MirCommon.SERVER_ERROR.SE_SELCHAR_CHAREXIST:
                case MirCommon.SERVER_ERROR.SE_SELCHAR_NOTEXIST:
                case MirCommon.SERVER_ERROR.SE_REG_INVALIDACCOUNT:
                case MirCommon.SERVER_ERROR.SE_REG_INVALIDPASSWORD:
                case MirCommon.SERVER_ERROR.SE_REG_INVALIDNAME:
                case MirCommon.SERVER_ERROR.SE_REG_INVALIDBIRTHDAY:
                case MirCommon.SERVER_ERROR.SE_REG_INVALIDPHONENUMBER:
                case MirCommon.SERVER_ERROR.SE_REG_INVALIDMOBILEPHONE:
                case MirCommon.SERVER_ERROR.SE_REG_INVALIDQUESTION:
                case MirCommon.SERVER_ERROR.SE_REG_INVALIDANSWER:
                case MirCommon.SERVER_ERROR.SE_REG_INVALIDIDCARD:
                case MirCommon.SERVER_ERROR.SE_REG_INVALIDEMAIL:
                case MirCommon.SERVER_ERROR.SE_CREATECHARACTER_INVALID_CHARNAME:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 判断是否为连接错误
        /// </summary>
        private bool IsConnectionError(SqlException ex)
        {
            // SQL Server 错误代码
            // -2: 超时
            // 20: 实例不可用
            // 53: 找不到服务器
            // 121: 信号量超时
            // 233: 客户端无法建立连接
            // 4060: 无法打开数据库
            // 18456: 登录失败
            int[] connectionErrorCodes = { -2, 20, 53, 121, 233, 4060, 18456 };
            
            return Array.Exists(connectionErrorCodes, code => code == ex.Number);
        }

        /// <summary>
        /// 将SQL异常映射到SERVER_ERROR
        /// </summary>
        private MirCommon.SERVER_ERROR MapSqlExceptionToServerError(SqlException ex)
        {
            switch (ex.Number)
            {
                case 18456: // 登录失败
                    return MirCommon.SERVER_ERROR.SE_LOGIN_PASSWORDERROR;
                case 4060: // 无法打开数据库
                    return MirCommon.SERVER_ERROR.SE_DB_NOTINITED;
                case -2: // 超时
                case 121: // 信号量超时
                    return MirCommon.SERVER_ERROR.SE_ODBC_SQLEXECDIRECTFAIL;
                case 53: // 找不到服务器
                case 233: // 客户端无法建立连接
                    return MirCommon.SERVER_ERROR.SE_ODBC_SQLCONNECTFAIL;
                default:
                    return MirCommon.SERVER_ERROR.SE_FAIL;
            }
        }

        /// <summary>
        /// 判断是否为MySQL连接错误
        /// </summary>
        private bool IsMySqlConnectionError(MySqlException ex)
        {
            // MySQL 错误代码
            // 1042: 无法连接到服务器
            // 1043: 错误的握手
            // 1044: 拒绝访问数据库
            // 1045: 拒绝访问（错误的用户名或密码）
            // 2002: 无法通过套接字连接到本地MySQL服务器
            // 2003: 无法连接到MySQL服务器
            // 2006: MySQL服务器已断开连接
            // 2013: 查询期间丢失与MySQL服务器的连接
            uint[] connectionErrorCodes = { 1042, 1043, 1044, 1045, 2002, 2003, 2006, 2013 };
            
            return Array.Exists(connectionErrorCodes, code => code == ex.Number);
        }

        /// <summary>
        /// 将MySQL异常映射到SERVER_ERROR
        /// </summary>
        private MirCommon.SERVER_ERROR MapMySqlExceptionToServerError(MySqlException ex)
        {
            switch (ex.Number)
            {
                case 1045: // 拒绝访问（错误的用户名或密码）
                    return MirCommon.SERVER_ERROR.SE_LOGIN_PASSWORDERROR;
                case 1044: // 拒绝访问数据库
                case 1049: // 未知数据库
                    return MirCommon.SERVER_ERROR.SE_DB_NOTINITED;
                case 2002: // 无法通过套接字连接到本地MySQL服务器
                case 2003: // 无法连接到MySQL服务器
                    return MirCommon.SERVER_ERROR.SE_ODBC_SQLCONNECTFAIL;
                case 2006: // MySQL服务器已断开连接
                case 2013: // 查询期间丢失与MySQL服务器的连接
                    return MirCommon.SERVER_ERROR.SE_ODBC_SQLEXECDIRECTFAIL;
                default:
                    return MirCommon.SERVER_ERROR.SE_FAIL;
            }
        }

        /// <summary>
        /// 判断是否为SQLite连接错误
        /// </summary>
        private bool IsSqliteConnectionError(SqliteException ex)
        {
            // SQLite 错误代码
            // 1: SQL错误或丢失数据库
            // 14: 无法打开数据库文件
            // 21: 数据库磁盘映像格式错误
            // 26: 数据库文件被锁定
            int[] connectionErrorCodes = { 1, 14, 21, 26 };
            
            return Array.Exists(connectionErrorCodes, code => code == ex.SqliteErrorCode);
        }

        /// <summary>
        /// 将SQLite异常映射到SERVER_ERROR
        /// </summary>
        private MirCommon.SERVER_ERROR MapSqliteExceptionToServerError(SqliteException ex)
        {
            switch (ex.SqliteErrorCode)
            {
                case 1: // SQL错误或丢失数据库
                case 14: // 无法打开数据库文件
                    return MirCommon.SERVER_ERROR.SE_DB_NOTINITED;
                case 26: // 数据库文件被锁定
                    return MirCommon.SERVER_ERROR.SE_ODBC_SQLEXECDIRECTFAIL;
                default:
                    return MirCommon.SERVER_ERROR.SE_FAIL;
            }
        }

        /// <summary>
        /// 获取错误描述
        /// </summary>
        private string GetErrorDescription(MirCommon.SERVER_ERROR error)
        {
            return error switch
            {
                MirCommon.SERVER_ERROR.SE_OK => "操作成功",
                MirCommon.SERVER_ERROR.SE_FAIL => "操作失败",
                MirCommon.SERVER_ERROR.SE_ALLOCMEMORYFAIL => "内存分配失败",
                MirCommon.SERVER_ERROR.SE_DB_NOMOREDATA => "没有更多数据",
                MirCommon.SERVER_ERROR.SE_DB_NOTINITED => "数据库未初始化",
                MirCommon.SERVER_ERROR.SE_LOGIN_ACCOUNTEXIST => "账号已存在",
                MirCommon.SERVER_ERROR.SE_LOGIN_ACCOUNTNOTEXIST => "账号不存在",
                MirCommon.SERVER_ERROR.SE_LOGIN_PASSWORDERROR => "密码错误",
                MirCommon.SERVER_ERROR.SE_SELCHAR_CHAREXIST => "角色已存在",
                MirCommon.SERVER_ERROR.SE_SELCHAR_NOTEXIST => "角色不存在",
                MirCommon.SERVER_ERROR.SE_REG_INVALIDACCOUNT => "无效的账号",
                MirCommon.SERVER_ERROR.SE_REG_INVALIDPASSWORD => "无效的密码",
                MirCommon.SERVER_ERROR.SE_REG_INVALIDNAME => "无效的名字",
                MirCommon.SERVER_ERROR.SE_REG_INVALIDBIRTHDAY => "无效的生日",
                MirCommon.SERVER_ERROR.SE_REG_INVALIDPHONENUMBER => "无效的电话号码",
                MirCommon.SERVER_ERROR.SE_REG_INVALIDMOBILEPHONE => "无效的手机号码",
                MirCommon.SERVER_ERROR.SE_REG_INVALIDQUESTION => "无效的问题",
                MirCommon.SERVER_ERROR.SE_REG_INVALIDANSWER => "无效的答案",
                MirCommon.SERVER_ERROR.SE_REG_INVALIDIDCARD => "无效的身份证",
                MirCommon.SERVER_ERROR.SE_REG_INVALIDEMAIL => "无效的邮箱",
                MirCommon.SERVER_ERROR.SE_CREATECHARACTER_INVALID_CHARNAME => "无效的角色名",
                MirCommon.SERVER_ERROR.SE_ODBC_SQLCONNECTFAIL => "数据库连接失败",
                MirCommon.SERVER_ERROR.SE_ODBC_SQLEXECDIRECTFAIL => "SQL执行失败",
                _ => $"未知错误: {error}"
            };
        }
    }

    /// <summary>
    /// 错误统计信息
    /// </summary>
    public class ErrorStatistics
    {
        public int CurrentRetryCount { get; set; }
        public DateTime LastErrorTime { get; set; }
        public int MaxRetryCount { get; set; }
        public bool IsHealthy { get; set; }
        
        // 添加Program.cs中使用的属性
        public int TotalErrors { get; set; }
        public int ConnectionErrors { get; set; }
        public int QueryErrors { get; set; }
        public int TimeoutErrors { get; set; }
        public int RetrySuccesses { get; set; }
        public int RetryFailures { get; set; }
        public string LastErrorMessage { get; set; } = string.Empty;
        
        public TimeSpan TimeSinceLastError => DateTime.Now - LastErrorTime;
        
        public override string ToString()
        {
            return $"错误统计: 当前重试次数={CurrentRetryCount}, 最大重试次数={MaxRetryCount}, " +
                   $"最后错误时间={LastErrorTime:yyyy-MM-dd HH:mm:ss}, 距离最后错误={TimeSinceLastError:hh\\:mm\\:ss}, " +
                   $"健康状态={(IsHealthy ? "健康" : "异常")}";
        }
    }

    /// <summary>
    /// 连接健康检查器
    /// </summary>
    public class ConnectionHealthChecker
    {
        private readonly ErrorHandler _errorHandler;
        private readonly TimeSpan _checkInterval;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _healthCheckTask;
        private bool _isRunning;

        public event EventHandler<ConnectionHealthChangedEventArgs> ConnectionHealthChanged;

        public ConnectionHealthChecker(ErrorHandler errorHandler, TimeSpan checkInterval)
        {
            _errorHandler = errorHandler;
            _checkInterval = checkInterval;
            _isRunning = false;
        }

        /// <summary>
        /// 启动健康检查
        /// </summary>
        public void Start()
        {
            if (_isRunning) return;
            
            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();
            _healthCheckTask = Task.Run(() => HealthCheckLoop(_cancellationTokenSource.Token));
            
            LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] 连接健康检查已启动，检查间隔: {_checkInterval.TotalSeconds}秒");
        }

        /// <summary>
        /// 停止健康检查
        /// </summary>
        public void Stop()
        {
            if (!_isRunning) return;
            
            _isRunning = false;
            _cancellationTokenSource?.Cancel();
            _healthCheckTask?.Wait(TimeSpan.FromSeconds(5));
            
            LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] 连接健康检查已停止");
        }

        private async Task HealthCheckLoop(CancellationToken cancellationToken)
        {
            bool lastHealthStatus = true;
            
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    bool isHealthy = await _errorHandler.CheckConnectionHealthAsync();
                    
                    if (isHealthy != lastHealthStatus)
                    {
                        lastHealthStatus = isHealthy;
                        OnConnectionHealthChanged(new ConnectionHealthChangedEventArgs(isHealthy));
                        
                        if (isHealthy)
                        {
                            LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] 连接健康检查: 连接已恢复");
                            _errorHandler.ResetErrorStatistics();
                        }
                        else
                        {
                            LogManager.Default.Warning($"[{DateTime.Now:HH:mm:ss}] 连接健康检查: 连接异常");
                        }
                    }
                    
                    var stats = _errorHandler.GetErrorStatistics();
                    if (!stats.IsHealthy)
                    {
                        LogManager.Default.Info($"[{DateTime.Now:HH:mm:ss}] 连接健康检查: {stats}");
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Default.Warning($"[{DateTime.Now:HH:mm:ss}] 连接健康检查异常: {ex.Message}");
                }

                try
                {
                    await Task.Delay(_checkInterval, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        protected virtual void OnConnectionHealthChanged(ConnectionHealthChangedEventArgs e)
        {
            ConnectionHealthChanged?.Invoke(this, e);
        }
    }

    /// <summary>
    /// 连接健康变化事件参数
    /// </summary>
    public class ConnectionHealthChangedEventArgs : EventArgs
    {
        public bool IsHealthy { get; }

        public ConnectionHealthChangedEventArgs(bool isHealthy)
        {
            IsHealthy = isHealthy;
        }
    }
}
