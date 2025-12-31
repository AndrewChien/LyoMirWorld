using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Linq;
using MirCommon;
using MirCommon.Network;
using MirCommon.Utils;

namespace GameServer
{
    // 简单的MineSpot类占位符
    public class MineSpot : MapObject
    {
        public ItemInstance? Mine()
        {
            // 简单的挖矿逻辑
            return null;
        }

        public override ObjectType GetObjectType()
        {
            return ObjectType.Event; // 矿点作为事件对象
        }

        public override bool GetViewMsg(out byte[] msg, MapObject? viewer = null)
        {
            // 矿点不需要发送可视消息
            msg = Array.Empty<byte>();
            return false;
        }
    }

    // 简单的MonsterCorpse类占位符
    public class MonsterCorpse : MapObject
    {
        public ItemInstance? GetMeat()
        {
            // 简单的挖肉逻辑
            return null;
        }

        public override ObjectType GetObjectType()
        {
            return ObjectType.Event; // 怪物尸体作为事件对象
        }

        public override bool GetViewMsg(out byte[] msg, MapObject? viewer = null)
        {
            // 怪物尸体不需要发送可视消息
            msg = Array.Empty<byte>();
            return false;
        }
    }

    /// <summary>
    /// 挖矿奖励类型
    /// </summary>
    public enum MineRewardType
    {
        Low = 0,      // 低级矿石
        Medium = 1,   // 中级矿石
        High = 2      // 高级矿石
    }

    /// <summary>
    /// 攻击模式枚举
    /// </summary>
    public enum e_humanattackmode
    {
        HAM_PEACE = 0,      // 和平模式
        HAM_GROUP = 1,      // 组队模式
        HAM_GUILD = 2,      // 行会模式
        HAM_COUPLE = 3,     // 夫妻模式
        HAM_MASTER = 4,     // 师徒模式
        HAM_CRIME = 5,      // 犯罪模式
        HAM_ALL = 6,        // 全体模式
        HAM_SUPERMAN = 7,   // 超人模式（GM）
        HAM_MAX = 8
    }

    /// <summary>
    /// 聊天频道枚举
    /// </summary>
    public enum e_chatchannel
    {
        CCH_NORMAL = 0,     // 普通频道
        CCH_WISPER = 1,     // 密谈频道
        CCH_CRY = 2,        // 喊话频道
        CCH_GM = 3,         // GM频道
        CCH_GROUP = 4,      // 组队频道
        CCH_GUILD = 5,      // 行会频道
        CCH_MAX = 6
    }

    /// <summary>
    /// 金钱类型枚举
    /// </summary>
    public enum MoneyType
    {
        Gold = 0,    // 金币
        Yuanbao = 1  // 元宝
    }

    /// <summary>
    /// 颜色常量类
    /// </summary>
    public static class CC
    {
        public const uint GREEN = 0x00FF00;      // 绿色
        public const uint RED = 0xFF0000;        // 红色
        public const uint BLUE = 0x0000FF;       // 蓝色
        public const uint YELLOW = 0xFFFF00;     // 黄色
        public const uint WHITE = 0xFFFFFF;      // 白色
        public const uint BLACK = 0x000000;      // 黑色
        public const uint CYAN = 0x00FFFF;       // 青色
        public const uint MAGENTA = 0xFF00FF;    // 洋红色
        public const uint GRAY = 0x808080;       // 灰色
        public const uint ORANGE = 0xFFA500;     // 橙色
        public const uint PURPLE = 0x800080;     // 紫色
        public const uint BROWN = 0xA52A2A;      // 棕色
        public const uint PINK = 0xFFC0CB;       // 粉色
        public const uint GOLD = 0xFFD700;       // 金色
        public const uint SILVER = 0xC0C0C0;     // 银色
        public const uint BRONZE = 0xCD7F32;     // 青铜色
    }

    /// <summary>
    /// 玩家对象 - 完整的玩家系统
    /// </summary>
    public partial class HumanPlayer : AliveObject
    {
        // 账号信息
        public string Account { get; set; } = string.Empty;
        public uint CharDBId { get; set; }

        // 角色基础属性
        public byte Job { get; set; }       // 职业: 0=战士 1=法师 2=道士
        public byte Sex { get; set; }       // 性别: 0=男 1=女
        public byte Hair { get; set; }      // 发型
        public byte Direction { get; set; } // 方向: 0-7

        // 经验和金钱
        public uint Exp { get; set; }
        public uint Gold { get; set; }
        public uint Yuanbao { get; set; } // 元宝

        // 基础属性
        public int BaseDC { get; set; }    // 基础攻击力
        public int BaseMC { get; set; }    // 基础魔法力
        public int BaseSC { get; set; }    // 基础道术力
        public int BaseAC { get; set; }    // 基础防御力
        public int BaseMAC { get; set; }   // 基础魔防力
        public int Accuracy { get; set; }  // 准确
        public int Agility { get; set; }   // 敏捷
        public int Lucky { get; set; }     // 幸运

        // 背包和装备
        public Inventory Inventory { get; private set; }
        public Equipment Equipment { get; private set; }

        // 技能和任务
        public SkillBook SkillBook { get; private set; }
        public PlayerQuestManager QuestManager { get; private set; }

        // 网络连接
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;

        // 登录信息
        public DateTime LoginTime { get; private set; }
        public DateTime LastActivity { get; set; }
        public bool IsFirstLogin { get; set; }

        // 发送消息委托（用于通过GameClient发送编码消息）
        public delegate void SendEncodedMessageDelegate(uint dwFlag, ushort wCmd, ushort w1, ushort w2, ushort w3, byte[]? payload = null);
        private SendEncodedMessageDelegate? _sendEncodedMessage;

        // PK值
        public uint PkValue { get; set; }

        // 组队
        public uint GroupId { get; set; }

        // 行会
        public Guild? Guild { get; set; }
        public string GuildGroupName { get; set; } = string.Empty;
        public uint GuildLevel { get; set; }

        // 交易
        public TradeObject? CurrentTrade { get; set; }

        // 其他玩家交互
        private uint _tradingWithPlayerId = 0;

        // 状态标记
        private readonly HashSet<string> _flags = new();
        private readonly object _flagLock = new();

        // 变量存储（用于脚本系统）
        private readonly Dictionary<string, string> _variables = new();
        private readonly object _varLock = new();

        // 高级系统
        public PetSystem PetSystem { get; private set; }
        public MountSystem MountSystem { get; private set; }
        public PKSystem PKSystem { get; private set; }
        public AchievementSystem AchievementSystem { get; private set; }
        public MailSystem MailSystem { get; private set; }

        // 经验加成技能
        private PlayerSkill? _expMagic;

        // 称号系统
        private string _currentTitle = string.Empty;
        private int _currentTitleIndex = 0;

        // 攻击模式和聊天频道
        private e_humanattackmode _attackMode = e_humanattackmode.HAM_PEACE;
        private e_chatchannel _chatChannel = e_chatchannel.CCH_NORMAL;

        // 聊天频道禁用状态
        private bool[] _chatChannelDisabled = new bool[(int)e_chatchannel.CCH_MAX];

        // 当前密谈对象
        private string _currentWisperTarget = string.Empty;

        // 聊天颜色
        private byte _chatColor = 1;

        // 聊天频道计时器
        private readonly Dictionary<e_chatchannel, DateTime> _chatChannelTimers = new();

        public HumanPlayer(string account, string name, uint charDbId, TcpClient? client = null)
        {
            Account = account;
            Name = name;
            CharDBId = charDbId;
            _tcpClient = client;
            _stream = client?.GetStream();

            LoginTime = DateTime.Now;
            LastActivity = DateTime.Now;

            // 初始化系统
            Inventory = new Inventory { MaxSlots = 40 };
            Equipment = new Equipment(this);
            SkillBook = new SkillBook();
            QuestManager = new PlayerQuestManager(this);

            // 初始化高级系统
            PetSystem = new PetSystem(this);
            MountSystem = new MountSystem(this);
            PKSystem = new PKSystem(this);
            AchievementSystem = new AchievementSystem(this);
            MailSystem = new MailSystem(this);

            // 初始属性
            Level = 1;
            Job = 0;
            Sex = 0;
            MaxHP = 100;
            CurrentHP = 100;
            MaxMP = 100;
            CurrentMP = 100;

            // 初始战斗属性
            Stats.MinDC = 1;
            Stats.MaxDC = 3;
            Stats.Accuracy = 5;
            Stats.Agility = 5;

            // 初始化基础属性
            BaseDC = 0;
            BaseMC = 0;
            BaseSC = 0;
            BaseAC = 0;
            BaseMAC = 0;
            Accuracy = 5;
            Agility = 5;
            Lucky = 0;
        }

        /// <summary>
        /// 设置发送消息委托（用于通过GameClient发送编码消息）
        /// </summary>
        public void SetSendMessageDelegate(SendEncodedMessageDelegate sendMessageDelegate)
        {
            _sendEncodedMessage = sendMessageDelegate;
        }

        /// <summary>
        /// 初始化玩家
        /// </summary>
        public bool Init(MirCommon.CREATEHUMANDESC createDesc)
        {
            try
            {
                LogManager.Default.Info($"开始初始化玩家");
                // 设置数据库信息
                var dbinfo = createDesc.dbinfo;

                // 设置基础属性
                Level = (byte)Math.Clamp(dbinfo.wLevel, (ushort)0, (ushort)255);
                Exp = dbinfo.dwCurExp;
                Gold = dbinfo.dwGold;
                Yuanbao = dbinfo.dwYuanbao;

                // 设置位置
                X = (ushort)dbinfo.x;
                Y = (ushort)dbinfo.y;

                // 设置职业、性别、发型
                Job = dbinfo.btClass;
                Sex = dbinfo.btSex;
                Hair = dbinfo.btHair;

                // 设置HP/MP
                CurrentHP = dbinfo.hp;
                MaxHP = dbinfo.maxhp;
                CurrentMP = dbinfo.mp;
                MaxMP = dbinfo.maxmp;

                // 设置战斗属性
                Stats.MinDC = dbinfo.mindc;
                Stats.MaxDC = dbinfo.maxdc;
                Stats.MinMC = dbinfo.minmc;
                Stats.MaxMC = dbinfo.maxmc;
                Stats.MinSC = dbinfo.minsc;
                Stats.MaxSC = dbinfo.maxsc;
                Stats.MinAC = dbinfo.minac;
                Stats.MaxAC = dbinfo.maxac;
                Stats.MinMAC = dbinfo.minmac;
                Stats.MaxMAC = dbinfo.maxmac;
                Stats.Accuracy = 5; // 默认值
                Stats.Agility = 5;  // 默认值
                Stats.Lucky = 0;    // 默认值

                // 设置基础属性
                BaseDC = dbinfo.mindc;
                BaseMC = dbinfo.minmc;
                BaseSC = dbinfo.minsc;
                BaseAC = dbinfo.minac;
                BaseMAC = dbinfo.minmac;
                Accuracy = 5;
                Agility = 5;
                Lucky = 0;

                // 设置行会信息
                GuildGroupName = dbinfo.szGuildName;

                // 设置首次登录标志
                IsFirstLogin = dbinfo.dwFlag[0] == 0; 

                // 发送初始化消息
                SendInitMessages();

                // 发送状态更新
                LogManager.Default.Info($"发送状态改变消息");
                SendStatusChanged();
                LogManager.Default.Info($"发送天气改变消息");
                SendTimeWeatherChanged();
                LogManager.Default.Info($"发送组队模式消息");
                SendGroupMode();
                LogManager.Default.Info($"发送元宝更新消息");
                SendMoneyChanged(MoneyType.Yuanbao);

                // 设置初始状态
                LogManager.Default.Info($"发送攻击模式消息");
                ChangeAttackMode(e_humanattackmode.HAM_PEACE);
                SaySystemAttrib(CC.GREEN, "更改攻击模式 CTRL+H 查看攻击模式信息 @atkinfo");

                LogManager.Default.Info($"发送聊天模式消息");
                ChangeChatChannel(e_chatchannel.CCH_NORMAL);
                SaySystemAttrib(CC.GREEN, "更改频道 CTRL+S 查看频道信息 @ccinfo");

                // 更新属性
                LogManager.Default.Info($"发送属性消息");
                UpdateProp();
                LogManager.Default.Info($"发送子属性消息");
                UpdateSubProp();

                LogManager.Default.Info($"玩家初始化成功: {Name} 等级:{Level} 职业:{Job} 性别:{Sex}");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"玩家初始化失败: {Name}, 错误: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 发送初始化消息
        /// </summary>
        private void SendInitMessages()
        {
            // 发送版本信息
            LogManager.Default.Info($"发送初始化消息1");
            SendMsg(0, 0x100, 0, 0, 0, "1.0.0"); // SM_READY

            // 发送背包大小
            ushort bagSize = 40; // 默认大背包
            LogManager.Default.Info($"发送背包大小消息1");
            SendMsg((uint)ObjectId, 0x9594, 0, bagSize, 0);

            // 发送准备就绪
            LogManager.Default.Info($"发送准备就绪消息1");
            SendMsg(0xf2d505d7, 0x100, 0, 0, 0); // SM_READY

            // 发送地图信息
            var map = LogicMapMgr.Instance?.GetLogicMapById((uint)MapId);
            LogManager.Default.Info($"map = {map}");
            if (map != null)
            {
                LogManager.Default.Info($"发送设置地图消息1");
                SendMsg((uint)ObjectId, 0x101, (ushort)X, (ushort)Y, (ushort)((Sex << 8) | Direction), map.MapName); // SM_SETMAP

                // 发送玩家信息
                uint[] dwParam = { GetFeather(), 0, GetStatus(), 0 };
                LogManager.Default.Info($"发送设置玩家消息1");
                SendMsg((uint)ObjectId, 0x102, (ushort)X, (ushort)Y, (ushort)((Sex << 8) | Direction), dwParam); // SM_SETPLAYER

                // 发送玩家名称
                LogManager.Default.Info($"发送玩家名称消息1");
                SendMsg((uint)ObjectId, 0x103, GetNameColor(this), 0, 0, Name); // SM_SETPLAYERNAME

                // 发送地图名称
                LogManager.Default.Info($"发送地图名称消息1");
                SendMsg(0, 0x104, 0, 0, 0, map.MapName); // SM_SETMAPNAME

                // 发送地图战斗属性
                LogManager.Default.Info($"发送地图战斗属性消息1");
                SendMsg(map.IsFightMap() ? 1u : 0u, 0x2c4, 0, 0, 0);
            }
        }

        /// <summary>
        /// 进入地图
        /// </summary>
        public void OnEnterMap(LogicMap map)
        {
            if (map == null) return;

            // 发送地图进入消息
            SendMapEnterMessages(map);

            // 调用基类方法
            base.OnEnterMap(map);

            // 更新状态
            SendTimeWeatherChanged();
            // UpdateViewName();

            if (GetStatus() > 0)
                SendStatusChanged();

            // 特殊处理
            // - 沙城宫殿进入处理
            // - 宠物跟随处理
            // - 特殊装备刷新
        }

        /// <summary>
        /// 发送地图进入消息
        /// </summary>
        private void SendMapEnterMessages(LogicMap map)
        {
            if (map == null) return;

            // 发送地图信息
            LogManager.Default.Info($"发送设置地图消息2");
            SendMsg((uint)ObjectId, 0x101, (ushort)X, (ushort)Y, (ushort)((Sex << 8) | Direction), map.MapName); // SM_SETMAP

            // 发送玩家信息
            uint[] dwParam = { GetFeather(), 0, GetStatus(), 0 };
            LogManager.Default.Info($"发送设置玩家消息2");
            SendMsg((uint)ObjectId, 0x102, (ushort)X, (ushort)Y, (ushort)((Sex << 8) | Direction), dwParam); // SM_SETPLAYER

            // 发送玩家名称
            LogManager.Default.Info($"发送玩家名称消息2");
            SendMsg((uint)ObjectId, 0x103, GetNameColor(this), 0, 0, Name); // SM_SETPLAYERNAME

            // 发送地图名称
            LogManager.Default.Info($"发送地图名称消息2");
            SendMsg(0, 0x104, 0, 0, 0, map.MapName); // SM_SETMAPNAME

            // 发送地图战斗属性
            LogManager.Default.Info($"发送地图战斗属性消息2");
            SendMsg(map.IsFightMap() ? 1u : 0u, 0x2c4, 0, 0, 0);
        }

        /// <summary>
        /// 获取特征值
        /// </summary>
        public uint GetFeather()
        {
            // 特征值计算：根据职业、性别、发型等计算
            uint feather = 0;
            feather |= (uint)(Job << 24);
            feather |= (uint)(Sex << 16);
            feather |= (uint)(Hair << 8);
            return feather;
        }

        /// <summary>
        /// 获取状态
        /// </summary>
        public uint GetStatus()
        {
            // 状态标志计算
            return 0; // 需要根据实际状态计算
        }

        /// <summary>
        /// 获取名称颜色
        /// </summary>
        public byte GetNameColor(HumanPlayer? viewer = null)
        {
            // 根据PK值、行会关系、沙城战状态等计算名称颜色
            return 255;
        }

        /// <summary>
        /// 发送消息给玩家（封装方法）
        /// </summary>
        public void SendMsg(uint dwFlag, ushort wCmd, ushort w1, ushort w2, ushort w3, object? data = null)
        {
            try
            {
                // 如果有发送消息委托，使用委托发送编码消息
                if (_sendEncodedMessage != null)
                {
                    byte[]? payload = null;
                    if (data != null)
                    {
                        if (data is string strData)
                        {
                            payload = System.Text.Encoding.GetEncoding("GBK").GetBytes(strData);
                        }
                        else if (data is uint[] uintArray)
                        {
                            // 将uint数组转换为字节数组
                            payload = new byte[uintArray.Length * 4];
                            Buffer.BlockCopy(uintArray, 0, payload, 0, payload.Length);
                        }
                        else if (data is byte[] byteArray)
                        {
                            payload = byteArray;
                        }
                        else
                        {
                            payload = System.Text.Encoding.GetEncoding("GBK").GetBytes(data.ToString() ?? "");
                        }
                    }

                    _sendEncodedMessage(dwFlag, wCmd, w1, w2, w3, payload);
                }
                else
                {
                    // 如果没有委托，使用GameMessageHandler编码消息
                    byte[]? payload = null;
                    if (data != null)
                    {
                        if (data is string strData)
                        {
                            payload = System.Text.Encoding.GetEncoding("GBK").GetBytes(strData);
                        }
                        else if (data is uint[] uintArray)
                        {
                            // 将uint数组转换为字节数组
                            payload = new byte[uintArray.Length * 4];
                            Buffer.BlockCopy(uintArray, 0, payload, 0, payload.Length);
                        }
                        else if (data is byte[] byteArray)
                        {
                            payload = byteArray;
                        }
                        else
                        {
                            payload = System.Text.Encoding.GetEncoding("GBK").GetBytes(data.ToString() ?? "");
                        }
                    }

                    // 创建MirMsg并编码
                    var msg = new MirCommon.MirMsgOrign
                    {
                        dwFlag = dwFlag,
                        wCmd = wCmd,
                        wParam = new ushort[3] { w1, w2, w3 },
                        //data = new byte[4]
                    };

                    // 编码并发送消息
                    byte[] encodedMessage = MirCommon.Network.GameMessageHandler.EncodeGameMessageOrign(msg, payload);
                    if (encodedMessage.Length > 0)
                    {
                        SendMessage(encodedMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"SendMsg失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更改攻击模式
        /// </summary>
        public void ChangeAttackMode(e_humanattackmode newMode)
        {
            if (newMode < e_humanattackmode.HAM_PEACE || newMode >= e_humanattackmode.HAM_MAX)
                return;

            _attackMode = newMode;

            // 发送攻击模式更新消息给客户端
            SendMsg((uint)ObjectId, 0x105, (ushort)newMode, 0, 0); // SM_ATTACKMODE

            LogManager.Default.Debug($"{Name} 更改攻击模式为: {newMode}");
        }

        /// <summary>
        /// 更改聊天频道
        /// </summary>
        public void ChangeChatChannel(e_chatchannel newChannel)
        {
            if (newChannel < e_chatchannel.CCH_NORMAL || newChannel >= e_chatchannel.CCH_MAX)
                return;

            _chatChannel = newChannel;

            // 发送聊天频道更新消息给客户端
            SendMsg((uint)ObjectId, 0x106, (ushort)newChannel, 0, 0); // SM_CHATCHANNEL

            LogManager.Default.Debug($"{Name} 更改聊天频道为: {newChannel}");
        }

        /// <summary>
        /// 发送时间天气变化消息
        /// </summary>
        public void SendTimeWeatherChanged()
        {
            // 获取当前游戏世界的时间和天气
            var gameWorld = GameWorld.Instance;
            if (gameWorld == null)
                return;

            // 发送时间信息
            var currentTime = DateTime.Now;
            SendMsg((uint)ObjectId, 0x107, (ushort)currentTime.Hour, (ushort)currentTime.Minute, 0); // SM_GAMETIME

            // 发送天气信息
            SendMsg((uint)ObjectId, 0x108, 0, 0, 0); // SM_WEATHER

            LogManager.Default.Debug($"{Name} 收到时间天气更新");
        }

        /// <summary>
        /// 发送组队模式消息
        /// </summary>
        public void SendGroupMode()
        {
            // 发送组队信息
            if (GroupId > 0)
            {
                // 有组队，发送组队信息
                SendMsg((uint)ObjectId, 0x109, 1, 0, 0, GroupId.ToString()); // SM_GROUPMODE
            }
            else
            {
                // 无组队
                SendMsg((uint)ObjectId, 0x109, 0, 0, 0); // SM_GROUPMODE
            }

            LogManager.Default.Debug($"{Name} 收到组队模式更新");
        }

        /// <summary>
        /// 发送金钱变化消息
        /// </summary>
        public void SendMoneyChanged(MoneyType moneyType)
        {
            uint amount = 0;
            ushort cmd = 0;

            switch (moneyType)
            {
                case MoneyType.Gold:
                    amount = Gold;
                    cmd = 0x10A; // SM_GOLDCHANGED
                    break;
                case MoneyType.Yuanbao:
                    amount = Yuanbao;
                    cmd = 0x10B; // SM_YUANBAOCHANGED
                    break;
                default:
                    return;
            }

            SendMsg((uint)ObjectId, cmd, (ushort)(amount & 0xFFFF), (ushort)(amount >> 16), 0);

            LogManager.Default.Debug($"{Name} {moneyType} 更新为: {amount}");
        }

        public override ObjectType GetObjectType() => ObjectType.Player;

        public override void Update()
        {
            base.Update();

            // 检查断线
            if (_tcpClient != null && !_tcpClient.Connected)
            {
                OnDisconnected();
                return;
            }

            // 更新任务系统
            QuestManager.Update();

            // 更新PK系统
            PKSystem.Update();

            // 更新最后活动时间
            LastActivity = DateTime.Now;

            // 检查动作完成
            if (CompleteAction())
            {
                // 动作完成后的处理
            }
        }

        #region 网络消息

        /// <summary>
        /// 发送消息给玩家
        /// </summary>
        public override void SendMessage(byte[] message)
        {
            if (_stream == null || !_tcpClient!.Connected)
                return;

            try
            {
                _stream.Write(message, 0, message.Length);
                _stream.Flush();
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送消息失败: {ex.Message}");
                OnDisconnected();
            }
        }

        /// <summary>
        /// 发送协议消息
        /// </summary>
        public void SendProtocolMsg(ushort cmd, byte[] data)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(cmd);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            if (data.Length > 0)
            {
                builder.WriteBytes(data);
            }

            SendMessage(builder.Build());
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        private void OnDisconnected()
        {
            try
            {
                _stream?.Close();
                _tcpClient?.Close();
            }
            catch { }

            _stream = null;
            _tcpClient = null;

            // 从地图移除
            CurrentMap?.RemoveObject(this);

            LogManager.Default.Info($"玩家断开连接: {Name}");
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 检查是否在范围内
        /// </summary>
        private bool IsInRange(GameObject target, int range)
        {
            if (target == null || CurrentMap == null)
                return false;

            // 将GameObject转换为MapObject来获取坐标
            if (target is MapObject mapObject)
            {
                int distanceX = Math.Abs(X - mapObject.X);
                int distanceY = Math.Abs(Y - mapObject.Y);
                return distanceX <= range && distanceY <= range;
            }

            // 如果target不是MapObject，返回false
            return false;
        }

        /// <summary>
        /// 说话
        /// </summary>
        public override void Say(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            // 检查聊天冷却时间
            if (!CheckChatCooldown(MirCommon.ChatChannel.WORLD))
                return;

            // 使用聊天过滤器处理消息
            string processedMessage = ChatFilter.Instance.ProcessChatMessage(message);

            // 检查是否可以发送消息
            if (!ChatFilter.Instance.CanSendMessage(processedMessage, out string reason))
            {
                SaySystem($"无法发送消息：{reason}");
                return;
            }

            // 记录日志
            LogManager.Default.Info($"{Name}: {processedMessage}");

            // 发送聊天消息给附近玩家
            SendChatMessage(MirCommon.ChatChannel.WORLD, processedMessage, null);
        }

        /// <summary>
        /// 系统消息（发送给玩家自己）
        /// </summary>
        public void SaySystem(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            LogManager.Default.Info($"[系统] {Name}: {message}");

            // 发送系统消息给玩家自己
            SendSystemMessage(message);
        }

        /// <summary>
        /// 发送聊天消息
        /// </summary>
        private void SendChatMessage(MirCommon.ChatChannel channel, string message, string? targetName)
        {
            if (CurrentMap == null)
                return;

            // 构建完整的聊天文本
            string fullMessage = $"{Name}: {message}";

            // 根据频道发送消息
            switch (channel)
            {
                case MirCommon.ChatChannel.WORLD:
                    // 发送给附近玩家
                    SendToNearbyPlayers(fullMessage, channel);
                    break;

                case MirCommon.ChatChannel.PRIVATE:
                    // 密谈频道
                    if (!string.IsNullOrEmpty(targetName))
                    {
                        SendWisperMessage(targetName, message);
                    }
                    else if (!string.IsNullOrEmpty(_currentWisperTarget))
                    {
                        SendWisperMessage(_currentWisperTarget, message);
                    }
                    else
                    {
                        SaySystem("当前密谈对象为空，无法密谈！");
                    }
                    break;

                case MirCommon.ChatChannel.HORN:
                    // 喊话频道（全地图可见）
                    SendToMapPlayers(fullMessage, channel);
                    break;

                case MirCommon.ChatChannel.TEAM:
                    // 组队频道
                    SendToGroupMembers(fullMessage);
                    break;

                case MirCommon.ChatChannel.GUILD:
                    // 行会频道
                    SendToGuildMembers(fullMessage);
                    break;
            }

            // 更新聊天计时器
            UpdateChatTimer(channel);
        }

        /// <summary>
        /// 检查聊天冷却时间
        /// </summary>
        private bool CheckChatCooldown(MirCommon.ChatChannel channel)
        {
            // 获取频道冷却时间
            int cooldownSeconds = GetChannelCooldown(channel);

            // 将MirCommon.ChatChannel转换为e_chatchannel
            e_chatchannel eChannel = ConvertToEChannel(channel);

            if (_chatChannelTimers.TryGetValue(eChannel, out var lastChatTime))
            {
                var elapsed = DateTime.Now - lastChatTime;
                if (elapsed.TotalSeconds < cooldownSeconds)
                {
                    int remaining = cooldownSeconds - (int)elapsed.TotalSeconds;
                    SaySystem($"{GetChannelName(channel)}频道 {remaining} 秒后才能继续发言！");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 获取频道冷却时间
        /// </summary>
        private int GetChannelCooldown(MirCommon.ChatChannel channel)
        {
            // 这里应该从服务器配置读取
            return channel switch
            {
                MirCommon.ChatChannel.WORLD => 1,    // 1秒
                MirCommon.ChatChannel.HORN => 10,      // 10秒
                MirCommon.ChatChannel.TEAM => 1,     // 1秒
                MirCommon.ChatChannel.GUILD => 1,     // 1秒
                MirCommon.ChatChannel.PRIVATE => 1,    // 1秒
                _ => 1
            };
        }

        /// <summary>
        /// 获取频道名称
        /// </summary>
        private string GetChannelName(MirCommon.ChatChannel channel)
        {
            return channel switch
            {
                MirCommon.ChatChannel.WORLD => "普通",
                MirCommon.ChatChannel.HORN => "喊话",
                MirCommon.ChatChannel.TEAM => "组队",
                MirCommon.ChatChannel.GUILD => "行会",
                MirCommon.ChatChannel.PRIVATE => "密谈",
                _ => "未知"
            };
        }

        /// <summary>
        /// 更新聊天计时器
        /// </summary>
        private void UpdateChatTimer(MirCommon.ChatChannel channel)
        {
            // 将MirCommon.ChatChannel转换为e_chatchannel
            e_chatchannel eChannel = ConvertToEChannel(channel);
            _chatChannelTimers[eChannel] = DateTime.Now;
        }

        /// <summary>
        /// 将MirCommon.ChatChannel转换为e_chatchannel
        /// </summary>
        private e_chatchannel ConvertToEChannel(MirCommon.ChatChannel channel)
        {
            // 简单的映射转换
            return channel switch
            {
                MirCommon.ChatChannel.WORLD => e_chatchannel.CCH_NORMAL,
                MirCommon.ChatChannel.PRIVATE => e_chatchannel.CCH_WISPER,
                MirCommon.ChatChannel.HORN => e_chatchannel.CCH_CRY,
                MirCommon.ChatChannel.TEAM => e_chatchannel.CCH_GROUP,
                MirCommon.ChatChannel.GUILD => e_chatchannel.CCH_GUILD,
                _ => e_chatchannel.CCH_NORMAL
            };
        }

        /// <summary>
        /// 发送给附近玩家
        /// </summary>
        private void SendToNearbyPlayers(string message, MirCommon.ChatChannel channel)
        {
            if (CurrentMap == null)
                return;

            // 获取附近玩家
            var nearbyPlayers = CurrentMap.GetObjectsInRange(X, Y, 10)
                .Where(obj => obj is HumanPlayer && obj != this)
                .Cast<HumanPlayer>();

            // 构建消息数据
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x64); // SM_CHAT
            builder.WriteUInt16(0x9700); // 属性
            builder.WriteUInt16(0x38); // 颜色
            builder.WriteUInt16(0x100); // 标志
            builder.WriteString(message);

            byte[] packet = builder.Build();

            // 发送给每个附近玩家
            foreach (var player in nearbyPlayers)
            {
                // 检查玩家是否禁用了该频道
                if (!player.IsChannelDisabled(channel))
                {
                    player.SendMessage(packet);
                }
            }

            // 也发送给自己
            SendMessage(packet);
        }

        /// <summary>
        /// 发送密谈消息
        /// </summary>
        private void SendWisperMessage(string targetName, string message)
        {
            // 查找目标玩家
            var targetPlayer = HumanPlayerMgr.Instance.FindByName(targetName);

            if (targetPlayer == null)
            {
                SaySystem($"{targetName} 目前不在线，无法密谈！");
                return;
            }

            if (targetPlayer == this)
            {
                SaySystem("你干吗要自言自语呢？");
                return;
            }

            // 检查目标是否禁用了密谈频道
            if (targetPlayer.IsChannelDisabled(MirCommon.ChatChannel.PRIVATE))
            {
                SaySystem("对方关闭了密谈频道，请稍候再试！");
                return;
            }

            // 发送密谈消息
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x67); // SM_WISPER
            builder.WriteUInt16(0xfffc); // 属性
            builder.WriteUInt16(0);
            builder.WriteUInt16(1); // 标志
            builder.WriteString($"{Name}=>{message}");

            targetPlayer.SendMessage(builder.Build());

            // 设置当前密谈对象
            _currentWisperTarget = targetName;
        }

        /// <summary>
        /// 发送给地图所有玩家（喊话频道）
        /// </summary>
        private void SendToMapPlayers(string message, MirCommon.ChatChannel channel)
        {
            if (CurrentMap == null)
                return;

            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x64); // SM_CHAT
            builder.WriteUInt16(0x9700); // 属性
            builder.WriteUInt16(0x38); // 颜色
            builder.WriteUInt16(0x100); // 标志
            builder.WriteString($"(!){message}");

            byte[] packet = builder.Build();

            // 发送给附近玩家代替全地图玩家
            var nearbyPlayers = CurrentMap.GetObjectsInRange(X, Y, 20)
                .Where(obj => obj is HumanPlayer)
                .Cast<HumanPlayer>();

            foreach (var player in nearbyPlayers)
            {
                if (!player.IsChannelDisabled(channel))
                {
                    player.SendMessage(packet);
                }
            }
        }

        /// <summary>
        /// 发送给组队成员
        /// </summary>
        private void SendToGroupMembers(string message)
        {
            if (GroupId == 0)
            {
                SaySystem("没有在编组内，组队频道发言无效！");
                return;
            }

            // 使用新的组队系统发送消息
            var group = GroupObjectManager.Instance?.GetPlayerGroup(this);
            if (group == null)
            {
                SaySystem("组队信息错误，无法发送组队消息");
                return;
            }

            group.SendChatMessage(this, message);
        }

        /// <summary>
        /// 发送给行会成员
        /// </summary>
        private void SendToGuildMembers(string message)
        {
            if (Guild == null)
            {
                SaySystem("没有加入行会，行会频道发言无效！");
                return;
            }

            // 这里需要根据实际的行会系统来完善
            SaySystem("行会频道功能暂未完全实现");
        }

        /// <summary>
        /// 发送系统消息给玩家
        /// </summary>
        private void SendSystemMessage(string message)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x64); // SM_CHAT
            builder.WriteUInt16(0xff00); // 系统消息属性
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteString($"[系统] {message}");

            SendMessage(builder.Build());
        }

        /// <summary>
        /// 检查是否禁用了指定频道
        /// </summary>
        private bool IsChannelDisabled(MirCommon.ChatChannel channel)
        {
            // 将MirCommon.ChatChannel转换为e_chatchannel
            e_chatchannel eChannel = ConvertToEChannel(channel);

            // 检查频道是否被禁用
            if ((int)eChannel < _chatChannelDisabled.Length)
            {
                return _chatChannelDisabled[(int)eChannel];
            }

            // 如果索引超出范围，返回false
            return false;
        }

        /// <summary>
        /// 发送组队解散消息
        /// </summary>
        public void SendGroupDestroyed()
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x28F); // SM_GROUPDESTROYED
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);

            SendMessage(builder.Build());
        }

        /// <summary>
        /// 发送系统消息（带属性）
        /// </summary>
        public void SaySystemAttrib(uint attrib, string message)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x64); // SM_SYSCHAT
            builder.WriteUInt16((ushort)(attrib & 0xFFFF));
            builder.WriteUInt16((ushort)(attrib >> 16));
            builder.WriteUInt16(0);
            builder.WriteString(message);

            SendMessage(builder.Build());
        }


        /// <summary>
        /// 检查是否在范围内
        /// </summary>
        private bool IsInRange(MapObject target, int range)
        {
            if (target == null || CurrentMap != target.CurrentMap)
                return false;

            int dx = Math.Abs(X - target.X);
            int dy = Math.Abs(Y - target.Y);
            return dx <= range && dy <= range;
        }

        /// <summary>
        /// 计算修理费用
        /// </summary>
        private uint CalculateRepairCost(ItemInstance item)
        {
            if (item == null)
                return 0;

            // 基础修理费用 = 物品售价 * 耐久度损失比例
            float durabilityLossRatio = 1.0f - ((float)item.Durability / item.MaxDurability);
            uint baseCost = (uint)(item.Definition.SellPrice * durabilityLossRatio);

            // 强化等级增加修理费用
            if (item.EnhanceLevel > 0)
                baseCost += (uint)(baseCost * item.EnhanceLevel * 0.1f);

            return Math.Max(10, baseCost); // 最低10金币
        }

        /// <summary>
        /// 发送背包更新消息
        /// </summary>
        private void SendInventoryUpdate()
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x288); // SM_INVENTORYUPDATE
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);

            // 添加背包物品信息
            var allItems = Inventory.GetAllItems();
            builder.WriteByte((byte)allItems.Count);

            foreach (var kvp in allItems)
            {
                var item = kvp.Value;
                builder.WriteByte((byte)kvp.Key); // 槽位
                builder.WriteUInt32((uint)item.InstanceId);
                builder.WriteInt32(item.ItemId);
                builder.WriteString(item.Definition.Name);
                builder.WriteUInt16((ushort)item.Count);
                builder.WriteUInt16((ushort)item.Durability);
                builder.WriteUInt16((ushort)item.MaxDurability);
            }

            SendMessage(builder.Build());
        }

        /// <summary>
        /// 发送装备更新消息
        /// </summary>
        private void SendEquipmentUpdate(EquipSlot slot, ItemInstance? item)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x287); // SM_EQUIPMENTUPDATE
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteByte((byte)slot);

            if (item != null)
            {
                builder.WriteUInt64((ulong)item.InstanceId);
                builder.WriteInt32(item.ItemId);
                builder.WriteString(item.Definition.Name);
                builder.WriteUInt16((ushort)item.Durability);
                builder.WriteUInt16((ushort)item.MaxDurability);
                builder.WriteByte((byte)item.EnhanceLevel);
            }
            else
            {
                builder.WriteUInt64(0);
                builder.WriteInt32(0);
                builder.WriteString("");
                builder.WriteUInt16(0);
                builder.WriteUInt16(0);
                builder.WriteByte(0);
            }

            SendMessage(builder.Build());
        }

        /// <summary>
        /// 发送HP/MP更新消息
        /// </summary>
        private void SendHPMPUpdate()
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x286); // SM_HPMPUPDATE
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16((ushort)CurrentHP);
            builder.WriteUInt16((ushort)MaxHP);
            builder.WriteUInt16((ushort)CurrentMP);
            builder.WriteUInt16((ushort)MaxMP);

            SendMessage(builder.Build());
        }

        /// <summary>
        /// 完善治疗方法，添加HP更新消息
        /// </summary>
        public override void Heal(int amount)
        {
            base.Heal(amount);
            SendHPMPUpdate();
        }

        /// <summary>
        /// 完善恢复MP方法，添加MP更新消息
        /// </summary>
        public override void RestoreMP(int amount)
        {
            base.RestoreMP(amount);
            SendHPMPUpdate();
        }

        /// <summary>
        /// 完善挖矿逻辑
        /// </summary>
        public bool DoMine(byte direction)
        {
            // 检查挖矿冷却时间
            if ((DateTime.Now - _lastMineTime).TotalMilliseconds < 800)
            {
                Say("挖矿太快了，请稍等");
                return false;
            }

            // 检查是否可以执行攻击动作
            if (!CanDoAttack())
            {
                Say("无法执行挖矿动作");
                return false;
            }

            Direction = direction;

            // 设置攻击动作
            SetAttackAction();

            // 检查地图标志
            if (CurrentMap is LogicMap logicMap)
            {
                // 检查地图是否允许挖矿
                if (!logicMap.IsFlagSeted(MapFlag.MF_MINE))
                {
                    Say("这个地图不能挖矿");
                    return false;
                }
            }

            // 增加挖矿计数器
            _mineCounter++;

            // 更新挖矿效果
            UpdateMineEffect();

            // 根据挖矿计数器判断是否获得矿石
            if (_mineCounter % 10 == 0)
            {
                // 每10次挖矿获得高级矿石
                Say("挖到了高级矿石！");
                GiveMineReward(MineRewardType.High);
            }
            else if (_mineCounter % 5 == 0)
            {
                // 每5次挖矿获得中级矿石
                Say("挖到了中级矿石！");
                GiveMineReward(MineRewardType.Medium);
            }
            else
            {
                // 普通挖矿
                Say("挖到了普通矿石！");
                GiveMineReward(MineRewardType.Low);
            }

            // 更新挖矿时间
            _lastMineTime = DateTime.Now;

            return true;
        }

        /// <summary>
        /// 检查是否可以执行攻击动作
        /// </summary>
        private bool CanDoAttack()
        {
            // 检查是否在战斗中
            if (IsInCombat())
            {
                Say("战斗中无法挖矿");
                return false;
            }

            // 检查是否在摆摊中
            if (IsInPrivateShop())
            {
                Say("摆摊中无法挖矿");
                return false;
            }

            // 检查是否在安全区
            if (InSafeArea())
            {
                Say("安全区无法挖矿");
                return false;
            }

            // 检查是否有足够的体力
            if (CurrentHP < 10)
            {
                Say("体力不足，无法挖矿");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 设置攻击动作
        /// </summary>
        private void SetAttackAction()
        {
            // 设置当前动作为攻击
            StartAction(ActionType.Attack, 0);

            // 发送攻击动作消息给附近玩家
            SendAttackActionMessage();
        }

        /// <summary>
        /// 更新挖矿效果
        /// </summary>
        private void UpdateMineEffect()
        {
            // 发送挖矿特效消息
            SendMineEffectMessage();

            // 播放挖矿音效
            PlayMineSound();

            // 减少少量体力（挖矿消耗体力）
            int staminaCost = 1;
            CurrentHP = Math.Max(0, CurrentHP - staminaCost);

            // 发送HP更新消息
            SendHPMPUpdate();
        }

        /// <summary>
        /// 给予挖矿奖励
        /// </summary>
        private void GiveMineReward(MineRewardType rewardType)
        {
            // 根据奖励类型创建不同的矿石
            ItemDefinition definition;
            switch (rewardType)
            {
                case MineRewardType.High:
                    definition = new ItemDefinition(4002, "金矿石", ItemType.Material);
                    definition.SellPrice = 500;
                    break;
                case MineRewardType.Medium:
                    definition = new ItemDefinition(4001, "银矿石", ItemType.Material);
                    definition.SellPrice = 200;
                    break;
                case MineRewardType.Low:
                default:
                    definition = new ItemDefinition(4000, "铁矿石", ItemType.Material);
                    definition.SellPrice = 50;
                    break;
            }

            // 创建物品实例
            var item = new ItemInstance(definition, (long)DateTime.Now.Ticks);

            // 添加到背包
            if (Inventory.AddItem(item))
            {
                // 记录日志
                LogManager.Default.Info($"{Name} 挖到了 {definition.Name}");

                // 增加挖矿技能经验
                AddMiningSkillExp(10);
            }
            else
            {
                Say("背包已满，矿石掉落到地上");

                // 创建地图物品
                if (CurrentMap != null)
                {
                    var mapItem = new MapItem(item)
                    {
                        OwnerPlayerId = ObjectId
                    };

                    // 放置在玩家脚下
                    CurrentMap.AddObject(mapItem, X, Y);
                }
            }
        }

        /// <summary>
        /// 发送攻击动作消息
        /// </summary>
        private void SendAttackActionMessage()
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x28F); // SM_ATTACKACTION
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteByte(Direction);

            SendMessage(builder.Build());
        }

        /// <summary>
        /// 发送挖矿特效消息
        /// </summary>
        private void SendMineEffectMessage()
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x290); // SM_MINEEFFECT
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteByte(Direction);

            SendMessage(builder.Build());
        }

        /// <summary>
        /// 播放挖矿音效
        /// </summary>
        private void PlayMineSound()
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x291); // SM_MINESOUND
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(1001); // 挖矿音效ID

            SendMessage(builder.Build());
        }

        /// <summary>
        /// 增加挖矿技能经验
        /// </summary>
        private void AddMiningSkillExp(int exp)
        {
            // 检查是否有挖矿技能
            var miningSkill = SkillBook.GetSkill(1001); // 假设1001是挖矿技能ID
            if (miningSkill != null)
            {
                miningSkill.AddExp(exp);

                // 检查技能升级
                if (miningSkill.CanLevelUp())
                {
                    miningSkill.LevelUp();
                    Say($"挖矿技能升级到 {miningSkill.Level} 级！");
                }
            }
        }

        /// <summary>
        /// 完善挖肉逻辑
        /// </summary>
        public bool GetMeal(byte direction)
        {
            Direction = direction;

            // 根据方向计算目标位置
            int targetX = X;
            int targetY = Y;

            switch (direction)
            {
                case 0: targetY--; break; // 上
                case 1: targetX++; targetY--; break; // 右上
                case 2: targetX++; break; // 右
                case 3: targetX++; targetY++; break; // 右下
                case 4: targetY++; break; // 下
                case 5: targetX--; targetY++; break; // 左下
                case 6: targetX--; break; // 左
                case 7: targetX--; targetY--; break; // 左上
            }

            // 检查目标位置是否有怪物尸体
            if (CurrentMap == null)
                return false;

            var corpse = CurrentMap.GetObjectAt(targetX, targetY) as MonsterCorpse;
            if (corpse == null)
            {
                Say("这里没有怪物尸体");
                return false;
            }

            // 执行挖肉
            return GetMeat(corpse);
        }

        /// <summary>
        /// 实现训练马匹逻辑
        /// </summary>
        public bool DoTrainHorse(byte direction)
        {
            Direction = direction;

            // 检查是否有坐骑
            var mount = Equipment.GetEquipment(EquipSlot.Mount);
            if (mount == null)
            {
                Say("你没有坐骑");
                return false;
            }

            // 检查坐骑是否需要训练
            if (mount.Durability >= mount.MaxDurability)
            {
                Say("坐骑不需要训练");
                return false;
            }

            // 检查金币是否足够
            uint trainCost = 100; // 训练费用
            if (Gold < trainCost)
            {
                Say($"训练需要 {trainCost} 金币");
                return false;
            }

            // 扣除金币
            if (!TakeGold(trainCost))
                return false;

            // 训练坐骑（增加耐久度）
            mount.Durability = Math.Min(mount.Durability + 10, mount.MaxDurability);

            // 记录日志
            LogManager.Default.Info($"{Name} 训练了坐骑，花费 {trainCost} 金币");

            // 发送训练成功消息
            SaySystem($"训练了坐骑，花费 {trainCost} 金币");

            // 发送装备更新消息
            SendEquipmentUpdate(EquipSlot.Mount, mount);

            return true;
        }

        /// <summary>
        /// 实现动作系统 - 开始动作
        /// </summary>
        private void StartAction(ActionType actionType, uint targetId)
        {
            // 设置当前动作
            _currentAction = actionType;
            _currentActionTarget = targetId;
            _actionStartTime = DateTime.Now;

            // 发送动作开始消息
            SendActionStart(actionType, targetId);
        }

        /// <summary>
        /// 实现动作系统 - 完成动作检查
        /// </summary>
        public override bool CompleteAction()
        {
            if (_currentAction == ActionType.None)
                return false;

            // 检查动作是否完成（根据动作类型和经过的时间）
            var elapsed = DateTime.Now - _actionStartTime;
            bool isComplete = false;

            switch (_currentAction)
            {
                case ActionType.Mining:
                    isComplete = elapsed.TotalSeconds >= 3.0; // 挖矿需要3秒
                    break;
                case ActionType.GetMeat:
                    isComplete = elapsed.TotalSeconds >= 2.0; // 挖肉需要2秒
                    break;
                default:
                    isComplete = elapsed.TotalSeconds >= 1.0; // 默认1秒
                    break;
            }

            if (isComplete)
            {
                // 执行动作完成逻辑
                switch (_currentAction)
                {
                    case ActionType.Mining:
                        CompleteMining(_currentActionTarget);
                        break;
                    case ActionType.GetMeat:
                        CompleteGetMeat(_currentActionTarget);
                        break;
                }

                // 清除动作状态
                _currentAction = ActionType.None;
                _currentActionTarget = 0;

                // 发送动作完成消息
                SendActionComplete();

                return true;
            }

            return false;
        }

        /// <summary>
        /// 实现过程系统 - 添加过程
        /// </summary>
        private void AddProcess(ProcessType processType, uint param1, uint param2)
        {
            // 创建过程对象
            // 注意：需要将ProcessType转换为GlobeProcessType
            var process = new GlobeProcess(GlobeProcessType.None, param1, param2)
            {
                // 使用构造函数已经设置了Type、Param1、Param2
            };

            // 添加到全局进程队列
            // GameWorld.Instance?.AddProcess(process); 

            // 发送过程开始消息
            SendProcessStart(processType, param1, param2);
        }

        /// <summary>
        /// 发送动作开始消息
        /// </summary>
        private void SendActionStart(ActionType actionType, uint targetId)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x28B); // SM_ACTIONSTART
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteByte((byte)actionType);
            builder.WriteUInt32(targetId);

            SendMessage(builder.Build());
        }

        /// <summary>
        /// 发送动作完成消息
        /// </summary>
        private void SendActionComplete()
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x28C); // SM_ACTIONCOMPLETE
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);

            SendMessage(builder.Build());
        }

        /// <summary>
        /// 发送过程开始消息
        /// </summary>
        private void SendProcessStart(ProcessType processType, uint param1, uint param2)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x28E); // SM_PROCESSSTART
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteByte((byte)processType);
            builder.WriteUInt32(param1);
            builder.WriteUInt32(param2);

            SendMessage(builder.Build());
        }

        // 动作系统相关字段
        private ActionType _currentAction = ActionType.None;
        private uint _currentActionTarget = 0;
        private DateTime _actionStartTime = DateTime.MinValue;

        #endregion

        #region 系统标志和状态方法

        /// <summary>
        /// 设置系统标志
        /// </summary>
        public void SetSystemFlag(int flag, bool value)
        {
            lock (_flagLock)
            {
                string flagKey = $"SF_{flag}";
                if (value)
                {
                    _flags.Add(flagKey);
                }
                else
                {
                    _flags.Remove(flagKey);
                }
            }
        }

        /// <summary>
        /// 获取系统标志
        /// </summary>
        public bool GetSystemFlag(int flag)
        {
            lock (_flagLock)
            {
                string flagKey = $"SF_{flag}";
                return _flags.Contains(flagKey);
            }
        }

        /// <summary>
        /// 获取数据库ID
        /// </summary>
        public uint GetDBId()
        {
            return CharDBId;
        }

        /// <summary>
        /// 获取背包
        /// </summary>
        public Inventory GetBag()
        {
            return Inventory;
        }

        /// <summary>
        /// 获取装备信息
        /// </summary>
        public int GetEquipments(MirCommon.EQUIPMENT[] equipments)
        {
            if (equipments == null || equipments.Length < 20)
                return 0;

            // 获取所有装备
            var allEquipment = Equipment.GetAllEquipment();
            int count = 0;

            foreach (var equip in allEquipment)
            {
                if (count >= equipments.Length)
                    break;

                // 创建EQUIPMENT结构体
                var equipment = new MirCommon.EQUIPMENT();
                // 注意：ItemInstance可能没有Slot属性，使用默认值0
                equipment.pos = 0; // (ushort)equip.Slot;

                // 创建ITEMCLIENT结构体
                var itemClient = new MirCommon.ITEMCLIENT();
                // 注意：ItemInstance可能没有InstanceId属性，使用默认值0
                itemClient.dwMakeIndex = 0; // (uint)equip.InstanceId;

                // 设置基础物品信息
                // 注意：ItemInstance可能没有ItemId属性，使用默认值0
                itemClient.baseitem.wImageIndex = 0; // (ushort)equip.ItemId;
                // 注意：ItemInstance可能没有Durability和MaxDurability属性，使用默认值0
                itemClient.wCurDura = 0; // (ushort)equip.Durability;
                itemClient.wMaxDura = 0; // (ushort)equip.MaxDurability;

                equipment.item = itemClient;
                equipments[count] = equipment;
                count++;
            }

            return count;
        }

        /// <summary>
        /// 发送特征变化消息
        /// </summary>
        public void SendFeatureChanged()
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x28F); // SM_FEATURECHANGED
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(GetFeather());

            SendMessage(builder.Build());
        }

        /// <summary>
        /// 更新属性
        /// </summary>
        public void UpdateProp()
        {
            // 重新计算总属性
            RecalcTotalStats();

            // 发送属性更新消息
            SendStatsChanged();
        }

        /// <summary>
        /// 更新子属性
        /// </summary>
        public void UpdateSubProp()
        {
            // 更新子属性（如准确、敏捷、幸运等）
            // 这里可以添加子属性的重新计算逻辑

            // 发送子属性更新消息
            SendSubPropChanged();
        }

        /// <summary>
        /// 发送状态变化消息
        /// </summary>
        public void SendStatusChanged()
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x290); // SM_STATUSCHANGED
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);

            // 写入状态信息
            builder.WriteUInt16((ushort)CurrentHP);
            builder.WriteUInt16((ushort)MaxHP);
            builder.WriteUInt16((ushort)CurrentMP);
            builder.WriteUInt16((ushort)MaxMP);
            builder.WriteUInt16((ushort)Stats.MinDC);
            builder.WriteUInt16((ushort)Stats.MaxDC);
            builder.WriteUInt16((ushort)Stats.MinAC);
            builder.WriteUInt16((ushort)Stats.MaxAC);
            builder.WriteUInt16((ushort)Stats.MinMAC);
            builder.WriteUInt16((ushort)Stats.MaxMAC);
            builder.WriteUInt16((ushort)Stats.Accuracy);
            builder.WriteUInt16((ushort)Stats.Agility);
            builder.WriteUInt16((ushort)Stats.Lucky);

            SendMessage(builder.Build());
        }

        /// <summary>
        /// 发送子属性变化消息
        /// </summary>
        private void SendSubPropChanged()
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x291); // SM_SUBPROPCHANGED
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);

            // 写入子属性信息
            builder.WriteUInt16((ushort)Accuracy);
            builder.WriteUInt16((ushort)Agility);
            builder.WriteUInt16((ushort)Lucky);

            SendMessage(builder.Build());
        }

        /// <summary>
        /// 发送属性变化消息
        /// </summary>
        private void SendStatsChanged()
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x292); // SM_STATSCHANGED
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);

            // 写入属性信息
            builder.WriteUInt16((ushort)BaseDC);
            builder.WriteUInt16((ushort)BaseMC);
            builder.WriteUInt16((ushort)BaseSC);
            builder.WriteUInt16((ushort)BaseAC);
            builder.WriteUInt16((ushort)BaseMAC);

            SendMessage(builder.Build());
        }

        /// <summary>
        /// 检查是否是首次登录
        /// </summary>
        public bool CheckIsFirstLogin()
        {
            return IsFirstLogin;
        }

        /// <summary>
        /// 添加过程
        /// </summary>
        public void AddProcess(int processType, uint param1, uint param2, uint param3, uint param4, uint param5, uint param6, object? data)
        {
            // 创建过程对象
            var process = new GlobeProcess((GlobeProcessType)processType, param1, param2, param3, param4, param5, (int)param6, data?.ToString());

            // 添加到全局进程队列
            // GameWorld.Instance?.AddProcess(process); 

            // 发送过程开始消息
            SendProcessStart((ProcessType)processType, param1, param2);
        }

        /// <summary>
        /// 加载变量
        /// </summary>
        public void LoadVars()
        {
            // 这里应该从数据库加载玩家变量
        }

        /// <summary>
        /// 设置背包物品位置
        /// </summary>
        public void SetBagItemPos(MirCommon.BAGITEMPOS[] itempos, int count)
        {
            // 这里应该设置背包物品的位置
        }

        #endregion

        #region 数据库消息处理方法

        /// <summary>
        /// 处理任务信息
        /// </summary>
        public void OnTaskInfo(MirCommon.Database.TaskInfo taskInfo)
        {
            try
            {
                LogManager.Default.Debug($"处理任务信息: 任务ID={taskInfo.dwTaskId}, 状态={taskInfo.dwState}");

                // 更新任务管理器
                // QuestManager.UpdateTask(taskInfo); 

                // 发送任务更新消息给客户端
                SendTaskUpdate(taskInfo);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理任务信息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置升级物品
        /// </summary>
        public void SetUpgradeItem(MirCommon.Item item)
        {
            try
            {
                LogManager.Default.Debug($"设置升级物品: 物品ID={item.dwMakeIndex}");

                // 创建物品实例
                var itemDef = ItemManager.Instance.GetDefinition((int)item.baseitem.wImageIndex);
                if (itemDef == null)
                {
                    LogManager.Default.Error($"找不到物品定义: {item.baseitem.wImageIndex}");
                    return;
                }

                var itemInstance = new ItemInstance(itemDef, (long)item.dwMakeIndex)
                {
                    Durability = item.wCurDura,
                    MaxDurability = item.wMaxDura,
                    Count = 1
                };

                // 添加到背包
                if (Inventory.AddItem(itemInstance))
                {
                    SaySystem($"获得了升级物品: {itemDef.Name}");
                    SendInventoryUpdate();
                }
                else
                {
                    SaySystem("背包已满，无法添加升级物品");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"设置升级物品失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置技能
        /// </summary>
        public void SetMagic(MirCommon.Database.MAGICDB magicDb, byte key)
        {
            try
            {
                LogManager.Default.Debug($"设置技能: 技能ID={magicDb.wMagicId}, 等级={magicDb.btCurLevel}, 快捷键={key}");

                // 获取技能定义
                var skillDef = SkillManager.Instance.GetDefinition((int)magicDb.wMagicId);
                if (skillDef == null)
                {
                    LogManager.Default.Error($"找不到技能定义: {magicDb.wMagicId}");
                    return;
                }

                // 学习或更新技能
                var skill = SkillBook.GetSkill((int)magicDb.wMagicId);
                if (skill == null)
                {
                    // 学习新技能
                    skill = new PlayerSkill(skillDef);
                    SkillBook.LearnSkill(skillDef);
                    SaySystem($"学会了新技能: {skillDef.Name}");
                }
                else
                {
                    // 更新技能等级
                    skill.Level = magicDb.btCurLevel;
                }

                // 设置技能快捷键
                // skill.Key = key; 

                // 发送技能列表给客户端
                SendMagicList();
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"设置技能失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送技能列表给客户端
        /// </summary>
        public void SendMagicList()
        {
            try
            {
                var skills = SkillBook.GetAllSkills();
                LogManager.Default.Debug($"发送技能列表: {skills.Count}个技能");

                var builder = new PacketBuilder();
                builder.WriteUInt32(ObjectId);
                builder.WriteUInt16(0x293); // SM_MAGICLIST
                builder.WriteUInt16(0);
                builder.WriteUInt16(0);
                builder.WriteUInt16(0);

                // 写入技能数量
                builder.WriteUInt16((ushort)skills.Count);

                // 写入每个技能的信息
                foreach (var skill in skills)
                {
                    builder.WriteUInt32((uint)skill.Definition.SkillId);
                    builder.WriteByte((byte)skill.Level);
                    builder.WriteByte(0); 
                    builder.WriteUInt32((uint)skill.UseCount);
                    builder.WriteString(skill.Definition.Name);
                }

                SendMessage(builder.Build());
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"发送技能列表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理宠物仓库物品
        /// </summary>
        public void OnPetBank(MirCommon.Item[] items, int count)
        {
            try
            {
                LogManager.Default.Debug($"处理宠物仓库物品: {count}个");

                // 清空宠物背包
                var petBag = PetSystem.GetPetBag();
                // petBag.Clear(); 

                // 添加新物品到宠物背包
                for (int i = 0; i < count; i++)
                {
                    var item = items[i];
                    var itemDef = ItemManager.Instance.GetDefinition((int)item.baseitem.wImageIndex);
                    if (itemDef == null)
                        continue;

                    var itemInstance = new ItemInstance(itemDef, (long)item.dwMakeIndex)
                    {
                        Durability = item.wCurDura,
                        MaxDurability = item.wMaxDura,
                        Count = 1
                    };

                    petBag.AddItem(itemInstance);
                }

                // 发送宠物背包更新消息
                SendPetBagUpdate();
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理宠物仓库物品失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送任务更新消息
        /// </summary>
        private void SendTaskUpdate(MirCommon.Database.TaskInfo taskInfo)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x294); // SM_TASKUPDATE
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt32(taskInfo.dwTaskId);
            builder.WriteByte((byte)taskInfo.dwState);
            builder.WriteString(""); 

            SendMessage(builder.Build());
        }

        /// <summary>
        /// 发送宠物背包更新消息
        /// </summary>
        private void SendPetBagUpdate()
        {
            var petBag = PetSystem.GetPetBag();
            var items = petBag.GetAllItems();

            var builder = new PacketBuilder();
            builder.WriteUInt32(ObjectId);
            builder.WriteUInt16(0x295); // SM_PETBAGUPDATE
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);

            // 写入物品数量
            builder.WriteUInt16((ushort)items.Count);

            // 写入每个物品的信息
            foreach (var item in items.Values)
            {
                builder.WriteUInt64((ulong)item.InstanceId);
                builder.WriteUInt32((uint)item.ItemId);
                builder.WriteString(item.Definition.Name);
                builder.WriteUInt16((ushort)item.Count);
                builder.WriteUInt16((ushort)item.Durability);
                builder.WriteUInt16((ushort)item.MaxDurability);
            }

            SendMessage(builder.Build());
        }

        #endregion

        #region 数据库保存方法

        /// <summary>
        /// 更新到数据库
        /// </summary>
        public void UpdateToDB()
        {
            try
            {
                // 保存玩家基本信息
                SavePlayerInfoToDB();

                // 保存物品数据
                UpdateItemsToDB();

                // 保存技能数据
                UpdateSkillsToDB();

                // 保存任务数据
                UpdateTasksToDB();

                // 保存邮件数据
                UpdateMailsToDB();

                // 保存成就数据
                UpdateAchievementsToDB();

                // 保存宠物数据
                UpdatePetsToDB();

                // 保存坐骑数据
                UpdateMountToDB();

                // 保存PK数据
                UpdatePKDataToDB();

                LogManager.Default.Info($"{Name} 数据已保存到数据库");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"保存玩家数据失败: {Name}, 错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存玩家基本信息到数据库
        /// </summary>
        private void SavePlayerInfoToDB()
        {
            // 这里应该调用数据库接口保存玩家基本信息
            // 包括：等级、经验、金币、元宝、属性、位置等

            // 构建玩家信息数据包
            var builder = new PacketBuilder();
            builder.WriteUInt32(CharDBId);
            builder.WriteString(Name);
            builder.WriteByte(Job);
            builder.WriteByte(Sex);
            builder.WriteByte(Hair);
            builder.WriteUInt16((ushort)Level);
            builder.WriteUInt32(Exp);
            builder.WriteUInt32(Gold);
            builder.WriteUInt32(Yuanbao);
            builder.WriteUInt16((ushort)CurrentHP);
            builder.WriteUInt16((ushort)MaxHP);
            builder.WriteUInt16((ushort)CurrentMP);
            builder.WriteUInt16((ushort)MaxMP);
            builder.WriteUInt16((ushort)X);
            builder.WriteUInt16((ushort)Y);
            builder.WriteUInt16((ushort)Direction);

            // 基础属性
            builder.WriteInt32(BaseDC);
            builder.WriteInt32(BaseMC);
            builder.WriteInt32(BaseSC);
            builder.WriteInt32(BaseAC);
            builder.WriteInt32(BaseMAC);
            builder.WriteInt32(Accuracy);
            builder.WriteInt32(Agility);
            builder.WriteInt32(Lucky);

            // 行会信息
            builder.WriteString(Guild?.Name ?? "");
            builder.WriteString(GuildGroupName);
            builder.WriteUInt32(GuildLevel);

            // PK值
            builder.WriteUInt32(PKSystem.GetPkValue());

            // 登录时间
            builder.WriteUInt64((ulong)LoginTime.Ticks);

            // 最后活动时间
            builder.WriteUInt64((ulong)LastActivity.Ticks);

            // 是否首次登录
            builder.WriteByte(IsFirstLogin ? (byte)1 : (byte)0);

            // 组队ID
            builder.WriteUInt32(GroupId);

            // 发送到数据库服务器
            // 这里需要调用实际的数据库接口
            LogManager.Default.Debug($"保存玩家基本信息: {Name}");
        }

        /// <summary>
        /// 更新物品到数据库
        /// </summary>
        private void UpdateItemsToDB()
        {
            try
            {
                // 保存背包物品
                SaveInventoryToDB();

                // 保存装备物品
                SaveEquipmentToDB();

                // 保存宠物背包物品
                SavePetBagToDB();

                // 保存银行物品
                SaveBankToDB();

                LogManager.Default.Debug($"保存物品数据: {Name}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"保存物品数据失败: {Name}, 错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存背包物品到数据库
        /// </summary>
        private void SaveInventoryToDB()
        {
            var items = Inventory.GetAllItems();
            if (items.Count == 0)
                return;

            // 构建物品数据包
            var builder = new PacketBuilder();
            builder.WriteUInt32(CharDBId);
            builder.WriteUInt16(0x01); // 背包标识

            // 写入物品数量
            builder.WriteUInt16((ushort)items.Count);

            // 写入每个物品的信息
            foreach (var kvp in items)
            {
                var item = kvp.Value;
                builder.WriteUInt64((ulong)item.InstanceId);
                builder.WriteUInt32((uint)item.ItemId);
                builder.WriteUInt16((ushort)item.Count);
                builder.WriteUInt16((ushort)item.Durability);
                builder.WriteUInt16((ushort)item.MaxDurability);
                builder.WriteByte((byte)item.EnhanceLevel);
                builder.WriteByte((byte)kvp.Key); // 背包槽位

                builder.WriteUInt32(0); // 属性1
                builder.WriteUInt32(0); // 属性2
                builder.WriteUInt32(0); // 属性3
            }

            // 发送到数据库服务器
            // 这里需要调用实际的数据库接口
        }

        /// <summary>
        /// 保存装备物品到数据库
        /// </summary>
        private void SaveEquipmentToDB()
        {
            var equipment = Equipment.GetAllEquipment();
            if (equipment.Count == 0)
                return;

            var builder = new PacketBuilder();
            builder.WriteUInt32(CharDBId);
            builder.WriteUInt16(0x02); // 装备标识

            builder.WriteUInt16((ushort)equipment.Count);

            foreach (var equip in equipment)
            {
                builder.WriteUInt64((ulong)equip.InstanceId);
                builder.WriteUInt32((uint)equip.ItemId);
                builder.WriteUInt16((ushort)equip.Durability);
                builder.WriteUInt16((ushort)equip.MaxDurability);
                builder.WriteByte((byte)equip.EnhanceLevel);
                builder.WriteByte((byte)0); 

                // 装备属性
                builder.WriteUInt32(0); // 属性1
                builder.WriteUInt32(0); // 属性2
                builder.WriteUInt32(0); // 属性3
            }
        }

        /// <summary>
        /// 保存宠物背包物品到数据库
        /// </summary>
        private void SavePetBagToDB()
        {
            var petBag = PetSystem.GetPetBag();
            var items = petBag.GetAllItems();
            if (items.Count == 0)
                return;

            var builder = new PacketBuilder();
            builder.WriteUInt32(CharDBId);
            builder.WriteUInt16(0x03); // 宠物背包标识

            builder.WriteUInt16((ushort)items.Count);

            foreach (var item in items.Values)
            {
                builder.WriteUInt64((ulong)item.InstanceId);
                builder.WriteUInt32((uint)item.ItemId);
                builder.WriteUInt16((ushort)item.Count);
                builder.WriteUInt16((ushort)item.Durability);
                builder.WriteUInt16((ushort)item.MaxDurability);
                builder.WriteByte((byte)item.EnhanceLevel);
                builder.WriteByte(0); // 宠物背包槽位

                builder.WriteUInt32(0); // 属性1
                builder.WriteUInt32(0); // 属性2
                builder.WriteUInt32(0); // 属性3
            }
        }

        /// <summary>
        /// 保存银行物品到数据库
        /// </summary>
        private void SaveBankToDB()
        {
            // 这里需要根据实际的银行系统实现
        }

        /// <summary>
        /// 保存技能数据到数据库
        /// </summary>
        private void UpdateSkillsToDB()
        {
            var skills = SkillBook.GetAllSkills();
            if (skills.Count == 0)
                return;

            var builder = new PacketBuilder();
            builder.WriteUInt32(CharDBId);
            builder.WriteUInt16(0x04); // 技能标识

            builder.WriteUInt16((ushort)skills.Count);

            foreach (var skill in skills)
            {
                builder.WriteUInt32((uint)skill.Definition.SkillId);
                builder.WriteUInt16((ushort)skill.Level);
                builder.WriteUInt32((uint)skill.UseCount); // 使用技能经验代替
                builder.WriteUInt32((uint)skill.UseCount);
                builder.WriteByte(0); 
            }
        }

        /// <summary>
        /// 保存任务数据到数据库
        /// </summary>
        private void UpdateTasksToDB()
        {
            // 这里需要根据实际的任务系统实现
        }

        /// <summary>
        /// 保存邮件数据到数据库
        /// </summary>
        private void UpdateMailsToDB()
        {
            var mails = MailSystem.GetMails();
            if (mails.Count == 0)
                return;

            var builder = new PacketBuilder();
            builder.WriteUInt32(CharDBId);
            builder.WriteUInt16(0x05); // 邮件标识

            builder.WriteUInt16((ushort)mails.Count);

            foreach (var mail in mails)
            {
                builder.WriteUInt32(mail.Id);
                builder.WriteString(mail.Sender);
                builder.WriteString(mail.Receiver);
                builder.WriteString(mail.Title);
                builder.WriteString(mail.Content);
                builder.WriteUInt64((ulong)mail.SendTime.Ticks);
                builder.WriteByte(mail.IsRead ? (byte)1 : (byte)0);
                builder.WriteByte(mail.AttachmentsClaimed ? (byte)1 : (byte)0);

                // 附件信息
                if (mail.Attachments != null && mail.Attachments.Count > 0)
                {
                    builder.WriteByte((byte)mail.Attachments.Count);
                    foreach (var attachment in mail.Attachments)
                    {
                        builder.WriteUInt64((ulong)attachment.InstanceId);
                        builder.WriteUInt32((uint)attachment.ItemId);
                        builder.WriteUInt16((ushort)attachment.Count);
                    }
                }
                else
                {
                    builder.WriteByte(0);
                }
            }
        }

        /// <summary>
        /// 保存成就数据到数据库
        /// </summary>
        private void UpdateAchievementsToDB()
        {
            var achievements = AchievementSystem.GetAchievements();
            if (achievements.Count == 0)
                return;

            var builder = new PacketBuilder();
            builder.WriteUInt32(CharDBId);
            builder.WriteUInt16(0x06); // 成就标识

            builder.WriteUInt16((ushort)achievements.Count);

            foreach (var achievement in achievements)
            {
                builder.WriteUInt32(achievement.Id);
                builder.WriteByte(achievement.Completed ? (byte)1 : (byte)0);
                if (achievement.CompletedTime.HasValue)
                {
                    builder.WriteUInt64((ulong)achievement.CompletedTime.Value.Ticks);
                }
                else
                {
                    builder.WriteUInt64(0);
                }
            }
        }

        /// <summary>
        /// 保存宠物数据到数据库
        /// </summary>
        private void UpdatePetsToDB()
        {
            // 这里需要根据实际的宠物系统实现
        }

        /// <summary>
        /// 保存坐骑数据到数据库
        /// </summary>
        private void UpdateMountToDB()
        {
            var mount = MountSystem.GetHorse();
            if (mount == null)
                return;

            var builder = new PacketBuilder();
            builder.WriteUInt32(CharDBId);
            builder.WriteUInt16(0x07); // 坐骑标识

            builder.WriteString(mount.Name);
            builder.WriteUInt16((ushort)mount.Level);
            builder.WriteUInt16((ushort)mount.CurrentHP);
            builder.WriteUInt16((ushort)mount.MaxHP);
            builder.WriteByte(MountSystem.IsRiding() ? (byte)1 : (byte)0);
            builder.WriteByte(MountSystem.IsHorseRest() ? (byte)1 : (byte)0);
        }

        /// <summary>
        /// 保存PK数据到数据库
        /// </summary>
        private void UpdatePKDataToDB()
        {
            var builder = new PacketBuilder();
            builder.WriteUInt32(CharDBId);
            builder.WriteUInt16(0x08); // PK数据标识

            builder.WriteUInt32(PKSystem.GetPkValue());
            builder.WriteByte(PKSystem.IsSelfDefense() ? (byte)1 : (byte)0);
        }

        /// <summary>
        /// 定时保存数据
        /// </summary>
        private void CheckAndSaveToDB()
        {
            // 每5分钟自动保存一次数据
            if ((DateTime.Now - LastActivity).TotalMinutes >= 5)
            {
                UpdateToDB();
                LastActivity = DateTime.Now;
            }
        }

        #endregion
    }

}
