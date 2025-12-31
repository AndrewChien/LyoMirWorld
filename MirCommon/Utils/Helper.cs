using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MirCommon.Utils
{
    public class Helper
    {
        /// <summary>
        /// 将字符串转换为固定字节数组
        /// 不足指定长度用0填充，超过指定长度截断
        /// </summary>
        public static byte[] ConvertToFixedBytes(string text, int bytelength, Encoding encoding = null)
        {
            encoding = encoding ?? Encoding.GetEncoding("GBK");
            byte[] result = new byte[bytelength];

            if (string.IsNullOrEmpty(text))
                return result; // 全0数组

            byte[] tempBytes = encoding.GetBytes(text);

            // 复制数据到固定长度数组
            int copyLength = Math.Min(tempBytes.Length, bytelength);
            Buffer.BlockCopy(tempBytes, 0, result, 0, copyLength);

            return result;
        }

        /// <summary>
        /// 十六进制转int
        /// </summary>
        public static bool TryHexToInt(string hexString, out int result)
        {
            result = 0;

            if (string.IsNullOrWhiteSpace(hexString))
                return false;

            try
            {
                hexString = hexString.Trim();
                hexString = NormalizeHexString(hexString);

                result = Convert.ToInt32(hexString, 16);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 标准化十六进制字符串（移除各种前缀）
        /// </summary>
        private static string NormalizeHexString(string hexString)
        {
            // 统一转为大写便于处理
            hexString = hexString.ToUpperInvariant();

            // 移除常见的前缀
            if (hexString.StartsWith("0X"))
                return hexString.Substring(2);
            if (hexString.StartsWith("X"))
                return hexString.Substring(1);
            if (hexString.StartsWith("#"))
                return hexString.Substring(1);
            if (hexString.StartsWith("&H"))
                return hexString.Substring(2);

            return hexString;
        }

        public static bool BoolParser(string val)
        {
            val = val.Trim();
            if (!string.IsNullOrEmpty(val))
            {
                if (val.Equals("1")) return true;
                else if (val.Equals("0")) return false;
                else if (bool.TryParse(val, out bool res))
                {
                    return res;
                }
                return false;
            }
            return false;
        }
    }
}
