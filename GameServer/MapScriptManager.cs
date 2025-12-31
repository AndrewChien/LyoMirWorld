using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MirCommon.Utils;

namespace GameServer
{
    /// <summary>
    /// 地图脚本事件位置
    /// </summary>
    public struct EventMapPosition
    {
        /// <summary>
        /// 地图ID
        /// </summary>
        public uint MapId { get; set; }

        /// <summary>
        /// X坐标
        /// </summary>
        public uint X { get; set; }

        /// <summary>
        /// Y坐标
        /// </summary>
        public uint Y { get; set; }

        /// <summary>
        /// 延迟时间
        /// </summary>
        public uint Delay { get; set; }

        /// <summary>
        /// 事件标志
        /// </summary>
        public ScriptEventFlag Flag { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public EventMapPosition(uint mapId, uint x, uint y, ScriptEventFlag flag = ScriptEventFlag.Enter, uint delay = 0)
        {
            MapId = mapId;
            X = x;
            Y = y;
            Delay = delay;
            Flag = flag;
        }

        /// <summary>
        /// 转换为字符串
        /// </summary>
        public override string ToString()
        {
            return $"[{MapId}:({X},{Y})] 标志:{Flag} 延迟:{Delay}";
        }
    }

    /// <summary>
    /// 脚本事件标志
    /// </summary>
    [Flags]
    public enum ScriptEventFlag
    {
        /// <summary>
        /// 进入事件
        /// </summary>
        Enter = 1,

        /// <summary>
        /// 离开事件
        /// </summary>
        Leave = 2
    }

    /// <summary>
    /// 脚本事件类
    /// </summary>
    public class ScriptEvent : EventObject
    {
        private static readonly List<ScriptEvent> _scriptEvents = new();
        private static readonly object _lock = new();

        /// <summary>
        /// 脚本页面名称
        /// </summary>
        public string ScriptPage { get; private set; } = string.Empty;

        /// <summary>
        /// 事件标志
        /// </summary>
        public ScriptEventFlag Flag { get; private set; }

        /// <summary>
        /// 私有构造函数
        /// </summary>
        private ScriptEvent()
        {
        }

        /// <summary>
        /// 创建脚本事件
        /// </summary>
        public static ScriptEvent? Create(uint mapId, uint x, uint y, ScriptEventFlag flag, string scriptPage)
        {
            if (string.IsNullOrEmpty(scriptPage))
                return null;

            try
            {
                var scriptEvent = new ScriptEvent
                {
                    MapId = (int)mapId,
                    X = (ushort)x,
                    Y = (ushort)y,
                    Flag = flag,
                    ScriptPage = scriptPage
                };

                lock (_lock)
                {
                    _scriptEvents.Add(scriptEvent);
                }

                LogManager.Default.Debug($"创建脚本事件: 地图={mapId}, 位置=({x},{y}), 标志={flag}, 脚本={scriptPage}");
                return scriptEvent;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"创建脚本事件失败: 地图={mapId}, 位置=({x},{y})", exception: ex);
                return null;
            }
        }

        /// <summary>
        /// 释放脚本事件
        /// </summary>
        public void Release()
        {
            try
            {
                lock (_lock)
                {
                    _scriptEvents.Remove(this);
                }

                // 从地图移除
                var map = MapManager.Instance.GetMap((uint)MapId);
                if (map != null)
                {
                    map.RemoveObject(this);
                }

                LogManager.Default.Debug($"释放脚本事件: 地图={MapId}, 位置=({X},{Y}), 脚本={ScriptPage}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"释放脚本事件失败: 地图={MapId}, 位置=({X},{Y})", exception: ex);
            }
        }

        /// <summary>
        /// 当对象进入时调用
        /// </summary>
        public override void OnEnter(MapObject mapObject)
        {
            base.OnEnter(mapObject);

            if ((Flag & ScriptEventFlag.Enter) != 0)
            {
                LogManager.Default.Debug($"脚本事件进入触发: 地图={MapId}, 位置=({X},{Y}), 脚本={ScriptPage}, 对象={mapObject.GetType().Name}");
                
                // 执行脚本
                if (mapObject.GetObjectType() == ObjectType.Player)
                {
                    var player = mapObject as HumanPlayer;
                    if (player != null)
                    {
                        // 获取ScriptTarget
                        var scriptTarget = GetScriptTargetFromPlayer(player);
                        if (scriptTarget != null)
                        {
                            // 执行脚本
                            SystemScript.Instance.Execute(scriptTarget, ScriptPage);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 当对象离开时调用
        /// </summary>
        public override void OnLeave(MapObject mapObject)
        {
            base.OnLeave(mapObject);

            if ((Flag & ScriptEventFlag.Leave) != 0)
            {
                LogManager.Default.Debug($"脚本事件离开触发: 地图={MapId}, 位置=({X},{Y}), 脚本={ScriptPage}, 对象={mapObject.GetType().Name}");
                
                // 执行脚本
                if (mapObject.GetObjectType() == ObjectType.Player)
                {
                    var player = mapObject as HumanPlayer;
                    if (player != null)
                    {
                        // 获取ScriptTarget
                        var scriptTarget = GetScriptTargetFromPlayer(player);
                        if (scriptTarget != null)
                        {
                            // 执行脚本
                            SystemScript.Instance.Execute(scriptTarget, ScriptPage);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 从玩家对象获取ScriptTarget
        /// </summary>
        private ScriptTarget? GetScriptTargetFromPlayer(HumanPlayer player)
        {
            // 检查玩家是否实现了ScriptTarget接口
            if (player is ScriptTarget scriptTarget)
            {
                return scriptTarget;
            }
            
            // 如果玩家没有实现ScriptTarget接口，创建一个包装器
            return new PlayerScriptTargetWrapper(player);
        }

        /// <summary>
        /// 当进入地图时调用
        /// </summary>
        protected override void OnEnterMap(LogicMap map)
        {
            base.OnEnterMap(map);
            LogManager.Default.Debug($"脚本事件进入地图: 地图={map.MapId}, 位置=({X},{Y}), 脚本={ScriptPage}");
        }

        /// <summary>
        /// 获取对象类型
        /// </summary>
        public override ObjectType GetObjectType()
        {
            return ObjectType.ScriptEvent;
        }

        /// <summary>
        /// 获取所有脚本事件
        /// </summary>
        public static List<ScriptEvent> GetAllScriptEvents()
        {
            lock (_lock)
            {
                return new List<ScriptEvent>(_scriptEvents);
            }
        }

        /// <summary>
        /// 获取指定地图的脚本事件
        /// </summary>
        public static List<ScriptEvent> GetScriptEventsByMap(uint mapId)
        {
            var result = new List<ScriptEvent>();
            lock (_lock)
            {
                foreach (var scriptEvent in _scriptEvents)
                {
                    if (scriptEvent.MapId == mapId)
                    {
                        result.Add(scriptEvent);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 清理所有脚本事件
        /// </summary>
        public static void ClearAll()
        {
            lock (_lock)
            {
                foreach (var scriptEvent in _scriptEvents)
                {
                    scriptEvent.Release();
                }
                _scriptEvents.Clear();
                LogManager.Default.Info("清理所有脚本事件");
            }
        }
    }

    /// <summary>
    /// 地图脚本管理器
    /// 负责加载和管理地图脚本事件
    /// </summary>
    public class MapScriptManager
    {
        private static MapScriptManager? _instance;

        /// <summary>
        /// 获取MapScriptManager单例实例
        /// </summary>
        public static MapScriptManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new MapScriptManager();
                }
                return _instance;
            }
        }

        /// <summary>
        /// 私有构造函数
        /// </summary>
        private MapScriptManager()
        {
        }

        /// <summary>
        /// 解析事件地图位置字符串
        /// </summary>
        private bool ParseEventMapPosition(string eventStr, out EventMapPosition position)
        {
            position = new EventMapPosition();

            try
            {
                // 移除开头的'['字符
                string str = eventStr.Trim();
                if (str.StartsWith("["))
                {
                    str = str.Substring(1);
                }

                // 分割字符串
                var parts = str.Split(new[] { ',', ':', '|' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3)
                {
                    return false;
                }

                // 解析地图ID和坐标
                if (!uint.TryParse(parts[0].Trim(), out uint mapId) ||
                    !uint.TryParse(parts[1].Trim(), out uint x) ||
                    !uint.TryParse(parts[2].Trim(), out uint y))
                {
                    return false;
                }

                position.MapId = mapId;
                position.X = x;
                position.Y = y;
                position.Flag = ScriptEventFlag.Enter; // 默认标志

                // 解析标志（如果有）
                if (parts.Length > 3)
                {
                    var flagParts = parts[3].Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var flagPart in flagParts)
                    {
                        var flag = flagPart.Trim().ToLower();
                        if (flag == "enter")
                        {
                            position.Flag |= ScriptEventFlag.Enter;
                        }
                        else if (flag == "leave")
                        {
                            position.Flag |= ScriptEventFlag.Leave;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析事件地图位置失败: {eventStr}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 加载地图脚本配置文件
        /// </summary>
        public bool Load(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    LogManager.Default.Warning($"地图脚本配置文件不存在: {filePath}");
                    return false;
                }

                LogManager.Default.Info($"加载地图脚本配置: {filePath}");

                var lines = SmartReader.ReadAllLines(filePath);
                int loadedCount = 0;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    // 格式：[地图ID:(X,Y)] 脚本页面
                    var parts = line.Split(new[] { ']' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2)
                        continue;

                    string eventStr = parts[0].Trim();
                    string scriptPage = parts[1].Trim();

                    if (!eventStr.StartsWith("[") || string.IsNullOrEmpty(scriptPage))
                        continue;

                    // 解析事件位置
                    if (!ParseEventMapPosition(eventStr, out var position))
                        continue;

                    // 添加地图脚本
                    AddMapScript(position, scriptPage);
                    loadedCount++;
                }

                LogManager.Default.Info($"成功加载 {loadedCount} 个地图脚本配置");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载地图脚本配置失败: {filePath}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 添加地图脚本
        /// </summary>
        public void AddMapScript(EventMapPosition position, string scriptPage)
        {
            try
            {
                // 创建脚本事件
                var scriptEvent = ScriptEvent.Create(position.MapId, position.X, position.Y, position.Flag, scriptPage);
                if (scriptEvent == null)
                {
                    LogManager.Default.Warning($"创建地图脚本失败: 地图={position.MapId}, 位置=({position.X},{position.Y}), 脚本={scriptPage}");
                    return;
                }

                // 添加到地图
                var map = MapManager.Instance.GetMap(position.MapId);
                if (map == null)
                {
                    LogManager.Default.Warning($"地图不存在: {position.MapId}");
                    scriptEvent.Release();
                    return;
                }

                if (!map.AddObject(scriptEvent, (int)position.X, (int)position.Y))
                {
                    LogManager.Default.Warning($"无法将脚本事件添加到地图: {position.MapId}");
                    scriptEvent.Release();
                    return;
                }

                LogManager.Default.Debug($"添加地图脚本: 地图={position.MapId}, 位置=({position.X},{position.Y}), 标志={position.Flag}, 脚本={scriptPage}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"添加地图脚本失败: 地图={position.MapId}, 位置=({position.X},{position.Y})", exception: ex);
            }
        }

        /// <summary>
        /// 重新加载地图脚本配置
        /// </summary>
        public bool Reload(string filePath)
        {
            try
            {
                // 清理现有脚本事件
                ScriptEvent.ClearAll();
                return Load(filePath);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"重新加载地图脚本配置失败: {filePath}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 获取所有脚本事件
        /// </summary>
        public List<ScriptEvent> GetAllScriptEvents()
        {
            return ScriptEvent.GetAllScriptEvents();
        }

        /// <summary>
        /// 获取指定地图的脚本事件
        /// </summary>
        public List<ScriptEvent> GetScriptEventsByMap(uint mapId)
        {
            return ScriptEvent.GetScriptEventsByMap(mapId);
        }

        /// <summary>
        /// 获取脚本事件数量
        /// </summary>
        public int GetScriptEventCount()
        {
            return ScriptEvent.GetAllScriptEvents().Count;
        }

        /// <summary>
        /// 清理所有地图脚本
        /// </summary>
        public void ClearAll()
        {
            ScriptEvent.ClearAll();
            LogManager.Default.Info("清理所有地图脚本");
        }

        /// <summary>
        /// 更新地图脚本管理器（供GameWorld调用）
        /// </summary>
        public void Update()
        {
            // 目前没有需要更新的内容
            // 可以在这里添加脚本事件的定期检查等逻辑
        }
    }

    /// <summary>
    /// 玩家ScriptTarget包装器
    /// </summary>
    public class PlayerScriptTargetWrapper : ScriptTarget
    {
        private readonly HumanPlayer _player;

        public PlayerScriptTargetWrapper(HumanPlayer player)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
        }

        /// <summary>
        /// 获取目标名称
        /// </summary>
        public string GetTargetName()
        {
            return _player.Name;
        }
        
        /// <summary>
        /// 获取目标ID
        /// </summary>
        public uint GetTargetId()
        {
            return _player.ObjectId;
        }
        
        /// <summary>
        /// 执行脚本动作
        /// </summary>
        public void ExecuteScriptAction(string action, params string[] parameters)
        {
            // 这里应该根据action执行相应的脚本动作
            LogManager.Default.Debug($"玩家 {_player.Name} 执行脚本动作: {action}, 参数: {string.Join(", ", parameters)}");
            
            // 根据动作类型执行相应的逻辑
            switch (action.ToLower())
            {
                case "say":
                    if (parameters.Length > 0)
                    {
                        _player.Say(parameters[0]);
                    }
                    break;
                case "additem":
                    if (parameters.Length > 0 && int.TryParse(parameters[0], out int itemId))
                    {
                        // 添加物品逻辑
                        LogManager.Default.Info($"脚本动作: 给玩家 {_player.Name} 添加物品 {itemId}");
                    }
                    break;
                case "addgold":
                    if (parameters.Length > 0 && uint.TryParse(parameters[0], out uint gold))
                    {
                        _player.AddGold(gold);
                    }
                    break;
                case "teleport":
                    if (parameters.Length > 2 && 
                        uint.TryParse(parameters[0], out uint mapId) &&
                        uint.TryParse(parameters[1], out uint x) &&
                        uint.TryParse(parameters[2], out uint y))
                    {
                        // 传送逻辑
                        LogManager.Default.Info($"脚本动作: 传送玩家 {_player.Name} 到地图 {mapId} ({x},{y})");
                    }
                    break;
                default:
                    LogManager.Default.Warning($"未知的脚本动作: {action}");
                    break;
            }
        }
    }
}
