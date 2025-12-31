using MirCommon.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GameServer
{
    /// <summary>
    /// 简单的INI文件读取器
    /// </summary>
    public class IniFile
    {
        private readonly Dictionary<string, Dictionary<string, string>> _sections = new();

        public IniFile(string filePath)
        {
            if (!File.Exists(filePath))
                return;

            var lines = SmartReader.ReadAllLines(filePath);
            string currentSection = "";

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(";") || trimmedLine.StartsWith("#"))
                    continue;

                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2).Trim();
                    if (!_sections.ContainsKey(currentSection))
                    {
                        _sections[currentSection] = new Dictionary<string, string>();
                    }
                }
                else if (currentSection != "")
                {
                    var parts = trimmedLine.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var value = parts[1].Trim();
                        _sections[currentSection][key] = value;
                    }
                }
            }
        }

        public string GetString(string section, string key, string defaultValue = "")
        {
            if (_sections.TryGetValue(section, out var sectionDict) && sectionDict.TryGetValue(key, out var value))
            {
                return value;
            }
            return defaultValue;
        }

        public int GetInt(string section, string key, int defaultValue = 0)
        {
            var strValue = GetString(section, key, defaultValue.ToString());
            if (int.TryParse(strValue, out var result))
            {
                return result;
            }
            return defaultValue;
        }

        public int GetInteger(string section, string key, int defaultValue = 0)
        {
            return GetInt(section, key, defaultValue);
        }

        public bool GetBool(string section, string key, bool defaultValue = false)
        {
            var strValue = GetString(section, key, defaultValue.ToString());
            if (bool.TryParse(strValue, out var result))
            {
                return result;
            }
            return defaultValue;
        }
    }
}
