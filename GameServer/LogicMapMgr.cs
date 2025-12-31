using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MirCommon.Utils;

namespace GameServer
{
    /// <summary>
    /// 逻辑地图管理器
    /// </summary>
    public class LogicMapMgr
    {
        private static LogicMapMgr? _instance;
        private readonly Dictionary<uint, LogicMap> _mapsById = new();
        private readonly Dictionary<string, LogicMap> _mapsByName = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();
        private const int MAX_LOGIC_MAP = 10240;

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static LogicMapMgr Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new LogicMapMgr();
                }
                return _instance;
            }
        }

        /// <summary>
        /// 私有构造函数
        /// </summary>
        private LogicMapMgr()
        {
        }

        /// <summary>
        /// 从指定路径加载所有逻辑地图配置文件
        /// </summary>
        /// <param name="path">配置文件路径</param>
        public void Load(string path)
        {
            lock (_lock)
            {
                try
                {
                    LogManager.Default.Info($"开始加载逻辑地图配置: {path}");

                    if (!Directory.Exists(path))
                    {
                        LogManager.Default.Warning($"逻辑地图配置目录不存在: {path}");
                        return;
                    }

                    // 查找所有.ini文件
                    var iniFiles = Directory.GetFiles(path, "*.ini", SearchOption.AllDirectories);
                    int loadedCount = 0;

                    foreach (var iniFile in iniFiles)
                    {
                        try
                        {
                            var map = LoadMapFromIni(iniFile);
                            if (map != null)
                            {
                                // 添加到字典
                                _mapsById[map.MapId] = map;
                                _mapsByName[map.MapName] = map;
                                loadedCount++;

                                LogManager.Default.Debug($"加载逻辑地图: {map.MapName} (ID: {map.MapId})");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogManager.Default.Error($"加载逻辑地图文件失败: {iniFile}", exception: ex);
                        }
                    }

                    // 初始化地图链接
                    InitMapLinks();

                    LogManager.Default.Info($"成功加载 {loadedCount} 个逻辑地图配置");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载逻辑地图配置失败: {path}", exception: ex);
                }
            }
        }

        /// <summary>
        /// 从INI文件加载单个地图配置
        /// </summary>
        private LogicMap? LoadMapFromIni(string iniFile)
        {
            try
            {
                // 使用IniFile类读取INI文件
                var ini = new IniFile(iniFile);
                
                // 读取物理地图名称
                string blockmap = ini.GetString("define", "blockmap", "");
                if (string.IsNullOrEmpty(blockmap))
                {
                    LogManager.Default.Warning($"地图配置文件缺少blockmap: {iniFile}");
                    return null;
                }

                // 读取地图名称
                string mapName = ini.GetString("define", "name", "");
                if (string.IsNullOrEmpty(mapName))
                {
                    LogManager.Default.Warning($"地图配置文件缺少name: {iniFile}");
                    return null;
                }

                // 读取小地图ID
                int miniMap = ini.GetInt("define", "minimap", 0);

                // 读取地图ID
                uint mapId = (uint)ini.GetInt("define", "mapid", 0);
                if (mapId == 0)
                {
                    LogManager.Default.Warning($"地图配置文件缺少mapid: {iniFile}");
                    return null;
                }

                // 读取链接数量
                int linkcount = ini.GetInt("define", "linkcount", 0);

                // 读取经验倍率
                int expfactor = ini.GetInt("define", "expfactor", 100);
                float expFactor = expfactor / 100.0f;

                // 读取地图标志字符串
                string flagStr = ini.GetString("define", "flag", "");

                // 将物理地图名称转换为大写
                string upperBlockmap = blockmap.ToUpper();

                // 通过PhysicsMapMgr获取物理地图
                var physicsMap = PhysicsMapMgr.Instance.GetPhysicsMapByName(upperBlockmap);
                if (physicsMap == null)
                {
                    LogManager.Default.Error($"无法加载物理地图: {upperBlockmap}，地图 {mapName} 加载失败");
                    return null;
                }

                // 从物理地图获取实际尺寸，而不是从INI读取
                int width = physicsMap.Width;
                int height = physicsMap.Height;

                // 创建地图实例
                var map = new LogicMap(mapId, mapName, width, height);

                // 设置物理地图关联
                map.SetPhysicsMap(physicsMap);

                // 设置小地图ID
                map.SetMiniMap(miniMap);

                // 设置经验倍率
                map.ExpFactor = expFactor;

                // 解析并设置地图标志
                if (!string.IsNullOrEmpty(flagStr))
                {
                    // 使用'|'分割标志字符串
                    string[] flags = flagStr.Split('|', StringSplitOptions.RemoveEmptyEntries);
                    foreach (string flag in flags)
                    {
                        string trimmedFlag = flag.Trim();
                        if (!string.IsNullOrEmpty(trimmedFlag))
                        {
                            // 直接调用LogicMap的SetFlag(string)方法，支持带参数的标志
                            map.SetFlag(trimmedFlag);
                        }
                    }
                }

                // 设置链接数量，稍后在InitLinks中处理
                map.SetLinkCount(linkcount);

                // 初始化地图单元格
                map.InitMapCells();

                LogManager.Default.Debug($"加载逻辑地图: {mapName} (ID: {mapId}, 尺寸: {width}x{height}, 物理地图: {upperBlockmap})");
                return map;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析地图配置文件失败: {iniFile}", exception: ex);
                return null;
            }
        }

        /// <summary>
        /// 初始化地图链接
        /// 在所有地图加载完成后调用每个地图的InitLinks方法
        /// </summary>
        private void InitMapLinks()
        {
            int linkCount = 0;
            
            foreach (var map in _mapsById.Values)
            {
                try
                {
                    map.InitLinks();
                    linkCount += map.GetLinkCount();
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"初始化地图链接失败: {map.MapName}", exception: ex);
                }
            }
            
            LogManager.Default.Info($"地图链接初始化完成，共处理 {linkCount} 个链接");
        }

        /// <summary>
        /// 通过地图ID获取逻辑地图
        /// </summary>
        public LogicMap? GetLogicMapById(uint mapId)
        {
            LogManager.Default.Debug($"当前LogicMap已有{_mapsById.Count}个地图");
            lock (_lock)
            {
                if (mapId == 0 || mapId > MAX_LOGIC_MAP)
                    return null;

                return _mapsById.TryGetValue(mapId, out var map) ? map : null;
            }
        }

        /// <summary>
        /// 通过地图名称获取逻辑地图
        /// </summary>
        public LogicMap? GetLogicMapByName(string mapName)
        {
            lock (_lock)
            {
                return _mapsByName.TryGetValue(mapName, out var map) ? map : null;
            }
        }

        /// <summary>
        /// 获取已加载的地图数量
        /// </summary>
        public int GetMapCount()
        {
            lock (_lock)
            {
                return _mapsById.Count;
            }
        }

        /// <summary>
        /// 获取所有已加载的地图
        /// </summary>
        public List<LogicMap> GetAllMaps()
        {
            lock (_lock)
            {
                return new List<LogicMap>(_mapsById.Values);
            }
        }

        /// <summary>
        /// 检查地图是否存在
        /// </summary>
        public bool HasMap(uint mapId)
        {
            lock (_lock)
            {
                return _mapsById.ContainsKey(mapId);
            }
        }

        /// <summary>
        /// 检查地图是否存在（通过名称）
        /// </summary>
        public bool HasMap(string mapName)
        {
            lock (_lock)
            {
                return _mapsByName.ContainsKey(mapName);
            }
        }

        /// <summary>
        /// 添加地图（手动添加，用于测试或动态创建）
        /// </summary>
        public void AddMap(LogicMap map)
        {
            lock (_lock)
            {
                if (map == null)
                    return;

                _mapsById[map.MapId] = map;
                _mapsByName[map.MapName] = map;
            }
        }

        /// <summary>
        /// 移除地图
        /// </summary>
        public bool RemoveMap(uint mapId)
        {
            lock (_lock)
            {
                if (!_mapsById.TryGetValue(mapId, out var map))
                    return false;

                _mapsById.Remove(mapId);
                _mapsByName.Remove(map.MapName);
                return true;
            }
        }

        /// <summary>
        /// 清除所有地图
        /// </summary>
        public void ClearAllMaps()
        {
            lock (_lock)
            {
                _mapsById.Clear();
                _mapsByName.Clear();
            }
        }

        /// <summary>
        /// 重新加载地图配置
        /// </summary>
        public void Reload(string path)
        {
            lock (_lock)
            {
                ClearAllMaps();
                Load(path);
            }
        }

        /// <summary>
        /// 获取地图名称（通过ID）
        /// </summary>
        public string GetMapName(uint mapId)
        {
            var map = GetLogicMapById(mapId);
            return map?.MapName ?? "未知地图";
        }

        /// <summary>
        /// 获取地图ID（通过名称）
        /// </summary>
        public uint GetMapId(string mapName)
        {
            var map = GetLogicMapByName(mapName);
            return map?.MapId ?? 0;
        }

        /// <summary>
        /// 检查是否可以传送到指定地图
        /// </summary>
        public bool CanTeleportTo(uint mapId)
        {
            var map = GetLogicMapById(mapId);
            return map?.AllowTeleport ?? false;
        }

        /// <summary>
        /// 检查是否可以回城到指定地图
        /// </summary>
        public bool CanRecallTo(uint mapId)
        {
            var map = GetLogicMapById(mapId);
            return map?.AllowRecall ?? false;
        }

        /// <summary>
        /// 检查地图是否安全区
        /// </summary>
        public bool IsSafeZone(uint mapId)
        {
            var map = GetLogicMapById(mapId);
            return map?.IsSafeZone ?? false;
        }

        /// <summary>
        /// 检查地图是否允许PK
        /// </summary>
        public bool AllowPK(uint mapId)
        {
            var map = GetLogicMapById(mapId);
            return map?.AllowPK ?? false;
        }

        /// <summary>
        /// 检查地图是否允许宠物
        /// </summary>
        public bool AllowPets(uint mapId)
        {
            var map = GetLogicMapById(mapId);
            return map?.AllowPets ?? false;
        }

        /// <summary>
        /// 检查地图是否允许坐骑
        /// </summary>
        public bool AllowMounts(uint mapId)
        {
            var map = GetLogicMapById(mapId);
            return map?.AllowMounts ?? false;
        }

        /// <summary>
        /// 获取地图经验倍率
        /// </summary>
        public float GetExpFactor(uint mapId)
        {
            var map = GetLogicMapById(mapId);
            return map?.ExpFactor ?? 1.0f;
        }

        /// <summary>
        /// 获取地图掉落倍率
        /// </summary>
        public float GetDropFactor(uint mapId)
        {
            var map = GetLogicMapById(mapId);
            return map?.DropFactor ?? 1.0f;
        }

        /// <summary>
        /// 将字符串标志转换为MapFlag枚举
        /// </summary>
        private MapFlag GetMapFlagFromString(string flagStr)
        {
            // 检查是否带参数
            int paramStart = flagStr.IndexOf('(');
            if (paramStart > 0)
            {
                // 提取标志名称（不带参数部分）
                string flagName = flagStr.Substring(0, paramStart).Trim().ToUpper();
                return GetMapFlagFromName(flagName);
            }
            else
            {
                // 不带参数的标志
                return GetMapFlagFromName(flagStr.ToUpper());
            }
        }
        
        /// <summary>
        /// 根据标志名称获取MapFlag枚举（内部方法）
        /// </summary>
        private MapFlag GetMapFlagFromName(string flagName)
        {
            switch (flagName)
            {
                case "SABUKPALACE":
                    return MapFlag.MF_NONE; // 沙巴克皇宫特殊处理
                case "FIGHTMAP":
                    return MapFlag.MF_FIGHT;
                case "NORANDOMMOVE":
                    return MapFlag.MF_NORUN;
                case "NORECONNECT":
                    return MapFlag.MF_NONE; // 禁止重连，需要特殊处理
                case "RIDEHORSE":
                    return MapFlag.MF_NOMOUNT;
                case "LEVELABOVE":
                case "LEVELBELOW":
                    return MapFlag.MF_NONE; // 等级限制，需要特殊处理
                case "LIMITJOB":
                    return MapFlag.MF_NONE; // 职业限制，需要特殊处理
                case "PKPOINTABOVE":
                case "PKPOINTBELOW":
                    return MapFlag.MF_NONE; // PK值限制，需要特殊处理
                case "NOESCAPE":
                    return MapFlag.MF_NOTELEPORT;
                case "NOHOME":
                    return MapFlag.MF_NORECALL;
                case "MINE":
                    return MapFlag.MF_MINE;
                case "WEATHER":
                case "DAY":
                case "NIGHT":
                    return MapFlag.MF_NONE; // 需要特殊处理
                case "NOGROUPMOVE":
                    return MapFlag.MF_NONE; // 禁止组队移动，需要特殊处理
                case "SANDCITYHOME":
                    return MapFlag.MF_NONE; // 沙城回城点，需要特殊处理
                case "NODMOVE":
                    return MapFlag.MF_NOWALK;
                case "NOFLASHMOVE":
                    return MapFlag.MF_NOTELEPORT;
                case "USERDEFINE1":
                case "USERDEFINE2":
                case "USERDEFINE3":
                case "USERDEFINE4":
                    return MapFlag.MF_NONE; // 用户自定义标志
                case "SAFE":
                    return MapFlag.MF_SAFE;
                case "NOPK":
                    return MapFlag.MF_NOPK;
                case "NOMONSTER":
                    return MapFlag.MF_NOMONSTER;
                case "NOPET":
                    return MapFlag.MF_NOPET;
                case "NODROP":
                    return MapFlag.MF_NODROP;
                case "NOGUILDWAR":
                    return MapFlag.MF_NOGUILDWAR;
                case "NODUEL":
                    return MapFlag.MF_NODUEL;
                case "NOSKILL":
                    return MapFlag.MF_NOSKILL;
                case "NOITEM":
                    return MapFlag.MF_NOITEM;
                case "NOSPELL":
                    return MapFlag.MF_NOSPELL;
                case "NOSIT":
                    return MapFlag.MF_NOSIT;
                case "NOSTAND":
                    return MapFlag.MF_NOSTAND;
                case "NODIE":
                    return MapFlag.MF_NODIE;
                case "NORESPAWN":
                    return MapFlag.MF_NORESPAWN;
                case "NOLOGOUT":
                    return MapFlag.MF_NOLOGOUT;
                case "NOSAVE":
                    return MapFlag.MF_NOSAVE;
                case "NOLOAD":
                    return MapFlag.MF_NOLOAD;
                case "NOSCRIPT":
                    return MapFlag.MF_NOSCRIPT;
                case "NOEVENT":
                    return MapFlag.MF_NOEVENT;
                case "NOMESSAGE":
                    return MapFlag.MF_NOMESSAGE;
                case "NOCHAT":
                    return MapFlag.MF_NOCHAT;
                case "NOWHISPER":
                    return MapFlag.MF_NOWHISPER;
                case "NOSHOUT":
                    return MapFlag.MF_NOSHOUT;
                case "NOTRADE":
                    return MapFlag.MF_NOTRADE;
                case "NOSTORE":
                    return MapFlag.MF_NOSTORE;
                default:
                    LogManager.Default.Warning($"未知的地图标志: {flagName}");
                    return MapFlag.MF_NONE;
            }
        }
    }
}
