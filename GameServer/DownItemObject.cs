using System;
using MirCommon.Utils;

namespace GameServer
{
    /// <summary>
    /// 掉落物品对象
    /// </summary>
    public class DownItemObject : MapObject
    {
        private HumanPlayer? _actionObject;
        private uint _actionObjectInstanceKey;
        private ItemClass? _itemClass;
        private uint _scriptTimes;
        private readonly ServerTimer _timer;
        private uint _ownerId;
        private ItemInstance _item;
        private uint _id;

        /// <summary>
        /// 构造函数
        /// </summary>
        public DownItemObject()
        {
            _id = 0;
            _timer = new ServerTimer();
            Clean();
        }

        /// <summary>
        /// 清理对象
        /// </summary>
        public void Clean()
        {
            _id = 0;
            _itemClass = null;
            _item = null!;
            SetActionObject(null);
            _scriptTimes = 0;
        }

        /// <summary>
        /// 获取物品
        /// </summary>
        public ItemInstance GetItem()
        {
            return _item;
        }

        /// <summary>
        /// 设置物品
        /// </summary>
        public void SetItem(ItemInstance item)
        {
            _item = item;
        }

        /// <summary>
        /// 获取ID
        /// </summary>
        public uint GetId()
        {
            return _id;
        }

        /// <summary>
        /// 设置ID
        /// </summary>
        public void SetId(uint id)
        {
            _id = id;
        }

        /// <summary>
        /// 获取所有者ID
        /// </summary>
        public uint GetOwnerId()
        {
            return _ownerId;
        }

        /// <summary>
        /// 设置所有者ID
        /// </summary>
        public void SetOwnerId(uint id)
        {
            _ownerId = id;
        }

        /// <summary>
        /// 获取对象类型
        /// </summary>
        public override ObjectType GetObjectType()
        {
            return ObjectType.DownItem;
        }

        /// <summary>
        /// 获取计时器
        /// </summary>
        public ServerTimer GetTimer()
        {
            return _timer;
        }

        /// <summary>
        /// 当物品掉落时调用
        /// </summary>
        public void OnDroped()
        {
            _timer.SaveTime();
            UpdateValid();
        }

        /// <summary>
        /// 设置动作对象
        /// </summary>
        public void SetActionObject(HumanPlayer? player)
        {
            _actionObject = player;
            if (player != null)
            {
                _actionObjectInstanceKey = player.InstanceKey;
            }
            else
            {
                _actionObjectInstanceKey = 0;
            }
        }

        /// <summary>
        /// 检查是否是金币
        /// </summary>
        public bool IsGold()
        {
            if (_item == null)
                return false;
            
            string itemName = _item.GetName() ?? string.Empty;
            return itemName.Contains("金币") || itemName.Contains("Gold");
        }

        /// <summary>
        /// 更新有效性
        /// </summary>
        public bool UpdateValid()
        {
            uint itemUpdateTime = 60000; // 默认60秒
            
            if (_timer.IsTimeOut(itemUpdateTime))
            {
                if (_ownerId == 0)
                {
                    // 删除物品
                    DownItemMgr.Instance?.DeleteGroundItem(CurrentMap as LogicMap, this);
                    return false;
                }
                else
                {
                    _ownerId = 0;
                    _timer.SaveTime();
                }
            }
            
            return true;
        }

        /// <summary>
        /// 当进入地图时调用
        /// </summary>
        protected override void OnEnterMap(LogicMap map)
        {
            base.OnEnterMap(map);
            
            LogManager.Default.Debug($"掉落物品进入地图: 地图={map.MapId}, 位置=({X},{Y}), 物品={_item?.GetName()}");
            
            _itemClass = new ItemClass();
        }

        /// <summary>
        /// 当离开地图时调用
        /// </summary>
        protected override void OnLeaveMap(LogicMap map)
        {
            LogManager.Default.Debug($"掉落物品离开地图: 地图={map.MapId}, 位置=({X},{Y}), 物品={_item?.GetName()}");
            
            base.OnLeaveMap(map);
        }

        /// <summary>
        /// 获取可视消息
        /// </summary>
        public override bool GetViewMsg(out byte[] msg, MapObject? viewer = null)
        {
            // 编码 SM_DOWNITEMAPPEAR 消息
            // 参数：dwFlag = m_Item.dwMakeIndex, wCmd = SM_DOWNITEMAPPEAR
            // w1 = m_wX, w2 = m_wY, w3 = m_Item.baseitem.wImageIndex
            // 负载数据：物品名称字符串
            
            if (_item == null)
            {
                msg = new byte[0];
                return false;
            }

            try
            {
                // 获取物品名称
                string itemName = _item.GetName() ?? string.Empty;
                byte[] nameBytes = System.Text.Encoding.GetEncoding("GBK").GetBytes(itemName);
                
                // 构建消息
                byte[] buffer = new byte[1024]; // 足够大的缓冲区
                int length = MirCommon.GameCodec.EncodeMsg(
                    buffer,
                    _item.GetMakeIndex(), // dwFlag
                    MirCommon.ProtocolCmd.SM_DOWNITEMAPPEAR, // wCmd
                    (ushort)X, // w1 = X坐标
                    (ushort)Y, // w2 = Y坐标
                    _item.GetImageIndex(), // w3 = 物品图片索引
                    nameBytes, // 负载数据：物品名称
                    nameBytes.Length // 数据大小
                );
                
                // 返回实际使用的部分
                msg = new byte[length];
                Array.Copy(buffer, 0, msg, 0, length);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取掉落物品可视消息失败: {ex.Message}");
                msg = new byte[0];
                return false;
            }
        }
        
        /// <summary>
        /// 获取离开视野消息
        /// </summary>
        public bool GetOutViewMsg(out byte[] msg, MapObject? viewer = null)
        {
            // 编码 SM_DOWNITEMDISAPPEAR 消息
            // 参数：dwFlag = m_Item.dwMakeIndex, wCmd = SM_DOWNITEMDISAPPEAR
            // w1 = m_wX, w2 = m_wY, w3 = 0, 无负载数据
            
            if (_item == null)
            {
                msg = new byte[0];
                return false;
            }

            try
            {
                byte[] buffer = new byte[1024];
                int length = MirCommon.GameCodec.EncodeMsg(
                    buffer,
                    _item.GetMakeIndex(), // dwFlag
                    MirCommon.ProtocolCmd.SM_DOWNITEMDISAPPEAR, // wCmd
                    (ushort)X, // w1 = X坐标
                    (ushort)Y, // w2 = Y坐标
                    0, // w3 = 0
                    null, // 无负载数据
                    0 // 数据大小为0
                );
                
                msg = new byte[length];
                Array.Copy(buffer, 0, msg, 0, length);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取掉落物品离开视野消息失败: {ex.Message}");
                msg = new byte[0];
                return false;
            }
        }

        /// <summary>
        /// 设置删除计时器
        /// </summary>
        public void SetDelTimer()
        {
            _timer.SetInterval(10000); // 10秒后删除
        }

        /// <summary>
        /// 检查删除计时器是否超时
        /// </summary>
        public bool IsDelTimerTimeOut(uint timeout)
        {
            return _timer.IsTimeOut(timeout);
        }
    }

    /// <summary>
    /// 物品类别
    /// </summary>
    public class ItemClass
    {
        public string DropPage { get; set; } = string.Empty;
        public uint DropPageDelay { get; set; }
        public uint DropPageExecuteTimes { get; set; }
        public string PickupPage { get; set; } = string.Empty;
    }
}
