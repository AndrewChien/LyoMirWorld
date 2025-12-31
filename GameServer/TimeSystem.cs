using System;
using System.Collections.Generic;

namespace GameServer
{
    /// <summary>
    /// 时间事件对象接口
    /// </summary>
    public interface ITimeEventObject
    {
        void OnMinuteChange(DateTime currentTime);
        void OnHourChange(DateTime currentTime);
        void OnDayChange(DateTime currentTime);
        void OnMonthChange(DateTime currentTime);
        void OnYearChange(DateTime currentTime);
    }

    /// <summary>
    /// 时间系统
    /// </summary>
    public class TimeSystem
    {
        private static TimeSystem _instance;
        private DateTime _lastUpdateTime;
        private DateTime _startupTime;
        private DateTime _currentTime;
        private Queue<ITimeEventObject> _timeEventQueue;
        private ushort _currentGameTime; // 游戏时间：小时*4 + 分钟/15

        /// <summary>
        /// 获取TimeSystem单例实例
        /// </summary>
        public static TimeSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new TimeSystem();
                }
                return _instance;
            }
        }

        /// <summary>
        /// 私有构造函数
        /// </summary>
        private TimeSystem()
        {
            _lastUpdateTime = DateTime.Now;
            _startupTime = DateTime.Now;
            _currentTime = DateTime.Now;
            _timeEventQueue = new Queue<ITimeEventObject>();
            _currentGameTime = CalculateGameTime(_startupTime);
        }

        /// <summary>
        /// 计算游戏时间
        /// 游戏时间 = 小时 * 4 + 分钟 / 15
        /// </summary>
        private ushort CalculateGameTime(DateTime time)
        {
            return (ushort)(time.Hour * 4 + time.Minute / 15);
        }

        /// <summary>
        /// 获取当前游戏时间
        /// </summary>
        public ushort GetCurrentTime()
        {
            return _currentGameTime;
        }

        /// <summary>
        /// 注册时间事件对象
        /// </summary>
        public bool RegisterTimeEvent(ITimeEventObject timeEvent)
        {
            if (timeEvent == null)
                return false;

            _timeEventQueue.Enqueue(timeEvent);
            return true;
        }

        /// <summary>
        /// 更新时间系统
        /// 每分钟检查一次时间变化
        /// </summary>
        public void Update()
        {
            // 检查是否过了1分钟
            TimeSpan elapsed = DateTime.Now - _lastUpdateTime;
            if (elapsed.TotalMilliseconds >= 60000)
            {
                _lastUpdateTime = DateTime.Now;
                
                DateTime oldTime = _currentTime;
                _currentTime = DateTime.Now;

                // 检查时间变化并触发相应事件
                if (oldTime.Year != _currentTime.Year)
                    OnYearChange();
                
                if (oldTime.Month != _currentTime.Month)
                    OnMonthChange();
                
                if (oldTime.Day != _currentTime.Day)
                    OnDayChange();
                
                if (oldTime.Hour != _currentTime.Hour)
                    OnHourChange();
                
                if (oldTime.Minute != _currentTime.Minute)
                    OnMinuteChange();
            }
        }

        /// <summary>
        /// 分钟变化事件
        /// </summary>
        private void OnMinuteChange()
        {
            // 通知所有注册的时间事件对象
            int count = _timeEventQueue.Count;
            for (int i = 0; i < count; i++)
            {
                ITimeEventObject timeEvent = _timeEventQueue.Dequeue();
                if (timeEvent != null)
                {
                    timeEvent.OnMinuteChange(_currentTime);
                    _timeEventQueue.Enqueue(timeEvent);
                }
            }

                // 检查游戏时间是否变化
                ushort newGameTime = CalculateGameTime(_currentTime);
                if (newGameTime != _currentGameTime)
                {
                    // 触发游戏时间变化事件
                    // 集成到GameWorld的全局进程队列
                    var process = new GlobeProcess(GlobeProcessType.TimeSystemUpdate, (uint)newGameTime);
                    GameWorld.Instance?.AddGlobeProcess(process);
                    _currentGameTime = newGameTime;
                }
        }

        /// <summary>
        /// 小时变化事件
        /// </summary>
        private void OnHourChange()
        {
            int count = _timeEventQueue.Count;
            for (int i = 0; i < count; i++)
            {
                ITimeEventObject timeEvent = _timeEventQueue.Dequeue();
                if (timeEvent != null)
                {
                    timeEvent.OnHourChange(_currentTime);
                    _timeEventQueue.Enqueue(timeEvent);
                }
            }
        }

        /// <summary>
        /// 天变化事件
        /// </summary>
        private void OnDayChange()
        {
            int count = _timeEventQueue.Count;
            for (int i = 0; i < count; i++)
            {
                ITimeEventObject timeEvent = _timeEventQueue.Dequeue();
                if (timeEvent != null)
                {
                    timeEvent.OnDayChange(_currentTime);
                    _timeEventQueue.Enqueue(timeEvent);
                }
            }
        }

        /// <summary>
        /// 月变化事件
        /// </summary>
        private void OnMonthChange()
        {
            int count = _timeEventQueue.Count;
            for (int i = 0; i < count; i++)
            {
                ITimeEventObject timeEvent = _timeEventQueue.Dequeue();
                if (timeEvent != null)
                {
                    timeEvent.OnMonthChange(_currentTime);
                    _timeEventQueue.Enqueue(timeEvent);
                }
            }
        }

        /// <summary>
        /// 年变化事件
        /// </summary>
        private void OnYearChange()
        {
            int count = _timeEventQueue.Count;
            for (int i = 0; i < count; i++)
            {
                ITimeEventObject timeEvent = _timeEventQueue.Dequeue();
                if (timeEvent != null)
                {
                    timeEvent.OnYearChange(_currentTime);
                    _timeEventQueue.Enqueue(timeEvent);
                }
            }
        }

        /// <summary>
        /// 获取启动时间
        /// </summary>
        public DateTime GetStartupTime()
        {
            return _startupTime;
        }

        /// <summary>
        /// 获取当前系统时间
        /// </summary>
        public DateTime GetCurrentSystemTime()
        {
            return _currentTime;
        }

        /// <summary>
        /// 获取时间事件队列数量
        /// </summary>
        public int GetTimeEventCount()
        {
            return _timeEventQueue.Count;
        }
    }
}
