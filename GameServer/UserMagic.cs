using System;

namespace GameServer
{
    /// <summary>
    /// 用户魔法技能结构
    /// </summary>
    public class UserMagic
    {
        public Magic Magic { get; set; }
        public MagicClass? Class { get; set; }
        public UserMagic? Next { get; set; }
        public DateTime LastUseTime { get; set; }
        public uint Flag { get; set; }
        public int AddPower { get; set; }
        public uint Color { get; set; }

        public UserMagic()
        {
            Magic = new Magic();
            Class = null;
            Next = null;
            LastUseTime = DateTime.Now;
            Flag = 0;
            AddPower = 0;
            Color = 0;
        }

        public UserMagic(Magic magic, MagicClass? magicClass = null)
        {
            Magic = magic;
            Class = magicClass;
            Next = null;
            LastUseTime = DateTime.Now;
            Flag = 0;
            AddPower = 0;
            Color = 0;
        }

        /// <summary>
        /// 检查是否已激活
        /// </summary>
        public bool IsActivated()
        {
            return (Flag & 0x80000000) != 0; // USERMAGICFLAG_ACTIVED
        }

        /// <summary>
        /// 设置激活状态
        /// </summary>
        public void SetActivated(bool activated)
        {
            if (activated)
                Flag |= 0x80000000;
            else
                Flag &= ~0x80000000u;
        }

        /// <summary>
        /// 检查是否可以使用（冷却时间已过）
        /// </summary>
        public bool CanUse(uint delayTime = 0)
        {
            if (Class == null)
                return false;

            uint elapsed = (uint)(DateTime.Now - LastUseTime).TotalMilliseconds;
            return elapsed >= delayTime;
        }

        /// <summary>
        /// 记录使用时间
        /// </summary>
        public void RecordUse()
        {
            LastUseTime = DateTime.Now;
        }

        /// <summary>
        /// 获取剩余冷却时间（毫秒）
        /// </summary>
        public uint GetCooldownRemaining(uint delayTime = 0)
        {
            if (Class == null)
                return 0;

            uint elapsed = (uint)(DateTime.Now - LastUseTime).TotalMilliseconds;
            if (elapsed >= delayTime)
                return 0;
            return delayTime - elapsed;
        }

        public override string ToString()
        {
            string magicName = Magic?.szName ?? "Unknown";
            string className = Class?.szName ?? "Unknown";
            return $"UserMagic[{magicName}, Class={className}, Level={Magic?.btLevel}, Activated={IsActivated()}]";
        }
    }

    /// <summary>
    /// 用户魔法技能链表
    /// </summary>
    public class UserMagicList
    {
        public UserMagic? Head { get; set; }
        public int Count { get; private set; }

        public UserMagicList()
        {
            Head = null;
            Count = 0;
        }

        /// <summary>
        /// 添加魔法到链表
        /// </summary>
        public void Add(UserMagic userMagic)
        {
            if (Head == null)
            {
                Head = userMagic;
            }
            else
            {
                userMagic.Next = Head;
                Head = userMagic;
            }
            Count++;
        }

        /// <summary>
        /// 移除指定魔法
        /// </summary>
        public bool Remove(UserMagic userMagic)
        {
            if (Head == null)
                return false;

            if (Head == userMagic)
            {
                Head = Head.Next;
                Count--;
                return true;
            }

            UserMagic? current = Head;
            while (current != null && current.Next != userMagic)
            {
                current = current.Next;
            }

            if (current != null && current.Next == userMagic)
            {
                current.Next = current.Next?.Next;
                Count--;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 根据魔法ID查找
        /// </summary>
        public UserMagic? FindById(ushort magicId)
        {
            UserMagic? current = Head;
            while (current != null)
            {
                if (current.Magic.wId == magicId)
                    return current;
                current = current.Next;
            }
            return null;
        }

        /// <summary>
        /// 根据魔法名称查找
        /// </summary>
        public UserMagic? FindByName(string magicName)
        {
            UserMagic? current = Head;
            while (current != null)
            {
                if (current.Magic.szName == magicName)
                    return current;
                current = current.Next;
            }
            return null;
        }

        /// <summary>
        /// 清空链表
        /// </summary>
        public void Clear()
        {
            Head = null;
            Count = 0;
        }

        /// <summary>
        /// 转换为数组
        /// </summary>
        public UserMagic[] ToArray()
        {
            UserMagic[] array = new UserMagic[Count];
            UserMagic? current = Head;
            int index = 0;
            while (current != null)
            {
                array[index++] = current;
                current = current.Next;
            }
            return array;
        }
    }
}
