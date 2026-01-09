namespace CalamityOverhaul.Content.Industrials.MaterialFlow.ItemPipelines
{
    /// <summary>
    /// 物流管道的端点模式
    /// </summary>
    public enum ItemPipelineMode
    {
        /// <summary>
        /// 普通模式，只作为传输通道
        /// </summary>
        Normal,
        /// <summary>
        /// 输出模式，从连接的存储中抽取物品
        /// </summary>
        Output,
        /// <summary>
        /// 输入模式，向连接的存储中输入物品
        /// </summary>
        Input
    }

    /// <summary>
    /// 物流管道连接的目标类型
    /// </summary>
    public enum ItemPipelineLinkType
    {
        /// <summary>
        /// 无连接
        /// </summary>
        None,
        /// <summary>
        /// 连接到另一个物流管道
        /// </summary>
        Pipeline,
        /// <summary>
        /// 连接到存储容器
        /// </summary>
        Storage
    }

    /// <summary>
    /// 物流管道的几何形状(复用电力管道的形状定义)
    /// </summary>
    public enum ItemPipelineShape
    {
        /// <summary>
        /// 端点(连接0个或1个其他管道)
        /// </summary>
        Endpoint,
        /// <summary>
        /// 直线
        /// </summary>
        Straight,
        /// <summary>
        /// 拐角
        /// </summary>
        Corner,
        /// <summary>
        /// 三通
        /// </summary>
        ThreeWay,
        /// <summary>
        /// 十字交叉
        /// </summary>
        Cross
    }

    /// <summary>
    /// 管道内传输的物品数据
    /// </summary>
    public struct TransportingItem
    {
        /// <summary>
        /// 物品类型ID
        /// </summary>
        public int ItemType;
        /// <summary>
        /// 物品数量
        /// </summary>
        public int Stack;
        /// <summary>
        /// 物品前缀
        /// </summary>
        public int Prefix;
        /// <summary>
        /// 传输进度(0到1，表示在当前管道段中的位置)
        /// </summary>
        public float Progress;
        /// <summary>
        /// 传输速度(每帧移动的进度)
        /// </summary>
        public float Speed;
        /// <summary>
        /// 目标输入点位置(用于寻路)
        /// </summary>
        public Terraria.DataStructures.Point16 TargetPosition;
        /// <summary>
        /// 来源方向索引(0上1下2左3右，用于避免物品回头)
        /// </summary>
        public int SourceDirection;

        public TransportingItem(int itemType, int stack, int prefix = 0) {
            ItemType = itemType;
            Stack = stack;
            Prefix = prefix;
            Progress = 0f;
            Speed = 0.1f;//默认速度
            TargetPosition = Terraria.DataStructures.Point16.NegativeOne;
            SourceDirection = -1;
        }

        public readonly bool IsValid => ItemType > 0 && Stack > 0;
    }
}
