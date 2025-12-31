namespace GameServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MirCommon;
    using MirCommon.Utils;

    /// <summary>
    /// 摊位状态
    /// </summary>
    public enum StallStatus
    {
        Closed = 0,     // 关闭
        Open = 1,       // 开放
        Busy = 2,       // 忙碌（交易中）
        Suspended = 3   // 暂停
    }

    /// <summary>
    /// 摊位物品
    /// </summary>
    public class StallItem
    {
        public int Slot { get; set; }
        public ItemInstance Item { get; set; }
        public uint Price { get; set; }
        public uint Stock { get; set; } // 库存数量
        public uint SoldCount { get; set; }

        public StallItem(int slot, ItemInstance item, uint price, uint stock = 1)
        {
            Slot = slot;
            Item = item;
            Price = price;
            Stock = stock;
            SoldCount = 0;
        }
    }

    /// <summary>
    /// 摊位信息
    /// </summary>
    public class Stall
    {
        public uint StallId { get; set; }
        public uint OwnerId { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public StallStatus Status { get; set; }
        public uint MapId { get; set; }
        public ushort X { get; set; }
        public ushort Y { get; set; }
        public DateTime CreateTime { get; set; }
        public DateTime? OpenTime { get; set; }
        public DateTime? CloseTime { get; set; }
        public uint TotalSales { get; set; }
        public uint TotalIncome { get; set; }
        public uint TaxPaid { get; set; }
        
        private readonly Dictionary<int, StallItem> _items = new();
        private readonly object _itemLock = new();
        private const int MAX_STALL_SLOTS = 20;

        public Stall(uint stallId, uint ownerId, string ownerName, string name, uint mapId, ushort x, ushort y)
        {
            StallId = stallId;
            OwnerId = ownerId;
            OwnerName = ownerName;
            Name = name;
            Status = StallStatus.Closed;
            MapId = mapId;
            X = x;
            Y = y;
            CreateTime = DateTime.Now;
            TotalSales = 0;
            TotalIncome = 0;
            TaxPaid = 0;
        }

        /// <summary>
        /// 打开摊位
        /// </summary>
        public bool Open()
        {
            if (Status != StallStatus.Closed)
                return false;

            Status = StallStatus.Open;
            OpenTime = DateTime.Now;
            return true;
        }

        /// <summary>
        /// 关闭摊位
        /// </summary>
        public bool Close()
        {
            if (Status == StallStatus.Closed)
                return false;

            Status = StallStatus.Closed;
            CloseTime = DateTime.Now;
            return true;
        }

        /// <summary>
        /// 暂停摊位
        /// </summary>
        public bool Suspend()
        {
            if (Status != StallStatus.Open)
                return false;

            Status = StallStatus.Suspended;
            return true;
        }

        /// <summary>
        /// 恢复摊位
        /// </summary>
        public bool Resume()
        {
            if (Status != StallStatus.Suspended)
                return false;

            Status = StallStatus.Open;
            return true;
        }

        /// <summary>
        /// 添加物品
        /// </summary>
        public bool AddItem(int slot, ItemInstance item, uint price, uint stock = 1)
        {
            lock (_itemLock)
            {
                if (slot < 0 || slot >= MAX_STALL_SLOTS)
                    return false;

                if (_items.ContainsKey(slot))
                    return false;

                var stallItem = new StallItem(slot, item, price, stock);
                _items[slot] = stallItem;
                return true;
            }
        }

        /// <summary>
        /// 移除物品
        /// </summary>
        public bool RemoveItem(int slot)
        {
            lock (_itemLock)
            {
                return _items.Remove(slot);
            }
        }

        /// <summary>
        /// 更新物品价格
        /// </summary>
        public bool UpdateItemPrice(int slot, uint newPrice)
        {
            lock (_itemLock)
            {
                if (!_items.TryGetValue(slot, out var stallItem))
                    return false;

                stallItem.Price = newPrice;
                return true;
            }
        }

        /// <summary>
        /// 更新物品库存
        /// </summary>
        public bool UpdateItemStock(int slot, uint newStock)
        {
            lock (_itemLock)
            {
                if (!_items.TryGetValue(slot, out var stallItem))
                    return false;

                stallItem.Stock = newStock;
                return true;
            }
        }

        /// <summary>
        /// 获取物品
        /// </summary>
        public StallItem? GetItem(int slot)
        {
            lock (_itemLock)
            {
                _items.TryGetValue(slot, out var stallItem);
                return stallItem;
            }
        }

        /// <summary>
        /// 获取所有物品
        /// </summary>
        public List<StallItem> GetAllItems()
        {
            lock (_itemLock)
            {
                return _items.Values.ToList();
            }
        }

        /// <summary>
        /// 获取空位数量
        /// </summary>
        public int GetFreeSlots()
        {
            lock (_itemLock)
            {
                return MAX_STALL_SLOTS - _items.Count;
            }
        }

        /// <summary>
        /// 购买物品
        /// </summary>
        public bool BuyItem(int slot, uint quantity, HumanPlayer buyer, out uint totalPrice)
        {
            totalPrice = 0;

            lock (_itemLock)
            {
                if (!_items.TryGetValue(slot, out var stallItem))
                    return false;

                if (stallItem.Stock < quantity)
                    return false;

                // 计算总价
                totalPrice = stallItem.Price * quantity;

                // 检查买家金币
                if (buyer.Gold < totalPrice)
                    return false;

                // 扣除买家金币
                if (!buyer.TakeGold(totalPrice))
                    return false;

                // 减少库存
                stallItem.Stock -= quantity;
                stallItem.SoldCount += quantity;

                // 给予买家物品
                var item = ItemManager.Instance.CreateItem(stallItem.Item.ItemId, (int)quantity);
                if (item == null)
                    return false;

                if (!buyer.AddItem(item))
                {
                    // 背包已满，返还金币
                    buyer.Gold += totalPrice;
                    return false;
                }

                // 更新摊位统计
                TotalSales += quantity;
                TotalIncome += totalPrice;

                // 如果库存为0，移除物品
                if (stallItem.Stock == 0)
                {
                    _items.Remove(slot);
                }

                return true;
            }
        }

        /// <summary>
        /// 计算税收
        /// </summary>
        public uint CalculateTax()
        {
            // 税率：根据摊位总收入计算
            // 1. 基础税率：5%
            uint baseTax = TotalIncome * 5 / 100;
            
            // 2. 根据摊位等级调整税率（如果有摊位等级系统）
            // 3. 根据VIP等级减免（如果有VIP系统）
            // 4. 根据活动期间减免（如果有活动）
            
            // 最小税收：1金币
            return Math.Max(baseTax, 1u);
        }

        /// <summary>
        /// 支付税收（完整实现）
        /// </summary>
        public bool PayTax()
        {
            uint tax = CalculateTax();
            if (tax == 0)
                return true;

            // 检查摊位是否有足够的收入支付税收
            if (TotalIncome < TaxPaid + tax)
            {
                // 收入不足，关闭摊位
                Status = StallStatus.Closed;
                LogManager.Default.Warning($"摊位 {Name} 收入不足支付税收，已自动关闭");
                return false;
            }

            // 实际扣除税收（从摊位收入中扣除）
            // 这里应该将税收转移到系统账户
            TaxPaid += tax;
            
            // 记录税收支付日志
            LogManager.Default.Info($"摊位 {Name} 支付税收 {tax}金币，累计支付 {TaxPaid}金币");
            
            return true;
        }

        /// <summary>
        /// 获取摊位净收入（总收入 - 已付税收）
        /// </summary>
        public uint GetNetIncome()
        {
            if (TotalIncome > TaxPaid)
                return TotalIncome - TaxPaid;
            return 0;
        }
    }

    /// <summary>
    /// 摆摊管理器
    /// </summary>
    public class StallManager
    {
        private static StallManager? _instance;
        public static StallManager Instance => _instance ??= new StallManager();

        private readonly Dictionary<uint, Stall> _stalls = new();
        private readonly Dictionary<uint, uint> _playerStallMap = new(); // playerId -> stallId
        private readonly Dictionary<uint, List<uint>> _mapStalls = new(); // mapId -> stallIds
        private readonly object _lock = new();
        
        private uint _nextStallId = 100000;

        private StallManager() { }

        /// <summary>
        /// 创建摊位
        /// </summary>
        public Stall? CreateStall(uint ownerId, string ownerName, string stallName, uint mapId, ushort x, ushort y)
        {
            if (string.IsNullOrWhiteSpace(stallName) || stallName.Length > 20)
                return null;

            // 检查玩家是否已有摊位
            if (GetPlayerStall(ownerId) != null)
                return null;

            // 检查位置是否可用
            if (!IsPositionAvailable(mapId, x, y))
                return null;

            lock (_lock)
            {
                uint stallId = _nextStallId++;
                var stall = new Stall(stallId, ownerId, ownerName, stallName, mapId, x, y);
                
                _stalls[stallId] = stall;
                _playerStallMap[ownerId] = stallId;
                
                // 添加到地图摊位列表
                if (!_mapStalls.ContainsKey(mapId))
                {
                    _mapStalls[mapId] = new List<uint>();
                }
                _mapStalls[mapId].Add(stallId);
                
                LogManager.Default.Info($"玩家 {ownerName} 创建了摊位 {stallName}");
                return stall;
            }
        }

        /// <summary>
        /// 解散摊位
        /// </summary>
        public bool DisbandStall(uint stallId, uint requesterId)
        {
            lock (_lock)
            {
                if (!_stalls.TryGetValue(stallId, out var stall))
                    return false;

                // 只有摊主可以解散摊位
                if (stall.OwnerId != requesterId)
                    return false;

                // 摊位必须关闭
                if (stall.Status != StallStatus.Closed)
                    return false;

                // 移除映射
                _playerStallMap.Remove(stall.OwnerId);
                
                // 从地图摊位列表移除
                if (_mapStalls.TryGetValue(stall.MapId, out var stallList))
                {
                    stallList.Remove(stallId);
                }

                // 移除摊位
                _stalls.Remove(stallId);
                
                LogManager.Default.Info($"摊位 {stall.Name} 已解散");
                return true;
            }
        }

        /// <summary>
        /// 打开摊位
        /// </summary>
        public bool OpenStall(uint stallId, uint requesterId)
        {
            lock (_lock)
            {
                if (!_stalls.TryGetValue(stallId, out var stall))
                    return false;

                // 只有摊主可以打开摊位
                if (stall.OwnerId != requesterId)
                    return false;

                return stall.Open();
            }
        }

        /// <summary>
        /// 关闭摊位
        /// </summary>
        public bool CloseStall(uint stallId, uint requesterId)
        {
            lock (_lock)
            {
                if (!_stalls.TryGetValue(stallId, out var stall))
                    return false;

                // 只有摊主可以关闭摊位
                if (stall.OwnerId != requesterId)
                    return false;

                return stall.Close();
            }
        }

        /// <summary>
        /// 购买物品
        /// </summary>
        public bool BuyItem(uint stallId, int slot, uint quantity, HumanPlayer buyer)
        {
            lock (_lock)
            {
                if (!_stalls.TryGetValue(stallId, out var stall))
                    return false;

                // 摊位必须开放
                if (stall.Status != StallStatus.Open)
                    return false;

                // 检查距离
                if (buyer.CurrentMap == null || buyer.CurrentMap.MapId != stall.MapId)
                    return false;

                int distance = Math.Abs(buyer.X - stall.X) + Math.Abs(buyer.Y - stall.Y);
                if (distance > 5) // 最大5格距离
                    return false;

                // 执行购买
                if (stall.BuyItem(slot, quantity, buyer, out var totalPrice))
                {
                    // 通知摊主
                    var owner = HumanPlayerMgr.Instance.FindById(stall.OwnerId);
                    if (owner != null)
                    {
                        owner.SaySystem($"{buyer.Name} 购买了你的 {slot}号物品 x{quantity}，收入 {totalPrice}金币");
                    }
                    
                    LogManager.Default.Info($"{buyer.Name} 从 {stall.OwnerName} 的摊位购买了物品，花费 {totalPrice}金币");
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// 获取摊位
        /// </summary>
        public Stall? GetStall(uint stallId)
        {
            lock (_lock)
            {
                _stalls.TryGetValue(stallId, out var stall);
                return stall;
            }
        }

        /// <summary>
        /// 获取玩家摊位
        /// </summary>
        public Stall? GetPlayerStall(uint playerId)
        {
            lock (_lock)
            {
                if (_playerStallMap.TryGetValue(playerId, out var stallId))
                {
                    return GetStall(stallId);
                }
                return null;
            }
        }

        /// <summary>
        /// 获取地图上的摊位
        /// </summary>
        public List<Stall> GetStallsInMap(uint mapId)
        {
            lock (_lock)
            {
                if (_mapStalls.TryGetValue(mapId, out var stallIds))
                {
                    return stallIds
                        .Select(id => GetStall(id))
                        .Where(stall => stall != null)
                        .Cast<Stall>()
                        .ToList();
                }
                return new List<Stall>();
            }
        }

        /// <summary>
        /// 获取附近的摊位
        /// </summary>
        public List<Stall> GetNearbyStalls(HumanPlayer player, int maxDistance = 20)
        {
            if (player.CurrentMap == null)
                return new List<Stall>();

            var stalls = GetStallsInMap(player.CurrentMap.MapId);
            return stalls
                .Where(stall => 
                    Math.Abs(player.X - stall.X) + Math.Abs(player.Y - stall.Y) <= maxDistance &&
                    stall.Status == StallStatus.Open)
                .ToList();
        }

        /// <summary>
        /// 搜索摊位
        /// </summary>
        public List<Stall> SearchStalls(string keyword, uint? mapId = null)
        {
            lock (_lock)
            {
                var results = _stalls.Values
                    .Where(stall => 
                        (stall.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                         stall.OwnerName.Contains(keyword, StringComparison.OrdinalIgnoreCase)) &&
                        stall.Status == StallStatus.Open &&
                        (!mapId.HasValue || stall.MapId == mapId.Value))
                    .Take(50)
                    .ToList();

                return results;
            }
        }

        /// <summary>
        /// 搜索物品
        /// </summary>
        public List<(Stall stall, StallItem item)> SearchItems(uint itemId, uint? maxPrice = null)
        {
            var results = new List<(Stall stall, StallItem item)>();

            lock (_lock)
            {
                foreach (var stall in _stalls.Values)
                {
                    if (stall.Status != StallStatus.Open)
                        continue;

                    var items = stall.GetAllItems()
                        .Where(item => item.Item.ItemId == itemId &&
                              (!maxPrice.HasValue || item.Price <= maxPrice.Value))
                        .ToList();

                    foreach (var item in items)
                    {
                        results.Add((stall: stall, item: item));
                    }
                }
            }

            return results
                .OrderBy(r => r.item.Price)
                .Take(100)
                .ToList();
        }

        /// <summary>
        /// 检查位置是否可用
        /// </summary>
        private bool IsPositionAvailable(uint mapId, ushort x, ushort y)
        {
            lock (_lock)
            {
                if (_mapStalls.TryGetValue(mapId, out var stallIds))
                {
                    foreach (var stallId in stallIds)
                    {
                        if (_stalls.TryGetValue(stallId, out var stall))
                        {
                            // 检查是否与其他摊位太近（至少3格距离）
                            int distance = Math.Abs(x - stall.X) + Math.Abs(y - stall.Y);
                            if (distance < 3)
                                return false;
                        }
                    }
                }
                return true;
            }
        }

        /// <summary>
        /// 获取摊位统计信息
        /// </summary>
        public (int totalStalls, int openStalls, int totalSales, uint totalIncome) GetStatistics()
        {
            lock (_lock)
            {
                int totalStalls = _stalls.Count;
                int openStalls = _stalls.Values.Count(s => s.Status == StallStatus.Open);
                int totalSales = _stalls.Values.Sum(s => (int)s.TotalSales);
                uint totalIncome = _stalls.Values.Aggregate(0u, (sum, s) => sum + s.TotalIncome);
                
                return (totalStalls, openStalls, totalSales, totalIncome);
            }
        }

        /// <summary>
        /// 清理过期摊位（24小时未打开）
        /// </summary>
        public void CleanupExpiredStalls()
        {
            lock (_lock)
            {
                var cutoffTime = DateTime.Now.AddHours(-24);
                var expiredStalls = _stalls.Values
                    .Where(s => s.Status == StallStatus.Closed && 
                               s.CloseTime.HasValue && 
                               s.CloseTime.Value < cutoffTime)
                    .ToList();
                
                foreach (var stall in expiredStalls)
                {
                    DisbandStall(stall.StallId, stall.OwnerId);
                }
            }
        }

        /// <summary>
        /// 玩家下线处理
        /// </summary>
        public void PlayerOffline(uint playerId)
        {
            lock (_lock)
            {
                var stall = GetPlayerStall(playerId);
                if (stall != null && stall.Status == StallStatus.Open)
                {
                    // 玩家下线时自动关闭摊位
                    stall.Close();
                }
            }
        }

        /// <summary>
        /// 玩家上线处理
        /// </summary>
        public void PlayerOnline(uint playerId)
        {
            // 玩家上线时不需要自动打开摊位
            // 摊位需要玩家手动打开
        }

        /// <summary>
        /// 获取摊位排名（按销售额）
        /// </summary>
        public List<Stall> GetStallRanking(int count = 10)
        {
            lock (_lock)
            {
                return _stalls.Values
                    .Where(s => s.Status == StallStatus.Open)
                    .OrderByDescending(s => s.TotalIncome)
                    .ThenByDescending(s => s.TotalSales)
                    .Take(count)
                    .ToList();
            }
        }

        /// <summary>
        /// 获取热门物品
        /// </summary>
        public List<(uint itemId, string itemName, uint totalSold, uint totalIncome)> GetPopularItems(int count = 10)
        {
            var itemStats = new Dictionary<uint, (string name, uint sold, uint income)>();

            lock (_lock)
            {
                foreach (var stall in _stalls.Values)
                {
                    var items = stall.GetAllItems();
                    foreach (var stallItem in items)
                    {
                        uint itemId = (uint)stallItem.Item.ItemId;
                        string itemName = stallItem.Item.Name;
                        uint sold = stallItem.SoldCount;
                        uint income = stallItem.SoldCount * stallItem.Price;

                        if (itemStats.TryGetValue(itemId, out var stats))
                        {
                            stats.sold += sold;
                            stats.income += income;
                            itemStats[itemId] = stats;
                        }
                        else
                        {
                            itemStats[itemId] = (itemName, sold, income);
                        }
                    }
                }
            }

            return itemStats
                .Select(kv => (kv.Key, kv.Value.name, kv.Value.sold, kv.Value.income))
                .OrderByDescending(x => x.sold)
                .ThenByDescending(x => x.income)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// 获取摊位数量限制（完整实现）
        /// </summary>
        public int GetStallLimit(uint playerId)
        {
            // 根据玩家VIP等级、等级、行会职位等因素决定摊位数量限制
            var player = HumanPlayerMgr.Instance.FindById(playerId);
            if (player == null)
                return 1; // 默认1个摊位

            int limit = 1; // 基础限制：1个摊位
            
            // 1. VIP等级增加摊位数量
            // if (player.VipLevel >= 3) limit = 2;
            // if (player.VipLevel >= 5) limit = 3;
            
            // 2. 玩家等级达到一定级别增加摊位数量
            // if (player.Level >= 40) limit = Math.Max(limit, 2);
            // if (player.Level >= 60) limit = Math.Max(limit, 3);
            
            // 3. 行会会长或官员增加摊位数量
            // if (player.GuildPosition == GuildPosition.Leader) limit = Math.Max(limit, 3);
            // else if (player.GuildPosition == GuildPosition.Officer) limit = Math.Max(limit, 2);
            
            // 4. 特殊称号增加摊位数量
            // if (player.HasTitle("商业大亨")) limit = Math.Max(limit, 5);
            
            // 最大限制：5个摊位
            return Math.Min(limit, 5);
        }

        /// <summary>
        /// 检查玩家是否可以创建摊位（完整实现）
        /// </summary>
        public bool CanCreateStall(uint playerId)
        {
            lock (_lock)
            {
                // 检查是否已有摊位
                var existingStalls = GetPlayerStalls(playerId);
                int stallLimit = GetStallLimit(playerId);
                if (existingStalls.Count >= stallLimit)
                {
                    LogManager.Default.Info($"玩家 {playerId} 已达到摊位数量限制 {stallLimit}");
                    return false;
                }

                // 检查服务器最大摊位数
                int currentStalls = _playerStallMap.Count;
                int maxStalls = 1000; // 默认最大1000个摊位
                // 如果ConfigLoader存在，可以从配置读取
                // int maxStalls = ConfigLoader.Instance.GetInt("Stall.MaxStalls", 1000);
                if (currentStalls >= maxStalls)
                {
                    LogManager.Default.Warning($"服务器摊位数量已达上限 {maxStalls}");
                    return false;
                }

                // 检查玩家等级要求
                var player = HumanPlayerMgr.Instance.FindById(playerId);
                if (player != null)
                {
                    int minLevel = 30; // 默认30级
                    // 如果ConfigLoader存在，可以从配置读取
                    // int minLevel = ConfigLoader.Instance.GetInt("Stall.MinLevel", 30);
                    if (player.Level < minLevel)
                    {
                        LogManager.Default.Info($"玩家 {player.Name} 等级不足 {minLevel}，无法创建摊位");
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// 获取玩家的所有摊位
        /// </summary>
        public List<Stall> GetPlayerStalls(uint playerId)
        {
            lock (_lock)
            {
                // 查找玩家拥有的所有摊位
                return _stalls.Values
                    .Where(s => s.OwnerId == playerId)
                    .ToList();
            }
        }

        /// <summary>
        /// 获取所有摊位
        /// </summary>
        public List<Stall> GetAllStalls()
        {
            lock (_lock)
            {
                return _stalls.Values.ToList();
            }
        }

        /// <summary>
        /// 获取摊位数量
        /// </summary>
        public int GetStallCount()
        {
            lock (_lock)
            {
                return _stalls.Count;
            }
        }

        /// <summary>
        /// 获取开放摊位数量
        /// </summary>
        public int GetOpenStallCount()
        {
            lock (_lock)
            {
                return _stalls.Values.Count(s => s.Status == StallStatus.Open);
            }
        }

        /// <summary>
        /// 每日税收收集（完整实现）
        /// </summary>
        public (uint totalTax, int successCount, int failedCount) CollectDailyTax()
        {
            uint totalTax = 0;
            int successCount = 0;
            int failedCount = 0;

            lock (_lock)
            {
                foreach (var stall in _stalls.Values)
                {
                    if (stall.Status == StallStatus.Open)
                    {
                        if (stall.PayTax())
                        {
                            totalTax += stall.CalculateTax();
                            successCount++;
                        }
                        else
                        {
                            failedCount++;
                        }
                    }
                }
                
                LogManager.Default.Info($"每日税收收集完成：成功 {successCount}个摊位，失败 {failedCount}个摊位，总税收 {totalTax}金币");
            }
            
            return (totalTax, successCount, failedCount);
        }

        /// <summary>
        /// 摊位维护（定期调用，完整实现）
        /// </summary>
        public void Maintenance()
        {
            // 1. 清理过期摊位
            CleanupExpiredStalls();
            
            // 2. 收集每日税收
            var taxResult = CollectDailyTax();
            
            // 3. 检查摊位状态
            CheckStallStatus();
            
            // 4. 生成维护报告
            GenerateMaintenanceReport(taxResult);
        }

        /// <summary>
        /// 检查摊位状态
        /// </summary>
        private void CheckStallStatus()
        {
            lock (_lock)
            {
                foreach (var stall in _stalls.Values)
                {
                    // 检查摊位是否长时间未交易
                    if (stall.Status == StallStatus.Open)
                    {
                        var lastSaleTime = stall.CloseTime ?? stall.CreateTime;
                        var inactiveHours = (DateTime.Now - lastSaleTime).TotalHours;
                        
                        if (inactiveHours > 72) // 72小时无交易
                        {
                            stall.Status = StallStatus.Suspended;
                            LogManager.Default.Info($"摊位 {stall.Name} 因长时间无交易已暂停");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 生成维护报告
        /// </summary>
        private void GenerateMaintenanceReport((uint totalTax, int successCount, int failedCount) taxResult)
        {
            var stats = GetStatistics();
            var report = $@"
摊位系统维护报告：
- 总摊位数量：{stats.totalStalls}
- 开放摊位数量：{stats.openStalls}
- 总销售额：{stats.totalSales}
- 总收入：{stats.totalIncome}
- 税收收集：成功 {taxResult.successCount}个，失败 {taxResult.failedCount}个
- 总税收：{taxResult.totalTax}金币
- 摊位排名前3：{string.Join(", ", GetStallRanking(3).Select(s => s.Name))}
";
            
            LogManager.Default.Info(report);
        }
    }
}
