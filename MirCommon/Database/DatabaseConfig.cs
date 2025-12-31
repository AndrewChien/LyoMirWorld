using System;
using System.Data;
using MirCommon.Utils;
using MySql.Data.MySqlClient;
using Microsoft.Data.Sqlite;

namespace MirCommon.Database
{
    /// <summary>
    /// 数据库配置
    /// </summary>
    public class DatabaseConfig
    {
        /// <summary>
        /// 数据库类型
        /// </summary>
        public DatabaseType Type { get; set; } = DatabaseType.SQLite;
        
        /// <summary>
        /// 连接字符串
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;
        
        /// <summary>
        /// 服务器地址（仅用于MySQL/SQL Server）
        /// </summary>
        public string Server { get; set; } = "(local)";
        
        /// <summary>
        /// 数据库名称
        /// </summary>
        public string Database { get; set; } = "MirWorldDB";
        
        /// <summary>
        /// 用户名
        /// </summary>
        public string UserId { get; set; } = "sa";
        
        /// <summary>
        /// 密码
        /// </summary>
        public string Password { get; set; } = "123456";
        
        /// <summary>
        /// SQLite文件路径（仅用于SQLite）
        /// </summary>
        public string SqliteFilePath { get; set; } = "MirWorldDB.sqlite";
        
        public int MaxConnections { get; set; } = 1024;
        public int ConnectionTimeout { get; set; } = 30;
        public int CommandTimeout { get; set; } = 30;

        /// <summary>
        /// 从配置字符串创建数据库配置
        /// </summary>
        public static DatabaseConfig FromConfigString(string type, string server, string database, string userId, string password, string sqlitePath = "")
        {
            var config = new DatabaseConfig();
            
            // 解析数据库类型
            config.Type = type.ToLower() switch
            {
                "sqlite" => DatabaseType.SQLite,
                "mysql" => DatabaseType.MySQL,
                "sqlserver" or "mssql" => DatabaseType.SqlServer,
                _ => DatabaseType.SQLite // 默认使用SQLite
            };
            
            config.Server = server;
            config.Database = database;
            config.UserId = userId;
            config.Password = password;
            config.SqliteFilePath = sqlitePath;
            
            // 构建连接字符串
            config.ConnectionString = config.Type switch
            {
                DatabaseType.SQLite => $"Data Source={sqlitePath};",  //Microsoft.Data.Sqlite使用的连接字符串格式
                DatabaseType.MySQL => $"Server={server};Database={database};User Id={userId};Password={password};",
                DatabaseType.SqlServer => $"Server={server};Database={database};User Id={userId};Password={password};",
                _ => $"Data Source={sqlitePath};Version=3;"
            };
            
            return config;
        }

        /// <summary>
        /// 从INI文件加载数据库配置
        /// </summary>
        public static DatabaseConfig LoadFromIni(string iniFilePath, string sectionName = "数据库服务器")
        {
            var config = new DatabaseConfig();
            
            try
            {
                var iniReader = new IniFileReader(iniFilePath);
                if (iniReader.Open())
                {
                    // 读取数据库类型
                    string dbType = iniReader.GetString(sectionName, "dbtype", "sqlite");
                    config.Type = dbType.ToLower() switch
                    {
                        "sqlite" => DatabaseType.SQLite,
                        "mysql" => DatabaseType.MySQL,
                        "sqlserver" or "mssql" => DatabaseType.SqlServer,
                        _ => DatabaseType.SQLite
                    };
                    
                    config.Server = iniReader.GetString(sectionName, "server", "(local)");
                    config.Database = iniReader.GetString(sectionName, "database", "MirWorldDB");
                    config.UserId = iniReader.GetString(sectionName, "account", "sa");
                    config.Password = iniReader.GetString(sectionName, "password", "123456");
                    config.SqliteFilePath = iniReader.GetString(sectionName, "sqlitepath", "MirWorldDB.sqlite");
                    config.MaxConnections = iniReader.GetInteger(sectionName, "maxconnection", 1024);
                    
                    // 构建连接字符串
                    config.ConnectionString = config.Type switch
                    {
                        DatabaseType.SQLite => $"Data Source={config.SqliteFilePath};Version=3;",
                        DatabaseType.MySQL => $"Server={config.Server};Database={config.Database};User Id={config.UserId};Password={config.Password};",
                        DatabaseType.SqlServer => $"Server={config.Server};Database={config.Database};User Id={config.UserId};Password={config.Password};",
                        _ => $"Data Source={config.SqliteFilePath};Version=3;"
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载数据库配置失败: {ex.Message}");
            }

            return config;
        }

        /// <summary>
        /// 获取连接字符串
        /// </summary>
        public string GetConnectionString()
        {
            if (!string.IsNullOrEmpty(ConnectionString))
                return ConnectionString;
                
            return Type switch
            {
                DatabaseType.SQLite => $"Data Source={SqliteFilePath};Version=3;",
                DatabaseType.MySQL => $"Server={Server};Database={Database};User Id={UserId};Password={Password};",
                DatabaseType.SqlServer => $"Server={Server};Database={Database};User Id={UserId};Password={Password};TrustServerCertificate=True;Connection Timeout={ConnectionTimeout};Max Pool Size={MaxConnections};",
                _ => $"Data Source={SqliteFilePath};Version=3;"
            };
        }

        /// <summary>
        /// 测试数据库连接
        /// </summary>
        public bool TestConnection()
        {
            try
            {
                switch (Type)
                {
                    case DatabaseType.SQLite:
                        using (var connection = new SqliteConnection(GetConnectionString()))
                        {
                            connection.Open();
                            return connection.State == ConnectionState.Open;
                        }
                        
                    case DatabaseType.MySQL:
                        using (var connection = new MySqlConnection(GetConnectionString()))
                        {
                            connection.Open();
                            return connection.State == ConnectionState.Open;
                        }
                        
                    case DatabaseType.SqlServer:
                    default:
                        using (var connection = new System.Data.SqlClient.SqlConnection(GetConnectionString()))
                        {
                            connection.Open();
                            return connection.State == ConnectionState.Open;
                        }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"数据库连接测试失败: {ex.Message}");
                return false;
            }
        }
    }
}
