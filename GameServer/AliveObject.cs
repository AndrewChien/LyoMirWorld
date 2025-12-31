using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using MirCommon;
using MirCommon.Utils;

namespace GameServer
{
    public abstract class AliveObject : MapObject, ICombatEntity
    {
        // 基本属性
        public string Name { get; set; } = "NONAME";
        public byte Level { get; set; } = 1;
        public int CurrentHP { get; set; }
        public int MaxHP { get; set; }
        public int CurrentMP { get; set; }
        public int MaxMP { get; set; }
        public int X { get; set; }
        public int Y { get; set; }

        // 移动相关
        public Direction CurrentDirection { get; set; }
        public ActionType CurrentAction { get; set; }
        public ushort ActionX { get; set; }
        public ushort ActionY { get; set; }
        public Direction ActionDirection { get; set; }
        public byte WalkSpeed { get; set; } = 2;
        public byte RunSpeed { get; set; } = 2;
        
        // 状态
        public bool IsDead { get; set; }
        public bool IsHidden { get; set; }
        public bool CanMove { get; set; } = true;
        
        // 战斗属性
        public CombatStats Stats { get; set; }
        public BuffManager BuffManager { get; private set; }
        
        // ICombatEntity 接口实现
        uint ICombatEntity.Id => ObjectId;
        string ICombatEntity.Name => Name;
        int ICombatEntity.CurrentHP { get => CurrentHP; set => CurrentHP = value; }
        int ICombatEntity.MaxHP { get => MaxHP; set => MaxHP = value; }
        int ICombatEntity.CurrentMP { get => CurrentMP; set => CurrentMP = value; }
        int ICombatEntity.MaxMP { get => MaxMP; set => MaxMP = value; }
        byte ICombatEntity.Level { get => Level; set => Level = value; }
        CombatStats ICombatEntity.Stats { get => Stats; set => Stats = value; }
        bool ICombatEntity.IsDead { get => IsDead; set => IsDead = value; }
        BuffManager ICombatEntity.BuffManager => BuffManager;

        int ICombatEntity.X { get => X; set => X = value; }
        int ICombatEntity.Y { get => Y; set => Y = value; }

        // 可见对象列表
        private readonly ConcurrentDictionary<uint, VisibleObject> _visibleObjects = new();
        private uint _visibleObjectUpdateFlag = 0;
        
        // 对象引用
        protected ObjectReference<AliveObject> _targetRef = new();
        protected ObjectReference<AliveObject> _hitterRef = new();
        protected ObjectReference<AliveObject> _ownerRef = new();
        
        // 攻击记录
        protected Dictionary<uint, AttackRecord> _attackRecords = new();
        protected readonly object _recordLock = new();
        
        // 进程队列
        private readonly Queue<ObjectProcess> _processQueue = new();
        private readonly object _processLock = new();
        
        // 定时器
        private DateTime _actionCompleteTime;
        private DateTime _lastHpRecoverTime;
        private DateTime _lastMpRecoverTime;

        protected AliveObject()
        {
            Stats = new CombatStats();
            BuffManager = new BuffManager(this as ICombatEntity);
            CurrentDirection = Direction.Down;
            CurrentAction = ActionType.Stand;
            MaxHP = 100;
            CurrentHP = 100;
            MaxMP = 100;
            CurrentMP = 100;
            _lastHpRecoverTime = DateTime.Now;
            _lastMpRecoverTime = DateTime.Now;
        }

        public override void Update()
        {
            base.Update();
            
            // 更新Buff
            BuffManager.Update();
            
            // 处理进程队列
            ProcessQueue();
            
            // 自动恢复
            AutoRecover();
            
            // 清理过期攻击记录
            CleanupOldRecords(TimeSpan.FromMinutes(5));
        }

        #region 移动相关

        /// <summary>
        /// 行走
        /// </summary>
        public virtual bool Walk(Direction dir, uint delay = 0)
        {
            if (!CanDoAction(ActionType.Walk))
                return false;

            var (newX, newY) = GetNextPosition(X, Y, dir);
            return WalkXY((ushort)newX, (ushort)newY, delay);
        }

        /// <summary>
        /// 行走到指定位置
        /// </summary>
        public virtual bool WalkXY(ushort x, ushort y, uint delay = 0)
        {
            if (CurrentMap == null || !CurrentMap.CanWalk(x, y))
                return false;

            var dir = GetDirection(X, Y, x, y);
            if (!SetAction(ActionType.Walk, dir, x, y, delay))
                return false;

            return true;
        }

        /// <summary>
        /// 奔跑
        /// </summary>
        public virtual bool Run(Direction dir, uint delay = 0)
        {
            if (!CanDoAction(ActionType.Run))
                return false;

            var (x1, y1) = GetNextPosition(X, Y, dir);
            var (newX, newY) = GetNextPosition(x1, y1, dir);
            return RunXY((ushort)newX, (ushort)newY, delay);
        }

        /// <summary>
        /// 奔跑到指定位置
        /// </summary>
        public virtual bool RunXY(ushort x, ushort y, uint delay = 0)
        {
            if (CurrentMap == null || !CurrentMap.CanWalk(x, y))
                return false;

            var dir = GetDirection(X, Y, x, y);
            if (!SetAction(ActionType.Run, dir, x, y, delay))
                return false;

            return true;
        }

        /// <summary>
        /// 转向
        /// </summary>
        public virtual bool Turn(Direction dir)
        {
            CurrentDirection = dir;
            ActionDirection = dir;
            return true;
        }

        /// <summary>
        /// 攻击
        /// </summary>
        public virtual bool Attack(Direction dir, uint delay = 0)
        {
            if (!CanDoAction(ActionType.Attack))
                return false;

            return SetAction(ActionType.Attack, dir, (ushort)X, (ushort)Y, delay);
        }

        /// <summary>
        /// 设置动作
        /// </summary>
        protected virtual bool SetAction(ActionType action, Direction dir, ushort x, ushort y, uint delay)
        {
            if (!CanDoAction(action))
                return false;

            CurrentAction = action;
            ActionDirection = dir;
            CurrentDirection = dir;
            ActionX = x;
            ActionY = y;
            
            uint actionTime = GetActionTime(action);
            _actionCompleteTime = DateTime.Now.AddMilliseconds(actionTime + delay);

            OnDoAction(action);
            return true;
        }

        /// <summary>
        /// 完成动作
        /// </summary>
        public virtual bool CompleteAction()
        {
            if (DateTime.Now < _actionCompleteTime)
                return false;

            switch (CurrentAction)
            {
                case ActionType.Walk:
                case ActionType.Run:
                    if (CurrentMap != null)
                    {
                        CurrentMap.MoveObject(this, ActionX, ActionY);
                    }
                    break;
            }

            CurrentAction = ActionType.Stand;
            return true;
        }

        /// <summary>
        /// 检查是否可以执行动作
        /// </summary>
        public virtual bool CanDoAction(ActionType action)
        {
            if (IsDead)
                return false;

            if (DateTime.Now < _actionCompleteTime)
                return false;

            if (action == ActionType.Walk || action == ActionType.Run)
            {
                if (!CanMove)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 获取动作时间
        /// </summary>
        protected virtual uint GetActionTime(ActionType action)
        {
            switch (action)
            {
                case ActionType.Walk:
                    return (uint)(1000 / WalkSpeed);
                case ActionType.Run:
                    return (uint)(1000 / RunSpeed);
                case ActionType.Attack:
                    return 500;
                default:
                    return 0;
            }
        }

        #endregion

        #region 战斗相关

        /// <summary>
        /// 受到攻击
        /// </summary>
        public virtual bool BeAttack(AliveObject attacker, int damage, DamageType damageType = DamageType.Physics)
        {
            if (IsDead)
                return false;

            return TakeDamage(attacker as ICombatEntity, damage, damageType);
        }

        /// <summary>
        /// 受到伤害 (ICombatEntity接口实现)
        /// </summary>
        public virtual bool TakeDamage(ICombatEntity attacker, int damage, DamageType damageType)
        {
            if (IsDead)
                return false;

            CurrentHP -= damage;
            
            if (CurrentHP <= 0)
            {
                CurrentHP = 0;
                IsDead = true;
                OnDeath(attacker as AliveObject);
                return true;
            }

            OnDamaged(attacker as AliveObject, damage, damageType);
            return false;
        }

        /// <summary>
        /// 受到伤害 (AliveObject版本，用于内部调用)
        /// </summary>
        public virtual bool TakeDamage(AliveObject attacker, int damage, DamageType damageType)
        {
            return TakeDamage(attacker as ICombatEntity, damage, damageType);
        }

        /// <summary>
        /// 造成伤害
        /// </summary>
        public virtual bool Damage(uint hitterId, int value)
        {
            return TakeDamage((ICombatEntity)null!, value, DamageType.Physics);
        }
        
        /// <summary>
        /// Attack method for ICombatEntity interface
        /// </summary>
        public virtual CombatResult Attack(ICombatEntity target, DamageType damageType = DamageType.Physics)
        {
            // 简单实现，子类可以覆盖
            var result = new CombatResult
            {
                DamageType = damageType,
                Hit = true,
                Damage = 0,
                Critical = false
            };

            int baseDamage = damageType == DamageType.Magic ? Stats.MinMC : Stats.MinDC;
            result.Damage = Math.Max(1, baseDamage);
            result.TargetDied = target.TakeDamage(this as ICombatEntity, result.Damage, damageType);

            return result;
        }

        /// <summary>
        /// 治疗
        /// </summary>
        public virtual void Heal(int amount)
        {
            if (IsDead)
                return;
            
            CurrentHP = Math.Min(CurrentHP + amount, MaxHP);
            SendHpMpChanged();
        }

        /// <summary>
        /// 恢复魔法
        /// </summary>
        public virtual void RestoreMP(int amount)
        {
            if (IsDead)
                return;
            
            CurrentMP = Math.Min(CurrentMP + amount, MaxMP);
            SendHpMpChanged();
        }

        /// <summary>
        /// 消耗魔法
        /// </summary>
        public virtual bool ConsumeMP(int amount)
        {
            if (CurrentMP < amount)
                return false;
            
            CurrentMP -= amount;
            SendHpMpChanged();
            return true;
        }

        /// <summary>
        /// 死亡
        /// </summary>
        public virtual void ToDeath(uint killerId = 0)
        {
            if (IsDead)
                return;

            IsDead = true;
            CurrentHP = 0;
            CurrentAction = ActionType.Die;
            
            OnDeath(null!);
        }

        #endregion

        #region 视野管理

        /// <summary>
        /// 更新视野范围
        /// </summary>
        public virtual void UpdateViewRange(int oldX, int oldY)
        {
            if (CurrentMap == null)
                return;

            int viewRange = 18; // 视野范围
            
            // 获取新视野内的对象
            var newObjects = CurrentMap.GetObjectsInRange(X, Y, viewRange);
            
            // 获取旧视野内的对象
            var oldObjects = CurrentMap.GetObjectsInRange(oldX, oldY, viewRange);

            // 找出进入视野的对象
            foreach (var obj in newObjects)
            {
                if (obj.ObjectId == ObjectId)
                    continue;

                if (!_visibleObjects.ContainsKey(obj.ObjectId))
                {
                    AddVisibleObject(obj);
                }
            }

            // 找出离开视野的对象
            foreach (var obj in oldObjects)
            {
                if (!newObjects.Contains(obj))
                {
                    RemoveVisibleObject(obj);
                }
            }
        }

        /// <summary>
        /// 添加可见对象
        /// </summary>
        public virtual void AddVisibleObject(MapObject obj)
        {
            var visObj = new VisibleObject
            {
                Object = obj,
                UpdateFlag = _visibleObjectUpdateFlag++
            };

            if (_visibleObjects.TryAdd(obj.ObjectId, visObj))
            {
                obj.AddRef();
                OnObjectEnterView(obj);
            }
        }

        /// <summary>
        /// 移除可见对象
        /// </summary>
        public virtual void RemoveVisibleObject(MapObject obj)
        {
            if (_visibleObjects.TryRemove(obj.ObjectId, out var visObj))
            {
                OnObjectLeaveView(obj);
                obj.DecRef();
            }
        }

        /// <summary>
        /// 搜索视野范围
        /// </summary>
        public virtual void SearchViewRange()
        {
            if (CurrentMap == null)
                return;

            var objects = CurrentMap.GetObjectsInRange(X, Y, 18);
            foreach (var obj in objects)
            {
                if (obj.ObjectId != ObjectId && !_visibleObjects.ContainsKey(obj.ObjectId))
                {
                    AddVisibleObject(obj);
                }
            }
        }

        /// <summary>
        /// 清理可见对象列表
        /// </summary>
        public virtual void CleanVisibleList()
        {
            foreach (var kvp in _visibleObjects.ToArray())
            {
                if (kvp.Value.Object != null)
                {
                    kvp.Value.Object.DecRef();
                }
                _visibleObjects.TryRemove(kvp.Key, out _);
            }
        }

        #endregion

        #region 消息发送

        /// <summary>
        /// 发送消息给自己
        /// </summary>
        public virtual void SendMessage(byte[] message)
        {
            // 子类实现
        }

        /// <summary>
        /// 发送消息给周围对象
        /// </summary>
        public virtual void SendAroundMsg(int v, int v1, byte[] message)
        {
            if (CurrentMap == null)
                return;

            CurrentMap.BroadcastMessageInRange(X, Y, 18, message);
        }

        /// <summary>
        /// 发送消息给地图所有对象
        /// </summary>
        public virtual void SendMapMsg(byte[] message)
        {
            CurrentMap?.BroadcastMessage(message);
        }

        /// <summary>
        /// 说话
        /// </summary>
        public virtual void Say(string message)
        {
            LogManager.Default.Info($"[{Name}]: {message}");
            // 发送聊天消息给周围玩家
            // 构建聊天消息包并广播给视野范围内的玩家
            // 在完整实现中，需要使用PacketBuilder构建SM_CHAT消息
            // 然后通过SendAroundMsg发送给周围玩家
        }

        /// <summary>
        /// 发送HP/MP变化
        /// </summary>
        protected virtual void SendHpMpChanged()
        {
            // 子类实现
        }

        #endregion

        #region 进程处理

        /// <summary>
        /// 添加进程
        /// </summary>
        public virtual bool AddProcess(ProcessType type, uint param1 = 0, uint param2 = 0, 
            uint param3 = 0, uint param4 = 0, uint delay = 0, int repeatTimes = 0, string? stringParam = null)
        {
            var process = new ObjectProcess(type)
            {
                Param1 = param1,
                Param2 = param2,
                Param3 = param3,
                Param4 = param4,
                Delay = delay,
                RepeatTimes = repeatTimes,
                StringParam = stringParam
            };

            return AddProcess(process);
        }

        /// <summary>
        /// 添加进程
        /// </summary>
        public virtual bool AddProcess(ObjectProcess process)
        {
            lock (_processLock)
            {
                _processQueue.Enqueue(process);
                return true;
            }
        }

        /// <summary>
        /// 处理进程队列
        /// </summary>
        protected virtual void ProcessQueue()
        {
            lock (_processLock)
            {
                while (_processQueue.Count > 0)
                {
                    var process = _processQueue.Peek();
                    
                    if (!process.ShouldExecute())
                        break;

                    _processQueue.Dequeue();
                    DoProcess(process);

                    if (process.RepeatTimes > 0)
                    {
                        process.RepeatTimes--;
                        process.ExecuteTime = DateTime.Now;
                        _processQueue.Enqueue(process);
                    }
                }
            }
        }

        /// <summary>
        /// 执行进程
        /// </summary>
        protected virtual void DoProcess(ObjectProcess process)
        {
            switch (process.Type)
            {
                case ProcessType.TakeDamage:
                    TakeDamage(null!, (int)process.Param1, (DamageType)process.Param2);
                    break;
                case ProcessType.Heal:
                    Heal((int)process.Param1);
                    break;
                case ProcessType.Die:
                    ToDeath(process.Param1);
                    break;
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 自动恢复
        /// </summary>
        protected virtual void AutoRecover()
        {
            if (IsDead || CurrentAction != ActionType.Stand)
                return;

            var now = DateTime.Now;

            // HP恢复
            if ((now - _lastHpRecoverTime).TotalMilliseconds >= 5000) // 5秒恢复一次
            {
                int recoverHp = GetAutoRecoverHp();
                if (recoverHp > 0 && CurrentHP < MaxHP)
                {
                    Heal(recoverHp);
                }
                _lastHpRecoverTime = now;
            }

            // MP恢复
            if ((now - _lastMpRecoverTime).TotalMilliseconds >= 5000)
            {
                int recoverMp = GetAutoRecoverMp();
                if (recoverMp > 0 && CurrentMP < MaxMP)
                {
                    RestoreMP(recoverMp);
                }
                _lastMpRecoverTime = now;
            }
        }

        /// <summary>
        /// 获取自动恢复HP量
        /// </summary>
        protected virtual int GetAutoRecoverHp()
        {
            return MaxHP / 50; // 2%恢复
        }

        /// <summary>
        /// 获取自动恢复MP量
        /// </summary>
        protected virtual int GetAutoRecoverMp()
        {
            return MaxMP / 50; // 2%恢复
        }

        /// <summary>
        /// 清理过期攻击记录
        /// </summary>
        public void CleanupOldRecords(TimeSpan timeout)
        {
            lock (_recordLock)
            {
                var now = DateTime.Now;
                var toRemove = new List<uint>();
                
                foreach (var kvp in _attackRecords)
                {
                    if (now - kvp.Value.LastAttackTime > timeout)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }

                foreach (var id in toRemove)
                {
                    _attackRecords.Remove(id);
                }
            }
        }

        /// <summary>
        /// 获取方向上的下一个位置
        /// </summary>
        protected (int x, int y) GetNextPosition(int x, int y, Direction dir)
        {
            switch (dir)
            {
                case Direction.Up: return (x, y - 1);
                case Direction.UpRight: return (x + 1, y - 1);
                case Direction.Right: return (x + 1, y);
                case Direction.DownRight: return (x + 1, y + 1);
                case Direction.Down: return (x, y + 1);
                case Direction.DownLeft: return (x - 1, y + 1);
                case Direction.Left: return (x - 1, y);
                case Direction.UpLeft: return (x - 1, y - 1);
                default: return (x, y);
            }
        }

        /// <summary>
        /// 获取两点之间的方向
        /// </summary>
        protected Direction GetDirection(int x1, int y1, int x2, int y2)
        {
            int dx = x2 - x1;
            int dy = y2 - y1;

            if (dx == 0 && dy < 0) return Direction.Up;
            if (dx > 0 && dy < 0) return Direction.UpRight;
            if (dx > 0 && dy == 0) return Direction.Right;
            if (dx > 0 && dy > 0) return Direction.DownRight;
            if (dx == 0 && dy > 0) return Direction.Down;
            if (dx < 0 && dy > 0) return Direction.DownLeft;
            if (dx < 0 && dy == 0) return Direction.Left;
            if (dx < 0 && dy < 0) return Direction.UpLeft;
            
            return Direction.Down;
        }

        #endregion

        #region 对象引用

        /// <summary>
        /// 设置目标
        /// </summary>
        public virtual void SetTarget(AliveObject? target)
        {
            var old = _targetRef.GetObject();
            _targetRef.SetObject(target);
            OnChangeTarget(old, target);
        }

        /// <summary>
        /// 获取目标
        /// </summary>
        public virtual AliveObject? GetTarget() => _targetRef.GetObject();

        /// <summary>
        /// 设置攻击者
        /// </summary>
        public virtual void SetHitter(AliveObject? hitter)
        {
            var old = _hitterRef.GetObject();
            _hitterRef.SetObject(hitter);
            OnChangeHitter(old, hitter);
        }

        /// <summary>
        /// 获取攻击者
        /// </summary>
        public virtual AliveObject? GetHitter() => _hitterRef.GetObject();

        /// <summary>
        /// 设置主人
        /// </summary>
        public virtual void SetOwner(AliveObject? owner)
        {
            var old = _ownerRef.GetObject();
            _ownerRef.SetObject(owner);
            OnChangeOwner(old, owner);
        }

        /// <summary>
        /// 获取主人
        /// </summary>
        public virtual AliveObject? GetOwner() => _ownerRef.GetObject();

        #endregion

        #region 事件回调

        protected virtual void OnDeath(AliveObject killer) { }
        protected virtual void OnDamaged(AliveObject attacker, int damage, DamageType damageType) { }
        protected virtual void OnKilledTarget(AliveObject target) { }
        protected virtual void OnDoAction(ActionType action) { }
        protected virtual void OnChangeTarget(AliveObject? old, AliveObject? newTarget) { }
        protected virtual void OnChangeHitter(AliveObject? old, AliveObject? newHitter) { }
        protected virtual void OnChangeOwner(AliveObject? old, AliveObject? newOwner) { }
        public virtual void OnObjectEnterView(MapObject obj) { }
        public virtual void OnObjectLeaveView(MapObject obj) { }

        #endregion

        public override bool GetViewMsg(out byte[] msg, MapObject? viewer = null)
        {
            // 子类实现具体的消息构建
            msg = Array.Empty<byte>();
            return false;
        }
    }

    /// <summary>
    /// 动作类型
    /// </summary>
    public enum ActionType
    {
        None = -1,      // 无动作
        Stand = 0,      // 站立
        Walk = 1,       // 行走
        Run = 2,        // 奔跑
        Attack = 3,     // 攻击
        Hit = 4,        // 被击
        Die = 5,        // 死亡
        Spell = 6,      // 施法
        Sit = 7,        // 坐下
        Mining = 8,     // 挖矿
        GetMeat = 9,    // 挖肉
        Max = 10,
        SpellCast = 11,
        Pickup = 12,
        Drop = 13,
        UseItem = 14,
        Equip = 15,
        UnEquip = 16,
        Trade = 17,
        Shop = 18,
        Repair = 19,
        TrainHorse = 20
    }
}
