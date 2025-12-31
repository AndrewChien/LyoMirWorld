using System;

namespace GameServer
{
    /// <summary>
    /// 脚本视图
    /// </summary>
    public class ScriptView
    {
        protected ScriptShell? _shell;
        protected byte[] _scriptPacket = Array.Empty<byte>();
        protected uint _param;
        protected uint _pageSize;

        /// <summary>
        /// 构造函数
        /// </summary>
        public ScriptView(ScriptShell? shell = null)
        {
            _shell = shell;
            _param = 0;
            _pageSize = 0;
        }

        /// <summary>
        /// 添加文本到视图
        /// </summary>
        public virtual bool AppendWords(string words)
        {
            Console.WriteLine($"脚本视图添加文本: {words}");
            return true;
        }

        /// <summary>
        /// 添加格式化文本到视图
        /// </summary>
        public bool AppendWordsEx(string format, params object[] args)
        {
            string words = string.Format(format, args);
            return AppendWords(words);
        }

        /// <summary>
        /// 发送页面到目标
        /// </summary>
        public virtual void SendPageToTarget(ScriptTarget target, uint param = 0)
        {
            Console.WriteLine($"发送页面到目标: {target.GetTargetName()}, 参数: {param}");
        }

        /// <summary>
        /// 发送关闭页面到目标
        /// </summary>
        public virtual void SendClosePageToTarget(ScriptTarget target)
        {
            Console.WriteLine($"发送关闭页面到目标: {target.GetTargetName()}");
        }

        /// <summary>
        /// 更改脚本shell
        /// </summary>
        public virtual void ChangeShell(ScriptShell shell)
        {
            _shell = shell;
        }

        /// <summary>
        /// 清空视图
        /// </summary>
        public void Clear()
        {
            _scriptPacket = Array.Empty<byte>();
            _param = 0;
            _pageSize = 0;
        }

        /// <summary>
        /// 获取数据包
        /// </summary>
        public byte[] GetPacket()
        {
            return _scriptPacket;
        }

        /// <summary>
        /// 获取参数
        /// </summary>
        public uint GetParam()
        {
            return _param;
        }

        /// <summary>
        /// 获取页面大小
        /// </summary>
        public uint GetSize()
        {
            return _pageSize;
        }

        /// <summary>
        /// 设置数据包
        /// </summary>
        public void SetPacket(byte[] packet)
        {
            _scriptPacket = packet;
        }

        /// <summary>
        /// 设置参数
        /// </summary>
        public void SetParam(uint param)
        {
            _param = param;
        }

        /// <summary>
        /// 设置页面大小
        /// </summary>
        public void SetPageSize(uint pageSize)
        {
            _pageSize = pageSize;
        }
    }

    /// <summary>
    /// 脚本页面视图
    /// </summary>
    public class ScriptPageView : ScriptView
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        public ScriptPageView(ScriptShell? shell = null) : base(shell)
        {
        }

        /// <summary>
        /// 添加文本到视图
        /// </summary>
        public override bool AppendWords(string words)
        {
            Console.WriteLine($"脚本页面视图添加文本: {words}");
            // 这里应该实现具体的页面文本添加逻辑
            return true;
        }

        /// <summary>
        /// 发送页面到目标
        /// </summary>
        public override void SendPageToTarget(ScriptTarget target, uint param = 0)
        {
            Console.WriteLine($"脚本页面视图发送页面到目标: {target.GetTargetName()}, 参数: {param}");
            // 这里应该实现具体的页面发送逻辑
        }

        /// <summary>
        /// 发送关闭页面到目标
        /// </summary>
        public override void SendClosePageToTarget(ScriptTarget target)
        {
            Console.WriteLine($"脚本页面视图发送关闭页面到目标: {target.GetTargetName()}");
            // 这里应该实现具体的关闭页面逻辑
        }

        /// <summary>
        /// 更改脚本shell
        /// </summary>
        public override void ChangeShell(ScriptShell shell)
        {
            base.ChangeShell(shell);
            Console.WriteLine($"脚本页面视图更改shell");
        }
    }
}
