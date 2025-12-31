using System;
using System.Collections.Generic;
using System.Linq;
using MirCommon;
using MirCommon.Utils;

namespace GameServer
{
    /// <summary>
    /// 怪物对象 - 带AI的怪物系统
    /// </summary>
    public class Monster : AliveObject
    {
        // 怪物ID和配置
        public int MonsterId { get; set; }
        public MonsterDefinition Definition { get; private set; }
        
        // 奖励
        public uint ExpReward { get; set; }
        public uint GoldReward { get; set; }
        
        // AI相关
        public MonsterAIType AIType { get; set; }
        public AIState CurrentAIState { get; set; }
        private ushort _homeX;
        private ushort _homeY;
        public int WanderRange { get; set; } = 10;
        
        // 攻击相关
        private DateTime _lastAttackTime;
        private DateTime _lastThinkTime;
        public int AttackRange { get; set; } = 1;
        public int ViewRange { get; set; } = 10;
        
        // 重生
        public bool CanRespawn { get; set; } = true;
        public int RespawnTime { get; set; } = 60; // 秒
        public DateTime DeathTime { get; set; }
        
        // 宠物相关
        public uint OwnerPlayerId { get; set; }
        public bool IsPet { get; set; }
        
        // 仇恨列表
        private readonly Dictionary<uint, int> _hatredList = new();
        private readonly object _hatredLock = new();

        public Monster(int monsterId, string name)
        {
            MonsterId = monsterId;
            Name = name;
            
            // 从定义加载属性
            Definition = MonsterManager.Instance.GetDefinition(monsterId) 
                ?? new MonsterDefinition(monsterId, name);
            
            LoadFromDefinition();
            
            AIType = MonsterAIType.Passive;
            CurrentAIState = AIState.Idle;
            _lastThinkTime = DateTime.Now;
            _lastAttackTime = DateTime.Now;
        }

        private void LoadFromDefinition()
        {
            Level = Definition.Level;
            MaxHP = Definition.HP;
            CurrentHP = MaxHP;
            MaxMP = Definition.MP;
            CurrentMP = MaxMP;
            
            Stats.MinDC = Definition.MinDC;
            Stats.MaxDC = Definition.MaxDC;
            Stats.MinAC = Definition.AC;
            Stats.MaxAC = Definition.AC + 5;
            Stats.MinMAC = Definition.MAC;
            Stats.MaxMAC = Definition.MAC + 5;
            Stats.Accuracy = Definition.Accuracy;
            Stats.Agility = Definition.Agility;
            
            ExpReward = Definition.ExpReward;
            GoldReward = Definition.GoldReward;
            AttackRange = Definition.AttackRange;
            ViewRange = Definition.ViewRange;
            WalkSpeed = Definition.WalkSpeed;
            RunSpeed = Definition.RunSpeed;
            AIType = Definition.AIType;
            WanderRange = Definition.WanderRange;
        }

        public override ObjectType GetObjectType() => ObjectType.Monster;

        /// <summary>
        /// 设置出生点
        /// </summary>
        public void SetHomePosition(ushort x, ushort y)
        {
            _homeX = x;
            _homeY = y;
        }

        public override void Update()
        {
            base.Update();
            
            if (IsDead)
            {
                // 检查是否可以重生
                if (CanRespawnNow())
                {
                    Respawn();
                }
                return;
            }

            // AI思考
            if ((DateTime.Now - _lastThinkTime).TotalMilliseconds >= 500) // 每0.5秒思考一次
            {
                Think();
                _lastThinkTime = DateTime.Now;
            }
        }

        #region AI系统

        /// <summary>
        /// AI思考
        /// </summary>
        private void Think()
        {
            // 根据AI类型和状态进行思考
            switch (CurrentAIState)
            {
                case AIState.Idle:
                    ThinkIdle();
                    break;
                case AIState.Wander:
                    ThinkWander();
                    break;
                case AIState.Chase:
                    ThinkChase();
                    break;
                case AIState.Attack:
                    ThinkAttack();
                    break;
                case AIState.Return:
                    ThinkReturn();
                    break;
            }
        }

        /// <summary>
        /// 待机思考
        /// </summary>
        private void ThinkIdle()
        {
            // 查找目标
            var target = FindTarget();
            if (target != null)
            {
                SetTarget(target);
                CurrentAIState = AIState.Chase;
                return;
            }

            // 随机巡逻
            if (AIType != MonsterAIType.Passive && Random.Shared.Next(100) < 10) // 10%概率开始巡逻
            {
                CurrentAIState = AIState.Wander;
            }
        }

        /// <summary>
        /// 巡逻思考
        /// </summary>
        private void ThinkWander()
        {
            // 查找目标
            var target = FindTarget();
            if (target != null)
            {
                SetTarget(target);
                CurrentAIState = AIState.Chase;
                return;
            }

            // 如果不在动作中，随机移动
            if (CurrentAction == ActionType.Stand)
            {
                // 检查是否离家太远
                int distanceFromHome = Math.Abs(X - _homeX) + Math.Abs(Y - _homeY);
                if (distanceFromHome > WanderRange)
                {
                    CurrentAIState = AIState.Return;
                    return;
                }

                // 随机移动
                var dir = (Direction)Random.Shared.Next(8);
                Walk(dir);
            }
        }

        /// <summary>
        /// 追击思考
        /// </summary>
        private void ThinkChase()
        {
            var target = GetTarget();
            if (target == null || target.IsDead || target.CurrentMap != CurrentMap)
            {
                SetTarget(null);
                CurrentAIState = AIState.Return;
                return;
            }

            // 检查是否离家太远
            int distanceFromHome = Math.Abs(X - _homeX) + Math.Abs(Y - _homeY);
            if (distanceFromHome > WanderRange * 2)
            {
                SetTarget(null);
                CurrentAIState = AIState.Return;
                return;
            }

            int distance = Math.Abs(X - target.X) + Math.Abs(Y - target.Y);

            // 在攻击范围内，切换到攻击状态
            if (distance <= AttackRange)
            {
                CurrentAIState = AIState.Attack;
                return;
            }

            // 追击目标
            if (CurrentAction == ActionType.Stand)
            {
                var dir = GetDirection(X, Y, target.X, target.Y);
                
                // 尝试奔跑
                if (distance > 3 && CanDoAction(ActionType.Run))
                {
                    Run(dir);
                }
                else
                {
                    Walk(dir);
                }
            }
        }

        /// <summary>
        /// 攻击思考
        /// </summary>
        private void ThinkAttack()
        {
            var target = GetTarget();
            if (target == null || target.IsDead)
            {
                SetTarget(null);
                CurrentAIState = AIState.Return;
                return;
            }

            int distance = Math.Abs(X - target.X) + Math.Abs(Y - target.Y);

            // 目标超出攻击范围，继续追击
            if (distance > AttackRange)
            {
                CurrentAIState = AIState.Chase;
                return;
            }

            // 攻击
            if (CurrentAction == ActionType.Stand)
            {
                var now = DateTime.Now;
                if ((now - _lastAttackTime).TotalMilliseconds >= 1000) // 1秒攻击一次
                {
                    var dir = GetDirection(X, Y, target.X, target.Y);
                    if (Attack(dir))
                    {
                        // 执行实际攻击
                        DoAttackTarget(target);
                        _lastAttackTime = now;
                    }
                }
            }
        }

        /// <summary>
        /// 返回思考
        /// </summary>
        private void ThinkReturn()
        {
            // 已经回到家了
            if (X == _homeX && Y == _homeY)
            {
                CurrentAIState = AIState.Idle;
                
                // 清理仇恨
                ClearHatred();
                
                // 回满血蓝
                CurrentHP = MaxHP;
                CurrentMP = MaxMP;
                return;
            }

            // 返回出生点
            if (CurrentAction == ActionType.Stand)
            {
                var dir = GetDirection(X, Y, _homeX, _homeY);
                Run(dir);
            }
        }

        /// <summary>
        /// 查找目标
        /// </summary>
        private AliveObject? FindTarget()
        {
            if (CurrentMap == null)
                return null;

            // 被动型怪物不主动攻击
            if (AIType == MonsterAIType.Passive)
            {
                // 但会攻击攻击过它的玩家
                var topHatred = GetTopHatredTarget();
                if (topHatred != null)
                    return topHatred;
                return null;
            }

            // 查找范围内的玩家
            var players = CurrentMap.GetPlayersInRange(X, Y, ViewRange);
            
            // 优先攻击仇恨最高的
            var hatredTarget = GetTopHatredTarget();
            if (hatredTarget != null && players.Contains(hatredTarget as HumanPlayer))
                return hatredTarget;

            // 否则攻击最近的
            HumanPlayer? nearest = null;
            int minDistance = int.MaxValue;
            
            foreach (var player in players)
            {
                if (player.IsDead)
                    continue;

                int distance = Math.Abs(X - player.X) + Math.Abs(Y - player.Y);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = player;
                }
            }

            return nearest;
        }

        /// <summary>
        /// 执行攻击
        /// </summary>
        private void DoAttackTarget(AliveObject target)
        {
            if (target == null || target.IsDead)
                return;

            // 计算伤害
            int damage = Random.Shared.Next(Stats.MinDC, Stats.MaxDC + 1);
            
            // 命中判定
            if (CheckHit(target))
            {
                target.BeAttack(this, damage, DamageType.Physics);
                
                if (target.IsDead)
                {
                    OnKilledTarget(target);
                }
            }
        }

        /// <summary>
        /// 命中判定
        /// </summary>
        private bool CheckHit(AliveObject target)
        {
            int hitRate = Stats.Accuracy + Level;
            int dodgeRate = target.Stats.Agility + target.Level;
            
            int hitChance = 70 + (hitRate - dodgeRate) * 2;
            hitChance = Math.Clamp(hitChance, 30, 95);

            return Random.Shared.Next(100) < hitChance;
        }

        #endregion

        #region 仇恨系统

        /// <summary>
        /// 增加仇恨
        /// </summary>
        public void AddHatred(uint objectId, int value)
        {
            lock (_hatredLock)
            {
                if (!_hatredList.ContainsKey(objectId))
                {
                    _hatredList[objectId] = 0;
                }
                _hatredList[objectId] += value;
            }
        }

        /// <summary>
        /// 获取仇恨最高的目标
        /// </summary>
        private AliveObject? GetTopHatredTarget()
        {
            lock (_hatredLock)
            {
                if (_hatredList.Count == 0)
                    return null;

                var topEntry = _hatredList.OrderByDescending(kvp => kvp.Value).First();
                
                // 从地图查找对象
                if (CurrentMap != null)
                {
                    var players = CurrentMap.GetPlayersInRange(X, Y, ViewRange * 2);
                    return players.FirstOrDefault(p => p.ObjectId == topEntry.Key);
                }

                return null;
            }
        }

        /// <summary>
        /// 清理仇恨
        /// </summary>
        private void ClearHatred()
        {
            lock (_hatredLock)
            {
                _hatredList.Clear();
            }
        }

        #endregion

        #region 重生系统

        /// <summary>
        /// 检查是否可以重生
        /// </summary>
        public bool CanRespawnNow()
        {
            if (!CanRespawn || !IsDead)
                return false;

            return (DateTime.Now - DeathTime).TotalSeconds >= RespawnTime;
        }

        /// <summary>
        /// 重生
        /// </summary>
        public void Respawn()
        {
            IsDead = false;
            CurrentHP = MaxHP;
            CurrentMP = MaxMP;
            CurrentAction = ActionType.Stand;
            CurrentAIState = AIState.Idle;
            
            // 回到出生点
            if (CurrentMap != null)
            {
                CurrentMap.MoveObject(this, _homeX, _homeY);
            }
            else
            {
                X = _homeX;
                Y = _homeY;
            }
            
            // 清理仇恨
            ClearHatred();
            
            LogManager.Default.Debug($"怪物 {Name} 在 ({_homeX},{_homeY}) 重生");
        }

        #endregion

        #region 事件回调

        protected override void OnDeath(AliveObject killer)
        {
            base.OnDeath(killer);
            
            DeathTime = DateTime.Now;
            CurrentAIState = AIState.Idle;
            
            LogManager.Default.Info($"怪物 {Name} 被 {killer?.Name ?? "未知"} 击杀");
        }

        protected override void OnDamaged(AliveObject attacker, int damage, DamageType damageType)
        {
            base.OnDamaged(attacker, damage, damageType);
            
            // 增加仇恨
            if (attacker != null)
            {
                AddHatred(attacker.ObjectId, damage);
                
                // 如果是被动怪物，被攻击后会反击
                if (AIType == MonsterAIType.Passive && CurrentAIState == AIState.Idle)
                {
                    SetTarget(attacker);
                    CurrentAIState = AIState.Chase;
                }
            }
        }

        #endregion

        public override bool GetViewMsg(out byte[] msg, MapObject? viewer = null)
        {
            // 构建怪物显示消息
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(ProtocolCmd.SM_APPEAR);
            builder.WriteUInt16((ushort)X);
            builder.WriteUInt16((ushort)Y);
            builder.WriteUInt16((ushort)CurrentDirection); // 方向
            
            // 怪物特征数据
            byte[] featureData = new byte[12];
            BitConverter.GetBytes(MonsterId).CopyTo(featureData, 0); // 怪物ID
            BitConverter.GetBytes((ushort)Level).CopyTo(featureData, 4); // 等级
            BitConverter.GetBytes((ushort)CurrentHP).CopyTo(featureData, 6); // 当前HP
            BitConverter.GetBytes((ushort)MaxHP).CopyTo(featureData, 8); // 最大HP
            BitConverter.GetBytes((ushort)(IsDead ? 1 : 0)).CopyTo(featureData, 10); // 状态
            
            builder.WriteBytes(featureData);
            builder.WriteString(Name);
            
            msg = builder.Build();
            return true;
        }
    }

    /// <summary>
    /// 怪物定义
    /// </summary>
    public class MonsterDefinition
    {
        public int MonsterId { get; set; }
        public string Name { get; set; }
        public byte Level { get; set; }
        public int HP { get; set; }
        public int MP { get; set; }
        public int MinDC { get; set; }
        public int MaxDC { get; set; }
        public int AC { get; set; }
        public int MAC { get; set; }
        public int Accuracy { get; set; }
        public int Agility { get; set; }
        public uint ExpReward { get; set; }
        public uint GoldReward { get; set; }
        public int AttackRange { get; set; } = 1;
        public int ViewRange { get; set; } = 10;
        public byte WalkSpeed { get; set; } = 2;
        public byte RunSpeed { get; set; } = 2;
        public MonsterAIType AIType { get; set; } = MonsterAIType.Active;
        public int WanderRange { get; set; } = 10;

        public MonsterDefinition(int monsterId, string name)
        {
            MonsterId = monsterId;
            Name = name;
            Level = 1;
            HP = 100;
            MP = 0;
            MinDC = 1;
            MaxDC = 3;
            AC = 0;
            MAC = 0;
            Accuracy = 5;
            Agility = 5;
            ExpReward = 10;
            GoldReward = 5;
        }
    }

    /// <summary>
    /// 怪物AI类型
    /// </summary>
    public enum MonsterAIType
    {
        Passive = 0,    // 被动：被攻击才反击
        Active = 1,     // 主动：主动攻击
        Guard = 2,      // 守卫：固定位置守卫
        Boss = 3        // BOSS：特殊AI
    }

    /// <summary>
    /// AI状态
    /// </summary>
    public enum AIState
    {
        Idle = 0,       // 待机
        Wander = 1,     // 巡逻
        Chase = 2,      // 追击
        Attack = 3,     // 攻击
        Return = 4,     // 返回
        Flee = 5        // 逃跑
    }

    /// <summary>
    /// 怪物管理器
    /// </summary>
    public class MonsterManager
    {
        private static MonsterManager? _instance;
        public static MonsterManager Instance => _instance ??= new MonsterManager();

        private readonly Dictionary<int, MonsterDefinition> _definitions = new();
        private readonly object _lock = new();

        private MonsterManager()
        {
            InitializeDefaultMonsters();
        }

        /// <summary>
        /// 初始化默认怪物
        /// </summary>
        private void InitializeDefaultMonsters()
        {
            // 1级怪物 - 鸡
            AddDefinition(new MonsterDefinition(1, "鸡")
            {
                Level = 1,
                HP = 15,
                MinDC = 1,
                MaxDC = 2,
                ExpReward = 5,
                GoldReward = 1,
                AIType = MonsterAIType.Passive,
                WalkSpeed = 3
            });

            // 5级怪物 - 鹿
            AddDefinition(new MonsterDefinition(2, "鹿")
            {
                Level = 5,
                HP = 80,
                MinDC = 3,
                MaxDC = 6,
                AC = 2,
                ExpReward = 20,
                GoldReward = 5,
                AIType = MonsterAIType.Passive
            });

            // 10级怪物 - 森林雪人
            AddDefinition(new MonsterDefinition(3, "森林雪人")
            {
                Level = 10,
                HP = 200,
                MinDC = 8,
                MaxDC = 15,
                AC = 5,
                MAC = 3,
                ExpReward = 50,
                GoldReward = 15,
                AIType = MonsterAIType.Active
            });

            // 15级怪物 - 骷髅
            AddDefinition(new MonsterDefinition(4, "骷髅")
            {
                Level = 15,
                HP = 350,
                MinDC = 12,
                MaxDC = 20,
                AC = 8,
                MAC = 5,
                ExpReward = 100,
                GoldReward = 25,
                AIType = MonsterAIType.Active
            });

            // 20级怪物 - 骷髅战士
            AddDefinition(new MonsterDefinition(5, "骷髅战士")
            {
                Level = 20,
                HP = 600,
                MinDC = 18,
                MaxDC = 30,
                AC = 12,
                MAC = 8,
                ExpReward = 200,
                GoldReward = 50,
                AIType = MonsterAIType.Active
            });

            LogManager.Default.Info($"已加载 {_definitions.Count} 个怪物定义");
        }

        /// <summary>
        /// 添加定义
        /// </summary>
        public void AddDefinition(MonsterDefinition definition)
        {
            lock (_lock)
            {
                _definitions[definition.MonsterId] = definition;
            }
        }

        /// <summary>
        /// 获取定义
        /// </summary>
        public MonsterDefinition? GetDefinition(int monsterId)
        {
            lock (_lock)
            {
                return _definitions.TryGetValue(monsterId, out var def) ? def : null;
            }
        }

        /// <summary>
        /// 创建怪物
        /// </summary>
        public Monster? CreateMonster(int monsterId)
        {
            var definition = GetDefinition(monsterId);
            if (definition == null)
                return null;

            return new Monster(monsterId, definition.Name);
        }

        /// <summary>
        /// 在地图刷怪
        /// </summary>
        public Monster? SpawnMonster(int monsterId, LogicMap map, ushort x, ushort y)
        {
            var monster = CreateMonster(monsterId);
            if (monster == null)
                return null;

            monster.SetHomePosition(x, y);
            
            if (map.AddObject(monster, x, y))
            {
                return monster;
            }

            return null;
        }
    }
}
