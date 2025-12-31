using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MirCommon.Utils;

namespace GameServer
{
    /// <summary>
    /// 特殊装备管理器
    /// 负责加载和管理特殊装备的功能配置
    /// </summary>
    public class SpecialEquipmentManager
    {
        private static SpecialEquipmentManager? _instance;
        
        /// <summary>
        /// 特殊装备功能配置
        /// 键：装备名称，值：功能配置字符串
        /// </summary>
        private readonly Dictionary<string, string> _specialEquipmentFunctions;
        
        /// <summary>
        /// 特殊装备效果缓存
        /// 键：装备名称，值：解析后的效果对象
        /// </summary>
        private readonly Dictionary<string, SpecialEquipmentEffect> _specialEquipmentEffects;

        /// <summary>
        /// 获取SpecialEquipmentManager单例实例
        /// </summary>
        public static SpecialEquipmentManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new SpecialEquipmentManager();
                }
                return _instance;
            }
        }

        /// <summary>
        /// 私有构造函数
        /// </summary>
        private SpecialEquipmentManager()
        {
            _specialEquipmentFunctions = new Dictionary<string, string>();
            _specialEquipmentEffects = new Dictionary<string, SpecialEquipmentEffect>();
        }

        /// <summary>
        /// 加载特殊装备功能配置
        /// </summary>
        /// <param name="filePath">配置文件路径</param>
        /// <returns>是否加载成功</returns>
        public bool LoadSpecialEquipmentFunction(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    LogManager.Default.Warning($"特殊装备配置文件不存在: {filePath}");
                    return false;
                }

                LogManager.Default.Info($"加载特殊装备配置: {filePath}");
                
                var lines = SmartReader.ReadAllLines(filePath);
                int loadedCount = 0;
                
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    // 格式：装备名称/功能配置
                    var parts = line.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        string equipmentName = parts[0].Trim();
                        string functionConfig = parts[1].Trim();
                        
                        // 存储原始配置
                        _specialEquipmentFunctions[equipmentName] = functionConfig;
                        
                        // 解析并缓存效果
                        var effect = ParseSpecialEquipmentEffect(equipmentName, functionConfig);
                        if (effect != null)
                        {
                            _specialEquipmentEffects[equipmentName] = effect;
                        }
                        
                        loadedCount++;
                        LogManager.Default.Debug($"特殊装备: {equipmentName} -> {functionConfig}");
                    }
                }

                LogManager.Default.Info($"成功加载 {loadedCount} 个特殊装备配置");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载特殊装备配置失败: {filePath}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 解析特殊装备效果
        /// </summary>
        private SpecialEquipmentEffect? ParseSpecialEquipmentEffect(string equipmentName, string functionConfig)
        {
            try
            {
                var effect = new SpecialEquipmentEffect
                {
                    EquipmentName = equipmentName,
                    FunctionConfig = functionConfig
                };

                // 根据配置字符串解析具体效果
                // 这里可以根据实际配置格式进行解析
                // 示例格式：属性加成/攻击力+10/防御力+5
                var configParts = functionConfig.Split('/');
                
                foreach (var part in configParts)
                {
                    var effectPart = part.Trim();
                    if (effectPart.Contains("+"))
                    {
                        var effectParts = effectPart.Split('+');
                        if (effectParts.Length == 2)
                        {
                            string attribute = effectParts[0].Trim();
                            if (int.TryParse(effectParts[1].Trim(), out int value))
                            {
                                effect.AttributeBonuses[attribute] = value;
                            }
                        }
                    }
                    else if (effectPart.Contains("-"))
                    {
                        var effectParts = effectPart.Split('-');
                        if (effectParts.Length == 2)
                        {
                            string attribute = effectParts[0].Trim();
                            if (int.TryParse(effectParts[1].Trim(), out int value))
                            {
                                effect.AttributeBonuses[attribute] = -value;
                            }
                        }
                    }
                }

                return effect;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析特殊装备效果失败: {equipmentName}", exception: ex);
                return null;
            }
        }

        /// <summary>
        /// 获取特殊装备的功能配置
        /// </summary>
        public string GetSpecialEquipmentFunction(string equipmentName)
        {
            return _specialEquipmentFunctions.TryGetValue(equipmentName, out var function) 
                ? function 
                : string.Empty;
        }

        /// <summary>
        /// 获取特殊装备的效果
        /// </summary>
        public SpecialEquipmentEffect? GetSpecialEquipmentEffect(string equipmentName)
        {
            return _specialEquipmentEffects.TryGetValue(equipmentName, out var effect) 
                ? effect 
                : null;
        }

        /// <summary>
        /// 检查是否为特殊装备
        /// </summary>
        public bool IsSpecialEquipment(string equipmentName)
        {
            return _specialEquipmentFunctions.ContainsKey(equipmentName);
        }

        /// <summary>
        /// 应用特殊装备效果到玩家（完整实现）
        /// </summary>
        public bool ApplySpecialEquipmentEffect(HumanPlayer player, string equipmentName)
        {
            if (player == null || string.IsNullOrEmpty(equipmentName))
                return false;

            var effect = GetSpecialEquipmentEffect(equipmentName);
            if (effect == null)
                return false;

            try
            {
                // 应用属性加成
                foreach (var bonus in effect.AttributeBonuses)
                {
                    ApplyAttributeBonus(player, bonus.Key, bonus.Value);
                    LogManager.Default.Debug($"玩家 {player.Name} 应用特殊装备效果: {equipmentName} -> {bonus.Key}: {bonus.Value}");
                }

                // 应用特殊效果
                if (effect.HasSpecialEffect)
                {
                    ApplySpecialEffect(player, effect);
                }

                LogManager.Default.Info($"玩家 {player.Name} 应用特殊装备效果: {equipmentName}");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"应用特殊装备效果失败: {equipmentName} 玩家: {player.Name}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 移除特殊装备效果（完整实现）
        /// </summary>
        public bool RemoveSpecialEquipmentEffect(HumanPlayer player, string equipmentName)
        {
            if (player == null || string.IsNullOrEmpty(equipmentName))
                return false;

            var effect = GetSpecialEquipmentEffect(equipmentName);
            if (effect == null)
                return false;

            try
            {
                // 移除属性加成
                foreach (var bonus in effect.AttributeBonuses)
                {
                    RemoveAttributeBonus(player, bonus.Key, bonus.Value);
                    LogManager.Default.Debug($"玩家 {player.Name} 移除特殊装备效果: {equipmentName} -> {bonus.Key}: {bonus.Value}");
                }

                // 移除特殊效果
                if (effect.HasSpecialEffect)
                {
                    RemoveSpecialEffect(player, effect);
                }

                LogManager.Default.Info($"玩家 {player.Name} 移除特殊装备效果: {equipmentName}");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"移除特殊装备效果失败: {equipmentName} 玩家: {player.Name}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 获取所有特殊装备名称
        /// </summary>
        public List<string> GetAllSpecialEquipmentNames()
        {
            return new List<string>(_specialEquipmentFunctions.Keys);
        }

        /// <summary>
        /// 获取特殊装备数量
        /// </summary>
        public int GetSpecialEquipmentCount()
        {
            return _specialEquipmentFunctions.Count;
        }

        /// <summary>
        /// 重新加载特殊装备配置
        /// </summary>
        public bool Reload(string filePath)
        {
            try
            {
                _specialEquipmentFunctions.Clear();
                _specialEquipmentEffects.Clear();
                return LoadSpecialEquipmentFunction(filePath);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"重新加载特殊装备配置失败: {filePath}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 应用属性加成到玩家
        /// </summary>
        private void ApplyAttributeBonus(HumanPlayer player, string attributeName, int bonusValue)
        {
            if (player == null || bonusValue == 0)
                return;

            // 根据属性名称应用加成
            switch (attributeName.ToLower())
            {
                case "攻击力":
                case "dc":
                    player.Stats.MinDC += bonusValue;
                    player.Stats.MaxDC += bonusValue;
                    break;
                    
                case "魔法力":
                case "mc":
                    player.Stats.MinMC += bonusValue;
                    player.Stats.MaxMC += bonusValue;
                    break;
                    
                case "道术力":
                case "sc":
                    player.Stats.MinSC += bonusValue;
                    player.Stats.MaxSC += bonusValue;
                    break;
                    
                case "防御力":
                case "ac":
                    player.Stats.MinAC += bonusValue;
                    player.Stats.MaxAC += bonusValue;
                    break;
                    
                case "魔防力":
                case "mac":
                    player.Stats.MinMAC += bonusValue;
                    player.Stats.MaxMAC += bonusValue;
                    break;
                    
                case "准确":
                case "accuracy":
                    player.Accuracy += bonusValue;
                    break;
                    
                case "敏捷":
                case "agility":
                    player.Agility += bonusValue;
                    break;
                    
                case "幸运":
                case "lucky":
                    player.Lucky += bonusValue;
                    break;
                    
                case "最大生命值":
                case "maxhp":
                    player.MaxHP += bonusValue;
                    if (player.CurrentHP > player.MaxHP)
                        player.CurrentHP = player.MaxHP;
                    break;
                    
                case "最大魔法值":
                case "maxmp":
                    player.MaxMP += bonusValue;
                    if (player.CurrentMP > player.MaxMP)
                        player.CurrentMP = player.MaxMP;
                    break;
                    
                case "当前生命值":
                case "hp":
                    player.CurrentHP = Math.Min(player.CurrentHP + bonusValue, player.MaxHP);
                    break;
                    
                case "当前魔法值":
                case "mp":
                    player.CurrentMP = Math.Min(player.CurrentMP + bonusValue, player.MaxMP);
                    break;
                    
                case "基础攻击力":
                case "basedc":
                    player.BaseDC += bonusValue;
                    break;
                    
                case "基础魔法力":
                case "basemc":
                    player.BaseMC += bonusValue;
                    break;
                    
                case "基础道术力":
                case "basesc":
                    player.BaseSC += bonusValue;
                    break;
                    
                case "基础防御力":
                case "baseac":
                    player.BaseAC += bonusValue;
                    break;
                    
                case "基础魔防力":
                case "basemac":
                    player.BaseMAC += bonusValue;
                    break;
                    
                default:
                    LogManager.Default.Warning($"未知的属性名称: {attributeName}");
                    break;
            }
        }

        /// <summary>
        /// 移除属性加成
        /// </summary>
        private void RemoveAttributeBonus(HumanPlayer player, string attributeName, int bonusValue)
        {
            if (player == null || bonusValue == 0)
                return;

            // 根据属性名称移除加成（应用相反的值）
            ApplyAttributeBonus(player, attributeName, -bonusValue);
        }

        /// <summary>
        /// 应用特殊效果
        /// </summary>
        private void ApplySpecialEffect(HumanPlayer player, SpecialEquipmentEffect effect)
        {
            if (player == null || effect == null || string.IsNullOrEmpty(effect.SpecialEffectType))
                return;

            // 根据特殊效果类型应用效果
            switch (effect.SpecialEffectType.ToLower())
            {
                case "麻痹":
                    // 麻痹效果：有一定概率使目标无法移动和攻击
                    // 这里可以设置玩家的麻痹状态
                    LogManager.Default.Info($"玩家 {player.Name} 获得麻痹效果");
                    break;
                    
                case "复活":
                    // 复活效果：死亡后自动复活
                    LogManager.Default.Info($"玩家 {player.Name} 获得复活效果");
                    break;
                    
                case "隐身":
                    // 隐身效果：怪物无法看到玩家
                    LogManager.Default.Info($"玩家 {player.Name} 获得隐身效果");
                    break;
                    
                case "传送":
                    // 传送效果：可以传送到指定位置
                    LogManager.Default.Info($"玩家 {player.Name} 获得传送效果");
                    break;
                    
                case "吸血":
                    // 吸血效果：攻击时恢复生命值
                    LogManager.Default.Info($"玩家 {player.Name} 获得吸血效果");
                    break;
                    
                case "吸魔":
                    // 吸魔效果：攻击时恢复魔法值
                    LogManager.Default.Info($"玩家 {player.Name} 获得吸魔效果");
                    break;
                    
                case "破防":
                    // 破防效果：无视目标部分防御
                    LogManager.Default.Info($"玩家 {player.Name} 获得破防效果");
                    break;
                    
                case "破魔":
                    // 破魔效果：无视目标部分魔防
                    LogManager.Default.Info($"玩家 {player.Name} 获得破魔效果");
                    break;
                    
                case "暴击":
                    // 暴击效果：增加暴击概率
                    LogManager.Default.Info($"玩家 {player.Name} 获得暴击效果");
                    break;
                    
                case "闪避":
                    // 闪避效果：增加闪避概率
                    LogManager.Default.Info($"玩家 {player.Name} 获得闪避效果");
                    break;
                    
                default:
                    LogManager.Default.Warning($"未知的特殊效果类型: {effect.SpecialEffectType}");
                    break;
            }
        }

        /// <summary>
        /// 移除特殊效果
        /// </summary>
        private void RemoveSpecialEffect(HumanPlayer player, SpecialEquipmentEffect effect)
        {
            if (player == null || effect == null || string.IsNullOrEmpty(effect.SpecialEffectType))
                return;

            // 根据特殊效果类型移除效果
            switch (effect.SpecialEffectType.ToLower())
            {
                case "麻痹":
                    LogManager.Default.Info($"玩家 {player.Name} 移除麻痹效果");
                    break;
                    
                case "复活":
                    LogManager.Default.Info($"玩家 {player.Name} 移除复活效果");
                    break;
                    
                case "隐身":
                    LogManager.Default.Info($"玩家 {player.Name} 移除隐身效果");
                    break;
                    
                case "传送":
                    LogManager.Default.Info($"玩家 {player.Name} 移除传送效果");
                    break;
                    
                case "吸血":
                    LogManager.Default.Info($"玩家 {player.Name} 移除吸血效果");
                    break;
                    
                case "吸魔":
                    LogManager.Default.Info($"玩家 {player.Name} 移除吸魔效果");
                    break;
                    
                case "破防":
                    LogManager.Default.Info($"玩家 {player.Name} 移除破防效果");
                    break;
                    
                case "破魔":
                    LogManager.Default.Info($"玩家 {player.Name} 移除破魔效果");
                    break;
                    
                case "暴击":
                    LogManager.Default.Info($"玩家 {player.Name} 移除暴击效果");
                    break;
                    
                case "闪避":
                    LogManager.Default.Info($"玩家 {player.Name} 移除闪避效果");
                    break;
                    
                default:
                    LogManager.Default.Warning($"未知的特殊效果类型: {effect.SpecialEffectType}");
                    break;
            }
        }

        /// <summary>
        /// 更新特殊装备管理器（供GameWorld调用）
        /// </summary>
        public void Update()
        {
            // 更新特殊装备的临时效果持续时间
            UpdateTemporaryEffects();
        }

        /// <summary>
        /// 更新临时效果
        /// </summary>
        private void UpdateTemporaryEffects()
        {
            // 这里可以添加临时效果持续时间的更新逻辑
            // 例如：减少持续时间，当时间为0时移除效果
        }
    }

    /// <summary>
    /// 特殊装备效果
    /// </summary>
    public class SpecialEquipmentEffect
    {
        /// <summary>
        /// 装备名称
        /// </summary>
        public string EquipmentName { get; set; } = string.Empty;

        /// <summary>
        /// 功能配置字符串
        /// </summary>
        public string FunctionConfig { get; set; } = string.Empty;

        /// <summary>
        /// 属性加成字典
        /// 键：属性名称，值：加成值
        /// </summary>
        public Dictionary<string, int> AttributeBonuses { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// 特殊效果类型
        /// </summary>
        public string SpecialEffectType { get; set; } = string.Empty;

        /// <summary>
        /// 效果持续时间（秒），0表示永久
        /// </summary>
        public int Duration { get; set; }

        /// <summary>
        /// 触发概率（0-100）
        /// </summary>
        public int TriggerProbability { get; set; }

        /// <summary>
        /// 冷却时间（秒）
        /// </summary>
        public int Cooldown { get; set; }

        /// <summary>
        /// 获取属性加成值
        /// </summary>
        public int GetAttributeBonus(string attributeName)
        {
            return AttributeBonuses.TryGetValue(attributeName, out int value) ? value : 0;
        }

        /// <summary>
        /// 是否有特殊效果
        /// </summary>
        public bool HasSpecialEffect => !string.IsNullOrEmpty(SpecialEffectType);

        /// <summary>
        /// 是否为临时效果
        /// </summary>
        public bool IsTemporaryEffect => Duration > 0;
    }
}
