using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Linq;
using MirCommon;
using MirCommon.Network;
using MirCommon.Utils;

namespace GameServer
{
    public partial class HumanPlayer : AliveObject
    {

        #region 经验和升级

        /// <summary>
        /// 增加经验
        /// </summary>
        public void AddExp(uint exp, bool noBonus = false, uint killerId = 0)
        {
            // 1. 组队经验分配
            // 2. 经验倍率计算（地图倍率、全局倍率、技能倍率）
            // 3. 双倍经验活动
            // 4. 宠物经验分配

            uint finalExp = exp;

            if (!noBonus)
            {
                // 经验倍率计算
                float expFactor = 1.0f; // 基础倍率

                // 全局经验倍率（从服务器配置读取）
                // 注意：这里使用Program.cs中的GameWorld类
                float globalExpFactor = 1.0f; 
                expFactor += globalExpFactor - 1.0f;

                // 地图经验倍率
                if (CurrentMap != null && CurrentMap is LogicMap logicMap)
                {
                    float mapExpFactor = logicMap.GetExpFactor();
                    expFactor += mapExpFactor - 1.0f;
                }

                // 技能经验倍率（如经验加成技能）
                if (_expMagic != null)
                {
                    // 检查技能是否生效
                    // 注意：SkillSystem.cs中的PlayerSkill没有nAddPower属性
                    uint addExp = exp; // 额外经验
                    // 发送技能特效消息
                    SaySystem("经验加成技能生效");
                    TrainMagic(_expMagic);
                }

                // 双倍经验活动
                if (killerId > 0 && IsGodBlessEffective(GodBlessType.DoubleExp))
                {
                    expFactor += 1.0f;
                    // 添加特效过程 - 使用现有的ProcessType枚举值
                    // 注意：需要先定义ProcessType.GodBless
                    // AddProcess(ProcessType.GodBless, killerId, 8);
                }

                // 应用经验倍率
                finalExp = (uint)Math.Round(expFactor * exp);

                // 个人经验倍率（如VIP等）
                finalExp = (uint)Math.Round(GetExpFactor() * finalExp);
            }

            Exp += finalExp;

            // 检查升级
            uint requiredExp = GetRequiredExp();
            while (Exp >= requiredExp && Level < 255)
            {
                Exp -= requiredExp;
                LevelUp();
                requiredExp = GetRequiredExp();
            }

            // 检查并更新称号
            CheckAndUpgradeTitle();

            // 通知客户端
            SendExpChanged(finalExp);

            // 宠物经验分配
            uint petExp = finalExp / 10;
            if (petExp == 0) petExp = 1;
            // PetSystem.DistributePetExp(petExp);
        }

        /// <summary>
        /// 获取个人经验倍率
        /// </summary>
        private float GetExpFactor()
        {
            // 这里应该从玩家状态、VIP等获取经验倍率
            return 1.0f;
        }

        /// <summary>
        /// 检查是否双倍经验生效
        /// </summary>
        private bool IsGodBlessEffective(GodBlessType type)
        {
            // 这里应该检查玩家是否有对应的神佑状态
            return false;
        }

        /// <summary>
        /// 检查并更新称号
        /// </summary>
        private void CheckAndUpgradeTitle()
        {
            // 根据经验、等级等条件更新称号
            // 这里需要实现称号系统

            // 检查是否需要更新称号
            int newTitleIndex = GetTitleIndexByExp(Exp);
            if (newTitleIndex != _currentTitleIndex)
            {
                _currentTitleIndex = newTitleIndex;
                _currentTitle = GetTitleByIndex(newTitleIndex);
                // 发送称号更新消息
                SendTitleChanged();
            }
        }

        /// <summary>
        /// 根据经验获取称号索引
        /// </summary>
        private int GetTitleIndexByExp(uint exp)
        {
            // 这里应该从配置读取称号经验要求
            return (int)(exp / 1000000);
        }

        /// <summary>
        /// 根据索引获取称号
        /// </summary>
        private string GetTitleByIndex(int index)
        {
            // 这里应该从配置读取称号名称
            string[] titles = {
                "新手", "学徒", "见习", "初级", "中级",
                "高级", "精英", "大师", "宗师", "传奇"
            };

            if (index < 0) return titles[0];
            if (index >= titles.Length) return titles[titles.Length - 1];
            return titles[index];
        }

        /// <summary>
        /// 训练技能
        /// </summary>
        private void TrainMagic(PlayerSkill skill)
        {
            if (skill == null) return;

            skill.UseCount++;

            // 每使用一定次数增加技能经验
            if (skill.UseCount % 10 == 0)
            {
                // 检查技能升级
                if (skill.CanLevelUp())
                {
                    skill.LevelUp();
                    // 发送技能升级消息
                    SendSkillUpgraded(skill);
                }
            }
        }

        /// <summary>
        /// 发送称号变化消息
        /// </summary>
        private void SendTitleChanged()
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x283); // SM_TITLECHANGED
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteString(_currentTitle);

            SendMessage(builder.Build());
        }

        /// <summary>
        /// 发送技能升级消息
        /// </summary>
        private void SendSkillUpgraded(PlayerSkill skill)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x284); // SM_SKILLUPGRADED
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32((uint)skill.Definition.SkillId);
            builder.WriteUInt16((ushort)skill.Level);

            SendMessage(builder.Build());
        }

        /// <summary>
        /// 升级
        /// </summary>
        private void LevelUp()
        {
            Level++;

            // 根据职业增加属性
            switch (Job)
            {
                case 0: // 战士
                    MaxHP += 30;
                    MaxMP += 5;
                    Stats.MinDC += 2;
                    Stats.MaxDC += 3;
                    Stats.MinAC += 1;
                    Stats.MaxAC += 2;
                    break;
                case 1: // 法师
                    MaxHP += 15;
                    MaxMP += 20;
                    Stats.MinMC += 2;
                    Stats.MaxMC += 3;
                    break;
                case 2: // 道士
                    MaxHP += 20;
                    MaxMP += 15;
                    Stats.MinSC += 2;
                    Stats.MaxSC += 3;
                    Stats.MinAC += 1;
                    Stats.MaxAC += 1;
                    break;
            }

            // 回满血蓝
            CurrentHP = MaxHP;
            CurrentMP = MaxMP;

            // 通知升级
            Say($"恭喜！你升到了 {Level} 级！");
            SendLevelUp();

            LogManager.Default.Info($"{Name} 升级到 {Level} 级");
        }

        /// <summary>
        /// 获取升级所需经验
        /// </summary>
        private uint GetRequiredExp()
        {
            // 实际应该从HumanDataDesc配置中读取
            if (Level <= 1) return 100;
            if (Level <= 10) return (uint)(Level * 100);
            if (Level <= 20) return (uint)(Level * 200);
            if (Level <= 30) return (uint)(Level * 400);
            if (Level <= 40) return (uint)(Level * 800);
            if (Level <= 50) return (uint)(Level * 1600);
            if (Level <= 60) return (uint)(Level * 3200);
            if (Level <= 70) return (uint)(Level * 6400);
            if (Level <= 80) return (uint)(Level * 12800);
            if (Level <= 90) return (uint)(Level * 25600);
            return (uint)(Level * 51200);
        }

        private void SendExpChanged(uint gainedExp)
        {
            // 发送经验变化消息
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x280); // SM_EXPCHANGED
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(Exp);
            builder.WriteUInt32(gainedExp);

            SendMessage(builder.Build());
        }

        private void SendLevelUp()
        {
            // 发送升级消息
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x281); // SM_LEVELUP
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16((ushort)Level);

            SendMessage(builder.Build());
        }

        #endregion

        #region 金钱管理

        /// <summary>
        /// 增加金币
        /// </summary>
        public bool AddGold(uint amount)
        {
            if (amount > uint.MaxValue - Gold)
                return false;

            Gold += amount;
            SendGoldChanged();
            return true;
        }

        /// <summary>
        /// 扣除金币
        /// </summary>
        public bool TakeGold(uint amount)
        {
            if (Gold < amount)
                return false;

            Gold -= amount;
            SendGoldChanged();
            return true;
        }

        /// <summary>
        /// 检查是否可以增加金币
        /// </summary>
        public bool CanAddGold(uint amount)
        {
            return amount <= uint.MaxValue - Gold;
        }

        /// <summary>
        /// 增加元宝
        /// </summary>
        public bool AddYuanbao(uint amount)
        {
            if (amount > uint.MaxValue - Yuanbao)
                return false;

            Yuanbao += amount;
            SendYuanbaoChanged();
            return true;
        }

        /// <summary>
        /// 扣除元宝
        /// </summary>
        public bool TakeYuanbao(uint amount)
        {
            if (Yuanbao < amount)
                return false;

            Yuanbao -= amount;
            SendYuanbaoChanged();
            return true;
        }

        /// <summary>
        /// 检查是否可以增加元宝
        /// </summary>
        public bool CanAddYuanbao(uint amount)
        {
            return amount <= uint.MaxValue - Yuanbao;
        }

        /// <summary>
        /// 检查是否可以扣除元宝
        /// </summary>
        public bool CanTakeYuanbao(uint amount)
        {
            return Yuanbao >= amount;
        }

        private void SendGoldChanged()
        {
            // 发送金币变化消息
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x282); // SM_GOLDCHANGED
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(Gold);

            SendMessage(builder.Build());
        }

        /// <summary>
        /// 发送元宝变化消息
        /// </summary>
        private void SendYuanbaoChanged()
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x283); // SM_YUANBAOCHANGED
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(Yuanbao);

            SendMessage(builder.Build());
        }

        #endregion

        #region 物品管理

        /// <summary>
        /// 拾取物品
        /// </summary>
        public bool PickupItem(MapItem mapItem)
        {
            if (mapItem == null || mapItem.CurrentMap != CurrentMap)
                return false;

            // 检查拾取权限
            if (!mapItem.CanPickup(ObjectId))
            {
                Say("这个物品还不能拾取");
                return false;
            }

            // 添加到背包
            if (!Inventory.AddItem(mapItem.Item))
            {
                Say("背包已满");
                return false;
            }

            // 从地图移除
            CurrentMap?.RemoveObject(mapItem);

            Say($"你获得了 {mapItem.Item.Definition.Name}");
            OnPickupItem(mapItem.Item);
            return true;
        }

        /// <summary>
        /// 丢弃物品
        /// </summary>
        public bool DropItem(int slot)
        {
            var item = Inventory.GetItem(slot);
            if (item == null || CurrentMap == null)
                return false;

            if (!item.Definition.CanDrop)
            {
                Say("这个物品不能丢弃");
                return false;
            }

            // 从背包移除
            if (!Inventory.RemoveItem(slot, 1))
                return false;

            // 创建地图物品
            var mapItem = new MapItem(item)
            {
                OwnerPlayerId = ObjectId
            };

            // 放置在玩家脚下
            CurrentMap.AddObject(mapItem, X, Y);

            OnDropItem(item);
            return true;
        }

        /// <summary>
        /// 使用物品
        /// </summary>
        public bool UseItem(int slot)
        {
            var item = Inventory.GetItem(slot);
            if (item == null)
                return false;

            switch (item.Definition.Type)
            {
                case ItemType.Potion:
                    return UsePotion(item);
                case ItemType.Book:
                    return LearnSkill(item);
                default:
                    Say("这个物品不能使用");
                    return false;
            }
        }

        /// <summary>
        /// 使用药水
        /// </summary>
        private bool UsePotion(ItemInstance item)
        {
            if (item.Definition.HP > 0)
            {
                Heal(item.Definition.HP);
            }

            if (item.Definition.MP > 0)
            {
                RestoreMP(item.Definition.MP);
            }

            // 消耗物品
            var slot = Inventory.GetAllItems().FirstOrDefault(kvp => kvp.Value == item).Key;
            if (slot >= 0)
            {
                Inventory.RemoveItem(slot, 1);
            }

            return true;
        }

        /// <summary>
        /// 学习技能
        /// </summary>
        private bool LearnSkill(ItemInstance item)
        {
            // 检查物品是否为技能书
            if (item.Definition.Type != ItemType.Book)
            {
                Say("这不是技能书");
                return false;
            }

            // 从物品中获取技能ID
            // 注意：ItemDefinition可能没有SkillId属性
            // 这里需要根据实际ItemDefinition类来调整
            int skillId = 0; 
            if (skillId == 0)
            {
                Say("这本技能书无法使用");
                return false;
            }

            // 检查是否已学习该技能
            if (SkillBook.HasSkill(skillId))
            {
                Say("你已经学会了这个技能");
                return false;
            }

            // 学习技能
            bool success = SkillExecutor.Instance.LearnSkill(this, skillId);
            if (success)
            {
                // 消耗技能书
                var slot = Inventory.GetAllItems().FirstOrDefault(kvp => kvp.Value == item).Key;
                if (slot >= 0)
                {
                    Inventory.RemoveItem(slot, 1);
                }
                Say($"你学会了 {item.Definition.Name}！");
            }
            else
            {
                Say("学习技能失败");
            }

            return success;
        }

        /// <summary>
        /// 拾取物品后的处理
        /// </summary>
        private void OnPickupItem(ItemInstance item)
        {
            // 可以在这里添加拾取物品后的额外处理
            // 如任务更新、成就等
        }

        /// <summary>
        /// 丢弃物品后的处理
        /// </summary>
        private void OnDropItem(ItemInstance item)
        {
            // 可以在这里添加丢弃物品后的额外处理
        }

        #endregion

        #region 技能系统

        /// <summary>
        /// 学习新技能
        /// </summary>
        public bool LearnNewSkill(uint skillId)
        {
            var definition = SkillManager.Instance.GetDefinition((int)skillId);
            if (definition == null)
                return false;

            return SkillBook.LearnSkill(definition);
        }

        /// <summary>
        /// 使用技能
        /// </summary>
        public bool UseSkill(uint skillId, uint targetId = 0)
        {
            var skill = SkillBook.GetSkill((int)skillId);
            if (skill == null)
            {
                Say("未学习此技能");
                return false;
            }

            // 获取目标
            ICombatEntity? target = null;
            if (targetId > 0)
            {
                target = CurrentMap?.GetObject(targetId) as ICombatEntity;
                if (target == null)
                {
                    Say("目标不存在");
                    return false;
                }
            }

            // 使用技能
            var result = SkillExecutor.Instance.UseSkill(this, (int)skillId, target);
            if (!result.Success)
            {
                Say(result.Message);
                return false;
            }

            return true;
        }

        #endregion

        #region 交易系统

        /// <summary>
        /// 发起交易
        /// </summary>
        public bool StartTrade(uint targetPlayerId)
        {
            if (_tradingWithPlayerId != 0)
            {
                Say("你已经在交易中");
                return false;
            }

            // 通过玩家管理器获取目标玩家
            var targetPlayer = HumanPlayerMgr.Instance.FindById(targetPlayerId);
            if (targetPlayer == null)
            {
                Say("目标玩家不存在");
                return false;
            }

            _tradingWithPlayerId = targetPlayerId;
            CurrentTrade = new TradeObject(this, targetPlayer);
            return true;
        }

        /// <summary>
        /// 添加交易物品
        /// </summary>
        public bool AddTradeItem(int slot, uint count)
        {
            if (CurrentTrade == null)
                return false;

            var item = Inventory.GetItem(slot);
            if (item == null)
                return false;

            // 创建Item对象（需要根据实际Item结构）
            var tradeItem = new ItemInstance(new ItemDefinition(0, "", ItemType.Other), 0);
            // TODO: 填充tradeItem的属性
            return CurrentTrade.PutItem(this, tradeItem);
        }

        /// <summary>
        /// 设置交易金币
        /// </summary>
        public bool SetTradeGold(uint amount)
        {
            if (CurrentTrade == null || Gold < amount)
                return false;

            return CurrentTrade.PutMoney(this, MoneyType.Gold, amount);
        }

        /// <summary>
        /// 确认交易
        /// </summary>
        public bool ConfirmTrade()
        {
            if (CurrentTrade == null)
                return false;

            return CurrentTrade.End(this, TradeEndType.Confirm);
        }

        /// <summary>
        /// 取消交易
        /// </summary>
        public void CancelTrade()
        {
            if (CurrentTrade != null)
            {
                CurrentTrade.End(this, TradeEndType.Cancel);
                CurrentTrade = null;
            }
            _tradingWithPlayerId = 0;
        }

        #endregion

        #region 挖矿/挖肉系统

        // 挖矿计时器和计数器
        private DateTime _lastMineTime = DateTime.MinValue;
        private uint _mineCounter = 0;

        // 挖肉计时器
        private DateTime _lastGetMeatTime = DateTime.MinValue;

        /// <summary>
        /// 挖矿
        /// </summary>
        public bool Mine(MineSpot mineSpot)
        {
            if (mineSpot == null || mineSpot.CurrentMap != CurrentMap)
                return false;

            // 检查距离
            if (!IsInRange(mineSpot, 1))
            {
                Say("距离太远");
                return false;
            }

            // 检查挖矿间隔
            if ((DateTime.Now - _lastMineTime).TotalSeconds < 3.0)
            {
                Say("挖矿太快了，请稍等");
                return false;
            }

            // 检查背包空间
            if (Inventory.GetUsedSlots() >= Inventory.MaxSlots)
            {
                Say("背包已满");
                return false;
            }

            // 执行挖矿动作
            StartAction(ActionType.Mining, mineSpot.ObjectId);

            // 更新挖矿时间
            _lastMineTime = DateTime.Now;
            _mineCounter++;

            return true;
        }

        /// <summary>
        /// 挖肉
        /// </summary>
        public bool GetMeat(MonsterCorpse corpse)
        {
            if (corpse == null || corpse.CurrentMap != CurrentMap)
                return false;

            // 检查距离
            if (!IsInRange(corpse, 1))
            {
                Say("距离太远");
                return false;
            }

            // 检查挖肉间隔
            if ((DateTime.Now - _lastGetMeatTime).TotalSeconds < 2.0)
            {
                Say("挖肉太快了，请稍等");
                return false;
            }

            // 检查背包空间（使用GetUsedSlots方法）
            if (Inventory.GetUsedSlots() >= Inventory.MaxSlots)
            {
                Say("背包已满");
                return false;
            }

            // 执行挖肉动作
            StartAction(ActionType.GetMeat, corpse.ObjectId);

            // 更新挖肉时间
            _lastGetMeatTime = DateTime.Now;

            return true;
        }

        /// <summary>
        /// 完成挖矿动作
        /// </summary>
        private void CompleteMining(uint mineSpotId)
        {
            // 获取矿点对象
            var mineSpot = CurrentMap?.GetObject(mineSpotId) as MineSpot;
            if (mineSpot == null)
                return;

            ItemDefinition definition;
            if (_mineCounter % 10 == 0)
            {
                // 每10次挖矿获得高级矿石
                definition = new ItemDefinition(4002, "金矿石", ItemType.Material);
                definition.SellPrice = 500; // 价值更高
            }
            else if (_mineCounter % 5 == 0)
            {
                // 每5次挖矿获得中级矿石
                definition = new ItemDefinition(4001, "银矿石", ItemType.Material);
                definition.SellPrice = 200;
            }
            else
            {
                // 普通矿石
                definition = new ItemDefinition(4000, "铁矿石", ItemType.Material);
                definition.SellPrice = 50;
            }

            // 创建物品实例（使用时间戳作为唯一ID）
            var item = new ItemInstance(definition, (long)DateTime.Now.Ticks);

            // 添加到背包
            if (Inventory.AddItem(item))
            {
                Say($"你挖到了一块{definition.Name}");

                // 增加挖矿技能经验（如果有挖矿技能）
                // 这里需要根据实际的技能系统来调整

                SaySystem("挖矿完成");
            }
            else
            {
                Say("背包已满");
            }

            // 矿点消失
            CurrentMap?.RemoveObject(mineSpot);
        }

        /// <summary>
        /// 完成挖肉动作
        /// </summary>
        private void CompleteGetMeat(uint corpseId)
        {
            // 获取尸体对象
            var corpse = CurrentMap?.GetObject(corpseId) as MonsterCorpse;
            if (corpse == null)
                return;

            Random rand = new Random();
            int meatCount = rand.Next(1, 4);

            ItemDefinition definition = new ItemDefinition(4003, "肉", ItemType.Material);
            definition.SellPrice = 10;

            bool success = false;
            for (int i = 0; i < meatCount; i++)
            {
                var item = new ItemInstance(definition, (long)(DateTime.Now.Ticks + i));
                if (Inventory.AddItem(item))
                {
                    success = true;
                }
                else
                {
                    break;
                }
            }

            if (success)
            {
                if (meatCount > 1)
                {
                    Say($"你获得了{meatCount}块肉");
                }
                else
                {
                    Say("你获得了一块肉");
                }

                // 增加挖肉技能经验（如果有相关技能）

                SaySystem("挖肉完成");
            }
            else
            {
                Say("背包已满");
            }

            // 尸体消失
            CurrentMap?.RemoveObject(corpse);
        }

        #endregion

        #region 移动和动作方法

        /// <summary>
        /// 行走到指定坐标
        /// </summary>
        public bool WalkXY(int x, int y)
        {
            // 检查坐标是否有效
            if (CurrentMap == null)
                return false;

            // 检查是否可以移动到目标位置
            if (!CurrentMap.CanMoveTo(x, y))
                return false;

            X = (ushort)x;
            Y = (ushort)y;
            return true;
        }

        /// <summary>
        /// 向指定方向行走
        /// </summary>
        public bool Walk(byte direction)
        {
            Direction = direction;
            // 根据方向计算新坐标
            int newX = X;
            int newY = Y;

            switch (direction)
            {
                case 0: newY--; break; // 上
                case 1: newX++; newY--; break; // 右上
                case 2: newX++; break; // 右
                case 3: newX++; newY++; break; // 右下
                case 4: newY++; break; // 下
                case 5: newX--; newY++; break; // 左下
                case 6: newX--; break; // 左
                case 7: newX--; newY--; break; // 左上
            }

            return WalkXY(newX, newY);
        }

        /// <summary>
        /// 跑到指定坐标
        /// </summary>
        public bool RunXY(int x, int y)
        {
            return WalkXY(x, y); 
        }

        /// <summary>
        /// 向指定方向跑
        /// </summary>
        public bool Run(byte direction)
        {
            return Walk(direction); 
        }

        /// <summary>
        /// 转向
        /// </summary>
        public bool Turn(byte direction)
        {
            Direction = direction;
            return true;
        }

        /// <summary>
        /// 攻击
        /// </summary>
        public bool Attack(byte direction)
        {
            Direction = direction;

            // 根据方向计算目标坐标
            int targetX = X;
            int targetY = Y;

            switch (direction)
            {
                case 0: targetY--; break; // 上
                case 1: targetX++; targetY--; break; // 右上
                case 2: targetX++; break; // 右
                case 3: targetX++; targetY++; break; // 右下
                case 4: targetY++; break; // 下
                case 5: targetX--; targetY++; break; // 左下
                case 6: targetX--; break; // 左
                case 7: targetX--; targetY--; break; // 左上
            }

            // 检查目标位置是否有可攻击的对象
            if (CurrentMap == null)
                return false;

            // 获取目标位置的对象
            var target = CurrentMap.GetObjectAt(targetX, targetY) as ICombatEntity;
            if (target == null)
            {
                Say("没有可攻击的目标");
                return false;
            }

            // 检查是否可以攻击该目标（根据攻击模式、行会关系等）
            if (!CanAttackTarget(target))
            {
                Say("不能攻击此目标");
                return false;
            }

            // 执行攻击
            PerformAttack(target);
            return true;
        }

        /// <summary>
        /// 检查是否可以攻击目标
        /// </summary>
        private bool CanAttackTarget(ICombatEntity target)
        {
            if (target == null || target == this)
                return false;

            // 检查目标类型
            if (target is HumanPlayer targetPlayer)
            {
                // 检查攻击模式
                // 这里需要根据实际的攻击模式系统来完善
                return true;
            }
            else if (target is MonsterEx)
            {
                // 可以攻击怪物
                return true;
            }
            else if (target is Npc)
            {
                // 检查是否可以攻击NPC（通常不能攻击NPC）
                return false;
            }

            return false;
        }

        /// <summary>
        /// 执行攻击
        /// </summary>
        private void PerformAttack(ICombatEntity target)
        {
            if (target == null || CurrentMap == null)
                return;

            // 使用CombatSystemManager执行战斗
            var combatResult = CombatSystemManager.Instance.ExecuteCombat(this, target, DamageType.Physics);

            if (!combatResult.Hit)
            {
                Say("攻击未命中");
                return;
            }

            // 消耗武器耐久度
            DamageWeaponDurability();

            // 检查PK（如果目标是玩家）
            if (target is HumanPlayer targetPlayer)
            {
                CheckPk(targetPlayer);
            }

            // 发送攻击消息
            SendAttackMessage(target, combatResult.Damage);

            // 如果目标死亡，处理经验奖励
            if (combatResult.TargetDied)
            {
                OnKillTarget(target);
            }
        }

        /// <summary>
        /// 消耗武器耐久度
        /// </summary>
        private void DamageWeaponDurability()
        {
            // 获取当前武器
            var weapon = Equipment.GetWeapon();
            if (weapon == null)
                return;

            // 实际应该根据武器类型、攻击次数、目标类型等计算

            // 检查武器是否有耐久度
            if (weapon.Durability > 0)
            {
                // 减少耐久度
                weapon.Durability--;

                // 如果耐久度为0，武器损坏
                if (weapon.Durability <= 0)
                {
                    Say("你的武器已经损坏！");
                    // 移除武器或设置为损坏状态
                    // 这里可以添加武器损坏的逻辑
                }

                // 发送武器耐久度更新消息
                // SendWeaponDurabilityUpdate(weapon);
            }
        }

        /// <summary>
        /// 检查PK
        /// </summary>
        private void CheckPk(HumanPlayer target)
        {
            if (target == null)
                return;

            // 检查是否在安全区
            if (InSafeArea() || target.InSafeArea())
                return;

            // 检查是否在战斗地图
            if (CurrentMap != null && CurrentMap is LogicMap logicMap && logicMap.IsFightMap())
                return;

            // 检查行会关系
            if (Guild != null && target.Guild != null)
            {
                // 检查是否是敌对行会
                if (Guild.IsKillGuild(target.Guild))
                    return;

                // 检查是否是联盟行会
                if (Guild.IsAllyGuild(target.Guild))
                    return;
            }

            // 增加PK值
            // 这里需要根据实际的PK系统来完善
            // AddPkPoint(1);

            // 发送PK值更新消息
            // SendPkValueUpdate();
        }

        /// <summary>
        /// 检查是否在安全区
        /// </summary>
        private bool InSafeArea()
        {
            if (CurrentMap == null)
                return false;

            // 检查当前位置是否在安全区内
            // 这里需要根据地图的安全区设置来检查
            return false;
        }

        /// <summary>
        /// 杀死目标后的处理
        /// </summary>
        private void OnKillTarget(ICombatEntity target)
        {
            if (target == null)
                return;

            // 计算经验值
            int exp = CombatSystemManager.Instance.CalculateExp(this, target);
            if (exp > 0)
            {
                AddExp((uint)exp, false, target.Id);
            }

            // 处理掉落物品
            // 这里需要根据目标的掉落系统来完善

            // 发送杀死目标消息
            SendKillMessage(target);
        }

        /// <summary>
        /// 发送杀死目标消息
        /// </summary>
        private void SendKillMessage(ICombatEntity target)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x28D); // SM_KILLTARGET
            builder.WriteUInt16((ushort)target.Id);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteString($"你杀死了 {target.Name}");

            SendMessage(builder.Build());
        }

        /// <summary>
        /// 发送攻击消息
        /// </summary>
        private void SendAttackMessage(ICombatEntity target, int damage)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x285); // SM_ATTACK
            builder.WriteUInt16((ushort)target.Id);
            builder.WriteUInt16((ushort)damage);
            builder.WriteUInt16(0);

            SendMessage(builder.Build());
        }

        /// <summary>
        /// 施放技能
        /// </summary>
        public bool SpellCast(int x, int y, uint magicId, ushort targetId)
        {
            // TODO: 实现技能施放逻辑
            return true;
        }

        /// <summary>
        /// 使用物品
        /// </summary>
        public bool UseItem(uint makeIndex)
        {
            // TODO: 实现使用物品逻辑
            return true;
        }

        /// <summary>
        /// 丢弃金币
        /// </summary>
        public bool DropGold(uint amount)
        {
            if (Gold < amount)
                return false;

            Gold -= amount;
            return true;
        }

        /// <summary>
        /// 购买物品
        /// </summary>
        public bool BuyItem(uint npcInstanceId, int itemIndex)
        {
            // 获取NPC
            var npc = NPCManager.Instance.GetNPC(npcInstanceId);
            if (npc == null || !npc.Definition.HasFunction(NPCFunction.Shop))
            {
                SaySystem("这个NPC不提供购买服务");
                return false;
            }

            // 检查NPC商店是否有该物品
            if (itemIndex < 0 || itemIndex >= npc.Definition.ShopItems.Count)
            {
                SaySystem("无效的物品索引");
                return false;
            }

            int itemId = npc.Definition.ShopItems[itemIndex];
            var itemDef = ItemManager.Instance.GetDefinition(itemId);
            if (itemDef == null)
            {
                SaySystem("物品不存在");
                return false;
            }

            // 计算价格（考虑NPC的出售倍率）
            uint price = (uint)(itemDef.BuyPrice * npc.Definition.SellRate);
            if (price == 0) price = 1; // 最低1金币

            // 检查金币是否足够
            if (Gold < price)
            {
                SaySystem($"金币不足，需要 {price} 金币");
                return false;
            }

            // 检查背包空间
            if (Inventory.GetUsedSlots() >= Inventory.MaxSlots)
            {
                SaySystem("背包已满");
                return false;
            }

            // 创建物品
            var item = ItemManager.Instance.CreateItem(itemId);
            if (item == null)
            {
                SaySystem("无法创建物品");
                return false;
            }

            // 扣除金币
            if (!TakeGold(price))
            {
                SaySystem("金币扣除失败");
                return false;
            }

            // 添加到背包
            if (!Inventory.AddItem(item))
            {
                // 如果添加失败，返还金币
                AddGold(price);
                SaySystem("背包空间不足");
                return false;
            }

            // 记录日志
            LogManager.Default.Info($"{Name} 从 {npc.Definition.Name} 购买了 {item.Definition.Name}，花费 {price} 金币");

            // 发送购买成功消息
            SaySystem($"购买了 {item.Definition.Name}，花费 {price} 金币");

            // 发送背包更新消息
            SendInventoryUpdate();

            return true;
        }

        /// <summary>
        /// 出售物品
        /// </summary>
        public bool SellItem(uint npcInstanceId, int bagSlot)
        {
            // 获取NPC
            var npc = NPCManager.Instance.GetNPC(npcInstanceId);
            if (npc == null || !npc.Definition.HasFunction(NPCFunction.Shop))
            {
                SaySystem("这个NPC不提供出售服务");
                return false;
            }

            // 获取背包物品
            var item = Inventory.GetItem(bagSlot);
            if (item == null)
            {
                SaySystem("该位置没有物品");
                return false;
            }

            // 检查物品是否可以出售
            if (!item.Definition.CanTrade)
            {
                SaySystem("这个物品不能出售");
                return false;
            }

            // 检查物品是否绑定
            if (item.IsBound)
            {
                SaySystem("绑定物品不能出售");
                return false;
            }

            // 计算价格（考虑NPC的收购倍率）
            uint price = (uint)(item.Definition.SellPrice * npc.Definition.BuyRate * item.Count);
            if (price == 0) price = 1; // 最低1金币

            // 确认出售
            SaySystem($"出售 {item.Definition.Name} x{item.Count}，获得 {price} 金币？");

            // 从背包移除物品
            if (!Inventory.RemoveItem(bagSlot, item.Count))
            {
                SaySystem("移除物品失败");
                return false;
            }

            // 添加金币
            if (!AddGold(price))
            {
                // 如果添加金币失败，返还物品
                Inventory.AddItem(item);
                SaySystem("金币添加失败");
                return false;
            }

            // 记录日志
            LogManager.Default.Info($"{Name} 向 {npc.Definition.Name} 出售了 {item.Definition.Name} x{item.Count}，获得 {price} 金币");

            // 发送出售成功消息
            SaySystem($"出售了 {item.Definition.Name} x{item.Count}，获得 {price} 金币");

            // 发送背包更新消息
            SendInventoryUpdate();

            return true;
        }

        /// <summary>
        /// 修理物品
        /// </summary>
        public bool RepairItem(uint npcInstanceId, int bagSlot)
        {
            // 获取NPC
            var npc = NPCManager.Instance.GetNPC(npcInstanceId);
            if (npc == null || !npc.Definition.HasFunction(NPCFunction.Repair))
            {
                SaySystem("这个NPC不提供修理服务");
                return false;
            }

            // 获取物品
            ItemInstance? item = null;

            if (bagSlot >= 0)
            {
                // 修理背包物品
                item = Inventory.GetItem(bagSlot);
                if (item == null)
                {
                    SaySystem("该位置没有物品");
                    return false;
                }
            }
            else
            {
                // 修理装备（bagSlot为负数表示装备槽）
                int equipSlot = -bagSlot - 1;
                if (equipSlot < 0 || equipSlot >= (int)EquipSlot.Max)
                {
                    SaySystem("无效的装备槽");
                    return false;
                }

                item = Equipment.GetEquipment((EquipSlot)equipSlot);
                if (item == null)
                {
                    SaySystem("该位置没有装备");
                    return false;
                }
            }

            // 检查物品是否需要修理
            if (item.Durability >= item.MaxDurability)
            {
                SaySystem("物品不需要修理");
                return false;
            }

            // 计算修理费用
            uint repairCost = CalculateRepairCost(item);
            if (repairCost == 0)
            {
                SaySystem("无法计算修理费用");
                return false;
            }

            // 检查金币是否足够
            if (Gold < repairCost)
            {
                SaySystem($"金币不足，需要 {repairCost} 金币");
                return false;
            }

            // 扣除金币
            if (!TakeGold(repairCost))
            {
                SaySystem("金币扣除失败");
                return false;
            }

            // 修理物品
            item.Durability = item.MaxDurability;

            // 记录日志
            LogManager.Default.Info($"{Name} 修理了 {item.Definition.Name}，花费 {repairCost} 金币");

            // 发送修理成功消息
            SaySystem($"修理了 {item.Definition.Name}，花费 {repairCost} 金币");

            // 发送物品更新消息
            if (bagSlot >= 0)
            {
                SendInventoryUpdate();
            }
            else
            {
                SendEquipmentUpdate((EquipSlot)(-bagSlot - 1), item);
            }

            return true;
        }

        /// <summary>
        /// 查询修理价格
        /// </summary>
        public bool QueryRepairPrice(uint npcInstanceId, int bagSlot)
        {
            // 获取NPC
            var npc = NPCManager.Instance.GetNPC(npcInstanceId);
            if (npc == null || !npc.Definition.HasFunction(NPCFunction.Repair))
            {
                SaySystem("这个NPC不提供修理服务");
                return false;
            }

            // 获取物品
            ItemInstance? item = null;

            if (bagSlot >= 0)
            {
                // 查询背包物品修理价格
                item = Inventory.GetItem(bagSlot);
                if (item == null)
                {
                    SaySystem("该位置没有物品");
                    return false;
                }
            }
            else
            {
                // 查询装备修理价格（bagSlot为负数表示装备槽）
                int equipSlot = -bagSlot - 1;
                if (equipSlot < 0 || equipSlot >= (int)EquipSlot.Max)
                {
                    SaySystem("无效的装备槽");
                    return false;
                }

                item = Equipment.GetEquipment((EquipSlot)equipSlot);
                if (item == null)
                {
                    SaySystem("该位置没有装备");
                    return false;
                }
            }

            // 检查物品是否需要修理
            if (item.Durability >= item.MaxDurability)
            {
                SaySystem("物品不需要修理");
                return true;
            }

            // 计算修理费用
            uint repairCost = CalculateRepairCost(item);
            if (repairCost == 0)
            {
                SaySystem("无法计算修理费用");
                return false;
            }

            // 显示修理价格
            SaySystem($"修理 {item.Definition.Name} 需要 {repairCost} 金币");

            return true;
        }

        /// <summary>
        /// 查看装备
        /// </summary>
        public bool ViewEquipment(uint targetPlayerId)
        {
            // 获取目标玩家
            var targetPlayer = HumanPlayerMgr.Instance.FindById(targetPlayerId);
            if (targetPlayer == null)
            {
                SaySystem("目标玩家不存在");
                return false;
            }

            // 检查距离
            if (CurrentMap != targetPlayer.CurrentMap || !IsInRange(targetPlayer, 5))
            {
                SaySystem("距离太远");
                return false;
            }

            // 构建装备信息消息
            var builder = new PacketBuilder();
            builder.WriteUInt32(targetPlayer.ObjectId);
            builder.WriteUInt16(0x28A); // SM_VIEWEQUIPMENT
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);

            // 添加玩家信息
            builder.WriteString(targetPlayer.Name);
            builder.WriteUInt16((ushort)targetPlayer.Level);
            builder.WriteByte(targetPlayer.Job);

            // 添加装备信息
            var allEquipment = targetPlayer.Equipment.GetAllEquipment();
            builder.WriteByte((byte)allEquipment.Count);

            foreach (var equip in allEquipment)
            {
                builder.WriteUInt32((uint)equip.InstanceId);
                builder.WriteInt32(equip.ItemId);
                builder.WriteString(equip.Definition.Name);
                builder.WriteUInt16((ushort)equip.Durability);
                builder.WriteUInt16((ushort)equip.MaxDurability);
                builder.WriteByte((byte)equip.EnhanceLevel);
            }

            // 发送消息
            SendMessage(builder.Build());

            // 记录日志
            LogManager.Default.Info($"{Name} 查看了 {targetPlayer.Name} 的装备");

            return true;
        }


        /// <summary>
        /// 特殊攻击
        /// </summary>
        public bool SpecialHit(byte direction, int skillType)
        {
            Direction = direction;

            // 根据技能类型执行不同的特殊攻击
            switch (skillType)
            {
                case 7:  // 刺杀剑术
                    return ExecuteAssassinate(direction);
                case 12: // 半月弯刀
                    return ExecuteHalfMoon(direction);
                case 25: // 烈火剑法
                    return ExecuteFireSword(direction);
                case 26: // 野蛮冲撞
                    return ExecuteRush(direction);
                default:
                    SaySystem("未知的特殊攻击类型");
                    return false;
            }
        }

        /// <summary>
        /// 执行刺杀剑术
        /// </summary>
        private bool ExecuteAssassinate(byte direction)
        {
            // 检查是否学习了刺杀剑术技能
            if (!SkillBook.HasSkill(1002)) // 1002是刺杀剑术的技能ID
            {
                SaySystem("未学习刺杀剑术");
                return false;
            }

            // 获取技能
            var skill = SkillBook.GetSkill(1002);
            if (skill == null || !skill.CanUse())
            {
                SaySystem("刺杀剑术技能不可用");
                return false;
            }

            // 根据方向计算目标位置
            int targetX = X;
            int targetY = Y;
            int secondTargetX = X;
            int secondTargetY = Y;

            // 刺杀剑术可以攻击两格距离的目标
            switch (direction)
            {
                case 0: // 上
                    targetY--;
                    secondTargetY -= 2;
                    break;
                case 1: // 右上
                    targetX++; targetY--;
                    secondTargetX += 2; secondTargetY -= 2;
                    break;
                case 2: // 右
                    targetX++;
                    secondTargetX += 2;
                    break;
                case 3: // 右下
                    targetX++; targetY++;
                    secondTargetX += 2; secondTargetY += 2;
                    break;
                case 4: // 下
                    targetY++;
                    secondTargetY += 2;
                    break;
                case 5: // 左下
                    targetX--; targetY++;
                    secondTargetX -= 2; secondTargetY += 2;
                    break;
                case 6: // 左
                    targetX--;
                    secondTargetX -= 2;
                    break;
                case 7: // 左上
                    targetX--; targetY--;
                    secondTargetX -= 2; secondTargetY -= 2;
                    break;
            }

            // 攻击第一个目标
            bool hitFirst = AttackTargetAt(targetX, targetY);

            // 攻击第二个目标（如果有）
            bool hitSecond = AttackTargetAt(secondTargetX, secondTargetY);

            // 使用技能
            if (hitFirst || hitSecond)
            {
                skill.Use();
                SaySystem("刺杀剑术！");
                return true;
            }

            SaySystem("没有可攻击的目标");
            return false;
        }

        /// <summary>
        /// 执行半月弯刀
        /// </summary>
        private bool ExecuteHalfMoon(byte direction)
        {
            // 检查是否学习了半月弯刀技能
            if (!SkillBook.HasSkill(1003)) // 1003是半月弯刀技能ID
            {
                SaySystem("未学习半月弯刀");
                return false;
            }

            // 获取技能
            var skill = SkillBook.GetSkill(1003);
            if (skill == null || !skill.CanUse())
            {
                SaySystem("半月弯刀技能不可用");
                return false;
            }

            // 半月弯刀攻击周围所有敌人
            bool hitAny = false;

            // 获取周围8个方向的目标
            for (int i = 0; i < 8; i++)
            {
                int targetX = X;
                int targetY = Y;

                switch (i)
                {
                    case 0: targetY--; break; // 上
                    case 1: targetX++; targetY--; break; // 右上
                    case 2: targetX++; break; // 右
                    case 3: targetX++; targetY++; break; // 右下
                    case 4: targetY++; break; // 下
                    case 5: targetX--; targetY++; break; // 左下
                    case 6: targetX--; break; // 左
                    case 7: targetX--; targetY--; break; // 左上
                }

                if (AttackTargetAt(targetX, targetY))
                {
                    hitAny = true;
                }
            }

            // 使用技能
            if (hitAny)
            {
                skill.Use();
                SaySystem("半月弯刀！");
                return true;
            }

            SaySystem("没有可攻击的目标");
            return false;
        }

        /// <summary>
        /// 执行烈火剑法
        /// </summary>
        private bool ExecuteFireSword(byte direction)
        {
            // 检查是否学习了烈火剑法技能
            if (!SkillBook.HasSkill(1004)) // 假设1004是烈火剑法技能ID
            {
                SaySystem("未学习烈火剑法");
                return false;
            }

            // 获取技能
            var skill = SkillBook.GetSkill(1004);
            if (skill == null || !skill.CanUse())
            {
                SaySystem("烈火剑法技能不可用");
                return false;
            }

            // 根据方向计算目标位置
            int targetX = X;
            int targetY = Y;

            switch (direction)
            {
                case 0: targetY--; break; // 上
                case 1: targetX++; targetY--; break; // 右上
                case 2: targetX++; break; // 右
                case 3: targetX++; targetY++; break; // 右下
                case 4: targetY++; break; // 下
                case 5: targetX--; targetY++; break; // 左下
                case 6: targetX--; break; // 左
                case 7: targetX--; targetY--; break; // 左上
            }

            // 攻击目标
            bool hit = AttackTargetAt(targetX, targetY);

            // 使用技能
            if (hit)
            {
                skill.Use();
                SaySystem("烈火剑法！");

                // 烈火剑法有额外伤害效果
                // 这里可以添加额外的伤害计算逻辑
                return true;
            }

            SaySystem("没有可攻击的目标");
            return false;
        }

        /// <summary>
        /// 执行野蛮冲撞
        /// </summary>
        private bool ExecuteRush(byte direction)
        {
            // 检查是否学习了野蛮冲撞技能
            if (!SkillBook.HasSkill(1005)) // 假设1005是野蛮冲撞技能ID
            {
                SaySystem("未学习野蛮冲撞");
                return false;
            }

            // 获取技能
            var skill = SkillBook.GetSkill(1005);
            if (skill == null || !skill.CanUse())
            {
                SaySystem("野蛮冲撞技能不可用");
                return false;
            }

            // 计算冲撞路径
            int targetX = X;
            int targetY = Y;

            // 冲撞可以移动多格距离
            int maxDistance = 3; // 最大冲撞距离
            bool hitTarget = false;

            for (int i = 1; i <= maxDistance; i++)
            {
                // 计算当前位置
                int currentX = X;
                int currentY = Y;

                switch (direction)
                {
                    case 0: currentY -= i; break; // 上
                    case 1: currentX += i; currentY -= i; break; // 右上
                    case 2: currentX += i; break; // 右
                    case 3: currentX += i; currentY += i; break; // 右下
                    case 4: currentY += i; break; // 下
                    case 5: currentX -= i; currentY += i; break; // 左下
                    case 6: currentX -= i; break; // 左
                    case 7: currentX -= i; currentY -= i; break; // 左上
                }

                // 检查是否可以移动到该位置
                if (CurrentMap == null || !CurrentMap.CanMoveTo(currentX, currentY))
                {
                    // 遇到障碍物或地图边界，停止冲撞
                    break;
                }

                // 检查该位置是否有目标
                var target = CurrentMap.GetObjectAt(currentX, currentY) as ICombatEntity;
                if (target != null && CanAttackTarget(target))
                {
                    // 攻击目标
                    PerformAttack(target);
                    hitTarget = true;

                    // 冲撞到目标后停止
                    break;
                }

                // 移动到该位置
                X = (ushort)currentX;
                Y = (ushort)currentY;
                targetX = currentX;
                targetY = currentY;
            }

            // 使用技能
            if (hitTarget)
            {
                skill.Use();
                SaySystem("野蛮冲撞！");
                return true;
            }

            // 即使没有撞到目标，也移动到目标位置
            if (targetX != X || targetY != Y)
            {
                X = (ushort)targetX;
                Y = (ushort)targetY;
                skill.Use();
                SaySystem("野蛮冲撞！");
                return true;
            }

            SaySystem("无法冲撞");
            return false;
        }

        /// <summary>
        /// 攻击指定位置的目标
        /// </summary>
        private bool AttackTargetAt(int x, int y)
        {
            if (CurrentMap == null)
                return false;

            var target = CurrentMap.GetObjectAt(x, y) as ICombatEntity;
            if (target == null)
                return false;

            if (!CanAttackTarget(target))
                return false;

            PerformAttack(target);
            return true;
        }

        #endregion

        #region 状态检查方法

        /// <summary>
        /// 是否在战斗中
        /// </summary>
        public bool IsInCombat()
        {
            return false;
        }

        /// <summary>
        /// 是否在摆摊中
        /// </summary>
        public bool IsInPrivateShop()
        {
            return false;
        }

        /// <summary>
        /// 重新计算总属性
        /// </summary>
        public void RecalcTotalStats()
        {
            // 重新计算所有属性
            // 基础属性 + 装备属性 + 技能加成等

            // 这里应该实现完整的属性计算逻辑
        }

        /// <summary>
        /// 添加物品到背包
        /// </summary>
        public bool AddItem(ItemInstance item)
        {
            return Inventory.AddItem(item);
        }

        #endregion

        #region 宠物仓库方法

        /// <summary>
        /// 放入宠物仓库
        /// </summary>
        public void PutItemToPetBag(uint itemId)
        {
            try
            {
                LogManager.Default.Info($"{Name} 放入宠物仓库物品: {itemId}");
                
                // 获取背包物品
                var item = Inventory.GetItem((int)itemId);
                if (item == null)
                {
                    SaySystem("物品不存在");
                    return;
                }
                
                // 检查宠物背包空间
                var petBag = PetSystem.GetPetBag();
                if (petBag.GetUsedSlots() >= petBag.MaxSlots)
                {
                    SaySystem("宠物背包已满");
                    return;
                }
                
                // 从背包移除物品
                if (!Inventory.RemoveItem((int)itemId, 1))
                {
                    SaySystem("移除物品失败");
                    return;
                }
                
                // 添加到宠物背包
                if (!petBag.AddItem(item))
                {
                    // 如果添加失败，返还物品
                    Inventory.AddItem(item);
                    SaySystem("放入宠物背包失败");
                    return;
                }
                
                // 发送背包更新消息
                SendInventoryUpdate();
                
                // 发送宠物背包更新消息
                SendPetBagUpdate();
                
                SaySystem($"已将 {item.Definition.Name} 放入宠物背包");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"放入宠物仓库失败: {ex.Message}");
                SaySystem("放入宠物仓库失败");
            }
        }

        /// <summary>
        /// 从宠物仓库取出
        /// </summary>
        public void GetItemFromPetBag(uint itemId)
        {
            try
            {
                LogManager.Default.Info($"{Name} 从宠物仓库取出物品: {itemId}");
                
                // 获取宠物背包物品
                var petBag = PetSystem.GetPetBag();
                var item = petBag.GetItem((int)itemId);
                if (item == null)
                {
                    SaySystem("物品不存在");
                    return;
                }
                
                // 检查背包空间
                if (Inventory.GetUsedSlots() >= Inventory.MaxSlots)
                {
                    SaySystem("背包已满");
                    return;
                }
                
                // 从宠物背包移除物品
                if (!petBag.RemoveItem((int)itemId, 1))
                {
                    SaySystem("移除物品失败");
                    return;
                }
                
                // 添加到背包
                if (!Inventory.AddItem(item))
                {
                    // 如果添加失败，返还物品
                    petBag.AddItem(item);
                    SaySystem("放入背包失败");
                    return;
                }
                
                // 发送宠物背包更新消息
                SendPetBagUpdate();
                
                // 发送背包更新消息
                SendInventoryUpdate();
                
                SaySystem($"已将 {item.Definition.Name} 从宠物背包取出");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"从宠物仓库取出失败: {ex.Message}");
                SaySystem("从宠物仓库取出失败");
            }
        }

        #endregion

        #region 任务系统方法

        /// <summary>
        /// 删除任务
        /// </summary>
        public void DeleteTask(uint taskId)
        {
            try
            {
                LogManager.Default.Info($"{Name} 删除任务: {taskId}");
                
                // 从任务管理器删除任务
                bool success = QuestManager.DeleteTask((int)taskId);
                
                if (success)
                {
                    SaySystem($"已删除任务: {taskId}");
                    
                    // 发送任务删除消息给客户端
                    SendTaskDeleted(taskId);
                }
                else
                {
                    SaySystem("删除任务失败");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"删除任务失败: {ex.Message}");
                SaySystem("删除任务失败");
            }
        }

        /// <summary>
        /// 发送任务删除消息
        /// </summary>
        private void SendTaskDeleted(uint taskId)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x296); // SM_TASKDELETED
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(taskId);
            
            SendMessage(builder.Build());
        }

        #endregion

        #region 好友系统方法

        /// <summary>
        /// 删除好友
        /// </summary>
        public void DeleteFriend(string friendName)
        {
            try
            {
                LogManager.Default.Info($"{Name} 删除好友: {friendName}");
                
                // 这里需要根据实际的好友系统实现
                SaySystem($"已删除好友: {friendName}");
                
                // 发送好友删除消息给客户端
                SendFriendDeleted(friendName);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"删除好友失败: {ex.Message}");
                SaySystem("删除好友失败");
            }
        }

        /// <summary>
        /// 回复添加好友请求
        /// </summary>
        public void ReplyAddFriendRequest(uint requestId, string replyData)
        {
            try
            {
                LogManager.Default.Info($"{Name} 回复添加好友请求: {requestId}, {replyData}");
                
                // 解析回复数据
                bool accept = replyData.Contains("accept", StringComparison.OrdinalIgnoreCase);
                
                // 这里需要根据实际的好友系统实现
                if (accept)
                {
                    SaySystem("已接受好友请求");
                }
                else
                {
                    SaySystem("已拒绝好友请求");
                }
                
                // 发送回复结果消息给客户端
                SendFriendRequestReply(requestId, accept);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"回复添加好友请求失败: {ex.Message}");
                SaySystem("回复添加好友请求失败");
            }
        }

        /// <summary>
        /// 发送好友删除消息
        /// </summary>
        private void SendFriendDeleted(string friendName)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x297); // SM_FRIENDDELETED
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteString(friendName);
            
            SendMessage(builder.Build());
        }

        /// <summary>
        /// 发送好友请求回复消息
        /// </summary>
        private void SendFriendRequestReply(uint requestId, bool accept)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x298); // SM_FRIENDREQUESTREPLY
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(requestId);
            builder.WriteByte(accept ? (byte)1 : (byte)0);
            
            SendMessage(builder.Build());
        }

        /// <summary>
        /// 发送好友系统错误消息
        /// </summary>
        public void SendFriendSystemError(byte error, string friendName)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x299); // SM_FRIENDSYSTEMERROR
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteByte(error);
            builder.WriteString(friendName);
            
            SendMessage(builder.Build());
        }

        /// <summary>
        /// 发送添加好友请求
        /// </summary>
        public void PostAddFriendRequest(HumanPlayer requester)
        {
            try
            {
                LogManager.Default.Info($"{Name} 收到来自 {requester.Name} 的好友请求");
                
                // 发送好友请求消息给客户端
                SendFriendRequest(requester);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送好友请求失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送好友请求消息
        /// </summary>
        private void SendFriendRequest(HumanPlayer requester)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x29A); // SM_FRIENDREQUEST
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(requester.ObjectId);
            builder.WriteString(requester.Name);
            
            SendMessage(builder.Build());
        }

        #endregion

        #region 行会系统方法

        /// <summary>
        /// 回复加入行会请求
        /// </summary>
        public void ReplyAddToGuildRequest(bool accept)
        {
            try
            {
                LogManager.Default.Info($"{Name} 回复加入行会请求: {(accept ? "接受" : "拒绝")}");
                
                // 这里需要根据实际的行会系统实现
                if (accept)
                {
                    SaySystem("已接受加入行会请求");
                }
                else
                {
                    SaySystem("已拒绝加入行会请求");
                }
                
                // 发送回复结果消息给客户端
                SendGuildRequestReply(accept);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"回复加入行会请求失败: {ex.Message}");
                SaySystem("回复加入行会请求失败");
            }
        }

        /// <summary>
        /// 发送加入行会请求
        /// </summary>
        public void PostAddToGuildRequest(HumanPlayer inviter)
        {
            try
            {
                LogManager.Default.Info($"{Name} 收到来自 {inviter.Name} 的加入行会请求");
                
                // 发送加入行会请求消息给客户端
                SendGuildInvite(inviter);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送加入行会请求失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送行会请求回复消息
        /// </summary>
        private void SendGuildRequestReply(bool accept)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x29B); // SM_GUILDREQUESTREPLY
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteByte(accept ? (byte)1 : (byte)0);
            
            SendMessage(builder.Build());
        }

        /// <summary>
        /// 发送行会邀请消息
        /// </summary>
        private void SendGuildInvite(HumanPlayer inviter)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x29C); // SM_GUILDINVITE
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(inviter.ObjectId);
            builder.WriteString(inviter.Name);
            builder.WriteString(inviter.Guild?.Name ?? "");
            
            SendMessage(builder.Build());
        }

        #endregion

        #region 仓库系统方法

        /// <summary>
        /// 从仓库取出物品
        /// </summary>
        public bool TakeBankItem(uint itemId)
        {
            try
            {
                LogManager.Default.Info($"{Name} 从仓库取出物品: {itemId}");
                
                // 这里需要根据实际的仓库系统实现
                
                // 检查背包空间
                if (Inventory.GetUsedSlots() >= Inventory.MaxSlots)
                {
                    SaySystem("背包已满");
                    return false;
                }
                
                // 模拟取出物品
                SaySystem("已从仓库取出物品");
                
                // 发送背包更新消息
                SendInventoryUpdate();
                
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"从仓库取出物品失败: {ex.Message}");
                SaySystem("从仓库取出物品失败");
                return false;
            }
        }

        /// <summary>
        /// 放入仓库物品
        /// </summary>
        public bool PutBankItem(uint itemId)
        {
            try
            {
                LogManager.Default.Info($"{Name} 放入仓库物品: {itemId}");
                
                // 这里需要根据实际的仓库系统实现
                
                // 检查仓库空间
                
                // 模拟放入物品
                SaySystem("已放入仓库物品");
                
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"放入仓库物品失败: {ex.Message}");
                SaySystem("放入仓库物品失败");
                return false;
            }
        }

        #endregion

        #region 技能快捷键方法

        /// <summary>
        /// 设置技能快捷键
        /// </summary>
        public void SetMagicKey(uint skillId, ushort key1, ushort key2)
        {
            try
            {
                LogManager.Default.Info($"{Name} 设置技能快捷键: 技能ID={skillId}, 快捷键={key1},{key2}");
                
                // 获取技能
                var skill = SkillBook.GetSkill((int)skillId);
                if (skill == null)
                {
                    SaySystem("技能不存在");
                    return;
                }
                
                // 设置技能快捷键
                
                // 发送技能快捷键更新消息
                SendMagicKeyUpdated(skillId, key1, key2);
                
                SaySystem("已设置技能快捷键");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"设置技能快捷键失败: {ex.Message}");
                SaySystem("设置技能快捷键失败");
            }
        }

        /// <summary>
        /// 发送技能快捷键更新消息
        /// </summary>
        private void SendMagicKeyUpdated(uint skillId, ushort key1, ushort key2)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x29D); // SM_MAGICKEYUPDATED
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(skillId);
            builder.WriteUInt16(key1);
            builder.WriteUInt16(key2);
            
            SendMessage(builder.Build());
        }

        #endregion

        #region 其他方法

        /// <summary>
        /// 切割尸体
        /// </summary>
        public bool CutBody(uint corpseId, ushort param1, ushort param2, ushort param3)
        {
            try
            {
                LogManager.Default.Info($"{Name} 切割尸体: 尸体ID={corpseId}, 参数={param1},{param2},{param3}");
                
                // 获取尸体对象
                var corpse = CurrentMap?.GetObject(corpseId) as MonsterCorpse;
                if (corpse == null)
                {
                    SaySystem("尸体不存在");
                    return false;
                }
                
                // 检查距离
                if (!IsInRange(corpse, 1))
                {
                    SaySystem("距离太远");
                    return false;
                }
                
                // 执行切割动作 - 使用GetMeat动作代替CutBody
                StartAction(ActionType.GetMeat, corpseId);
                
                // 更新切割时间
                _lastGetMeatTime = DateTime.Now;
                
                // 发送切割特效消息
                SendCutBodyEffect(corpseId);
                
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"切割尸体失败: {ex.Message}");
                SaySystem("切割尸体失败");
                return false;
            }
        }

        /// <summary>
        /// 发送切割尸体特效消息
        /// </summary>
        private void SendCutBodyEffect(uint corpseId)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x29E); // SM_CUTBODYEFFECT
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(corpseId);
            
            SendMessage(builder.Build());
        }

        /// <summary>
        /// 放入物品
        /// </summary>
        public void OnPutItem(uint itemId, uint param)
        {
            try
            {
                LogManager.Default.Info($"{Name} 放入物品: 物品ID={itemId}, 参数={param}");
                
                // 这里需要根据实际的物品系统实现
                SaySystem("已放入物品");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"放入物品失败: {ex.Message}");
                SaySystem("放入物品失败");
            }
        }

        /// <summary>
        /// 显示宠物信息
        /// </summary>
        public void ShowPetInfo()
        {
            try
            {
                LogManager.Default.Info($"{Name} 显示宠物信息");
                
                // 获取宠物信息
                var petInfo = PetSystem.GetPetInfo();
                
                // 发送宠物信息消息
                SendPetInfo(petInfo);
                
                SaySystem("已显示宠物信息");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"显示宠物信息失败: {ex.Message}");
                SaySystem("显示宠物信息失败");
            }
        }

        /// <summary>
        /// 发送宠物信息消息
        /// </summary>
        private void SendPetInfo(object petInfo)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x29F); // SM_PETINFO
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            
            // 这里需要根据实际的宠物信息结构来构建消息
            builder.WriteString("宠物信息");
            
            SendMessage(builder.Build());
        }

        #endregion
    }
}
