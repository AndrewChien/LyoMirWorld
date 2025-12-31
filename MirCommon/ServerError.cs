namespace MirCommon
{
    /// <summary>
    /// 服务器错误码
    /// </summary>
    public enum SERVER_ERROR
    {
        /// <summary>
        /// 成功
        /// </summary>
        SE_OK = 0,
        
        /// <summary>
        /// 失败
        /// </summary>
        SE_FAIL = 1,
        
        /// <summary>
        /// 内存分配失败
        /// </summary>
        SE_ALLOCMEMORYFAIL = 2,
        
        /// <summary>
        /// 没有更多数据
        /// </summary>
        SE_DB_NOMOREDATA = 3,
        
        /// <summary>
        /// 数据库未初始化
        /// </summary>
        SE_DB_NOTINITED = 4,
        
        /// <summary>
        /// 账号已存在
        /// </summary>
        SE_LOGIN_ACCOUNTEXIST = 100,
        
        /// <summary>
        /// 账号不存在
        /// </summary>
        SE_LOGIN_ACCOUNTNOTEXIST = 101,
        
        /// <summary>
        /// 密码错误
        /// </summary>
        SE_LOGIN_PASSWORDERROR = 102,
        
        /// <summary>
        /// 角色已存在
        /// </summary>
        SE_SELCHAR_CHAREXIST = 200,
        
        /// <summary>
        /// 角色不存在
        /// </summary>
        SE_SELCHAR_NOTEXIST = 201,
        
        /// <summary>
        /// 无效的账号
        /// </summary>
        SE_REG_INVALIDACCOUNT = 300,
        
        /// <summary>
        /// 无效的密码
        /// </summary>
        SE_REG_INVALIDPASSWORD = 301,
        
        /// <summary>
        /// 无效的名字
        /// </summary>
        SE_REG_INVALIDNAME = 302,
        
        /// <summary>
        /// 无效的生日
        /// </summary>
        SE_REG_INVALIDBIRTHDAY = 303,
        
        /// <summary>
        /// 无效的电话号码
        /// </summary>
        SE_REG_INVALIDPHONENUMBER = 304,
        
        /// <summary>
        /// 无效的手机号码
        /// </summary>
        SE_REG_INVALIDMOBILEPHONE = 305,
        
        /// <summary>
        /// 无效的问题
        /// </summary>
        SE_REG_INVALIDQUESTION = 306,
        
        /// <summary>
        /// 无效的答案
        /// </summary>
        SE_REG_INVALIDANSWER = 307,
        
        /// <summary>
        /// 无效的身份证
        /// </summary>
        SE_REG_INVALIDIDCARD = 308,
        
        /// <summary>
        /// 无效的邮箱
        /// </summary>
        SE_REG_INVALIDEMAIL = 309,
        
        /// <summary>
        /// 无效的角色名
        /// </summary>
        SE_CREATECHARACTER_INVALID_CHARNAME = 400,
        
        /// <summary>
        /// 数据库连接失败
        /// </summary>
        SE_ODBC_SQLCONNECTFAIL = 500,
        
        /// <summary>
        /// SQL执行失败
        /// </summary>
        SE_ODBC_SQLEXECDIRECTFAIL = 501,
    }
}
