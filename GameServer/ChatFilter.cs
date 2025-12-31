using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GameServer
{
    /// <summary>
    /// 聊天过滤器 - 敏感词检查和过滤系统
    /// </summary>
    public class ChatFilter
    {
        private static ChatFilter _instance;
        public static ChatFilter Instance => _instance ??= new ChatFilter();

        // 敏感词列表（应该从配置文件加载）
        private readonly HashSet<string> _sensitiveWords = new(StringComparer.OrdinalIgnoreCase);
        
        // 替换字符
        private const char REPLACE_CHAR = '*';
        
        // 正则表达式模式（用于更复杂的过滤）
        private readonly List<Regex> _regexPatterns = new();

        private ChatFilter()
        {
            InitializeSensitiveWords();
            InitializeRegexPatterns();
        }

        /// <summary>
        /// 初始化敏感词列表
        /// </summary>
        private void InitializeSensitiveWords()
        {
            // 这里应该从配置文件加载敏感词
            string[] defaultSensitiveWords = {
                "fuck", "shit", "asshole", "bitch", "bastard",
                "操", "傻逼", "脑残", "垃圾", "废物",
                "共产党", "政府", "领导人", "政治", "敏感词"
            };

            foreach (var word in defaultSensitiveWords)
            {
                _sensitiveWords.Add(word);
            }
        }

        /// <summary>
        /// 初始化正则表达式模式
        /// </summary>
        private void InitializeRegexPatterns()
        {
            // 添加一些正则表达式模式用于更复杂的过滤
            // 例如：电话号码、邮箱、网址等
            _regexPatterns.Add(new Regex(@"\d{11}", RegexOptions.Compiled)); // 11位手机号
            _regexPatterns.Add(new Regex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", RegexOptions.Compiled)); // 邮箱
            _regexPatterns.Add(new Regex(@"https?://[^\s]+", RegexOptions.Compiled)); // 网址
        }

        /// <summary>
        /// 检查消息是否包含敏感词
        /// </summary>
        public bool ContainsSensitiveWords(string message)
        {
            if (string.IsNullOrEmpty(message))
                return false;

            // 转换为小写进行不区分大小写的检查
            string lowerMessage = message.ToLowerInvariant();

            // 检查敏感词
            foreach (var word in _sensitiveWords)
            {
                if (lowerMessage.Contains(word.ToLowerInvariant()))
                {
                    return true;
                }
            }

            // 检查正则表达式模式
            foreach (var pattern in _regexPatterns)
            {
                if (pattern.IsMatch(message))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 过滤敏感词
        /// </summary>
        public string FilterSensitiveWords(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;

            string filteredMessage = message;
            
            // 过滤敏感词
            foreach (var word in _sensitiveWords)
            {
                if (filteredMessage.Contains(word, StringComparison.OrdinalIgnoreCase))
                {
                    // 用星号替换敏感词
                    string replacement = new string(REPLACE_CHAR, word.Length);
                    filteredMessage = Regex.Replace(filteredMessage, word, replacement, RegexOptions.IgnoreCase);
                }
            }

            // 过滤正则表达式匹配的内容
            foreach (var pattern in _regexPatterns)
            {
                filteredMessage = pattern.Replace(filteredMessage, "***");
            }

            return filteredMessage;
        }

        /// <summary>
        /// 添加敏感词（用于动态更新敏感词列表）
        /// </summary>
        public void AddSensitiveWord(string word)
        {
            if (!string.IsNullOrEmpty(word))
            {
                _sensitiveWords.Add(word);
            }
        }

        /// <summary>
        /// 移除敏感词
        /// </summary>
        public bool RemoveSensitiveWord(string word)
        {
            return _sensitiveWords.Remove(word);
        }

        /// <summary>
        /// 获取所有敏感词（用于管理界面）
        /// </summary>
        public List<string> GetAllSensitiveWords()
        {
            return _sensitiveWords.ToList();
        }

        /// <summary>
        /// 清空敏感词列表
        /// </summary>
        public void ClearSensitiveWords()
        {
            _sensitiveWords.Clear();
        }

        /// <summary>
        /// 重新加载敏感词（从配置文件）
        /// </summary>
        public void ReloadSensitiveWords()
        {
            _sensitiveWords.Clear();
            InitializeSensitiveWords();
        }

        /// <summary>
        /// 检查消息长度
        /// </summary>
        public bool CheckMessageLength(string message, int maxLength = 120)
        {
            if (string.IsNullOrEmpty(message))
                return false;

            return message.Length <= maxLength;
        }

        /// <summary>
        /// 截断过长的消息
        /// </summary>
        public string TruncateMessage(string message, int maxLength = 120)
        {
            if (string.IsNullOrEmpty(message) || message.Length <= maxLength)
                return message;

            return message.Substring(0, maxLength);
        }

        /// <summary>
        /// 检查消息是否包含非法字符
        /// </summary>
        public bool ContainsIllegalCharacters(string message)
        {
            if (string.IsNullOrEmpty(message))
                return false;

            // 检查控制字符（除了换行和回车）
            foreach (char c in message)
            {
                if (char.IsControl(c) && c != '\n' && c != '\r')
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 移除非法字符
        /// </summary>
        public string RemoveIllegalCharacters(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;

            // 移除控制字符（除了换行和回车）
            var chars = message.Where(c => !char.IsControl(c) || c == '\n' || c == '\r').ToArray();
            return new string(chars);
        }

        /// <summary>
        /// 完整的聊天消息处理
        /// </summary>
        public string ProcessChatMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;

            // 1. 移除非法字符
            string processedMessage = RemoveIllegalCharacters(message);

            // 2. 检查长度并截断
            if (!CheckMessageLength(processedMessage))
            {
                processedMessage = TruncateMessage(processedMessage);
            }

            // 3. 过滤敏感词
            if (ContainsSensitiveWords(processedMessage))
            {
                processedMessage = FilterSensitiveWords(processedMessage);
            }

            return processedMessage;
        }

        /// <summary>
        /// 检查是否可以发送消息
        /// </summary>
        public bool CanSendMessage(string message, out string reason)
        {
            reason = string.Empty;

            if (string.IsNullOrEmpty(message))
            {
                reason = "消息内容为空";
                return false;
            }

            // 检查长度
            if (!CheckMessageLength(message))
            {
                reason = $"消息过长（最大{120}字符）";
                return false;
            }

            // 检查非法字符
            if (ContainsIllegalCharacters(message))
            {
                reason = "消息包含非法字符";
                return false;
            }

            // 检查敏感词
            if (ContainsSensitiveWords(message))
            {
                reason = "消息包含敏感词";
                return false;
            }

            return true;
        }
    }
}
