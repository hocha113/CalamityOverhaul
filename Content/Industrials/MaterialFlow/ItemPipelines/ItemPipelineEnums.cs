namespace CalamityOverhaul.Content.Industrials.MaterialFlow.ItemPipelines
{
    /// <summary>物流管道端点模式</summary>
    public enum ItemPipelineMode
    {
        /// <summary>普通通道</summary>
        Normal,
        /// <summary>从存储抽取</summary>
        Output,
        /// <summary>向存储存入</summary>
        Input
    }

    /// <summary>物流管道连接目标类型</summary>
    public enum ItemPipelineLinkType
    {
        /// <summary>无连接</summary>
        None,
        /// <summary>邻接物流管道</summary>
        Pipeline,
        /// <summary>存储容器</summary>
        Storage
    }

    /// <summary>物流管道几何形状</summary>
    public enum ItemPipelineShape
    {
        /// <summary>端点</summary>
        Endpoint,
        /// <summary>直线</summary>
        Straight,
        /// <summary>拐角</summary>
        Corner,
        /// <summary>三通</summary>
        ThreeWay,
        /// <summary>十字</summary>
        Cross
    }

    /// <summary>管道内传输物品，分叉时由路由表动态选路</summary>
    public struct TransportingItem
    {
        /// <summary>每段默认推进速度(进度/帧)</summary>
        public const float DefaultSpeed = 0.2f;

        /// <summary>物品类型 ID</summary>
        public int ItemType;
        /// <summary>堆叠数</summary>
        public int Stack;
        /// <summary>前缀</summary>
        public int Prefix;
        /// <summary>当前段进度 0~1</summary>
        public float Progress;
        /// <summary>每帧推进量</summary>
        public float Speed;
        /// <summary>来源方向 0上1下2左3右，-1 为刚抽取</summary>
        public sbyte SourceDirection;
        /// <summary>已反向回流次数，抑制振荡</summary>
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
