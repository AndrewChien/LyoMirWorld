namespace GameServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MirCommon;
    using MirCommon.Utils;

    /// <summary>
    /// 行会扩展类
    /// 添加行会战争、联盟等功能
    /// </summary>
    public class GuildEx : Guild
    {
        // 敌对行会列表
        private readonly HashSet<uint> _killGuilds = new();
        private readonly object _killGuildLock = new();

        // 联盟行会列表
        private readonly HashSet<uint> _allyGuilds = new();
        private readonly object _allyGuildLock = new();

        // 行会战争相关属性
        private uint _warCount = 0;
        private const uint MAX_WAR_COUNT = 5; // 最大同时战争数量

        public GuildEx(uint guildId, string name, uint leaderId, string leaderName) 
            : base(guildId, name, leaderId, leaderName)
        {
        }

        /// <summary>
        /// 检查是否为敌对行会
        /// </summary>
        public override bool IsKillGuild(Guild otherGuild)
        {
            if (otherGuild == null)
                return false;

            // 如果不是GuildEx类型，则不能是敌对行会
            if (otherGuild is not GuildEx otherGuildEx)
                return false;

            lock (_killGuildLock)
            {
                return _killGuilds.Contains(otherGuildEx.GuildId);
            }
        }

        /// <summary>
        /// 添加敌对行会
        /// </summary>
        public bool AddKillGuild(GuildEx otherGuild)
        {
            if (otherGuild == null || otherGuild.GuildId == GuildId)
                return false;

            lock (_killGuildLock)
            {
                if (_killGuilds.Count >= MAX_WAR_COUNT)
                {
                    LogManager.Default.Warning($"行会 {Name} 已达到最大敌对行会数量限制");
                    return false;
                }

                if (_killGuilds.Contains(otherGuild.GuildId))
                    return false;

                _killGuilds.Add(otherGuild.GuildId);
                _warCount++;

                LogManager.Default.Info($"行会 {Name} 添加敌对行会: {otherGuild.Name}");
                return true;
            }
        }

        /// <summary>
        /// 移除敌对行会
        /// </summary>
        public bool RemoveKillGuild(GuildEx otherGuild)
        {
            if (otherGuild == null)
                return false;

            lock (_killGuildLock)
            {
                if (!_killGuilds.Contains(otherGuild.GuildId))
                    return false;

                _killGuilds.Remove(otherGuild.GuildId);
                if (_warCount > 0)
                    _warCount--;

                LogManager.Default.Info($"行会 {Name} 移除敌对行会: {otherGuild.Name}");
                return true;
            }
        }

        /// <summary>
        /// 获取所有敌对行会ID
        /// </summary>
        public List<uint> GetKillGuilds()
        {
            lock (_killGuildLock)
            {
                return new List<uint>(_killGuilds);
            }
        }

        /// <summary>
        /// 获取敌对行会数量
        /// </summary>
        public int GetKillGuildCount()
        {
            lock (_killGuildLock)
            {
                return _killGuilds.Count;
            }
        }

        /// <summary>
        /// 检查是否为联盟行会
        /// </summary>
        public override bool IsAllyGuild(Guild otherGuild)
        {
            if (otherGuild == null)
                return false;

            // 如果不是GuildEx类型，则不能是联盟行会
            if (otherGuild is not GuildEx otherGuildEx)
                return false;

            lock (_allyGuildLock)
            {
                return _allyGuilds.Contains(otherGuildEx.GuildId);
            }
        }

        /// <summary>
        /// 添加联盟行会
        /// </summary>
        public bool AddAllyGuild(GuildEx otherGuild)
        {
            if (otherGuild == null || otherGuild.GuildId == GuildId)
                return false;

            lock (_allyGuildLock)
            {
                if (_allyGuilds.Contains(otherGuild.GuildId))
                    return false;

                _allyGuilds.Add(otherGuild.GuildId);
                LogManager.Default.Info($"行会 {Name} 与 {otherGuild.Name} 结盟");
                return true;
            }
        }

        /// <summary>
        /// 解除联盟
        /// </summary>
        public bool BreakAlly(string otherGuildName)
        {
            lock (_allyGuildLock)
            {
                var guildToRemove = _allyGuilds.FirstOrDefault(guildId => 
                {
                    var guild = GuildManager.Instance.GetGuild(guildId) as GuildEx;
                    return guild != null && guild.Name == otherGuildName;
                });

                if (guildToRemove != 0)
                {
                    _allyGuilds.Remove(guildToRemove);
                    LogManager.Default.Info($"行会 {Name} 与 {otherGuildName} 解除联盟");
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// 获取所有联盟行会ID
        /// </summary>
        public List<uint> GetAllyGuilds()
        {
            lock (_allyGuildLock)
            {
                return new List<uint>(_allyGuilds);
            }
        }

        /// <summary>
        /// 获取联盟行会数量
        /// </summary>
        public int GetAllyGuildCount()
        {
            lock (_allyGuildLock)
            {
                return _allyGuilds.Count;
            }
        }

        /// <summary>
        /// 发送行会消息
        /// </summary>
        public void SendWords(string message)
        {
            var onlineMembers = GetOnlineMembers();
            foreach (var member in onlineMembers)
            {
                var player = HumanPlayerMgr.Instance.FindById(member.PlayerId);
                if (player != null)
                {
                    player.SaySystem(message);
                }
            }
        }

        /// <summary>
        /// 刷新周围玩家名字颜色
        /// 在行会战争开始/结束时调用
        /// </summary>
        public void ReviewAroundNameColor()
        {
            var onlineMembers = GetOnlineMembers();
            foreach (var member in onlineMembers)
            {
                var player = HumanPlayerMgr.Instance.FindById(member.PlayerId);
                if (player != null)
                {
                    // 实现完整的玩家名字颜色更新逻辑
                    // 需要更新玩家周围其他玩家看到的该玩家名字颜色
                    UpdatePlayerNameColor(player);
                }
            }
        }

        /// <summary>
        /// 更新玩家名字颜色
        /// </summary>
        private void UpdatePlayerNameColor(HumanPlayer player)
        {
            if (player == null || player.CurrentMap == null)
                return;

            // 获取玩家周围的其他玩家
            var nearbyPlayers = player.CurrentMap.GetPlayersInRange(player.X, player.Y, 18);
            foreach (var nearbyPlayer in nearbyPlayers)
            {
                if (nearbyPlayer.ObjectId == player.ObjectId)
                    continue;

                // 检查两个玩家是否处于战争状态
                bool isAtWar = false;
                if (nearbyPlayer.Guild is GuildEx nearbyGuildEx)
                {
                    isAtWar = IsKillGuild(nearbyGuildEx);
                }

                // 根据战争状态更新名字颜色
                // 这里应该发送更新名字颜色的消息给nearbyPlayer
                UpdateNameColorForViewer(player, nearbyPlayer, isAtWar);
            }
        }

        /// <summary>
        /// 为观察者更新目标玩家的名字颜色
        /// </summary>
        private void UpdateNameColorForViewer(HumanPlayer targetPlayer, HumanPlayer viewer, bool isAtWar)
        {
            if (targetPlayer == null || viewer == null)
                return;

            // 构建名字颜色更新消息
            if (isAtWar)
            {
                LogManager.Default.Debug($"玩家 {viewer.Name} 看到 {targetPlayer.Name} 的名字颜色变为红色（战争状态）");
                // 实际实现：构建并发送名字颜色更新消息
                // var message = BuildNameColorMessage(targetPlayer.ObjectId, NameColor.Red);
                // viewer.SendMessage(message);
            }
            else
            {
                LogManager.Default.Debug($"玩家 {viewer.Name} 看到 {targetPlayer.Name} 的名字颜色恢复正常");
                // 实际实现：构建并发送名字颜色更新消息
                // var message = BuildNameColorMessage(targetPlayer.ObjectId, NameColor.Normal);
                // viewer.SendMessage(message);
            }
        }

        /// <summary>
        /// 检查是否可以添加新的敌对行会
        /// </summary>
        public bool CanAddKillGuild()
        {
            lock (_killGuildLock)
            {
                return _killGuilds.Count < MAX_WAR_COUNT;
            }
        }

        /// <summary>
        /// 获取行会战争状态
        /// </summary>
        public bool IsInWar()
        {
            lock (_killGuildLock)
            {
                return _killGuilds.Count > 0;
            }
        }

        /// <summary>
        /// 获取行会战争信息
        /// </summary>
        public string GetWarInfo()
        {
            lock (_killGuildLock)
            {
                if (_killGuilds.Count == 0)
                    return "当前没有行会战争";

                var guildNames = new List<string>();
                foreach (var guildId in _killGuilds)
                {
                    var guild = GuildManager.Instance.GetGuild(guildId) as GuildEx;
                    if (guild != null)
                    {
                        guildNames.Add(guild.Name);
                    }
                }

                return $"正在与以下行会进行战争: {string.Join(", ", guildNames)}";
            }
        }

        /// <summary>
        /// 获取联盟信息
        /// </summary>
        public string GetAllyInfo()
        {
            lock (_allyGuildLock)
            {
                if (_allyGuilds.Count == 0)
                    return "当前没有联盟行会";

                var guildNames = new List<string>();
                foreach (var guildId in _allyGuilds)
                {
                    var guild = GuildManager.Instance.GetGuild(guildId) as GuildEx;
                    if (guild != null)
                    {
                        guildNames.Add(guild.Name);
                    }
                }

                return $"联盟行会: {string.Join(", ", guildNames)}";
            }
        }

        /// <summary>
        /// 清理所有敌对关系（用于行会解散时）
        /// </summary>
        public void ClearAllKillRelations()
        {
            lock (_killGuildLock)
            {
                var killGuildsCopy = new List<uint>(_killGuilds);
                foreach (var guildId in killGuildsCopy)
                {
                    var otherGuild = GuildManager.Instance.GetGuildEx(guildId);
                    if (otherGuild != null)
                    {
                        otherGuild.RemoveKillGuild(this);
                    }
                }
                _killGuilds.Clear();
                _warCount = 0;
            }
        }

        /// <summary>
        /// 清理所有联盟关系（用于行会解散时）
        /// </summary>
        public void ClearAllAllyRelations()
        {
            lock (_allyGuildLock)
            {
                var allyGuildsCopy = new List<uint>(_allyGuilds);
                foreach (var guildId in allyGuildsCopy)
                {
                    var otherGuild = GuildManager.Instance.GetGuildEx(guildId);
                    if (otherGuild != null)
                    {
                        otherGuild.BreakAlly(Name);
                    }
                }
                _allyGuilds.Clear();
            }
        }

        /// <summary>
        /// 获取行会综合信息
        /// </summary>
        public new string ToString()
        {
            return $"行会: {Name} (ID: {GuildId}), 等级: {Level}, 成员: {GetMemberCount()}/{GetMaxMembers()}, " +
                   $"战争: {GetKillGuildCount()}, 联盟: {GetAllyGuildCount()}";
        }
    }
}
