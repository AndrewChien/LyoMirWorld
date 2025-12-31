using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using MirCommon;
using MirCommon.Utils;

namespace GameServer
{
    /// <summary>
    /// 配置文件加载管理器 - 负责加载所有游戏配置文件
    /// </summary>
    public class ConfigLoader
    {
        private static ConfigLoader? _instance;
        public static ConfigLoader Instance => _instance ??= new ConfigLoader();

        // 配置文件路径常量
        private const string DATA_PATH = "./data";
        private const string MAPS_PATH = "./data/maps";
        private const string SCRIPT_PATH = "./data/script";
        private const string MARKET_PATH = "./data/Market";
        private const string GAMEMASTER_PATH = "./data/GameMaster";
        private const string GUILD_PATH = "./data/guildbase/guilds";
        private const string FIGURE_PATH = "./data/figure";
        private const string VARIABLES_PATH = "./data/Variables";
        private const string STRINGLIST_PATH = "./data/stringlist";
        private const string MONITEMS_PATH = "./data/MonItems";
        private const string MONGENS_PATH = "./data/MonGens";
        private const string TASK_PATH = "./data/task";

        // 内存缓存数据结构
        private readonly Dictionary<int, Dictionary<int, HumanDataDesc>> _humanDataDescs = new();
        private readonly Dictionary<int, StartPoint> _startPoints = new();
        private readonly Dictionary<string, int> _startPointNameToIndex = new();
        private readonly List<FirstLoginInfo> _firstLoginInfos = new();
        private readonly Dictionary<string, string> _gameNames = new();
        private readonly Dictionary<int, float> _gameVars = new();
        private readonly Dictionary<int, int> _channelWaitTimes = new();
        
        // 配置解析器实例
        private readonly Parsers.ItemDataParser _itemDataParser = new();
        private readonly Parsers.MagicDataParser _magicDataParser = new();
        private readonly Parsers.NpcConfigParser _npcConfigParser = new();
        private readonly Parsers.MonsterDataParser _monsterDataParser = new();

        // 管理器实例
        private readonly MarketManager _marketManager = MarketManager.Instance;
        private readonly AutoScriptManager _autoScriptManager = AutoScriptManager.Instance;
        private readonly TitleManager _titleManager = TitleManager.Instance;
        private readonly TopManager _topManager = TopManager.Instance;
        private readonly TaskManager _taskManager = TaskManager.Instance;
        
        // 新实现的管理器实例
        private readonly PhysicsMapMgr _physicsMapMgr = PhysicsMapMgr.Instance;
        private readonly MagicManager _magicManager = MagicManager.Instance;
        private readonly NpcManagerEx _npcManagerEx = NpcManagerEx.Instance;
        private readonly MonsterManagerEx _monsterManagerEx = MonsterManagerEx.Instance;
        private readonly ScriptObjectMgr _scriptObjectMgr = ScriptObjectMgr.Instance;
        private readonly MonsterGenManager _monsterGenManager = MonsterGenManager.Instance;
        private readonly MonItemsMgr _monItemsMgr = MonItemsMgr.Instance;
        private readonly SpecialEquipmentManager _specialEquipmentManager = SpecialEquipmentManager.Instance;
        
        // 公告数据
        private string _notice = string.Empty;
        private readonly List<string> _lineNotices = new();

        private ConfigLoader() { }

        /// <summary>
        /// 加载所有配置文件（同步版本，保持向后兼容）
        /// </summary>
        public bool LoadAllConfigs()
        {
            return LoadAllConfigsAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// 加载所有配置文件
        /// </summary>
        public async Task<bool> LoadAllConfigsAsync()
        {
            try
            {
                LogManager.Default.Info("开始加载游戏配置文件（异步）...");

                // 1. 脚本系统首先读取
                await Task.Run(() => LoadScriptSystemOptimized());

                // 2. 加载服务器基础配置
                if (!LoadServerConfig())
                {
                    LogManager.Default.Error("加载服务器配置失败");
                    return false;
                }

                // 3. 地图系统
                await Task.Run(() => LoadMapSystem());

                // 4. 和地图密切相关的东西（安全区、出生点、公告）
                await Task.Run(() => LoadMapRelatedConfigs());

                // 5. 物品系统
                await Task.Run(() => LoadItemSystem());

                // 6. 技能/魔法配置
                await Task.Run(() => LoadMagicSystem());

                // 7. 称号配置
                await Task.Run(() => LoadTitles());

                // 8. 捆绑物品配置
                await Task.Run(() => LoadBundleItem());

                // 9. NPC配置（必须等待ScriptObjectMgr.Load()加载完成后才能加载）
                await Task.Run(() => LoadNpcConfigs());

                // 10. GM配置
                await Task.Run(() => LoadGMConfigs());

                // 11. 行会配置
                await Task.Run(() => LoadGuildConfigs());

                // 12. 怪物掉落配置
                await Task.Run(() => LoadMonsterItems());

                // 13. 怪物配置
                await Task.Run(() => LoadMonsterConfigs());

                // 14. 怪物生成配置
                await Task.Run(() => LoadMonsterGen());

                // 15. 初始化玩家管理器
                await Task.Run(() => LoadHumanPlayerMgr());

                // 16. 初始化怪物生成点
                await Task.Run(() => _monsterGenManager.InitAllGen());

                // 17. 特殊协议初始化
                InitSpecialProtocol();

                // 18. 沙城初始化
                await LoadSandCityAsync();

                // 19. 排行榜系统
                await Task.Run(() => LoadTopList());

                // 20. 特殊装备配置
                await Task.Run(() => LoadSpecialItem());

                // 21. 矿石列表（需要地图已加载）
                await Task.Run(() => LoadMineList());

                // 22. 市场系统
                await Task.Run(() => LoadMarket());

                // 23. 自动脚本
                await Task.Run(() => LoadAutoScript());

                // 24. 地图脚本
                await Task.Run(() => LoadMapScript());

                // 25. 任务系统
                await Task.Run(() => LoadTasks());

                // 26. 加载人物数据描述（在LoadServerConfig中已加载，这里确保正确性）
                await Task.Run(() => EnsureHumanDataDescsLoaded());

                // 27. 加载出生点配置（在LoadMapRelatedConfigs中已加载，这里确保正确性）
                await Task.Run(() => EnsureStartPointsLoaded());

                // 28. 加载首次登录信息（在LoadServerConfig中已加载，这里确保正确性）
                await Task.Run(() => EnsureFirstLoginInfoLoaded());

                LogManager.Default.Info("所有配置文件加载完成");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error("加载配置文件时发生错误", exception: ex);
                return false;
            }
        }

        // ========== 异步方法实现 ==========

        /// <summary>
        /// 加载地图相关配置（安全区、出生点、公告）
        /// </summary>
        private void LoadMapRelatedConfigs()
        {
            LoadSafeArea();
            LoadStartPoint();
            LoadNotice();
        }

        /// <summary>
        /// 加载服务器基础配置
        /// </summary>
        private bool LoadServerConfig()
        {
            LogManager.Default.Info("加载服务器配置...");
            
            try
            {
                string serverConfigFile = Path.Combine(DATA_PATH, "server.txt");
                if (!File.Exists(serverConfigFile))
                {
                    LogManager.Default.Error($"服务器配置文件不存在: {serverConfigFile}");
                    return false;
                }

                // 使用IniFile解析配置文件
                var iniFile = new IniFile(serverConfigFile);
                
                // 加载经验因子
                float expFactor = iniFile.GetInteger("setting", "expfactor", 100) / 100.0f;
                GameWorld.Instance.SetExpFactor(expFactor);
                
                // 加载速度配置
                LoadSpeedConfigFromIni(iniFile);
                
                // 加载名称配置
                LoadNameConfigFromIni(iniFile);
                
                // 加载变量配置
                LoadVarConfigFromIni(iniFile);
                
                // 加载聊天等待时间配置
                LoadChatWaitConfigFromIni(iniFile);
                
                // 加载人物数据描述
                LoadHumanDataDescsFromIni(iniFile);
                
                // 加载首次登录信息
                LoadFirstLoginInfoFromIni(iniFile);
                
                LogManager.Default.Info("服务器配置加载完成");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载服务器配置失败", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 从INI文件加载速度配置
        /// </summary>
        private void LoadSpeedConfigFromIni(IniFile iniFile)
        {
            int walkSpeed = iniFile.GetInteger("speed", "walk", 600);
            int runSpeed = iniFile.GetInteger("speed", "run", 300);
            int attackSpeed = iniFile.GetInteger("speed", "attack", 800);
            int beAttackSpeed = iniFile.GetInteger("speed", "beattack", 800);
            int spellSkillSpeed = iniFile.GetInteger("speed", "spellskill", 800);

            SetGameVar(GameVarConstants.WalkSpeed, walkSpeed);
            SetGameVar(GameVarConstants.RunSpeed, runSpeed);
            SetGameVar(GameVarConstants.AttackSpeed, attackSpeed);
            SetGameVar(GameVarConstants.BeAttackSpeed, beAttackSpeed);
            SetGameVar(GameVarConstants.SpellSkillSpeed, spellSkillSpeed);

            LogManager.Default.Debug($"速度配置 - 行走:{walkSpeed} 跑步:{runSpeed} 攻击:{attackSpeed}");
        }

        /// <summary>
        /// 从INI文件加载名称配置
        /// </summary>
        private void LoadNameConfigFromIni(IniFile iniFile)
        {
            string goldName = iniFile.GetString("name", "goldname", "金币");
            string maleName = iniFile.GetString("name", "malename", "男");
            string femaleName = iniFile.GetString("name", "femalename", "女");
            string warrName = iniFile.GetString("name", "warr", "战士");
            string magicanName = iniFile.GetString("name", "magican", "法师");
            string taoshiName = iniFile.GetString("name", "taoshi", "道士");
            string guildNotice = iniFile.GetString("name", "GUILDNOTICE", "公告");
            string killGuilds = iniFile.GetString("name", "KILLGUILDS", "敌对行会");
            string allyGuilds = iniFile.GetString("name", "ALLYGUILDS", "联盟行会");
            string members = iniFile.GetString("name", "MEMBERS", "行会成员");
            string version = iniFile.GetString("name", "version", "1, 8, 8, 8");
            string topOfWorld = iniFile.GetString("name", "topofworld", "天下第一");
            string upgradeMineStone = iniFile.GetString("name", "upgrademinestone", "黑铁矿石");
            string loginScript = iniFile.GetString("name", "loginscript", "system.login");
            string levelupScript = iniFile.GetString("name", "levelupscript", "system.levelup");
            string logoutScript = iniFile.GetString("name", "logoutscript", "system.logout");
            string physicsMapPath = iniFile.GetString("name", "PHYSICSMAPPATH", "./data/maps/physics");
            string physicsCachePath = iniFile.GetString("name", "PHYSICSCACHEPATH", "./data/maps/pm_cache");

            SetGameName(GameName.GoldName, goldName);
            SetGameName(GameName.MaleName, maleName);
            SetGameName(GameName.FemaleName, femaleName);
            SetGameName(GameName.WarrName, warrName);
            SetGameName(GameName.MagicanName, magicanName);
            SetGameName(GameName.TaoshiName, taoshiName);
            SetGameName(GameName.GuildNotice, guildNotice);
            SetGameName(GameName.KillGuilds, killGuilds);
            SetGameName(GameName.AllyGuilds, allyGuilds);
            SetGameName(GameName.Members, members);
            SetGameName(GameName.Version, version);
            SetGameName(GameName.TopOfWorld, topOfWorld);
            SetGameName(GameName.UpgradeMineStone, upgradeMineStone);
            SetGameName(GameName.LoginScript, loginScript);
            SetGameName(GameName.LevelUpScript, levelupScript);
            SetGameName(GameName.LogoutScript, logoutScript);
            SetGameName(GameName.PhysicsMapPath, physicsMapPath);
            SetGameName(GameName.PhysicsCachePath, physicsCachePath);

            LogManager.Default.Debug($"职业名称 - 战士:{warrName} 法师:{magicanName} 道士:{taoshiName}");
        }

        /// <summary>
        /// 从INI文件加载变量配置
        /// </summary>
        private void LoadVarConfigFromIni(IniFile iniFile)
        {
            int maxGold = iniFile.GetInteger("var", "maxgold", 5000000);
            int maxYuanbao = iniFile.GetInteger("var", "maxyuanbao", 2000);
            int maxGroupMember = iniFile.GetInteger("var", "maxgroupmember", 10);
            int redPkPoint = iniFile.GetInteger("var", "redpkpoint", 12);
            int yellowPkPoint = iniFile.GetInteger("var", "yellowpkpoint", 6);
            int storageSize = iniFile.GetInteger("var", "storeagesize", 39);
            int charInfoBackupTime = iniFile.GetInteger("var", "charinfobackuptime", 30);
            int onePkPointTime = iniFile.GetInteger("var", "onepkpointtime", 120);
            int grayNameTime = iniFile.GetInteger("var", "graynametime", 60);
            int oncePkPoint = iniFile.GetInteger("var", "oncepkpoint", 3);
            int pkCurseRate = iniFile.GetInteger("var", "pkcurserate", 50);
            int addFriendLevel = iniFile.GetInteger("var", "ADDFRIENDLEVEL", 7);
            bool enableSafeAreaNotice = iniFile.GetInteger("var", "ENABLESAFEAREANOTICE", 0) != 0;
            int privateShopLevel = iniFile.GetInteger("var", "PRIVATESHOPLEVEL", 20);
            int initDressColor = iniFile.GetInteger("var", "INITDRESSCOLOR", -1);
            int repairDamageDura = iniFile.GetInteger("var", "REPAIRDAMAGEDURA", 1000);
            int dropTargetDistance = iniFile.GetInteger("var", "DROPTARGETDISTANCE", 14);
            int weaponDamageRate = iniFile.GetInteger("var", "WEAPONDAMAGERATE", 15);
            int dressDamageRate = iniFile.GetInteger("var", "DRESSDAMAGERATE", 15);
            int defenceDamageRate = iniFile.GetInteger("var", "DEFENCEDAMAGERATE", 15);
            int jewelryDamageRate = iniFile.GetInteger("var", "JEWELRYDAMAGERATE", 15);
            int randomUpgradeItemRate = iniFile.GetInteger("var", "RANDOMUPGRADEITEMRATE", 16);
            int pushedDelay = iniFile.GetInteger("var", "PUSHEDDELAY", 1200);
            int pushedHitDelay = iniFile.GetInteger("var", "PUSHEDHITDELAY", 1200);
            int dbUpdateDelay = iniFile.GetInteger("var", "DBUPDATEDELAY", 2000);
            int maxUpgradeTimes = iniFile.GetInteger("var", "MAXUPGRADETIMES", 10);
            int rushGridDelay = iniFile.GetInteger("var", "RUSHGRIDDELAY", 400);
            int monGenFactor = iniFile.GetInteger("var", "MONGENFACTOR", 100);
            int hpRecoverPoint = iniFile.GetInteger("var", "HPRECOVERPOINT", 16);
            int hpRecoverTime = iniFile.GetInteger("var", "HPRECOVERTIME", 1000);
            int mpRecoverPoint = iniFile.GetInteger("var", "MPRECOVERPOINT", 16);
            int mpRecoverTime = iniFile.GetInteger("var", "MPRECOVERTIME", 1000);
            int guildWarTime = iniFile.GetInteger("var", "GUILDWARTIME", 3600 * 3);
            int startGuildMemberCount = iniFile.GetInteger("var", "STARTGUILDMEMBERCOUNT", 64);
            int dropTargetTime = iniFile.GetInteger("var", "DROPTARGETTIME", 30);
            int sandCityTakeTime = iniFile.GetInteger("var", "sandcitytaketime", 300);
            int warEnemyColor = iniFile.GetInteger("var", "WARENEMYCOLOR", 243);
            int warAllyColor = iniFile.GetInteger("var", "WARALLYCOLOR", 4);
            int warNormalColor = iniFile.GetInteger("var", "WARNORMALCOLOR", 219);
            int warTimeLong = iniFile.GetInteger("var", "WARTIMELONG", 240);
            int warStartTime = iniFile.GetInteger("var", "warstarttime", 20) % 24;
            int bodyTime = iniFile.GetInteger("var", "bodytime", 60);
            int itemUpdateTime = iniFile.GetInteger("setting", "downitemupdatetime", 300) * 1000;

            SetGameVar(GameVarConstants.MaxGold, maxGold);
            SetGameVar(GameVarConstants.MaxYuanbao, maxYuanbao);
            SetGameVar(GameVarConstants.MaxGroupMember, maxGroupMember);
            SetGameVar(GameVarConstants.RedPkPoint, redPkPoint);
            SetGameVar(GameVarConstants.YellowPkPoint, yellowPkPoint);
            SetGameVar(GameVarConstants.StorageSize, storageSize);
            SetGameVar(GameVarConstants.CharInfoBackupTime, charInfoBackupTime);
            SetGameVar(GameVarConstants.OnePkPointTime, onePkPointTime);
            SetGameVar(GameVarConstants.GrayNameTime, grayNameTime);
            SetGameVar(GameVarConstants.OncePkPoint, oncePkPoint);
            SetGameVar(GameVarConstants.PkCurseRate, pkCurseRate);
            SetGameVar(GameVarConstants.AddFriendLevel, addFriendLevel);
            SetGameVar(GameVarConstants.EnableSafeAreaNotice, enableSafeAreaNotice ? 1 : 0);
            SetGameVar(GameVarConstants.PrivateShopLevel, privateShopLevel);
            SetGameVar(GameVarConstants.InitDressColor, initDressColor);
            SetGameVar(GameVarConstants.RepairDamagedDura, repairDamageDura);
            SetGameVar(GameVarConstants.DropTargetDistance, dropTargetDistance);
            SetGameVar(GameVarConstants.WeaponDamageRate, weaponDamageRate);
            SetGameVar(GameVarConstants.DressDamageRate, dressDamageRate);
            SetGameVar(GameVarConstants.DefenceDamageRate, defenceDamageRate);
            SetGameVar(GameVarConstants.JewelryDamageRate, jewelryDamageRate);
            SetGameVar(GameVarConstants.RandomUpgradeItemRate, randomUpgradeItemRate);
            SetGameVar(GameVarConstants.PushedDelay, pushedDelay);
            SetGameVar(GameVarConstants.PushedHitDelay, pushedHitDelay);
            SetGameVar(GameVarConstants.DBUpdateDelay, dbUpdateDelay);
            SetGameVar(GameVarConstants.MaxUpgradeTimes, maxUpgradeTimes);
            SetGameVar(GameVarConstants.RushGridDelay, rushGridDelay);
            SetGameVar(GameVarConstants.MonGenFactor, monGenFactor);
            SetGameVar(GameVarConstants.HpRecoverPoint, hpRecoverPoint);
            SetGameVar(GameVarConstants.HpRecoverTime, hpRecoverTime);
            SetGameVar(GameVarConstants.MpRecoverPoint, mpRecoverPoint);
            SetGameVar(GameVarConstants.MpRecoverTime, mpRecoverTime);
            SetGameVar(GameVarConstants.GuildWarTime, guildWarTime);
            SetGameVar(GameVarConstants.StartGuildMemberCount, startGuildMemberCount);
            SetGameVar(GameVarConstants.DropTargetTime, dropTargetTime);
            SetGameVar(GameVarConstants.SandCityTakeTime, sandCityTakeTime);
            SetGameVar(GameVarConstants.WarEnemyColor, warEnemyColor);
            SetGameVar(GameVarConstants.WarAllyColor, warAllyColor);
            SetGameVar(GameVarConstants.WarNormalColor, warNormalColor);
            SetGameVar(GameVarConstants.WarTimeLong, warTimeLong);
            SetGameVar(GameVarConstants.WarStartTime, warStartTime);
            SetGameVar(GameVarConstants.BodyTime, bodyTime);
            SetGameVar(GameVarConstants.ItemUpdateTime, itemUpdateTime);

            // 设置大背包标志
            bool useBigBag = iniFile.GetInteger("setting", "enable60slots", 0) != 0;
            GameWorld.Instance.SetUseBigBag(useBigBag);

            LogManager.Default.Debug($"游戏变量 - 最大金币:{maxGold} 最大元宝:{maxYuanbao} 最大组队人数:{maxGroupMember}");
        }

        /// <summary>
        /// 从INI文件加载聊天等待时间配置
        /// </summary>
        private void LoadChatWaitConfigFromIni(IniFile iniFile)
        {
            int normalWait = iniFile.GetInteger("chatwait", "normal", 1);
            int cryWait = iniFile.GetInteger("chatwait", "cry", 10);
            int whisperWait = iniFile.GetInteger("chatwait", "whisper", 2);
            int groupWait = iniFile.GetInteger("chatwait", "group", 2);
            int guildWait = iniFile.GetInteger("chatwait", "guild", 3);
            int coupleWait = iniFile.GetInteger("chatwait", "couple", 1);
            int gmWait = iniFile.GetInteger("chatwait", "gm", 0);
            int friendWait = iniFile.GetInteger("chatwait", "friend", 2);

            SetChannelWaitTime(ChatWaitChannel.Normal, normalWait);
            SetChannelWaitTime(ChatWaitChannel.Cry, cryWait);
            SetChannelWaitTime(ChatWaitChannel.Whisper, whisperWait);
            SetChannelWaitTime(ChatWaitChannel.Group, groupWait);
            SetChannelWaitTime(ChatWaitChannel.Guild, guildWait);
            SetChannelWaitTime(ChatWaitChannel.Couple, coupleWait);
            SetChannelWaitTime(ChatWaitChannel.GM, gmWait);
            SetChannelWaitTime(ChatWaitChannel.Friend, friendWait);

            LogManager.Default.Debug($"聊天等待 - 普通:{normalWait}秒 喊话:{cryWait}秒");
        }

        /// <summary>
        /// 从INI文件加载人物数据描述
        /// </summary>
        private void LoadHumanDataDescsFromIni(IniFile iniFile)
        {
            string warriorFile = iniFile.GetString("humandata", "warrior", null);
            string magicianFile = iniFile.GetString("humandata", "magician", null);
            string taoshiFile = iniFile.GetString("humandata", "taoshi", null);

            if (!string.IsNullOrEmpty(warriorFile))
            {
                string warriorPath = Path.Combine(DATA_PATH, warriorFile);
                if (LoadHumanDataDesc(0, warriorPath))
                {
                    LogManager.Default.Info($"战士人物数据描述已加载: {warriorPath}");
                }
                else
                {
                    LogManager.Default.Warning($"无法加载战士人物数据描述: {warriorPath}");
                }
            }

            if (!string.IsNullOrEmpty(magicianFile))
            {
                string magicianPath = Path.Combine(DATA_PATH, magicianFile);
                if (LoadHumanDataDesc(1, magicianPath))
                {
                    LogManager.Default.Info($"法师人物数据描述已加载: {magicianPath}");
                }
                else
                {
                    LogManager.Default.Warning($"无法加载法师人物数据描述: {magicianPath}");
                }
            }

            if (!string.IsNullOrEmpty(taoshiFile))
            {
                string taoshiPath = Path.Combine(DATA_PATH, taoshiFile);
                if (LoadHumanDataDesc(2, taoshiPath))
                {
                    LogManager.Default.Info($"道士人物数据描述已加载: {taoshiPath}");
                }
                else
                {
                    LogManager.Default.Warning($"无法加载道士人物数据描述: {taoshiPath}");
                }
            }
        }

        /// <summary>
        /// 从INI文件加载首次登录信息
        /// </summary>
        private void LoadFirstLoginInfoFromIni(IniFile iniFile)
        {
            try
            {
                var firstLoginInfo = new FirstLoginInfo();
                
                // 加载首次登录等级
                firstLoginInfo.Level = iniFile.GetInteger("firstlogin", "startlevel", 1);
                
                // 加载首次登录金币
                firstLoginInfo.Gold = (uint)iniFile.GetInteger("firstlogin", "startgold", 0);
                
                // 加载首次登录物品
                string startItem = iniFile.GetString("firstlogin", "startitem", null);
                if (!string.IsNullOrEmpty(startItem))
                {
                    var itemParts = startItem.Split('/');
                    foreach (var itemPart in itemParts)
                    {
                        var itemDetails = itemPart.Split('*');
                        if (itemDetails.Length >= 1)
                        {
                            var firstLoginItem = new FirstLoginItem
                            {
                                ItemName = itemDetails[0].Trim(),
                                Count = itemDetails.Length >= 2 ? int.Parse(itemDetails[1].Trim()) : 1
                            };
                            firstLoginInfo.Items.Add(firstLoginItem);
                        }
                    }
                }
                
                // 存储到本地缓存（向后兼容）
                _firstLoginInfos.Clear();
                _firstLoginInfos.Add(firstLoginInfo);
                
                // 存储到GameWorld实例
                GameWorld.Instance.SetFirstLoginInfo(firstLoginInfo);
                
                LogManager.Default.Info($"首次登录信息加载完成: 等级={firstLoginInfo.Level}, 金币={firstLoginInfo.Gold}, 物品数={firstLoginInfo.Items.Count}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载首次登录信息失败", exception: ex);
            }
        }

        /// <summary>
        /// 初始化特殊协议
        /// </summary>
        private void InitSpecialProtocol()
        {
            LogManager.Default.Info("初始化特殊协议...");
            
            try
            {
                // 这里实现特殊协议初始化逻辑
                
                LogManager.Default.Info("特殊协议初始化完成");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"初始化特殊协议失败", exception: ex);
            }
        }

        /// <summary>
        /// 加载沙城配置
        /// </summary>
        private async Task LoadSandCityAsync()
        {
            LogManager.Default.Info("加载沙城配置...");
            
            await Task.Run(() =>
            {
                try
                {
                    // 使用SandCity单例初始化
                    var sandCity = SandCity.Instance;
                    sandCity.Init();
                    
                    LogManager.Default.Info("沙城配置加载完成");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载沙城配置失败", exception: ex);
                }
            });
        }

        /// <summary>
        /// 加载技能系统
        /// </summary>
        private void LoadMagicSystem()
        {
            LogManager.Default.Info("加载技能系统...");

            // 1. 基础技能
            LoadBaseMagic();

            // 2. 技能扩展
            LoadMagicExt();
        }

        /// <summary>
        /// 确保人物数据描述已加载
        /// </summary>
        private void EnsureHumanDataDescsLoaded()
        {
            // 在LoadServerConfig中已加载，这里只做验证
            if (_humanDataDescs.Count == 0)
            {
                LogManager.Default.Warning("人物数据描述未加载，尝试重新加载");
                LoadHumanDataDescs();
            }
            else
            {
                LogManager.Default.Debug("人物数据描述已加载");
            }
        }

        /// <summary>
        /// 确保出生点配置已加载
        /// </summary>
        private void EnsureStartPointsLoaded()
        {
            // 在LoadMapRelatedConfigs中已加载，这里只做验证
            if (_startPoints.Count == 0)
            {
                LogManager.Default.Warning("出生点配置未加载，尝试重新加载");
                LoadStartPoints();
            }
            else
            {
                LogManager.Default.Debug("出生点配置已加载");
            }
        }

        /// <summary>
        /// 确保首次登录信息已加载
        /// </summary>
        private void EnsureFirstLoginInfoLoaded()
        {
            // 在LoadServerConfig中已加载，这里只做验证
            if (_firstLoginInfos.Count == 0)
            {
                LogManager.Default.Warning("首次登录信息未加载，尝试重新加载");
                LoadFirstLoginInfo();
            }
            else
            {
                LogManager.Default.Debug("首次登录信息已加载");
            }
        }

        /// <summary>
        /// 优化脚本系统加载，避免重复
        /// </summary>
        private void LoadScriptSystemOptimized()
        {
            LogManager.Default.Info("加载脚本系统...");

            // 只使用ScriptObjectMgr加载脚本系统，避免重复
            // 检查是否已经加载过
            bool isLoaded = _scriptObjectMgr.GetScriptObjectCount() > 0;
            if (!isLoaded)
            {
                string scriptPath = SCRIPT_PATH;
                if (Directory.Exists(scriptPath))
                {
                    _scriptObjectMgr.Load(scriptPath);
                    int scriptCount = _scriptObjectMgr.GetScriptObjectCount();
                    LogManager.Default.Info($"成功加载 {scriptCount} 个脚本对象");
                }
                else
                {
                    LogManager.Default.Warning($"脚本目录不存在: {scriptPath}");
                }
            }
            else
            {
                LogManager.Default.Debug("脚本系统已加载，跳过重复加载");
            }

            // 初始化SystemScript
            LoadSystemScript();
        }

        /// <summary>
        /// 加载速度配置
        /// </summary>
        private void LoadSpeedConfig(Dictionary<string, string> config)
        {
            // 行走、跑步、攻击等速度配置
            int walkSpeed = GetConfigInt(config, "speed.walk", 600);
            int runSpeed = GetConfigInt(config, "speed.run", 300);
            int attackSpeed = GetConfigInt(config, "speed.attack", 800);
            int beAttackSpeed = GetConfigInt(config, "speed.beattack", 800);
            int spellSkillSpeed = GetConfigInt(config, "speed.spellskill", 800);

            SetGameVar(GameVarConstants.WalkSpeed, walkSpeed);
            SetGameVar(GameVarConstants.RunSpeed, runSpeed);
            SetGameVar(GameVarConstants.AttackSpeed, attackSpeed);
            SetGameVar(GameVarConstants.BeAttackSpeed, beAttackSpeed);
            SetGameVar(GameVarConstants.SpellSkillSpeed, spellSkillSpeed);

            LogManager.Default.Debug($"速度配置 - 行走:{walkSpeed} 跑步:{runSpeed} 攻击:{attackSpeed}");
        }

        /// <summary>
        /// 加载名称配置
        /// </summary>
        private void LoadNameConfig(Dictionary<string, string> config)
        {
            string goldName = GetConfigString(config, "name.goldname", "金币");
            string maleName = GetConfigString(config, "name.malename", "男");
            string femaleName = GetConfigString(config, "name.femalename", "女");
            string warrName = GetConfigString(config, "name.warr", "战士");
            string magicanName = GetConfigString(config, "name.magican", "法师");
            string taoshiName = GetConfigString(config, "name.taoshi", "道士");

            SetGameName(GameName.GoldName, goldName);
            SetGameName(GameName.MaleName, maleName);
            SetGameName(GameName.FemaleName, femaleName);
            SetGameName(GameName.WarrName, warrName);
            SetGameName(GameName.MagicanName, magicanName);
            SetGameName(GameName.TaoshiName, taoshiName);

            LogManager.Default.Debug($"职业名称 - 战士:{warrName} 法师:{magicanName} 道士:{taoshiName}");
        }

        /// <summary>
        /// 加载变量配置
        /// </summary>
        private void LoadVarConfig(Dictionary<string, string> config)
        {
            int maxGold = GetConfigInt(config, "var.maxgold", 5000000);
            int maxYuanbao = GetConfigInt(config, "var.maxyuanbao", 2000);
            int maxGroupMember = GetConfigInt(config, "var.maxgroupmember", 10);
            int redPkPoint = GetConfigInt(config, "var.redpkpoint", 12);
            int yellowPkPoint = GetConfigInt(config, "var.yellowpkpoint", 6);
            int storageSize = GetConfigInt(config, "var.storagesize", 100);
            int charInfoBackupTime = GetConfigInt(config, "var.charinfobackuptime", 5);
            int onePkPointTime = GetConfigInt(config, "var.onepkpointtime", 60);
            int grayNameTime = GetConfigInt(config, "var.graynametime", 300);
            int oncePkPoint = GetConfigInt(config, "var.oncepkpoint", 1);
            int pkCurseRate = GetConfigInt(config, "var.pkcurserate", 10);
            int addFriendLevel = GetConfigInt(config, "var.addfriendlevel", 30);
            bool enableSafeAreaNotice = GetConfigBool(config, "var.enablesafeareanotice", true);

            SetGameVar(GameVarConstants.MaxGold, maxGold);
            SetGameVar(GameVarConstants.MaxYuanbao, maxYuanbao);
            SetGameVar(GameVarConstants.MaxGroupMember, maxGroupMember);
            SetGameVar(GameVarConstants.RedPkPoint, redPkPoint);
            SetGameVar(GameVarConstants.YellowPkPoint, yellowPkPoint);
            SetGameVar(GameVarConstants.StorageSize, storageSize);
            SetGameVar(GameVarConstants.CharInfoBackupTime, charInfoBackupTime);
            SetGameVar(GameVarConstants.OnePkPointTime, onePkPointTime);
            SetGameVar(GameVarConstants.GrayNameTime, grayNameTime);
            SetGameVar(GameVarConstants.OncePkPoint, oncePkPoint);
            SetGameVar(GameVarConstants.PkCurseRate, pkCurseRate);
            SetGameVar(GameVarConstants.AddFriendLevel, addFriendLevel);
            SetGameVar(GameVarConstants.EnableSafeAreaNotice, enableSafeAreaNotice ? 1 : 0);

            LogManager.Default.Debug($"游戏变量 - 最大金币:{maxGold} 最大元宝:{maxYuanbao} 最大组队人数:{maxGroupMember}");
        }

        /// <summary>
        /// 加载聊天等待时间配置
        /// </summary>
        private void LoadChatWaitConfig(Dictionary<string, string> config)
        {
            int normalWait = GetConfigInt(config, "chatwait.normal", 1);
            int cryWait = GetConfigInt(config, "chatwait.cry", 10);
            int whisperWait = GetConfigInt(config, "chatwait.whisper", 2);
            int groupWait = GetConfigInt(config, "chatwait.group", 2);
            int guildWait = GetConfigInt(config, "chatwait.guild", 3);

            SetChannelWaitTime(ChatWaitChannel.Normal, normalWait);
            SetChannelWaitTime(ChatWaitChannel.Cry, cryWait);
            SetChannelWaitTime(ChatWaitChannel.Whisper, whisperWait);
            SetChannelWaitTime(ChatWaitChannel.Group, groupWait);
            SetChannelWaitTime(ChatWaitChannel.Guild, guildWait);

            LogManager.Default.Debug($"聊天等待 - 普通:{normalWait}秒 喊话:{cryWait}秒");
        }

        /// <summary>
        /// 加载脚本系统
        /// </summary>
        private void LoadScriptSystem()
        {
            LogManager.Default.Info("加载脚本系统...");

            // 使用ScriptObjectMgr加载脚本系统
            LoadScriptObjectMgr();

            // 加载脚本对象（保持向后兼容）
            string scriptPath = SCRIPT_PATH;
            if (Directory.Exists(scriptPath))
            {
                LogManager.Default.Info($"加载脚本目录: {scriptPath}");
                LoadScriptObjects(scriptPath);
            }

            // 加载脚本变量（保持向后兼容）
            string varsPath = VARIABLES_PATH;
            if (Directory.Exists(varsPath))
            {
                LogManager.Default.Info($"加载脚本变量目录: {varsPath}");
                LoadScriptVariables(varsPath);
            }

            // 加载字符串列表（保持向后兼容）
            string stringListPath = STRINGLIST_PATH;
            if (Directory.Exists(stringListPath))
            {
                LogManager.Default.Info($"加载字符串列表: {stringListPath}");
                LoadStringList(stringListPath);
            }
        }

        /// <summary>
        /// 加载脚本对象
        /// </summary>
        private void LoadScriptObjects(string scriptPath)
        {
            try
            {
                var parser = new Parsers.SimpleScriptParser();
                var scriptFiles = Directory.GetFiles(scriptPath, "*.txt", SearchOption.AllDirectories);
                int loadedCount = 0;

                foreach (var file in scriptFiles)
                {
                    var scriptLines = parser.Parse(file);
                    if (scriptLines.Count > 0)
                    {
                        loadedCount++;
                        LogManager.Default.Debug($"加载脚本文件: {Path.GetFileName(file)} ({scriptLines.Count} 行)");
                    }
                }

                LogManager.Default.Info($"加载了 {loadedCount} 个脚本文件");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载脚本对象失败: {scriptPath}", exception: ex);
            }
        }

        /// <summary>
        /// 加载脚本变量
        /// </summary>
        private void LoadScriptVariables(string varsPath)
        {
            try
            {
                var parser = new Parsers.TextFileParser();
                var varFiles = Directory.GetFiles(varsPath, "*.txt", SearchOption.AllDirectories);
                int loadedCount = 0;

                foreach (var file in varFiles)
                {
                    var variables = parser.LoadKeyValue(file);
                    if (variables.Count > 0)
                    {
                        loadedCount++;
                        LogManager.Default.Debug($"加载变量文件: {Path.GetFileName(file)} ({variables.Count} 个变量)");
                    }
                }

                LogManager.Default.Info($"加载了 {loadedCount} 个变量文件");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载脚本变量失败: {varsPath}", exception: ex);
            }
        }

        /// <summary>
        /// 加载字符串列表
        /// </summary>
        private void LoadStringList(string stringListPath)
        {
            try
            {
                var parser = new Parsers.TextFileParser();
                var stringFiles = Directory.GetFiles(stringListPath, "*.txt", SearchOption.AllDirectories);
                int loadedCount = 0;

                foreach (var file in stringFiles)
                {
                    var strings = parser.LoadLines(file);
                    if (strings.Count > 0)
                    {
                        loadedCount++;
                        LogManager.Default.Debug($"加载字符串文件: {Path.GetFileName(file)} ({strings.Count} 个字符串)");
                    }
                }

                LogManager.Default.Info($"加载了 {loadedCount} 个字符串文件");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载字符串列表失败: {stringListPath}", exception: ex);
            }
        }

        /// <summary>
        /// 加载地图相关配置
        /// </summary>
        private void LoadMapConfigs()
        {
            LogManager.Default.Info("加载地图配置...");

            // 加载物理地图
            LoadPhysicsMaps();

            // 加载逻辑地图
            LoadLogicMaps();

            // 加载安全区配置
            LoadSafeArea();

            // 加载出生点配置
            LoadStartPoint();

            // 加载地图脚本
            LoadMapScript();
        }

        /// <summary>
        /// 加载物理地图
        /// </summary>
        private void LoadPhysicsMaps()
        {
            string physicsPath = Path.Combine(MAPS_PATH, "physics");
            string cachePath = Path.Combine(MAPS_PATH, "pm_cache");

            if (Directory.Exists(physicsPath))
            {
                LogManager.Default.Info($"物理地图路径: {physicsPath}");
                LogManager.Default.Info($"物理地图缓存路径: {cachePath}");
                
                try
                {
                    // 使用PhysicsMapMgr加载物理地图
                    _physicsMapMgr.Init(physicsPath, cachePath);
                    int mapCount = _physicsMapMgr.LoadedMapCount;
                    LogManager.Default.Info($"成功加载 {mapCount} 个物理地图");
                    
                    // 同时使用旧的解析器保持向后兼容
                    var mapParser = new Parsers.MapFileParser();
                    var mapFiles = Directory.GetFiles(physicsPath, "*.map", SearchOption.AllDirectories);
                    int oldLoadedCount = 0;
                    
                    foreach (var mapFile in mapFiles)
                    {
                        // 检查是否有缓存
                        string cacheFile = Path.Combine(cachePath, Path.GetFileNameWithoutExtension(mapFile) + ".pmc");
                        Parsers.MapFileParser.MapData? mapData = null;
                        
                        if (File.Exists(cacheFile))
                        {
                            mapData = mapParser.LoadMapCache(cacheFile);
                        }
                        
                        if (mapData == null)
                        {
                            mapData = mapParser.LoadMapFile(mapFile);
                            if (mapData != null)
                            {
                                // 保存缓存
                                mapParser.SaveMapCache(mapData, cacheFile);
                            }
                        }
                        
                        if (mapData != null)
                        {
                            oldLoadedCount++;
                            LogManager.Default.Debug($"旧解析器加载物理地图: {mapData.FileName} ({mapData.Width}x{mapData.Height})");
                        }
                    }
                    
                    LogManager.Default.Debug($"旧解析器加载了 {oldLoadedCount} 个物理地图");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载物理地图失败: {physicsPath}", exception: ex);
                }
            }
        }

        /// <summary>
        /// 加载逻辑地图
        /// </summary>
        private void LoadLogicMaps()
        {
            string logicPath = Path.Combine(MAPS_PATH, "logic");

            if (Directory.Exists(logicPath))
            {
                LogManager.Default.Info($"加载逻辑地图: {logicPath}");
                try
                {
                    // 使用LogicMapMgr加载逻辑地图
                    LogicMapMgr.Instance.Load(logicPath);
                    int mapCount = LogicMapMgr.Instance.GetMapCount();
                    LogManager.Default.Info($"成功加载 {mapCount} 个逻辑地图配置");
                    
                    // 同时使用旧的解析器保持向后兼容
                    var logicParser = new Parsers.LogicMapConfigParser();
                    if (logicParser.LoadMapConfigs(logicPath))
                    {
                        int oldMapCount = 0;
                        foreach (var map in logicParser.GetAllMaps())
                        {
                            oldMapCount++;
                            LogManager.Default.Debug($"逻辑地图配置: ID={map.MapID}, 名称={map.MapName}");
                        }
                        LogManager.Default.Debug($"旧解析器加载了 {oldMapCount} 个逻辑地图配置");
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载逻辑地图失败: {logicPath}", exception: ex);
                }
            }
        }

        /// <summary>
        /// 加载安全区配置 (safearea.csv)
        /// </summary>
        private void LoadSafeArea()
        {
            string safeAreaFile = Path.Combine(DATA_PATH, "safearea.csv");

            if (File.Exists(safeAreaFile))
            {
                LogManager.Default.Info($"加载安全区配置: {safeAreaFile}");
                try
                {
                    var lines = SmartReader.ReadAllLines(safeAreaFile);
                    int count = 0;
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                            continue;

                        var parts = line.Split(',');
                        if (parts.Length >= 4)
                        {
                            // 格式: MapID, CenterX, CenterY, Radius
                            if (int.TryParse(parts[0], out int mapId) &&
                                int.TryParse(parts[1], out int centerX) &&
                                int.TryParse(parts[2], out int centerY) &&
                                int.TryParse(parts[3], out int radius))
                            {
                                count++;
                                LogManager.Default.Debug($"安全区 - 地图:{mapId} 中心:({centerX},{centerY}) 半径:{radius}");
                            }
                        }
                    }
                    LogManager.Default.Info($"加载了 {count} 个安全区配置");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载安全区配置失败: {safeAreaFile}", exception: ex);
                }
            }
        }

        /// <summary>
        /// 加载出生点配置 (startpoint.csv)
        /// </summary>
        private void LoadStartPoint()
        {
            string startPointFile = Path.Combine(DATA_PATH, "startpoint.csv");

            if (File.Exists(startPointFile))
            {
                LogManager.Default.Info($"加载出生点配置: {startPointFile}");
                try
                {
                    var lines = SmartReader.ReadAllLines(startPointFile);
                    int count = 0;
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                            continue;

                        var parts = line.Split(',');
                        if (parts.Length >= 7)
                        {
                            // 格式: Name, MapID, X, Y, Range, Fighter, Magician, Taoshi
                            string name = parts[0].Trim();
                            if (int.TryParse(parts[1], out int mapId) &&
                                int.TryParse(parts[2], out int x) &&
                                int.TryParse(parts[3], out int y) &&
                                int.TryParse(parts[4], out int range))
                            {
                                count++;
                                LogManager.Default.Debug($"出生点 - {name} 地图:{mapId} 位置:({x},{y}) 范围:{range}");
                            }
                        }
                    }
                    LogManager.Default.Info($"加载了 {count} 个出生点配置");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载出生点配置失败: {startPointFile}", exception: ex);
                }
            }
        }

        /// <summary>
        /// 加载地图脚本 (mapscript.txt)
        /// </summary>
        private void LoadMapScript()
        {
            string mapScriptFile = Path.Combine(DATA_PATH, "mapscript.txt");

            if (File.Exists(mapScriptFile))
            {
                LogManager.Default.Info($"加载地图脚本: {mapScriptFile}");
                try
                {
                    var parser = new Parsers.TextFileParser();
                    var lines = parser.LoadLines(mapScriptFile);
                    int count = 0;
                    
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                            continue;
                        
                        var parts = line.Split('=');
                        if (parts.Length == 2)
                        {
                            string mapName = parts[0].Trim();
                            string scriptName = parts[1].Trim();
                            
                            LogManager.Default.Debug($"地图脚本: {mapName} -> {scriptName}");
                            count++;
                        }
                    }
                    
                    LogManager.Default.Info($"加载了 {count} 个地图脚本配置");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载地图脚本失败: {mapScriptFile}", exception: ex);
                }
            }
            else
            {
                LogManager.Default.Warning($"地图脚本文件不存在: {mapScriptFile}");
            }
        }

        /// <summary>
        /// 加载物品配置
        /// </summary>
        private void LoadItemConfigs()
        {
            LogManager.Default.Info("加载物品配置...");

            // 加载基础物品数据
            LoadBaseItem();

            // 加载物品限制
            LoadItemLimit();

            // 加载物品脚本链接
            LoadItemScript();

            // 加载捆绑物品
            LoadBundleItem();

            // 加载特殊装备
            LoadSpecialItem();
        }

        /// <summary>
        /// 加载基础物品数据 (baseitem.txt)
        /// </summary>
        private void LoadBaseItem()
        {
            string itemFile = Path.Combine(DATA_PATH, "baseitem.txt");

            if (File.Exists(itemFile))
            {
                LogManager.Default.Info($"加载基础物品: {itemFile}");
                
                // 使用ItemManager加载物品数据
                if (ItemManager.Instance.Load(itemFile))
                {
                    LogManager.Default.Info("基础物品数据加载成功");
                }
                else
                {
                    LogManager.Default.Warning("加载基础物品数据失败");
                }
                
                // 同时使用ItemDataParser保持向后兼容
                if (_itemDataParser.Load(itemFile))
                {
                    LogManager.Default.Debug("ItemDataParser加载物品数据成功");
                }
            }
            else
            {
                LogManager.Default.Warning($"基础物品文件不存在: {itemFile}");
            }
        }

        /// <summary>
        /// 加载物品限制 (itemlimit.txt)
        /// </summary>
        private void LoadItemLimit()
        {
            string limitFile = Path.Combine(DATA_PATH, "itemlimit.txt");

            if (File.Exists(limitFile))
            {
                LogManager.Default.Info($"加载物品限制: {limitFile}");
                try
                {
                    var parser = new Parsers.TextFileParser();
                    var lines = parser.LoadLines(limitFile);
                    int count = 0;
                    
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                            continue;
                        
                        var parts = line.Split('=');
                        if (parts.Length == 2)
                        {
                            string itemName = parts[0].Trim();
                            string limitValue = parts[1].Trim();
                            
                            LogManager.Default.Debug($"物品限制: {itemName} -> {limitValue}");
                            count++;
                        }
                    }
                    
                    LogManager.Default.Info($"加载了 {count} 个物品限制配置");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载物品限制失败: {limitFile}", exception: ex);
                }
            }
            else
            {
                LogManager.Default.Warning($"物品限制文件不存在: {limitFile}");
            }
        }

        /// <summary>
        /// 加载物品脚本链接 (itemscript.txt)
        /// </summary>
        private void LoadItemScript()
        {
            string scriptFile = Path.Combine(DATA_PATH, "itemscript.txt");

            if (File.Exists(scriptFile))
            {
                LogManager.Default.Info($"加载物品脚本链接: {scriptFile}");
                if (_itemDataParser.LoadItemScript(scriptFile))
                {
                    LogManager.Default.Info("物品脚本链接加载成功");
                }
                else
                {
                    LogManager.Default.Warning("加载物品脚本链接失败");
                }
            }
            else
            {
                LogManager.Default.Warning($"物品脚本链接文件不存在: {scriptFile}");
            }
        }

        /// <summary>
        /// 加载捆绑物品 (bundleitem.csv)
        /// </summary>
        private void LoadBundleItem()
        {
            string bundleFile = Path.Combine(DATA_PATH, "bundleitem.csv");

            if (File.Exists(bundleFile))
            {
                LogManager.Default.Info($"加载捆绑物品: {bundleFile}");
                try
                {
                    // 使用BundleManager加载捆绑物品配置
                    BundleManager.Instance.LoadBundle(bundleFile, true); // true表示CSV格式
                    int bundleManagerCount = BundleManager.Instance.GetBundleCount();
                    LogManager.Default.Info($"成功加载 {bundleManagerCount} 个捆绑物品配置");
                    
                    // 同时使用旧的解析器保持向后兼容
                    var parser = new Parsers.CSVParser();
                    var bundleData = parser.Parse(bundleFile, false);
                    int oldCount = 0;
                    
                    foreach (var row in bundleData)
                    {
                        if (row.Count >= 3)
                        {
                            string itemName = row["Column0"];
                            string bundleName = row["Column1"];
                            int itemCount = int.Parse(row["Column2"]);
                            
                            LogManager.Default.Debug($"捆绑物品: {itemName} -> {bundleName} x{itemCount}");
                            oldCount++;
                        }
                    }
                    
                    LogManager.Default.Debug($"旧解析器加载了 {oldCount} 个捆绑物品配置");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载捆绑物品失败: {bundleFile}", exception: ex);
                }
            }
            else
            {
                LogManager.Default.Warning($"捆绑物品文件不存在: {bundleFile}");
            }
        }

        /// <summary>
        /// 加载特殊装备 (specialitem.txt)
        /// </summary>
        private void LoadSpecialItem()
        {
            string specialFile = Path.Combine(DATA_PATH, "specialitem.txt");

            if (File.Exists(specialFile))
            {
                LogManager.Default.Info($"加载特殊装备: {specialFile}");
                try
                {
                    // 使用SpecialEquipmentManager加载特殊装备配置
                    if (_specialEquipmentManager.LoadSpecialEquipmentFunction(specialFile))
                    {
                        int count = _specialEquipmentManager.GetSpecialEquipmentCount();
                        LogManager.Default.Info($"成功加载 {count} 个特殊装备配置");
                    }
                    else
                    {
                        LogManager.Default.Warning("加载特殊装备配置失败");
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载特殊装备失败: {specialFile}", exception: ex);
                }
            }
            else
            {
                LogManager.Default.Warning($"特殊装备文件不存在: {specialFile}");
            }
        }

        /// <summary>
        /// 加载技能/魔法配置
        /// </summary>
        private void LoadMagicConfigs()
        {
            LogManager.Default.Info("加载技能配置...");

            // 使用MagicManager加载魔法技能配置
            LoadMagicManager();

            // 加载基础技能（保持向后兼容）
            LoadBaseMagic();

            // 加载技能扩展（保持向后兼容）
            LoadMagicExt();
        }

        /// <summary>
        /// 加载基础技能 (basemagic.txt)
        /// </summary>
        private void LoadBaseMagic()
        {
            string magicFile = Path.Combine(DATA_PATH, "basemagic.txt");

            if (File.Exists(magicFile))
            {
                LogManager.Default.Info($"加载基础技能: {magicFile}");
                if (_magicDataParser.Load(magicFile))
                {
                    LogManager.Default.Info("基础技能加载成功");
                }
                else
                {
                    LogManager.Default.Warning("加载基础技能失败");
                }
            }
            else
            {
                LogManager.Default.Warning($"基础技能文件不存在: {magicFile}");
            }
        }

        /// <summary>
        /// 加载技能扩展 (magicext.csv)
        /// </summary>
        private void LoadMagicExt()
        {
            string magicExtFile = Path.Combine(DATA_PATH, "magicext.csv");

            if (File.Exists(magicExtFile))
            {
                LogManager.Default.Info($"加载技能扩展: {magicExtFile}");
                try
                {
                    var parser = new Parsers.CSVParser();
                    var magicExtData = parser.Parse(magicExtFile, true);
                    int count = 0;
                    
                    foreach (var row in magicExtData)
                    {
                        if (row.ContainsKey("MagicID") && row.ContainsKey("ExtValue"))
                        {
                            string magicId = row["MagicID"];
                            string extValue = row["ExtValue"];
                            
                            LogManager.Default.Debug($"技能扩展: {magicId} -> {extValue}");
                            count++;
                        }
                    }
                    
                    LogManager.Default.Info($"加载了 {count} 个技能扩展配置");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载技能扩展失败: {magicExtFile}", exception: ex);
                }
            }
            else
            {
                LogManager.Default.Warning($"技能扩展文件不存在: {magicExtFile}");
            }
        }

        /// <summary>
        /// 加载NPC配置
        /// </summary>
        private void LoadNpcConfigs()
        {
            LogManager.Default.Info("加载NPC配置...");

            // 使用NpcManagerEx加载NPC配置
            LoadNpcManagerEx();

            // 加载NPC生成配置（保持向后兼容）
            LoadNpcGen();
        }

        /// <summary>
        /// 加载NPC生成配置 (npcgen.txt)
        /// </summary>
        private void LoadNpcGen()
        {
            string npcGenFile = Path.Combine(DATA_PATH, "npcgen.txt");

            if (File.Exists(npcGenFile))
            {
                LogManager.Default.Info($"加载NPC生成: {npcGenFile}");
                if (_npcConfigParser.Load(npcGenFile))
                {
                    int npcCount = 0;
                    foreach (var npc in _npcConfigParser.GetAllNpcs())
                    {
                        npcCount++;
                    }
                    LogManager.Default.Info($"成功加载 {npcCount} 个NPC配置");
                }
                else
                {
                    LogManager.Default.Warning("加载NPC生成配置失败");
                }
            }
            else
            {
                LogManager.Default.Warning($"NPC生成文件不存在: {npcGenFile}");
            }
        }

        /// <summary>
        /// 加载怪物配置
        /// </summary>
        private void LoadMonsterConfigs()
        {
            LogManager.Default.Info("加载怪物配置...");

            // 加载基础怪物数据
            LoadBaseMonster();

            // 加载怪物脚本
            LoadMonsterScript();

            // 加载怪物生成配置
            LoadMonsterGen();

            // 加载怪物掉落
            LoadMonsterItems();
        }

        /// <summary>
        /// 加载基础怪物数据 (BaseMonsterEx.txt)
        /// </summary>
        private void LoadBaseMonster()
        {
            string monsterFile = Path.Combine(DATA_PATH, "BaseMonsterEx.txt");

            if (File.Exists(monsterFile))
            {
                LogManager.Default.Info($"加载基础怪物: {monsterFile}");
                if (_monsterDataParser.Load(monsterFile))
                {
                    int monsterCount = 0;
                    foreach (var monster in _monsterDataParser.GetAllMonsters())
                    {
                        monsterCount++;
                    }
                    LogManager.Default.Info($"成功加载 {monsterCount} 个怪物数据");
                }
                else
                {
                    LogManager.Default.Warning("加载基础怪物数据失败");
                }
            }
            else
            {
                LogManager.Default.Warning($"基础怪物文件不存在: {monsterFile}");
            }
        }

        /// <summary>
        /// 加载怪物脚本 (monsterscript.txt)
        /// </summary>
        private void LoadMonsterScript()
        {
            string scriptFile = Path.Combine(DATA_PATH, "monsterscript.txt");

            if (File.Exists(scriptFile))
            {
                LogManager.Default.Info($"加载怪物脚本: {scriptFile}");
                try
                {
                    var parser = new Parsers.TextFileParser();
                    var lines = parser.LoadLines(scriptFile);
                    int count = 0;
                    
                    foreach (var line in lines)
                    {
                        var parts = line.Split('=');
                        if (parts.Length == 2)
                        {
                            string monsterName = parts[0].Trim();
                            string scriptName = parts[1].Trim();
                            
                            LogManager.Default.Debug($"怪物脚本: {monsterName} -> {scriptName}");
                            count++;
                        }
                    }
                    
                    LogManager.Default.Info($"加载了 {count} 个怪物脚本配置");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载怪物脚本失败: {scriptFile}", exception: ex);
                }
            }
            else
            {
                LogManager.Default.Warning($"怪物脚本文件不存在: {scriptFile}");
            }
        }

        /// <summary>
        /// 加载怪物生成配置 (MonGens目录)
        /// </summary>
        private void LoadMonsterGen()
        {
            string monGenPath = MONGENS_PATH;

            if (Directory.Exists(monGenPath))
            {
                LogManager.Default.Info($"加载怪物生成配置: {monGenPath}");
                try
                {
                    var parser = new Parsers.TextFileParser();
                    var genFiles = Directory.GetFiles(monGenPath, "*.txt", SearchOption.AllDirectories);
                    int totalCount = 0;
                    
                    foreach (var file in genFiles)
                    {
                        var lines = parser.LoadLines(file);
                        int fileCount = 0;
                        
                        foreach (var line in lines)
                        {
                            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                                continue;
                            
                            var parts = line.Split(',');
                            if (parts.Length >= 5)
                            {
                                // 格式: MapID, X, Y, Range, MonsterName, Count, Interval
                                string monsterName = parts[4].Trim();
                                fileCount++;
                            }
                        }
                        
                        LogManager.Default.Debug($"怪物生成文件: {Path.GetFileName(file)} ({fileCount} 个生成点)");
                        totalCount += fileCount;
                    }
                    
                    LogManager.Default.Info($"加载了 {totalCount} 个怪物生成配置");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载怪物生成配置失败: {monGenPath}", exception: ex);
                }
            }
            else
            {
                LogManager.Default.Warning($"怪物生成目录不存在: {monGenPath}");
            }
        }

        /// <summary>
        /// 加载怪物掉落 (MonItems目录)
        /// </summary>
        private void LoadMonsterItems()
        {
            string monItemsPath = MONITEMS_PATH;

            if (Directory.Exists(monItemsPath))
            {
                LogManager.Default.Info($"加载怪物掉落: {monItemsPath}");
                try
                {
                    var parser = new Parsers.TextFileParser();
                    var itemFiles = Directory.GetFiles(monItemsPath, "*.txt", SearchOption.AllDirectories);
                    int totalCount = 0;
                    
                    foreach (var file in itemFiles)
                    {
                        var lines = parser.LoadLines(file);
                        int fileCount = 0;
                        
                        foreach (var line in lines)
                        {
                            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                                continue;
                            
                            var parts = line.Split(',');
                            if (parts.Length >= 3)
                            {
                                // 格式: MonsterName, ItemName, DropRate
                                string monsterName = parts[0].Trim();
                                string itemName = parts[1].Trim();
                                float dropRate = float.Parse(parts[2].Trim());
                                
                                fileCount++;
                            }
                        }
                        
                        LogManager.Default.Debug($"怪物掉落文件: {Path.GetFileName(file)} ({fileCount} 个掉落配置)");
                        totalCount += fileCount;
                    }
                    
                    LogManager.Default.Info($"加载了 {totalCount} 个怪物掉落配置");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载怪物掉落失败: {monItemsPath}", exception: ex);
                }
            }
            else
            {
                LogManager.Default.Warning($"怪物掉落目录不存在: {monItemsPath}");
            }
        }

        /// <summary>
        /// 加载行会配置
        /// </summary>
        private void LoadGuildConfigs()
        {
            LogManager.Default.Info("加载行会配置...");

            string guildPath = GUILD_PATH;

            if (Directory.Exists(guildPath))
            {
                LogManager.Default.Info($"加载行会数据: {guildPath}");
                try
                {
                    var guildFiles = Directory.GetFiles(guildPath, "*.txt", SearchOption.AllDirectories);
                    int loadedCount = 0;
                    
                    foreach (var file in guildFiles)
                    {
                        var parser = new Parsers.TextFileParser();
                        var lines = parser.LoadLines(file);
                        
                        foreach (var line in lines)
                        {
                            // 解析行会数据格式: 行会名称=会长名称,成员数,等级,...
                            var parts = line.Split('=');
                            if (parts.Length == 2)
                            {
                                string guildName = parts[0].Trim();
                                string guildData = parts[1].Trim();
                                loadedCount++;
                                LogManager.Default.Debug($"行会数据: {guildName} -> {guildData}");
                            }
                        }
                    }
                    
                    LogManager.Default.Info($"加载了 {loadedCount} 个行会数据");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载行会数据失败: {guildPath}", exception: ex);
                }
            }
            else
            {
                LogManager.Default.Warning($"行会目录不存在: {guildPath}");
            }
        }

        /// <summary>
        /// 加载GM配置
        /// </summary>
        private void LoadGMConfigs()
        {
            LogManager.Default.Info("加载GM配置...");

            // 加载GM列表
            LoadGMList();

            // 加载GM命令定义
            LoadGMCommandDef();
        }

        /// <summary>
        /// 加载GM列表 (gmlist.txt)
        /// </summary>
        private void LoadGMList()
        {
            string gmListFile = Path.Combine(DATA_PATH, "gmlist.txt");

            if (File.Exists(gmListFile))
            {
                LogManager.Default.Info($"加载GM列表: {gmListFile}");
                try
                {
                    var parser = new Parsers.TextFileParser();
                    var lines = parser.LoadLines(gmListFile);
                    int count = 0;
                    
                    foreach (var line in lines)
                    {
                        // 格式: 角色名=GM等级
                        var parts = line.Split('=');
                        if (parts.Length == 2)
                        {
                            string playerName = parts[0].Trim();
                            string gmLevel = parts[1].Trim();
                            count++;
                            LogManager.Default.Debug($"GM列表: {playerName} -> 等级{gmLevel}");
                        }
                    }
                    
                    LogManager.Default.Info($"加载了 {count} 个GM账号");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载GM列表失败: {gmListFile}", exception: ex);
                }
            }
            else
            {
                LogManager.Default.Warning($"GM列表文件不存在: {gmListFile}");
            }
        }

        /// <summary>
        /// 加载GM命令定义 (cmdlist.txt)
        /// </summary>
        private void LoadGMCommandDef()
        {
            string cmdListFile = Path.Combine(GAMEMASTER_PATH, "cmdlist.txt");

            if (File.Exists(cmdListFile))
            {
                LogManager.Default.Info($"加载GM命令定义: {cmdListFile}");
                try
                {
                    var parser = new Parsers.TextFileParser();
                    var lines = parser.LoadLines(cmdListFile);
                    int count = 0;
                    
                    foreach (var line in lines)
                    {
                        // 格式: 命令名=描述|权限等级|参数格式
                        var parts = line.Split('=');
                        if (parts.Length == 2)
                        {
                            string commandName = parts[0].Trim();
                            string commandDef = parts[1].Trim();
                            count++;
                            LogManager.Default.Debug($"GM命令: {commandName} -> {commandDef}");
                        }
                    }
                    
                    LogManager.Default.Info($"加载了 {count} 个GM命令定义");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载GM命令定义失败: {cmdListFile}", exception: ex);
                }
            }
            else
            {
                LogManager.Default.Warning($"GM命令定义文件不存在: {cmdListFile}");
            }
        }

        /// <summary>
        /// 加载公告 (notice.txt, linenotice.txt)
        /// </summary>
        private void LoadNotice()
        {
            // 加载主公告
            string noticeFile = Path.Combine(DATA_PATH, "notice.txt");
            if (File.Exists(noticeFile))
            {
                LogManager.Default.Info($"加载公告: {noticeFile}");
                try
                {
                    _notice = SmartReader.ReadTextFile(noticeFile);
                    LogManager.Default.Info($"公告内容长度: {_notice.Length}");
                    
                    // 存储到GameWorld
                    GameWorld.Instance.SetNotice(_notice);
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载公告失败: {noticeFile}", exception: ex);
                }
            }

            // 加载滚动公告
            string lineNoticeFile = Path.Combine(DATA_PATH, "linenotice.txt");
            if (File.Exists(lineNoticeFile))
            {
                LogManager.Default.Info($"加载滚动公告: {lineNoticeFile}");
                try
                {
                    var lines = SmartReader.ReadAllLines(lineNoticeFile);
                    _lineNotices.Clear();
                    
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                            continue;
                        _lineNotices.Add(line);
                    }
                    LogManager.Default.Info($"加载了 {_lineNotices.Count} 条滚动公告");
                    
                    // 存储到GameWorld
                    GameWorld.Instance.SetLineNotices(_lineNotices);
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载滚动公告失败: {lineNoticeFile}", exception: ex);
                }
            }
        }
        
        /// <summary>
        /// 获取公告
        /// </summary>
        public string GetNotice() => _notice;
        
        /// <summary>
        /// 获取滚动公告
        /// </summary>
        public List<string> GetLineNotices() => _lineNotices;

        /// <summary>
        /// 加载称号 (titles.csv)
        /// </summary>
        private void LoadTitles()
        {
            string titlesFile = Path.Combine(DATA_PATH, "titles.csv");

            if (File.Exists(titlesFile))
            {
                LogManager.Default.Info($"加载称号: {titlesFile}");
                try
                {
                    if (_titleManager.Load(titlesFile))
                    {
                        LogManager.Default.Info($"称号配置加载成功，共 {_titleManager.GetAllTitles().Count()} 个称号");
                    }
                    else
                    {
                        LogManager.Default.Warning("加载称号配置失败");
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载称号失败: {titlesFile}", exception: ex);
                }
            }
            else
            {
                LogManager.Default.Warning($"称号文件不存在: {titlesFile}");
            }
        }

        /// <summary>
        /// 加载排行榜 (toplist.txt, topnpc.txt)
        /// </summary>
        private void LoadTopList()
        {
            // 加载排行榜NPC
            string topNpcFile = Path.Combine(FIGURE_PATH, "topnpc.txt");
            if (File.Exists(topNpcFile))
            {
                LogManager.Default.Info($"加载排行榜NPC: {topNpcFile}");
                try
                {
                    if (_topManager.LoadTopNpcs(topNpcFile))
                    {
                        LogManager.Default.Info($"排行榜NPC配置加载成功，共 {_topManager.GetAllTopNpcs().Count()} 个NPC");
                    }
                    else
                    {
                        LogManager.Default.Warning("加载排行榜NPC配置失败");
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载排行榜NPC失败: {topNpcFile}", exception: ex);
                }
            }

            // 加载排行榜数据配置
            string topListFile = Path.Combine(FIGURE_PATH, "toplist.txt");
            if (File.Exists(topListFile))
            {
                LogManager.Default.Info($"加载排行榜数据配置: {topListFile}");
                try
                {
                    if (_topManager.LoadTopListConfig(topListFile))
                    {
                        LogManager.Default.Info("排行榜数据配置加载成功");
                    }
                    else
                    {
                        LogManager.Default.Warning("加载排行榜数据配置失败");
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载排行榜数据配置失败: {topListFile}", exception: ex);
                }
            }
        }

        /// <summary>
        /// 加载矿石列表 (minelist.txt)
        /// </summary>
        private void LoadMineList()
        {
            string mineListFile = Path.Combine(DATA_PATH, "minelist.txt");

            if (File.Exists(mineListFile))
            {
                LogManager.Default.Info($"加载矿石列表: {mineListFile}");
                try
                {
                    var parser = new Parsers.TextFileParser();
                    var lines = parser.LoadLines(mineListFile);
                    int count = 0;
                    
                    foreach (var line in lines)
                    {
                        // 格式: 地图ID,矿石名称,最小耐久,最大耐久,出现率
                        var parts = line.Split(',');
                        if (parts.Length >= 5)
                        {
                            if (uint.TryParse(parts[0].Trim(), out uint mapId) &&
                                ushort.TryParse(parts[2].Trim(), out ushort duraMin) &&
                                ushort.TryParse(parts[3].Trim(), out ushort duraMax) &&
                                ushort.TryParse(parts[4].Trim(), out ushort rate))
                            {
                                string oreName = parts[1].Trim();
                                
                                // 获取地图对象
                                var map = MapManager.Instance.GetMap(mapId);
                                if (map != null)
                                {
                                    // 添加矿石到地图
                                    map.AddMineItem(oreName, duraMin, duraMax, rate);
                                    count++;
                                    LogManager.Default.Debug($"地图 {mapId} 添加矿石: {oreName} (耐久:{duraMin}-{duraMax}, 率:{rate})");
                                }
                                else
                                {
                                    LogManager.Default.Warning($"地图 {mapId} 不存在，无法添加矿石: {oreName}");
                                }
                            }
                        }
                    }
                    
                    LogManager.Default.Info($"加载了 {count} 个矿石配置");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载矿石列表失败: {mineListFile}", exception: ex);
                }
            }
            else
            {
                LogManager.Default.Warning($"矿石列表文件不存在: {mineListFile}");
            }
        }

        /// <summary>
        /// 加载市场配置 (Market目录)
        /// </summary>
        private void LoadMarket()
        {
            // 加载市场滚动文字
            string scrollTextFile = Path.Combine(MARKET_PATH, "scrolltext.txt");
            if (File.Exists(scrollTextFile))
            {
                LogManager.Default.Info($"加载市场滚动文字: {scrollTextFile}");
                try
                {
                    _marketManager.LoadScrollText(scrollTextFile);
                    LogManager.Default.Info("市场滚动文字加载成功");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载市场滚动文字失败: {scrollTextFile}", exception: ex);
                }
            }

            // 加载市场主目录
            string mainDirFile = Path.Combine(MARKET_PATH, "MainDir.txt");
            if (File.Exists(mainDirFile))
            {
                LogManager.Default.Info($"加载市场主目录: {mainDirFile}");
                try
                {
                    if (_marketManager.LoadMainDirectory(mainDirFile))
                    {
                        LogManager.Default.Info("市场主目录配置加载成功");
                    }
                    else
                    {
                        LogManager.Default.Warning("加载市场主目录配置失败");
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载市场主目录失败: {mainDirFile}", exception: ex);
                }
            }
        }

        /// <summary>
        /// 加载自动脚本 (autoscript.txt)
        /// </summary>
        private void LoadAutoScript()
        {
            string autoScriptFile = Path.Combine(DATA_PATH, "autoscript.txt");

            if (File.Exists(autoScriptFile))
            {
                LogManager.Default.Info($"加载自动脚本: {autoScriptFile}");
                try
                {
                    _autoScriptManager.Load(autoScriptFile);
                    LogManager.Default.Info("自动脚本加载成功");
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载自动脚本失败: {autoScriptFile}", exception: ex);
                }
            }
            else
            {
                LogManager.Default.Warning($"自动脚本文件不存在: {autoScriptFile}");
            }
        }

        /// <summary>
        /// 加载任务系统 (task目录)
        /// </summary>
        private void LoadTasks()
        {
            string taskPath = TASK_PATH;

            if (Directory.Exists(taskPath))
            {
                LogManager.Default.Info($"加载任务系统: {taskPath}");
                try
                {
                    if (_taskManager.Load(taskPath))
                    {
                        LogManager.Default.Info("任务系统加载成功");
                    }
                    else
                    {
                        LogManager.Default.Warning("加载任务系统失败");
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Default.Error($"加载任务系统失败: {taskPath}", exception: ex);
                }
            }
            else
            {
                LogManager.Default.Warning($"任务目录不存在: {taskPath}");
            }
        }

        /// <summary>
        /// 加载人物数据描述
        /// </summary>
        private void LoadHumanDataDescs()
        {
            LogManager.Default.Info("加载人物数据描述...");

            // 加载战士数据
            LoadHumanDataDesc(0, Path.Combine(DATA_PATH, "humandata_warr.txt"));
            
            // 加载法师数据
            LoadHumanDataDesc(1, Path.Combine(DATA_PATH, "humandata_magican.txt"));
            
            // 加载道士数据
            LoadHumanDataDesc(2, Path.Combine(DATA_PATH, "humandata_taoshi.txt"));
        }

        /// <summary>
        /// 加载出生点配置
        /// </summary>
        private void LoadStartPoints()
        {
            LogManager.Default.Info("加载出生点配置...");

            string startPointFile = Path.Combine(DATA_PATH, "startpoint.csv");
            if (!File.Exists(startPointFile))
            {
                LogManager.Default.Warning($"出生点配置文件不存在: {startPointFile}");
                return;
            }

            try
            {
                var lines = SmartReader.ReadAllLines(startPointFile);
                int index = 0;
                var startPoints = new Dictionary<int, StartPoint>();
                
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    var parts = line.Split(',');
                    if (parts.Length >= 7)
                    {
                        string name = parts[0].Trim();
                        if (int.TryParse(parts[1], out int mapId) &&
                            int.TryParse(parts[2], out int x) &&
                            int.TryParse(parts[3], out int y) &&
                            int.TryParse(parts[4], out int range))
                        {
                            var startPoint = new StartPoint
                            {
                                Index = index,
                                Name = name,
                                MapId = mapId,
                                X = x,
                                Y = y,
                                Range = range
                            };

                            // 存储到本地缓存（向后兼容）
                            _startPoints[index] = startPoint;
                            _startPointNameToIndex[name] = index;
                            
                            // 存储到GameWorld实例
                            startPoints[index] = startPoint;
                            index++;
                        }
                    }
                }

                // 批量设置到GameWorld
                GameWorld.Instance.SetStartPoints(startPoints);
                LogManager.Default.Info($"加载了 {_startPoints.Count} 个出生点配置");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载出生点配置失败: {startPointFile}", exception: ex);
            }
        }

        /// <summary>
        /// 加载首次登录信息
        /// </summary>
        private void LoadFirstLoginInfo()
        {
            LogManager.Default.Info("加载首次登录信息...");

            string firstLoginFile = Path.Combine(DATA_PATH, "firstlogin.txt");
            if (!File.Exists(firstLoginFile))
            {
                LogManager.Default.Warning($"首次登录配置文件不存在: {firstLoginFile}");
                return;
            }

            try
            {
                var lines = SmartReader.ReadAllLines(firstLoginFile);
                var currentInfo = new FirstLoginInfo();

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    var parts = line.Split('=');
                    if (parts.Length == 2)
                    {
                        string key = parts[0].Trim();
                        string value = parts[1].Trim();

                        switch (key)
                        {
                            case "level":
                                if (int.TryParse(value, out int level))
                                    currentInfo.Level = level;
                                break;
                            case "gold":
                                if (uint.TryParse(value, out uint gold))
                                    currentInfo.Gold = gold;
                                break;
                            case "item":
                                // 处理物品信息
                                var itemParts = value.Split('*');
                                if (itemParts.Length == 2 && 
                                    int.TryParse(itemParts[1], out int count))
                                {
                                    currentInfo.Items.Add(new FirstLoginItem
                                    {
                                        ItemName = itemParts[0].Trim(),
                                        Count = count
                                    });
                                }
                                break;
                        }
                    }
                }

                // 存储到本地缓存（向后兼容）
                _firstLoginInfos.Add(currentInfo);
                
                // 存储到GameWorld实例
                GameWorld.Instance.SetFirstLoginInfo(currentInfo);
                
                LogManager.Default.Info($"加载首次登录信息: 等级={currentInfo.Level}, 金币={currentInfo.Gold}, 物品数={currentInfo.Items.Count}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载首次登录信息失败: {firstLoginFile}", exception: ex);
            }
        }

        /// <summary>
        /// 加载HumanPlayerMgr
        /// </summary>
        private void LoadHumanPlayerMgr()
        {
            LogManager.Default.Info("初始化玩家管理器...");
            try
            {
                // HumanPlayerMgr是单例，在首次访问时自动初始化
                var playerMgr = HumanPlayerMgr.Instance;
                LogManager.Default.Info("玩家管理器初始化完成");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"初始化玩家管理器失败", exception: ex);
            }
        }

        /// <summary>
        /// 加载MagicManager
        /// </summary>
        private void LoadMagicManager()
        {
            LogManager.Default.Info("加载魔法技能管理器...");
            try
            {
                // 使用MagicManager加载所有魔法技能配置
                _magicManager.LoadAll();
                int magicCount = _magicManager.GetMagicCount();
                LogManager.Default.Info($"成功加载 {magicCount} 个魔法技能配置");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载魔法技能管理器失败", exception: ex);
            }
        }

        /// <summary>
        /// 加载NpcManagerEx
        /// </summary>
        private void LoadNpcManagerEx()
        {
            LogManager.Default.Info("加载NPC管理器扩展...");
            try
            {
                // 加载NPC配置文件
                string npcGenFile = Path.Combine(DATA_PATH, "npcgen.txt");
                if (File.Exists(npcGenFile))
                {
                    _npcManagerEx.Load(npcGenFile);
                    LogManager.Default.Info("NPC管理器扩展加载完成");
                }
                else
                {
                    LogManager.Default.Warning($"NPC生成文件不存在: {npcGenFile}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载NPC管理器扩展失败", exception: ex);
            }
        }

        /// <summary>
        /// 加载ScriptObjectMgr
        /// </summary>
        private void LoadScriptObjectMgr()
        {
            LogManager.Default.Info("加载脚本对象管理器...");
            try
            {
                // 加载脚本目录
                string scriptPath = SCRIPT_PATH;
                if (Directory.Exists(scriptPath))
                {
                    _scriptObjectMgr.Load(scriptPath);
                    int scriptCount = _scriptObjectMgr.GetScriptObjectCount();
                    int varCount = _scriptObjectMgr.GetDefineVariableCount();
                    LogManager.Default.Info($"成功加载 {scriptCount} 个脚本对象和 {varCount} 个定义变量");
                }
                else
                {
                    LogManager.Default.Warning($"脚本目录不存在: {scriptPath}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载脚本对象管理器失败", exception: ex);
            }
        }

        /// <summary>
        /// 加载MonsterManagerEx
        /// </summary>
        private void LoadMonsterManagerEx()
        {
            LogManager.Default.Info("加载怪物管理器扩展...");
            try
            {
                // 加载怪物定义文件
                string monsterFile = Path.Combine(DATA_PATH, "BaseMonsterEx.txt");
                if (File.Exists(monsterFile))
                {
                    if (_monsterManagerEx.LoadMonsters(monsterFile))
                    {
                        LogManager.Default.Info("怪物管理器扩展加载完成");
                    }
                    else
                    {
                        LogManager.Default.Warning("加载怪物定义文件失败");
                    }
                }
                else
                {
                    LogManager.Default.Warning($"怪物定义文件不存在: {monsterFile}");
                }

                // 加载怪物脚本文件
                string monsterScriptFile = Path.Combine(DATA_PATH, "monsterscript.txt");
                if (File.Exists(monsterScriptFile))
                {
                    _monsterManagerEx.LoadMonsterScript(monsterScriptFile);
                    LogManager.Default.Info("怪物脚本加载完成");
                }
                else
                {
                    LogManager.Default.Warning($"怪物脚本文件不存在: {monsterScriptFile}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载怪物管理器扩展失败", exception: ex);
            }
        }

        /// <summary>
        /// 加载MonsterGenManager
        /// </summary>
        private void LoadMonsterGenManager()
        {
            LogManager.Default.Info("加载怪物生成管理器...");
            try
            {
                // 加载怪物生成配置
                string monGenPath = MONGENS_PATH;
                if (Directory.Exists(monGenPath))
                {
                    if (_monsterGenManager.LoadMonGen(monGenPath))
                    {
                        int genCount = _monsterGenManager.GetGenCount();
                        LogManager.Default.Info($"成功加载 {genCount} 个怪物生成点");
                        
                        // 初始化所有生成点
                        _monsterGenManager.InitAllGen();
                        LogManager.Default.Info("怪物生成点初始化完成");
                    }
                    else
                    {
                        LogManager.Default.Warning("加载怪物生成配置失败");
                    }
                }
                else
                {
                    LogManager.Default.Warning($"怪物生成目录不存在: {monGenPath}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载怪物生成管理器失败", exception: ex);
            }
        }

        /// <summary>
        /// 加载MonItemsMgr
        /// </summary>
        private void LoadMonItemsMgr()
        {
            LogManager.Default.Info("加载怪物掉落管理器...");
            try
            {
                // 加载怪物掉落配置
                string monItemsPath = MONITEMS_PATH;
                if (Directory.Exists(monItemsPath))
                {
                    if (_monItemsMgr.LoadMonItems(monItemsPath))
                    {
                        int itemsCount = _monItemsMgr.GetMonItemsCount();
                        LogManager.Default.Info($"成功加载 {itemsCount} 个怪物掉落配置");
                    }
                    else
                    {
                        LogManager.Default.Warning("加载怪物掉落配置失败");
                    }
                }
                else
                {
                    LogManager.Default.Warning($"怪物掉落目录不存在: {monItemsPath}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载怪物掉落管理器失败", exception: ex);
            }
        }

        /// <summary>
        /// 加载TimeSystem
        /// </summary>
        private void LoadTimeSystem()
        {
            LogManager.Default.Info("初始化时间系统...");
            try
            {
                // TimeSystem是单例，在首次访问时自动初始化
                var timeSystem = TimeSystem.Instance;
                LogManager.Default.Info("时间系统初始化完成");
                
                // 记录启动时间和当前游戏时间
                var startupTime = timeSystem.GetStartupTime();
                var currentGameTime = timeSystem.GetCurrentTime();
                LogManager.Default.Info($"服务器启动时间: {startupTime:yyyy-MM-dd HH:mm:ss}");
                LogManager.Default.Info($"当前游戏时间: {currentGameTime} (小时*4 + 分钟/15)");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"初始化时间系统失败", exception: ex);
            }
        }

        /// <summary>
        /// 加载SystemScript
        /// </summary>
        private void LoadSystemScript()
        {
            LogManager.Default.Info("初始化系统脚本...");
            try
            {
                // 获取系统脚本对象
                var systemScriptObject = ScriptObjectMgr.Instance.GetScriptObject("system");
                if (systemScriptObject != null)
                {
                    // 初始化SystemScript
                    SystemScript.Instance.Init(systemScriptObject);
                    LogManager.Default.Info("系统脚本初始化完成");
                    
                    // 记录系统脚本信息
                    var scriptObj = SystemScript.Instance.GetScriptObject();
                    if (scriptObj != null)
                    {
                        LogManager.Default.Info($"系统脚本对象: {scriptObj.Name}, 文件: {scriptObj.FilePath}");
                    }
                }
                else
                {
                    LogManager.Default.Warning("系统脚本对象不存在，创建空脚本对象");
                    // 创建空脚本对象
                    var emptyScriptObject = new ScriptObject();
                    SystemScript.Instance.Init(emptyScriptObject);
                    LogManager.Default.Info("使用空脚本对象初始化系统脚本");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"初始化系统脚本失败", exception: ex);
            }
        }

        /// <summary>
        /// 加载SpecialEquipmentManager
        /// </summary>
        private void LoadSpecialEquipmentManager()
        {
            LogManager.Default.Info("初始化特殊装备管理器...");
            try
            {
                // SpecialEquipmentManager是单例，在首次访问时自动初始化
                var specialEquipmentManager = SpecialEquipmentManager.Instance;
                LogManager.Default.Info("特殊装备管理器初始化完成");
                
                // 记录特殊装备管理器信息
                int equipmentCount = specialEquipmentManager.GetSpecialEquipmentCount();
                LogManager.Default.Info($"特殊装备管理器已加载 {equipmentCount} 个装备配置");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"初始化特殊装备管理器失败", exception: ex);
            }
        }

        /// <summary>
        /// 加载MapManager
        /// </summary>
        private void LoadMapManager()
        {
            LogManager.Default.Info("初始化地图管理器...");
            try
            {
                // 使用MapManager加载地图数据
                if (MapManager.Instance.Load())
                {
                    int mapCount = MapManager.Instance.GetMapCount();
                    LogManager.Default.Info($"成功加载 {mapCount} 个地图");
                }
                else
                {
                    LogManager.Default.Warning("加载地图数据失败");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"初始化地图管理器失败", exception: ex);
            }
        }

        // 辅助方法
        private int GetConfigInt(Dictionary<string, string> config, string key, int defaultValue)
        {
            if (config.TryGetValue(key, out var value) && int.TryParse(value, out int result))
                return result;
            return defaultValue;
        }

        private string GetConfigString(Dictionary<string, string> config, string key, string defaultValue)
        {
            return config.TryGetValue(key, out var value) ? value : defaultValue;
        }

        private bool GetConfigBool(Dictionary<string, string> config, string key, bool defaultValue)
        {
            if (config.TryGetValue(key, out var value))
            {
                if (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1")
                    return true;
                if (value.Equals("false", StringComparison.OrdinalIgnoreCase) || value == "0")
                    return false;
            }
            return defaultValue;
        }

        // 公共接口方法
        public HumanDataDesc? GetHumanDataDesc(int profession, int level)
        {
            // 优先从GameWorld实例获取
            var desc = GameWorld.Instance.GetHumanDataDesc(profession, level);
            if (desc != null)
                return desc;
            
            // 向后兼容：从本地缓存获取
            if (_humanDataDescs.TryGetValue(profession, out var levelDict) &&
                levelDict.TryGetValue(level, out var localDesc))
            {
                return localDesc;
            }
            return null;
        }

        public StartPoint? GetStartPoint(int index)
        {
            // 优先从GameWorld实例获取
            var point = GameWorld.Instance.GetStartPoint(index);
            if (point != null)
                return point;
            
            // 向后兼容：从本地缓存获取
            return _startPoints.TryGetValue(index, out var localPoint) ? localPoint : null;
        }

        public StartPoint? GetStartPoint(string name)
        {
            // 优先从GameWorld实例获取
            var point = GameWorld.Instance.GetStartPoint(name);
            if (point != null)
                return point;
            
            // 向后兼容：从本地缓存获取
            if (_startPointNameToIndex.TryGetValue(name, out int index))
            {
                return GetStartPoint(index);
            }
            return null;
        }

        public bool GetBornPoint(int profession, out int mapId, out int x, out int y, string? startPointName = null)
        {
            // 使用GameWorld实例的GetBornPoint方法
            return GameWorld.Instance.GetBornPoint(profession, out mapId, out x, out y, startPointName);
        }

        public FirstLoginInfo? GetFirstLoginInfo()
        {
            // 优先从GameWorld实例获取
            var info = GameWorld.Instance.GetFirstLoginInfo();
            if (info != null)
                return info;
            
            // 向后兼容：从本地缓存获取
            return _firstLoginInfos.Count > 0 ? _firstLoginInfos[0] : null;
        }

        public string GetGameName(string nameKey)
        {
            // 优先从GameWorld实例获取
            var name = GameWorld.Instance.GetGameName(nameKey);
            if (name != nameKey) // 如果GameWorld中有定义，返回该值
                return name;
            
            // 向后兼容：从本地缓存获取
            return _gameNames.TryGetValue(nameKey, out var localName) ? localName : nameKey;
        }

        public float GetGameVar(int varKey)
        {
            // 优先从GameWorld实例获取
            var value = GameWorld.Instance.GetGameVar(varKey);
            if (value != 0f) // 如果GameWorld中有定义，返回该值
                return value;
            
            // 向后兼容：从本地缓存获取
            return _gameVars.TryGetValue(varKey, out var localValue) ? localValue : 0f;
        }

        public int GetChannelWaitTime(int channel)
        {
            // 优先从GameWorld实例获取
            var time = GameWorld.Instance.GetChannelWaitTime(channel);
            if (time != 1) // 如果GameWorld中有定义，返回该值
                return time;
            
            // 向后兼容：从本地缓存获取
            return _channelWaitTimes.TryGetValue(channel, out var localTime) ? localTime : 1;
        }

        private void SetGameName(string key, string value)
        {
            // 存储到本地缓存（向后兼容）
            _gameNames[key] = value;
            // 存储到GameWorld实例
            GameWorld.Instance.SetGameName(key, value);
        }

        private void SetGameVar(int key, float value)
        {
            // 存储到本地缓存（向后兼容）
            _gameVars[key] = value;
            // 存储到GameWorld实例
            GameWorld.Instance.SetGameVar(key, value);
        }

        private void SetChannelWaitTime(int channel, int seconds)
        {
            // 存储到本地缓存（向后兼容）
            _channelWaitTimes[channel] = seconds;
            // 存储到GameWorld实例
            GameWorld.Instance.SetChannelWaitTime(channel, seconds);
        }

        /// <summary>
        /// 加载人物数据描述文件
        /// </summary>
        public bool LoadHumanDataDesc(int profession, string filePath)
        {
            if (!File.Exists(filePath))
            {
                LogManager.Default.Warning($"人物数据描述文件不存在: {filePath}");
                return false;
            }

            try
            {
                LogManager.Default.Info($"加载人物数据描述: 职业{profession} 文件:{filePath}");
                var lines = SmartReader.ReadAllLines(filePath);
                
                if (!_humanDataDescs.ContainsKey(profession))
                {
                    _humanDataDescs[profession] = new Dictionary<int, HumanDataDesc>();
                }

                var levelDict = _humanDataDescs[profession];
                var descs = new Dictionary<int, HumanDataDesc>();
                int count = 0;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    var parts = line.Split(',');
                    if (parts.Length >= 24)
                    {
                        // 解析等级数据
                        if (int.TryParse(parts[0], out int level))
                        {
                            var desc = new HumanDataDesc
                            {
                                Level = level,
                                Hp = ushort.Parse(parts[1]),
                                Mp = ushort.Parse(parts[2]),
                                LevelupExp = uint.Parse(parts[3]),
                                MinAc = ushort.Parse(parts[4]),
                                MaxAc = ushort.Parse(parts[5]),
                                MinMac = ushort.Parse(parts[6]),
                                MaxMac = ushort.Parse(parts[7]),
                                MinDc = ushort.Parse(parts[8]),
                                MaxDc = ushort.Parse(parts[9]),
                                MinMc = ushort.Parse(parts[10]),
                                MaxMc = ushort.Parse(parts[11]),
                                MinSc = ushort.Parse(parts[12]),
                                MaxSc = ushort.Parse(parts[13]),
                                HandWeight = ushort.Parse(parts[14]),
                                BagWeight = ushort.Parse(parts[15]),
                                BodyWeight = ushort.Parse(parts[16]),
                                HitRate = ushort.Parse(parts[17]),
                                Escape = ushort.Parse(parts[18]),
                                MageEscape = ushort.Parse(parts[19]),
                                PoisonEscape = ushort.Parse(parts[20]),
                                HpRecover = ushort.Parse(parts[21]),
                                MagicRecover = ushort.Parse(parts[22])
                            };

                            // 存储到本地缓存（向后兼容）
                            levelDict[level] = desc;
                            
                            // 存储到临时字典，用于批量设置到GameWorld
                            descs[level] = desc;
                            count++;
                        }
                    }
                }

                // 批量设置到GameWorld
                GameWorld.Instance.SetHumanDataDescs(profession, descs);
                
                LogManager.Default.Info($"加载了职业 {profession} 的 {count} 个等级数据");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载人物数据描述失败: {filePath}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 重新加载指定的配置
        /// </summary>
        public void ReloadConfig(string configType)
        {
            try
            {
                switch (configType.ToLower())
                {
                    case "item":
                        LoadItemConfigs();
                        LogManager.Default.Info("物品配置已重新加载");
                        break;

                    case "monster":
                        LoadMonsterConfigs();
                        LogManager.Default.Info("怪物配置已重新加载");
                        break;

                    case "magic":
                    case "skill":
                        LoadMagicConfigs();
                        LogManager.Default.Info("技能配置已重新加载");
                        break;

                    case "npc":
                        LoadNpcConfigs();
                        LogManager.Default.Info("NPC配置已重新加载");
                        break;

                    case "map":
                        LoadMapConfigs();
                        LogManager.Default.Info("地图配置已重新加载");
                        break;

                    case "guild":
                        LoadGuildConfigs();
                        LogManager.Default.Info("行会配置已重新加载");
                        break;

                    case "gm":
                        LoadGMConfigs();
                        LogManager.Default.Info("GM配置已重新加载");
                        break;

                    case "market":
                        LoadMarket();
                        LogManager.Default.Info("市场配置已重新加载");
                        break;

                    case "autoscript":
                        LoadAutoScript();
                        LogManager.Default.Info("自动脚本已重新加载");
                        break;

                    case "task":
                        LoadTasks();
                        LogManager.Default.Info("任务配置已重新加载");
                        break;

                    case "all":
                        LoadAllConfigs();
                        LogManager.Default.Info("所有配置已重新加载");
                        break;

                    default:
                        LogManager.Default.Warning($"未知的配置类型: {configType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"重新加载配置失败: {configType}", exception: ex);
            }
        }

        /// <summary>
        /// 加载地图系统
        /// </summary>
        private void LoadMapSystem()
        {
            LogManager.Default.Info("加载地图系统...");
            LoadMapConfigs();
        }

        /// <summary>
        /// 加载物品系统
        /// </summary>
        private void LoadItemSystem()
        {
            LogManager.Default.Info("加载物品系统...");
            LoadItemConfigs();
        }
    }

    /// <summary>
    /// 人物数据描述
    /// </summary>
    public class HumanDataDesc
    {
        public int Level { get; set; }
        public ushort Hp { get; set; }
        public ushort Mp { get; set; }
        public uint LevelupExp { get; set; }
        public ushort MinAc { get; set; }
        public ushort MaxAc { get; set; }
        public ushort MinMac { get; set; }
        public ushort MaxMac { get; set; }
        public ushort MinDc { get; set; }
        public ushort MaxDc { get; set; }
        public ushort MinMc { get; set; }
        public ushort MaxMc { get; set; }
        public ushort MinSc { get; set; }
        public ushort MaxSc { get; set; }
        public ushort HandWeight { get; set; }
        public ushort BagWeight { get; set; }
        public ushort BodyWeight { get; set; }
        public ushort HitRate { get; set; }
        public ushort Escape { get; set; }
        public ushort MageEscape { get; set; }
        public ushort PoisonEscape { get; set; }
        public ushort HpRecover { get; set; }
        public ushort MagicRecover { get; set; }
    }

    /// <summary>
    /// 出生点配置
    /// </summary>
    public class StartPoint
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public int MapId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Range { get; set; }
    }

    /// <summary>
    /// 首次登录信息
    /// </summary>
    public class FirstLoginInfo
    {
        public int Level { get; set; } = 1;
        public uint Gold { get; set; } = 1000;
        public List<FirstLoginItem> Items { get; set; } = new();
    }

    /// <summary>
    /// 首次登录物品信息
    /// </summary>
    public class FirstLoginItem
    {
        public string ItemName { get; set; } = string.Empty;
        public int Count { get; set; } = 1;
    }

    // GameVar、GameName、ChatWaitChannel枚举现在在GameWorld.cs中定义
    // 这里不再重复定义，直接使用GameWorld.cs中的定义
}
