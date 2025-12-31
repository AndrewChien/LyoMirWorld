using System;

namespace GameServer
{
    /// <summary>
    /// 全局进程类
    /// </summary>
    public class GlobeProcess
    {
        public uint ProcessId { get; set; }
        public GlobeProcessType Type { get; set; }
        public uint Param1 { get; set; }
        public uint Param2 { get; set; }
        public uint Param3 { get; set; }
        public uint Param4 { get; set; }
        public uint Delay { get; set; }
        public int RepeatTimes { get; set; }
        public string? StringParam { get; set; }
        public DateTime ExecuteTime { get; set; }
        public DateTime CreateTime { get; set; }

        public GlobeProcess(GlobeProcessType type)
        {
            ProcessId = 0;
            Type = type;
            ExecuteTime = DateTime.Now;
            CreateTime = DateTime.Now;
        }

        public GlobeProcess(GlobeProcessType type, uint param1, uint param2 = 0, uint param3 = 0, uint param4 = 0, 
            uint delay = 0, int repeatTimes = 0, string? stringParam = null)
        {
            ProcessId = 0;
            Type = type;
            Param1 = param1;
            Param2 = param2;
            Param3 = param3;
            Param4 = param4;
            Delay = delay;
            RepeatTimes = repeatTimes;
            StringParam = stringParam;
            ExecuteTime = DateTime.Now.AddMilliseconds(delay);
            CreateTime = DateTime.Now;
        }

        public bool ShouldExecute()
        {
            return DateTime.Now >= ExecuteTime;
        }

        public void Execute()
        {
            // 执行进程逻辑
            // 具体执行逻辑由调用者根据Type决定
        }

        public override string ToString()
        {
            return $"GlobeProcess[Id={ProcessId}, Type={Type}, ExecuteTime={ExecuteTime}, Delay={Delay}ms]";
        }
    }

    /// <summary>
    /// 全局进程类型
    /// </summary>
    public enum GlobeProcessType
    {
        None = 0,
        SandCityWarStart = 1,           // 沙城战争开始
        SandCityWarEnd = 2,             // 沙城战争结束
        SandCityNpcShow = 3,            // 沙城NPC显示
        SandCityNpcHide = 4,            // 沙城NPC隐藏
        SystemMessage = 5,              // 系统消息
        BroadcastMessage = 6,           // 广播消息
        PlayerKick = 7,                 // 踢出玩家
        PlayerBan = 8,                  // 封禁玩家
        ServerShutdown = 9,             // 服务器关闭
        ServerRestart = 10,             // 服务器重启
        DatabaseSave = 11,              // 数据库保存
        LogCleanup = 12,                // 日志清理
        MonsterGen = 13,                // 怪物生成
        ItemCleanup = 14,               // 物品清理
        GuildWarStart = 15,             // 行会战争开始
        GuildWarEnd = 16,               // 行会战争结束
        QuestUpdate = 17,               // 任务更新
        BuffUpdate = 18,                // Buff更新
        TimeSystemUpdate = 19,          // 时间系统更新
        EventManagerUpdate = 20,        // 事件管理器更新
        AutoScriptUpdate = 21,          // 自动脚本更新
        MapScriptUpdate = 22,           // 地图脚本更新
        ScriptVariableUpdate = 23,      // 脚本变量更新
        TopManagerUpdate = 24,          // 排行榜更新
        MarketManagerUpdate = 25,       // 市场管理器更新
        SpecialEquipmentUpdate = 26,    // 特殊装备更新
        TitleManagerUpdate = 27,        // 称号管理器更新
        TaskManagerUpdate = 28,         // 任务管理器更新
        Max = 29
    }
}
