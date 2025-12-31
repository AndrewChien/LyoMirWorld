using System;
using System.Collections.Generic;
using System.Linq;
using MirCommon;
using MirCommon.Utils;

// 类型别名：Player = HumanPlayer
using Player = GameServer.HumanPlayer;

namespace GameServer
{
    /// <summary>
    /// 交易状态
    /// </summary>
    public enum TradeState
    {
        PuttingItems = 0,      // 放置物品阶段
        WaitingForOther = 1,   // 等待对方确认
        Completed = 2,         // 交易完成
        Cancelled = 3          // 交易取消
    }

    /// <summary>
    /// 交易结束类型
    /// </summary>
    public enum TradeEndType
    {
        Cancel = 0,            // 取消交易
        Confirm = 1            // 确认交易
    }

    /// <summary>
    /// 交易方信息
    /// </summary>
    public class TradeSide
    {
        public Player Player { get; set; }
        public List<ItemInstance> Items { get; set; } = new List<ItemInstance>(10);
        public uint Gold { get; set; }
        public uint Yuanbao { get; set; }
        public bool Ready { get; set; }

        public TradeSide(Player player)
        {
            Player = player;
            // 初始化10个物品槽位
            for (int i = 0; i < 10; i++)
            {
                Items.Add(null);
            }
        }

        public int GetItemCount()
        {
            return Items.Count(item => !IsDefaultItem(item));
        }

        public bool AddItem(ItemInstance item)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (IsDefaultItem(Items[i]))
                {
                    Items[i] = item;
                    return true;
                }
            }
            return false;
        }

        public bool RemoveItem(int index)
        {
            if (index >= 0 && index < Items.Count && !IsDefaultItem(Items[index]))
            {
                Items[index] = null;
                return true;
            }
            return false;
        }

        public void ClearItems()
        {
            for (int i = 0; i < Items.Count; i++)
            {
                Items[i] = null;
            }
        }

        private bool IsDefaultItem(ItemInstance item)
        {
            return item == null || item.InstanceId == 0;
        }

        private string GetItemName(ItemInstance item)
        {
            return item?.Definition?.Name ?? "";
        }

        private uint GetItemId(ItemInstance item)
        {
            return (uint)(item?.InstanceId ?? 0);
        }
    }

    /// <summary>
    /// 交易对象
    /// </summary>
    public class TradeObject
    {
        private TradeSide[] _sides = new TradeSide[2];
        private TradeState _state = TradeState.PuttingItems;
        private string _errorMessage = "交易成功!";
        private DateTime _startTime;
        private const int TRADE_TIMEOUT_SECONDS = 60; // 交易超时时间（秒）

        public TradeObject(Player player1, Player player2)
        {
            _sides[0] = new TradeSide(player1);
            _sides[1] = new TradeSide(player2);
            _state = TradeState.PuttingItems;
            _startTime = DateTime.Now;
        }

        public TradeState State => _state;
        public string ErrorMessage => _errorMessage;

        internal TradeSide GetSide(Player player)
        {
            if (_sides[0].Player == player) return _sides[0];
            if (_sides[1].Player == player) return _sides[1];
            return null;
        }

        internal TradeSide GetOtherSide(Player player)
        {
            if (_sides[0].Player == player) return _sides[1];
            if (_sides[1].Player == player) return _sides[0];
            return null;
        }

        private bool IsDefaultItem(Item item)
        {
            return item.dwMakeIndex == 0 && item.baseitem.btNameLength == 0;
        }

        private string GetItemName(Item item)
        {
            return item.baseitem.szName ?? "";
        }

        private uint GetItemId(Item item)
        {
            return item.dwMakeIndex;
        }

        /// <summary>
        /// 开始交易
        /// </summary>
        public bool Begin()
        {
            var player1 = _sides[0].Player;
            var player2 = _sides[1].Player;

            if (player1 == null || player2 == null)
                return false;

            // 检查距离
            if (!IsPlayersInRange(player1, player2))
            {
                _errorMessage = "交易双方距离太远";
                return false;
            }

            // 检查玩家状态
            if (!CanPlayerTrade(player1) || !CanPlayerTrade(player2))
            {
                _errorMessage = "玩家状态不允许交易";
                return false;
            }

            // 设置交易对象
            player1.CurrentTrade = this;
            player2.CurrentTrade = this;

            // 发送交易开始消息
            player1.SendTradeStart(player2.Name);
            player2.SendTradeStart(player1.Name);

            LogManager.Default.Info($"{player1.Name} 和 {player2.Name} 开始交易");
            return true;
        }

        /// <summary>
        /// 检查玩家是否在交易范围内
        /// </summary>
        private bool IsPlayersInRange(Player player1, Player player2)
        {
            if (player1.CurrentMap != player2.CurrentMap)
                return false;

            int dx = Math.Abs(player1.X - player2.X);
            int dy = Math.Abs(player1.Y - player2.Y);
            return dx <= 5 && dy <= 5; // 5格范围内
        }

        /// <summary>
        /// 检查玩家是否可以交易
        /// </summary>
        private bool CanPlayerTrade(Player player)
        {
            // 不能死亡、不能战斗、不能摆摊等
            if (player.CurrentHP <= 0)
                return false;

            // 检查是否在战斗中
            if (player.IsInCombat())
                return false;

            // 检查是否在摆摊
            if (player.IsInPrivateShop())
                return false;

            return true;
        }

        /// <summary>
        /// 放入物品
        /// </summary>
        public bool PutItem(Player player, ItemInstance item)
        {
            var side = GetSide(player);
            var otherSide = GetOtherSide(player);

            if (side == null || otherSide == null)
            {
                _errorMessage = "交易方不存在";
                return false;
            }

            if (_state != TradeState.PuttingItems)
            {
                _errorMessage = "无法放入物品，对方已经按下交易按钮！";
                player.SendTradeError();
                return false;
            }

            // 检查物品是否有效
            if (item == null || item.InstanceId == 0)
            {
                _errorMessage = "无效的物品";
                return false;
            }

            // 检查物品是否可以交易
            if (!CanItemBeTraded(item))
            {
                _errorMessage = "该物品不能交易";
                return false;
            }

            // 检查玩家是否拥有该物品
            if (!PlayerHasItem(player, item))
            {
                _errorMessage = "您没有这个物品";
                return false;
            }

            // 添加到交易栏
            if (!side.AddItem(item))
            {
                _errorMessage = "交易栏已满，无法放入新物品!";
                player.SendTradeError();
                return false;
            }

            // 从玩家背包移除物品（临时）
            RemoveItemFromPlayer(player, item);

            // 通知对方
            otherSide.Player.SendTradeOtherAddItem(player, item);

            LogManager.Default.Debug($"{player.Name} 放入物品: {item?.Definition?.Name ?? ""}");
            return true;
        }

        /// <summary>
        /// 检查物品是否可以交易
        /// </summary>
        private bool CanItemBeTraded(ItemInstance item)
        {
            // 这里需要根据实际物品属性检查
            return true; 
        }

        /// <summary>
        /// 检查玩家是否拥有该物品
        /// </summary>
        private bool PlayerHasItem(Player player, ItemInstance item)
        {
            // 检查背包和装备栏
            // 这里需要根据实际物品系统实现
            return true; 
        }

        /// <summary>
        /// 从玩家背包移除物品
        /// </summary>
        private void RemoveItemFromPlayer(Player player, ItemInstance item)
        {
            // 从背包或装备栏移除物品
            // 这里需要根据实际物品系统实现
        }

        /// <summary>
        /// 放入货币
        /// </summary>
        public bool PutMoney(Player player, MoneyType type, uint amount)
        {
            var side = GetSide(player);
            var otherSide = GetOtherSide(player);

            if (side == null || otherSide == null)
            {
                _errorMessage = "交易方不存在";
                return false;
            }

            // 检查玩家是否有足够的货币
            if (!PlayerHasEnoughMoney(player, type, amount))
            {
                _errorMessage = type == MoneyType.Gold ? "金币不足" : "元宝不足";
                return false;
            }

            if (type == MoneyType.Gold)
            {
                side.Gold = amount;
                // 从玩家扣除金币（临时）
                player.TakeGold(amount);
            }
            else
            {
                side.Yuanbao = amount;
                // 从玩家扣除元宝
                if (!player.TakeYuanbao(amount))
                {
                    _errorMessage = "元宝扣除失败";
                    return false;
                }
            }

            // 通知对方
            otherSide.Player.SendTradeOtherAddMoney(player, type, amount);

            LogManager.Default.Debug($"{player.Name} 放入{(type == MoneyType.Gold ? "金币" : "元宝")}: {amount}");
            return true;
        }

        /// <summary>
        /// 检查玩家是否有足够的货币
        /// </summary>
        private bool PlayerHasEnoughMoney(Player player, MoneyType type, uint amount)
        {
            if (type == MoneyType.Gold)
                return player.Gold >= amount;
            else
                return player.CanTakeYuanbao(amount); // 检查是否有足够的元宝
        }

        /// <summary>
        /// 结束交易
        /// </summary>
        public bool End(Player player, TradeEndType endType)
        {
            var side = GetSide(player);
            var otherSide = GetOtherSide(player);

            if (side == null || otherSide == null)
            {
                _errorMessage = "您现在不在交易状态！";
                return false;
            }

            // 检查交易是否超时
            if ((DateTime.Now - _startTime).TotalSeconds > TRADE_TIMEOUT_SECONDS)
            {
                _errorMessage = "交易超时";
                DoCancel(side, otherSide);
                return true;
            }

            bool tradeEnded = false;

            switch (endType)
            {
                case TradeEndType.Cancel:
                    // 取消交易
                    otherSide.Player.SaySystem("对方取消交易！");
                    DoCancel(side, otherSide);
                    tradeEnded = true;
                    break;

                case TradeEndType.Confirm:
                    if (side.Ready)
                    {
                        side.Player.SaySystemTrade("请让对方按下交易按钮");
                        otherSide.Player.SaySystemTrade("对方再次要求你确认交易，按下[交易]键确认");
                    }
                    else
                    {
                        side.Ready = true;
                        if (otherSide.Ready)
                        {
                            // 进行交换
                            if (!DoExchange(side, otherSide))
                            {
                                DoCancel(side, otherSide);
                            }
                            tradeEnded = true;
                        }
                        else
                        {
                            _state = TradeState.WaitingForOther;
                            side.Player.SaySystemTrade("请让对方按下交易按钮");
                            otherSide.Player.SaySystemTrade("对方再次要求你确认交易，按下[交易]键确认");
                        }
                    }
                    break;
            }

            if (tradeEnded)
            {
                // 清理交易对象
                side.Player.CurrentTrade = null;
                otherSide.Player.CurrentTrade = null;
                
                // 从交易管理器移除
                TradeManager.Instance.EndTrade(this);
            }

            return true;
        }

        /// <summary>
        /// 执行交易
        /// </summary>
        private bool DoExchange(TradeSide actionSide, TradeSide otherSide)
        {
            // 检查背包空间
            int itemCount1 = actionSide.GetItemCount();
            int itemCount2 = otherSide.GetItemCount();

            if (itemCount1 > otherSide.Player.Inventory.MaxSlots - otherSide.Player.Inventory.GetUsedSlots())
            {
                actionSide.Player.SaySystem("对方的背包无法容纳这么多物品！");
                return false;
            }

            if (itemCount2 > actionSide.Player.Inventory.MaxSlots - actionSide.Player.Inventory.GetUsedSlots())
            {
                otherSide.Player.SaySystem("对方的背包无法容纳这么多物品！");
                return false;
            }

            // 检查货币空间
            if (actionSide.Gold > 0)
            {
                if (!otherSide.Player.CanAddGold(actionSide.Gold))
                {
                    actionSide.Player.SaySystem("钱币太多，对方拿不下！");
                    return false;
                }
            }

            if (actionSide.Yuanbao > 0)
            {
                if (!otherSide.Player.CanAddYuanbao(actionSide.Yuanbao))
                {
                    actionSide.Player.SaySystem("元宝太多，对方拿不下！");
                    return false;
                }
            }

            if (otherSide.Gold > 0)
            {
                if (!actionSide.Player.CanAddGold(otherSide.Gold))
                {
                    otherSide.Player.SaySystem("钱币太多，对方拿不下！");
                    return false;
                }
            }

            if (otherSide.Yuanbao > 0)
            {
                if (!actionSide.Player.CanAddYuanbao(otherSide.Yuanbao))
                {
                    otherSide.Player.SaySystem("元宝太多，对方拿不下！");
                    return false;
                }
            }

            // 执行交换
            // 交换物品
            for (int i = 0; i < 10; i++)
            {
                if (actionSide.Items[i] != null && actionSide.Items[i].InstanceId != 0)
                {
                    // 添加物品到对方背包
                    if (!otherSide.Player.Inventory.AddItem(actionSide.Items[i]))
                    {
                        // 如果添加失败，取消交易
                        return false;
                    }
                }
                if (otherSide.Items[i] != null && otherSide.Items[i].InstanceId != 0)
                {
                    // 添加物品到己方背包
                    if (!actionSide.Player.Inventory.AddItem(otherSide.Items[i]))
                    {
                        // 如果添加失败，取消交易
                        return false;
                    }
                }
            }

            // 交换货币
            actionSide.Player.AddGold(otherSide.Gold);
            otherSide.Player.AddGold(actionSide.Gold);
            
            // 实现元宝交换
            if (actionSide.Yuanbao > 0)
            {
                if (!otherSide.Player.AddYuanbao(actionSide.Yuanbao))
                {
                    // 如果添加失败，取消交易
                    return false;
                }
            }
            
            if (otherSide.Yuanbao > 0)
            {
                if (!actionSide.Player.AddYuanbao(otherSide.Yuanbao))
                {
                    // 如果添加失败，取消交易
                    return false;
                }
            }

            // 发送交易完成消息
            actionSide.Player.SendTradeEnd();
            actionSide.Player.SaySystemTrade("交易成功");
            
            otherSide.Player.SendTradeEnd();
            otherSide.Player.SaySystemTrade("交易成功");

            _state = TradeState.Completed;
            LogManager.Default.Info($"{actionSide.Player.Name} 和 {otherSide.Player.Name} 交易成功");
            return true;
        }

        /// <summary>
        /// 取消交易
        /// </summary>
        private void DoCancel(TradeSide actionSide, TradeSide otherSide)
        {
            // 返还物品
            for (int i = 0; i < 10; i++)
            {
                if (actionSide.Items[i] != null && actionSide.Items[i].InstanceId != 0)
                {
                    // 返还物品到己方背包
                    actionSide.Player.Inventory.AddItem(actionSide.Items[i]);
                }
                if (otherSide.Items[i] != null && otherSide.Items[i].InstanceId != 0)
                {
                    // 返还物品到对方背包
                    otherSide.Player.Inventory.AddItem(otherSide.Items[i]);
                }
            }

            // 返还货币
            actionSide.Player.AddGold(actionSide.Gold);
            if (actionSide.Yuanbao > 0)
            {
                actionSide.Player.AddYuanbao(actionSide.Yuanbao);
            }
            
            otherSide.Player.AddGold(otherSide.Gold);
            if (otherSide.Yuanbao > 0)
            {
                otherSide.Player.AddYuanbao(otherSide.Yuanbao);
            }

            // 发送交易取消消息
            actionSide.Player.SendTradeCancelled();
            actionSide.Player.SaySystemTrade("交易取消");
            
            otherSide.Player.SendTradeCancelled();
            otherSide.Player.SaySystemTrade("交易取消");

            _state = TradeState.Cancelled;
            LogManager.Default.Info($"{actionSide.Player.Name} 和 {otherSide.Player.Name} 交易取消");
        }

        /// <summary>
        /// 更新交易状态（检查超时等）
        /// </summary>
        public void Update()
        {
            // 检查交易是否超时
            if (_state == TradeState.PuttingItems || _state == TradeState.WaitingForOther)
            {
                if ((DateTime.Now - _startTime).TotalSeconds > TRADE_TIMEOUT_SECONDS)
                {
                    // 交易超时，自动取消
                    var side1 = _sides[0];
                    var side2 = _sides[1];
                    DoCancel(side1, side2);
                    
                    // 清理交易对象
                    side1.Player.CurrentTrade = null;
                    side2.Player.CurrentTrade = null;
                    
                    // 从交易管理器移除
                    TradeManager.Instance.EndTrade(this);
                }
            }
        }
    }

    /// <summary>
    /// 交易管理器
    /// </summary>
    public class TradeManager
    {
        private static TradeManager _instance;
        public static TradeManager Instance => _instance ??= new TradeManager();

        private readonly List<TradeObject> _activeTrades = new List<TradeObject>();
        private readonly object _lock = new object();

        private TradeManager() { }

        /// <summary>
        /// 开始交易
        /// </summary>
        public bool StartTrade(Player player1, Player player2)
        {
            lock (_lock)
            {
                // 检查是否已经在交易中
                if (player1.CurrentTrade != null || player2.CurrentTrade != null)
                {
                    return false;
                }

                // 创建交易对象
                var trade = new TradeObject(player1, player2);
                if (trade.Begin())
                {
                    _activeTrades.Add(trade);
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// 结束交易
        /// </summary>
        public void EndTrade(TradeObject trade)
        {
            lock (_lock)
            {
                _activeTrades.Remove(trade);
            }
        }

        /// <summary>
        /// 获取玩家的交易对象
        /// </summary>
        public TradeObject GetPlayerTrade(Player player)
        {
            lock (_lock)
            {
                return _activeTrades.FirstOrDefault(t => 
                    t.GetSide(player) != null || t.GetOtherSide(player) != null);
            }
        }
    }

    /// <summary>
    /// 交易相关的玩家扩展方法
    /// </summary>
    public static class TradePlayerExtensions
    {
        public static void SendTradeStart(this Player player, string otherPlayerName)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(player.ObjectId);
            builder.WriteUInt16(0x290); // SM_TRADESTART
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteString(otherPlayerName);
            
            player.SendMessage(builder.Build());
            LogManager.Default.Debug($"{player.Name} 收到交易开始消息，对方: {otherPlayerName}");
        }

        public static void SendTradeError(this Player player)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(player.ObjectId);
            builder.WriteUInt16(0x291); // SM_TRADEERROR
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            
            player.SendMessage(builder.Build());
            LogManager.Default.Debug($"{player.Name} 收到交易错误消息");
        }

        public static void SendTradeOtherAddItem(this Player player, Player otherPlayer, ItemInstance item)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(player.ObjectId);
            builder.WriteUInt16(0x292); // SM_TRADEOTHERADDITEM
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(otherPlayer.ObjectId);
            builder.WriteUInt64((ulong)(item?.InstanceId ?? 0));
            builder.WriteString(item?.Definition?.Name ?? "");
            
            player.SendMessage(builder.Build());
            LogManager.Default.Debug($"{player.Name} 收到对方 {otherPlayer.Name} 放入物品: {item?.Definition?.Name ?? ""}");
        }

        public static void SendTradeOtherAddMoney(this Player player, Player otherPlayer, MoneyType type, uint amount)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(player.ObjectId);
            builder.WriteUInt16(0x293); // SM_TRADEOTHERADDMONEY
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(otherPlayer.ObjectId);
            builder.WriteUInt16((ushort)type);
            builder.WriteUInt32(amount);
            
            player.SendMessage(builder.Build());
            LogManager.Default.Debug($"{player.Name} 收到对方 {otherPlayer.Name} 放入{(type == MoneyType.Gold ? "金币" : "元宝")}: {amount}");
        }

        public static void SendTradeEnd(this Player player)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(player.ObjectId);
            builder.WriteUInt16(0x294); // SM_TRADEEND
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            
            player.SendMessage(builder.Build());
            LogManager.Default.Debug($"{player.Name} 收到交易结束消息");
        }

        public static void SendTradeCancelled(this Player player)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(player.ObjectId);
            builder.WriteUInt16(0x295); // SM_TRADECANCELLED
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            
            player.SendMessage(builder.Build());
            LogManager.Default.Debug($"{player.Name} 收到交易取消消息");
        }

        public static void SaySystemTrade(this Player player, string message)
        {
            player.SaySystem($"[交易] {message}");
        }
    }
}
