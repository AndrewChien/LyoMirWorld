using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MirCommon.Database
{
    /// <summary>
    /// 创建角色描述结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct CREATECHARDESC
    {
        public uint dwKey;
        
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szName;
        
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szServer;
        
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string szAccount;
        
        public byte btClass;
        public byte btSex;
        public byte btHair;
        public byte btLevel;
        
        public CREATECHARDESC(uint key, string account, string server, string name, byte job, byte sex, byte hair, byte level)
        {
            dwKey = key;
            szAccount = account;
            szServer = server;
            szName = name;
            btClass = job;
            btSex = sex;
            btHair = hair;
            btLevel = level;
        }
    }

    /// <summary>
    /// 查询角色列表结果
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct tQueryCharList_Result
    {
        public int count;
        public IntPtr pData; // 指向SelectCharList数组的指针
        
        public SelectCharList[] GetCharacters()
        {
            if (count == 0 || pData == IntPtr.Zero)
                return Array.Empty<SelectCharList>();
                
            var result = new SelectCharList[count];
            int size = Marshal.SizeOf<SelectCharList>();
            
            for (int i = 0; i < count; i++)
            {
                IntPtr ptr = new IntPtr(pData.ToInt64() + i * size);
                result[i] = Marshal.PtrToStructure<SelectCharList>(ptr);
            }
            
            return result;
        }
    }

    /// <summary>
    /// 查询地图位置结果
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct tQueryMapPosition_Result
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
        public string szMapName;
        
        public short x;
        public short y;
    }

    /// <summary>
    /// 角色数据库信息结构
    /// typedef struct tagCHARDBINFO
    /// {
    ///     DWORD   dwClientKey;    // 第一个字段！
    ///     char    szName[20];
    ///     DWORD   dwDBId;
    ///     DWORD   mapid;
    ///     WORD    x;
    ///     WORD    y;
    ///     DWORD   dwGold;
    ///     DWORD   dwYuanbao;
    ///     DWORD   dwCurExp;
    ///     WORD    wLevel;
    ///     BYTE    btClass;
    ///     BYTE    btHair;
    ///     BYTE    btSex;
    ///     BYTE    flag;
    ///     WORD    hp;
    ///     WORD    mp;
    ///     WORD    maxhp;
    ///     WORD    maxmp;
    ///     BYTE    mindc;
    ///     BYTE    maxdc;
    ///     BYTE    minmc;
    ///     BYTE    maxmc;
    ///     BYTE    minsc;
    ///     BYTE    maxsc;
    ///     BYTE    minac;
    ///     BYTE    maxac;
    ///     BYTE    minmac;
    ///     BYTE    maxmac;
    ///     WORD    weight;
    ///     BYTE    handweight;
    ///     BYTE    bodyweight;
    ///     DWORD   dwForgePoint;
    ///     DWORD   dwProp[8];
    ///     DWORD   dwFlag[4];
    ///     char    szStartPoint[40];
    ///     char    szGuildName[32];
    /// }CHARDBINFO;
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct CHARDBINFO
    {
        public uint dwClientKey;         // 客户端Key
        
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
        public string szName;            // 角色名
        
        public uint dwDBId;              // 数据库ID
        public uint mapid;               // 地图ID
        public ushort x;                 // X坐标
        public ushort y;                 // Y坐标
        public uint dwGold;              // 金币
        public uint dwYuanbao;           // 元宝
        public uint dwCurExp;            // 当前经验
        public ushort wLevel;            // 等级
        public byte btClass;             // 职业
        public byte btHair;              // 发型
        public byte btSex;               // 性别
        public byte flag;                // 标志
        
        public ushort hp;                // 当前HP
        public ushort mp;                // 当前MP
        public ushort maxhp;             // 最大HP
        public ushort maxmp;             // 最大MP
        
        public byte mindc;               // 最小物理攻击
        public byte maxdc;               // 最大物理攻击
        public byte minmc;               // 最小魔法攻击
        public byte maxmc;               // 最大魔法攻击
        public byte minsc;               // 最小道术攻击
        public byte maxsc;               // 最大道术攻击
        public byte minac;               // 最小物理防御
        public byte maxac;               // 最大物理防御
        public byte minmac;              // 最小魔法防御
        public byte maxmac;              // 最大魔法防御
        
        public ushort weight;            // 背包负重
        public byte handweight;          // 手腕负重
        public byte bodyweight;          // 身体负重
        
        public uint dwForgePoint;        // 锻造点
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public uint[] dwProp;            // 属性数组（8个）
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public uint[] dwFlag;            // 标志数组（4个）
        
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 40)]
        public string szStartPoint;      // 起始点
        
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szGuildName;       // 行会名称
        
        public static int Size => Marshal.SizeOf<CHARDBINFO>();
        
        /// <summary>
        /// 构造函数，初始化数组字段
        /// </summary>
        public CHARDBINFO()
        {
            dwProp = new uint[8];
            dwFlag = new uint[4];
            szName = "";
            szStartPoint = "";
            szGuildName = "";
        }
        
        /// <summary>
        /// 序列化为字节数组
        /// </summary>
        public byte[] ToBytes()
        {
            byte[] buffer = new byte[Size];
            IntPtr ptr = Marshal.AllocHGlobal(Size);
            
            try
            {
                Marshal.StructureToPtr(this, ptr, false);
                Marshal.Copy(ptr, buffer, 0, Size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            
            return buffer;
        }
        
        /// <summary>
        /// 从字节数组反序列化
        /// </summary>
        public static CHARDBINFO FromBytes(byte[] data)
        {
            if (data.Length < Size)
                throw new ArgumentException($"数据长度不足，需要{Size}字节，实际{data.Length}字节");
                
            IntPtr ptr = Marshal.AllocHGlobal(Size);
            
            try
            {
                Marshal.Copy(data, 0, ptr, Size);
                return Marshal.PtrToStructure<CHARDBINFO>(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }

    /// <summary>
    /// 物品数据库结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DBITEM
    {
        public Item item;                // 物品
        public ushort wPos;              // 位置
        public byte btFlag;              // 标志（背包、装备等）
        
        public static int Size => Marshal.SizeOf<DBITEM>();
        
        /// <summary>
        /// 从游戏物品结构转换
        /// </summary>
        public static DBITEM FromItem(Item item, uint ownerId, byte flag, ushort pos, uint findKey = 0)
        {
            return new DBITEM
            {
                item = item,
                wPos = pos,
                btFlag = flag
            };
        }
        
        /// <summary>
        /// 转换为游戏物品结构
        /// </summary>
        public Item ToItem()
        {
            return item;
        }
        
        /// <summary>
        /// 序列化为字节数组
        /// </summary>
        public byte[] ToBytes()
        {
            byte[] buffer = new byte[Size];
            IntPtr ptr = Marshal.AllocHGlobal(Size);
            
            try
            {
                Marshal.StructureToPtr(this, ptr, false);
                Marshal.Copy(ptr, buffer, 0, Size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            
            return buffer;
        }
        
        /// <summary>
        /// 从字节数组反序列化
        /// </summary>
        public static DBITEM FromBytes(byte[] data)
        {
            if (data.Length < Size)
                throw new ArgumentException($"数据长度不足，需要{Size}字节，实际{data.Length}字节");
                
            IntPtr ptr = Marshal.AllocHGlobal(Size);
            
            try
            {
                Marshal.Copy(data, 0, ptr, Size);
                return Marshal.PtrToStructure<DBITEM>(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }

    /// <summary>
    /// 技能数据库结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MAGICDB
    {
        public byte btUserKey;           // 用户键
        public byte btCurLevel;          // 当前等级
        public ushort wMagicId;          // 技能ID
        public uint dwCurTrain;          // 当前训练值
        
        public static int Size => Marshal.SizeOf<MAGICDB>();
        
        /// <summary>
        /// 序列化为字节数组
        /// </summary>
        public byte[] ToBytes()
        {
            byte[] buffer = new byte[Size];
            IntPtr ptr = Marshal.AllocHGlobal(Size);
            
            try
            {
                Marshal.StructureToPtr(this, ptr, false);
                Marshal.Copy(ptr, buffer, 0, Size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            
            return buffer;
        }
        
        /// <summary>
        /// 从字节数组反序列化
        /// </summary>
        public static MAGICDB FromBytes(byte[] data)
        {
            if (data.Length < Size)
                throw new ArgumentException($"数据长度不足，需要{Size}字节，实际{data.Length}字节");
                
            IntPtr ptr = Marshal.AllocHGlobal(Size);
            
            try
            {
                Marshal.Copy(data, 0, ptr, Size);
                return Marshal.PtrToStructure<MAGICDB>(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }

    /// <summary>
    /// 物品位置结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BAGITEMPOS
    {
        public uint dwItemIndex;         // 物品索引
        public byte btFlag;              // 标志
        public ushort wPos;              // 位置
        
        public static int Size => Marshal.SizeOf<BAGITEMPOS>();
    }

    /// <summary>
    /// 执行SQL记录定义
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ExecSqlRecord
    {
        public int fieldCount;           // 字段数量
        public IntPtr fieldTypes;        // 字段类型指针
        public IntPtr fieldNames;        // 字段名称指针
        
        public static int Size => Marshal.SizeOf<ExecSqlRecord>();
    }

    /// <summary>
    /// 任务信息结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TaskInfo
    {
        public uint dwOwner;             // 所有者ID
        public uint dwTaskId;            // 任务ID
        public uint dwState;             // 任务状态
        public uint dwParam1;            // 参数1
        public uint dwParam2;            // 参数2
        public uint dwParam3;            // 参数3
        public uint dwParam4;            // 参数4
        
        public static int Size => Marshal.SizeOf<TaskInfo>();
        
        /// <summary>
        /// 序列化为字节数组
        /// </summary>
        public byte[] ToBytes()
        {
            byte[] buffer = new byte[Size];
            IntPtr ptr = Marshal.AllocHGlobal(Size);
            
            try
            {
                Marshal.StructureToPtr(this, ptr, false);
                Marshal.Copy(ptr, buffer, 0, Size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            
            return buffer;
        }
        
        /// <summary>
        /// 从字节数组反序列化
        /// </summary>
        public static TaskInfo FromBytes(byte[] data)
        {
            if (data.Length < Size)
                throw new ArgumentException($"数据长度不足，需要{Size}字节，实际{data.Length}字节");
                
            IntPtr ptr = Marshal.AllocHGlobal(Size);
            
            try
            {
                Marshal.Copy(data, 0, ptr, Size);
                return Marshal.PtrToStructure<TaskInfo>(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }

    /// <summary>
    /// 数据库结构序列化工具
    /// </summary>
    public static class DatabaseSerializer
    {
        /// <summary>
        /// 序列化CHARDBINFO数组
        /// </summary>
        public static byte[] SerializeCharDbInfos(CHARDBINFO[] infos)
        {
            if (infos == null || infos.Length == 0)
                return Array.Empty<byte>();
                
            int size = CHARDBINFO.Size;
            byte[] buffer = new byte[size * infos.Length];
            
            for (int i = 0; i < infos.Length; i++)
            {
                byte[] charData = infos[i].ToBytes();
                Array.Copy(charData, 0, buffer, i * size, size);
            }
            
            return buffer;
        }
        
        /// <summary>
        /// 反序列化CHARDBINFO数组
        /// </summary>
        public static CHARDBINFO[] DeserializeCharDbInfos(byte[] data)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<CHARDBINFO>();
                
            int size = CHARDBINFO.Size;
            if (data.Length % size != 0)
                throw new ArgumentException($"数据长度必须是{size}的倍数");
                
            int count = data.Length / size;
            var result = new CHARDBINFO[count];
            
            for (int i = 0; i < count; i++)
            {
                byte[] charData = new byte[size];
                Array.Copy(data, i * size, charData, 0, size);
                result[i] = CHARDBINFO.FromBytes(charData);
            }
            
            return result;
        }
        
        /// <summary>
        /// 序列化DBITEM数组
        /// </summary>
        public static byte[] SerializeDbItems(DBITEM[] items)
        {
            if (items == null || items.Length == 0)
                return Array.Empty<byte>();
                
            int size = DBITEM.Size;
            byte[] buffer = new byte[size * items.Length];
            
            for (int i = 0; i < items.Length; i++)
            {
                byte[] itemData = items[i].ToBytes();
                Array.Copy(itemData, 0, buffer, i * size, size);
            }
            
            return buffer;
        }
        
        /// <summary>
        /// 反序列化DBITEM数组
        /// </summary>
        public static DBITEM[] DeserializeDbItems(byte[] data)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<DBITEM>();
                
            int size = DBITEM.Size;
            if (data.Length % size != 0)
                throw new ArgumentException($"数据长度必须是{size}的倍数");
                
            int count = data.Length / size;
            var result = new DBITEM[count];
            
            for (int i = 0; i < count; i++)
            {
                byte[] itemData = new byte[size];
                Array.Copy(data, i * size, itemData, 0, size);
                result[i] = DBITEM.FromBytes(itemData);
            }
            
            return result;
        }
        
        /// <summary>
        /// 序列化MAGICDB数组
        /// </summary>
        public static byte[] SerializeMagicDbs(MAGICDB[] magics)
        {
            if (magics == null || magics.Length == 0)
                return Array.Empty<byte>();
                
            int size = MAGICDB.Size;
            byte[] buffer = new byte[size * magics.Length];
            
            for (int i = 0; i < magics.Length; i++)
            {
                byte[] magicData = magics[i].ToBytes();
                Array.Copy(magicData, 0, buffer, i * size, size);
            }
            
            return buffer;
        }
        
        /// <summary>
        /// 反序列化MAGICDB数组
        /// </summary>
        public static MAGICDB[] DeserializeMagicDbs(byte[] data)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<MAGICDB>();
                
            int size = MAGICDB.Size;
            if (data.Length % size != 0)
                throw new ArgumentException($"数据长度必须是{size}的倍数");
                
            int count = data.Length / size;
            var result = new MAGICDB[count];
            
            for (int i = 0; i < count; i++)
            {
                byte[] magicData = new byte[size];
                Array.Copy(data, i * size, magicData, 0, size);
                result[i] = MAGICDB.FromBytes(magicData);
            }
            
            return result;
        }
        
        /// <summary>
        /// 序列化TaskInfo数组
        /// </summary>
        public static byte[] SerializeTaskInfos(TaskInfo[] tasks)
        {
            if (tasks == null || tasks.Length == 0)
                return Array.Empty<byte>();
                
            int size = TaskInfo.Size;
            byte[] buffer = new byte[size * tasks.Length];
            
            for (int i = 0; i < tasks.Length; i++)
            {
                byte[] taskData = tasks[i].ToBytes();
                Array.Copy(taskData, 0, buffer, i * size, size);
            }
            
            return buffer;
        }
        
        /// <summary>
        /// 反序列化TaskInfo数组
        /// </summary>
        public static TaskInfo[] DeserializeTaskInfos(byte[] data)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<TaskInfo>();
                
            int size = TaskInfo.Size;
            if (data.Length % size != 0)
                throw new ArgumentException($"数据长度必须是{size}的倍数");
                
            int count = data.Length / size;
            var result = new TaskInfo[count];
            
            for (int i = 0; i < count; i++)
            {
                byte[] taskData = new byte[size];
                Array.Copy(data, i * size, taskData, 0, size);
                result[i] = TaskInfo.FromBytes(taskData);
            }
            
            return result;
        }
    }
}
