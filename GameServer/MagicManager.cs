using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MirCommon.Utils;

namespace GameServer
{
    /// <summary>
    /// 魔法技能管理器
    /// </summary>
    public class MagicManager
    {
        private static MagicManager? _instance;
        public static MagicManager Instance => _instance ??= new MagicManager();

        private string? _errorMsg;
        private readonly List<MagicClass> _magicClassList = new();
        private readonly Dictionary<string, MagicClass> _magicClassHash = new();
        private readonly MagicClass?[] _magicArray = new MagicClass[512]; 

        private MagicManager()
        {
            // 初始化数组
            for (int i = 0; i < _magicArray.Length; i++)
            {
                _magicArray[i] = null;
            }
        }

        /// <summary>
        /// 获取错误信息
        /// </summary>
        public string? GetErrorMsg() => _errorMsg;

        /// <summary>
        /// 加载魔法技能文件
        /// </summary>
        /// <param name="magicFile">魔法技能文件路径</param>
        public void LoadMagic(string magicFile)
        {
            if (!File.Exists(magicFile))
            {
                _errorMsg = $"魔法技能文件不存在: {magicFile}";
                LogManager.Default.Error(_errorMsg);
                return;
            }

            try
            {
                var lines = SmartReader.ReadAllLines(magicFile);
                int successCount = 0;
                int failCount = 0;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                        continue;

                    if (!AddMagicClassString(line))
                    {
                        failCount++;
                        LogManager.Default.Warning($"无法解析的魔法技能信息: {line}");
                    }
                    else
                    {
                        successCount++;
                    }
                }

                LogManager.Default.Info($"魔法技能加载完成: 成功{successCount}个, 失败{failCount}个");
            }
            catch (Exception ex)
            {
                _errorMsg = $"加载魔法技能文件失败: {ex.Message}";
                LogManager.Default.Error(_errorMsg, exception: ex);
            }
        }

        /// <summary>
        /// 解析魔法技能字符串
        /// 格式: name/id/job/effecttype/effectvalue/needlv1/lv1exp/needlv2/lv2exp/needlv3/lv3exp/spl/pwr/maxpwr/dspl/dpwr/dmaxpwr/delay/desc
        /// </summary>
        private bool AddMagicClassString(string magicClassDesc)
        {
            try
            {
                var parts = magicClassDesc.Split('/');
                // 最少需要19个参数
                if (parts.Length < 19)
                {
                    _errorMsg = $"参数不足: {parts.Length} < 19";
                    return false;
                }

                var magicClass = new MagicClass();

                // 解析基本参数
                magicClass.szName = parts[0].Trim();
                magicClass.id = uint.Parse(parts[1].Trim());
                magicClass.btJob = byte.Parse(parts[2].Trim());
                magicClass.btEffectType = byte.Parse(parts[3].Trim());
                magicClass.btEffectValue = byte.Parse(parts[4].Trim());

                // 解析等级需求
                magicClass.btNeedLv[0] = byte.Parse(parts[5].Trim());
                magicClass.dwNeedExp[0] = uint.Parse(parts[6].Trim());
                magicClass.btNeedLv[1] = byte.Parse(parts[7].Trim());
                magicClass.dwNeedExp[1] = uint.Parse(parts[8].Trim());
                magicClass.btNeedLv[2] = byte.Parse(parts[9].Trim());
                magicClass.dwNeedExp[2] = uint.Parse(parts[10].Trim());

                // 第3级和第4级使用第2级的值
                magicClass.btNeedLv[3] = magicClass.btNeedLv[2];
                magicClass.dwNeedExp[3] = magicClass.dwNeedExp[2];

                // 解析魔法属性
                magicClass.sSpell = short.Parse(parts[11].Trim());
                magicClass.sPower = short.Parse(parts[12].Trim());
                magicClass.sMaxPower = short.Parse(parts[13].Trim());
                magicClass.sDefSpell = short.Parse(parts[14].Trim());
                magicClass.sDefPower = short.Parse(parts[15].Trim());
                magicClass.sDefMaxPower = short.Parse(parts[16].Trim());

                // 解析延迟和描述
                magicClass.wDelay = ushort.Parse(parts[17].Trim());
                magicClass.szDesc = parts[18].Trim();

                // 解析前置技能（第20个参数）
                if (parts.Length > 19 && !string.IsNullOrWhiteSpace(parts[19]))
                {
                    var needMagicParts = parts[19].Split('|');
                    for (int i = 0; i < Math.Min(needMagicParts.Length, 3); i++)
                    {
                        if (!string.IsNullOrWhiteSpace(needMagicParts[i]))
                        {
                            magicClass.wNeedMagic[i] = ushort.Parse(needMagicParts[i].Trim());
                        }
                    }
                }

                // 解析互斥技能（第21个参数）
                if (parts.Length > 20 && !string.IsNullOrWhiteSpace(parts[20]))
                {
                    var mutexMagicParts = parts[20].Split('|');
                    for (int i = 0; i < Math.Min(mutexMagicParts.Length, 3); i++)
                    {
                        if (!string.IsNullOrWhiteSpace(mutexMagicParts[i]))
                        {
                            magicClass.wMutexMagic[i] = ushort.Parse(mutexMagicParts[i].Trim());
                        }
                    }
                }

                // 添加到管理器
                return AddMagicClass(magicClass);
            }
            catch (Exception ex)
            {
                _errorMsg = $"解析魔法技能字符串失败: {ex.Message}";
                LogManager.Default.Error(_errorMsg, exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 添加魔法技能类
        /// </summary>
        private bool AddMagicClass(MagicClass magicClass)
        {
            try
            {
                // 检查是否已存在同名技能
                if (_magicClassHash.TryGetValue(magicClass.szName, out var existingClass))
                {
                    // 更新现有技能
                    UpdateMagicClass(existingClass, magicClass);
                    return true;
                }

                // 创建新技能
                var newMagicClass = new MagicClass();
                CopyMagicClass(newMagicClass, magicClass);

                // 添加到列表和哈希表
                _magicClassList.Add(newMagicClass);
                _magicClassHash[newMagicClass.szName] = newMagicClass;

                // 添加到数组（如果ID在范围内）
                if (newMagicClass.id < _magicArray.Length)
                {
                    _magicArray[newMagicClass.id] = newMagicClass;
                }
                else
                {
                    LogManager.Default.Warning($"魔法技能ID超出范围: {newMagicClass.id} >= {_magicArray.Length}");
                }

                return true;
            }
            catch (Exception ex)
            {
                _errorMsg = $"添加魔法技能类失败: {ex.Message}";
                LogManager.Default.Error(_errorMsg, exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 更新魔法技能类
        /// </summary>
        private void UpdateMagicClass(MagicClass target, MagicClass source)
        {
            target.id = source.id;
            target.btJob = source.btJob;
            target.btEffectType = source.btEffectType;
            target.btEffectValue = source.btEffectValue;
            
            for (int i = 0; i < 4; i++)
            {
                target.btNeedLv[i] = source.btNeedLv[i];
                target.dwNeedExp[i] = source.dwNeedExp[i];
            }
            
            target.sSpell = source.sSpell;
            target.sPower = source.sPower;
            target.sMaxPower = source.sMaxPower;
            target.sDefSpell = source.sDefSpell;
            target.sDefPower = source.sDefPower;
            target.sDefMaxPower = source.sDefMaxPower;
            target.wDelay = source.wDelay;
            target.szDesc = source.szDesc;
            
            for (int i = 0; i < 3; i++)
            {
                target.wNeedMagic[i] = source.wNeedMagic[i];
                target.wMutexMagic[i] = source.wMutexMagic[i];
            }
        }

        /// <summary>
        /// 复制魔法技能类
        /// </summary>
        private void CopyMagicClass(MagicClass target, MagicClass source)
        {
            target.szName = source.szName;
            UpdateMagicClass(target, source);
        }

        /// <summary>
        /// 根据ID获取魔法技能类
        /// </summary>
        public MagicClass? GetClassById(int id)
        {
            if (id >= 0 && id < _magicArray.Length)
            {
                return _magicArray[id];
            }
            return null;
        }

        /// <summary>
        /// 根据名称获取魔法技能类
        /// </summary>
        public MagicClass? GetClassByName(string magicName)
        {
            return _magicClassHash.TryGetValue(magicName, out var magicClass) ? magicClass : null;
        }

        /// <summary>
        /// 根据名称创建魔法技能
        /// </summary>
        public bool CreateMagic(string magicName, out Magic magic)
        {
            magic = new Magic();
            
            var magicClass = GetClassByName(magicName);
            if (magicClass == null)
            {
                _errorMsg = $"找不到魔法技能: {magicName}";
                return false;
            }

            GetMagicFromClass(magicClass, magic);
            return true;
        }

        /// <summary>
        /// 根据ID创建魔法技能
        /// </summary>
        public bool CreateMagic(uint id, out Magic magic)
        {
            magic = new Magic();
            
            var magicClass = GetClassById((int)id);
            if (magicClass == null)
            {
                _errorMsg = $"找不到魔法技能ID: {id}";
                return false;
            }

            GetMagicFromClass(magicClass, magic);
            return true;
        }

        /// <summary>
        /// 从魔法技能类创建魔法技能
        /// </summary>
        public void GetMagicFromClass(MagicClass magicClass, Magic magic)
        {
            if (magicClass == null) return;

            // 复制名称
            magic.szName = magicClass.szName.Length > 12 ? magicClass.szName.Substring(0, 12) : magicClass.szName;
            magic.btNameLength = (byte)magic.szName.Length;

            // 设置基本属性
            magic.btLevel = 0;
            magic.wDelayTime = magicClass.wDelay;
            magic.wId = (ushort)magicClass.id;
            magic.job = magicClass.btJob;

            // 设置等级需求
            for (int i = 0; i < 4; i++)
            {
                magic.btNeedLevel[i] = magicClass.btNeedLv[i];
                magic.iLevelupExp[i] = (int)magicClass.dwNeedExp[i];
            }

            // 设置效果
            magic.btEffectType = magicClass.btEffectType;
            magic.btEffect = magicClass.btEffectValue;

            // 设置威力
            magic.wPower = (ushort)magicClass.sPower;
            magic.wSpell = (ushort)magicClass.sSpell;
            magic.wMaxPower = (ushort)magicClass.sMaxPower;
            magic.wDefMaxPower = (ushort)magicClass.sDefMaxPower;
            magic.btDefPower = (byte)magicClass.sDefPower;
            magic.btDefSpell = (byte)magicClass.sDefSpell;

            // 其他字段
            magic.cKey = '\0';
        }

        /// <summary>
        /// 加载魔法技能扩展文件
        /// </summary>
        public void LoadMagicExt(string magicExtFile, bool isCsv = false)
        {
            if (!File.Exists(magicExtFile))
            {
                _errorMsg = $"魔法技能扩展文件不存在: {magicExtFile}";
                LogManager.Default.Error(_errorMsg);
                return;
            }

            try
            {
                var lines = SmartReader.ReadAllLines(magicExtFile);
                int startLine = isCsv ? 1 : 0; // CSV文件有标题行
                int loadedCount = 0;

                for (int i = startLine; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                        continue;

                    var parts = isCsv ? ParseCsvLine(line) : line.Split('\t');
                    if (parts.Length < 10) continue;

                    // 解析扩展数据
                    string magicName = parts[0].Trim();
                    bool bNoEffect = Helper.BoolParser(parts[1].Trim());
                    bool bActive = Helper.BoolParser(parts[2].Trim());
                    uint bForced = uint.Parse(parts[3].Trim());
                    ushort wCharmCount = ushort.Parse(parts[4].Trim());
                    ushort wRedPoisonCount = ushort.Parse(parts[5].Trim());
                    ushort wGreenPoisonCount = ushort.Parse(parts[6].Trim());
                    ushort wStrawManCount = ushort.Parse(parts[7].Trim());
                    ushort wStrawWomanCount = ushort.Parse(parts[8].Trim());
                    string szSpecial = parts.Length > 9 ? parts[9].Trim() : "";

                    // 查找对应的魔法技能类
                    var magicClass = GetClassByName(magicName);
                    if (magicClass != null)
                    {
                        // 设置特殊字段
                        magicClass.szSpecial = szSpecial;

                        // 设置标志位
                        if (bNoEffect)
                            magicClass.dwFlag |= (uint)MagicFlag.MAGICFLAG_NOEFFECT;

                        if (bActive)
                            magicClass.dwFlag |= (uint)MagicFlag.MAGICFLAG_ACTIVED;

                        if (bForced == 2)
                            magicClass.dwFlag |= (uint)MagicFlag.MAGICFLAG_FORCED_EXP;
                        else if (bForced != 0)
                            magicClass.dwFlag |= (uint)MagicFlag.MAGICFLAG_FORCED;

                        // 设置消耗物品数量
                        magicClass.wCharmCount = wCharmCount;
                        if (wCharmCount > 0)
                            magicClass.dwFlag |= (uint)MagicFlag.MAGICFLAG_USECHARM;

                        magicClass.wRedPoisonCount = wRedPoisonCount;
                        if (wRedPoisonCount > 0)
                            magicClass.dwFlag |= (uint)MagicFlag.MAGICFLAG_USEREDPOISON;

                        magicClass.wGreenPoisonCount = wGreenPoisonCount;
                        if (wGreenPoisonCount > 0)
                            magicClass.dwFlag |= (uint)MagicFlag.MAGICFLAG_USEGREENPOISON;

                        magicClass.wStrawManCount = wStrawManCount;
                        if (wStrawManCount > 0)
                            magicClass.dwFlag |= (uint)MagicFlag.MAGICFLAG_USESTRAWMAN;

                        magicClass.wStrawWomanCount = wStrawWomanCount;
                        if (wStrawWomanCount > 0)
                            magicClass.dwFlag |= (uint)MagicFlag.MAGICFLAG_USESTRAWWOMAN;

                        loadedCount++;
                    }
                }

                LogManager.Default.Info($"魔法技能扩展加载完成: {loadedCount}个");
            }
            catch (Exception ex)
            {
                _errorMsg = $"加载魔法技能扩展文件失败: {ex.Message}";
                LogManager.Default.Error(_errorMsg, exception: ex);
            }
        }

        /// <summary>
        /// 解析CSV行（处理引号内的逗号）
        /// </summary>
        private string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            foreach (char c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            result.Add(current.ToString());
            return result.ToArray();
        }

        /// <summary>
        /// 保存魔法技能列表
        /// </summary>
        public void SaveMagicList(string filename)
        {
            try
            {
                using var writer = new StreamWriter(filename, false, Encoding.GetEncoding("GBK"));
                
                foreach (var magicClass in _magicClassList)
                {
                    if (magicClass != null)
                    {
                        // 格式: name/id/job/effecttype/effectvalue/needlv1/lv1exp/needlv2/lv2exp/needlv3/lv3exp/spl/pwr/maxpwr/dspl/dpwr/dmaxpwr/delay/desc
                        writer.WriteLine($"{magicClass.szName}/{magicClass.id}/{magicClass.btJob}/{magicClass.btEffectType}/{magicClass.btEffectValue}/" +
                                         $"{magicClass.btNeedLv[0]}/{magicClass.dwNeedExp[0]}/" +
                                         $"{magicClass.btNeedLv[1]}/{magicClass.dwNeedExp[1]}/" +
                                         $"{magicClass.btNeedLv[2]}/{magicClass.dwNeedExp[2]}/" +
                                         $"{magicClass.sSpell}/{magicClass.sPower}/{magicClass.sMaxPower}/" +
                                         $"{magicClass.sDefSpell}/{magicClass.sDefPower}/{magicClass.sDefMaxPower}/" +
                                         $"{magicClass.wDelay}/{magicClass.szDesc}");
                    }
                }
                
                LogManager.Default.Info($"魔法技能列表已保存到: {filename}");
            }
            catch (Exception ex)
            {
                _errorMsg = $"保存魔法技能列表失败: {ex.Message}";
                LogManager.Default.Error(_errorMsg, exception: ex);
            }
        }

        /// <summary>
        /// 获取魔法技能数量
        /// </summary>
        public int GetMagicCount()
        {
            return _magicClassList.Count;
        }

        /// <summary>
        /// 加载所有魔法技能配置（用于ConfigLoader集成）
        /// </summary>
        public bool LoadAll()
        {
            try
            {
                string dataPath = "./data";
                string magicFile = Path.Combine(dataPath, "basemagic.txt");
                string magicExtFile = Path.Combine(dataPath, "magicext.csv");

                // 加载基础魔法技能
                if (File.Exists(magicFile))
                {
                    LoadMagic(magicFile);
                    LogManager.Default.Info($"基础魔法技能加载完成: {magicFile}");
                }
                else
                {
                    LogManager.Default.Warning($"基础魔法技能文件不存在: {magicFile}");
                }

                // 加载魔法技能扩展
                if (File.Exists(magicExtFile))
                {
                    LoadMagicExt(magicExtFile, true);
                    LogManager.Default.Info($"魔法技能扩展加载完成: {magicExtFile}");
                }
                else
                {
                    LogManager.Default.Warning($"魔法技能扩展文件不存在: {magicExtFile}");
                }

                return true;
            }
            catch (Exception ex)
            {
                _errorMsg = $"加载所有魔法技能配置失败: {ex.Message}";
                LogManager.Default.Error(_errorMsg, exception: ex);
                return false;
            }
        }
    }
}
