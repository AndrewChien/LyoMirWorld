using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MirCommon.Utils;

namespace GameServer
{
    /// <summary>
    /// 排行榜类型
    /// </summary>
    public enum TopListType
    {
        Level = 0,      // 等级排行榜
        Power = 1,      // 战斗力排行榜
        Wealth = 2,     // 财富排行榜
        Pk = 3,         // PK排行榜
        Guild = 4,      // 行会排行榜
        Achievement = 5 // 成就排行榜
    }

    /// <summary>
    /// 排行榜条目
    /// </summary>
    public class TopListEntry
    {
        public int Rank { get; set; }
        public uint PlayerId { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public int Value { get; set; }
        public DateTime UpdateTime { get; set; }
        public object? ExtraData { get; set; }
    }

    /// <summary>
    /// 排行榜NPC位置
    /// </summary>
    public class TopNpcLocation
    {
        public string NpcName { get; set; } = string.Empty;
        public int MapId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public TopListType ListType { get; set; }
    }

    /// <summary>
    /// 排行榜管理器
    /// </summary>
    public class TopManager
    {
        private static TopManager? _instance;
        public static TopManager Instance => _instance ??= new TopManager();

        private readonly Dictionary<TopListType, List<TopListEntry>> _topLists = new();
        private readonly Dictionary<string, TopNpcLocation> _topNpcs = new();
        private readonly Dictionary<TopListType, string> _listDataFiles = new();

        private TopManager()
        {
            // 初始化所有排行榜类型
            foreach (TopListType type in Enum.GetValues(typeof(TopListType)))
            {
                _topLists[type] = new List<TopListEntry>();
            }
        }

        /// <summary>
        /// 加载排行榜NPC配置
        /// </summary>
        public bool LoadTopNpcs(string filePath)
        {
            if (!File.Exists(filePath))
            {
                LogManager.Default.Warning($"排行榜NPC配置文件不存在: {filePath}");
                return false;
            }

            try
            {
                var lines = SmartReader.ReadAllLines(filePath);
                int count = 0;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    var parts = line.Split('=');
                    if (parts.Length == 2)
                    {
                        var npcName = parts[0].Trim();
                        var locationStr = parts[1].Trim();
                        var locationParts = locationStr.Split(',');

                        if (locationParts.Length >= 3)
                        {
                            if (int.TryParse(locationParts[0], out int mapId) &&
                                int.TryParse(locationParts[1], out int x) &&
                                int.TryParse(locationParts[2], out int y))
                            {
                                var location = new TopNpcLocation
                                {
                                    NpcName = npcName,
                                    MapId = mapId,
                                    X = x,
                                    Y = y
                                };

                                // 尝试从NPC名称推断排行榜类型
                                if (npcName.Contains("等级"))
                                    location.ListType = TopListType.Level;
                                else if (npcName.Contains("战斗力"))
                                    location.ListType = TopListType.Power;
                                else if (npcName.Contains("财富"))
                                    location.ListType = TopListType.Wealth;
                                else if (npcName.Contains("PK"))
                                    location.ListType = TopListType.Pk;
                                else if (npcName.Contains("行会"))
                                    location.ListType = TopListType.Guild;
                                else
                                    location.ListType = TopListType.Level;

                                _topNpcs[npcName] = location;
                                count++;
                            }
                        }
                    }
                }

                LogManager.Default.Info($"加载排行榜NPC配置: {count} 个NPC");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载排行榜NPC配置失败: {filePath}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 加载排行榜数据配置
        /// </summary>
        public bool LoadTopListConfig(string filePath)
        {
            if (!File.Exists(filePath))
            {
                LogManager.Default.Warning($"排行榜数据配置文件不存在: {filePath}");
                return false;
            }

            try
            {
                var lines = SmartReader.ReadAllLines(filePath);
                int count = 0;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    var parts = line.Split('=');
                    if (parts.Length == 2)
                    {
                        var listTypeStr = parts[0].Trim();
                        var dataFile = parts[1].Trim();

                        if (Enum.TryParse<TopListType>(listTypeStr, true, out var listType))
                        {
                            _listDataFiles[listType] = dataFile;
                            count++;
                        }
                    }
                }

                LogManager.Default.Info($"加载排行榜数据配置: {count} 个排行榜类型");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载排行榜数据配置失败: {filePath}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 加载排行榜数据
        /// </summary>
        public bool LoadTopListData(TopListType listType, string filePath)
        {
            if (!File.Exists(filePath))
            {
                LogManager.Default.Warning($"排行榜数据文件不存在: {filePath}");
                return false;
            }

            try
            {
                var entries = new List<TopListEntry>();
                var lines = SmartReader.ReadAllLines(filePath);
                int rank = 1;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    var parts = line.Split(',');
                    if (parts.Length >= 3)
                    {
                        if (uint.TryParse(parts[0], out uint playerId) &&
                            int.TryParse(parts[2], out int value))
                        {
                            var entry = new TopListEntry
                            {
                                Rank = rank++,
                                PlayerId = playerId,
                                PlayerName = parts[1].Trim(),
                                Value = value,
                                UpdateTime = DateTime.Now
                            };

                            // 如果有额外数据
                            if (parts.Length > 3)
                            {
                                entry.ExtraData = parts[3].Trim();
                            }

                            entries.Add(entry);
                        }
                    }
                }

                _topLists[listType] = entries;
                LogManager.Default.Info($"加载排行榜数据: {listType}, {entries.Count} 个条目");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载排行榜数据失败: {filePath}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 更新排行榜条目
        /// </summary>
        public void UpdateTopListEntry(TopListType listType, uint playerId, string playerName, int value, object? extraData = null)
        {
            var list = _topLists[listType];
            
            // 查找现有条目
            var existingEntry = list.Find(e => e.PlayerId == playerId);
            
            if (existingEntry != null)
            {
                // 更新现有条目
                existingEntry.PlayerName = playerName;
                existingEntry.Value = value;
                existingEntry.ExtraData = extraData;
                existingEntry.UpdateTime = DateTime.Now;
            }
            else
            {
                // 添加新条目
                var newEntry = new TopListEntry
                {
                    PlayerId = playerId,
                    PlayerName = playerName,
                    Value = value,
                    UpdateTime = DateTime.Now,
                    ExtraData = extraData
                };
                list.Add(newEntry);
            }

            // 重新排序
            SortTopList(listType);
        }

        /// <summary>
        /// 获取排行榜
        /// </summary>
        public List<TopListEntry> GetTopList(TopListType listType, int maxCount = 100)
        {
            var list = _topLists[listType];
            return list.Count > maxCount ? list.GetRange(0, maxCount) : list;
        }

        /// <summary>
        /// 获取玩家排名
        /// </summary>
        public int GetPlayerRank(TopListType listType, uint playerId)
        {
            var list = _topLists[listType];
            var entry = list.Find(e => e.PlayerId == playerId);
            return entry?.Rank ?? -1;
        }

        /// <summary>
        /// 获取排行榜NPC位置
        /// </summary>
        public TopNpcLocation? GetTopNpcLocation(string npcName)
        {
            return _topNpcs.TryGetValue(npcName, out var location) ? location : null;
        }

        /// <summary>
        /// 获取所有排行榜NPC
        /// </summary>
        public IEnumerable<TopNpcLocation> GetAllTopNpcs()
        {
            return _topNpcs.Values;
        }

        /// <summary>
        /// 获取排行榜数据文件
        /// </summary>
        public string? GetListDataFile(TopListType listType)
        {
            return _listDataFiles.TryGetValue(listType, out var file) ? file : null;
        }

        /// <summary>
        /// 保存排行榜数据到文件
        /// </summary>
        public bool SaveTopListData(TopListType listType, string filePath)
        {
            try
            {
                var list = _topLists[listType];
                var lines = new List<string>();

                foreach (var entry in list)
                {
                    var line = $"{entry.PlayerId},{entry.PlayerName},{entry.Value}";
                    if (entry.ExtraData != null)
                    {
                        line += $",{entry.ExtraData}";
                    }
                    lines.Add(line);
                }

                File.WriteAllLines(filePath, lines, Encoding.GetEncoding("GBK"));
                LogManager.Default.Info($"保存排行榜数据: {listType}, {list.Count} 个条目");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"保存排行榜数据失败: {filePath}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 更新所有排行榜
        /// </summary>
        public void UpdateAllTopLists()
        {
            // 这里应该从游戏数据中更新所有排行榜
            LogManager.Default.Debug("更新所有排行榜");
            
            foreach (TopListType type in Enum.GetValues(typeof(TopListType)))
            {
                // 重新排序
                SortTopList(type);
            }
        }

        /// <summary>
        /// 更新排行榜管理器（供GameWorld调用）
        /// </summary>
        public void Update()
        {
            UpdateAllTopLists();
        }

        /// <summary>
        /// 排序排行榜
        /// </summary>
        private void SortTopList(TopListType listType)
        {
            var list = _topLists[listType];
            
            // 根据排行榜类型使用不同的排序规则
            switch (listType)
            {
                case TopListType.Pk:
                    // PK排行榜：值越大排名越靠前
                    list.Sort((a, b) => b.Value.CompareTo(a.Value));
                    break;
                default:
                    // 其他排行榜：值越大排名越靠前
                    list.Sort((a, b) => b.Value.CompareTo(a.Value));
                    break;
            }

            // 更新排名
            for (int i = 0; i < list.Count; i++)
            {
                list[i].Rank = i + 1;
            }
        }
    }
}
