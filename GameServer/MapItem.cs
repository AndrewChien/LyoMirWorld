using System;

namespace GameServer
{
    /// <summary>
    /// 地图上的物品对象
    /// </summary>
    public class MapItem : MapObject
    {
        /// <summary>
        /// 物品实例
        /// </summary>
        public ItemInstance Item { get; set; }
        
        /// <summary>
        /// 物品所有者玩家ID
        /// </summary>
        public uint OwnerPlayerId { get; set; }
        
        /// <summary>
        /// 物品掉落时间
        /// </summary>
        public DateTime DropTime { get; set; }
        
        /// <summary>
        /// 物品过期时间
        /// </summary>
        public DateTime? ExpireTime { get; set; }
        
        /// <summary>
        /// 是否可以被拾取
        /// </summary>
        public bool CanBePicked { get; set; } = true;
        public int ProtectTime { get; internal set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public MapItem(ItemInstance item)
        {
            Item = item;
            DropTime = DateTime.Now;
            // 默认30分钟后过期
            ExpireTime = DropTime.AddMinutes(30);
        }
        
        /// <summary>
        /// 检查是否可以拾取
        /// </summary>
        public bool CanPickup(uint playerId)
        {
            if (!CanBePicked)
                return false;
                
            // 如果物品有所有者，需要检查拾取权限
            if (OwnerPlayerId > 0 && OwnerPlayerId != playerId)
            {
                // 检查是否过了保护时间（比如30秒）
                if (DateTime.Now < DropTime.AddSeconds(30))
                    return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// 获取对象类型
        /// </summary>
        public override ObjectType GetObjectType()
        {
            return ObjectType.Item;
        }
        
        /// <summary>
        /// 获取可视消息
        /// </summary>
        public override bool GetViewMsg(out byte[] msg, MapObject? viewer = null)
        {
            // 地图物品需要发送可视消息
            msg = Array.Empty<byte>();
            return false;
        }
        
        /// <summary>
        /// 更新物品状态
        /// </summary>
        public override void Update()
        {
            base.Update();
            
            // 检查是否过期
            if (ExpireTime.HasValue && DateTime.Now >= ExpireTime.Value)
            {
                // 从地图移除
                CurrentMap?.RemoveObject(this);
            }
        }

        internal int GetRemainingProtectTime()
        {
            throw new NotImplementedException();
        }
    }
}
