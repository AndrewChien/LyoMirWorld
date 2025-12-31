using System;
using System.Collections.Generic;

namespace GameServer
{
    /// <summary>
    /// 系统脚本
    /// </summary>
    public class SystemScript : ScriptShell
    {
        private static SystemScript? _instance;
        public static SystemScript Instance => _instance ??= new SystemScript();

        private ScriptObject? _scriptObject;

        /// <summary>
        /// 私有构造函数
        /// </summary>
        private SystemScript()
        {
            _scriptObject = null;
        }

        /// <summary>
        /// 初始化系统脚本
        /// </summary>
        public bool Init(ScriptObject? scriptObject)
        {
            _scriptObject = scriptObject;
            
            if (_scriptObject != null)
            {
                Console.WriteLine($"系统脚本初始化成功: {_scriptObject.Name}");
                return true;
            }
            else
            {
                Console.WriteLine("系统脚本初始化: 使用空脚本对象");
                return true;
            }
        }

        /// <summary>
        /// 获取系统脚本对象
        /// </summary>
        public ScriptObject? GetScriptObject()
        {
            return _scriptObject;
        }

        /// <summary>
        /// 执行系统脚本
        /// </summary>
        public void ExecuteSystemScript(string scriptName, ScriptTarget? target = null)
        {
            if (_scriptObject == null)
            {
                // 尝试从ScriptObjectMgr获取系统脚本
                var scriptObj = ScriptObjectMgr.Instance.GetScriptObject(scriptName);
                if (scriptObj != null)
                {
                    _scriptObject = scriptObj;
                }
                else
                {
                    Console.WriteLine($"系统脚本 {scriptName} 不存在");
                    return;
                }
            }

            Console.WriteLine($"执行系统脚本: {scriptName}");
            
            // 执行脚本逻辑
            if (target != null)
            {
                Execute(target, _scriptObject);
            }
            else
            {
                // 如果没有指定目标，只执行脚本内容
                _scriptObject.Execute();
            }
        }

        /// <summary>
        /// 执行登录脚本
        /// </summary>
        public void ExecuteLoginScript(ScriptTarget target)
        {
            ExecuteSystemScript("system.login", target);
        }

        /// <summary>
        /// 执行升级脚本
        /// </summary>
        public void ExecuteLevelUpScript(ScriptTarget target)
        {
            ExecuteSystemScript("system.levelup", target);
        }

        /// <summary>
        /// 执行登出脚本
        /// </summary>
        public void ExecuteLogoutScript(ScriptTarget target)
        {
            ExecuteSystemScript("system.logout", target);
        }

        /// <summary>
        /// 重新加载系统脚本
        /// </summary>
        public void Reload()
        {
            if (_scriptObject != null)
            {
                _scriptObject.Reload();
                Console.WriteLine($"系统脚本已重新加载: {_scriptObject.Name}");
            }
        }
    }

    /// <summary>
    /// 脚本执行引擎基类 
    /// </summary>
    public class ScriptShell
    {
        protected readonly Dictionary<string, string> _variables = new(StringComparer.OrdinalIgnoreCase);
        protected ScriptObject? _currentScriptObject;
        protected ExecuteResult _executeResult = ExecuteResult.Ok;
        protected string _resultValue = string.Empty;

        /// <summary>
        /// 执行结果枚举
        /// </summary>
        public enum ExecuteResult
        {
            Ok,
            Close,
            Return,
            Break
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public ScriptShell()
        {
            _currentScriptObject = null;
            InitializeDefaultVariables();
        }

        /// <summary>
        /// 初始化默认变量
        /// </summary>
        private void InitializeDefaultVariables()
        {
            // 添加默认系统变量
            _variables["_return"] = "0";
            _variables["_p1"] = "0";
            _variables["_p2"] = "0";
            _variables["_p3"] = "0";
            _variables["_p4"] = "0";
            _variables["returnvalue"] = "0";
        }

        /// <summary>
        /// 执行脚本
        /// </summary>
        public bool Execute(ScriptTarget target, ScriptObject scriptObject)
        {
            if (scriptObject == null)
                return false;

            _currentScriptObject = scriptObject;
            _executeResult = ExecuteResult.Ok;
            
            Console.WriteLine($"脚本执行开始: {scriptObject.Name}, 目标: {target.GetTargetName()}");

            try
            {
                // 这里应该实现完整的脚本执行逻辑
                scriptObject.Execute();
                
                // 设置执行结果
                SetExecuteResult(ExecuteResult.Ok, "0");
                
                Console.WriteLine($"脚本执行完成: {scriptObject.Name}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"脚本执行异常 {scriptObject.Name}: {ex.Message}");
                SetExecuteResult(ExecuteResult.Close, "0");
                return false;
            }
        }

        /// <summary>
        /// 执行指定页面
        /// </summary>
        public bool Execute(ScriptTarget target, string pageName)
        {
            if (_currentScriptObject == null)
                return false;

            Console.WriteLine($"执行脚本页面: {pageName}, 目标: {target.GetTargetName()}");
            
            var lines = _currentScriptObject.Lines;
            bool inTargetPage = false;
            
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i].Trim();
                
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    string page = line.Substring(1, line.Length - 2).Trim();
                    inTargetPage = page.Equals(pageName, StringComparison.OrdinalIgnoreCase);
                }
                else if (inTargetPage && !string.IsNullOrEmpty(line))
                {
                    // 执行页面中的命令
                    ExecuteCommand(line, target);
                }
            }
            
            return true;
        }

        /// <summary>
        /// 执行单条命令
        /// </summary>
        private void ExecuteCommand(string command, ScriptTarget target)
        {
            if (string.IsNullOrEmpty(command))
                return;

            // 解析命令
            var parsedCommand = ParseCommand(command);
            if (parsedCommand == null)
                return;

            // 获取命令处理器
            var proc = CommandManager.Instance.GetCommandProc(parsedCommand.CommandName);
            if (proc == null)
            {
                Console.WriteLine($"未知命令: {parsedCommand.CommandName}");
                return;
            }

            // 准备参数
            var scriptParams = new ScriptParam[parsedCommand.Parameters.Count];
            for (int i = 0; i < parsedCommand.Parameters.Count; i++)
            {
                scriptParams[i] = new ScriptParam
                {
                    StringValue = parsedCommand.Parameters[i],
                    IntValue = TryParseInt(parsedCommand.Parameters[i])
                };
            }

            // 执行命令
            bool continueExecution = true;
            try
            {
                uint result = proc(this, target, null, scriptParams, (uint)parsedCommand.Parameters.Count, ref continueExecution);
                Console.WriteLine($"命令执行结果: {parsedCommand.CommandName} = {result}");
                
                // 设置返回值
                SetVariable("_return", result.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"命令执行异常 {parsedCommand.CommandName}: {ex.Message}");
            }
        }

        /// <summary>
        /// 解析命令字符串
        /// </summary>
        private ParsedCommand? ParseCommand(string command)
        {
            if (string.IsNullOrEmpty(command))
                return null;

            command = command.Trim();
            if (string.IsNullOrEmpty(command))
                return null;

            // 分割命令和参数
            var parts = new List<string>();
            bool inQuotes = false;
            string currentPart = string.Empty;

            for (int i = 0; i < command.Length; i++)
            {
                char c = command[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    if (!inQuotes)
                    {
                        parts.Add(currentPart);
                        currentPart = string.Empty;
                    }
                }
                else if (c == ' ' && !inQuotes)
                {
                    if (!string.IsNullOrEmpty(currentPart))
                    {
                        parts.Add(currentPart);
                        currentPart = string.Empty;
                    }
                }
                else
                {
                    currentPart += c;
                }
            }

            if (!string.IsNullOrEmpty(currentPart))
            {
                parts.Add(currentPart);
            }

            if (parts.Count == 0)
                return null;

            var parsedCommand = new ParsedCommand
            {
                CommandName = parts[0].ToUpper(),
                Parameters = new List<string>()
            };

            for (int i = 1; i < parts.Count; i++)
            {
                parsedCommand.Parameters.Add(parts[i]);
            }

            return parsedCommand;
        }

        /// <summary>
        /// 尝试解析整数
        /// </summary>
        private int TryParseInt(string value)
        {
            if (int.TryParse(value, out int result))
                return result;
            return 0;
        }

        /// <summary>
        /// 解析的命令结构
        /// </summary>
        private class ParsedCommand
        {
            public string CommandName { get; set; } = string.Empty;
            public List<string> Parameters { get; set; } = new List<string>();
        }

        /// <summary>
        /// 获取变量值
        /// </summary>
        public string? GetVariableValue(string variableName)
        {
            if (string.IsNullOrEmpty(variableName))
                return null;

            return _variables.TryGetValue(variableName, out var value) ? value : null;
        }

        /// <summary>
        /// 设置变量值
        /// </summary>
        public void SetVariable(string variableName, string value)
        {
            if (string.IsNullOrEmpty(variableName))
                return;

            _variables[variableName] = value;
        }

        /// <summary>
        /// 设置执行结果
        /// </summary>
        public void SetExecuteResult(ExecuteResult result, string value = "0")
        {
            _executeResult = result;
            _resultValue = value;
            SetVariable("returnvalue", value);
        }

        /// <summary>
        /// 获取执行结果
        /// </summary>
        public ExecuteResult GetExecuteResult()
        {
            return _executeResult;
        }

        /// <summary>
        /// 获取执行结果值
        /// </summary>
        public string GetExecuteResultValue()
        {
            return _resultValue;
        }

        /// <summary>
        /// 获取当前脚本对象
        /// </summary>
        public ScriptObject? GetCurrentScriptObject()
        {
            return _currentScriptObject;
        }

        /// <summary>
        /// 获取所有变量
        /// </summary>
        public Dictionary<string, string> GetAllVariables()
        {
            return new Dictionary<string, string>(_variables);
        }

        /// <summary>
        /// 清空变量
        /// </summary>
        public void ClearVariables()
        {
            _variables.Clear();
            InitializeDefaultVariables();
        }
    }

    /// <summary>
    /// 脚本目标接口
    /// </summary>
    public interface ScriptTarget
    {
        /// <summary>
        /// 获取目标名称
        /// </summary>
        string GetTargetName();
        
        /// <summary>
        /// 获取目标ID
        /// </summary>
        uint GetTargetId();
        
        /// <summary>
        /// 执行脚本动作
        /// </summary>
        void ExecuteScriptAction(string action, params string[] parameters);
    }

    /// <summary>
    /// 基础脚本目标实现
    /// </summary>
    public class BaseScriptTarget : ScriptTarget
    {
        private readonly string _name;
        private readonly uint _id;

        public BaseScriptTarget(string name, uint id)
        {
            _name = name;
            _id = id;
        }

        public string GetTargetName()
        {
            return _name;
        }

        public uint GetTargetId()
        {
            return _id;
        }

        public void ExecuteScriptAction(string action, params string[] parameters)
        {
            Console.WriteLine($"脚本动作: {action}, 参数: {string.Join(", ", parameters)}");
            // 这里应该实现具体的动作执行逻辑
        }
    }
}
