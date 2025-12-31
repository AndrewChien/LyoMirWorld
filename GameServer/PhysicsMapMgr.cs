using System;
using System.Collections.Generic;
using System.IO;
using MirCommon.Utils;

namespace GameServer
{
    /// <summary>
    /// 物理地图管理器
    /// </summary>
    public class PhysicsMapMgr
    {
        private static PhysicsMapMgr _instance;
        private readonly Dictionary<string, PhysicsMap> _mapDictionary;
        private string _physicsMapPath = string.Empty;
        private string _physicsCachePath = string.Empty;
        private bool _useCache = false;

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static PhysicsMapMgr Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new PhysicsMapMgr();
                }
                return _instance;
            }
        }

        /// <summary>
        /// 私有构造函数
        /// </summary>
        private PhysicsMapMgr()
        {
            _mapDictionary = new Dictionary<string, PhysicsMap>(StringComparer.OrdinalIgnoreCase);
            _useCache = false;
        }

        /// <summary>
        /// 初始化物理地图管理器
        /// </summary>
        /// <param name="physicsMapPath">物理地图路径</param>
        /// <param name="physicsCachePath">物理地图缓存路径</param>
        public void Init(string physicsMapPath, string physicsCachePath)
        {
            _physicsMapPath = physicsMapPath ?? string.Empty;
            _physicsCachePath = physicsCachePath ?? string.Empty;

            // 检查缓存路径是否可用
            if (Directory.Exists(_physicsCachePath))
            {
                _useCache = true;
                LogManager.Default.Info("地图CACHE功能启用，将大大提高地图读取速度！");
            }
            else
            {
                _useCache = false;
                LogManager.Default.Warning("地图CACHE路径不可用，CACHE被禁用，可能导致读取时间过长！");
                
                // 检查物理地图路径是否可用
                if (!Directory.Exists(_physicsMapPath))
                {
                    LogManager.Default.Error("物理地图路径不可用，物理地图无法正常读取！");
                }
            }
        }

        /// <summary>
        /// 加载物理地图
        /// </summary>
        /// <param name="mapName">地图名称</param>
        /// <returns>加载的物理地图，失败返回null</returns>
        public PhysicsMap Load(string mapName)
        {
            if (string.IsNullOrEmpty(mapName))
                return null;

            // 先检查是否已加载
            if (_mapDictionary.TryGetValue(mapName, out var existingMap))
                return existingMap;

            PhysicsMap map = new PhysicsMap();
            bool loaded = false;

            // 如果启用缓存，先尝试加载缓存
            if (_useCache)
            {
                string cacheFilename = Path.Combine(_physicsCachePath, mapName + ".PMC");
                if (File.Exists(cacheFilename))
                {
                    loaded = map.LoadCache(cacheFilename);
                    if (loaded)
                    {
                        LogManager.Default.Debug($"从缓存加载物理地图: {mapName}");
                    }
                }
            }

            // 如果缓存加载失败，尝试从原始地图文件加载
            if (!loaded)
            {
                string mapFilename = Path.Combine(_physicsMapPath, mapName + ".nmp");
                if (File.Exists(mapFilename))
                {
                    loaded = map.LoadMap(mapFilename);
                    if (loaded)
                    {
                        LogManager.Default.Debug($"从原始文件加载物理地图: {mapName}");
                        
                        // 如果启用缓存，保存缓存
                        if (_useCache)
                        {
                            map.SaveCache(_physicsCachePath);
                        }
                    }
                }
            }

            if (loaded)
            {
                _mapDictionary[map.Name] = map;
                return map;
            }
            else
            {
                //LogManager.Default.Error($"无法加载物理地图: {mapName}");//lyo：此处正常缺地图文件，不显示
                return null;
            }
        }

        /// <summary>
        /// 根据地图名称获取物理地图
        /// </summary>
        /// <param name="mapName">地图名称</param>
        /// <returns>物理地图，如果不存在则尝试加载</returns>
        public PhysicsMap GetPhysicsMapByName(string mapName)
        {
            if (string.IsNullOrEmpty(mapName))
                return null;

            // 先尝试从字典获取
            if (_mapDictionary.TryGetValue(mapName, out var map))
                return map;

            // 如果不存在，尝试加载
            return Load(mapName);
        }

        /// <summary>
        /// 检查地图是否已加载
        /// </summary>
        /// <param name="mapName">地图名称</param>
        /// <returns>是否已加载</returns>
        public bool IsMapLoaded(string mapName)
        {
            return _mapDictionary.ContainsKey(mapName);
        }

        /// <summary>
        /// 获取已加载的地图数量
        /// </summary>
        public int LoadedMapCount => _mapDictionary.Count;

        /// <summary>
        /// 清除所有已加载的地图
        /// </summary>
        public void ClearAllMaps()
        {
            _mapDictionary.Clear();
            LogManager.Default.Info("已清除所有物理地图");
        }

        /// <summary>
        /// 获取所有已加载的地图名称
        /// </summary>
        public List<string> GetAllMapNames()
        {
            return new List<string>(_mapDictionary.Keys);
        }

        /// <summary>
        /// 检查位置是否被阻挡
        /// </summary>
        /// <param name="mapName">地图名称</param>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <returns>是否被阻挡，如果地图不存在返回true</returns>
        public bool IsBlocked(string mapName, int x, int y)
        {
            var map = GetPhysicsMapByName(mapName);
            if (map == null)
                return true; // 地图不存在，视为阻挡

            return map.IsBlocked(x, y);
        }

        /// <summary>
        /// 预加载多个地图
        /// </summary>
        /// <param name="mapNames">地图名称数组</param>
        /// <returns>成功加载的地图数量</returns>
        public int PreloadMaps(string[] mapNames)
        {
            int successCount = 0;
            foreach (var mapName in mapNames)
            {
                if (string.IsNullOrEmpty(mapName))
                    continue;

                var map = Load(mapName);
                if (map != null)
                    successCount++;
            }
            
            LogManager.Default.Info($"预加载地图完成，成功: {successCount}/{mapNames.Length}");
            return successCount;
        }
    }
}
