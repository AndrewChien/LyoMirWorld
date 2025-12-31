using System;
using System.Collections.Generic;
using MirCommon;
using MirCommon.Utils;

namespace GameServer
{
    /// <summary>
    /// 怪物扩展类
    /// </summary>
    public class MonsterEx : AliveObject
    {
        // 怪物类定义
        private MonsterClass? _desc;
        
        // 生成器引用
        private MonsterGen? _gen;
        
        // AI相关
        private DateTime _lastAITime;
        private DateTime _lastAttackTime;
        private DateTime _delTimer;
        
        // 怪物类型
        private byte _type;
        
        // 是否已切割
        private bool _cuted;
        
        // 是否前往目标点
        private bool _gotoPoint;
        private ushort _gotoX;
        private ushort _gotoY;
        
        // 更新键（用于UpdateFreeObjects）
        private uint _updateKey;

        public MonsterEx()
        {
            _lastAITime = DateTime.Now;
            _lastAttackTime = DateTime.Now;
            _delTimer = DateTime.Now;
            _type = 0;
            _cuted = false;
            _gotoPoint = false;
            _gotoX = 0;
            _gotoY = 0;
            _updateKey = 0;
        }

        /// <summary>
        /// 初始化怪物
        /// </summary>
        public bool Init(MonsterClass desc, int mapId, int x, int y, MonsterGen? gen = null)
        {
            if (desc == null)
                return false;

            _desc = desc;
            _gen = gen;
            
            // 设置基础属性
            Name = desc.Base.ViewName;
            Level = desc.Base.Level;
            
            // 设置生命值和魔法值
            MaxHP = desc.Prop.HP;
            CurrentHP = MaxHP;
            MaxMP = desc.Prop.MP;
            CurrentMP = MaxMP;
            
            // 设置属性
            Stats.MinDC = desc.Prop.DC1;
            Stats.MaxDC = desc.Prop.DC2;
            Stats.MinAC = desc.Prop.AC1;
            Stats.MaxAC = desc.Prop.AC2;
            Stats.MinMAC = desc.Prop.MAC1;
            Stats.MaxMAC = desc.Prop.MAC2;
            Stats.MinMC = desc.Prop.MC1;
            Stats.MaxMC = desc.Prop.MC2;
            Stats.Accuracy = desc.Prop.Hit;
            
            // 设置位置
            CurrentMap = MapManager.Instance.GetMap((uint)mapId);
            if (CurrentMap == null)
                return false;
                
            X = (ushort)x;
            Y = (ushort)y;
            
            // 添加到地图
            if (!CurrentMap.AddObject(this, X, Y))
                return false;
            
            // 设置怪物类型
            _type = 0;
            
            // 执行出生脚本
            if (!string.IsNullOrEmpty(desc.BornScript))
            {
                ExecuteScript(desc.BornScript);
            }
            
            Console.WriteLine($"怪物 {Name} 在 ({x},{y}) 创建");
            return true;
        }

        /// <summary>
        /// 清理怪物
        /// </summary>
        public new void Clean()
        {
            // 从地图移除
            if (CurrentMap != null)
            {
                CurrentMap.RemoveObject(this);
            }
            
            // 清理生成器引用
            ClearGen();
            
            // 清理脚本引用
            _desc = null;
            
            Console.WriteLine($"怪物 {Name} 被清理");
        }

        /// <summary>
        /// 清理生成器引用
        /// </summary>
        public void ClearGen()
        {
            if (_gen != null)
            {
                _gen.CurrentCount--;
                _gen = null;
            }
        }

        /// <summary>
        /// 设置删除计时器
        /// </summary>
        public void SetDelTimer()
        {
            _delTimer = DateTime.Now;
        }

        /// <summary>
        /// 检查删除计时器是否超时
        /// </summary>
        public bool IsDelTimerTimeOut(int timeout)
        {
            return (DateTime.Now - _delTimer).TotalMilliseconds >= timeout;
        }

        /// <summary>
        /// 获取更新键
        /// </summary>
        public uint GetUpdateKey()
        {
            return _updateKey;
        }

        /// <summary>
        /// 设置ID
        /// </summary>
        public void SetId(uint id)
        {
            ObjectId = id;
        }

        /// <summary>
        /// 获取地图
        /// </summary>
        public LogicMap? GetMap()
        {
            return CurrentMap;
        }

        /// <summary>
        /// 检查是否死亡
        /// </summary>
        public bool IsDeath()
        {
            return IsDead;
        }

        /// <summary>
        /// 更新怪物
        /// </summary>
        public override void Update()
        {
            base.Update();
            
            if (IsDead)
                return;
            
            // 更新AI
            if ((DateTime.Now - _lastAITime).TotalMilliseconds >= _desc?.Prop.AIDelay)
            {
                UpdateAI();
                _lastAITime = DateTime.Now;
            }
            
            // 更新恢复
            UpdateRecover();
            
            // 更新更新键
            _updateKey = (uint)Environment.TickCount;
        }

        /// <summary>
        /// 更新AI
        /// </summary>
        private void UpdateAI()
        {
            if (_desc == null || CurrentMap == null)
                return;
            
            // 根据AI设置更新怪物行为
            switch (_desc.AISet.MoveStyle)
            {
                case 0: // 站立不动
                    break;
                case 1: // 随机移动
                    RandomMove();
                    break;
                case 2: // 追击目标
                    ChaseTarget();
                    break;
                case 3: // 逃跑
                    Escape();
                    break;
            }
            
            // 检查攻击
            if ((DateTime.Now - _lastAttackTime).TotalMilliseconds >= _desc.AttackDesc.Delay)
            {
                CheckAttack();
                _lastAttackTime = DateTime.Now;
            }
        }

        /// <summary>
        /// 随机移动
        /// </summary>
        private void RandomMove()
        {
            if (CurrentAction != ActionType.Stand)
                return;
                
            var dir = (Direction)Random.Shared.Next(8);
            Walk(dir);
        }

        /// <summary>
        /// 追击目标
        /// </summary>
        private void ChaseTarget()
        {
            var target = GetTarget();
            if (target == null || target.IsDead || target.CurrentMap != CurrentMap)
                return;
                
            int distance = Math.Abs(X - target.X) + Math.Abs(Y - target.Y);
            
            if (distance <= _desc.AttackDesc.AttackDistance)
            {
                // 在攻击范围内，停止移动
                return;
            }
            
            // 追击目标
            if (CurrentAction == ActionType.Stand)
            {
                var dir = GetDirection(X, Y, target.X, target.Y);
                Walk(dir);
            }
        }

        /// <summary>
        /// 逃跑
        /// </summary>
        private void Escape()
        {
            var target = GetTarget();
            if (target == null || target.IsDead || target.CurrentMap != CurrentMap)
                return;
                
            int distance = Math.Abs(X - target.X) + Math.Abs(Y - target.Y);
            
            if (distance > _desc.AISet.EscapeDistance)
            {
                // 超出逃跑距离，停止逃跑
                return;
            }
            
            // 逃跑（远离目标）
            if (CurrentAction == ActionType.Stand)
            {
                var dir = GetDirection(target.X, target.Y, X, Y);
                Run(dir);
            }
        }

        /// <summary>
        /// 检查攻击
        /// </summary>
        private void CheckAttack()
        {
            var target = GetTarget();
            if (target == null || target.IsDead || target.CurrentMap != CurrentMap)
                return;
                
            int distance = Math.Abs(X - target.X) + Math.Abs(Y - target.Y);
            
            if (distance <= _desc.AttackDesc.AttackDistance)
            {
                // 执行攻击
                AttackTarget(target);
            }
        }

        /// <summary>
        /// 攻击目标
        /// </summary>
        private void AttackTarget(AliveObject target)
        {
            if (target == null || target.IsDead)
                return;
                
            // 计算伤害
            int damage = Random.Shared.Next(Stats.MinDC, Stats.MaxDC + 1);
            
            // 应用攻击效果
            target.BeAttack(this, damage, DamageType.Physics);
            
            // 执行攻击脚本
            if (!string.IsNullOrEmpty(_desc.GotTargetScript))
            {
                ExecuteScript(_desc.GotTargetScript);
            }
            
            // 检查是否击杀目标
            if (target.IsDead && !string.IsNullOrEmpty(_desc.KillTargetScript))
            {
                ExecuteScript(_desc.KillTargetScript);
            }
        }

        /// <summary>
        /// 更新恢复
        /// </summary>
        private void UpdateRecover()
        {
            if (_desc == null)
                return;
                
            // 恢复生命值
            if (CurrentHP < MaxHP && _desc.Prop.RecoverHP > 0)
            {
                CurrentHP = Math.Min(MaxHP, CurrentHP + _desc.Prop.RecoverHP);
            }
            
            // 恢复魔法值
            if (CurrentMP < MaxMP && _desc.Prop.RecoverMP > 0)
            {
                CurrentMP = Math.Min(MaxMP, CurrentMP + _desc.Prop.RecoverMP);
            }
        }

        /// <summary>
        /// 执行脚本
        /// </summary>
        private void ExecuteScript(string scriptName)
        {
            if (string.IsNullOrEmpty(scriptName))
                return;
            
            // 获取脚本对象
            var scriptObject = ScriptObjectMgr.Instance.GetScriptObject(scriptName);
            if (scriptObject == null)
            {
                Console.WriteLine($"怪物 {Name} 执行脚本失败: 脚本 {scriptName} 不存在");
                return;
            }
            
            // 执行脚本
            Console.WriteLine($"怪物 {Name} 执行脚本: {scriptName}");
            
            // 解析并执行脚本内容
            ExecuteScriptContent(scriptObject);
        }
        
        /// <summary>
        /// 执行脚本内容
        /// </summary>
        private void ExecuteScriptContent(ScriptObject scriptObject)
        {
            if (scriptObject == null)
                return;
                
            foreach (var line in scriptObject.Lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("//"))
                    continue;
                    
                // 解析脚本命令
                ParseAndExecuteCommand(trimmedLine);
            }
        }
        
        /// <summary>
        /// 解析并执行命令
        /// </summary>
        private void ParseAndExecuteCommand(string command)
        {
            // 简单的命令解析
            var parts = command.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return;
                
            var cmd = parts[0].ToLower();
            
            switch (cmd)
            {
                case "say":
                    if (parts.Length > 1)
                    {
                        string message = string.Join(" ", parts, 1, parts.Length - 1);
                        Say(message);
                    }
                    break;
                    
                case "move":
                    if (parts.Length >= 3 && ushort.TryParse(parts[1], out ushort x) && ushort.TryParse(parts[2], out ushort y))
                    {
                        MoveTo(x, y);
                    }
                    break;
                    
                case "attack":
                    if (parts.Length > 1)
                    {
                        string targetName = parts[1];
                        AttackTargetByName(targetName);
                    }
                    break;
                    
                case "spawn":
                    if (parts.Length >= 4 && int.TryParse(parts[1], out int monsterId) && 
                        ushort.TryParse(parts[2], out ushort spawnX) && ushort.TryParse(parts[3], out ushort spawnY))
                    {
                        SpawnMonster(monsterId, spawnX, spawnY);
                    }
                    break;
                    
                case "teleport":
                    if (parts.Length >= 3 && ushort.TryParse(parts[1], out ushort teleX) && ushort.TryParse(parts[2], out ushort teleY))
                    {
                        Teleport(teleX, teleY);
                    }
                    break;
                    
                case "setvar":
                    if (parts.Length >= 3)
                    {
                        string varName = parts[1];
                        string varValue = parts[2];
                        SetVariable(varName, varValue);
                    }
                    break;
                    
                case "call":
                    if (parts.Length > 1)
                    {
                        string subScriptName = parts[1];
                        ExecuteScript(subScriptName);
                    }
                    break;
                    
                default:
                    Console.WriteLine($"怪物 {Name} 未知脚本命令: {command}");
                    break;
            }
        }
        
        /// <summary>
        /// 移动到指定位置
        /// </summary>
        private void MoveTo(ushort x, ushort y)
        {
            if (CurrentMap == null)
                return;
                
            // 设置目标点
            _gotoPoint = true;
            _gotoX = x;
            _gotoY = y;
            
            Console.WriteLine($"怪物 {Name} 移动到 ({x},{y})");
        }
        
        /// <summary>
        /// 按名称攻击目标
        /// </summary>
        private void AttackTargetByName(string targetName)
        {
            if (CurrentMap == null)
                return;
                
            var players = CurrentMap.GetPlayersInRange(X, Y, 10); // 使用默认视野范围
            var target = players.FirstOrDefault(p => p.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));
            
            if (target != null)
            {
                SetTarget(target);
                Console.WriteLine($"怪物 {Name} 攻击目标: {targetName}");
            }
        }
        
        /// <summary>
        /// 刷怪
        /// </summary>
        private void SpawnMonster(int monsterId, ushort x, ushort y)
        {
            if (CurrentMap == null)
                return;
                
            var monster = MonsterManagerEx.Instance.CreateMonster(monsterId);
            if (monster != null)
            {
                monster.SetId(MonsterManagerEx.Instance.GetNextObjectId());
                monster.Init(MonsterManagerEx.Instance.GetMonsterClass(monsterId), (int)CurrentMap.MapId, x, y);
                Console.WriteLine($"怪物 {Name} 刷出怪物 {monsterId} 在 ({x},{y})");
            }
        }
        
        /// <summary>
        /// 传送
        /// </summary>
        private void Teleport(ushort x, ushort y)
        {
            if (CurrentMap == null)
                return;
                
            CurrentMap.MoveObject(this, x, y);
            Console.WriteLine($"怪物 {Name} 传送到 ({x},{y})");
        }
        
        /// <summary>
        /// 设置变量
        /// </summary>
        private void SetVariable(string varName, string varValue)
        {
            // TODO: 实现变量存储
            Console.WriteLine($"怪物 {Name} 设置变量 {varName} = {varValue}");
        }

        /// <summary>
        /// 获取怪物类描述
        /// </summary>
        public MonsterClass? GetDesc()
        {
            return _desc;
        }

        /// <summary>
        /// 获取生成器
        /// </summary>
        public MonsterGen? GetGen()
        {
            return _gen;
        }

        /// <summary>
        /// 设置生成器
        /// </summary>
        public void SetGen(MonsterGen? gen)
        {
            _gen = gen;
        }

        /// <summary>
        /// 获取怪物类型
        /// </summary>
        public byte GetSType()
        {
            return _type;
        }

        /// <summary>
        /// 设置怪物类型
        /// </summary>
        public void SetSType(byte type)
        {
            _type = type;
        }

        /// <summary>
        /// 获取对象类型
        /// </summary>
        public override ObjectType GetObjectType()
        {
            return ObjectType.Monster;
        }

        /// <summary>
        /// 受到攻击
        /// </summary>
        protected override void OnDamaged(AliveObject attacker, int damage, DamageType damageType)
        {
            base.OnDamaged(attacker, damage, damageType);
            
            // 执行受伤脚本
            if (_desc != null && !string.IsNullOrEmpty(_desc.HurtScript))
            {
                ExecuteScript(_desc.HurtScript);
            }
        }

        /// <summary>
        /// 死亡
        /// </summary>
        protected override void OnDeath(AliveObject killer)
        {
            base.OnDeath(killer);
            
            // 执行死亡脚本
            if (_desc != null && !string.IsNullOrEmpty(_desc.DeathScript))
            {
                ExecuteScript(_desc.DeathScript);
            }
            
            Console.WriteLine($"怪物 {Name} 被 {killer?.Name ?? "未知"} 击杀");
        }

        /// <summary>
        /// 获取怪物显示消息
        /// </summary>
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
            if (_desc != null)
            {
                BitConverter.GetBytes(_desc.Base.MonsterId).CopyTo(featureData, 0); // 怪物ID
            }
            else
            {
                BitConverter.GetBytes(0).CopyTo(featureData, 0); // 默认怪物ID
            }
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
}
