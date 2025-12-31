using System.Data;
using MySql.Data.MySqlClient;

namespace MirCommon.Database
{
    /// <summary>
    /// MySQL数据库实现
    /// </summary>
    public class MySQLDatabase : BaseDatabase
    {
        public MySQLDatabase(string connectionString) : base(connectionString)
        {
        }
        
        protected override IDbConnection CreateConnection()
        {
            return new MySqlConnection(_connectionString);
        }
        
        protected override IDbDataParameter CreateParameter(string name, object value)
        {
            return new MySqlParameter(name, value);
        }
        
        
        public override SERVER_ERROR OpenDataBase()
        {
            try
            {
                _connection = CreateConnection();
                _connection.Open();
                
                // 检查并创建必要的表
                CreateTablesIfNotExist();
                
                return SERVER_ERROR.SE_OK;
            }
            catch (System.Exception)
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        /// <summary>
        /// 创建必要的表（如果不存在）
        /// </summary>
        private void CreateTablesIfNotExist()
        {
            using var cmd = _connection!.CreateCommand();
            
            // 创建账号表
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS TBL_ACCOUNT (
                    FLD_ACCOUNT VARCHAR(50) PRIMARY KEY,
                    FLD_PASSWORD VARCHAR(50) NOT NULL,
                    FLD_NAME VARCHAR(50),
                    FLD_BIRTHDAY VARCHAR(20),
                    FLD_Q1 VARCHAR(100),
                    FLD_A1 VARCHAR(100),
                    FLD_Q2 VARCHAR(100),
                    FLD_A2 VARCHAR(100),
                    FLD_EMAIL VARCHAR(100),
                    FLD_PHONENUMBER VARCHAR(20),
                    FLD_MOBILEPHONENUMBER VARCHAR(20),
                    FLD_IDCARD VARCHAR(20)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
            cmd.ExecuteNonQuery();
            
            // 创建角色表
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS TBL_CHARACTER (
                    FLD_ACCOUNT VARCHAR(50) NOT NULL,
                    FLD_SERVERNAME VARCHAR(50) NOT NULL,
                    FLD_NAME VARCHAR(50) NOT NULL,
                    FLD_JOB TINYINT NOT NULL,
                    FLD_HAIR TINYINT NOT NULL,
                    FLD_SEX TINYINT NOT NULL,
                    FLD_LEVEL SMALLINT NOT NULL,
                    FLD_MAPID INT NOT NULL,
                    FLD_X INT NOT NULL,
                    FLD_Y INT NOT NULL,
                    FLD_DATA LONGBLOB,
                    FLD_DELETED TINYINT DEFAULT 0,
                    FLD_DELETEDATE TIMESTAMP NULL,
                    PRIMARY KEY (FLD_ACCOUNT, FLD_SERVERNAME, FLD_NAME),
                    INDEX idx_account_server (FLD_ACCOUNT, FLD_SERVERNAME)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
            cmd.ExecuteNonQuery();
            
            // 创建物品表
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS TBL_ITEMS (
                    FLD_OWNER INT NOT NULL,
                    FLD_FLAG TINYINT NOT NULL,
                    FLD_DATA LONGBLOB NOT NULL,
                    PRIMARY KEY (FLD_OWNER, FLD_FLAG)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
            cmd.ExecuteNonQuery();
            
            // 创建技能表
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS TBL_MAGIC (
                    FLD_OWNER INT NOT NULL PRIMARY KEY,
                    FLD_DATA LONGBLOB NOT NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
            cmd.ExecuteNonQuery();
        }
    }
}
