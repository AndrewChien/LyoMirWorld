using System;

namespace MirCommon.Database
{
    /// <summary>
    /// 数据库类型
    /// </summary>
    public enum DatabaseType
    {
        /// <summary>
        /// SQLite数据库
        /// </summary>
        SQLite,
        
        /// <summary>
        /// MySQL数据库
        /// </summary>
        MySQL,
        
        /// <summary>
        /// SQL Server数据库
        /// </summary>
        SqlServer
    }
    
    /// <summary>
    /// 数据库工厂
    /// </summary>
    public static class DatabaseFactory
    {
        /// <summary>
        /// 创建数据库实例
        /// </summary>
        /// <param name="type">数据库类型</param>
        /// <param name="connectionString">连接字符串</param>
        /// <returns>数据库实例</returns>
        public static IDatabase CreateDatabase(DatabaseType type, string connectionString)
        {
            return type switch
            {
                DatabaseType.SQLite => new SQLiteDatabase(connectionString),
                DatabaseType.MySQL => new MySQLDatabase(connectionString),
                DatabaseType.SqlServer => new SqlServerDatabase(connectionString),
                _ => throw new ArgumentException($"不支持的数据库类型: {type}")
            };
        }
        
        /// <summary>
        /// 从配置创建数据库实例
        /// </summary>
        /// <param name="config">数据库配置</param>
        /// <returns>数据库实例</returns>
        public static IDatabase CreateDatabaseFromConfig(DatabaseConfig config)
        {
            return config.Type switch
            {
                DatabaseType.SQLite => new SQLiteDatabase(config.ConnectionString),
                DatabaseType.MySQL => new MySQLDatabase(config.ConnectionString),
                DatabaseType.SqlServer => new SqlServerDatabase(config.ConnectionString),
                _ => throw new ArgumentException($"不支持的数据库类型: {config.Type}")
            };
        }
    }
    
}
