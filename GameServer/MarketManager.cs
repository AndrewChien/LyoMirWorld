using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MirCommon.Utils;

namespace GameServer
{
    /// <summary>
    /// 市场物品定义
    /// </summary>
    public class MarketItem
    {
        public uint Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public uint Price { get; set; }
        public uint Stock { get; set; }
        public uint MaxStock { get; set; }
        public uint RefreshInterval { get; set; }
        public uint LastRefreshTime { get; set; }
    }

    /// <summary>
    /// 市场定义
    /// </summary>
    public class Market
    {
        public uint Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<MarketItem> Items { get; set; } = new();
    }

    /// <summary>
    /// 市场管理器
    /// </summary>
    public class MarketManager
    {
        private static MarketManager? _instance;
        public static MarketManager Instance => _instance ??= new MarketManager();

        private readonly Dictionary<uint, Market> _markets = new();
        private readonly Dictionary<uint, MarketItem> _items = new();
        private string _scrollText = string.Empty;

        private MarketManager() { }

        /// <summary>
        /// 获取市场滚动文字
        /// </summary>
        public string GetMarketScrollText() => _scrollText;

        /// <summary>
        /// 获取市场物品
        /// </summary>
        public MarketItem? GetItem(uint id)
        {
            return _items.TryGetValue(id, out var item) ? item : null;
        }

        /// <summary>
        /// 加载滚动文字
        /// </summary>
        public void LoadScrollText(string filePath)
        {
            if (!File.Exists(filePath))
            {
                LogManager.Default.Warning($"市场滚动文字文件不存在: {filePath}");
                return;
            }

            try
            {
                _scrollText = SmartReader.ReadTextFile(filePath);
                LogManager.Default.Info($"加载市场滚动文字: {filePath} (长度: {_scrollText.Length})");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载市场滚动文字失败: {filePath}", exception: ex);
            }
        }

        /// <summary>
        /// 加载市场配置
        /// </summary>
        public bool LoadMarkets(string filePath)
        {
            if (!File.Exists(filePath))
            {
                LogManager.Default.Warning($"市场配置文件不存在: {filePath}");
                return false;
            }

            try
            {
                var lines = SmartReader.ReadAllLines(filePath);
                Market? currentMarket = null;
                int marketCount = 0;
                int itemCount = 0;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    var trimmedLine = line.Trim();
                    
                    // 市场定义: [MarketId:市场名称]
                    if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    {
                        var marketDef = trimmedLine.Substring(1, trimmedLine.Length - 2);
                        var parts = marketDef.Split(':');
                        
                        if (parts.Length >= 2 && uint.TryParse(parts[0], out uint marketId))
                        {
                            currentMarket = new Market
                            {
                                Id = marketId,
                                Name = parts[1].Trim()
                            };
                            
                            if (parts.Length > 2)
                                currentMarket.Description = parts[2].Trim();
                            
                            _markets[marketId] = currentMarket;
                            marketCount++;
                        }
                    }
                    // 物品定义: 物品ID,物品名称,价格,库存,最大库存,刷新间隔
                    else if (currentMarket != null && trimmedLine.Contains(","))
                    {
                        var parts = trimmedLine.Split(',');
                        if (parts.Length >= 6 && 
                            uint.TryParse(parts[0], out uint itemId) &&
                            uint.TryParse(parts[2], out uint price) &&
                            uint.TryParse(parts[3], out uint stock) &&
                            uint.TryParse(parts[4], out uint maxStock) &&
                            uint.TryParse(parts[5], out uint refreshInterval))
                        {
                            var item = new MarketItem
                            {
                                Id = itemId,
                                Name = parts[1].Trim(),
                                Price = price,
                                Stock = stock,
                                MaxStock = maxStock,
                                RefreshInterval = refreshInterval,
                                LastRefreshTime = 0
                            };
                            
                            currentMarket.Items.Add(item);
                            _items[itemId] = item;
                            itemCount++;
                        }
                    }
                }

                LogManager.Default.Info($"加载市场配置: {marketCount} 个市场, {itemCount} 个物品");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载市场配置失败: {filePath}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 加载市场主目录
        /// </summary>
        public bool LoadMainDirectory(string filePath)
        {
            if (!File.Exists(filePath))
            {
                LogManager.Default.Warning($"市场主目录文件不存在: {filePath}");
                return false;
            }

            try
            {
                var lines = SmartReader.ReadAllLines(filePath);
                int count = 0;
                
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    // 市场主目录格式: 目录名称=市场ID列表
                    var parts = line.Split('=');
                    if (parts.Length == 2)
                    {
                        string directoryName = parts[0].Trim();
                        string marketIds = parts[1].Trim();
                        count++;
                        LogManager.Default.Debug($"市场主目录: {directoryName} -> {marketIds}");
                    }
                }

                LogManager.Default.Info($"加载市场主目录: {count} 个目录");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载市场主目录失败: {filePath}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 添加市场
        /// </summary>
        public Market? AddMarket(uint id)
        {
            if (_markets.ContainsKey(id))
                return _markets[id];

            var market = new Market { Id = id };
            _markets[id] = market;
            return market;
        }

        /// <summary>
        /// 获取市场
        /// </summary>
        public Market? GetMarket(uint marketId)
        {
            return _markets.TryGetValue(marketId, out var market) ? market : null;
        }

        /// <summary>
        /// 创建新物品
        /// </summary>
        public MarketItem NewItem()
        {
            return new MarketItem();
        }

        /// <summary>
        /// 删除物品
        /// </summary>
        public void DeleteItem(MarketItem item)
        {
            if (item == null) return;
            
            _items.Remove(item.Id);
            
            // 从所有市场中移除该物品
            foreach (var market in _markets.Values)
            {
                market.Items.RemoveAll(i => i.Id == item.Id);
            }
        }

        /// <summary>
        /// 获取所有市场
        /// </summary>
        public IEnumerable<Market> GetAllMarkets()
        {
            return _markets.Values;
        }

        /// <summary>
        /// 获取所有物品
        /// </summary>
        public IEnumerable<MarketItem> GetAllItems()
        {
            return _items.Values;
        }

        /// <summary>
        /// 更新市场管理器（供GameWorld调用）
        /// </summary>
        public void Update()
        {
            // 更新市场物品的刷新逻辑
            UpdateMarketItems();
        }

        /// <summary>
        /// 更新市场物品
        /// </summary>
        private void UpdateMarketItems()
        {
            uint currentTime = (uint)Environment.TickCount;
            int refreshedCount = 0;

            foreach (var item in _items.Values)
            {
                // 检查是否需要刷新库存
                if (item.RefreshInterval > 0 && 
                    currentTime - item.LastRefreshTime >= item.RefreshInterval)
                {
                    // 刷新库存到最大库存
                    item.Stock = item.MaxStock;
                    item.LastRefreshTime = currentTime;
                    refreshedCount++;
                }
            }

            if (refreshedCount > 0)
            {
                LogManager.Default.Debug($"刷新市场物品库存: {refreshedCount} 个物品");
            }
        }
    }
}
