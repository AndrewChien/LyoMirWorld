using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace MirCommon.Database
{
    /// <summary>
    /// 数据库管理器
    /// </summary>
    public class DatabaseManager : IDisposable
    {
        private readonly DatabaseConfig _config;
        private readonly ConcurrentBag<SqlConnection> _connectionPool = new();
        private readonly object _poolLock = new();
        private bool _disposed = false;
        private int _activeConnections = 0;

        public DatabaseManager(DatabaseConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// 初始化数据库管理器
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                // 创建初始连接池
                for (int i = 0; i < Math.Min(10, _config.MaxConnections); i++)
                {
                    var connection = CreateConnection();
                    if (connection != null)
                    {
                        _connectionPool.Add(connection);
                    }
                }

                Console.WriteLine($"数据库管理器初始化完成，连接池大小: {_connectionPool.Count}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"数据库管理器初始化失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取数据库连接
        /// </summary>
        public SqlConnection? GetConnection()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DatabaseManager));

            lock (_poolLock)
            {
                // 尝试从连接池获取
                if (_connectionPool.TryTake(out var connection))
                {
                    _activeConnections++;
                    return connection;
                }

                // 创建新连接
                if (_activeConnections < _config.MaxConnections)
                {
                    var newConnection = CreateConnection();
                    if (newConnection != null)
                    {
                        _activeConnections++;
                        return newConnection;
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// 释放数据库连接
        /// </summary>
        public void ReleaseConnection(SqlConnection connection)
        {
            if (connection == null)
                return;

            lock (_poolLock)
            {
                if (_disposed)
                {
                    connection.Dispose();
                    return;
                }

                // 如果连接状态正常，放回连接池
                if (connection.State == ConnectionState.Open && _connectionPool.Count < _config.MaxConnections)
                {
                    _connectionPool.Add(connection);
                }
                else
                {
                    connection.Dispose();
                }

                _activeConnections--;
            }
        }

        /// <summary>
        /// 执行查询并返回DataTable
        /// </summary>
        public async Task<DataTable?> ExecuteQueryAsync(string sql, params SqlParameter[] parameters)
        {
            var connection = GetConnection();
            if (connection == null)
                return null;

            try
            {
                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddRange(parameters);
                command.CommandTimeout = _config.CommandTimeout;

                using var adapter = new SqlDataAdapter(command);
                var dataTable = new DataTable();
                await Task.Run(() => adapter.Fill(dataTable));
                return dataTable;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"执行查询失败: {ex.Message}");
                return null;
            }
            finally
            {
                ReleaseConnection(connection);
            }
        }

        /// <summary>
        /// 执行非查询命令
        /// </summary>
        public async Task<int> ExecuteNonQueryAsync(string sql, params SqlParameter[] parameters)
        {
            var connection = GetConnection();
            if (connection == null)
                return -1;

            try
            {
                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddRange(parameters);
                command.CommandTimeout = _config.CommandTimeout;

                return await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"执行非查询命令失败: {ex.Message}");
                return -1;
            }
            finally
            {
                ReleaseConnection(connection);
            }
        }

        /// <summary>
        /// 执行标量查询
        /// </summary>
        public async Task<object?> ExecuteScalarAsync(string sql, params SqlParameter[] parameters)
        {
            var connection = GetConnection();
            if (connection == null)
                return null;

            try
            {
                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddRange(parameters);
                command.CommandTimeout = _config.CommandTimeout;

                return await command.ExecuteScalarAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"执行标量查询失败: {ex.Message}");
                return null;
            }
            finally
            {
                ReleaseConnection(connection);
            }
        }

        /// <summary>
        /// 执行存储过程
        /// </summary>
        public async Task<DataTable?> ExecuteStoredProcedureAsync(string procedureName, params SqlParameter[] parameters)
        {
            var connection = GetConnection();
            if (connection == null)
                return null;

            try
            {
                using var command = new SqlCommand(procedureName, connection);
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddRange(parameters);
                command.CommandTimeout = _config.CommandTimeout;

                using var adapter = new SqlDataAdapter(command);
                var dataTable = new DataTable();
                await Task.Run(() => adapter.Fill(dataTable));
                return dataTable;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"执行存储过程失败: {ex.Message}");
                return null;
            }
            finally
            {
                ReleaseConnection(connection);
            }
        }

        /// <summary>
        /// 批量插入数据
        /// </summary>
        public async Task<bool> BulkInsertAsync(string tableName, DataTable data)
        {
            var connection = GetConnection();
            if (connection == null)
                return false;

            try
            {
                using var bulkCopy = new SqlBulkCopy(connection)
                {
                    DestinationTableName = tableName,
                    BatchSize = 1000,
                    BulkCopyTimeout = _config.CommandTimeout
                };

                await bulkCopy.WriteToServerAsync(data);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"批量插入失败: {ex.Message}");
                return false;
            }
            finally
            {
                ReleaseConnection(connection);
            }
        }

        /// <summary>
        /// 开始事务
        /// </summary>
        public SqlTransaction? BeginTransaction()
        {
            var connection = GetConnection();
            if (connection == null)
                return null;

            try
            {
                return connection.BeginTransaction();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"开始事务失败: {ex.Message}");
                ReleaseConnection(connection);
                return null;
            }
        }

        /// <summary>
        /// 提交事务
        /// </summary>
        public void CommitTransaction(SqlTransaction transaction)
        {
            if (transaction == null)
                return;

            try
            {
                transaction.Commit();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"提交事务失败: {ex.Message}");
            }
            finally
            {
                transaction.Connection?.Close();
                ReleaseConnection(transaction.Connection!);
            }
        }

        /// <summary>
        /// 回滚事务
        /// </summary>
        public void RollbackTransaction(SqlTransaction transaction)
        {
            if (transaction == null)
                return;

            try
            {
                transaction.Rollback();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"回滚事务失败: {ex.Message}");
            }
            finally
            {
                transaction.Connection?.Close();
                ReleaseConnection(transaction.Connection!);
            }
        }

        /// <summary>
        /// 获取连接池状态
        /// </summary>
        public (int total, int active, int available) GetConnectionPoolStatus()
        {
            lock (_poolLock)
            {
                return (_config.MaxConnections, _activeConnections, _connectionPool.Count);
            }
        }

        /// <summary>
        /// 创建新连接
        /// </summary>
        private SqlConnection? CreateConnection()
        {
            try
            {
                var connection = new SqlConnection(_config.GetConnectionString());
                connection.Open();
                return connection;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"创建数据库连接失败: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                lock (_poolLock)
                {
                    while (_connectionPool.TryTake(out var connection))
                    {
                        connection.Dispose();
                    }
                }
            }
        }
    }
}
