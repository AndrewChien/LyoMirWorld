namespace GameServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MirCommon;
    using MirCommon.Network;
    using MirCommon.Utils;

    /// <summary>
    /// 聊天消息
    /// </summary>
    public class ChatMessage
    {
        public uint SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public ChatChannel Channel { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public uint TargetId { get; set; }
        public string TargetName { get; set; } = string.Empty;

        public ChatMessage(uint senderId, string senderName, ChatChannel channel, string message)
        {
            SenderId = senderId;
            SenderName = senderName;
            Channel = channel;
            Message = message;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// 聊天管理器
    /// 注意：现在使用MirCommon.ChatChannel统一定义
    /// 频道映射关系：
    /// - Normal -> AREA (区域聊天)
    /// - Whisper -> PRIVATE (私聊)
    /// - Guild -> GUILD (行会)
    /// - Group -> TEAM (队伍)
    /// - System -> SYSTEM (系统)
    /// - World -> WORLD (世界)
    /// - Trade -> TRADE (交易)
    /// - Shout -> HORN (喇叭/喊话)
    /// - Help -> HELP (帮助)
    /// - Announcement -> ANNOUNCEMENT (公告)
    /// </summary>
    public class ChatManager
    {
        private static ChatManager? _instance;
        public static ChatManager Instance => _instance ??= new ChatManager();

        private readonly Dictionary<ChatChannel, List<ChatMessage>> _channelHistory = new();
        private readonly Dictionary<uint, List<ChatMessage>> _playerHistory = new();
        private readonly object _lock = new();

        // 聊天限制
        private readonly Dictionary<uint, DateTime> _lastChatTime = new();
        private readonly Dictionary<uint, int> _chatSpamCount = new();
        private const int MAX_CHAT_SPAM = 5;
        private const int CHAT_SPAM_INTERVAL_SECONDS = 10;
        private const int CHAT_COOLDOWN_SECONDS = 1;

        private ChatManager()
        {
            // 初始化所有频道
            foreach (ChatChannel channel in Enum.GetValues(typeof(ChatChannel)))
            {
                _channelHistory[channel] = new List<ChatMessage>();
            }
        }

        /// <summary>
        /// 发送聊天消息
        /// </summary>
        public bool SendMessage(HumanPlayer sender, ChatChannel channel, string message, uint targetId = 0, string targetName = "")
        {
            if (sender == null || string.IsNullOrWhiteSpace(message))
                return false;

            // 检查聊天限制
            if (!CanChat(sender.ObjectId))
            {
                sender.SaySystem("发言过于频繁，请稍后再试");
                return false;
            }

            // 检查消息长度
            if (message.Length > 200)
            {
                sender.SaySystem("消息过长，最多200个字符");
                return false;
            }

            // 检查敏感词
            if (ContainsSensitiveWords(message))
            {
                sender.SaySystem("消息包含敏感词汇");
                return false;
            }

            // 根据频道处理消息（使用MirCommon.ChatChannel）
            switch (channel)
            {
                case ChatChannel.AREA:  // 区域聊天（原Normal）
                    return SendNormalMessage(sender, message);
                    
                case ChatChannel.PRIVATE:  // 私聊（原Whisper）
                    return SendWhisperMessage(sender, targetId, targetName, message);
                    
                case ChatChannel.GUILD:  // 行会聊天
                    return SendGuildMessage(sender, message);
                    
                case ChatChannel.TEAM:  // 组队聊天（原Group）
                    return SendGroupMessage(sender, message);
                    
                case ChatChannel.WORLD:  // 世界聊天
                    return SendWorldMessage(sender, message);
                    
                case ChatChannel.TRADE:  // 交易频道
                    return SendTradeMessage(sender, message);
                    
                case ChatChannel.HORN:  // 喇叭/喊话（原Shout）
                    return SendShoutMessage(sender, message);
                    
                case ChatChannel.HELP:  // 帮助频道
                    return SendHelpMessage(sender, message);
                    
                default:
                    return false;
            }
        }

        /// <summary>
        /// 发送普通聊天消息
        /// </summary>
        private bool SendNormalMessage(HumanPlayer sender, string message)
        {
            // 获取附近玩家
            var nearbyPlayers = GetNearbyPlayers(sender);
            if (nearbyPlayers.Count == 0)
                return true;

            var chatMessage = new ChatMessage(sender.ObjectId, sender.Name, ChatChannel.AREA, message);
            
            // 发送给附近玩家
            foreach (var player in nearbyPlayers)
            {
                SendChatMessageToPlayer(player, chatMessage);
            }

            // 记录消息
            RecordMessage(chatMessage);
            
            LogManager.Default.Info($"[普通] {sender.Name}: {message}");
            return true;
        }

        /// <summary>
        /// 发送私聊消息
        /// </summary>
        private bool SendWhisperMessage(HumanPlayer sender, uint targetId, string targetName, string message)
        {
            // 获取目标玩家
            var targetPlayer = HumanPlayerMgr.Instance.FindById(targetId);
            if (targetPlayer == null)
            {
                sender.SaySystem($"玩家 {targetName} 不在线");
                return false;
            }

            var chatMessage = new ChatMessage(sender.ObjectId, sender.Name, ChatChannel.PRIVATE, message)
            {
                TargetId = targetId,
                TargetName = targetName
            };

            // 发送给发送者
            SendChatMessageToPlayer(sender, chatMessage);
            
            // 发送给接收者
            SendChatMessageToPlayer(targetPlayer, chatMessage);
            
            // 记录消息
            RecordMessage(chatMessage);
            
            LogManager.Default.Info($"[私聊] {sender.Name} -> {targetName}: {message}");
            return true;
        }

        /// <summary>
        /// 发送行会消息
        /// </summary>
        private bool SendGuildMessage(HumanPlayer sender, string message)
        {
            if (sender.Guild == null)
            {
                sender.SaySystem("你还没有加入行会");
                return false;
            }

            var chatMessage = new ChatMessage(sender.ObjectId, sender.Name, ChatChannel.GUILD, message);
            
            // 获取行会所有成员
            var guildMembers = sender.Guild.GetAllMembers();
            foreach (var member in guildMembers)
            {
                var player = HumanPlayerMgr.Instance.FindById(member.PlayerId);
                if (player != null)
                {
                    SendChatMessageToPlayer(player, chatMessage);
                }
            }

            // 记录消息
            RecordMessage(chatMessage);
            
            LogManager.Default.Info($"[行会] {sender.Name}: {message}");
            return true;
        }

        /// <summary>
        /// 发送组队消息
        /// </summary>
        private bool SendGroupMessage(HumanPlayer sender, string message)
        {
            if (sender.GroupId == 0)
            {
                sender.SaySystem("你还没有加入队伍");
                return false;
            }

            // TODO: 实现组队系统后完善此方法
            sender.SaySystem("组队功能暂未实现");
            return false;
        }

        /// <summary>
        /// 发送世界消息
        /// </summary>
        private bool SendWorldMessage(HumanPlayer sender, string message)
        {
            // 检查等级限制
            if (sender.Level < 20)
            {
                sender.SaySystem("需要20级才能使用世界频道");
                return false;
            }

            var chatMessage = new ChatMessage(sender.ObjectId, sender.Name, ChatChannel.WORLD, message);
            
            // 发送给所有在线玩家
            var allPlayers = HumanPlayerMgr.Instance.GetAllPlayers();
            foreach (var player in allPlayers)
            {
                SendChatMessageToPlayer(player, chatMessage);
            }

            // 记录消息
            RecordMessage(chatMessage);
            
            LogManager.Default.Info($"[世界] {sender.Name}: {message}");
            return true;
        }

        /// <summary>
        /// 发送交易消息
        /// </summary>
        private bool SendTradeMessage(HumanPlayer sender, string message)
        {
            // 检查等级限制
            if (sender.Level < 15)
            {
                sender.SaySystem("需要15级才能使用交易频道");
                return false;
            }

            var chatMessage = new ChatMessage(sender.ObjectId, sender.Name, ChatChannel.TRADE, message);
            
            // 发送给所有在线玩家
            var allPlayers = HumanPlayerMgr.Instance.GetAllPlayers();
            foreach (var player in allPlayers)
            {
                SendChatMessageToPlayer(player, chatMessage);
            }

            // 记录消息
            RecordMessage(chatMessage);
            
            LogManager.Default.Info($"[交易] {sender.Name}: {message}");
            return true;
        }

        /// <summary>
        /// 发送喊话消息
        /// </summary>
        private bool SendShoutMessage(HumanPlayer sender, string message)
        {
            // 喊话有更远的范围
            var shoutPlayers = GetShoutRangePlayers(sender);
            if (shoutPlayers.Count == 0)
                return true;

            var chatMessage = new ChatMessage(sender.ObjectId, sender.Name, ChatChannel.HORN, message);
            
            foreach (var player in shoutPlayers)
            {
                SendChatMessageToPlayer(player, chatMessage);
            }

            // 记录消息
            RecordMessage(chatMessage);
            
            LogManager.Default.Info($"[喊话] {sender.Name}: {message}");
            return true;
        }

        /// <summary>
        /// 发送帮助消息
        /// </summary>
        private bool SendHelpMessage(HumanPlayer sender, string message)
        {
            var chatMessage = new ChatMessage(sender.ObjectId, sender.Name, ChatChannel.HELP, message);
            
            // 发送给所有在线玩家
            var allPlayers = HumanPlayerMgr.Instance.GetAllPlayers();
            foreach (var player in allPlayers)
            {
                SendChatMessageToPlayer(player, chatMessage);
            }

            // 记录消息
            RecordMessage(chatMessage);
            
            LogManager.Default.Info($"[帮助] {sender.Name}: {message}");
            return true;
        }

        /// <summary>
        /// 发送系统消息
        /// </summary>
        public void SendSystemMessage(string message, ChatChannel channel = ChatChannel.SYSTEM)
        {
            var chatMessage = new ChatMessage(0, "系统", channel, message);
            
            // 发送给所有在线玩家
            var allPlayers = HumanPlayerMgr.Instance.GetAllPlayers();
            foreach (var player in allPlayers)
            {
                SendChatMessageToPlayer(player, chatMessage);
            }

            // 记录消息
            RecordMessage(chatMessage);
            
            LogManager.Default.Info($"[系统] {message}");
        }

        /// <summary>
        /// 发送公告
        /// </summary>
        public void SendAnnouncement(string message)
        {
            var chatMessage = new ChatMessage(0, "公告", ChatChannel.ANNOUNCEMENT, message);
            
            // 发送给所有在线玩家
            var allPlayers = HumanPlayerMgr.Instance.GetAllPlayers();
            foreach (var player in allPlayers)
            {
                SendChatMessageToPlayer(player, chatMessage);
            }

            // 记录消息
            RecordMessage(chatMessage);
            
            LogManager.Default.Info($"[公告] {message}");
        }

        /// <summary>
        /// 发送聊天消息给玩家
        /// </summary>
        private void SendChatMessageToPlayer(HumanPlayer player, ChatMessage chatMessage)
        {
            // 使用SendMsg方法发送编码消息
            // 构建负载数据：发送者ID + 发送者名称 + 频道 + 消息 + 目标ID + 目标名称
            // 注意：这里需要将数据转换为字节数组
            using (var ms = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(ms))
            {
                // 写入发送者ID
                writer.Write(chatMessage.SenderId);
                
                // 写入发送者名称（GBK编码）
                byte[] senderNameBytes = System.Text.Encoding.GetEncoding("GBK").GetBytes(chatMessage.SenderName);
                writer.Write(senderNameBytes);
                writer.Write((byte)0); // 字符串结束符
                
                // 写入频道
                writer.Write((ushort)chatMessage.Channel);
                
                // 写入消息（GBK编码）
                byte[] messageBytes = System.Text.Encoding.GetEncoding("GBK").GetBytes(chatMessage.Message);
                writer.Write(messageBytes);
                writer.Write((byte)0); // 字符串结束符
                
                // 写入目标ID
                writer.Write(chatMessage.TargetId);
                
                // 写入目标名称（GBK编码）
                byte[] targetNameBytes = System.Text.Encoding.GetEncoding("GBK").GetBytes(chatMessage.TargetName);
                writer.Write(targetNameBytes);
                writer.Write((byte)0); // 字符串结束符
                
                byte[] payload = ms.ToArray();
                
                // 发送消息
                player.SendMsg(player.ObjectId, GameMessageHandler.ServerCommands.SM_CHAT, 0, 0, 0, payload);
            }
        }

        /// <summary>
        /// 获取附近玩家
        /// </summary>
        private List<HumanPlayer> GetNearbyPlayers(HumanPlayer sender)
        {
            var nearbyPlayers = new List<HumanPlayer>();
            
            if (sender.CurrentMap == null)
                return nearbyPlayers;

            // 获取地图上的所有玩家
            var allPlayers = HumanPlayerMgr.Instance.GetAllPlayers();
            foreach (var player in allPlayers)
            {
                if (player.ObjectId == sender.ObjectId)
                    continue;

                if (player.CurrentMap != sender.CurrentMap)
                    continue;

                // 检查距离（普通聊天范围：10格）
                int distance = Math.Abs(sender.X - player.X) + Math.Abs(sender.Y - player.Y);
                if (distance <= 10)
                {
                    nearbyPlayers.Add(player);
                }
            }

            return nearbyPlayers;
        }

        /// <summary>
        /// 获取喊话范围玩家
        /// </summary>
        private List<HumanPlayer> GetShoutRangePlayers(HumanPlayer sender)
        {
            var shoutPlayers = new List<HumanPlayer>();
            
            if (sender.CurrentMap == null)
                return shoutPlayers;

            // 获取地图上的所有玩家
            var allPlayers = HumanPlayerMgr.Instance.GetAllPlayers();
            foreach (var player in allPlayers)
            {
                if (player.ObjectId == sender.ObjectId)
                    continue;

                if (player.CurrentMap != sender.CurrentMap)
                    continue;

                // 喊话范围：20格
                int distance = Math.Abs(sender.X - player.X) + Math.Abs(sender.Y - player.Y);
                if (distance <= 20)
                {
                    shoutPlayers.Add(player);
                }
            }

            return shoutPlayers;
        }

        /// <summary>
        /// 检查是否可以发言
        /// </summary>
        private bool CanChat(uint playerId)
        {
            lock (_lock)
            {
                var now = DateTime.Now;
                
                // 检查冷却时间
                if (_lastChatTime.TryGetValue(playerId, out var lastTime))
                {
                    var timeSinceLastChat = (now - lastTime).TotalSeconds;
                    if (timeSinceLastChat < CHAT_COOLDOWN_SECONDS)
                        return false;
                }

                // 检查刷屏
                if (!_chatSpamCount.ContainsKey(playerId))
                    _chatSpamCount[playerId] = 0;

                // 重置刷屏计数
                if ((now - lastTime).TotalSeconds > CHAT_SPAM_INTERVAL_SECONDS)
                {
                    _chatSpamCount[playerId] = 0;
                }

                // 检查是否超过限制
                if (_chatSpamCount[playerId] >= MAX_CHAT_SPAM)
                    return false;

                // 更新计数和时间
                _chatSpamCount[playerId]++;
                _lastChatTime[playerId] = now;
                
                return true;
            }
        }

        /// <summary>
        /// 检查敏感词
        /// </summary>
        private bool ContainsSensitiveWords(string message)
        {
            // 这里应该从配置文件加载敏感词列表
            string[] sensitiveWords = {
                "fuck", "shit", "asshole", "bitch", "damn",
                "操", "傻逼", "垃圾", "废物", "妈的"
            };

            string lowerMessage = message.ToLower();
            foreach (var word in sensitiveWords)
            {
                if (lowerMessage.Contains(word))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 记录消息
        /// </summary>
        private void RecordMessage(ChatMessage message)
        {
            lock (_lock)
            {
                // 记录到频道历史
                if (_channelHistory.ContainsKey(message.Channel))
                {
                    var history = _channelHistory[message.Channel];
                    history.Add(message);
                    
                    // 限制历史记录数量
                    if (history.Count > 1000)
                    {
                        history.RemoveAt(0);
                    }
                }

                // 记录到玩家历史
                if (!_playerHistory.ContainsKey(message.SenderId))
                {
                    _playerHistory[message.SenderId] = new List<ChatMessage>();
                }
                
                var playerHistory = _playerHistory[message.SenderId];
                playerHistory.Add(message);
                
                if (playerHistory.Count > 500)
                {
                    playerHistory.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// 获取频道历史消息
        /// </summary>
        public List<ChatMessage> GetChannelHistory(ChatChannel channel, int count = 50)
        {
            lock (_lock)
            {
                if (_channelHistory.TryGetValue(channel, out var history))
                {
                    int startIndex = Math.Max(0, history.Count - count);
                    return history.Skip(startIndex).Take(count).ToList();
                }
                return new List<ChatMessage>();
            }
        }

        /// <summary>
        /// 获取玩家历史消息
        /// </summary>
        public List<ChatMessage> GetPlayerHistory(uint playerId, int count = 100)
        {
            lock (_lock)
            {
                if (_playerHistory.TryGetValue(playerId, out var history))
                {
                    int startIndex = Math.Max(0, history.Count - count);
                    return history.Skip(startIndex).Take(count).ToList();
                }
                return new List<ChatMessage>();
            }
        }

        /// <summary>
        /// 清除玩家聊天记录
        /// </summary>
        public void ClearPlayerHistory(uint playerId)
        {
            lock (_lock)
            {
                _playerHistory.Remove(playerId);
            }
        }

        /// <summary>
        /// 清除频道聊天记录
        /// </summary>
        public void ClearChannelHistory(ChatChannel channel)
        {
            lock (_lock)
            {
                if (_channelHistory.ContainsKey(channel))
                {
                    _channelHistory[channel].Clear();
                }
            }
        }

        /// <summary>
        /// 获取玩家最后发言时间
        /// </summary>
        public DateTime? GetLastChatTime(uint playerId)
        {
            lock (_lock)
            {
                if (_lastChatTime.TryGetValue(playerId, out var lastTime))
                {
                    return lastTime;
                }
                return null;
            }
        }

        /// <summary>
        /// 重置玩家聊天限制
        /// </summary>
        public void ResetChatLimits(uint playerId)
        {
            lock (_lock)
            {
                _lastChatTime.Remove(playerId);
                _chatSpamCount.Remove(playerId);
            }
        }
    }
}
