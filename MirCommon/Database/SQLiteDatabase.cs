using System.Data;
using Microsoft.Data.Sqlite;

namespace MirCommon.Database
{
    /// <summary>
    /// SQLite数据库实现
    /// </summary>
    public class SQLiteDatabase : BaseDatabase
    {
        public SQLiteDatabase(string connectionString) : base(connectionString)
        {
        }
        
        protected override IDbConnection CreateConnection()
        {
            return new SqliteConnection(_connectionString);
        }
        
        protected override IDbDataParameter CreateParameter(string name, object value)
        {
            return new SqliteParameter(name, value);
        }
        
        
        public override SERVER_ERROR OpenDataBase()
        {
            try
            {
                // 确保SQLite数据库文件存在
                var builder = new SqliteConnectionStringBuilder(_connectionString);
                var dataSource = builder.DataSource;
                
                if (!string.IsNullOrEmpty(dataSource) && !System.IO.File.Exists(dataSource))
                {
                    // 创建数据库文件
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();
                    
                    // 创建必要的表
                    CreateTables(connection);
                    
                    connection.Close();
                }
                
                _connection = CreateConnection();
                _connection.Open();
                return SERVER_ERROR.SE_OK;
            }
            catch (System.Exception)
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        /// <summary>
        /// 创建必要的表
        /// </summary>
        private void CreateTables(SqliteConnection connection)
        {
            using var cmd = connection.CreateCommand();
            
            // 创建账号表（使用SQL文件中的表结构）
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS TBL_ACCOUNT (
                    ACCOUNT TEXT PRIMARY KEY,
                    PASSWORD TEXT NOT NULL,
                    NAME TEXT,
                    BIRTHDAY TEXT,
                    Q1 TEXT,
                    A1 TEXT,
                    Q2 TEXT,
                    A2 TEXT,
                    EMAIL TEXT,
                    PHONENUMBER TEXT,
                    MOBILEPHONENUMBER TEXT,
                    IDCARD TEXT
                )";
            cmd.ExecuteNonQuery();
            
            // 创建角色表（使用SQL文件中的表结构）
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS TBL_CHARACTER_INFO (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    ACCOUNT TEXT NOT NULL DEFAULT '',
                    NAME TEXT NOT NULL DEFAULT '',
                    CLASS INTEGER NOT NULL DEFAULT 0,
                    SEX INTEGER NOT NULL DEFAULT 0,
                    VLEVEL INTEGER NOT NULL DEFAULT 0,
                    MAPNAME TEXT NOT NULL DEFAULT '0',
                    POSX INTEGER NOT NULL DEFAULT 300,
                    POSY INTEGER NOT NULL DEFAULT 300,
                    HAIR INTEGER NOT NULL DEFAULT 0,
                    SERVER TEXT NOT NULL DEFAULT '',
                    ODATE TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    DELFLAG INTEGER NOT NULL DEFAULT 0,
                    DELDATE TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    CREATEDATE TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    CUREXP INTEGER NOT NULL DEFAULT 0,
                    HP INTEGER NOT NULL DEFAULT 0,
                    MP INTEGER NOT NULL DEFAULT 0,
                    MAXHP INTEGER NOT NULL DEFAULT 0,
                    MAXMP INTEGER NOT NULL DEFAULT 0,
                    MINDC INTEGER NOT NULL DEFAULT 0,
                    MAXDC INTEGER NOT NULL DEFAULT 0,
                    MINMC INTEGER NOT NULL DEFAULT 0,
                    MAXMC INTEGER NOT NULL DEFAULT 0,
                    MINSC INTEGER NOT NULL DEFAULT 0,
                    MAXSC INTEGER NOT NULL DEFAULT 0,
                    MINAC INTEGER NOT NULL DEFAULT 0,
                    MAXAC INTEGER NOT NULL DEFAULT 0,
                    MINMAC INTEGER NOT NULL DEFAULT 0,
                    MAXMAC INTEGER NOT NULL DEFAULT 0,
                    WEIGHT INTEGER NOT NULL DEFAULT 0,
                    HANDWEIGHT INTEGER NOT NULL DEFAULT 0,
                    BODYWEIGHT INTEGER NOT NULL DEFAULT 0,
                    GOLD INTEGER NOT NULL DEFAULT 0,
                    MAPID INTEGER NOT NULL DEFAULT 0,
                    YUANBAO INTEGER NOT NULL DEFAULT 0,
                    FLAG1 INTEGER NOT NULL DEFAULT 0,
                    FLAG2 INTEGER NOT NULL DEFAULT 0,
                    FLAG3 INTEGER NOT NULL DEFAULT 0,
                    FLAG4 INTEGER NOT NULL DEFAULT 0,
                    GUILDNAME TEXT NOT NULL DEFAULT '',
                    FORGEPOINT INTEGER NOT NULL DEFAULT 0,
                    PROP1 INTEGER NOT NULL DEFAULT 0,
                    PROP2 INTEGER NOT NULL DEFAULT 0,
                    PROP3 INTEGER NOT NULL DEFAULT 0,
                    PROP4 INTEGER NOT NULL DEFAULT 0,
                    PROP5 INTEGER NOT NULL DEFAULT 0,
                    PROP6 INTEGER NOT NULL DEFAULT 0,
                    PROP7 INTEGER NOT NULL DEFAULT 0,
                    PROP8 INTEGER NOT NULL DEFAULT 0
                )";
            cmd.ExecuteNonQuery();
            
            // 创建物品表（使用SQL文件中的表结构）
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS TBL_CHARACTER_ITEM (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    OWNERID INTEGER NOT NULL DEFAULT 0,
                    FLAG INTEGER NOT NULL DEFAULT 0,
                    NAME TEXT NOT NULL DEFAULT '',
                    MINDC INTEGER NOT NULL DEFAULT 0,
                    MAXDC INTEGER NOT NULL DEFAULT 0,
                    MINMC INTEGER NOT NULL DEFAULT 0,
                    MAXMC INTEGER NOT NULL DEFAULT 0,
                    MINSC INTEGER NOT NULL DEFAULT 0,
                    MAXSC INTEGER NOT NULL DEFAULT 0,
                    MINAC INTEGER NOT NULL DEFAULT 0,
                    MAXAC INTEGER NOT NULL DEFAULT 0,
                    MINMAC INTEGER NOT NULL DEFAULT 0,
                    MAXMAC INTEGER NOT NULL DEFAULT 0,
                    DURA INTEGER NOT NULL DEFAULT 0,
                    CURDURA INTEGER NOT NULL DEFAULT 0,
                    MAXDURA INTEGER NOT NULL DEFAULT 0,
                    NEEDTYPE INTEGER NOT NULL DEFAULT 0,
                    NEEDLEVEL INTEGER NOT NULL DEFAULT 0,
                    SPECIALPOWER INTEGER NOT NULL DEFAULT 0,
                    NEEDIDENTIFY INTEGER NOT NULL DEFAULT 0,
                    WEIGHT INTEGER NOT NULL DEFAULT 0,
                    STDMODE INTEGER NOT NULL DEFAULT 0,
                    SHAPE INTEGER NOT NULL DEFAULT 0,
                    PRICE INTEGER NOT NULL DEFAULT 0,
                    UNKNOWN_1 INTEGER NOT NULL DEFAULT 0,
                    UNKNOWN_2 INTEGER NOT NULL DEFAULT 0,
                    POS INTEGER NOT NULL DEFAULT 0,
                    FINDKEY INTEGER NOT NULL DEFAULT 0,
                    IMAGEINDEX INTEGER NOT NULL DEFAULT 0,
                    DELFLAG INTEGER NOT NULL DEFAULT 0
                )";
            cmd.ExecuteNonQuery();
            
            // 创建技能表（使用SQL文件中的表结构）
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS TBL_CHARACTER_MAGIC (
                    CHARID INTEGER NOT NULL DEFAULT 0,
                    USERKEY INTEGER NOT NULL DEFAULT 0,
                    CURLEVEL INTEGER NOT NULL DEFAULT 0,
                    MAGICID INTEGER NOT NULL DEFAULT 0,
                    CURTRAIN INTEGER NOT NULL DEFAULT 0,
                    PRIMARY KEY (CHARID, MAGICID)
                )";
            cmd.ExecuteNonQuery();
            
            // 创建社区表（使用SQL文件中的表结构）
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS TBL_CHARACTER_COMMUNITY (
                    OWNERID INTEGER PRIMARY KEY,
                    MARRIAGE TEXT NOT NULL DEFAULT '',
                    MASTER TEXT NOT NULL DEFAULT '',
                    STUDENT1 TEXT NOT NULL DEFAULT '',
                    STUDENT2 TEXT NOT NULL DEFAULT '',
                    STUDENT3 TEXT NOT NULL DEFAULT '',
                    FRIEND1 TEXT NOT NULL DEFAULT '',
                    FRIEND2 TEXT NOT NULL DEFAULT '',
                    FRIEND3 TEXT NOT NULL DEFAULT '',
                    FRIEND4 TEXT NOT NULL DEFAULT '',
                    FRIEND5 TEXT NOT NULL DEFAULT '',
                    FRIEND6 TEXT NOT NULL DEFAULT '',
                    FRIEND7 TEXT NOT NULL DEFAULT '',
                    FRIEND8 TEXT NOT NULL DEFAULT '',
                    FRIEND9 TEXT NOT NULL DEFAULT '',
                    FRIEND10 TEXT NOT NULL DEFAULT ''
                )";
            cmd.ExecuteNonQuery();
            
            // 创建任务表（使用SQL文件中的表结构）
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS TBL_CHARACTER_TASK (
                    CHARID INTEGER NOT NULL DEFAULT 0,
                    ACHIEVEMENT INTEGER NOT NULL DEFAULT 0,
                    TASKID1 INTEGER NOT NULL DEFAULT 0,
                    TASKSTEP1 INTEGER NOT NULL DEFAULT 0,
                    TASKID2 INTEGER NOT NULL DEFAULT 0,
                    TASKSTEP2 INTEGER NOT NULL DEFAULT 0,
                    TASKID3 INTEGER NOT NULL DEFAULT 0,
                    TASKSTEP3 INTEGER NOT NULL DEFAULT 0,
                    TASKID4 INTEGER NOT NULL DEFAULT 0,
                    TASKSTEP4 INTEGER NOT NULL DEFAULT 0,
                    TASKID5 INTEGER NOT NULL DEFAULT 0,
                    TASKSTEP5 INTEGER NOT NULL DEFAULT 0,
                    TASKID6 INTEGER NOT NULL DEFAULT 0,
                    TASKSTEP6 INTEGER NOT NULL DEFAULT 0,
                    TASKID7 INTEGER NOT NULL DEFAULT 0,
                    TASKSTEP7 INTEGER NOT NULL DEFAULT 0,
                    TASKID8 INTEGER NOT NULL DEFAULT 0,
                    TASKSTEP8 INTEGER NOT NULL DEFAULT 0,
                    TASKID9 INTEGER NOT NULL DEFAULT 0,
                    TASKSTEP9 INTEGER NOT NULL DEFAULT 0,
                    TASKID10 INTEGER NOT NULL DEFAULT 0,
                    TASKSTEP10 INTEGER NOT NULL DEFAULT 0,
                    FLAGS TEXT NOT NULL DEFAULT ''
                )";
            cmd.ExecuteNonQuery();
            
            // 创建索引
            cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_character_info_account ON TBL_CHARACTER_INFO(ACCOUNT)";
            cmd.ExecuteNonQuery();
            
            cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_character_info_name ON TBL_CHARACTER_INFO(NAME)";
            cmd.ExecuteNonQuery();
            
            cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_character_info_guild ON TBL_CHARACTER_INFO(GUILDNAME)";
            cmd.ExecuteNonQuery();
            
            cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_character_task_charid ON TBL_CHARACTER_TASK(CHARID)";
            cmd.ExecuteNonQuery();
        }
    }
}
