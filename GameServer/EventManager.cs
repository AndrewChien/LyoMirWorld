using System;
using System.Collections.Generic;
using System.Linq;
using MirCommon;
using MirCommon.Utils;

namespace GameServer
{
    /// <summary>
    /// 地图单元格事件标志
    /// </summary>
    public static class MapEventFlags
    {
        public const ushort EVENTFLAG_ENTEREVENT = 0x8000;  // 进入事件标志
        public const ushort EVENTFLAG_LEAVEEVENT = 0x4000;  // 离开事件标志
        public const ushort EVENTFLAG_CITYEVENT = 0x2000;   // 城市事件标志
    }

    /// <summary>
    /// 地图单元格信息类
    /// </summary>
    public class MapCellInfo
    {
        /// <summary>
        /// 事件标志
        /// </summary>
        public ushort EventFlag { get; set; }

        /// <summary>
        /// 对象列表
        /// </summary>
        public LinkedList<MapObject> ObjectList { get; } = new LinkedList<MapObject>();

        /// <summary>
        /// 获取对象数量
        /// </summary>
        public int GetObjectCount()
        {
            return ObjectList.Count;
        }

        /// <summary>
        /// 获取第一个对象节点
        /// </summary>
        public LinkedListNode<MapObject>? GetFirstNode()
        {
            return ObjectList.First;
        }

        /// <summary>
        /// 添加对象到单元格
        /// </summary>
        public void AddObject(MapObject obj)
        {
            ObjectList.AddLast(obj);
        }

        /// <summary>
        /// 从单元格移除对象
        /// </summary>
        public bool RemoveObject(MapObject obj)
        {
            return ObjectList.Remove(obj);
        }

        /// <summary>
        /// 检查是否设置了进入事件标志
        /// </summary>
        public bool HasEnterEventFlag()
        {
            return (EventFlag & MapEventFlags.EVENTFLAG_ENTEREVENT) != 0;
        }

        /// <summary>
        /// 检查是否设置了离开事件标志
        /// </summary>
        public bool HasLeaveEventFlag()
        {
            return (EventFlag & MapEventFlags.EVENTFLAG_LEAVEEVENT) != 0;
        }

        /// <summary>
        /// 设置进入事件标志
        /// </summary>
        public void SetEnterEventFlag()
        {
            EventFlag |= MapEventFlags.EVENTFLAG_ENTEREVENT;
        }

        /// <summary>
        /// 设置离开事件标志
        /// </summary>
        public void SetLeaveEventFlag()
        {
            EventFlag |= MapEventFlags.EVENTFLAG_LEAVEEVENT;
        }

        /// <summary>
        /// 清除进入事件标志
        /// </summary>
        public void ClearEnterEventFlag()
        {
            EventFlag &= unchecked((ushort)~MapEventFlags.EVENTFLAG_ENTEREVENT);
        }

        /// <summary>
        /// 清除离开事件标志
        /// </summary>
        public void ClearLeaveEventFlag()
        {
            EventFlag &= unchecked((ushort)~MapEventFlags.EVENTFLAG_LEAVEEVENT);
        }
    }

    /// <summary>
    /// 事件处理器基类
    /// </summary>
    public abstract class EventProcessor
    {
        public EventProcessor()
        {
        }

        /// <summary>
        /// 更新事件处理器
        /// </summary>
        public virtual void Update()
        {
        }

        /// <summary>
        /// 当对象进入事件范围时调用
        /// </summary>
        public virtual void OnEnter(VisibleEvent visibleEvent, MapObject mapObject)
        {
        }

        /// <summary>
        /// 当对象离开事件范围时调用
        /// </summary>
        public virtual void OnLeave(VisibleEvent visibleEvent, MapObject mapObject)
        {
        }

        /// <summary>
        /// 当事件更新时调用
        /// </summary>
        public virtual void OnUpdate(VisibleEvent visibleEvent)
        {
        }

        /// <summary>
        /// 当事件关闭时调用
        /// </summary>
        public virtual void OnClose(VisibleEvent visibleEvent)
        {
        }

        /// <summary>
        /// 当事件创建时调用
        /// </summary>
        public virtual void OnCreate(VisibleEvent visibleEvent)
        {
        }
    }

    /// <summary>
    /// 事件对象基类
    /// </summary>
    public class EventObject : MapObject
    {
        protected bool _disabled;

        public EventObject()
        {
            _disabled = false;
        }

        /// <summary>
        /// 清理事件对象
        /// </summary>
        public override void Clean()
        {
            base.Clean();
        }

        /// <summary>
        /// 当对象进入事件范围时调用
        /// </summary>
        public virtual void OnEnter(MapObject mapObject)
        {
        }

        /// <summary>
        /// 当对象离开事件范围时调用
        /// </summary>
        public virtual void OnLeave(MapObject mapObject)
        {
        }

        /// <summary>
        /// 禁用事件
        /// </summary>
        public virtual void Disable()
        {
            _disabled = true;
        }

        /// <summary>
        /// 启用事件
        /// </summary>
        public virtual void Enable()
        {
            _disabled = false;
        }

        /// <summary>
        /// 检查事件是否被禁用
        /// </summary>
        public bool IsDisabled()
        {
            return _disabled;
        }

        /// <summary>
        /// 设置进入标志
        /// </summary>
        public void SetEnterFlag(LogicMap map)
        {
            var cellInfo = map.GetMapCellInfo(X, Y);
            if (cellInfo != null)
            {
                cellInfo.SetEnterEventFlag();
            }
        }

        /// <summary>
        /// 设置离开标志
        /// </summary>
        public void SetLeaveFlag(LogicMap map)
        {
            var cellInfo = map.GetMapCellInfo(X, Y);
            if (cellInfo != null)
            {
                cellInfo.SetLeaveEventFlag();
            }
        }

        /// <summary>
        /// 当离开地图时调用
        /// </summary>
        protected override void OnLeaveMap(LogicMap map)
        {
            var cellInfo = map.GetMapCellInfo(X, Y);
            if (cellInfo != null)
            {
                // 检查是否设置了进入或离开事件标志
                if (cellInfo.HasEnterEventFlag() || cellInfo.HasLeaveEventFlag())
                {
                    // 计算同一单元格中其他事件对象的数量
                    int eventCount = 0;
                    var node = cellInfo.GetFirstNode();
                    while (node != null)
                    {
                        var obj = node.Value;
                        if (obj != this && obj.GetObjectType() == ObjectType.Event)
                        {
                            eventCount++;
                        }
                        node = node.Next;
                    }
                    
                    // 如果没有其他事件对象，清除标志
                    if (eventCount == 0)
                    {
                        if (cellInfo.HasEnterEventFlag())
                        {
                            cellInfo.ClearEnterEventFlag();
                        }
                        if (cellInfo.HasLeaveEventFlag())
                        {
                            cellInfo.ClearLeaveEventFlag();
                        }
                    }
                }
            }
            
            base.OnLeaveMap(map);
        }

        /// <summary>
        /// 获取对象类型
        /// </summary>
        public override ObjectType GetObjectType()
        {
            return ObjectType.Event;
        }

        /// <summary>
        /// 获取可视消息
        /// </summary>
        public override bool GetViewMsg(out byte[] msg, MapObject? viewer = null)
        {
            msg = new byte[0];
            // TODO: 实现事件对象的可视消息
            return false;
        }
    }

    /// <summary>
    /// 可见事件类
    /// </summary>
    public class VisibleEvent : EventObject
    {
        private uint _view;
        private ServerTimer _runTimer;
        private ServerTimer _closeTimer;
        private EventProcessor _eventProcessor;
        private bool _closed;
        private uint _param1;
        private uint _param2;

        public VisibleEvent()
        {
            _view = 0;
            _closed = false;
            _param1 = 0;
            _param2 = 0;
            _runTimer = new ServerTimer();
            _closeTimer = new ServerTimer();
        }

        /// <summary>
        /// 创建可见事件
        /// </summary>
        public bool Create(LogicMap map, int x, int y, uint view, uint runTick, uint lastTime, 
                          EventProcessor processor, uint param1 = 0, uint param2 = 0)
        {
            try
            {
                // 设置基本属性
                MapId = (int)map.MapId;
                X = (ushort)x;
                Y = (ushort)y;
                _view = view;
                _param1 = param1;
                _param2 = param2;
                _eventProcessor = processor;
                _closed = false;

                // 设置计时器
                _runTimer.SetInterval(runTick);
                _closeTimer.SetInterval(lastTime);

                // 添加到地图
                if (!map.AddObject(this, x, y))
                {
                    LogManager.Default.Warning($"无法将事件添加到地图: {map.MapId}");
                    return false;
                }

                // 调用处理器的OnCreate方法
                _eventProcessor?.OnCreate(this);

                LogManager.Default.Debug($"创建可见事件: 地图={map.MapId}, 位置=({x},{y}), 视野={view}");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"创建可见事件失败", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 关闭事件
        /// </summary>
        public void Close()
        {
            if (_closed)
                return;

            _closed = true;
            
            // 调用处理器的OnClose方法
            _eventProcessor?.OnClose(this);

            // 从地图移除
            var map = MapManager.Instance.GetMap((uint)MapId);
            if (map != null)
            {
                map.RemoveObject(this);
            }

            LogManager.Default.Debug($"关闭可见事件: 地图={MapId}, 位置=({X},{Y})");
        }

        /// <summary>
        /// 清理事件
        /// </summary>
        public override void Clean()
        {
            _eventProcessor = null;
            base.Clean();
        }

        /// <summary>
        /// 获取视图消息
        /// </summary>
        public bool GetViewMessage(out string message, MapObject viewer = null)
        {
            try
            {
                byte[] buffer = new byte[1024];
                uint[] paramArray = new uint[] { _param1, _param2 };
                
                // 使用GameCodec.EncodeMsg编码消息
                int length = MirCommon.GameCodec.EncodeMsg(
                    buffer,
                    (uint)this.ObjectId,  // dwFlag - 对象ID
                    804,                  // wCmd - 可见事件出现消息
                    (ushort)(_view & 0xffff),  // w1 - 视野
                    (ushort)X,            // w2 - X坐标
                    (ushort)Y,            // w3 - Y坐标
                    paramArray.SelectMany(BitConverter.GetBytes).ToArray(),  // 参数数据
                    paramArray.Length * sizeof(uint)  // 数据大小
                );
                
                message = Convert.ToBase64String(buffer, 0, length);
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"获取视图消息失败", exception: ex);
                message = string.Empty;
                return false;
            }
        }

        /// <summary>
        /// 获取离开视图消息
        /// </summary>
        public bool GetOutViewMessage(out string message, MapObject viewer = null)
        {
            try
            {
                byte[] buffer = new byte[1024];
                
                // 使用GameCodec.EncodeMsg编码消息
                int length = MirCommon.GameCodec.EncodeMsg(
                    buffer,
                    (uint)this.ObjectId,  // dwFlag - 对象ID
                    805,                  // wCmd - 可见事件消失消息
                    0,                    // w1 - 保留
                    (ushort)X,            // w2 - X坐标
                    (ushort)Y,            // w3 - Y坐标
                    null,                 // 无额外数据
                    0                     // 数据大小为0
                );
                
                message = Convert.ToBase64String(buffer, 0, length);
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"获取离开视图消息失败", exception: ex);
                message = string.Empty;
                return false;
            }
        }

        /// <summary>
        /// 当对象进入事件范围时调用
        /// </summary>
        public override void OnEnter(MapObject mapObject)
        {
            base.OnEnter(mapObject);
            _eventProcessor?.OnEnter(this, mapObject);
        }

        /// <summary>
        /// 当对象离开事件范围时调用
        /// </summary>
        public override void OnLeave(MapObject mapObject)
        {
            base.OnLeave(mapObject);
            _eventProcessor?.OnLeave(this, mapObject);
        }

        /// <summary>
        /// 更新事件有效性
        /// </summary>
        public bool UpdateValid()
        {
            if (_closed || _disabled)
                return false;

            // 检查关闭计时器
            if (_closeTimer.IsTimeOut())
            {
                Close();
                return false;
            }

            // 检查运行计时器
            if (_runTimer.IsTimeOut())
            {
                _eventProcessor?.OnUpdate(this);
                _runTimer.Reset();
            }

            return true;
        }

        /// <summary>
        /// 设置参数
        /// </summary>
        public void SetParam(uint param1, uint param2)
        {
            _param1 = param1;
            _param2 = param2;
        }

        /// <summary>
        /// 获取参数1
        /// </summary>
        public uint GetParam1()
        {
            return _param1;
        }

        /// <summary>
        /// 获取参数2
        /// </summary>
        public uint GetParam2()
        {
            return _param2;
        }

        /// <summary>
        /// 获取视野范围
        /// </summary>
        public uint GetView()
        {
            return _view;
        }

        /// <summary>
        /// 当进入地图时调用
        /// </summary>
        protected override void OnEnterMap(LogicMap map)
        {
            base.OnEnterMap(map);
            
            int mx = X;
            int my = Y;
            
            // 获取视图消息
            if (!GetViewMessage(out string viewMsg))
                return;
                
            byte[] viewMsgBytes = Convert.FromBase64String(viewMsg);
            
            // 向周围25x25范围内的玩家发送可见事件消息
            for (int x = -12; x <= 12; x++)
            {
                for (int y = -12; y <= 12; y++)
                {
                    var cellInfo = map.GetMapCellInfo(mx + x, my + y);
                    if (cellInfo != null && cellInfo.GetObjectCount() > 0)
                    {
                        var node = cellInfo.GetFirstNode();
                        while (node != null)
                        {
                            var obj = node.Value;
                            if (obj.GetObjectType() == ObjectType.Player)
                            {
                                if (obj is HumanPlayer player)
                                {
                                    // 检查玩家是否可以看到可见事件
                                    // 这里应该检查玩家的可见对象标志位
                                    player.SendMessage(viewMsgBytes);
                                }
                            }
                            node = node.Next;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 当离开地图时调用
        /// </summary>
        protected override void OnLeaveMap(LogicMap map)
        {
            int mx = X;
            int my = Y;
            
            // 获取离开视图消息
            if (GetOutViewMessage(out string outViewMsg))
            {
                byte[] outViewMsgBytes = Convert.FromBase64String(outViewMsg);
                
                // 向周围25x25范围内的玩家发送离开事件消息
                for (int x = -12; x <= 12; x++)
                {
                    for (int y = -12; y <= 12; y++)
                    {
                        var cellInfo = map.GetMapCellInfo(mx + x, my + y);
                        if (cellInfo != null && cellInfo.GetObjectCount() > 0)
                        {
                            var node = cellInfo.GetFirstNode();
                            while (node != null)
                            {
                                var obj = node.Value;
                                if (obj.GetObjectType() == ObjectType.Player)
                                {
                                    if (obj is HumanPlayer player)
                                    {
                                        // 检查玩家是否可以看到可见事件
                                        // 这里应该检查玩家的可见对象标志位
                                        player.SendMessage(outViewMsgBytes);
                                    }
                                }
                                node = node.Next;
                            }
                        }
                    }
                }
            }
            
            base.OnLeaveMap(map);
        }

        /// <summary>
        /// 获取事件处理器
        /// </summary>
        public EventProcessor GetProcessor()
        {
            return _eventProcessor;
        }

        /// <summary>
        /// 设置删除计时器
        /// </summary>
        public void SetDelTimer()
        {
            // 设置关闭计时器为30秒
            _closeTimer.SetInterval((uint)(30 * 1000));
        }

        /// <summary>
        /// 检查删除计时器是否超时
        /// </summary>
        public bool IsDelTimerTimeOut(uint timeout)
        {
            return _closeTimer.IsTimeOut(timeout);
        }

        /// <summary>
        /// 获取对象类型
        /// </summary>
        public override ObjectType GetObjectType()
        {
            return ObjectType.VisibleEvent;
        }
    }

    /// <summary>
    /// 事件管理器
    /// 负责管理游戏中的各种事件
    /// </summary>
    public class EventManager
    {
        private static EventManager? _instance;
        public static EventManager Instance => _instance ??= new EventManager();

        // 对象池
        private readonly ObjectPool<VisibleEvent> _visibleEventPool;
        
        // 删除对象队列
        private readonly Queue<VisibleEvent> _deleteObjectQueue;
        
        // 处理器列表
        private readonly LinkedList<EventProcessor> _processorList;
        
        // 可见事件列表
        private readonly LinkedList<MapObject> _visibleEventList;
        
        // 当前更新状态
        private LinkedListNode<MapObject>? _currentUpdateEvent;
        private LinkedListNode<EventProcessor>? _currentUpdateProcessor;

        private readonly object _lock = new();

        public EventManager()
        {
            _visibleEventPool = new ObjectPool<VisibleEvent>(() => new VisibleEvent(), 100);
            _deleteObjectQueue = new Queue<VisibleEvent>(2000);
            _processorList = new LinkedList<EventProcessor>();
            _visibleEventList = new LinkedList<MapObject>();
            _currentUpdateEvent = null;
            _currentUpdateProcessor = null;
        }

        /// <summary>
        /// 创建新的可见事件
        /// </summary>
        public VisibleEvent? NewVisibleEvent(LogicMap map, int x, int y, uint view, 
                                            uint runTick, uint lastTime, EventProcessor processor,
                                            uint param1 = 0, uint param2 = 0)
        {
            lock (_lock)
            {
                // 检查是否已存在相同位置和视野的事件
                if (map.FindEventObject(x, y, view) != null)
                {
                    LogManager.Default.Debug($"在位置({x},{y})发现相同视野({view})的事件");
                    return null;
                }

                // 从对象池获取事件
                var visibleEvent = _visibleEventPool.Get();
                if (visibleEvent == null)
                {
                    LogManager.Default.Warning("可见事件对象池为空");
                    return null;
                }

                // 创建事件
                if (!visibleEvent.Create(map, x, y, view, runTick, lastTime, processor, param1, param2))
                {
                    _visibleEventPool.Return(visibleEvent);
                    return null;
                }

                // 如果处理器为空，添加到可见事件列表
                if (processor == null)
                {
                    _visibleEventList.AddLast(visibleEvent);
                }

                LogManager.Default.Info($"创建新可见事件: 地图={map.MapId}, 位置=({x},{y}), 视野={view}");
                return visibleEvent;
            }
        }

        /// <summary>
        /// 删除可见事件
        /// </summary>
        public void DelVisibleEvent(VisibleEvent visibleEvent)
        {
            lock (_lock)
            {
                visibleEvent.Close();
                visibleEvent.Clean();
                _visibleEventPool.Return(visibleEvent);
                
                LogManager.Default.Debug($"删除可见事件: 地图={visibleEvent.MapId}, 位置=({visibleEvent.X},{visibleEvent.Y})");
            }
        }

        /// <summary>
        /// 预删除可见事件（放入删除队列）
        /// </summary>
        public void PreDelVisibleEvent(VisibleEvent visibleEvent)
        {
            lock (_lock)
            {
                // 如果处理器为空，从可见事件列表中移除
                if (visibleEvent.GetProcessor() == null)
                {
                    var node = _visibleEventList.Find(visibleEvent);
                    if (node != null)
                    {
                        _visibleEventList.Remove(node);
                    }
                }

                // 设置删除计时器
                visibleEvent.SetDelTimer();

                // 放入删除队列
                if (_deleteObjectQueue.Count < 2000)
                {
                    _deleteObjectQueue.Enqueue(visibleEvent);
                }
                else
                {
                    // 队列满，直接删除
                    DelVisibleEvent(visibleEvent);
                }
            }
        }

        /// <summary>
        /// 更新删除对象队列
        /// </summary>
        public void UpdateDeleteObject()
        {
            lock (_lock)
            {
                int count = _deleteObjectQueue.Count;
                if (count == 0)
                    return;

                var visibleEvent = _deleteObjectQueue.Dequeue();
                if (visibleEvent != null)
                {
                    // 检查删除计时器是否超时（30秒）
                    if (visibleEvent.IsDelTimerTimeOut((uint)(30 * 1000)))
                    {
                        DelVisibleEvent(visibleEvent);
                    }
                    else
                    {
                        // 未超时，重新放回队列
                        _deleteObjectQueue.Enqueue(visibleEvent);
                    }
                }
            }
        }

        /// <summary>
        /// 添加事件处理器
        /// </summary>
        public void AddEventProcessor(EventProcessor processor)
        {
            lock (_lock)
            {
                _processorList.AddLast(processor);
                LogManager.Default.Debug($"添加事件处理器: {processor.GetType().Name}");
            }
        }

        /// <summary>
        /// 移除事件处理器
        /// </summary>
        public void RemoveEventProcessor(EventProcessor processor)
        {
            lock (_lock)
            {
                var node = _processorList.Find(processor);
                if (node != null)
                {
                    _processorList.Remove(node);
                    LogManager.Default.Debug($"移除事件处理器: {processor.GetType().Name}");
                }
            }
        }

        /// <summary>
        /// 更新所有事件
        /// </summary>
        public void UpdateEvents()
        {
            lock (_lock)
            {
                // 更新可见事件
                UpdateVisibleEvents();

                // 更新事件处理器
                UpdateEventProcessors();
            }
        }

        /// <summary>
        /// 更新事件管理器（供GameWorld调用）
        /// </summary>
        public void Update()
        {
            UpdateEvents();
        }

        /// <summary>
        /// 更新可见事件
        /// </summary>
        private void UpdateVisibleEvents()
        {
            var currentNode = _currentUpdateEvent;
            var nextNode = (LinkedListNode<MapObject>?)null;
            
            if (currentNode == null)
                currentNode = _visibleEventList.First;

            uint count = 0;
            while (currentNode != null && count < 100)
            {
                nextNode = currentNode.Next;
                
                var visibleEvent = currentNode.Value as VisibleEvent;
                if (visibleEvent != null)
                {
                    visibleEvent.UpdateValid();
                }
                
                currentNode = nextNode;
                count++;
            }
            
            _currentUpdateEvent = currentNode;
        }

        /// <summary>
        /// 更新事件处理器
        /// </summary>
        private void UpdateEventProcessors()
        {
            var currentProcessor = _currentUpdateProcessor;
            var nextProcessor = (LinkedListNode<EventProcessor>?)null;
            
            if (currentProcessor == null)
                currentProcessor = _processorList.First;

            uint count = 0;
            while (currentProcessor != null && count < 100)
            {
                nextProcessor = currentProcessor.Next;
                currentProcessor.Value.Update();
                currentProcessor = nextProcessor;
                count++;
            }
            
            _currentUpdateProcessor = currentProcessor;
        }

        /// <summary>
        /// 获取可见事件数量
        /// </summary>
        public int GetVisibleEventCount()
        {
            lock (_lock)
            {
                return _visibleEventList.Count;
            }
        }

        /// <summary>
        /// 获取事件处理器数量
        /// </summary>
        public int GetProcessorCount()
        {
            lock (_lock)
            {
                return _processorList.Count;
            }
        }

        /// <summary>
        /// 获取删除队列中的事件数量
        /// </summary>
        public int GetDeleteQueueCount()
        {
            lock (_lock)
            {
                return _deleteObjectQueue.Count;
            }
        }

        /// <summary>
        /// 清理所有事件
        /// </summary>
        public void ClearAllEvents()
        {
            lock (_lock)
            {
                // 清理可见事件列表
                foreach (var mapObject in _visibleEventList)
                {
                    if (mapObject is VisibleEvent visibleEvent)
                    {
                        DelVisibleEvent(visibleEvent);
                    }
                }
                _visibleEventList.Clear();

                // 清理删除队列
                while (_deleteObjectQueue.Count > 0)
                {
                    var visibleEvent = _deleteObjectQueue.Dequeue();
                    DelVisibleEvent(visibleEvent);
                }

                // 清理处理器列表
                _processorList.Clear();

                // 重置更新状态
                _currentUpdateEvent = null;
                _currentUpdateProcessor = null;

                LogManager.Default.Info("清理所有事件");
            }
        }
    }
}
