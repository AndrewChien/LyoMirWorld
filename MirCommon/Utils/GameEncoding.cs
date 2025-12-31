using System;
using System.Text;

namespace MirCommon.Utils
{
    /// <summary>
    /// 游戏编码辅助类
    /// C++在Windows中文环境下使用GBK编码（Code Page 936）
    /// </summary>
    public static class GameEncoding
    {
        /// <summary>
        /// GBK编码（Code Page 936）
        /// </summary>
        public static readonly Encoding GBK;

        static GameEncoding()
        {
            // 注册编码提供程序，以支持GBK等编码
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            
            // 获取GBK编码
            GBK = Encoding.GetEncoding("GBK");
        }
        
        /// <summary>
        /// 获取GBK编码的字节数组
        /// </summary>
        /// <param name="text">要编码的字符串</param>
        /// <returns>GBK编码的字节数组</returns>
        public static byte[] GetBytes(string text)
        {
            if (string.IsNullOrEmpty(text))
                return Array.Empty<byte>();
            return GBK.GetBytes(text);
        }
        
        /// <summary>
        /// 从GBK编码的字节数组获取字符串
        /// </summary>
        /// <param name="bytes">GBK编码的字节数组</param>
        /// <returns>解码后的字符串</returns>
        public static string GetString(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;
            return GBK.GetString(bytes).TrimEnd('\0');
        }
        
        /// <summary>
        /// 从GBK编码的字节数组获取字符串（指定范围）
        /// </summary>
        /// <param name="bytes">GBK编码的字节数组</param>
        /// <param name="index">开始位置</param>
        /// <param name="count">字节数量</param>
        /// <returns>解码后的字符串</returns>
        public static string GetString(byte[] bytes, int index, int count)
        {
            if (bytes == null || bytes.Length == 0 || count == 0)
                return string.Empty;
            return GBK.GetString(bytes, index, count).TrimEnd('\0');
        }
    }
}
