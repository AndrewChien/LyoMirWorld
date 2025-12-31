using System;
using System.Collections.Generic;
using System.Linq;
using MirCommon;
using MirCommon.Network;

namespace GameServer
{
    /// <summary>
    /// 宠物系统
    /// </summary>
    public class PetSystem
    {
        private readonly HumanPlayer _owner;
        private readonly List<Monster> _pets = new();
        private Monster? _mainPet;
        private readonly object _petLock = new();
        
        // 宠物背包
        private readonly Inventory _petBag = new() { MaxSlots = 10 };
        
        public PetSystem(HumanPlayer owner)
        {
            _owner = owner;
        }
        
        /// <summary>
        /// 获取宠物数量
        /// </summary>
        public int GetPetCount()
        {
            lock (_petLock)
            {
                return _pets.Count;
            }
        }
        
        /// <summary>
        /// 最大宠物数量
        /// </summary>
        public int MaxPets => 5; // 最大宠物数量
        
        /// <summary>
        /// 召唤宠物
        /// </summary>
        public bool SummonPet(string petName, bool setOwner = true, int x = -1, int y = -1)
        {
            if (_pets.Count >= 5) // 最大宠物数量
            {
                _owner.Say("宠物数量已达上限");
                return false;
            }
            
            // 创建宠物怪物
            var pet = new Monster(0, petName) // 使用0作为怪物ID，因为宠物不是标准怪物
            {
                OwnerPlayerId = setOwner ? _owner.ObjectId : 0,
                IsPet = true
            };
            
            // 设置位置
            if (x == -1 || y == -1)
            {
                x = _owner.X;
                y = _owner.Y;
            }
            
            // 添加到地图
            if (_owner.CurrentMap != null)
            {
                _owner.CurrentMap.AddObject(pet, (ushort)x, (ushort)y);
            }
            
            lock (_petLock)
            {
                _pets.Add(pet);
                if (_mainPet == null)
                {
                    _mainPet = pet;
                }
            }
            
            _owner.Say($"召唤了 {petName}");
            return true;
        }
        
        /// <summary>
        /// 释放宠物
        /// </summary>
        public bool ReleasePet(string petName)
        {
            lock (_petLock)
            {
                var pet = _pets.FirstOrDefault(p => p.Name == petName);
                if (pet == null)
                {
                    _owner.Say($"没有找到宠物 {petName}");
                    return false;
                }
                
                // 从地图移除
                pet.CurrentMap?.RemoveObject(pet);
                _pets.Remove(pet);
                
                if (_mainPet == pet)
                {
                    _mainPet = _pets.FirstOrDefault();
                }
                
                _owner.Say($"释放了 {petName}");
                return true;
            }
        }
        
        /// <summary>
        /// 设置宠物目标
        /// </summary>
        public void SetPetTarget(AliveObject target)
        {
            lock (_petLock)
            {
                foreach (var pet in _pets)
                {
                    pet.SetTarget(target);
                }
            }
        }
        
        /// <summary>
        /// 清理所有宠物
        /// </summary>
        public void CleanPets()
        {
            lock (_petLock)
            {
                foreach (var pet in _pets)
                {
                    pet.CurrentMap?.RemoveObject(pet);
                }
                _pets.Clear();
                _mainPet = null;
            }
        }
        
        /// <summary>
        /// 获取宠物背包
        /// </summary>
        public Inventory GetPetBag() => _petBag;
        
        /// <summary>
        /// 设置宠物背包大小
        /// </summary>
        public bool SetPetBagSize(int size)
        {
            if (size != 5 && size != 10 && size != 0)
                return false;
                
            _petBag.MaxSlots = size;
            SendPetBagInfo();
            return true;
        }
        
        /// <summary>
        /// 从宠物背包获取物品
        /// </summary>
        public bool GetItemFromPetBag(ulong makeIndex)
        {
            var item = _petBag.FindItem(makeIndex);
            if (item == null)
                return false;
                
            if (!_owner.Inventory.AddItem(item))
            {
                _owner.Say("背包已满");
                return false;
            }
            
            _petBag.RemoveItem(makeIndex, 1);
            SendPetBagInfo();
            return true;
        }
        
        /// <summary>
        /// 放入物品到宠物背包
        /// </summary>
        public bool PutItemToPetBag(ulong makeIndex)
        {
            var item = _owner.Inventory.FindItem(makeIndex);
            if (item == null)
                return false;
                
            if (!_petBag.AddItem(item))
            {
                _owner.Say("宠物背包已满");
                return false;
            }
            
            _owner.Inventory.RemoveItem(makeIndex, 1);
            SendPetBagInfo();
            return true;
        }
        
        /// <summary>
        /// 发送宠物背包信息
        /// </summary>
        private void SendPetBagInfo()
        {
            // 发送设置宠物背包大小消息
            SendSetPetBag((ushort)_petBag.MaxSlots);
            
            // 发送宠物背包物品列表
            SendPetBag();
        }
        
        /// <summary>
        /// 发送设置宠物背包大小消息
        /// </summary>
        private void SendSetPetBag(ushort size)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(_owner.ObjectId);
            builder.WriteUInt16(0x9602); // SM_SETPETBAG
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(size);
            
            _owner.SendMessage(builder.Build());
        }
        
        /// <summary>
        /// 发送宠物背包物品列表
        /// </summary>
        private void SendPetBag()
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(_owner.ObjectId);
            builder.WriteUInt16(0x9603); // SM_PETBAG
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            
            // 写入宠物背包信息
            var items = _petBag.GetAllItems();
            builder.WriteUInt16((ushort)_petBag.MaxSlots);
            builder.WriteUInt16((ushort)items.Count);
            
            // 写入宠物背包物品列表
            foreach (var item in items.Values)
            {
                builder.WriteUInt64((ulong)item.InstanceId);
                builder.WriteUInt16((ushort)item.Definition.ItemId);
                builder.WriteUInt16((ushort)item.Durability);
                builder.WriteUInt16((ushort)item.MaxDurability);
                builder.WriteUInt32(item.Definition.SellPrice);
                builder.WriteByte(0); // 未知字段1
                builder.WriteByte(0); // 未知字段2
                builder.WriteByte(0); // 未知字段3
                builder.WriteByte(0); // 未知字段4
            }
            
            _owner.SendMessage(builder.Build());
        }
        
        /// <summary>
        /// 显示宠物信息
        /// </summary>
        public void ShowPetInfo()
        {
            lock (_petLock)
            {
                _owner.Say($"宠物数量: {_pets.Count}");
                foreach (var pet in _pets)
                {
                    _owner.Say($"{pet.Name} - 等级: {pet.Level} HP: {pet.CurrentHP}/{pet.MaxHP}");
                }
            }
        }

        /// <summary>
        /// 获取宠物信息
        /// </summary>
        public object GetPetInfo()
        {
            lock (_petLock)
            {
                // 返回宠物信息对象
                var petInfo = new
                {
                    PetCount = _pets.Count,
                    MainPet = _mainPet?.Name ?? "无",
                    Pets = _pets.Select(p => new
                    {
                        Name = p.Name,
                        Level = p.Level,
                        HP = $"{p.CurrentHP}/{p.MaxHP}",
                        IsMain = p == _mainPet
                    }).ToList(),
                    PetBagSize = _petBag.MaxSlots,
                    PetBagUsed = _petBag.GetUsedSlots()
                };
                
                return petInfo;
            }
        }
        
        /// <summary>
        /// 分配宠物经验
        /// </summary>
        public void DistributePetExp(uint exp)
        {
            lock (_petLock)
            {
                if (_pets.Count == 0)
                    return;
                    
                uint expPerPet = exp / (uint)_pets.Count;
                foreach (var pet in _pets)
                {
                    // 宠物获得经验
                    // 这里可以添加宠物升级逻辑
                    _owner.Say($"{pet.Name} 获得 {expPerPet} 经验");
                }
            }
        }
    }
    
    /// <summary>
    /// 坐骑系统
    /// </summary>
    public class MountSystem
    {
        private readonly HumanPlayer _owner;
        private MonsterEx? _horse;
        private bool _isRiding;
        private bool _horseRest;
        
        public MountSystem(HumanPlayer owner)
        {
            _owner = owner;
        }
        
        /// <summary>
        /// 获取坐骑
        /// </summary>
        public MonsterEx? GetHorse() => _horse;
        
        /// <summary>
        /// 设置坐骑
        /// </summary>
        public void SetHorse(MonsterEx? horse)
        {
            _horse = horse;
            if (_horse == null)
            {
                _isRiding = false;
            }
        }
        
        /// <summary>
        /// 骑乘坐骑
        /// </summary>
        public bool RideHorse()
        {
            if (_horse == null)
            {
                _owner.Say("你没有坐骑");
                return false;
            }
            
            if (_horse.CurrentHP <= 0)
            {
                _owner.Say("坐骑已死亡");
                return false;
            }
            
            _isRiding = true;
            _owner.Say("骑乘坐骑");
            return true;
        }
        
        /// <summary>
        /// 下马
        /// </summary>
        public void Dismount()
        {
            _isRiding = false;
            _owner.Say("下马");
        }
        
        /// <summary>
        /// 是否骑乘中
        /// </summary>
        public bool IsRiding() => _isRiding;
        
        /// <summary>
        /// 获取移动速度
        /// </summary>
        public byte GetRunSpeed()
        {
            if (_isRiding) return 3; // 骑马速度
            return 2; // 跑步速度
        }
        
        /// <summary>
        /// 是否装备了坐骑
        /// </summary>
        public bool IsEquipedHorse()
        {
            // 检查装备栏是否有坐骑装备
            var horseItem = _owner.Equipment.GetItem(EquipSlot.Mount);
            return horseItem != null;
        }
        
        /// <summary>
        /// 获取装备的坐骑物品
        /// </summary>
        public ItemInstance? GetEquipedHorseItem()
        {
            return _owner.Equipment.GetItem(EquipSlot.Mount);
        }
        
        /// <summary>
        /// 训练坐骑
        /// </summary>
        public bool TrainHorse(int dir)
        {
            if (_horse == null)
            {
                _owner.Say("你没有坐骑");
                return false;
            }
            
            // 检查是否可以执行动作
            if (!_owner.CanDoAction(ActionType.Attack))
            {
                _owner.Say("当前不能执行动作");
                return false;
            }
            
            // 检查武器是否为马鞭
            var weapon = _owner.Equipment.GetItem(EquipSlot.Weapon);
            if (weapon == null || weapon.Definition.Type != ItemType.Weapon) 
            {
                _owner.Say("需要装备马鞭才能训练坐骑");
                return false;
            }
            
            // 根据方向计算目标位置
            int targetX = _owner.X;
            int targetY = _owner.Y;
            
            switch (dir)
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
            
            // 检查目标位置是否有马匹
            if (_owner.CurrentMap == null)
                return false;
                
            var horse = _owner.CurrentMap.GetObjectAt(targetX, targetY) as MonsterEx;
            if (horse == null)
            {
                _owner.Say("目标位置没有马匹");
                return false;
            }
            
            var desc = horse.GetDesc();
            if (desc == null)
            {
                _owner.Say("这匹马不能训练");
                return false;
            }
            
            if (!desc.Base.ViewName.Contains("马"))
            {
                _owner.Say("这不是骑乘类型的马匹");
                return false;
            }
            
            // 训练成功
            _owner.Say("训练成功！");
            
            // 设置坐骑
            SetHorse(horse);
            
            // 发送训练成功消息
            SendTrainHorseSuccess();
            return true;
        }
        
        /// <summary>
        /// 发送训练坐骑成功消息
        /// </summary>
        private void SendTrainHorseSuccess()
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(_owner.ObjectId);
            builder.WriteUInt16(0x28F); // SM_TRAINHORSESUCCESS
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            
            _owner.SendMessage(builder.Build());
        }
        
        /// <summary>
        /// 切换坐骑休息状态
        /// </summary>
        public void ToggleHorseRest()
        {
            _horseRest = !_horseRest;
            _owner.Say(_horseRest ? "坐骑休息" : "坐骑工作");
        }
        
        /// <summary>
        /// 是否坐骑休息中
        /// </summary>
        public bool IsHorseRest() => _horseRest;
    }
    
    /// <summary>
    /// PK系统
    /// </summary>
    public class PKSystem
    {
        private readonly HumanPlayer _owner;
        private uint _pkValue;
        private DateTime _lastPkTime;
        private bool _justPk;
        private bool _isSelfDefense; // 正当防卫标记
        private DateTime _lastSelfDefenseTime;
        
        // PK值颜色阈值
        private const uint PK_VALUE_PURPLE = 10;  // 紫名
        private const uint PK_VALUE_ORANGE = 50;  // 橙名
        private const uint PK_VALUE_RED = 100;    // 红名
        
        // PK值衰减时间（分钟）
        private const int PK_DECAY_MINUTES = 5;
        
        public PKSystem(HumanPlayer owner)
        {
            _owner = owner;
            _pkValue = 0;
            _lastPkTime = DateTime.MinValue;
            _justPk = false;
            _isSelfDefense = false;
            _lastSelfDefenseTime = DateTime.MinValue;
        }
        
        /// <summary>
        /// 获取PK值
        /// </summary>
        public uint GetPkValue() => _pkValue;
        
        /// <summary>
        /// 设置PK值
        /// </summary>
        public void SetPkValue(uint value)
        {
            _pkValue = value;
            UpdateNameColor();
        }
        
        /// <summary>
        /// 增加PK点
        /// </summary>
        public void AddPkPoint(uint points = 1, bool isSelfDefense = false)
        {
            if (isSelfDefense)
            {
                _isSelfDefense = true;
                _lastSelfDefenseTime = DateTime.Now;
                return;
            }
            
            _pkValue += points;
            _lastPkTime = DateTime.Now;
            _justPk = true;
            
            // 更新名字颜色
            UpdateNameColor();
            
            // 检查是否需要武器诅咒
            CheckWeaponCurse();
            
            // 发送PK值变化消息
            SendPkValueChanged();
            
            _owner.Say($"PK值增加 {points}，当前PK值: {_pkValue}");
        }
        
        /// <summary>
        /// 减少PK点
        /// </summary>
        public void DecPkPoint(uint points = 1)
        {
            if (_pkValue >= points)
            {
                _pkValue -= points;
            }
            else
            {
                _pkValue = 0;
            }
            
            UpdateNameColor();
            SendPkValueChanged();
        }
        
        /// <summary>
        /// 获取名字颜色
        /// </summary>
        public byte GetNameColor(MapObject? viewer = null)
        {
            // 0 = 白名（正常）
            // 1 = 绿名（组队）
            // 2 = 红名（PK值>=100）
            // 3 = 灰名（死亡）
            // 4 = 蓝名（行会）
            // 5 = 紫名（PK值>=10）
            // 6 = 橙名（PK值>=50）
            
            if (_pkValue >= PK_VALUE_RED) return 2; // 红名
            if (_pkValue >= PK_VALUE_ORANGE) return 6; // 橙名
            if (_pkValue >= PK_VALUE_PURPLE) return 5; // 紫名
            
            // 检查是否在组队中
            if (_owner.GroupId != 0 && viewer is HumanPlayer viewerPlayer && viewerPlayer.GroupId == _owner.GroupId)
                return 1; // 绿名（队友）
                
            // 检查是否在同一行会
            if (_owner.Guild != null && viewer is HumanPlayer viewerPlayer2 && viewerPlayer2.Guild == _owner.Guild)
                return 4; // 蓝名（同公会）
                
            return 0; // 白名
        }
        
        /// <summary>
        /// 检查PK
        /// </summary>
        public bool CheckPk(AliveObject target)
        {
            if (target is HumanPlayer targetPlayer)
            {
                // 检查是否正当防卫
                if (targetPlayer.PKSystem._justPk || targetPlayer.PKSystem._isSelfDefense)
                {
                    // 对方刚刚PK或处于正当防卫状态，属于正当防卫
                    AddPkPoint(1, true);
                    return true;
                }
                
                // 检查是否同组
                if (_owner.GroupId != 0 && _owner.GroupId == targetPlayer.GroupId)
                {
                    _owner.Say("不能攻击队友");
                    return false;
                }
                
                // 检查是否同公会
                if (_owner.Guild != null && _owner.Guild == targetPlayer.Guild)
                {
                    _owner.Say("不能攻击同公会成员");
                    return false;
                }
                
                // 增加PK值
                AddPkPoint();
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 检查武器诅咒
        /// </summary>
        private void CheckWeaponCurse()
        {
            if (_pkValue >= PK_VALUE_RED)
            {
                // 检查武器是否会被诅咒
                var weapon = _owner.Equipment.GetItem(EquipSlot.Weapon);
                if (weapon != null)
                {
                    // 有一定概率诅咒武器
                    int curseProbability = 30; // 30%概率
                    
                    // 如果PK值更高，概率增加
                    if (_pkValue >= PK_VALUE_RED * 2)
                        curseProbability = 50;
                    else if (_pkValue >= PK_VALUE_RED * 3)
                        curseProbability = 70;
                    
                    if (Random.Shared.Next(100) < curseProbability)
                    {
                        // 诅咒武器
                        CurseWeapon(weapon);
                    }
                }
            }
        }
        
        /// <summary>
        /// 诅咒武器
        /// </summary>
        private void CurseWeapon(ItemInstance weapon)
        {
            if (weapon == null)
                return;
                
            // 武器属性存储在Ac1（幸运）和Mac1（诅咒）中
            
            // 增加诅咒值（通过ExtraStats实现）
            int curseValue = weapon.ExtraStats.GetValueOrDefault("Curse", 0);
            curseValue++;
            weapon.ExtraStats["Curse"] = curseValue;
            
            // 减少幸运值（如果有）
            int luckyValue = weapon.Definition.Lucky;
            if (luckyValue > 0)
                weapon.Definition.Lucky = luckyValue - 1;
            
            // 发送武器被诅咒消息
            _owner.Say("你的武器被诅咒了！");
            
            // 发送武器更新消息到客户端
            SendWeaponCursed(weapon);
            
            // 记录日志
            Console.WriteLine($"{_owner.Name} 的武器被诅咒，当前诅咒值: {curseValue}");
        }
        
        /// <summary>
        /// 发送武器被诅咒消息
        /// </summary>
        private void SendWeaponCursed(ItemInstance weapon)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(_owner.ObjectId);
            builder.WriteUInt16(0x290); // SM_WEAPONCURSED
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt64((ulong)weapon.InstanceId);
            builder.WriteUInt16((ushort)weapon.ExtraStats.GetValueOrDefault("Curse", 0));
            builder.WriteUInt16((ushort)weapon.Definition.Lucky);
            
            _owner.SendMessage(builder.Build());
        }
        
        /// <summary>
        /// 死亡掉落物品逻辑
        /// </summary>
        public List<ItemInstance> GetDeathDropItems()
        {
            var dropItems = new List<ItemInstance>();
            
            if (_pkValue >= PK_VALUE_RED)
            {
                // 红名死亡掉落更多物品
                // 掉落身上装备（有一定概率）
                foreach (var slot in Enum.GetValues<EquipSlot>())
                {
                    var item = _owner.Equipment.GetItem(slot);
                    if (item != null && Random.Shared.Next(100) < 50) // 50%概率掉落
                    {
                        dropItems.Add(item);
                    }
                }
                
                // 掉落背包物品（有一定概率）
                var inventoryItems = _owner.Inventory.GetAllItems();
                foreach (var item in inventoryItems.Values)
                {
                    if (Random.Shared.Next(100) < 30) // 30%概率掉落
                    {
                        dropItems.Add(item);
                    }
                }
            }
            else if (_pkValue >= PK_VALUE_ORANGE)
            {
                // 橙名死亡掉落部分物品
                var inventoryItems = _owner.Inventory.GetAllItems();
                int dropCount = Math.Min(3, inventoryItems.Count);
                for (int i = 0; i < dropCount; i++)
                {
                    if (inventoryItems.Count > 0)
                    {
                        var randomIndex = Random.Shared.Next(inventoryItems.Count);
                        dropItems.Add(inventoryItems.Values.ElementAt(randomIndex));
                    }
                }
            }
            else if (_pkValue >= PK_VALUE_PURPLE)
            {
                // 紫名死亡掉落少量物品
                var inventoryItems = _owner.Inventory.GetAllItems();
                if (inventoryItems.Count > 0)
                {
                    var randomIndex = Random.Shared.Next(inventoryItems.Count);
                    dropItems.Add(inventoryItems.Values.ElementAt(randomIndex));
                }
            }
            
            return dropItems;
        }
        
        /// <summary>
        /// 更新名字颜色
        /// </summary>
        private void UpdateNameColor()
        {
            // 发送名字颜色更新到周围玩家
            var builder = new PacketBuilder();
            builder.WriteUInt32(_owner.ObjectId);
            builder.WriteUInt16(0x285); // SM_NAMECOLORCHANGED
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteByte(GetNameColor());
            
            // 发送给周围玩家
            var packet = builder.Build();
            _owner.CurrentMap?.SendToNearbyPlayers(_owner.X, _owner.Y, packet);
        }
        
        /// <summary>
        /// 发送PK值变化消息
        /// </summary>
        private void SendPkValueChanged()
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(_owner.ObjectId);
            builder.WriteUInt16(0x286); // SM_PKVALUECHANGED
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(_pkValue);
            
            _owner.SendMessage(builder.Build());
        }
        
        /// <summary>
        /// 是否正当PK
        /// </summary>
        public void SetJustPk(bool justPk = true)
        {
            _justPk = justPk;
        }
        
        /// <summary>
        /// 是否处于正当防卫状态
        /// </summary>
        public bool IsSelfDefense()
        {
            return _isSelfDefense && (DateTime.Now - _lastSelfDefenseTime).TotalMinutes < 5;
        }
        
        /// <summary>
        /// 更新PK计时器
        /// </summary>
        public void Update()
        {
            // 每5分钟减少1点PK值
            if (_pkValue > 0 && (DateTime.Now - _lastPkTime).TotalMinutes >= PK_DECAY_MINUTES)
            {
                DecPkPoint(1);
                _lastPkTime = DateTime.Now;
            }
            
            // 清除刚刚PK标记（30秒后）
            if (_justPk && (DateTime.Now - _lastPkTime).TotalSeconds >= 30)
            {
                _justPk = false;
            }
            
            // 清除正当防卫标记（5分钟后）
            if (_isSelfDefense && (DateTime.Now - _lastSelfDefenseTime).TotalMinutes >= 5)
            {
                _isSelfDefense = false;
            }
        }
        
        /// <summary>
        /// 获取PK状态描述
        /// </summary>
        public string GetPkStatus()
        {
            if (_pkValue >= PK_VALUE_RED) return "红名（罪恶滔天）";
            if (_pkValue >= PK_VALUE_ORANGE) return "橙名（恶贯满盈）";
            if (_pkValue >= PK_VALUE_PURPLE) return "紫名（小有恶名）";
            return "白名（善良公民）";
        }
        
        /// <summary>
        /// 是否可以攻击目标
        /// </summary>
        public bool CanAttack(AliveObject target)
        {
            if (target is HumanPlayer targetPlayer)
            {
                // 检查是否同组
                if (_owner.GroupId != 0 && _owner.GroupId == targetPlayer.GroupId)
                    return false;
                    
                // 检查是否同公会
                if (_owner.Guild != null && _owner.Guild == targetPlayer.Guild)
                    return false;
                    
                // 检查目标是否处于保护状态（如新手保护）
                if (targetPlayer.Level < 10 && _owner.Level >= 10)
                {
                    _owner.Say("不能攻击新手玩家");
                    return false;
                }
                
                return true;
            }
            
            return true; // 可以攻击怪物
        }
    }
    
    /// <summary>
    /// 成就系统
    /// </summary>
    public class AchievementSystem
    {
        private readonly HumanPlayer _owner;
        private readonly Dictionary<uint, Achievement> _achievements = new();
        private readonly Dictionary<AchievementType, uint> _progress = new();
        
        public AchievementSystem(HumanPlayer owner)
        {
            _owner = owner;
            InitializeAchievements();
        }
        
        /// <summary>
        /// 初始化成就
        /// </summary>
        private void InitializeAchievements()
        {
            // 等级成就
            AddAchievement(new Achievement
            {
                Id = 1,
                Name = "初出茅庐",
                Description = "达到10级",
                Type = AchievementType.Level,
                TargetValue = 10,
                RewardExp = 1000,
                RewardGold = 1000
            });
            
            AddAchievement(new Achievement
            {
                Id = 2,
                Name = "小有所成",
                Description = "达到30级",
                Type = AchievementType.Level,
                TargetValue = 30,
                RewardExp = 5000,
                RewardGold = 5000
            });
            
            // 杀怪成就
            AddAchievement(new Achievement
            {
                Id = 101,
                Name = "怪物猎人",
                Description = "击杀100只怪物",
                Type = AchievementType.KillMonster,
                TargetValue = 100,
                RewardExp = 2000,
                RewardGold = 2000
            });
            
            // 物品成就
            AddAchievement(new Achievement
            {
                Id = 201,
                Name = "装备收集者",
                Description = "获得10件装备",
                Type = AchievementType.GetItem,
                TargetValue = 10,
                RewardExp = 1500,
                RewardGold = 1500
            });
        }
        
        /// <summary>
        /// 添加成就
        /// </summary>
        private void AddAchievement(Achievement achievement)
        {
            _achievements[achievement.Id] = achievement;
        }
        
        /// <summary>
        /// 更新成就进度
        /// </summary>
        public void UpdateProgress(AchievementType type, uint value = 1)
        {
            if (!_progress.ContainsKey(type))
            {
                _progress[type] = 0;
            }
            
            _progress[type] += value;
            CheckAchievements(type);
        }
        
        /// <summary>
        /// 检查成就完成
        /// </summary>
        private void CheckAchievements(AchievementType type)
        {
            var currentValue = _progress.ContainsKey(type) ? _progress[type] : 0;
            
            foreach (var achievement in _achievements.Values)
            {
                if (achievement.Type == type && !achievement.Completed && currentValue >= achievement.TargetValue)
                {
                    CompleteAchievement(achievement.Id);
                }
            }
        }
        
        /// <summary>
        /// 完成成就
        /// </summary>
        public bool CompleteAchievement(uint achievementId)
        {
            if (!_achievements.TryGetValue(achievementId, out var achievement) || achievement.Completed)
                return false;
            
            achievement.Completed = true;
            achievement.CompletedTime = DateTime.Now;
            
            // 发放奖励
            _owner.AddExp(achievement.RewardExp);
            _owner.AddGold(achievement.RewardGold);
            
            _owner.Say($"成就达成: {achievement.Name} - {achievement.Description}");
            _owner.Say($"获得奖励: {achievement.RewardExp}经验, {achievement.RewardGold}金币");
            
            // TODO: 保存到数据库
            return true;
        }
        
        /// <summary>
        /// 获取成就列表
        /// </summary>
        public List<Achievement> GetAchievements()
        {
            return _achievements.Values.ToList();
        }
        
        /// <summary>
        /// 获取成就进度
        /// </summary>
        public uint GetProgress(AchievementType type)
        {
            return _progress.TryGetValue(type, out var value) ? value : 0;
        }
    }
    
    /// <summary>
    /// 邮件系统
    /// </summary>
    public class MailSystem
    {
        private readonly HumanPlayer _owner;
        private readonly List<Mail> _mails = new();
        private readonly object _mailLock = new();
        
        public MailSystem(HumanPlayer owner)
        {
            _owner = owner;
        }
        
        /// <summary>
        /// 发送邮件
        /// </summary>
        public bool SendMail(string receiverName, string title, string content, List<ItemInstance>? attachments = null)
        {
            if (string.IsNullOrEmpty(receiverName) || string.IsNullOrEmpty(title))
            {
                _owner.Say("收件人或标题不能为空");
                return false;
            }
            
            // 检查收件人是否存在（通过玩家管理器）
            var receiver = HumanPlayerMgr.Instance.FindByName(receiverName);
            if (receiver == null)
            {
                _owner.Say($"玩家 {receiverName} 不存在或不在线");
                return false;
            }
            
            // 检查附件是否有效
            if (attachments != null && attachments.Count > 0)
            {
                // 检查附件数量限制
                if (attachments.Count > 5)
                {
                    _owner.Say("附件数量不能超过5个");
                    return false;
                }
                
                // 检查附件物品是否属于发送者
                foreach (var attachment in attachments)
                {
                    if (!_owner.Inventory.HasItem((ulong)attachment.InstanceId))
                    {
                        _owner.Say("附件物品不属于你");
                        return false;
                    }
                }
            }
            
            // 创建邮件对象
            var mail = new Mail
            {
                Id = GenerateMailId(),
                Sender = _owner.Name,
                Receiver = receiverName,
                Title = title,
                Content = content,
                SendTime = DateTime.Now,
                IsRead = false,
                Attachments = attachments,
                AttachmentsClaimed = false
            };
            
            // 保存邮件到数据库
            if (!SaveMailToDatabase(mail))
            {
                _owner.Say("邮件发送失败，数据库错误");
                return false;
            }
            
            // 从发送者背包移除附件物品
            if (attachments != null)
            {
                foreach (var attachment in attachments)
                {
                    _owner.Inventory.RemoveItem((ulong)attachment.InstanceId, 1);
                }
            }
            
            // 通知接收者有新邮件
            receiver.MailSystem.ReceiveMail(mail);
            
            // 发送邮件发送成功消息
            _owner.Say($"邮件已发送给 {receiverName}");
            
            // 记录日志
            Console.WriteLine($"{_owner.Name} 发送邮件给 {receiverName}，标题: {title}");
            
            return true;
        }
        
        /// <summary>
        /// 生成邮件ID
        /// </summary>
        private uint GenerateMailId()
        {
            // 使用时间戳生成唯一ID
            return (uint)DateTime.Now.Ticks;
        }
        
        /// <summary>
        /// 保存邮件到数据库
        /// </summary>
        private bool SaveMailToDatabase(Mail mail)
        {
            // 这里应该调用数据库接口保存邮件
            return true;
        }
        
        /// <summary>
        /// 接收邮件
        /// </summary>
        public void ReceiveMail(Mail mail)
        {
            lock (_mailLock)
            {
                _mails.Add(mail);
            }
            
            // 通知玩家有新邮件
            _owner.Say("你有新邮件");
        }
        
        /// <summary>
        /// 获取邮件列表
        /// </summary>
        public List<Mail> GetMails()
        {
            lock (_mailLock)
            {
                return new List<Mail>(_mails);
            }
        }
        
        /// <summary>
        /// 读取邮件
        /// </summary>
        public Mail? ReadMail(uint mailId)
        {
            lock (_mailLock)
            {
                var mail = _mails.FirstOrDefault(m => m.Id == mailId);
                if (mail != null && !mail.IsRead)
                {
                    mail.IsRead = true;
                    mail.ReadTime = DateTime.Now;
                }
                return mail;
            }
        }
        
        /// <summary>
        /// 删除邮件
        /// </summary>
        public bool DeleteMail(uint mailId)
        {
            lock (_mailLock)
            {
                var mail = _mails.FirstOrDefault(m => m.Id == mailId);
                if (mail == null)
                    return false;
                    
                _mails.Remove(mail);
                return true;
            }
        }
        
        /// <summary>
        /// 领取附件
        /// </summary>
        public bool ClaimAttachment(uint mailId)
        {
            lock (_mailLock)
            {
                var mail = _mails.FirstOrDefault(m => m.Id == mailId);
                if (mail == null || mail.Attachments == null || mail.Attachments.Count == 0)
                    return false;
                    
                if (mail.AttachmentsClaimed)
                {
                    _owner.Say("附件已领取");
                    return false;
                }
                
                // 尝试添加附件到背包
                foreach (var item in mail.Attachments)
                {
                    if (!_owner.Inventory.AddItem(item))
                    {
                        _owner.Say("背包空间不足");
                        return false;
                    }
                }
                
                mail.AttachmentsClaimed = true;
                mail.ClaimTime = DateTime.Now;
                _owner.Say("附件领取成功");
                return true;
            }
        }
    }
    
    /// <summary>
    /// 成就类型
    /// </summary>
    public enum AchievementType
    {
        Level,
        KillMonster,
        GetItem,
        CompleteQuest,
        JoinGuild,
        PvPKill,
        UseSkill,
        CraftItem
    }
    
    /// <summary>
    /// 成就
    /// </summary>
    public class Achievement
    {
        public uint Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public AchievementType Type { get; set; }
        public uint TargetValue { get; set; }
        public uint RewardExp { get; set; }
        public uint RewardGold { get; set; }
        public bool Completed { get; set; }
        public DateTime? CompletedTime { get; set; }
    }
    
    /// <summary>
    /// 邮件
    /// </summary>
    public class Mail
    {
        public uint Id { get; set; }
        public string Sender { get; set; } = string.Empty;
        public string Receiver { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime SendTime { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadTime { get; set; }
        public List<ItemInstance>? Attachments { get; set; }
        public bool AttachmentsClaimed { get; set; }
        public DateTime? ClaimTime { get; set; }
    }
}
