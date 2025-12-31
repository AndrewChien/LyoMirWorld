using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MirCommon.Utils;

namespace GameServer.Parsers
{
    /// <summary>
    /// 物品类定义
    /// </summary>
    public class ItemClass
    {
        public string Name { get; set; } = "";
        public byte StdMode { get; set; }
        public int Shape { get; set; }
        public ushort Image { get; set; }
        public sbyte SpecialPower { get; set; }
        public byte[] AC { get; set; } = new byte[2];
        public byte[] MAC { get; set; } = new byte[2];
        public byte[] DC { get; set; } = new byte[2];
        public byte[] MC { get; set; } = new byte[2];
        public byte[] SC { get; set; } = new byte[2];
        public byte Weight { get; set; }
        public ushort MaxDura { get; set; }
        public ushort DuraTimes { get; set; } = 1000;
        public int Price { get; set; }
        public byte NeedType { get; set; }
        public byte NeedLevel { get; set; }
        public int StateView { get; set; }
        public string PageScript { get; set; } = "";
        public string PickupScript { get; set; } = "";
        public string DropScript { get; set; } = "";
        public uint DropScriptDelay { get; set; }
        public uint DropScriptExecuteTimes { get; set; }
        public ushort ItemLimit { get; set; }
    }

    /// <summary>
    /// 物品数据解析器
    /// baseitem.txt格式: name/stdmode/shape/image/specialpower/ac1/ac2/mac1/mac2/dc1/dc2/mc1/mc2/sc1/sc2/weight/maxdura/price/needtype/needlevel/stateview/@script
    /// </summary>
    public class ItemDataParser
    {
        private readonly Dictionary<string, ItemClass> _items = new();

        public int ItemCount => _items.Count;

        /// <summary>
        /// 加载物品数据文件
        /// </summary>
        public bool Load(string filePath)
        {
            if (!File.Exists(filePath))
            {
                LogManager.Default.Warning($"物品数据文件不存在: {filePath}");
                return false;
            }

            try
            {
                var lines = SmartReader.ReadAllLines(filePath);
                int successCount = 0;
                int lineNumber = 0;

                foreach (var line in lines)
                {
                    lineNumber++;
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                        continue;

                    if (ParseItemLine(line, out var itemClass))
                    {
                        if (AddItem(itemClass))
                        {
                            successCount++;
                        }
                        else
                        {
                            // 物品已存在，更新数据
                            _items[itemClass.Name] = itemClass;
                            LogManager.Default.Debug($"更新物品数据: {itemClass.Name}");
                        }
                    }
                    else
                    {
                        LogManager.Default.Warning($"解析物品数据失败 (行{lineNumber}): {line}");
                    }
                }

                LogManager.Default.Info($"成功加载 {successCount} 个物品数据");
                return successCount > 0;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载物品数据失败: {filePath}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 解析物品数据行
        /// </summary>
        private bool ParseItemLine(string line, out ItemClass itemClass)
        {
            itemClass = new ItemClass();

            try
            {
                var parts = line.Split('/');
                if (parts.Length < 20)
                {
                    return false;
                }

                itemClass.Name = parts[0].Trim();
                itemClass.StdMode = byte.Parse(parts[1].Trim());
                itemClass.Shape = int.Parse(parts[2].Trim());
                itemClass.Image = ushort.Parse(parts[3].Trim());
                itemClass.SpecialPower = sbyte.Parse(parts[4].Trim());
                itemClass.AC[0] = byte.Parse(parts[5].Trim());
                itemClass.AC[1] = byte.Parse(parts[6].Trim());
                itemClass.MAC[0] = byte.Parse(parts[7].Trim());
                itemClass.MAC[1] = byte.Parse(parts[8].Trim());
                itemClass.DC[0] = byte.Parse(parts[9].Trim());
                itemClass.DC[1] = byte.Parse(parts[10].Trim());
                itemClass.MC[0] = byte.Parse(parts[11].Trim());
                itemClass.MC[1] = byte.Parse(parts[12].Trim());
                itemClass.SC[0] = byte.Parse(parts[13].Trim());
                itemClass.SC[1] = byte.Parse(parts[14].Trim());
                itemClass.Weight = byte.Parse(parts[15].Trim());

                // 处理耐久度（可能包含 * 乘数）
                string duraStr = parts[16].Trim();
                if (duraStr.Contains('*'))
                {
                    var duraParts = duraStr.Split('*');
                    itemClass.MaxDura = ushort.Parse(duraParts[0].Trim());
                    itemClass.DuraTimes = ushort.Parse(duraParts[1].Trim());
                }
                else
                {
                    itemClass.MaxDura = ushort.Parse(duraStr);
                    itemClass.DuraTimes = 1000;
                }

                itemClass.Price = int.Parse(parts[17].Trim());
                itemClass.NeedType = byte.Parse(parts[18].Trim());
                itemClass.NeedLevel = byte.Parse(parts[19].Trim());

                if(parts.Length > 20 && parts[20].Trim().StartsWith("@"))
                {
                    itemClass.PageScript = parts[20].Trim();
                    return true;
                }
                else
                {
                    // 可选参数
                    if (parts.Length > 20)
                    {
                        itemClass.StateView = int.Parse(parts[20].Trim());
                    }
                    else
                    {
                        itemClass.StateView = itemClass.Shape;
                    }
                }

                // 脚本（最后一个参数，以@开头）
                if (parts.Length > 20 && parts[parts.Length - 1].StartsWith("@"))
                {
                    itemClass.PageScript = parts[parts.Length - 1].Substring(1);
                }

                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Debug($"解析物品行失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 添加物品
        /// </summary>
        public bool AddItem(ItemClass itemClass)
        {
            if (_items.ContainsKey(itemClass.Name))
            {
                return false;
            }

            _items[itemClass.Name] = itemClass;
            return true;
        }

        /// <summary>
        /// 根据名称获取物品
        /// </summary>
        public ItemClass? GetItem(string name)
        {
            return _items.TryGetValue(name, out var item) ? item : null;
        }

        /// <summary>
        /// 获取所有物品
        /// </summary>
        public IEnumerable<ItemClass> GetAllItems()
        {
            return _items.Values;
        }

        /// <summary>
        /// 加载物品限制配置 (itemlimit.txt)
        /// 格式: 物品名=限制1|限制2|限制3
        /// </summary>
        public bool LoadItemLimit(string filePath)
        {
            if (!File.Exists(filePath))
            {
                LogManager.Default.Warning($"物品限制文件不存在: {filePath}");
                return false;
            }

            try
            {
                var lines = SmartReader.ReadAllLines(filePath);
                int count = 0;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                        continue;

                    var parts = line.Split(new[] { '=', '|' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2)
                        continue;

                    string itemName = parts[0].Trim();
                    var item = GetItem(itemName);
                    if (item == null)
                    {
                        LogManager.Default.Debug($"物品不存在，无法设置限制: {itemName}");
                        continue;
                    }

                    ushort limit = 0;
                    for (int i = 1; i < parts.Length; i++)
                    {
                        string limitType = parts[i].Trim().ToUpper();
                        int bitPos = GetItemLimitBit(limitType);
                        if (bitPos >= 0)
                        {
                            limit |= (ushort)(1 << bitPos);
                        }
                    }

                    item.ItemLimit = limit;
                    count++;
                }

                LogManager.Default.Info($"成功加载 {count} 个物品限制配置");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载物品限制配置失败: {filePath}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 获取物品限制位
        /// </summary>
        private int GetItemLimitBit(string limitType)
        {
            return limitType switch
            {
                "DROP" => 0,
                "SELL" => 1,
                "TRADE" => 2,
                "STORAGE" => 3,
                "REPAIR" => 4,
                "UPGRADE" => 5,
                _ => -1
            };
        }

        /// <summary>
        /// 加载物品脚本链接 (itemscript.txt)
        /// 格式: 物品名=拾取脚本,丢弃脚本,使用脚本
        /// </summary>
        public bool LoadItemScript(string filePath)
        {
            if (!File.Exists(filePath))
            {
                LogManager.Default.Warning($"物品脚本链接文件不存在: {filePath}");
                return false;
            }

            try
            {
                var lines = SmartReader.ReadAllLines(filePath);
                int count = 0;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                        continue;

                    var parts = line.Split('=');
                    if (parts.Length < 2)
                        continue;

                    string itemName = parts[0].Trim();
                    var item = GetItem(itemName);
                    if (item == null)
                    {
                        LogManager.Default.Debug($"物品不存在，无法设置脚本: {itemName}");
                        continue;
                    }

                    var scripts = parts[1].Split(',');
                    if (scripts.Length > 0)
                        item.PickupScript = scripts[0].Trim();

                    if (scripts.Length > 1)
                    {
                        // 丢弃脚本可能包含延迟和执行次数
                        var dropParts = scripts[1].Split('|');
                        if (dropParts.Length == 1)
                        {
                            item.DropScript = dropParts[0].Trim();
                            item.DropScriptDelay = 0;
                            item.DropScriptExecuteTimes = 0;
                        }
                        else if (dropParts.Length == 2)
                        {
                            item.DropScript = dropParts[1].Trim();
                            item.DropScriptDelay = uint.Parse(dropParts[0].Trim());
                            item.DropScriptExecuteTimes = 0;
                        }
                        else if (dropParts.Length >= 3)
                        {
                            item.DropScript = dropParts[2].Trim();
                            item.DropScriptDelay = uint.Parse(dropParts[1].Trim());
                            item.DropScriptExecuteTimes = uint.Parse(dropParts[0].Trim());
                        }
                    }

                    if (scripts.Length > 2)
                        item.PageScript = scripts[2].Trim();

                    count++;
                }

                LogManager.Default.Info($"成功加载 {count} 个物品脚本链接");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载物品脚本链接失败: {filePath}", exception: ex);
                return false;
            }
        }
    }
}
