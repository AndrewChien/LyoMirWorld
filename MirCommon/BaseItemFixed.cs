using System;
using System.Runtime.InteropServices;

namespace MirCommon
{
    /// <summary>
    /// 修复后的基础物品结构
    /// 总大小：44字节
    /// 注意：使用固定大小的字节数组，避免对象引用问题
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct BaseItemFixed
    {
        // 偏移量0: 名称长度 (1字节)
        [FieldOffset(0)]
        public byte btNameLength;
        
        // 偏移量1-14: 名称 (14字节) - 使用固定大小的字节数组
        [FieldOffset(1)]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 14)]
        public byte[] szName;
        
        // 偏移量15: 标准模式 (1字节)
        [FieldOffset(15)]
        public byte btStdMode;
        
        // 偏移量16: 形状 (1字节)
        [FieldOffset(16)]
        public byte btShape;
        
        // 偏移量17: 重量 (1字节)
        [FieldOffset(17)]
        public byte btWeight;
        
        // 偏移量18: 动画计数 (1字节) - high3bits = index, low5bits = count
        [FieldOffset(18)]
        public byte btAniCount;
        
        // 偏移量19: 特殊力量 (1字节)
        [FieldOffset(19)]
        public byte btSpecialpower;
        
        // 偏移量20-21: UNION 1 - bNeedIdentify和btPriceType (2字节)
        [FieldOffset(20)]
        public byte bNeedIdentify;
        
        [FieldOffset(21)]
        public byte btPriceType;
        
        // 偏移量20-21: UNION 1 - wMapId (2字节)
        [FieldOffset(20)]
        public ushort wMapId;
        
        // 偏移量22-23: 图像索引 (2字节)
        [FieldOffset(22)]
        public ushort wImageIndex;
        
        // 偏移量24-25: 最大耐久 (2字节)
        [FieldOffset(24)]
        public ushort wMaxDura;
        
        // 偏移量26-35: UNION 2 - 属性结构1 (10字节)
        [FieldOffset(26)]
        public byte btMinDef;
        
        [FieldOffset(27)]
        public byte btMaxDef;
        
        [FieldOffset(28)]
        public byte btMinMagDef;
        
        [FieldOffset(29)]
        public byte btMaxMagDef;
        
        [FieldOffset(30)]
        public byte btMinAtk;
        
        [FieldOffset(31)]
        public byte btMaxAtk;
        
        [FieldOffset(32)]
        public byte btMinMagAtk;
        
        [FieldOffset(33)]
        public byte btMaxMagAtk;
        
        [FieldOffset(34)]
        public byte btMinSouAtk;
        
        [FieldOffset(35)]
        public byte btMaxSouAtk;
        
        // 偏移量26-35: UNION 2 - 属性结构2 (10字节)
        [FieldOffset(26)]
        public byte Ac1;
        
        [FieldOffset(27)]
        public byte Ac2;
        
        [FieldOffset(28)]
        public byte Mac1;
        
        [FieldOffset(29)]
        public byte Mac2;
        
        [FieldOffset(30)]
        public byte Dc1;
        
        [FieldOffset(31)]
        public byte Dc2;
        
        [FieldOffset(32)]
        public byte Mc1;
        
        [FieldOffset(33)]
        public byte Mc2;
        
        [FieldOffset(34)]
        public byte Sc1;
        
        [FieldOffset(35)]
        public byte Sc2;
        
        // 偏移量26-35: UNION 2 - 属性结构3 (10字节)
        [FieldOffset(26)]
        public ushort wAc;
        
        [FieldOffset(28)]
        public ushort wMac;
        
        [FieldOffset(30)]
        public ushort wDc;
        
        [FieldOffset(32)]
        public ushort wMc;
        
        [FieldOffset(34)]
        public ushort wSc;
        
        // 偏移量36: 需求类型 (1字节)
        [FieldOffset(36)]
        public byte needtype;
        
        // 偏移量37: 需求值 (1字节)
        [FieldOffset(37)]
        public byte needvalue;
        
        // 偏移量38-39: UNION 3 - wUnknown或btFlag和btUpgradeTimes (2字节)
        [FieldOffset(38)]
        public ushort wUnknown;
        
        [FieldOffset(38)]
        public byte btFlag;
        
        [FieldOffset(39)]
        public byte btUpgradeTimes;
        
        // 偏移量40-43: 价格 (4字节)
        [FieldOffset(40)]
        public int nPrice;
        
        /// <summary>
        /// 默认构造函数
        /// </summary>
        public BaseItemFixed()
        {
            btNameLength = 0;
            szName = new byte[14];
            btStdMode = 0;
            btShape = 0;
            btWeight = 0;
            btAniCount = 0;
            btSpecialpower = 0;
            bNeedIdentify = 0;
            btPriceType = 0;
            wMapId = 0;
            wImageIndex = 0;
            wMaxDura = 0;
            btMinDef = 0;
            btMaxDef = 0;
            btMinMagDef = 0;
            btMaxMagDef = 0;
            btMinAtk = 0;
            btMaxAtk = 0;
            btMinMagAtk = 0;
            btMaxMagAtk = 0;
            btMinSouAtk = 0;
            btMaxSouAtk = 0;
            Ac1 = 0;
            Ac2 = 0;
            Mac1 = 0;
            Mac2 = 0;
            Dc1 = 0;
            Dc2 = 0;
            Mc1 = 0;
            Mc2 = 0;
            Sc1 = 0;
            Sc2 = 0;
            wAc = 0;
            wMac = 0;
            wDc = 0;
            wMc = 0;
            wSc = 0;
            needtype = 0;
            needvalue = 0;
            wUnknown = 0;
            btFlag = 0;
            btUpgradeTimes = 0;
            nPrice = 0;
        }
        
        /// <summary>
        /// 设置名称（使用GBK编码）
        /// </summary>
        public void SetName(string name)
        {
            byte[] bytes = StringEncoding.GetGBKBytes(name);
            int length = Math.Min(bytes.Length, 13); // 保留1字节给null终止符
            Array.Copy(bytes, 0, szName, 0, length);
            if (length < 14) szName[length] = 0; // null终止符
            btNameLength = (byte)length;
        }
        
        /// <summary>
        /// 获取名称（使用GBK编码）
        /// </summary>
        public string GetName()
        {
            int nullIndex = Array.IndexOf(szName, (byte)0);
            if (nullIndex < 0) nullIndex = Math.Min(szName.Length, btNameLength);
            return StringEncoding.GetGBKString(szName, 0, nullIndex);
        }
        
        /// <summary>
        /// 获取结构体大小
        /// </summary>
        public static int Size => 44;
        
        /// <summary>
        /// 验证结构体大小
        /// </summary>
        public static bool ValidateSize()
        {
            int csharpSize = Marshal.SizeOf<BaseItemFixed>();
            int cppSize = 44;
            return csharpSize == cppSize;
        }
        
        /// <summary>
        /// 验证字段偏移量
        /// </summary>
        public static bool ValidateFieldOffsets()
        {
            try
            {
                // 验证关键字段的偏移量
                int btNameLengthOffset = Marshal.OffsetOf<BaseItemFixed>("btNameLength").ToInt32();
                int szNameOffset = Marshal.OffsetOf<BaseItemFixed>("szName").ToInt32();
                int btStdModeOffset = Marshal.OffsetOf<BaseItemFixed>("btStdMode").ToInt32();
                int nPriceOffset = Marshal.OffsetOf<BaseItemFixed>("nPrice").ToInt32();
                
                return btNameLengthOffset == 0 &&
                       szNameOffset == 1 &&
                       btStdModeOffset == 15 &&
                       nPriceOffset == 40;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// 转换为字符串表示
        /// </summary>
        public override string ToString()
        {
            return $"BaseItemFixed: Name={GetName()}, StdMode={btStdMode}, Price={nPrice}";
        }
    }
    
    /// <summary>
    /// 修复后的物品结构（使用BaseItemFixed）
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ItemFixed
    {
        public BaseItemFixed baseitem;      // 44字节
        public uint dwMakeIndex;            // 48字节
        public ushort wCurDura;             // 50字节
        public ushort wMaxDura;             // 52字节
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public uint[] dwParam;              // 68字节
        
        public ItemFixed()
        {
            baseitem = new BaseItemFixed();
            dwMakeIndex = 0;
            wCurDura = 0;
            wMaxDura = 0;
            dwParam = new uint[4];
        }
        
        /// <summary>
        /// 获取结构体大小
        /// </summary>
        public static int Size => 68;
    }
    
    /// <summary>
    /// 修复后的客户端物品结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ItemClientFixed
    {
        public BaseItemFixed baseitem;      // 44字节
        public uint dwMakeIndex;            // 48字节
        public ushort wCurDura;             // 50字节
        public ushort wMaxDura;             // 52字节
        
        public ItemClientFixed()
        {
            baseitem = new BaseItemFixed();
            dwMakeIndex = 0;
            wCurDura = 0;
            wMaxDura = 0;
        }
        
        /// <summary>
        /// 获取结构体大小
        /// </summary>
        public static int Size => 52;
    }
    
    /// <summary>
    /// 数据结构验证工具
    /// </summary>
    public static class DataStructureValidator
    {
        /// <summary>
        /// 验证所有数据结构的大小
        /// </summary>
        public static void ValidateAllStructures()
        {
            Console.WriteLine("=== 数据结构大小验证 ===");
            
            // 验证BaseItemFixed
            int baseItemSize = Marshal.SizeOf<BaseItemFixed>();
            Console.WriteLine($"BaseItemFixed: C#={baseItemSize}字节, 匹配: {baseItemSize == 44}");
            
            // 验证ItemFixed
            int itemSize = Marshal.SizeOf<ItemFixed>();
            Console.WriteLine($"ItemFixed: C#={itemSize}字节, 匹配: {itemSize == 68}");
            
            // 验证ItemClientFixed
            int itemClientSize = Marshal.SizeOf<ItemClientFixed>();
            Console.WriteLine($"ItemClientFixed: C#={itemClientSize}字节, 匹配: {itemClientSize == 52}");
            
            // 验证字段偏移量
            Console.WriteLine($"\n=== 字段偏移量验证 ===");
            Console.WriteLine($"BaseItemFixed字段偏移量验证: {BaseItemFixed.ValidateFieldOffsets()}");
            
            // 验证关键字段
            ValidateKeyFields();
        }
        
        /// <summary>
        /// 验证关键字段
        /// </summary>
        private static void ValidateKeyFields()
        {
            Console.WriteLine($"\n=== 关键字段验证 ===");
            
            var item = new ItemFixed();
            item.baseitem.SetName("TestItem");
            item.baseitem.btStdMode = 5;
            item.baseitem.nPrice = 1000;
            item.dwMakeIndex = 12345;
            item.wCurDura = 50;
            item.wMaxDura = 100;
            
            Console.WriteLine($"物品名称: {item.baseitem.GetName()}");
            Console.WriteLine($"标准模式: {item.baseitem.btStdMode}");
            Console.WriteLine($"价格: {item.baseitem.nPrice}");
            Console.WriteLine($"制造索引: {item.dwMakeIndex}");
            Console.WriteLine($"耐久: {item.wCurDura}/{item.wMaxDura}");
            
            // 测试union字段
            item.baseitem.bNeedIdentify = 1;
            item.baseitem.btPriceType = 2;
            Console.WriteLine($"需要鉴定: {item.baseitem.bNeedIdentify}, 价格类型: {item.baseitem.btPriceType}");
            
            // 测试另一个union字段
            item.baseitem.wMapId = 1001;
            Console.WriteLine($"地图ID: {item.baseitem.wMapId}");
        }
        
        /// <summary>
        /// 创建测试数据
        /// </summary>
        public static byte[] CreateTestBinaryData()
        {
            var item = new ItemFixed();
            
            // 设置测试数据
            item.baseitem.SetName("TestItem");
            item.baseitem.btStdMode = 5;
            item.baseitem.btShape = 1;
            item.baseitem.btWeight = 10;
            item.baseitem.btAniCount = 3;
            item.baseitem.btSpecialpower = 0;
            item.baseitem.bNeedIdentify = 1;
            item.baseitem.btPriceType = 0;
            item.baseitem.wImageIndex = 100;
            item.baseitem.wMaxDura = 100;
            item.baseitem.btMinDef = 5;
            item.baseitem.btMaxDef = 10;
            item.baseitem.btMinMagDef = 3;
            item.baseitem.btMaxMagDef = 6;
            item.baseitem.btMinAtk = 10;
            item.baseitem.btMaxAtk = 20;
            item.baseitem.btMinMagAtk = 5;
            item.baseitem.btMaxMagAtk = 10;
            item.baseitem.btMinSouAtk = 2;
            item.baseitem.btMaxSouAtk = 4;
            item.baseitem.needtype = 0;
            item.baseitem.needvalue = 10;
            item.baseitem.btFlag = 1;
            item.baseitem.btUpgradeTimes = 0;
            item.baseitem.nPrice = 1000;
            
            item.dwMakeIndex = 123456;
            item.wCurDura = 75;
            item.wMaxDura = 100;
            item.dwParam[0] = 1;
            item.dwParam[1] = 2;
            item.dwParam[2] = 3;
            item.dwParam[3] = 4;
            
            // 转换为字节数组
            int size = Marshal.SizeOf<ItemFixed>();
            byte[] buffer = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(item, ptr, false);
                Marshal.Copy(ptr, buffer, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            
            return buffer;
        }
    }
}
