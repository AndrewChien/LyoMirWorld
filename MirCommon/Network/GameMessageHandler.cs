using MirCommon.Utils;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MirCommon.Network
{
    /// <summary>
    /// 游戏消息处理器
    /// </summary>
    public static class GameMessageHandler
    {
        #region （客户端通讯专用）

        /// <summary>
        /// 解码游戏消息
        /// </summary>
        public static bool DecodeGameMessageOrign(byte[] encodedData, int encodedLength, out MirMsgOrign msg, out byte[] payload)
        {
            msg = new MirMsgOrign();
            payload = Array.Empty<byte>();

            try
            {
                // 检查消息格式：以'#'开头，以'!'结尾
                if (encodedLength < 3 || encodedData[0] != '#' || encodedData[encodedLength - 1] != '!')
                {
                    // 调试日志
                    if (encodedLength > 0)
                    {
                        string debugStr = Encoding.GetEncoding("GBK").GetString(encodedData, 0, Math.Min(encodedLength, 50));
                        LogManager.Default.Debug($"DecodeGameMessageOrign: 消息格式错误，长度={encodedLength}, 第一个字符='{(char)encodedData[0]}', 最后一个字符='{(char)encodedData[encodedLength-1]}', 内容='{debugStr}'");
                    }
                    return false;
                }

                // 去掉首尾字符
                int codedLength = encodedLength - 2;
                byte[] codedData = new byte[codedLength];
                Array.Copy(encodedData, 1, codedData, 0, codedLength);

                // 检查第一个字符是否是数字（'0'-'9'），如果是则跳过
                int decodeStart = 0;
                if (codedLength > 0 && codedData[0] >= '0' && codedData[0] <= '9')
                {
                    decodeStart = 1;
                    // 调整codedLength
                    codedLength--;
                    LogManager.Default.Debug($"DecodeGameMessageOrign: 跳过开头的数字字符 '{codedData[0]}'");
                }

                // 解码消息
                byte[] decodedData = new byte[codedLength * 3]; // 解码后数据可能更大
                byte[] dataToDecode = new byte[codedLength];
                Array.Copy(codedData, decodeStart, dataToDecode, 0, codedLength);
                
                // 调试日志：显示要解码的数据
                string dataToDecodeStr = Encoding.GetEncoding("GBK").GetString(dataToDecode, 0, Math.Min(codedLength, 50));
                LogManager.Default.Debug($"DecodeGameMessageOrign: 要解码的数据长度={codedLength}, 内容='{dataToDecodeStr}'");
                
                int decodedLength = GameCodec.UnGameCode(dataToDecode, decodedData);
                LogManager.Default.Debug($"DecodeGameMessageOrign: 解码后长度={decodedLength}, 需要至少{MirMsgOrign.Size}字节");

                // 检查解码后的数据长度是否足够
                // 注意：客户端可能发送12字节的MirMsgHeader或16字节的完整MirMsgOrign
                int minSize = Math.Min(MirMsgOrign.Size, 12); // 至少需要12字节（MirMsgHeader大小）
                
                if (decodedLength < minSize)
                {
                    LogManager.Default.Debug($"DecodeGameMessageOrign: 解码后数据长度不足: {decodedLength} < {minSize}");
                    return false;
                }

                // 解析消息头
                // 首先尝试解析为MirMsgHeader（12字节）
                if (decodedLength >= 12)
                {
                    IntPtr ptr = Marshal.AllocHGlobal(12);
                    try
                    {
                        Marshal.Copy(decodedData, 0, ptr, 12);
                        var header = Marshal.PtrToStructure<MirMsgHeader>(ptr);

                        // 将MirMsgHeader转换为MirMsgOrign
                        msg.dwFlag = header.dwFlag;
                        msg.wCmd = header.wCmd;
                        msg.wParam = new ushort[3] { header.w1, header.w2, header.w3 };
                        //msg.data = new byte[4];
                        
                        LogManager.Default.Debug($"DecodeGameMessageOrign: 解析成功（12字节头）, 命令=0x{msg.wCmd:X4}, Flag=0x{msg.dwFlag:X8}, wParam=[{msg.wParam[0]},{msg.wParam[1]},{msg.wParam[2]}]");
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(ptr);
                    }
                }

                // 如果解码后的数据长度足够，尝试解析为完整的MirMsgOrign（16字节）
                if (decodedLength >= MirMsgOrign.Size)
                {
                    IntPtr ptr = Marshal.AllocHGlobal(MirMsgOrign.Size);
                    try
                    {
                        Marshal.Copy(decodedData, 0, ptr, MirMsgOrign.Size);
                        var fullMsg = Marshal.PtrToStructure<MirMsgOrign>(ptr);
                        
                        // 使用完整消息覆盖部分消息
                        msg.dwFlag = fullMsg.dwFlag;
                        msg.wCmd = fullMsg.wCmd;
                        msg.wParam = fullMsg.wParam;
                        //msg.data = fullMsg.data;
                        
                        LogManager.Default.Debug($"DecodeGameMessageOrign: 解析成功（16字节完整消息）, 命令=0x{msg.wCmd:X4}, Flag=0x{msg.dwFlag:X8}");
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(ptr);
                    }
                }

                // 提取负载数据
                // 注意：负载数据从消息头之后开始
                int headerSize = (decodedLength >= MirMsgOrign.Size) ? MirMsgOrign.Size : 12;
                if (decodedLength > headerSize)
                {
                    payload = new byte[decodedLength - headerSize];
                    Array.Copy(decodedData, headerSize, payload, 0, payload.Length);
                    LogManager.Default.Debug($"DecodeGameMessageOrign: 负载数据长度={payload.Length}");
                }

                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Debug($"DecodeGameMessageOrign: 解码游戏消息失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 编码游戏消息，证实客户端读取正常
        /// </summary>
        public static byte[] EncodeGameMessageOrign(MirMsgOrign msg, byte[]? payload = null)
        {
            try
            {
                // 将消息头转换为字节数组
                int headerSize = MirMsgOrign.Size;
                byte[] headerBytes = new byte[headerSize];
                IntPtr ptr = Marshal.AllocHGlobal(headerSize);
                try
                {
                    Marshal.StructureToPtr(msg, ptr, false);
                    Marshal.Copy(ptr, headerBytes, 0, headerSize);
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }

                // 计算总长度
                int payloadLength = payload?.Length ?? 0;
                int totalLength = headerSize + payloadLength;

                // 编码数据
                byte[] encodedData = new byte[totalLength * 2 + 3]; // 编码后可能更大
                int encodedSize = 1;
                encodedData[0] = (byte)'#';

                // 编码消息头
                byte[] tempBuffer1 = new byte[headerSize * 2];
                int codedSize1 = GameCodec.CodeGameCode(headerBytes, headerSize, tempBuffer1);
                Array.Copy(tempBuffer1, 0, encodedData, encodedSize, codedSize1);
                encodedSize += codedSize1;

                // 编码负载数据
                if (payload != null && payloadLength > 0)
                {
                    byte[] tempBuffer2 = new byte[payloadLength * 2];
                    int codedSize2 = GameCodec.CodeGameCode(payload, payloadLength, tempBuffer2);
                    Array.Copy(tempBuffer2, 0, encodedData, encodedSize, codedSize2);
                    encodedSize += codedSize2;
                }

                encodedData[encodedSize++] = (byte)'!';

                // 返回实际使用的部分
                byte[] result = new byte[encodedSize];
                Array.Copy(encodedData, 0, result, 0, encodedSize);
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"编码游戏消息失败: {ex.Message}");
                return Array.Empty<byte>();
            }
        }

        #endregion

        /// <summary>
        /// 解析单个消息
        /// </summary>
        public static int ParseSingleMessage(byte[] data, int startIndex, int length, 
            Action<MirMsgOrign, byte[]> messageHandler)
        {
            char startChar = '#';
            char endChar = '!';
            int parsedSize = 0;
            int messageStart = -1;
            
            for (int i = 0; i < length; i++)
            {
                int currentIndex = startIndex + i;
                char currentChar = (char)data[currentIndex];
                
                if (currentChar == '*')
                {
                    // 心跳包响应
                    parsedSize = i + 1;
                }
                else if (currentChar == startChar)
                {
                    messageStart = currentIndex + 1; // '#'后面的位置
                }
                else if (currentChar == endChar)
                {
                    if (messageStart != -1)
                    {
                        // 处理从messageStart到currentIndex之间的消息
                        int encodedLength = currentIndex - messageStart;
                        byte[] encodedData = new byte[encodedLength];
                        Array.Copy(data, messageStart, encodedData, 0, encodedLength);
                        
                        // 检查第一个字符是否是数字（'0'-'9'），如果是则跳过
                        int decodeStart = 0;
                        if (encodedLength > 0 && encodedData[0] >= '0' && encodedData[0] <= '9')
                        {
                            decodeStart = 1;
                        }
                        
                        // 解码游戏消息
                        byte[] decoded = new byte[(encodedLength - decodeStart) * 3];
                        byte[] dataToDecode = new byte[encodedLength - decodeStart];
                        Array.Copy(encodedData, decodeStart, dataToDecode, 0, dataToDecode.Length);
                        int decodedSize = GameCodec.UnGameCode(dataToDecode, decoded);
                        
                        if (decodedSize >= MirMsgOrign.Size)
                        {
                            // 解析消息头
                            IntPtr ptr = Marshal.AllocHGlobal(MirMsgOrign.Size);
                            try
                            {
                                Marshal.Copy(decoded, 0, ptr, MirMsgOrign.Size);
                                var msg = Marshal.PtrToStructure<MirMsgOrign>(ptr);
                                
                                // 提取负载数据
                                byte[] payload = Array.Empty<byte>();
                                if (decodedSize > MirMsgOrign.Size)
                                {
                                    payload = new byte[decodedSize - MirMsgOrign.Size];
                                    Array.Copy(decoded, MirMsgOrign.Size, payload, 0, payload.Length);
                                }
                                
                                // 调用消息处理器
                                messageHandler?.Invoke(msg, payload);
                            }
                            finally
                            {
                                Marshal.FreeHGlobal(ptr);
                            }
                        }
                        
                        messageStart = -1;
                    }
                    parsedSize = i + 1;
                }
            }
            
            return parsedSize;
        }

        /// <summary>
        /// 创建游戏消息，证实客户端读取正常
        /// </summary>
        public static MirMsgOrign CreateMessage2(uint dwFlag, ushort wCmd, ushort w1 = 0, ushort w2 = 0, ushort w3 = 0)
        {
            var msg = new MirMsgOrign
            {
                dwFlag = dwFlag,
                wCmd = wCmd,
                wParam = new ushort[3] { w1, w2, w3 },
                //data = new byte[4]
            };
            return msg;
        }

        /// <summary>
        /// 发送游戏消息，证实客户端读取正常
        /// </summary>
        public static void SendGameMessage2(System.Net.Sockets.NetworkStream stream, MirMsgOrign msg, byte[]? payload = null)
        {
            try
            {
                byte[] encoded = EncodeGameMessageOrign(msg, payload);
                if (encoded.Length > 0)
                {
                    stream.Write(encoded, 0, encoded.Length);
                    stream.Flush();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发送游戏消息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送简单游戏消息，证实客户端读取正常
        /// </summary>
        public static void SendSimpleMessage2(System.Net.Sockets.NetworkStream stream, uint dwFlag, ushort wCmd,
            ushort w1 = 0, ushort w2 = 0, ushort w3 = 0, byte[]? payload = null)
        {
            var msg = CreateMessage2(dwFlag, wCmd, w1, w2, w3);
            SendGameMessage2(stream, msg, payload);
        }


        public static class ClientCommands
        {
            public const ushort CM_CONFIRMFIRSTDIALOG = 0x3fa;
            public const ushort CM_SAY = 0xbd6;
            public const ushort CM_WALK = 0xbc3;
            public const ushort CM_RUN = 0xbc5;
            public const ushort CM_ATTACK = 0xbc6;
            public const ushort CM_TURN = 0xbc2;
            public const ushort CM_GETMEAL = 0xbc4;
            public const ushort CM_ZUOYI = 0xbcd;
            public const ushort CM_SPELLSKILL = 0xbc9;
            public const ushort CM_STOP = 0xbcc;
            public const ushort CM_TAKEONITEM = 1003;
            public const ushort CM_TAKEOFFITEM = 1004;
            public const ushort CM_DROPITEM = 0x3e8;
            public const ushort CM_PICKUPITEM = 0x3e9;
            public const ushort CM_QUERYTRADE = 0x401;
            public const ushort CM_PUTTRADEITEM = 0x402;
            public const ushort CM_PUTTRADEGOLD = 0x405;
            public const ushort CM_QUERYTRADEEND = 0x406;
            public const ushort CM_CANCELTRADE = 0x404;
            public const ushort CM_SELECTLINK = 0x3f3;
            public const ushort CM_QUERYSTARTPRIVATESHOP = 0x5eb1;
            public const ushort CM_CHANGEGROUPMODE = 0x3fb;
            public const ushort CM_QUERYADDGROUPMEMBER = 0x3fc;
            public const ushort CM_DELETEGROUPMEMBER = 0x3fe;

            // 其他命令
            public const ushort CM_ENTERGAME = 0x0f;

            public const ushort CM_PUTITEMTOPETBAG = 0xc02;
            public const ushort CM_GETITEMFROMPETBAG = 0xc03;
            public const ushort CM_DELETETASK = 0x5eb2;
            public const ushort CM_GMCOMMAND = 0x9999;
            public const ushort CM_COMPLETELYQUIT = 0x6a;
            public const ushort CM_CUTBODY = 0x3ef;
            public const ushort CM_PUTITEM = 0x8810;
            public const ushort CM_SHOWPETINFO = 0x8897;
            public const ushort CM_QUERYTIME = 0x0c00;
            public const ushort CM_MARKET = 0x1000;
            public const ushort CM_MINE = 0xbc7;
            public const ushort CM_DELETEFRIEND = 0x43;
            public const ushort CM_REPLYADDFRIEND = 0x44;
            public const ushort CM_ADDFRIEND = 0x42;
            public const ushort CM_CREATEGUILD = 0x6891;
            public const ushort CM_RIDEHORSE = 0x40;
            public const ushort CM_REPLYADDTOGUILD = 0xaaa;
            public const ushort CM_INVITETOGUILD = 0x40e;
            public const ushort CM_TAKEBANKITEM = 0x408;
            public const ushort CM_PUTBANKITEM = 0x407;
            public const ushort CM_QUERYCOMMUNITY = 0x51;
            public const ushort CM_QUERYITEMLIST = 0x3f7;
            public const ushort CM_SELLITEM = 0x3f5;
            public const ushort CM_QUERYITEMSELLPRICE = 0x3f4;
            public const ushort CM_QUERYGROUPPOSITION = 0x041f;
            public const ushort CM_REMOVEGUILDMEMBER = 0x40f;
            public const ushort CM_EDITGUILDNOTICE = 0x410;
            public const ushort CM_EDITGUILDTITLE = 0x411;
            public const ushort CM_QUERYGUILDEXP = 0x412;
            public const ushort CM_QUERYGUILDINFO = 0x40b;
            public const ushort CM_QUERYGUILDMEMBERLIST = 0x40d;
            public const ushort CM_SPECIALHIT_POJISHIELD = 0xbd2;  // 破击，破盾
            public const ushort CM_SPECIALHIT_HALFMOON = 0xbd0;    // 半月
            public const ushort CM_SPECIALHIT_FIRE = 0xbd1;        // 烈火
            public const ushort CM_SPECIALHIT_ASSASSINATE = 0xbcb; // 刺杀
            public const ushort CM_SPECIALHIT_KILL = 0xbca;        // 攻杀
            public const ushort CM_QUERYHISTORYADDR = 0x8d00;
            public const ushort CM_SETMAGICKEY = 0x3f0;
            public const ushort CM_DROPGOLD = 0x3ed;  // SM_DROPGOLD
            public const ushort CM_USEITEM = 0x3ee;
            public const ushort CM_QUERYMINIMAP = 0x409;
            public const ushort CM_BUYITEM = 0x3f6;
            public const ushort CM_SETBAGITEMPOS = 0x45;
            public const ushort CM_REPAIRITEM = 0x3ff;
            public const ushort CM_QUERYREPAIRPRICE = 0x400;
            public const ushort CM_NPCTALK = 0x3f2;
            public const ushort CM_RESTARTGAME = 0x3f1;
            public const ushort CM_VIEWEQUIPMENT = 0x52;
            public const ushort CM_PING = 0x3d3;
            public const ushort CM_TRAINHORSE = 0xba0;

            // 添加缺失的命令（根据错误日志）
            public const ushort CM_LEAVESERVER = 0x6a;  // 106 - 离开服务器
            public const ushort SM_UNKNOWN_COMMAND = 0x45;   // 69 - 未知命令，需要进一步确认
        }

        /// <summary>
        /// 服务器消息常量
        /// </summary>
        public static class ServerCommands
        {
            public const ushort SM_FIRSTDIALOG = 0x292;
            public const ushort SM_TAKEON_OK = 615;
            public const ushort SM_TAKEON_FAIL = 616;
            public const ushort SM_TAKEOFF_OK = 619;
            public const ushort SM_TAKEOFF_FAIL = 620;
            public const ushort SM_ADDBAGITEM = 0xc8;
            public const ushort SM_BAGINFO = 0xc9;
            public const ushort SM_EQUIPMENTS = 0x26d;
            public const ushort SM_TRADESTART = 0x2a1;
            public const ushort SM_PUTTRADEITEMOK = 0x2a3;
            public const ushort SM_PUTTRADEITEMFAIL = 0x2a4;
            public const ushort SM_PUTTRADEGOLDOK = 0x2ac;
            public const ushort SM_PUTTRADEGOLDFAIL = 0x2ad;
            public const ushort SM_TRADEEND = 0x2af;
            public const ushort SM_TRADECANCELED = 0x2a9;
            public const ushort SM_OTHERPUTTRADEGOLD = 0x2ae;
            public const ushort SM_OTHERPUTTRADEITEM = 0x2aa;
            public const ushort SM_GROUPMODE = 0x293;
            public const ushort SM_GROUPCREATE = 0x294;
            public const ushort SM_UPDATEMEMBERINFO = 0x2e0;
            public const ushort SM_GROUPMEMBERLIST = 0x29b;
            public const ushort SM_GROUPDESTROYED = 0x29a;
            public const ushort SM_ENTERGAMEOK = 0x0f;
            public const ushort SM_SETMAP = 0x33;
            public const ushort SM_SETPLAYER = 0x32;
            public const ushort SM_WALK = 0x0b;
            public const ushort SM_RUN = 0x0d;
            public const ushort SM_ATTACK = 0x0e;
            public const ushort SM_APPEAR = 0x0a;
            public const ushort SM_DISAPPEAR = 0x1e;
            public const ushort SM_STOP = 0xcc;
            public const ushort SM_CHAT = 0x28;
            public const ushort SM_WEIGHTCHANGED = 0x35;  // 重量变化

            public const ushort SM_TIMERESPONSE = 0x9600;
            public const ushort SM_BANKTAKEOK = 0x2c1;
            public const ushort SM_BANKTAKEFAIL = 0x2c2;
            public const ushort SM_BANKPUTOK = 0x2bd;
            public const ushort SM_BANKPUTFAIL = 0x2be;
            public const ushort SM_SELLITEMOK = 0x288;
            public const ushort SM_SELLITEMFAIL = 0x289;
            public const ushort SM_QUERYITEMSELLPRICE = 0x287;
            public const ushort SM_QUERYITEMLISTFAIL = 0x028B;
            public const ushort SM_BUYITEMOK = 0x28a;
            public const ushort SM_BUYITEMFAIL = 0x28b;
            public const ushort SM_MINIMAP = 0x2c6;
            public const ushort SM_REPAIRITEMFAIL = 0x029E;
            public const ushort SM_QUERYREPAIRPRICE = 0x29f;
            public const ushort SM_GUILDFRONTPAGE = 0x2f1;
            public const ushort SM_GUILDFRONTPAGEFAIL = 0x2f2;
            public const ushort SM_GUILDMEMBERLIST = 0x2f4;
            public const ushort SM_GUILDEXP = 0x2cd;
            public const ushort SM_VIEWDETAIL = 0x2ef;
            public const ushort SM_PINGRESPONSE = 0x3d4;
            public const ushort SM_DROPITEMOK = 0x258;
            public const ushort SM_DROPITEMFAIL = 0x259;
            public const ushort SM_RIDEHORSERESPONSE = 0xcd;
            public const ushort SM_HISTORYADDR = 0x518c;
            public const ushort SM_SETITEMPOSITION = 0x46;   // 设置物品位置


            public const ushort SM_UNKNOWN_COMMAND = 0x45;   // 69 - 未知命令，需要进一步确认
            public const ushort SM_ERROR = 0x6a;  // 106 - 未知命令，需要进一步确认
        }


        ///// <summary>
        ///// 解码游戏消息（支持复包处理）
        ///// </summary>
        //public static bool DecodeGameMessage(byte[] encodedData, int encodedLength, out MirMsg msg, out byte[] payload)
        //{
        //    msg = new MirMsg();
        //    payload = Array.Empty<byte>();

        //    try
        //    {
        //        // 检查消息格式：以'#'开头，以'!'结尾
        //        if (encodedLength < 3 || encodedData[0] != '#' || encodedData[encodedLength - 1] != '!')
        //        {
        //            // 调试日志
        //            if (encodedLength > 0)
        //            {
        //                string debugStr = Encoding.GetEncoding("GBK").GetString(encodedData, 0, Math.Min(encodedLength, 50));
        //                LogManager.Default.Debug($"DecodeGameMessage: 消息格式错误，长度={encodedLength}, 第一个字符='{(char)encodedData[0]}', 最后一个字符='{(char)encodedData[encodedLength - 1]}', 内容='{debugStr}'");
        //            }
        //            return false;
        //        }

        //        // 去掉首尾字符
        //        int codedLength = encodedLength - 2;
        //        byte[] codedData = new byte[codedLength];
        //        Array.Copy(encodedData, 1, codedData, 0, codedLength);

        //        int decodeStart = 0;
        //        if (codedLength > 0 && codedData[0] >= '0' && codedData[0] <= '9')
        //        {
        //            decodeStart = 1;
        //            // 调整codedLength
        //            codedLength--;
        //            LogManager.Default.Debug($"DecodeGameMessage: 跳过开头的数字字符 '{codedData[0]}'");
        //        }

        //        // 解码消息
        //        byte[] decodedData = new byte[codedLength * 3]; // 解码后数据可能更大
        //        byte[] dataToDecode = new byte[codedLength];
        //        Array.Copy(codedData, decodeStart, dataToDecode, 0, codedLength);

        //        // 调试日志：显示要解码的数据
        //        string dataToDecodeStr = Encoding.GetEncoding("GBK").GetString(dataToDecode, 0, Math.Min(codedLength, 50));
        //        LogManager.Default.Debug($"DecodeGameMessage: 要解码的数据长度={codedLength}, 内容='{dataToDecodeStr}'");

        //        int decodedLength = GameCodec.UnGameCode(dataToDecode, decodedData);
        //        LogManager.Default.Debug($"DecodeGameMessage: 解码后长度={decodedLength}, 需要至少{MirMsg.Size}字节");

        //        // 检查解码后的数据长度是否足够
        //        // 注意：客户端可能发送12字节的MirMsgHeader或16字节的完整MirMsg
        //        int minSize = Math.Min(MirMsg.Size, 12); // 至少需要12字节（MirMsgHeader大小）

        //        if (decodedLength < minSize)
        //        {
        //            LogManager.Default.Debug($"DecodeGameMessage: 解码后数据长度不足: {decodedLength} < {minSize}");
        //            return false;
        //        }

        //        // 解析消息头
        //        // 首先尝试解析为MirMsgHeader（12字节）
        //        if (decodedLength >= 12)
        //        {
        //            IntPtr ptr = Marshal.AllocHGlobal(12);
        //            try
        //            {
        //                Marshal.Copy(decodedData, 0, ptr, 12);
        //                var header = Marshal.PtrToStructure<MirMsgHeader>(ptr);

        //                // 将MirMsgHeader转换为MirMsg
        //                msg.dwFlag = header.dwFlag;
        //                msg.wCmd = header.wCmd;
        //                msg.wParam = new ushort[3] { header.w1, header.w2, header.w3 };
        //                msg.data = new byte[4];

        //                LogManager.Default.Debug($"DecodeGameMessage: 解析成功（12字节头）, 命令=0x{msg.wCmd:X4}, Flag=0x{msg.dwFlag:X8}, wParam=[{msg.wParam[0]},{msg.wParam[1]},{msg.wParam[2]}]");
        //            }
        //            finally
        //            {
        //                Marshal.FreeHGlobal(ptr);
        //            }
        //        }

        //        // 如果解码后的数据长度足够，尝试解析为完整的MirMsg（16字节）
        //        if (decodedLength >= MirMsg.Size)
        //        {
        //            IntPtr ptr = Marshal.AllocHGlobal(MirMsg.Size);
        //            try
        //            {
        //                Marshal.Copy(decodedData, 0, ptr, MirMsg.Size);
        //                var fullMsg = Marshal.PtrToStructure<MirMsg>(ptr);

        //                // 使用完整消息覆盖部分消息
        //                msg.dwFlag = fullMsg.dwFlag;
        //                msg.wCmd = fullMsg.wCmd;
        //                msg.wParam = fullMsg.wParam;
        //                msg.data = fullMsg.data;

        //                LogManager.Default.Debug($"DecodeGameMessage: 解析成功（16字节完整消息）, 命令=0x{msg.wCmd:X4}, Flag=0x{msg.dwFlag:X8}");
        //            }
        //            finally
        //            {
        //                Marshal.FreeHGlobal(ptr);
        //            }
        //        }

        //        // 提取负载数据
        //        // 注意：负载数据从消息头之后开始
        //        int headerSize = (decodedLength >= MirMsg.Size) ? MirMsg.Size : 12;
        //        if (decodedLength > headerSize)
        //        {
        //            payload = new byte[decodedLength - headerSize];
        //            Array.Copy(decodedData, headerSize, payload, 0, payload.Length);
        //            LogManager.Default.Debug($"DecodeGameMessage: 负载数据长度={payload.Length}");
        //        }

        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        LogManager.Default.Debug($"DecodeGameMessage: 解码游戏消息失败: {ex.Message}");
        //        return false;
        //    }
        //}

        /// <summary>
        /// 编码游戏消息
        /// </summary>
        public static byte[] EncodeGameMessage(MirMsg msg, byte[]? payload = null)
        {
            try
            {
                // 将消息头转换为字节数组
                int headerSize = MirMsg.Size;
                byte[] headerBytes = new byte[headerSize];
                IntPtr ptr = Marshal.AllocHGlobal(headerSize);
                try
                {
                    Marshal.StructureToPtr(msg, ptr, false);
                    Marshal.Copy(ptr, headerBytes, 0, headerSize);
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }

                // 计算总长度
                int payloadLength = payload?.Length ?? 0;
                int totalLength = headerSize + payloadLength;

                // 编码数据
                byte[] encodedData = new byte[totalLength * 2 + 3]; // 编码后可能更大
                int encodedSize = 1;
                encodedData[0] = (byte)'#';

                // 编码消息头
                byte[] tempBuffer1 = new byte[headerSize * 2];
                int codedSize1 = GameCodec.CodeGameCode(headerBytes, headerSize, tempBuffer1);
                Array.Copy(tempBuffer1, 0, encodedData, encodedSize, codedSize1);
                encodedSize += codedSize1;

                // 编码负载数据
                if (payload != null && payloadLength > 0)
                {
                    byte[] tempBuffer2 = new byte[payloadLength * 2];
                    int codedSize2 = GameCodec.CodeGameCode(payload, payloadLength, tempBuffer2);
                    Array.Copy(tempBuffer2, 0, encodedData, encodedSize, codedSize2);
                    encodedSize += codedSize2;
                }

                encodedData[encodedSize++] = (byte)'!';

                // 返回实际使用的部分
                byte[] result = new byte[encodedSize];
                Array.Copy(encodedData, 0, result, 0, encodedSize);
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"编码游戏消息失败: {ex.Message}");
                return Array.Empty<byte>();
            }
        }

        /// <summary>
        /// 创建游戏消息
        /// </summary>
        public static MirMsg CreateMessage(uint dwFlag, ushort wCmd, ushort w1 = 0, ushort w2 = 0, ushort w3 = 0)
        {
            var msg = new MirMsg
            {
                dwFlag = dwFlag,
                wCmd = wCmd,
                wParam = new ushort[3] { w1, w2, w3 },
                data = new byte[4]
            };
            return msg;
        }

        /// <summary>
        /// 发送游戏消息
        /// </summary>
        public static void SendGameMessage(System.Net.Sockets.NetworkStream stream, MirMsg msg, byte[]? payload = null)
        {
            try
            {
                byte[] encoded = EncodeGameMessage(msg, payload);
                if (encoded.Length > 0)
                {
                    stream.Write(encoded, 0, encoded.Length);
                    stream.Flush();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发送游戏消息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送简单游戏消息
        /// </summary>
        public static void SendSimpleMessage(System.Net.Sockets.NetworkStream stream, uint dwFlag, ushort wCmd,
            ushort w1 = 0, ushort w2 = 0, ushort w3 = 0, byte[]? payload = null)
        {
            var msg = CreateMessage(dwFlag, wCmd, w1, w2, w3);
            SendGameMessage(stream, msg, payload);
        }

        //public static void ProcessClientMessage(byte[] data, int length, 
        //    Action<MirMsg, byte[]> messageHandler)
        //{
        //    if (DecodeGameMessage(data, length, out var msg, out var payload))
        //    {
        //        messageHandler?.Invoke(msg, payload);
        //    }
        //}
    }
}
