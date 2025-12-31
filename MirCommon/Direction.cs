namespace MirCommon
{
    /// <summary>
    /// 方向枚举（统一定义）
    /// 用于所有需要方向的场景：移动、攻击、视野等
    /// </summary>
    public enum Direction : byte
    {
        /// <summary>
        /// 上
        /// </summary>
        Up = 0,
        
        /// <summary>
        /// 右上
        /// </summary>
        UpRight = 1,
        
        /// <summary>
        /// 右
        /// </summary>
        Right = 2,
        
        /// <summary>
        /// 右下
        /// </summary>
        DownRight = 3,
        
        /// <summary>
        /// 下
        /// </summary>
        Down = 4,
        
        /// <summary>
        /// 左下
        /// </summary>
        DownLeft = 5,
        
        /// <summary>
        /// 左
        /// </summary>
        Left = 6,
        
        /// <summary>
        /// 左上
        /// </summary>
        UpLeft = 7
    }
}
