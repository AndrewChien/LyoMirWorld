using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MirCommon;
using MirCommon.Utils;

namespace GameServer
{
    /// <summary>
    /// 脚本对象管理器
    /// </summary>
    public class ScriptObjectMgr
    {
        private static ScriptObjectMgr? _instance;
        public static ScriptObjectMgr Instance => _instance ??= new ScriptObjectMgr();

        // 脚本对象缓存
        private readonly Dictionary<string, ScriptObject> _scriptObjects = new(StringComparer.OrdinalIgnoreCase);
        
        // 定义变量缓存
        private readonly Dictionary<string, string> _defineVariables = new(StringComparer.OrdinalIgnoreCase);

        private readonly object _lock = new();

        private ScriptObjectMgr()
        {
        }

        public bool HasLoaded = false;
        /// <summary>
        /// 加载脚本文件
        /// </summary>
        public void Load(string path)
        {
            if (!Directory.Exists(path))
            {
                Console.WriteLine($"脚本目录不存在: {path}");
                return;
            }

            lock (_lock)
            {
                // 加载脚本文件 (*.txt)
                var scriptFiles = Directory.GetFiles(path, "*.txt", SearchOption.AllDirectories);
                foreach (var file in scriptFiles)
                {
                    LoadScript(file);
                }

                // 加载定义文件 (*.def)
                var defineFiles = Directory.GetFiles(path, "*.def", SearchOption.AllDirectories);
                foreach (var file in defineFiles)
                {
                    LoadDefine(file);
                }

                Console.WriteLine($"从 {path} 加载了 {_scriptObjects.Count} 个脚本对象和 {_defineVariables.Count} 个定义变量");
            }

            HasLoaded = true;
        }

        /// <summary>
        /// 加载脚本文件
        /// </summary>
        private void LoadScript(string fileName)
        {
            try
            {
                var scriptObject = new ScriptObject();
                if (scriptObject.Load(fileName))
                {
                    string name = Path.GetFileNameWithoutExtension(fileName);
                    _scriptObjects[name] = scriptObject;
                    Console.WriteLine($"加载脚本: {name}");
                }
                else
                {
                    Console.WriteLine($"加载脚本失败: {fileName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载脚本文件异常 {fileName}: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载定义文件
        /// </summary>
        private void LoadDefine(string fileName)
        {
            try
            {
                var lines = SmartReader.ReadAllLines(fileName);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line) || !line.StartsWith("#define", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // 解析 #define 行
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        string variableName = parts[1];
                        string variableValue = parts[2];

                        // 移除可能的引号
                        if (variableValue.StartsWith("\"") && variableValue.EndsWith("\""))
                            variableValue = variableValue.Substring(1, variableValue.Length - 2);

                        if (_defineVariables.ContainsKey(variableName))
                        {
                            Console.WriteLine($"发现重定义在 {fileName} 文件的 {i + 1} 行: {variableName} 已经定义过值 [{_defineVariables[variableName]}]");
                        }
                        else
                        {
                            _defineVariables[variableName] = variableValue;
                        }
                    }
                }

                Console.WriteLine($"从 {fileName} 加载了定义变量");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载定义文件异常 {fileName}: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取脚本对象
        /// </summary>
        public ScriptObject? GetScriptObject(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            lock (_lock)
            {
                return _scriptObjects.TryGetValue(name, out var scriptObject) ? scriptObject : null;
            }
        }

        /// <summary>
        /// 获取变量值
        /// </summary>
        public string? GetVariableValue(string variableName)
        {
            if (string.IsNullOrEmpty(variableName))
                return null;

            lock (_lock)
            {
                return _defineVariables.TryGetValue(variableName, out var value) ? value : null;
            }
        }

        /// <summary>
        /// 获取所有脚本对象名称
        /// </summary>
        public List<string> GetAllScriptObjectNames()
        {
            lock (_lock)
            {
                return _scriptObjects.Keys.ToList();
            }
        }

        /// <summary>
        /// 获取所有定义变量
        /// </summary>
        public Dictionary<string, string> GetAllDefineVariables()
        {
            lock (_lock)
            {
                return new Dictionary<string, string>(_defineVariables);
            }
        }

        /// <summary>
        /// 获取脚本对象数量
        /// </summary>
        public int GetScriptObjectCount()
        {
            lock (_lock)
            {
                return _scriptObjects.Count;
            }
        }

        /// <summary>
        /// 获取定义变量数量
        /// </summary>
        public int GetDefineVariableCount()
        {
            lock (_lock)
            {
                return _defineVariables.Count;
            }
        }

        /// <summary>
        /// 清空所有缓存
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _scriptObjects.Clear();
                _defineVariables.Clear();
            }
        }
    }

    /// <summary>
    /// 脚本对象
    /// </summary>
    public class ScriptObject
    {
        public string Name { get; private set; } = string.Empty;
        public string FilePath { get; private set; } = string.Empty;
        public List<string> Lines { get; private set; } = new();
        public DateTime LastModified { get; private set; }

        /// <summary>
        /// 加载脚本文件
        /// </summary>
        public bool Load(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                FilePath = filePath;
                Name = Path.GetFileNameWithoutExtension(filePath);
                Lines = SmartReader.ReadAllLines(filePath).ToList();
                LastModified = File.GetLastWriteTime(filePath);

                Console.WriteLine($"脚本对象 {Name} 加载成功，共 {Lines.Count} 行");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载脚本对象失败 {filePath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 重新加载脚本文件
        /// </summary>
        public bool Reload()
        {
            if (string.IsNullOrEmpty(FilePath))
                return false;

            return Load(FilePath);
        }

        /// <summary>
        /// 检查是否需要重新加载
        /// </summary>
        public bool NeedReload()
        {
            if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath))
                return false;

            var currentModified = File.GetLastWriteTime(FilePath);
            return currentModified > LastModified;
        }

        /// <summary>
        /// 获取脚本内容
        /// </summary>
        public string GetContent()
        {
            return string.Join(Environment.NewLine, Lines);
        }

        /// <summary>
        /// 获取指定行
        /// </summary>
        public string? GetLine(int lineNumber)
        {
            if (lineNumber < 1 || lineNumber > Lines.Count)
                return null;

            return Lines[lineNumber - 1];
        }

        /// <summary>
        /// 查找包含指定文本的行
        /// </summary>
        public List<int> FindLinesContaining(string text)
        {
            var result = new List<int>();
            for (int i = 0; i < Lines.Count; i++)
            {
                if (Lines[i].Contains(text, StringComparison.OrdinalIgnoreCase))
                    result.Add(i + 1);
            }
            return result;
        }

        /// <summary>
        /// 执行脚本
        /// </summary>
        public void Execute()
        {
            Console.WriteLine($"执行脚本: {Name}");
            Console.WriteLine($"脚本内容: {GetContent()}");
        }

        /// <summary>
        /// 执行脚本（完整版本）
        /// </summary>
        public bool Execute(ScriptShell shell, ScriptTarget target, string pageName = "")
        {
            if (shell == null || target == null)
                return false;

            Console.WriteLine($"执行脚本: {Name}, 目标: {target.GetTargetName()}, 页面: {pageName}");

            try
            {
                // 设置当前脚本对象
                shell.SetVariable("_scriptname", Name);
                shell.SetVariable("_scriptfile", FilePath);

                // 如果指定了页面名，执行指定页面
                if (!string.IsNullOrEmpty(pageName))
                {
                    return ExecutePage(shell, target, pageName);
                }
                else
                {
                    // 否则执行整个脚本
                    return ExecuteAll(shell, target);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"执行脚本异常 {Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 执行指定页面
        /// </summary>
        private bool ExecutePage(ScriptShell shell, ScriptTarget target, string pageName)
        {
            bool inTargetPage = false;
            bool pageFound = false;

            for (int i = 0; i < Lines.Count; i++)
            {
                string line = Lines[i].Trim();
                
                // 检查页面标记
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    string page = line.Substring(1, line.Length - 2).Trim();
                    inTargetPage = page.Equals(pageName, StringComparison.OrdinalIgnoreCase);
                    if (inTargetPage)
                    {
                        pageFound = true;
                        Console.WriteLine($"找到页面: {pageName}");
                    }
                }
                else if (inTargetPage && !string.IsNullOrEmpty(line))
                {
                    // 跳过注释行
                    if (line.StartsWith(";") || line.StartsWith("//"))
                        continue;

                    // 执行命令
                    ExecuteLine(shell, target, line);
                }
            }

            if (!pageFound)
            {
                Console.WriteLine($"未找到页面: {pageName}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 执行整个脚本
        /// </summary>
        private bool ExecuteAll(ScriptShell shell, ScriptTarget target)
        {
            bool inPage = false;
            string currentPage = "";

            for (int i = 0; i < Lines.Count; i++)
            {
                string line = Lines[i].Trim();
                
                // 检查页面标记
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    string page = line.Substring(1, line.Length - 2).Trim();
                    inPage = true;
                    currentPage = page;
                    Console.WriteLine($"进入页面: {page}");
                }
                else if (inPage && !string.IsNullOrEmpty(line))
                {
                    // 跳过注释行
                    if (line.StartsWith(";") || line.StartsWith("//"))
                        continue;

                    // 执行命令
                    ExecuteLine(shell, target, line);
                }
            }

            return true;
        }

        /// <summary>
        /// 执行单行命令
        /// </summary>
        private void ExecuteLine(ScriptShell shell, ScriptTarget target, string line)
        {
            try
            {
                // 解析命令
                var parsedCommand = ParseCommand(line);
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
                uint result = proc(shell, target, null, scriptParams, (uint)parsedCommand.Parameters.Count, ref continueExecution);
                
                Console.WriteLine($"命令执行: {parsedCommand.CommandName} = {result}");
                
                // 设置返回值
                shell.SetVariable("_return", result.ToString());
                shell.SetVariable("_lastresult", result.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"执行命令异常: {line}, 错误: {ex.Message}");
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
    }
}
