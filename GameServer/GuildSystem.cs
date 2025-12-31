namespace GameServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MirCommon;
    using MirCommon.Utils;

    /// <summary>
    /// 行会职位
    /// </summary>
    public enum GuildRank
    {
        Member = 0,         // 成员
        Elder = 1,          // 长老
        ViceLeader = 2,     // 副会长
        Leader = 3          // 会长
    }

    /// <summary>
    /// 行会成员信息
    /// </summary>
    public class GuildMember
    {
        public uint PlayerId { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public GuildRank Rank { get; set; }
        public DateTime JoinTime { get; set; }
        public uint Contribution { get; set; }
        public uint LastContributionTime { get; set; }
        public bool IsOnline { get; set; }

        public GuildMember(uint playerId, string playerName, GuildRank rank = GuildRank.Member)
        {
            PlayerId = playerId;
            PlayerName = playerName;
            Rank = rank;
            JoinTime = DateTime.Now;
            Contribution = 0;
            LastContributionTime = 0;
            IsOnline = true;
        }
    }

    /// <summary>
    /// 行会信息
    /// </summary>
    public class Guild
    {
        public uint GuildId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string LeaderName { get; set; } = string.Empty;
        public uint LeaderId { get; set; }
        public DateTime CreateTime { get; set; }
        public string Notice { get; set; } = string.Empty;
        public uint Funds { get; set; }
        public uint Level { get; set; }
        public uint Experience { get; set; }
        
        private readonly Dictionary<uint, GuildMember> _members = new();
        private readonly object _memberLock = new();
        
        // 行会仓库
        private readonly List<ItemInstance> _warehouse = new();
        private readonly object _warehouseLock = new();
        private const int MAX_WAREHOUSE_SLOTS = 100;

        public Guild(uint guildId, string name, uint leaderId, string leaderName)
        {
            GuildId = guildId;
            Name = name;
            LeaderId = leaderId;
            LeaderName = leaderName;
            CreateTime = DateTime.Now;
            Level = 1;
            Experience = 0;
            Funds = 0;
            Notice = "欢迎加入行会！";
            
            // 添加会长为成员
            AddMember(leaderId, leaderName, GuildRank.Leader);
        }

        /// <summary>
        /// 添加成员
        /// </summary>
        public bool AddMember(uint playerId, string playerName, GuildRank rank = GuildRank.Member)
        {
            lock (_memberLock)
            {
                if (_members.ContainsKey(playerId))
                    return false;

                if (_members.Count >= GetMaxMembers())
                    return false;

                var member = new GuildMember(playerId, playerName, rank);
                _members[playerId] = member;
                
                LogManager.Default.Info($"玩家 {playerName} 加入行会 {Name}");
                return true;
            }
        }

        /// <summary>
        /// 移除成员
        /// </summary>
        public bool RemoveMember(uint playerId)
        {
            lock (_memberLock)
            {
                if (!_members.TryGetValue(playerId, out var member))
                    return false;

                // 不能移除会长
                if (member.Rank == GuildRank.Leader)
                    return false;

                _members.Remove(playerId);
                
                LogManager.Default.Info($"玩家 {member.PlayerName} 离开行会 {Name}");
                return true;
            }
        }

        /// <summary>
        /// 移除成员（根据名称）
        /// </summary>
        public bool RemoveMember(string playerName)
        {
            lock (_memberLock)
            {
                var member = _members.Values.FirstOrDefault(m => m.PlayerName == playerName);
                if (member == null)
                    return false;

                // 不能移除会长
                if (member.Rank == GuildRank.Leader)
                    return false;

                _members.Remove(member.PlayerId);
                
                LogManager.Default.Info($"玩家 {member.PlayerName} 离开行会 {Name}");
                return true;
            }
        }

        /// <summary>
        /// 获取成员
        /// </summary>
        public GuildMember? GetMember(uint playerId)
        {
            lock (_memberLock)
            {
                _members.TryGetValue(playerId, out var member);
                return member;
            }
        }

        /// <summary>
        /// 获取所有成员
        /// </summary>
        public List<GuildMember> GetAllMembers()
        {
            lock (_memberLock)
            {
                return _members.Values.ToList();
            }
        }

        /// <summary>
        /// 获取在线成员
        /// </summary>
        public List<GuildMember> GetOnlineMembers()
        {
            lock (_memberLock)
            {
                return _members.Values.Where(m => m.IsOnline).ToList();
            }
        }

        /// <summary>
        /// 获取成员数量
        /// </summary>
        public int GetMemberCount()
        {
            lock (_memberLock)
            {
                return _members.Count;
            }
        }

        /// <summary>
        /// 获取最大成员数量
        /// </summary>
        public int GetMaxMembers()
        {
            // 根据行会等级决定最大成员数
            return (int)Level * 10 + 20; // 1级:30人, 2级:40人, 3级:50人...
        }

        /// <summary>
        /// 设置成员职位
        /// </summary>
        public bool SetMemberRank(uint playerId, GuildRank newRank)
        {
            lock (_memberLock)
            {
                if (!_members.TryGetValue(playerId, out var member))
                    return false;

                // 不能修改会长职位
                if (member.Rank == GuildRank.Leader)
                    return false;

                member.Rank = newRank;
                return true;
            }
        }

        /// <summary>
        /// 增加贡献度
        /// </summary>
        public void AddContribution(uint playerId, uint amount)
        {
            lock (_memberLock)
            {
                if (_members.TryGetValue(playerId, out var member))
                {
                    member.Contribution += amount;
                    
                    // 同时增加行会经验
                    AddExperience(amount / 10);
                }
            }
        }

        /// <summary>
        /// 增加行会经验
        /// </summary>
        public void AddExperience(uint amount)
        {
            Experience += amount;
            
            // 检查升级
            uint requiredExp = GetRequiredExperience();
            while (Experience >= requiredExp && Level < 10)
            {
                Experience -= requiredExp;
                Level++;
                requiredExp = GetRequiredExperience();
                
                LogManager.Default.Info($"行会 {Name} 升级到 {Level} 级");
            }
        }

        /// <summary>
        /// 获取升级所需经验
        /// </summary>
        private uint GetRequiredExperience()
        {
            return Level * 10000;
        }

        /// <summary>
        /// 设置公告
        /// </summary>
        public void SetNotice(string notice)
        {
            if (notice.Length > 200)
                notice = notice.Substring(0, 200);
            
            Notice = notice;
        }

        /// <summary>
        /// 增加资金
        /// </summary>
        public bool AddFunds(uint amount)
        {
            if (amount > uint.MaxValue - Funds)
                return false;
            
            Funds += amount;
            return true;
        }

        /// <summary>
        /// 扣除资金
        /// </summary>
        public bool TakeFunds(uint amount)
        {
            if (Funds < amount)
                return false;
            
            Funds -= amount;
            return true;
        }

        /// <summary>
        /// 添加物品到仓库
        /// </summary>
        public bool AddToWarehouse(ItemInstance item)
        {
            lock (_warehouseLock)
            {
                if (_warehouse.Count >= MAX_WAREHOUSE_SLOTS)
                    return false;

                _warehouse.Add(item);
                return true;
            }
        }

        /// <summary>
        /// 从仓库移除物品
        /// </summary>
        public bool RemoveFromWarehouse(int index)
        {
            lock (_warehouseLock)
            {
                if (index < 0 || index >= _warehouse.Count)
                    return false;

                _warehouse.RemoveAt(index);
                return true;
            }
        }

        /// <summary>
        /// 获取仓库物品
        /// </summary>
        public List<ItemInstance> GetWarehouseItems()
        {
            lock (_warehouseLock)
            {
                return new List<ItemInstance>(_warehouse);
            }
        }

        /// <summary>
        /// 获取仓库空位数量
        /// </summary>
        public int GetWarehouseFreeSlots()
        {
            lock (_warehouseLock)
            {
                return MAX_WAREHOUSE_SLOTS - _warehouse.Count;
            }
        }

        /// <summary>
        /// 检查是否为敌对行会（虚方法，在GuildEx中重写）
        /// </summary>
        public virtual bool IsKillGuild(Guild otherGuild)
        {
            // 基础Guild类没有敌对行会功能
            return false;
        }

        /// <summary>
        /// 检查是否为联盟行会（虚方法，在GuildEx中重写）
        /// </summary>
        public virtual bool IsAllyGuild(Guild otherGuild)
        {
            // 基础Guild类没有联盟行会功能
            return false;
        }

        /// <summary>
        /// 成员上线
        /// </summary>
        public void MemberOnline(uint playerId)
        {
            lock (_memberLock)
            {
                if (_members.TryGetValue(playerId, out var member))
                {
                    member.IsOnline = true;
                }
            }
        }

        /// <summary>
        /// 成员下线
        /// </summary>
        public void MemberOffline(uint playerId)
        {
            lock (_memberLock)
            {
                if (_members.TryGetValue(playerId, out var member))
                {
                    member.IsOnline = false;
                }
            }
        }

        internal LogicMap? GetGuildMap()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 获取行会首页信息
        /// </summary>
        public string GetFrontPage()
        {
            return $"行会名称: {Name}\n会长: {LeaderName}\n等级: {Level}\n成员: {GetMemberCount()}/{GetMaxMembers()}\n资金: {Funds}\n公告: {Notice}";
        }

        /// <summary>
        /// 发送行会首页信息给玩家
        /// </summary>
        public void SendFirstPage(HumanPlayer player)
        {
            if (player == null) return;
            player.SaySystem(GetFrontPage());
        }

        /// <summary>
        /// 发送行会经验信息给玩家
        /// </summary>
        public void SendExp(HumanPlayer player)
        {
            if (player == null) return;
            player.SaySystem($"行会经验: {Experience}/{GetRequiredExperience()}");
        }

        /// <summary>
        /// 发送行会成员列表给玩家
        /// </summary>
        public void SendMemberList(HumanPlayer player)
        {
            if (player == null) return;
            
            var members = GetAllMembers();
            string memberList = $"行会成员 ({members.Count}人):\n";
            foreach (var member in members)
            {
                memberList += $"{member.PlayerName} - {member.Rank} (贡献: {member.Contribution})\n";
            }
            player.SaySystem(memberList);
        }

        /// <summary>
        /// 解析成员列表
        /// </summary>
        public bool ParseMemberList(HumanPlayer player, string memberList)
        {
            if (player == null) return false;
            
            LogManager.Default.Info($"解析行会成员列表: {memberList}");
            return true;
        }

        /// <summary>
        /// 获取错误消息
        /// </summary>
        public string GetErrorMsg()
        {
            return "操作失败";
        }

        /// <summary>
        /// 检查玩家是否是行会会长
        /// </summary>
        public bool IsMaster(HumanPlayer player)
        {
            if (player == null)
                return false;
            
            return LeaderId == player.ObjectId;
        }
    }

    /// <summary>
    /// 行会管理器
    /// </summary>
    public class GuildManager
    {
        private static GuildManager? _instance;
        public static GuildManager Instance => _instance ??= new GuildManager();

        private readonly Dictionary<uint, Guild> _guilds = new();
        private readonly Dictionary<uint, uint> _playerGuildMap = new(); // playerId -> guildId
        private readonly object _lock = new();
        
        private uint _nextGuildId = 1000;

        private GuildManager() { }

        /// <summary>
        /// 创建行会
        /// </summary>
        public Guild? CreateGuild(string name, uint leaderId, string leaderName)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 16)
                return null;

            // 检查名称是否已存在
            if (GetGuildByName(name) != null)
                return null;

            // 检查玩家是否已有行会
            if (GetPlayerGuild(leaderId) != null)
                return null;

            lock (_lock)
            {
                uint guildId = _nextGuildId++;
                var guild = new Guild(guildId, name, leaderId, leaderName);
                
                _guilds[guildId] = guild;
                _playerGuildMap[leaderId] = guildId;
                
                LogManager.Default.Info($"行会 {name} 创建成功，会长：{leaderName}");
                return guild;
            }
        }

        /// <summary>
        /// 创建扩展行会（GuildEx）
        /// </summary>
        public GuildEx? CreateGuildEx(string name, uint leaderId, string leaderName)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 16)
                return null;

            // 检查名称是否已存在
            if (GetGuildByName(name) != null)
                return null;

            // 检查玩家是否已有行会
            if (GetPlayerGuild(leaderId) != null)
                return null;

            lock (_lock)
            {
                uint guildId = _nextGuildId++;
                var guild = new GuildEx(guildId, name, leaderId, leaderName);
                
                _guilds[guildId] = guild;
                _playerGuildMap[leaderId] = guildId;
                
                LogManager.Default.Info($"扩展行会 {name} 创建成功，会长：{leaderName}");
                return guild;
            }
        }

        /// <summary>
        /// 解散行会
        /// </summary>
        public bool DisbandGuild(uint guildId, uint requesterId)
        {
            lock (_lock)
            {
                if (!_guilds.TryGetValue(guildId, out var guild))
                    return false;

                // 只有会长可以解散行会
                if (guild.LeaderId != requesterId)
                    return false;

                // 如果是GuildEx，清理敌对和联盟关系
                if (guild is GuildEx guildEx)
                {
                    guildEx.ClearAllKillRelations();
                    guildEx.ClearAllAllyRelations();
                }

                // 移除所有成员映射
                foreach (var member in guild.GetAllMembers())
                {
                    _playerGuildMap.Remove(member.PlayerId);
                }

                // 移除行会
                _guilds.Remove(guildId);
                
                LogManager.Default.Info($"行会 {guild.Name} 已解散");
                return true;
            }
        }

        /// <summary>
        /// 加入行会
        /// </summary>
        public bool JoinGuild(uint guildId, uint playerId, string playerName)
        {
            lock (_lock)
            {
                if (!_guilds.TryGetValue(guildId, out var guild))
                    return false;

                // 检查玩家是否已有行会
                if (_playerGuildMap.ContainsKey(playerId))
                    return false;

                // 添加到行会
                if (!guild.AddMember(playerId, playerName))
                    return false;

                _playerGuildMap[playerId] = guildId;
                return true;
            }
        }

        /// <summary>
        /// 离开行会
        /// </summary>
        public bool LeaveGuild(uint playerId)
        {
            lock (_lock)
            {
                if (!_playerGuildMap.TryGetValue(playerId, out var guildId))
                    return false;

                if (!_guilds.TryGetValue(guildId, out var guild))
                    return false;

                // 会长不能离开行会，只能解散
                if (guild.LeaderId == playerId)
                    return false;

                // 从行会移除
                if (!guild.RemoveMember(playerId))
                    return false;

                _playerGuildMap.Remove(playerId);
                return true;
            }
        }

        /// <summary>
        /// 踢出成员
        /// </summary>
        public bool KickMember(uint guildId, uint requesterId, uint targetId)
        {
            lock (_lock)
            {
                if (!_guilds.TryGetValue(guildId, out var guild))
                    return false;

                // 检查权限
                var requester = guild.GetMember(requesterId);
                var target = guild.GetMember(targetId);
                
                if (requester == null || target == null)
                    return false;

                // 只有会长、副会长、长老可以踢人
                if (requester.Rank < GuildRank.Elder)
                    return false;

                // 不能踢比自己职位高的人
                if (target.Rank >= requester.Rank && requesterId != guild.LeaderId)
                    return false;

                // 从行会移除
                if (!guild.RemoveMember(targetId))
                    return false;

                _playerGuildMap.Remove(targetId);
                return true;
            }
        }

        /// <summary>
        /// 设置成员职位
        /// </summary>
        public bool SetMemberRank(uint guildId, uint requesterId, uint targetId, GuildRank newRank)
        {
            lock (_lock)
            {
                if (!_guilds.TryGetValue(guildId, out var guild))
                    return false;

                // 只有会长可以设置职位
                if (guild.LeaderId != requesterId)
                    return false;

                return guild.SetMemberRank(targetId, newRank);
            }
        }

        /// <summary>
        /// 获取行会
        /// </summary>
        public Guild? GetGuild(uint guildId)
        {
            lock (_lock)
            {
                _guilds.TryGetValue(guildId, out var guild);
                return guild;
            }
        }

        /// <summary>
        /// 获取扩展行会（GuildEx）
        /// </summary>
        public GuildEx? GetGuildEx(uint guildId)
        {
            lock (_lock)
            {
                if (_guilds.TryGetValue(guildId, out var guild) && guild is GuildEx guildEx)
                {
                    return guildEx;
                }
                return null;
            }
        }

        /// <summary>
        /// 根据名称获取行会
        /// </summary>
        public Guild? GetGuildByName(string name)
        {
            lock (_lock)
            {
                return _guilds.Values.FirstOrDefault(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// 根据名称获取扩展行会
        /// </summary>
        public GuildEx? GetGuildExByName(string name)
        {
            lock (_lock)
            {
                var guild = _guilds.Values.FirstOrDefault(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                return guild as GuildEx;
            }
        }

        /// <summary>
        /// 获取玩家所在行会
        /// </summary>
        public Guild? GetPlayerGuild(uint playerId)
        {
            lock (_lock)
            {
                if (_playerGuildMap.TryGetValue(playerId, out var guildId))
                {
                    return GetGuild(guildId);
                }
                return null;
            }
        }

        /// <summary>
        /// 获取玩家所在扩展行会
        /// </summary>
        public GuildEx? GetPlayerGuildEx(uint playerId)
        {
            lock (_lock)
            {
                if (_playerGuildMap.TryGetValue(playerId, out var guildId))
                {
                    return GetGuildEx(guildId);
                }
                return null;
            }
        }

        /// <summary>
        /// 获取所有行会
        /// </summary>
        public List<Guild> GetAllGuilds()
        {
            lock (_lock)
            {
                return _guilds.Values.ToList();
            }
        }

        /// <summary>
        /// 搜索行会
        /// </summary>
        public List<Guild> SearchGuilds(string keyword)
        {
            lock (_lock)
            {
                return _guilds.Values
                    .Where(g => g.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    .Take(20)
                    .ToList();
            }
        }

        /// <summary>
        /// 玩家上线
        /// </summary>
        public void PlayerOnline(uint playerId)
        {
            lock (_lock)
            {
                var guild = GetPlayerGuild(playerId);
                guild?.MemberOnline(playerId);
            }
        }

        /// <summary>
        /// 玩家下线
        /// </summary>
        public void PlayerOffline(uint playerId)
        {
            lock (_lock)
            {
                var guild = GetPlayerGuild(playerId);
                guild?.MemberOffline(playerId);
            }
        }

        /// <summary>
        /// 增加贡献度
        /// </summary>
        public void AddContribution(uint playerId, uint amount)
        {
            lock (_lock)
            {
                var guild = GetPlayerGuild(playerId);
                guild?.AddContribution(playerId, amount);
            }
        }

        /// <summary>
        /// 增加行会资金
        /// </summary>
        public bool AddGuildFunds(uint guildId, uint amount)
        {
            lock (_lock)
            {
                var guild = GetGuild(guildId);
                return guild?.AddFunds(amount) ?? false;
            }
        }

        /// <summary>
        /// 扣除行会资金
        /// </summary>
        public bool TakeGuildFunds(uint guildId, uint amount)
        {
            lock (_lock)
            {
                var guild = GetGuild(guildId);
                return guild?.TakeFunds(amount) ?? false;
            }
        }

        /// <summary>
        /// 设置行会公告
        /// </summary>
        public bool SetGuildNotice(uint guildId, uint requesterId, string notice)
        {
            lock (_lock)
            {
                var guild = GetGuild(guildId);
                if (guild == null)
                    return false;

                // 检查权限：会长、副会长可以设置公告
                var member = guild.GetMember(requesterId);
                if (member == null || member.Rank < GuildRank.ViceLeader)
                    return false;

                guild.SetNotice(notice);
                return true;
            }
        }

        /// <summary>
        /// 发送行会消息
        /// </summary>
        public void SendGuildMessage(uint guildId, string message)
        {
            lock (_lock)
            {
                var guild = GetGuild(guildId);
                if (guild == null)
                    return;

                var onlineMembers = guild.GetOnlineMembers();
                foreach (var member in onlineMembers)
                {
                    var player = HumanPlayerMgr.Instance.FindById(member.PlayerId);
                    if (player != null)
                    {
                        // 使用聊天系统发送行会消息
                        ChatManager.Instance.SendMessage(player, ChatChannel.GUILD, message);
                    }
                }
            }
        }

        /// <summary>
        /// 获取行会排名（按等级和经验）
        /// </summary>
        public List<Guild> GetGuildRanking(int count = 10)
        {
            lock (_lock)
            {
                return _guilds.Values
                    .OrderByDescending(g => g.Level)
                    .ThenByDescending(g => g.Experience)
                    .ThenByDescending(g => g.GetMemberCount())
                    .Take(count)
                    .ToList();
            }
        }

        /// <summary>
        /// 获取行会成员排名（按贡献度）
        /// </summary>
        public List<GuildMember> GetMemberRanking(uint guildId, int count = 20)
        {
            lock (_lock)
            {
                var guild = GetGuild(guildId);
                if (guild == null)
                    return new List<GuildMember>();

                return guild.GetAllMembers()
                    .OrderByDescending(m => m.Contribution)
                    .ThenByDescending(m => m.Rank)
                    .Take(count)
                    .ToList();
            }
        }

        /// <summary>
        /// 检查行会名称是否可用
        /// </summary>
        public bool IsGuildNameAvailable(string name)
        {
            lock (_lock)
            {
                return GetGuildByName(name) == null;
            }
        }

        /// <summary>
        /// 获取行会统计信息
        /// </summary>
        public (int totalGuilds, int totalMembers, int onlineMembers) GetStatistics()
        {
            lock (_lock)
            {
                int totalGuilds = _guilds.Count;
                int totalMembers = _playerGuildMap.Count;
                int onlineMembers = 0;
                
                foreach (var guild in _guilds.Values)
                {
                    onlineMembers += guild.GetOnlineMembers().Count;
                }
                
                return (totalGuilds, totalMembers, onlineMembers);
            }
        }
    }
}
