using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MirCommon;
using MirCommon.Utils;

namespace GameServer
{
    /// <summary>
    /// 怪物掉落管理器
    /// </summary>
    public class MonItemsMgr
    {
        private static MonItemsMgr? _instance;
        public static MonItemsMgr Instance => _instance ??= new MonItemsMgr();

        // 怪物掉落哈希表（怪物名称 -> 掉落配置）
        private readonly Dictionary<string, MonItems> _monItemsHash = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _hashLock = new();

        private MonItemsMgr()
        {
            // 初始化
        }

        /// <summary>
        /// 加载怪物掉落配置文件
        /// </summary>
        public bool LoadMonItems(string path)
        {
            LogManager.Default.Info($"加载怪物掉落配置文件: {path}");

            if (!Directory.Exists(path))
            {
                LogManager.Default.Error($"怪物掉落配置目录不存在: {path}");
                return false;
            }

            try
            {
                // 查找所有.txt文件
                var files = Directory.GetFiles(path, "*.txt", SearchOption.AllDirectories);
                int loadedCount = 0;

                foreach (var file in files)
                {
                    if (LoadMonItemsFile(file))
                    {
                        loadedCount++;
                    }
                }

                LogManager.Default.Info($"成功加载 {loadedCount} 个怪物掉落配置文件");
                return loadedCount > 0;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载怪物掉落配置文件失败: {path}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 加载单个怪物掉落文件
        /// </summary>
        private bool LoadMonItemsFile(string fileName)
        {
            try
            {
                // 从文件名获取怪物名称（不带扩展名）
                string monsterName = Path.GetFileNameWithoutExtension(fileName);
                if (string.IsNullOrEmpty(monsterName))
                {
                    LogManager.Default.Warning($"无法从文件名获取怪物名称: {fileName}");
                    return false;
                }

                // 检查是否已存在该怪物的掉落配置
                MonItems? monItems;
                lock (_hashLock)
                {
                    if (_monItemsHash.TryGetValue(monsterName, out monItems))
                    {
                        // 更新现有配置 - 清理旧的掉落物品
                        ClearDownItems(monItems);
                        LogManager.Default.Info($"更新怪物 {monsterName} 的物品掉落文件: {Path.GetFileName(fileName)}");
                    }
                    else
                    {
                        // 创建新的掉落配置
                        monItems = new MonItems
                        {
                            MonsterName = monsterName,
                            FileName = fileName
                        };
                    }
                }

                // 解析文件内容 - 使用ANSI编码读取文件
                var lines = SmartReader.ReadAllLines(fileName);
                int itemCount = 0;

                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                        continue;

                    if (ParseDownItemLine(trimmedLine, out var downItem))
                    {
                        // 添加到掉落列表
                        downItem.Next = monItems.Items;
                        monItems.Items = downItem;
                        itemCount++;
                    }
                }

                // 更新哈希表
                lock (_hashLock)
                {
                    _monItemsHash[monsterName] = monItems;
                }

                LogManager.Default.Debug($"文件 {Path.GetFileName(fileName)} 加载了 {itemCount} 个掉落物品");
                return itemCount > 0;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载怪物掉落文件失败: {fileName}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 解析掉落物品行
        /// 格式: 最小数量 最大数量 物品名称 [数量] [最大数量]
        /// 例如: 1 5 金币 * 1000 5000
        /// </summary>
        private bool ParseDownItemLine(string line, out DownItem downItem)
        {
            downItem = new DownItem();

            try
            {
                // 分割参数
                var parts = line.Split(new[] { ' ', '\t', '-', '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3)
                {
                    LogManager.Default.Warning($"掉落物品格式错误: {line}");
                    return false;
                }

                // 解析最小和最大数量
                if (!int.TryParse(parts[0], out int min) || !int.TryParse(parts[1], out int max))
                {
                    LogManager.Default.Warning($"掉落物品数量解析失败: {line}");
                    return false;
                }

                string itemName = parts[2];

                // 检查是否为随机耐久度
                bool randomDura = false;
                if (itemName.StartsWith("*"))
                {
                    randomDura = true;
                    itemName = itemName.Substring(1);
                }

                // 检查是否为金币
                bool isGold = false;
                string goldName = GameWorld.Instance.GetGameName("GoldName");
                if (string.Equals(itemName, goldName, StringComparison.OrdinalIgnoreCase))
                {
                    isGold = true;
                }
                else
                {
                    // 检查物品是否存在 - 通过名称查找物品定义
                    var itemDefinitions = ItemManager.Instance.GetAllDefinitions();
                    var itemDefinition = itemDefinitions.FirstOrDefault(d => 
                        string.Equals(d.Name, itemName, StringComparison.OrdinalIgnoreCase));
                    
                    if (itemDefinition == null)
                    {
                        LogManager.Default.Warning($"掉落物品中出现未定义的物品: {itemName}");
                        return false;
                    }
                }

                // 解析数量和最大数量
                int count = 1;
                int countMax = 1;

                if (parts.Length > 3)
                {
                    if (!int.TryParse(parts[3], out count))
                    {
                        count = 1;
                    }
                }

                if (parts.Length > 4)
                {
                    if (!int.TryParse(parts[4], out countMax))
                    {
                        countMax = count;
                    }
                }

                // 设置掉落物品属性
                downItem.Name = itemName;
                downItem.Min = min;
                downItem.Max = max;
                downItem.Count = count;
                downItem.CountMax = countMax;
                downItem.RandomDura = randomDura;
                downItem.IsGold = isGold;
                downItem.Current = 0;

                // 计算周期最大值
                Random random = new();
                downItem.CycleMax = random.Next((int)(max * 0.8), (int)(max * 1.3) + 1);
                downItem.Current = random.Next(downItem.CycleMax);

                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析掉落物品行失败: {line}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 获取怪物掉落配置
        /// </summary>
        public MonItems? GetMonItems(string monsterName)
        {
            lock (_hashLock)
            {
                return _monItemsHash.TryGetValue(monsterName, out var monItems) ? monItems : null;
            }
        }

        /// <summary>
        /// 创建掉落物品
        /// </summary>
        public bool CreateDownItem(DownItem downItem, out ItemInstance item)
        {
            item = null!;

            try
            {
                if (downItem.IsGold)
                {
                    // 创建金币
                    Random random = new();
                    int count = random.Next(downItem.Count, downItem.CountMax + 1);
                    
                    string goldName = GameWorld.Instance.GetGameName("GoldName");
                    
                    // 查找金币物品定义
                    var itemDefinitions = ItemManager.Instance.GetAllDefinitions();
                    var goldDefinition = itemDefinitions.FirstOrDefault(d => 
                        string.Equals(d.Name, goldName, StringComparison.OrdinalIgnoreCase));
                    
                    if (goldDefinition == null)
                    {
                        // 如果没有金币定义，创建一个临时的
                        goldDefinition = new ItemDefinition(9999, goldName, ItemType.Other)
                        {
                            MaxStack = 1000000,
                            CanTrade = true,
                            CanDrop = true,
                            CanDestroy = false,
                            BuyPrice = 1,
                            SellPrice = 1
                        };
                    }
                    
                    // 创建金币物品实例
                    item = new ItemInstance(goldDefinition, DateTime.Now.Ticks)
                    {
                        Count = count,
                        Name = goldName
                    };
                    
                    return true;
                }
                else
                {
                    // 创建普通物品
                    var itemDefinitions = ItemManager.Instance.GetAllDefinitions();
                    var itemDefinition = itemDefinitions.FirstOrDefault(d => 
                        string.Equals(d.Name, downItem.Name, StringComparison.OrdinalIgnoreCase));
                    
                    if (itemDefinition == null)
                    {
                        LogManager.Default.Warning($"找不到物品定义: {downItem.Name}");
                        return false;
                    }
                    
                    Random random = new();
                    int count = random.Next(downItem.Count, downItem.CountMax + 1);
                    
                    // 创建物品实例
                    item = new ItemInstance(itemDefinition, DateTime.Now.Ticks)
                    {
                        Count = count,
                        Name = downItem.Name
                    };
                    
                    // 处理随机耐久度
                    if (downItem.RandomDura)
                    {
                        item.Durability = random.Next(50, 100);
                        item.MaxDurability = 100;
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"创建掉落物品失败: {downItem.Name}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 获取金币对应的图片索引
        /// </summary>
        private ushort GetGoldImageIndex(int count)
        {
            if (count > 1000)
                return 0xE5;
            else if (count > 500)
                return 0xE4;
            else if (count > 300)
                return 0xE3;
            else if (count > 100)
                return 0xE2;
            else
                return 0xE1;
        }

        /// <summary>
        /// 更新掉落物品周期
        /// </summary>
        public bool UpdateDownItemCycle(DownItem downItem)
        {
            if (downItem == null)
                return false;

            downItem.Current++;
            if (downItem.Current >= downItem.CycleMax)
            {
                if (downItem.Max < 5)
                {
                    downItem.CycleMax = downItem.Max;
                }
                else
                {
                    Random random = new();
                    downItem.CycleMax = random.Next((int)(downItem.Max * 0.7f), (int)(downItem.Max * 1.3f) + 1);
                }
                downItem.Current = 0;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取所有怪物掉落配置
        /// </summary>
        public List<MonItems> GetAllMonItems()
        {
            lock (_hashLock)
            {
                return _monItemsHash.Values.ToList();
            }
        }

        /// <summary>
        /// 获取怪物掉落配置数量
        /// </summary>
        public int GetMonItemsCount()
        {
            lock (_hashLock)
            {
                return _monItemsHash.Count;
            }
        }

        /// <summary>
        /// 清理所有怪物掉落配置
        /// </summary>
        public void ClearAllMonItems()
        {
            lock (_hashLock)
            {
                foreach (var monItems in _monItemsHash.Values)
                {
                    ClearDownItems(monItems);
                }
                _monItemsHash.Clear();
            }
        }

        /// <summary>
        /// 清理掉落物品链表
        /// </summary>
        private void ClearDownItems(MonItems monItems)
        {
            if (monItems == null)
                return;

            var current = monItems.Items;
            while (current != null)
            {
                var next = current.Next;
                current.Next = null;
                current = next;
            }
            monItems.Items = null;
        }
    }

    /// <summary>
    /// 掉落物品结构
    /// </summary>
    public class DownItem
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public int CountMax { get; set; }
        public int Min { get; set; }
        public int Max { get; set; }
        public int Current { get; set; }
        public int CycleMax { get; set; }
        public bool RandomDura { get; set; }
        public bool IsGold { get; set; }
        public byte[] Flag { get; set; } = new byte[2];
        public DownItem? Next { get; set; }
    }

    /// <summary>
    /// 怪物掉落配置结构
    /// </summary>
    public class MonItems
    {
        public DownItem? Items { get; set; }
        public string MonsterName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }
}
