using System;
using System.Diagnostics;

namespace MirCommon.Utils
{
    /// <summary>
    /// 服务器计时器
    /// </summary>
    public class ServerTimer
    {
        private uint _savedTime;
        private uint _timeoutTime;
        private static readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        /// <summary>
        /// 创建服务器计时器
        /// </summary>
        public ServerTimer()
        {
            _savedTime = 0;
            _timeoutTime = 0;
        }

        /// <summary>
        /// 获取当前时间（毫秒）
        /// </summary>
        public static uint GetTime()
        {
            return (uint)_stopwatch.ElapsedMilliseconds;
        }

        /// <summary>
        /// 保存当前时间
        /// </summary>
        public void SaveTime()
        {
            _savedTime = GetTime();
        }

        /// <summary>
        /// 保存当前时间并设置超时时间
        /// </summary>
        public void SaveTime(uint newTimeout)
        {
            SetInterval(newTimeout);
            SaveTime();
        }

        /// <summary>
        /// 检查是否超时（静态方法）
        /// </summary>
        public static bool IsTimeOut(uint startTime, uint timeout)
        {
            uint currentTime = GetTime();
            return GetTimeToTime(startTime, currentTime) >= timeout;
        }

        /// <summary>
        /// 检查是否超时（指定超时时间）
        /// </summary>
        public bool IsTimeOut(uint timeout)
        {
            uint currentTime = GetTime();
            return GetTimeToTime(_savedTime, currentTime) >= timeout;
        }

        /// <summary>
        /// 设置间隔时间
        /// </summary>
        public void SetInterval(uint interval)
        {
            _savedTime = GetTime();
            _timeoutTime = interval;
        }

        /// <summary>
        /// 检查是否超时（使用内部超时时间）
        /// </summary>
        public bool IsTimeOut()
        {
            uint currentTime = GetTime();
            return GetTimeToTime(_savedTime, currentTime) >= _timeoutTime;
        }

        /// <summary>
        /// 获取超时时间
        /// </summary>
        public uint GetTimeout()
        {
            return _timeoutTime;
        }

        /// <summary>
        /// 获取保存的时间
        /// </summary>
        public uint GetSavedTime()
        {
            return _savedTime;
        }

        /// <summary>
        /// 设置保存的时间
        /// </summary>
        public void SetSavedTime(uint time)
        {
            _savedTime = time;
        }

        /// <summary>
        /// 重置计时器
        /// </summary>
        public void Reset()
        {
            SaveTime();
        }

        /// <summary>
        /// 获取剩余时间
        /// </summary>
        public uint GetRemainingTime()
        {
            if (_timeoutTime == 0)
                return 0;

            uint elapsed = GetTimeToTime(_savedTime, GetTime());
            return elapsed >= _timeoutTime ? 0 : _timeoutTime - elapsed;
        }

        /// <summary>
        /// 获取已过去的时间
        /// </summary>
        public uint GetElapsedTime()
        {
            return GetTimeToTime(_savedTime, GetTime());
        }

        /// <summary>
        /// 计算两个时间点之间的时间差
        /// </summary>
        private static uint GetTimeToTime(uint t1, uint t2)
        {
            const uint MAX_TIME = uint.MaxValue;
            return t1 <= t2 ? (t2 - t1) : (MAX_TIME - t1 + t2);
        }

        /// <summary>
        /// 获取计时器状态字符串
        /// </summary>
        public override string ToString()
        {
            return $"ServerTimer[Saved={_savedTime}, Timeout={_timeoutTime}, Remaining={GetRemainingTime()}]";
        }
    }
}
