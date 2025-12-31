using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MirCommon.Utils
{
    /// <summary>
    /// INI配置文件读取器
    /// </summary>
    public class IniFileReader
    {
        private readonly Dictionary<string, Dictionary<string, string>> _sections = new();
        private readonly string _filePath;

        public IniFileReader(string filePath)
        {
            _filePath = filePath;
        }

        /// <summary>
        /// 打开并解析INI文件
        /// </summary>
        public bool Open()
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    Console.WriteLine($"配置文件不存在: {_filePath}");
                    return false;
                }

                var lines = SmartReader.ReadAllLines(_filePath);
                string currentSection = "";

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();

                    // 跳过空行和注释
                    if (string.IsNullOrWhiteSpace(trimmedLine) || 
                        trimmedLine.StartsWith(";") || 
                        trimmedLine.StartsWith("#"))
                        continue;

                    // 检查是否是节名
                    if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    {
                        currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2).Trim();
                        if (!_sections.ContainsKey(currentSection))
                        {
                            _sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        }
                        continue;
                    }

                    // 解析键值对
                    var equalIndex = trimmedLine.IndexOf('=');
                    if (equalIndex > 0)
                    {
                        var key = trimmedLine.Substring(0, equalIndex).Trim();
                        var value = trimmedLine.Substring(equalIndex + 1).Trim();

                        // 如果没有当前节，使用空字符串作为全局节
                        var section = string.IsNullOrEmpty(currentSection) ? "" : currentSection;
                        
                        if (!_sections.ContainsKey(section))
                        {
                            _sections[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        }

                        _sections[section][key] = value;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取配置文件失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取字符串值
        /// </summary>
        /// <param name="section">节名，null或空字符串表示全局节</param>
        /// <param name="key">键名</param>
        /// <param name="defaultValue">默认值</param>
        public string GetString(string? section, string key, string defaultValue = "")
        {
            section = section ?? "";
            
            if (_sections.TryGetValue(section, out var sectionData))
            {
                if (sectionData.TryGetValue(key, out var value))
                {
                    return value;
                }
            }

            return defaultValue;
        }

        /// <summary>
        /// 获取整数值
        /// </summary>
        /// <param name="section">节名，null或空字符串表示全局节</param>
        /// <param name="key">键名</param>
        /// <param name="defaultValue">默认值</param>
        public int GetInteger(string? section, string key, int defaultValue = 0)
        {
            var strValue = GetString(section, key, defaultValue.ToString());
            
            if (int.TryParse(strValue, out var result))
            {
                return result;
            }

            return defaultValue;
        }

        /// <summary>
        /// 获取布尔值
        /// </summary>
        /// <param name="section">节名，null或空字符串表示全局节</param>
        /// <param name="key">键名</param>
        /// <param name="defaultValue">默认值</param>
        public bool GetBoolean(string? section, string key, bool defaultValue = false)
        {
            var strValue = GetString(section, key, defaultValue.ToString());
            
            // 支持多种布尔值表示
            strValue = strValue.ToLower().Trim();
            if (strValue == "1" || strValue == "true" || strValue == "yes" || strValue == "on")
                return true;
            if (strValue == "0" || strValue == "false" || strValue == "no" || strValue == "off")
                return false;

            return defaultValue;
        }

        /// <summary>
        /// 检查节是否存在
        /// </summary>
        public bool HasSection(string section)
        {
            return _sections.ContainsKey(section ?? "");
        }

        /// <summary>
        /// 检查键是否存在
        /// </summary>
        public bool HasKey(string? section, string key)
        {
            section = section ?? "";
            if (_sections.TryGetValue(section, out var sectionData))
            {
                return sectionData.ContainsKey(key);
            }
            return false;
        }

        /// <summary>
        /// 获取所有节名
        /// </summary>
        public IEnumerable<string> GetSections()
        {
            return _sections.Keys;
        }

        /// <summary>
        /// 获取指定节的所有键
        /// </summary>
        public IEnumerable<string> GetKeys(string? section)
        {
            section = section ?? "";
            if (_sections.TryGetValue(section, out var sectionData))
            {
                return sectionData.Keys;
            }
            return Array.Empty<string>();
        }
    }
}
