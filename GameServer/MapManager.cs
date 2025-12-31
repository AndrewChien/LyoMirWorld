using System;
using System.Collections.Generic;
using MirCommon.Utils;

namespace GameServer
{
    /// <summary>
    /// 地图管理器
    /// </summary>
    public class MapManager
    {
        private static MapManager? _instance;
        public static MapManager Instance => _instance ??= new MapManager();

        private readonly Dictionary<uint, LogicMap> _maps = new();
        private readonly object _lock = new();

        private MapManager()
        {
            // 不再在构造函数中初始化，等待Load方法调用
        }

        /// <summary>
        /// 从LogicMapMgr加载地图数据
        /// </summary>
        public bool Load()
        {
            try
            {
                // 从LogicMapMgr获取所有地图配置
                var logicMaps = LogicMapMgr.Instance.GetAllMaps();
                int loadedCount = 0;
                
                foreach (var logicMap in logicMaps)
                {
                    // 创建LogicMap实例
                    var map = new LogicMap(logicMap.MapId, logicMap.MapName, logicMap.Width, logicMap.Height)
                    {
                        IsSafeZone = logicMap.IsSafeZone,
                        AllowPK = logicMap.AllowPK,
                        AllowPets = logicMap.AllowPets,
                        AllowMounts = logicMap.AllowMounts,
                        AllowTeleport = logicMap.AllowTeleport,
                        AllowRecall = logicMap.AllowRecall,
                        ExpFactor = logicMap.ExpFactor,
                        DropFactor = logicMap.DropFactor,
                        MinLevel = logicMap.MinLevel,
                        MaxLevel = logicMap.MaxLevel,
                        NeedItem = logicMap.NeedItem,
                        NeedQuest = logicMap.NeedQuest,
                        ScriptFile = logicMap.ScriptFile
                    };

                    // 添加地图
                    AddMap(map);
                    loadedCount++;
                    
                    LogManager.Default.Debug($"加载地图: ID={logicMap.MapId}, 名称={logicMap.MapName}, 大小={logicMap.Width}x{logicMap.Height}");
                }

                // 如果没有加载到地图，使用默认地图作为后备
                if (loadedCount == 0)
                {
                    LogManager.Default.Warning("未从LogicMapMgr加载到地图，使用默认地图");
                    InitializeDefaultMaps();
                    loadedCount = _maps.Count;
                }

                LogManager.Default.Info($"已加载 {loadedCount} 个地图");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载地图数据失败: {ex.Message}", exception: ex);
                // 发生异常时使用默认地图作为后备
                InitializeDefaultMaps();
                return false;
            }
        }

        /// <summary>
        /// 初始化默认地图（后备方案）
        /// </summary>
        private void InitializeDefaultMaps()
        {
            // 添加一些默认地图
            AddMap(new LogicMap(0, "比奇城", 1000, 1000)
            {
                IsSafeZone = true,
                AllowPK = false,
                AllowPets = true,
                AllowMounts = true,
                AllowTeleport = true,
                AllowRecall = true
            });

            AddMap(new LogicMap(1, "毒蛇山谷", 800, 800)
            {
                IsSafeZone = false,
                AllowPK = true,
                AllowPets = true,
                AllowMounts = true,
                AllowTeleport = true,
                AllowRecall = true
            });

            AddMap(new LogicMap(2, "盟重土城", 1000, 1000)
            {
                IsSafeZone = true,
                AllowPK = false,
                AllowPets = true,
                AllowMounts = true,
                AllowTeleport = true,
                AllowRecall = true
            });

            AddMap(new LogicMap(3, "沙巴克城", 600, 600)
            {
                IsSafeZone = false,
                AllowPK = true,
                AllowPets = true,
                AllowMounts = true,
                AllowTeleport = false,
                AllowRecall = false
            });

            AddMap(new LogicMap(4, "祖玛寺庙一层", 400, 400)
            {
                IsSafeZone = false,
                AllowPK = true,
                AllowPets = true,
                AllowMounts = false,
                AllowTeleport = false,
                AllowRecall = true
            });

            AddMap(new LogicMap(5, "石墓一层", 400, 400)
            {
                IsSafeZone = false,
                AllowPK = true,
                AllowPets = true,
                AllowMounts = false,
                AllowTeleport = false,
                AllowRecall = true
            });

            AddMap(new LogicMap(6, "沃玛寺庙一层", 400, 400)
            {
                IsSafeZone = false,
                AllowPK = true,
                AllowPets = true,
                AllowMounts = false,
                AllowTeleport = false,
                AllowRecall = true
            });

            AddMap(new LogicMap(7, "赤月峡谷一层", 400, 400)
            {
                IsSafeZone = false,
                AllowPK = true,
                AllowPets = true,
                AllowMounts = false,
                AllowTeleport = false,
                AllowRecall = true
            });

            AddMap(new LogicMap(8, "牛魔寺庙一层", 400, 400)
            {
                IsSafeZone = false,
                AllowPK = true,
                AllowPets = true,
                AllowMounts = false,
                AllowTeleport = false,
                AllowRecall = true
            });

            AddMap(new LogicMap(9, "封魔谷", 800, 800)
            {
                IsSafeZone = false,
                AllowPK = true,
                AllowPets = true,
                AllowMounts = true,
                AllowTeleport = true,
                AllowRecall = true
            });

            AddMap(new LogicMap(10, "苍月岛", 800, 800)
            {
                IsSafeZone = false,
                AllowPK = true,
                AllowPets = true,
                AllowMounts = true,
                AllowTeleport = true,
                AllowRecall = true
            });

            AddMap(new LogicMap(11, "白日门", 800, 800)
            {
                IsSafeZone = false,
                AllowPK = true,
                AllowPets = true,
                AllowMounts = true,
                AllowTeleport = true,
                AllowRecall = true
            });

            AddMap(new LogicMap(12, "魔龙城", 800, 800)
            {
                IsSafeZone = false,
                AllowPK = true,
                AllowPets = true,
                AllowMounts = true,
                AllowTeleport = true,
                AllowRecall = true
            });

            LogManager.Default.Info($"已加载 {_maps.Count} 个默认地图");
        }

        /// <summary>
        /// 添加地图
        /// </summary>
        public void AddMap(LogicMap map)
        {
            lock (_lock)
            {
                _maps[map.MapId] = map;
            }
        }

        /// <summary>
        /// 获取地图
        /// </summary>
        public LogicMap? GetMap(uint mapId)
        {
            lock (_lock)
            {
                _maps.TryGetValue(mapId, out var map);
                return map;
            }
        }

        /// <summary>
        /// 移除地图
        /// </summary>
        public bool RemoveMap(uint mapId)
        {
            lock (_lock)
            {
                return _maps.Remove(mapId);
            }
        }

        /// <summary>
        /// 获取所有地图
        /// </summary>
        public List<LogicMap> GetAllMaps()
        {
            lock (_lock)
            {
                return new List<LogicMap>(_maps.Values);
            }
        }

        /// <summary>
        /// 获取地图数量
        /// </summary>
        public int GetMapCount()
        {
            lock (_lock)
            {
                return _maps.Count;
            }
        }

        /// <summary>
        /// 检查地图是否存在
        /// </summary>
        public bool HasMap(uint mapId)
        {
            lock (_lock)
            {
                return _maps.ContainsKey(mapId);
            }
        }

        /// <summary>
        /// 获取地图名称
        /// </summary>
        public string GetMapName(uint mapId)
        {
            var map = GetMap(mapId);
            return map?.MapName ?? "未知地图";
        }

        /// <summary>
        /// 获取地图大小
        /// </summary>
        public (int width, int height) GetMapSize(uint mapId)
        {
            var map = GetMap(mapId);
            if (map == null)
                return (0, 0);
            return (map.Width, map.Height);
        }

        /// <summary>
        /// 检查是否可以传送到指定地图
        /// </summary>
        public bool CanTeleportTo(uint mapId)
        {
            var map = GetMap(mapId);
            return map?.AllowTeleport ?? false;
        }

        /// <summary>
        /// 检查是否可以回城到指定地图
        /// </summary>
        public bool CanRecallTo(uint mapId)
        {
            var map = GetMap(mapId);
            return map?.AllowRecall ?? false;
        }

        /// <summary>
        /// 检查地图是否安全区
        /// </summary>
        public bool IsSafeZone(uint mapId)
        {
            var map = GetMap(mapId);
            return map?.IsSafeZone ?? false;
        }

        /// <summary>
        /// 检查地图是否允许PK
        /// </summary>
        public bool AllowPK(uint mapId)
        {
            var map = GetMap(mapId);
            return map?.AllowPK ?? false;
        }

        /// <summary>
        /// 检查地图是否允许宠物
        /// </summary>
        public bool AllowPets(uint mapId)
        {
            var map = GetMap(mapId);
            return map?.AllowPets ?? false;
        }

        /// <summary>
        /// 检查地图是否允许坐骑
        /// </summary>
        public bool AllowMounts(uint mapId)
        {
            var map = GetMap(mapId);
            return map?.AllowMounts ?? false;
        }

        /// <summary>
        /// 获取地图经验倍率
        /// </summary>
        public float GetExpFactor(uint mapId)
        {
            var map = GetMap(mapId);
            return map?.ExpFactor ?? 1.0f;
        }

        /// <summary>
        /// 获取地图掉落倍率
        /// </summary>
        public float GetDropFactor(uint mapId)
        {
            var map = GetMap(mapId);
            return map?.DropFactor ?? 1.0f;
        }

        /// <summary>
        /// 设置地图经验倍率
        /// </summary>
        public void SetExpFactor(uint mapId, float factor)
        {
            var map = GetMap(mapId);
            if (map != null)
            {
                map.ExpFactor = factor;
            }
        }

        /// <summary>
        /// 设置地图掉落倍率
        /// </summary>
        public void SetDropFactor(uint mapId, float factor)
        {
            var map = GetMap(mapId);
            if (map != null)
            {
                map.DropFactor = factor;
            }
        }

        /// <summary>
        /// 更新所有地图
        /// </summary>
        public void UpdateAllMaps()
        {
            lock (_lock)
            {
                foreach (var map in _maps.Values)
                {
                    map.Update();
                }
            }
        }

        /// <summary>
        /// 获取地图上的玩家总数
        /// </summary>
        public int GetTotalPlayerCount()
        {
            int total = 0;
            lock (_lock)
            {
                foreach (var map in _maps.Values)
                {
                    total += map.GetPlayerCount();
                }
            }
            return total;
        }

        /// <summary>
        /// 获取地图上的怪物总数
        /// </summary>
        public int GetTotalMonsterCount()
        {
            int total = 0;
            lock (_lock)
            {
                foreach (var map in _maps.Values)
                {
                    total += map.GetMonsterCount();
                }
            }
            return total;
        }

        /// <summary>
        /// 获取地图上的NPC总数
        /// </summary>
        public int GetTotalNPCCount()
        {
            int total = 0;
            lock (_lock)
            {
                foreach (var map in _maps.Values)
                {
                    total += map.GetNPCCount();
                }
            }
            return total;
        }

        /// <summary>
        /// 获取地图上的物品总数
        /// </summary>
        public int GetTotalItemCount()
        {
            int total = 0;
            lock (_lock)
            {
                foreach (var map in _maps.Values)
                {
                    total += map.GetItemCount();
                }
            }
            return total;
        }

        /// <summary>
        /// 获取地图上的总对象数
        /// </summary>
        public int GetTotalObjectCount()
        {
            int total = 0;
            lock (_lock)
            {
                foreach (var map in _maps.Values)
                {
                    total += map.GetTotalObjectCount();
                }
            }
            return total;
        }

        /// <summary>
        /// 显示所有地图信息
        /// </summary>
        public void ShowAllMapInfo()
        {
            lock (_lock)
            {
                LogManager.Default.Info("=== 地图信息 ===");
                foreach (var map in _maps.Values)
                {
                    LogManager.Default.Info($"[{map.MapId}] {map.MapName} - 大小:{map.Width}x{map.Height} 玩家:{map.GetPlayerCount()} 怪物:{map.GetMonsterCount()} NPC:{map.GetNPCCount()}");
                }
            }
        }

        /// <summary>
        /// 查找玩家所在的地图
        /// </summary>
        public LogicMap? FindPlayerMap(uint playerId)
        {
            lock (_lock)
            {
                foreach (var map in _maps.Values)
                {
                    if (map.GetPlayer(playerId) != null)
                    {
                        return map;
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// 查找怪物所在的地图
        /// </summary>
        public LogicMap? FindMonsterMap(uint monsterId)
        {
            lock (_lock)
            {
                foreach (var map in _maps.Values)
                {
                    if (map.GetMonster(monsterId) != null)
                    {
                        return map;
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// 查找NPC所在的地图
        /// </summary>
        public LogicMap? FindNPCMap(uint npcId)
        {
            lock (_lock)
            {
                foreach (var map in _maps.Values)
                {
                    if (map.GetNPC(npcId) != null)
                    {
                        return map;
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// 查找物品所在的地图
        /// </summary>
        public LogicMap? FindItemMap(uint itemId)
        {
            lock (_lock)
            {
                foreach (var map in _maps.Values)
                {
                    if (map.GetItem(itemId) != null)
                    {
                        return map;
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// 传送玩家到指定地图
        /// </summary>
        public bool TeleportPlayer(HumanPlayer player, uint targetMapId, int x, int y)
        {
            var currentMap = player.CurrentMap;
            var targetMap = GetMap(targetMapId);

            if (targetMap == null)
            {
                player.Say("目标地图不存在");
                return false;
            }

            if (!targetMap.CanMoveTo(x, y))
            {
                player.Say("目标位置不可到达");
                return false;
            }

            // 从当前地图移除玩家
            currentMap?.RemoveObject(player);

            // 添加到目标地图
            if (targetMap.AddObject(player, x, y))
            {
                player.Say($"已传送到 {targetMap.MapName}");
                return true;
            }

            return false;
        }

        /// <summary>
        /// 回城玩家到安全区
        /// </summary>
        public bool RecallPlayer(HumanPlayer player, uint targetMapId)
        {
            var targetMap = GetMap(targetMapId);
            if (targetMap == null || !targetMap.IsSafeZone)
            {
                player.Say("目标地图不是安全区");
                return false;
            }

            // 传送到地图中心
            int centerX = targetMap.Width / 2;
            int centerY = targetMap.Height / 2;

            return TeleportPlayer(player, targetMapId, centerX, centerY);
        }

        /// <summary>
        /// 随机传送玩家
        /// </summary>
        public bool RandomTeleportPlayer(HumanPlayer player, uint targetMapId)
        {
            var targetMap = GetMap(targetMapId);
            if (targetMap == null)
            {
                player.Say("目标地图不存在");
                return false;
            }

            Random random = new Random();
            int attempts = 0;
            const int maxAttempts = 100;

            while (attempts < maxAttempts)
            {
                int x = random.Next(0, targetMap.Width);
                int y = random.Next(0, targetMap.Height);

                if (targetMap.CanMoveTo(x, y))
                {
                    return TeleportPlayer(player, targetMapId, x, y);
                }

                attempts++;
            }

            player.Say("无法找到可传送的位置");
            return false;
        }

        internal LogicMap? GetTownMap()
        {
            throw new NotImplementedException();
        }
    }
}
