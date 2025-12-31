namespace GameServer
{
    /// <summary>
    /// 游戏变量类
    /// </summary>
    public static class GameVariables
    {
        // 沙城相关变量
        public static uint SandCityTakeTime { get; set; } = 0;
        public static uint WarTimeLong { get; set; } = 7200; // 2小时，单位：秒
        public static uint WarStartTime { get; set; } = 0;
        
        // 其他游戏变量
        public static uint MaxPlayers { get; set; } = 1000;
        public static uint MaxMonsters { get; set; } = 5000;
        public static uint MaxNPCs { get; set; } = 1000;
        public static uint MaxItems { get; set; } = 10000;
        
        // 时间相关
        public static uint GameTime { get; set; } = 0;
        public static uint ServerStartTime { get; set; } = 0;
        
        // 系统相关
        public static bool IsServerRunning { get; set; } = true;
        public static bool IsMaintenance { get; set; } = false;
        
        // 初始化游戏变量
        public static void Initialize()
        {
            SandCityTakeTime = 0;
            WarTimeLong = 7200; // 2小时
            WarStartTime = 0;
            MaxPlayers = 1000;
            MaxMonsters = 5000;
            MaxNPCs = 1000;
            MaxItems = 10000;
            GameTime = 0;
            ServerStartTime = (uint)DateTimeOffset.Now.ToUnixTimeSeconds();
            IsServerRunning = true;
            IsMaintenance = false;
        }
        
        // 获取当前游戏时间
        public static uint GetGameTime()
        {
            return (uint)DateTimeOffset.Now.ToUnixTimeSeconds() - ServerStartTime;
        }
        
        // 更新游戏时间
        public static void UpdateGameTime()
        {
            GameTime = GetGameTime();
        }
        
        // 检查是否在沙城战争时间内
        public static bool IsSandCityWarTime()
        {
            if (WarStartTime == 0 || WarTimeLong == 0)
                return false;
                
            uint currentTime = GetGameTime();
            return currentTime >= WarStartTime && currentTime <= WarStartTime + WarTimeLong;
        }
        
        // 获取沙城战争剩余时间
        public static uint GetSandCityWarRemainingTime()
        {
            if (!IsSandCityWarTime())
                return 0;
                
            uint currentTime = GetGameTime();
            return WarStartTime + WarTimeLong - currentTime;
        }
        
        // 开始沙城战争
        public static void StartSandCityWar()
        {
            WarStartTime = GetGameTime();
        }
        
        // 结束沙城战争
        public static void EndSandCityWar()
        {
            WarStartTime = 0;
        }
    }
}
