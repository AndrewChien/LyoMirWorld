using System;
using System.Collections.Generic;
using MirCommon;

namespace GameServer
{
    /// <summary>
    /// 战斗实体接口
    /// </summary>
    public interface ICombatEntity
    {
        uint Id { get; }
        string Name { get; }
        int CurrentHP { get; set; }
        int MaxHP { get; set; }
        int CurrentMP { get; set; }
        int MaxMP { get; set; }
        byte Level { get; set; }
        CombatStats Stats { get; set; }
        bool IsDead { get; set; }
        BuffManager BuffManager { get; }
        int X { get; set; }
        int Y { get; set; }

        void Heal(int amount);
        void RestoreMP(int amount);
        bool ConsumeMP(int amount);
        bool TakeDamage(ICombatEntity attacker, int damage, DamageType damageType);
        CombatResult Attack(ICombatEntity target, DamageType damageType = DamageType.Physics);
    }

    /// <summary>
    /// 伤害类型
    /// </summary>
    public enum DamageType
    {
        Physics = 0,    // 物理伤害
        Magic = 1,      // 魔法伤害
        Poison = 2,     // 毒素伤害
    }

    /// <summary>
    /// 战斗结果
    /// </summary>
    public class CombatResult
    {
        public bool Hit { get; set; }           // 是否命中
        public int Damage { get; set; }         // 伤害值
        public bool Critical { get; set; }      // 是否暴击
        public DamageType DamageType { get; set; }
        public bool TargetDied { get; set; }    // 目标是否死亡
    }

    /// <summary>
    /// 攻击记录
    /// </summary>
    public class AttackRecord
    {
        public uint AttackerId { get; set; }
        public int TotalDamage { get; set; }
        public int HitCount { get; set; }
        public DateTime LastAttackTime { get; set; }

        public AttackRecord(uint attackerId)
        {
            AttackerId = attackerId;
            TotalDamage = 0;
            HitCount = 0;
            LastAttackTime = DateTime.Now;
        }

        public void AddDamage(int damage)
        {
            TotalDamage += damage;
            HitCount++;
            LastAttackTime = DateTime.Now;
        }
    }


    /// <summary>
    /// 战斗实体（可以战斗的对象基类）
    /// </summary>
    public abstract class CombatEntity : ICombatEntity
    {
        public uint Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int CurrentHP { get; set; }
        public int MaxHP { get; set; }
        public int CurrentMP { get; set; }
        public int MaxMP { get; set; }
        public byte Level { get; set; }
        public CombatStats Stats { get; set; }
        public bool IsDead { get; set; }
        public int X { get; set; }
        public int Y { get; set; }

        // 攻击记录
        protected Dictionary<uint, AttackRecord> _attackRecords = new();
        protected readonly object _recordLock = new();

        // 当前目标
        protected uint _targetId = 0;
        
        // Buff管理器
        public BuffManager BuffManager { get; private set; }
        int ICombatEntity.X { get => X; set => X = value; }
        int ICombatEntity.Y { get => Y; set => Y = value; }

        public CombatEntity()
        {
            Stats = new CombatStats();
            Level = 1;
            MaxHP = 100;
            CurrentHP = 100;
            MaxMP = 100;
            CurrentMP = 100;
            BuffManager = new BuffManager(this);
        }

        /// <summary>
        /// 执行攻击
        /// </summary>
        public virtual CombatResult Attack(ICombatEntity target, DamageType damageType = DamageType.Physics)
        {
            var result = new CombatResult
            {
                DamageType = damageType,
                Hit = false,
                Damage = 0,
                Critical = false
            };

            // 命中判定
            if (!CheckHit(target))
            {
                return result;
            }

            result.Hit = true;

            // 计算伤害
            int baseDamage = CalculateDamage(damageType);
            
            // 暴击判定
            if (CheckCritical())
            {
                result.Critical = true;
                baseDamage = (int)(baseDamage * 1.5f);
            }

            // 应用防御
            int finalDamage = ApplyDefence(target, baseDamage, damageType);
            result.Damage = Math.Max(1, finalDamage); // 至少造成1点伤害

            // 对目标造成伤害
            result.TargetDied = target.TakeDamage(this, result.Damage, damageType);

            // 记录攻击
            RecordAttack(target.Id, result.Damage);

            return result;
        }

        /// <summary>
        /// 受到伤害
        /// </summary>
        public virtual bool TakeDamage(ICombatEntity attacker, int damage, DamageType damageType)
        {
            if (IsDead) return false;

            CurrentHP -= damage;
            
            if (CurrentHP <= 0)
            {
                CurrentHP = 0;
                IsDead = true;
                OnDeath(attacker);
                return true;
            }

            OnDamaged(attacker, damage, damageType);
            return false;
        }

        /// <summary>
        /// 命中判定
        /// 其中hitrate是攻击者的命中值，escape是目标的闪避值
        /// </summary>
        protected virtual bool CheckHit(ICombatEntity target)
        {
            int hitRate = Stats.Accuracy;
            int escape = target.Stats.Agility;
            
            if (escape <= 0)
                return true;  // 目标没有闪避值，必定命中
            
            int minEscape = Math.Max(1, escape / 15);
            int randomEscape = Random.Shared.Next(minEscape, escape + 1);
            
            return hitRate >= randomEscape;
        }

        /// <summary>
        /// 暴击判定
        /// </summary>
        protected virtual bool CheckCritical()
        {
            // 基础暴击率
            int baseCritRate = Stats.CriticalRate;
            
            // 幸运值影响暴击率（每点幸运增加0.5%暴击率）
            int luckyBonus = Stats.Lucky / 2;
            
            // 总暴击率
            int totalCritRate = baseCritRate + luckyBonus;
            
            // 限制暴击率范围（0-50%）
            totalCritRate = Math.Clamp(totalCritRate, 0, 50);
            
            // 诅咒值降低暴击率（每点诅咒降低1%暴击率）
            if (Stats.Curse > 0)
            {
                totalCritRate -= Stats.Curse;
                totalCritRate = Math.Max(0, totalCritRate);
            }
            
            return Random.Shared.Next(100) < totalCritRate;
        }

        /// <summary>
        /// 计算基础伤害
        /// </summary>
        protected virtual int CalculateDamage(DamageType damageType)
        {
            int minDamage, maxDamage;
            
            switch (damageType)
            {
                case DamageType.Magic:
                    minDamage = Stats.MinMC;
                    maxDamage = Stats.MaxMC;
                    // 魔法伤害受魔法力影响
                    if (Stats.Lucky > 0)
                    {
                        // 幸运值增加最小魔法伤害
                        minDamage += Stats.Lucky / 3;
                    }
                    break;
                case DamageType.Physics:
                default:
                    minDamage = Stats.MinDC;
                    maxDamage = Stats.MaxDC;
                    // 物理伤害受攻击力影响
                    if (Stats.Lucky > 0)
                    {
                        // 幸运值增加最小物理伤害
                        minDamage += Stats.Lucky / 2;
                    }
                    break;
                case DamageType.Poison:
                    // 毒素伤害受道术力影响
                    minDamage = Stats.MinSC;
                    maxDamage = Stats.MaxSC;
                    if (Stats.Lucky > 0)
                    {
                        minDamage += Stats.Lucky / 4;
                    }
                    break;
            }

            // 确保最小伤害不超过最大伤害
            if (minDamage > maxDamage)
                minDamage = maxDamage;
                
            // 在最小和最大伤害之间随机
            if (minDamage == maxDamage)
                return minDamage;
                
            return Random.Shared.Next(minDamage, maxDamage + 1);
        }

        /// <summary>
        /// 应用防御
        /// </summary>
        protected virtual int ApplyDefence(ICombatEntity target, int damage, DamageType damageType)
        {
            int defence = 0;
            
            switch (damageType)
            {
                case DamageType.Magic:
                    // 魔法防御在最小和最大魔防之间随机
                    if (target.Stats.MinMAC < target.Stats.MaxMAC)
                        defence = Random.Shared.Next(target.Stats.MinMAC, target.Stats.MaxMAC + 1);
                    else
                        defence = target.Stats.MinMAC;
                    break;
                    
                case DamageType.Physics:
                    // 物理防御在最小和最大防御之间随机
                    if (target.Stats.MinAC < target.Stats.MaxAC)
                        defence = Random.Shared.Next(target.Stats.MinAC, target.Stats.MaxAC + 1);
                    else
                        defence = target.Stats.MinAC;
                    break;
                    
                case DamageType.Poison:
                    // 毒素伤害受毒抗性影响
                    defence = target.Stats.PoisonResistance / 10; // 每10%毒抗提供1点防御
                    break;
                    
                default:
                    defence = 0;
                    break;
            }
            
            // 应用伤害减免百分比
            float damageReduction = 0;
            switch (damageType)
            {
                case DamageType.Magic:
                    damageReduction = target.Stats.MagicResistance / 100.0f;
                    break;
                case DamageType.Physics:
                    damageReduction = target.Stats.PhysicalResistance / 100.0f;
                    break;
                case DamageType.Poison:
                    damageReduction = target.Stats.PoisonResistance / 100.0f;
                    break;
            }
            
            // 先应用固定防御值，再应用百分比减免
            int damageAfterDefence = Math.Max(0, damage - defence);
            int finalDamage = (int)(damageAfterDefence * (1.0f - damageReduction));
            
            // 至少造成1点伤害（如果原始伤害大于0）
            if (damage > 0 && finalDamage <= 0)
                finalDamage = 1;
                
            return finalDamage;
        }

        /// <summary>
        /// 记录攻击
        /// </summary>
        protected void RecordAttack(uint targetId, int damage)
        {
            lock (_recordLock)
            {
                if (!_attackRecords.TryGetValue(targetId, out var record))
                {
                    record = new AttackRecord(Id);
                    _attackRecords[targetId] = record;
                }
                record.AddDamage(damage);
            }
        }

        /// <summary>
        /// 获取攻击记录
        /// </summary>
        public AttackRecord? GetAttackRecord(uint targetId)
        {
            lock (_recordLock)
            {
                return _attackRecords.TryGetValue(targetId, out var record) ? record : null;
            }
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
        /// 治疗
        /// </summary>
        public virtual void Heal(int amount)
        {
            if (IsDead) return;
            
            CurrentHP = Math.Min(CurrentHP + amount, MaxHP);
        }

        /// <summary>
        /// 恢复魔法
        /// </summary>
        public virtual void RestoreMP(int amount)
        {
            if (IsDead) return;
            
            CurrentMP = Math.Min(CurrentMP + amount, MaxMP);
        }

        /// <summary>
        /// 消耗魔法
        /// </summary>
        public virtual bool ConsumeMP(int amount)
        {
            if (CurrentMP < amount) return false;
            
            CurrentMP -= amount;
            return true;
        }

        // 事件回调
        protected virtual void OnDeath(ICombatEntity killer) { }
        protected virtual void OnDamaged(ICombatEntity attacker, int damage, DamageType damageType) { }
        protected virtual void OnKilledTarget(ICombatEntity target) { }
    }

    /// <summary>
    /// 战斗系统管理器
    /// </summary>
    public class CombatSystemManager
    {
        private static CombatSystemManager? _instance;
        public static CombatSystemManager Instance => _instance ??= new CombatSystemManager();

        private CombatSystemManager() { }

        /// <summary>
        /// 执行战斗
        /// </summary>
        public CombatResult ExecuteCombat(ICombatEntity attacker, ICombatEntity target, DamageType damageType = DamageType.Physics)
        {
            if (attacker.IsDead || target.IsDead)
            {
                return new CombatResult { Hit = false };
            }

            var result = attacker.Attack(target, damageType);
            
            if (result.Hit)
            {
                LogCombat(attacker, target, result);
            }

            return result;
        }

        /// <summary>
        /// 范围攻击
        /// </summary>
        public List<CombatResult> ExecuteAreaAttack(ICombatEntity attacker, List<ICombatEntity> targets, DamageType damageType = DamageType.Physics)
        {
            var results = new List<CombatResult>();
            
            foreach (var target in targets)
            {
                if (target.IsDead) continue;
                
                var result = ExecuteCombat(attacker, target, damageType);
                results.Add(result);
            }

            return results;
        }

        /// <summary>
        /// 记录战斗日志
        /// </summary>
        private void LogCombat(ICombatEntity attacker, ICombatEntity target, CombatResult result)
        {
            string msg = $"{attacker.Name} 攻击 {target.Name} ";
            
            if (result.Hit)
            {
                msg += $"命中! 造成 {result.Damage} 点";
                msg += result.DamageType == DamageType.Magic ? "魔法" : "物理";
                msg += "伤害";
                
                if (result.Critical)
                {
                    msg += " (暴击!)";
                }

                if (result.TargetDied)
                {
                    msg += $" {target.Name} 已死亡!";
                }
            }
            else
            {
                msg += "未命中!";
            }

            // 可以通过日志系统输出
            Console.WriteLine($"[战斗] {msg}");
        }

        /// <summary>
        /// 计算经验值
        /// </summary>
        public int CalculateExp(ICombatEntity killer, ICombatEntity target)
        {
            // 基础经验
            int baseExp = target.Level * 10;
            
            // 等级差修正
            int levelDiff = target.Level - killer.Level;
            float modifier = 1.0f + (levelDiff * 0.1f);
            modifier = Math.Clamp(modifier, 0.1f, 2.0f);

            return (int)(baseExp * modifier);
        }
    }
}
