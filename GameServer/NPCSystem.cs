using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;
using System.IO;
using MirCommon;
using MirCommon.Network;
using MirCommon.Utils;
using System.Text;

namespace GameServer
{
    #region 基础类型定义

    /// <summary>
    /// NPC类型
    /// </summary>
    public enum NPCType
    {
        Normal = 0,         // 普通NPC
        Shop = 1,           // 商店
        Storage = 2,        // 仓库
        Mission = 3,        // 任务NPC
        Teleport = 4,       // 传送NPC
        Guild = 5,          // 公会NPC
        Blacksmith = 6,     // 铁匠（修理/强化）
        Appraisal = 7,      // 鉴定师
        Trainer = 8,        // 技能训练师
        Guard = 9,          // 守卫
        Banker = 10         // 银行
    }

    /// <summary>
    /// NPC功能标志
    /// </summary>
    [Flags]
    public enum NPCFunction
    {
        None = 0,
        Talk = 1 << 0,          // 对话
        Shop = 1 << 1,          // 商店
        Repair = 1 << 2,        // 修理
        Enhance = 1 << 3,       // 强化
        Storage = 1 << 4,       // 仓库
        Mission = 1 << 5,       // 任务
        Teleport = 1 << 6,      // 传送
        Skill = 1 << 7,         // 学习技能
        Guild = 1 << 8,         // 公会
        Identify = 1 << 9,      // 鉴定
        Bank = 1 << 10          // 银行
    }

    /// <summary>
    /// 传送目的地
    /// </summary>
    public class TeleportDestination
    {
        public string Name { get; set; } = string.Empty;
        public int MapId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public uint Cost { get; set; }          // 传送费用
        public int RequireLevel { get; set; }   // 等级需求

        public TeleportDestination(string name, int mapId, int x, int y, uint cost = 0)
        {
            Name = name;
            MapId = mapId;
            X = x;
            Y = y;
            Cost = cost;
        }
    }

    #endregion

    #region NPC定义和实例

    /// <summary>
    /// NPC定义（模板）
    /// </summary>
    public class NPCDefinition
    {
        public int NPCId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public NPCType Type { get; set; }
        public NPCFunction Functions { get; set; }
        public int ModelId { get; set; }
        public string ScriptFile { get; set; } = string.Empty;
        public string Greeting { get; set; } = string.Empty;

        // 商店相关
        public List<int> ShopItems { get; set; } = new();
        public float BuyRate { get; set; } = 1.0f;      // 收购倍率
        public float SellRate { get; set; } = 1.0f;     // 出售倍率

        // 传送相关
        public List<TeleportDestination> Destinations { get; set; } = new();

        public NPCDefinition(int npcId, string name, NPCType type)
        {
            NPCId = npcId;
            Name = name;
            Type = type;
        }

        public bool HasFunction(NPCFunction function)
        {
            return (Functions & function) != 0;
        }
    }

    /// <summary>
    /// NPC实例（地图上的具体NPC）
    /// </summary>
    public class NPCInstance : Npc
    {
        public uint InstanceId { get; set; }
        public NPCDefinition Definition { get; set; }
        public bool IsActive { get; set; } = true;
        
        // 对话脚本
        private NPCScript? _script;

        public NPCInstance(NPCDefinition definition, uint instanceId, int mapId, int x, int y)
            : base(definition.NPCId, definition.Name, ConvertToNpcType(definition.Type))
        {
            Definition = definition;
            InstanceId = instanceId;
            MapId = mapId;
            X = (ushort)x;
            Y = (ushort)y;

            // 设置Npc属性
            this.Name = definition.Name;
            this.CanTalk = definition.HasFunction(NPCFunction.Talk);
            this.CanTrade = definition.HasFunction(NPCFunction.Shop);
            this.CanRepair = definition.HasFunction(NPCFunction.Repair);
            this.CanStore = definition.HasFunction(NPCFunction.Storage);
            this.ScriptFile = definition.ScriptFile;

            // 加载脚本
            if (!string.IsNullOrEmpty(definition.ScriptFile))
            {
                _script = NPCScriptManager.Instance.LoadScript(definition.ScriptFile);
            }
        }

        private static NpcType ConvertToNpcType(NPCType npcType)
        {
            return npcType switch
            {
                NPCType.Shop => NpcType.Merchant,
                NPCType.Storage => NpcType.Warehouse,
                NPCType.Teleport => NpcType.Teleporter,
                NPCType.Blacksmith => NpcType.Repair,
                _ => NpcType.Normal
            };
        }

        public string GetGreeting()
        {
            return string.IsNullOrEmpty(Definition.Greeting) 
                ? $"你好，我是{Definition.Name}。" 
                : Definition.Greeting;
        }

        public NPCScript? GetScript() => _script;

        /// <summary>
        /// 玩家与NPC交互
        /// </summary>
        public void OnInteract(HumanPlayer player)
        {
            if (!IsActive)
                return;

            LogManager.Default.Info($"{player.Name} 与 {Definition.Name} 交互");

            // 开始交互会话
            var session = NPCInteractionManager.Instance.StartInteraction(player, InstanceId);
            if (session == null)
                return;

            // 发送问候
            SendGreeting(player);
        }

        /// <summary>
        /// 发送问候消息
        /// </summary>
        private void SendGreeting(HumanPlayer player)
        {
            var greeting = GetGreeting();
            
            // 构建问候消息
            var builder = new PacketBuilder();
            builder.WriteUInt32(InstanceId);
            builder.WriteUInt16(ProtocolCmd.SM_NPCTALK);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteString(greeting);
            
            byte[] packet = builder.Build();
            player.SendMessage(packet);
        }

        public override bool GetViewMsg(out byte[] msg, MapObject? viewer = null)
        {
            // 构建NPC显示消息
            var builder = new PacketBuilder();
            builder.WriteUInt32(InstanceId);
            builder.WriteUInt16(ProtocolCmd.SM_APPEAR);
            builder.WriteUInt16((ushort)X);
            builder.WriteUInt16((ushort)Y);
            builder.WriteUInt16(0); // 方向，NPC默认朝下
            
            // NPC特征数据
            byte[] featureData = new byte[12];
            BitConverter.GetBytes(Definition.ModelId).CopyTo(featureData, 0);
            BitConverter.GetBytes(0).CopyTo(featureData, 4); // 状态
            BitConverter.GetBytes(0).CopyTo(featureData, 8); // 健康
            
            builder.WriteBytes(featureData);
            builder.WriteString($"{Definition.Name}/{Definition.Title}");
            
            msg = builder.Build();
            return true;
        }
    }

    #endregion

    #region 对话系统

    /// <summary>
    /// NPC对话选项
    /// </summary>
    public class DialogOption
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;

        public DialogOption(int id, string text, string action = "")
        {
            Id = id;
            Text = text;
            Action = action;
        }
    }

    /// <summary>
    /// NPC对话
    /// </summary>
    public class NPCDialog
    {
        public int DialogId { get; set; }
        public string Text { get; set; } = string.Empty;
        public List<DialogOption> Options { get; set; } = new();

        public NPCDialog(int dialogId, string text)
        {
            DialogId = dialogId;
            Text = text;
        }

        public void AddOption(DialogOption option)
        {
            Options.Add(option);
        }
    }

    /// <summary>
    /// NPC脚本
    /// </summary>
    public class NPCScript
    {
        public string ScriptName { get; set; } = string.Empty;
        private readonly Dictionary<int, NPCDialog> _dialogs = new();
        private int _startDialogId = 1;

        public NPCScript(string scriptName)
        {
            ScriptName = scriptName;
        }

        public void AddDialog(NPCDialog dialog)
        {
            _dialogs[dialog.DialogId] = dialog;
        }

        public NPCDialog? GetDialog(int dialogId)
        {
            _dialogs.TryGetValue(dialogId, out var dialog);
            return dialog;
        }

        public NPCDialog? GetStartDialog()
        {
            return GetDialog(_startDialogId);
        }

        public void SetStartDialog(int dialogId)
        {
            _startDialogId = dialogId;
        }
    }

    /// <summary>
    /// JSON脚本数据类
    /// </summary>
    public class ScriptData
    {
        public string ScriptName { get; set; } = string.Empty;
        public int StartDialogId { get; set; } = 1;
        public List<DialogData> Dialogs { get; set; } = new();
    }

    public class DialogData
    {
        public int DialogId { get; set; }
        public string Text { get; set; } = string.Empty;
        public List<OptionData> Options { get; set; } = new();
    }

    public class OptionData
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;
    }

    /// <summary>
    /// NPC脚本管理器
    /// </summary>
    public class NPCScriptManager
    {
        private static NPCScriptManager? _instance;
        public static NPCScriptManager Instance => _instance ??= new NPCScriptManager();

        private readonly ConcurrentDictionary<string, NPCScript> _scripts = new();
        private readonly string _scriptsDirectory = "NPCScripts";

        private NPCScriptManager()
        {
            InitializeDefaultScripts();
        }

        public NPCScript? LoadScript(string scriptFile)
        {
            if (_scripts.TryGetValue(scriptFile, out var script))
                return script;

            // 尝试从文件加载脚本
            var loadedScript = LoadScriptFromFile(scriptFile);
            if (loadedScript != null)
            {
                _scripts[scriptFile] = loadedScript;
                return loadedScript;
            }

            // 文件加载失败，返回默认脚本
            return CreateDefaultScript(scriptFile);
        }

        private NPCScript? LoadScriptFromFile(string scriptFile)
        {
            try
            {
                string filePath = Path.Combine(_scriptsDirectory, $"{scriptFile}.json");
                
                if (!File.Exists(filePath))
                {
                    LogManager.Default.Warning($"NPC脚本文件不存在: {filePath}");
                    return null;
                }

                string jsonContent = SmartReader.ReadTextFile(filePath);
                var scriptData = JsonSerializer.Deserialize<ScriptData>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (scriptData == null)
                {
                    LogManager.Default.Error($"无法解析NPC脚本文件: {filePath}");
                    return null;
                }

                var script = new NPCScript(scriptData.ScriptName);
                script.SetStartDialog(scriptData.StartDialogId);

                foreach (var dialogData in scriptData.Dialogs)
                {
                    var dialog = new NPCDialog(dialogData.DialogId, dialogData.Text);
                    
                    foreach (var optionData in dialogData.Options)
                    {
                        var option = new DialogOption(optionData.Id, optionData.Text, optionData.Action)
                        {
                            Condition = optionData.Condition
                        };
                        dialog.AddOption(option);
                    }
                    
                    script.AddDialog(dialog);
                }

                LogManager.Default.Info($"已从文件加载NPC脚本: {scriptFile}");
                return script;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载NPC脚本文件失败 {scriptFile}: {ex.Message}");
                return null;
            }
        }

        private NPCScript? CreateDefaultScript(string scriptFile)
        {
            switch (scriptFile.ToLower())
            {
                case "weaponshop":
                    return CreateWeaponShopScript();
                case "potionshop":
                    return CreatePotionShopScript();
                case "teleporter":
                    return CreateTeleporterScript();
                default:
                    LogManager.Default.Warning($"未找到NPC脚本: {scriptFile}，使用空脚本");
                    return CreateEmptyScript(scriptFile);
            }
        }

        private NPCScript CreateWeaponShopScript()
        {
            var script = new NPCScript("WeaponShop");
            var startDialog = new NPCDialog(1, "欢迎光临武器店！我这里有各种强大的武器。");
            startDialog.AddOption(new DialogOption(1, "我想看看你的武器", "SHOP"));
            startDialog.AddOption(new DialogOption(2, "我想修理武器", "REPAIR"));
            startDialog.AddOption(new DialogOption(3, "强化武器", "ENHANCE"));
            startDialog.AddOption(new DialogOption(4, "再见", "EXIT"));
            script.AddDialog(startDialog);
            return script;
        }

        private NPCScript CreatePotionShopScript()
        {
            var script = new NPCScript("PotionShop");
            var potionDialog = new NPCDialog(1, "需要药水吗？我这里有最好的恢复药水！");
            potionDialog.AddOption(new DialogOption(1, "购买药水", "SHOP"));
            potionDialog.AddOption(new DialogOption(2, "离开", "EXIT"));
            script.AddDialog(potionDialog);
            return script;
        }

        private NPCScript CreateTeleporterScript()
        {
            var script = new NPCScript("Teleporter");
            var teleportDialog = new NPCDialog(1, "我可以把你传送到各个主要城市。");
            teleportDialog.AddOption(new DialogOption(1, "传送到银杏山谷", "TELEPORT:1"));
            teleportDialog.AddOption(new DialogOption(2, "传送到毒蛇山谷", "TELEPORT:2"));
            teleportDialog.AddOption(new DialogOption(3, "取消", "EXIT"));
            script.AddDialog(teleportDialog);
            return script;
        }

        private NPCScript CreateEmptyScript(string scriptName)
        {
            var script = new NPCScript(scriptName);
            var emptyDialog = new NPCDialog(1, $"你好，我是{scriptName}。");
            emptyDialog.AddOption(new DialogOption(1, "离开", "EXIT"));
            script.AddDialog(emptyDialog);
            return script;
        }

        public void RegisterScript(NPCScript script)
        {
            _scripts[script.ScriptName] = script;
        }

        private void InitializeDefaultScripts()
        {
            // 尝试从文件加载所有脚本
            LoadAllScriptsFromDirectory();
            
            // 如果没有加载到任何脚本，创建默认脚本
            if (_scripts.Count == 0)
            {
                RegisterScript(CreateWeaponShopScript());
                RegisterScript(CreatePotionShopScript());
                RegisterScript(CreateTeleporterScript());
            }

            LogManager.Default.Info($"已加载 {_scripts.Count} 个NPC脚本");
        }

        private void LoadAllScriptsFromDirectory()
        {
            try
            {
                if (!Directory.Exists(_scriptsDirectory))
                {
                    LogManager.Default.Warning($"NPC脚本目录不存在: {_scriptsDirectory}");
                    return;
                }

                var jsonFiles = Directory.GetFiles(_scriptsDirectory, "*.json");
                foreach (var file in jsonFiles)
                {
                    try
                    {
                        string fileName = Path.GetFileNameWithoutExtension(file);
                        var script = LoadScriptFromFile(fileName);
                        if (script != null)
                        {
                            _scripts[fileName] = script;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogManager.Default.Error($"加载脚本文件失败 {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"扫描脚本目录失败: {ex.Message}");
            }
        }
    }

    #endregion

    #region NPC管理器

    /// <summary>
    /// NPC管理器
    /// </summary>
    public class NPCManager
    {
        private static NPCManager? _instance;
        public static NPCManager Instance => _instance ??= new NPCManager();

        private readonly ConcurrentDictionary<int, NPCDefinition> _definitions = new();
        private readonly ConcurrentDictionary<uint, NPCInstance> _instances = new();
        private readonly Dictionary<int, List<uint>> _mapNPCs = new();
        private uint _nextInstanceId = 10000;

        private NPCManager()
        {
            InitializeDefaultNPCs();
        }

        private void InitializeDefaultNPCs()
        {
            // 武器商人
            var weaponShop = new NPCDefinition(1001, "武器商人", NPCType.Shop)
            {
                Title = "精良武器店",
                Functions = NPCFunction.Talk | NPCFunction.Shop | NPCFunction.Repair | NPCFunction.Enhance,
                ScriptFile = "WeaponShop",
                Greeting = "欢迎来到武器店！",
                BuyRate = 0.5f,
                SellRate = 1.2f
            };
            weaponShop.ShopItems.AddRange(new[] { 1001, 1002, 1003 }); // 木剑、铁剑、钢剑
            AddDefinition(weaponShop);

            // 防具商人
            var armorShop = new NPCDefinition(1002, "防具商人", NPCType.Shop)
            {
                Title = "坚固防具店",
                Functions = NPCFunction.Talk | NPCFunction.Shop | NPCFunction.Repair,
                ScriptFile = "ArmorShop",
                Greeting = "需要防具吗？",
                BuyRate = 0.5f,
                SellRate = 1.2f
            };
            armorShop.ShopItems.AddRange(new[] { 2001, 2002 }); // 布衣、皮甲
            AddDefinition(armorShop);

            // 药店商人
            var potionShop = new NPCDefinition(1003, "药店老板", NPCType.Shop)
            {
                Title = "万能药铺",
                Functions = NPCFunction.Talk | NPCFunction.Shop,
                ScriptFile = "PotionShop",
                Greeting = "买药水吗？",
                BuyRate = 0.3f,
                SellRate = 1.0f
            };
            potionShop.ShopItems.AddRange(new[] { 3001, 3002, 3003 }); // 各种药水
            AddDefinition(potionShop);

            // 仓库管理员
            var storage = new NPCDefinition(1004, "仓库管理员", NPCType.Storage)
            {
                Title = "安全仓库",
                Functions = NPCFunction.Talk | NPCFunction.Storage,
                Greeting = "我可以帮你保管物品。"
            };
            AddDefinition(storage);

            // 传送员
            var teleporter = new NPCDefinition(1005, "传送员", NPCType.Teleport)
            {
                Title = "城市传送",
                Functions = NPCFunction.Talk | NPCFunction.Teleport,
                ScriptFile = "Teleporter",
                Greeting = "需要传送吗？"
            };
            teleporter.Destinations.Add(new TeleportDestination("银杏山谷", 1, 200, 200, 100));
            teleporter.Destinations.Add(new TeleportDestination("毒蛇山谷", 2, 200, 200, 200));
            AddDefinition(teleporter);

            // 铁匠
            var blacksmith = new NPCDefinition(1006, "铁匠", NPCType.Blacksmith)
            {
                Title = "大师铁匠",
                Functions = NPCFunction.Talk | NPCFunction.Repair | NPCFunction.Enhance,
                Greeting = "我可以修理和强化装备。"
            };
            AddDefinition(blacksmith);

            // 技能训练师
            var trainer = new NPCDefinition(1007, "技能大师", NPCType.Trainer)
            {
                Title = "武技训练",
                Functions = NPCFunction.Talk | NPCFunction.Skill,
                Greeting = "想学习新技能吗？"
            };
            AddDefinition(trainer);

            LogManager.Default.Info($"已加载 {_definitions.Count} 个NPC定义");
        }

        public void AddDefinition(NPCDefinition definition)
        {
            _definitions[definition.NPCId] = definition;
        }

        public NPCDefinition? GetDefinition(int npcId)
        {
            _definitions.TryGetValue(npcId, out var definition);
            return definition;
        }

        public NPCInstance? CreateNPC(int npcId, int mapId, int x, int y)
        {
            var definition = GetDefinition(npcId);
            if (definition == null)
                return null;

            uint instanceId = System.Threading.Interlocked.Increment(ref _nextInstanceId);
            var npc = new NPCInstance(definition, instanceId, mapId, x, y);
            
            _instances[instanceId] = npc;

            // 添加到地图NPC列表
            if (!_mapNPCs.ContainsKey(mapId))
            {
                _mapNPCs[mapId] = new List<uint>();
            }
            _mapNPCs[mapId].Add(instanceId);

            LogManager.Default.Debug($"创建NPC: {definition.Name} 于地图{mapId} ({x},{y})");
            return npc;
        }

        public NPCInstance? GetNPC(uint instanceId)
        {
            _instances.TryGetValue(instanceId, out var npc);
            return npc;
        }

        public List<NPCInstance> GetMapNPCs(int mapId)
        {
            if (!_mapNPCs.TryGetValue(mapId, out var npcIds))
                return new List<NPCInstance>();

            return npcIds
                .Select(id => GetNPC(id))
                .Where(npc => npc != null)
                .Cast<NPCInstance>()
                .ToList();
        }

        public bool RemoveNPC(uint instanceId)
        {
            if (_instances.TryRemove(instanceId, out var npc))
            {
                if (_mapNPCs.TryGetValue(npc.MapId, out var list))
                {
                    list.Remove(instanceId);
                }
                return true;
            }
            return false;
        }

        public List<NPCDefinition> GetAllDefinitions()
        {
            return _definitions.Values.ToList();
        }

        public void InitializeMapNPCs()
        {
            // 在比奇城创建NPC
            CreateNPC(1001, 0, 300, 300); // 武器商人
            CreateNPC(1002, 0, 310, 300); // 防具商人
            CreateNPC(1003, 0, 320, 300); // 药店老板
            CreateNPC(1004, 0, 300, 310); // 仓库管理员
            CreateNPC(1005, 0, 250, 250); // 传送员
            CreateNPC(1006, 0, 330, 300); // 铁匠
            CreateNPC(1007, 0, 300, 320); // 技能训练师

            LogManager.Default.Info($"已在地图上创建 {_instances.Count} 个NPC");
        }
    }

    #endregion

    #region NPC交互管理器

    /// <summary>
    /// 玩家NPC交互会话
    /// </summary>
    public class NPCSession
    {
        public HumanPlayer Player { get; set; }
        public NPCInstance NPC { get; set; }
        public int CurrentDialogId { get; set; }
        public DateTime LastInteractTime { get; set; }

        public NPCSession(HumanPlayer player, NPCInstance npc)
        {
            Player = player;
            NPC = npc;
            CurrentDialogId = 1;
            LastInteractTime = DateTime.Now;
        }

        public bool IsTimeout(int timeoutSeconds = 60)
        {
            return (DateTime.Now - LastInteractTime).TotalSeconds > timeoutSeconds;
        }

        public void UpdateInteractTime()
        {
            LastInteractTime = DateTime.Now;
        }
    }

    /// <summary>
    /// NPC交互管理器
    /// </summary>
    public class NPCInteractionManager
    {
        private static NPCInteractionManager? _instance;
        public static NPCInteractionManager Instance => _instance ??= new NPCInteractionManager();

        private readonly ConcurrentDictionary<uint, NPCSession> _sessions = new();

        private NPCInteractionManager() { }

        public NPCSession? StartInteraction(HumanPlayer player, uint npcInstanceId)
        {
            var npc = NPCManager.Instance.GetNPC(npcInstanceId);
            if (npc == null || !npc.IsActive)
                return null;

            var session = new NPCSession(player, npc);
            _sessions[player.ObjectId] = session;

            LogManager.Default.Debug($"{player.Name} 开始与 {npc.Definition.Name} 交互");
            return session;
        }

        public void EndInteraction(uint playerId)
        {
            if (_sessions.TryRemove(playerId, out var session))
            {
                LogManager.Default.Debug($"{session.Player.Name} 结束与 {session.NPC.Definition.Name} 的交互");
            }
        }

        public NPCSession? GetSession(uint playerId)
        {
            _sessions.TryGetValue(playerId, out var session);
            return session;
        }

        public void CleanupTimeoutSessions()
        {
            var timeoutSessions = _sessions.Values
                .Where(s => s.IsTimeout())
                .ToList();

            foreach (var session in timeoutSessions)
            {
                EndInteraction(session.Player.ObjectId);
            }
        }

        /// <summary>
        /// 处理玩家选择
        /// </summary>
        public void HandlePlayerChoice(HumanPlayer player, int optionId)
        {
            var session = GetSession(player.ObjectId);
            if (session == null) return;

            session.UpdateInteractTime();

            var script = session.NPC.GetScript();
            if (script == null) return;

            var currentDialog = script.GetDialog(session.CurrentDialogId);
            if (currentDialog == null) return;

            var option = currentDialog.Options.FirstOrDefault(o => o.Id == optionId);
            if (option == null) return;

            // 处理动作
            HandleAction(player, session.NPC, option.Action);
        }

        private void HandleAction(HumanPlayer player, NPCInstance npc, string action)
        {
            if (string.IsNullOrEmpty(action)) return;

            var parts = action.Split(':');
            var actionType = parts[0];

            switch (actionType)
            {
                case "SHOP":
                    OpenShop(player, npc);
                    break;
                case "REPAIR":
                    OpenRepair(player, npc);
                    break;
                case "ENHANCE":
                    OpenEnhance(player, npc);
                    break;
                case "STORAGE":
                    OpenStorage(player, npc);
                    break;
                case "TELEPORT":
                    if (parts.Length > 1 && int.TryParse(parts[1], out int mapId))
                    {
                        TeleportPlayer(player, mapId);
                    }
                    break;
                case "EXIT":
                    EndInteraction(player.ObjectId);
                    break;
            }
        }

        private void OpenShop(HumanPlayer player, NPCInstance npc)
        {
            LogManager.Default.Info($"{player.Name} 打开 {npc.Definition.Name} 的商店");
            
            // 构建商店界面消息
            var builder = new PacketBuilder();
            builder.WriteUInt32(npc.InstanceId);
            builder.WriteUInt16(ProtocolCmd.SM_OPENSHOP);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            
            // 发送商店物品列表
            byte[] packet = builder.Build();
            player.SendMessage(packet);
        }

        private void OpenRepair(HumanPlayer player, NPCInstance npc)
        {
            LogManager.Default.Info($"{player.Name} 请求修理装备");
            
            var builder = new PacketBuilder();
            builder.WriteUInt32(npc.InstanceId);
            builder.WriteUInt16(ProtocolCmd.SM_OPENREPAIR);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            
            byte[] packet = builder.Build();
            player.SendMessage(packet);
        }

        private void OpenEnhance(HumanPlayer player, NPCInstance npc)
        {
            LogManager.Default.Info($"{player.Name} 请求强化装备");
            
            var builder = new PacketBuilder();
            builder.WriteUInt32(npc.InstanceId);
            builder.WriteUInt16(ProtocolCmd.SM_OPENENHANCE);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            
            byte[] packet = builder.Build();
            player.SendMessage(packet);
        }

        private void OpenStorage(HumanPlayer player, NPCInstance npc)
        {
            LogManager.Default.Info($"{player.Name} 打开仓库");
            
            var builder = new PacketBuilder();
            builder.WriteUInt32(npc.InstanceId);
            builder.WriteUInt16(ProtocolCmd.SM_OPENSTORAGE);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            
            byte[] packet = builder.Build();
            player.SendMessage(packet);
        }

        private void TeleportPlayer(HumanPlayer player, int mapId)
        {
            LogManager.Default.Info($"{player.Name} 传送到地图 {mapId}");
            
            // 获取传送目的地
            var npc = NPCManager.Instance.GetNPC(GetSession(player.ObjectId)?.NPC.InstanceId ?? 0);
            if (npc == null) return;
            
            var destination = npc.Definition.Destinations.FirstOrDefault(d => d.MapId == mapId);
            if (destination == null) return;
            
            // 检查费用
            if (player.Gold < destination.Cost)
            {
                player.Say($"传送需要 {destination.Cost} 金币");
                return;
            }
            
            // 扣除金币
            if (!player.TakeGold(destination.Cost))
                return;
            
            // 传送玩家
            var map = MapManager.Instance.GetMap((uint)destination.MapId);
            if (map != null)
            {
                // 使用Npc类的TeleportPlayer方法
                var npcInstance = NPCManager.Instance.GetNPC(GetSession(player.ObjectId)?.NPC.InstanceId ?? 0);
                if (npcInstance != null)
                {
                    npcInstance.TeleportPlayer(player, destination.MapId, (ushort)destination.X, (ushort)destination.Y, destination.Cost);
                }
            }
        }
    }

    #endregion

    #region 游戏服务器集成

    /// <summary>
    /// NPC系统初始化
    /// </summary>
    public static class NPCSystemInitializer
    {
        public static void Initialize()
        {
            // 初始化NPC管理器
            NPCManager.Instance.InitializeMapNPCs();
            
            // 初始化脚本管理器
            _ = NPCScriptManager.Instance;
            
            // 初始化交互管理器
            _ = NPCInteractionManager.Instance;
            
            LogManager.Default.Info("NPC系统初始化完成");
        }
    }

    /// <summary>
    /// NPC消息处理器
    /// </summary>
    public static class NPCMessageHandler
    {
        /// <summary>
        /// 处理玩家与NPC交互消息
        /// </summary>
        public static void HandleNPCInteract(HumanPlayer player, uint npcInstanceId)
        {
            var npc = NPCManager.Instance.GetNPC(npcInstanceId);
            if (npc == null)
            {
                LogManager.Default.Warning($"玩家 {player.Name} 尝试与不存在的NPC交互: {npcInstanceId}");
                return;
            }
            
            npc.OnInteract(player);
        }

        /// <summary>
        /// 处理玩家对话选择
        /// </summary>
        public static void HandleDialogChoice(HumanPlayer player, int optionId)
        {
            NPCInteractionManager.Instance.HandlePlayerChoice(player, optionId);
        }

        /// <summary>
        /// 处理商店购买
        /// </summary>
        public static void HandleShopBuy(HumanPlayer player, uint npcInstanceId, int itemIndex)
        {
            var npc = NPCManager.Instance.GetNPC(npcInstanceId);
            if (npc == null || !npc.Definition.HasFunction(NPCFunction.Shop))
                return;
            
            // TODO: 实现购买逻辑
            LogManager.Default.Info($"{player.Name} 从 {npc.Definition.Name} 购买物品 {itemIndex}");
        }

        /// <summary>
        /// 处理商店出售
        /// </summary>
        public static void HandleShopSell(HumanPlayer player, uint npcInstanceId, int bagSlot)
        {
            var npc = NPCManager.Instance.GetNPC(npcInstanceId);
            if (npc == null || !npc.Definition.HasFunction(NPCFunction.Shop))
                return;
            
            // TODO: 实现出售逻辑
            LogManager.Default.Info($"{player.Name} 向 {npc.Definition.Name} 出售物品 {bagSlot}");
        }

        /// <summary>
        /// 处理链接选择
        /// </summary>
        public static void HandleSelectLink(HumanPlayer player, uint npcInstanceId, string link)
        {
            var npc = NPCManager.Instance.GetNPC(npcInstanceId);
            if (npc == null)
                return;
            
            LogManager.Default.Info($"{player.Name} 选择链接: {link} (NPC: {npc.Definition.Name})");
            
            // 处理链接选择
            var session = NPCInteractionManager.Instance.GetSession(player.ObjectId);
            if (session != null)
            {
                // 解析链接动作
                if (link.StartsWith("dialog:"))
                {
                    if (int.TryParse(link.Substring(7), out int dialogId))
                    {
                        session.CurrentDialogId = dialogId;
                        // 发送新的对话框
                        SendDialog(player, npc, dialogId);
                    }
                }
                else if (link.StartsWith("action:"))
                {
                    string action = link.Substring(7);
                    NPCInteractionManager.Instance.HandlePlayerChoice(player, 0); // 使用0作为选项ID，因为动作已经指定
                }
            }
        }

        /// <summary>
        /// 发送对话框
        /// </summary>
        private static void SendDialog(HumanPlayer player, NPCInstance npc, int dialogId)
        {
            var script = npc.GetScript();
            if (script == null)
                return;
            
            var dialog = script.GetDialog(dialogId);
            if (dialog == null)
                return;
            
            // 构建对话框消息
            var builder = new PacketBuilder();
            builder.WriteUInt32(npc.InstanceId);
            builder.WriteUInt16(ProtocolCmd.SM_DIALOG);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteString(dialog.Text);
            
            // 添加选项
            builder.WriteByte((byte)dialog.Options.Count);
            foreach (var option in dialog.Options)
            {
                builder.WriteInt32(option.Id);
                builder.WriteString(option.Text);
            }
            
            byte[] packet = builder.Build();
            player.SendMessage(packet);
        }
    }

    #endregion
}
