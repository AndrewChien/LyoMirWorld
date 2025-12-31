using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using MirCommon;
using MirCommon.Utils;

namespace GameServer
{
    /// <summary>
    /// NPC管理器扩展
    /// </summary>
    public class NpcManagerEx
    {
        private static NpcManagerEx? _instance;
        public static NpcManagerEx Instance => _instance ??= new NpcManagerEx();

        // NPC定义缓存
        private readonly Dictionary<int, NpcDefinitionEx> _definitions = new();
        private readonly Dictionary<uint, NpcInstanceEx> _instances = new();
        private readonly Dictionary<int, List<uint>> _mapNpcs = new();
        private readonly Dictionary<uint, NpcInstanceEx> _dynamicNpcs = new();
        
        // NPC更新队列
        private readonly Queue<NpcInstanceEx> _updateQueue = new();
        private int _updateIndex = 0;
        
        // 对象池
        private readonly Queue<NpcGoodsListEx> _goodsListPool = new();
        private readonly Queue<NpcGoodsItemListEx> _goodsItemListPool = new();
        
        private uint _nextInstanceId = 10000;
        private uint _nextDynamicId = 0x70000000;
        private readonly object _lock = new();

        private NpcManagerEx()
        {
            //InitializeDefaultNpcs();//（必须等待ScriptObjectMgr.Load()加载完成后才能加载）
        }

        /// <summary>
        /// 加载NPC配置文件（必须等待ScriptObjectMgr.Load()加载完成后才能加载）
        /// </summary>
        public bool Load(string filename)
        {
            if (!File.Exists(filename))
            {
                LogManager.Default.Warning($"NPC配置文件不存在: {filename}");
                return false;
            }

            try
            {
                var lines = SmartReader.ReadAllLines(filename);
                int loadedCount = 0;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    if (AddNpcFromString(line))
                        loadedCount++;
                }

                LogManager.Default.Info($"从 {filename} 加载了 {loadedCount} 个NPC");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载NPC配置文件失败 {filename}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从字符串添加NPC
        /// 格式: name/id/view/mapid/x/y/istalk/scriptfile[/buypercent/sellpercent]
        /// </summary>
        public bool AddNpcFromString(string npcString)
        {
            var parts = npcString.Split('/');
            if (parts.Length < 7)
            {
                LogManager.Default.Warning($"NPC字符串格式错误: {npcString}");
                return false;
            }

            string name = parts[0];
            if (!int.TryParse(parts[1], out int dbId) ||
                !Helper.TryHexToInt(parts[2], out int view) ||      //十六进制，如"0x25"
                !int.TryParse(parts[3], out int mapId) ||
                !int.TryParse(parts[4], out int x) ||
                !int.TryParse(parts[5], out int y) ||
                !int.TryParse(parts[6], out int canTalk))
            {
                LogManager.Default.Warning($"NPC字符串解析失败: {npcString}");
                return false;
            }

            // 如果不能对话，跳过
            if (canTalk == 0)
                return false;

            string scriptFile = parts.Length > 7 ? parts[7] : string.Empty;

            // 获取脚本对象
            var scriptObject = ScriptObjectMgr.Instance.GetScriptObject(scriptFile);
            if (scriptObject == null)
            {
                LogManager.Default.Warning($"NPC脚本对象不存在: {scriptFile}");
                return false;
            }

            // 创建NPC定义
            var definition = new NpcDefinitionEx
            {
                NpcId = dbId,
                Name = name,
                ViewId = view,
                ScriptFile = scriptFile,
                ScriptObject = scriptObject
            };

            // 设置买卖比例
            if (parts.Length > 8)
            {
                if (int.TryParse(parts[8], out int buyPercent))
                    definition.BuyPercent = buyPercent / 100.0f;
                
                if (parts.Length > 9 && int.TryParse(parts[9], out int sellPercent))
                    definition.SellPercent = sellPercent / 100.0f;
            }

            // 添加定义
            AddDefinition(definition);

            // 创建NPC实例
            var npc = CreateNpc(dbId, mapId, x, y);
            if (npc == null)
                return false;

            LogManager.Default.Debug($"NPC {name} 进入世界在({mapId})({x},{y})");
            return true;
        }

        /// <summary>
        /// 添加NPC定义
        /// </summary>
        public void AddDefinition(NpcDefinitionEx definition)
        {
            lock (_lock)
            {
                _definitions[definition.NpcId] = definition;
            }
        }

        /// <summary>
        /// 获取NPC定义
        /// </summary>
        public NpcDefinitionEx? GetDefinition(int npcId)
        {
            lock (_lock)
            {
                return _definitions.TryGetValue(npcId, out var definition) ? definition : null;
            }
        }

        /// <summary>
        /// 创建NPC实例
        /// </summary>
        public NpcInstanceEx? CreateNpc(int npcId, int mapId, int x, int y)
        {
            var definition = GetDefinition(npcId);
            if (definition == null)
                return null;

            lock (_lock)
            {
                uint instanceId = Interlocked.Increment(ref _nextInstanceId);
                var npc = new NpcInstanceEx(definition, instanceId, mapId, x, y);
                
                _instances[instanceId] = npc;

                // 添加到地图NPC列表
                if (!_mapNpcs.ContainsKey(mapId))
                    _mapNpcs[mapId] = new List<uint>();
                _mapNpcs[mapId].Add(instanceId);

                // 添加到地图
                var map = MapManager.Instance.GetMap((uint)mapId);
                if (map != null)
                {
                    map.AddObject(npc, x, y);
                }

                return npc;
            }
        }

        /// <summary>
        /// 获取NPC实例
        /// </summary>
        public NpcInstanceEx? GetNpc(uint instanceId)
        {
            lock (_lock)
            {
                return _instances.TryGetValue(instanceId, out var npc) ? npc : null;
            }
        }

        /// <summary>
        /// 获取地图上的所有NPC
        /// </summary>
        public List<NpcInstanceEx> GetMapNpcs(int mapId)
        {
            lock (_lock)
            {
                if (!_mapNpcs.TryGetValue(mapId, out var npcIds))
                    return new List<NpcInstanceEx>();

                return npcIds
                    .Select(id => GetNpc(id))
                    .Where(npc => npc != null)
                    .Cast<NpcInstanceEx>()
                    .ToList();
            }
        }

        /// <summary>
        /// 移除NPC
        /// </summary>
        public bool RemoveNpc(uint instanceId)
        {
            lock (_lock)
            {
                if (!_instances.TryGetValue(instanceId, out var npc))
                    return false;

                // 从地图列表中移除
                if (_mapNpcs.TryGetValue(npc.MapId, out var list))
                    list.Remove(instanceId);

                // 从地图移除
                var map = MapManager.Instance.GetMap((uint)npc.MapId);
                if (map != null)
                {
                    map.RemoveObject(npc);
                }

                // 从实例字典移除
                _instances.Remove(instanceId);

                return true;
            }
        }

        /// <summary>
        /// 添加动态NPC
        /// </summary>
        public bool AddDynamicNpc(uint ident, string name, uint viewId, uint mapId, uint x, uint y, string scriptFile)
        {
            var scriptObject = ScriptObjectMgr.Instance.GetScriptObject(scriptFile);
            if (scriptObject == null)
                return false;

            var map = MapManager.Instance.GetMap(mapId);
            if (map == null)
                return false;

            lock (_lock)
            {
                uint dynamicId = _nextDynamicId | ident;
                var definition = new NpcDefinitionEx
                {
                    NpcId = (int)ident,
                    Name = name,
                    ViewId = (int)viewId,
                    ScriptFile = scriptFile,
                    ScriptObject = scriptObject,
                    IsDynamic = true
                };

                var npc = new NpcInstanceEx(definition, dynamicId, (int)mapId, (int)x, (int)y);
                
                // 添加到动态NPC列表
                _dynamicNpcs[dynamicId] = npc;
                
                // 添加到地图
                if (!map.AddObject(npc, (int)x, (int)y))
                {
                    _dynamicNpcs.Remove(dynamicId);
                    return false;
                }

                LogManager.Default.Debug($"动态NPC {name} 进入世界在({mapId})({x},{y})");
                return true;
            }
        }

        /// <summary>
        /// 移除动态NPC
        /// </summary>
        public bool RemoveDynamicNpc(uint ident)
        {
            lock (_lock)
            {
                uint dynamicId = _nextDynamicId | ident;
                if (!_dynamicNpcs.TryGetValue(dynamicId, out var npc))
                    return false;

                // 从地图移除
                if (npc.CurrentMap != null)
                    npc.CurrentMap.RemoveObject(npc);

                // 保存物品
                npc.SaveItems();

                // 从动态列表移除
                _dynamicNpcs.Remove(dynamicId);

                return true;
            }
        }

        /// <summary>
        /// 获取动态NPC
        /// </summary>
        public NpcInstanceEx? GetDynamicNpc(uint ident)
        {
            lock (_lock)
            {
                uint dynamicId = _nextDynamicId | ident;
                return _dynamicNpcs.TryGetValue(dynamicId, out var npc) ? npc : null;
            }
        }

        /// <summary>
        /// 更新NPC
        /// 使用队列轮流更新NPC，每次Update只更新一个NPC
        /// </summary>
        public void Update()
        {
            lock (_lock)
            {
                // 如果队列为空，重新填充队列
                if (_updateQueue.Count == 0)
                {
                    foreach (var npc in _instances.Values)
                    {
                        _updateQueue.Enqueue(npc);
                    }
                    foreach (var npc in _dynamicNpcs.Values)
                    {
                        _updateQueue.Enqueue(npc);
                    }
                }
                
                // 从队列中取出一个NPC进行更新
                if (_updateQueue.Count > 0)
                {
                    var npc = _updateQueue.Dequeue();
                    if (npc != null && npc.IsActive)
                    {
                        npc.Update();
                        // 更新后将NPC放回队列尾部
                        _updateQueue.Enqueue(npc);
                    }
                }
            }
        }

        /// <summary>
        /// 获取NPC数量
        /// </summary>
        public int GetCount()
        {
            lock (_lock)
            {
                return _instances.Count + _dynamicNpcs.Count;
            }
        }

        /// <summary>
        /// 分配商品列表
        /// </summary>
        public NpcGoodsListEx AllocGoodsList()
        {
            lock (_lock)
            {
                if (_goodsListPool.Count > 0)
                    return _goodsListPool.Dequeue();
                
                return new NpcGoodsListEx();
            }
        }

        /// <summary>
        /// 释放商品列表
        /// </summary>
        public void FreeGoodsList(NpcGoodsListEx goodsList)
        {
            lock (_lock)
            {
                goodsList.Clear();
                _goodsListPool.Enqueue(goodsList);
            }
        }

        /// <summary>
        /// 分配商品项列表
        /// </summary>
        public NpcGoodsItemListEx AllocGoodsItemList()
        {
            lock (_lock)
            {
                if (_goodsItemListPool.Count > 0)
                    return _goodsItemListPool.Dequeue();
                
                return new NpcGoodsItemListEx();
            }
        }

        /// <summary>
        /// 释放商品项列表
        /// </summary>
        public void FreeGoodsItemList(NpcGoodsItemListEx goodsItemList)
        {
            lock (_lock)
            {
                goodsItemList.Clear();
                _goodsItemListPool.Enqueue(goodsItemList);
            }
        }

        /// <summary>
        /// 初始化默认NPC
        /// </summary>
        //private void InitializeDefaultNpcs()
        //{
        //    // 加载NPC配置文件
        //    string npcFile = Path.Combine("./data", "npcgen.txt");
        //    if (File.Exists(npcFile))
        //    {
        //        Load(npcFile);
        //    }
        //    else
        //    {
        //        LogManager.Default.Warning($"NPC配置文件不存在: {npcFile}，创建默认NPC");
        //        CreateDefaultNpcs();
        //    }
        //}

        /// <summary>
        /// 创建默认NPC
        /// </summary>
        private void CreateDefaultNpcs()
        {
            // 创建一些基本NPC
            var defaultNpcs = new[]
            {
                new NpcDefinitionEx { NpcId = 1001, Name = "武器商人", ViewId = 100, ScriptFile = "weaponshop" },
                new NpcDefinitionEx { NpcId = 1002, Name = "防具商人", ViewId = 101, ScriptFile = "armorshop" },
                new NpcDefinitionEx { NpcId = 1003, Name = "药店老板", ViewId = 102, ScriptFile = "potionshop" },
                new NpcDefinitionEx { NpcId = 1004, Name = "仓库管理员", ViewId = 103, ScriptFile = "storage" },
                new NpcDefinitionEx { NpcId = 1005, Name = "传送员", ViewId = 104, ScriptFile = "teleporter" },
                new NpcDefinitionEx { NpcId = 1006, Name = "铁匠", ViewId = 105, ScriptFile = "blacksmith" },
                new NpcDefinitionEx { NpcId = 1007, Name = "技能训练师", ViewId = 106, ScriptFile = "trainer" }
            };

            foreach (var definition in defaultNpcs)
            {
                AddDefinition(definition);
            }

            LogManager.Default.Info($"已加载 {_definitions.Count} 个NPC定义");
        }
    }

    /// <summary>
    /// NPC定义扩展
    /// </summary>
    public class NpcDefinitionEx
    {
        public int NpcId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int ViewId { get; set; }
        public string ScriptFile { get; set; } = string.Empty;
        public ScriptObject? ScriptObject { get; set; }
        public float BuyPercent { get; set; } = 1.0f;
        public float SellPercent { get; set; } = 1.0f;
        public bool IsDynamic { get; set; }
    }

    /// <summary>
    /// NPC实例扩展
    /// </summary>
    public class NpcInstanceEx : Npc
    {
        public uint InstanceId { get; set; }
        public NpcDefinitionEx Definition { get; set; }
        public bool IsActive { get; set; } = true;

        public NpcInstanceEx(NpcDefinitionEx definition, uint instanceId, int mapId, int x, int y)
            : base(definition.NpcId, definition.Name, ConvertToNpcType(definition))
        {
            Definition = definition;
            InstanceId = instanceId;
            MapId = mapId;
            X = (ushort)x;
            Y = (ushort)y;
            ScriptFile = definition.ScriptFile;
        }

        private static NpcType ConvertToNpcType(NpcDefinitionEx definition)
        {
            // 根据脚本文件或名称判断NPC类型
            if (definition.ScriptFile.Contains("shop", StringComparison.OrdinalIgnoreCase))
                return NpcType.Merchant;
            if (definition.ScriptFile.Contains("storage", StringComparison.OrdinalIgnoreCase))
                return NpcType.Warehouse;
            if (definition.ScriptFile.Contains("teleport", StringComparison.OrdinalIgnoreCase))
                return NpcType.Teleporter;
            if (definition.ScriptFile.Contains("blacksmith", StringComparison.OrdinalIgnoreCase))
                return NpcType.Repair;
            
            return NpcType.Normal;
        }

        /// <summary>
        /// 更新NPC状态
        /// </summary>
        public new void Update()
        {
            // 检查定时器是否超时
            long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (currentTime - _lastUpdateTime > 10000) // 10秒
            {
                _lastUpdateTime = currentTime;
                
                // 遍历商品列表，补充库存
                // 注意：这里需要访问商品列表，但当前NpcInstanceEx没有商品列表字段
                // 需要从父类或定义中获取商品列表
                
                // 如果有商品变化，保存物品
                if (_hasChanged)
                {
                    SaveItems();
                    _hasChanged = false;
                }
            }
            
            // 调用父类的Update方法
            base.Update();
        }

        /// <summary>
        /// 保存物品
        /// </summary>
        public void SaveItems()
        {
            if (!_hasChanged)
                return;
                
            try
            {
                // 创建保存目录
                string saveDir = Path.Combine(".", "data", "Market_Save");
                if (!Directory.Exists(saveDir))
                {
                    Directory.CreateDirectory(saveDir);
                }
                
                // 生成文件名：market_{StoreId}.dat
                string filename = Path.Combine(saveDir, $"market_{Definition.NpcId:X8}.dat");
                
                // 这里需要保存商品列表到文件
                // 由于当前结构中没有商品列表，这里只创建空文件作为占位
                File.WriteAllText(filename, $"NPC {Definition.Name} 的商品数据 - 保存时间: {DateTime.Now}", Encoding.GetEncoding("GBK"));
                
                LogManager.Default.Debug($"保存NPC {Definition.Name} 的商品数据到 {filename}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"保存NPC物品失败: {Definition.Name}, 错误: {ex.Message}");
            }
        }
        
        // 私有字段
        private long _lastUpdateTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        private bool _hasChanged = false;
    }

    /// <summary>
    /// NPC商品列表扩展
    /// </summary>
    public class NpcGoodsListEx
    {
        public List<int> ItemIds { get; set; } = new();

        public void Clear()
        {
            ItemIds.Clear();
        }
    }

    /// <summary>
    /// NPC商品项列表扩展
    /// </summary>
    public class NpcGoodsItemListEx
    {
        public List<NpcGoodsItemEx> Items { get; set; } = new();

        public void Clear()
        {
            Items.Clear();
        }
    }

    /// <summary>
    /// NPC商品项扩展
    /// </summary>
    public class NpcGoodsItemEx
    {
        public int ItemId { get; set; }
        public int Price { get; set; }
        public int Stock { get; set; }
    }

}
