using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using MirCommon;
using MirCommon.Utils;

namespace GameServer
{
    /// <summary>
    /// 物品类型
    /// </summary>
    public enum ItemType
    {
        Weapon = 0,         // 武器
        Armor = 1,          // 防具
        Helmet = 2,         // 头盔
        Necklace = 3,       // 项链
        Ring = 4,           // 戒指
        Bracelet = 5,       // 手镯
        Belt = 6,           // 腰带
        Boots = 7,          // 鞋子
        Potion = 8,         // 药水
        Scroll = 9,         // 卷轴
        Book = 10,          // 技能书
        Material = 11,      // 材料
        Quest = 12,         // 任务物品
        Other = 99,          // 其他
        Food = 100,
        Charm = 101
    }

    /// <summary>
    /// 物品品质
    /// </summary>
    public enum ItemQuality
    {
        Normal = 0,         // 普通 (白色)
        Fine = 1,           // 精良 (绿色)
        Rare = 2,           // 稀有 (蓝色)
        Epic = 3,           // 史诗 (紫色)
        Legendary = 4,      // 传说 (橙色)
        Mythic = 5          // 神话 (红色)
    }

    /// <summary>
    /// 装备位置
    /// </summary>
    public enum EquipSlot
    {
        Weapon = 0,         // 武器
        Helmet = 1,         // 头盔
        Armor = 2,          // 衣服
        Necklace = 3,       // 项链
        RingLeft = 4,       // 左戒指
        RingRight = 5,      // 右戒指
        BraceletLeft = 6,   // 左手镯
        BraceletRight = 7,  // 右手镯
        Belt = 8,           // 腰带
        Boots = 9,          // 鞋子
        Mount = 10,         // 坐骑
        Max = 11
    }

    /// <summary>
    /// 物品定义
    /// </summary>
    public class ItemDefinition
    {
        public int ItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ItemType Type { get; set; }
        public ItemQuality Quality { get; set; }
        public int Level { get; set; }
        public int MaxStack { get; set; } = 1;
        public bool CanTrade { get; set; } = true;
        public bool CanDrop { get; set; } = true;
        public bool CanDestroy { get; set; } = true;
        public uint BuyPrice { get; set; }
        public uint SellPrice { get; set; }

        // 装备属性
        public int DC { get; set; }         // 攻击力
        public int MC { get; set; }         // 魔法力
        public int SC { get; set; }         // 道术
        public int AC { get; set; }         // 防御
        public int MAC { get; set; }        // 魔防
        public int Accuracy { get; set; }   // 准确
        public int Agility { get; set; }    // 敏捷
        public int HP { get; set; }         // 生命
        public int MP { get; set; }         // 魔法
        public int Lucky { get; set; }      // 幸运

        // 需求
        public int RequireLevel { get; set; }
        public int RequireJob { get; set; } = -1; // -1表示所有职业
        public int RequireSex { get; set; } = -1; // -1表示所有性别，0=男，1=女
        public bool CanDropInSafeArea { get; internal set; }
        public bool CanUse { get; internal set; }
        public bool IsConsumable { get; internal set; }
        public int SubType { get; internal set; }

        public ItemDefinition(int itemId, string name, ItemType type)
        {
            ItemId = itemId;
            Name = name;
            Type = type;
            Quality = ItemQuality.Normal;
            MaxStack = type == ItemType.Potion || type == ItemType.Material ? 100 : 1;
        }
    }

    /// <summary>
    /// 物品实例
    /// </summary>
    public class ItemInstance
    {
        public long InstanceId { get; set; }
        public int ItemId { get; set; }
        public ItemDefinition Definition { get; set; }
        public int Count { get; set; } = 1;
        public int Durability { get; set; }
        public int MaxDurability { get; set; }
        public bool IsBound { get; set; }
        public DateTime CreateTime { get; set; }

        // 强化等级
        public int EnhanceLevel { get; set; }

        // 附加属性（可以超过基础属性）
        public Dictionary<string, int> ExtraStats { get; set; } = new();
        public string Name { get; internal set; }
        public uint BoundPlayerId { get; internal set; }
        public bool IsExpired { get; internal set; }

        public ItemInstance(ItemDefinition definition, long instanceId)
        {
            Definition = definition;
            ItemId = definition.ItemId;
            InstanceId = instanceId;
            Count = 1;
            MaxDurability = 100;
            Durability = MaxDurability;
            CreateTime = DateTime.Now;
            Name = definition.Name;
        }
        
        /// <summary>
        /// 获取物品制造索引
        /// </summary>
        public uint GetMakeIndex()
        {
            return (uint)InstanceId;
        }
        
        /// <summary>
        /// 获取物品图片索引
        /// </summary>
        public ushort GetImageIndex()
        {
            // 在实际项目中，这里应该从Definition中获取真正的图片索引
            return (ushort)ItemId;
        }
        
        /// <summary>
        /// 获取物品名称
        /// </summary>
        public string GetName()
        {
            return Name ?? Definition.Name;
        }

        public bool CanStackWith(ItemInstance other)
        {
            return ItemId == other.ItemId && 
                   !IsBound && !other.IsBound &&
                   Definition.MaxStack > 1 &&
                   Count < Definition.MaxStack;
        }

        public int GetTotalDC() => Definition.DC + ExtraStats.GetValueOrDefault("DC", 0) + EnhanceLevel * 2;
        public int GetTotalAC() => Definition.AC + ExtraStats.GetValueOrDefault("AC", 0) + EnhanceLevel;
        public int GetTotalMAC() => Definition.MAC + ExtraStats.GetValueOrDefault("MAC", 0) + EnhanceLevel;
    }

    /// <summary>
    /// 背包
    /// </summary>
    public class Inventory
    {
        private readonly Dictionary<int, ItemInstance> _items = new();
        private readonly object _lock = new();
        public int MaxSlots { get; set; } = 40;

        public bool AddItem(ItemInstance item)
        {
            lock (_lock)
            {
                // 尝试堆叠
                if (item.Definition.MaxStack > 1)
                {
                    foreach (var existingItem in _items.Values)
                    {
                        if (existingItem.CanStackWith(item))
                        {
                            int canAdd = Math.Min(
                                item.Count,
                                item.Definition.MaxStack - existingItem.Count
                            );
                            existingItem.Count += canAdd;
                            item.Count -= canAdd;
                            
                            if (item.Count == 0)
                                return true;
                        }
                    }
                }

                // 找空位
                for (int i = 0; i < MaxSlots; i++)
                {
                    if (!_items.ContainsKey(i))
                    {
                        _items[i] = item;
                        return true;
                    }
                }

                return false; // 背包满
            }
        }

        public bool RemoveItem(int slot, int count = 1)
        {
            lock (_lock)
            {
                if (!_items.TryGetValue(slot, out var item))
                    return false;

                if (item.Count < count)
                    return false;

                item.Count -= count;
                if (item.Count == 0)
                {
                    _items.Remove(slot);
                }

                return true;
            }
        }

        public ItemInstance? GetItem(int slot)
        {
            lock (_lock)
            {
                _items.TryGetValue(slot, out var item);
                return item;
            }
        }

        public bool MoveItem(int fromSlot, int toSlot)
        {
            lock (_lock)
            {
                if (!_items.ContainsKey(fromSlot))
                    return false;

                var item = _items[fromSlot];
                _items.Remove(fromSlot);

                if (_items.ContainsKey(toSlot))
                {
                    var targetItem = _items[toSlot];
                    _items[fromSlot] = targetItem;
                }

                _items[toSlot] = item;
                return true;
            }
        }

        public int GetItemCount(int itemId)
        {
            lock (_lock)
            {
                return _items.Values
                    .Where(i => i.ItemId == itemId)
                    .Sum(i => i.Count);
            }
        }

        public int GetUsedSlots()
        {
            lock (_lock)
            {
                return _items.Count;
            }
        }

        public Dictionary<int, ItemInstance> GetAllItems()
        {
            lock (_lock)
            {
                return new Dictionary<int, ItemInstance>(_items);
            }
        }

        /// <summary>
        /// 清空背包
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _items.Clear();
                LogManager.Default.Info($"背包已清空");
            }
        }

        public ItemInstance? FindItem(ulong makeIndex)
        {
            lock (_lock)
            {
                return _items.Values.FirstOrDefault(item => item.InstanceId == (long)makeIndex);
            }
        }

        public int FindSlotByMakeIndex(ulong makeIndex)
        {
            lock (_lock)
            {
                var item = _items.Values.FirstOrDefault(item => item.InstanceId == (long)makeIndex);
                if (item == null)
                    return -1;

                return _items.FirstOrDefault(kvp => kvp.Value == item).Key;
            }
        }

        public bool HasItem(ulong makeIndex)
        {
            lock (_lock)
            {
                return _items.Values.Any(item => item.InstanceId == (long)makeIndex);
            }
        }

        public ItemInstance? GetItemByMakeIndex(ulong makeIndex)
        {
            return FindItem(makeIndex);
        }

        public bool RemoveItemByMakeIndex(ulong makeIndex, int count = 1)
        {
            return RemoveItem(makeIndex, count);
        }

        public bool RemoveItem(ulong makeIndex, int count = 1)
        {
            lock (_lock)
            {
                var item = _items.Values.FirstOrDefault(item => item.InstanceId == (long)makeIndex);
                if (item == null)
                    return false;

                if (item.Count < count)
                    return false;

                item.Count -= count;
                if (item.Count == 0)
                {
                    var slot = _items.FirstOrDefault(kvp => kvp.Value == item).Key;
                    _items.Remove(slot);
                }

                return true;
            }
        }
    }

    /// <summary>
    /// 装备栏
    /// </summary>
    public class Equipment
    {
        private readonly ItemInstance?[] _slots = new ItemInstance[(int)EquipSlot.Max];
        private readonly object _lock = new();
        private readonly HumanPlayer _owner;

        public Equipment(HumanPlayer owner)
        {
            _owner = owner;
        }

        /// <summary>
        /// 装备物品
        /// </summary>
        public bool Equip(EquipSlot slot, ItemInstance item)
        {
            lock (_lock)
            {
                if (!CanEquip(item))
                    return false;

                // 检查装备位置是否正确
                if (!IsCorrectSlot(slot, item))
                {
                    _owner.Say("这个装备不能放在这个位置");
                    return false;
                }

                // 检查是否有装备在该位置
                var oldItem = _slots[(int)slot];
                if (oldItem != null)
                {
                    // 先卸下旧装备
                    if (!UnequipToInventory(slot))
                    {
                        _owner.Say("背包空间不足，无法卸下旧装备");
                        return false;
                    }
                }

                // 装备新物品
                _slots[(int)slot] = item;
                
                // 应用装备属性
                ApplyEquipmentStats(item, true);
                
                // 发送装备更新消息
                SendEquipmentUpdate(slot, item);
                
                _owner.Say($"装备了 {item.Definition.Name}");
                return true;
            }
        }

        /// <summary>
        /// 卸下装备到背包
        /// </summary>
        public ItemInstance? Unequip(EquipSlot slot)
        {
            lock (_lock)
            {
                var item = _slots[(int)slot];
                if (item == null)
                    return null;

                // 检查背包空间
                if (!_owner.Inventory.AddItem(item))
                {
                    _owner.Say("背包空间不足");
                    return null;
                }

                // 移除装备属性
                ApplyEquipmentStats(item, false);
                
                // 清空装备槽
                _slots[(int)slot] = null;
                
                // 发送装备更新消息
                SendEquipmentUpdate(slot, null);
                
                _owner.Say($"卸下了 {item.Definition.Name}");
                return item;
            }
        }

        /// <summary>
        /// 卸下装备到背包（内部方法）
        /// </summary>
        private bool UnequipToInventory(EquipSlot slot)
        {
            var item = _slots[(int)slot];
            if (item == null)
                return true;

            if (!_owner.Inventory.AddItem(item))
                return false;

            // 移除装备属性
            ApplyEquipmentStats(item, false);
            
            _slots[(int)slot] = null;
            return true;
        }

        /// <summary>
        /// 获取装备
        /// </summary>
        public ItemInstance? GetEquipment(EquipSlot slot)
        {
            lock (_lock)
            {
                return _slots[(int)slot];
            }
        }

        /// <summary>
        /// 获取物品（别名方法）
        /// </summary>
        public ItemInstance? GetItem(EquipSlot slot)
        {
            return GetEquipment(slot);
        }

        /// <summary>
        /// 检查是否可以装备
        /// </summary>
        public bool CanEquip(ItemInstance item)
        {
            // 检查等级需求
            if (_owner.Level < item.Definition.RequireLevel)
            {
                _owner.Say($"需要等级 {item.Definition.RequireLevel}");
                return false;
            }

            // 检查职业需求
            if (item.Definition.RequireJob != -1 && _owner.Job != item.Definition.RequireJob)
            {
                _owner.Say("职业不符");
                return false;
            }

            // 检查性别需求（如果有）
            if (item.Definition.RequireSex != -1 && _owner.Sex != item.Definition.RequireSex)
            {
                _owner.Say("性别不符");
                return false;
            }

            // 检查绑定状态
            if (item.IsBound && item.IsBound)
            {
                _owner.Say("绑定物品不能装备");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 检查装备位置是否正确
        /// </summary>
        private bool IsCorrectSlot(EquipSlot slot, ItemInstance item)
        {
            // 根据物品类型确定正确的装备位置
            switch (item.Definition.Type)
            {
                case ItemType.Weapon:
                    return slot == EquipSlot.Weapon;
                case ItemType.Armor:
                    return slot == EquipSlot.Armor;
                case ItemType.Helmet:
                    return slot == EquipSlot.Helmet;
                case ItemType.Necklace:
                    return slot == EquipSlot.Necklace;
                case ItemType.Ring:
                    return slot == EquipSlot.RingLeft || slot == EquipSlot.RingRight;
                case ItemType.Bracelet:
                    return slot == EquipSlot.BraceletLeft || slot == EquipSlot.BraceletRight;
                case ItemType.Belt:
                    return slot == EquipSlot.Belt;
                case ItemType.Boots:
                    return slot == EquipSlot.Boots;
                case ItemType.Other:
                    // 坐骑等特殊装备
                    return slot == EquipSlot.Mount;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 应用装备属性
        /// </summary>
        private void ApplyEquipmentStats(ItemInstance item, bool equip)
        {
            int multiplier = equip ? 1 : -1;

            // 应用基础属性
            _owner.BaseDC += item.Definition.DC * multiplier;
            _owner.BaseMC += item.Definition.MC * multiplier;
            _owner.BaseSC += item.Definition.SC * multiplier;
            _owner.BaseAC += item.Definition.AC * multiplier;
            _owner.BaseMAC += item.Definition.MAC * multiplier;
            _owner.Accuracy += item.Definition.Accuracy * multiplier;
            _owner.Agility += item.Definition.Agility * multiplier;
            _owner.MaxHP += item.Definition.HP * multiplier;
            _owner.MaxMP += item.Definition.MP * multiplier;
            _owner.Lucky += item.Definition.Lucky * multiplier;

            // 应用额外属性
            foreach (var extraStat in item.ExtraStats)
            {
                switch (extraStat.Key)
                {
                    case "DC":
                        _owner.BaseDC += extraStat.Value * multiplier;
                        break;
                    case "AC":
                        _owner.BaseAC += extraStat.Value * multiplier;
                        break;
                    case "MAC":
                        _owner.BaseMAC += extraStat.Value * multiplier;
                        break;
                    case "HP":
                        _owner.MaxHP += extraStat.Value * multiplier;
                        break;
                    case "MP":
                        _owner.MaxMP += extraStat.Value * multiplier;
                        break;
                    case "Lucky":
                        _owner.Lucky += extraStat.Value * multiplier;
                        break;
                }
            }

            // 应用强化等级加成
            if (item.EnhanceLevel > 0)
            {
                _owner.BaseDC += item.EnhanceLevel * 2 * multiplier;
                _owner.BaseAC += item.EnhanceLevel * multiplier;
                _owner.BaseMAC += item.EnhanceLevel * multiplier;
            }

            // 更新当前HP/MP（如果最大值变化）
            if (equip)
            {
                _owner.CurrentHP = Math.Min(_owner.CurrentHP, _owner.MaxHP);
                _owner.CurrentMP = Math.Min(_owner.CurrentMP, _owner.MaxMP);
            }

            // 重新计算总属性
            _owner.RecalcTotalStats();
        }

        /// <summary>
        /// 发送装备更新消息
        /// </summary>
        private void SendEquipmentUpdate(EquipSlot slot, ItemInstance? item)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(_owner.ObjectId);
            builder.WriteUInt16(0x287); // SM_EQUIPMENTUPDATE
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteByte((byte)slot);
            
            if (item != null)
            {
                builder.WriteUInt64((ulong)item.InstanceId);
                builder.WriteInt32(item.ItemId);
                builder.WriteString(item.Definition.Name);
                builder.WriteUInt16((ushort)item.Durability);
                builder.WriteUInt16((ushort)item.MaxDurability);
                builder.WriteByte((byte)item.EnhanceLevel);
                
                // 发送装备属性
                builder.WriteInt32(item.Definition.DC);
                builder.WriteInt32(item.Definition.MC);
                builder.WriteInt32(item.Definition.SC);
                builder.WriteInt32(item.Definition.AC);
                builder.WriteInt32(item.Definition.MAC);
                builder.WriteInt32(item.Definition.Accuracy);
                builder.WriteInt32(item.Definition.Agility);
                builder.WriteInt32(item.Definition.HP);
                builder.WriteInt32(item.Definition.MP);
                builder.WriteInt32(item.Definition.Lucky);
                
                // 发送额外属性
                builder.WriteByte((byte)item.ExtraStats.Count);
                foreach (var extraStat in item.ExtraStats)
                {
                    builder.WriteString(extraStat.Key);
                    builder.WriteInt32(extraStat.Value);
                }
            }
            else
            {
                builder.WriteUInt64(0);
                builder.WriteInt32(0);
                builder.WriteString("");
                builder.WriteUInt16(0);
                builder.WriteUInt16(0);
                builder.WriteByte(0);
                
                // 空装备的属性
                for (int i = 0; i < 10; i++) builder.WriteInt32(0);
                builder.WriteByte(0);
            }
            
            _owner.SendMessage(builder.Build());
        }

        /// <summary>
        /// 获取总属性
        /// </summary>
        public CombatStats GetTotalStats()
        {
            var stats = new CombatStats();
            
            lock (_lock)
            {
                foreach (var item in _slots)
                {
                    if (item == null) continue;
                    
                    stats.MinDC += item.GetTotalDC();
                    stats.MaxDC += item.GetTotalDC();
                    stats.MinAC += item.GetTotalAC();
                    stats.MaxAC += item.GetTotalAC();
                    stats.MinMAC += item.GetTotalMAC();
                    stats.MaxMAC += item.GetTotalMAC();
                    stats.Accuracy += item.Definition.Accuracy;
                    stats.Agility += item.Definition.Agility;
                    stats.Lucky += item.Definition.Lucky;
                    stats.HP += item.Definition.HP;
                    stats.MP += item.Definition.MP;
                }
            }

            return stats;
        }

        /// <summary>
        /// 检查装备耐久度
        /// </summary>
        public void CheckDurability()
        {
            lock (_lock)
            {
                foreach (var item in _slots)
                {
                    if (item == null) continue;
                    
                    if (item.Durability <= 0)
                    {
                        // 装备损坏
                        _owner.Say($"{item.Definition.Name} 已损坏，需要修理");
                        
                        // 装备损坏效果：移除装备属性加成
                        ApplyEquipmentStats(item, false);
                        
                        // 发送装备损坏消息
                        SendEquipmentBrokenMessage(item);
                        
                        // 从装备栏移除损坏的装备
                        for (int i = 0; i < _slots.Length; i++)
                        {
                            if (_slots[i] == item)
                            {
                                _slots[i] = null;
                                break;
                            }
                        }
                        
                        // 发送装备更新消息
                        for (int i = 0; i < _slots.Length; i++)
                        {
                            if (_slots[i] == null) continue;
                            SendEquipmentUpdate((EquipSlot)i, _slots[i]);
                        }
                    }
                    else if (item.Durability <= item.MaxDurability * 0.2)
                    {
                        // 装备耐久度低警告
                        _owner.Say($"{item.Definition.Name} 耐久度低，请及时修理");
                        
                        // 发送耐久度警告消息
                        SendDurabilityWarningMessage(item);
                    }
                }
            }
        }
        
        /// <summary>
        /// 发送装备损坏消息
        /// </summary>
        private void SendEquipmentBrokenMessage(ItemInstance item)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(_owner.ObjectId);
            builder.WriteUInt16(0x28F); // SM_EQUIPMENTBROKEN
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteString(item.Definition.Name);
            
            _owner.SendMessage(builder.Build());
        }
        
        /// <summary>
        /// 发送耐久度警告消息
        /// </summary>
        private void SendDurabilityWarningMessage(ItemInstance item)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(_owner.ObjectId);
            builder.WriteUInt16(0x290); // SM_DURABILITYWARNING
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteString(item.Definition.Name);
            builder.WriteUInt16((ushort)item.Durability);
            builder.WriteUInt16((ushort)item.MaxDurability);
            
            _owner.SendMessage(builder.Build());
        }

        /// <summary>
        /// 减少装备耐久度
        /// </summary>
        public void ReduceDurability(int amount = 1)
        {
            lock (_lock)
            {
                foreach (var item in _slots)
                {
                    if (item == null) continue;
                    
                    // 根据装备类型决定耐久度消耗
                    int durabilityLoss = amount;
                    
                    // 武器在攻击时消耗更多耐久度
                    if (item.Definition.Type == ItemType.Weapon)
                        durabilityLoss *= 2;
                    
                    // 防具在被攻击时消耗耐久度
                    if (item.Definition.Type == ItemType.Armor || 
                        item.Definition.Type == ItemType.Helmet ||
                        item.Definition.Type == ItemType.Boots)
                        durabilityLoss = 1; // 防具每次被攻击消耗1点耐久度
                    
                    // 首饰耐久度消耗较慢
                    if (item.Definition.Type == ItemType.Necklace ||
                        item.Definition.Type == ItemType.Ring ||
                        item.Definition.Type == ItemType.Bracelet ||
                        item.Definition.Type == ItemType.Belt)
                        durabilityLoss = (int)(amount * 0.5); // 首饰消耗减半
                    
                    item.Durability = Math.Max(0, item.Durability - durabilityLoss);
                    
                    if (item.Durability <= 0)
                    {
                        // 装备损坏
                        _owner.Say($"{item.Definition.Name} 已损坏");
                        
                        // 装备损坏效果：移除装备属性加成
                        ApplyEquipmentStats(item, false);
                        
                        // 发送装备损坏消息
                        SendEquipmentBrokenMessage(item);
                        
                        // 从装备栏移除损坏的装备
                        for (int i = 0; i < _slots.Length; i++)
                        {
                            if (_slots[i] == item)
                            {
                                _slots[i] = null;
                                break;
                            }
                        }
                    }
                    else if (item.Durability <= item.MaxDurability * 0.2)
                    {
                        // 装备耐久度低警告
                        _owner.Say($"{item.Definition.Name} 耐久度低，请及时修理");
                        
                        // 发送耐久度警告消息
                        SendDurabilityWarningMessage(item);
                    }
                }
            }
        }

        /// <summary>
        /// 修理装备
        /// </summary>
        public bool RepairEquipment(EquipSlot slot)
        {
            lock (_lock)
            {
                var item = _slots[(int)slot];
                if (item == null)
                {
                    _owner.Say("该位置没有装备");
                    return false;
                }

                // 计算修理费用
                uint repairCost = CalculateRepairCost(item);
                if (_owner.Gold < repairCost)
                {
                    _owner.Say($"修理需要 {repairCost} 金币，金币不足");
                    return false;
                }

                // 扣除金币
                _owner.TakeGold(repairCost);
                
                // 修理装备
                item.Durability = item.MaxDurability;
                
                _owner.Say($"修理了 {item.Definition.Name}，花费 {repairCost} 金币");
                return true;
            }
        }

        /// <summary>
        /// 计算修理费用
        /// </summary>
        private uint CalculateRepairCost(ItemInstance item)
        {
            // 基础修理费用 = 物品售价 * 耐久度损失比例
            float durabilityLossRatio = 1.0f - ((float)item.Durability / item.MaxDurability);
            uint baseCost = (uint)(item.Definition.SellPrice * durabilityLossRatio);
            
            // 强化等级增加修理费用
            if (item.EnhanceLevel > 0)
                baseCost += (uint)(baseCost * item.EnhanceLevel * 0.1f);
            
            return Math.Max(10, baseCost); // 最低10金币
        }

        /// <summary>
        /// 显示装备信息
        /// </summary>
        public void ShowEquipmentInfo()
        {
            lock (_lock)
            {
                _owner.Say("=== 装备信息 ===");
                
                for (int i = 0; i < _slots.Length; i++)
                {
                    var item = _slots[i];
                    var slotName = ((EquipSlot)i).ToString();
                    
                    if (item != null)
                    {
                        _owner.Say($"{slotName}: {item.Definition.Name} (Lv.{item.Definition.Level})");
                        _owner.Say($"  耐久: {item.Durability}/{item.MaxDurability}");
                        _owner.Say($"  强化: +{item.EnhanceLevel}");
                        
                        if (item.Definition.DC > 0)
                            _owner.Say($"  攻击: {item.GetTotalDC()}");
                        if (item.Definition.AC > 0)
                            _owner.Say($"  防御: {item.GetTotalAC()}");
                        if (item.Definition.MAC > 0)
                            _owner.Say($"  魔防: {item.GetTotalMAC()}");
                    }
                    else
                    {
                        _owner.Say($"{slotName}: 空");
                    }
                }
            }
        }

        /// <summary>
        /// 获取所有装备
        /// </summary>
        public List<ItemInstance> GetAllEquipment()
        {
            lock (_lock)
            {
                return _slots.Where(item => item != null).ToList()!;
            }
        }
        
        /// <summary>
        /// 获取当前武器
        /// </summary>
        public ItemInstance? GetWeapon()
        {
            lock (_lock)
            {
                return _slots[(int)EquipSlot.Weapon];
            }
        }

        /// <summary>
        /// 检查是否有特殊装备效果
        /// </summary>
        public void CheckSpecialEffects()
        {
            lock (_lock)
            {
                foreach (var item in _slots)
                {
                    if (item == null) continue;
                    
                    // 检查特殊效果
                    CheckItemSpecialEffects(item);
                }
            }
        }
        
        /// <summary>
        /// 检查物品特殊效果
        /// </summary>
        private void CheckItemSpecialEffects(ItemInstance item)
        {
            // 检查幸运属性
            if (item.Definition.Lucky > 0)
            {
                // 幸运装备增加暴击率和命中率
                _owner.Lucky += item.Definition.Lucky;
                // 发送幸运效果消息
                if (item.Definition.Lucky >= 3)
                {
                    SendSpecialEffectMessage(item, "幸运+3：大幅增加暴击率");
                }
                else if (item.Definition.Lucky >= 2)
                {
                    SendSpecialEffectMessage(item, "幸运+2：增加暴击率");
                }
            }
            
            // 检查诅咒属性
            if (item.Definition.Lucky < 0)
            {
                // 诅咒装备减少属性
                _owner.Lucky += item.Definition.Lucky; // 幸运值为负
                SendSpecialEffectMessage(item, $"诅咒：减少幸运{Math.Abs(item.Definition.Lucky)}点");
            }
            
            // 检查套装效果
            CheckSetBonusEffects();
            
            // 检查特殊装备效果（如麻痹、复活等）
            CheckUniqueItemEffects(item);
        }
        
        /// <summary>
        /// 检查套装效果
        /// </summary>
        private void CheckSetBonusEffects()
        {
            // 统计套装部件数量
            Dictionary<string, int> setCounts = new();
            
            foreach (var item in _slots)
            {
                if (item == null) continue;
                
                // 假设ItemDefinition有SetName属性
                // 这里需要根据实际的ItemDefinition结构来调整
                // string setName = item.Definition.SetName;
                // if (!string.IsNullOrEmpty(setName))
                // {
                //     setCounts.TryGetValue(setName, out int count);
                //     setCounts[setName] = count + 1;
                // }
            }
            
            // 应用套装效果
            foreach (var kvp in setCounts)
            {
                string setName = kvp.Key;
                int count = kvp.Value;
                
                if (count >= 2)
                {
                    // 2件套效果
                    ApplySetBonus(setName, 2);
                }
                if (count >= 4)
                {
                    // 4件套效果
                    ApplySetBonus(setName, 4);
                }
                if (count >= 6)
                {
                    // 6件套效果
                    ApplySetBonus(setName, 6);
                }
            }
        }
        
        /// <summary>
        /// 应用套装效果
        /// </summary>
        private void ApplySetBonus(string setName, int pieceCount)
        {
            // 根据套装名称和件数应用效果
            // 这里需要根据实际的套装配置来完善
            string effectMessage = $"{setName} {pieceCount}件套效果激活";
            SendSpecialEffectMessage(null, effectMessage);
        }
        
        /// <summary>
        /// 检查特殊物品效果（麻痹戒指、复活戒指等）
        /// </summary>
        private void CheckUniqueItemEffects(ItemInstance item)
        {
            // 根据物品ID检查特殊效果
            switch (item.ItemId)
            {
                case 5001: // 麻痹戒指
                    // 麻痹效果：攻击时有概率麻痹目标
                    // 这里需要实现麻痹效果逻辑
                    SendSpecialEffectMessage(item, "麻痹戒指：攻击时有概率麻痹目标");
                    break;
                    
                case 5002: // 复活戒指
                    // 复活效果：死亡后自动复活
                    // 这里需要实现复活效果逻辑
                    SendSpecialEffectMessage(item, "复活戒指：死亡后自动复活");
                    break;
                    
                case 5003: // 护身戒指
                    // 护身效果：受到伤害时优先消耗MP
                    // 这里需要实现护身效果逻辑
                    SendSpecialEffectMessage(item, "护身戒指：受到伤害时优先消耗MP");
                    break;
                    
                case 5004: // 传送戒指
                    // 传送效果：可以使用传送功能
                    // 这里需要实现传送效果逻辑
                    SendSpecialEffectMessage(item, "传送戒指：可以使用传送功能");
                    break;
                    
                case 5005: // 隐身戒指
                    // 隐身效果：可以隐身
                    // 这里需要实现隐身效果逻辑
                    SendSpecialEffectMessage(item, "隐身戒指：可以隐身");
                    break;
                    
                default:
                    // 其他物品没有特殊效果
                    break;
            }
        }
        
        /// <summary>
        /// 发送特殊效果消息
        /// </summary>
        private void SendSpecialEffectMessage(ItemInstance? item, string effectMessage)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(_owner.ObjectId);
            builder.WriteUInt16(0x291); // SM_SPECIALEFFECT
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            
            if (item != null)
            {
                builder.WriteString(item.Definition.Name);
            }
            else
            {
                builder.WriteString("");
            }
            
            builder.WriteString(effectMessage);
            
            _owner.SendMessage(builder.Build());
        }
    }

    /// <summary>
    /// 物品管理器
    /// </summary>
    public class ItemManager
    {
        private static ItemManager? _instance;
        public static ItemManager Instance => _instance ??= new ItemManager();

        private readonly ConcurrentDictionary<int, ItemDefinition> _definitions = new();
        private readonly ConcurrentDictionary<string, ItemDefinition> _definitionsByName = new();
        private long _nextInstanceId = 1;
        private bool _isLoaded = false;

        private ItemManager()
        {
            // 不再在构造函数中初始化，等待Load方法调用
        }

        /// <summary>
        /// 加载物品数据文件
        /// </summary>
        public bool Load(string filePath)
        {
            if (_isLoaded)
            {
                LogManager.Default.Warning("物品数据已加载，跳过重复加载");
                return true;
            }

            try
            {
                var parser = new Parsers.ItemDataParser();
                if (parser.Load(filePath))
                {
                    int loadedCount = 0;
                    foreach (var itemClass in parser.GetAllItems())
                    {
                        if (AddItemClass(itemClass))
                        {
                            loadedCount++;
                        }
                    }
                    
                    LogManager.Default.Info($"成功加载 {loadedCount} 个物品定义");
                    _isLoaded = true;
                    return true;
                }
                else
                {
                    LogManager.Default.Error($"加载物品数据文件失败: {filePath}");
                    // 加载失败时使用默认物品作为后备
                    InitializeDefaultItems();
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载物品数据时发生异常: {filePath}", exception: ex);
                // 发生异常时使用默认物品作为后备
                InitializeDefaultItems();
                return false;
            }
        }

        /// <summary>
        /// 加载物品限制配置
        /// </summary>
        public bool LoadLimit(string filePath)
        {
            try
            {
                var parser = new Parsers.ItemDataParser();
                return parser.LoadItemLimit(filePath);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载物品限制配置失败: {filePath}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 加载物品脚本链接
        /// </summary>
        public bool LoadScriptLink(string filePath)
        {
            try
            {
                var parser = new Parsers.ItemDataParser();
                return parser.LoadItemScript(filePath);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载物品脚本链接失败: {filePath}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 添加物品类（将ItemClass转换为ItemDefinition）
        /// </summary>
        private bool AddItemClass(Parsers.ItemClass itemClass)
        {
            try
            {
                // 将StdMode转换为ItemType
                ItemType itemType = ConvertStdModeToItemType(itemClass.StdMode);
                
                // 生成唯一的ItemId（使用名称的哈希值）
                int itemId = Math.Abs(itemClass.Name.GetHashCode()) % 1000000;
                
                // 确保ItemId是唯一的
                while (_definitions.ContainsKey(itemId))
                {
                    itemId = (itemId + 1) % 1000000;
                }

                var definition = new ItemDefinition(itemId, itemClass.Name, itemType)
                {
                    // 基础属性
                    Level = itemClass.NeedLevel,
                    DC = itemClass.DC[0] + itemClass.DC[1], // 最小+最大
                    MC = itemClass.MC[0] + itemClass.MC[1],
                    SC = itemClass.SC[0] + itemClass.SC[1],
                    AC = itemClass.AC[0] + itemClass.AC[1],
                    MAC = itemClass.MAC[0] + itemClass.MAC[1],
                    
                    // 其他属性
                    MaxStack = GetMaxStackByType(itemType),
                    BuyPrice = (uint)itemClass.Price,
                    SellPrice = (uint)(itemClass.Price / 2), // 售价通常是买价的一半
                    RequireLevel = itemClass.NeedLevel,
                    
                    // 根据NeedType设置职业需求
                    RequireJob = ConvertNeedTypeToJob(itemClass.NeedType),
                    
                    // 耐久度
                    // 注意：ItemDefinition中没有直接的耐久度属性，耐久度在ItemInstance中处理
                };

                // 根据StdMode设置特殊属性
                SetSpecialProperties(definition, itemClass);

                AddDefinition(definition);
                _definitionsByName[itemClass.Name] = definition;
                
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"添加物品类失败: {itemClass.Name}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 将StdMode转换为ItemType
        /// </summary>
        private ItemType ConvertStdModeToItemType(byte stdMode)
        {
            switch (stdMode)
            {
                case 5:  // 武器
                case 6:  // 武器
                case 10: // 男衣服
                case 11: // 女衣服
                    return ItemType.Weapon;
                case 15: // 头盔
                    return ItemType.Helmet;
                case 19: // 项链
                    return ItemType.Necklace;
                case 20: // 戒指
                case 21: // 戒指
                case 22: // 戒指
                case 23: // 戒指
                    return ItemType.Ring;
                case 24: // 手镯
                case 26: // 手镯
                    return ItemType.Bracelet;
                case 30: // 符咒
                    return ItemType.Scroll;
                case 31: // 肉
                case 40: // 书
                    return ItemType.Book;
                case 42: // 药水
                    return ItemType.Potion;
                case 45: // 矿石
                    return ItemType.Material;
                default:
                    return ItemType.Other;
            }
        }

        /// <summary>
        /// 根据物品类型获取最大堆叠数量
        /// </summary>
        private int GetMaxStackByType(ItemType type)
        {
            return type == ItemType.Potion || type == ItemType.Material || type == ItemType.Scroll ? 100 : 1;
        }

        /// <summary>
        /// 将NeedType转换为职业需求
        /// </summary>
        private int ConvertNeedTypeToJob(byte needType)
        {
            // 0=战士, 1=法师, 2=道士, -1=所有职业
            switch (needType)
            {
                case 0: return 0; // 战士
                case 1: return 1; // 法师
                case 2: return 2; // 道士
                default: return -1; // 所有职业
            }
        }

        /// <summary>
        /// 设置特殊属性
        /// </summary>
        private void SetSpecialProperties(ItemDefinition definition, Parsers.ItemClass itemClass)
        {
            // 设置准确、敏捷、幸运等属性
            // 这里可以根据StdMode和Shape设置不同的属性
            if (itemClass.StdMode == 5 || itemClass.StdMode == 6) // 武器
            {
                definition.Accuracy = itemClass.SpecialPower; // SpecialPower可能表示准确
            }
            
            // 设置幸运/诅咒
            definition.Lucky = itemClass.SpecialPower;
        }

        /// <summary>
        /// 初始化默认物品（后备方案）
        /// </summary>
        private void InitializeDefaultItems()
        {
            // 武器
            AddDefinition(new ItemDefinition(1001, "木剑", ItemType.Weapon)
            {
                Level = 1,
                DC = 2,
                RequireLevel = 1,
                BuyPrice = 50,
                SellPrice = 10
            });

            AddDefinition(new ItemDefinition(1002, "铁剑", ItemType.Weapon)
            {
                Level = 5,
                DC = 5,
                RequireLevel = 5,
                Quality = ItemQuality.Fine,
                BuyPrice = 200,
                SellPrice = 40
            });

            AddDefinition(new ItemDefinition(1003, "钢剑", ItemType.Weapon)
            {
                Level = 10,
                DC = 10,
                RequireLevel = 10,
                Quality = ItemQuality.Rare,
                BuyPrice = 1000,
                SellPrice = 200
            });

            // 防具
            AddDefinition(new ItemDefinition(2001, "布衣", ItemType.Armor)
            {
                Level = 1,
                AC = 2,
                RequireLevel = 1,
                BuyPrice = 50,
                SellPrice = 10
            });

            AddDefinition(new ItemDefinition(2002, "皮甲", ItemType.Armor)
            {
                Level = 5,
                AC = 5,
                RequireLevel = 5,
                Quality = ItemQuality.Fine,
                BuyPrice = 200,
                SellPrice = 40
            });

            // 药水
            AddDefinition(new ItemDefinition(3001, "小红药", ItemType.Potion)
            {
                MaxStack = 100,
                HP = 50,
                BuyPrice = 10,
                SellPrice = 2
            });

            AddDefinition(new ItemDefinition(3002, "大红药", ItemType.Potion)
            {
                MaxStack = 100,
                HP = 150,
                BuyPrice = 30,
                SellPrice = 6
            });

            AddDefinition(new ItemDefinition(3003, "小蓝药", ItemType.Potion)
            {
                MaxStack = 100,
                MP = 50,
                BuyPrice = 10,
                SellPrice = 2
            });

            // 材料
            AddDefinition(new ItemDefinition(4001, "铁矿石", ItemType.Material)
            {
                MaxStack = 100,
                BuyPrice = 5,
                SellPrice = 1
            });

            AddDefinition(new ItemDefinition(4002, "布料", ItemType.Material)
            {
                MaxStack = 100,
                BuyPrice = 3,
                SellPrice = 1
            });

            LogManager.Default.Info($"已加载 {_definitions.Count} 个默认物品定义");
            _isLoaded = true;
        }

        public void AddDefinition(ItemDefinition definition)
        {
            _definitions[definition.ItemId] = definition;
        }

        public ItemDefinition? GetDefinition(int itemId)
        {
            _definitions.TryGetValue(itemId, out var definition);
            return definition;
        }

        public ItemInstance? CreateItem(int itemId, int count = 1)
        {
            var definition = GetDefinition(itemId);
            if (definition == null)
                return null;

            long instanceId = System.Threading.Interlocked.Increment(ref _nextInstanceId);
            var item = new ItemInstance(definition, instanceId)
            {
                Count = count
            };

            return item;
        }

        public List<ItemDefinition> GetAllDefinitions()
        {
            return _definitions.Values.ToList();
        }

        public List<ItemDefinition> GetItemsByType(ItemType type)
        {
            return _definitions.Values
                .Where(d => d.Type == type)
                .ToList();
        }

        public List<ItemDefinition> GetItemsByQuality(ItemQuality quality)
        {
            return _definitions.Values
                .Where(d => d.Quality == quality)
                .ToList();
        }
    }

    /// <summary>
    /// 掉落系统
    /// </summary>
    public class LootSystem
    {
        public class LootEntry
        {
            public int ItemId { get; set; }
            public float DropRate { get; set; } // 0.0 - 1.0
            public int MinCount { get; set; } = 1;
            public int MaxCount { get; set; } = 1;
        }

        private readonly Dictionary<int, List<LootEntry>> _monsterLoots = new();

        public void AddMonsterLoot(int monsterId, int itemId, float dropRate, int minCount = 1, int maxCount = 1)
        {
            if (!_monsterLoots.ContainsKey(monsterId))
            {
                _monsterLoots[monsterId] = new List<LootEntry>();
            }

            _monsterLoots[monsterId].Add(new LootEntry
            {
                ItemId = itemId,
                DropRate = dropRate,
                MinCount = minCount,
                MaxCount = maxCount
            });
        }

        public List<ItemInstance> GenerateLoot(int monsterId)
        {
            var loot = new List<ItemInstance>();

            if (!_monsterLoots.TryGetValue(monsterId, out var entries))
                return loot;

            foreach (var entry in entries)
            {
                if (Random.Shared.NextDouble() < entry.DropRate)
                {
                    int count = Random.Shared.Next(entry.MinCount, entry.MaxCount + 1);
                    var item = ItemManager.Instance.CreateItem(entry.ItemId, count);
                    if (item != null)
                    {
                        loot.Add(item);
                    }
                }
            }

            return loot;
        }

        public void InitializeDefaultLoots()
        {
            // 骷髅(ID:1)掉落
            AddMonsterLoot(1, 3001, 0.3f);  // 30%掉小红药
            AddMonsterLoot(1, 4001, 0.2f);  // 20%掉铁矿石
            AddMonsterLoot(1, 1001, 0.05f); // 5%掉木剑

            LogManager.Default.Info("掉落表已初始化");
        }
    }
}
