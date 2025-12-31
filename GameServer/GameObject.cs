using System;
using System.Collections.Generic;
using System.Threading;
using MirCommon;

namespace GameServer
{
    /// <summary>
    /// 游戏对象基类 - 所有游戏对象的基础
    /// </summary>
    public abstract class GameObject
    {
        private static uint _nextObjectId = 1;
        
        public uint ObjectId { get; protected set; }
        public uint InstanceKey { get; protected set; }
        private int _referenceCount = 0;

        protected GameObject()
        {
            ObjectId = Interlocked.Increment(ref _nextObjectId);
            InstanceKey = (uint)DateTime.Now.Ticks;
        }

        public virtual void Clean()
        {
            // 清理资源
        }

        public virtual void Update()
        {
            // 每帧更新
        }

        public int AddRef()
        {
            return Interlocked.Increment(ref _referenceCount);
        }

        public int DecRef()
        {
            int count = Interlocked.Decrement(ref _referenceCount);
            if (count < 0)
            {
                throw new InvalidOperationException("引用计数不能为负");
            }
            return count;
        }

        public int GetRefCount() => _referenceCount;
    }

    /// <summary>
    /// 地图对象 - 可以在地图上显示的对象
    /// </summary>
    public abstract class MapObject : GameObject
    {
        public int MapId { get; set; }
        public ushort X { get; set; }
        public ushort Y { get; set; }
        public LogicMap? CurrentMap { get; set; }
        
        // 对象类型
        public abstract ObjectType GetObjectType();
        
        // 是否可见
        public bool IsVisible { get; set; } = true;
        
        protected MapObject()
        {
            MapId = -1;
            X = 0;
            Y = 0;
        }

        /// <summary>
        /// 设置位置
        /// </summary>
        public virtual void SetPosition(ushort x, ushort y)
        {
            ushort oldX = X;
            ushort oldY = Y;
            X = x;
            Y = y;
            OnPositionChanged(oldX, oldY, x, y);
        }

        /// <summary>
        /// 进入地图
        /// </summary>
        public virtual bool EnterMap(LogicMap map, ushort x, ushort y)
        {
            if (CurrentMap != null)
            {
                LeaveMap();
            }

            CurrentMap = map;
            MapId = (int)map.MapId;
            X = x;
            Y = y;

            OnEnterMap(map);
            return true;
        }

        /// <summary>
        /// 离开地图
        /// </summary>
        public virtual bool LeaveMap()
        {
            if (CurrentMap == null)
                return false;

            var map = CurrentMap;
            CurrentMap = null;
            MapId = -1;

            OnLeaveMap(map);
            return true;
        }

        /// <summary>
        /// 获取可视消息
        /// </summary>
        public abstract bool GetViewMsg(out byte[] msg, MapObject? viewer = null);

        // 事件回调
        protected virtual void OnPositionChanged(ushort oldX, ushort oldY, ushort newX, ushort newY) { }
        protected virtual void OnEnterMap(LogicMap map) { }
        protected virtual void OnLeaveMap(LogicMap map) { }
    }

    /// <summary>
    /// 对象类型枚举
    /// </summary>
    public enum ObjectType
    {
        Player = 0,         // 玩家
        NPC = 1,           // NPC
        Monster = 2,       // 怪物
        Item = 3,          // 物品
        Event = 4,         // 事件
        VisibleEvent = 5,  // 可见事件
        Map = 6,           // 地图
        ScriptEvent = 7,   // 脚本事件
        DownItem = 8,      // 掉落物品
        Max = 9,
        MineSpot = 10,    // 采矿点
        MonsterCorpse = 11 // 怪物尸体
    }

    /// <summary>
    /// 可见对象信息
    /// </summary>
    public class VisibleObject
    {
        public MapObject? Object { get; set; }
        public uint UpdateFlag { get; set; }
        
        public VisibleObject()
        {
            Object = null;
            UpdateFlag = 0;
        }
    }

    /// <summary>
    /// 对象引用 - 用于安全引用其他对象
    /// </summary>
    public class ObjectReference<T> where T : GameObject
    {
        private T? _object;
        private uint _instanceKey;

        public void SetObject(T? obj)
        {
            if (_object != null)
            {
                _object.DecRef();
            }

            _object = obj;
            if (_object != null)
            {
                _object.AddRef();
                _instanceKey = _object.InstanceKey;
            }
            else
            {
                _instanceKey = 0;
            }
        }

        public T? GetObject()
        {
            return IsValid() ? _object : null;
        }

        public bool IsValid()
        {
            if (_object == null)
                return false;
            
            return _object.InstanceKey == _instanceKey;
        }

        public void Clear()
        {
            SetObject(null);
        }
    }

    /// <summary>
    /// 对象进程/事件
    /// </summary>
    public class ObjectProcess
    {
        public ProcessType Type { get; set; }
        public uint Param1 { get; set; }
        public uint Param2 { get; set; }
        public uint Param3 { get; set; }
        public uint Param4 { get; set; }
        public uint Delay { get; set; }
        public int RepeatTimes { get; set; }
        public string? StringParam { get; set; }
        public DateTime ExecuteTime { get; set; }

        public ObjectProcess(ProcessType type)
        {
            Type = type;
            ExecuteTime = DateTime.Now;
        }

        public bool ShouldExecute()
        {
            return DateTime.Now >= ExecuteTime.AddMilliseconds(Delay);
        }
    }

    /// <summary>
    /// 进程类型
    /// </summary>
    public enum ProcessType
    {
        None = 0,
        BeAttack = 1,           // 被攻击
        BeMagicAttack = 2,      // 被魔法攻击
        ClearStatus = 3,        // 清除状态
        Die = 4,                // 死亡
        Relive = 5,             // 复活
        TakeDamage = 6,         // 受到伤害
        Heal = 7,               // 治疗
        AddBuff = 8,            // 添加Buff
        RemoveBuff = 9,         // 移除Buff
        Cast = 10,              // 施法
        Max = 11
    }
}
