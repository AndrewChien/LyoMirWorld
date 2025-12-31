using System;
using System.Collections.Generic;
using System.IO;
using MirCommon.Utils;

namespace GameServer.Parsers
{
    /// <summary>
    /// 技能/魔法数据
    /// </summary>
    public class MagicData
    {
        public string Name { get; set; } = "";
        public byte MagicID { get; set; }
        public byte EffectType { get; set; }
        public byte Effect { get; set; }
        public byte Spell { get; set; }
        public byte Power { get; set; }
        public ushort MaxPower { get; set; }
        public byte Job { get; set; }
        public ushort NeedL1 { get; set; }
        public ushort NeedL2 { get; set; }
        public ushort NeedL3 { get; set; }
        public byte Train1 { get; set; }
        public byte Train2 { get; set; }
        public byte Train3 { get; set; }
        public int Delay { get; set; }
    }

    /// <summary>
    /// 技能/魔法数据解析器 (basemagic.txt)
    /// </summary>
    public class MagicDataParser
    {
        private readonly Dictionary<string, MagicData> _magics = new();

        public bool Load(string filePath)
        {
            if (!File.Exists(filePath)) return false;

            try
            {
                // 使用ANSI编码读取文件
                var lines = SmartReader.ReadAllLines(filePath);
                int count = 0;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                        continue;

                    var parts = line.Split('/');
                    if (parts.Length < 14) continue;

                    var magic = new MagicData
                    {
                        Name = parts[0].Trim(),
                        MagicID = byte.Parse(parts[1].Trim()),
                        EffectType = byte.Parse(parts[2].Trim()),
                        Effect = byte.Parse(parts[3].Trim()),
                        Spell = byte.Parse(parts[4].Trim()),
                        Power = byte.Parse(parts[5].Trim()),
                        MaxPower = ushort.Parse(parts[6].Trim()),
                        Job = byte.Parse(parts[7].Trim()),
                        NeedL1 = ushort.Parse(parts[8].Trim()),
                        NeedL2 = ushort.Parse(parts[9].Trim()),
                        NeedL3 = ushort.Parse(parts[10].Trim()),
                        Train1 = byte.Parse(parts[11].Trim()),
                        Train2 = byte.Parse(parts[12].Trim()),
                        Train3 = byte.Parse(parts[13].Trim())
                    };

                    if (parts.Length > 14)
                        magic.Delay = int.Parse(parts[14].Trim());

                    _magics[magic.Name] = magic;
                    count++;
                }

                LogManager.Default.Info($"成功加载 {count} 个技能数据");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载技能数据失败: {filePath}", exception: ex);
                return false;
            }
        }

        public MagicData? GetMagic(string name) => _magics.TryGetValue(name, out var magic) ? magic : null;
    }

    /// <summary>
    /// NPC生成数据
    /// </summary>
    public class NpcGenData
    {
        public string Name { get; set; } = "";
        public int ID { get; set; }
        public int View { get; set; }
        public int MapID { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public bool Istalk { get; set; }
        public string ScriptName { get; set; } = "";
        public int Flag { get; set; }
    }

    /// <summary>
    /// NPC配置解析器 (npcgen.txt)
    /// 格式: 名称/地图ID/X/Y/脚本名/标志
    /// </summary>
    public class NpcConfigParser
    {
        private readonly List<NpcGenData> _npcs = new();

        public bool Load(string filePath)
        {
            if (!File.Exists(filePath)) return false;

            try
            {
                // 使用ANSI编码读取文件
                var lines = SmartReader.ReadAllLines(filePath);
                int count = 0;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                        continue;

                    var parts = line.Split(new[] { '/', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 4) continue;

                    Helper.TryHexToInt(parts[2].Trim(), out int view);
                    var npc = new NpcGenData
                    {
                        Name = parts[0].Trim(),
                        ID = int.Parse(parts[1].Trim()),
                        View = view,
                        MapID = int.Parse(parts[3].Trim()),
                        X = int.Parse(parts[4].Trim()),
                        Y = int.Parse(parts[5].Trim()),
                        Istalk = parts.Length > 6 ? Helper.BoolParser(parts[5].Trim()):false,
                        ScriptName = parts.Length > 7 ? parts[7].Trim():""
                    };

                    if (parts.Length > 4)
                        npc.ScriptName = parts[4].Trim();
                    if (parts.Length > 5)
                        npc.Flag = int.Parse(parts[5].Trim());

                    _npcs.Add(npc);
                    count++;
                }

                LogManager.Default.Info($"成功加载 {count} 个NPC配置");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载NPC配置失败: {filePath}", exception: ex);
                return false;
            }
        }

        public IEnumerable<NpcGenData> GetAllNpcs() => _npcs;
    }

    /// <summary>
    /// CSV格式解析器 - 通用CSV文件解析
    /// </summary>
    public class CSVParser
    {
        public List<Dictionary<string, string>> Parse(string filePath, bool hasHeader = true)
        {
            var result = new List<Dictionary<string, string>>();
            if (!File.Exists(filePath)) return result;

            try
            {
                // 使用ANSI编码读取文件
                var lines = SmartReader.ReadAllLines(filePath);
                if (lines.Length == 0) return result;

                string[]? headers = null;
                int startIndex = 0;

                if (hasHeader)
                {
                    headers = lines[0].Split(',');
                    startIndex = 1;
                }

                for (int i = startIndex; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i]) || lines[i].TrimStart().StartsWith("#"))
                        continue;

                    var values = lines[i].Split(',');
                    var row = new Dictionary<string, string>();

                    if (headers != null)
                    {
                        for (int j = 0; j < Math.Min(headers.Length, values.Length); j++)
                        {
                            row[headers[j].Trim()] = values[j].Trim();
                        }
                    }
                    else
                    {
                        for (int j = 0; j < values.Length; j++)
                        {
                            row[$"Column{j}"] = values[j].Trim();
                        }
                    }

                    result.Add(row);
                }

                return result;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析CSV文件失败: {filePath}", exception: ex);
                return result;
            }
        }
    }

    /// <summary>
    /// INI格式解析器
    /// </summary>
    public class INIParser
    {
        private readonly Dictionary<string, Dictionary<string, string>> _sections = new();

        public bool Load(string filePath)
        {
            if (!File.Exists(filePath)) return false;

            try
            {
                // 使用ANSI编码读取文件
                var lines = SmartReader.ReadAllLines(filePath);
                string currentSection = "default";
                _sections[currentSection] = new Dictionary<string, string>();

                foreach (var line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#"))
                        continue;

                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        currentSection = trimmed.Substring(1, trimmed.Length - 2).Trim();
                        if (!_sections.ContainsKey(currentSection))
                            _sections[currentSection] = new Dictionary<string, string>();
                        continue;
                    }

                    int equalIndex = trimmed.IndexOf('=');
                    if (equalIndex > 0)
                    {
                        string key = trimmed.Substring(0, equalIndex).Trim();
                        string value = trimmed.Substring(equalIndex + 1).Trim();
                        _sections[currentSection][key] = value;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析INI文件失败: {filePath}", exception: ex);
                return false;
            }
        }

        public string GetValue(string section, string key, string defaultValue = "")
        {
            if (_sections.TryGetValue(section, out var sectionData))
            {
                if (sectionData.TryGetValue(key, out var value))
                    return value;
            }
            return defaultValue;
        }

        public Dictionary<string, string>? GetSection(string section)
        {
            return _sections.TryGetValue(section, out var sectionData) ? sectionData : null;
        }
    }

    /// <summary>
    /// 简单脚本文件解析器
    /// </summary>
    public class SimpleScriptParser
    {
        public class ScriptLine
        {
            public string Command { get; set; } = "";
            public List<string> Parameters { get; set; } = new();
            public string RawLine { get; set; } = "";
        }

        public List<ScriptLine> Parse(string filePath)
        {
            var result = new List<ScriptLine>();
            if (!File.Exists(filePath)) return result;

            try
            {
                // 使用ANSI编码读取文件
                var lines = SmartReader.ReadAllLines(filePath);

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                        continue;

                    var scriptLine = new ScriptLine { RawLine = line };
                    var parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length > 0)
                    {
                        scriptLine.Command = parts[0];
                        for (int i = 1; i < parts.Length; i++)
                        {
                            scriptLine.Parameters.Add(parts[i]);
                        }
                    }

                    result.Add(scriptLine);
                }

                return result;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析脚本文件失败: {filePath}", exception: ex);
                return result;
            }
        }
    }

    /// <summary>
    /// 通用文本文件解析器
    /// </summary>
    public class TextFileParser
    {
        public List<string> LoadLines(string filePath, bool skipComments = true, bool skipEmpty = true)
        {
            var result = new List<string>();
            if (!File.Exists(filePath)) return result;

            try
            {
                // 使用ANSI编码读取文件
                var lines = SmartReader.ReadAllLines(filePath);

                foreach (var line in lines)
                {
                    if (skipEmpty && string.IsNullOrWhiteSpace(line))
                        continue;
                    if (skipComments && line.TrimStart().StartsWith("#"))
                        continue;

                    result.Add(line);
                }

                return result;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"读取文本文件失败: {filePath}", exception: ex);
                return result;
            }
        }

        public Dictionary<string, string> LoadKeyValue(string filePath, char separator = '=')
        {
            var result = new Dictionary<string, string>();
            if (!File.Exists(filePath)) return result;

            try
            {
                // 使用ANSI编码读取文件
                var lines = SmartReader.ReadAllLines(filePath);

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                        continue;

                    int sepIndex = line.IndexOf(separator);
                    if (sepIndex > 0)
                    {
                        string key = line.Substring(0, sepIndex).Trim();
                        string value = line.Substring(sepIndex + 1).Trim();
                        result[key] = value;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"读取键值对文件失败: {filePath}", exception: ex);
                return result;
            }
        }
    }
}
