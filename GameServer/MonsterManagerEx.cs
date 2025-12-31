using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MirCommon;
using MirCommon.Utils;

namespace GameServer
{
    /// <summary>
    /// 怪物管理器扩展
    /// </summary>
    public class MonsterManagerEx
    {
        private static MonsterManagerEx? _instance;
        public static MonsterManagerEx Instance => _instance ??= new MonsterManagerEx();

        // 怪物类定义哈希表
        private readonly Dictionary<string, MonsterClass> _monsterClassHash = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _classLock = new();

        // 怪物实例列表
        private readonly List<MonsterEx> _monsterList = new();
        private readonly object _monsterLock = new();

        // 当前活动怪物（用于脚本执行等）
        private MonsterEx? _activeMonster;

        private MonsterManagerEx()
        {
            // 初始化
        }

        /// <summary>
        /// 加载怪物定义文件
        /// </summary>
        public bool LoadMonsters(string fileName)
        {
            LogManager.Default.Info($"加载怪物定义文件: {fileName}");

            if (!File.Exists(fileName))
            {
                LogManager.Default.Error($"怪物定义文件不存在: {fileName}");
                return false;
            }

            try
            {
                var lines = SmartReader.ReadAllLines(fileName);
                MonsterClass? currentClass = null;

                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                        continue;

                    if (trimmedLine.StartsWith("@"))
                    {
                        // 开始新的怪物类定义
                        if (currentClass != null)
                        {
                            // 保存前一个怪物类
                            SaveMonsterClass(currentClass);
                        }

                        // 创建新的怪物类
                        currentClass = new MonsterClass();
                        
                        // 初始化怪物类
                        string className = trimmedLine.Substring(1).Trim();
                        if (className.Length > 16)
                            className = className.Substring(0, 16);

                        currentClass.Base.ClassName = className;
                        currentClass.Base.ViewName = string.Empty;
                        currentClass.Base.Race = 0;
                        currentClass.Base.Image = 0;
                        currentClass.Base.Level = 0;
                        currentClass.Base.NameColor = 0;
                        currentClass.Base.Feature = 0;
                    }
                    else if (currentClass != null)
                    {
                        // 解析属性行
                        ParsePropertyLine(currentClass, trimmedLine);
                    }
                }

                // 保存最后一个怪物类
                if (currentClass != null)
                {
                    SaveMonsterClass(currentClass);
                }

                LogManager.Default.Info($"成功加载 {_monsterClassHash.Count} 个怪物定义");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载怪物定义文件失败: {fileName}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 加载怪物脚本文件
        /// </summary>
        public void LoadMonsterScript(string fileName)
        {
            LogManager.Default.Info($"加载怪物脚本文件: {fileName}");

            if (!File.Exists(fileName))
            {
                LogManager.Default.Warning($"怪物脚本文件不存在: {fileName}");
                return;
            }

            try
            {
                var lines = SmartReader.ReadAllLines(fileName);
                int count = 0;

                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                        continue;

                    // 格式: 怪物名称=出生脚本,获得目标脚本,击杀目标脚本,受伤脚本,死亡脚本
                    var parts = trimmedLine.Split('=', 2);
                    if (parts.Length != 2)
                        continue;

                    string monsterName = parts[0].Trim();
                    string scriptData = parts[1].Trim();

                    var monsterClass = GetClassByName(monsterName);
                    if (monsterClass == null)
                    {
                        LogManager.Default.Debug($"怪物类未找到: {monsterName}");
                        continue;
                    }

                    var scriptParts = scriptData.Split(',');
                    
                    // 清理旧的脚本
                    monsterClass.BornScript = null;
                    monsterClass.GotTargetScript = null;
                    monsterClass.KillTargetScript = null;
                    monsterClass.HurtScript = null;
                    monsterClass.DeathScript = null;

                    // 设置新的脚本
                    if (scriptParts.Length > 0 && !string.IsNullOrEmpty(scriptParts[0]))
                        monsterClass.BornScript = scriptParts[0];
                    if (scriptParts.Length > 1 && !string.IsNullOrEmpty(scriptParts[1]))
                        monsterClass.GotTargetScript = scriptParts[1];
                    if (scriptParts.Length > 2 && !string.IsNullOrEmpty(scriptParts[2]))
                        monsterClass.KillTargetScript = scriptParts[2];
                    if (scriptParts.Length > 3 && !string.IsNullOrEmpty(scriptParts[3]))
                        monsterClass.HurtScript = scriptParts[3];
                    if (scriptParts.Length > 4 && !string.IsNullOrEmpty(scriptParts[4]))
                        monsterClass.DeathScript = scriptParts[4];

                    count++;
                }

                LogManager.Default.Info($"成功加载 {count} 个怪物脚本");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载怪物脚本文件失败: {fileName}", exception: ex);
            }
        }

        /// <summary>
        /// 创建怪物实例
        /// </summary>
        public MonsterEx? CreateMonster(string monsterName, int mapId, int x, int y, MonsterGen? gen = null)
        {
            var monsterClass = GetClassByName(monsterName);
            if (monsterClass == null)
            {
                LogManager.Default.Warning($"怪物类未找到: {monsterName}");
                return null;
            }

            var monster = new MonsterEx();
            if (!monster.Init(monsterClass, mapId, x, y, gen))
            {
                LogManager.Default.Warning($"初始化怪物失败: {monsterName}");
                return null;
            }

            // 添加到怪物列表
            lock (_monsterLock)
            {
                _monsterList.Add(monster);
            }

            // 更新生成器计数
            if (gen != null)
            {
                gen.CurrentCount++;
            }

            monsterClass.Count++;

            return monster;
        }

        /// <summary>
        /// 创建怪物实例（通过ID）
        /// </summary>
        public MonsterEx? CreateMonster(int monsterId)
        {
            // 通过ID查找怪物类
            var monsterClass = GetMonsterClass(monsterId);
            if (monsterClass == null)
            {
                LogManager.Default.Warning($"怪物类未找到: {monsterId}");
                return null;
            }

            return new MonsterEx();
        }

        /// <summary>
        /// 删除怪物
        /// </summary>
        public bool DeleteMonster(MonsterEx monster)
        {
            if (monster == null)
                return false;

            // 清理生成器引用
            monster.ClearGen();

            // 从列表中移除
            lock (_monsterLock)
            {
                _monsterList.Remove(monster);
            }

            // 清理怪物资源
            monster.Clean();

            return true;
        }

        /// <summary>
        /// 添加额外怪物（不通过生成器）
        /// </summary>
        public bool AddExtraMonster(MonsterEx monster)
        {
            if (monster == null)
                return false;

            lock (_monsterLock)
            {
                _monsterList.Add(monster);
            }

            // 设置怪物ID
            uint id = (uint)_monsterList.Count;
            id |= ((uint)ObjectType.Monster << 24);
            monster.SetId(id);

            return true;
        }

        /// <summary>
        /// 根据ID获取怪物
        /// </summary>
        public MonsterEx? GetMonsterById(uint id)
        {
            uint index = id & 0xffffff;
            lock (_monsterLock)
            {
                if (index > 0 && index <= _monsterList.Count)
                {
                    return _monsterList[(int)index - 1];
                }
            }
            return null;
        }

        /// <summary>
        /// 根据名称获取怪物类
        /// </summary>
        public MonsterClass? GetClassByName(string name)
        {
            lock (_classLock)
            {
                return _monsterClassHash.TryGetValue(name, out var monsterClass) ? monsterClass : null;
            }
        }

        /// <summary>
        /// 获取怪物数量
        /// </summary>
        public int GetCount()
        {
            lock (_monsterLock)
            {
                return _monsterList.Count;
            }
        }

        /// <summary>
        /// 获取当前活动怪物
        /// </summary>
        public MonsterEx? GetCurrentActiveMonster()
        {
            return _activeMonster;
        }

        /// <summary>
        /// 设置当前活动怪物
        /// </summary>
        public void SetCurrentActiveMonster(MonsterEx? monster)
        {
            _activeMonster = monster;
        }

        /// <summary>
        /// 获取所有怪物类
        /// </summary>
        public List<MonsterClass> GetAllMonsterClasses()
        {
            lock (_classLock)
            {
                return _monsterClassHash.Values.ToList();
            }
        }

        /// <summary>
        /// 获取所有怪物实例
        /// </summary>
        public List<MonsterEx> GetAllMonsters()
        {
            lock (_monsterLock)
            {
                return new List<MonsterEx>(_monsterList);
            }
        }

        /// <summary>
        /// 清理所有怪物
        /// </summary>
        public void ClearAllMonsters()
        {
            lock (_monsterLock)
            {
                foreach (var monster in _monsterList)
                {
                    monster.Clean();
                }
                _monsterList.Clear();
            }
        }

        /// <summary>
        /// 保存怪物类到哈希表
        /// </summary>
        private void SaveMonsterClass(MonsterClass monsterClass)
        {
            if (string.IsNullOrEmpty(monsterClass.Base.ClassName))
                return;

            lock (_classLock)
            {
                if (_monsterClassHash.TryGetValue(monsterClass.Base.ClassName, out var existingClass))
                {
                    // 更新现有怪物类
                    // 保留脚本引用
                    monsterClass.BornScript = existingClass.BornScript;
                    monsterClass.GotTargetScript = existingClass.GotTargetScript;
                    monsterClass.KillTargetScript = existingClass.KillTargetScript;
                    monsterClass.HurtScript = existingClass.HurtScript;
                    monsterClass.DeathScript = existingClass.DeathScript;

                    // 复制数据
                    CopyMonsterClass(existingClass, monsterClass);
                    LogManager.Default.Info($"怪物 {monsterClass.Base.ClassName} 被更新");
                }
                else
                {
                    // 添加新怪物类
                    _monsterClassHash[monsterClass.Base.ClassName] = monsterClass;
                }
            }
        }

        /// <summary>
        /// 复制怪物类数据
        /// </summary>
        private void CopyMonsterClass(MonsterClass dest, MonsterClass src)
        {
            // 复制基础信息
            dest.Base.ViewName = src.Base.ViewName;
            dest.Base.Race = src.Base.Race;
            dest.Base.Image = src.Base.Image;
            dest.Base.Level = src.Base.Level;
            dest.Base.NameColor = src.Base.NameColor;
            dest.Base.Feature = src.Base.Feature;

            // 复制属性
            dest.Prop.HP = src.Prop.HP;
            dest.Prop.MP = src.Prop.MP;
            dest.Prop.Hit = src.Prop.Hit;
            dest.Prop.Speed = src.Prop.Speed;
            dest.Prop.AC1 = src.Prop.AC1;
            dest.Prop.AC2 = src.Prop.AC2;
            dest.Prop.DC1 = src.Prop.DC1;
            dest.Prop.DC2 = src.Prop.DC2;
            dest.Prop.MAC1 = src.Prop.MAC1;
            dest.Prop.MAC2 = src.Prop.MAC2;
            dest.Prop.MC1 = src.Prop.MC1;
            dest.Prop.MC2 = src.Prop.MC2;
            dest.Prop.Exp = src.Prop.Exp;
            dest.Prop.AIDelay = src.Prop.AIDelay;
            dest.Prop.WalkDelay = src.Prop.WalkDelay;
            dest.Prop.RecoverHP = src.Prop.RecoverHP;
            dest.Prop.RecoverHPTime = src.Prop.RecoverHPTime;
            dest.Prop.RecoverMP = src.Prop.RecoverMP;
            dest.Prop.RecoverMPTime = src.Prop.RecoverMPTime;

            // 复制特殊属性
            dest.SProp.PFlag = src.SProp.PFlag;
            dest.SProp.CallRate = src.SProp.CallRate;
            dest.SProp.AntSoulWall = src.SProp.AntSoulWall;
            dest.SProp.AntTrouble = src.SProp.AntTrouble;
            dest.SProp.AntHolyWord = src.SProp.AntHolyWord;

            // 复制AI设置
            dest.AISet.MoveStyle = src.AISet.MoveStyle;
            dest.AISet.DieStyle = src.AISet.DieStyle;
            dest.AISet.TargetSelect = src.AISet.TargetSelect;
            dest.AISet.TargetFlag = src.AISet.TargetFlag;
            dest.AISet.ViewDistance = src.AISet.ViewDistance;
            dest.AISet.CoolEyes = src.AISet.CoolEyes;
            dest.AISet.EscapeDistance = src.AISet.EscapeDistance;
            dest.AISet.LockDir = src.AISet.LockDir;

            // 复制宠物设置
            dest.PetSet.Type = src.PetSet.Type;
            dest.PetSet.StopAt = src.PetSet.StopAt;

            // 复制攻击描述
            dest.AttackDesc.AttackStyle = src.AttackDesc.AttackStyle;
            dest.AttackDesc.AttackDistance = src.AttackDesc.AttackDistance;
            dest.AttackDesc.Delay = src.AttackDesc.Delay;
            dest.AttackDesc.DamageStyle = src.AttackDesc.DamageStyle;
            dest.AttackDesc.DamageRange = src.AttackDesc.DamageRange;
            dest.AttackDesc.DamageType = src.AttackDesc.DamageType;
            dest.AttackDesc.AppendEffect = src.AttackDesc.AppendEffect;
            dest.AttackDesc.AppendRate = src.AttackDesc.AppendRate;
            dest.AttackDesc.CostHP = src.AttackDesc.CostHP;
            dest.AttackDesc.CostMP = src.AttackDesc.CostMP;
            dest.AttackDesc.Action = src.AttackDesc.Action;
            dest.AttackDesc.AppendTime = src.AttackDesc.AppendTime;

            // 复制变身设置
            for (int i = 0; i < 3; i++)
            {
                dest.ChangeInto[i].Situation1.Situation = src.ChangeInto[i].Situation1.Situation;
                dest.ChangeInto[i].Situation1.Param = src.ChangeInto[i].Situation1.Param;
                dest.ChangeInto[i].Situation2.Situation = src.ChangeInto[i].Situation2.Situation;
                dest.ChangeInto[i].Situation2.Param = src.ChangeInto[i].Situation2.Param;
                dest.ChangeInto[i].ChangeInto = src.ChangeInto[i].ChangeInto;
                dest.ChangeInto[i].AppendEffect = src.ChangeInto[i].AppendEffect;
                dest.ChangeInto[i].Anim = src.ChangeInto[i].Anim;
                dest.ChangeInto[i].Enabled = src.ChangeInto[i].Enabled;
            }

            // 复制掉落物品
            dest.DownItems = src.DownItems;
        }

        /// <summary>
        /// 解析属性行
        /// </summary>
        private void ParsePropertyLine(MonsterClass monsterClass, string line)
        {
            try
            {
                // 移除注释
                int commentIndex = line.IndexOf('#');
                if (commentIndex >= 0)
                {
                    line = line.Substring(0, commentIndex).Trim();
                }

                if (string.IsNullOrEmpty(line))
                    return;

                // 分割键值对
                var parts = line.Split(':', 2);
                if (parts.Length != 2)
                {
                    LogManager.Default.Debug($"无效的属性行格式: {line}");
                    return;
                }

                string key = parts[0].Trim();
                string value = parts[1].Trim();

                // 根据键名解析属性
                switch (key.ToLower())
                {
                    case "base":
                        ParseBaseProperty(monsterClass, value);
                        break;
                    case "prop":
                        ParsePropProperty(monsterClass, value);
                        break;
                    case "sprop":
                        ParseSPropProperty(monsterClass, value);
                        break;
                    case "aiset":
                        ParseAISetProperty(monsterClass, value);
                        break;
                    case "petset":
                        ParsePetSetProperty(monsterClass, value);
                        break;
                    case "attack":
                        ParseAttackProperty(monsterClass, value);
                        break;
                    case "append":
                        ParseAppendProperty(monsterClass, value);
                        break;
                    case "chg1":
                        ParseChangeIntoProperty(monsterClass, 0, value);
                        break;
                    case "chg2":
                        ParseChangeIntoProperty(monsterClass, 1, value);
                        break;
                    case "chg3":
                        ParseChangeIntoProperty(monsterClass, 2, value);
                        break;
                    case "viewname":
                        monsterClass.Base.ViewName = value;
                        break;
                    case "race":
                        if (byte.TryParse(value, out byte race))
                            monsterClass.Base.Race = race;
                        break;
                    case "image":
                        if (byte.TryParse(value, out byte image))
                            monsterClass.Base.Image = image;
                        break;
                    case "level":
                        if (byte.TryParse(value, out byte level))
                            monsterClass.Base.Level = level;
                        break;
                    case "namecolor":
                        if (byte.TryParse(value, out byte nameColor))
                            monsterClass.Base.NameColor = nameColor;
                        break;
                    case "feature":
                        if (uint.TryParse(value, out uint feature))
                            monsterClass.Base.Feature = feature;
                        break;
                    case "hp":
                        if (ushort.TryParse(value, out ushort hp))
                            monsterClass.Prop.HP = hp;
                        break;
                    case "mp":
                        if (ushort.TryParse(value, out ushort mp))
                            monsterClass.Prop.MP = mp;
                        break;
                    case "hit":
                        if (byte.TryParse(value, out byte hit))
                            monsterClass.Prop.Hit = hit;
                        break;
                    case "speed":
                        if (byte.TryParse(value, out byte speed))
                            monsterClass.Prop.Speed = speed;
                        break;
                    case "ac1":
                        if (byte.TryParse(value, out byte ac1))
                            monsterClass.Prop.AC1 = ac1;
                        break;
                    case "ac2":
                        if (byte.TryParse(value, out byte ac2))
                            monsterClass.Prop.AC2 = ac2;
                        break;
                    case "dc1":
                        if (byte.TryParse(value, out byte dc1))
                            monsterClass.Prop.DC1 = dc1;
                        break;
                    case "dc2":
                        if (byte.TryParse(value, out byte dc2))
                            monsterClass.Prop.DC2 = dc2;
                        break;
                    case "mac1":
                        if (byte.TryParse(value, out byte mac1))
                            monsterClass.Prop.MAC1 = mac1;
                        break;
                    case "mac2":
                        if (byte.TryParse(value, out byte mac2))
                            monsterClass.Prop.MAC2 = mac2;
                        break;
                    case "mc1":
                        if (byte.TryParse(value, out byte mc1))
                            monsterClass.Prop.MC1 = mc1;
                        break;
                    case "mc2":
                        if (byte.TryParse(value, out byte mc2))
                            monsterClass.Prop.MC2 = mc2;
                        break;
                    case "exp":
                        if (uint.TryParse(value, out uint exp))
                            monsterClass.Prop.Exp = exp;
                        break;
                    case "aidelay":
                        if (ushort.TryParse(value, out ushort aiDelay))
                            monsterClass.Prop.AIDelay = aiDelay;
                        break;
                    case "walkdelay":
                        if (ushort.TryParse(value, out ushort walkDelay))
                            monsterClass.Prop.WalkDelay = walkDelay;
                        break;
                    case "recoverhp":
                        if (ushort.TryParse(value, out ushort recoverHP))
                            monsterClass.Prop.RecoverHP = recoverHP;
                        break;
                    case "recoverhptime":
                        if (ushort.TryParse(value, out ushort recoverHPTime))
                            monsterClass.Prop.RecoverHPTime = recoverHPTime;
                        break;
                    case "recovermp":
                        if (ushort.TryParse(value, out ushort recoverMP))
                            monsterClass.Prop.RecoverMP = recoverMP;
                        break;
                    case "recovermptime":
                        if (ushort.TryParse(value, out ushort recoverMPTime))
                            monsterClass.Prop.RecoverMPTime = recoverMPTime;
                        break;
                    case "pflag":
                        if (uint.TryParse(value, out uint pFlag))
                            monsterClass.SProp.PFlag = pFlag;
                        break;
                    case "callrate":
                        if (byte.TryParse(value, out byte callRate))
                            monsterClass.SProp.CallRate = callRate;
                        break;
                    case "antsoulwall":
                        if (byte.TryParse(value, out byte antSoulWall))
                            monsterClass.SProp.AntSoulWall = antSoulWall;
                        break;
                    case "anttrouble":
                        if (byte.TryParse(value, out byte antTrouble))
                            monsterClass.SProp.AntTrouble = antTrouble;
                        break;
                    case "antholyword":
                        if (byte.TryParse(value, out byte antHolyWord))
                            monsterClass.SProp.AntHolyWord = antHolyWord;
                        break;
                    case "movestyle":
                        if (byte.TryParse(value, out byte moveStyle))
                            monsterClass.AISet.MoveStyle = moveStyle;
                        break;
                    case "diestyle":
                        if (byte.TryParse(value, out byte dieStyle))
                            monsterClass.AISet.DieStyle = dieStyle;
                        break;
                    case "targetselect":
                        if (byte.TryParse(value, out byte targetSelect))
                            monsterClass.AISet.TargetSelect = targetSelect;
                        break;
                    case "targetflag":
                        if (byte.TryParse(value, out byte targetFlag))
                            monsterClass.AISet.TargetFlag = targetFlag;
                        break;
                    case "viewdistance":
                        if (byte.TryParse(value, out byte viewDistance))
                            monsterClass.AISet.ViewDistance = viewDistance;
                        break;
                    case "cooleyes":
                        if (byte.TryParse(value, out byte coolEyes))
                            monsterClass.AISet.CoolEyes = coolEyes;
                        break;
                    case "escapedistance":
                        if (byte.TryParse(value, out byte escapeDistance))
                            monsterClass.AISet.EscapeDistance = escapeDistance;
                        break;
                    case "lockdir":
                        if (byte.TryParse(value, out byte lockDir))
                            monsterClass.AISet.LockDir = lockDir;
                        break;
                    case "pettype":
                        if (byte.TryParse(value, out byte petType))
                            monsterClass.PetSet.Type = petType;
                        break;
                    case "petstopat":
                        if (byte.TryParse(value, out byte petStopAt))
                            monsterClass.PetSet.StopAt = petStopAt;
                        break;
                    case "attackstyle":
                        if (int.TryParse(value, out int attackStyle))
                            monsterClass.AttackDesc.AttackStyle = attackStyle;
                        break;
                    case "attackdistance":
                        if (int.TryParse(value, out int attackDistance))
                            monsterClass.AttackDesc.AttackDistance = attackDistance;
                        break;
                    case "delay":
                        if (int.TryParse(value, out int delay))
                            monsterClass.AttackDesc.Delay = delay;
                        break;
                    case "damagestyle":
                        if (int.TryParse(value, out int damageStyle))
                            monsterClass.AttackDesc.DamageStyle = damageStyle;
                        break;
                    case "damagerange":
                        if (int.TryParse(value, out int damageRange))
                            monsterClass.AttackDesc.DamageRange = damageRange;
                        break;
                    case "damagetype":
                        if (int.TryParse(value, out int damageType))
                            monsterClass.AttackDesc.DamageType = damageType;
                        break;
                    case "appendeffect":
                        if (int.TryParse(value, out int appendEffect))
                            monsterClass.AttackDesc.AppendEffect = appendEffect;
                        break;
                    case "appendrate":
                        if (int.TryParse(value, out int appendRate))
                            monsterClass.AttackDesc.AppendRate = appendRate;
                        break;
                    case "costhp":
                        if (int.TryParse(value, out int costHP))
                            monsterClass.AttackDesc.CostHP = costHP;
                        break;
                    case "costmp":
                        if (int.TryParse(value, out int costMP))
                            monsterClass.AttackDesc.CostMP = costMP;
                        break;
                    case "action":
                        if (ushort.TryParse(value, out ushort action))
                            monsterClass.AttackDesc.Action = action;
                        break;
                    case "appendtime":
                        if (ushort.TryParse(value, out ushort appendTime))
                            monsterClass.AttackDesc.AppendTime = appendTime;
                        break;
                    case "bornscript":
                        monsterClass.BornScript = value;
                        break;
                    case "gottargetscript":
                        monsterClass.GotTargetScript = value;
                        break;
                    case "killtargetscript":
                        monsterClass.KillTargetScript = value;
                        break;
                    case "hurtscript":
                        monsterClass.HurtScript = value;
                        break;
                    case "deathscript":
                        monsterClass.DeathScript = value;
                        break;
                    default:
                        // 检查是否是变身设置
                        if (key.StartsWith("changeinto", StringComparison.OrdinalIgnoreCase))
                        {
                            ParseChangeInto(monsterClass, key, value);
                        }
                        else
                        {
                            LogManager.Default.Debug($"未知的属性键: {key}");
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析属性行失败: {line}", exception: ex);
            }
        }

        /// <summary>
        /// 解析基础属性 (base:)
        /// 格式: ViewName/Race/Image/Level/NameColor
        /// </summary>
        private void ParseBaseProperty(MonsterClass monsterClass, string value)
        {
            try
            {
                var parts = value.Split('/');
                if (parts.Length >= 1)
                    monsterClass.Base.ViewName = parts[0];
                if (parts.Length >= 2 && byte.TryParse(parts[1], out byte race))
                    monsterClass.Base.Race = race;
                if (parts.Length >= 3 && byte.TryParse(parts[2], out byte image))
                    monsterClass.Base.Image = image;
                if (parts.Length >= 4 && byte.TryParse(parts[3], out byte level))
                    monsterClass.Base.Level = level;
                if (parts.Length >= 5 && byte.TryParse(parts[4], out byte nameColor))
                    monsterClass.Base.NameColor = nameColor;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析基础属性失败: {value}", exception: ex);
            }
        }

        /// <summary>
        /// 解析怪物属性 (prop:)
        /// 格式: HP/MP/Hit/Speed/AC1/AC2/DC1/DC2/MAC1/MAC2/MC1/MC2/Exp/AIDelay/WalkDelay/RecoverHP/RecoverHPTime/RecoverMP/RecoverMPTime
        /// </summary>
        private void ParsePropProperty(MonsterClass monsterClass, string value)
        {
            try
            {
                var parts = value.Split('/');
                if (parts.Length >= 1 && ushort.TryParse(parts[0], out ushort hp))
                    monsterClass.Prop.HP = hp;
                if (parts.Length >= 2 && ushort.TryParse(parts[1], out ushort mp))
                    monsterClass.Prop.MP = mp;
                if (parts.Length >= 3 && byte.TryParse(parts[2], out byte hit))
                    monsterClass.Prop.Hit = hit;
                if (parts.Length >= 4 && byte.TryParse(parts[3], out byte speed))
                    monsterClass.Prop.Speed = speed;
                if (parts.Length >= 5 && byte.TryParse(parts[4], out byte ac1))
                    monsterClass.Prop.AC1 = ac1;
                if (parts.Length >= 6 && byte.TryParse(parts[5], out byte ac2))
                    monsterClass.Prop.AC2 = ac2;
                if (parts.Length >= 7 && byte.TryParse(parts[6], out byte dc1))
                    monsterClass.Prop.DC1 = dc1;
                if (parts.Length >= 8 && byte.TryParse(parts[7], out byte dc2))
                    monsterClass.Prop.DC2 = dc2;
                if (parts.Length >= 9 && byte.TryParse(parts[8], out byte mac1))
                    monsterClass.Prop.MAC1 = mac1;
                if (parts.Length >= 10 && byte.TryParse(parts[9], out byte mac2))
                    monsterClass.Prop.MAC2 = mac2;
                if (parts.Length >= 11 && byte.TryParse(parts[10], out byte mc1))
                    monsterClass.Prop.MC1 = mc1;
                if (parts.Length >= 12 && byte.TryParse(parts[11], out byte mc2))
                    monsterClass.Prop.MC2 = mc2;
                if (parts.Length >= 13 && uint.TryParse(parts[12], out uint exp))
                    monsterClass.Prop.Exp = exp;
                if (parts.Length >= 14 && ushort.TryParse(parts[13], out ushort aiDelay))
                    monsterClass.Prop.AIDelay = aiDelay;
                if (parts.Length >= 15 && ushort.TryParse(parts[14], out ushort walkDelay))
                    monsterClass.Prop.WalkDelay = walkDelay;
                if (parts.Length >= 16 && ushort.TryParse(parts[15], out ushort recoverHP))
                    monsterClass.Prop.RecoverHP = recoverHP;
                if (parts.Length >= 17 && ushort.TryParse(parts[16], out ushort recoverHPTime))
                    monsterClass.Prop.RecoverHPTime = recoverHPTime;
                if (parts.Length >= 18 && ushort.TryParse(parts[17], out ushort recoverMP))
                    monsterClass.Prop.RecoverMP = recoverMP;
                if (parts.Length >= 19 && ushort.TryParse(parts[18], out ushort recoverMPTime))
                    monsterClass.Prop.RecoverMPTime = recoverMPTime;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析怪物属性失败: {value}", exception: ex);
            }
        }

        /// <summary>
        /// 解析特殊属性 (sprop:)
        /// 格式: PFlag/CallRate/AntSoulWall/AntTrouble/AntHolyWord
        /// </summary>
        private void ParseSPropProperty(MonsterClass monsterClass, string value)
        {
            try
            {
                var parts = value.Split('/');
                if (parts.Length >= 1 && uint.TryParse(parts[0], out uint pFlag))
                    monsterClass.SProp.PFlag = pFlag;
                if (parts.Length >= 2 && byte.TryParse(parts[1], out byte callRate))
                    monsterClass.SProp.CallRate = callRate;
                if (parts.Length >= 3 && byte.TryParse(parts[2], out byte antSoulWall))
                    monsterClass.SProp.AntSoulWall = antSoulWall;
                if (parts.Length >= 4 && byte.TryParse(parts[3], out byte antTrouble))
                    monsterClass.SProp.AntTrouble = antTrouble;
                if (parts.Length >= 5 && byte.TryParse(parts[4], out byte antHolyWord))
                    monsterClass.SProp.AntHolyWord = antHolyWord;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析特殊属性失败: {value}", exception: ex);
            }
        }

        /// <summary>
        /// 解析AI设置 (aiset:)
        /// 格式: MoveStyle/DieStyle/TargetSelect/TargetFlag/ViewDistance/CoolEyes/EscapeDistance/LockDir
        /// </summary>
        private void ParseAISetProperty(MonsterClass monsterClass, string value)
        {
            try
            {
                var parts = value.Split('/');
                if (parts.Length >= 1 && byte.TryParse(parts[0], out byte moveStyle))
                    monsterClass.AISet.MoveStyle = moveStyle;
                if (parts.Length >= 2 && byte.TryParse(parts[1], out byte dieStyle))
                    monsterClass.AISet.DieStyle = dieStyle;
                if (parts.Length >= 3 && byte.TryParse(parts[2], out byte targetSelect))
                    monsterClass.AISet.TargetSelect = targetSelect;
                if (parts.Length >= 4 && byte.TryParse(parts[3], out byte targetFlag))
                    monsterClass.AISet.TargetFlag = targetFlag;
                if (parts.Length >= 5 && byte.TryParse(parts[4], out byte viewDistance))
                    monsterClass.AISet.ViewDistance = viewDistance;
                if (parts.Length >= 6 && byte.TryParse(parts[5], out byte coolEyes))
                    monsterClass.AISet.CoolEyes = coolEyes;
                if (parts.Length >= 7 && byte.TryParse(parts[6], out byte escapeDistance))
                    monsterClass.AISet.EscapeDistance = escapeDistance;
                if (parts.Length >= 8 && byte.TryParse(parts[7], out byte lockDir))
                    monsterClass.AISet.LockDir = lockDir;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析AI设置失败: {value}", exception: ex);
            }
        }

        /// <summary>
        /// 解析宠物设置 (petset:)
        /// 格式: Type/StopAt
        /// </summary>
        private void ParsePetSetProperty(MonsterClass monsterClass, string value)
        {
            try
            {
                var parts = value.Split('/');
                if (parts.Length >= 1 && byte.TryParse(parts[0], out byte type))
                    monsterClass.PetSet.Type = type;
                if (parts.Length >= 2 && byte.TryParse(parts[1], out byte stopAt))
                    monsterClass.PetSet.StopAt = stopAt;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析宠物设置失败: {value}", exception: ex);
            }
        }

        /// <summary>
        /// 解析攻击属性 (attack:)
        /// 格式: AttackStyle/AttackDistance/Delay/DamageStyle/DamageRange/DamageType/AppendEffect/AppendRate/CostHP/CostMP
        /// </summary>
        private void ParseAttackProperty(MonsterClass monsterClass, string value)
        {
            try
            {
                var parts = value.Split('/');
                if (parts.Length < 10)
                {
                    LogManager.Default.Warning($"攻击属性字段不足10个: {value}");
                    return;
                }
                
                monsterClass.AttackDesc.AttackStyle = StringToInteger(parts[0]);
                monsterClass.AttackDesc.AttackDistance = StringToInteger(parts[1]);
                monsterClass.AttackDesc.Delay = StringToInteger(parts[2]);
                monsterClass.AttackDesc.DamageStyle = StringToInteger(parts[3]);
                monsterClass.AttackDesc.DamageRange = StringToInteger(parts[4]);
                monsterClass.AttackDesc.DamageType = StringToInteger(parts[5]);
                monsterClass.AttackDesc.AppendEffect = StringToInteger(parts[6]);
                monsterClass.AttackDesc.AppendRate = StringToInteger(parts[7]);
                monsterClass.AttackDesc.CostHP = StringToInteger(parts[8]);
                monsterClass.AttackDesc.CostMP = StringToInteger(parts[9]);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析攻击属性失败: {value}", exception: ex);
            }
        }

        /// <summary>
        /// 解析附加属性 (append:)
        /// 格式: Feature/Action/AppendTime
        /// </summary>
        private void ParseAppendProperty(MonsterClass monsterClass, string value)
        {
            try
            {
                var parts = value.Split('/');
                if (parts.Length < 1)
                {
                    LogManager.Default.Warning($"附加属性字段不足: {value}");
                    return;
                }
                
                // 第一个字段是Feature
                monsterClass.Base.Feature = (uint)StringToInteger(parts[0]);
                
                // 第二个字段是Action（可选）
                if (parts.Length > 1)
                    monsterClass.AttackDesc.Action = (ushort)StringToInteger(parts[1]);
                else
                    monsterClass.AttackDesc.Action = 0;
                
                // 第三个字段是AppendTime（可选）
                if (parts.Length > 2)
                    monsterClass.AttackDesc.AppendTime = (ushort)StringToInteger(parts[2]);
                else
                    monsterClass.AttackDesc.AppendTime = 0;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析附加属性失败: {value}", exception: ex);
            }
        }

        /// <summary>
        /// 解析变身属性 (chg1, chg2, chg3)
        /// 格式: Situation1/Param1/Situation2/Param2/ChangeInto/AppendEffect/Anim/Enabled
        /// </summary>
        private void ParseChangeIntoProperty(MonsterClass monsterClass, int index, string value)
        {
            try
            {
                if (index < 0 || index >= 3)
                    return;

                var parts = value.Split('/');
                var changeInto = monsterClass.ChangeInto[index];
                
                if (parts.Length >= 1 && int.TryParse(parts[0], out int situation1))
                    changeInto.Situation1.Situation = situation1;
                if (parts.Length >= 2 && int.TryParse(parts[1], out int param1))
                    changeInto.Situation1.Param = param1;
                if (parts.Length >= 3 && int.TryParse(parts[2], out int situation2))
                    changeInto.Situation2.Situation = situation2;
                if (parts.Length >= 4 && int.TryParse(parts[3], out int param2))
                    changeInto.Situation2.Param = param2;
                if (parts.Length >= 5)
                    changeInto.ChangeInto = parts[4];
                if (parts.Length >= 6 && int.TryParse(parts[5], out int appendEffect))
                    changeInto.AppendEffect = appendEffect;
                if (parts.Length >= 7 && bool.TryParse(parts[6], out bool anim))
                    changeInto.Anim = anim;
                if (parts.Length >= 8 && bool.TryParse(parts[7], out bool enabled))
                    changeInto.Enabled = enabled;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析变身属性失败: index={index}, value={value}", exception: ex);
            }
        }

        /// <summary>
        /// 解析变身设置
        /// </summary>
        private void ParseChangeInto(MonsterClass monsterClass, string key, string value)
        {
            try
            {
                // 格式: ChangeInto0=情境1,参数1,情境2,参数2,变身目标,附加效果,动画,启用
                if (key.Length < 10)
                    return;

                string indexStr = key.Substring(10);
                if (!int.TryParse(indexStr, out int index) || index < 0 || index >= 3)
                    return;

                var parts = value.Split(',');
                if (parts.Length < 8)
                    return;

                var changeInto = monsterClass.ChangeInto[index];
                
                // 情境1
                if (int.TryParse(parts[0], out int situation1))
                    changeInto.Situation1.Situation = situation1;
                
                // 参数1
                if (int.TryParse(parts[1], out int param1))
                    changeInto.Situation1.Param = param1;
                
                // 情境2
                if (int.TryParse(parts[2], out int situation2))
                    changeInto.Situation2.Situation = situation2;
                
                // 参数2
                if (int.TryParse(parts[3], out int param2))
                    changeInto.Situation2.Param = param2;
                
                // 变身目标
                changeInto.ChangeInto = parts[4];
                
                // 附加效果
                if (int.TryParse(parts[5], out int appendEffect))
                    changeInto.AppendEffect = appendEffect;
                
                // 动画
                if (bool.TryParse(parts[6], out bool anim))
                    changeInto.Anim = anim;
                
                // 启用
                if (bool.TryParse(parts[7], out bool enabled))
                    changeInto.Enabled = enabled;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析变身设置失败: {key}={value}", exception: ex);
            }
        }

        /// <summary>
        /// 获取下一个对象ID
        /// </summary>
        public uint GetNextObjectId()
        {
            lock (_monsterLock)
            {
                uint id = (uint)(_monsterList.Count + 1);
                id |= ((uint)ObjectType.Monster << 24);
                return id;
            }
        }

        /// <summary>
        /// 根据ID获取怪物类
        /// </summary>
        public MonsterClass? GetMonsterClass(int monsterId)
        {
            lock (_classLock)
            {
                // 通过ID查找怪物类
                foreach (var monsterClass in _monsterClassHash.Values)
                {
                    // 这里需要根据实际情况实现ID查找逻辑
                    return monsterClass;
                }
                return null;
            }
        }

        /// <summary>
        /// 字符串转整数
        /// </summary>
        private int StringToInteger(string str)
        {
            if (string.IsNullOrEmpty(str))
                return 0;
            
            // 移除空白字符
            str = str.Trim();
            
            // 尝试解析整数
            if (int.TryParse(str, out int result))
                return result;
            
            // 如果解析失败，尝试处理十六进制格式
            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                str = str.Substring(2);
                if (int.TryParse(str, System.Globalization.NumberStyles.HexNumber, null, out result))
                    return result;
            }
            
            // 如果还是失败，返回0
            LogManager.Default.Warning($"无法将字符串转换为整数: '{str}'");
            return 0;
        }
    }
}
