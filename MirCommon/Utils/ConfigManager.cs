using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MirCommon.Utils
{
    /// <summary>
    /// 配置管理器
    /// </summary>
    public class ConfigManager
    {
        private readonly Dictionary<string, object> _config = new();
        private readonly string _configFile;
        private readonly object _lock = new();

        public ConfigManager(string configFile = "config.json")
        {
            _configFile = configFile;
        }

        /// <summary>
        /// 加载配置文件
        /// </summary>
        public bool Load()
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(_configFile))
                    {
                        // 创建默认配置
                        CreateDefaultConfig();
                        return true;
                    }

                    string json = File.ReadAllText(_configFile);
                    var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

                    if (config != null)
                    {
                        _config.Clear();
                        foreach (var kvp in config)
                        {
                            _config[kvp.Key] = kvp.Value;
                        }
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载配置文件失败: {ex.Message}", "Config");
                    return false;
                }
            }
        }

        /// <summary>
        /// 保存配置文件
        /// </summary>
        public bool Save()
        {
            lock (_lock)
            {
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    };

                    string json = JsonSerializer.Serialize(_config, options);
                    File.WriteAllText(_configFile, json);
                    return true;
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"保存配置文件失败: {ex.Message}", "Config");
                    return false;
                }
            }
        }

        /// <summary>
        /// 获取字符串值
        /// </summary>
        public string GetString(string key, string defaultValue = "")
        {
            lock (_lock)
            {
                if (_config.TryGetValue(key, out var value))
                {
                    if (value is JsonElement element)
                    {
                        return element.GetString() ?? defaultValue;
                    }
                    return value?.ToString() ?? defaultValue;
                }
                return defaultValue;
            }
        }

        /// <summary>
        /// 获取整数值
        /// </summary>
        public int GetInt(string key, int defaultValue = 0)
        {
            lock (_lock)
            {
                if (_config.TryGetValue(key, out var value))
                {
                    if (value is JsonElement element && element.ValueKind == JsonValueKind.Number)
                    {
                        return element.GetInt32();
                    }
                    if (int.TryParse(value?.ToString(), out int result))
                    {
                        return result;
                    }
                }
                return defaultValue;
            }
        }

        /// <summary>
        /// 获取布尔值
        /// </summary>
        public bool GetBool(string key, bool defaultValue = false)
        {
            lock (_lock)
            {
                if (_config.TryGetValue(key, out var value))
                {
                    if (value is JsonElement element)
                    {
                        if (element.ValueKind == JsonValueKind.True) return true;
                        if (element.ValueKind == JsonValueKind.False) return false;
                    }
                    if (bool.TryParse(value?.ToString(), out bool result))
                    {
                        return result;
                    }
                }
                return defaultValue;
            }
        }

        /// <summary>
        /// 设置值
        /// </summary>
        public void SetValue(string key, object value)
        {
            lock (_lock)
            {
                _config[key] = value;
            }
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        private void CreateDefaultConfig()
        {
            _config.Clear();

            // 数据库配置
            _config["Database.Server"] = "(local)";
            _config["Database.Name"] = "MirWorldDB";
            _config["Database.User"] = "sa";
            _config["Database.Password"] = "dragon";

            // 服务器配置
            _config["Server.Port"] = 5100;
            _config["Server.MaxConnections"] = 100;
            _config["Server.Name"] = "DBServer";

            // 日志配置
            _config["Log.Directory"] = "logs";
            _config["Log.WriteToConsole"] = true;
            _config["Log.WriteToFile"] = true;

            Save();
        }

        /// <summary>
        /// 检查键是否存在
        /// </summary>
        public bool HasKey(string key)
        {
            lock (_lock)
            {
                return _config.ContainsKey(key);
            }
        }

        /// <summary>
        /// 获取所有键
        /// </summary>
        public string[] GetAllKeys()
        {
            lock (_lock)
            {
                return new List<string>(_config.Keys).ToArray();
            }
        }
    }

    /// <summary>
    /// 服务器配置帮助类
    /// </summary>
    public static class ServerConfigHelper
    {
        /// <summary>
        /// 从配置管理器加载日志配置
        /// </summary>
        public static (string server, string database, string user, string password, int port, 
            int maxConnections, string name, string directory, bool writeToConsole, 
            bool writeToFile) LoadLogConfig(ConfigManager config)
        {
            return (
                config.GetString("Database.Server", "(local)"),
                config.GetString("Database.Name", "MirWorldDB"),
                config.GetString("Database.User", "sa"),
                config.GetString("Database.Password", "dragon"),

                config.GetInt("Server.Port", 5100),
                config.GetInt("Server.MaxConnections", 100),
                config.GetString("Server.Name", "Server"),

                config.GetString("Log.Directory", "logs"),
                config.GetBool("Log.WriteToConsole", true),
                config.GetBool("Log.WriteToFile", true)
            );
        }
    }
}
