using System;
using System.Data;
using System.Text;
using MirCommon.Utils;

namespace MirCommon.Database
{
    /// <summary>
    /// 数据库基类
    /// </summary>
    public abstract class BaseDatabase : IDatabase
    {
        protected IDbConnection? _connection;
        protected readonly string _connectionString;
        
        protected BaseDatabase(string connectionString)
        {
            _connectionString = connectionString;
        }
        
        /// <summary>
        /// 创建数据库连接
        /// </summary>
        protected abstract IDbConnection CreateConnection();
        
        /// <summary>
        /// 创建参数
        /// </summary>
        protected abstract IDbDataParameter CreateParameter(string name, object value);
        
        public virtual SERVER_ERROR OpenDataBase()
        {
            try
            {
                _connection = CreateConnection();
                _connection.Open();
                return SERVER_ERROR.SE_OK;
            }
            catch (Exception)
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR CheckAccount(string account, string password)
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM TBL_ACCOUNT WHERE ACCOUNT = @account AND PASSWORD = @password";
                cmd.Parameters.Add(CreateParameter("@account", account));
                cmd.Parameters.Add(CreateParameter("@password", password));
                
                var result = cmd.ExecuteScalar();
                return (result != null && Convert.ToInt32(result) > 0) ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR CheckAccountExist(string account)
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM TBL_ACCOUNT WHERE ACCOUNT = @account";
                cmd.Parameters.Add(CreateParameter("@account", account));
                
                var result = cmd.ExecuteScalar();
                return (result != null && Convert.ToInt32(result) > 0) ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR CreateAccount(string account, string password, string name, string birthday,
                                                 string q1, string a1, string q2, string a2, string email,
                                                 string phoneNumber, string mobilePhoneNumber, string idCard)
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"INSERT INTO TBL_ACCOUNT (ACCOUNT, PASSWORD, NAME, BIRTHDAY, 
                                  Q1, A1, Q2, A2, EMAIL, PHONENUMBER, MOBILEPHONENUMBER, IDCARD)
                                  VALUES (@account, @password, @name, @birthday, @q1, @a1, @q2, @a2, @email, @phone, @mobile, @idcard)";
                
                cmd.Parameters.Add(CreateParameter("@account", account));
                cmd.Parameters.Add(CreateParameter("@password", password));
                cmd.Parameters.Add(CreateParameter("@name", name));
                cmd.Parameters.Add(CreateParameter("@birthday", birthday));
                cmd.Parameters.Add(CreateParameter("@q1", q1));
                cmd.Parameters.Add(CreateParameter("@a1", a1));
                cmd.Parameters.Add(CreateParameter("@q2", q2));
                cmd.Parameters.Add(CreateParameter("@a2", a2));
                cmd.Parameters.Add(CreateParameter("@email", email));
                cmd.Parameters.Add(CreateParameter("@phone", phoneNumber));
                cmd.Parameters.Add(CreateParameter("@mobile", mobilePhoneNumber));
                cmd.Parameters.Add(CreateParameter("@idcard", idCard));
                
                return cmd.ExecuteNonQuery() > 0 ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR ChangePassword(string account, string oldPassword, string newPassword)
        {
            try
            {
                // 先验证旧密码
                if (CheckAccount(account, oldPassword) != SERVER_ERROR.SE_OK)
                    return SERVER_ERROR.SE_FAIL;
                
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "UPDATE TBL_ACCOUNT SET PASSWORD = @newPassword WHERE ACCOUNT = @account";
                cmd.Parameters.Add(CreateParameter("@newPassword", newPassword));
                cmd.Parameters.Add(CreateParameter("@account", account));
                
                return cmd.ExecuteNonQuery() > 0 ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR GetCharList(string account, string serverName, out string charListData)
        {
            charListData = "";
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"SELECT NAME, CLASS, HAIR, VLEVEL, SEX, ODATE 
                                  FROM TBL_CHARACTER_INFO 
                                  WHERE ACCOUNT = @account AND SERVER = @server AND DELFLAG = 0
                                  ORDER BY ODATE DESC";
                cmd.Parameters.Add(CreateParameter("@account", account));
                cmd.Parameters.Add(CreateParameter("@server", serverName));
                
                using var reader = cmd.ExecuteReader();
                var result = new StringBuilder();
                
                while (reader.Read())
                {
                    string name = reader.GetString(0);
                    byte job = reader.GetByte(1);
                    byte hair = reader.GetByte(2);
                    ushort level = (ushort)reader.GetInt16(3);
                    byte sex = reader.GetByte(4);
                    DateTime odate = reader.GetDateTime(5);
                    
                    // 格式: "name/class/hair/level/sex/"
                    if (result.Length == 0)
                    {
                        // 第一个角色（最近创建的）加'*'表示上次登录角色
                        result.Append($"*{name}/{job}/{hair}/{level}/{sex}/");
                    }
                    else
                    {
                        result.Append($"{name}/{job}/{hair}/{level}/{sex}/");
                    }
                }
                
                charListData = result.ToString();
                return SERVER_ERROR.SE_OK;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"GetCharList失败: {ex.Message}");
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR GetDelCharList(string account, string serverName, out string delCharListData)
        {
            delCharListData = "";
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"SELECT NAME, CLASS, SEX, VLEVEL, HAIR, DELDATE 
                                  FROM TBL_CHARACTER_INFO 
                                  WHERE ACCOUNT = @account AND SERVER = @server AND DELFLAG = 1";
                cmd.Parameters.Add(CreateParameter("@account", account));
                cmd.Parameters.Add(CreateParameter("@server", serverName));
                
                using var reader = cmd.ExecuteReader();
                var result = new StringBuilder();
                
                while (reader.Read())
                {
                    string name = reader.GetString(0);
                    byte job = reader.GetByte(1);
                    byte sex = reader.GetByte(2);
                    ushort level = (ushort)reader.GetInt16(3);
                    byte hair = reader.GetByte(4);
                    DateTime deldate = reader.GetDateTime(5);
                    
                    result.Append($"{name}/{job}/{sex}/{level}/{hair}/{deldate:yyyy-MM-dd HH:mm:ss}/");
                }
                
                delCharListData = result.ToString();
                return SERVER_ERROR.SE_OK;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR CreateCharacter(string account, string serverName, string name, byte job, byte hair, byte sex)
        {
            // 向后兼容的版本，使用默认等级1
            return CreateCharacter(account, serverName, name, job, hair, sex, 1);
        }
        
        public virtual SERVER_ERROR CreateCharacter(string account, string serverName, string name, byte job, byte hair, byte sex, byte level)
        {
            try
            {
                // 检查角色名是否已存在
                using var checkCmd = _connection!.CreateCommand();
                checkCmd.CommandText = "SELECT COUNT(*) FROM TBL_CHARACTER_INFO WHERE SERVER = @server AND NAME = @name";
                checkCmd.Parameters.Add(CreateParameter("@server", serverName));
                checkCmd.Parameters.Add(CreateParameter("@name", name));
                
                if (Convert.ToInt32(checkCmd.ExecuteScalar()) > 0)
                    return SERVER_ERROR.SE_SELCHAR_CHAREXIST; // 角色已存在，返回200
                
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"INSERT INTO TBL_CHARACTER_INFO (ACCOUNT, SERVER, NAME, CLASS, SEX, VLEVEL, HAIR, ODATE, DELFLAG) 
                                  VALUES (@account, @server, @name, @job, @sex, @level, @hair, @odate, @delflag)";
                
                cmd.Parameters.Add(CreateParameter("@account", account));
                cmd.Parameters.Add(CreateParameter("@server", serverName));
                cmd.Parameters.Add(CreateParameter("@name", name));
                cmd.Parameters.Add(CreateParameter("@job", job));
                cmd.Parameters.Add(CreateParameter("@sex", sex));
                cmd.Parameters.Add(CreateParameter("@level", level));
                cmd.Parameters.Add(CreateParameter("@hair", hair));
                cmd.Parameters.Add(CreateParameter("@odate", DateTime.Now));
                cmd.Parameters.Add(CreateParameter("@delflag", 0)); // 默认未删除
                
                return cmd.ExecuteNonQuery() > 0 ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR CreateCharacter(CREATECHARDESC desc)
        {
            return CreateCharacter(desc.szAccount, desc.szServer, desc.szName, desc.btClass, desc.btHair, desc.btSex, desc.btLevel);
        }
        
        public virtual SERVER_ERROR DelCharacter(string account, string serverName, string name)
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"UPDATE TBL_CHARACTER_INFO SET DELFLAG = 1, DELDATE = CURRENT_TIMESTAMP 
                                  WHERE ACCOUNT = @account AND SERVER = @server AND NAME = @name";
                cmd.Parameters.Add(CreateParameter("@account", account));
                cmd.Parameters.Add(CreateParameter("@server", serverName));
                cmd.Parameters.Add(CreateParameter("@name", name));
                
                return cmd.ExecuteNonQuery() > 0 ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR RestoreCharacter(string account, string serverName, string name)
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"UPDATE TBL_CHARACTER_INFO SET DELFLAG = 0 
                                  WHERE ACCOUNT = @account AND SERVER = @server AND NAME = @name";
                cmd.Parameters.Add(CreateParameter("@account", account));
                cmd.Parameters.Add(CreateParameter("@server", serverName));
                cmd.Parameters.Add(CreateParameter("@name", name));
                
                return cmd.ExecuteNonQuery() > 0 ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR GetCharDBInfo(string account, string serverName, string name, out byte[] charData)
        {
            charData = Array.Empty<byte>();
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"SELECT 
                    ID, CLASS, SEX, VLEVEL, MAPNAME, POSX, POSY, HAIR,
                    CUREXP, HP, MP, MAXHP, MAXMP, MINDC, MAXDC,
                    MINMC, MAXMC, MINSC, MAXSC, MINAC, MAXAC,
                    MINMAC, MAXMAC, WEIGHT, HANDWEIGHT, BODYWEIGHT,
                    GOLD, MAPID, YUANBAO, FLAG1, FLAG2, FLAG3, FLAG4, GUILDNAME, FORGEPOINT, 
                    PROP1, PROP2, PROP3, PROP4, PROP5, PROP6, PROP7, PROP8
                    FROM TBL_CHARACTER_INFO 
                    WHERE ACCOUNT = @account AND SERVER = @server AND NAME = @name";
                cmd.Parameters.Add(CreateParameter("@account", account));
                cmd.Parameters.Add(CreateParameter("@server", serverName));
                cmd.Parameters.Add(CreateParameter("@name", name));
                
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    // 注意：这里需要从DBServer的ClientConnection中获取clientKey
                    var charDbInfo = new CHARDBINFO
                    {
                        // dwClientKey将在DBServer的HandleGetCharDBInfo方法中设置
                        dwClientKey = 0, // 临时值，将在上层设置
                        szName = name,
                        dwDBId = (uint)reader.GetInt32(0),
                        mapid = (uint)reader.GetInt32(27), // MAPID
                        x = (ushort)reader.GetInt16(5), // POSX
                        y = (ushort)reader.GetInt16(6), // POSY
                        dwGold = (uint)reader.GetInt32(26), // GOLD
                        dwYuanbao = (uint)reader.GetInt32(28), // YUANBAO
                        dwCurExp = (uint)reader.GetInt32(8), // CUREXP
                        wLevel = (ushort)reader.GetInt16(3), // VLEVEL
                        btClass = reader.GetByte(1), // CLASS
                        btHair = reader.GetByte(7), // HAIR
                        btSex = reader.GetByte(2), // SEX
                        flag = 0, // 默认值
                        hp = (ushort)reader.GetInt16(9), // HP
                        mp = (ushort)reader.GetInt16(10), // MP
                        maxhp = (ushort)reader.GetInt16(11), // MAXHP
                        maxmp = (ushort)reader.GetInt16(12), // MAXMP
                        mindc = reader.GetByte(13), // MINDC
                        maxdc = reader.GetByte(14), // MAXDC
                        minmc = reader.GetByte(15), // MINMC
                        maxmc = reader.GetByte(16), // MAXMC
                        minsc = reader.GetByte(17), // MINSC
                        maxsc = reader.GetByte(18), // MAXSC
                        minac = reader.GetByte(19), // MINAC
                        maxac = reader.GetByte(20), // MAXAC
                        minmac = reader.GetByte(21), // MINMAC
                        maxmac = reader.GetByte(22), // MAXMAC
                        weight = (ushort)reader.GetInt16(23), // WEIGHT
                        handweight = (byte)reader.GetInt16(24), // HANDWEIGHT
                        bodyweight = (byte)reader.GetInt16(25), // BODYWEIGHT
                        dwForgePoint = (uint)reader.GetInt32(34), // FORGEPOINT
                        dwProp = new uint[8] {
                            (uint)reader.GetInt32(35), // PROP1
                            (uint)reader.GetInt32(36), // PROP2
                            (uint)reader.GetInt32(37), // PROP3
                            (uint)reader.GetInt32(38), // PROP4
                            (uint)reader.GetInt32(39), // PROP5
                            (uint)reader.GetInt32(40), // PROP6
                            (uint)reader.GetInt32(41), // PROP7
                            (uint)reader.GetInt32(42)  // PROP8
                        },
                        dwFlag = new uint[4] {
                            (uint)reader.GetInt32(29), // FLAG1
                            (uint)reader.GetInt32(30), // FLAG2
                            (uint)reader.GetInt32(31), // FLAG3
                            (uint)reader.GetInt32(32)  // FLAG4
                        },
                        szStartPoint = reader.GetString(4), // MAPNAME作为起始点
                        szGuildName = reader.IsDBNull(33) ? "" : reader.GetString(33) // GUILDNAME
                    };
                    
                    charData = charDbInfo.ToBytes();
                    return SERVER_ERROR.SE_OK;
                }
                
                return SERVER_ERROR.SE_FAIL;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"GetCharDBInfo失败: {ex.Message}");
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR PutCharDBInfo(string account, string serverName, string name, byte[] charData)
        {
            try
            {
                return SERVER_ERROR.SE_OK;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR QueryItems(uint ownerId, byte flag, out byte[] itemsData)
        {
            itemsData = Array.Empty<byte>();
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"SELECT 
                    ID, NAME, MINDC, MAXDC, MINMC, MAXMC, MINSC, MAXSC, MINAC, MAXAC,
                    MINMAC, MAXMAC, DURA, CURDURA, MAXDURA, NEEDTYPE, NEEDLEVEL, SPECIALPOWER, NEEDIDENTIFY,
                    WEIGHT, STDMODE, SHAPE, PRICE, UNKNOWN_1, UNKNOWN_2, POS, FINDKEY, IMAGEINDEX
                    FROM TBL_CHARACTER_ITEM 
                    WHERE OWNERID = @owner AND FLAG = @flag AND DELFLAG = 0";
                cmd.Parameters.Add(CreateParameter("@owner", ownerId));
                cmd.Parameters.Add(CreateParameter("@flag", flag));
                
                using var reader = cmd.ExecuteReader();
                var dbItems = new System.Collections.Generic.List<DBITEM>();
                
                while (reader.Read())
                {
                    try
                    {
                        // 调试：显示所有列的值
                        Console.WriteLine($"读取物品数据，字段数: {reader.FieldCount}");
                        //for (int i = 0; i < reader.FieldCount; i++)
                        //{
                        //    try
                        //    {
                        //        var value = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString();
                        //        Console.WriteLine($"  列{i}: {value}");
                        //    }
                        //    catch
                        //    {
                        //        Console.WriteLine($"  列{i}: <读取错误>");
                        //    }
                        //}
                        
                        // 创建BaseItem结构 - 使用更安全的方式读取
                        var baseItem = new MirCommon.BaseItem
                        {
                            btNameLength = (byte)Math.Min(reader.GetString(1).Length, 14), // NAME长度
                            szName = reader.GetString(1).PadRight(14, '\0').Substring(0, 14), // NAME，固定14字节
                            btStdMode = reader.GetByte(20), // STDMODE
                            btShape = reader.GetByte(21),   // SHAPE
                            btWeight = reader.GetByte(19),  // WEIGHT
                            btAniCount = 0, // 默认值
                            btSpecialpower = reader.GetByte(17), // SPECIALPOWER
                            bNeedIdentify = reader.GetByte(18), // NEEDIDENTIFY
                            btPriceType = 0, // 默认值
                            wImageIndex = reader.FieldCount > 28 ? (ushort)reader.GetInt16(28) : (ushort)0, // IMAGEINDEX
                            wMaxDura = (ushort)reader.GetInt16(12), // DURA
                            Ac1 = reader.GetByte(8),  // MINAC
                            Ac2 = reader.GetByte(9),  // MAXAC
                            Mac1 = reader.GetByte(10), // MINMAC
                            Mac2 = reader.GetByte(11), // MAXMAC
                            Dc1 = reader.GetByte(2),  // MINDC
                            Dc2 = reader.GetByte(3),  // MAXDC
                            Mc1 = reader.GetByte(4),  // MINMC
                            Mc2 = reader.GetByte(5),  // MAXMC
                            Sc1 = reader.GetByte(6),  // MINSC
                            Sc2 = reader.GetByte(7),  // MAXSC
                            needtype = reader.GetByte(15), // NEEDTYPE
                            needvalue = reader.GetByte(16), // NEEDLEVEL
                            btFlag = 0, // 默认值
                            btUpgradeTimes = 0, // 默认值
                            nPrice = reader.GetInt32(22) // PRICE
                        };
                        
                        // 创建Item结构
                        var item = new MirCommon.Item
                        {
                            baseitem = baseItem,
                            dwMakeIndex = (uint)reader.GetInt32(0), // ID
                            wCurDura = (ushort)reader.GetInt16(13), // CURDURA
                            wMaxDura = (ushort)reader.GetInt16(14), // MAXDURA
                            dwParam = new uint[4] { 0, 0, 0, 0 } // 默认值
                        };
                        
                        // 创建DBITEM结构
                        var dbItem = new DBITEM
                        {
                            item = item,
                            wPos = (ushort)reader.GetInt16(26), // POS
                            btFlag = flag
                        };
                        
                        dbItems.Add(dbItem);
                        Console.WriteLine($"成功创建DBITEM: ID={item.dwMakeIndex}, Name={baseItem.szName}, Pos={dbItem.wPos}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"创建DBITEM时出错: {ex.Message}");
                        Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                        // 继续处理其他物品
                    }
                }
                
                Console.WriteLine($"总共创建了 {dbItems.Count} 个DBITEM");
                
                // 序列化DBITEM数组
                if (dbItems.Count > 0)
                {
                    itemsData = DatabaseSerializer.SerializeDbItems(dbItems.ToArray());
                    Console.WriteLine($"序列化成功，数据长度: {itemsData.Length} 字节");
                }
                else
                {
                    Console.WriteLine("没有物品需要序列化");
                }
                
                return SERVER_ERROR.SE_OK;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"QueryItems失败: {ex.Message}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                LogManager.Default.Error($"QueryItems失败: {ex.Message}");
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR UpdateItems(uint ownerId, byte flag, byte[] itemsData)
        {
            try
            {
                return SERVER_ERROR.SE_OK;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR QueryMagic(uint ownerId, out byte[] magicData)
        {
            magicData = Array.Empty<byte>();
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"SELECT USERKEY, CURLEVEL, MAGICID, CURTRAIN
                    FROM TBL_CHARACTER_MAGIC 
                    WHERE CHARID = @owner";
                cmd.Parameters.Add(CreateParameter("@owner", ownerId));
                
                using var reader = cmd.ExecuteReader();
                var magicList = new System.Collections.Generic.List<MAGICDB>();
                
                while (reader.Read())
                {
                    var magicDb = new MAGICDB
                    {
                        btUserKey = reader.GetByte(0), // USERKEY
                        btCurLevel = reader.GetByte(1), // CURLEVEL
                        wMagicId = (ushort)reader.GetInt16(2), // MAGICID
                        dwCurTrain = (uint)reader.GetInt32(3) // CURTRAIN
                    };
                    
                    magicList.Add(magicDb);
                }
                
                // 序列化MAGICDB数组
                magicData = DatabaseSerializer.SerializeMagicDbs(magicList.ToArray());
                return SERVER_ERROR.SE_OK;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"QueryMagic失败: {ex.Message}");
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR UpdateMagic(uint ownerId, byte[] magicData)
        {
            try
            {
                return SERVER_ERROR.SE_OK;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR ExecSqlCommand(string sql, out DataTable result)
        {
            result = new DataTable();
            try
            {
                // 使用DataReader手动填充DataTable
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = sql;
                using var reader = cmd.ExecuteReader();
                
                // 创建列
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    result.Columns.Add(reader.GetName(i), reader.GetFieldType(i));
                }
                
                // 填充数据
                while (reader.Read())
                {
                    var row = result.NewRow();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row[i] = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
                    }
                    result.Rows.Add(row);
                }
                
                return SERVER_ERROR.SE_OK;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual void Close()
        {
            _connection?.Close();
            _connection?.Dispose();
            _connection = null;
        }
        
        // 以下是新增接口方法的默认实现
        
        public virtual SERVER_ERROR GetMapPosition(string account, string serverName, string name, out string mapName, out short x, out short y)
        {
            mapName = "";
            x = 0;
            y = 0;
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"SELECT MAPNAME, POSX, POSY FROM TBL_CHARACTER_INFO 
                                  WHERE ACCOUNT = @account AND SERVER = @server AND NAME = @name";
                cmd.Parameters.Add(CreateParameter("@account", account));
                cmd.Parameters.Add(CreateParameter("@server", serverName));
                cmd.Parameters.Add(CreateParameter("@name", name));
                
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    mapName = reader.GetString(0);
                    x = reader.GetInt16(1);
                    y = reader.GetInt16(2);
                    return SERVER_ERROR.SE_OK;
                }
                return SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR GetFreeItemId(out uint itemId)
        {
            itemId = 0;
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "SELECT TOP 1 ID FROM TBL_CHARACTER_ITEM WHERE DELFLAG = 1";
                
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    itemId = (uint)reader.GetInt32(0);
                    return SERVER_ERROR.SE_OK;
                }
                return SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR FindItemId(uint ownerId, byte flag, ushort pos, uint findKey, out uint itemId)
        {
            itemId = 0;
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"SELECT TOP 1 ID FROM TBL_CHARACTER_ITEM 
                                  WHERE OWNERID = @owner AND FLAG = @flag AND POS = @pos AND FINDKEY = @findKey";
                cmd.Parameters.Add(CreateParameter("@owner", ownerId));
                cmd.Parameters.Add(CreateParameter("@flag", flag));
                cmd.Parameters.Add(CreateParameter("@pos", pos));
                cmd.Parameters.Add(CreateParameter("@findKey", findKey));
                
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    itemId = (uint)reader.GetInt32(0);
                    
                    // 清除FINDKEY
                    using var updateCmd = _connection!.CreateCommand();
                    updateCmd.CommandText = "UPDATE TBL_CHARACTER_ITEM SET FINDKEY = 0 WHERE ID = @id";
                    updateCmd.Parameters.Add(CreateParameter("@id", itemId));
                    updateCmd.ExecuteNonQuery();
                    
                    return SERVER_ERROR.SE_OK;
                }
                return SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR UpgradeItem(uint makeIndex, uint upgrade)
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"UPDATE TBL_CHARACTER_ITEM 
                                  SET NEEDIDENTIFY = 1, FLAG = @flag, FINDKEY = @upgrade 
                                  WHERE ID = @id";
                cmd.Parameters.Add(CreateParameter("@flag", (byte)1)); // IDF_UPGRADE
                cmd.Parameters.Add(CreateParameter("@upgrade", upgrade));
                cmd.Parameters.Add(CreateParameter("@id", makeIndex));
                
                return cmd.ExecuteNonQuery() > 0 ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR CreateItem(uint ownerId, byte flag, ushort pos, byte[] itemData)
        {
            try
            {
                return SERVER_ERROR.SE_OK;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR CreateItemEx(uint ownerId, byte flag, ushort pos, byte[] itemData)
        {
            try
            {
                return SERVER_ERROR.SE_OK;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR UpdateItem(uint ownerId, byte flag, ushort pos, byte[] itemData)
        {
            try
            {
                return SERVER_ERROR.SE_OK;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR DeleteItem(uint itemId)
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "DELETE FROM TBL_CHARACTER_ITEM WHERE ID = @id";
                cmd.Parameters.Add(CreateParameter("@id", itemId));
                
                return cmd.ExecuteNonQuery() > 0 ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR UpdateItemPos(uint itemId, byte flag, ushort pos)
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "UPDATE TBL_CHARACTER_ITEM SET FLAG = @flag, POS = @pos WHERE ID = @id";
                cmd.Parameters.Add(CreateParameter("@flag", flag));
                cmd.Parameters.Add(CreateParameter("@pos", pos));
                cmd.Parameters.Add(CreateParameter("@id", itemId));
                
                return cmd.ExecuteNonQuery() > 0 ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR UpdateItemPosEx(byte flag, ushort count, byte[] itemPosData)
        {
            try
            {
                // 这里需要解析itemPosData并批量更新物品位置
                return SERVER_ERROR.SE_OK;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR UpdateItemOwner(uint itemId, uint ownerId, byte flag, ushort pos)
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"UPDATE TBL_CHARACTER_ITEM 
                                  SET OWNERID = @owner, FLAG = @flag, POS = @pos 
                                  WHERE ID = @id";
                cmd.Parameters.Add(CreateParameter("@owner", ownerId));
                cmd.Parameters.Add(CreateParameter("@flag", flag));
                cmd.Parameters.Add(CreateParameter("@pos", pos));
                cmd.Parameters.Add(CreateParameter("@id", itemId));
                
                return cmd.ExecuteNonQuery() > 0 ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR DeleteMagic(uint ownerId, ushort magicId)
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "DELETE FROM TBL_CHARACTER_MAGIC WHERE CHARID = @owner AND MAGICID = @magicId";
                cmd.Parameters.Add(CreateParameter("@owner", ownerId));
                cmd.Parameters.Add(CreateParameter("@magicId", magicId));
                
                return cmd.ExecuteNonQuery() > 0 ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR UpdateCommunity(uint ownerId, string communityData)
        {
            try
            {
                // 这里需要解析communityData并更新TBL_CHARACTER_COMMUNITY表
                return SERVER_ERROR.SE_OK;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR QueryCommunity(uint ownerId, out string communityData)
        {
            communityData = "";
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"SELECT MARRIAGE, MASTER, STUDENT1, STUDENT2, STUDENT3,
                    FRIEND1, FRIEND2, FRIEND3, FRIEND4, FRIEND5, FRIEND6, FRIEND7, FRIEND8, FRIEND9, FRIEND10
                    FROM TBL_CHARACTER_COMMUNITY 
                    WHERE OWNERID = @owner";
                cmd.Parameters.Add(CreateParameter("@owner", ownerId));
                
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    var result = new StringBuilder();
                    
                    // 妻子（MARRIAGE字段）
                    string wife = reader.IsDBNull(0) ? "" : reader.GetString(0);
                    if (!string.IsNullOrEmpty(wife))
                        result.Append($"{wife}/");
                    else
                        result.Append("/");
                    
                    // 师傅
                    string master = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    if (!string.IsNullOrEmpty(master))
                        result.Append($"{master}/");
                    else
                        result.Append("/");
                    
                    // 徒弟1-3
                    for (int i = 2; i <= 4; i++)
                    {
                        string student = reader.IsDBNull(i) ? "" : reader.GetString(i);
                        if (!string.IsNullOrEmpty(student))
                            result.Append($"{student}/");
                        else
                            result.Append("/");
                    }
                    
                    // 好友1-10
                    for (int i = 5; i <= 14; i++)
                    {
                        string friend = reader.IsDBNull(i) ? "" : reader.GetString(i);
                        if (!string.IsNullOrEmpty(friend))
                            result.Append($"{friend}/");
                        else
                            result.Append("/");
                    }
                    
                    communityData = result.ToString();
                    return SERVER_ERROR.SE_OK;
                }
                else
                {
                    // 如果没有记录，返回空字符串（所有字段为空）
                    // 1(妻子)+1(师傅)+3(徒弟)+10(好友)=15个斜杠
                    communityData = "///////////////";
                    return SERVER_ERROR.SE_OK;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"QueryCommunity失败: {ex.Message}");
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR DeleteMarriage(string name, string marriage)
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"UPDATE TBL_CHARACTER_COMMUNITY 
                                  SET MARRIAGE = '' 
                                  WHERE NAME = @name AND MARRIAGE = @marriage";
                cmd.Parameters.Add(CreateParameter("@name", name));
                cmd.Parameters.Add(CreateParameter("@marriage", marriage));
                
                return cmd.ExecuteNonQuery() > 0 ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR DeleteTeacher(string name, string teacher)
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"UPDATE TBL_CHARACTER_COMMUNITY 
                                  SET MASTER = '' 
                                  WHERE NAME = @name AND MASTER = @teacher";
                cmd.Parameters.Add(CreateParameter("@name", name));
                cmd.Parameters.Add(CreateParameter("@teacher", teacher));
                
                return cmd.ExecuteNonQuery() > 0 ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR DeleteStudent(string teacher, string student)
        {
            try
            {
                // 这里需要查询并更新对应的学生字段
                return SERVER_ERROR.SE_OK;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR BreakFriend(string friend1, string friend2)
        {
            try
            {
                // 这里需要查询并更新对应的好友字段
                return SERVER_ERROR.SE_OK;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR RestoreGuild(string name, string guildName)
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"UPDATE TBL_CHARACTER_INFO 
                                  SET GUILDNAME = @guildName 
                                  WHERE NAME = @name";
                cmd.Parameters.Add(CreateParameter("@guildName", guildName));
                cmd.Parameters.Add(CreateParameter("@name", name));
                
                return cmd.ExecuteNonQuery() > 0 ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR AddCredit(string name, uint count)
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "SELECT FLAG1 FROM TBL_CHARACTER_INFO WHERE NAME = @name";
                cmd.Parameters.Add(CreateParameter("@name", name));
                
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    uint credit = (uint)reader.GetInt32(0);
                    uint newCredit = credit + count;
                    if (newCredit > 0xFFFF) newCredit = 0xFFFF;
                    
                    reader.Close();
                    
                    using var updateCmd = _connection!.CreateCommand();
                    updateCmd.CommandText = "UPDATE TBL_CHARACTER_INFO SET FLAG1 = @credit WHERE NAME = @name";
                    updateCmd.Parameters.Add(CreateParameter("@credit", newCredit));
                    updateCmd.Parameters.Add(CreateParameter("@name", name));
                    
                    return updateCmd.ExecuteNonQuery() > 0 ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
                }
                return SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR QueryTaskInfo(uint ownerId, out byte[] taskInfoData)
        {
            taskInfoData = Array.Empty<byte>();
            try
            {
                // 检查数据库类型
                bool isSQLite = _connectionString.Contains("Data Source=") || _connectionString.Contains(".sqlite");
                
                if (isSQLite)
                {
                    // SQLite数据库使用不同的表结构
                    using var cmd = _connection!.CreateCommand();
                    cmd.CommandText = @"SELECT ACHIEVEMENT, TASKID1, TASKSTEP1, TASKID2, TASKSTEP2, 
                        TASKID3, TASKSTEP3, TASKID4, TASKSTEP4, TASKID5, TASKSTEP5,
                        TASKID6, TASKSTEP6, TASKID7, TASKSTEP7, TASKID8, TASKSTEP8,
                        TASKID9, TASKSTEP9, TASKID10, TASKSTEP10, FLAGS
                        FROM TBL_CHARACTER_TASK 
                        WHERE CHARID = @owner";
                    cmd.Parameters.Add(CreateParameter("@owner", ownerId));
                    
                    using var reader = cmd.ExecuteReader();
                    var taskList = new System.Collections.Generic.List<TaskInfo>();
                    
                    if (reader.Read())
                    {
                        // SQLite表结构：ACHIEVEMENT, TASKID1, TASKSTEP1, ..., TASKID10, TASKSTEP10, FLAGS
                        uint achievement = (uint)reader.GetInt32(0);
                        
                        // 处理10个任务槽位
                        for (int i = 0; i < 10; i++)
                        {
                            int taskIdIndex = 1 + i * 2;
                            int taskStepIndex = 2 + i * 2;
                            
                            uint taskId = (uint)reader.GetInt32(taskIdIndex);
                            uint taskStep = (uint)reader.GetInt32(taskStepIndex);
                            
                            if (taskId > 0)
                            {
                                var taskInfo = new TaskInfo
                                {
                                    dwOwner = ownerId,
                                    dwTaskId = taskId,
                                    dwState = taskStep, // TASKSTEP作为状态
                                    dwParam1 = 0, // 默认值
                                    dwParam2 = 0, // 默认值
                                    dwParam3 = 0, // 默认值
                                    dwParam4 = 0 // 默认值
                                };
                                
                                taskList.Add(taskInfo);
                            }
                        }
                        
                        // 如果有FLAGS字段，可以解析它
                        // string flags = reader.IsDBNull(21) ? "" : reader.GetString(21);
                    }
                    
                    // 序列化TaskInfo数组
                    taskInfoData = DatabaseSerializer.SerializeTaskInfos(taskList.ToArray());
                    return SERVER_ERROR.SE_OK;
                }
                else
                {
                    // MySQL/SQL Server数据库使用原来的表结构
                    using var cmd = _connection!.CreateCommand();
                    cmd.CommandText = @"SELECT TASKID, TASKSTATUS, TASKPROGRESS, TASKFLAG, TASKTIME
                        FROM TBL_CHARACTER_TASK 
                        WHERE CHARID = @owner";
                    cmd.Parameters.Add(CreateParameter("@owner", ownerId));
                    
                    using var reader = cmd.ExecuteReader();
                    var taskList = new System.Collections.Generic.List<TaskInfo>();
                    
                    while (reader.Read())
                    {
                        var taskInfo = new TaskInfo
                        {
                            dwOwner = ownerId,
                            dwTaskId = (uint)reader.GetInt32(0), // TASKID
                            dwState = (uint)reader.GetInt32(1), // TASKSTATUS
                            dwParam1 = (uint)reader.GetInt32(2), // TASKPROGRESS
                            dwParam2 = (uint)reader.GetInt32(3), // TASKFLAG
                            dwParam3 = (uint)reader.GetInt32(4), // TASKTIME
                            dwParam4 = 0 // 默认值
                        };
                        
                        taskList.Add(taskInfo);
                    }
                    
                    // 序列化TaskInfo数组
                    taskInfoData = DatabaseSerializer.SerializeTaskInfos(taskList.ToArray());
                    return SERVER_ERROR.SE_OK;
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"QueryTaskInfo失败: {ex.Message}");
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR UpdateTaskInfo(uint ownerId, byte[] taskInfoData)
        {
            try
            {
                // 这里需要解析taskInfoData并更新TBL_CHARACTER_TASK表
                return SERVER_ERROR.SE_OK;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        public virtual SERVER_ERROR QueryUpgradeItem(uint ownerId, out byte[] upgradeItemData)
        {
            upgradeItemData = Array.Empty<byte>();
            try
            {
                // IDF_UPGRADE = 1
                byte flag = 1; // IDF_UPGRADE
                
                // 调用QueryItems方法查询升级物品
                return QueryItems(ownerId, flag, out upgradeItemData);
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
    }
}
