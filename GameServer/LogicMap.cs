using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using MirCommon.Utils;

namespace GameServer
{
    /// <summary>
    /// 逻辑地图类
    /// </summary>
    public class LogicMap : MapObject
    {
        /// <summary>
        /// 地图ID
        /// </summary>
        public uint MapId { get; set; }
        
        /// <summary>
        /// 地图名称
        /// </summary>
        public string MapName { get; set; } = string.Empty;
        
        /// <summary>
        /// 地图宽度
        /// </summary>
        public int Width { get; set; }
        
        /// <summary>
        /// 地图高度
        /// </summary>
        public int Height { get; set; }
        
        /// <summary>
        /// 最小等级限制
        /// </summary>
        public int MinLevel { get; set; }
        
        /// <summary>
        /// 最大等级限制
        /// </summary>
        public int MaxLevel { get; set; }
        
        /// <summary>
        /// 需要物品
        /// </summary>
        public string NeedItem { get; set; } = string.Empty;
        
        /// <summary>
        /// 需要任务
        /// </summary>
        public string NeedQuest { get; set; } = string.Empty;
        
        /// <summary>
        /// 脚本文件
        /// </summary>
        public string ScriptFile { get; set; } = string.Empty;
        
        /// <summary>
        /// 地图上的对象
        /// </summary>
        private readonly Dictionary<uint, MapObject> _objects = new();
        
        /// <summary>
        /// 地图上的玩家
        /// </summary>
        private readonly Dictionary<uint, HumanPlayer> _players = new();
        
        /// <summary>
        /// 地图上的怪物
        /// </summary>
        private readonly Dictionary<uint, Monster> _monsters = new();
        
        /// <summary>
        /// 地图上的NPC
        /// </summary>
        private readonly Dictionary<uint, Npc> _npcs = new();
        
        /// <summary>
        /// 地图上的物品
        /// </summary>
        private readonly Dictionary<uint, MapItem> _items = new();
        
        /// <summary>
        /// 地图单元格信息数组
        /// </summary>
        private MapCellInfo[,]? _mapCellInfo;
        
        /// <summary>
        /// 地图经验倍率
        /// </summary>
        public float ExpFactor { get; set; } = 1.0f;
        
        /// <summary>
        /// 地图掉落倍率
        /// </summary>
        public float DropFactor { get; set; } = 1.0f;
        
        /// <summary>
        /// 地图是否安全区
        /// </summary>
        public bool IsSafeZone { get; set; }
        
        /// <summary>
        /// 地图是否可以PK
        /// </summary>
        public bool AllowPK { get; set; }
        
        /// <summary>
        /// 地图是否可以召唤宠物
        /// </summary>
        public bool AllowPets { get; set; }
        
        /// <summary>
        /// 地图是否可以骑马
        /// </summary>
        public bool AllowMounts { get; set; }
        
        /// <summary>
        /// 地图是否可以传送
        /// </summary>
        public bool AllowTeleport { get; set; }
        
        /// <summary>
        /// 地图是否可以回城
        /// </summary>
        public bool AllowRecall { get; set; }
        
        /// <summary>
        /// 地图标志位
        /// </summary>
        public MapFlag MapFlags { get; set; }
        
        /// <summary>
        /// 地图标志参数存储
        /// 用于存储带参数的标志，如levelbelow(22,16)、noreconnect(16)、mine(300)
        /// </summary>
        private readonly Dictionary<MapFlag, List<uint>> _flagParams = new();
        
        /// <summary>
        /// 地图天气
        /// </summary>
        public MapWeather Weather { get; set; }
        
        /// <summary>
        /// 地图时间
        /// </summary>
        public MapTime Time { get; set; }
        
        /// <summary>
        /// 地图音乐
        /// </summary>
        public string Music { get; set; } = string.Empty;
        
        /// <summary>
        /// 地图背景
        /// </summary>
        public string Background { get; set; } = string.Empty;
        public object TownX { get; internal set; }
        public object TownY { get; internal set; }
        public object GuildX { get; internal set; }
        public object GuildY { get; internal set; }

        /// <summary>
        /// 矿石列表
        /// </summary>
        private readonly List<MineItem> _mineItems = new();
        
        /// <summary>
        /// 最大矿石率
        /// </summary>
        private uint _mineRateMax = 0;
        
        /// <summary>
        /// 关联的物理地图
        /// </summary>
        private PhysicsMap? _physicsMap;
        
        /// <summary>
        /// 小地图ID
        /// </summary>
        private int _miniMapId;
        
        /// <summary>
        /// 链接数量
        /// </summary>
        private int _linkCount;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public LogicMap(uint mapId, string mapName, int width, int height)
        {
            MapId = mapId;
            MapName = mapName;
            Width = width;
            Height = height;
            
            // 初始化地图单元格信息数组
            if (width > 0 && height > 0)
            {
                _mapCellInfo = new MapCellInfo[width, height];
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        _mapCellInfo[x, y] = new MapCellInfo();
                    }
                }
            }
        }
        
        /// <summary>
        /// 获取经验倍率
        /// </summary>
        public float GetExpFactor()
        {
            return ExpFactor;
        }
        
        /// <summary>
        /// 获取掉落倍率
        /// </summary>
        public float GetDropFactor()
        {
            return DropFactor;
        }
        
        /// <summary>
        /// 获取对象
        /// </summary>
        public MapObject? GetObject(uint objectId)
        {
            return _objects.TryGetValue(objectId, out var obj) ? obj : null;
        }
        
        /// <summary>
        /// 获取玩家
        /// </summary>
        public HumanPlayer? GetPlayer(uint playerId)
        {
            return _players.TryGetValue(playerId, out var player) ? player : null;
        }
        
        /// <summary>
        /// 获取怪物
        /// </summary>
        public Monster? GetMonster(uint monsterId)
        {
            return _monsters.TryGetValue(monsterId, out var monster) ? monster : null;
        }
        
        /// <summary>
        /// 获取NPC
        /// </summary>
        public Npc? GetNPC(uint npcId)
        {
            return _npcs.TryGetValue(npcId, out var npc) ? npc : null;
        }
        
        /// <summary>
        /// 获取物品
        /// </summary>
        public MapItem? GetItem(uint itemId)
        {
            return _items.TryGetValue(itemId, out var item) ? item : null;
        }

        /// <summary>
        /// 添加对象到地图
        /// </summary>
        public bool AddObject(MapObject obj, int x, int y)
        {
            if (obj == null || x < 0 || x >= Width || y < 0 || y >= Height)
                return false;
                
            obj.X = (ushort)x;
            obj.Y = (ushort)y;
            obj.CurrentMap = this;
            
            _objects[obj.ObjectId] = obj;
            
            // 添加到地图单元格
            if (_mapCellInfo != null)
            {
                _mapCellInfo[x, y].AddObject(obj);
            }
            
            // 根据对象类型添加到相应的字典
            switch (obj.GetObjectType())
            {
                case ObjectType.Player:
                    if (obj is HumanPlayer player)
                        _players[obj.ObjectId] = player;
                    break;
                case ObjectType.Monster:
                    if (obj is Monster monster)
                        _monsters[obj.ObjectId] = monster;
                    break;
                case ObjectType.NPC:
                    if (obj is Npc npc)
                        _npcs[obj.ObjectId] = npc;
                    break;
                case ObjectType.Item:
                    if (obj is MapItem item)
                        _items[obj.ObjectId] = item;
                    break;
            }
            
            return true;
        }
        
        /// <summary>
        /// 从地图移除对象
        /// </summary>
        public bool RemoveObject(MapObject obj)
        {
            if (obj == null || !_objects.ContainsKey(obj.ObjectId))
                return false;
                
            _objects.Remove(obj.ObjectId);
            
            // 从地图单元格移除
            if (_mapCellInfo != null && obj.X < Width && obj.Y < Height)
            {
                _mapCellInfo[obj.X, obj.Y].RemoveObject(obj);
            }
            
            // 根据对象类型从相应的字典中移除
            switch (obj.GetObjectType())
            {
                case ObjectType.Player:
                    _players.Remove(obj.ObjectId);
                    break;
                case ObjectType.Monster:
                    _monsters.Remove(obj.ObjectId);
                    break;
                case ObjectType.NPC:
                    _npcs.Remove(obj.ObjectId);
                    break;
                case ObjectType.Item:
                    _items.Remove(obj.ObjectId);
                    break;
            }
            
            obj.CurrentMap = null;
            return true;
        }
        
        /// <summary>
        /// 移除对象（通过ID）
        /// </summary>
        public bool RemoveObject(uint objectId)
        {
            if (!_objects.TryGetValue(objectId, out var obj))
                return false;
                
            return RemoveObject(obj);
        }
        
        /// <summary>
        /// 检查是否可以移动到指定位置
        /// </summary>
        public bool CanMoveTo(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return false;
                
            // 这里应该检查地图障碍物、其他对象等
            return true;
        }
        
        /// <summary>
        /// 验证坐标是否在地图范围内
        /// </summary>
        public bool VerifyPos(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }
        
        /// <summary>
        /// 检查位置是否被阻挡
        /// </summary>
        public bool IsBlocked(int x, int y)
        {
            if (!VerifyPos(x, y))
                return true;
                
            if (IsLocked(x, y))
                return true;
                
            // 检查物理地图是否阻挡
            return IsPhysicsBlocked(x, y);
        }
        
        /// <summary>
        /// 检查物理地图是否阻挡
        /// </summary>
        public bool IsPhysicsBlocked(int x, int y)
        {
            // 检查关联的物理地图
            if (_physicsMap == null)
                return false;
                
            // 调用物理地图的IsBlocked方法
            return _physicsMap.IsBlocked(x, y);
        }
        
        /// <summary>
        /// 检查位置是否被锁定
        /// </summary>
        public bool IsLocked(int x, int y)
        {
            return false;
        }
        
        /// <summary>
        /// 获取有效点
        /// 在指定位置周围搜索可移动的点
        /// </summary>
        public int GetValidPoint(int x, int y, Point[] ptArray, int arraySize)
        {
            if (ptArray == null || arraySize <= 0)
                return 0;
                
            // 搜索点数组
            Point[] searchPoints = new Point[]
            {
                new Point(-1, -1), new Point(0, -1), new Point(1, -1), new Point(1, 0),
                new Point(1, 1), new Point(0, 1), new Point(-1, 1), new Point(-1, 0),
                new Point(-2, -2), new Point(-1, -2), new Point(0, -2), new Point(1, -2),
                new Point(2, -2), new Point(2, -1), new Point(2, 0), new Point(2, 1),
                new Point(2, 2), new Point(1, 2), new Point(0, 2), new Point(-1, 2),
                new Point(-2, 2), new Point(-2, 1), new Point(-2, 0), new Point(-2, -1),
                new Point(-3, -3), new Point(-2, -3), new Point(-1, -3), new Point(0, -3),
                new Point(1, -3), new Point(2, -3), new Point(3, -3), new Point(3, -2),
                new Point(3, -1), new Point(3, 0), new Point(3, 1), new Point(3, 2),
                new Point(3, 3), new Point(2, 3), new Point(1, 3), new Point(0, 3),
                new Point(-1, 3), new Point(-2, 3), new Point(-3, 3), new Point(-3, 2),
                new Point(-3, 1), new Point(-3, 0), new Point(-3, -1), new Point(-3, -2),
                new Point(-4, -4), new Point(-3, -4), new Point(-2, -4), new Point(-1, -4),
                new Point(0, -4), new Point(1, -4), new Point(2, -4), new Point(3, -4),
                new Point(4, -4), new Point(4, -3), new Point(4, -2), new Point(4, -1),
                new Point(4, 0), new Point(4, 1), new Point(4, 2), new Point(4, 3),
                new Point(4, 4), new Point(3, 4), new Point(2, 4), new Point(1, 4),
                new Point(0, 4), new Point(-1, 4), new Point(-2, 4), new Point(-3, 4),
                new Point(-4, 4), new Point(-4, 3), new Point(-4, 2), new Point(-4, 1),
                new Point(-4, 0), new Point(-4, -1), new Point(-4, -2), new Point(-4, -3)
            };
            
            int count = 0;
            for (int i = 0; i < searchPoints.Length && count < arraySize; i++)
            {
                int newX = x + searchPoints[i].X;
                int newY = y + searchPoints[i].Y;
                
                if (!IsBlocked(newX, newY))
                {
                    ptArray[count] = new Point(newX, newY);
                    count++;
                }
            }
            
            return count;
        }
        
        /// <summary>
        /// 获取掉落物品点
        /// 在指定位置周围搜索适合掉落物品的点
        /// </summary>
        public int GetDropItemPoint(int x, int y, Point[] ptArray, int arraySize)
        {
            if (ptArray == null || arraySize <= 0)
                return 0;
                
            // 搜索点数组
            Point[] searchPoints = new Point[]
            {
                new Point(-1, -1), new Point(0, -1), new Point(1, -1), new Point(1, 0),
                new Point(1, 1), new Point(0, 1), new Point(-1, 1), new Point(-1, 0),
                new Point(-2, -2), new Point(-1, -2), new Point(0, -2), new Point(1, -2),
                new Point(2, -2), new Point(2, -1), new Point(2, 0), new Point(2, 1),
                new Point(2, 2), new Point(1, 2), new Point(0, 2), new Point(-1, 2),
                new Point(-2, 2), new Point(-2, 1), new Point(-2, 0), new Point(-2, -1),
                new Point(-3, -3), new Point(-2, -3), new Point(-1, -3), new Point(0, -3),
                new Point(1, -3), new Point(2, -3), new Point(3, -3), new Point(3, -2),
                new Point(3, -1), new Point(3, 0), new Point(3, 1), new Point(3, 2),
                new Point(3, 3), new Point(2, 3), new Point(1, 3), new Point(0, 3),
                new Point(-1, 3), new Point(-2, 3), new Point(-3, 3), new Point(-3, 2),
                new Point(-3, 1), new Point(-3, 0), new Point(-3, -1), new Point(-3, -2),
                new Point(-4, -4), new Point(-3, -4), new Point(-2, -4), new Point(-1, -4),
                new Point(0, -4), new Point(1, -4), new Point(2, -4), new Point(3, -4),
                new Point(4, -4), new Point(4, -3), new Point(4, -2), new Point(4, -1),
                new Point(4, 0), new Point(4, 1), new Point(4, 2), new Point(4, 3),
                new Point(4, 4), new Point(3, 4), new Point(2, 4), new Point(1, 4),
                new Point(0, 4), new Point(-1, 4), new Point(-2, 4), new Point(-3, 4),
                new Point(-4, 4), new Point(-4, 3), new Point(-4, 2), new Point(-4, 1),
                new Point(-4, 0), new Point(-4, -1), new Point(-4, -2), new Point(-4, -3)
            };
            
            int[] drops = new int[searchPoints.Length];
            for (int i = 0; i < drops.Length; i++)
            {
                drops[i] = -1; // 初始化为-1，表示未检查
            }
            
            int dropPointCount = 0;
            
            for (int i = 0; i < arraySize; i++)
            {
                Point? bestPoint = null;
                int bestCount = int.MaxValue;
                int bestIndex = -1;
                
                for (int j = 0; j < searchPoints.Length; j++)
                {
                    if (drops[j] == -1)
                    {
                        int newX = x + searchPoints[j].X;
                        int newY = y + searchPoints[j].Y;
                        
                        if (IsBlocked(newX, newY))
                        {
                            drops[j] = -2; // 标记为阻挡
                            continue;
                        }
                        
                        // 计算该位置的掉落物品数量
                        drops[j] = GetDupCount(newX, newY, ObjectType.Item);
                    }
                    
                    if (drops[j] == -2) // 阻挡位置
                    {
                        continue;
                    }
                    
                    if (drops[j] < 10) 
                    {
                        if (bestPoint == null || drops[j] < bestCount)
                        {
                            bestCount = drops[j];
                            bestPoint = searchPoints[j];
                            bestIndex = j;
                            
                            if (bestCount == 0)
                                break;
                        }
                    }
                }
                
                if (bestPoint == null)
                    break;
                    
                drops[bestIndex]++;
                ptArray[dropPointCount] = new Point(x + bestPoint.Value.X, y + bestPoint.Value.Y);
                dropPointCount++;
                
                if (dropPointCount >= arraySize)
                    return dropPointCount;
            }
            
            return dropPointCount;
        }
        
        /// <summary>
        /// 获取指定位置的对象重复数量
        /// </summary>
        public int GetDupCount(int x, int y)
        {
            int dupCount = 0;
            
            foreach (var obj in _objects.Values)
            {
                if (obj.X == x && obj.Y == y)
                {
                    var objType = obj.GetObjectType();
                    if (objType == ObjectType.Monster || objType == ObjectType.NPC || objType == ObjectType.Player)
                    {
                        if (obj is AliveObject aliveObj && !aliveObj.IsDead)
                        {
                            dupCount++;
                        }
                    }
                }
            }
            
            return dupCount;
        }
        
        /// <summary>
        /// 获取指定位置和类型的对象重复数量
        /// </summary>
        public int GetDupCount(int x, int y, ObjectType type)
        {
            // 如果是阻挡位置，返回-1
            if (IsPhysicsBlocked(x, y))
                return -1;
                
            int dupCount = 0;
            
            foreach (var obj in _objects.Values)
            {
                if (obj.X == x && obj.Y == y && obj.GetObjectType() == type)
                {
                    dupCount++;
                }
            }
            
            return dupCount;
        }
        
        /// <summary>
        /// 检查是否可以行走（CanWalk方法）
        /// </summary>
        public bool CanWalk(int x, int y)
        {
            return CanMoveTo(x, y);
        }
        
        /// <summary>
        /// 移动对象（MoveObject方法）
        /// </summary>
        public bool MoveObject(MapObject obj, int newX, int newY)
        {
            if (obj == null || !CanMoveTo(newX, newY))
                return false;
                
            obj.X = (ushort)newX;
            obj.Y = (ushort)newY;
            return true;
        }
        
        /// <summary>
        /// 广播消息给范围内的玩家（BroadcastMessageInRange方法）
        /// </summary>
        public void BroadcastMessageInRange(int centerX, int centerY, int range, byte[] message)
        {
            SendToNearbyPlayers(centerX, centerY, range, message);
        }
        
        /// <summary>
        /// 广播消息给所有玩家（BroadcastMessage方法）
        /// </summary>
        public void BroadcastMessage(byte[] message)
        {
            SendToAllPlayers(message);
        }
        
        /// <summary>
        /// 获取指定范围内的对象
        /// </summary>
        public List<MapObject> GetObjectsInRange(int centerX, int centerY, int range)
        {
            var result = new List<MapObject>();
            
            foreach (var obj in _objects.Values)
            {
                int dx = Math.Abs(obj.X - centerX);
                int dy = Math.Abs(obj.Y - centerY);
                
                if (dx <= range && dy <= range)
                {
                    result.Add(obj);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 获取指定范围内的玩家
        /// </summary>
        public List<HumanPlayer> GetPlayersInRange(int centerX, int centerY, int range)
        {
            var result = new List<HumanPlayer>();
            
            foreach (var player in _players.Values)
            {
                int dx = Math.Abs(player.X - centerX);
                int dy = Math.Abs(player.Y - centerY);
                
                if (dx <= range && dy <= range)
                {
                    result.Add(player);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 获取指定范围内的怪物
        /// </summary>
        public List<Monster> GetMonstersInRange(int centerX, int centerY, int range)
        {
            var result = new List<Monster>();
            
            foreach (var monster in _monsters.Values)
            {
                int dx = Math.Abs(monster.X - centerX);
                int dy = Math.Abs(monster.Y - centerY);
                
                if (dx <= range && dy <= range)
                {
                    result.Add(monster);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 获取指定范围内的NPC
        /// </summary>
        public List<Npc> GetNPCsInRange(int centerX, int centerY, int range)
        {
            var result = new List<Npc>();
            
            foreach (var npc in _npcs.Values)
            {
                int dx = Math.Abs(npc.X - centerX);
                int dy = Math.Abs(npc.Y - centerY);
                
                if (dx <= range && dy <= range)
                {
                    result.Add(npc);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 获取指定范围内的物品
        /// </summary>
        public List<MapItem> GetItemsInRange(int centerX, int centerY, int range)
        {
            var result = new List<MapItem>();
            
            foreach (var item in _items.Values)
            {
                int dx = Math.Abs(item.X - centerX);
                int dy = Math.Abs(item.Y - centerY);
                
                if (dx <= range && dy <= range)
                {
                    result.Add(item);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 获取指定位置的对象
        /// </summary>
        public MapObject? GetObjectAt(int x, int y)
        {
            foreach (var obj in _objects.Values)
            {
                if (obj.X == x && obj.Y == y)
                {
                    return obj;
                }
            }
            return null;
        }

        /// <summary>
        /// 获取地图单元格信息
        /// </summary>
        public MapCellInfo? GetMapCellInfo(int x, int y)
        {
            if (_mapCellInfo == null || x < 0 || x >= Width || y < 0 || y >= Height)
                return null;
                
            return _mapCellInfo[x, y];
        }
        
        /// <summary>
        /// 检查是否是战斗地图
        /// </summary>
        public bool IsFightMap()
        {
            // 战斗地图通常不是安全区且允许PK
            return !IsSafeZone && AllowPK;
        }

        /// <summary>
        /// 查找指定位置和视野的事件对象
        /// </summary>
        public MapObject? FindEventObject(int x, int y, uint view)
        {
            foreach (var obj in _objects.Values)
            {
                // 检查对象是否是事件类型
                if (obj.GetObjectType() == ObjectType.Event || obj.GetObjectType() == ObjectType.VisibleEvent)
                {
                    // 检查位置是否相同
                    if (obj.X == x && obj.Y == y)
                    {
                        // 如果是可见事件，检查视野是否相同
                        if (obj is VisibleEvent visibleEvent)
                        {
                            if (visibleEvent.GetView() == view)
                            {
                                return obj;
                            }
                        }
                        else
                        {
                            // 普通事件对象，不需要检查视野
                            return obj;
                        }
                    }
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// 发送消息给范围内的所有玩家
        /// </summary>
        public void SendToNearbyPlayers(int centerX, int centerY, int range, byte[] message)
        {
            var players = GetPlayersInRange(centerX, centerY, range);
            foreach (var player in players)
            {
                player.SendMessage(message);
            }
        }
        
        /// <summary>
        /// 发送消息给范围内的所有玩家（带默认范围）
        /// </summary>
        public void SendToNearbyPlayers(int centerX, int centerY, byte[] message)
        {
            // 默认范围设为10
            SendToNearbyPlayers(centerX, centerY, 10, message);
        }
        
        /// <summary>
        /// 发送消息给地图上的所有玩家
        /// </summary>
        public void SendToAllPlayers(byte[] message)
        {
            foreach (var player in _players.Values)
            {
                player.SendMessage(message);
            }
        }
        
        /// <summary>
        /// 获取地图上的玩家数量
        /// </summary>
        public int GetPlayerCount()
        {
            return _players.Count;
        }
        
        /// <summary>
        /// 获取地图上的怪物数量
        /// </summary>
        public int GetMonsterCount()
        {
            return _monsters.Count;
        }
        
        /// <summary>
        /// 获取地图上的NPC数量
        /// </summary>
        public int GetNPCCount()
        {
            return _npcs.Count;
        }
        
        /// <summary>
        /// 获取地图上的物品数量
        /// </summary>
        public int GetItemCount()
        {
            return _items.Count;
        }
        
        /// <summary>
        /// 获取地图上的总对象数量
        /// </summary>
        public int GetTotalObjectCount()
        {
            return _objects.Count;
        }
        
        /// <summary>
        /// 更新地图
        /// </summary>
        public void Update()
        {
            // 更新所有怪物
            foreach (var monster in _monsters.Values.ToList())
            {
                monster.Update();
            }
            
            // 更新所有NPC
            foreach (var npc in _npcs.Values.ToList())
            {
                npc.Update();
            }
            
            // 检查过期的物品
            var now = DateTime.Now;
            var expiredItems = _items.Values.Where(item => item.ExpireTime.HasValue && item.ExpireTime.Value < now).ToList();
            foreach (var item in expiredItems)
            {
                RemoveObject(item);
            }
        }
        
        /// <summary>
        /// 添加矿石物品
        /// </summary>
        public void AddMineItem(string name, ushort duraMin, ushort duraMax, ushort rate)
        {
            var mineItem = new MineItem
            {
                Name = name,
                DuraMin = duraMin,
                DuraMax = duraMax,
                Rate = rate
            };
            
            _mineItems.Add(mineItem);
            _mineRateMax += rate;
        }
        
        /// <summary>
        /// 获取矿石物品
        /// </summary>
        public bool GotMineItem(HumanPlayer player)
        {
            if (_mineItems.Count == 0 || _mineRateMax == 0)
                return false;
                
            // 随机选择一个矿石
            uint randomValue = (uint)new Random().Next((int)_mineRateMax);
            uint currentRate = 0;
            
            foreach (var mineItem in _mineItems)
            {
                currentRate += mineItem.Rate;
                if (randomValue < currentRate)
                {
                    // 这里应该给玩家添加矿石物品
                    LogManager.Default.Info($"玩家 {player.Name} 在地图 {MapName} 挖到矿石: {mineItem.Name}");
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// 检查地图标志是否设置
        /// </summary>
        public bool IsFlagSeted(MapFlag flag)
        {
            return (MapFlags & flag) != 0;
        }
        
        /// <summary>
        /// 设置地图标志
        /// 支持带参数的标志，如levelbelow(22,16)、noreconnect(16)、mine(300)
        /// </summary>
        public void SetFlag(MapFlag flag)
        {
            MapFlags |= flag;
        }
        
        /// <summary>
        /// 设置带参数的地图标志
        /// </summary>
        public void SetFlag(string flagStr)
        {
            // 解析标志字符串，支持带参数的标志
            ParseAndSetFlag(flagStr);
        }
        
        /// <summary>
        /// 设置带参数的地图标志
        /// </summary>
        public void SetFlag(MapFlag flag, params uint[] parameters)
        {
            MapFlags |= flag;
            
            if (parameters != null && parameters.Length > 0)
            {
                _flagParams[flag] = new List<uint>(parameters);
            }
        }
        
        /// <summary>
        /// 清除地图标志
        /// </summary>
        public void ClearFlag(MapFlag flag)
        {
            MapFlags &= ~flag;
            _flagParams.Remove(flag);
        }
        
        /// <summary>
        /// 获取标志参数
        /// </summary>
        public List<uint>? GetFlagParams(MapFlag flag)
        {
            return _flagParams.TryGetValue(flag, out var parameters) ? parameters : null;
        }
        
        /// <summary>
        /// 获取标志参数（单个参数）
        /// </summary>
        public uint GetFlagParam(MapFlag flag, int index = 0)
        {
            if (_flagParams.TryGetValue(flag, out var parameters) && index >= 0 && index < parameters.Count)
            {
                return parameters[index];
            }
            return 0;
        }
        
        /// <summary>
        /// 检查标志是否设置（带参数检查）
        /// </summary>
        public bool IsFlagSeted(MapFlag flag, uint paramValue = 0)
        {
            if (!IsFlagSeted(flag))
                return false;
                
            if (paramValue == 0)
                return true;
                
            var parameters = GetFlagParams(flag);
            if (parameters == null || parameters.Count == 0)
                return false;
                
            return parameters.Contains(paramValue);
        }
        
        /// <summary>
        /// 解析并设置标志字符串
        /// </summary>
        private void ParseAndSetFlag(string flagStr)
        {
            if (string.IsNullOrEmpty(flagStr))
                return;
                
            string upperFlagStr = flagStr.ToUpper();
            
            // 检查是否带参数
            int paramStart = upperFlagStr.IndexOf('(');
            int paramEnd = upperFlagStr.IndexOf(')');
            
            if (paramStart > 0 && paramEnd > paramStart)
            {
                // 带参数的标志
                string flagName = upperFlagStr.Substring(0, paramStart).Trim();
                string paramStr = upperFlagStr.Substring(paramStart + 1, paramEnd - paramStart - 1);
                
                // 解析参数
                List<uint> parameters = new List<uint>();
                string[] paramParts = paramStr.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (string param in paramParts)
                {
                    if (uint.TryParse(param.Trim(), out uint paramValue))
                    {
                        parameters.Add(paramValue);
                    }
                }
                
                // 根据标志名称设置标志
                MapFlag flag = GetMapFlagFromName(flagName);
                if (flag != MapFlag.MF_NONE)
                {
                    SetFlag(flag, parameters.ToArray());
                    LogManager.Default.Debug($"设置带参数的地图标志: {flagName}({paramStr}) -> {flag}");
                }
            }
            else
            {
                // 不带参数的标志
                MapFlag flag = GetMapFlagFromName(upperFlagStr);
                if (flag != MapFlag.MF_NONE)
                {
                    SetFlag(flag);
                    LogManager.Default.Debug($"设置地图标志: {upperFlagStr} -> {flag}");
                }
            }
        }
        
        /// <summary>
        /// 根据标志名称获取MapFlag枚举
        /// </summary>
        private MapFlag GetMapFlagFromName(string flagName)
        {
            // 这里需要与LogicMapMgr中的GetMapFlagFromString方法保持一致
            switch (flagName)
            {
                case "SABUKPALACE":
                    return MapFlag.MF_NONE;
                case "FIGHTMAP":
                    return MapFlag.MF_FIGHT;
                case "NORANDOMMOVE":
                    return MapFlag.MF_NORUN;
                case "NORECONNECT":
                    return MapFlag.MF_NONE; // 需要特殊处理
                case "RIDEHORSE":
                    return MapFlag.MF_NOMOUNT;
                case "LEVELABOVE":
                case "LEVELBELOW":
                    return MapFlag.MF_NONE; // 等级限制，需要特殊处理
                case "LIMITJOB":
                    return MapFlag.MF_NONE; // 职业限制，需要特殊处理
                case "PKPOINTABOVE":
                case "PKPOINTBELOW":
                    return MapFlag.MF_NONE; // PK值限制，需要特殊处理
                case "NOESCAPE":
                    return MapFlag.MF_NOTELEPORT;
                case "NOHOME":
                    return MapFlag.MF_NORECALL;
                case "MINE":
                    return MapFlag.MF_MINE;
                case "WEATHER":
                case "DAY":
                case "NIGHT":
                    return MapFlag.MF_NONE; // 需要特殊处理
                case "NOGROUPMOVE":
                    return MapFlag.MF_NONE; // 禁止组队移动，需要特殊处理
                case "SANDCITYHOME":
                    return MapFlag.MF_NONE; // 沙城回城点，需要特殊处理
                case "NODMOVE":
                    return MapFlag.MF_NOWALK;
                case "NOFLASHMOVE":
                    return MapFlag.MF_NOTELEPORT;
                case "USERDEFINE1":
                case "USERDEFINE2":
                case "USERDEFINE3":
                case "USERDEFINE4":
                    return MapFlag.MF_NONE; // 用户自定义标志
                case "SAFE":
                    return MapFlag.MF_SAFE;
                case "NOPK":
                    return MapFlag.MF_NOPK;
                case "NOMONSTER":
                    return MapFlag.MF_NOMONSTER;
                case "NOPET":
                    return MapFlag.MF_NOPET;
                case "NODROP":
                    return MapFlag.MF_NODROP;
                case "NOGUILDWAR":
                    return MapFlag.MF_NOGUILDWAR;
                case "NODUEL":
                    return MapFlag.MF_NODUEL;
                case "NOSKILL":
                    return MapFlag.MF_NOSKILL;
                case "NOITEM":
                    return MapFlag.MF_NOITEM;
                case "NOSPELL":
                    return MapFlag.MF_NOSPELL;
                case "NOSIT":
                    return MapFlag.MF_NOSIT;
                case "NOSTAND":
                    return MapFlag.MF_NOSTAND;
                case "NODIE":
                    return MapFlag.MF_NODIE;
                case "NORESPAWN":
                    return MapFlag.MF_NORESPAWN;
                case "NOLOGOUT":
                    return MapFlag.MF_NOLOGOUT;
                case "NOSAVE":
                    return MapFlag.MF_NOSAVE;
                case "NOLOAD":
                    return MapFlag.MF_NOLOAD;
                case "NOSCRIPT":
                    return MapFlag.MF_NOSCRIPT;
                case "NOEVENT":
                    return MapFlag.MF_NOEVENT;
                case "NOMESSAGE":
                    return MapFlag.MF_NOMESSAGE;
                case "NOCHAT":
                    return MapFlag.MF_NOCHAT;
                case "NOWHISPER":
                    return MapFlag.MF_NOWHISPER;
                case "NOSHOUT":
                    return MapFlag.MF_NOSHOUT;
                case "NOTRADE":
                    return MapFlag.MF_NOTRADE;
                case "NOSTORE":
                    return MapFlag.MF_NOSTORE;
                default:
                    LogManager.Default.Warning($"未知的地图标志: {flagName}");
                    return MapFlag.MF_NONE;
            }
        }
        
        /// <summary>
        /// 获取对象类型
        /// </summary>
        public override ObjectType GetObjectType()
        {
            return ObjectType.Map;
        }
        
        /// <summary>
        /// 获取可视消息
        /// </summary>
        public override bool GetViewMsg(out byte[] msg, MapObject? viewer = null)
        {
            // 地图不需要发送可视消息
            msg = Array.Empty<byte>();
            return false;
        }

        /// <summary>
        /// 设置物理地图关联
        /// </summary>
        public void SetPhysicsMap(PhysicsMap physicsMap)
        {
            _physicsMap = physicsMap;
            
            // 调用物理地图的AddRefMap方法
            if (_physicsMap != null)
            {
                _physicsMap.AddRefMap(this);
            }
        }

        /// <summary>
        /// 设置小地图ID
        /// </summary>
        public void SetMiniMap(int miniMapId)
        {
            _miniMapId = miniMapId;
        }

        /// <summary>
        /// 设置链接数量
        /// </summary>
        public void SetLinkCount(int linkCount)
        {
            _linkCount = linkCount;
        }


        /// <summary>
        /// 获取小地图ID
        /// </summary>
        public int GetMiniMapId()
        {
            return _miniMapId;
        }

        /// <summary>
        /// 获取链接数量
        /// </summary>
        public int GetLinkCount()
        {
            return _linkCount;
        }

        /// <summary>
        /// 获取关联的物理地图
        /// </summary>
        public PhysicsMap? GetPhysicsMap()
        {
            return _physicsMap;
        }

        /// <summary>
        /// 初始化地图单元格
        /// </summary>
        public void InitMapCells()
        {
            try
            {
                if (_physicsMap == null)
                {
                    LogManager.Default.Error($"地图 {MapName} 没有关联的物理地图，无法初始化单元格");
                    return;
                }

                // 确保宽度和高度有效
                if (Width <= 0 || Height <= 0)
                {
                    LogManager.Default.Error($"地图 {MapName} 尺寸无效: {Width}x{Height}");
                    return;
                }

                // 如果已经初始化过，先清理
                if (_mapCellInfo != null)
                {
                    _mapCellInfo = null;
                }

                // 创建地图单元格信息数组
                _mapCellInfo = new MapCellInfo[Width, Height];
                for (int x = 0; x < Width; x++)
                {
                    for (int y = 0; y < Height; y++)
                    {
                        _mapCellInfo[x, y] = new MapCellInfo();
                    }
                }

                LogManager.Default.Debug($"地图 {MapName} 初始化了 {Width}x{Height} 个单元格");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"初始化地图单元格失败: {MapName}", exception: ex);
            }
        }

        /// <summary>
        /// 初始化链接
        /// </summary>
        public void InitLinks()
        {
            if (_linkCount <= 0)
                return;

            try
            {
                
                LogManager.Default.Info($"地图 {MapName} 有 {_linkCount} 个链接点需要初始化");
                
                for (int i = 0; i < _linkCount; i++)
                {
                    LogManager.Default.Debug($"地图 {MapName} 链接点 {i+1}: 需要从INI文件读取配置");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"初始化地图链接失败: {MapName}", exception: ex);
            }
        }
    }
    
    /// <summary>
    /// 地图天气
    /// </summary>
    public enum MapWeather
    {
        Clear = 0,          // 晴朗
        Rain = 1,           // 下雨
        Snow = 2,           // 下雪
        Fog = 3,            // 雾
        Storm = 4,          // 暴风雨
        Sandstorm = 5       // 沙尘暴
    }
    
    /// <summary>
    /// 地图时间
    /// </summary>
    public enum MapTime
    {
        Day = 0,            // 白天
        Night = 1,          // 夜晚
        Dawn = 2,           // 黎明
        Dusk = 3            // 黄昏
    }
    
    /// <summary>
    /// 矿石物品定义
    /// </summary>
    public class MineItem
    {
        public string Name { get; set; } = string.Empty;
        public ushort DuraMin { get; set; }
        public ushort DuraMax { get; set; }
        public ushort Rate { get; set; }
    }
    
    /// <summary>
    /// 链接点定义
    /// 用于地图之间的传送
    /// </summary>
    public class LinkPoint
    {
        public uint LinkId { get; set; }
        public uint SourceMapId { get; set; }
        public ushort SourceX { get; set; }
        public ushort SourceY { get; set; }
        public uint TargetMapId { get; set; }
        public ushort TargetX { get; set; }
        public ushort TargetY { get; set; }
        public int NeedLevel { get; set; }
        public string NeedItem { get; set; } = string.Empty;
        public string NeedQuest { get; set; } = string.Empty;
        public string ScriptFile { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// 地形类型
    /// </summary>
    public enum TerrainType
    {
        Normal = 0,     // 普通地形
        Water = 1,      // 水域
        Mountain = 2,   // 山脉
        Forest = 3,     // 森林
        Desert = 4,     // 沙漠
        Snow = 5,       // 雪地
        Lava = 6        // 岩浆
    }
}
