using System;
using System.Runtime.InteropServices;

namespace MirCommon
{
    /// <summary>
    /// 锁定的消息结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct LMirMsg
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool bUnCodedMsg;
        public int size;
        public MirMsg msg;
        
        public LMirMsg()
        {
            bUnCodedMsg = false;
            size = 0;
            msg = new MirMsg();
        }
    }
    
    /// <summary>
    /// 私有商店头部
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct PrivateShopHeader
    {
        public ushort w1;
        public byte w2;
        public byte btFlag;
        public uint dw1;
        public ushort wCount;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 52)]
        public string szName;
        
        public PrivateShopHeader()
        {
            w1 = 0;
            w2 = 0;
            btFlag = 0;
            dw1 = 0;
            wCount = 0;
            szName = string.Empty;
        }
    }
    
    /// <summary>
    /// 私有商店显示
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PrivateShopShow
    {
        public PrivateShopHeader header;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
        public ItemClient[] items;
        
        public PrivateShopShow()
        {
            header = new PrivateShopHeader();
            items = new ItemClient[10];
            for (int i = 0; i < 10; i++)
            {
                items[i] = new ItemClient();
            }
        }
    }
    
    /// <summary>
    /// 私有商店物品查询
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PrivateShopItemQuery
    {
        public uint dwMakeIndex;
        public uint dwPrice;
        public ushort wPriceType;
        
        public PrivateShopItemQuery()
        {
            dwMakeIndex = 0;
            dwPrice = 0;
            wPriceType = 0;
        }
    }
    
    /// <summary>
    /// 私有商店查询
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct PrivateShopQuery
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 52)]
        public string szName;
        public PrivateShopItemQuery item;
        
        public PrivateShopQuery()
        {
            szName = string.Empty;
            item = new PrivateShopItemQuery();
        }
    }
    
    /// <summary>
    /// 装备结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Equipment
    {
        public ushort pos;
        public ItemClient item;
        
        public Equipment()
        {
            pos = 0;
            item = new ItemClient();
        }
    }
    
    /// <summary>
    /// 用于内存中的物品位置表示，不用于数据库持久化
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DBItemPos
    {
        public Item item;
        public ushort pos;
        public byte btFlag;
        
        public DBItemPos()
        {
            item = new Item();
            pos = 0;
            btFlag = 0;
        }
    }
    
    /// <summary>
    /// 玩家结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct PlayerStruct
    {
        public HumanProp prop;
        public uint dwGold;
        public uint dwSuperGold;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MirDefine.ALLBAGSIZE)]
        public Item[] bagitems;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MirDefine.MAXEQUIPMENTPOS)]
        public Item[] equipments;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szName;
        public byte btState;
        public ushort x;
        public ushort y;
        public byte btDir;
        
        public PlayerStruct()
        {
            prop = new HumanProp();
            dwGold = 0;
            dwSuperGold = 0;
            bagitems = new Item[MirDefine.ALLBAGSIZE];
            for (int i = 0; i < MirDefine.ALLBAGSIZE; i++)
            {
                bagitems[i] = new Item();
            }
            equipments = new Item[MirDefine.MAXEQUIPMENTPOS];
            for (int i = 0; i < MirDefine.MAXEQUIPMENTPOS; i++)
            {
                equipments[i] = new Item();
            }
            szName = string.Empty;
            btState = 0;
            x = 0;
            y = 0;
            btDir = 0;
        }
    }
    
    /// <summary>
    /// 其他玩家结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct OtherPlayer
    {
        public ushort x;
        public ushort y;
        public byte btDir;
        public byte btState;
        public ushort wNouse;
        public uint outview;
        public uint feather;
        public ushort wCurHp;
        public ushort wMaxHp;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szName;
        public uint dwListId;
        public uint dwGameId;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bDead;
        
        public OtherPlayer()
        {
            x = 0;
            y = 0;
            btDir = 0;
            btState = 0;
            wNouse = 0;
            outview = 0;
            feather = 0;
            wCurHp = 0;
            wMaxHp = 0;
            szName = string.Empty;
            dwListId = 0;
            dwGameId = 0;
            bDead = false;
        }
    }
    
    /// <summary>
    /// 注册账号结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct RegisterAccount
    {
        public byte btAccount;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 10)]
        public string szAccount;
        public byte btPassword;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 10)]
        public string szPassword;
        public byte btName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
        public string szName;
        public byte btIdCard;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 19)]
        public string szIdCard;
        public byte btPhoneNumber;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string szPhoneNumber;
        public byte btQ1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
        public string szQ1;
        public byte btA1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
        public string szA1;
        public byte btEmail;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 40)]
        public string szEmail;
        public byte btQ2;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
        public string szQ2;
        public byte btA2;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
        public string szA2;
        public byte btBirthday;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 10)]
        public string szBirthday;
        public byte btMobileNumber;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 11)]
        public string szMobileNumber;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 85)]
        public byte[] szUnknown;
        
        public RegisterAccount()
        {
            btAccount = 0;
            szAccount = string.Empty;
            btPassword = 0;
            szPassword = string.Empty;
            btName = 0;
            szName = string.Empty;
            btIdCard = 0;
            szIdCard = string.Empty;
            btPhoneNumber = 0;
            szPhoneNumber = string.Empty;
            btQ1 = 0;
            szQ1 = string.Empty;
            btA1 = 0;
            szA1 = string.Empty;
            btEmail = 0;
            szEmail = string.Empty;
            btQ2 = 0;
            szQ2 = string.Empty;
            btA2 = 0;
            szA2 = string.Empty;
            btBirthday = 0;
            szBirthday = string.Empty;
            btMobileNumber = 0;
            szMobileNumber = string.Empty;
            szUnknown = new byte[85];
        }
    }
    
    /// <summary>
    /// 注册账号索引
    /// </summary>
    public enum RegisterAccountIndex
    {
        RAI_ACCOUNT = 0,
        RAI_PASSWORD = 11,
        RAI_NAME = 22,
        RAI_IDCARD = 43,
        RAI_PHONENUMBER = 63,
        RAI_Q1 = 78,
        RAI_A1 = 99,
        RAI_MAIL = 120,
        RAI_Q2 = 161,
        RAI_A2 = 182,
        RAI_BIRTHDAY = 203,
        RAI_MOBILEPHONENUMBER = 214,
        RAI_UNKNOWN = 226,
    }
    
    /// <summary>
    /// 创建角色描述
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct CreateCharDesc
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
        
        public CreateCharDesc()
        {
            dwKey = 0;
            szName = string.Empty;
            szServer = string.Empty;
            szAccount = string.Empty;
            btClass = 0;
            btSex = 0;
            btHair = 0;
            btLevel = 0;
        }
    }
    
    /// <summary>
    /// 特征结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Feather
    {
        public byte btRace;
        public byte btWeapon;
        public byte btHair;
        public byte btDress;
        
        public Feather()
        {
            btRace = 0;
            btWeapon = 0;
            btHair = 0;
            btDress = 0;
        }
    }
    
    /// <summary>
    /// 物品数据库标志
    /// </summary>
    public enum ItemDbFlag
    {
        IDF_GROUND,
        IDF_BAG,
        IDF_EQUIPMENT,
        IDF_NPC,
        IDF_BANK,
        IDF_CACHE,
        IDF_PETBANK,
        IDF_UPGRADE,
    }
    
    /// <summary>
    /// 升级添加掩码
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct UpgradeAddMask
    {
        public ushort wAddMask;
        public ushort wItemLimit;
        
        public UpgradeAddMask()
        {
            wAddMask = 0;
            wItemLimit = 0;
        }
    }
    
    /// <summary>
    /// 属性索引
    /// </summary>
    public enum PropIndex
    {
        PI_MINAC,
        PI_MAXAC,
        PI_MINMAC,
        PI_MAXMAC,
        PI_MINDC,
        PI_MAXDC,
        PI_MINMC,
        PI_MAXMC,
        PI_MINSC,
        PI_MAXSC,
        PI_HITRATE,
        PI_ESCAPE,
        PI_MAGESCAPE,
        PI_POISONESCAPE,
        PI_ATTACKSPEED,
        PI_LUCKY,
        PI_DAWN,
        PI_HPRECOVER,
        PI_MPRECOVER,
        PI_POISONRECOVER,
        PI_HARD,
        PI_HOLLY,
        PI_LEVEL,
        PI_CURBAGWEIGHT,
        PI_MAXBAGWEIGHT,
        PI_CURHANDWEIGHT,
        PI_MAXHANDWEIGHT,
        PI_CURBODYWEIGHT,
        PI_MAXBODYWEIGHT,
        PI_CURHP,
        PI_CURMP,
        PI_MAXHP,
        PI_MAXMP,
        PI_EXP,
        PI_PROP_COUNT,
    }
    
    /// <summary>
    /// 物品需求类型
    /// </summary>
    public enum ItemNeedType
    {
        INT_LEVEL,
        INT_DC,
        INT_MC,
        INT_SC,
        INT_PKVALUE,
        INT_CREDIT,
        INT_SABUKOWNER,
    }
    
    /// <summary>
    /// 创建物品结构
    /// 用于DM_CREATEITEM消息
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CREATEITEM
    {
        public uint dwClientKey;     // 客户端密钥
        public Item item;            // 物品
        public ushort wPos;          // 位置
        public byte btFlag;          // 标志
        
        public CREATEITEM()
        {
            dwClientKey = 0;
            item = new Item();
            wPos = 0;
            btFlag = 0;
        }
    }
    
    /// <summary>
    /// 客户端物品结构
    /// 用于发送给客户端的物品信息
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ITEMCLIENT
    {
        public BaseItem baseitem;    // 基础物品信息
        public uint dwMakeIndex;     // 制造索引
        public ushort wCurDura;      // 当前耐久
        public ushort wMaxDura;      // 最大耐久
        
        public ITEMCLIENT()
        {
            baseitem = new BaseItem();
            dwMakeIndex = 0;
            wCurDura = 0;
            wMaxDura = 0;
        }
    }
    
    /// <summary>
    /// 创建玩家描述结构
    /// 用于创建玩家对象
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CREATEHUMANDESC
    {
        public Database.CHARDBINFO dbinfo;    // 角色数据库信息
        public IntPtr pClientObj;    // 客户端对象指针
        
        public CREATEHUMANDESC()
        {
            dbinfo = new Database.CHARDBINFO();
            pClientObj = IntPtr.Zero;
        }
    }
    
    /// <summary>
    /// 装备结构
    /// 用于发送装备信息
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct EQUIPMENT
    {
        public ushort pos;           // 装备位置
        public ITEMCLIENT item;      // 物品信息
        
        public EQUIPMENT()
        {
            pos = 0;
            item = new ITEMCLIENT();
        }
    }
    
    /// <summary>
    /// 背包物品位置结构
    /// 用于发送背包物品位置信息
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BAGITEMPOS
    {
        public uint ItemId;          // 物品ID
        public ushort wPos;          // 位置
        
        public BAGITEMPOS()
        {
            ItemId = 0;
            wPos = 0;
        }
    }

    /// <summary>
    /// 数据库错误码
    /// </summary>
    public enum DbError
    {
        SE_OK = 0,                  // 成功
        SE_FAIL = 1,                // 失败
        SE_ALLOCMEMORYFAIL = 2,     // 内存分配失败
        SE_DB_NOMOREDATA = 3,       // 没有更多数据
        SE_DB_NOTINITED = 4,        // 数据库未初始化
        SE_LOGIN_ACCOUNTEXIST = 100, // 账号已存在
        SE_LOGIN_ACCOUNTNOTEXIST = 101, // 账号不存在
        SE_LOGIN_PASSWORDERROR = 102, // 密码错误
        SE_SELCHAR_CHAREXIST = 200, // 角色已存在
        SE_SELCHAR_NOTEXIST = 201,  // 角色不存在
        SE_REG_INVALIDACCOUNT = 300, // 无效的账号
        SE_REG_INVALIDPASSWORD = 301, // 无效的密码
        SE_REG_INVALIDNAME = 302,   // 无效的名字
        SE_REG_INVALIDBIRTHDAY = 303, // 无效的生日
        SE_REG_INVALIDPHONENUMBER = 304, // 无效的电话号码
        SE_REG_INVALIDMOBILEPHONE = 305, // 无效的手机号码
        SE_REG_INVALIDQUESTION = 306, // 无效的问题
        SE_REG_INVALIDANSWER = 307,  // 无效的答案
        SE_REG_INVALIDIDCARD = 308,  // 无效的身份证
        SE_REG_INVALIDEMAIL = 309,   // 无效的邮箱
        SE_CREATECHARACTER_INVALID_CHARNAME = 400, // 无效的角色名
        SE_ODBC_SQLCONNECTFAIL = 500, // 数据库连接失败
        SE_ODBC_SQLEXECDIRECTFAIL = 501, // SQL执行失败
    }

    /// <summary>
    /// 列类型
    /// </summary>
    public enum eColType
    {
        CT_STRING,      // 字符串
        CT_TINYINT,     // 8位整数
        CT_SMALLINT,    // 16位整数
        CT_INTEGER,     // 32位整数
        CT_BIGINT,      // 64位整数
        CT_DATETIME,    // 时间
        CT_CODEDARRAY,  // 编码存的数据
    }

    /// <summary>
    /// 数据库物品操作
    /// </summary>
    public enum dbitemoperation
    {
        DIO_DELETE,
        DIO_UPDATEPOS,
        DIO_UPDATEOWNER,
        DIO_UPDATEDURA
    }

    /// <summary>
    /// 服务器中心消息
    /// </summary>
    public enum scmsg
    {
        SCM_START,
        // 注册服务器
        // send...
        // dwFlag = id( server = 0 other id != 0 )
        // data = REGISTER_SERVER_INFO
        // recv...
        // dwFlag = id
        // w1 = success?
        // w2 = reason
        // data = REGISTER_SERVER_RESULT
        SCM_REGISTERSERVER,
        // 取得选人服务器地址
        // send...
        // data = loginid/servername
        // recv...
        // w1 = success?
        // w2 = reason
        // data = ip/port/selectid
        SCM_GETSELCHARSERVERADDR,
        // 取得游戏世界服务器地址
        // send...
        // mapname/x/y/servername
        // recv
        // w1 = success?
        // w2 = reason
        // data = ip/port
        SCM_GETGAMESERVERADDR,
        // 更新服务器信息
        // send...
        // w1 = connections
        // dwFlag = float loop time
        // every one second
        SCM_UPDATESERVERINFO,
        // 取得服务器地址
        // send...
        // w1 = type
        // data = name
        // recv
        // w1 = success?reason
        // data = SERVERADDR
        SCM_FINDSERVER,
        // 发送服务器间消息
        // send...
        // dwflag = 0
        // w1 = cmd
        // w2 = sendtype
        // w3 = sendparam
        // data = data
        // recv...
        // dwflag = 0
        // w1 = cmd
        // w2 = sendservertype
        // w3 = sendserverindex
        // data = data
        SCM_MSGACROSSSERVER,
    }

    /// <summary>
    /// 跨服务器消息
    /// </summary>
    public enum MSG_ACROSS_SERVER
    {
        // 踢掉某人
        // 让数据库里的标记设置成不在线
        // data = account
        MAS_KICKCONNECTION,
        // 登陆服务器请求进入选人服务器
        // send...
        // data = loginid/account
        // recv...
        // data = selcharid
        MAS_ENTERSELCHARSERVER,
        // 进入游戏世界服务器
        // send...
        // data = ENTERGAMESERVER
        // recv...
        // data = fail or success
        MAS_ENTERGAMESERVER,
        // send...
        // data = LID/ACCOUNT/SID
        // recv
        // data = 
        MAS_RESTARTGAME,
    }
}
