using System.Data;

namespace MirCommon.Database
{
    /// <summary>
    /// 数据库接口
    /// </summary>
    public interface IDatabase
    {
        /// <summary>
        /// 打开数据库连接
        /// </summary>
        SERVER_ERROR OpenDataBase();
        
        /// <summary>
        /// 检查账号密码
        /// </summary>
        SERVER_ERROR CheckAccount(string account, string password);
        
        /// <summary>
        /// 检查账号是否存在
        /// </summary>
        SERVER_ERROR CheckAccountExist(string account);
        
        /// <summary>
        /// 创建账号
        /// </summary>
        SERVER_ERROR CreateAccount(string account, string password, string name, string birthday,
                                   string q1, string a1, string q2, string a2, string email,
                                   string phoneNumber, string mobilePhoneNumber, string idCard);
        
        /// <summary>
        /// 修改密码
        /// </summary>
        SERVER_ERROR ChangePassword(string account, string oldPassword, string newPassword);
        
        /// <summary>
        /// 查询角色列表
        /// </summary>
        SERVER_ERROR GetCharList(string account, string serverName, out string charListData);
        
        /// <summary>
        /// 获取已删除角色列表
        /// </summary>
        SERVER_ERROR GetDelCharList(string account, string serverName, out string delCharListData);
        
        /// <summary>
        /// 创建角色（向后兼容版本，使用默认等级1）
        /// </summary>
        SERVER_ERROR CreateCharacter(string account, string serverName, string name, byte job, byte hair, byte sex);
        
        /// <summary>
        /// 创建角色（完整版本，包含等级参数）
        /// </summary>
        SERVER_ERROR CreateCharacter(string account, string serverName, string name, byte job, byte hair, byte sex, byte level);
        
        /// <summary>
        /// 创建角色（使用CREATECHARDESC结构）
        /// </summary>
        SERVER_ERROR CreateCharacter(CREATECHARDESC desc);
        
        /// <summary>
        /// 删除角色（标记为已删除）
        /// </summary>
        SERVER_ERROR DelCharacter(string account, string serverName, string name);
        
        /// <summary>
        /// 恢复角色
        /// </summary>
        SERVER_ERROR RestoreCharacter(string account, string serverName, string name);
        
        /// <summary>
        /// 获取角色数据库信息
        /// </summary>
        SERVER_ERROR GetCharDBInfo(string account, string serverName, string name, out byte[] charData);
        
        /// <summary>
        /// 保存角色数据库信息
        /// </summary>
        SERVER_ERROR PutCharDBInfo(string account, string serverName, string name, byte[] charData);
        
        /// <summary>
        /// 获取地图位置
        /// </summary>
        SERVER_ERROR GetMapPosition(string account, string serverName, string name, out string mapName, out short x, out short y);
        
        /// <summary>
        /// 获取空闲物品ID
        /// </summary>
        SERVER_ERROR GetFreeItemId(out uint itemId);
        
        /// <summary>
        /// 查找物品ID
        /// </summary>
        SERVER_ERROR FindItemId(uint ownerId, byte flag, ushort pos, uint findKey, out uint itemId);
        
        /// <summary>
        /// 升级物品
        /// </summary>
        SERVER_ERROR UpgradeItem(uint makeIndex, uint upgrade);
        
        /// <summary>
        /// 创建物品
        /// </summary>
        SERVER_ERROR CreateItem(uint ownerId, byte flag, ushort pos, byte[] itemData);
        
        /// <summary>
        /// 创建物品（扩展）
        /// </summary>
        SERVER_ERROR CreateItemEx(uint ownerId, byte flag, ushort pos, byte[] itemData);
        
        /// <summary>
        /// 更新物品
        /// </summary>
        SERVER_ERROR UpdateItem(uint ownerId, byte flag, ushort pos, byte[] itemData);
        
        /// <summary>
        /// 删除物品
        /// </summary>
        SERVER_ERROR DeleteItem(uint itemId);
        
        /// <summary>
        /// 更新物品位置
        /// </summary>
        SERVER_ERROR UpdateItemPos(uint itemId, byte flag, ushort pos);
        
        /// <summary>
        /// 批量更新物品位置
        /// </summary>
        SERVER_ERROR UpdateItemPosEx(byte flag, ushort count, byte[] itemPosData);
        
        /// <summary>
        /// 更新物品所有者
        /// </summary>
        SERVER_ERROR UpdateItemOwner(uint itemId, uint ownerId, byte flag, ushort pos);
        
        /// <summary>
        /// 查询物品
        /// </summary>
        SERVER_ERROR QueryItems(uint ownerId, byte flag, out byte[] itemsData);
        
        /// <summary>
        /// 更新物品
        /// </summary>
        SERVER_ERROR UpdateItems(uint ownerId, byte flag, byte[] itemsData);
        
        /// <summary>
        /// 查询技能
        /// </summary>
        SERVER_ERROR QueryMagic(uint ownerId, out byte[] magicData);
        
        /// <summary>
        /// 更新技能
        /// </summary>
        SERVER_ERROR UpdateMagic(uint ownerId, byte[] magicData);
        
        /// <summary>
        /// 删除技能
        /// </summary>
        SERVER_ERROR DeleteMagic(uint ownerId, ushort magicId);
        
        /// <summary>
        /// 更新社区信息
        /// </summary>
        SERVER_ERROR UpdateCommunity(uint ownerId, string communityData);
        
        /// <summary>
        /// 查询社区信息
        /// </summary>
        SERVER_ERROR QueryCommunity(uint ownerId, out string communityData);
        
        /// <summary>
        /// 删除婚姻关系
        /// </summary>
        SERVER_ERROR DeleteMarriage(string name, string marriage);
        
        /// <summary>
        /// 删除师徒关系
        /// </summary>
        SERVER_ERROR DeleteTeacher(string name, string teacher);
        
        /// <summary>
        /// 删除学生
        /// </summary>
        SERVER_ERROR DeleteStudent(string teacher, string student);
        
        /// <summary>
        /// 解除好友关系
        /// </summary>
        SERVER_ERROR BreakFriend(string friend1, string friend2);
        
        /// <summary>
        /// 恢复行会
        /// </summary>
        SERVER_ERROR RestoreGuild(string name, string guildName);
        
        /// <summary>
        /// 添加积分
        /// </summary>
        SERVER_ERROR AddCredit(string name, uint count);
        
        /// <summary>
        /// 查询任务信息
        /// </summary>
        SERVER_ERROR QueryTaskInfo(uint ownerId, out byte[] taskInfoData);
        
        /// <summary>
        /// 更新任务信息
        /// </summary>
        SERVER_ERROR UpdateTaskInfo(uint ownerId, byte[] taskInfoData);
        
        /// <summary>
        /// 查询升级物品
        /// </summary>
        SERVER_ERROR QueryUpgradeItem(uint ownerId, out byte[] upgradeItemData);
        
        /// <summary>
        /// 执行SQL命令
        /// </summary>
        SERVER_ERROR ExecSqlCommand(string sql, out DataTable result);
        
        /// <summary>
        /// 关闭数据库连接
        /// </summary>
        void Close();
    }
}
