using System;
using System.Runtime.InteropServices;

namespace MirCommon
{
    /// <summary>
    /// 位字段支持工具类
    /// </summary>
    public static class BitFieldHelper
    {
        /// <summary>
        /// 从值中提取位字段
        /// </summary>
        /// <param name="value">原始值</param>
        /// <param name="offset">位偏移量</param>
        /// <param name="mask">位掩码</param>
        /// <returns>提取的位字段值</returns>
        public static uint ExtractBits(uint value, int offset, uint mask)
        {
            return (value >> offset) & mask;
        }
        
        /// <summary>
        /// 设置位字段到值中
        /// </summary>
        /// <param name="original">原始值</param>
        /// <param name="bits">要设置的位字段值</param>
        /// <param name="offset">位偏移量</param>
        /// <param name="mask">位掩码</param>
        /// <returns>设置后的值</returns>
        public static uint SetBits(uint original, uint bits, int offset, uint mask)
        {
            // 清除原始值中的位字段
            uint cleared = original & ~(mask << offset);
            // 设置新的位字段
            return cleared | ((bits & mask) << offset);
        }
        
        /// <summary>
        /// 创建位掩码
        /// </summary>
        /// <param name="bitCount">位数</param>
        /// <returns>位掩码</returns>
        public static uint CreateMask(int bitCount)
        {
            if (bitCount >= 32) return uint.MaxValue;
            return (1u << bitCount) - 1;
        }
    }
    
    /// <summary>
    /// 修复后的删除日期结构
    /// 总大小：4字节
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DeletedDateFixed
    {
        private uint _data;
        
        // 位字段定义
        private const int YEAR_OFFSET = 0;
        private const uint YEAR_MASK = 0xFFFu;      // 12位 (0-4095)
        private const int YEAR_BITS = 12;
        
        private const int MONTH_OFFSET = 12;
        private const uint MONTH_MASK = 0xFu;       // 4位 (0-15)
        private const int MONTH_BITS = 4;
        
        private const int DAY_OFFSET = 16;
        private const uint DAY_MASK = 0x1Fu;        // 5位 (0-31)
        private const int DAY_BITS = 5;
        
        private const int HOUR_OFFSET = 21;
        private const uint HOUR_MASK = 0xFu;        // 4位 (0-15)
        private const int HOUR_BITS = 4;
        
        private const int MINUTE_OFFSET = 25;
        private const uint MINUTE_MASK = 0x3Fu;     // 6位 (0-63)
        private const int MINUTE_BITS = 6;
        
        private const int BFLAG_OFFSET = 31;
        private const uint BFLAG_MASK = 0x1u;       // 1位 (0-1)
        private const int BFLAG_BITS = 1;
        
        /// <summary>
        /// 年份（0-4095）
        /// </summary>
        public uint Year
        {
            get => BitFieldHelper.ExtractBits(_data, YEAR_OFFSET, YEAR_MASK);
            set => _data = BitFieldHelper.SetBits(_data, value, YEAR_OFFSET, YEAR_MASK);
        }
        
        /// <summary>
        /// 月份（0-15）
        /// </summary>
        public uint Month
        {
            get => BitFieldHelper.ExtractBits(_data, MONTH_OFFSET, MONTH_MASK);
            set => _data = BitFieldHelper.SetBits(_data, value, MONTH_OFFSET, MONTH_MASK);
        }
        
        /// <summary>
        /// 日期（0-31）
        /// </summary>
        public uint Day
        {
            get => BitFieldHelper.ExtractBits(_data, DAY_OFFSET, DAY_MASK);
            set => _data = BitFieldHelper.SetBits(_data, value, DAY_OFFSET, DAY_MASK);
        }
        
        /// <summary>
        /// 小时（0-15）
        /// </summary>
        public uint Hour
        {
            get => BitFieldHelper.ExtractBits(_data, HOUR_OFFSET, HOUR_MASK);
            set => _data = BitFieldHelper.SetBits(_data, value, HOUR_OFFSET, HOUR_MASK);
        }
        
        /// <summary>
        /// 分钟（0-63）
        /// </summary>
        public uint Minute
        {
            get => BitFieldHelper.ExtractBits(_data, MINUTE_OFFSET, MINUTE_MASK);
            set => _data = BitFieldHelper.SetBits(_data, value, MINUTE_OFFSET, MINUTE_MASK);
        }
        
        /// <summary>
        /// 标志位（最高位）
        /// </summary>
        public bool BFlag
        {
            get => BitFieldHelper.ExtractBits(_data, BFLAG_OFFSET, BFLAG_MASK) == 1;
            set => _data = BitFieldHelper.SetBits(_data, value ? 1u : 0u, BFLAG_OFFSET, BFLAG_MASK);
        }
        
        /// <summary>
        /// 获取原始数据
        /// </summary>
        public uint RawData => _data;
        
        /// <summary>
        /// 设置原始数据
        /// </summary>
        public void SetRawData(uint data)
        {
            _data = data;
        }
        
        /// <summary>
        /// 默认构造函数
        /// </summary>
        public DeletedDateFixed()
        {
            _data = 0;
        }
        
        /// <summary>
        /// 创建删除日期
        /// </summary>
        public DeletedDateFixed(uint year, uint month, uint day, uint hour, uint minute, bool bFlag = false)
        {
            _data = 0;
            Year = year;
            Month = month;
            Day = day;
            Hour = hour;
            Minute = minute;
            BFlag = bFlag;
        }
        
        /// <summary>
        /// 从原始数据创建删除日期
        /// </summary>
        public DeletedDateFixed(uint rawData)
        {
            _data = rawData;
        }
        
        /// <summary>
        /// 转换为DateTime（如果可能）
        /// </summary>
        public DateTime? ToDateTime()
        {
            try
            {
                int year = (int)Year;
                int month = (int)Month;
                int day = (int)Day;
                int hour = (int)Hour;
                int minute = (int)Minute;
                
                // 添加基础年份（假设是2000年之后）
                if (year < 100) year += 2000;
                
                return new DateTime(year, month, day, hour, minute, 0);
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// 从DateTime创建
        /// </summary>
        public static DeletedDateFixed FromDateTime(DateTime dateTime, bool bFlag = false)
        {
            uint year = (uint)(dateTime.Year % 4096); // 确保在0-4095范围内
            uint month = (uint)dateTime.Month;
            uint day = (uint)dateTime.Day;
            uint hour = (uint)dateTime.Hour;
            uint minute = (uint)dateTime.Minute;
            
            return new DeletedDateFixed(year, month, day, hour, minute, bFlag);
        }
        
        /// <summary>
        /// 验证位字段范围
        /// </summary>
        public bool Validate()
        {
            return Year <= YEAR_MASK &&
                   Month <= MONTH_MASK &&
                   Day <= DAY_MASK &&
                   Hour <= HOUR_MASK &&
                   Minute <= MINUTE_MASK;
        }
        
        /// <summary>
        /// 转换为字符串表示
        /// </summary>
        public override string ToString()
        {
            return $"{Year:0000}-{Month:00}-{Day:00} {Hour:00}:{Minute:00} (BFlag={BFlag})";
        }
    }
    
    /// <summary>
    /// 升级添加掩码结构
    /// 使用位字段存储升级信息
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct UpgradeAddMaskFixed
    {
        // 整个结构作为DWORD
        [FieldOffset(0)]
        private uint _dwValue;
        
        // 作为两个WORD
        [FieldOffset(0)]
        public ushort wAddMask;
        
        [FieldOffset(2)]
        public ushort wItemLimit;
        
        // 位字段定义
        // addtype1: 3位 (0-7)
        private const int ADDTYPE1_OFFSET = 0;
        private const uint ADDTYPE1_MASK = 0x7u;
        
        // addtype2: 3位 (0-7)
        private const int ADDTYPE2_OFFSET = 3;
        private const uint ADDTYPE2_MASK = 0x7u;
        
        // addvalue1: 2位 (0-3)
        private const int ADDVALUE1_OFFSET = 6;
        private const uint ADDVALUE1_MASK = 0x3u;
        
        // addvalue2: 2位 (0-3)
        private const int ADDVALUE2_OFFSET = 8;
        private const uint ADDVALUE2_MASK = 0x3u;
        
        // adddura: 2位 (0-3)
        private const int ADDDURA_OFFSET = 10;
        private const uint ADDDURA_MASK = 0x3u;
        
        // badddura: 1位 (0-1)
        private const int BADDDURA_OFFSET = 12;
        private const uint BADDDURA_MASK = 0x1u;
        
        // flag: 3位 (0-7)
        private const int FLAG_OFFSET = 13;
        private const uint FLAG_MASK = 0x7u;
        
        // left: 16位 (剩余位)
        private const int LEFT_OFFSET = 16;
        private const uint LEFT_MASK = 0xFFFFu;
        
        /// <summary>
        /// 添加类型1
        /// </summary>
        public uint AddType1
        {
            get => BitFieldHelper.ExtractBits(_dwValue, ADDTYPE1_OFFSET, ADDTYPE1_MASK);
            set => _dwValue = BitFieldHelper.SetBits(_dwValue, value, ADDTYPE1_OFFSET, ADDTYPE1_MASK);
        }
        
        /// <summary>
        /// 添加类型2
        /// </summary>
        public uint AddType2
        {
            get => BitFieldHelper.ExtractBits(_dwValue, ADDTYPE2_OFFSET, ADDTYPE2_MASK);
            set => _dwValue = BitFieldHelper.SetBits(_dwValue, value, ADDTYPE2_OFFSET, ADDTYPE2_MASK);
        }
        
        /// <summary>
        /// 添加值1
        /// </summary>
        public uint AddValue1
        {
            get => BitFieldHelper.ExtractBits(_dwValue, ADDVALUE1_OFFSET, ADDVALUE1_MASK);
            set => _dwValue = BitFieldHelper.SetBits(_dwValue, value, ADDVALUE1_OFFSET, ADDVALUE1_MASK);
        }
        
        /// <summary>
        /// 添加值2
        /// </summary>
        public uint AddValue2
        {
            get => BitFieldHelper.ExtractBits(_dwValue, ADDVALUE2_OFFSET, ADDVALUE2_MASK);
            set => _dwValue = BitFieldHelper.SetBits(_dwValue, value, ADDVALUE2_OFFSET, ADDVALUE2_MASK);
        }
        
        /// <summary>
        /// 添加耐久
        /// </summary>
        public uint AddDura
        {
            get => BitFieldHelper.ExtractBits(_dwValue, ADDDURA_OFFSET, ADDDURA_MASK);
            set => _dwValue = BitFieldHelper.SetBits(_dwValue, value, ADDDURA_OFFSET, ADDDURA_MASK);
        }
        
        /// <summary>
        /// 是否添加耐久标志
        /// </summary>
        public bool BAddDura
        {
            get => BitFieldHelper.ExtractBits(_dwValue, BADDDURA_OFFSET, BADDDURA_MASK) == 1;
            set => _dwValue = BitFieldHelper.SetBits(_dwValue, value ? 1u : 0u, BADDDURA_OFFSET, BADDDURA_MASK);
        }
        
        /// <summary>
        /// 标志位
        /// </summary>
        public uint Flag
        {
            get => BitFieldHelper.ExtractBits(_dwValue, FLAG_OFFSET, FLAG_MASK);
            set => _dwValue = BitFieldHelper.SetBits(_dwValue, value, FLAG_OFFSET, FLAG_MASK);
        }
        
        /// <summary>
        /// 剩余位
        /// </summary>
        public uint Left
        {
            get => BitFieldHelper.ExtractBits(_dwValue, LEFT_OFFSET, LEFT_MASK);
            set => _dwValue = BitFieldHelper.SetBits(_dwValue, value, LEFT_OFFSET, LEFT_MASK);
        }
        
        /// <summary>
        /// 默认构造函数
        /// </summary>
        public UpgradeAddMaskFixed()
        {
            _dwValue = 0;
            wAddMask = 0;
            wItemLimit = 0;
        }
        
        /// <summary>
        /// 从原始值创建
        /// </summary>
        public UpgradeAddMaskFixed(uint dwValue)
        {
            _dwValue = dwValue;
            wAddMask = (ushort)(dwValue & 0xFFFF);
            wItemLimit = (ushort)(dwValue >> 16);
        }
        
        /// <summary>
        /// 验证位字段范围
        /// </summary>
        public bool Validate()
        {
            return AddType1 <= ADDTYPE1_MASK &&
                   AddType2 <= ADDTYPE2_MASK &&
                   AddValue1 <= ADDVALUE1_MASK &&
                   AddValue2 <= ADDVALUE2_MASK &&
                   AddDura <= ADDDURA_MASK &&
                   Flag <= FLAG_MASK;
        }
        
        /// <summary>
        /// 转换为字符串表示
        /// </summary>
        public override string ToString()
        {
            return $"AddType1={AddType1}, AddType2={AddType2}, AddValue1={AddValue1}, AddValue2={AddValue2}, " +
                   $"AddDura={AddDura}, BAddDura={BAddDura}, Flag={Flag}, Left={Left}";
        }
    }
    
    /// <summary>
    /// 服务器标识结构
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct ServerIdFixed
    {
        [FieldOffset(0)]
        public uint dwId;
        
        [FieldOffset(0)]
        public byte bType;      // 服务器类型
        
        [FieldOffset(1)]
        public byte bGroup;     // 服务器组
        
        [FieldOffset(2)]
        public byte bId;        // 服务器唯一标识
        
        [FieldOffset(3)]
        public byte bIndex;     // 服务器在服务器中心的注册顺序
        
        /// <summary>
        /// 默认构造函数
        /// </summary>
        public ServerIdFixed()
        {
            dwId = 0;
            bType = 0;
            bGroup = 0;
            bId = 0;
            bIndex = 0;
        }
        
        /// <summary>
        /// 从组件创建
        /// </summary>
        public ServerIdFixed(byte type, byte group, byte id, byte index)
        {
            dwId = 0;
            bType = type;
            bGroup = group;
            bId = id;
            bIndex = index;
        }
        
        /// <summary>
        /// 从DWORD创建
        /// </summary>
        public ServerIdFixed(uint id)
        {
            bType = 0;
            bGroup = 0;
            bId = 0;
            bIndex = 0;
            dwId = id;
        }
        
        /// <summary>
        /// 验证服务器ID
        /// </summary>
        public bool Validate()
        {
            // 这里可以添加验证逻辑
            return true;
        }
        
        /// <summary>
        /// 转换为字符串表示
        /// </summary>
        public override string ToString()
        {
            return $"Type={bType}, Group={bGroup}, Id={bId}, Index={bIndex} (dwId=0x{dwId:X8})";
        }
    }
    
    /// <summary>
    /// 位字段测试工具
    /// </summary>
    public static class BitFieldTester
    {
        /// <summary>
        /// 测试所有位字段结构
        /// </summary>
        public static void TestAll()
        {
            Console.WriteLine("=== 位字段结构测试 ===");
            
            TestDeletedDate();
            TestUpgradeAddMask();
            TestServerId();
            TestBitFieldHelper();
        }
        
        /// <summary>
        /// 测试删除日期结构
        /// </summary>
        private static void TestDeletedDate()
        {
            Console.WriteLine("\n--- 测试删除日期结构 ---");
            
            // 测试1: 创建和设置
            var date = new DeletedDateFixed(2025, 12, 17, 22, 53, true);
            Console.WriteLine($"创建日期: {date}");
            Console.WriteLine($"原始数据: 0x{date.RawData:X8}");
            Console.WriteLine($"验证: {date.Validate()}");
            
            // 测试2: 从原始数据创建
            uint rawData = 0x8C2B5A1F; // 示例数据
            var date2 = new DeletedDateFixed(rawData);
            Console.WriteLine($"\n从原始数据创建: 0x{rawData:X8}");
            Console.WriteLine($"解析结果: {date2}");
            
            // 测试3: 转换为DateTime
            var dateTime = date.ToDateTime();
            Console.WriteLine($"\n转换为DateTime: {dateTime}");
            
            // 测试4: 从DateTime创建
            var now = DateTime.Now;
            var date3 = DeletedDateFixed.FromDateTime(now, true);
            Console.WriteLine($"\n从DateTime创建: {now}");
            Console.WriteLine($"创建结果: {date3}");
        }
        
        /// <summary>
        /// 测试升级添加掩码结构
        /// </summary>
        private static void TestUpgradeAddMask()
        {
            Console.WriteLine("\n--- 测试升级添加掩码结构 ---");
            
            // 测试1: 创建和设置
            var mask = new UpgradeAddMaskFixed();
            mask.AddType1 = 3;
            mask.AddType2 = 5;
            mask.AddValue1 = 2;
            mask.AddValue2 = 1;
            mask.AddDura = 3;
            mask.BAddDura = true;
            mask.Flag = 4;
            mask.Left = 0x1234;
            
            Console.WriteLine($"创建掩码: {mask}");
            Console.WriteLine($"wAddMask: 0x{mask.wAddMask:X4}");
            Console.WriteLine($"wItemLimit: 0x{mask.wItemLimit:X4}");
            Console.WriteLine($"验证: {mask.Validate()}");
            
            // 测试2: 从原始值创建
            uint rawValue = 0x87654321;
            var mask2 = new UpgradeAddMaskFixed(rawValue);
            Console.WriteLine($"\n从原始值创建: 0x{rawValue:X8}");
            Console.WriteLine($"解析结果: {mask2}");
        }
        
        /// <summary>
        /// 测试服务器ID结构
        /// </summary>
        private static void TestServerId()
        {
            Console.WriteLine("\n--- 测试服务器ID结构 ---");
            
            // 测试1: 从组件创建
            var serverId1 = new ServerIdFixed(1, 2, 3, 4);
            Console.WriteLine($"从组件创建: {serverId1}");
            
            // 测试2: 从DWORD创建
            uint id = 0x04030201;
            var serverId2 = new ServerIdFixed(id);
            Console.WriteLine($"从DWORD创建 (0x{id:X8}): {serverId2}");
            
            // 测试3: 修改组件
            serverId2.bType = 10;
            serverId2.bGroup = 20;
            Console.WriteLine($"修改后: {serverId2}");
            Console.WriteLine($"新的dwId: 0x{serverId2.dwId:X8}");
        }
        
        /// <summary>
        /// 测试位字段帮助类
        /// </summary>
        private static void TestBitFieldHelper()
        {
            Console.WriteLine("\n--- 测试位字段帮助类 ---");
            
            // 测试提取位字段
            uint value = 0x12345678;
            uint extracted = BitFieldHelper.ExtractBits(value, 8, 0xFF);
            Console.WriteLine($"提取位字段: 0x{value:X8} >> 8 & 0xFF = 0x{extracted:X2}");
            
            // 测试设置位字段
            uint modified = BitFieldHelper.SetBits(value, 0xAA, 16, 0xFF);
            Console.WriteLine($"设置位字段: 0x{value:X8} 设置16-23位为0xAA = 0x{modified:X8}");
            
            // 测试创建掩码
            uint mask5 = BitFieldHelper.CreateMask(5);
            uint mask12 = BitFieldHelper.CreateMask(12);
            Console.WriteLine($"创建5位掩码: 0x{mask5:X8} ({mask5})");
            Console.WriteLine($"创建12位掩码: 0x{mask12:X8} ({mask12})");
        }
        
        /// <summary>
        /// 运行所有测试
        /// </summary>
        public static void RunAllTests()
        {
            try
            {
                TestAll();
                Console.WriteLine("\n=== 所有测试完成 ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n=== 测试失败: {ex.Message} ===");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
            }
        }
    }
}
