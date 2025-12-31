using System;
using System.Collections.Generic;

namespace GameServer
{
    /// <summary>
    /// 怪物类定义
    /// </summary>
    public class MonsterClass
    {
        // 基础信息
        public MonsterBaseInfo Base { get; set; } = new MonsterBaseInfo();
        
        // 属性
        public MonsterProp Prop { get; set; } = new MonsterProp();
        
        // 特殊属性
        public MonsterSpecialProp SProp { get; set; } = new MonsterSpecialProp();
        
        // AI设置
        public MonsterAISet AISet { get; set; } = new MonsterAISet();
        
        // 宠物设置
        public MonsterPetSet PetSet { get; set; } = new MonsterPetSet();
        
        // 攻击描述
        public MonsterAttackDesc AttackDesc { get; set; } = new MonsterAttackDesc();
        
        // 变身设置（最多3个）
        public MonsterChangeInto[] ChangeInto { get; set; } = new MonsterChangeInto[3];
        
        // 脚本
        public string? BornScript { get; set; }
        public string? GotTargetScript { get; set; }
        public string? KillTargetScript { get; set; }
        public string? HurtScript { get; set; }
        public string? DeathScript { get; set; }
        
        // 掉落物品
        public object? DownItems { get; set; }
        
        // 计数
        public int Count { get; set; }
        
        public MonsterClass()
        {
            for (int i = 0; i < 3; i++)
            {
                ChangeInto[i] = new MonsterChangeInto();
            }
        }
    }

    /// <summary>
    /// 怪物基础信息
    /// </summary>
    public class MonsterBaseInfo
    {
        public string ClassName { get; set; } = string.Empty;  // 类名（16字符）
        public string ViewName { get; set; } = string.Empty;   // 显示名（16字符）
        public byte Race { get; set; }                         // 种族
        public byte Image { get; set; }                        // 形象
        public byte Level { get; set; }                        // 等级
        public byte NameColor { get; set; }                    // 名字颜色
        public uint Feature { get; set; }                      // 特征
        public int MonsterId { get; set; }                     // 怪物ID
    }

    /// <summary>
    /// 怪物属性
    /// </summary>
    public class MonsterProp
    {
        public ushort HP { get; set; }                         // 生命值
        public ushort MP { get; set; }                         // 魔法值
        public byte Hit { get; set; }                          // 命中
        public byte Speed { get; set; }                        // 速度
        public byte AC1 { get; set; }                          // 防御下限
        public byte AC2 { get; set; }                          // 防御上限
        public byte DC1 { get; set; }                          // 攻击下限
        public byte DC2 { get; set; }                          // 攻击上限
        public byte MAC1 { get; set; }                         // 魔防下限
        public byte MAC2 { get; set; }                         // 魔防上限
        public byte MC1 { get; set; }                          // 魔法下限
        public byte MC2 { get; set; }                          // 魔法上限
        public uint Exp { get; set; }                          // 经验值
        public ushort AIDelay { get; set; }                    // AI延迟
        public ushort WalkDelay { get; set; }                  // 行走延迟
        public ushort RecoverHP { get; set; }                  // 恢复生命
        public ushort RecoverHPTime { get; set; }              // 恢复生命时间
        public ushort RecoverMP { get; set; }                  // 恢复魔法
        public ushort RecoverMPTime { get; set; }              // 恢复魔法时间
    }

    /// <summary>
    /// 怪物特殊属性
    /// </summary>
    public class MonsterSpecialProp
    {
        public uint PFlag { get; set; }                        // 特殊标志
        public byte CallRate { get; set; }                     // 召唤率
        public byte AntSoulWall { get; set; }                  // 抗灵魂墙
        public byte AntTrouble { get; set; }                   // 抗困魔
        public byte AntHolyWord { get; set; }                  // 抗圣言
    }

    /// <summary>
    /// 怪物AI设置
    /// </summary>
    public class MonsterAISet
    {
        public byte MoveStyle { get; set; }                    // 移动方式
        public byte DieStyle { get; set; }                     // 死亡方式
        public byte TargetSelect { get; set; }                 // 目标选择
        public byte TargetFlag { get; set; }                   // 目标标志
        public byte ViewDistance { get; set; }                 // 视野距离
        public byte CoolEyes { get; set; }                     // 冷静之眼
        public byte EscapeDistance { get; set; }               // 逃跑距离
        public byte LockDir { get; set; }                      // 锁定方向
    }

    /// <summary>
    /// 怪物宠物设置
    /// </summary>
    public class MonsterPetSet
    {
        public byte Type { get; set; }                         // 类型
        public byte StopAt { get; set; }                       // 停止位置
    }

    /// <summary>
    /// 怪物攻击描述
    /// </summary>
    public class MonsterAttackDesc
    {
        public int AttackStyle { get; set; }                   // 攻击方式
        public int AttackDistance { get; set; }                // 攻击距离
        public int Delay { get; set; }                         // 延迟
        public int DamageStyle { get; set; }                   // 伤害方式
        public int DamageRange { get; set; }                   // 伤害范围
        public int DamageType { get; set; }                    // 伤害类型
        public int AppendEffect { get; set; }                  // 附加效果
        public int AppendRate { get; set; }                    // 附加率
        public int CostHP { get; set; }                        // 消耗生命
        public int CostMP { get; set; }                        // 消耗魔法
        public ushort Action { get; set; }                     // 动作
        public ushort AppendTime { get; set; }                 // 附加时间
    }

    /// <summary>
    /// 怪物变身设置
    /// </summary>
    public class MonsterChangeInto
    {
        public AttackSituation Situation1 { get; set; } = new AttackSituation();
        public AttackSituation Situation2 { get; set; } = new AttackSituation();
        public string ChangeInto { get; set; } = string.Empty; // 变身目标
        public int AppendEffect { get; set; }                  // 附加效果
        public bool Anim { get; set; }                         // 是否动画
        public bool Enabled { get; set; }                      // 是否启用
    }

    /// <summary>
    /// 攻击情境
    /// </summary>
    public class AttackSituation
    {
        public int Situation { get; set; }                     // 情境类型
        public int Param { get; set; }                         // 参数
    }

    /// <summary>
    /// 怪物生成器
    /// </summary>
    public class MonsterGen
    {
        public string MonsterName { get; set; } = string.Empty; // 怪物名称（32字符）
        public int MapId { get; set; }                         // 地图ID
        public int X { get; set; }                             // X坐标
        public int Y { get; set; }                             // Y坐标
        public int Range { get; set; }                         // 范围
        public int MaxCount { get; set; }                      // 最大数量
        public int RefreshDelay { get; set; }                  // 刷新延迟（毫秒）
        public int CurrentCount { get; set; }                  // 当前数量
        public int ErrorTime { get; set; }                     // 错误次数
        public DateTime LastRefreshTime { get; set; }          // 最后刷新时间
        public string? ScriptPage { get; set; }                // 脚本页面
        public bool StartWhenAllDead { get; set; }             // 全部死亡后开始

        public MonsterGen()
        {
            MonsterName = string.Empty;
            MapId = 0;
            X = 0;
            Y = 0;
            Range = 0;
            MaxCount = 0;
            RefreshDelay = 0;
            CurrentCount = 0;
            ErrorTime = 0;
            LastRefreshTime = DateTime.MinValue;
            ScriptPage = null;
            StartWhenAllDead = false;
        }
    }
}
