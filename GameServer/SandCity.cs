using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MirCommon;
using MirCommon.Utils;

namespace GameServer
{
    /// <summary>
    /// 攻击沙城请求结构体
    /// </summary>
    public struct AttackSabukRequest
    {
        public string GuildName { get; set; }
        public DateTime AttackTime { get; set; }
        public GuildEx Guild { get; set; }
    }

    /// <summary>
    /// 沙城霸主信息结构体
    /// </summary>
    public struct TopCharInfo
    {
        public string Name { get; set; }
        public string GuildName { get; set; }
        public byte Sex { get; set; }
        public byte Class { get; set; }
        public ushort Level { get; set; }
        public uint Exp { get; set; }
        public uint DBId { get; set; }
        public DateTime RankingTime { get; set; }
    }

    /// <summary>
    /// 沙城系统
    /// </summary>
    public class SandCity : ITimeEventObject
    {
        private static SandCity _instance;
        public static SandCity Instance => _instance ??= new SandCity();

        // 常量定义
        private const int MAX_ATTACKREQUEST = 100;
        private const int PAGEREQUESTCOUNT = 8;

        // 沙城组件
        private CSCPalaceDoor _palaceDoor;
        private CSCDoor _mainGate;
        private CPalaceWall _leftWall;
        private CPalaceWall _centerWall;
        private CPalaceWall _rightWall;
        private CSCArcher[] _archers = new CSCArcher[12];

        // 沙城属性
        private string _name = string.Empty;
        private GuildEx _ownerGuild;
        private uint _totalGold;
        private uint _todayIncome;
        private DateTime _changeTime;
        private DateTime _incomeTime;
        private DateTime _warTime;
        private bool _warStarted;
        private uint _homeX;
        private uint _homeY;
        private uint _homeMapId;
        private uint _castleMapId;
        private uint _warRangeX;
        private uint _warRangeY;
        private uint _palaceMapId;
        private uint _palaceDoorX;
        private uint _palaceDoorY;
        private uint _castlePalaceDoorX;
        private uint _castlePalaceDoorY;
        private float _texRate;
        private uint _texRatePercent;
        private uint _rebate;
        private Npc _sabukMaster;
        private TopCharInfo _sabukMasterInfo;

        // 攻城请求管理
        private AttackSabukRequest[] _attackRequests = new AttackSabukRequest[MAX_ATTACKREQUEST];
        private int _attackRequestCount;
        private GuildEx[] _warGuilds = new GuildEx[MAX_ATTACKREQUEST];
        private int _warGuildCount;

        // 攻城战状态
        private bool _identifyStart;
        private ServerTimer _identifyTimer;
        private ServerTimer _warTimer;

        // 地图引用
        private LogicMap _palaceMap;

        /// <summary>
        /// 私有构造函数
        /// </summary>
        private SandCity()
        {
            _palaceDoor = new CSCPalaceDoor();
            _mainGate = new CSCDoor();
            _leftWall = new CPalaceWall();
            _centerWall = new CPalaceWall();
            _rightWall = new CPalaceWall();
            _identifyTimer = new ServerTimer();
            _warTimer = new ServerTimer();
            _warStarted = false;
            _identifyStart = false;
            _attackRequestCount = 0;
            _warGuildCount = 0;
            _texRate = 0.5f;
        }

        /// <summary>
        /// 获取沙城名称
        /// </summary>
        public string GetName() => _name;

        /// <summary>
        /// 获取沙城所有者行会
        /// </summary>
        public GuildEx GetOwnerGuild() => _ownerGuild;

        /// <summary>
        /// 获取沙城总资金
        /// </summary>
        public uint GetTotalGold() => _totalGold;

        /// <summary>
        /// 获取今日收入
        /// </summary>
        public uint GetTodayIncoming() => _todayIncome;

        /// <summary>
        /// 获取税率
        /// </summary>
        public uint GetTexRate() => _texRatePercent;

        /// <summary>
        /// 设置税率
        /// </summary>
        public void SetTexRate(uint rate)
        {
            _texRatePercent = rate;
            _texRate = (float)rate / 100.0f;
        }

        /// <summary>
        /// 获取返利比例
        /// </summary>
        public uint GetRebate() => _rebate;

        /// <summary>
        /// 设置返利比例
        /// </summary>
        public void SetRebate(uint rebate) => _rebate = rebate;

        /// <summary>
        /// 检查攻城战是否开始
        /// </summary>
        public bool IsWarStarted() => _warStarted;

        /// <summary>
        /// 获取沙城霸主信息
        /// </summary>
        public TopCharInfo GetMasterInfo() => _sabukMasterInfo;

        /// <summary>
        /// 初始化沙城系统
        /// </summary>
        public bool Init()
        {
            try
            {
                string configPath = ".\\data\\GuildBase\\SabukW.txt";
                if (!File.Exists(configPath))
                {
                    LogManager.Default.Error($"沙城描述文件不存在: {configPath}");
                    return false;
                }

                // 读取配置文件
                string[] lines = SmartReader.ReadAllLines(configPath);
                var config = new Dictionary<string, Dictionary<string, string>>();
                string currentSection = "";
                
                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                        continue;
                        
                    if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    {
                        currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
                        config[currentSection] = new Dictionary<string, string>();
                    }
                    else if (currentSection != "")
                    {
                        int equalsIndex = trimmedLine.IndexOf('=');
                        if (equalsIndex > 0)
                        {
                            string key = trimmedLine.Substring(0, equalsIndex).Trim();
                            string value = trimmedLine.Substring(equalsIndex + 1).Trim();
                            config[currentSection][key] = value;
                        }
                    }
                }

                // 读取基本设置
                _name = GetConfigValue(config, "setup", "castleName", "沙巴克");
                string ownerGuildName = GetConfigValue(config, "setup", "OwnGuild", "");
                if (!string.IsNullOrEmpty(ownerGuildName))
                {
                    _ownerGuild = GuildManagerEx.GetGuildByName(ownerGuildName);
                }

                // 读取时间信息
                _changeTime = ParseDateTime(GetConfigValue(config, "setup", "ChangeDate", "2005-9-25 0:0:0"));
                _warTime = ParseDateTime(GetConfigValue(config, "setup", "wardate", "2005-9-25 0:0:0"));
                _incomeTime = ParseDateTime(GetConfigValue(config, "setup", "IncomeToday", "2005-9-25 0:0:0"));

                // 读取财务信息
                _todayIncome = uint.Parse(GetConfigValue(config, "setup", "TodayIncome", "0"));
                _totalGold = uint.Parse(GetConfigValue(config, "setup", "TotalGold", "0"));

                uint texRate = uint.Parse(GetConfigValue(config, "setup", "texrate", "50"));
                SetRebate(uint.Parse(GetConfigValue(config, "setup", "rebate", "70")));
                SetTexRate(texRate);

                // 读取防御设置
                _homeX = uint.Parse(GetConfigValue(config, "defense", "CastleHomeX", "224"));
                _homeY = uint.Parse(GetConfigValue(config, "defense", "CastleHomeY", "436"));
                _homeMapId = uint.Parse(GetConfigValue(config, "defense", "CastleHomeMap", "116"));
                _castleMapId = uint.Parse(GetConfigValue(config, "defense", "CastleMap", "116"));
                _warRangeX = uint.Parse(GetConfigValue(config, "defense", "CastleWarRangeX", "100"));
                _warRangeY = uint.Parse(GetConfigValue(config, "defense", "CastleWarRangeY", "100"));

                _palaceMapId = uint.Parse(GetConfigValue(config, "defense", "PalaceMap", "123"));
                _palaceDoorX = uint.Parse(GetConfigValue(config, "defense", "PalaceDoorX", "18"));
                _palaceDoorY = uint.Parse(GetConfigValue(config, "defense", "PalaceDoorY", "25"));
                _castlePalaceDoorX = uint.Parse(GetConfigValue(config, "defense", "CastlePalaceDoorX", "167"));
                _castlePalaceDoorY = uint.Parse(GetConfigValue(config, "defense", "CastlePalaceDoorY", "370"));

                // 设置地图事件标志
                var castleMap = LogicMapMgr.Instance.GetLogicMapById(_castleMapId);
                if (castleMap != null)
                {
                    castleMap.SetMapEventFlagRect((int)_homeX, (int)_homeY, (int)_warRangeX, (int)_warRangeY, EventFlag.NoDamage, true);
                }

                var palaceMap = LogicMapMgr.Instance.GetLogicMapById(_palaceMapId);
                if (palaceMap != null)
                {
                    palaceMap.SetMapEventFlagRect(10, 10, 100, 100, EventFlag.NoDamage, true);
                    _palaceMap = palaceMap;
                    palaceMap.SetFlag(1, true); // SABUKPALACE标志
                }

                // 初始化城墙和城门
                uint mapId = _castleMapId;
                uint x, y;

                x = uint.Parse(GetConfigValue(config, "defense", "CenterWallX", "164"));
                y = uint.Parse(GetConfigValue(config, "defense", "CenterWallY", "372")); // 0x174 = 372
                _centerWall.Init(GetConfigValue(config, "defense", "CenterWallName", "CenterWall"), mapId, x, y, uint.Parse(GetConfigValue(config, "defense", "CenterWallHp", "0")));

                x = uint.Parse(GetConfigValue(config, "defense", "LeftWallX", "158")); // 0x9e = 158
                y = uint.Parse(GetConfigValue(config, "defense", "LeftWallY", "371")); // 0x173 = 371
                _leftWall.Init(GetConfigValue(config, "defense", "LeftWallName", "LeftWall"), mapId, x, y, uint.Parse(GetConfigValue(config, "defense", "LeftWallHp", "0")));

                x = uint.Parse(GetConfigValue(config, "defense", "RightWallX", "170")); // 0xaa = 170
                y = uint.Parse(GetConfigValue(config, "defense", "RightWallY", "366")); // 0x16e = 366
                _rightWall.Init(GetConfigValue(config, "defense", "RightWallName", "RightWall"), mapId, x, y, uint.Parse(GetConfigValue(config, "defense", "RightWallHp", "0")));

                x = uint.Parse(GetConfigValue(config, "defense", "MainDoorX", "206")); // 0xce = 206
                y = uint.Parse(GetConfigValue(config, "defense", "MainDoorY", "416")); // 0x1a0 = 416
                _mainGate.Init(GetConfigValue(config, "defense", "MainDoorName", "MainDoor"), mapId, x, y, uint.Parse(GetConfigValue(config, "defense", "MainDoorHp", "0")), GetConfigValue(config, "defense", "MainDoorOpen", "0") == "1");

                // 初始化弓箭手
                int archerCount = 0;
                for (int i = 0; i < 12; i++)
                {
                    string xName = $"Archer_{i + 1}_X";
                    string yName = $"Archer_{i + 1}_Y";
                    string nameName = $"Archer_{i + 1}_Name";
                    string hpName = $"Archer_{i + 1}_HP";

                    string xStr = GetConfigValue(config, "defense", xName, "-1");
                    string yStr = GetConfigValue(config, "defense", yName, "-1");
                    string archerName = GetConfigValue(config, "defense", nameName, "");

                    if (!uint.TryParse(xStr, out x) || !uint.TryParse(yStr, out y) || string.IsNullOrEmpty(archerName))
                        continue;

                    _archers[archerCount] = new CSCArcher();
                    if (_archers[archerCount].Init(archerName, mapId, x, y, uint.Parse(GetConfigValue(config, "defense", hpName, "0"))))
                    {
                        archerCount++;
                    }
                    else
                    {
                        _archers[archerCount] = null;
                    }
                }

                // 设置无敌状态
                _centerWall.SetSystemFlag(SystemFlag.NoDamage, true);
                _leftWall.SetSystemFlag(SystemFlag.NoDamage, true);
                _rightWall.SetSystemFlag(SystemFlag.NoDamage, true);
                _mainGate.SetSystemFlag(SystemFlag.NoDamage, true);

                // 初始化皇宫入口门点
                x = uint.Parse(GetConfigValue(config, "defense", "CastlePalaceDoorX", "416")); // 0x1a0 = 416
                y = uint.Parse(GetConfigValue(config, "defense", "CastlePalaceDoorY", "416")); // 0x1a0 = 416
                uint palaceMapId = uint.Parse(GetConfigValue(config, "defense", "PalaceMap", "123"));
                uint palaceDoorX = uint.Parse(GetConfigValue(config, "defense", "PalaceDoorX", "20"));
                uint palaceDoorY = uint.Parse(GetConfigValue(config, "defense", "PalaceDoorY", "20"));

                if (!_palaceDoor.Create(mapId, x, y, palaceMapId, palaceDoorX, palaceDoorY))
                {
                    LogManager.Default.Warning("沙城皇宫入口门点创建失败！");
                }

                // 初始化沙城霸主
                string masterFigure = GetConfigValue(config, "setup", "masterfigure", "");
                if (!string.IsNullOrEmpty(masterFigure))
                {
                    // 创建NPC
                    _sabukMaster = new Npc(0, masterFigure, NpcType.Normal);
                    
                    string topSabukMaster = GetConfigValue(config, "setup", "topsabukmaster", "");
                    if (!string.IsNullOrEmpty(topSabukMaster))
                    {
                        var topParts = topSabukMaster.Split('/');
                        if (topParts.Length >= 7)
                        {
                            _sabukMasterInfo.Name = topParts[0];
                            _sabukMasterInfo.DBId = uint.Parse(topParts[1]);
                            _sabukMasterInfo.Class = byte.Parse(topParts[2]);
                            _sabukMasterInfo.Sex = byte.Parse(topParts[3]);
                            _sabukMasterInfo.Level = ushort.Parse(topParts[4]);
                            _sabukMasterInfo.Exp = uint.Parse(topParts[5]);
                            _sabukMasterInfo.RankingTime = ParseDateTime(topParts[6]);

                            if (!string.IsNullOrEmpty(_sabukMasterInfo.Name) && _ownerGuild != null)
                            {
                                string longName = $"{_sabukMaster.GetName()}\\{_ownerGuild.GetName()}\\{_sabukMasterInfo.Name}\\";
                                _sabukMaster.SetLongName(longName);
                                _sabukMaster.SendChangeName();

                int index = _sabukMasterInfo.Class * 2 + _sabukMasterInfo.Sex;
                // _sabukMaster.SetView(TopManager.Instance.GetTopView(index));
                                _sabukMaster.SendFeatureChanged();
                            }
                        }
                    }
                }

                // 加载攻城请求
                LoadAttackRequest();

                // 注册时间事件
                TimeSystem.Instance.RegisterTimeEvent(this);

                LogManager.Default.Info($"沙城系统初始化完成: {_name}");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error("沙城系统初始化失败", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 开始攻城战
        /// </summary>
        public bool StartWar()
        {
            if (_warGuildCount <= 0)
                PrepareAttackGuild(DateTime.Now);

            if (_warGuildCount <= 0)
            {
                LogManager.Default.Warning("没有攻城行会!");
                return false;
            }

            // 移除城墙和城门的无敌状态
            _centerWall.SetSystemFlag(SystemFlag.NoDamage, false);
            _leftWall.SetSystemFlag(SystemFlag.NoDamage, false);
            _rightWall.SetSystemFlag(SystemFlag.NoDamage, false);
            _mainGate.SetSystemFlag(SystemFlag.NoDamage, false);

            // 发送系统公告
            string warText = _ownerGuild != null 
                ? $"攻城战开始，守城方为 {_ownerGuild.GetName()}！" 
                : "攻城战开始，没有守城方！";
            GameWorld.Instance.PostSystemMessage(warText);

            _warStarted = true;
            _identifyStart = false;

            // 设置攻城行会状态
            for (int i = 0; i < _warGuildCount; i++)
            {
                if (_warGuilds[i] != null)
                {
                    _warGuilds[i].SetAttackSabuk(true);
                    string guildWarText = $"{_warGuilds[i].GetName()} 的攻城战斗开始！";
                    GameWorld.Instance.PostSystemMessage(guildWarText);
                }
            }

            // 隐藏沙城NPC
            GameWorld.Instance.HideSandCityNpc();
            GameWorld.Instance.AddGlobeProcess(new GlobeProcess(GlobeProcessType.None));
            _warTimer.SaveTime();

            LogManager.Default.Info($"攻城战开始，攻城行会数量: {_warGuildCount}");
            return true;
        }

        /// <summary>
        /// 结束攻城战
        /// </summary>
        public bool EndWar()
        {
            // 恢复城墙和城门的无敌状态
            _centerWall.SetSystemFlag(SystemFlag.NoDamage, true);
            _leftWall.SetSystemFlag(SystemFlag.NoDamage, true);
            _rightWall.SetSystemFlag(SystemFlag.NoDamage, true);
            _mainGate.SetSystemFlag(SystemFlag.NoDamage, true);

            GameWorld.Instance.PostSystemMessage("攻城战结束！");
            _warStarted = false;
            _identifyStart = false;

            // 清除攻城行会状态
            for (int i = 0; i < _warGuildCount; i++)
            {
                if (_warGuilds[i] != null)
                {
                    _warGuilds[i].SetAttackSabuk(false);
                }
            }
            _warGuildCount = 0;

            // 显示沙城NPC
            GameWorld.Instance.ShowSandCityNpc();
            GameWorld.Instance.AddGlobeProcess(new GlobeProcess(GlobeProcessType.None));

            LogManager.Default.Info("攻城战结束");
            return true;
        }

        /// <summary>
        /// 更新攻城战状态
        /// </summary>
        public void UpdateWar()
        {
            if (!_warStarted)
                return;

            if (_identifyStart)
            {
                uint sandCityTakeTime = (uint)GameWorld.Instance.GetGameVar(GameVarConstants.SandCityTakeTime);
            if (_identifyTimer.IsTimeOut((uint)(sandCityTakeTime * 1000)))
                {
                    // 检测是否要结束战争
                    if (IdentifyEnd())
                    {
                        // 夺城成功
                        StopIdentify();
                        return;
                    }
                    else
                    {
                        StopIdentify();
                    }
                }
            }

            uint warTimeLong = (uint)GameWorld.Instance.GetGameVar(GameVarConstants.WarTimeLong);
            if (_warTimer.IsTimeOut((uint)(warTimeLong * 60000)))
            {
                EndWar();
            }
        }

        /// <summary>
        /// 玩家进入皇宫
        /// </summary>
        public void OnEnterPalace(HumanPlayer player)
        {
            // 搜索皇宫地图
            GuildEx guild = player.GetGuild();
            
            // 进来的人是没有行会的，或者是沙城的人
            if (guild == null || guild == _ownerGuild)
            {
                StopIdentify();
                return;
            }

            // 如果没有开始验证
            if (!_identifyStart)
            {
                // 并且进来的不是行会老大，就不测试是否开始验证
                if (!guild.IsMaster(player))
                {
                    return;
                }
            }
            
            ProcIdentify();
        }

        /// <summary>
        /// 玩家离开皇宫
        /// </summary>
        public void OnLeavePalace(HumanPlayer player)
        {
            GuildEx guild = player.GetGuild();
            if (_identifyStart)
            {
                // 如果开始验证，并且离开的人不是老大，就不需要再次验证
                if (guild != null && !guild.IsMaster(player))
                    return;
            }
            ProcIdentify();
        }

        /// <summary>
        /// 处理身份验证
        /// </summary>
        private void ProcIdentify()
        {
            GuildEx guild = null;
            bool identStart = false;
            
            if (_palaceMap != null)
            {
                var list = _palaceMap.GetObjList();
                foreach (var obj in list)
                {
                if (obj is HumanPlayer)
                    {
                        HumanPlayer player = (HumanPlayer)obj;
                        if (player.IsDeath())
                            continue;
                            
                        GuildEx tGuild = player.GetGuild();
                        // 有没有门派的人在
                        if (tGuild == null)
                        {
                            StopIdentify();
                            return;
                        }
                        // 有沙城成员在
                        if (tGuild == _ownerGuild)
                        {
                            StopIdentify();
                            return;
                        }
                        if (guild == null)
                            guild = tGuild;
                        else if (guild != tGuild)
                        {
                            StopIdentify();
                            return;
                        }
                        // 如果不是攻城者，就停止认证
                        if (!guild.IsAttackSabuk())
                        {
                            StopIdentify();
                            return;
                        }
                        // 接下来，从里面找会长
                        if (guild.IsMaster(player))
                            identStart = true;
                    }
                }
                if (identStart)
                    StartIdentify();
            }
        }

        /// <summary>
        /// 开始身份验证
        /// </summary>
        private void StartIdentify()
        {
            if (_identifyStart)
                return;
            _identifyStart = true;
            _identifyTimer.SaveTime();
        }

        /// <summary>
        /// 停止身份验证
        /// </summary>
        private void StopIdentify()
        {
            _identifyStart = false;
        }

        /// <summary>
        /// 身份验证结束
        /// </summary>
        private bool IdentifyEnd()
        {
            if (_palaceMap == null)
                return false;
                
            bool identSucc = false;
            var list = _palaceMap.GetObjList();
            GuildEx guild = null;

            foreach (var obj in list)
            {
                if (obj is HumanPlayer)
                {
                    HumanPlayer player = (HumanPlayer)obj;
                    if (player.IsDeath())
                        continue;
                        
                    GuildEx tGuild = player.GetGuild();
                    if (tGuild == null)
                    {
                        StopIdentify();
                        return false;
                    }
                    if (tGuild == _ownerGuild)
                    {
                        StopIdentify();
                        return false;
                    }
                    if (guild == null)
                        guild = tGuild;
                    else if (guild != tGuild)
                    {
                        StopIdentify();
                        return false;
                    }
                    // 接下来，从里面找会长
                    if (guild.IsMaster(player))
                        identSucc = true;
                }
            }
            
            if (guild != null && identSucc)
            {
                // 换主人
                ChangeOwner(guild);
            }
            
            // 夺城成功
            return identSucc;
        }

        /// <summary>
        /// 更换沙城所有者
        /// </summary>
        public void ChangeOwner(GuildEx newOwner)
        {
            GuildEx oldOwner = _ownerGuild;
            _ownerGuild = newOwner;
            
            if (oldOwner != null)
            {
                oldOwner.SetAttackSabuk(true);
                oldOwner.RefreshMemberName();
            }
            
            if (newOwner != null)
            {
                string text = $"沙城被 {newOwner.GetName()} 行会取得！";
                GameWorld.Instance.PostSystemMessage(text);
                newOwner.SetAttackSabuk(false);
                newOwner.RefreshMemberName();
            }
            
            _changeTime = DateTime.Now;
            _sabukMasterInfo = new TopCharInfo();
            UpdateSabukMasterFigure();
            Save();
        }

        /// <summary>
        /// 打开城门
        /// </summary>
        public void OpenGate()
        {
            _mainGate.Open();
        }

        /// <summary>
        /// 关闭城门
        /// </summary>
        public void CloseGate()
        {
            _mainGate.Close();
        }

        /// <summary>
        /// 修复城门
        /// </summary>
        public void RepairGate()
        {
            _mainGate.Repair();
        }

        /// <summary>
        /// 修复城墙
        /// </summary>
        public void RepairWall(int index)
        {
            switch (index)
            {
                case 1:
                    _leftWall.Repair();
                    break;
                case 2:
                    _centerWall.Repair();
                    break;
                case 3:
                    _rightWall.Repair();
                    break;
            }
        }

        /// <summary>
        /// 保存沙城数据
        /// </summary>
        public void Save()
        {
            try
            {
                string filePath = ".\\data\\GuildBase\\SabukW.txt";
                using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.GetEncoding("GBK")))
                {
                    writer.WriteLine("[Setup]");
                    writer.WriteLine($"CastleName={_name}");
                    writer.WriteLine($"OwnGuild={(_ownerGuild != null ? _ownerGuild.GetName() : "")}");
                    writer.WriteLine($"ChangeDate=\"{_changeTime:yyyy-M-d H:mm:ss}\"");
                    writer.WriteLine($"WarDate=\"{_warTime:yyyy-M-d H:mm:ss}\"");
                    writer.WriteLine($"IncomeToday=\"{_incomeTime:yyyy-M-d H:mm:ss}\"");
                    writer.WriteLine($"TotalGold={_totalGold}");
                    writer.WriteLine($"TodayIncome={_todayIncome}");
                    writer.WriteLine($"TexRate={_texRatePercent}");
                    writer.WriteLine($"Rebate={_rebate}");
                    writer.WriteLine();

                    if (_sabukMaster != null)
                    {
                        writer.WriteLine($"masterfigure={_sabukMaster.GetName()}/1/{_sabukMaster.GetView()}/{_sabukMaster.GetMapId()}/{_sabukMaster.GetX()}/{_sabukMaster.GetY()}/1/沙城霸主");
                        
                        if (!string.IsNullOrEmpty(_sabukMasterInfo.Name))
                        {
                            writer.WriteLine($"topsabukmaster={_sabukMasterInfo.Name}/{_sabukMasterInfo.DBId}/{_sabukMasterInfo.Class}/{_sabukMasterInfo.Sex}/{_sabukMasterInfo.Level}/{_sabukMasterInfo.Exp}/{_sabukMasterInfo.RankingTime:yyyy-MM-dd}");
                        }
                    }

                    writer.WriteLine("[Defense]");
                    writer.WriteLine($"CastleMap={_castleMapId}");
                    writer.WriteLine($"CastleHomeMap={_homeMapId}");
                    writer.WriteLine($"CastleHomeX={_homeX}");
                    writer.WriteLine($"CastleHomeY={_homeY}");
                    writer.WriteLine($"CastleWarRangeX={_warRangeX}");
                    writer.WriteLine($"CastleWarRangeY={_warRangeY}");
                    writer.WriteLine();
                    
                    writer.WriteLine($"PalaceMap={_palaceMapId}");
                    writer.WriteLine($"PalaceDoorX={_palaceDoorX}");
                    writer.WriteLine($"PalaceDoorY={_palaceDoorY}");
                    writer.WriteLine($"CastlePalaceDoorX={_castlePalaceDoorX}");
                    writer.WriteLine($"CastlePalaceDoorY={_castlePalaceDoorY}");
                    writer.WriteLine();

                        writer.WriteLine($"MainDoorName={(_mainGate.GetDesc() != null ? ((dynamic)_mainGate.GetDesc()).Base.ClassName : "MainDoor")}");
                    writer.WriteLine($"MainDoorX={_mainGate.GetX()}");
                    writer.WriteLine($"MainDoorY={_mainGate.GetY()}");
                    writer.WriteLine($"MainDoorOpen={(_mainGate.IsOpened() ? 1 : 0)}");
                    writer.WriteLine($"MainDoorHP={_mainGate.GetPropValue(PropIndex.CurHp)}");
                    writer.WriteLine();

                        writer.WriteLine($"LeftWallName={(_leftWall.GetDesc() != null ? ((dynamic)_leftWall.GetDesc()).Base.ClassName : "LeftWall")}");
                    writer.WriteLine($"LeftWallX={_leftWall.GetX()}");
                    writer.WriteLine($"LeftWallY={_leftWall.GetY()}");
                    writer.WriteLine($"LeftWallHP={_leftWall.GetPropValue(PropIndex.CurHp)}");
                    writer.WriteLine();

                        writer.WriteLine($"CenterWallName={(_centerWall.GetDesc() != null ? ((dynamic)_centerWall.GetDesc()).Base.ClassName : "CenterWall")}");
                    writer.WriteLine($"CenterWallX={_centerWall.GetX()}");
                    writer.WriteLine($"CenterWallY={_centerWall.GetY()}");
                    writer.WriteLine($"CenterWallHP={_centerWall.GetPropValue(PropIndex.CurHp)}");
                    writer.WriteLine();

                        writer.WriteLine($"RightWallName={(_rightWall.GetDesc() != null ? ((dynamic)_rightWall.GetDesc()).Base.ClassName : "RightWall")}");
                    writer.WriteLine($"RightWallX={_rightWall.GetX()}");
                    writer.WriteLine($"RightWallY={_rightWall.GetY()}");
                    writer.WriteLine($"RightWallHP={_rightWall.GetPropValue(PropIndex.CurHp)}");
                    writer.WriteLine();

                    for (int i = 0; i < 12; i++)
                    {
                        if (_archers[i] == null)
                            continue;

                        writer.WriteLine($"Archer_{i + 1}_Name={(_archers[i].GetDesc() != null ? ((dynamic)_archers[i].GetDesc()).Base.ClassName : "Archer")}");
                        writer.WriteLine($"Archer_{i + 1}_X={_archers[i].GetX()}");
                        writer.WriteLine($"Archer_{i + 1}_Y={_archers[i].GetY()}");
                        writer.WriteLine($"Archer_{i + 1}_HP={_archers[i].GetPropValue(PropIndex.CurHp)}");
                        writer.WriteLine();
                    }
                }

                LogManager.Default.Info($"沙城数据保存成功: {filePath}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"沙城数据保存失败", exception: ex);
            }
        }

        /// <summary>
        /// 传送玩家到沙城回城点
        /// </summary>
        public void Home(HumanPlayer player)
        {
            if (player != null)
                player.FlyTo(_homeMapId, _homeX, _homeY);
        }

        /// <summary>
        /// 添加攻城请求
        /// </summary>
        public bool AddAttackRequest(GuildEx guild, bool today = false)
        {
            if (_attackRequestCount >= MAX_ATTACKREQUEST)
            {
                LogManager.Default.Warning("达到最大的攻城申请数目，无法继续申请！");
                return false;
            }
            
            if (guild == null)
            {
                LogManager.Default.Warning("您还没有加入行会！");
                return false;
            }

            if (guild == _ownerGuild)
            {
                LogManager.Default.Warning("不能攻击自己的城堡！");
                return false;
            }

            string guildName = guild.GetName();
            for (int i = 0; i < _attackRequestCount; i++)
            {
                if (_attackRequests[i].Guild == guild)
                {
                    LogManager.Default.Warning("您已经申请过攻城战了！");
                    return false;
                }
            }

            _attackRequests[_attackRequestCount].GuildName = guildName;
            _attackRequests[_attackRequestCount].AttackTime = DateTime.Now;
            if (!today)
                _attackRequests[_attackRequestCount].AttackTime = _attackRequests[_attackRequestCount].AttackTime.AddDays(2);
            
            // 调整到20点
            _attackRequests[_attackRequestCount].AttackTime = new DateTime(
                _attackRequests[_attackRequestCount].AttackTime.Year,
                _attackRequests[_attackRequestCount].AttackTime.Month,
                _attackRequests[_attackRequestCount].AttackTime.Day,
                20, 0, 0);
                
            _attackRequests[_attackRequestCount].Guild = guild;
            _attackRequestCount++;
            
            SaveAttackRequest();
            return true;
        }

        /// <summary>
        /// 准备攻城行会
        /// </summary>
        private void PrepareAttackGuild(DateTime currentTime)
        {
            AttackSabukRequest[] tempRequests = new AttackSabukRequest[MAX_ATTACKREQUEST];
            int count = 0;
            _warGuildCount = 0;

            for (int i = 0; i < _attackRequestCount; i++)
            {
                if (currentTime.Year == _attackRequests[i].AttackTime.Year &&
                    currentTime.Month == _attackRequests[i].AttackTime.Month &&
                    currentTime.Day == _attackRequests[i].AttackTime.Day)
                {
                    _warGuilds[_warGuildCount++] = _attackRequests[i].Guild;
                }
                else
                {
                    tempRequests[count++] = _attackRequests[i];
                }
            }

            if (count > 0)
            {
                Array.Copy(tempRequests, _attackRequests, count);
            }
            _attackRequestCount = count;
            
            SaveAttackRequest();
        }

        /// <summary>
        /// 小时变化事件
        /// </summary>
        public void OnHourChange(DateTime currentTime)
        {
            float warStartTime = GameWorld.Instance.GetGameVar(GameVarConstants.WarStartTime);
            if (currentTime.Hour == (int)warStartTime) // 晚上20点
            {
                if (_warStarted)
                    return;

                // 找出所有的今天的攻城申请
                PrepareAttackGuild(currentTime);

                if (_warGuildCount > 0)
                {
                    StartWar();
                }
            }
        }

        /// <summary>
        /// 分钟变化事件
        /// </summary>
        public void OnMinuteChange(DateTime currentTime) { }

        /// <summary>
        /// 天变化事件
        /// </summary>
        public void OnDayChange(DateTime currentTime) { }

        /// <summary>
        /// 月变化事件
        /// </summary>
        public void OnMonthChange(DateTime currentTime) { }

        /// <summary>
        /// 年变化事件
        /// </summary>
        public void OnYearChange(DateTime currentTime) { }

        /// <summary>
        /// 保存攻城请求
        /// </summary>
        private void SaveAttackRequest()
        {
            try
            {
                string filePath = ".\\data\\GuildBase\\Attackreq.txt";
                using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.GetEncoding("GBK")))
                {
                    for (int i = 0; i < _attackRequestCount; i++)
                    {
                        writer.WriteLine($"{_attackRequests[i].Guild.GetName()}|\"{_attackRequests[i].AttackTime:yyyy-M-d}\"");
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error("无法存储攻城请求！", exception: ex);
            }
        }

        /// <summary>
        /// 加载攻城请求
        /// </summary>
        private void LoadAttackRequest()
        {
            try
            {
                string filePath = ".\\data\\GuildBase\\Attackreq.txt";
                if (!File.Exists(filePath))
                    return;

                string[] lines = SmartReader.ReadAllLines(filePath);
                _attackRequestCount = 0;

                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    string[] parts = line.Split('|');
                    if (parts.Length != 2)
                        continue;

                    string guildName = parts[0].Trim();
                    string timeStr = parts[1].Trim().Trim('"');
                    
                    GuildEx guild = GuildManagerEx.GetGuildByName(guildName);
                    if (guild != null)
                    {
                        _attackRequests[_attackRequestCount].GuildName = guildName;
                        if (string.IsNullOrEmpty(timeStr))
                        {
                            _attackRequests[_attackRequestCount].AttackTime = DateTime.Now;
                        }
                        else
                        {
                            if (DateTime.TryParse(timeStr, out DateTime attackTime))
                            {
                                _attackRequests[_attackRequestCount].AttackTime = attackTime;
                            }
                            else
                            {
                                _attackRequests[_attackRequestCount].AttackTime = DateTime.Now;
                            }
                        }
                        _attackRequests[_attackRequestCount].Guild = guild;
                        _attackRequestCount++;
                    }
                }

                LogManager.Default.Info($"加载攻城请求: {_attackRequestCount} 个请求");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error("加载攻城请求失败", exception: ex);
            }
        }

        /// <summary>
        /// 准备攻城请求页面
        /// </summary>
        public int PrepareAttackRequestPage(uint page, char[] buffer)
        {
            // 每个页面显示
            if (_attackRequestCount <= 0)
            {
                string text = "没有攻城请求\\\\\\<知道了./@exit>\\";
                text.CopyTo(0, buffer, 0, text.Length);
                return text.Length;
            }

            uint lastPage = (uint)((_attackRequestCount + PAGEREQUESTCOUNT - 1) / PAGEREQUESTCOUNT);
            if (page > lastPage)
                page = lastPage;

            int start = (int)page * PAGEREQUESTCOUNT;
            int end = (int)page * PAGEREQUESTCOUNT + PAGEREQUESTCOUNT;
            end = end > _attackRequestCount ? _attackRequestCount : end;

            string pageText = "";
            for (int i = start; i < end; i++)
            {
                pageText += $"{_attackRequests[i].AttackTime:yyyy年-MM月-dd日}\"{_attackRequests[i].GuildName}\"\\";
            }

            if (page != 0)
            {
                pageText += $"<上一页/@AttackRequestPage{page - 1}>  ";
            }

            if (end != _attackRequestCount)
            {
                pageText += $"<下一页/@AttackRequestPage{page + 1}>  ";
            }

            pageText += "\\<知道了./@exit>\\";
            
            if (pageText.Length <= buffer.Length)
            {
                pageText.CopyTo(0, buffer, 0, pageText.Length);
                return pageText.Length;
            }
            
            return 0;
        }

        /// <summary>
        /// 设置沙城霸主
        /// </summary>
        public bool SetSabukMaster(HumanPlayer player)
        {
            if (_ownerGuild == null)
                return false;
            if (_sabukMaster == null)
                return false;
            if (!_ownerGuild.IsFirstMaster(player))
                return false;

                _sabukMasterInfo.Class = (byte)player.GetPro();
                _sabukMasterInfo.Sex = (byte)player.GetSex();
            _sabukMasterInfo.DBId = player.GetDBId();
            _sabukMasterInfo.Exp = player.GetPropValue(PropIndex.Exp);
            _sabukMasterInfo.RankingTime = DateTime.Now;
                _sabukMasterInfo.Level = (ushort)player.GetPropValue(PropIndex.Level);
            _sabukMasterInfo.Name = player.GetName();

            if (!string.IsNullOrEmpty(_sabukMasterInfo.Name))
            {
                string longName = $"{_sabukMaster.GetName()}\\{_ownerGuild.GetName()}\\{_sabukMasterInfo.Name}\\";
                _sabukMaster.SetLongName(longName);
                _sabukMaster.SendChangeName();
                
                int index = _sabukMasterInfo.Class * 2 + _sabukMasterInfo.Sex;
                // _sabukMaster.SetView(TopManager.Instance.GetTopView(index));
                _sabukMaster.SendFeatureChanged();
                
                Save();
            }

            return true;
        }

        /// <summary>
        /// 更新沙城霸主形象
        /// </summary>
        public void UpdateSabukMasterFigure()
        {
            if (_sabukMaster == null)
                return;

            if (!string.IsNullOrEmpty(_sabukMasterInfo.Name))
            {
                string longName = $"{_sabukMaster.GetName()}\\{_ownerGuild.GetName()}\\{_sabukMasterInfo.Name}\\";
                _sabukMaster.SetLongName(longName);
                _sabukMaster.SendChangeName();
                
                int index = _sabukMasterInfo.Class * 2 + _sabukMasterInfo.Sex;
                // _sabukMaster.SetView(TopManager.Instance.GetTopView(index));
                _sabukMaster.SendFeatureChanged();
            }
            else
            {
                _sabukMaster.SetLongName(_sabukMaster.GetName());
                _sabukMaster.SendChangeName();
            }
        }

        /// <summary>
        /// 增加收入
        /// </summary>
        public bool AddIncoming(uint incoming)
        {
            incoming = (uint)(incoming * _texRate);
            if (!AddTotalGold(incoming))
                return false;
                
            _todayIncome += incoming;
            
            DateTime now = DateTime.Now;
            if (now.Day != _incomeTime.Day)
                _todayIncome = 0;
                
            _incomeTime = now;
            return true;
        }

        /// <summary>
        /// 增加总资金
        /// </summary>
        public bool AddTotalGold(uint addGold)
        {
            if (uint.MaxValue - _totalGold < addGold)
                return false;
                
            _totalGold += addGold;
            return true;
        }

        /// <summary>
        /// 减少总资金
        /// </summary>
        public bool DecTotalGold(uint decGold)
        {
            if (_totalGold < decGold)
                return false;
                
            _totalGold -= decGold;
            return true;
        }

        /// <summary>
        /// 解析日期时间字符串
        /// </summary>
        private DateTime ParseDateTime(string dateTimeStr)
        {
            if (DateTime.TryParse(dateTimeStr, out DateTime result))
                return result;
            return DateTime.Now;
        }

        /// <summary>
        /// 获取配置值
        /// </summary>
        private string GetConfigValue(Dictionary<string, Dictionary<string, string>> config, string section, string key, string defaultValue)
        {
            if (config.TryGetValue(section, out var sectionDict) && sectionDict.TryGetValue(key, out var value))
            {
                return value;
            }
            return defaultValue;
        }
    }
}
