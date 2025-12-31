using MirCommon;
using MirCommon.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;

namespace GameServer
{
    /// <summary>
    /// 玩家管理器 - 集中管理所有在线玩家
    /// </summary>
    public class HumanPlayerMgr
    {
        private static HumanPlayerMgr? _instance;
        public static HumanPlayerMgr Instance => _instance ??= new HumanPlayerMgr();

        // 最大玩家数量
        private const int MAX_HUMANPLAYER = 128;

        // 玩家列表（使用ID索引）
        private readonly Dictionary<uint, HumanPlayer> _playersById = new();

        // 玩家名称哈希表（快速通过名称查找）
        private readonly Dictionary<string, HumanPlayer> _playersByName = new(StringComparer.OrdinalIgnoreCase);

        // 对象ID分配器
        private uint _nextPlayerId = 1;

        // 锁对象，确保线程安全
        private readonly object _lock = new();

        private HumanPlayerMgr() { }

        /// <summary>
        /// 通过名称查找玩家
        /// </summary>
        public HumanPlayer? FindByName(string name)
        {
            lock (_lock)
            {
                return _playersByName.TryGetValue(name, out var player) ? player : null;
            }
        }

        /// <summary>
        /// 通过ID查找玩家
        /// </summary>
        public HumanPlayer? FindById(uint id)
        {
            lock (_lock)
            {
                // 玩家对象的ID格式：高8位=OBJ_PLAYER(0x01)，低24位=实际ID
                if ((id & 0xff000000) >> 24 == 0x01) // OBJ_PLAYER
                {
                    id &= 0xffffff; // 取低24位
                }

                return _playersById.TryGetValue(id, out var player) ? player : null;
            }
        }

        /// <summary>
        /// 创建新玩家
        /// </summary>
        public HumanPlayer? NewPlayer(string account, string name, uint charDbId, TcpClient _client)
        {
            lock (_lock)
            {
                // 检查是否达到最大玩家数量
                if (_playersById.Count >= MAX_HUMANPLAYER)
                {
                    LogManager.Default.Warning($"已达到最大玩家数量限制: {MAX_HUMANPLAYER}");
                    return null;
                }

                // 检查名称是否已存在
                if (_playersByName.ContainsKey(name))
                {
                    LogManager.Default.Warning($"玩家名称已存在: {name}");
                    return null;
                }

                // 分配新的玩家ID
                uint playerId = _nextPlayerId++;
                if (playerId > 0xffffff) // 24位最大值
                {
                    playerId = 1; // 回绕
                    _nextPlayerId = 2;
                }

                // 创建玩家对象ID（高8位为OBJ_PLAYER）
                uint objectId = playerId | (0x01u << 24); // OBJ_PLAYER = 0x01

                // 创建玩家对象
                var player = new HumanPlayer(account, name, charDbId, _client);
                
                // 使用反射设置ObjectId（因为setter是protected）
                typeof(GameObject).GetProperty("ObjectId")?.SetValue(player, objectId);

                // 添加到管理器中
                _playersById[playerId] = player;
                _playersByName[name] = player;


                LogManager.Default.Info($"创建新玩家: {name} (ID: {objectId:X8})");
                return player;
            }
        }

        /// <summary>
        /// 删除玩家
        /// </summary>
        public bool DeletePlayer(HumanPlayer player)
        {
            if (player == null)
                return false;

            lock (_lock)
            {
                // 获取玩家ID的低24位
                uint playerId = player.ObjectId & 0xffffff;

                // 从名称哈希表中移除
                _playersByName.Remove(player.Name);

                // 从ID列表中移除
                bool removed = _playersById.Remove(playerId);

                if (removed)
                {
                    // 清理玩家数据
                    // player.Clean();

                    LogManager.Default.Info($"删除玩家: {player.Name} (ID: {player.ObjectId:X8})");
                }

                return removed;
            }
        }

        /// <summary>
        /// 添加玩家到名称列表
        /// </summary>
        public bool AddPlayerNameList(HumanPlayer player, string name)
        {
            if (player == null || string.IsNullOrEmpty(name))
                return false;

            lock (_lock)
            {
                // 检查名称是否已存在
                if (_playersByName.ContainsKey(name))
                {
                    LogManager.Default.Warning($"玩家名称已存在: {name}");
                    return false;
                }

                // 添加到名称哈希表
                _playersByName[name] = player;

                LogManager.Default.Debug($"添加玩家到名称列表: {name} -> {player.ObjectId:X8}");
                return true;
            }
        }

        /// <summary>
        /// 获取玩家数量
        /// </summary>
        public int GetCount()
        {
            lock (_lock)
            {
                return _playersById.Count;
            }
        }

        /// <summary>
        /// 获取所有玩家列表
        /// </summary>
        public List<HumanPlayer> GetAllPlayers()
        {
            lock (_lock)
            {
                return _playersById.Values.ToList();
            }
        }

        /// <summary>
        /// 通过名称或ID查找玩家（通用方法）
        /// </summary>
        public HumanPlayer? FindPlayer(string identifier)
        {
            // 尝试作为名称查找
            var player = FindByName(identifier);
            if (player != null)
                return player;

            // 尝试作为ID查找
            if (uint.TryParse(identifier, out uint id))
            {
                return FindById(id);
            }

            return null;
        }

        /// <summary>
        /// 检查玩家是否在线
        /// </summary>
        public bool IsPlayerOnline(string name)
        {
            lock (_lock)
            {
                return _playersByName.ContainsKey(name);
            }
        }

        /// <summary>
        /// 检查玩家是否在线（通过ID）
        /// </summary>
        public bool IsPlayerOnline(uint id)
        {
            lock (_lock)
            {
                return _playersById.ContainsKey(id & 0xffffff);
            }
        }

        /// <summary>
        /// 广播消息给所有玩家
        /// </summary>
        public void BroadcastToAllPlayers(byte[] message)
        {
            lock (_lock)
            {
                foreach (var player in _playersById.Values)
                {
                    try
                    {
                        player.SendMessage(message);
                    }
                    catch (Exception ex)
                    {
                        LogManager.Default.Error($"广播消息给玩家失败: {player.Name}", exception: ex);
                    }
                }
            }
        }

        /// <summary>
        /// 广播系统消息给所有玩家
        /// </summary>
        public void BroadcastSystemMessage(string message)
        {
            var builder = new PacketBuilder();
            builder.WriteUInt16(0x64); // SM_CHAT
            builder.WriteUInt16(0xff00); // 系统消息属性
            builder.WriteUInt16(0);
            builder.WriteUInt16(0);
            builder.WriteString($"[系统公告] {message}");

            BroadcastToAllPlayers(builder.Build());
        }

        /// <summary>
        /// 更新所有玩家（游戏循环中调用）
        /// </summary>
        public void UpdateAllPlayers()
        {
            lock (_lock)
            {
                foreach (var player in _playersById.Values.ToList()) // 使用副本避免修改集合
                {
                    try
                    {
                        player.Update();
                    }
                    catch (Exception ex)
                    {
                        LogManager.Default.Error($"更新玩家失败: {player.Name}", exception: ex);
                    }
                }
            }
        }

        /// <summary>
        /// 强制断开所有玩家连接（服务器关闭时使用）
        /// </summary>
        public void DisconnectAllPlayers()
        {
            lock (_lock)
            {
                var players = _playersById.Values.ToList();
                foreach (var player in players)
                {
                    try
                    {
                        // 发送断开连接消息
                        var builder = new PacketBuilder();
                        builder.WriteUInt16(0x100); // SM_DISCONNECT
                        builder.WriteUInt16(0);
                        builder.WriteUInt16(0);
                        builder.WriteUInt16(0);
                        builder.WriteString("服务器关闭");

                        player.SendMessage(builder.Build());

                        // 从地图移除
                        player.CurrentMap?.RemoveObject(player);

                        LogManager.Default.Info($"断开玩家连接: {player.Name}");
                    }
                    catch (Exception ex)
                    {
                        LogManager.Default.Error($"断开玩家连接失败: {player.Name}", exception: ex);
                    }
                }

                // 清空所有列表
                _playersById.Clear();
                _playersByName.Clear();
                _nextPlayerId = 1;

                LogManager.Default.Info($"已断开所有 {players.Count} 个玩家连接");
            }
        }

        /// <summary>
        /// 获取在线玩家统计信息
        /// </summary>
        public PlayerStats GetPlayerStats()
        {
            lock (_lock)
            {
                var stats = new PlayerStats
                {
                    TotalPlayers = _playersById.Count,
                    MaxPlayers = MAX_HUMANPLAYER,
                    PlayersByJob = new Dictionary<byte, int>(),
                    PlayersByLevel = new Dictionary<int, int>()
                };

                // 统计职业分布
                foreach (var player in _playersById.Values)
                {
                    // 职业统计
                    if (!stats.PlayersByJob.ContainsKey(player.Job))
                        stats.PlayersByJob[player.Job] = 0;
                    stats.PlayersByJob[player.Job]++;

                    // 等级统计（按10级分组）
                    int levelGroup = (player.Level / 10) * 10;
                    if (!stats.PlayersByLevel.ContainsKey(levelGroup))
                        stats.PlayersByLevel[levelGroup] = 0;
                    stats.PlayersByLevel[levelGroup]++;
                }

                return stats;
            }
        }

        /// <summary>
        /// 查找附近的玩家
        /// </summary>
        public List<HumanPlayer> FindNearbyPlayers(uint mapId, int x, int y, int range)
        {
            var result = new List<HumanPlayer>();

            lock (_lock)
            {
                foreach (var player in _playersById.Values)
                {
                    if (player.CurrentMap?.MapId == mapId)
                    {
                        int dx = Math.Abs(player.X - x);
                        int dy = Math.Abs(player.Y - y);
                        if (dx <= range && dy <= range)
                        {
                            result.Add(player);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 查找地图上的所有玩家
        /// </summary>
        public List<HumanPlayer> FindPlayersInMap(uint mapId)
        {
            var result = new List<HumanPlayer>();

            lock (_lock)
            {
                foreach (var player in _playersById.Values)
                {
                    if (player.CurrentMap?.MapId == mapId)
                    {
                        result.Add(player);
                    }
                }
            }

            return result;
        }
    }

    /// <summary>
    /// 玩家统计信息
    /// </summary>
    public class PlayerStats
    {
        public int TotalPlayers { get; set; }
        public int MaxPlayers { get; set; }
        public Dictionary<byte, int> PlayersByJob { get; set; } = new();
        public Dictionary<int, int> PlayersByLevel { get; set; } = new();

        public override string ToString()
        {
            string jobStats = string.Join(", ", PlayersByJob.Select(kv => $"{GetJobName(kv.Key)}:{kv.Value}"));
            string levelStats = string.Join(", ", PlayersByLevel.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}-{kv.Key + 9}级:{kv.Value}"));

            return $"在线玩家: {TotalPlayers}/{MaxPlayers} | 职业分布: {jobStats} | 等级分布: {levelStats}";
        }

        private string GetJobName(byte job)
        {
            return job switch
            {
                0 => "战士",
                1 => "法师",
                2 => "道士",
                _ => "未知"
            };
        }
    }
}
