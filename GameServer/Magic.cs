using System;

namespace GameServer
{
    /// <summary>
    /// 魔法技能结构
    /// </summary>
    public class Magic
    {
        public char cKey;                    // 当前的按键
        public byte btLevel;                 // 当前等级
        public ushort wUnknown;              // 未知字段
        public int iCurExp;                  // 当前经验
        public ushort wId;                   // 技能ID
        public byte btNameLength;            // 名称长度
        public string szName = "";           // 技能名称（最大12字符）
        public byte btEffectType;            // 效果类型
        public byte btEffect;                // 效果值
        public byte btUnknown;               // 未知字段
        public ushort wSpell;                // 魔法值消耗
        public ushort wPower;                // 威力
        public byte[] btNeedLevel = new byte[4]; // 需要等级[0-3]
        public ushort wUnknown2;             // 未知字段
        public int[] iLevelupExp = new int[4];   // 升级经验[0-3]
        public byte btUnknown2;              // 未知字段
        public byte job;                     // 职业
        public ushort wUnknown3;             // 未知字段
        public ushort wDelayTime;            // 延迟时间
        public ushort wUnknown4;             // 未知字段
        public byte btDefSpell;              // 防御魔法值消耗
        public byte btDefPower;              // 防御威力
        public ushort wMaxPower;             // 最大威力
        public ushort wDefMaxPower;          // 防御最大威力
        public byte[] btUnknown4 = new byte[18]; // 未知字段（18字节）

        public Magic()
        {
            // 初始化数组
            for (int i = 0; i < 4; i++)
            {
                btNeedLevel[i] = 0;
                iLevelupExp[i] = 0;
            }
            for (int i = 0; i < 18; i++)
            {
                btUnknown4[i] = 0;
            }
        }
    }

    /// <summary>
    /// 魔法技能类结构
    /// </summary>
    public class MagicClass
    {
        public string szName = "";           // 技能名称（最大19字符）
        public uint id;                      // 技能ID
        public byte btJob;                   // 职业
        public byte btEffectType;            // 效果类型
        public byte btEffectValue;           // 效果值
        public byte[] btNeedLv = new byte[4];    // 需要等级[0-3]
        public uint[] dwNeedExp = new uint[4];   // 升级经验[0-3]
        public short sSpell;                 // 魔法值消耗
        public short sPower;                 // 威力
        public short sMaxPower;              // 最大威力
        public short sDefSpell;              // 防御魔法值消耗
        public short sDefPower;              // 防御威力
        public short sDefMaxPower;           // 防御最大威力
        public ushort wDelay;                // 延迟时间
        public string szDesc = "";           // 描述（最大199字符）
        public ushort[] wNeedMagic = new ushort[3];  // 需要的前置技能
        public ushort[] wMutexMagic = new ushort[3]; // 互斥技能
        public uint dwFlag;                  // 标志位
        public ushort wCharmCount;           // 道符使用个数
        public ushort wRedPoisonCount;       // 红毒使用个数
        public ushort wGreenPoisonCount;     // 绿毒使用个数
        public ushort wStrawManCount;        // 稻草人使用个数
        public ushort wStrawWomanCount;      // 稻草女人使用个数
        public string szSpecial = "";        // 特殊字段（最大255字符）

        public MagicClass()
        {
            // 初始化数组
            for (int i = 0; i < 4; i++)
            {
                btNeedLv[i] = 0;
                dwNeedExp[i] = 0;
            }
            for (int i = 0; i < 3; i++)
            {
                wNeedMagic[i] = 0;
                wMutexMagic[i] = 0;
            }
        }
    }

    /// <summary>
    /// 魔法标志位定义
    /// </summary>
    [Flags]
    public enum MagicFlag : uint
    {
        MAGICFLAG_NOEFFECT = 0x00000001,     // 不显示效果
        MAGICFLAG_ACTIVED = 0x00000002,      // 需激活技能
        MAGICFLAG_FORCED = 0x00000004,       // 被动技能
        MAGICFLAG_FORCED_EXP = 0x00000008,   // 被动技能（经验）
        MAGICFLAG_USECHARM = 0x00000010,     // 使用道符
        MAGICFLAG_USEREDPOISON = 0x00000020, // 使用红毒
        MAGICFLAG_USEGREENPOISON = 0x00000040, // 使用绿毒
        MAGICFLAG_USESTRAWMAN = 0x00000080,  // 使用稻草人
        MAGICFLAG_USESTRAWWOMAN = 0x00000100 // 使用稻草女人
    }
}
