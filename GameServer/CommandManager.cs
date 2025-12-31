using System;
using System.Collections.Generic;

namespace GameServer
{
    /// <summary>
    /// 命令参数
    /// </summary>
    public class ScriptParam
    {
        public string StringValue { get; set; } = string.Empty;
        public int IntValue { get; set; }
    }

    /// <summary>
    /// 命令处理器委托
    /// </summary>
    public delegate uint CommandProc(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution);

    /// <summary>
    /// 命令管理器
    /// </summary>
    public class CommandManager
    {
        private static CommandManager? _instance;
        public static CommandManager Instance => _instance ??= new CommandManager();

        private readonly Dictionary<string, CommandProc> _commandProcs = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        private CommandManager()
        {
            InitializeDefaultCommands();
        }

        /// <summary>
        /// 初始化默认命令
        /// </summary>
        private void InitializeDefaultCommands()
        {
            // 注册基础命令
            AddCommand("RANDOM", ProcRandom);
            AddCommand("GIVE", ProcGive);
            AddCommand("SET", ProcSet);
            AddCommand("CHECK", ProcCheck);
            AddCommand("CHECKEX", ProcCheckEx);
            AddCommand("TAKEBAGITEM", ProcTakeBagItem);
            AddCommand("GIVEGOLD", ProcGiveGold);
            AddCommand("TAKEGOLD", ProcTakeGold);
            AddCommand("GIVEYUANBAO", ProcGiveYuanbao);
            AddCommand("TAKEYUANBAO", ProcTakeYuanbao);
            AddCommand("GIVEEXP", ProcGiveExp);
            AddCommand("MOVE", ProcMove);
            AddCommand("MAPMOVE", ProcMapMove);
            AddCommand("CLOSE", ProcClose);
            AddCommand("GOTO", ProcGoto);
            AddCommand("DELAY", ProcDelay);
            AddCommand("RETURN", ProcReturn);
            AddCommand("CALL", ProcCall);
            AddCommand("INC", ProcInc);
            AddCommand("DEC", ProcDec);
            AddCommand("VAR", ProcVar);
            AddCommand("CLRVAR", ProcClrVar);
            AddCommand("MOVR", ProcMovr);
            AddCommand("SYSTEMMSG", ProcSystemMsg);
            AddCommand("SCROLLMSG", ProcScrollMsg);
        }

        /// <summary>
        /// 添加命令
        /// </summary>
        public bool AddCommand(string command, CommandProc proc)
        {
            if (string.IsNullOrEmpty(command) || proc == null)
                return false;

            lock (_lock)
            {
                if (_commandProcs.ContainsKey(command))
                {
                    Console.WriteLine($"命令 {command} 已经注册过");
                    return false;
                }

                _commandProcs[command] = proc;
                Console.WriteLine($"注册命令: {command}");
                return true;
            }
        }

        /// <summary>
        /// 获取命令处理器
        /// </summary>
        public CommandProc? GetCommandProc(string command)
        {
            if (string.IsNullOrEmpty(command))
                return null;

            lock (_lock)
            {
                return _commandProcs.TryGetValue(command, out var proc) ? proc : null;
            }
        }

        /// <summary>
        /// 更改命令名称
        /// </summary>
        public bool ChangeCommandName(string oldCommand, string newCommand)
        {
            if (string.IsNullOrEmpty(oldCommand) || string.IsNullOrEmpty(newCommand))
                return false;

            lock (_lock)
            {
                if (!_commandProcs.TryGetValue(oldCommand, out var proc))
                    return false;

                _commandProcs.Remove(oldCommand);
                _commandProcs[newCommand] = proc;
                return true;
            }
        }

        /// <summary>
        /// 获取所有命令名称
        /// </summary>
        public List<string> GetAllCommandNames()
        {
            lock (_lock)
            {
                return new List<string>(_commandProcs.Keys);
            }
        }

        /// <summary>
        /// 获取命令数量
        /// </summary>
        public int GetCommandCount()
        {
            lock (_lock)
            {
                return _commandProcs.Count;
            }
        }

        #region 命令处理器实现

        // 随机数命令
        private static uint ProcRandom(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            var random = new Random();
            if (paramCount == 0)
                return (uint)random.Next();
            else if (paramCount == 1)
                return (uint)random.Next(parameters[0].IntValue);
            else
                return (uint)random.Next(parameters[0].IntValue, parameters[1].IntValue);
        }

        // 给予物品命令
        private static uint ProcGive(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"给予物品: {parameters[0].StringValue}");
            return 1;
        }

        // 设置标志命令
        private static uint ProcSet(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 1)
                return 0;

            Console.WriteLine($"设置标志: {parameters[0].StringValue}");
            return 1;
        }

        // 检查命令
        private static uint ProcCheck(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 1)
                return 0;

            int value = 0;
            if (paramCount > 1)
            {
                int compValue = parameters[1].IntValue;
                return (uint)(value == compValue ? 1 : 0);
            }
            else
            {
                return (uint)(value > 0 ? 1 : 0);
            }
        }

        // 扩展检查命令
        private static uint ProcCheckEx(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount != 3)
                return 0;

            int value = 0;
            int compValue = parameters[2].IntValue;
            char sign = parameters[1].StringValue[0];

            switch (sign)
            {
                case '<': return (uint)(value < compValue ? 1 : 0);
                case '>': return (uint)(value > compValue ? 1 : 0);
                case '=': return (uint)(value == compValue ? 1 : 0);
                case '!': return (uint)(value != compValue ? 1 : 0);
                default: return 0;
            }
        }

        // 从背包拿走物品命令
        private static uint ProcTakeBagItem(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"从背包拿走物品: {parameters[0].StringValue}");
            return 1;
        }

        // 给予金币命令
        private static uint ProcGiveGold(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"给予金币: {parameters[0].IntValue}");
            return 1;
        }

        // 拿走金币命令
        private static uint ProcTakeGold(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"拿走金币: {parameters[0].IntValue}");
            return 1;
        }

        // 给予元宝命令
        private static uint ProcGiveYuanbao(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"给予元宝: {parameters[0].IntValue}");
            return 1;
        }

        // 拿走元宝命令
        private static uint ProcTakeYuanbao(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"拿走元宝: {parameters[0].IntValue}");
            return 1;
        }

        // 给予经验命令
        private static uint ProcGiveExp(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"给予经验: {parameters[0].IntValue}");
            return 1;
        }

        // 移动命令
        private static uint ProcMove(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;
            
            Console.WriteLine($"移动命令执行");
            return 1;
        }

        // 地图移动命令
        private static uint ProcMapMove(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;
            
            Console.WriteLine($"地图移动命令执行");
            return 1;
        }

        // 关闭命令
        private static uint ProcClose(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            shell.SetExecuteResult(ScriptShell.ExecuteResult.Close);
            continueExecution = false;
            return 0;
        }

        // 跳转命令
        private static uint ProcGoto(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"跳转到页面: {parameters[0].StringValue}");
            return 1;
        }

        // 延迟命令
        private static uint ProcDelay(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 2)
                return 0;

            Console.WriteLine($"延迟 {parameters[0].IntValue} 秒后执行: {parameters[1].StringValue}");
            return 1;
        }

        // 返回命令
        private static uint ProcReturn(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            string value = paramCount > 0 ? parameters[0].StringValue : "0";
            shell.SetExecuteResult(ScriptShell.ExecuteResult.Return, value);
            continueExecution = false;
            return 0;
        }

        // 调用命令
        private static uint ProcCall(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"调用页面: {parameters[0].StringValue}");
            return 1;
        }

        // 增加命令
        private static uint ProcInc(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"增加变量: {parameters[0].StringValue}");
            return 1;
        }

        // 减少命令
        private static uint ProcDec(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"减少变量: {parameters[0].StringValue}");
            return 1;
        }

        // 变量命令
        private static uint ProcVar(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"设置变量: {parameters[0].StringValue}");
            return 1;
        }

        // 清除变量命令
        private static uint ProcClrVar(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"清除变量: {parameters[0].StringValue}");
            return 1;
        }

        // 移动变量命令
        private static uint ProcMovr(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"移动变量: {parameters[0].StringValue}");
            return 1;
        }

        // 系统消息命令
        private static uint ProcSystemMsg(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"系统消息: {parameters[0].StringValue}");
            return 1;
        }

        // 滚动消息命令
        private static uint ProcScrollMsg(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"滚动消息: {parameters[0].StringValue}");
            return 1;
        }

        // 检查背包物品命令
        private static uint ProcCheckBagItem(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"检查背包物品: {parameters[0].StringValue}");
            return 1;
        }

        // 检查装备命令
        private static uint ProcCheckEquipment(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"检查装备: {parameters[0].StringValue}");
            return 1;
        }

        // 升级命令
        private static uint ProcLevelUp(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"升级: {parameters[0].IntValue} 级");
            return 1;
        }

        // 添加名称列表命令
        private static uint ProcAddNameList(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"添加名称到列表: {parameters[0].StringValue}");
            return 1;
        }

        // 添加账号列表命令
        private static uint ProcAddAccountList(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"添加账号到列表: {parameters[0].StringValue}");
            return 1;
        }

        // 添加IP列表命令
        private static uint ProcAddIpList(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"添加IP到列表: {parameters[0].StringValue}");
            return 1;
        }

        // 删除名称列表命令
        private static uint ProcDelNameList(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"从列表删除名称: {parameters[0].StringValue}");
            return 1;
        }

        // 删除账号列表命令
        private static uint ProcDelAccountList(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"从列表删除账号: {parameters[0].StringValue}");
            return 1;
        }

        // 删除IP列表命令
        private static uint ProcDelIpList(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"从列表删除IP: {parameters[0].StringValue}");
            return 1;
        }

        // 检查账号列表命令
        private static uint ProcCheckAccountList(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"检查账号列表: {parameters[0].StringValue}");
            return 1;
        }

        // 检查角色名称列表命令
        private static uint ProcCheckCharNameList(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"检查角色名称列表: {parameters[0].StringValue}");
            return 1;
        }

        // 检查IP列表命令
        private static uint ProcCheckIpList(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"检查IP列表: {parameters[0].StringValue}");
            return 1;
        }

        // 小时命令
        private static uint ProcHour(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 2)
                return 0;

            int hour = DateTime.Now.Hour;
            int start = parameters[0].IntValue;
            int end = parameters[1].IntValue;
            return (uint)(hour >= start && hour <= end ? 1 : 0);
        }

        // 分钟命令
        private static uint ProcMinute(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 2)
                return 0;

            int minute = DateTime.Now.Minute;
            int start = parameters[0].IntValue;
            int end = parameters[1].IntValue;
            return (uint)(minute >= start && minute <= end ? 1 : 0);
        }

        // 星期几命令
        private static uint ProcDayOfWeek(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            int dayOfWeek = (int)DateTime.Now.DayOfWeek;
            int targetDay = parameters[0].IntValue;
            return (uint)(dayOfWeek == targetDay ? 1 : 0);
        }

        // 之前命令
        private static uint ProcBefore(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"检查时间之前: {parameters[0].StringValue}");
            return 1;
        }

        // 之后命令
        private static uint ProcAfter(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"检查时间之后: {parameters[0].StringValue}");
            return 1;
        }

        // 设置标志命令
        private static uint ProcSetFlag(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"设置标志: {parameters[0].IntValue}");
            return 1;
        }

        // 清除标志命令
        private static uint ProcClrFlag(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"清除标志: {parameters[0].IntValue}");
            return 1;
        }

        // 检查标志命令
        private static uint ProcCheckFlag(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"检查标志: {parameters[0].IntValue}");
            return 1;
        }

        // 是沙巴克所有者命令
        private static uint ProcIsSabukOwner(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"检查是否是沙巴克所有者");
            return 0;
        }

        // 是沙巴克成员命令
        private static uint ProcIsSabukMember(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"检查是否是沙巴克成员");
            return 0;
        }

        // 是行会会长命令
        private static uint ProcIsGuildMaster(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"检查是否是行会会长");
            return 0;
        }

        // 有行会命令
        private static uint ProcHasGuild(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"检查是否有行会");
            return 0;
        }

        // 是攻击沙巴克行会命令
        private static uint ProcIsAttackSabukGuild(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"检查是否是攻击沙巴克行会");
            return 0;
        }

        // 请求攻击沙巴克命令
        private static uint ProcRequestAttackSabuk(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"请求攻击沙巴克");
            return 0;
        }

        // 改变名称颜色命令
        private static uint ProcChangeNameColor(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 2)
                return 0;

            Console.WriteLine($"改变名称颜色: {parameters[0].IntValue}, 时间: {parameters[1].IntValue}");
            return 1;
        }

        // 改变衣服颜色命令
        private static uint ProcChangeDressColor(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"改变衣服颜色: {parameters[0].IntValue}");
            return 1;
        }

        // 修理主门命令
        private static uint ProcRepairMainDoor(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"修理主门");
            return 1;
        }

        // 修理城墙命令
        private static uint ProcRepairWall(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"修理城墙: {parameters[0].IntValue}");
            return 1;
        }

        // 沙巴克战争已开始命令
        private static uint ProcIsSabukWarStarted(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"检查沙巴克战争是否已开始");
            return 0;
        }

        // 给予声望命令
        private static uint ProcGiveCredit(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"给予声望: {parameters[0].IntValue}");
            return 1;
        }

        // 拿走声望命令
        private static uint ProcTakeCredit(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"拿走声望: {parameters[0].IntValue}");
            return 1;
        }

        // 增加PK点命令
        private static uint ProcIncPkPoint(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"增加PK点: {parameters[0].IntValue}");
            return 1;
        }

        // 减少PK点命令
        private static uint ProcDecPkPoint(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"减少PK点: {parameters[0].IntValue}");
            return 1;
        }

        // 清除PK点命令
        private static uint ProcClrPkPoint(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"清除PK点");
            return 1;
        }

        // 执行武器升级命令
        private static uint ProcDoUpgradeWeapon(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"执行武器升级");
            return 1;
        }

        // 拿走升级武器命令
        private static uint ProcTakeUpgradeWeapon(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"拿走升级武器");
            return 1;
        }

        // 有升级武器命令
        private static uint ProcHasUpgradeWeapon(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"检查是否有升级武器");
            return 0;
        }

        // 是首次登录命令
        private static uint ProcIsFirstLogin(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"检查是否是首次登录");
            return 0;
        }

        // 清除首次登录命令
        private static uint ProcClearFirstLogin(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"清除首次登录标志");
            return 1;
        }

        // 是组成员命令
        private static uint ProcIsGroupMember(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"检查是否是组成员");
            return 0;
        }

        // 是组领导命令
        private static uint ProcIsGroupLeader(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"检查是否是组领导");
            return 0;
        }

        // 进入排行榜命令
        private static uint ProcEnterTopList(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"进入排行榜");
            return 1;
        }

        // 设置沙巴克主人命令
        private static uint ProcSetSabukMaster(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"设置沙巴克主人");
            return 1;
        }

        // 检查地图人数命令
        private static uint ProcCheckMapHum(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 3)
                return 0;

            Console.WriteLine($"检查地图人数: 地图ID={parameters[0].IntValue}, 符号={parameters[1].StringValue}, 数量={parameters[2].IntValue}");
            return 0;
        }

        // 检查地图怪物命令
        private static uint ProcCheckMapMon(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 3)
                return 0;

            Console.WriteLine($"检查地图怪物: 地图ID={parameters[0].IntValue}, 符号={parameters[1].StringValue}, 数量={parameters[2].IntValue}");
            return 0;
        }

        // 怪物生成命令
        private static uint ProcMonGen(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 6)
                return 0;

            Console.WriteLine($"怪物生成: 名称={parameters[0].StringValue}, 地图ID={parameters[1].IntValue}, X={parameters[2].IntValue}, Y={parameters[3].IntValue}, 范围={parameters[4].IntValue}, 数量={parameters[5].IntValue}");
            return 1;
        }

        // 目标怪物生成命令
        private static uint ProcTargetMonGen(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 8)
                return 0;

            Console.WriteLine($"目标怪物生成: 名称={parameters[0].StringValue}, 地图ID={parameters[1].IntValue}, X={parameters[2].IntValue}, Y={parameters[3].IntValue}, 范围={parameters[4].IntValue}, 数量={parameters[5].IntValue}, 目标X={parameters[6].IntValue}, 目标Y={parameters[7].IntValue}");
            return 1;
        }

        // 人在线命令
        private static uint ProcHumOnline(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"检查人在线: {parameters[0].StringValue}");
            return 0;
        }

        // 输入文本命令
        private static uint ProcInputText(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 3)
                return 0;

            Console.WriteLine($"输入文本: 提示={parameters[0].StringValue}, 长度={parameters[1].IntValue}, 页面={parameters[2].StringValue}");
            return 1;
        }

        // 有师傅命令
        private static uint ProcHasTeacher(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"检查是否有师傅");
            return 0;
        }

        // 有主人命令
        private static uint ProcHasMaster(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"检查是否有主人");
            return 0;
        }

        // 添加徒弟命令
        private static uint ProcAddStudent(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"添加徒弟: {parameters[0].StringValue}");
            return 1;
        }

        // 删除徒弟命令
        private static uint ProcDeleteStudent(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"删除徒弟: {parameters[0].StringValue}");
            return 1;
        }

        // 可以收徒命令
        private static uint ProcCanTakeStudent(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"检查是否可以收徒");
            return 1;
        }

        // 已结婚命令
        private static uint ProcIsMarried(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"检查是否已结婚");
            return 0;
        }

        // 远程调用命令
        private static uint ProcRemoteCall(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 2)
                return 0;

            Console.WriteLine($"远程调用: 目标={parameters[0].StringValue}, 命令={parameters[1].StringValue}");
            return 1;
        }

        // 拿走装备命令
        private static uint ProcTakeEquipment(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"拿走装备: {parameters[0].IntValue}");
            return 1;
        }

        // 拿走装备扩展命令
        private static uint ProcTakeEquipmentEx(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"拿走装备扩展: {parameters[0].StringValue}");
            return 1;
        }

        // 添加师傅声望命令
        private static uint ProcAddTeacherCredit(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"添加师傅声望: {parameters[0].IntValue}");
            return 1;
        }

        // 值命令
        private static uint ProcValueOf(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"值命令: {parameters[0].StringValue}");
            return 1;
        }

        // 调试模式命令
        private static uint ProcDebugMode(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"调试模式命令");
            return 1;
        }

        // 有徒弟命令
        private static uint ProcHasStudent(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"检查是否有徒弟");
            return 0;
        }

        // 扩展系统消息命令
        private static uint ProcSystemMsgEx(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"扩展系统消息: {parameters[0].StringValue}");
            return 1;
        }

        // 打开页面命令
        private static uint ProcOpenPage(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"打开页面: {parameters[0].StringValue}");
            return 1;
        }

        // 更新装备命令
        private static uint ProcUpdateEquipment(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"更新装备: {parameters[0].StringValue}");
            return 1;
        }

        // 显示怪物命令
        private static uint ProcShowMonster(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"显示怪物: {parameters[0].StringValue}");
            return 1;
        }

        // 变形命令
        private static uint ProcTransform(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"变形: {parameters[0].StringValue}");
            return 1;
        }

        // 字符串相等命令
        private static uint ProcStrEqu(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 2)
                return 0;

            bool result = parameters[0].StringValue.Equals(parameters[1].StringValue, StringComparison.Ordinal);
            return (uint)(result ? 1 : 0);
        }

        // 字符串相等（不区分大小写）命令
        private static uint ProcStrEquNoCase(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 2)
                return 0;

            bool result = parameters[0].StringValue.Equals(parameters[1].StringValue, StringComparison.OrdinalIgnoreCase);
            return (uint)(result ? 1 : 0);
        }

        // 字符串相等（长度）命令
        private static uint ProcStrEquLength(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 2)
                return 0;

            bool result = parameters[0].StringValue.Length == parameters[1].IntValue;
            return (uint)(result ? 1 : 0);
        }

        // 字符串相等（长度，不区分大小写）命令
        private static uint ProcStrEquLengthNoCase(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 2)
                return 0;

            bool result = parameters[0].StringValue.Length == parameters[1].IntValue;
            return (uint)(result ? 1 : 0);
        }

        // 保存变量到数据库命令
        private static uint ProcSaveVarToDb(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"保存变量到数据库: {parameters[0].StringValue}");
            return 1;
        }

        // 多重检查地图人数命令
        private static uint ProcMultiCheckMapHum(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount < 3)
                return 0;

            Console.WriteLine($"多重检查地图人数: 地图ID={parameters[0].IntValue}, 符号={parameters[1].StringValue}, 数量={parameters[2].IntValue}");
            return 0;
        }

        // 改变字体颜色命令
        private static uint ProcChangeFontColor(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"改变字体颜色: {parameters[0].IntValue}");
            return 1;
        }

        // 组移动命令
        private static uint ProcGroupMove(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"组移动: {parameters[0].StringValue}");
            return 1;
        }

        // 多重地图人物传送命令
        private static uint ProcMultiMapHumTeleport(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"多重地图人物传送: {parameters[0].StringValue}");
            return 1;
        }

        // 消息框命令
        private static uint ProcMsgBox(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"消息框: {parameters[0].StringValue}");
            return 1;
        }

        // 计时器开始命令
        private static uint ProcTimerStart(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"计时器开始: {parameters[0].StringValue}");
            return 1;
        }

        // 计时器停止命令
        private static uint ProcTimerStop(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"计时器停止: {parameters[0].StringValue}");
            return 1;
        }

        // 计时器超时命令
        private static uint ProcTimerTimeout(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"计时器超时: {parameters[0].StringValue}");
            return 1;
        }

        // 添加动态NPC命令
        private static uint ProcAddDynamicNpc(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"添加动态NPC: {parameters[0].StringValue}");
            return 1;
        }

        // 移除动态NPC命令
        private static uint ProcRemoveDynamicNpc(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"移除动态NPC: {parameters[0].StringValue}");
            return 1;
        }

        // 系统延迟命令
        private static uint ProcSystemDelay(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"系统延迟: {parameters[0].IntValue}");
            return 1;
        }

        // 显示字符串列表命令
        private static uint ProcShowStringList(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"显示字符串列表: {parameters[0].StringValue}");
            return 1;
        }

        // 获取字符串列表行数命令
        private static uint ProcGetStringListLines(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"获取字符串列表行数: {parameters[0].StringValue}");
            return 0;
        }

        // 有任务命令
        private static uint ProcHasTask(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"检查是否有任务: {parameters[0].StringValue}");
            return 0;
        }

        // 添加任务命令
        private static uint ProcAddTask(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"添加任务: {parameters[0].StringValue}");
            return 1;
        }

        // 移除任务命令
        private static uint ProcRemoveTask(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"移除任务: {parameters[0].StringValue}");
            return 1;
        }

        // 重新加载任务命令
        private static uint ProcReloadTask(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"重新加载任务: {parameters[0].StringValue}");
            return 1;
        }

        // 修改任务命令
        private static uint ProcModifyTask(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"修改任务: {parameters[0].StringValue}");
            return 1;
        }

        // 完成任务命令
        private static uint ProcCompleteTask(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"完成任务: {parameters[0].StringValue}");
            return 1;
        }

        // 检查任务步骤命令
        private static uint ProcCheckTaskStep(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"检查任务步骤: {parameters[0].StringValue}");
            return 0;
        }

        // 添加HP命令
        private static uint ProcAddHp(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"添加HP: {parameters[0].IntValue}");
            return 1;
        }

        // 重新加载物品限制命令
        private static uint ProcReloadItemLimit(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"重新加载物品限制");
            return 1;
        }

        // 重新加载物品脚本命令
        private static uint ProcReloadItemScript(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"重新加载物品脚本");
            return 1;
        }

        // 是第一个行会会长命令
        private static uint ProcIsFirstGuildMaster(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"检查是否是第一个行会会长");
            return 0;
        }

        // 格式化字符串命令
        private static uint ProcFormatString(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"格式化字符串: {parameters[0].StringValue}");
            return 1;
        }

        // 检查日期时间命令
        private static uint ProcCheckDateTime(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"检查日期时间: {parameters[0].StringValue}");
            return 0;
        }

        // 添加字符串列表命令
        private static uint ProcAddStringList(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"添加字符串列表: {parameters[0].StringValue}");
            return 1;
        }

        // 删除字符串列表命令
        private static uint ProcDelStringList(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"删除字符串列表: {parameters[0].StringValue}");
            return 1;
        }

        // 检查字符串列表命令
        private static uint ProcCheckStringList(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"检查字符串列表: {parameters[0].StringValue}");
            return 0;
        }

        // 清除字符串列表命令
        private static uint ProcClearStringList(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"清除字符串列表");
            return 1;
        }

        // 建造物品命令
        private static uint ProcBuildItem(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"建造物品: {parameters[0].StringValue}");
            return 1;
        }

        // 执行地图脚本命令
        private static uint ProcDoMapScript(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"执行地图脚本: {parameters[0].StringValue}");
            return 1;
        }

        // 有追踪物品命令
        private static uint ProcHasTracedItem(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"检查是否有追踪物品: {parameters[0].StringValue}");
            return 0;
        }

        // 发送行会求救命令
        private static uint ProcSendGuildSos(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"发送行会求救");
            return 1;
        }

        // 在安全区域命令
        private static uint ProcInSafeArea(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"检查是否在安全区域");
            return 0;
        }

        // 在城市区域命令
        private static uint ProcInCityArea(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"检查是否在城市区域");
            return 0;
        }

        // 在战争区域命令
        private static uint ProcInWarArea(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            Console.WriteLine($"检查是否在战争区域");
            return 0;
        }

        // 拿走锻造率命令
        private static uint ProcTakeForgeRate(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"拿走锻造率: {parameters[0].IntValue}");
            return 1;
        }

        // 添加锻造率命令
        private static uint ProcAddForgeRate(ScriptShell shell, ScriptTarget target, ScriptView view, ScriptParam[] parameters, uint paramCount, ref bool continueExecution)
        {
            if (paramCount == 0)
                return 0;

            Console.WriteLine($"添加锻造率: {parameters[0].IntValue}");
            return 1;
        }

        #endregion
    }
}
