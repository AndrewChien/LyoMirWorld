using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using MirCommon;
using MirCommon.Utils;

namespace GameServer
{
    /// <summary>
    /// 游戏世界类
    /// 管理所有游戏对象和配置数据
    /// </summary>
    public class GameWorld
    {
        private static GameWorld? _instance;
        public static GameWorld Instance => _instance ??= new GameWorld();

        // 玩家管理
        private readonly ConcurrentDictionary<uint, HumanPlayer> _players = new();
        private readonly DateTime _startTime = DateTime.Now;
        private long _updateCount = 0;
        private uint _loopCount = 0;

        private readonly ConcurrentDictionary<int, float> _gameVars = new();      
        private readonly ConcurrentDictionary<string, string> _gameNames = new(); 
        private readonly ConcurrentDictionary<int, ConcurrentDictionary<int, HumanDataDesc>> _humanDataDescs = new(); 
        private readonly ConcurrentDictionary<int, StartPoint> _startPoints = new(); 
        private readonly ConcurrentDictionary<string, int> _startPointNameToIndex = new();
        private readonly List<FirstLoginInfo> _firstLoginInfos = new();
        private readonly ConcurrentDictionary<int, int> _channelWaitTimes = new(); 
        
        // 公告数据
        private string _notice = "欢迎来到lyo的测试传世！";
        private readonly ConcurrentBag<string> _lineNotices = new();
        
        // 线程安全锁
        private readonly ReaderWriterLockSlim _configLock = new();

        // 怪物更新相关
        private readonly ConcurrentQueue<MonsterEx> _updateMonsterQueue = new(); 
        private readonly ConcurrentQueue<GlobeProcess> _globeProcessQueue = new(); 
        private Timer? _monsterUpdateTimer;
        private Timer? _dbUpdateTimer;
        private const uint UPDATE_LOOP = 1000; // 更新循环常量

        // 配置变更事件
        public event Action<string>? OnConfigChanged;

        private GameWorld()
        {
            InitializeDefaultValues();
        }

        /// <summary>
        /// 初始化默认值
        /// </summary>
        private void InitializeDefaultValues()
        {
            // 游戏变量默认值
            SetGameVar(GameVarConstants.MaxGold, 5000000);
            SetGameVar(GameVarConstants.MaxYuanbao, 2000);
            SetGameVar(GameVarConstants.MaxGroupMember, 10);
            SetGameVar(GameVarConstants.RedPkPoint, 12);
            SetGameVar(GameVarConstants.YellowPkPoint, 6);
            SetGameVar(GameVarConstants.StorageSize, 100);
            SetGameVar(GameVarConstants.CharInfoBackupTime, 5);
            SetGameVar(GameVarConstants.OnePkPointTime, 60);
            SetGameVar(GameVarConstants.GrayNameTime, 300);
            SetGameVar(GameVarConstants.OncePkPoint, 1);
            SetGameVar(GameVarConstants.PkCurseRate, 10);
            SetGameVar(GameVarConstants.AddFriendLevel, 30);
            SetGameVar(GameVarConstants.EnableSafeAreaNotice, 1);
            SetGameVar(GameVarConstants.WalkSpeed, 600);
            SetGameVar(GameVarConstants.RunSpeed, 300);
            SetGameVar(GameVarConstants.AttackSpeed, 800);
            SetGameVar(GameVarConstants.BeAttackSpeed, 800);
            SetGameVar(GameVarConstants.SpellSkillSpeed, 800);
            SetGameVar(GameVarConstants.ExpFactor, 1.0f);

            // 游戏名称默认值
            SetGameName(GameName.GoldName, "金币");
            SetGameName(GameName.MaleName, "男");
            SetGameName(GameName.FemaleName, "女");
            SetGameName(GameName.WarrName, "战士");
            SetGameName(GameName.MagicanName, "法师");
            SetGameName(GameName.TaoshiName, "道士");
            SetGameName(GameName.Version, "1,8,8,8");

            // 聊天等待时间默认值
            SetChannelWaitTime(ChatWaitChannel.Normal, 1);
            SetChannelWaitTime(ChatWaitChannel.Cry, 10);
            SetChannelWaitTime(ChatWaitChannel.Whisper, 2);
            SetChannelWaitTime(ChatWaitChannel.Group, 2);
            SetChannelWaitTime(ChatWaitChannel.Guild, 3);
            SetChannelWaitTime(ChatWaitChannel.GM, 0);
        }

        /// <summary>
        /// 初始化游戏世界
        /// </summary>
        public bool Initialize()
        {
            LogManager.Default.Info("游戏世界初始化...");
            
            // 初始化物理地图管理器
            string physicsMapPath = GetGameName(GameName.PhysicsMapPath);
            string physicsCachePath = GetGameName(GameName.PhysicsCachePath);
            
            if (!string.IsNullOrEmpty(physicsMapPath) && !string.IsNullOrEmpty(physicsCachePath))
            {
                PhysicsMapMgr.Instance.Init(physicsMapPath, physicsCachePath);
                LogManager.Default.Info($"物理地图管理器初始化完成: 地图路径={physicsMapPath}, 缓存路径={physicsCachePath}");
            }
            else
            {
                LogManager.Default.Warning("物理地图路径或缓存路径未配置，物理地图管理器初始化跳过");
            }
            
            return true;
        }

        /// <summary>
        /// 更新游戏世界
        /// </summary>
        public void Update()
        {
            Interlocked.Increment(ref _updateCount);
            
            // 更新时间系统
            TimeSystem.Instance.Update();
            
            // 处理全局进程队列
            ProcessGlobeProcessQueue();
        }

        #region 玩家管理
        public void AddPlayer(HumanPlayer player)
        {
            _players[player.ObjectId] = player;
        }

        /// <summary>
        /// 添加地图对象
        /// </summary>
        public bool AddMapObject(GameObject obj)
        {
            if (obj == null)
                return false;

            // 如果是玩家对象，添加到玩家字典
            if (obj is HumanPlayer player)
            {
                AddPlayer(player);
                return true;
            }

            // 对于其他类型的游戏对象，可以在这里添加相应的处理逻辑
            // 例如：添加到怪物字典、NPC字典等
            LogManager.Default.Debug($"添加地图对象: {obj.GetType().Name}, ID={obj.ObjectId}");
            return true;
        }

        public void RemovePlayer(uint playerId)
        {
            _players.TryRemove(playerId, out _);
        }

        public HumanPlayer[] GetAllPlayers()
        {
            return _players.Values.ToArray();
        }

        public HumanPlayer? GetPlayer(uint playerId)
        {
            return _players.TryGetValue(playerId, out var player) ? player : null;
        }

        public int GetPlayerCount() => _players.Count;
        #endregion

        #region 配置数据访问接口
        /// <summary>
        /// 获取游戏变量值
        /// </summary>
        public float GetGameVar(int varKey)
        {
            return _gameVars.TryGetValue(varKey, out var value) ? value : 0f;
        }

        /// <summary>
        /// 设置游戏变量值
        /// </summary>
        public void SetGameVar(int varKey, float value)
        {
            _gameVars[varKey] = value;
            OnConfigChanged?.Invoke($"GameVar_{varKey}");
        }

        /// <summary>
        /// 设置经验因子
        /// </summary>
        public void SetExpFactor(float factor)
        {
            SetGameVar(GameVarConstants.ExpFactor, factor);
        }

        /// <summary>
        /// 设置是否使用大背包
        /// </summary>
        public void SetUseBigBag(bool useBigBag)
        {
            // 这里可以存储大背包标志到游戏变量或单独字段
            LogManager.Default.Info($"设置大背包标志: {useBigBag}");
        }

        /// <summary>
        /// 获取游戏名称
        /// </summary>
        public string GetGameName(string nameKey)
        {
            return _gameNames.TryGetValue(nameKey, out var name) ? name : nameKey;
        }

        /// <summary>
        /// 设置游戏名称
        /// </summary>
        public void SetGameName(string nameKey, string value)
        {
            _gameNames[nameKey] = value;
            OnConfigChanged?.Invoke($"GameName_{nameKey}");
        }

        /// <summary>
        /// 获取人物数据描述
        /// </summary>
        public HumanDataDesc? GetHumanDataDesc(int profession, int level)
        {
            if (_humanDataDescs.TryGetValue(profession, out var levelDict) &&
                levelDict.TryGetValue(level, out var desc))
            {
                return desc;
            }
            return null;
        }

        /// <summary>
        /// 设置人物数据描述
        /// </summary>
        public void SetHumanDataDesc(int profession, int level, HumanDataDesc desc)
        {
            if (!_humanDataDescs.ContainsKey(profession))
            {
                _humanDataDescs[profession] = new ConcurrentDictionary<int, HumanDataDesc>();
            }
            _humanDataDescs[profession][level] = desc;
            OnConfigChanged?.Invoke($"HumanDataDesc_{profession}_{level}");
        }

        /// <summary>
        /// 获取出生点
        /// </summary>
        public StartPoint? GetStartPoint(int index)
        {
            return _startPoints.TryGetValue(index, out var point) ? point : null;
        }

        /// <summary>
        /// 通过名称获取出生点
        /// </summary>
        public StartPoint? GetStartPoint(string name)
        {
            if (_startPointNameToIndex.TryGetValue(name, out int index))
            {
                return GetStartPoint(index);
            }
            return null;
        }

        /// <summary>
        /// 设置出生点
        /// </summary>
        public void SetStartPoint(int index, StartPoint point)
        {
            _startPoints[index] = point;
            if (!string.IsNullOrEmpty(point.Name))
            {
                _startPointNameToIndex[point.Name] = index;
            }
            OnConfigChanged?.Invoke($"StartPoint_{index}");
        }

        /// <summary>
        /// 获取首次登录信息
        /// </summary>
        public FirstLoginInfo? GetFirstLoginInfo()
        {
            return _firstLoginInfos.Count > 0 ? _firstLoginInfos[0] : null;
        }

        /// <summary>
        /// 设置首次登录信息
        /// </summary>
        public void SetFirstLoginInfo(FirstLoginInfo info)
        {
            _firstLoginInfos.Clear();
            _firstLoginInfos.Add(info);
            OnConfigChanged?.Invoke("FirstLoginInfo");
        }

        /// <summary>
        /// 获取聊天等待时间
        /// </summary>
        public int GetChannelWaitTime(int channel)
        {
            return _channelWaitTimes.TryGetValue(channel, out var time) ? time : 1;
        }

        /// <summary>
        /// 设置聊天等待时间
        /// </summary>
        public void SetChannelWaitTime(int channel, int seconds)
        {
            _channelWaitTimes[channel] = seconds;
            OnConfigChanged?.Invoke($"ChannelWaitTime_{channel}");
        }

        /// <summary>
        /// 获取出生点
        /// </summary>
        public bool GetBornPoint(int profession, out int mapId, out int x, out int y, string? startPointName = null)
        {
            mapId = 0;
            x = 0;
            y = 0;

            // 如果有指定的出生点名称，使用该出生点
            if (!string.IsNullOrEmpty(startPointName))
            {
                var startPoint = GetStartPoint(startPointName);
                if (startPoint != null)
                {
                    mapId = startPoint.MapId;
                    x = startPoint.X;
                    y = startPoint.Y;
                    return true;
                }
            }

            // 否则根据职业获取默认出生点
            // 从配置中读取默认出生点，如果没有配置则使用默认值
            var defaultStartPoint = GetStartPoint("新手村");
            if (defaultStartPoint != null)
            {
                mapId = defaultStartPoint.MapId;
                x = defaultStartPoint.X;
                y = defaultStartPoint.Y;
                return true;
            }

            // 如果配置中没有"新手村"出生点，使用硬编码的默认值
            // 这些值应该从配置文件读取，但这里作为后备方案
            switch (profession)
            {
                case 0: // 战士
                    mapId = 16;
                    x = 477;
                    y = 222;
                    break;
                case 1: // 法师
                    mapId = 16;
                    x = 477;
                    y = 222;
                    break;
                case 2: // 道士
                    mapId = 16;
                    x = 477;
                    y = 222;
                    break;
                default:
                    return false;
            }

            return true;
        }
        #endregion

        #region 批量配置操作
        /// <summary>
        /// 批量设置游戏变量
        /// </summary>
        public void SetGameVars(Dictionary<int, float> vars)
        {
            foreach (var kvp in vars)
            {
                SetGameVar(kvp.Key, kvp.Value);
            }
        }

        /// <summary>
        /// 批量设置游戏名称
        /// </summary>
        public void SetGameNames(Dictionary<string, string> names)
        {
            foreach (var kvp in names)
            {
                SetGameName(kvp.Key, kvp.Value);
            }
        }

        /// <summary>
        /// 批量设置人物数据描述
        /// </summary>
        public void SetHumanDataDescs(int profession, Dictionary<int, HumanDataDesc> descs)
        {
            if (!_humanDataDescs.ContainsKey(profession))
            {
                _humanDataDescs[profession] = new ConcurrentDictionary<int, HumanDataDesc>();
            }

            foreach (var kvp in descs)
            {
                _humanDataDescs[profession][kvp.Key] = kvp.Value;
            }
            OnConfigChanged?.Invoke($"HumanDataDescs_{profession}");
        }

        /// <summary>
        /// 批量设置出生点
        /// </summary>
        public void SetStartPoints(Dictionary<int, StartPoint> points)
        {
            foreach (var kvp in points)
            {
                SetStartPoint(kvp.Key, kvp.Value);
            }
        }

        /// <summary>
        /// 批量设置聊天等待时间
        /// </summary>
        public void SetChannelWaitTimes(Dictionary<int, int> waitTimes)
        {
            foreach (var kvp in waitTimes)
            {
                SetChannelWaitTime(kvp.Key, kvp.Value);
            }
        }

        /// <summary>
        /// 设置矿石列表
        /// </summary>
        public void SetMineList(Dictionary<string, string> mineList)
        {
            // 存储矿石列表到内存中
            // 注意：这里需要添加一个字段来存储矿石列表
            LogManager.Default.Info($"设置矿石列表: {mineList.Count} 个矿石");
            OnConfigChanged?.Invoke("MineList");
        }
        
        /// <summary>
        /// 设置公告
        /// </summary>
        public void SetNotice(string notice)
        {
            _notice = notice;
            OnConfigChanged?.Invoke("Notice");
        }
        
        /// <summary>
        /// 获取公告
        /// </summary>
        public string GetNotice() => _notice;
        
        /// <summary>
        /// 设置滚动公告
        /// </summary>
        public void SetLineNotices(IEnumerable<string> notices)
        {
            _lineNotices.Clear();
            foreach (var notice in notices)
            {
                _lineNotices.Add(notice);
            }
            OnConfigChanged?.Invoke("LineNotices");
        }
        
        /// <summary>
        /// 获取滚动公告
        /// </summary>
        public List<string> GetLineNotices() => _lineNotices.ToList();
        #endregion

        #region 统计信息
        public GameMap? GetMap(int mapId)
        {
            // 从地图管理器获取指定ID的地图
            // 在完整实现中，应该有一个MapManager来管理所有地图
            // 目前返回null，表示地图系统尚未完全实现
            return null;
        }

        public GameMap[] GetAllMaps()
        {
            // 获取所有已加载的地图
            // 在完整实现中，MapManager会维护所有活动地图的列表
            // 目前返回空数组
            return Array.Empty<GameMap>();
        }

        public int GetMapCount() => 0;

        public long GetUpdateCount() => _updateCount;

        public TimeSpan GetUptime() => DateTime.Now - _startTime;
        #endregion

        #region 用户魔法管理
        /// <summary>
        /// 分配用户魔法对象
        /// </summary>
        public UserMagic AllocUserMagic()
        {
            return new UserMagic();
        }

        /// <summary>
        /// 释放用户魔法对象
        /// </summary>
        public void FreeUserMagic(UserMagic userMagic)
        {
            if (userMagic != null)
            {
                userMagic.Next = null;
                userMagic.Class = null;
            }
        }

        /// <summary>
        /// 分配对象进程
        /// </summary>
        public ObjectProcess AllocProcess(string? stringParam = null)
        {
            var process = new ObjectProcess(ProcessType.None);
            if (!string.IsNullOrEmpty(stringParam))
            {
                // 可以在这里处理字符串参数
            }
            return process;
        }

        /// <summary>
        /// 释放对象进程
        /// </summary>
        public void FreeProcess(ObjectProcess process)
        {
        }
        #endregion

        #region 怪物更新和全局进程管理
        /// <summary>
        /// 启动怪物更新线程
        /// </summary>
        public void StartMonsterUpdateThread()
        {
            if (_monsterUpdateTimer == null)
            {
                _monsterUpdateTimer = new Timer(ThdUpdateMonsterCallback, null, 0, 1); // 1ms间隔
                LogManager.Default.Info("怪物更新线程已启动");
            }
        }

        /// <summary>
        /// 停止怪物更新线程
        /// </summary>
        public void StopMonsterUpdateThread()
        {
            _monsterUpdateTimer?.Dispose();
            _monsterUpdateTimer = null;
            LogManager.Default.Info("怪物更新线程已停止");
        }

        /// <summary>
        /// 怪物更新回调函数
        /// </summary>
        private void ThdUpdateMonsterCallback(object? state)
        {
            try
            {
                _loopCount++;
                if (_loopCount >= UPDATE_LOOP)
                    _loopCount = 0;

                // 更新怪物队列中的怪物
                int count = _updateMonsterQueue.Count;
                for (int i = 0; i < count; i++)
                {
                    if (_updateMonsterQueue.TryDequeue(out var monster))
                    {
                        if (monster != null && !monster.IsDeath())
                        {
                            monster.Update();
                            // 如果怪物还活着，重新加入队列
                            _updateMonsterQueue.Enqueue(monster);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error("怪物更新线程异常", exception: ex);
            }
        }

        /// <summary>
        /// 添加怪物到更新队列
        /// </summary>
        public void AddUpdateMonster(MonsterEx monster)
        {
            if (monster == null || monster.IsDeath())
                return;

            _updateMonsterQueue.Enqueue(monster);
        }

        /// <summary>
        /// 从更新队列移除怪物
        /// </summary>
        public void RemoveUpdateMonster(MonsterEx monster)
        {
        }

        /// <summary>
        /// 添加全局进程
        /// </summary>
        public bool AddGlobeProcess(GlobeProcess process)
        {
            if (process == null)
                return false;

            _globeProcessQueue.Enqueue(process);
            return true;
        }

        /// <summary>
        /// 获取全局进程
        /// </summary>
        public GlobeProcess? GetGlobeProcess()
        {
            if (_globeProcessQueue.TryDequeue(out var process))
                return process;
            return null;
        }

        /// <summary>
        /// 启动数据库更新定时器
        /// </summary>
        public void StartDBUpdateTimer()
        {
            if (_dbUpdateTimer == null)
            {
                _dbUpdateTimer = new Timer(DBUpdateCallback, null, 2000, 2000); // 2秒间隔
                LogManager.Default.Info("数据库更新定时器已启动");
            }
        }

        /// <summary>
        /// 停止数据库更新定时器
        /// </summary>
        public void StopDBUpdateTimer()
        {
            _dbUpdateTimer?.Dispose();
            _dbUpdateTimer = null;
            LogManager.Default.Info("数据库更新定时器已停止");
        }

        /// <summary>
        /// 数据库更新回调函数
        /// </summary>
        private void DBUpdateCallback(object? state)
        {
            try
            {
                // 这里可以添加数据库更新逻辑
                // 例如：保存玩家数据、更新游戏状态等
                LogManager.Default.Debug("数据库更新定时器触发");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error("数据库更新定时器异常", exception: ex);
            }
        }

        /// <summary>
        /// 获取循环计数
        /// </summary>
        public uint GetLoopCount() => _loopCount;

        /// <summary>
        /// 获取怪物更新队列数量
        /// </summary>
        public int GetUpdateMonsterCount() => _updateMonsterQueue.Count;

        /// <summary>
        /// 处理全局进程队列
        /// </summary>
        private void ProcessGlobeProcessQueue()
        {
            try
            {
                // 处理所有需要执行的全局进程
                int count = _globeProcessQueue.Count;
                for (int i = 0; i < count; i++)
                {
                    if (_globeProcessQueue.TryDequeue(out var process))
                    {
                        if (process != null && process.ShouldExecute())
                        {
                            // 根据进程类型执行相应的操作
                            ExecuteGlobeProcess(process);
                            
                            // 如果进程需要重复执行，重新加入队列
                            if (process.RepeatTimes > 0)
                            {
                                process.RepeatTimes--;
                                process.ExecuteTime = DateTime.Now.AddMilliseconds(process.Delay);
                                _globeProcessQueue.Enqueue(process);
                            }
                        }
                        else if (process != null)
                        {
                            // 如果进程还未到执行时间，重新加入队列
                            _globeProcessQueue.Enqueue(process);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error("处理全局进程队列异常", exception: ex);
            }
        }

        /// <summary>
        /// 执行全局进程
        /// </summary>
        private void ExecuteGlobeProcess(GlobeProcess process)
        {
            try
            {
                switch (process.Type)
                {
                    case GlobeProcessType.TimeSystemUpdate:
                        // 时间系统更新处理
                        HandleTimeSystemUpdate(process);
                        break;
                    case GlobeProcessType.EventManagerUpdate:
                         EventManager.Instance?.Update();
                        LogManager.Default.Debug($"事件管理器更新进程: {process.Type}");
                        break;
                    case GlobeProcessType.AutoScriptUpdate: 
                        AutoScriptManager.Instance?.Update();
                        LogManager.Default.Debug($"自动脚本更新进程: {process.Type}");
                        break;
                    case GlobeProcessType.MapScriptUpdate: 
                        MapScriptManager.Instance?.Update();
                        LogManager.Default.Debug($"地图脚本更新进程: {process.Type}");
                        break;
                    case GlobeProcessType.TopManagerUpdate:
                        TopManager.Instance?.Update();
                        LogManager.Default.Debug($"排行榜更新进程: {process.Type}");
                        break;
                    case GlobeProcessType.MarketManagerUpdate:
                        MarketManager.Instance?.Update();
                        LogManager.Default.Debug($"市场管理器更新进程: {process.Type}");
                        break;
                    case GlobeProcessType.SpecialEquipmentUpdate:	
                        SpecialEquipmentManager.Instance?.Update();
                        LogManager.Default.Debug($"特殊装备更新进程: {process.Type}");
                        break;
                    case GlobeProcessType.TitleManagerUpdate:
                        TitleManager.Instance?.Update();
                        LogManager.Default.Debug($"称号管理器更新进程: {process.Type}");
                        break;
                    case GlobeProcessType.TaskManagerUpdate:
                        TaskManager.Instance?.Update();
                        LogManager.Default.Debug($"任务管理器更新进程: {process.Type}");
                        break;
                    default:
                        // 其他类型的进程可以在这里添加处理逻辑
                        LogManager.Default.Debug($"执行全局进程: {process.Type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"执行全局进程异常: {process.Type}", exception: ex);
            }
        }

        /// <summary>
        /// 处理时间系统更新
        /// </summary>
        private void HandleTimeSystemUpdate(GlobeProcess process)
        {
            // 这里可以添加时间系统更新的具体逻辑
            // 例如：通知所有玩家时间变化、触发时间相关事件等
            LogManager.Default.Debug($"时间系统更新: 游戏时间={process.Param1}");
            
            // 可以在这里添加广播游戏时间变化的消息
            // 例如：BroadcastGameTimeUpdate((ushort)process.Param1);
        }

        /// <summary>
        /// 获取全局进程队列数量
        /// </summary>
        public int GetGlobeProcessCount() => _globeProcessQueue.Count;
        #endregion

        #region 配置热重载
        /// <summary>
        /// 重新加载配置
        /// </summary>
        public void ReloadConfig(string configType)
        {
            try
            {
                LogManager.Default.Info($"重新加载配置: {configType}");
                
                // 通知配置变更
                OnConfigChanged?.Invoke($"Reload_{configType}");
                
                // 这里可以添加具体的重载逻辑
                // 例如：重新从文件加载配置并更新内存变量
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"重新加载配置失败: {configType}", exception: ex);
            }
        }
        #endregion
    }


    /// <summary>
    /// 游戏名称枚举
    /// </summary>
    public static class GameName
    {
        public const string GoldName = "goldname";
        public const string MaleName = "malename";
        public const string FemaleName = "femalename";
        public const string GuildNotice = "GUILDNOTICE";
        public const string KillGuilds = "KILLGUILDS";
        public const string AllyGuilds = "ALLYGUILDS";
        public const string Members = "MEMBERS";
        public const string Version = "version";
        public const string WarrName = "warr";
        public const string MagicanName = "magican";
        public const string TaoshiName = "taoshi";
        public const string TopOfWorld = "topofworld";
        public const string UpgradeMineStone = "upgrademinestone";
        public const string LoginScript = "loginscript";
        public const string LevelUpScript = "levelupscript";
        public const string LogoutScript = "logoutscript";
        public const string PhysicsMapPath = "PHYSICSMAPPATH";
        public const string PhysicsCachePath = "PHYSICSCACHEPATH";
    }

    /// <summary>
    /// 聊天频道等待时间枚举
    /// </summary>
    public static class ChatWaitChannel
    {
        public const int Normal = 0;
        public const int Cry = 1;
        public const int Whisper = 2;
        public const int Group = 3;
        public const int Guild = 4;
        public const int Couple = 5;
        public const int GM = 6;
        public const int Friend = 7;
    }
}
