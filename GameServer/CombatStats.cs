using System;

namespace GameServer
{
    /// <summary>
    /// 战斗属性类
    /// </summary>
    public class CombatStats
    {
        /// <summary>
        /// 当前生命值
        /// </summary>
        public int HP { get; set; }
        
        /// <summary>
        /// 最大生命值
        /// </summary>
        public int MaxHP { get; set; }
        
        /// <summary>
        /// 当前魔法值
        /// </summary>
        public int MP { get; set; }
        
        /// <summary>
        /// 最大魔法值
        /// </summary>
        public int MaxMP { get; set; }
        
        /// <summary>
        /// 攻击力下限
        /// </summary>
        public int MinDC { get; set; }
        
        /// <summary>
        /// 攻击力上限
        /// </summary>
        public int MaxDC { get; set; }
        
        /// <summary>
        /// 魔法力下限
        /// </summary>
        public int MinMC { get; set; }
        
        /// <summary>
        /// 魔法力上限
        /// </summary>
        public int MaxMC { get; set; }
        
        /// <summary>
        /// 道术力下限
        /// </summary>
        public int MinSC { get; set; }
        
        /// <summary>
        /// 道术力上限
        /// </summary>
        public int MaxSC { get; set; }
        
        /// <summary>
        /// 防御力下限
        /// </summary>
        public int MinAC { get; set; }
        
        /// <summary>
        /// 防御力上限
        /// </summary>
        public int MaxAC { get; set; }
        
        /// <summary>
        /// 魔防力下限
        /// </summary>
        public int MinMAC { get; set; }
        
        /// <summary>
        /// 魔防力上限
        /// </summary>
        public int MaxMAC { get; set; }
        
        /// <summary>
        /// 准确
        /// </summary>
        public int Accuracy { get; set; }
        
        /// <summary>
        /// 敏捷
        /// </summary>
        public int Agility { get; set; }
        
        /// <summary>
        /// 幸运
        /// </summary>
        public int Lucky { get; set; }
        
        /// <summary>
        /// 诅咒
        /// </summary>
        public int Curse { get; set; }
        
        /// <summary>
        /// 攻击速度
        /// </summary>
        public int AttackSpeed { get; set; }
        
        /// <summary>
        /// 施法速度
        /// </summary>
        public int CastSpeed { get; set; }
        
        /// <summary>
        /// 移动速度
        /// </summary>
        public int MoveSpeed { get; set; }
        
        /// <summary>
        /// 暴击率（百分比）
        /// </summary>
        public int CriticalRate { get; set; }
        
        /// <summary>
        /// 暴击伤害（百分比）
        /// </summary>
        public int CriticalDamage { get; set; }
        
        /// <summary>
        /// 闪避率（百分比）
        /// </summary>
        public int DodgeRate { get; set; }
        
        /// <summary>
        /// 命中率（百分比）
        /// </summary>
        public int HitRate { get; set; }
        
        /// <summary>
        /// 物理伤害减免（百分比）
        /// </summary>
        public int PhysicalResistance { get; set; }
        
        /// <summary>
        /// 魔法伤害减免（百分比）
        /// </summary>
        public int MagicResistance { get; set; }
        
        /// <summary>
        /// 毒抗性（百分比）
        /// </summary>
        public int PoisonResistance { get; set; }
        
        /// <summary>
        /// 冰冻抗性（百分比）
        /// </summary>
        public int FreezeResistance { get; set; }
        
        /// <summary>
        /// 眩晕抗性（百分比）
        /// </summary>
        public int StunResistance { get; set; }
        
        /// <summary>
        /// 沉默抗性（百分比）
        /// </summary>
        public int SilenceResistance { get; set; }
        
        /// <summary>
        /// 生命恢复速度（每5秒）
        /// </summary>
        public int HPRegen { get; set; }
        
        /// <summary>
        /// 魔法恢复速度（每5秒）
        /// </summary>
        public int MPRegen { get; set; }
        
        /// <summary>
        /// 经验加成（百分比）
        /// </summary>
        public int ExpBonus { get; set; }
        
        /// <summary>
        /// 掉落加成（百分比）
        /// </summary>
        public int DropBonus { get; set; }
        
        /// <summary>
        /// 金币加成（百分比）
        /// </summary>
        public int GoldBonus { get; set; }
        
        /// <summary>
        /// 伤害加成（百分比）
        /// </summary>
        public int DamageBonus { get; set; }
        
        /// <summary>
        /// 伤害减免（百分比）
        /// </summary>
        public int DamageReduction { get; set; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public CombatStats()
        {
            // 默认值
            HP = 100;
            MaxHP = 100;
            MP = 100;
            MaxMP = 100;
            MinDC = 1;
            MaxDC = 3;
            Accuracy = 5;
            Agility = 5;
            AttackSpeed = 1000; // 毫秒
            MoveSpeed = 400;    // 毫秒
            CriticalRate = 5;
            CriticalDamage = 150;
            DodgeRate = 5;
            HitRate = 95;
            HPRegen = 5;
            MPRegen = 5;
        }
        
        /// <summary>
        /// 获取总攻击力
        /// </summary>
        public int GetTotalDC()
        {
            return MinDC + MaxDC;
        }
        
        /// <summary>
        /// 获取总魔法力
        /// </summary>
        public int GetTotalMC()
        {
            return MinMC + MaxMC;
        }
        
        /// <summary>
        /// 获取总道术力
        /// </summary>
        public int GetTotalSC()
        {
            return MinSC + MaxSC;
        }
        
        /// <summary>
        /// 获取总防御力
        /// </summary>
        public int GetTotalAC()
        {
            return MinAC + MaxAC;
        }
        
        /// <summary>
        /// 获取总魔防力
        /// </summary>
        public int GetTotalMAC()
        {
            return MinMAC + MaxMAC;
        }
        
        /// <summary>
        /// 获取平均攻击力
        /// </summary>
        public float GetAverageDC()
        {
            return (MinDC + MaxDC) / 2.0f;
        }
        
        /// <summary>
        /// 获取平均魔法力
        /// </summary>
        public float GetAverageMC()
        {
            return (MinMC + MaxMC) / 2.0f;
        }
        
        /// <summary>
        /// 获取平均道术力
        /// </summary>
        public float GetAverageSC()
        {
            return (MinSC + MaxSC) / 2.0f;
        }
        
        /// <summary>
        /// 获取平均防御力
        /// </summary>
        public float GetAverageAC()
        {
            return (MinAC + MaxAC) / 2.0f;
        }
        
        /// <summary>
        /// 获取平均魔防力
        /// </summary>
        public float GetAverageMAC()
        {
            return (MinMAC + MaxMAC) / 2.0f;
        }
        
        /// <summary>
        /// 获取攻击力范围
        /// </summary>
        public string GetDCRange()
        {
            return $"{MinDC}-{MaxDC}";
        }
        
        /// <summary>
        /// 获取魔法力范围
        /// </summary>
        public string GetMCRange()
        {
            return $"{MinMC}-{MaxMC}";
        }
        
        /// <summary>
        /// 获取道术力范围
        /// </summary>
        public string GetSCRange()
        {
            return $"{MinSC}-{MaxSC}";
        }
        
        /// <summary>
        /// 获取防御力范围
        /// </summary>
        public string GetACRange()
        {
            return $"{MinAC}-{MaxAC}";
        }
        
        /// <summary>
        /// 获取魔防力范围
        /// </summary>
        public string GetMACRange()
        {
            return $"{MinMAC}-{MaxMAC}";
        }
        
        /// <summary>
        /// 获取生命值百分比
        /// </summary>
        public float GetHPPercentage()
        {
            if (MaxHP <= 0) return 0;
            return (float)HP / MaxHP * 100;
        }
        
        /// <summary>
        /// 获取魔法值百分比
        /// </summary>
        public float GetMPPercentage()
        {
            if (MaxMP <= 0) return 0;
            return (float)MP / MaxMP * 100;
        }
        
        /// <summary>
        /// 检查是否存活
        /// </summary>
        public bool IsAlive()
        {
            return HP > 0;
        }
        
        /// <summary>
        /// 检查是否有魔法值
        /// </summary>
        public bool HasMP()
        {
            return MP > 0;
        }
        
        /// <summary>
        /// 治疗
        /// </summary>
        public void Heal(int amount)
        {
            HP = Math.Min(HP + amount, MaxHP);
        }
        
        /// <summary>
        /// 恢复魔法值
        /// </summary>
        public void RestoreMP(int amount)
        {
            MP = Math.Min(MP + amount, MaxMP);
        }
        
        /// <summary>
        /// 造成伤害
        /// </summary>
        public void TakeDamage(int damage)
        {
            HP = Math.Max(HP - damage, 0);
        }
        
        /// <summary>
        /// 消耗魔法值
        /// </summary>
        public void ConsumeMP(int amount)
        {
            MP = Math.Max(MP - amount, 0);
        }
        
        /// <summary>
        /// 增加最大生命值
        /// </summary>
        public void IncreaseMaxHP(int amount)
        {
            MaxHP += amount;
            HP += amount; // 同时增加当前生命值
        }
        
        /// <summary>
        /// 增加最大魔法值
        /// </summary>
        public void IncreaseMaxMP(int amount)
        {
            MaxMP += amount;
            MP += amount; // 同时增加当前魔法值
        }
        
        /// <summary>
        /// 重置属性
        /// </summary>
        public void Reset()
        {
            HP = MaxHP;
            MP = MaxMP;
        }
        
        /// <summary>
        /// 复制属性
        /// </summary>
        public CombatStats Clone()
        {
            return (CombatStats)MemberwiseClone();
        }
        
        /// <summary>
        /// 合并属性（用于buff等效果）
        /// </summary>
        public void Merge(CombatStats other)
        {
            if (other == null) return;
            
            // 叠加属性
            MaxHP += other.MaxHP;
            MaxMP += other.MaxMP;
            MinDC += other.MinDC;
            MaxDC += other.MaxDC;
            MinMC += other.MinMC;
            MaxMC += other.MaxMC;
            MinSC += other.MinSC;
            MaxSC += other.MaxSC;
            MinAC += other.MinAC;
            MaxAC += other.MaxAC;
            MinMAC += other.MinMAC;
            MaxMAC += other.MaxMAC;
            Accuracy += other.Accuracy;
            Agility += other.Agility;
            Lucky += other.Lucky;
            Curse += other.Curse;
            AttackSpeed += other.AttackSpeed;
            CastSpeed += other.CastSpeed;
            MoveSpeed += other.MoveSpeed;
            CriticalRate += other.CriticalRate;
            CriticalDamage += other.CriticalDamage;
            DodgeRate += other.DodgeRate;
            HitRate += other.HitRate;
            PhysicalResistance += other.PhysicalResistance;
            MagicResistance += other.MagicResistance;
            PoisonResistance += other.PoisonResistance;
            FreezeResistance += other.FreezeResistance;
            StunResistance += other.StunResistance;
            SilenceResistance += other.SilenceResistance;
            HPRegen += other.HPRegen;
            MPRegen += other.MPRegen;
            ExpBonus += other.ExpBonus;
            DropBonus += other.DropBonus;
            GoldBonus += other.GoldBonus;
            DamageBonus += other.DamageBonus;
            DamageReduction += other.DamageReduction;
        }
        
        /// <summary>
        /// 移除属性（用于buff等效果结束）
        /// </summary>
        public void Remove(CombatStats other)
        {
            if (other == null) return;
            
            // 移除属性
            MaxHP -= other.MaxHP;
            MaxMP -= other.MaxMP;
            MinDC -= other.MinDC;
            MaxDC -= other.MaxDC;
            MinMC -= other.MinMC;
            MaxMC -= other.MaxMC;
            MinSC -= other.MinSC;
            MaxSC -= other.MaxSC;
            MinAC -= other.MinAC;
            MaxAC -= other.MaxAC;
            MinMAC -= other.MinMAC;
            MaxMAC -= other.MaxMAC;
            Accuracy -= other.Accuracy;
            Agility -= other.Agility;
            Lucky -= other.Lucky;
            Curse -= other.Curse;
            AttackSpeed -= other.AttackSpeed;
            CastSpeed -= other.CastSpeed;
            MoveSpeed -= other.MoveSpeed;
            CriticalRate -= other.CriticalRate;
            CriticalDamage -= other.CriticalDamage;
            DodgeRate -= other.DodgeRate;
            HitRate -= other.HitRate;
            PhysicalResistance -= other.PhysicalResistance;
            MagicResistance -= other.MagicResistance;
            PoisonResistance -= other.PoisonResistance;
            FreezeResistance -= other.FreezeResistance;
            StunResistance -= other.StunResistance;
            SilenceResistance -= other.SilenceResistance;
            HPRegen -= other.HPRegen;
            MPRegen -= other.MPRegen;
            ExpBonus -= other.ExpBonus;
            DropBonus -= other.DropBonus;
            GoldBonus -= other.GoldBonus;
            DamageBonus -= other.DamageBonus;
            DamageReduction -= other.DamageReduction;
            
            // 确保不会低于最小值
            HP = Math.Min(HP, MaxHP);
            MP = Math.Min(MP, MaxMP);
            MinDC = Math.Max(MinDC, 0);
            MaxDC = Math.Max(MaxDC, 0);
            MinMC = Math.Max(MinMC, 0);
            MaxMC = Math.Max(MaxMC, 0);
            MinSC = Math.Max(MinSC, 0);
            MaxSC = Math.Max(MaxSC, 0);
            MinAC = Math.Max(MinAC, 0);
            MaxAC = Math.Max(MaxAC, 0);
            MinMAC = Math.Max(MinMAC, 0);
            MaxMAC = Math.Max(MaxMAC, 0);
            Accuracy = Math.Max(Accuracy, 0);
            Agility = Math.Max(Agility, 0);
            Lucky = Math.Max(Lucky, 0);
            Curse = Math.Max(Curse, 0);
        }
    }
}
