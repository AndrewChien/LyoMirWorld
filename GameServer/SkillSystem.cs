namespace GameServer
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Concurrent;
    using System.Linq;
    using MirCommon;
    using MirCommon.Utils;

    // 类型别名：Player = HumanPlayer
    using Player = HumanPlayer;
    /// <summary>
    /// 技能类型
    /// </summary>
    public enum SkillType
    {
        Passive = 0,        // 被动技能
        Active = 1,         // 主动技能
        Buff = 2,           // 增益技能
        Debuff = 3,         // 减益技能
        Summon = 4,         // 召唤技能
        Teleport = 5,       // 传送技能
        Attack = 6,         // 攻击技能
        Heal = 7            // 治疗技能
    }

    /// <summary>
    /// 技能目标类型
    /// </summary>
    public enum SkillTargetType
    {
        Self = 0,           // 自己
        Enemy = 1,          // 敌人
        Friend = 2,         // 友方
        Ground = 3,         // 地面位置
        Area = 4            // 范围
    }

    /// <summary>
    /// 技能效果类型
    /// </summary>
    public enum SkillEffectType
    {
        Damage = 0,         // 伤害
        Heal = 1,           // 治疗
        Buff = 2,           // 增益
        Debuff = 3,         // 减益
        Stun = 4,           // 眩晕
        Slow = 5,           // 减速
        Poison = 6,         // 中毒
        Shield = 7,         // 护盾
        Teleport = 8,       // 传送
        Summon = 9          // 召唤
    }

    /// <summary>
    /// 技能定义
    /// </summary>
    public class SkillDefinition
    {
        public int SkillId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public SkillType Type { get; set; }
        public SkillTargetType TargetType { get; set; }
        
        // 需求
        public int RequireLevel { get; set; }
        public int RequireJob { get; set; } = -1;  // -1表示所有职业
        public int RequireSkill { get; set; }      // 前置技能
        
        // 消耗
        public int MPCost { get; set; }
        public int HPCost { get; set; }
        public uint GoldCost { get; set; }
        
        // 冷却
        public int Cooldown { get; set; }          // 冷却时间(毫秒)
        public int CastTime { get; set; }          // 施法时间(毫秒)
        
        // 范围
        public int Range { get; set; }             // 施法距离
        public int Radius { get; set; }            // 效果半径
        
        // 效果
        public List<SkillEffect> Effects { get; set; } = new();
        
        // 升级
        public int MaxLevel { get; set; } = 3;
        public Dictionary<int, SkillLevelData> LevelData { get; set; } = new();

        public SkillDefinition(int skillId, string name, SkillType type)
        {
            SkillId = skillId;
            Name = name;
            Type = type;
            MaxLevel = 3;
        }

        public SkillLevelData? GetLevelData(int level)
        {
            LevelData.TryGetValue(level, out var data);
            return data;
        }
    }

    /// <summary>
    /// 技能等级数据
    /// </summary>
    public class SkillLevelData
    {
        public int Level { get; set; }
        public int MPCost { get; set; }
        public int Power { get; set; }          // 威力
        public int Duration { get; set; }       // 持续时间(毫秒)
        public int Cooldown { get; set; }       // 冷却时间
        public int Range { get; set; }          // 范围
        public uint LearnCost { get; set; }     // 学习费用

        public SkillLevelData(int level)
        {
            Level = level;
        }
    }

    /// <summary>
    /// 技能效果
    /// </summary>
    public class SkillEffect
    {
        public SkillEffectType Type { get; set; }
        public int Value { get; set; }
        public int Duration { get; set; }
        public float Chance { get; set; } = 1.0f;   // 触发概率

        public SkillEffect(SkillEffectType type, int value, int duration = 0)
        {
            Type = type;
            Value = value;
            Duration = duration;
        }
    }

    /// <summary>
    /// 玩家已学技能
    /// </summary>
    public class PlayerSkill
    {
        public int SkillId { get; set; }
        public SkillDefinition Definition { get; set; }
        public int Level { get; set; }
        public DateTime LearnTime { get; set; }
        public DateTime LastUseTime { get; set; }
        public int UseCount { get; set; }
        public byte Key { get; set; } // 快捷键（0-9，0表示未设置）

        public PlayerSkill(SkillDefinition definition)
        {
            Definition = definition;
            SkillId = definition.SkillId;
            Level = 1;
            LearnTime = DateTime.Now;
            LastUseTime = DateTime.MinValue;
            Key = 0; // 默认未设置快捷键
        }

        public bool CanUse()
        {
            var levelData = Definition.GetLevelData(Level);
            if (levelData == null) return false;

            var cooldownMs = (DateTime.Now - LastUseTime).TotalMilliseconds;
            return cooldownMs >= levelData.Cooldown;
        }

        public TimeSpan GetRemainingCooldown()
        {
            var levelData = Definition.GetLevelData(Level);
            if (levelData == null) return TimeSpan.Zero;

            var elapsed = (DateTime.Now - LastUseTime).TotalMilliseconds;
            var remaining = levelData.Cooldown - elapsed;
            return remaining > 0 ? TimeSpan.FromMilliseconds(remaining) : TimeSpan.Zero;
        }

        public bool CanLevelUp()
        {
            return Level < Definition.MaxLevel && 
                   Definition.LevelData.ContainsKey(Level + 1);
        }

        public void LevelUp()
        {
            if (CanLevelUp())
            {
                Level++;
                LogManager.Default.Info($"技能 {Definition.Name} 升级到 {Level} 级");
            }
        }

        public void Use()
        {
            LastUseTime = DateTime.Now;
            UseCount++;
        }

        /// <summary>
        /// 增加技能经验
        /// </summary>
        public void AddExp(int exp)
        {
            // 增加技能经验
            UseCount += exp;
            
            // 每增加一定经验检查技能升级
            if (UseCount % 50 == 0) // 每50次使用检查一次升级
            {
                // 检查技能升级
                if (CanLevelUp())
                {
                    LevelUp();
                }
            }
        }
    }

    /// <summary>
    /// 技能书
    /// </summary>
    public class SkillBook
    {
        public PlayerSkill Skill { get; }
        private readonly Dictionary<int, PlayerSkill> _skills = new();
        private readonly object _lock = new();
        public int MaxSkills { get; set; } = 20;

        public SkillBook()
        {
            Skill = null!;
        }

        public bool LearnSkill(SkillDefinition definition)
        {
            lock (_lock)
            {
                if (_skills.ContainsKey(definition.SkillId))
                    return false;

                if (_skills.Count >= MaxSkills)
                    return false;

                var skill = new PlayerSkill(definition);
                _skills[definition.SkillId] = skill;
                
                LogManager.Default.Info($"学习技能: {definition.Name}");
                return true;
            }
        }

        public bool ForgetSkill(int skillId)
        {
            lock (_lock)
            {
                if (_skills.Remove(skillId))
                {
                    LogManager.Default.Info($"遗忘技能ID: {skillId}");
                    return true;
                }
                return false;
            }
        }

        public PlayerSkill? GetSkill(int skillId)
        {
            lock (_lock)
            {
                _skills.TryGetValue(skillId, out var skill);
                return skill;
            }
        }

        public bool HasSkill(int skillId)
        {
            lock (_lock)
            {
                return _skills.ContainsKey(skillId);
            }
        }

        public List<PlayerSkill> GetAllSkills()
        {
            lock (_lock)
            {
                return _skills.Values.ToList();
            }
        }

        public bool LevelUpSkill(int skillId)
        {
            lock (_lock)
            {
                var skill = GetSkill(skillId);
                if (skill == null || !skill.CanLevelUp())
                    return false;

                skill.LevelUp();
                return true;
            }
        }

        public PlayerSkill? GetMagic(uint magicId)
        {
            lock (_lock)
            {
                return _skills.Values.FirstOrDefault(skill => skill.SkillId == magicId);
            }
        }

        public PlayerSkill? GetMagicByKey(byte key)
        {
            lock (_lock)
            {
                return _skills.Values.FirstOrDefault();
            }
        }

        public bool HasMagic(uint magicId)
        {
            lock (_lock)
            {
                return _skills.ContainsKey((int)magicId);
            }
        }

        public void SetMagicKey(uint magicId, byte key)
        {
            LogManager.Default.Info($"设置技能 {magicId} 的快捷键为 {key}");
        }
    }

    /// <summary>
    /// 技能管理器
    /// </summary>
    public class SkillManager
    {
        private static SkillManager? _instance;
        public static SkillManager Instance => _instance ??= new SkillManager();

        private readonly ConcurrentDictionary<int, SkillDefinition> _definitions = new();

        private SkillManager()
        {
            InitializeDefaultSkills();
        }

        private void InitializeDefaultSkills()
        {
            // 战士技能
            var basicSword = new SkillDefinition(1001, "基础剑法", SkillType.Attack)
            {
                Description = "基础的剑术攻击",
                TargetType = SkillTargetType.Enemy,
                RequireJob = 0, // 战士
                RequireLevel = 1,
                Range = 1,
                MaxLevel = 3
            };
            basicSword.Effects.Add(new SkillEffect(SkillEffectType.Damage, 10));
            basicSword.LevelData[1] = new SkillLevelData(1) { MPCost = 2, Power = 10, Cooldown = 1000, LearnCost = 100 };
            basicSword.LevelData[2] = new SkillLevelData(2) { MPCost = 3, Power = 15, Cooldown = 900, LearnCost = 500 };
            basicSword.LevelData[3] = new SkillLevelData(3) { MPCost = 4, Power = 20, Cooldown = 800, LearnCost = 1000 };
            AddDefinition(basicSword);

            var assassinate = new SkillDefinition(1002, "刺杀剑术", SkillType.Attack)
            {
                Description = "强力的突刺攻击",
                TargetType = SkillTargetType.Enemy,
                RequireJob = 0,
                RequireLevel = 7,
                RequireSkill = 1001,
                Range = 2,
                MaxLevel = 3
            };
            assassinate.Effects.Add(new SkillEffect(SkillEffectType.Damage, 30));
            assassinate.LevelData[1] = new SkillLevelData(1) { MPCost = 5, Power = 30, Cooldown = 3000, LearnCost = 1000 };
            assassinate.LevelData[2] = new SkillLevelData(2) { MPCost = 7, Power = 45, Cooldown = 2500, LearnCost = 5000 };
            assassinate.LevelData[3] = new SkillLevelData(3) { MPCost = 10, Power = 60, Cooldown = 2000, LearnCost = 10000 };
            AddDefinition(assassinate);

            var halfMoon = new SkillDefinition(1003, "半月弯刀", SkillType.Attack)
            {
                Description = "攻击周围所有敌人",
                TargetType = SkillTargetType.Area,
                RequireJob = 0,
                RequireLevel = 19,
                Range = 1,
                Radius = 2,
                MaxLevel = 3
            };
            halfMoon.Effects.Add(new SkillEffect(SkillEffectType.Damage, 25));
            halfMoon.LevelData[1] = new SkillLevelData(1) { MPCost = 8, Power = 25, Cooldown = 5000, LearnCost = 5000 };
            halfMoon.LevelData[2] = new SkillLevelData(2) { MPCost = 12, Power = 40, Cooldown = 4000, LearnCost = 20000 };
            halfMoon.LevelData[3] = new SkillLevelData(3) { MPCost = 15, Power = 55, Cooldown = 3000, LearnCost = 50000 };
            AddDefinition(halfMoon);

            // 法师技能
            var fireball = new SkillDefinition(2001, "火球术", SkillType.Attack)
            {
                Description = "发射火球攻击敌人",
                TargetType = SkillTargetType.Enemy,
                RequireJob = 1, // 法师
                RequireLevel = 1,
                Range = 7,
                MaxLevel = 3
            };
            fireball.Effects.Add(new SkillEffect(SkillEffectType.Damage, 15));
            fireball.LevelData[1] = new SkillLevelData(1) { MPCost = 4, Power = 15, Cooldown = 1500, LearnCost = 100 };
            fireball.LevelData[2] = new SkillLevelData(2) { MPCost = 6, Power = 25, Cooldown = 1200, LearnCost = 500 };
            fireball.LevelData[3] = new SkillLevelData(3) { MPCost = 8, Power = 35, Cooldown = 1000, LearnCost = 1000 };
            AddDefinition(fireball);

            var lightning = new SkillDefinition(2002, "雷电术", SkillType.Attack)
            {
                Description = "召唤雷电攻击敌人",
                TargetType = SkillTargetType.Enemy,
                RequireJob = 1,
                RequireLevel = 17,
                Range = 7,
                MaxLevel = 3
            };
            lightning.Effects.Add(new SkillEffect(SkillEffectType.Damage, 40));
            lightning.LevelData[1] = new SkillLevelData(1) { MPCost = 12, Power = 40, Cooldown = 3000, LearnCost = 5000 };
            lightning.LevelData[2] = new SkillLevelData(2) { MPCost = 18, Power = 60, Cooldown = 2500, LearnCost = 20000 };
            lightning.LevelData[3] = new SkillLevelData(3) { MPCost = 25, Power = 80, Cooldown = 2000, LearnCost = 50000 };
            AddDefinition(lightning);

            var hellFire = new SkillDefinition(2003, "地狱火", SkillType.Attack)
            {
                Description = "范围火焰攻击",
                TargetType = SkillTargetType.Area,
                RequireJob = 1,
                RequireLevel = 35,
                Range = 7,
                Radius = 3,
                MaxLevel = 3
            };
            hellFire.Effects.Add(new SkillEffect(SkillEffectType.Damage, 50));
            hellFire.LevelData[1] = new SkillLevelData(1) { MPCost = 30, Power = 50, Cooldown = 8000, LearnCost = 50000 };
            hellFire.LevelData[2] = new SkillLevelData(2) { MPCost = 45, Power = 80, Cooldown = 7000, LearnCost = 200000 };
            hellFire.LevelData[3] = new SkillLevelData(3) { MPCost = 60, Power = 110, Cooldown = 6000, LearnCost = 500000 };
            AddDefinition(hellFire);

            // 道士技能
            var heal = new SkillDefinition(3001, "治愈术", SkillType.Heal)
            {
                Description = "恢复生命值",
                TargetType = SkillTargetType.Friend,
                RequireJob = 2, // 道士
                RequireLevel = 1,
                Range = 7,
                MaxLevel = 3
            };
            heal.Effects.Add(new SkillEffect(SkillEffectType.Heal, 20));
            heal.LevelData[1] = new SkillLevelData(1) { MPCost = 6, Power = 20, Cooldown = 2000, LearnCost = 100 };
            heal.LevelData[2] = new SkillLevelData(2) { MPCost = 9, Power = 35, Cooldown = 1800, LearnCost = 500 };
            heal.LevelData[3] = new SkillLevelData(3) { MPCost = 12, Power = 50, Cooldown = 1500, LearnCost = 1000 };
            AddDefinition(heal);

            var poison = new SkillDefinition(3002, "施毒术", SkillType.Debuff)
            {
                Description = "对敌人施加毒素",
                TargetType = SkillTargetType.Enemy,
                RequireJob = 2,
                RequireLevel = 14,
                Range = 7,
                MaxLevel = 3
            };
            poison.Effects.Add(new SkillEffect(SkillEffectType.Poison, 5, 10000));
            poison.LevelData[1] = new SkillLevelData(1) { MPCost = 8, Power = 5, Duration = 10000, Cooldown = 3000, LearnCost = 2000 };
            poison.LevelData[2] = new SkillLevelData(2) { MPCost = 12, Power = 8, Duration = 15000, Cooldown = 2500, LearnCost = 10000 };
            poison.LevelData[3] = new SkillLevelData(3) { MPCost = 16, Power = 12, Duration = 20000, Cooldown = 2000, LearnCost = 30000 };
            AddDefinition(poison);

            var summonSkeleton = new SkillDefinition(3003, "召唤骷髅", SkillType.Summon)
            {
                Description = "召唤骷髅协助战斗",
                TargetType = SkillTargetType.Self,
                RequireJob = 2,
                RequireLevel = 19,
                MaxLevel = 3
            };
            summonSkeleton.Effects.Add(new SkillEffect(SkillEffectType.Summon, 1));
            summonSkeleton.LevelData[1] = new SkillLevelData(1) { MPCost = 20, Power = 1, Cooldown = 10000, LearnCost = 5000 };
            summonSkeleton.LevelData[2] = new SkillLevelData(2) { MPCost = 30, Power = 2, Cooldown = 8000, LearnCost = 20000 };
            summonSkeleton.LevelData[3] = new SkillLevelData(3) { MPCost = 40, Power = 3, Cooldown = 6000, LearnCost = 50000 };
            AddDefinition(summonSkeleton);

            LogManager.Default.Info($"已加载 {_definitions.Count} 个技能定义");
        }

        public void AddDefinition(SkillDefinition definition)
        {
            _definitions[definition.SkillId] = definition;
        }

        public SkillDefinition? GetDefinition(int skillId)
        {
            _definitions.TryGetValue(skillId, out var definition);
            return definition;
        }

        public List<SkillDefinition> GetAllDefinitions()
        {
            return _definitions.Values.ToList();
        }

        public List<SkillDefinition> GetSkillsByJob(int job)
        {
            return _definitions.Values
                .Where(s => s.RequireJob == -1 || s.RequireJob == job)
                .OrderBy(s => s.RequireLevel)
                .ToList();
        }

        public List<SkillDefinition> GetLearnableSkills(Player player)
        {
            var skillBook = player.SkillBook;
            
            return _definitions.Values
                .Where(s => 
                    (s.RequireJob == -1 || s.RequireJob == player.Job) &&
                    s.RequireLevel <= player.Level &&
                    !skillBook.HasSkill(s.SkillId) &&
                    (s.RequireSkill == 0 || skillBook.HasSkill(s.RequireSkill))
                )
                .OrderBy(s => s.RequireLevel)
                .ToList();
        }
    }

    /// <summary>
    /// 技能使用结果
    /// </summary>
    public class SkillUseResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<ICombatEntity> AffectedTargets { get; set; } = new();
        public int TotalDamage { get; set; }
        public int TotalHeal { get; set; }

        public SkillUseResult(bool success, string message = "")
        {
            Success = success;
            Message = message;
        }
    }

    /// <summary>
    /// 技能执行器
    /// </summary>
    public class SkillExecutor
    {
        private static SkillExecutor? _instance;
        public static SkillExecutor Instance => _instance ??= new SkillExecutor();

        private SkillExecutor() { }

        /// <summary>
        /// 使用技能
        /// </summary>
        public SkillUseResult UseSkill(Player caster, int skillId, ICombatEntity? target = null, int x = 0, int y = 0)
        {
            // 获取技能
            var playerSkill = caster.SkillBook.GetSkill(skillId);
            if (playerSkill == null)
                return new SkillUseResult(false, "未学习此技能");

            // 检查冷却
            if (!playerSkill.CanUse())
            {
                var remaining = playerSkill.GetRemainingCooldown();
                return new SkillUseResult(false, $"冷却中，还需{remaining.TotalSeconds:F1}秒");
            }

            // 获取等级数据
            var levelData = playerSkill.Definition.GetLevelData(playerSkill.Level);
            if (levelData == null)
                return new SkillUseResult(false, "技能数据错误");

            // 检查MP
            if (!caster.ConsumeMP(levelData.MPCost))
                return new SkillUseResult(false, "魔法不足");

            // 检查HP消耗
            if (playerSkill.Definition.HPCost > 0 && caster.CurrentHP <= playerSkill.Definition.HPCost)
                return new SkillUseResult(false, "生命值不足");

            // 检查施法距离
            if (target != null && caster.CurrentMap != null)
            {
                int distance = Math.Abs(caster.X - target.X) + Math.Abs(caster.Y - target.Y);
                if (distance > playerSkill.Definition.Range)
                    return new SkillUseResult(false, "距离太远");
            }

            // 消耗资源
            if (playerSkill.Definition.HPCost > 0)
                caster.CurrentHP -= playerSkill.Definition.HPCost;
            
            if (playerSkill.Definition.GoldCost > 0)
            {
                if (!caster.TakeGold(playerSkill.Definition.GoldCost))
                    return new SkillUseResult(false, "金币不足");
            }

            // 标记使用
            playerSkill.Use();

            // 执行技能效果
            var result = ExecuteSkill(caster, playerSkill, target, x, y);
            
            if (result.Success)
            {
                // 发送技能使用消息
                SendSkillUseMessage(caster, playerSkill, target, x, y);
                
                // 增加技能经验
                TrainSkill(caster, playerSkill);
                
                LogManager.Default.Info($"{caster.Name} 使用技能 {playerSkill.Definition.Name} (等级{playerSkill.Level})");
            }

            return result;
        }

        /// <summary>
        /// 执行技能效果
        /// </summary>
        private SkillUseResult ExecuteSkill(Player caster, PlayerSkill skill, ICombatEntity? target, int x, int y)
        {
            var result = new SkillUseResult(true, "技能释放成功");
            var levelData = skill.Definition.GetLevelData(skill.Level);
            if (levelData == null)
                return new SkillUseResult(false, "技能数据错误");

            // 根据技能类型执行不同的效果
            switch (skill.Definition.Type)
            {
                case SkillType.Attack:
                    result = ExecuteAttackSkill(caster, skill, target, x, y);
                    break;
                    
                case SkillType.Heal:
                    result = ExecuteHealSkill(caster, skill, target, x, y);
                    break;
                    
                case SkillType.Buff:
                    result = ExecuteBuffSkill(caster, skill, target, x, y);
                    break;
                    
                case SkillType.Debuff:
                    result = ExecuteDebuffSkill(caster, skill, target, x, y);
                    break;
                    
                case SkillType.Summon:
                    result = ExecuteSummonSkill(caster, skill, target, x, y);
                    break;
                    
                case SkillType.Teleport:
                    result = ExecuteTeleportSkill(caster, skill, target, x, y);
                    break;
                    
                case SkillType.Passive:
                    // 被动技能不需要主动释放
                    result = new SkillUseResult(false, "被动技能不能主动释放");
                    break;
            }

            return result;
        }

        /// <summary>
        /// 执行攻击技能
        /// </summary>
        private SkillUseResult ExecuteAttackSkill(Player caster, PlayerSkill skill, ICombatEntity? target, int x, int y)
        {
            var result = new SkillUseResult(true, "攻击成功");
            var levelData = skill.Definition.GetLevelData(skill.Level);
            if (levelData == null)
                return new SkillUseResult(false, "技能数据错误");

            // 获取目标列表
            var targets = GetSkillTargets(caster, skill, target, x, y);
            
            foreach (var t in targets)
            {
                // 计算伤害
                int baseDamage = levelData.Power;
                
                // 根据职业计算伤害加成
                int damageBonus = 0;
                switch (caster.Job)
                {
                    case 0: // 战士
                        damageBonus = caster.Stats.MinDC;
                        break;
                    case 1: // 法师
                        damageBonus = caster.Stats.MinMC;
                        break;
                    case 2: // 道士
                        damageBonus = caster.Stats.MinSC;
                        break;
                }
                
                int totalDamage = baseDamage + damageBonus;
                
                // 执行战斗
                var combatResult = CombatSystemManager.Instance.ExecuteCombat(caster, t, DamageType.Magic);
                result.TotalDamage += combatResult.Damage;
                result.AffectedTargets.Add(t);
                
                // 发送伤害消息
                SendDamageMessage(caster, t, combatResult.Damage);
            }

            return result;
        }

        /// <summary>
        /// 执行治疗技能
        /// </summary>
        private SkillUseResult ExecuteHealSkill(Player caster, PlayerSkill skill, ICombatEntity? target, int x, int y)
        {
            var result = new SkillUseResult(true, "治疗成功");
            var levelData = skill.Definition.GetLevelData(skill.Level);
            if (levelData == null)
                return new SkillUseResult(false, "技能数据错误");

            // 获取目标列表
            var targets = GetSkillTargets(caster, skill, target, x, y);
            
            foreach (var t in targets)
            {
                int healAmount = levelData.Power;
                t.Heal(healAmount);
                result.TotalHeal += healAmount;
                result.AffectedTargets.Add(t);
                
                // 发送治疗消息
                SendHealMessage(caster, t, healAmount);
            }

            return result;
        }

        /// <summary>
        /// 执行增益技能
        /// </summary>
        private SkillUseResult ExecuteBuffSkill(Player caster, PlayerSkill skill, ICombatEntity? target, int x, int y)
        {
            var result = new SkillUseResult(true, "增益效果生效");
            var levelData = skill.Definition.GetLevelData(skill.Level);
            if (levelData == null)
                return new SkillUseResult(false, "技能数据错误");

            // 获取目标列表
            var targets = GetSkillTargets(caster, skill, target, x, y);
            
            foreach (var t in targets)
            {
                // 应用增益效果
                // 根据技能效果类型应用不同的增益
                foreach (var effect in skill.Definition.Effects)
                {
                    switch (effect.Type)
                    {
                        case SkillEffectType.Buff:
                            // 应用增益效果
                            ApplyBuffEffect(caster, t, skill, effect, levelData);
                            break;
                        case SkillEffectType.Shield:
                            // 应用护盾效果
                            ApplyShieldEffect(caster, t, skill, effect, levelData);
                            break;
                        case SkillEffectType.Heal:
                            // 应用治疗效果
                            ApplyHealEffect(caster, t, skill, effect, levelData);
                            break;
                    }
                }
                
                result.AffectedTargets.Add(t);
                
                // 发送增益消息
                SendBuffMessage(caster, t, skill.Definition.Name);
            }

            return result;
        }

        /// <summary>
        /// 执行减益技能
        /// </summary>
        private SkillUseResult ExecuteDebuffSkill(Player caster, PlayerSkill skill, ICombatEntity? target, int x, int y)
        {
            var result = new SkillUseResult(true, "减益效果生效");
            var levelData = skill.Definition.GetLevelData(skill.Level);
            if (levelData == null)
                return new SkillUseResult(false, "技能数据错误");

            // 获取目标列表
            var targets = GetSkillTargets(caster, skill, target, x, y);
            
            foreach (var t in targets)
            {
                // 应用减益效果
                // 根据技能效果类型应用不同的减益
                foreach (var effect in skill.Definition.Effects)
                {
                    ApplyDebuffEffect(caster, t, skill, effect, levelData);
                }
                
                result.AffectedTargets.Add(t);
                
                // 发送减益消息
                SendDebuffMessage(caster, t, skill.Definition.Name);
            }

            return result;
        }

        /// <summary>
        /// 执行召唤技能
        /// </summary>
        private SkillUseResult ExecuteSummonSkill(Player caster, PlayerSkill skill, ICombatEntity? target, int x, int y)
        {
            var result = new SkillUseResult(true, "召唤成功");
            var levelData = skill.Definition.GetLevelData(skill.Level);
            if (levelData == null)
                return new SkillUseResult(false, "技能数据错误");

            // 检查召唤数量限制
            if (caster.PetSystem.GetPetCount() >= caster.PetSystem.MaxPets)
                return new SkillUseResult(false, "召唤数量已达上限");

            // 创建召唤物
            // 根据技能ID创建对应的召唤物
            string petName = GetSummonPetName(skill.SkillId, skill.Level);
            int summonCount = levelData.Power; // 召唤数量
            
            for (int i = 0; i < summonCount; i++)
            {
                // 计算召唤位置
                int summonX = x;
                int summonY = y;
                
                if (summonX == 0 && summonY == 0)
                {
                    // 如果没有指定位置，则在玩家周围召唤
                    summonX = caster.X + Random.Shared.Next(-2, 3);
                    summonY = caster.Y + Random.Shared.Next(-2, 3);
                }
                
                // 召唤宠物
                bool success = caster.PetSystem.SummonPet(petName, true, summonX, summonY);
                if (!success)
                {
                    return new SkillUseResult(false, "召唤失败");
                }
            }
            
            // 发送召唤消息
            SendSummonMessage(caster, skill.Definition.Name);
            
            return result;
        }

        /// <summary>
        /// 执行传送技能
        /// </summary>
        private SkillUseResult ExecuteTeleportSkill(Player caster, PlayerSkill skill, ICombatEntity? target, int x, int y)
        {
            var result = new SkillUseResult(true, "传送成功");
            var levelData = skill.Definition.GetLevelData(skill.Level);
            if (levelData == null)
                return new SkillUseResult(false, "技能数据错误");

            // 检查目标位置是否有效
            if (caster.CurrentMap == null || !caster.CurrentMap.CanMoveTo(x, y))
                return new SkillUseResult(false, "无法传送到该位置");

            // 执行传送
            caster.X = (ushort)x;
            caster.Y = (ushort)y;
            
            // 发送传送消息
            SendTeleportMessage(caster, x, y);
            
            return result;
        }

        /// <summary>
        /// 获取技能目标列表
        /// </summary>
        private List<ICombatEntity> GetSkillTargets(Player caster, PlayerSkill skill, ICombatEntity? target, int x, int y)
        {
            var targets = new List<ICombatEntity>();
            
            switch (skill.Definition.TargetType)
            {
                case SkillTargetType.Self:
                    targets.Add(caster);
                    break;
                    
                case SkillTargetType.Enemy:
                    if (target != null)
                        targets.Add(target);
                    break;
                    
                case SkillTargetType.Friend:
                    if (target != null)
                        targets.Add(target);
                    else
                        targets.Add(caster);
                    break;
                    
                case SkillTargetType.Area:
                    // 获取范围内的所有目标
                    if (caster.CurrentMap != null)
                    {
                        var areaTargets = caster.CurrentMap.GetObjectsInRange(x, y, skill.Definition.Radius);
                        foreach (var obj in areaTargets)
                        {
                            if (obj is ICombatEntity combatEntity)
                            {
                                // 根据技能类型筛选目标
                                if (skill.Definition.Type == SkillType.Attack && combatEntity != caster)
                                    targets.Add(combatEntity);
                                else if (skill.Definition.Type == SkillType.Heal && combatEntity == caster)
                                    targets.Add(combatEntity);
                            }
                        }
                    }
                    break;
                    
                case SkillTargetType.Ground:
                    // 地面目标不需要实体目标
                    break;
            }

            return targets;
        }


        /// <summary>
        /// 发送技能使用消息
        /// </summary>
        private void SendSkillUseMessage(Player caster, PlayerSkill skill, ICombatEntity? target, int x, int y)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(caster.ObjectId);
            builder.WriteUInt16(0x285); // SM_SKILLUSE
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32((uint)skill.SkillId);
            builder.WriteUInt16((ushort)skill.Level);
            builder.WriteUInt32(target?.Id ?? 0);
            builder.WriteUInt16((ushort)x);
            builder.WriteUInt16((ushort)y);
            
            caster.SendMessage(builder.Build());
        }

        /// <summary>
        /// 发送伤害消息
        /// </summary>
        private void SendDamageMessage(Player caster, ICombatEntity target, int damage)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(caster.ObjectId);
            builder.WriteUInt16(0x286); // SM_DAMAGE
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(target.Id);
            builder.WriteUInt32((uint)damage);
            
            caster.SendMessage(builder.Build());
        }

        /// <summary>
        /// 发送治疗消息
        /// </summary>
        private void SendHealMessage(Player caster, ICombatEntity target, int healAmount)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(caster.ObjectId);
            builder.WriteUInt16(0x287); // SM_HEAL
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(target.Id);
            builder.WriteUInt32((uint)healAmount);
            
            caster.SendMessage(builder.Build());
        }

        /// <summary>
        /// 发送增益消息
        /// </summary>
        private void SendBuffMessage(Player caster, ICombatEntity target, string buffName)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(caster.ObjectId);
            builder.WriteUInt16(0x288); // SM_BUFF
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(target.Id);
            builder.WriteString(buffName);
            
            caster.SendMessage(builder.Build());
        }

        /// <summary>
        /// 发送减益消息
        /// </summary>
        private void SendDebuffMessage(Player caster, ICombatEntity target, string debuffName)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(caster.ObjectId);
            builder.WriteUInt16(0x289); // SM_DEBUFF
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(target.Id);
            builder.WriteString(debuffName);
            
            caster.SendMessage(builder.Build());
        }

        /// <summary>
        /// 发送召唤消息
        /// </summary>
        private void SendSummonMessage(Player caster, string summonName)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(caster.ObjectId);
            builder.WriteUInt16(0x28A); // SM_SUMMON
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteString(summonName);
            
            caster.SendMessage(builder.Build());
        }

        /// <summary>
        /// 发送传送消息
        /// </summary>
        private void SendTeleportMessage(Player caster, int x, int y)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(caster.ObjectId);
            builder.WriteUInt16(0x28B); // SM_TELEPORT
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16((ushort)x);
            builder.WriteUInt16((ushort)y);
            
            caster.SendMessage(builder.Build());
        }

        /// <summary>
        /// 发送技能升级消息
        /// </summary>
        private void SendSkillLevelUpMessage(Player caster, PlayerSkill skill)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(caster.ObjectId);
            builder.WriteUInt16(0x28C); // SM_SKILLLEVELUP
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32((uint)skill.SkillId);
            builder.WriteUInt16((ushort)skill.Level);
            
            caster.SendMessage(builder.Build());
        }

        public bool LearnSkill(Player player, int skillId)
        {
            var definition = SkillManager.Instance.GetDefinition(skillId);
            if (definition == null)
                return false;

            // 检查需求
            if (definition.RequireLevel > player.Level)
                return false;

            if (definition.RequireJob != -1 && definition.RequireJob != player.Job)
                return false;

            if (definition.RequireSkill != 0 && !player.SkillBook.HasSkill(definition.RequireSkill))
                return false;

            // 检查费用
            var levelData = definition.GetLevelData(1);
            if (levelData != null && player.Gold < levelData.LearnCost)
                return false;

            // 学习技能
            if (player.SkillBook.LearnSkill(definition))
            {
                if (levelData != null)
                {
                    player.Gold -= levelData.LearnCost;
                }
                LogManager.Default.Info($"{player.Name} 学习了技能 {definition.Name}");
                return true;
            }

            return false;
        }

        public bool LevelUpSkill(Player player, int skillId)
        {
            var playerSkill = player.SkillBook.GetSkill(skillId);
            if (playerSkill == null || !playerSkill.CanLevelUp())
                return false;

            var nextLevel = playerSkill.Level + 1;
            var levelData = playerSkill.Definition.GetLevelData(nextLevel);
            if (levelData == null)
                return false;

            // 检查费用
            if (player.Gold < levelData.LearnCost)
                return false;

            // 升级
            player.Gold -= levelData.LearnCost;
            player.SkillBook.LevelUpSkill(skillId);
            
            LogManager.Default.Info($"{player.Name} 的技能 {playerSkill.Definition.Name} 升级到 {nextLevel} 级");
            return true;
        }

        /// <summary>
        /// 应用增益效果
        /// </summary>
        private void ApplyBuffEffect(Player caster, ICombatEntity target, PlayerSkill skill, SkillEffect effect, SkillLevelData levelData)
        {
            // 获取Buff定义管理器中的增益效果
            var buffDefinition = BuffDefinitionManager.Instance.GetDefinition(1001); // 默认使用力量祝福
            if (buffDefinition != null)
            {
                // 根据技能等级调整效果值
                buffDefinition.Value = levelData.Power;
                buffDefinition.Duration = effect.Duration > 0 ? effect.Duration : levelData.Duration;
                
                // 应用Buff
                target.BuffManager?.AddBuff(buffDefinition, caster);
            }
        }

        /// <summary>
        /// 应用护盾效果
        /// </summary>
        private void ApplyShieldEffect(Player caster, ICombatEntity target, PlayerSkill skill, SkillEffect effect, SkillLevelData levelData)
        {
            // 获取护盾Buff定义
            var shieldDefinition = BuffDefinitionManager.Instance.GetDefinition(1007); // 护盾
            if (shieldDefinition != null)
            {
                shieldDefinition.Value = levelData.Power;
                shieldDefinition.Duration = effect.Duration > 0 ? effect.Duration : levelData.Duration;
                
                // 应用护盾
                target.BuffManager?.AddBuff(shieldDefinition, caster);
            }
        }

        /// <summary>
        /// 应用治疗效果
        /// </summary>
        private void ApplyHealEffect(Player caster, ICombatEntity target, PlayerSkill skill, SkillEffect effect, SkillLevelData levelData)
        {
            // 直接治疗
            int healAmount = levelData.Power;
            target.Heal(healAmount);
            
            // 发送治疗消息
            SendHealMessage(caster, target, healAmount);
        }

        /// <summary>
        /// 应用减益效果
        /// </summary>
        private void ApplyDebuffEffect(Player caster, ICombatEntity target, PlayerSkill skill, SkillEffect effect, SkillLevelData levelData)
        {
            // 根据效果类型应用不同的减益
            switch (effect.Type)
            {
                case SkillEffectType.Poison:
                    ApplyPoisonEffect(caster, target, skill, effect, levelData);
                    break;
                case SkillEffectType.Slow:
                    ApplySlowEffect(caster, target, skill, effect, levelData);
                    break;
                case SkillEffectType.Stun:
                    ApplyStunEffect(caster, target, skill, effect, levelData);
                    break;
            }
        }

        /// <summary>
        /// 应用中毒效果
        /// </summary>
        private void ApplyPoisonEffect(Player caster, ICombatEntity target, PlayerSkill skill, SkillEffect effect, SkillLevelData levelData)
        {
            var poisonDefinition = BuffDefinitionManager.Instance.GetDefinition(2001); // 中毒
            if (poisonDefinition != null)
            {
                poisonDefinition.Value = levelData.Power;
                poisonDefinition.Duration = effect.Duration > 0 ? effect.Duration : levelData.Duration;
                
                // 应用中毒效果
                target.BuffManager?.AddBuff(poisonDefinition, caster);
            }
        }

        /// <summary>
        /// 应用减速效果
        /// </summary>
        private void ApplySlowEffect(Player caster, ICombatEntity target, PlayerSkill skill, SkillEffect effect, SkillLevelData levelData)
        {
            var slowDefinition = BuffDefinitionManager.Instance.GetDefinition(2006); // 减速
            if (slowDefinition != null)
            {
                slowDefinition.Value = levelData.Power;
                slowDefinition.Duration = effect.Duration > 0 ? effect.Duration : levelData.Duration;
                
                // 应用减速效果
                target.BuffManager?.AddBuff(slowDefinition, caster);
            }
        }

        /// <summary>
        /// 应用眩晕效果
        /// </summary>
        private void ApplyStunEffect(Player caster, ICombatEntity target, PlayerSkill skill, SkillEffect effect, SkillLevelData levelData)
        {
            var stunDefinition = BuffDefinitionManager.Instance.GetDefinition(3001); // 眩晕
            if (stunDefinition != null)
            {
                stunDefinition.Duration = effect.Duration > 0 ? effect.Duration : levelData.Duration;
                
                // 应用眩晕效果
                target.BuffManager?.AddBuff(stunDefinition, caster);
            }
        }

        /// <summary>
        /// 获取召唤宠物名称
        /// </summary>
        private string GetSummonPetName(int skillId, int skillLevel)
        {
            // 根据技能ID返回对应的宠物名称
            switch (skillId)
            {
                case 3003: // 召唤骷髅
                    return skillLevel >= 3 ? "骷髅精灵" : 
                           skillLevel >= 2 ? "骷髅战士" : "骷髅";
                default:
                    return "未知宠物";
            }
        }

        /// <summary>
        /// 训练技能
        /// </summary>
        private void TrainSkill(Player caster, PlayerSkill skill)
        {
            skill.UseCount++;
            
            // 每使用一定次数增加技能经验
            if (skill.UseCount % 10 == 0)
            {
                // 增加技能经验
                // 根据技能等级和职业计算经验值
                int expGain = CalculateSkillExpGain(caster, skill);
                
                // 这里可以保存技能经验到数据库
                LogManager.Default.Info($"{caster.Name} 的技能 {skill.Definition.Name} 获得 {expGain} 经验");
                
                // 检查技能升级
                if (skill.CanLevelUp())
                {
                    // 检查升级条件
                    if (CheckSkillLevelUpConditions(caster, skill))
                    {
                        // 自动升级技能
                        skill.LevelUp();
                        
                        // 发送技能升级消息
                        SendSkillLevelUpMessage(caster, skill);
                        
                        // 通知玩家
                        caster.Say($"恭喜！你的技能 {skill.Definition.Name} 升级到 {skill.Level} 级");
                    }
                }
            }
        }

        /// <summary>
        /// 计算技能经验获得
        /// </summary>
        private int CalculateSkillExpGain(Player caster, PlayerSkill skill)
        {
            // 基础经验值
            int baseExp = 10;
            
            // 根据技能等级调整
            int levelBonus = skill.Level * 5;
            
            // 根据职业调整
            int jobBonus = 0;
            switch (caster.Job)
            {
                case 0: // 战士
                    jobBonus = skill.Definition.Type == SkillType.Attack ? 10 : 5;
                    break;
                case 1: // 法师
                    jobBonus = skill.Definition.Type == SkillType.Attack || skill.Definition.Type == SkillType.Buff ? 10 : 5;
                    break;
                case 2: // 道士
                    jobBonus = skill.Definition.Type == SkillType.Heal || skill.Definition.Type == SkillType.Summon ? 10 : 5;
                    break;
            }
            
            return baseExp + levelBonus + jobBonus;
        }

        /// <summary>
        /// 检查技能升级条件
        /// </summary>
        private bool CheckSkillLevelUpConditions(Player caster, PlayerSkill skill)
        {
            // 检查玩家等级是否足够
            if (caster.Level < skill.Definition.RequireLevel * skill.Level)
            {
                return false;
            }
            
            // 检查技能使用次数是否足够
            int requiredUses = skill.Level * 50; // 每级需要50次使用
            if (skill.UseCount < requiredUses)
            {
                return false;
            }
            
            // 检查是否有足够的金币（如果需要）
            var nextLevelData = skill.Definition.GetLevelData(skill.Level + 1);
            if (nextLevelData != null && caster.Gold < nextLevelData.LearnCost)
            {
                return false;
            }
            
            return true;
        }
    }
}
