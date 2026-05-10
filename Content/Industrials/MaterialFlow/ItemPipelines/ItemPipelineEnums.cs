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
    /// 管道内传输中的物品数据
    /// 使用值类型 + 紧凑布局，按值在管道之间传递；
    /// 不再持有固定的目标位置，目标在每次到达分叉时由路由表动态决定，
    /// 这样能彻底避免目标失效后物品被困死的旧问题
    /// </summary>
    public struct TransportingItem
    {
        /// <summary>每段管道的默认推进速度(进度/帧)</summary>
        public const float DefaultSpeed = 0.2f;

        /// <summary>物品类型ID</summary>
        public int ItemType;
        /// <summary>物品数量</summary>
        public int Stack;
        /// <summary>物品前缀</summary>
        public int Prefix;
        /// <summary>当前段进度(0~1)</summary>
        public float Progress;
        /// <summary>每帧推进量</summary>
        public float Speed;
        /// <summary>来源方向(0上1下2左3右), -1 表示无来源(刚被抽取)</summary>
        public sbyte SourceDirection;
        /// <summary>本物品在网络中已经发生过反向回流的次数, 用于抑制反复振荡</summary>
        public byte ReverseHops;

        public TransportingItem(int itemType, int stack, int prefix = 0) {
            ItemType = itemType;
            Stack = stack;
            Prefix = prefix;
            Progress = 0f;
            Speed = DefaultSpeed;
            SourceDirection = -1;
            ReverseHops = 0;
        }

        public readonly bool IsValid => ItemType > 0 && Stack > 0;
    }
}
