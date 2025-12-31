namespace GameServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MirCommon;
    using MirCommon.Utils;

    /// <summary>
    /// 关系类型
    /// </summary>
    public enum RelationshipType
    {
        MasterApprentice = 0,    // 师徒
        Married = 1,             // 夫妻
        SwornBrother = 2,        // 结拜兄弟
        Friend = 3               // 好友
    }

    /// <summary>
    /// 关系状态
    /// </summary>
    public enum RelationshipStatus
    {
        Pending = 0,     // 等待确认
        Active = 1,      // 活跃中
        Broken = 2,      // 已解除
        Expired = 3      // 已过期
    }

    /// <summary>
    /// 关系信息
    /// </summary>
    public class Relationship
    {
        public uint RelationshipId { get; set; }
        public RelationshipType Type { get; set; }
        public uint Player1Id { get; set; }
        public string Player1Name { get; set; } = string.Empty;
        public uint Player2Id { get; set; }
        public string Player2Name { get; set; } = string.Empty;
        public RelationshipStatus Status { get; set; }
        public DateTime CreateTime { get; set; }
        public DateTime? ConfirmTime { get; set; }
        public DateTime? BreakTime { get; set; }
        public uint Experience { get; set; }
        public uint Level { get; set; }
        public string CustomName { get; set; } = string.Empty; // 自定义关系名称

        public Relationship(uint relationshipId, RelationshipType type, uint player1Id, string player1Name, uint player2Id, string player2Name)
        {
            RelationshipId = relationshipId;
            Type = type;
            Player1Id = player1Id;
            Player1Name = player1Name;
            Player2Id = player2Id;
            Player2Name = player2Name;
            Status = RelationshipStatus.Pending;
            CreateTime = DateTime.Now;
            Level = 1;
            Experience = 0;
        }

        /// <summary>
        /// 确认关系
        /// </summary>
        public void Confirm()
        {
            Status = RelationshipStatus.Active;
            ConfirmTime = DateTime.Now;
        }

        /// <summary>
        /// 解除关系
        /// </summary>
        public void Break()
        {
            Status = RelationshipStatus.Broken;
            BreakTime = DateTime.Now;
        }

        /// <summary>
        /// 增加经验
        /// </summary>
        public void AddExperience(uint amount)
        {
            Experience += amount;
            
            // 检查升级
            uint requiredExp = GetRequiredExperience();
            while (Experience >= requiredExp && Level < GetMaxLevel())
            {
                Experience -= requiredExp;
                Level++;
                requiredExp = GetRequiredExperience();
            }
        }

        /// <summary>
        /// 获取升级所需经验
        /// </summary>
        private uint GetRequiredExperience()
        {
            return Level * 1000;
        }

        /// <summary>
        /// 获取最大等级
        /// </summary>
        private uint GetMaxLevel()
        {
            return Type switch
            {
                RelationshipType.MasterApprentice => 10,
                RelationshipType.Married => 20,
                RelationshipType.SwornBrother => 15,
                RelationshipType.Friend => 5,
                _ => 5
            };
        }

        /// <summary>
        /// 获取关系加成
        /// </summary>
        public RelationshipBonus GetBonus()
        {
            return new RelationshipBonus
            {
                ExpBonus = GetExpBonus(),
                DropBonus = GetDropBonus(),
                DamageBonus = GetDamageBonus(),
                DefenseBonus = GetDefenseBonus()
            };
        }

        /// <summary>
        /// 获取经验加成
        /// </summary>
        private float GetExpBonus()
        {
            return Type switch
            {
                RelationshipType.MasterApprentice => 0.05f + Level * 0.01f, // 5% + 1%每级
                RelationshipType.Married => 0.10f + Level * 0.005f,         // 10% + 0.5%每级
                RelationshipType.SwornBrother => 0.03f + Level * 0.008f,    // 3% + 0.8%每级
                RelationshipType.Friend => 0.01f + Level * 0.002f,          // 1% + 0.2%每级
                _ => 0
            };
        }

        /// <summary>
        /// 获取掉落加成
        /// </summary>
        private float GetDropBonus()
        {
            return Type switch
            {
                RelationshipType.Married => 0.05f + Level * 0.002f,         // 5% + 0.2%每级
                RelationshipType.SwornBrother => 0.03f + Level * 0.0015f,   // 3% + 0.15%每级
                _ => 0
            };
        }

        /// <summary>
        /// 获取伤害加成
        /// </summary>
        private float GetDamageBonus()
        {
            return Type switch
            {
                RelationshipType.Married => 0.03f + Level * 0.001f,         // 3% + 0.1%每级
                RelationshipType.SwornBrother => 0.05f + Level * 0.002f,    // 5% + 0.2%每级
                _ => 0
            };
        }

        /// <summary>
        /// 获取防御加成
        /// </summary>
        private float GetDefenseBonus()
        {
            return Type switch
            {
                RelationshipType.Married => 0.03f + Level * 0.001f,         // 3% + 0.1%每级
                RelationshipType.SwornBrother => 0.04f + Level * 0.0015f,   // 4% + 0.15%每级
                _ => 0
            };
        }
    }

    /// <summary>
    /// 关系加成
    /// </summary>
    public class RelationshipBonus
    {
        public float ExpBonus { get; set; }      // 经验加成
        public float DropBonus { get; set; }     // 掉落加成
        public float DamageBonus { get; set; }   // 伤害加成
        public float DefenseBonus { get; set; }  // 防御加成
    }

    /// <summary>
    /// 师徒关系特殊信息
    /// </summary>
    public class MasterApprenticeInfo
    {
        public uint MasterId { get; set; }
        public string MasterName { get; set; } = string.Empty;
        public List<uint> ApprenticeIds { get; set; } = new();
        public uint MaxApprentices { get; set; } = 3;
        public DateTime LastRewardTime { get; set; }
        public uint TotalTaught { get; set; }    // 总共教导的徒弟数
    }

    /// <summary>
    /// 夫妻关系特殊信息
    /// </summary>
    public class MarriageInfo
    {
        public uint HusbandId { get; set; }
        public string HusbandName { get; set; } = string.Empty;
        public uint WifeId { get; set; }
        public string WifeName { get; set; } = string.Empty;
        public DateTime WeddingTime { get; set; }
        public uint WeddingMap { get; set; }
        public uint WeddingX { get; set; }
        public uint WeddingY { get; set; }
        public string Vows { get; set; } = string.Empty; // 誓言
    }

    /// <summary>
    /// 关系管理器
    /// </summary>
    public class RelationshipManager
    {
        private static RelationshipManager? _instance;
        public static RelationshipManager Instance => _instance ??= new RelationshipManager();

        private readonly Dictionary<uint, Relationship> _relationships = new();
        private readonly Dictionary<uint, List<uint>> _playerRelationships = new(); // playerId -> relationshipIds
        private readonly Dictionary<uint, MasterApprenticeInfo> _masterInfo = new();
        private readonly Dictionary<uint, MarriageInfo> _marriageInfo = new();
        private readonly object _lock = new();
        
        private uint _nextRelationshipId = 10000;

        private RelationshipManager() { }

        /// <summary>
        /// 发起关系请求
        /// </summary>
        public Relationship? RequestRelationship(RelationshipType type, uint requesterId, string requesterName, uint targetId, string targetName)
        {
            if (requesterId == targetId)
                return null;

            // 检查是否已有相同类型的关系
            if (HasRelationship(type, requesterId, targetId))
                return null;

            // 检查关系限制
            if (!CanHaveRelationship(type, requesterId, targetId))
                return null;

            lock (_lock)
            {
                uint relationshipId = _nextRelationshipId++;
                var relationship = new Relationship(relationshipId, type, requesterId, requesterName, targetId, targetName);
                
                _relationships[relationshipId] = relationship;
                
                // 添加到玩家关系映射
                AddToPlayerRelationships(requesterId, relationshipId);
                AddToPlayerRelationships(targetId, relationshipId);
                
                LogManager.Default.Info($"{requesterName} 向 {targetName} 发起{GetTypeName(type)}关系请求");
                return relationship;
            }
        }

        /// <summary>
        /// 确认关系请求
        /// </summary>
        public bool ConfirmRelationship(uint relationshipId, uint confirmerId)
        {
            lock (_lock)
            {
                if (!_relationships.TryGetValue(relationshipId, out var relationship))
                    return false;

                // 只有被邀请者可以确认
                if (relationship.Player2Id != confirmerId)
                    return false;

                // 检查关系是否还在等待状态
                if (relationship.Status != RelationshipStatus.Pending)
                    return false;

                // 检查关系限制
                if (!CanHaveRelationship(relationship.Type, relationship.Player1Id, relationship.Player2Id))
                    return false;

                relationship.Confirm();
                
                // 如果是师徒关系，更新师徒信息
                if (relationship.Type == RelationshipType.MasterApprentice)
                {
                    UpdateMasterApprenticeInfo(relationship);
                }
                // 如果是夫妻关系，更新夫妻信息
                else if (relationship.Type == RelationshipType.Married)
                {
                    UpdateMarriageInfo(relationship);
                }
                
                LogManager.Default.Info($"{relationship.Player2Name} 确认了与 {relationship.Player1Name} 的{GetTypeName(relationship.Type)}关系");
                return true;
            }
        }

        /// <summary>
        /// 拒绝关系请求
        /// </summary>
        public bool RejectRelationship(uint relationshipId, uint rejecterId)
        {
            lock (_lock)
            {
                if (!_relationships.TryGetValue(relationshipId, out var relationship))
                    return false;

                // 只有被邀请者可以拒绝
                if (relationship.Player2Id != rejecterId)
                    return false;

                // 检查关系是否还在等待状态
                if (relationship.Status != RelationshipStatus.Pending)
                    return false;

                relationship.Break();
                
                LogManager.Default.Info($"{relationship.Player2Name} 拒绝了与 {relationship.Player1Name} 的{GetTypeName(relationship.Type)}关系请求");
                return true;
            }
        }

        /// <summary>
        /// 解除关系
        /// </summary>
        public bool BreakRelationship(uint relationshipId, uint breakerId)
        {
            lock (_lock)
            {
                if (!_relationships.TryGetValue(relationshipId, out var relationship))
                    return false;

                // 检查权限：只有关系双方可以解除关系
                if (relationship.Player1Id != breakerId && relationship.Player2Id != breakerId)
                    return false;

                // 检查关系是否活跃
                if (relationship.Status != RelationshipStatus.Active)
                    return false;

                relationship.Break();
                
                // 如果是师徒关系，更新师徒信息
                if (relationship.Type == RelationshipType.MasterApprentice)
                {
                    RemoveFromMasterApprenticeInfo(relationship);
                }
                // 如果是夫妻关系，更新夫妻信息
                else if (relationship.Type == RelationshipType.Married)
                {
                    RemoveMarriageInfo(relationship);
                }
                
                LogManager.Default.Info($"{GetPlayerName(breakerId)} 解除了与 {GetOtherPlayerName(relationship, breakerId)} 的{GetTypeName(relationship.Type)}关系");
                return true;
            }
        }

        /// <summary>
        /// 获取玩家关系
        /// </summary>
        public List<Relationship> GetPlayerRelationships(uint playerId, RelationshipType? type = null, RelationshipStatus? status = null)
        {
            lock (_lock)
            {
                if (!_playerRelationships.TryGetValue(playerId, out var relationshipIds))
                    return new List<Relationship>();

                var relationships = relationshipIds
                    .Select(id => _relationships.TryGetValue(id, out var rel) ? rel : null)
                    .Where(rel => rel != null)
                    .Cast<Relationship>()
                    .ToList();

                // 按类型过滤
                if (type.HasValue)
                {
                    relationships = relationships.Where(r => r.Type == type.Value).ToList();
                }

                // 按状态过滤
                if (status.HasValue)
                {
                    relationships = relationships.Where(r => r.Status == status.Value).ToList();
                }

                return relationships;
            }
        }

        /// <summary>
        /// 获取活跃关系
        /// </summary>
        public Relationship? GetActiveRelationship(uint playerId, RelationshipType type)
        {
            var relationships = GetPlayerRelationships(playerId, type, RelationshipStatus.Active);
            return relationships.FirstOrDefault();
        }

        /// <summary>
        /// 检查是否有关系
        /// </summary>
        public bool HasRelationship(RelationshipType type, uint player1Id, uint player2Id)
        {
            var relationships = GetPlayerRelationships(player1Id, type, RelationshipStatus.Active);
            return relationships.Any(r => (r.Player1Id == player2Id || r.Player2Id == player2Id));
        }

        /// <summary>
        /// 增加关系经验
        /// </summary>
        public void AddRelationshipExperience(uint relationshipId, uint amount)
        {
            lock (_lock)
            {
                if (_relationships.TryGetValue(relationshipId, out var relationship))
                {
                    relationship.AddExperience(amount);
                }
            }
        }

        /// <summary>
        /// 获取关系加成
        /// </summary>
        public RelationshipBonus GetRelationshipBonus(uint playerId)
        {
            var bonus = new RelationshipBonus();
            var activeRelationships = GetPlayerRelationships(playerId, status: RelationshipStatus.Active);
            
            foreach (var relationship in activeRelationships)
            {
                var relBonus = relationship.GetBonus();
                bonus.ExpBonus += relBonus.ExpBonus;
                bonus.DropBonus += relBonus.DropBonus;
                bonus.DamageBonus += relBonus.DamageBonus;
                bonus.DefenseBonus += relBonus.DefenseBonus;
            }
            
            return bonus;
        }

        /// <summary>
        /// 获取师徒信息
        /// </summary>
        public MasterApprenticeInfo? GetMasterApprenticeInfo(uint masterId)
        {
            lock (_lock)
            {
                _masterInfo.TryGetValue(masterId, out var info);
                return info;
            }
        }

        /// <summary>
        /// 获取夫妻信息
        /// </summary>
        public MarriageInfo? GetMarriageInfo(uint playerId)
        {
            lock (_lock)
            {
                // 查找玩家是丈夫还是妻子
                return _marriageInfo.Values.FirstOrDefault(m => m.HusbandId == playerId || m.WifeId == playerId);
            }
        }

        /// <summary>
        /// 检查是否可以建立关系
        /// </summary>
        private bool CanHaveRelationship(RelationshipType type, uint player1Id, uint player2Id)
        {
            // 检查是否已有相同类型的关系
            if (HasRelationship(type, player1Id, player2Id))
                return false;

            // 类型特定的限制
            switch (type)
            {
                case RelationshipType.MasterApprentice:
                    // 检查师徒限制
                    var masterInfo = GetMasterApprenticeInfo(player1Id);
                    if (masterInfo != null && masterInfo.ApprenticeIds.Count >= masterInfo.MaxApprentices)
                        return false;
                    
                    // 徒弟不能同时有多个师傅
                    var apprenticeRelationships = GetPlayerRelationships(player2Id, RelationshipType.MasterApprentice, RelationshipStatus.Active);
                    if (apprenticeRelationships.Count > 0)
                        return false;
                    break;
                    
                case RelationshipType.Married:
                    // 检查是否已结婚
                    var marriage1 = GetActiveRelationship(player1Id, RelationshipType.Married);
                    var marriage2 = GetActiveRelationship(player2Id, RelationshipType.Married);
                    if (marriage1 != null || marriage2 != null)
                        return false;
                    
                    // 检查性别：夫妻需要不同性别
                    var player1 = HumanPlayerMgr.Instance.FindById(player1Id);
                    var player2 = HumanPlayerMgr.Instance.FindById(player2Id);
                    
                    if (player1 == null || player2 == null)
                        return false; // 玩家不存在
                    
                    if (player1.Sex == player2.Sex)
                        return false; // 同性不能结婚
                    
                    break;
                    
                case RelationshipType.SwornBrother:
                    // 结拜兄弟数量限制
                    var brotherRelationships = GetPlayerRelationships(player1Id, RelationshipType.SwornBrother, RelationshipStatus.Active);
                    if (brotherRelationships.Count >= 5) // 最多5个结拜兄弟
                        return false;
                    break;
                    
                case RelationshipType.Friend:
                    // 好友数量限制
                    var friendRelationships = GetPlayerRelationships(player1Id, RelationshipType.Friend, RelationshipStatus.Active);
                    if (friendRelationships.Count >= 100) // 最多100个好友
                        return false;
                    break;
            }
            
            return true;
        }

        /// <summary>
        /// 更新师徒信息
        /// </summary>
        private void UpdateMasterApprenticeInfo(Relationship relationship)
        {
            if (relationship.Type != RelationshipType.MasterApprentice)
                return;

            lock (_lock)
            {
                if (!_masterInfo.TryGetValue(relationship.Player1Id, out var masterInfo))
                {
                    masterInfo = new MasterApprenticeInfo
                    {
                        MasterId = relationship.Player1Id,
                        MasterName = relationship.Player1Name
                    };
                    _masterInfo[relationship.Player1Id] = masterInfo;
                }

                // 添加徒弟
                if (!masterInfo.ApprenticeIds.Contains(relationship.Player2Id))
                {
                    masterInfo.ApprenticeIds.Add(relationship.Player2Id);
                }
            }
        }

        /// <summary>
        /// 从师徒信息中移除
        /// </summary>
        private void RemoveFromMasterApprenticeInfo(Relationship relationship)
        {
            if (relationship.Type != RelationshipType.MasterApprentice)
                return;

            lock (_lock)
            {
                if (_masterInfo.TryGetValue(relationship.Player1Id, out var masterInfo))
                {
                    masterInfo.ApprenticeIds.Remove(relationship.Player2Id);
                    
                    // 如果没有徒弟了，移除师傅信息
                    if (masterInfo.ApprenticeIds.Count == 0)
                    {
                        _masterInfo.Remove(relationship.Player1Id);
                    }
                }
            }
        }

        /// <summary>
        /// 更新夫妻信息
        /// </summary>
        private void UpdateMarriageInfo(Relationship relationship)
        {
            if (relationship.Type != RelationshipType.Married)
                return;

            lock (_lock)
            {
                // 确定丈夫和妻子
                var marriageInfo = new MarriageInfo
                {
                    HusbandId = relationship.Player1Id,
                    HusbandName = relationship.Player1Name,
                    WifeId = relationship.Player2Id,
                    WifeName = relationship.Player2Name,
                    WeddingTime = relationship.ConfirmTime ?? DateTime.Now,
                    Vows = "执子之手，与子偕老"
                };
                
                _marriageInfo[relationship.Player1Id] = marriageInfo;
            }
        }

        /// <summary>
        /// 移除夫妻信息
        /// </summary>
        private void RemoveMarriageInfo(Relationship relationship)
        {
            if (relationship.Type != RelationshipType.Married)
                return;

            lock (_lock)
            {
                _marriageInfo.Remove(relationship.Player1Id);
            }
        }

        /// <summary>
        /// 添加到玩家关系映射
        /// </summary>
        private void AddToPlayerRelationships(uint playerId, uint relationshipId)
        {
            if (!_playerRelationships.TryGetValue(playerId, out var relationships))
            {
                relationships = new List<uint>();
                _playerRelationships[playerId] = relationships;
            }
            
            if (!relationships.Contains(relationshipId))
            {
                relationships.Add(relationshipId);
            }
        }

        /// <summary>
        /// 获取关系类型名称
        /// </summary>
        private string GetTypeName(RelationshipType type)
        {
            return type switch
            {
                RelationshipType.MasterApprentice => "师徒",
                RelationshipType.Married => "夫妻",
                RelationshipType.SwornBrother => "结拜兄弟",
                RelationshipType.Friend => "好友",
                _ => "未知"
            };
        }

        /// <summary>
        /// 获取玩家名称
        /// </summary>
        private string GetPlayerName(uint playerId)
        {
            // 从HumanPlayerMgr获取玩家名称
            var player = HumanPlayerMgr.Instance.FindById(playerId);
            if (player != null)
                return player.Name;
            
            // 如果找不到玩家，尝试从关系缓存中查找
            lock (_lock)
            {
                // 查找玩家参与的关系
                foreach (var relationship in _relationships.Values)
                {
                    if (relationship.Player1Id == playerId)
                        return relationship.Player1Name;
                    if (relationship.Player2Id == playerId)
                        return relationship.Player2Name;
                }
            }
            
            // 如果都找不到，返回默认名称
            return $"玩家{playerId}";
        }

        /// <summary>
        /// 获取关系中的另一个玩家名称
        /// </summary>
        private string GetOtherPlayerName(Relationship relationship, uint playerId)
        {
            if (relationship.Player1Id == playerId)
                return relationship.Player2Name;
            else
                return relationship.Player1Name;
        }

        /// <summary>
        /// 获取关系统计信息
        /// </summary>
        public (int totalRelationships, int activeRelationships, int pendingRelationships) GetStatistics()
        {
            lock (_lock)
            {
                int total = _relationships.Count;
                int active = _relationships.Values.Count(r => r.Status == RelationshipStatus.Active);
                int pending = _relationships.Values.Count(r => r.Status == RelationshipStatus.Pending);
                
                return (total, active, pending);
            }
        }

        /// <summary>
        /// 清理过期关系
        /// </summary>
        public void CleanupExpiredRelationships()
        {
            lock (_lock)
            {
                var now = DateTime.Now;
                var expiredRelationships = _relationships.Values
                    .Where(r => r.Status == RelationshipStatus.Pending && 
                               (now - r.CreateTime).TotalDays > 7) // 7天未确认的关系过期
                    .ToList();
                
                foreach (var relationship in expiredRelationships)
                {
                    relationship.Status = RelationshipStatus.Expired;
                    LogManager.Default.Info($"关系 {relationship.RelationshipId} 已过期");
                }
            }
        }
    }
}
