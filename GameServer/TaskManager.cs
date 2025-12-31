using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MirCommon.Utils;

namespace GameServer
{
    /// <summary>
    /// 任务类型
    /// </summary>
    public enum TaskType
    {
        Main = 0,       // 主线任务
        Side = 1,       // 支线任务
        Daily = 2,      // 日常任务
        Weekly = 3,     // 周常任务
        Achievement = 4, // 成就任务
        Event = 5       // 活动任务
    }

    /// <summary>
    /// 任务状态
    /// </summary>
    public enum TaskStatus
    {
        NotStarted = 0, // 未开始
        InProgress = 1, // 进行中
        Completed = 2,  // 已完成
        Failed = 3,     // 已失败
        Abandoned = 4   // 已放弃
    }

    /// <summary>
    /// 任务目标
    /// </summary>
    public class TaskObjective
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty; // "monster", "item", "npc", "location"
        public string TargetName { get; set; } = string.Empty;
        public int RequiredCount { get; set; }
        public int CurrentCount { get; set; }
        public bool IsCompleted => CurrentCount >= RequiredCount;
    }

    /// <summary>
    /// 任务奖励
    /// </summary>
    public class TaskReward
    {
        public int Exp { get; set; }
        public int Gold { get; set; }
        public List<TaskRewardItem> Items { get; set; } = new();
        public List<int> Buffs { get; set; } = new();
        public string? NextTask { get; set; }
    }

    /// <summary>
    /// 任务奖励物品
    /// </summary>
    public class TaskRewardItem
    {
        public string ItemName { get; set; } = string.Empty;
        public int Count { get; set; }
        public int Chance { get; set; } = 100; // 掉落几率（百分比）
    }

    /// <summary>
    /// 任务定义
    /// </summary>
    public class TaskDefinition
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public TaskType Type { get; set; }
        public int RequiredLevel { get; set; }
        public string? PredecessorTask { get; set; } // 前置任务
        public List<TaskObjective> Objectives { get; set; } = new();
        public TaskReward Reward { get; set; } = new();
        public int TimeLimit { get; set; } // 时间限制（秒），0表示无限制
        public int MaxAttempts { get; set; } = 1; // 最大尝试次数
        public bool Repeatable { get; set; } // 是否可重复
        public int RepeatInterval { get; set; } // 重复间隔（秒）
    }

    /// <summary>
    /// 玩家任务
    /// </summary>
    public class PlayerTask
    {
        public int TaskId { get; set; }
        public TaskStatus Status { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? CompleteTime { get; set; }
        public DateTime? ExpireTime { get; set; }
        public int Attempts { get; set; }
        public List<TaskObjective> Objectives { get; set; } = new();
        public bool RewardClaimed { get; set; }
    }

    /// <summary>
    /// 任务管理器
    /// </summary>
    public class TaskManager
    {
        private static TaskManager? _instance;
        public static TaskManager Instance => _instance ??= new TaskManager();

        private readonly Dictionary<int, TaskDefinition> _taskDefinitions = new();
        private readonly Dictionary<uint, List<PlayerTask>> _playerTasks = new();

        private TaskManager() { }

        /// <summary>
        /// 加载任务配置
        /// </summary>
        public bool Load(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                LogManager.Default.Warning($"任务目录不存在: {directoryPath}");
                return false;
            }

            try
            {
                var taskFiles = Directory.GetFiles(directoryPath, "*.txt", SearchOption.AllDirectories);
                int loadedCount = 0;

                foreach (var file in taskFiles)
                {
                    if (LoadTaskFile(file))
                        loadedCount++;
                }

                LogManager.Default.Info($"加载任务配置: {loadedCount} 个任务文件, {_taskDefinitions.Count} 个任务定义");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载任务配置失败: {directoryPath}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 加载单个任务文件
        /// </summary>
        private bool LoadTaskFile(string filePath)
        {
            try
            {
                var lines = SmartReader.ReadAllLines(filePath);
                TaskDefinition? currentTask = null;
                int taskCount = 0;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    var trimmedLine = line.Trim();
                    
                    // 任务定义开始: [TaskId:任务名称]
                    if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    {
                        var taskDef = trimmedLine.Substring(1, trimmedLine.Length - 2);
                        var parts = taskDef.Split(':');
                        
                        if (parts.Length >= 2 && int.TryParse(parts[0], out int taskId))
                        {
                            currentTask = new TaskDefinition
                            {
                                Id = taskId,
                                Name = parts[1].Trim()
                            };
                            
                            if (parts.Length > 2)
                                currentTask.Description = parts[2].Trim();
                            
                            _taskDefinitions[taskId] = currentTask;
                            taskCount++;
                        }
                    }
                    // 任务属性
                    else if (currentTask != null && trimmedLine.Contains("="))
                    {
                        var parts = trimmedLine.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            var key = parts[0].Trim();
                            var value = parts[1].Trim();
                            
                            ParseTaskProperty(currentTask, key, value);
                        }
                    }
                    // 任务目标
                    else if (currentTask != null && trimmedLine.StartsWith("objective:"))
                    {
                        var objectiveStr = trimmedLine.Substring(10).Trim();
                        var objective = ParseTaskObjective(objectiveStr);
                        if (objective != null)
                            currentTask.Objectives.Add(objective);
                    }
                    // 任务奖励
                    else if (currentTask != null && trimmedLine.StartsWith("reward:"))
                    {
                        var rewardStr = trimmedLine.Substring(7).Trim();
                        ParseTaskReward(currentTask.Reward, rewardStr);
                    }
                }

                LogManager.Default.Debug($"加载任务文件: {Path.GetFileName(filePath)} ({taskCount} 个任务)");
                return taskCount > 0;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载任务文件失败: {filePath}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 解析任务属性
        /// </summary>
        private void ParseTaskProperty(TaskDefinition task, string key, string value)
        {
            switch (key.ToLower())
            {
                case "type":
                    if (Enum.TryParse<TaskType>(value, true, out var type))
                        task.Type = type;
                    break;
                case "requiredlevel":
                    if (int.TryParse(value, out int level))
                        task.RequiredLevel = level;
                    break;
                case "predecessor":
                    task.PredecessorTask = value;
                    break;
                case "timelimit":
                    if (int.TryParse(value, out int timeLimit))
                        task.TimeLimit = timeLimit;
                    break;
                case "maxattempts":
                    if (int.TryParse(value, out int maxAttempts))
                        task.MaxAttempts = maxAttempts;
                    break;
                case "repeatable":
                    task.Repeatable = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
                    break;
                case "repeatinterval":
                    if (int.TryParse(value, out int repeatInterval))
                        task.RepeatInterval = repeatInterval;
                    break;
            }
        }

        /// <summary>
        /// 解析任务目标
        /// </summary>
        private TaskObjective? ParseTaskObjective(string objectiveStr)
        {
            var parts = objectiveStr.Split(',');
            if (parts.Length < 4)
                return null;

            if (int.TryParse(parts[0], out int id) && int.TryParse(parts[3], out int requiredCount))
            {
                return new TaskObjective
                {
                    Id = id,
                    Description = parts[1].Trim(),
                    TargetType = parts[2].Trim(),
                    TargetName = parts.Length > 4 ? parts[4].Trim() : string.Empty,
                    RequiredCount = requiredCount,
                    CurrentCount = 0
                };
            }

            return null;
        }

        /// <summary>
        /// 解析任务奖励
        /// </summary>
        private void ParseTaskReward(TaskReward reward, string rewardStr)
        {
            var parts = rewardStr.Split(';');
            
            foreach (var part in parts)
            {
                var rewardParts = part.Split('=');
                if (rewardParts.Length == 2)
                {
                    var key = rewardParts[0].Trim();
                    var value = rewardParts[1].Trim();
                    
                    switch (key.ToLower())
                    {
                        case "exp":
                            if (int.TryParse(value, out int exp))
                                reward.Exp = exp;
                            break;
                        case "gold":
                            if (int.TryParse(value, out int gold))
                                reward.Gold = gold;
                            break;
                        case "item":
                            var itemParts = value.Split('*');
                            if (itemParts.Length == 2 && int.TryParse(itemParts[1], out int count))
                            {
                                reward.Items.Add(new TaskRewardItem
                                {
                                    ItemName = itemParts[0].Trim(),
                                    Count = count
                                });
                            }
                            break;
                        case "buff":
                            if (int.TryParse(value, out int buffId))
                                reward.Buffs.Add(buffId);
                            break;
                        case "nexttask":
                            reward.NextTask = value;
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// 获取任务定义
        /// </summary>
        public TaskDefinition? GetTaskDefinition(int taskId)
        {
            return _taskDefinitions.TryGetValue(taskId, out var definition) ? definition : null;
        }

        /// <summary>
        /// 获取所有任务定义
        /// </summary>
        public IEnumerable<TaskDefinition> GetAllTaskDefinitions()
        {
            return _taskDefinitions.Values;
        }

        /// <summary>
        /// 开始任务
        /// </summary>
        public bool StartTask(uint playerId, int taskId)
        {
            if (!_taskDefinitions.ContainsKey(taskId))
                return false;

            var taskDef = _taskDefinitions[taskId];
            
            // 检查前置任务
            if (!string.IsNullOrEmpty(taskDef.PredecessorTask))
            {
                if (!HasCompletedTask(playerId, taskDef.PredecessorTask))
                    return false;
            }

            // 检查等级要求
            var player = GameWorld.Instance.GetPlayer(playerId);
            if (player == null || player.Level < taskDef.RequiredLevel)
                return false;

            // 检查是否已达到最大尝试次数
            var playerTask = GetPlayerTask(playerId, taskId);
            if (playerTask != null && playerTask.Attempts >= taskDef.MaxAttempts)
                return false;

            // 检查是否可重复
            if (!taskDef.Repeatable && playerTask != null && playerTask.Status == TaskStatus.Completed)
                return false;

            // 创建或更新玩家任务
            if (playerTask == null)
            {
                playerTask = new PlayerTask
                {
                    TaskId = taskId,
                    Status = TaskStatus.InProgress,
                    StartTime = DateTime.Now,
                    Attempts = 1,
                    Objectives = new List<TaskObjective>()
                };

                // 复制任务目标
                foreach (var objective in taskDef.Objectives)
                {
                    playerTask.Objectives.Add(new TaskObjective
                    {
                        Id = objective.Id,
                        Description = objective.Description,
                        TargetType = objective.TargetType,
                        TargetName = objective.TargetName,
                        RequiredCount = objective.RequiredCount,
                        CurrentCount = 0
                    });
                }

                // 设置过期时间
                if (taskDef.TimeLimit > 0)
                    playerTask.ExpireTime = DateTime.Now.AddSeconds(taskDef.TimeLimit);

                if (!_playerTasks.ContainsKey(playerId))
                    _playerTasks[playerId] = new List<PlayerTask>();
                
                _playerTasks[playerId].Add(playerTask);
            }
            else
            {
                // 重新开始任务
                playerTask.Status = TaskStatus.InProgress;
                playerTask.StartTime = DateTime.Now;
                playerTask.CompleteTime = null;
                playerTask.Attempts++;
                playerTask.RewardClaimed = false;

                // 重置任务目标
                foreach (var objective in playerTask.Objectives)
                {
                    objective.CurrentCount = 0;
                }

                // 设置过期时间
                if (taskDef.TimeLimit > 0)
                    playerTask.ExpireTime = DateTime.Now.AddSeconds(taskDef.TimeLimit);
            }

            LogManager.Default.Info($"玩家 {playerId} 开始任务: {taskDef.Name} (ID: {taskId})");
            return true;
        }

        /// <summary>
        /// 完成任务
        /// </summary>
        public bool CompleteTask(uint playerId, int taskId)
        {
            var playerTask = GetPlayerTask(playerId, taskId);
            if (playerTask == null || playerTask.Status != TaskStatus.InProgress)
                return false;

            // 检查所有目标是否完成
            foreach (var objective in playerTask.Objectives)
            {
                if (!objective.IsCompleted)
                    return false;
            }

            // 检查是否过期
            if (playerTask.ExpireTime.HasValue && DateTime.Now > playerTask.ExpireTime.Value)
            {
                playerTask.Status = TaskStatus.Failed;
                return false;
            }

            playerTask.Status = TaskStatus.Completed;
            playerTask.CompleteTime = DateTime.Now;

            LogManager.Default.Info($"玩家 {playerId} 完成任务: {taskId}");
            return true;
        }

        /// <summary>
        /// 放弃任务
        /// </summary>
        public bool AbandonTask(uint playerId, int taskId)
        {
            var playerTask = GetPlayerTask(playerId, taskId);
            if (playerTask == null || playerTask.Status != TaskStatus.InProgress)
                return false;

            playerTask.Status = TaskStatus.Abandoned;

            LogManager.Default.Info($"玩家 {playerId} 放弃任务: {taskId}");
            return true;
        }

        /// <summary>
        /// 更新任务目标进度
        /// </summary>
        public bool UpdateTaskObjective(uint playerId, int taskId, int objectiveId, int progress)
        {
            var playerTask = GetPlayerTask(playerId, taskId);
            if (playerTask == null || playerTask.Status != TaskStatus.InProgress)
                return false;

            var objective = playerTask.Objectives.Find(o => o.Id == objectiveId);
            if (objective == null)
                return false;

            objective.CurrentCount = Math.Min(objective.CurrentCount + progress, objective.RequiredCount);
            return true;
        }

        /// <summary>
        /// 领取任务奖励
        /// </summary>
        public bool ClaimTaskReward(uint playerId, int taskId)
        {
            var playerTask = GetPlayerTask(playerId, taskId);
            if (playerTask == null || playerTask.Status != TaskStatus.Completed || playerTask.RewardClaimed)
                return false;

            var taskDef = GetTaskDefinition(taskId);
            if (taskDef == null)
                return false;

            // 这里应该发放奖励给玩家
            LogManager.Default.Info($"玩家 {playerId} 领取任务奖励: {taskDef.Name} (经验: {taskDef.Reward.Exp}, 金币: {taskDef.Reward.Gold})");

            playerTask.RewardClaimed = true;
            return true;
        }

        /// <summary>
        /// 获取玩家任务
        /// </summary>
        public PlayerTask? GetPlayerTask(uint playerId, int taskId)
        {
            if (!_playerTasks.ContainsKey(playerId))
                return null;

            return _playerTasks[playerId].Find(t => t.TaskId == taskId);
        }

        /// <summary>
        /// 获取玩家所有任务
        /// </summary>
        public List<PlayerTask> GetPlayerTasks(uint playerId)
        {
            return _playerTasks.TryGetValue(playerId, out var tasks) ? tasks : new List<PlayerTask>();
        }

        /// <summary>
        /// 检查玩家是否已完成任务
        /// </summary>
        public bool HasCompletedTask(uint playerId, string taskName)
        {
            if (!_playerTasks.ContainsKey(playerId))
                return false;

            foreach (var task in _playerTasks[playerId])
            {
                var taskDef = GetTaskDefinition(task.TaskId);
                if (taskDef != null && taskDef.Name == taskName && task.Status == TaskStatus.Completed)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 更新任务状态（检查过期等）
        /// </summary>
        public void Update()
        {
            var now = DateTime.Now;
            var expiredPlayers = new List<uint>();

            foreach (var kvp in _playerTasks)
            {
                var playerId = kvp.Key;
                var tasks = kvp.Value;
                var tasksToRemove = new List<PlayerTask>();

                foreach (var task in tasks)
                {
                    // 检查任务是否过期
                    if (task.ExpireTime.HasValue && now > task.ExpireTime.Value && task.Status == TaskStatus.InProgress)
                    {
                        task.Status = TaskStatus.Failed;
                        LogManager.Default.Debug($"任务过期: 玩家 {playerId}, 任务 {task.TaskId}");
                    }

                    // 检查已完成且已领取奖励的任务是否可以移除
                    if (task.Status == TaskStatus.Completed && task.RewardClaimed)
                    {
                        var taskDef = GetTaskDefinition(task.TaskId);
                        if (taskDef != null && !taskDef.Repeatable)
                        {
                            tasksToRemove.Add(task);
                        }
                    }

                    // 检查已失败或已放弃的任务是否可以移除
                    if (task.Status == TaskStatus.Failed || task.Status == TaskStatus.Abandoned)
                    {
                        tasksToRemove.Add(task);
                    }
                }

                // 移除需要清理的任务
                foreach (var task in tasksToRemove)
                {
                    tasks.Remove(task);
                }

                if (tasks.Count == 0)
                    expiredPlayers.Add(playerId);
            }

            // 清理没有任务的玩家
            foreach (var playerId in expiredPlayers)
            {
                _playerTasks.Remove(playerId);
            }
        }

        /// <summary>
        /// 获取可接受的任务列表
        /// </summary>
        public List<TaskDefinition> GetAvailableTasks(uint playerId)
        {
            var availableTasks = new List<TaskDefinition>();
            var player = GameWorld.Instance.GetPlayer(playerId);
            
            if (player == null)
                return availableTasks;

            foreach (var taskDef in _taskDefinitions.Values)
            {
                // 检查等级要求
                if (player.Level < taskDef.RequiredLevel)
                    continue;

                // 检查前置任务
                if (!string.IsNullOrEmpty(taskDef.PredecessorTask))
                {
                    if (!HasCompletedTask(playerId, taskDef.PredecessorTask))
                        continue;
                }

                // 检查是否已达到最大尝试次数
                var playerTask = GetPlayerTask(playerId, taskDef.Id);
                if (playerTask != null && playerTask.Attempts >= taskDef.MaxAttempts)
                    continue;

                // 检查是否可重复
                if (!taskDef.Repeatable && playerTask != null && playerTask.Status == TaskStatus.Completed)
                    continue;

                availableTasks.Add(taskDef);
            }

            return availableTasks;
        }

        /// <summary>
        /// 获取进行中的任务
        /// </summary>
        public List<PlayerTask> GetInProgressTasks(uint playerId)
        {
            var tasks = GetPlayerTasks(playerId);
            return tasks.FindAll(t => t.Status == TaskStatus.InProgress);
        }

        /// <summary>
        /// 获取已完成的任务
        /// </summary>
        public List<PlayerTask> GetCompletedTasks(uint playerId)
        {
            var tasks = GetPlayerTasks(playerId);
            return tasks.FindAll(t => t.Status == TaskStatus.Completed);
        }

        /// <summary>
        /// 重置所有任务数据（用于测试）
        /// </summary>
        public void Reset()
        {
            _taskDefinitions.Clear();
            _playerTasks.Clear();
            LogManager.Default.Info("任务管理器已重置");
        }
    }
}
