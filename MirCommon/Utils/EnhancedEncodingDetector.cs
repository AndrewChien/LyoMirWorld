using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ude;

namespace MirCommon.Utils
{
    public class SmartReader
    {
        private static bool _encodingProviderRegistered = false;

        /// <summary>
        /// 注册编码提供程序（确保支持所有编码）
        /// </summary>
        private static void EnsureEncodingProvider()
        {
            if (!_encodingProviderRegistered)
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                _encodingProviderRegistered = true;
            }
        }

        /// <summary>
        /// 检测文件编码（增强版，解决Ude的局限性）
        /// </summary>
        public static Encoding DetectEncoding(string filePath, int sampleSize = 4096)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("文件不存在", filePath);

            EnsureEncodingProvider();

            // 阶段1：快速BOM检测（处理Ude可能漏掉的BOM情况）
            var bomEncoding = DetectBomEncodingQuick(filePath);
            if (bomEncoding != null)
                return bomEncoding;

            // 阶段2：使用Ude进行统计分析
            byte[] buffer = new byte[sampleSize];

            using (var fs = File.OpenRead(filePath))
            {
                int bytesRead = fs.Read(buffer, 0, sampleSize);

                // 使用Ude检测
                ICharsetDetector detector = new CharsetDetector();
                detector.Feed(buffer, 0, bytesRead);
                detector.DataEnd();

                if (detector.Charset != null && detector.Confidence > 0.5)
                {
                    string charset = detector.Charset.ToUpperInvariant();

                    // 优化编码名称映射
                    var encoding = MapCharsetToEncoding(charset);
                    if (encoding != null)
                        return encoding;
                }
            }

            // 阶段3：Ude未检测到或置信度低，尝试其他方法
            return FallbackDetection(filePath);
        }

        /// <summary>
        /// 快速BOM检测（比Ude更可靠）
        /// </summary>
        private static Encoding DetectBomEncodingQuick(string filePath)
        {
            byte[] bom = new byte[4];

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                int bytesRead = fs.Read(bom, 0, 4);

                if (bytesRead >= 4)
                {
                    // UTF-32 BE
                    if (bom[0] == 0x00 && bom[1] == 0x00 && bom[2] == 0xFE && bom[3] == 0xFF)
                        return Encoding.GetEncoding("UTF-32BE");

                    // UTF-32 LE
                    if (bom[0] == 0xFF && bom[1] == 0xFE && bom[2] == 0x00 && bom[3] == 0x00)
                        return Encoding.UTF32;
                }

                if (bytesRead >= 3)
                {
                    // UTF-8 BOM
                    if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                        return Encoding.UTF8;
                }

                if (bytesRead >= 2)
                {
                    // UTF-16 LE
                    if (bom[0] == 0xFF && bom[1] == 0xFE)
                        return Encoding.Unicode;

                    // UTF-16 BE
                    if (bom[0] == 0xFE && bom[1] == 0xFF)
                        return Encoding.BigEndianUnicode;
                }
            }

            return null;
        }

        /// <summary>
        /// 优化编码名称映射（解决Ude返回的非标准名称）
        /// </summary>
        private static Encoding MapCharsetToEncoding(string charset)
        {
            try
            {
                return charset.ToUpperInvariant() switch
                {
                    // UTF系列
                    "UTF-8" => Encoding.UTF8,
                    "UTF-8-BOM" => Encoding.UTF8,
                    "UTF-16LE" or "UTF-16" => Encoding.Unicode,
                    "UTF-16BE" => Encoding.BigEndianUnicode,
                    "UTF-32LE" or "UTF-32" => Encoding.UTF32,
                    "UTF-32BE" => Encoding.GetEncoding("UTF-32BE"),

                    // 中文编码
                    "GB2312" or "GBK" or "GB18030" => Encoding.GetEncoding("GB18030"), // 使用GB18030兼容所有
                    "BIG5" or "BIG5-HKSCS" => Encoding.GetEncoding("Big5"),

                    // 日文编码
                    "SHIFT_JIS" => Encoding.GetEncoding("Shift_JIS"),
                    "EUC-JP" => Encoding.GetEncoding("EUC-JP"),
                    "ISO-2022-JP" => Encoding.GetEncoding("ISO-2022-JP"),

                    // 韩文编码
                    "EUC-KR" => Encoding.GetEncoding("EUC-KR"),
                    "ISO-2022-KR" => Encoding.GetEncoding("ISO-2022-KR"),

                    // 西里尔字母
                    "WINDOWS-1251" => Encoding.GetEncoding(1251),
                    "KOI8-R" => Encoding.GetEncoding("KOI8-R"),
                    "KOI8-U" => Encoding.GetEncoding("KOI8-U"),
                    "ISO-8859-5" => Encoding.GetEncoding("ISO-8859-5"),

                    // 西欧语言
                    "WINDOWS-1252" => Encoding.GetEncoding(1252),
                    "ISO-8859-1" => Encoding.GetEncoding("ISO-8859-1"),
                    "ISO-8859-15" => Encoding.GetEncoding("ISO-8859-15"),

                    // 中东语言
                    "WINDOWS-1256" => Encoding.GetEncoding(1256), // 阿拉伯语
                    "WINDOWS-1255" => Encoding.GetEncoding(1255), // 希伯来语

                    // 默认尝试直接获取
                    _ => Encoding.GetEncoding(charset)
                };
            }
            catch (ArgumentException)
            {
                // 编码名称无法识别
                return null;
            }
        }

        /// <summary>
        /// 回退检测（Ude检测失败时使用）
        /// </summary>
        private static Encoding FallbackDetection(string filePath)
        {
            byte[] buffer = new byte[8192]; // 更大的样本

            using (var fs = File.OpenRead(filePath))
            {
                int bytesRead = fs.Read(buffer, 0, buffer.Length);

                // 1. 尝试UTF-8（无BOM）
                if (IsValidUtf8(buffer, bytesRead))
                    return Encoding.UTF8;

                // 2. 尝试中文编码
                var chineseEncodings = new[]
                {
                "GB18030", // 最全面的中文编码
                "GBK",
                "GB2312"
            };

                foreach (var encodingName in chineseEncodings)
                {
                    try
                    {
                        var encoding = Encoding.GetEncoding(encodingName);
                        string test = encoding.GetString(buffer, 0, Math.Min(bytesRead, 1024));

                        // 检查是否包含中文或常见ASCII字符
                        if (IsLikelyValidText(test, encodingName))
                            return encoding;
                    }
                    catch
                    {
                        // 继续尝试下一个
                    }
                }

                // 3. 尝试常见编码
                var commonEncodings = new[]
                {
                Encoding.Default,
                Encoding.GetEncoding(1252), // Windows West European
                Encoding.GetEncoding("ISO-8859-1"),
                Encoding.GetEncoding("Windows-1251") // Cyrillic
            };

                foreach (var encoding in commonEncodings)
                {
                    try
                    {
                        string test = encoding.GetString(buffer, 0, Math.Min(bytesRead, 1024));

                        // 简单有效性检查
                        if (!ContainsExcessiveControlChars(test))
                            return encoding;
                    }
                    catch
                    {
                        continue;
                    }
                }
            }

            // 4. 最后手段：系统默认编码
            return Encoding.Default;
        }

        /// <summary>
        /// 检查是否为有效的UTF-8（无BOM）
        /// </summary>
        private static bool IsValidUtf8(byte[] buffer, int length)
        {
            try
            {
                string test = Encoding.UTF8.GetString(buffer, 0, length);

                // 检查替换字符（U+FFFD）
                if (test.Contains('\uFFFD'))
                    return false;

                // 检查无效的控制字符（除了常见的如\r\n\t）
                foreach (char c in test)
                {
                    if (char.IsControl(c) && c != '\r' && c != '\n' && c != '\t')
                    {
                        // 如果是其他控制字符，可能不是有效文本
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 检查文本是否可能是有效的
        /// </summary>
        private static bool IsLikelyValidText(string text, string encodingName)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            int validChars = 0;
            int totalChars = Math.Min(text.Length, 100); // 检查前100个字符

            for (int i = 0; i < totalChars; i++)
            {
                char c = text[i];

                // 可打印ASCII字符（包括控制字符如\r\n\t）
                if (c >= 32 && c <= 126 || c == '\r' || c == '\n' || c == '\t')
                {
                    validChars++;
                }
                // 中文编码特有的字符范围
                else if (encodingName.StartsWith("GB") || encodingName == "Big5")
                {
                    if ((c >= 0x4E00 && c <= 0x9FFF) || // 基本汉字
                        (c >= 0x3400 && c <= 0x4DBF) || // 扩展A
                        (c >= 0x20000 && c <= 0x2A6DF) || // 扩展B
                        (c >= 0x2A700 && c <= 0x2B73F) || // 扩展C
                        (c >= 0x2B740 && c <= 0x2B81F) || // 扩展D
                        (c >= 0x2B820 && c <= 0x2CEAF))   // 扩展E
                    {
                        validChars++;
                    }
                }
            }

            // 如果有一定比例的可识别字符，认为是有效文本
            return (double)validChars / totalChars > 0.7;
        }

        /// <summary>
        /// 检查是否包含过多控制字符
        /// </summary>
        private static bool ContainsExcessiveControlChars(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            int controlChars = 0;
            int totalChars = Math.Min(text.Length, 100);

            for (int i = 0; i < totalChars; i++)
            {
                char c = text[i];

                // 统计非常见控制字符
                if (char.IsControl(c) && c != '\r' && c != '\n' && c != '\t')
                {
                    controlChars++;
                }
            }

            // 如果超过10%是控制字符，可能不是有效文本
            return (double)controlChars / totalChars > 0.1;
        }

        /// <summary>
        /// 读取文件并自动检测编码
        /// </summary>
        public static string ReadTextFile(string filePath)
        {
            var encoding = DetectEncoding(filePath);
            return File.ReadAllText(filePath, encoding);
        }

        /// <summary>
        /// 读取文件所有行
        /// </summary>
        public static string[] ReadAllLines(string filePath)
        {
            var encoding = DetectEncoding(filePath);
            return File.ReadAllLines(filePath, encoding);
        }

        /// <summary>
        /// 流式读取大文件
        /// </summary>
        public static IEnumerable<string> ReadLines(string filePath)
        {
            var encoding = DetectEncoding(filePath);

            using (var reader = new StreamReader(filePath, encoding))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    yield return line;
                }
            }
        }
    }
}
