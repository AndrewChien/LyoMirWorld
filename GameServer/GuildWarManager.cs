namespace GameServer
{
    using System;
    using System.Collections.Generic;
    using MirCommon;
    using MirCommon.Utils;

    /// <summary>
    /// 行会战争信息结构体
    /// </summary>
    public class GuildWar
    {
        public GuildEx? RequestGuild { get; set; }    // 申请方行会
        public GuildEx? AttackGuild { get; set; }     // 被攻击方行会
        public ServerTimer WarTimer { get; set; }     // 战争计时器

        public GuildWar()
        {
            RequestGuild = null;
            AttackGuild = null;
            WarTimer = new ServerTimer();
        }

        public GuildWar(GuildEx requestGuild, GuildEx attackGuild, uint warDuration)
        {
            RequestGuild = requestGuild;
            AttackGuild = attackGuild;
            WarTimer = new ServerTimer();
            WarTimer.SaveTime(warDuration * 1000); // 转换为毫秒
        }

        /// <summary>
        /// 检查战争是否超时
        /// </summary>
        public bool IsTimeOut()
        {
            return WarTimer.IsTimeOut();
        }

        /// <summary>
        /// 获取剩余时间（毫秒）
        /// </summary>
        public uint GetRemainingTime()
        {
            return WarTimer.GetRemainingTime();
        }

        /// <summary>
        /// 获取战争信息
        /// </summary>
        public string GetWarInfo()
        {
            if (RequestGuild == null || AttackGuild == null)
                return "无效的战争信息";

            var remainingMinutes = GetRemainingTime() / 60000;
            return $"{RequestGuild.Name} 对 {AttackGuild.Name} 的战争，剩余时间: {remainingMinutes}分钟";
        }
    }


    /// <summary>
    /// 行会战争管理器
    /// </summary>
    public class GuildWarManager
    {
        private static GuildWarManager? _instance;
        public static GuildWarManager Instance => _instance ??= new GuildWarManager();

        private const int MAX_GUILD_WAR = 1024; // 最大战争数量

        // 对象池
        private readonly ObjectPool<GuildWar> _warPool;

        // 战争数组
        private readonly GuildWar?[] _guildWars = new GuildWar[MAX_GUILD_WAR];
        private readonly object _warLock = new();

        private uint _warCount = 0;      // 当前战争数量
        private uint _updatePtr = 0;     // 更新指针

        // 战争持续时间
        private uint _warDuration = 3 * 60 * 60; // 3小时，单位：秒

        private GuildWarManager()
        {
            // 初始化对象池，预创建100个对象，最大1000个
            _warPool = new ObjectPool<GuildWar>(() => new GuildWar(), 100, 1000);
        }

        /// <summary>
        /// 从对象池获取战争对象
        /// </summary>
        private GuildWar? GetWarFromPool()
        {
            return _warPool.Get();
        }

        /// <summary>
        /// 归还战争对象到对象池
        /// </summary>
        private void ReturnWarToPool(GuildWar war)
        {
            if (war == null)
                return;

            // 重置对象状态
            war.RequestGuild = null;
            war.AttackGuild = null;
            war.WarTimer = new ServerTimer();

            _warPool.Return(war);
        }

        /// <summary>
        /// 申请行会战争
        /// </summary>
        public bool RequestWar(GuildEx requestGuild, GuildEx attackGuild)
        {
            if (requestGuild == null || attackGuild == null)
            {
                SetError(1000, "行会参数无效");
                return false;
            }

            if (requestGuild.GuildId == attackGuild.GuildId)
            {
                SetError(1001, "不能对自己行会宣战");
                return false;
            }

            lock (_warLock)
            {
                // 检查是否达到最大战争数
                if (_warCount >= MAX_GUILD_WAR)
                {
                    SetError(1002, "已经达到最大战争数!");
                    return false;
                }

                // 检查是否为联盟行会，如果是则解除联盟
                if (requestGuild.IsAllyGuild(attackGuild))
                {
                    requestGuild.BreakAlly(attackGuild.Name);
                }

                if (attackGuild.IsAllyGuild(requestGuild))
                {
                    attackGuild.BreakAlly(requestGuild.Name);
                }

                // 检查是否已存在相同的战争
                for (uint i = 0; i < _warCount; i++)
                {
                    var existingWar = _guildWars[i];
                    if (existingWar == null)
                        continue;

                    if ((existingWar.AttackGuild == attackGuild && existingWar.RequestGuild == requestGuild) ||
                        (existingWar.AttackGuild == requestGuild && existingWar.RequestGuild == attackGuild))
                    {
                        SetError(1003, "无法重复申请行会战!");
                        return false;
                    }
                }

                // 从对象池获取战争对象
                var newWar = GetWarFromPool();
                if (newWar == null)
                {
                    SetError(1004, "当前战争资源紧缺，无法进行行会战!");
                    return false;
                }

                // 检查双方是否都可以添加敌对行会
                if (!attackGuild.AddKillGuild(requestGuild))
                {
                    SetError(1005, "和对方进行行会战的行会已经达到上限，请稍候再试");
                    return false;
                }

                if (!requestGuild.AddKillGuild(attackGuild))
                {
                    attackGuild.RemoveKillGuild(requestGuild);
                    SetError(1006, "和您的行会进行行会战的行会已经达到上限，请稍候再试");
                    return false;
                }

                // 设置战争信息
                newWar.RequestGuild = requestGuild;
                newWar.AttackGuild = attackGuild;
                newWar.WarTimer.SaveTime(_warDuration * 1000); // 转换为毫秒

                // 添加到战争数组
                _guildWars[_warCount] = newWar;
                _warCount++;

                // 发送战争开始消息
                string warMessage = $"{requestGuild.Name}和{attackGuild.Name}的行会战争开始，持续三小时";
                attackGuild.SendWords(warMessage);
                attackGuild.ReviewAroundNameColor();
                requestGuild.SendWords(warMessage);
                requestGuild.ReviewAroundNameColor();

                LogManager.Default.Info($"行会战争开始: {requestGuild.Name} vs {attackGuild.Name}");
                return true;
            }
        }

        /// <summary>
        /// 更新战争状态
        /// </summary>
        public void Update()
        {
            lock (_warLock)
            {
                if (_warCount == 0)
                    return;

                if (_updatePtr >= _warCount)
                    _updatePtr = 0;

                var currentWar = _guildWars[_updatePtr];
                if (currentWar == null)
                {
                    _updatePtr++;
                    return;
                }

                // 检查战争是否超时
                if (currentWar.IsTimeOut())
                {
                    EndWar(currentWar);
                }

                _updatePtr++;
            }
        }

        /// <summary>
        /// 结束战争
        /// </summary>
        private void EndWar(GuildWar war)
        {
            if (war.RequestGuild == null || war.AttackGuild == null)
                return;

            // 移除敌对关系
            war.AttackGuild.RemoveKillGuild(war.RequestGuild);
            war.RequestGuild.RemoveKillGuild(war.AttackGuild);

            // 刷新名字颜色
            war.AttackGuild.ReviewAroundNameColor();
            war.RequestGuild.ReviewAroundNameColor();

            // 发送战争结束消息
            string endMessage = $"{war.RequestGuild.Name}和{war.AttackGuild.Name}的行会战争结束";
            war.AttackGuild.SendWords(endMessage);
            war.RequestGuild.SendWords(endMessage);

            LogManager.Default.Info($"行会战争结束: {war.RequestGuild.Name} vs {war.AttackGuild.Name}");

            // 从数组中移除战争
            RemoveWar(war);
        }

        /// <summary>
        /// 从数组中移除战争
        /// </summary>
        private void RemoveWar(GuildWar war)
        {
            lock (_warLock)
            {
                // 查找战争索引
                int warIndex = -1;
                for (int i = 0; i < _warCount; i++)
                {
                    if (_guildWars[i] == war)
                    {
                        warIndex = i;
                        break;
                    }
                }

                if (warIndex == -1)
                    return;

                // 归还对象到池中
                ReturnWarToPool(war);

                // 用最后一个元素替换当前元素
                _warCount--;
                _guildWars[warIndex] = _guildWars[_warCount];
                _guildWars[_warCount] = null;

                // 调整更新指针
                if (_updatePtr >= _warCount)
                    _updatePtr = 0;
            }
        }

        /// <summary>
        /// 强制结束战争（GM命令等）
        /// </summary>
        public bool ForceEndWar(uint requestGuildId, uint attackGuildId)
        {
            lock (_warLock)
            {
                for (int i = 0; i < _warCount; i++)
                {
                    var war = _guildWars[i];
                    if (war == null || war.RequestGuild == null || war.AttackGuild == null)
                        continue;

                    if ((war.RequestGuild.GuildId == requestGuildId && war.AttackGuild.GuildId == attackGuildId) ||
                        (war.RequestGuild.GuildId == attackGuildId && war.AttackGuild.GuildId == requestGuildId))
                    {
                        EndWar(war);
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// 获取所有进行中的战争
        /// </summary>
        public List<GuildWar> GetAllWars()
        {
            lock (_warLock)
            {
                var wars = new List<GuildWar>();
                for (int i = 0; i < _warCount; i++)
                {
                    if (_guildWars[i] != null)
                    {
                        wars.Add(_guildWars[i]!);
                    }
                }
                return wars;
            }
        }

        /// <summary>
        /// 获取行会参与的战争
        /// </summary>
        public List<GuildWar> GetGuildWars(GuildEx guild)
        {
            if (guild == null)
                return new List<GuildWar>();

            lock (_warLock)
            {
                var wars = new List<GuildWar>();
                for (int i = 0; i < _warCount; i++)
                {
                    var war = _guildWars[i];
                    if (war == null || war.RequestGuild == null || war.AttackGuild == null)
                        continue;

                    if (war.RequestGuild.GuildId == guild.GuildId || war.AttackGuild.GuildId == guild.GuildId)
                    {
                        wars.Add(war);
                    }
                }
                return wars;
            }
        }

        /// <summary>
        /// 检查两个行会是否处于战争状态
        /// </summary>
        public bool AreGuildsAtWar(GuildEx guild1, GuildEx guild2)
        {
            if (guild1 == null || guild2 == null)
                return false;

            lock (_warLock)
            {
                for (int i = 0; i < _warCount; i++)
                {
                    var war = _guildWars[i];
                    if (war == null || war.RequestGuild == null || war.AttackGuild == null)
                        continue;

                    if ((war.RequestGuild.GuildId == guild1.GuildId && war.AttackGuild.GuildId == guild2.GuildId) ||
                        (war.RequestGuild.GuildId == guild2.GuildId && war.AttackGuild.GuildId == guild1.GuildId))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// 获取战争统计信息
        /// </summary>
        public (int totalWars, int activeWars) GetStatistics()
        {
            lock (_warLock)
            {
                int activeWars = 0;
                for (int i = 0; i < _warCount; i++)
                {
                    var war = _guildWars[i];
                    if (war != null && !war.IsTimeOut())
                    {
                        activeWars++;
                    }
                }
                return ((int)_warCount, activeWars);
            }
        }

        /// <summary>
        /// 设置战争持续时间（秒）
        /// </summary>
        public void SetWarDuration(uint durationInSeconds)
        {
            _warDuration = durationInSeconds;
            LogManager.Default.Info($"行会战争持续时间设置为: {durationInSeconds}秒 ({durationInSeconds / 3600}小时)");
        }

        /// <summary>
        /// 获取战争持续时间
        /// </summary>
        public uint GetWarDuration()
        {
            return _warDuration;
        }

        /// <summary>
        /// 清理所有战争（服务器关闭时调用）
        /// </summary>
        public void ClearAllWars()
        {
            lock (_warLock)
            {
                for (int i = 0; i < _warCount; i++)
                {
                    var war = _guildWars[i];
                    if (war != null)
                    {
                        // 结束战争但不发送消息
                        if (war.RequestGuild != null && war.AttackGuild != null)
                        {
                            war.AttackGuild.RemoveKillGuild(war.RequestGuild);
                            war.RequestGuild.RemoveKillGuild(war.AttackGuild);
                        }
                        ReturnWarToPool(war);
                        _guildWars[i] = null;
                    }
                }
                _warCount = 0;
                _updatePtr = 0;
                LogManager.Default.Info("已清理所有行会战争");
            }
        }

        /// <summary>
        /// 设置错误信息
        /// </summary>
        private void SetError(int errorCode, string errorMessage)
        {
            LogManager.Default.Error($"行会战争错误 {errorCode}: {errorMessage}");
        }

        /// <summary>
        /// 获取管理器状态信息
        /// </summary>
        public string GetStatusInfo()
        {
            var stats = GetStatistics();
            return $"行会战争管理器状态: 总战争数={stats.totalWars}, 活跃战争={stats.activeWars}, 池可用对象={_warPool.AvailableCount}, 已创建对象={_warPool.CreatedCount}";
        }
    }
}
