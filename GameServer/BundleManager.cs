using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MirCommon.Utils;

namespace GameServer
{
    public struct BundleInfo
    {
        /// <summary>
        /// 捆绑物品名称（最大20字符）
        /// </summary>
        public string Name;
        
        /// <summary>
        /// 拆解出的物品名称（最大20字符）
        /// </summary>
        public string ExtractName;
        
        /// <summary>
        /// 拆解出的物品数量
        /// </summary>
        public int Count;
        
        public BundleInfo(string name, string extractName, int count)
        {
            Name = name;
            ExtractName = extractName;
            Count = count;
        }
    }

    public class BundleManager
    {
        private static BundleManager? _instance;
        
        /// <summary>
        /// 单例实例
        /// </summary>
        public static BundleManager Instance => _instance ??= new BundleManager();
        
        /// <summary>
        /// 捆绑物品哈希表（名称 -> 捆绑信息）
        /// </summary>
        private readonly Dictionary<string, BundleInfo> _bundleHash = new Dictionary<string, BundleInfo>(StringComparer.OrdinalIgnoreCase);
        
        /// <summary>
        /// 私有构造函数（单例模式）
        /// </summary>
        private BundleManager()
        {
        }
        
        /// <summary>
        /// 从文件加载捆绑配置
        /// </summary>
        /// <param name="bundleFile">捆绑配置文件路径</param>
        /// <param name="isCsv">是否为CSV格式（默认为false）</param>
        public void LoadBundle(string bundleFile, bool isCsv = false)
        {
            if (!File.Exists(bundleFile))
            {
                LogManager.Default.Warning($"捆绑配置文件不存在: {bundleFile}");
                return;
            }
            
            try
            {
                string[] lines = SmartReader.ReadAllLines(bundleFile);
                
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;
                    
                    string[] parts;
                    
                    if (isCsv)
                    {
                        // CSV格式：使用逗号分隔
                        parts = line.Split(',');
                    }
                    else
                    {
                        // s20: 20字符字符串，s20: 20字符字符串，d: 整数
                        parts = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    }
                    
                    if (parts.Length < 3)
                    {
                        LogManager.Default.Warning($"捆绑配置行格式错误: {line}");
                        continue;
                    }
                    
                    string name = parts[0].Trim();
                    string extractName = parts[1].Trim();
                    
                    if (!int.TryParse(parts[2].Trim(), out int count))
                    {
                        LogManager.Default.Warning($"捆绑配置数量解析失败: {line}");
                        continue;
                    }
                    
                    // 限制名称长度
                    if (name.Length > 20) name = name.Substring(0, 20);
                    if (extractName.Length > 20) extractName = extractName.Substring(0, 20);
                    
                    var bundleInfo = new BundleInfo(name, extractName, count);
                    
                    // 添加到哈希表（如果已存在则更新）
                    _bundleHash[name] = bundleInfo;
                }
                
                LogManager.Default.Info($"成功加载 {_bundleHash.Count} 个捆绑配置");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载捆绑配置文件失败: {bundleFile}", exception: ex);
            }
        }
        
        /// <summary>
        /// 获取捆绑物品信息
        /// </summary>
        /// <param name="name">捆绑物品名称</param>
        /// <param name="extractItemName">输出：拆解出的物品名称</param>
        /// <param name="count">输出：拆解出的物品数量</param>
        /// <returns>是否找到该捆绑物品</returns>
        public bool GetBundleInfo(string name, out string extractItemName, out int count)
        {
            extractItemName = string.Empty;
            count = 0;
            
            if (string.IsNullOrEmpty(name))
                return false;
            
            if (_bundleHash.TryGetValue(name, out BundleInfo bundleInfo))
            {
                extractItemName = bundleInfo.ExtractName;
                count = bundleInfo.Count;
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 获取捆绑物品信息
        /// </summary>
        /// <param name="name">捆绑物品名称</param>
        /// <returns>捆绑信息，如果不存在则返回null</returns>
        public BundleInfo? GetBundleInfo(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            
            if (_bundleHash.TryGetValue(name, out BundleInfo bundleInfo))
            {
                return bundleInfo;
            }
            
            return null;
        }
        
        /// <summary>
        /// 检查是否存在指定名称的捆绑物品
        /// </summary>
        /// <param name="name">捆绑物品名称</param>
        /// <returns>是否存在</returns>
        public bool ContainsBundle(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            
            return _bundleHash.ContainsKey(name);
        }
        
        /// <summary>
        /// 获取所有捆绑物品名称
        /// </summary>
        /// <returns>捆绑物品名称列表</returns>
        public List<string> GetAllBundleNames()
        {
            return new List<string>(_bundleHash.Keys);
        }
        
        /// <summary>
        /// 获取所有捆绑物品信息
        /// </summary>
        /// <returns>捆绑物品信息列表</returns>
        public List<BundleInfo> GetAllBundleInfos()
        {
            return new List<BundleInfo>(_bundleHash.Values);
        }
        
        /// <summary>
        /// 获取捆绑物品数量
        /// </summary>
        /// <returns>捆绑物品数量</returns>
        public int GetBundleCount()
        {
            return _bundleHash.Count;
        }
        
        /// <summary>
        /// 清空所有捆绑配置
        /// </summary>
        public void Clear()
        {
            _bundleHash.Clear();
            LogManager.Default.Info("已清空所有捆绑配置");
        }
        
        /// <summary>
        /// 重新加载捆绑配置
        /// </summary>
        /// <param name="bundleFile">捆绑配置文件路径</param>
        /// <param name="isCsv">是否为CSV格式</param>
        public void ReloadBundle(string bundleFile, bool isCsv = false)
        {
            Clear();
            LoadBundle(bundleFile, isCsv);
        }
        
        /// <summary>
        /// 添加或更新捆绑配置
        /// </summary>
        /// <param name="name">捆绑物品名称</param>
        /// <param name="extractName">拆解出的物品名称</param>
        /// <param name="count">拆解出的物品数量</param>
        /// <returns>是否成功</returns>
        public bool AddOrUpdateBundle(string name, string extractName, int count)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(extractName) || count <= 0)
                return false;
            
            // 限制名称长度
            if (name.Length > 20) name = name.Substring(0, 20);
            if (extractName.Length > 20) extractName = extractName.Substring(0, 20);
            
            var bundleInfo = new BundleInfo(name, extractName, count);
            _bundleHash[name] = bundleInfo;
            
            return true;
        }
        
        /// <summary>
        /// 移除捆绑配置
        /// </summary>
        /// <param name="name">捆绑物品名称</param>
        /// <returns>是否成功移除</returns>
        public bool RemoveBundle(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            
            return _bundleHash.Remove(name);
        }
    }
}
