using MirCommon;
using MirCommon.Network;
using MirCommon.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
// 类型别名
using Player = GameServer.HumanPlayer;

namespace GameServer
{
    class Program
    {
        private static GameServerApp? _server;
        private static bool _isRunning = true;

        static async Task Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            Console.Title = "MirWorld Game Server - C# 版本";
            Console.WriteLine("===========================================");
            Console.WriteLine("   传世游戏服务器 - C# 版本 v0.6");
            Console.WriteLine("===========================================");
            Console.WriteLine();

            try
            {
                //var configManager = new ConfigManager("gameserver_config.json");
                //configManager.Load();

                //var logConfig = ServerConfigHelper.LoadLogConfig(configManager);
                //var logger = new Logger(logConfig.directory, logConfig.writeToConsole, logConfig.writeToFile);
                //LogManager.SetDefaultLogger(logger);

                // 使用INI配置文件
                var iniReader = new IniFileReader("config.ini");
                if (!iniReader.Open())
                {
                    Console.WriteLine("无法打开配置文件 config.ini");
                    return;
                }

                _server = new GameServerApp(iniReader);

                if (await _server.Initialize())
                {
                    LogManager.Default.Info("游戏服务器初始化成功");
                    //启动与传世客户端通讯线程
                    await _server.Start();
                    //启动与ServerCenter通讯线程
                    await _server.StartServerCenterClient();
                    //启动控制台控制器线程
                    _ = Task.Run(() => CommandLoop());
                    
                    //游戏世界逻辑处理循环（主线程）
                    while (_isRunning)
                    {
                        await Task.Delay(100);
                        _server.Update();
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Fatal("严重错误", exception: ex);
            }
            finally
            {
                _server?.Stop();
                LogManager.Shutdown();
            }
        }

        private static void CommandLoop()
        {
            Console.WriteLine("\n可用命令:");
            Console.WriteLine("  help     - 显示帮助");
            Console.WriteLine("  status   - 显示服务器状态");
            Console.WriteLine("  players  - 列出在线玩家");
            Console.WriteLine("  maps     - 列出所有地图");
            Console.WriteLine("  monsters - 列出所有怪物");
            Console.WriteLine("  spawn    - 刷怪 (spawn <mapId> <monsterId>)");
            Console.WriteLine("  clear    - 清屏");
            Console.WriteLine("  exit     - 退出服务器\n");

            while (_isRunning)
            {
                Console.Write("> ");
                string? input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input)) continue;

                var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var command = parts[0].ToLower();

                switch (command)
                {
                    case "help":
                        Console.WriteLine("\n可用命令: help, status, players, maps, monsters, spawn, clear, exit\n");
                        break;
                    case "players":
                        _server?.ListPlayers();
                        break;
                    case "maps":
                        _server?.ListMaps();
                        break;
                    case "monsters":
                        _server?.ListMonsters();
                        break;
                    case "status":
                        _server?.ShowStatus();
                        break;
                    case "spawn":
                        if (parts.Length >= 3 && int.TryParse(parts[1], out int mapId) && int.TryParse(parts[2], out int monsterId))
                        {
                            _server?.SpawnMonster(mapId, monsterId);
                        }
                        else
                        {
                            Console.WriteLine("用法: spawn <mapId> <monsterId>");
                        }
                        break;
                    case "clear":
                        Console.Clear();
                        break;
                    case "exit":
                    case "quit":
                        Console.WriteLine("正在关闭服务器...");
                        _isRunning = false;
                        break;
                    default:
                        Console.WriteLine($"未知命令: {command}。输入 help 查看可用命令。");
                        break;
                }
            }
        }
    }
}
