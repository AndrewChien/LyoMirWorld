using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MirCommon;
using MirCommon.Utils;

namespace GameServer
{
    /// <summary>
    /// 怪物生成管理器
    /// </summary>
    public class MonsterGenManager
    {
        private static MonsterGenManager? _instance;
        public static MonsterGenManager Instance => _instance ??= new MonsterGenManager();

        // 怪物生成点数组（最大100000个）
        private readonly MonsterGen?[] _monsterGens = new MonsterGen?[100000];
        private int _monsterGenCount = 0;
        private int _refreshMonGenIndex = 0;

        // 对象池
        private readonly Queue<MonsterGen> _monsterGenPool = new();
        private readonly object _poolLock = new();
        private int _poolCacheSize = 1000; // 缓存大小
        private int _poolUsedCount = 0;
        private int _poolFreeCount = 0;

        private MonsterGenManager()
        {
            // 初始化对象池缓存
            CacheObjects(_poolCacheSize);
        }
        
        /// <summary>
        /// 缓存对象
        /// </summary>
        private void CacheObjects(int count)
        {
            lock (_poolLock)
            {
                for (int i = 0; i < count; i++)
                {
                    _monsterGenPool.Enqueue(new MonsterGen());
                    _poolFreeCount++;
                }
                LogManager.Default.Debug($"对象池缓存了 {count} 个MonsterGen对象");
            }
        }
        
        /// <summary>
        /// 从对象池分配新对象
        /// </summary>
        private MonsterGen AllocFromPool()
        {
            lock (_poolLock)
            {
                if (_monsterGenPool.Count == 0)
                {
                    // 池为空，自动扩容
                    int expandSize = Math.Max(100, _poolCacheSize / 2);
                    CacheObjects(expandSize);
                    _poolCacheSize += expandSize;
                }
                
                var obj = _monsterGenPool.Dequeue();
                _poolFreeCount--;
                _poolUsedCount++;
                
                // 重置对象状态
                ResetMonsterGen(obj);
                
                return obj;
            }
        }
        
        /// <summary>
        /// 将对象返回到对象池
        /// </summary>
        private void ReturnToPool(MonsterGen gen)
        {
            if (gen == null)
                return;

            lock (_poolLock)
            {
                // 重置对象
                ResetMonsterGen(gen);
                
                // 放回对象池
                _monsterGenPool.Enqueue(gen);
                _poolUsedCount--;
                _poolFreeCount++;
            }
        }
        
        /// <summary>
        /// 重置MonsterGen对象状态
        /// </summary>
        private void ResetMonsterGen(MonsterGen gen)
        {
            gen.MonsterName = string.Empty;
            gen.MapId = 0;
            gen.X = 0;
            gen.Y = 0;
            gen.Range = 0;
            gen.MaxCount = 0;
            gen.RefreshDelay = 0;
            gen.CurrentCount = 0;
            gen.ErrorTime = 0;
            gen.LastRefreshTime = DateTime.MinValue;
            gen.ScriptPage = null;
            gen.StartWhenAllDead = false;
        }
        
        /// <summary>
        /// 获取对象池统计信息
        /// </summary>
        public (int total, int used, int free) GetPoolStats()
        {
            lock (_poolLock)
            {
                return (_poolCacheSize, _poolUsedCount, _poolFreeCount);
            }
        }

        /// <summary>
        /// 加载怪物生成配置文件
        /// </summary>
        public bool LoadMonGen(string path)
        {
            LogManager.Default.Info($"加载怪物生成配置文件: {path}");

            if (!Directory.Exists(path))
            {
                LogManager.Default.Error($"怪物生成配置目录不存在: {path}");
                return false;
            }

            try
            {
                // 查找所有.txt文件
                var files = Directory.GetFiles(path, "*.txt", SearchOption.AllDirectories);
                int loadedCount = 0;

                foreach (var file in files)
                {
                    if (LoadMonGenFile(file))
                    {
                        loadedCount++;
                    }
                }

                LogManager.Default.Info($"成功加载 {loadedCount} 个怪物生成配置文件");
                return loadedCount > 0;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载怪物生成配置文件失败: {path}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 加载单个怪物生成文件
        /// </summary>
        private bool LoadMonGenFile(string fileName)
        {
            try
            {
                var lines = SmartReader.ReadAllLines(fileName);
                int count = 0;

                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                        continue;

                    if (AddMonsterGen(trimmedLine))
                    {
                        count++;
                    }
                }

                LogManager.Default.Info($"文件 {Path.GetFileName(fileName)} 加载了 {count} 个生成点");
                return count > 0;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载怪物生成文件失败: {fileName}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 添加怪物生成点
        /// 格式: name/mapid/x/y/range/count/refreshdelay(seconds)[/scriptpage]
        /// </summary>
        public bool AddMonsterGen(string genDesc)
        {
            if (_monsterGenCount >= 100000)
            {
                LogManager.Default.Error($"怪物生成点数量已达上限 (100000)");
                return false;
            }

            var parts = genDesc.Split('/');
            if (parts.Length < 7)
            {
                LogManager.Default.Warning($"怪物生成点格式错误: {genDesc}");
                return false;
            }

            string monsterName = parts[0].Trim();
            
            // 检查怪物类是否存在
            var monsterClass = MonsterManagerEx.Instance.GetClassByName(monsterName);
            if (monsterClass == null)
            {
                LogManager.Default.Warning($"怪物生成点中出现未设置的怪物: {monsterName}");
                return false;
            }

            // 解析参数
            if (!int.TryParse(parts[1], out int mapId) ||
                !int.TryParse(parts[2], out int x) ||
                !int.TryParse(parts[3], out int y) ||
                !int.TryParse(parts[4], out int range) ||
                !int.TryParse(parts[5], out int count) ||
                !int.TryParse(parts[6], out int refreshDelay))
            {
                LogManager.Default.Warning($"怪物生成点参数解析失败: {genDesc}");
                return false;
            }

            // 检查地图是否存在
            var map = LogicMapMgr.Instance.GetLogicMapById((uint)mapId);
            if (map == null || x < 0 || y < 0 || x >= map.Width || y >= map.Height)
            {
                LogManager.Default.Warning($"怪物生成点地图不存在或坐标越界: map({mapId})({x},{y})");
                return false;
            }

            // 从对象池分配新的生成点
            MonsterGen gen = AllocFromPool();

            // 设置生成点属性
            gen.MonsterName = monsterName;
            gen.MapId = mapId;
            gen.X = x;
            gen.Y = y;
            gen.Range = range;
            gen.MaxCount = count;
            gen.RefreshDelay = refreshDelay * 1000; // 转换为毫秒
            gen.CurrentCount = 0;
            gen.ErrorTime = 0;
            gen.LastRefreshTime = DateTime.MinValue;
            gen.StartWhenAllDead = false;

            // 检查是否全部死亡后开始
            string refreshDelayStr = parts[6];
            if (refreshDelayStr.StartsWith("*"))
            {
                gen.StartWhenAllDead = true;
                refreshDelayStr = refreshDelayStr.Substring(1);
                if (int.TryParse(refreshDelayStr, out int actualDelay))
                {
                    gen.RefreshDelay = actualDelay * 1000;
                }
            }

            // 设置脚本页面（如果有）
            if (parts.Length > 7)
            {
                gen.ScriptPage = parts[7];
            }

            // 添加到数组
            _monsterGens[_monsterGenCount++] = gen;

            LogManager.Default.Debug($"添加怪物生成点: {monsterName} 在 map({mapId})({x},{y}) 数量:{count} 间隔:{refreshDelay}s");
            return true;
        }

        /// <summary>
        /// 更新怪物生成
        /// </summary>
        public void UpdateGen()
        {
            if (_monsterGenCount == 0)
                return;

            // 循环更新生成点
            if (_refreshMonGenIndex >= _monsterGenCount)
                _refreshMonGenIndex = 0;

            var gen = _monsterGens[_refreshMonGenIndex];
            int currentIndex = _refreshMonGenIndex;
            _refreshMonGenIndex++;

            if (gen == null)
                return;

            // 检查是否需要刷新
            if (gen.StartWhenAllDead)
            {
                if (gen.CurrentCount > 0)
                {
                    gen.LastRefreshTime = DateTime.Now;
                    return;
                }
            }

            int needRefreshCount = gen.StartWhenAllDead ? gen.MaxCount : gen.MaxCount - gen.CurrentCount;
            if (needRefreshCount <= 0)
                return;

            // 检查刷新延迟
            if (gen.LastRefreshTime != DateTime.MinValue &&
                (DateTime.Now - gen.LastRefreshTime).TotalMilliseconds < gen.RefreshDelay)
                return;

            // 更新生成点
            if (!UpdateGenPtr(gen, needRefreshCount))
            {
                // 生成点失效，从数组中移除
                _monsterGens[currentIndex] = null;
                ReturnToPool(gen);
            }

            gen.LastRefreshTime = DateTime.Now;
        }

        /// <summary>
        /// 初始化所有生成点
        /// </summary>
        public void InitAllGen()
        {
            if (_monsterGenCount == 0)
                return;

            LogManager.Default.Info($"初始化怪物生成点... 总数: {_monsterGenCount}");

            for (int i = 0; i < _monsterGenCount; i++)
            {
                var gen = _monsterGens[i];
                if (gen == null)
                    continue;

                if (!UpdateGenPtr(gen, gen.MaxCount + 1))
                {
                    // 生成点失效
                    ReturnToPool(gen);
                    _monsterGens[i] = null;
                }
                else
                {
                    gen.LastRefreshTime = DateTime.Now;
                }
            }

            LogManager.Default.Info($"怪物生成点初始化完成");
        }

        /// <summary>
        /// 更新生成点指针（实际生成怪物）
        /// </summary>
        private bool UpdateGenPtr(MonsterGen gen, int maxCount, bool setGenPtr = true, bool gotoTarget = false, ushort targetX = 0, ushort targetY = 0)
        {
            if (gen == null)
                return false;

            var map = LogicMapMgr.Instance.GetLogicMapById((uint)gen.MapId);
            if (map == null)
            {
                LogManager.Default.Error($"怪物生成点地图不存在: map({gen.MapId})({gen.X},{gen.Y}) 怪物:{gen.MonsterName}");
                return false;
            }

            int mapWidth = map.Width;
            int mapHeight = map.Height;

            int successCount = 0;
            int startX = gen.X - gen.Range;
            int endX = gen.X + gen.Range;
            int startY = gen.Y - gen.Range;
            int endY = gen.Y + gen.Range;

            Random random = new();

            for (int i = 0; i < maxCount && gen.CurrentCount < gen.MaxCount; i++)
            {
                // 在范围内随机生成坐标
                int tx = random.Next(startX, endX + 1);
                int ty = random.Next(startY, endY + 1);

                // 确保坐标在地图范围内
                if (tx < 0) tx = 0;
                if (tx >= mapWidth) tx = mapWidth - 1;
                if (ty < 0) ty = 0;
                if (ty >= mapHeight) ty = mapHeight - 1;

                // 检查坐标是否可通行
                if (IsPhysicsBlocked(map, tx, ty))
                {
                    // 尝试在周围寻找可通行点
                    var validPoint = GetValidPoint(map, tx, ty, 1);
                    if (validPoint == null)
                        continue;

                    tx = validPoint.Value.X;
                    ty = validPoint.Value.Y;
                }

                // 创建怪物
                var monster = MonsterManagerEx.Instance.CreateMonster(gen.MonsterName, gen.MapId, tx, ty, gen);
                if (monster == null)
                    continue;

                // 添加到游戏世界
                if (!AddMapObjectToWorld(monster))
                {
                    MonsterManagerEx.Instance.DeleteMonster(monster);
                    continue;
                }

                successCount++;
                gen.CurrentCount++;

                if (successCount >= maxCount)
                    return true;
            }

            // 检查错误次数
            if (gen.CurrentCount == 0)
            {
                gen.ErrorTime++;
                if (gen.ErrorTime > 10)
                {
                    LogManager.Default.Error($"怪物生成点连续10次生成失败，被禁用: map({gen.MapId})({gen.X},{gen.Y}) 怪物:{gen.MonsterName}");
                    return false;
                }
            }
            else
            {
                gen.ErrorTime = 0;
            }

            return true;
        }

        /// <summary>
        /// 获取怪物生成点数量
        /// </summary>
        public int GetGenCount()
        {
            return _monsterGenCount;
        }

        /// <summary>
        /// 获取所有怪物生成点
        /// </summary>
        public List<MonsterGen> GetAllGens()
        {
            var list = new List<MonsterGen>();
            for (int i = 0; i < _monsterGenCount; i++)
            {
                if (_monsterGens[i] != null)
                {
                    list.Add(_monsterGens[i]!);
                }
            }
            return list;
        }

        /// <summary>
        /// 清理所有怪物生成点
        /// </summary>
        public void ClearAllGens()
        {
            for (int i = 0; i < _monsterGenCount; i++)
            {
                if (_monsterGens[i] != null)
                {
                    ReturnToPool(_monsterGens[i]!);
                    _monsterGens[i] = null;
                }
            }
            _monsterGenCount = 0;
            _refreshMonGenIndex = 0;
            
            // 记录对象池统计信息
            var stats = GetPoolStats();
            LogManager.Default.Info($"清理所有怪物生成点，对象池统计: 总数={stats.total}, 使用中={stats.used}, 空闲={stats.free}");
        }

        /// <summary>
        /// 检查位置是否被物理阻挡
        /// </summary>
        private bool IsPhysicsBlocked(LogicMap map, int x, int y)
        {
            // 首先检查坐标是否在地图范围内
            if (x < 0 || x >= map.Width || y < 0 || y >= map.Height)
                return true;

            // 获取地图名称
            string mapName = map.MapName;
            if (string.IsNullOrEmpty(mapName))
                return false; // 如果没有地图名称，假设不被阻挡

            // 通过物理地图管理器检查阻挡
            return PhysicsMapMgr.Instance.IsBlocked(mapName, x, y);
        }

        /// <summary>
        /// 获取有效点（在指定范围内寻找可通行点）
        /// </summary>
        private Point? GetValidPoint(LogicMap map, int centerX, int centerY, int range)
        {
            if (range <= 0)
                return null;

            // 从中心点开始向外搜索
            for (int r = 0; r <= range; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        // 只检查边界点
                        if (Math.Abs(dx) != r && Math.Abs(dy) != r)
                            continue;

                        int x = centerX + dx;
                        int y = centerY + dy;

                        // 检查坐标是否在地图范围内
                        if (x < 0 || x >= map.Width || y < 0 || y >= map.Height)
                            continue;

                        // 检查是否被阻挡
                        if (!IsPhysicsBlocked(map, x, y))
                        {
                            return new Point(x, y);
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 添加地图对象到游戏世界
        /// </summary>
        private bool AddMapObjectToWorld(MonsterEx monster)
        {
            if (monster == null)
                return false;

            // 获取地图
            var map = LogicMapMgr.Instance.GetLogicMapById((uint)monster.MapId);
            if (map == null)
                return false;

            // 将怪物添加到地图
            return map.AddObject(monster, monster.X, monster.Y);
        }

        /// <summary>
        /// 简单点结构
        /// </summary>
        private struct Point
        {
            public int X { get; set; }
            public int Y { get; set; }

            public Point(int x, int y)
            {
                X = x;
                Y = y;
            }
        }
    }
}
