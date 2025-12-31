using System;
using System.Collections.Generic;
using System.Linq;
using MirCommon;
using MirCommon.Utils;

namespace GameServer
{
    /// <summary>
    /// NPC对象 - 功能性NPC系统
    /// </summary>
    public class Npc : MapObject
    {
        // 基础属性
        public int NpcId { get; set; }
        public string Name { get; set; }
        public NpcType Type { get; set; }
        public int ImageIndex { get; set; }
        
        // 功能相关
        public bool CanTalk { get; set; } = true;
        public bool CanTrade { get; set; } = false;
        public bool CanRepair { get; set; } = false;
        public bool CanStore { get; set; } = false;
        
        // 脚本相关
        public string? ScriptFile { get; set; }
        
        // 商店库存（如果是商人NPC）
        private readonly List<ItemInstance> _shopItems = new();
        private readonly object _shopLock = new();

        public Npc(int npcId, string name, NpcType type)
        {
            NpcId = npcId;
            Name = name;
            Type = type;
        }

        public override ObjectType GetObjectType() => ObjectType.NPC;

        /// <summary>
        /// 玩家与NPC对话
        /// </summary>
        public void OnTalk(HumanPlayer player)
        {
            if (!CanTalk)
                return;

            LogManager.Default.Info($"{player.Name} 与 {Name} 对话");
            
            // 根据NPC类型执行不同的逻辑
            switch (Type)
            {
                case NpcType.Merchant:
                    OpenShop(player);
                    break;
                case NpcType.Warehouse:
                    OpenWarehouse(player);
                    break;
                case NpcType.Quest:
                    ShowQuests(player);
                    break;
                case NpcType.Teleporter:
                    ShowTeleportMenu(player);
                    break;
                case NpcType.Script:
                    ExecuteScript(player);
                    break;
                default:
                    SendGreeting(player);
                    break;
            }
        }

        /// <summary>
        /// 打开商店
        /// </summary>
        private void OpenShop(HumanPlayer player)
        {
            if (!CanTrade)
            {
                player.Say("我现在不能交易");
                return;
            }

            lock (_shopLock)
            {
                // 发送商店界面消息
                var builder = new PacketBuilder();
                builder.WriteUInt32(ObjectId);
                builder.WriteUInt16(ProtocolCmd.SM_OPENSHOP);
                builder.WriteUInt16(0);
                builder.WriteUInt16(0);
                builder.WriteUInt16(0);
                
                // 发送商店物品列表
                builder.WriteUInt16((ushort)_shopItems.Count);
                foreach (var item in _shopItems)
                {
                    builder.WriteUInt32((uint)item.ItemId);
                    builder.WriteUInt32((uint)item.Definition.BuyPrice);
                    builder.WriteString(item.Definition.Name);
                }
                
                byte[] packet = builder.Build();
                player.SendMessage(packet);
                
                LogManager.Default.Debug($"{player.Name} 打开了 {Name} 的商店");
            }
        }

        /// <summary>
        /// 打开仓库
        /// </summary>
        private void OpenWarehouse(HumanPlayer player)
        {
            if (!CanStore)
            {
                player.Say("我现在不能提供仓库服务");
                return;
            }

            // 发送仓库界面消息
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(ProtocolCmd.SM_OPENSTORAGE);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            
            byte[] packet = builder.Build();
            player.SendMessage(packet);
            
            LogManager.Default.Debug($"{player.Name} 打开了仓库");
        }

        /// <summary>
        /// 显示任务
        /// </summary>
        private void ShowQuests(HumanPlayer player)
        {
            // 获取玩家可接任务列表
            var availableQuests = QuestDefinitionManager.Instance.GetAllDefinitions()
                .Where(q => q.CanAccept(player) && 
                           !player.QuestManager.HasActiveQuest(q.QuestId) &&
                           (!player.QuestManager.HasCompletedQuest(q.QuestId) || q.Repeatable))
                .ToList();
            
            if (availableQuests.Count == 0)
            {
                player.Say("我这里暂时没有适合你的任务。");
                return;
            }

            // 构建任务列表消息
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(ProtocolCmd.SM_DIALOG);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteString("我这里有些任务，你愿意帮忙吗？");
            
            // 添加任务选项
            builder.WriteByte((byte)availableQuests.Count);
            for (int i = 0; i < availableQuests.Count; i++)
            {
                var quest = availableQuests[i];
                builder.WriteInt32(i + 1);
                builder.WriteString($"{quest.Name} (等级要求: {quest.RequireLevel})");
            }
            
            byte[] packet = builder.Build();
            player.SendMessage(packet);
        }

        /// <summary>
        /// 显示传送菜单
        /// </summary>
        private void ShowTeleportMenu(HumanPlayer player)
        {
            // 获取传送目的地列表（这里使用硬编码的示例目的地）
            var destinations = new List<(string name, int mapId, ushort x, ushort y, uint cost)>
            {
                ("比奇城", 0, 300, 300, 100),
                ("银杏山谷", 1, 200, 200, 200),
                ("毒蛇山谷", 2, 150, 150, 300),
                ("盟重省", 3, 400, 400, 500)
            };

            // 构建传送列表消息
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(ProtocolCmd.SM_TELEPORTLIST);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            
            // 发送目的地列表
            builder.WriteUInt16((ushort)destinations.Count);
            foreach (var dest in destinations)
            {
                builder.WriteString(dest.name);
                builder.WriteUInt32((uint)dest.mapId);
                builder.WriteUInt16(dest.x);
                builder.WriteUInt16(dest.y);
                builder.WriteUInt32(dest.cost);
            }
            
            byte[] packet = builder.Build();
            player.SendMessage(packet);
        }

        /// <summary>
        /// 执行脚本
        /// </summary>
        private void ExecuteScript(HumanPlayer player)
        {
            if (string.IsNullOrEmpty(ScriptFile))
            {
                SendGreeting(player);
                return;
            }

            // 加载并执行NPC脚本
            var script = NPCScriptManager.Instance.LoadScript(ScriptFile);
            if (script == null)
            {
                player.Say("脚本加载失败");
                return;
            }

            var startDialog = script.GetStartDialog();
            if (startDialog == null)
            {
                player.Say("脚本没有可用的对话");
                return;
            }

            // 发送起始对话框
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(ProtocolCmd.SM_DIALOG);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteString(startDialog.Text);
            
            // 添加选项
            builder.WriteByte((byte)startDialog.Options.Count);
            foreach (var option in startDialog.Options)
            {
                builder.WriteInt32(option.Id);
                builder.WriteString(option.Text);
            }
            
            byte[] packet = builder.Build();
            player.SendMessage(packet);
            
            LogManager.Default.Debug($"执行NPC脚本: {ScriptFile}");
        }

        /// <summary>
        /// 发送问候
        /// </summary>
        private void SendGreeting(HumanPlayer player)
        {
            player.Say($"{Name}: 你好，{player.Name}！");
        }

        /// <summary>
        /// 添加商品到商店
        /// </summary>
        public void AddShopItem(ItemInstance item)
        {
            lock (_shopLock)
            {
                _shopItems.Add(item);
            }
        }

        /// <summary>
        /// 购买物品
        /// </summary>
        public bool BuyItem(HumanPlayer player, int itemIndex)
        {
            if (!CanTrade)
                return false;

            lock (_shopLock)
            {
                if (itemIndex < 0 || itemIndex >= _shopItems.Count)
                    return false;

                var item = _shopItems[itemIndex];
                
                // 检查价格
                if (player.Gold < item.Definition.BuyPrice)
                {
                    player.Say("你的金币不足");
                    return false;
                }

                // 扣除金币
                if (!player.TakeGold(item.Definition.BuyPrice))
                    return false;

                // 添加物品到背包
                var newItem = ItemManager.Instance.CreateItem(item.ItemId);
                if (newItem != null && player.Inventory.AddItem(newItem))
                {
                    player.Say($"购买了 {item.Definition.Name}");
                    return true;
                }
                else
                {
                    // 背包满了，退款
                    player.AddGold(item.Definition.BuyPrice);
                    player.Say("背包已满");
                    return false;
                }
            }
        }

        /// <summary>
        /// 出售物品
        /// </summary>
        public bool SellItem(HumanPlayer player, int bagSlot)
        {
            if (!CanTrade)
                return false;

            var item = player.Inventory.GetItem(bagSlot);
            if (item == null)
                return false;

            if (!item.Definition.CanTrade)
            {
                player.Say("这个物品不能出售");
                return false;
            }

            // 移除物品
            if (!player.Inventory.RemoveItem(bagSlot, 1))
                return false;

            // 给予金币
            player.AddGold(item.Definition.SellPrice);
            player.Say($"出售了 {item.Definition.Name}，获得 {item.Definition.SellPrice} 金币");
            
            return true;
        }

        /// <summary>
        /// 修理物品
        /// </summary>
        public bool RepairItem(HumanPlayer player, EquipSlot slot)
        {
            if (!CanRepair)
            {
                player.Say("我不能修理物品");
                return false;
            }

            var item = player.Equipment.GetEquipment(slot);
            if (item == null)
            {
                player.Say("没有装备可以修理");
                return false;
            }

            if (item.Durability >= item.MaxDurability)
            {
                player.Say("这个物品不需要修理");
                return false;
            }

            // 计算修理费用
            int repairCost = CalculateRepairCost(item);
            
            if (player.Gold < repairCost)
            {
                player.Say($"修理需要 {repairCost} 金币，你的金币不足");
                return false;
            }

            // 扣除金币
            if (!player.TakeGold((uint)repairCost))
                return false;

            // 修理
            item.Durability = item.MaxDurability;
            player.Say($"修理完成，花费 {repairCost} 金币");
            
            return true;
        }

        /// <summary>
        /// 计算修理费用
        /// </summary>
        private int CalculateRepairCost(ItemInstance item)
        {
            int damageCost = (item.MaxDurability - item.Durability) * 10;
            int baseCost = (int)(item.Definition.BuyPrice * 0.1);
            return Math.Max(damageCost, baseCost);
        }

        /// <summary>
        /// 传送玩家
        /// </summary>
        public bool TeleportPlayer(HumanPlayer player, int targetMapId, ushort targetX, ushort targetY, uint cost = 0)
        {
            // 检查费用
            if (cost > 0)
            {
                if (player.Gold < cost)
                {
                    player.Say($"传送需要 {cost} 金币");
                    return false;
                }
                
                if (!player.TakeGold(cost))
                    return false;
            }

            // 获取目标地图
            var targetMap = MapManager.Instance.GetMap((uint)targetMapId);
            if (targetMap == null)
            {
                player.Say("传送目标不存在");
                if (cost > 0) player.AddGold(cost); // 退款
                return false;
            }

            // 离开当前地图
            if (player.CurrentMap != null)
            {
                player.CurrentMap.RemoveObject(player);
            }

            // 进入新地图
            if (targetMap.AddObject(player, targetX, targetY))
            {
                player.Say($"已传送到 {targetMap.MapName}");
                return true;
            }
            else
            {
                player.Say("传送失败");
                if (cost > 0) player.AddGold(cost); // 退款
                return false;
            }
        }

        public override bool GetViewMsg(out byte[] msg, MapObject? viewer = null)
        {
            // 构建NPC显示消息
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(ProtocolCmd.SM_APPEAR);
            builder.WriteUInt16((ushort)X);
            builder.WriteUInt16((ushort)Y);
            builder.WriteUInt16(0); // 方向，NPC默认朝下
            
            // NPC特征数据
            byte[] featureData = new byte[12];
            BitConverter.GetBytes(ImageIndex).CopyTo(featureData, 0);
            BitConverter.GetBytes(0).CopyTo(featureData, 4); // 状态
            BitConverter.GetBytes(0).CopyTo(featureData, 8); // 健康
            
            builder.WriteBytes(featureData);
            builder.WriteString(Name);
            
            msg = builder.Build();
            return true;
        }
    }

    /// <summary>
    /// NPC类型
    /// </summary>
    public enum NpcType
    {
        Normal = 0,      // 普通NPC
        Merchant = 1,    // 商人
        Warehouse = 2,   // 仓库
        Quest = 3,       // 任务
        Teleporter = 4,  // 传送
        Repair = 5,      // 修理
        Script = 6       // 脚本NPC
    }

    /// <summary>
    /// NPC管理器
    /// </summary>
    public class NpcManager
    {
        private static NpcManager? _instance;
        public static NpcManager Instance => _instance ??= new NpcManager();

        private readonly Dictionary<int, Npc> _npcs = new();
        private readonly object _lock = new();

        private NpcManager()
        {
        }

        /// <summary>
        /// 初始化默认NPC
        /// </summary>
        public void Initialize()
        {
            CreateDefaultNpcs();
        }

        private void CreateDefaultNpcs()
        {
            // 创建一些测试NPC
            
            // 比奇城商人
            var merchant1 = new Npc(1001, "武器店老板", NpcType.Merchant)
            {
                CanTrade = true
            };
            AddShopItems(merchant1, ItemType.Weapon);
            AddNpc(merchant1);

            // 比奇城药店
            var merchant2 = new Npc(1002, "药店老板", NpcType.Merchant)
            {
                CanTrade = true
            };
            AddShopItems(merchant2, ItemType.Potion);
            AddNpc(merchant2);

            // 仓库管理员
            var warehouse = new Npc(1003, "仓库管理员", NpcType.Warehouse)
            {
                CanStore = true
            };
            AddNpc(warehouse);

            // 修理工
            var repair = new Npc(1004, "铁匠", NpcType.Repair)
            {
                CanRepair = true
            };
            AddNpc(repair);

            // 传送员
            var teleporter = new Npc(1005, "传送员", NpcType.Teleporter);
            AddNpc(teleporter);

            LogManager.Default.Info($"已创建 {_npcs.Count} 个NPC");
        }

        /// <summary>
        /// 为商人添加商品
        /// </summary>
        private void AddShopItems(Npc npc, ItemType type)
        {
            var items = ItemManager.Instance.GetItemsByType(type);
            foreach (var itemDef in items.Take(20)) // 最多20个商品
            {
                var item = ItemManager.Instance.CreateItem(itemDef.ItemId);
                if (item != null)
                {
                    npc.AddShopItem(item);
                }
            }
        }

        /// <summary>
        /// 添加NPC
        /// </summary>
        public void AddNpc(Npc npc)
        {
            lock (_lock)
            {
                _npcs[npc.NpcId] = npc;
            }
        }

        /// <summary>
        /// 获取NPC
        /// </summary>
        public Npc? GetNpc(int npcId)
        {
            lock (_lock)
            {
                return _npcs.TryGetValue(npcId, out var npc) ? npc : null;
            }
        }

        /// <summary>
        /// 在地图放置NPC
        /// </summary>
        public bool PlaceNpc(int npcId, LogicMap map, ushort x, ushort y)
        {
            var npc = GetNpc(npcId);
            if (npc == null)
                return false;

            return map.AddObject(npc, x, y);
        }

        /// <summary>
        /// 创建自定义NPC
        /// </summary>
        public Npc CreateNpc(int npcId, string name, NpcType type)
        {
            var npc = new Npc(npcId, name, type);
            AddNpc(npc);
            return npc;
        }
    }
}
