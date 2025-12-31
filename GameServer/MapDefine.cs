using System;

namespace GameServer
{
    /// <summary>
    /// 地图相关定义
    /// </summary>
    
    /// <summary>
    /// 地图标志
    /// </summary>
    [Flags]
    public enum MapFlag : uint
    {
        MF_NONE = 0x00000000,      // 无标志
        MF_MINE = 0x00000001,      // 允许挖矿
        MF_FIGHT = 0x00000002,     // 战斗地图
        MF_SAFE = 0x00000004,      // 安全区
        MF_NOPK = 0x00000008,      // 禁止PK
        MF_NOMONSTER = 0x00000010, // 无怪物
        MF_NOPET = 0x00000020,     // 禁止宠物
        MF_NOMOUNT = 0x00000040,   // 禁止坐骑
        MF_NOTELEPORT = 0x00000080, // 禁止传送
        MF_NORECALL = 0x00000100,  // 禁止回城
        MF_NODROP = 0x00000200,    // 禁止丢弃物品
        MF_NOGUILDWAR = 0x00000400, // 禁止行会战
        MF_NODUEL = 0x00000800,    // 禁止决斗
        MF_NOSKILL = 0x00001000,   // 禁止技能
        MF_NOITEM = 0x00002000,    // 禁止使用物品
        MF_NOSPELL = 0x00004000,   // 禁止施法
        MF_NORUN = 0x00008000,     // 禁止跑步
        MF_NOWALK = 0x00010000,    // 禁止行走
        MF_NOSIT = 0x00020000,     // 禁止坐下
        MF_NOSTAND = 0x00040000,   // 禁止站立
        MF_NODIE = 0x00080000,     // 禁止死亡
        MF_NORESPAWN = 0x00100000, // 禁止复活
        MF_NOLOGOUT = 0x00200000,  // 禁止登出
        MF_NOSAVE = 0x00400000,    // 禁止保存
        MF_NOLOAD = 0x00800000,    // 禁止加载
        MF_NOSCRIPT = 0x01000000,  // 禁止脚本
        MF_NOEVENT = 0x02000000,   // 禁止事件
        MF_NOMESSAGE = 0x04000000, // 禁止消息
        MF_NOCHAT = 0x08000000,    // 禁止聊天
        MF_NOWHISPER = 0x10000000, // 禁止密谈
        MF_NOSHOUT = 0x20000000,   // 禁止喊话
        MF_NOTRADE = 0x40000000,   // 禁止交易
        MF_NOSTORE = 0x80000000,   // 禁止商店
        MF_DAY = 0x00000001,       // 白天标志（与MF_MINE重叠，需要另外处理）
        MF_NIGHT = 0x00000002,     // 夜晚标志（与MF_FIGHT重叠，需要另外处理）
        MF_WEATHER = 0x00000004    // 天气标志（与MF_SAFE重叠，需要另外处理）
    }

    /// <summary>
    /// 天气信息
    /// </summary>
    public class WeatherInfo
    {
        public ushort WeatherIndex { get; set; }
        public ushort Flag { get; set; }
        public uint WeatherColor { get; set; }
        public uint BGColor { get; set; }

        public WeatherInfo()
        {
            WeatherIndex = 0;
            Flag = 0;
            WeatherColor = 0xFFFFFFFF;
            BGColor = 0;
        }
    }
}
