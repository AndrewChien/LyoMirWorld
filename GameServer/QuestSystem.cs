using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using MirCommon;
using MirCommon.Utils;

// 类型别名：Player = HumanPlayer
using Player = GameServer.HumanPlayer;

namespace GameServer
{
    /// <summary>
    /// 任务类型
    /// </summary>
    public enum QuestType
    {
        Main = 0,           // 主线任务
        Side = 1,           // 支线任务
        Daily = 2,          // 每日任务
        Weekly = 3,         // 周常任务
        Repeatable = 4,     // 可重复任务
        Achievement = 5     // 成就任务
    }

    /// <summary>
    /// 任务目标类型
    /// </summary>
    public enum QuestObjectiveType
    {
        KillMonster = 0,    // 击杀怪物
        CollectItem = 1,    // 收集物品
        TalkToNPC = 2,      // 对话NPC
        ReachLevel = 3,     // 达到等级
        ReachLocation = 4,  // 到达地点
        UseItem = 5,        // 使用物品
        LearnSkill = 6,     // 学习技能
        EquipItem = 7,      // 装备物品
        KillPlayer = 8,     // 击杀玩家
        CompleteQuest = 9   // 完成其他任务
    }

    /// <summary>
    /// 任务状态
    /// </summary>
    public enum QuestStatus
    {
        NotStarted = 0,     // 未开始
        InProgress = 1,     // 进行中
        Completed = 2,      // 已完成
        Failed = 3,         // 失败
        Abandoned = 4       // 已放弃
    }

    /// <summary>
    /// 任务目标
    /// </summary>
    public class QuestObjective
    {
        public int ObjectiveId { get; set; }
        public QuestObjectiveType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public int TargetId { get; set; }       // 目标ID（怪物ID/物品ID/NPC ID等）
        public int RequiredCount { get; set; }  // 需要数量
        public int MapId { get; set; }          // 地图限制（0表示不限）
        public bool Optional { get; set; }      // 是否可选

        public QuestObjective(int id, QuestObjectiveType type, string description, int targetId, int count)
        {
            ObjectiveId = id;
            Type = type;
            Description = description;
            TargetId = targetId;
            RequiredCount = count;
        }
    }

    /// <summary>
    /// 任务奖励
    /// </summary>
    public class QuestReward
    {
        public uint Exp { get; set; }           // 经验
        public uint Gold { get; set; }          // 金币
        public List<QuestItemReward> Items { get; set; } = new();

        public class QuestItemReward
        {
            public int ItemId { get; set; }
            public int Count { get; set; }
            public bool Optional { get; set; }  // 可选奖励

            public QuestItemReward(int itemId, int count, bool optional = false)
            {
                ItemId = itemId;
                Count = count;
                Optional = optional;
            }
        }
    }

    /// <summary>
    /// 任务定义
    /// </summary>
    public class QuestDefinition
    {
        public int QuestId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public QuestType Type { get; set; }
        
        // 需求
        public int RequireLevel { get; set; }
        public int RequireJob { get; set; } = -1;   // -1表示所有职业
        public List<int> RequireQuests { get; set; } = new();  // 前置任务
        
        // 接取和提交
        public int StartNPCId { get; set; }
        public int EndNPCId { get; set; }
        
        // 目标
        public List<QuestObjective> Objectives { get; set; } = new();
        
        // 奖励
        public QuestReward Reward { get; set; } = new();
        
        // 时间限制
        public int TimeLimit { get; set; }      // 时间限制（秒）0表示无限制
        
        // 其他
        public bool AutoComplete { get; set; }  // 是否自动完成
        public bool Repeatable { get; set; }    // 是否可重复
        public int RepeatInterval { get; set; } // 重复间隔（秒）

        public QuestDefinition(int questId, string name, QuestType type)
        {
            QuestId = questId;
            Name = name;
            Type = type;
        }

        public bool CanAccept(Player player)
        {
            // 检查等级
            if (player.Level < RequireLevel)
                return false;

            // 检查职业
            if (RequireJob != -1 && player.Job != RequireJob)
                return false;

            // 检查前置任务
            foreach (var questId in RequireQuests)
            {
                if (!player.QuestManager.HasCompletedQuest(questId))
                    return false;
            }

            return true;
        }
    }

    /// <summary>
    /// 任务进度
    /// </summary>
    public class QuestProgress
    {
        public int QuestId { get; set; }
        public QuestDefinition Definition { get; set; }
        public QuestStatus Status { get; set; }
        public DateTime AcceptTime { get; set; }
        public DateTime? CompleteTime { get; set; }
        
        // 目标进度
        public Dictionary<int, int> ObjectiveProgress { get; set; } = new();

        public QuestProgress(QuestDefinition definition)
        {
            Definition = definition;
            QuestId = definition.QuestId;
            Status = QuestStatus.InProgress;
            AcceptTime = DateTime.Now;

            // 初始化目标进度
            foreach (var objective in definition.Objectives)
            {
                ObjectiveProgress[objective.ObjectiveId] = 0;
            }
        }

        public void UpdateProgress(int objectiveId, int count)
        {
            if (ObjectiveProgress.ContainsKey(objectiveId))
            {
                ObjectiveProgress[objectiveId] += count;
                
                // 检查是否完成
                var objective = Definition.Objectives.FirstOrDefault(o => o.ObjectiveId == objectiveId);
                if (objective != null)
                {
                    ObjectiveProgress[objectiveId] = Math.Min(
                        ObjectiveProgress[objectiveId],
                        objective.RequiredCount
                    );
                }
            }
        }

        public bool IsObjectiveComplete(int objectiveId)
        {
            if (!ObjectiveProgress.TryGetValue(objectiveId, out var progress))
                return false;

            var objective = Definition.Objectives.FirstOrDefault(o => o.ObjectiveId == objectiveId);
            return objective != null && progress >= objective.RequiredCount;
        }

        public bool IsQuestComplete()
        {
            foreach (var objective in Definition.Objectives)
            {
                if (objective.Optional)
                    continue;

                if (!IsObjectiveComplete(objective.ObjectiveId))
                    return false;
            }
            return true;
        }

        public float GetCompletionRate()
        {
            if (Definition.Objectives.Count == 0)
                return 0f;

            int total = 0;
            int completed = 0;

            foreach (var objective in Definition.Objectives)
            {
                if (objective.Optional)
                    continue;

                total++;
                if (IsObjectiveComplete(objective.ObjectiveId))
                    completed++;
            }

            return total > 0 ? (float)completed / total : 0f;
        }

        public bool IsExpired()
        {
            if (Definition.TimeLimit <= 0)
                return false;

            return (DateTime.Now - AcceptTime).TotalSeconds > Definition.TimeLimit;
        }
    }

    /// <summary>
    /// 玩家任务管理器
    /// </summary>
    public class PlayerQuestManager
    {
        private readonly Player _player;
        private readonly Dictionary<int, QuestProgress> _activeQuests = new();
        private readonly HashSet<int> _completedQuests = new();
        private readonly Dictionary<int, DateTime> _questCooldowns = new();
        private readonly object _lock = new();
        
        public int MaxActiveQuests { get; set; } = 20;

        public PlayerQuestManager(Player player)
        {
            _player = player;
        }

        public bool AcceptQuest(int questId)
        {
            lock (_lock)
            {
                var definition = QuestDefinitionManager.Instance.GetDefinition(questId);
                if (definition == null)
                    return false;

                // 检查是否已接取
                if (_activeQuests.ContainsKey(questId))
                    return false;

                // 检查任务上限
                if (_activeQuests.Count >= MaxActiveQuests)
                    return false;

                // 检查是否可接取
                if (!definition.CanAccept(_player))
                    return false;

                // 检查冷却
                if (_questCooldowns.TryGetValue(questId, out var cooldownEnd))
                {
                    if (DateTime.Now < cooldownEnd)
                        return false;
                }

                // 创建任务进度
                var progress = new QuestProgress(definition);
                _activeQuests[questId] = progress;

                LogManager.Default.Info($"{_player.Name} 接取任务: {definition.Name}");
                return true;
            }
        }

        public bool AbandonQuest(int questId)
        {
            lock (_lock)
            {
                if (_activeQuests.TryGetValue(questId, out var progress))
                {
                    progress.Status = QuestStatus.Abandoned;
                    _activeQuests.Remove(questId);
                    
                    LogManager.Default.Info($"{_player.Name} 放弃任务: {progress.Definition.Name}");
                    return true;
                }
                return false;
            }
        }

        public bool CompleteQuest(int questId)
        {
            lock (_lock)
            {
                if (!_activeQuests.TryGetValue(questId, out var progress))
                    return false;

                if (!progress.IsQuestComplete())
                    return false;

                // 给予奖励
                GiveRewards(progress.Definition);

                // 标记完成
                progress.Status = QuestStatus.Completed;
                progress.CompleteTime = DateTime.Now;
                _completedQuests.Add(questId);
                _activeQuests.Remove(questId);

                // 设置冷却
                if (progress.Definition.Repeatable && progress.Definition.RepeatInterval > 0)
                {
                    _questCooldowns[questId] = DateTime.Now.AddSeconds(progress.Definition.RepeatInterval);
                }

                LogManager.Default.Info($"{_player.Name} 完成任务: {progress.Definition.Name}");
                return true;
            }
        }

        private void GiveRewards(QuestDefinition definition)
        {
            var reward = definition.Reward;

            // 经验
            if (reward.Exp > 0)
            {
                _player.AddExp(reward.Exp);
                LogManager.Default.Debug($"{_player.Name} 获得经验: {reward.Exp}");
            }

            // 金币
            if (reward.Gold > 0)
            {
                _player.Gold += reward.Gold;
                LogManager.Default.Debug($"{_player.Name} 获得金币: {reward.Gold}");
            }

            // 物品奖励
            if (reward.Items.Count > 0)
            {
                var itemManager = ItemManager.Instance;
                var optionalItems = new List<QuestReward.QuestItemReward>();
                
                foreach (var itemReward in reward.Items)
                {
                    if (itemReward.Optional)
                    {
                        // 可选奖励，先收集起来
                        optionalItems.Add(itemReward);
                        continue;
                    }

                    // 必得奖励
                    GiveItemReward(itemReward);
                }

                // 处理可选奖励（如果有的话）
                if (optionalItems.Count > 0)
                {
                    // 这里可以添加逻辑让玩家选择可选奖励
                    var random = new Random();
                    var selectedReward = optionalItems[random.Next(optionalItems.Count)];
                    GiveItemReward(selectedReward);
                    
                    LogManager.Default.Debug($"{_player.Name} 获得可选奖励: {selectedReward.ItemId} x{selectedReward.Count}");
                }
            }
        }

        /// <summary>
        /// 给予物品奖励
        /// </summary>
        private void GiveItemReward(QuestReward.QuestItemReward itemReward)
        {
            var itemManager = ItemManager.Instance;
            
            // 获取物品定义
            var itemDef = itemManager.GetDefinition(itemReward.ItemId);
            if (itemDef == null)
            {
                LogManager.Default.Warning($"任务奖励物品不存在: {itemReward.ItemId}");
                return;
            }

            // 检查背包空间
            if (!HasSpaceForItem(itemDef, itemReward.Count))
            {
                // 如果背包空间不足，尝试发送邮件
                SendRewardByMail(itemReward);
                return;
            }

            // 添加物品到背包
            for (int i = 0; i < itemReward.Count; i++)
            {
                var item = itemManager.CreateItem(itemReward.ItemId);
                if (item != null)
                {
                    if (!_player.Inventory.AddItem(item))
                    {
                        // 如果添加失败，尝试发送邮件
                        SendRewardByMail(itemReward);
                        break;
                    }
                }
            }

            LogManager.Default.Debug($"{_player.Name} 获得物品奖励: {itemDef.Name} x{itemReward.Count}");
        }

        /// <summary>
        /// 检查背包是否有足够空间
        /// </summary>
        private bool HasSpaceForItem(ItemDefinition itemDef, int count)
        {
            // 如果是可堆叠物品，检查是否有堆叠空间
            if (itemDef.MaxStack > 1)
            {
                int currentCount = _player.Inventory.GetItemCount(itemDef.ItemId);
                int maxStackable = itemDef.MaxStack;
                
                // 计算现有堆叠中还能放多少
                int existingSpace = 0;
                var allItems = _player.Inventory.GetAllItems();
                foreach (var kvp in allItems)
                {
                    var item = kvp.Value;
                    if (item.ItemId == itemDef.ItemId)
                    {
                        existingSpace += itemDef.MaxStack - item.Count;
                    }
                }
                
                if (existingSpace >= count)
                    return true;
                
                // 还需要新的格子
                int neededSlots = (int)Math.Ceiling((double)(count - existingSpace) / itemDef.MaxStack);
                int freeSlots = _player.Inventory.MaxSlots - allItems.Count;
                return freeSlots >= neededSlots;
            }
            else
            {
                // 不可堆叠物品，需要足够的空位
                int freeSlots = _player.Inventory.MaxSlots - _player.Inventory.GetUsedSlots();
                return freeSlots >= count;
            }
        }

        /// <summary>
        /// 通过邮件发送奖励
        /// </summary>
        private void SendRewardByMail(QuestReward.QuestItemReward itemReward)
        {
            // 这里应该实现邮件系统
            LogManager.Default.Warning($"{_player.Name} 背包空间不足，无法获得任务奖励物品: {itemReward.ItemId} x{itemReward.Count}");
            
            // 这里应该发送邮件给玩家
            // MailSystem.Instance.SendItemMail(_player.PlayerId, "任务奖励", $"任务奖励物品: {itemReward.ItemId} x{itemReward.Count}", itemReward.ItemId, itemReward.Count);
        }

        public void UpdateProgress(QuestObjectiveType type, int targetId, int count = 1)
        {
            lock (_lock)
            {
                foreach (var progress in _activeQuests.Values)
                {
                    if (progress.Status != QuestStatus.InProgress)
                        continue;

                    foreach (var objective in progress.Definition.Objectives)
                    {
                        if (objective.Type == type && objective.TargetId == targetId)
                        {
                            progress.UpdateProgress(objective.ObjectiveId, count);
                            
                            if (progress.IsObjectiveComplete(objective.ObjectiveId))
                            {
                                LogManager.Default.Debug(
                                    $"{_player.Name} 完成任务目标: {objective.Description}"
                                );
                            }

                            // 检查自动完成
                            if (progress.IsQuestComplete() && progress.Definition.AutoComplete)
                            {
                                CompleteQuest(progress.QuestId);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 当拾取物品时更新任务进度
        /// </summary>
        public void OnItemPickup(int itemId, int count = 1)
        {
            UpdateProgress(QuestObjectiveType.CollectItem, itemId, count);
        }

        /// <summary>
        /// 当击杀怪物时更新任务进度
        /// </summary>
        public void OnMonsterKill(int monsterId)
        {
            UpdateProgress(QuestObjectiveType.KillMonster, monsterId, 1);
        }

        /// <summary>
        /// 当攻击怪物时更新任务进度（用于攻击事件）
        /// </summary>
        public void OnAttackMonster(AliveObject target, int damage)
        {
            if (target is Monster monster)
            {
                // 这里可以记录攻击事件，但任务进度通常是在击杀时更新
                // 如果需要攻击事件，可以在这里添加逻辑
            }
        }

        /// <summary>
        /// 当装备物品时更新任务进度
        /// </summary>
        public void OnItemEquip(int itemId)
        {
            UpdateProgress(QuestObjectiveType.EquipItem, itemId, 1);
        }

        /// <summary>
        /// 当学习技能时更新任务进度
        /// </summary>
        public void OnSkillLearn(int skillId)
        {
            UpdateProgress(QuestObjectiveType.LearnSkill, skillId, 1);
        }

        /// <summary>
        /// 当与NPC对话时更新任务进度
        /// </summary>
        public void OnNPCTalk(int npcId)
        {
            UpdateProgress(QuestObjectiveType.TalkToNPC, npcId, 1);
        }

        /// <summary>
        /// 当等级提升时更新任务进度
        /// </summary>
        public void OnLevelUp(int level)
        {
            UpdateProgress(QuestObjectiveType.ReachLevel, level, 1);
        }

        public void Update()
        {
            lock (_lock)
            {
                var expiredQuests = new List<int>();

                // 检查过期任务
                foreach (var kvp in _activeQuests)
                {
                    if (kvp.Value.IsExpired())
                    {
                        kvp.Value.Status = QuestStatus.Failed;
                        expiredQuests.Add(kvp.Key);
                    }
                }

                // 移除过期任务
                foreach (var questId in expiredQuests)
                {
                    _activeQuests.Remove(questId);
                    LogManager.Default.Info($"{_player.Name} 任务失败(超时): {questId}");
                }
            }
        }

        public QuestProgress? GetQuest(int questId)
        {
            lock (_lock)
            {
                _activeQuests.TryGetValue(questId, out var progress);
                return progress;
            }
        }

        public bool HasActiveQuest(int questId)
        {
            lock (_lock)
            {
                return _activeQuests.ContainsKey(questId);
            }
        }

        public bool HasCompletedQuest(int questId)
        {
            lock (_lock)
            {
                return _completedQuests.Contains(questId);
            }
        }

        public List<QuestProgress> GetActiveQuests()
        {
            lock (_lock)
            {
                return _activeQuests.Values.ToList();
            }
        }

        public List<int> GetCompletedQuests()
        {
            lock (_lock)
            {
                return _completedQuests.ToList();
            }
        }

        public List<QuestDefinition> GetAvailableQuests()
        {
            return QuestDefinitionManager.Instance.GetAllDefinitions()
                .Where(q => q.CanAccept(_player) && 
                           !HasActiveQuest(q.QuestId) &&
                           (!_completedQuests.Contains(q.QuestId) || q.Repeatable))
                .ToList();
        }

        /// <summary>
        /// 更新任务
        /// </summary>
        public void UpdateTask(int taskId, int state, int param1 = 0, int param2 = 0, int param3 = 0)
        {
            lock (_lock)
            {
                LogManager.Default.Info($"更新任务: 任务ID={taskId}, 状态={state}, 参数1={param1}, 参数2={param2}, 参数3={param3}");
                
                // 检查任务是否存在
                if (!_activeQuests.TryGetValue(taskId, out var progress))
                {
                    LogManager.Default.Warning($"任务不存在: {taskId}");
                    return;
                }

                // 更新任务状态
                if (state >= 0)
                {
                    progress.Status = (QuestStatus)state;
                    LogManager.Default.Debug($"更新任务状态: {progress.Status}");
                }

                // 更新任务参数（如果有）
                // 这里可以根据param1, param2, param3更新任务进度
                if (param1 > 0 || param2 > 0 || param3 > 0)
                {
                    // 更新任务目标进度
                    UpdateTaskProgress(progress, param1, param2, param3);
                }

                // 检查任务是否完成
                if (progress.IsQuestComplete())
                {
                    LogManager.Default.Info($"任务完成: {progress.Definition.Name}");
                    CompleteQuest(taskId);
                }
                else if (progress.IsExpired())
                {
                    LogManager.Default.Info($"任务过期: {progress.Definition.Name}");
                    progress.Status = QuestStatus.Failed;
                    _activeQuests.Remove(taskId);
                }
            }
        }

        /// <summary>
        /// 删除任务
        /// </summary>
        public bool DeleteTask(int taskId)
        {
            lock (_lock)
            {
                LogManager.Default.Info($"删除任务: 任务ID={taskId}");
                
                // 检查任务是否存在
                if (!_activeQuests.TryGetValue(taskId, out var progress))
                {
                    LogManager.Default.Warning($"任务不存在: {taskId}");
                    return false;
                }

                // 从活动任务中移除
                _activeQuests.Remove(taskId);
                
                // 添加到已完成任务列表（标记为已放弃）
                _completedQuests.Add(taskId);
                
                LogManager.Default.Info($"已删除任务: {progress.Definition.Name}");
                return true;
            }
        }

        /// <summary>
        /// 更新任务进度
        /// </summary>
        private void UpdateTaskProgress(QuestProgress progress, int param1, int param2, int param3)
        {
            // 根据任务类型更新进度
            foreach (var objective in progress.Definition.Objectives)
            {
                // 如果参数1大于0，更新第一个目标
                if (param1 > 0 && objective.ObjectiveId == 1)
                {
                    progress.UpdateProgress(objective.ObjectiveId, param1);
                    LogManager.Default.Debug($"更新目标1进度: +{param1}");
                }
                
                // 如果参数2大于0，更新第二个目标
                if (param2 > 0 && objective.ObjectiveId == 2)
                {
                    progress.UpdateProgress(objective.ObjectiveId, param2);
                    LogManager.Default.Debug($"更新目标2进度: +{param2}");
                }
                
                // 如果参数3大于0，更新第三个目标
                if (param3 > 0 && objective.ObjectiveId == 3)
                {
                    progress.UpdateProgress(objective.ObjectiveId, param3);
                    LogManager.Default.Debug($"更新目标3进度: +{param3}");
                }
            }
        }

        //internal object GetQuestByItemId(int itemId)
        //{
        //    throw new NotImplementedException();
        //}
    }

    /// <summary>
    /// 任务定义管理器
    /// </summary>
    public class QuestDefinitionManager
    {
        private static QuestDefinitionManager? _instance;
        public static QuestDefinitionManager Instance => _instance ??= new QuestDefinitionManager();

        private readonly ConcurrentDictionary<int, QuestDefinition> _definitions = new();

        private QuestDefinitionManager()
        {
            InitializeDefaultQuests();
        }

        private void InitializeDefaultQuests()
        {
            // 新手任务1
            var quest1 = new QuestDefinition(1001, "初入江湖", QuestType.Main)
            {
                Description = "与村长对话，了解这个世界",
                RequireLevel = 1,
                StartNPCId = 1001,
                EndNPCId = 1001
            };
            quest1.Objectives.Add(new QuestObjective(
                1, QuestObjectiveType.TalkToNPC, "与村长对话", 1001, 1
            ));
            quest1.Reward.Exp = 100;
            quest1.Reward.Gold = 50;
            AddDefinition(quest1);

            // 新手任务2
            var quest2 = new QuestDefinition(1002, "清理害虫", QuestType.Main)
            {
                Description = "击杀5只骷髅",
                RequireLevel = 1,
                StartNPCId = 1001,
                EndNPCId = 1001
            };
            quest2.RequireQuests.Add(1001);
            quest2.Objectives.Add(new QuestObjective(
                1, QuestObjectiveType.KillMonster, "击杀骷髅", 1, 5
            ));
            quest2.Reward.Exp = 200;
            quest2.Reward.Gold = 100;
            quest2.Reward.Items.Add(new QuestReward.QuestItemReward(3001, 10)); // 10个小红药
            AddDefinition(quest2);

            // 新手任务3
            var quest3 = new QuestDefinition(1003, "装备自己", QuestType.Main)
            {
                Description = "装备一把武器",
                RequireLevel = 1,
                StartNPCId = 1001,
                EndNPCId = 1001,
                AutoComplete = true
            };
            quest3.RequireQuests.Add(1002);
            quest3.Objectives.Add(new QuestObjective(
                1, QuestObjectiveType.EquipItem, "装备武器", 1001, 1
            ));
            quest3.Reward.Exp = 150;
            quest3.Reward.Gold = 200;
            AddDefinition(quest3);

            // 新手任务4
            var quest4 = new QuestDefinition(1004, "学习技能", QuestType.Main)
            {
                Description = "学习一个技能",
                RequireLevel = 1,
                StartNPCId = 1007,
                EndNPCId = 1007,
                AutoComplete = true
            };
            quest4.RequireQuests.Add(1003);
            quest4.Objectives.Add(new QuestObjective(
                1, QuestObjectiveType.LearnSkill, "学习技能", 0, 1
            ));
            quest4.Reward.Exp = 300;
            quest4.Reward.Gold = 500;
            AddDefinition(quest4);

            // 每日任务
            var dailyQuest = new QuestDefinition(2001, "每日清理", QuestType.Daily)
            {
                Description = "每日击杀10只怪物",
                RequireLevel = 5,
                StartNPCId = 1001,
                EndNPCId = 1001,
                Repeatable = true,
                RepeatInterval = 86400 // 24小时
            };
            dailyQuest.Objectives.Add(new QuestObjective(
                1, QuestObjectiveType.KillMonster, "击杀任意怪物", 0, 10
            ));
            dailyQuest.Reward.Exp = 500;
            dailyQuest.Reward.Gold = 1000;
            AddDefinition(dailyQuest);

            // 收集任务
            var collectQuest = new QuestDefinition(3001, "收集材料", QuestType.Side)
            {
                Description = "收集10个铁矿石",
                RequireLevel = 5,
                StartNPCId = 1006,
                EndNPCId = 1006
            };
            collectQuest.Objectives.Add(new QuestObjective(
                1, QuestObjectiveType.CollectItem, "收集铁矿石", 4001, 10
            ));
            collectQuest.Reward.Exp = 300;
            collectQuest.Reward.Gold = 500;
            AddDefinition(collectQuest);

            // 等级任务
            var levelQuest = new QuestDefinition(4001, "成长之路", QuestType.Achievement)
            {
                Description = "达到10级",
                RequireLevel = 1,
                StartNPCId = 0,
                EndNPCId = 0,
                AutoComplete = true
            };
            levelQuest.Objectives.Add(new QuestObjective(
                1, QuestObjectiveType.ReachLevel, "达到10级", 10, 1
            ));
            levelQuest.Reward.Exp = 1000;
            levelQuest.Reward.Gold = 2000;
            AddDefinition(levelQuest);

            LogManager.Default.Info($"已加载 {_definitions.Count} 个任务定义");
        }

        public void AddDefinition(QuestDefinition definition)
        {
            _definitions[definition.QuestId] = definition;
        }

        public QuestDefinition? GetDefinition(int questId)
        {
            _definitions.TryGetValue(questId, out var definition);
            return definition;
        }

        public List<QuestDefinition> GetAllDefinitions()
        {
            return _definitions.Values.ToList();
        }

        public List<QuestDefinition> GetQuestsByType(QuestType type)
        {
            return _definitions.Values
                .Where(q => q.Type == type)
                .ToList();
        }

        public List<QuestDefinition> GetQuestsByNPC(int npcId)
        {
            return _definitions.Values
                .Where(q => q.StartNPCId == npcId || q.EndNPCId == npcId)
                .ToList();
        }
    }
}
