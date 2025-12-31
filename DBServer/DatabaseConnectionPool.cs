using System;
using System.Collections.Concurrent;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using MirCommon.Utils;

namespace DBServer
{
    /// <summary>
    /// 数据库连接池管理器
    /// </summary>
    public class DatabaseConnectionPool : IDisposable
    {
        private readonly string _connectionString;
        private readonly int _maxConnections;
        private readonly ConcurrentBag<SqlConnection> _connections;
        private readonly SemaphoreSlim _semaphore;
        private readonly Timer _healthCheckTimer;
        private readonly object _lock = new();
        private bool _disposed;
        private int _activeConnections;
        
        // 监控指标
        private long _totalConnectionsCreated;
        private long _totalConnectionsReused;
        private long _totalConnectionErrors;
        private DateTime _startTime;
        
        public DatabaseConnectionPool(string connectionString, int maxConnections = 100)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _maxConnections = maxConnections;
            _connections = new ConcurrentBag<SqlConnection>();
            _semaphore = new SemaphoreSlim(maxConnections, maxConnections);
            _activeConnections = 0;
            _totalConnectionsCreated = 0;
            _totalConnectionsReused = 0;
            _totalConnectionErrors = 0;
            _startTime = DateTime.Now;
            
            // 启动健康检查定时器（每5分钟检查一次）
            _healthCheckTimer = new Timer(HealthCheckCallback, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
            
            LogManager.Default.Info($"数据库连接池初始化完成，最大连接数: {maxConnections}");
        }
        
        /// <summary>
        /// 获取数据库连接
        /// </summary>
        public async Task<SqlConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            
            try
            {
                Interlocked.Increment(ref _activeConnections);
                
                // 尝试从池中获取连接
                if (_connections.TryTake(out var connection))
                {
                    Interlocked.Increment(ref _totalConnectionsReused);
                    
                    // 检查连接是否仍然有效
                    if (connection.State == ConnectionState.Open)
                    {
                        return connection;
                    }
                    else
                    {
                        // 连接已关闭，创建新连接
                        connection.Dispose();
                        Interlocked.Decrement(ref _totalConnectionsReused);
                    }
                }
                
                // 创建新连接
                Interlocked.Increment(ref _totalConnectionsCreated);
                connection = new SqlConnection(_connectionString);
                
                try
                {
                    await connection.OpenAsync(cancellationToken);
                    LogManager.Default.Debug($"创建新的数据库连接，当前活跃连接数: {_activeConnections}");
                    return connection;
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref _totalConnectionErrors);
                    LogManager.Default.Error($"创建数据库连接失败: {ex.Message}");
                    connection.Dispose();
                    throw;
                }
            }
            catch
            {
                Interlocked.Decrement(ref _activeConnections);
                _semaphore.Release();
                throw;
            }
        }
        
        /// <summary>
        /// 释放连接回连接池
        /// </summary>
        public void ReleaseConnection(SqlConnection connection)
        {
            if (connection == null)
                return;
                
            try
            {
                // 如果连接仍然有效，放回池中
                if (connection.State == ConnectionState.Open)
                {
                    _connections.Add(connection);
                }
                else
                {
                    // 连接已关闭，直接释放
                    connection.Dispose();
                }
            }
            finally
            {
                Interlocked.Decrement(ref _activeConnections);
                _semaphore.Release();
            }
        }
        
        /// <summary>
        /// 执行数据库操作（自动管理连接）
        /// </summary>
        public async Task<T> ExecuteWithConnectionAsync<T>(
            Func<SqlConnection, Task<T>> operation,
            CancellationToken cancellationToken = default)
        {
            var connection = await GetConnectionAsync(cancellationToken);
            
            try
            {
                return await operation(connection);
            }
            finally
            {
                ReleaseConnection(connection);
            }
        }
        
        /// <summary>
        /// 执行数据库操作（自动管理连接）
        /// </summary>
        public async Task ExecuteWithConnectionAsync(
            Func<SqlConnection, Task> operation,
            CancellationToken cancellationToken = default)
        {
            var connection = await GetConnectionAsync(cancellationToken);
            
            try
            {
                await operation(connection);
            }
            finally
            {
                ReleaseConnection(connection);
            }
        }
        
        /// <summary>
        /// 健康检查回调
        /// </summary>
        private void HealthCheckCallback(object state)
        {
            try
            {
                lock (_lock)
                {
                    var connectionsToRemove = new List<SqlConnection>();
                    
                    // 检查所有连接的健康状态
                    while (_connections.TryTake(out var connection))
                    {
                        try
                        {
                            // 执行简单的健康检查查询
                            using (var cmd = new SqlCommand("SELECT 1", connection))
                            {
                                if (connection.State != ConnectionState.Open)
                                {
                                    connectionsToRemove.Add(connection);
                                    continue;
                                }
                                
                                var result = cmd.ExecuteScalar();
                                if (result == null || (int)result != 1)
                                {
                                    connectionsToRemove.Add(connection);
                                    continue;
                                }
                            }
                            
                            // 连接健康，放回池中
                            _connections.Add(connection);
                        }
                        catch
                        {
                            // 连接不健康，释放
                            connectionsToRemove.Add(connection);
                        }
                    }
                    
                    // 释放不健康的连接
                    foreach (var connection in connectionsToRemove)
                    {
                        connection.Dispose();
                    }
                    
                    if (connectionsToRemove.Count > 0)
                    {
                        LogManager.Default.Warning($"数据库连接池健康检查移除了 {connectionsToRemove.Count} 个不健康的连接");
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"数据库连接池健康检查失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 获取连接池统计信息
        /// </summary>
        public ConnectionPoolStats GetStats()
        {
            return new ConnectionPoolStats
            {
                TotalConnectionsCreated = _totalConnectionsCreated,
                TotalConnectionsReused = _totalConnectionsReused,
                TotalConnectionErrors = _totalConnectionErrors,
                ActiveConnections = _activeConnections,
                AvailableConnections = _connections.Count,
                MaxConnections = _maxConnections,
                Uptime = DateTime.Now - _startTime
            };
        }
        
        /// <summary>
        /// 清理连接池
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                while (_connections.TryTake(out var connection))
                {
                    connection.Dispose();
                }
                
                LogManager.Default.Info("数据库连接池已清理");
            }
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _healthCheckTimer?.Dispose();
                Clear();
                _semaphore?.Dispose();
                
                LogManager.Default.Info("数据库连接池已释放");
            }
        }
    }
    
    /// <summary>
    /// 连接池统计信息
    /// </summary>
    public class ConnectionPoolStats
    {
        public long TotalConnectionsCreated { get; set; }
        public long TotalConnectionsReused { get; set; }
        public long TotalConnectionErrors { get; set; }
        public int ActiveConnections { get; set; }
        public int AvailableConnections { get; set; }
        public int MaxConnections { get; set; }
        public TimeSpan Uptime { get; set; }
        
        public double ConnectionReuseRate => TotalConnectionsCreated > 0 
            ? (double)TotalConnectionsReused / TotalConnectionsCreated 
            : 0;
            
        public double ErrorRate => TotalConnectionsCreated > 0 
            ? (double)TotalConnectionErrors / TotalConnectionsCreated 
            : 0;
            
        public override string ToString()
        {
            return $"连接池统计: 创建={TotalConnectionsCreated}, 重用={TotalConnectionsReused}, " +
                   $"活跃={ActiveConnections}, 可用={AvailableConnections}, 最大={MaxConnections}, " +
                   $"运行时间={Uptime:hh\\:mm\\:ss}, 重用率={ConnectionReuseRate:P2}, 错误率={ErrorRate:P2}";
        }
    }
    
    /// <summary>
    /// 查询缓存管理器
    /// </summary>
    public class QueryCacheManager
    {
        private readonly ConcurrentDictionary<string, CacheEntry> _cache;
        private readonly TimeSpan _defaultExpiration;
        private readonly Timer _cleanupTimer;
        private readonly object _lock = new();
        
        public QueryCacheManager(TimeSpan defaultExpiration = default)
        {
            _cache = new ConcurrentDictionary<string, CacheEntry>();
            _defaultExpiration = defaultExpiration == default ? TimeSpan.FromMinutes(5) : defaultExpiration;
            
            // 启动清理定时器（每分钟清理一次过期缓存）
            _cleanupTimer = new Timer(CleanupCallback, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }
        
        /// <summary>
        /// 获取缓存数据
        /// </summary>
        public bool TryGet<T>(string key, out T value)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (entry.ExpirationTime > DateTime.Now)
                {
                    value = (T)entry.Value;
                    entry.LastAccessTime = DateTime.Now;
                    return true;
                }
                else
                {
                    // 缓存已过期，移除
                    _cache.TryRemove(key, out _);
                }
            }
            
            value = default;
            return false;
        }
        
        /// <summary>
        /// 设置缓存数据
        /// </summary>
        public void Set<T>(string key, T value, TimeSpan? expiration = null)
        {
            var entry = new CacheEntry
            {
                Key = key,
                Value = value,
                CreationTime = DateTime.Now,
                LastAccessTime = DateTime.Now,
                ExpirationTime = DateTime.Now + (expiration ?? _defaultExpiration)
            };
            
            _cache[key] = entry;
        }
        
        /// <summary>
        /// 移除缓存数据
        /// </summary>
        public bool Remove(string key)
        {
            return _cache.TryRemove(key, out _);
        }
        
        /// <summary>
        /// 清理过期缓存
        /// </summary>
        private void CleanupCallback(object state)
        {
            try
            {
                var now = DateTime.Now;
                var keysToRemove = new List<string>();
                
                foreach (var kvp in _cache)
                {
                    if (kvp.Value.ExpirationTime <= now)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }
                
                foreach (var key in keysToRemove)
                {
                    _cache.TryRemove(key, out _);
                }
                
                if (keysToRemove.Count > 0)
                {
                    LogManager.Default.Debug($"查询缓存清理了 {keysToRemove.Count} 个过期条目");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"查询缓存清理失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public CacheStats GetStats()
        {
            return new CacheStats
            {
                TotalEntries = _cache.Count,
                MemoryUsage = CalculateMemoryUsage()
            };
        }
        
        /// <summary>
        /// 估算内存使用量
        /// </summary>
        private long CalculateMemoryUsage()
        {
            long total = 0;
            foreach (var kvp in _cache)
            {
                total += EstimateSize(kvp.Value.Value);
            }
            return total;
        }
        
        /// <summary>
        /// 估算对象大小
        /// </summary>
        private long EstimateSize(object obj)
        {
            if (obj == null) return 0;
            
            // 简单估算：字符串长度 * 2 + 对象开销
            if (obj is string str)
            {
                return str.Length * 2 + 20;
            }
            
            // 数组估算
            if (obj is Array array)
            {
                long size = 20; // 数组对象开销
                if (array.Length > 0)
                {
                    var element = array.GetValue(0);
                    size += array.Length * EstimateSize(element);
                }
                return size;
            }
            
            // 默认估算
            return 50;
        }
        
        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            _cache.Clear();
        }
        
        private class CacheEntry
        {
            public string Key { get; set; }
            public object Value { get; set; }
            public DateTime CreationTime { get; set; }
            public DateTime LastAccessTime { get; set; }
            public DateTime ExpirationTime { get; set; }
        }
    }
    
    /// <summary>
    /// 缓存统计信息
    /// </summary>
    public class CacheStats
    {
        public int TotalEntries { get; set; }
        public long MemoryUsage { get; set; } // 字节
        
        public override string ToString()
        {
            return $"缓存统计: 条目数={TotalEntries}, 内存使用={MemoryUsage / 1024}KB";
        }
    }
    
    /// <summary>
    /// 批量操作管理器
    /// </summary>
    public class BatchOperationManager
    {
        private readonly DatabaseConnectionPool _connectionPool;
        private readonly List<BatchOperation> _operations;
        private readonly object _lock = new();
        private readonly Timer _flushTimer;
        private readonly int _batchSize;
        
        public BatchOperationManager(DatabaseConnectionPool connectionPool, int batchSize = 100)
        {
            _connectionPool = connectionPool ?? throw new ArgumentNullException(nameof(connectionPool));
            _operations = new List<BatchOperation>();
            _batchSize = batchSize;
            
            // 启动定时刷新（每5秒刷新一次）
            _flushTimer = new Timer(FlushCallback, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }
        
        /// <summary>
        /// 添加批量操作
        /// </summary>
        public void AddOperation(string sql, params SqlParameter[] parameters)
        {
            lock (_lock)
            {
                _operations.Add(new BatchOperation
                {
                    Sql = sql,
                    Parameters = parameters?.ToList() ?? new List<SqlParameter>(),
                    Timestamp = DateTime.Now
                });
                
                // 如果达到批量大小，立即执行
                if (_operations.Count >= _batchSize)
                {
                    Task.Run(() => ExecuteBatchAsync());
                }
            }
        }
        
        /// <summary>
        /// 执行批量操作
        /// </summary>
        private async Task ExecuteBatchAsync()
        {
            List<BatchOperation> operationsToExecute;
            
            lock (_lock)
            {
                if (_operations.Count == 0)
                    return;
                    
                operationsToExecute = new List<BatchOperation>(_operations);
                _operations.Clear();
            }
            
            try
            {
                await _connectionPool.ExecuteWithConnectionAsync(async connection =>
                {
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            foreach (var operation in operationsToExecute)
                            {
                                using (var cmd = new SqlCommand(operation.Sql, connection, transaction))
                                {
                                    cmd.Parameters.AddRange(operation.Parameters.ToArray());
                                    await cmd.ExecuteNonQueryAsync();
                                }
                            }
                            
                            await transaction.CommitAsync();
                            LogManager.Default.Debug($"批量操作执行成功，处理了 {operationsToExecute.Count} 个操作");
                        }
                        catch (Exception ex)
                        {
                            await transaction.RollbackAsync();
                            LogManager.Default.Error($"批量操作执行失败: {ex.Message}");
                            throw;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"批量操作执行异常: {ex.Message}");
                
                // 将失败的操作重新加入队列（可以添加重试逻辑）
                lock (_lock)
                {
                    _operations.InsertRange(0, operationsToExecute);
                }
            }
        }
        
        /// <summary>
        /// 定时刷新回调
        /// </summary>
        private void FlushCallback(object state)
        {
            Task.Run(() => ExecuteBatchAsync());
        }
        
        /// <summary>
        /// 强制刷新所有待处理操作
        /// </summary>
        public async Task FlushAsync()
        {
            await ExecuteBatchAsync();
        }
        
        /// <summary>
        /// 获取批量操作统计信息
        /// </summary>
        public BatchStats GetStats()
        {
            lock (_lock)
            {
                return new BatchStats
                {
                    PendingOperations = _operations.Count,
                    BatchSize = _batchSize,
                    LastFlushTime = _operations.Count > 0 ? _operations[0].Timestamp : DateTime.MinValue
                };
            }
        }
        
        public void Dispose()
        {
            _flushTimer?.Dispose();
            // 在释放前刷新所有待处理操作
            Task.Run(async () => await FlushAsync()).Wait();
        }
        
        private class BatchOperation
        {
            public string Sql { get; set; }
            public List<SqlParameter> Parameters { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
    
    /// <summary>
    /// 批量操作统计信息
    /// </summary>
    public class BatchStats
    {
        public int PendingOperations { get; set; }
        public int BatchSize { get; set; }
        public DateTime LastFlushTime { get; set; }
        
        public override string ToString()
        {
            return $"批量操作统计: 待处理={PendingOperations}, 批量大小={BatchSize}, 最后刷新时间={LastFlushTime:yyyy-MM-dd HH:mm:ss}";
        }
    }
}
