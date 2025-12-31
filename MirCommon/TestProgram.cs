using System;
using System.Runtime.InteropServices;

namespace MirCommon
{
    /// <summary>
    /// 简单测试程序 - 验证基本数据结构兼容性
    /// </summary>
    public static class SimpleTest
    {
        /// <summary>
        /// 主入口点
        /// </summary>
        public static void Main()
        {
            Console.WriteLine("=== 简单数据结构兼容性测试 ===");
            Console.WriteLine($"测试开始时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine();

            try
            {
                // 测试MirMsg结构
                TestMirMsg();

                // 测试MirMsgHeader结构
                TestMirMsgHeader();

                // 测试DeletedDate结构
                TestDeletedDate();

                // 测试ServerId结构
                TestServerId();

                Console.WriteLine($"\n\n=== 所有测试完成 ===");
                Console.WriteLine($"测试结束时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine("\n按任意键退出...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ 测试程序发生错误: {ex.Message}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                Console.WriteLine("\n按任意键退出...");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// 测试MirMsg结构
        /// </summary>
        private static void TestMirMsg()
        {
            Console.WriteLine("\n=== 测试MirMsg结构 ===");

            var msg = new MirMsg();
            msg.dwFlag = 0x12345678;
            msg.wCmd = 0xABCD;
            msg.wParam[0] = 0x1111;
            msg.wParam[1] = 0x2222;
            msg.wParam[2] = 0x3333;
            msg.data[0] = 0xAA;
            msg.data[1] = 0xBB;
            msg.data[2] = 0xCC;
            msg.data[3] = 0xDD;

            int size = Marshal.SizeOf<MirMsg>();
            Console.WriteLine($"MirMsg大小: {size}字节");
            Console.WriteLine($"预期大小: 16字节 (C++ MIRMSG结构大小)");
            Console.WriteLine($"大小匹配: {size == 16}");

            // 验证字段偏移量
            int dwFlagOffset = Marshal.OffsetOf<MirMsg>("dwFlag").ToInt32();
            int wCmdOffset = Marshal.OffsetOf<MirMsg>("wCmd").ToInt32();
            int dataOffset = Marshal.OffsetOf<MirMsg>("data").ToInt32();

            Console.WriteLine($"字段偏移量验证:");
            Console.WriteLine($"  dwFlag偏移: {dwFlagOffset} (预期: 0) - {(dwFlagOffset == 0 ? "✅" : "❌")}");
            Console.WriteLine($"  wCmd偏移: {wCmdOffset} (预期: 4) - {(wCmdOffset == 4 ? "✅" : "❌")}");
            Console.WriteLine($"  data偏移: {dataOffset} (预期: 12) - {(dataOffset == 12 ? "✅" : "❌")}");

            // 测试序列化
            byte[] buffer = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(msg, ptr, false);
                Marshal.Copy(ptr, buffer, 0, size);

                Console.WriteLine($"序列化成功，数据: {BitConverter.ToString(buffer)}");

                // 反序列化
                var msg2 = Marshal.PtrToStructure<MirMsg>(ptr);
                Console.WriteLine($"反序列化成功:");
                Console.WriteLine($"  dwFlag: 0x{msg2.dwFlag:X8} (预期: 0x12345678) - {(msg2.dwFlag == 0x12345678 ? "✅" : "❌")}");
                Console.WriteLine($"  wCmd: 0x{msg2.wCmd:X4} (预期: 0xABCD) - {(msg2.wCmd == 0xABCD ? "✅" : "❌")}");
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        /// <summary>
        /// 测试MirMsgHeader结构
        /// </summary>
        private static void TestMirMsgHeader()
        {
            Console.WriteLine("\n=== 测试MirMsgHeader结构 ===");

            var header = new MirMsgHeader(0x12345678, 0xABCD, 1, 2, 3);

            int size = Marshal.SizeOf<MirMsgHeader>();
            Console.WriteLine($"MirMsgHeader大小: {size}字节");
            Console.WriteLine($"预期大小: 12字节 (C++ MIRMSGHEADER结构大小)");
            Console.WriteLine($"大小匹配: {size == 12}");

            // 验证字段偏移量
            int dwFlagOffset = Marshal.OffsetOf<MirMsgHeader>("dwFlag").ToInt32();
            int wCmdOffset = Marshal.OffsetOf<MirMsgHeader>("wCmd").ToInt32();
            int w3Offset = Marshal.OffsetOf<MirMsgHeader>("w3").ToInt32();

            Console.WriteLine($"字段偏移量验证:");
            Console.WriteLine($"  dwFlag偏移: {dwFlagOffset} (预期: 0) - {(dwFlagOffset == 0 ? "✅" : "❌")}");
            Console.WriteLine($"  wCmd偏移: {wCmdOffset} (预期: 4) - {(wCmdOffset == 4 ? "✅" : "❌")}");
            Console.WriteLine($"  w3偏移: {w3Offset} (预期: 10) - {(w3Offset == 10 ? "✅" : "❌")}");
        }

        /// <summary>
        /// 测试DeletedDate结构
        /// </summary>
        private static void TestDeletedDate()
        {
            Console.WriteLine("\n=== 测试DeletedDate结构 ===");

            var date = new DeletedDate(2025, 12, 17, 23, 15, true);

            int size = Marshal.SizeOf<DeletedDate>();
            Console.WriteLine($"DeletedDate大小: {size}字节");
            Console.WriteLine($"预期大小: 4字节 (C++ deleted_date结构大小)");
            Console.WriteLine($"大小匹配: {size == 4}");

            Console.WriteLine($"日期信息:");
            Console.WriteLine($"  年份: {date.Year} (预期: 2025) - {(date.Year == 2025 ? "✅" : "❌")}");
            Console.WriteLine($"  月份: {date.Month} (预期: 12) - {(date.Month == 12 ? "✅" : "❌")}");
            Console.WriteLine($"  日期: {date.Day} (预期: 17) - {(date.Day == 17 ? "✅" : "❌")}");
            Console.WriteLine($"  小时: {date.Hour} (预期: 23) - {(date.Hour == 23 ? "✅" : "❌")}");
            Console.WriteLine($"  分钟: {date.Minute} (预期: 15) - {(date.Minute == 15 ? "✅" : "❌")}");
            Console.WriteLine($"  标志: {date.BFlag} (预期: True) - {(date.BFlag == true ? "✅" : "❌")}");

            // 测试原始数据
            uint rawData = date.RawData;
            Console.WriteLine($"原始数据: 0x{rawData:X8}");

            // 测试从原始数据创建
            var date2 = new DeletedDate(rawData);
            Console.WriteLine($"从原始数据创建:");
            Console.WriteLine($"  年份: {date2.Year} (预期: 2025) - {(date2.Year == 2025 ? "✅" : "❌")}");
            Console.WriteLine($"  月份: {date2.Month} (预期: 12) - {(date2.Month == 12 ? "✅" : "❌")}");
        }

        /// <summary>
        /// 测试ServerId结构
        /// </summary>
        private static void TestServerId()
        {
            Console.WriteLine("\n=== 测试ServerId结构 ===");

            var serverId = new ServerId();
            serverId.dwId = 0x12345678;

            int size = Marshal.SizeOf<ServerId>();
            Console.WriteLine($"ServerId大小: {size}字节");
            Console.WriteLine($"预期大小: 4字节 (C++ ServerId union大小)");
            Console.WriteLine($"大小匹配: {size == 4}");

            Console.WriteLine($"服务器ID信息:");
            Console.WriteLine($"  dwId: 0x{serverId.dwId:X8}");
            Console.WriteLine($"  bType: {serverId.bType}");
            Console.WriteLine($"  bGroup: {serverId.bGroup}");
            Console.WriteLine($"  bId: {serverId.bId}");
            Console.WriteLine($"  bIndex: {serverId.bIndex}");

            // 测试通过字段设置
            serverId.bType = 1;
            serverId.bGroup = 2;
            serverId.bId = 3;
            serverId.bIndex = 4;

            Console.WriteLine($"设置后:");
            Console.WriteLine($"  bType: {serverId.bType} (预期: 1) - {(serverId.bType == 1 ? "✅" : "❌")}");
            Console.WriteLine($"  bGroup: {serverId.bGroup} (预期: 2) - {(serverId.bGroup == 2 ? "✅" : "❌")}");
            Console.WriteLine($"  bId: {serverId.bId} (预期: 3) - {(serverId.bId == 3 ? "✅" : "❌")}");
            Console.WriteLine($"  bIndex: {serverId.bIndex} (预期: 4) - {(serverId.bIndex == 4 ? "✅" : "❌")}");
            Console.WriteLine($"  dwId: 0x{serverId.dwId:X8}");
        }

        /// <summary>
        /// 生成测试报告
        /// </summary>
        public static string GenerateTestReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== 简单数据结构兼容性报告 ===");
            sb.AppendLine($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            // 添加数据结构大小信息
            sb.AppendLine("=== 数据结构大小汇总 ===");
            sb.AppendLine($"MirMsg: {Marshal.SizeOf<MirMsg>()}字节 (C++: 16字节)");
            sb.AppendLine($"MirMsgHeader: {Marshal.SizeOf<MirMsgHeader>()}字节 (C++: 12字节)");
            sb.AppendLine($"DeletedDate: {Marshal.SizeOf<DeletedDate>()}字节 (C++: 4字节)");
            sb.AppendLine($"ServerId: {Marshal.SizeOf<ServerId>()}字节 (C++: 4字节)");

            // 添加额外信息
            sb.AppendLine("\n=== 额外测试信息 ===");
            sb.AppendLine($"测试时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"操作系统: {RuntimeInformation.OSDescription}");
            sb.AppendLine($"框架: {RuntimeInformation.FrameworkDescription}");
            sb.AppendLine($"进程架构: {RuntimeInformation.ProcessArchitecture}");

            return sb.ToString();
        }
    }

    /// <summary>
    /// 测试程序 - 用于验证数据结构兼容性
    /// </summary>
    public static class TestProgram
    {
        /// <summary>
        /// 主入口点
        /// </summary>
        public static void Main()
        {
            Console.WriteLine("=== MIRWORLD C# 数据结构兼容性测试 ===");
            Console.WriteLine($"测试开始时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine();
            
            try
            {
                // 运行数据结构验证
                Console.WriteLine("阶段1: 数据结构兼容性验证");
                Console.WriteLine("==========================");
                DataStructureValidator.ValidateAllStructures();
                
                Console.WriteLine("\n\n阶段2: 消息结构测试");
                Console.WriteLine("==================");
                // MessageCodec类在MirDefine.cs中已经存在，名为GameCodec
                TestMessageCodec();
                
                Console.WriteLine("\n\n阶段3: 位字段测试");
                Console.WriteLine("================");
                BitFieldTester.RunAllTests();
                
                Console.WriteLine("\n\n阶段4: 综合测试");
                Console.WriteLine("==============");
                RunComprehensiveTests();
                
                Console.WriteLine($"\n\n=== 所有测试完成 ===");
                Console.WriteLine($"测试结束时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine("\n按任意键退出...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ 测试程序发生错误: {ex.Message}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                Console.WriteLine("\n按任意键退出...");
                Console.ReadKey();
            }
        }
        
        /// <summary>
        /// 测试消息编码解码功能
        /// </summary>
        private static void TestMessageCodec()
        {
            Console.WriteLine("=== 消息编码解码测试 ===");
            
            try
            {
                // 测试数据
                byte[] testData = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
                byte[] codedBuffer = new byte[testData.Length * 2];
                byte[] decodedBuffer = new byte[testData.Length];
                
                // 编码
                int codedSize = GameCodec.CodeGameCode(testData, testData.Length, codedBuffer);
                Console.WriteLine($"编码前: {BitConverter.ToString(testData)}");
                Console.WriteLine($"编码后 ({codedSize}字节): {BitConverter.ToString(codedBuffer, 0, codedSize)}");
                
                // 解码
                int decodedSize = GameCodec.UnGameCode(codedBuffer, decodedBuffer);
                Console.WriteLine($"解码后 ({decodedSize}字节): {BitConverter.ToString(decodedBuffer, 0, decodedSize)}");
                
                // 验证编码解码是否一致
                bool match = true;
                for (int i = 0; i < Math.Min(testData.Length, decodedSize); i++)
                {
                    if (testData[i] != decodedBuffer[i])
                    {
                        match = false;
                        break;
                    }
                }
                
                if (match && testData.Length == decodedSize)
                {
                    Console.WriteLine("✅ 编码解码测试通过！");
                }
                else
                {
                    Console.WriteLine("❌ 编码解码测试失败！");
                }
                
                // 测试完整消息编码
                Console.WriteLine("\n=== 完整消息编码测试 ===");
                byte[] msgBuffer = new byte[256];
                int msgSize = GameCodec.EncodeMsg(msgBuffer, 0x12345678, 0xABCD, 1, 2, 3, testData);
                Console.WriteLine($"消息编码成功，大小: {msgSize}字节");
                Console.WriteLine($"消息数据: {BitConverter.ToString(msgBuffer, 0, msgSize)}");
                
                // 测试消息解码
                Console.WriteLine("\n=== 完整消息解码测试 ===");
                bool decodeResult = GameCodec.DecodeMsg(msgBuffer, msgSize, out var header, out var payload);
                if (decodeResult)
                {
                    Console.WriteLine($"消息解码成功:");
                    Console.WriteLine($"  消息头: dwFlag=0x{header.dwFlag:X8}, wCmd=0x{header.wCmd:X4}");
                    Console.WriteLine($"  参数: w1={header.w1}, w2={header.w2}, w3={header.w3}");
                    if (payload != null)
                    {
                        Console.WriteLine($"  负载数据: {BitConverter.ToString(payload)}");
                    }
                }
                else
                {
                    Console.WriteLine("❌ 消息解码失败！");
                }
                
                Console.WriteLine("\n✅ 所有编码解码测试完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 编码解码测试失败: {ex.Message}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// 运行综合测试
        /// </summary>
        private static void RunComprehensiveTests()
        {
            Console.WriteLine("--- 综合测试 ---");
            
            // 测试BaseItemFixed的union功能
            TestBaseItemUnion();
            
            // 测试ItemFixed的序列化
            TestItemSerialization();
            
            // 测试MirMsg的兼容性
            TestMirMsgCompatibility();
            
            // 测试位字段的实际使用
            TestBitFieldUsage();
            
            Console.WriteLine("✅ 综合测试完成");
        }
        
        /// <summary>
        /// 测试BaseItemFixed的union功能
        /// </summary>
        private static void TestBaseItemUnion()
        {
            Console.WriteLine("\n测试BaseItemFixed的union功能:");
            
            var item = new BaseItemFixed();
            
            // 测试union 1: bNeedIdentify和btPriceType
            item.bNeedIdentify = 1;
            item.btPriceType = 2;
            Console.WriteLine($"设置bNeedIdentify={item.bNeedIdentify}, btPriceType={item.btPriceType}");
            Console.WriteLine($"对应的wMapId=0x{item.wMapId:X4}");
            
            // 测试union 1: wMapId
            item.wMapId = 0x1234;
            Console.WriteLine($"设置wMapId=0x{item.wMapId:X4}");
            Console.WriteLine($"对应的bNeedIdentify={item.bNeedIdentify}, btPriceType={item.btPriceType}");
            
            // 测试union 2: 属性结构
            item.btMinDef = 5;
            item.btMaxDef = 10;
            Console.WriteLine($"设置btMinDef={item.btMinDef}, btMaxDef={item.btMaxDef}");
            Console.WriteLine($"对应的Ac1={item.Ac1}, Ac2={item.Ac2}");
            Console.WriteLine($"对应的wAc=0x{item.wAc:X4}");
            
            // 测试union 3: wUnknown/btFlag/btUpgradeTimes
            item.btFlag = 1;
            item.btUpgradeTimes = 2;
            Console.WriteLine($"设置btFlag={item.btFlag}, btUpgradeTimes={item.btUpgradeTimes}");
            Console.WriteLine($"对应的wUnknown=0x{item.wUnknown:X4}");
        }
        
        /// <summary>
        /// 测试ItemFixed的序列化
        /// </summary>
        private static void TestItemSerialization()
        {
            Console.WriteLine("\n测试ItemFixed的序列化:");
            
            var item = new ItemFixed();
            
            // 设置测试数据
            item.baseitem.SetName("TestItem");
            item.baseitem.btStdMode = 5;
            item.baseitem.nPrice = 1000;
            item.dwMakeIndex = 123456;
            item.wCurDura = 75;
            item.wMaxDura = 100;
            item.dwParam[0] = 1;
            item.dwParam[1] = 2;
            item.dwParam[2] = 3;
            item.dwParam[3] = 4;
            
            // 序列化
            int size = Marshal.SizeOf<ItemFixed>();
            byte[] buffer = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            
            try
            {
                Marshal.StructureToPtr(item, ptr, false);
                Marshal.Copy(ptr, buffer, 0, size);
                
                Console.WriteLine($"ItemFixed序列化成功，大小: {size}字节");
                Console.WriteLine($"预期大小: 68字节 (C++ ITEM结构大小)");
                Console.WriteLine($"大小匹配: {size == 68}");
                
                // 反序列化
                var item2 = Marshal.PtrToStructure<ItemFixed>(ptr);
                Console.WriteLine($"反序列化成功:");
                Console.WriteLine($"  名称: {item2.baseitem.GetName()}");
                Console.WriteLine($"  标准模式: {item2.baseitem.btStdMode}");
                Console.WriteLine($"  价格: {item2.baseitem.nPrice}");
                Console.WriteLine($"  制造索引: {item2.dwMakeIndex}");
                Console.WriteLine($"  耐久: {item2.wCurDura}/{item2.wMaxDura}");
                
                // 验证数据一致性
                bool dataMatch = item.baseitem.GetName() == item2.baseitem.GetName() &&
                                item.baseitem.btStdMode == item2.baseitem.btStdMode &&
                                item.baseitem.nPrice == item2.baseitem.nPrice &&
                                item.dwMakeIndex == item2.dwMakeIndex &&
                                item.wCurDura == item2.wCurDura &&
                                item.wMaxDura == item2.wMaxDura;
                
                Console.WriteLine($"数据一致性验证: {(dataMatch ? "✅" : "❌")}");
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
        
        /// <summary>
        /// 测试MirMsg的兼容性
        /// </summary>
        private static void TestMirMsgCompatibility()
        {
            Console.WriteLine("\n测试MirMsg的兼容性:");
            
            var msg = new MirMsg();
            msg.dwFlag = 0x12345678;
            msg.wCmd = 0xABCD;
            msg.wParam[0] = 0x1111;
            msg.wParam[1] = 0x2222;
            msg.wParam[2] = 0x3333;
            msg.data[0] = 0xAA;
            msg.data[1] = 0xBB;
            msg.data[2] = 0xCC;
            msg.data[3] = 0xDD;
            
            int size = Marshal.SizeOf<MirMsg>();
            Console.WriteLine($"MirMsg大小: {size}字节");
            Console.WriteLine($"预期大小: 16字节 (C++ MIRMSG结构大小)");
            Console.WriteLine($"大小匹配: {size == 16}");
            
            // 验证字段偏移量
            int dwFlagOffset = Marshal.OffsetOf<MirMsg>("dwFlag").ToInt32();
            int wCmdOffset = Marshal.OffsetOf<MirMsg>("wCmd").ToInt32();
            int dataOffset = Marshal.OffsetOf<MirMsg>("data").ToInt32();
            
            Console.WriteLine($"字段偏移量验证:");
            Console.WriteLine($"  dwFlag偏移: {dwFlagOffset} (预期: 0) - {(dwFlagOffset == 0 ? "✅" : "❌")}");
            Console.WriteLine($"  wCmd偏移: {wCmdOffset} (预期: 4) - {(wCmdOffset == 4 ? "✅" : "❌")}");
            Console.WriteLine($"  data偏移: {dataOffset} (预期: 12) - {(dataOffset == 12 ? "✅" : "❌")}");
        }
        
        /// <summary>
        /// 测试位字段的实际使用
        /// </summary>
        private static void TestBitFieldUsage()
        {
            Console.WriteLine("\n测试位字段的实际使用:");
            
            // 测试DeletedDateFixed
            var date = new DeletedDateFixed(2025, 12, 17, 23, 1, true);
            Console.WriteLine($"创建DeletedDateFixed: {date}");
            Console.WriteLine($"原始数据: 0x{date.RawData:X8}");
            
            // 修改位字段
            date.Year = 2026;
            date.Month = 1;
            date.Day = 1;
            date.Hour = 0;
            date.Minute = 0;
            date.BFlag = false;
            
            Console.WriteLine($"修改后: {date}");
            Console.WriteLine($"新原始数据: 0x{date.RawData:X8}");
            
            // 测试UpgradeAddMaskFixed
            var mask = new UpgradeAddMaskFixed();
            mask.AddType1 = 3;
            mask.AddType2 = 5;
            mask.AddValue1 = 2;
            mask.AddValue2 = 1;
            mask.AddDura = 3;
            mask.BAddDura = true;
            mask.Flag = 4;
            mask.Left = 0x1234;
            
            Console.WriteLine($"创建UpgradeAddMaskFixed: {mask}");
            Console.WriteLine($"wAddMask: 0x{mask.wAddMask:X4}");
            Console.WriteLine($"wItemLimit: 0x{mask.wItemLimit:X4}");
            
            // 验证位字段范围
            Console.WriteLine($"位字段范围验证: {(mask.Validate() ? "✅" : "❌")}");
        }
        
        /// <summary>
        /// 生成测试报告
        /// </summary>
        public static string GenerateTestReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== 数据结构兼容性报告 ===");
            sb.AppendLine($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            
            // 添加数据结构大小信息
            sb.AppendLine("=== 数据结构大小汇总 ===");
            sb.AppendLine($"BaseItemFixed: {Marshal.SizeOf<BaseItemFixed>()}字节 (C++: 44字节)");
            sb.AppendLine($"ItemFixed: {Marshal.SizeOf<ItemFixed>()}字节 (C++: 68字节)");
            sb.AppendLine($"ItemClientFixed: {Marshal.SizeOf<ItemClientFixed>()}字节 (C++: 52字节)");
            sb.AppendLine($"MirMsg: {Marshal.SizeOf<MirMsg>()}字节 (C++: 16字节)");
            sb.AppendLine($"MirMsgHeader: {Marshal.SizeOf<MirMsgHeader>()}字节 (C++: 12字节)");
            sb.AppendLine($"DeletedDateFixed: {Marshal.SizeOf<DeletedDateFixed>()}字节 (C++: 4字节)");
            sb.AppendLine($"UpgradeAddMaskFixed: {Marshal.SizeOf<UpgradeAddMaskFixed>()}字节 (C++: 4字节)");
            sb.AppendLine($"ServerIdFixed: {Marshal.SizeOf<ServerIdFixed>()}字节 (C++: 4字节)");
            
            // 添加额外信息
            sb.AppendLine("\n=== 额外测试信息 ===");
            sb.AppendLine($"测试时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"操作系统: {RuntimeInformation.OSDescription}");
            sb.AppendLine($"框架: {RuntimeInformation.FrameworkDescription}");
            sb.AppendLine($"进程架构: {RuntimeInformation.ProcessArchitecture}");
            
            return sb.ToString();
        }
    }
}
