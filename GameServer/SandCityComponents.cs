using System;
using MirCommon.Utils;

namespace GameServer
{
    /// <summary>
    /// 系统标志枚举
    /// </summary>
    public enum SystemFlag
    {
        NoDamage = 0,       // 无敌状态
        NoTarget = 1,       // 不可被选中
        Invisible = 2,      // 隐身
        NoMove = 3,         // 不可移动
        NoAttack = 4,       // 不可攻击
        NoMagic = 5,        // 不可使用魔法
        NoItem = 6,         // 不可使用物品
        NoChat = 7,         // 不可聊天
        NoTrade = 8,        // 不可交易
        NoDrop = 9,         // 不可丢弃物品
        NoPickup = 10,      // 不可拾取物品
        NoRecall = 11,      // 不可被召唤
        NoTrans = 12,       // 不可传送
        NoFly = 13,         // 不可飞行
        NoDie = 14,         // 不会死亡
        NoExp = 15,         // 不获得经验
        NoPK = 16,          // 不可PK
        NoGuild = 17,       // 不可加入行会
        NoParty = 18,       // 不可组队
        NoFriend = 19,      // 不可加好友
        NoMail = 20,        // 不可发送邮件
        NoAuction = 21,     // 不可拍卖
        NoMarket = 22,      // 不可市场交易
        NoStall = 23,       // 不可摆摊
        NoQuest = 24,       // 不可接任务
        NoSkill = 25,       // 不可学习技能
        NoBuff = 26,        // 不可获得Buff
        NoDebuff = 27,      // 不可获得Debuff
        NoHeal = 28,        // 不可被治疗
        NoResurrect = 29,   // 不可被复活
        NoSummon = 30,      // 不可被召唤
        NoPet = 31,         // 不可携带宠物
        NoMount = 32,       // 不可骑乘
        NoTransform = 33,   // 不可变身
        NoDisguise = 34,    // 不可伪装
        NoStealth = 35,     // 不可潜行
        NoSneak = 36,       // 不可偷袭
        NoBackstab = 37,    // 不可背刺
        NoCritical = 38,    // 不可暴击
        NoDodge = 39,       // 不可闪避
        NoBlock = 40,       // 不可格挡
        NoParry = 41,       // 不可招架
        NoCounter = 42,     // 不可反击
        NoReflect = 43,     // 不可反射
        NoAbsorb = 44,      // 不可吸收
        NoReduce = 45,      // 不可减免
        NoIgnore = 46,      // 不可无视防御
        NoPenetrate = 47,   // 不可穿透
        NoSplash = 48,      // 不可溅射
        NoChain = 49,       // 不可连锁
        NoBounce = 50,      // 不可弹射
        NoSpread = 51,      // 不可扩散
        NoExplode = 52,     // 不可爆炸
        NoImplode = 53,     // 不可内爆
        NoVortex = 54,      // 不可漩涡
        NoBlackhole = 55,   // 不可黑洞
        NoGravity = 56,     // 不可重力
        NoTime = 57,        // 不可时间
        NoSpace = 58,       // 不可空间
        NoDimension = 59,   // 不可维度
        NoReality = 60,     // 不可现实
        NoDream = 61,       // 不可梦境
        NoIllusion = 62,    // 不可幻象
        NoMirage = 63,      // 不可海市蜃楼
        NoPhantom = 64,     // 不可幽灵
        NoGhost = 65,       // 不可鬼魂
        NoSpirit = 66,      // 不可灵魂
        NoDemon = 67,       // 不可恶魔
        NoAngel = 68,       // 不可天使
        NoGod = 69,         // 不可神
        NoDevil = 70,       // 不可魔鬼
        NoDragon = 71,      // 不可龙
        NoPhoenix = 72,     // 不可凤凰
        NoUnicorn = 73,     // 不可独角兽
        NoGriffin = 74,     // 不可狮鹫
        NoPegasus = 75,     // 不可飞马
        NoMermaid = 76,     // 不可美人鱼
        NoSiren = 77,       // 不可塞壬
        NoHydra = 78,       // 不可九头蛇
        NoChimera = 79,     // 不可奇美拉
        NoCerberus = 80,    // 不可刻耳柏洛斯
        NoMinotaur = 81,    // 不可弥诺陶洛斯
        NoCyclops = 82,     // 不可独眼巨人
        NoGiant = 83,       // 不可巨人
        NoTitan = 84,       // 不可泰坦
        NoGolem = 85,       // 不可魔像
        NoElemental = 86,   // 不可元素
        NoUndead = 87,      // 不可亡灵
        NoConstruct = 88,   // 不可构装体
        NoAberration = 89,  // 不可异怪
        NoBeast = 90,       // 不可野兽
        NoHumanoid = 91,    // 不可人形生物
        NoMonstrosity = 92, // 不可怪物
        NoOoze = 93,        // 不可泥怪
        NoPlant = 94,       // 不可植物
        NoVermin = 95,      // 不可虫类
        NoMax = 96
    }

    /// <summary>
    /// 沙城皇宫入口门点
    /// </summary>
    public class CSCPalaceDoor
    {
        private uint _fromMapId;
        private uint _fromX;
        private uint _fromY;
        private uint _toMapId;
        private uint _toX;
        private uint _toY;

        public CSCPalaceDoor()
        {
        }

        /// <summary>
        /// 创建门点
        /// </summary>
        public bool Create(uint fromMapId, uint fromX, uint fromY, uint toMapId, uint toX, uint toY)
        {
            _fromMapId = fromMapId;
            _fromX = fromX;
            _fromY = fromY;
            _toMapId = toMapId;
            _toX = toX;
            _toY = toY;

            LogManager.Default.Info($"创建沙城皇宫入口门点: 从({fromMapId},{fromX},{fromY}) 到({toMapId},{toX},{toY})");
            return true;
        }

        /// <summary>
        /// 获取源地图ID
        /// </summary>
        public uint GetFromMapId() => _fromMapId;

        /// <summary>
        /// 获取源X坐标
        /// </summary>
        public uint GetFromX() => _fromX;

        /// <summary>
        /// 获取源Y坐标
        /// </summary>
        public uint GetFromY() => _fromY;

        /// <summary>
        /// 获取目标地图ID
        /// </summary>
        public uint GetToMapId() => _toMapId;

        /// <summary>
        /// 获取目标X坐标
        /// </summary>
        public uint GetToX() => _toX;

        /// <summary>
        /// 获取目标Y坐标
        /// </summary>
        public uint GetToY() => _toY;
    }

    /// <summary>
    /// 沙城城门
    /// </summary>
    public class CSCDoor : AliveObject
    {
        private string _name = string.Empty;
        private uint _hp;
        private bool _isOpened;

        public CSCDoor()
        {
        }

        /// <summary>
        /// 初始化城门
        /// </summary>
        public bool Init(string name, uint mapId, uint x, uint y, uint hp, bool isOpened = false)
        {
            _name = name;
            _hp = hp;
            _isOpened = isOpened;

            // 设置位置
            MapId = (int)mapId;
            X = (int)x;
            Y = (int)y;

            LogManager.Default.Info($"初始化城门: {name}, 位置({mapId},{x},{y}), HP={hp}, 状态={(isOpened ? "开启" : "关闭")}");
            return true;
        }

        /// <summary>
        /// 打开城门
        /// </summary>
        public void Open()
        {
            _isOpened = true;
            LogManager.Default.Info($"城门 {_name} 已打开");
        }

        /// <summary>
        /// 关闭城门
        /// </summary>
        public void Close()
        {
            _isOpened = false;
            LogManager.Default.Info($"城门 {_name} 已关闭");
        }

        /// <summary>
        /// 修复城门
        /// </summary>
        public void Repair()
        {
            _hp = GetMaxHp();
            LogManager.Default.Info($"城门 {_name} 已修复，HP={_hp}");
        }

        /// <summary>
        /// 检查城门是否开启
        /// </summary>
        public bool IsOpened() => _isOpened;

        /// <summary>
        /// 获取城门名称
        /// </summary>
        public string GetName() => _name;

        /// <summary>
        /// 获取当前HP
        /// </summary>
        public uint GetHp() => _hp;

        /// <summary>
        /// 获取最大HP
        /// </summary>
        public uint GetMaxHp() => 10000; // 默认最大HP

        /// <summary>
        /// 设置系统标志
        /// </summary>
        public void SetSystemFlag(SystemFlag flag, bool value)
        {
            // 实现系统标志设置逻辑
            LogManager.Default.Debug($"城门 {_name} 设置系统标志 {flag} = {value}");
        }

        /// <summary>
        /// 获取对象类型
        /// </summary>
        public override ObjectType GetObjectType()
        {
            return ObjectType.Event; // 城门属于事件类型
        }

        /// <summary>
        /// 获取属性值
        /// </summary>
        public uint GetPropValue(PropIndex index)
        {
            switch (index)
            {
                case PropIndex.CurHp:
                    return _hp;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// 获取描述
        /// </summary>
        public object GetDesc()
        {
            return new { Base = new { ClassName = _name } };
        }

        /// <summary>
        /// 获取X坐标
        /// </summary>
        public int GetX() => X;

        /// <summary>
        /// 获取Y坐标
        /// </summary>
        public int GetY() => Y;
    }

    /// <summary>
    /// 沙城城墙
    /// </summary>
    public class CPalaceWall : AliveObject
    {
        private string _name = string.Empty;
        private uint _hp;

        public CPalaceWall()
        {
        }

        /// <summary>
        /// 初始化城墙
        /// </summary>
        public bool Init(string name, uint mapId, uint x, uint y, uint hp)
        {
            _name = name;
            _hp = hp;

            // 设置位置
            MapId = (int)mapId;
            X = (int)x;
            Y = (int)y;

            LogManager.Default.Info($"初始化城墙: {name}, 位置({mapId},{x},{y}), HP={hp}");
            return true;
        }

        /// <summary>
        /// 修复城墙
        /// </summary>
        public void Repair()
        {
            _hp = GetMaxHp();
            LogManager.Default.Info($"城墙 {_name} 已修复，HP={_hp}");
        }

        /// <summary>
        /// 获取城墙名称
        /// </summary>
        public string GetName() => _name;

        /// <summary>
        /// 获取当前HP
        /// </summary>
        public uint GetHp() => _hp;

        /// <summary>
        /// 获取最大HP
        /// </summary>
        public uint GetMaxHp() => 5000; // 默认最大HP

        /// <summary>
        /// 设置系统标志
        /// </summary>
        public void SetSystemFlag(SystemFlag flag, bool value)
        {
            // 实现系统标志设置逻辑
            LogManager.Default.Debug($"城墙 {_name} 设置系统标志 {flag} = {value}");
        }

        /// <summary>
        /// 获取对象类型
        /// </summary>
        public override ObjectType GetObjectType()
        {
            return ObjectType.Event; // 城墙属于事件类型
        }

        /// <summary>
        /// 获取属性值
        /// </summary>
        public uint GetPropValue(PropIndex index)
        {
            switch (index)
            {
                case PropIndex.CurHp:
                    return _hp;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// 获取描述
        /// </summary>
        public object GetDesc()
        {
            return new { Base = new { ClassName = _name } };
        }

        /// <summary>
        /// 获取X坐标
        /// </summary>
        public int GetX() => X;

        /// <summary>
        /// 获取Y坐标
        /// </summary>
        public int GetY() => Y;
    }

    /// <summary>
    /// 沙城弓箭手
    /// </summary>
    public class CSCArcher : AliveObject
    {
        private string _name = string.Empty;
        private uint _hp;

        public CSCArcher()
        {
        }

        /// <summary>
        /// 初始化弓箭手
        /// </summary>
        public bool Init(string name, uint mapId, uint x, uint y, uint hp)
        {
            _name = name;
            _hp = hp;

            // 设置位置
            MapId = (int)mapId;
            X = (int)x;
            Y = (int)y;

            LogManager.Default.Info($"初始化弓箭手: {name}, 位置({mapId},{x},{y}), HP={hp}");
            return true;
        }

        /// <summary>
        /// 获取弓箭手名称
        /// </summary>
        public string GetName() => _name;

        /// <summary>
        /// 获取当前HP
        /// </summary>
        public uint GetHp() => _hp;

        /// <summary>
        /// 获取最大HP
        /// </summary>
        public uint GetMaxHp() => 1000; // 默认最大HP

        /// <summary>
        /// 获取对象类型
        /// </summary>
        public override ObjectType GetObjectType()
        {
            return ObjectType.NPC; // 弓箭手属于NPC类型
        }

        /// <summary>
        /// 获取属性值
        /// </summary>
        public uint GetPropValue(PropIndex index)
        {
            switch (index)
            {
                case PropIndex.CurHp:
                    return _hp;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// 获取描述
        /// </summary>
        public object GetDesc()
        {
            return new { Base = new { ClassName = _name } };
        }

        /// <summary>
        /// 获取X坐标
        /// </summary>
        public int GetX() => X;

        /// <summary>
        /// 获取Y坐标
        /// </summary>
        public int GetY() => Y;
    }

    /// <summary>
    /// 属性索引枚举
    /// </summary>
    public enum PropIndex
    {
        CurHp = 0,      // 当前HP
        MaxHp = 1,      // 最大HP
        CurMp = 2,      // 当前MP
        MaxMp = 3,      // 最大MP
        Level = 4,      // 等级
        Exp = 5,        // 经验
        AC = 6,         // 防御
        MAC = 7,        // 魔法防御
        DC = 8,         // 攻击
        MC = 9,         // 魔法
        SC = 10,        // 道术
        Hit = 11,       // 命中
        Speed = 12,     // 速度
        Luck = 13,      // 幸运
        Curse = 14,     // 诅咒
        Accuracy = 15,  // 准确
        Agility = 16,   // 敏捷
        Max = 17
    }
}
