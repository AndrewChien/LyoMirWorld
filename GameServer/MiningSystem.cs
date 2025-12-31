namespace GameServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MirCommon;
    using MirCommon.Utils;

    /// <summary>
    /// 资源类型
    /// </summary>
    public enum ResourceType
    {
        Ore = 0,        // 矿石
        Meat = 1,       // 肉
        Herb = 2,       // 草药
        Wood = 3,       // 木材
        Gem = 4         // 宝石
    }

    /// <summary>
    /// 资源点信息
    /// </summary>
    public class ResourceNode
    {
        public uint NodeId { get; set; }
        public ResourceType Type { get; set; }
        public uint MapId { get; set; }
        public ushort X { get; set; }
        public ushort Y { get; set; }
        public uint ResourceId { get; set; }    // 资源物品ID
        public string ResourceName { get; set; } = string.Empty;
        public uint MaxQuantity { get; set; }   // 最大数量
        public uint CurrentQuantity { get; set; } // 当前数量
        public uint RespawnTime { get; set; }   // 重生时间（秒）
        public DateTime LastHarvestTime { get; set; }
        public uint HarvestCount { get; set; }  // 采集次数
        public bool IsActive { get; set; }

        public ResourceNode(uint nodeId, ResourceType type, uint mapId, ushort x, ushort y, uint resourceId, string resourceName)
        {
            NodeId = nodeId;
            Type = type;
            MapId = mapId;
            X = x;
            Y = y;
            ResourceId = resourceId;
            ResourceName = resourceName;
            MaxQuantity = 100;
            CurrentQuantity = MaxQuantity;
            RespawnTime = 300; // 5分钟
            LastHarvestTime = DateTime.MinValue;
            HarvestCount = 0;
            IsActive = true;
        }

        /// <summary>
        /// 采集资源
        /// </summary>
        public uint Harvest(uint amount)
        {
            if (!IsActive || CurrentQuantity == 0)
                return 0;

            uint harvested = Math.Min(amount, CurrentQuantity);
            CurrentQuantity -= harvested;
            LastHarvestTime = DateTime.Now;
            HarvestCount++;

            // 如果资源耗尽，标记为非活跃
            if (CurrentQuantity == 0)
            {
                IsActive = false;
            }

            return harvested;
        }

        /// <summary>
        /// 更新资源点
        /// </summary>
        public void Update()
        {
            if (!IsActive && (DateTime.Now - LastHarvestTime).TotalSeconds >= RespawnTime)
            {
                // 重生资源
                CurrentQuantity = MaxQuantity;
                IsActive = true;
                HarvestCount = 0;
            }
        }
    }

    /// <summary>
    /// 采集工具
    /// </summary>
    public class HarvestingTool
    {
        public uint ItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public ResourceType[] SupportedTypes { get; set; } = Array.Empty<ResourceType>();
        public float Efficiency { get; set; } = 1.0f;  // 效率系数
        public uint Durability { get; set; } = 100;    // 耐久度
        public uint MaxDurability { get; set; } = 100;

        public HarvestingTool(uint itemId, string name, ResourceType[] supportedTypes, float efficiency = 1.0f)
        {
            ItemId = itemId;
            Name = name;
            SupportedTypes = supportedTypes;
            Efficiency = efficiency;
        }

        /// <summary>
        /// 检查是否支持资源类型
        /// </summary>
        public bool SupportsType(ResourceType type)
        {
            return SupportedTypes.Contains(type);
        }

        /// <summary>
        /// 使用工具
        /// </summary>
        public bool Use()
        {
            if (Durability == 0)
                return false;

            Durability = Math.Max(0, Durability - 1);
            return true;
        }

        /// <summary>
        /// 修复工具
        /// </summary>
        public void Repair()
        {
            Durability = MaxDurability;
        }
    }

    /// <summary>
    /// 采集结果
    /// </summary>
    public class HarvestResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public uint ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public uint Quantity { get; set; }
        public uint Experience { get; set; }
        public bool ToolBroken { get; set; }

        public HarvestResult(bool success, string message = "")
        {
            Success = success;
            Message = message;
        }
    }

    /// <summary>
    /// 挖矿/挖肉系统
    /// </summary>
    public class MiningSystem
    {
        private static MiningSystem? _instance;
        public static MiningSystem Instance => _instance ??= new MiningSystem();

        private readonly Dictionary<uint, ResourceNode> _resourceNodes = new();
        private readonly Dictionary<ResourceType, List<HarvestingTool>> _tools = new();
        private readonly Dictionary<uint, DateTime> _playerLastHarvestTime = new();
        private readonly object _lock = new();
        
        private uint _nextNodeId = 10000;
        private const uint HARVEST_COOLDOWN_SECONDS = 2; // 采集冷却时间

        private MiningSystem()
        {
            InitializeDefaultTools();
            InitializeDefaultResourceNodes();
        }

        /// <summary>
        /// 初始化默认工具
        /// </summary>
        private void InitializeDefaultTools()
        {
            // 矿镐
            var pickaxe = new HarvestingTool(5001, "矿镐", 
                new[] { ResourceType.Ore, ResourceType.Gem }, 1.2f);
            AddTool(pickaxe);

            // 斧头
            var axe = new HarvestingTool(5002, "斧头", 
                new[] { ResourceType.Wood }, 1.1f);
            AddTool(axe);

            // 小刀
            var knife = new HarvestingTool(5003, "小刀", 
                new[] { ResourceType.Meat }, 1.0f);
            AddTool(knife);

            // 药锄
            var herbTool = new HarvestingTool(5004, "药锄", 
                new[] { ResourceType.Herb }, 1.3f);
            AddTool(herbTool);

            // 万能工具
            var universalTool = new HarvestingTool(5005, "万能工具", 
                Enum.GetValues(typeof(ResourceType)).Cast<ResourceType>().ToArray(), 0.8f);
            AddTool(universalTool);
        }

        /// <summary>
        /// 初始化默认资源点
        /// </summary>
        private void InitializeDefaultResourceNodes()
        {
            // 矿石资源点
            AddResourceNode(ResourceType.Ore, 0, 100, 100, 6001, "铜矿石");
            AddResourceNode(ResourceType.Ore, 0, 120, 110, 6002, "铁矿石");
            AddResourceNode(ResourceType.Ore, 0, 140, 120, 6003, "银矿石");
            AddResourceNode(ResourceType.Ore, 0, 160, 130, 6004, "金矿石");

            // 肉类资源点（怪物尸体）
            AddResourceNode(ResourceType.Meat, 1, 200, 200, 6101, "猪肉");
            AddResourceNode(ResourceType.Meat, 1, 220, 210, 6102, "牛肉");
            AddResourceNode(ResourceType.Meat, 1, 240, 220, 6103, "羊肉");
            AddResourceNode(ResourceType.Meat, 1, 260, 230, 6104, "鸡肉");

            // 草药资源点
            AddResourceNode(ResourceType.Herb, 2, 300, 300, 6201, "止血草");
            AddResourceNode(ResourceType.Herb, 2, 320, 310, 6202, "回蓝草");
            AddResourceNode(ResourceType.Herb, 2, 340, 320, 6203, "解毒草");
            AddResourceNode(ResourceType.Herb, 2, 360, 330, 6204, "经验草");

            // 木材资源点
            AddResourceNode(ResourceType.Wood, 3, 400, 400, 6301, "松木");
            AddResourceNode(ResourceType.Wood, 3, 420, 410, 6302, "橡木");
            AddResourceNode(ResourceType.Wood, 3, 440, 420, 6303, "红木");
            AddResourceNode(ResourceType.Wood, 3, 460, 430, 6304, "紫檀木");

            // 宝石资源点
            AddResourceNode(ResourceType.Gem, 4, 500, 500, 6401, "红宝石");
            AddResourceNode(ResourceType.Gem, 4, 520, 510, 6402, "蓝宝石");
            AddResourceNode(ResourceType.Gem, 4, 540, 520, 6403, "绿宝石");
            AddResourceNode(ResourceType.Gem, 4, 560, 530, 6404, "钻石");

            LogManager.Default.Info($"已初始化 {_resourceNodes.Count} 个资源点");
        }

        /// <summary>
        /// 添加资源点
        /// </summary>
        private void AddResourceNode(ResourceType type, uint mapId, ushort x, ushort y, uint resourceId, string resourceName)
        {
            uint nodeId = _nextNodeId++;
            var node = new ResourceNode(nodeId, type, mapId, x, y, resourceId, resourceName);
            _resourceNodes[nodeId] = node;
        }

        /// <summary>
        /// 添加工具
        /// </summary>
        private void AddTool(HarvestingTool tool)
        {
            foreach (var type in tool.SupportedTypes)
            {
                if (!_tools.ContainsKey(type))
                {
                    _tools[type] = new List<HarvestingTool>();
                }
                _tools[type].Add(tool);
            }
        }

        /// <summary>
        /// 采集资源
        /// </summary>
        public HarvestResult Harvest(HumanPlayer player, uint nodeId, uint toolItemId = 0)
        {
            if (player == null)
                return new HarvestResult(false, "玩家不存在");

            // 检查冷却时间
            if (!CanHarvest(player.ObjectId))
            {
                return new HarvestResult(false, "采集冷却中");
            }

            lock (_lock)
            {
                if (!_resourceNodes.TryGetValue(nodeId, out var node))
                    return new HarvestResult(false, "资源点不存在");

                // 检查资源点是否活跃
                if (!node.IsActive)
                    return new HarvestResult(false, "资源已耗尽");

                // 检查距离
                if (player.CurrentMap == null || player.CurrentMap.MapId != node.MapId)
                    return new HarvestResult(false, "距离太远");

                int distance = Math.Abs(player.X - node.X) + Math.Abs(player.Y - node.Y);
                if (distance > 2) // 最大2格距离
                    return new HarvestResult(false, "距离太远");

                // 获取工具
                HarvestingTool? tool = null;
                if (toolItemId > 0)
                {
                    tool = GetTool(toolItemId, node.Type);
                    if (tool == null)
                        return new HarvestResult(false, "工具不支持此资源类型");
                }

                // 使用工具
                bool toolBroken = false;
                if (tool != null)
                {
                    if (!tool.Use())
                    {
                        toolBroken = true;
                        return new HarvestResult(false, "工具已损坏");
                    }
                }

                // 计算采集数量
                uint baseAmount = GetBaseHarvestAmount(player, node.Type);
                if (tool != null)
                {
                    baseAmount = (uint)(baseAmount * tool.Efficiency);
                }

                // 采集资源
                uint harvested = node.Harvest(baseAmount);
                if (harvested == 0)
                    return new HarvestResult(false, "采集失败");

                // 给予玩家物品
                var item = ItemManager.Instance.CreateItem((int)node.ResourceId, (int)harvested);
                if (item == null)
                    return new HarvestResult(false, "物品创建失败");

                if (!player.AddItem(item))
                {
                    // 背包已满
                    return new HarvestResult(false, "背包已满");
                }

                // 给予经验
                uint exp = GetHarvestExperience(node.Type, harvested);
                player.AddExp(exp);

                // 更新玩家最后采集时间
                _playerLastHarvestTime[player.ObjectId] = DateTime.Now;

                // 记录日志
                LogManager.Default.Info($"{player.Name} 采集了 {node.ResourceName} x{harvested}");

                return new HarvestResult(true, $"采集成功，获得 {node.ResourceName} x{harvested}")
                {
                    ItemId = node.ResourceId,
                    ItemName = node.ResourceName,
                    Quantity = harvested,
                    Experience = exp,
                    ToolBroken = toolBroken
                };
            }
        }

        /// <summary>
        /// 挖矿
        /// </summary>
        public HarvestResult Mine(HumanPlayer player, uint nodeId, uint toolItemId = 0)
        {
            return Harvest(player, nodeId, toolItemId);
        }

        /// <summary>
        /// 挖肉
        /// </summary>
        public HarvestResult GetMeat(HumanPlayer player, uint nodeId, uint toolItemId = 0)
        {
            return Harvest(player, nodeId, toolItemId);
        }

        /// <summary>
        /// 获取基础采集数量
        /// </summary>
        private uint GetBaseHarvestAmount(HumanPlayer player, ResourceType type)
        {
            uint baseAmount = 1;

            // 根据玩家等级和技能增加数量
            switch (type)
            {
                case ResourceType.Ore:
                    baseAmount += (uint)(player.Level / 10);
                    break;
                case ResourceType.Meat:
                    baseAmount += (uint)(player.Level / 15);
                    break;
                case ResourceType.Herb:
                    baseAmount += (uint)(player.Level / 20);
                    break;
                case ResourceType.Wood:
                    baseAmount += (uint)(player.Level / 12);
                    break;
                case ResourceType.Gem:
                    baseAmount = 1; // 宝石每次只能采集1个
                    break;
            }

            // 随机波动
            Random rand = new Random();
            int randomBonus = rand.Next(0, 3); // 0-2的随机加成
            baseAmount += (uint)randomBonus;

            return Math.Max(1, baseAmount);
        }

        /// <summary>
        /// 获取采集经验
        /// </summary>
        private uint GetHarvestExperience(ResourceType type, uint quantity)
        {
            return type switch
            {
                ResourceType.Ore => 10 * quantity,
                ResourceType.Meat => 8 * quantity,
                ResourceType.Herb => 12 * quantity,
                ResourceType.Wood => 6 * quantity,
                ResourceType.Gem => 50 * quantity,
                _ => 5 * quantity
            };
        }

        /// <summary>
        /// 检查是否可以采集
        /// </summary>
        private bool CanHarvest(uint playerId)
        {
            lock (_lock)
            {
                if (!_playerLastHarvestTime.TryGetValue(playerId, out var lastTime))
                    return true;

                var timeSinceLastHarvest = (DateTime.Now - lastTime).TotalSeconds;
                return timeSinceLastHarvest >= HARVEST_COOLDOWN_SECONDS;
            }
        }

        /// <summary>
        /// 获取工具
        /// </summary>
        private HarvestingTool? GetTool(uint itemId, ResourceType type)
        {
            if (_tools.TryGetValue(type, out var toolList))
            {
                return toolList.FirstOrDefault(t => t.ItemId == itemId);
            }
            return null;
        }

        /// <summary>
        /// 获取附近的资源点
        /// </summary>
        public List<ResourceNode> GetNearbyResourceNodes(HumanPlayer player, ResourceType? type = null, int maxDistance = 10)
        {
            if (player.CurrentMap == null)
                return new List<ResourceNode>();

            lock (_lock)
            {
                return _resourceNodes.Values
                    .Where(node => 
                        node.MapId == player.CurrentMap.MapId &&
                        node.IsActive &&
                        (type == null || node.Type == type) &&
                        Math.Abs(player.X - node.X) + Math.Abs(player.Y - node.Y) <= maxDistance)
                    .ToList();
            }
        }

        /// <summary>
        /// 获取资源点
        /// </summary>
        public ResourceNode? GetResourceNode(uint nodeId)
        {
            lock (_lock)
            {
                _resourceNodes.TryGetValue(nodeId, out var node);
                return node;
            }
        }

        /// <summary>
        /// 获取所有资源点
        /// </summary>
        public List<ResourceNode> GetAllResourceNodes()
        {
            lock (_lock)
            {
                return _resourceNodes.Values.ToList();
            }
        }

        /// <summary>
        /// 更新资源点（定期调用）
        /// </summary>
        public void UpdateResourceNodes()
        {
            lock (_lock)
            {
                foreach (var node in _resourceNodes.Values)
                {
                    node.Update();
                }
            }
        }

        /// <summary>
        /// 添加新的资源点
        /// </summary>
        public bool AddResourceNode(ResourceNode node)
        {
            lock (_lock)
            {
                if (_resourceNodes.ContainsKey(node.NodeId))
                    return false;

                _resourceNodes[node.NodeId] = node;
                return true;
            }
        }

        /// <summary>
        /// 移除资源点
        /// </summary>
        public bool RemoveResourceNode(uint nodeId)
        {
            lock (_lock)
            {
                return _resourceNodes.Remove(nodeId);
            }
        }

        /// <summary>
        /// 获取工具列表
        /// </summary>
        public List<HarvestingTool> GetToolsForType(ResourceType type)
        {
            lock (_lock)
            {
                if (_tools.TryGetValue(type, out var toolList))
                {
                    return new List<HarvestingTool>(toolList);
                }
                return new List<HarvestingTool>();
            }
        }

        /// <summary>
        /// 获取所有工具
        /// </summary>
        public List<HarvestingTool> GetAllTools()
        {
            lock (_lock)
            {
                var allTools = new List<HarvestingTool>();
                foreach (var toolList in _tools.Values)
                {
                    allTools.AddRange(toolList);
                }
                return allTools.DistinctBy(t => t.ItemId).ToList();
            }
        }

        /// <summary>
        /// 获取采集统计信息
        /// </summary>
        public (int totalNodes, int activeNodes, int totalHarvests) GetStatistics()
        {
            lock (_lock)
            {
                int totalNodes = _resourceNodes.Count;
                int activeNodes = _resourceNodes.Values.Count(n => n.IsActive);
                int totalHarvests = _resourceNodes.Values.Sum(n => (int)n.HarvestCount);
                
                return (totalNodes, activeNodes, totalHarvests);
            }
        }

        /// <summary>
        /// 重置玩家采集冷却
        /// </summary>
        public void ResetPlayerCooldown(uint playerId)
        {
            lock (_lock)
            {
                _playerLastHarvestTime.Remove(playerId);
            }
        }

        /// <summary>
        /// 获取玩家最后采集时间
        /// </summary>
        public DateTime? GetPlayerLastHarvestTime(uint playerId)
        {
            lock (_lock)
            {
                if (_playerLastHarvestTime.TryGetValue(playerId, out var lastTime))
                {
                    return lastTime;
                }
                return null;
            }
        }

        /// <summary>
        /// 清理过期数据
        /// </summary>
        public void Cleanup()
        {
            lock (_lock)
            {
                // 清理长时间未采集的玩家数据（24小时）
                var cutoffTime = DateTime.Now.AddHours(-24);
                var expiredPlayers = _playerLastHarvestTime
                    .Where(kv => kv.Value < cutoffTime)
                    .Select(kv => kv.Key)
                    .ToList();
                
                foreach (var playerId in expiredPlayers)
                {
                    _playerLastHarvestTime.Remove(playerId);
                }
            }
        }
    }
}
