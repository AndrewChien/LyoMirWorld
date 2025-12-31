using System;
using System.Data;
using MirCommon;
using MirCommon.Database;
using SERVER_ERROR = MirCommon.SERVER_ERROR;

namespace DBServer
{
    /// <summary>
    /// 新的数据库操作类，支持多种数据库
    /// </summary>
    public class AppDB_New
    {
        private readonly IDatabase _database;
        private readonly ErrorHandler? _errorHandler;
        private readonly ConnectionHealthChecker? _healthChecker;
        
        /// <summary>
        /// 从配置创建数据库实例
        /// </summary>
        public AppDB_New(string dbType, string server, string database, string userId, string password, string sqlitePath = "")
        {
            var config = DatabaseConfig.FromConfigString(dbType, server, database, userId, password, sqlitePath);
            _database = DatabaseFactory.CreateDatabaseFromConfig(config);
            _errorHandler = new ErrorHandler(config.ConnectionString, config.Type);
            _healthChecker = new ConnectionHealthChecker(_errorHandler, TimeSpan.FromMinutes(1));
        }
        
        /// <summary>
        /// 从数据库配置创建实例
        /// </summary>
        public AppDB_New(DatabaseConfig config)
        {
            _database = DatabaseFactory.CreateDatabaseFromConfig(config);
            _errorHandler = new ErrorHandler(config.ConnectionString, config.Type);
            _healthChecker = new ConnectionHealthChecker(_errorHandler, TimeSpan.FromMinutes(1));
        }
        
        /// <summary>
        /// 启动健康检查
        /// </summary>
        public void StartHealthCheck()
        {
            _healthChecker?.Start();
        }
        
        /// <summary>
        /// 停止健康检查
        /// </summary>
        public void StopHealthCheck()
        {
            _healthChecker?.Stop();
        }
        
        /// <summary>
        /// 获取错误统计信息
        /// </summary>
        public ErrorStatistics? GetErrorStatistics()
        {
            return _errorHandler?.GetErrorStatistics();
        }
        
        /// <summary>
        /// 打开数据库连接
        /// </summary>
        public SERVER_ERROR OpenDataBase()
        {
            if (_errorHandler == null)
            {
                return _database.OpenDataBase();
            }
            
            var result = _errorHandler.ExecuteWithRetry(() => (MirCommon.SERVER_ERROR)_database.OpenDataBase(), "打开数据库连接");
            return (SERVER_ERROR)result;
        }
        
        /// <summary>
        /// 检查账号密码
        /// </summary>
        public SERVER_ERROR CheckAccount(string account, string password)
        {
            if (_errorHandler == null)
            {
                return _database.CheckAccount(account, password);
            }
            
            var result = _errorHandler.ExecuteWithRetry(() => (MirCommon.SERVER_ERROR)_database.CheckAccount(account, password), "检查账号密码");
            return (SERVER_ERROR)result;
        }
        
        /// <summary>
        /// 检查账号是否存在
        /// </summary>
        public SERVER_ERROR CheckAccountExist(string account)
        {
            if (_errorHandler == null)
            {
                return _database.CheckAccountExist(account);
            }
            
            var result = _errorHandler.ExecuteWithRetry(() => (MirCommon.SERVER_ERROR)_database.CheckAccountExist(account), "检查账号是否存在");
            return (SERVER_ERROR)result;
        }
        
        /// <summary>
        /// 创建账号
        /// </summary>
        public SERVER_ERROR CreateAccount(string account, string password, string name, string birthday,
                                         string q1, string a1, string q2, string a2, string email,
                                         string phoneNumber, string mobilePhoneNumber, string idCard)
        {
            if (_errorHandler == null)
            {
                return _database.CreateAccount(account, password, name, birthday, q1, a1, q2, a2, email, phoneNumber, mobilePhoneNumber, idCard);
            }
            
            var result = _errorHandler.ExecuteWithRetry(() => 
                (MirCommon.SERVER_ERROR)_database.CreateAccount(account, password, name, birthday, q1, a1, q2, a2, email, phoneNumber, mobilePhoneNumber, idCard), 
                "创建账号");
            return (SERVER_ERROR)result;
        }
        
        /// <summary>
        /// 修改密码
        /// </summary>
        public SERVER_ERROR ChangePassword(string account, string oldPassword, string newPassword)
        {
            try
            {
                // 先验证旧密码
                if (CheckAccount(account, oldPassword) != SERVER_ERROR.SE_OK)
                    return SERVER_ERROR.SE_FAIL;
                
                // 修改密码逻辑在数据库实现中处理
                return _database.ChangePassword(account, oldPassword, newPassword);
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        /// <summary>
        /// 查询角色列表
        /// </summary>
        public SERVER_ERROR GetCharList(string account, string serverName, out string charListData)
        {
            return _database.GetCharList(account, serverName, out charListData);
        }
        
        /// <summary>
        /// 创建角色（向后兼容版本，使用默认等级1）
        /// </summary>
        public SERVER_ERROR CreateCharacter(string account, string serverName, string name, byte job, byte hair, byte sex)
        {
            if (_errorHandler == null)
            {
                return _database.CreateCharacter(account, serverName, name, job, hair, sex);
            }
            
            var result = _errorHandler.ExecuteWithRetry(() => 
                (MirCommon.SERVER_ERROR)_database.CreateCharacter(account, serverName, name, job, hair, sex), 
                "创建角色");
            return (SERVER_ERROR)result;
        }
        
        /// <summary>
        /// 创建角色（完整版本，包含等级参数）
        /// </summary>
        public SERVER_ERROR CreateCharacter(string account, string serverName, string name, byte job, byte hair, byte sex, byte level)
        {
            if (_errorHandler == null)
            {
                return _database.CreateCharacter(account, serverName, name, job, hair, sex, level);
            }
            
            var result = _errorHandler.ExecuteWithRetry(() => 
                (MirCommon.SERVER_ERROR)_database.CreateCharacter(account, serverName, name, job, hair, sex, level), 
                "创建角色（带等级）");
            return (SERVER_ERROR)result;
        }
        
        /// <summary>
        /// 创建角色（使用CREATECHARDESC结构）
        /// </summary>
        public SERVER_ERROR CreateCharacter(CREATECHARDESC desc)
        {
            if (_errorHandler == null)
            {
                return _database.CreateCharacter(desc);
            }
            
            var result = _errorHandler.ExecuteWithRetry(() => 
                (MirCommon.SERVER_ERROR)_database.CreateCharacter(desc), 
                "创建角色（使用结构）");
            return (SERVER_ERROR)result;
        }
        
        /// <summary>
        /// 删除角色（标记为已删除）
        /// </summary>
        public SERVER_ERROR DelCharacter(string account, string serverName, string name)
        {
            return _database.DelCharacter(account, serverName, name);
        }
        
        /// <summary>
        /// 恢复角色
        /// </summary>
        public SERVER_ERROR RestoreCharacter(string account, string serverName, string name)
        {
            return _database.RestoreCharacter(account, serverName, name);
        }
        
        /// <summary>
        /// 获取角色数据库信息
        /// </summary>
        public SERVER_ERROR GetCharDBInfo(string account, string serverName, string name, out byte[] charData)
        {
            if (_errorHandler == null)
            {
                return _database.GetCharDBInfo(account, serverName, name, out charData);
            }
            
            byte[] localCharData = Array.Empty<byte>();
            var result = _errorHandler.ExecuteWithRetry(() => 
                (MirCommon.SERVER_ERROR)_database.GetCharDBInfo(account, serverName, name, out localCharData), 
                "获取角色数据库信息");
            
            charData = localCharData;
            return (SERVER_ERROR)result;
        }
        
        /// <summary>
        /// 保存角色数据库信息
        /// </summary>
        public SERVER_ERROR PutCharDBInfo(string account, string serverName, string name, byte[] charData)
        {
            if (_errorHandler == null)
            {
                return _database.PutCharDBInfo(account, serverName, name, charData);
            }
            
            var result = _errorHandler.ExecuteWithRetry(() => 
                (MirCommon.SERVER_ERROR)_database.PutCharDBInfo(account, serverName, name, charData), 
                "保存角色数据库信息");
            return (SERVER_ERROR)result;
        }
        
        /// <summary>
        /// 查询物品
        /// </summary>
        public SERVER_ERROR QueryItems(uint ownerId, byte flag, out byte[] itemsData)
        {
            return _database.QueryItems(ownerId, flag, out itemsData);
        }
        
        /// <summary>
        /// 更新物品
        /// </summary>
        public SERVER_ERROR UpdateItems(uint ownerId, byte flag, byte[] itemsData)
        {
            return _database.UpdateItems(ownerId, flag, itemsData);
        }
        
        /// <summary>
        /// 查询技能
        /// </summary>
        public SERVER_ERROR QueryMagic(uint ownerId, out byte[] magicData)
        {
            return _database.QueryMagic(ownerId, out magicData);
        }
        
        /// <summary>
        /// 更新技能
        /// </summary>
        public SERVER_ERROR UpdateMagic(uint ownerId, byte[] magicData)
        {
            return _database.UpdateMagic(ownerId, magicData);
        }
        
        /// <summary>
        /// 执行SQL命令
        /// </summary>
        public SERVER_ERROR ExecSqlCommand(string sql, out DataTable result)
        {
            return _database.ExecSqlCommand(sql, out result);
        }
        
        /// <summary>
        /// 关闭数据库连接
        /// </summary>
        public void Close()
        {
            _database.Close();
        }
        
        // 以下是新增接口方法的实现
        
        public SERVER_ERROR GetDelCharList(string account, string serverName, out string delCharListData)
        {
            return _database.GetDelCharList(account, serverName, out delCharListData);
        }
        
        public SERVER_ERROR GetMapPosition(string account, string serverName, string name, out string mapName, out short x, out short y)
        {
            return _database.GetMapPosition(account, serverName, name, out mapName, out x, out y);
        }
        
        public SERVER_ERROR GetFreeItemId(out uint itemId)
        {
            return _database.GetFreeItemId(out itemId);
        }
        
        public SERVER_ERROR FindItemId(uint ownerId, byte flag, ushort pos, uint findKey, out uint itemId)
        {
            return _database.FindItemId(ownerId, flag, pos, findKey, out itemId);
        }
        
        public SERVER_ERROR UpgradeItem(uint makeIndex, uint upgrade)
        {
            return _database.UpgradeItem(makeIndex, upgrade);
        }
        
        public SERVER_ERROR CreateItem(uint ownerId, byte flag, ushort pos, byte[] itemData)
        {
            return _database.CreateItem(ownerId, flag, pos, itemData);
        }
        
        public SERVER_ERROR CreateItemEx(uint ownerId, byte flag, ushort pos, byte[] itemData)
        {
            return _database.CreateItemEx(ownerId, flag, pos, itemData);
        }
        
        public SERVER_ERROR UpdateItem(uint ownerId, byte flag, ushort pos, byte[] itemData)
        {
            return _database.UpdateItem(ownerId, flag, pos, itemData);
        }
        
        public SERVER_ERROR DeleteItem(uint itemId)
        {
            return _database.DeleteItem(itemId);
        }
        
        public SERVER_ERROR UpdateItemPos(uint itemId, byte flag, ushort pos)
        {
            return _database.UpdateItemPos(itemId, flag, pos);
        }
        
        public SERVER_ERROR UpdateItemPosEx(byte flag, ushort count, byte[] itemPosData)
        {
            return _database.UpdateItemPosEx(flag, count, itemPosData);
        }
        
        public SERVER_ERROR UpdateItemOwner(uint itemId, uint ownerId, byte flag, ushort pos)
        {
            return _database.UpdateItemOwner(itemId, ownerId, flag, pos);
        }
        
        public SERVER_ERROR DeleteMagic(uint ownerId, ushort magicId)
        {
            return _database.DeleteMagic(ownerId, magicId);
        }
        
        public SERVER_ERROR UpdateCommunity(uint ownerId, string communityData)
        {
            return _database.UpdateCommunity(ownerId, communityData);
        }
        
        public SERVER_ERROR QueryCommunity(uint ownerId, out string communityData)
        {
            return _database.QueryCommunity(ownerId, out communityData);
        }
        
        public SERVER_ERROR DeleteMarriage(string name, string marriage)
        {
            return _database.DeleteMarriage(name, marriage);
        }
        
        public SERVER_ERROR DeleteTeacher(string name, string teacher)
        {
            return _database.DeleteTeacher(name, teacher);
        }
        
        public SERVER_ERROR DeleteStudent(string teacher, string student)
        {
            return _database.DeleteStudent(teacher, student);
        }
        
        public SERVER_ERROR BreakFriend(string friend1, string friend2)
        {
            return _database.BreakFriend(friend1, friend2);
        }
        
        public SERVER_ERROR RestoreGuild(string name, string guildName)
        {
            return _database.RestoreGuild(name, guildName);
        }
        
        public SERVER_ERROR AddCredit(string name, uint count)
        {
            return _database.AddCredit(name, count);
        }
        
        public SERVER_ERROR QueryTaskInfo(uint ownerId, out byte[] taskInfoData)
        {
            return _database.QueryTaskInfo(ownerId, out taskInfoData);
        }
        
        public SERVER_ERROR UpdateTaskInfo(uint ownerId, byte[] taskInfoData)
        {
            return _database.UpdateTaskInfo(ownerId, taskInfoData);
        }
        
        public SERVER_ERROR QueryUpgradeItem(uint ownerId, out byte[] upgradeItemData)
        {
            return _database.QueryUpgradeItem(ownerId, out upgradeItemData);
        }
    }
}
