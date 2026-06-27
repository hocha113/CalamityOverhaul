using CalamityOverhaul.Content.Industrials.ElectricPowers;
using InnoVault.Concurrent;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.MaterialFlow.ItemPipelines
{
    /// <summary>物流管道 TP，路由选路+反压抽取+卡死自愈+8帧侧扫</summary>
    [VaultLoaden(CWRConstant.Asset + "MaterialFlow")]
    internal class ItemPipelineTP : TileProcessor, ICWRLoader
    {
        #region 资源和本地化
        public override int TargetTileID => ModContent.TileType<ItemPipelineTile>();
        public static Asset<Texture2D> Pipeline { get; private set; }
        public static Asset<Texture2D> PipelineSide { get; private set; }
        public static Asset<Texture2D> PipelineCorner { get; private set; }
        public static Asset<Texture2D> PipelineCornerSide { get; private set; }
        public static Asset<Texture2D> PipelineCross { get; private set; }
        public static Asset<Texture2D> PipelineCrossSide { get; private set; }
        public static Asset<Texture2D> PipelineChannel { get; private set; }
        public static Asset<Texture2D> PipelineChannelSide { get; private set; }
        public static Asset<Texture2D> PipelineThreeCrutches { get; private set; }
        public static Asset<Texture2D> PipelineThreeCrutchesSide { get; private set; }

        public static LocalizedText ModeNormalText { get; private set; }
        public static LocalizedText ModeOutputText { get; private set; }
        public static LocalizedText ModeInputText { get; private set; }
        public static LocalizedText NotEndpointHintText { get; private set; }

        void ICWRLoader.SetupData() {
            ModeNormalText = Language.GetOrRegister($"Mods.CalamityOverhaul.UI.ItemPipeline.Normal", () => "普通");
            ModeOutputText = Language.GetOrRegister($"Mods.CalamityOverhaul.UI.ItemPipeline.Output", () => "输出");
            ModeInputText = Language.GetOrRegister($"Mods.CalamityOverhaul.UI.ItemPipeline.Input", () => "输入");
            NotEndpointHintText = Language.GetOrRegister($"Mods.CalamityOverhaul.UI.ItemPipeline.NotEndpointHint", () => "只能在管道末端设置输入输出");
        }

        void ICWRLoader.UnLoadData() {
            ItemPipelineNetwork.Clear();
        }
        #endregion

        #region 形状查找表
        private const int UP = 1, DOWN = 2, LEFT = 4, RIGHT = 8;
        private static readonly (ItemPipelineShape shape, int rotation)[] ShapeLookup = new (ItemPipelineShape, int)[16];

        static ItemPipelineTP() {
            for (int mask = 0; mask < 16; mask++) {
                ShapeLookup[mask] = CalculateShape(mask);
            }
        }

        private static (ItemPipelineShape, int) CalculateShape(int mask) {
            int count = CountBits(mask);
            return count switch {
                4 => (ItemPipelineShape.Cross, 0),
                3 => (ItemPipelineShape.ThreeWay, GetThreeWayRotation(mask)),
                2 => IsOpposite(mask) ? (ItemPipelineShape.Straight, (mask & (UP | DOWN)) != 0 ? 0 : 1)
                                      : (ItemPipelineShape.Corner, GetCornerRotation(mask)),
                _ => (ItemPipelineShape.Endpoint, 0)
            };
        }

        private static int CountBits(int n) => (n & 1) + ((n >> 1) & 1) + ((n >> 2) & 1) + ((n >> 3) & 1);
        private static bool IsOpposite(int mask) => mask == (UP | DOWN) || mask == (LEFT | RIGHT);

        private static int GetThreeWayRotation(int mask) {
            if ((mask & UP) == 0) return 0;
            if ((mask & DOWN) == 0) return 1;
            if ((mask & LEFT) == 0) return 2;
            return 3;
        }

        private static int GetCornerRotation(int mask) {
            if ((mask & (UP | RIGHT)) == (UP | RIGHT)) return 0;
            if ((mask & (DOWN | RIGHT)) == (DOWN | RIGHT)) return 1;
            if ((mask & (UP | LEFT)) == (UP | LEFT)) return 2;
            return 3;
        }
        #endregion

        #region 字段
        public Color BaseColor => new Color(180, 140, 90);

        public ItemPipelineMode Mode { get; private set; } = ItemPipelineMode.Normal;
        public ItemPipelineShape Shape { get; private set; } = ItemPipelineShape.Endpoint;
        public bool IsEndpoint => GetPipelineConnectionCount() <= 1;

        public int StorageDirectionIndex {
            get {
                for (int i = 0; i < 4; i++) {
                    if (SideStates[i].LinkType == ItemPipelineLinkType.Storage) {
                        return i;
                    }
                }
                return -1;
            }
        }

        public int ShapeRotationID { get; private set; } = 0;
        internal List<ItemPipelineSideState> SideStates { get; private set; }

        /// <summary>在传物品，可空</summary>
        internal TransportingItem? CurrentItem { get; set; } = null;

        /// <summary>抽取节流计时</summary>
        private int extractCooldown;
        /// <summary>抽取间隔(帧)</summary>
        private const int ExtractInterval = 8;
        /// <summary>单次抽取上限</summary>
        private const int ExtractBatchSize = 64;

        /// <summary>本节卡死帧数</summary>
        private int stuckFrames;
        /// <summary>阶段一：放宽路由</summary>
        private const int LooseRoutingThreshold = 60;
        /// <summary>阶段二：任意前向空管</summary>
        private const int AnyForwardThreshold = 180;
        /// <summary>阶段三：允许反向回流</summary>
        private const int ReverseFlowThreshold = 360;
        /// <summary>阶段四：投存储或掉落(60s)</summary>
        private const int RescueDropThreshold = 3600;
        /// <summary>单物品最大反向跳数</summary>
        private const int MaxReverseHopsPerItem = 8;

        /// <summary>最近拒收帧戳</summary>
        private int lastDepositRejectFrame = -1000;
        /// <summary>拒收冷却(帧)</summary>
        private const int DepositRejectCooldown = 30;

        /// <summary>输出端流动动画</summary>
        private PipelineFlowAnimator flowAnimator;

        /// <summary>连接掩码缓存</summary>
        private int lastConnectionMask = -1;
        /// <summary>侧位已初始化</summary>
        private bool sideStatesInitialized;

        /// <summary>物品筛选器</summary>
        internal Item ItemFilter;
        /// <summary>筛选器版本缓存</summary>
        private int cachedFilterVersion = -1;
        /// <summary>筛选 ID 集合缓存</summary>
        private readonly HashSet<int> cachedFilterItemIds = [];
        /// <summary>空筛选即允许全部</summary>
        private bool cachedFilterAllowAll = true;

        /// <summary>悬停动画进度</summary>
        internal float hoverSengs;

        /// <summary>跨岛共享存储(如同一箱子被不同管网连接)时的访问互斥锁，避免并发存取损坏物品数据</summary>
        private static readonly object storageGate = new();
        #endregion

        #region 初始化和更新
        public override void SetProperty() {
            SideStates = [
                new ItemPipelineSideState(new Point16(0, -1), 0),//上
                new ItemPipelineSideState(new Point16(0, 1), 1), //下
                new ItemPipelineSideState(new Point16(-1, 0), 2),//左
                new ItemPipelineSideState(new Point16(1, 0), 3)  //右
            ];
            ItemFilter = new Item();
            sideStatesInitialized = false;

            //新管标脏路由
            ItemPipelineNetwork.MarkDirty();
        }

        /// <summary>管道与相邻管道互相 hand-off 物品，按连通岛屿并行更新(岛内串行，跨岛并行)</summary>
        public override ParallelExecutionKind ParallelKind => ParallelExecutionKind.Grouped;

        /// <summary>声明四向相邻格，使同一连通管网的管道落入同一并行岛屿</summary>
        public override void CollectGroupLinks(ref TPGroupLinkBuilder builder) {
            builder.Link(Position.X, Position.Y - 1);
            builder.Link(Position.X, Position.Y + 1);
            builder.Link(Position.X - 1, Position.Y);
            builder.Link(Position.X + 1, Position.Y);
        }

        /// <summary>并行前在主线程统一重建全局路由，避免并行 Update 中并发触碰路由表</summary>
        public override void PreParallel() => ItemPipelineNetwork.EnsureBuilt();

        public override void Update() {
            if (!sideStatesInitialized) {
                foreach (var side in SideStates) {
                    side.CoreTP = this;
                    side.Position = Position;
                }
                sideStatesInitialized = true;
            }

            //四向连接，快验+节流全扫
            foreach (var side in SideStates) {
                //Position 生命周期内不变
                side.UpdateConnectionState();
            }

            //形状变亦标脏
            UpdateShape();

            //路由重建已上移到 PreParallel() 在主线程统一执行，此处不再调用

            //模式驱动逻辑
            switch (Mode) {
                case ItemPipelineMode.Output:
                    UpdateOutputMode();
                    break;
                case ItemPipelineMode.Input:
                    UpdateInputMode();
                    break;
                    //Normal 仅通道
            }

            //推进物品与卡死自愈
            UpdateTransportingItem();

            //输出端流动动画
            if (Mode == ItemPipelineMode.Output) {
                UpdateFlowAnimation();
            }
            else if (flowAnimator != null) {
                flowAnimator.Clear();
                flowAnimator = null;
            }

            //悬停动画进度
            hoverSengs = HoverTP
                ? Math.Min(hoverSengs + 0.1f, 1f)
                : Math.Max(hoverSengs - 0.1f, 0f);
        }

        private void UpdateShape() {
            int connectionMask = 0;
            if (SideStates[0].LinkType == ItemPipelineLinkType.Pipeline) connectionMask |= UP;
            if (SideStates[1].LinkType == ItemPipelineLinkType.Pipeline) connectionMask |= DOWN;
            if (SideStates[2].LinkType == ItemPipelineLinkType.Pipeline) connectionMask |= LEFT;
            if (SideStates[3].LinkType == ItemPipelineLinkType.Pipeline) connectionMask |= RIGHT;

            if (connectionMask == lastConnectionMask) {
                return;
            }
            var (shape, rotation) = ShapeLookup[connectionMask];
            Shape = shape;
            ShapeRotationID = rotation;
            lastConnectionMask = connectionMask;

            //端点变中继则取消模式
            if (Mode != ItemPipelineMode.Normal && !IsEndpoint) {
                Mode = ItemPipelineMode.Normal;
                SendData();
            }
            //形状变标脏
            ItemPipelineNetwork.MarkDirty();
        }

        private int GetPipelineConnectionCount() {
            int count = 0;
            foreach (var side in SideStates) {
                if (side.LinkType == ItemPipelineLinkType.Pipeline) {
                    count++;
                }
            }
            return count;
        }

        private void UpdateFlowAnimation() {
            flowAnimator ??= new PipelineFlowAnimator();
            flowAnimator.Tick(this);
        }
        #endregion

        #region 输出模式
        /// <summary>输出模式反压抽取</summary>
        private void UpdateOutputMode() {
            //在传则等待
            if (CurrentItem.HasValue) {
                return;
            }

            //抽取节流
            if (extractCooldown > 0) {
                extractCooldown--;
                return;
            }
            extractCooldown = ExtractInterval;

            //无可达输入则退出
            var reachableInputs = ItemPipelineNetwork.GetReachableInputs(Position);
            if (reachableInputs == null || reachableInputs.Count == 0) {
                return;
            }

            //直连存储依次抽
            for (int sideIdx = 0; sideIdx < SideStates.Count; sideIdx++) {
                var side = SideStates[sideIdx];
                if (side.LinkType != ItemPipelineLinkType.Storage) {
                    continue;
                }
                var storage = side.GetStorageProvider();
                if (storage == null || !storage.IsValid) {
                    continue;
                }

                //跨岛共享存储互斥，避免并发抽取损坏数据
                lock (storageGate) {
                    //首个允许类型
                    foreach (var storedItem in storage.GetStoredItems()) {
                        if (storedItem == null || storedItem.IsAir) {
                            continue;
                        }
                        if (!IsItemAllowedByFilter(storedItem.type)) {
                            continue;
                        }
                        //有输入能收才抽
                        if (!HasAvailableInputForItem(storedItem.type, reachableInputs)) {
                            continue;
                        }

                        int extractAmount = Math.Min(storedItem.stack, ExtractBatchSize);
                        Item withdrawn = storage.WithdrawItem(storedItem.type, extractAmount);
                        if (withdrawn != null && !withdrawn.IsAir) {
                            CurrentItem = new TransportingItem(withdrawn.type, withdrawn.stack, withdrawn.prefix) {
                                SourceDirection = (sbyte)side.DirectionIndex
                            };
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 网络中是否存在能接收指定物品类型的输入端
        /// </summary>
        private bool HasAvailableInputForItem(int itemType, List<Point16> reachableInputs) {
            for (int i = 0; i < reachableInputs.Count; i++) {
                var inputPos = reachableInputs[i];
                if (inputPos == Position) {
                    continue;
                }
                if (!TileProcessorLoader.AutoPositionGetTP(inputPos, out ItemPipelineTP inputTP)) {
                    continue;
                }
                if (inputTP.CanReceiveItem(itemType)) {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region 输入模式
        /// <summary>输入模式存入，失败走自愈</summary>
        private void UpdateInputMode() {
            if (!CurrentItem.HasValue) {
                return;
            }
            var item = CurrentItem.Value;
            if (item.Progress < 1f) {
                return;//物品还没到达中心
            }

            //尝试存入直连存储
            for (int i = 0; i < SideStates.Count; i++) {
                var side = SideStates[i];
                if (side.LinkType != ItemPipelineLinkType.Storage) {
                    continue;
                }
                var storage = side.GetStorageProvider();
                if (storage == null || !storage.IsValid) {
                    continue;
                }

                //跨岛共享存储互斥，避免并发存入损坏数据
                lock (storageGate) {
                    Item toDeposit = new Item(item.ItemType, item.Stack) { prefix = (byte)item.Prefix };
                    if (!storage.CanAcceptItem(toDeposit)) {
                        continue;
                    }
                    int beforeStack = toDeposit.stack;
                    if (storage.DepositItem(toDeposit)) {
                        int remaining = ResolveRemainingStack(beforeStack, toDeposit);
                        if (remaining <= 0) {
                            CurrentItem = null;
                        }
                        else {
                            //部分存入: 剩余的继续等待或在卡死自愈阶段被重定向
                            item.Stack = remaining;
                            CurrentItem = item;
                        }
                        return;
                    }
                }
            }

            //没存进去, 记录拒收时间, 让其他输出端在短期内不要再选自己
            lastDepositRejectFrame = (int)Main.GameUpdateCount;
        }

        /// <summary>DepositItem 后推断剩余量，防复制</summary>
        private static int ResolveRemainingStack(int beforeStack, Item toDeposit) {
            //stack 清零即全入
            if (toDeposit == null || toDeposit.IsAir || toDeposit.stack <= 0) {
                return 0;
            }
            //stack 已改即部分入
            if (toDeposit.stack < beforeStack) {
                return toDeposit.stack;
            }
            //未改 stack 保守视为全入，与原版一致防复制
            return 0;
        }

        /// <summary>输入端是否可收该类型</summary>
        public bool CanReceiveItem(int itemType) {
            if (Mode != ItemPipelineMode.Input) {
                return false;
            }
            //已有物品占位 -> 暂不接收
            if (CurrentItem.HasValue) {
                return false;
            }
            //短期内拒收过 -> 视为不可接收, 给上游重定向时间
            if ((int)Main.GameUpdateCount - lastDepositRejectFrame < DepositRejectCooldown) {
                return false;
            }
            if (!IsItemAllowedByFilter(itemType)) {
                return false;
            }

            //至少一个直连存储能接收此物品
            Item testItem = new Item(itemType, 1);
            for (int i = 0; i < SideStates.Count; i++) {
                var side = SideStates[i];
                if (side.LinkType != ItemPipelineLinkType.Storage) {
                    continue;
                }
                var storage = side.GetStorageProvider();
                if (storage == null || !storage.IsValid) {
                    continue;
                }
                lock (storageGate) {
                    if (storage.CanAcceptItem(testItem)) {
                        return true;
                    }
                }
            }
            return false;
        }
        #endregion

        #region 物品传输与卡死自愈
        private void UpdateTransportingItem() {
            if (!CurrentItem.HasValue) {
                stuckFrames = 0;
                return;
            }

            var item = CurrentItem.Value;
            if (item.Speed <= 0f) {
                item.Speed = TransportingItem.DefaultSpeed;
            }

            //没到中心: 推进进度即可
            if (item.Progress < 1f) {
                item.Progress = Math.Min(item.Progress + item.Speed, 1f);
                CurrentItem = item;
                stuckFrames = 0;
                return;
            }

            //已到中心, 输入端模式下应当由 UpdateInputMode 已经处理过(成功就消失);
            //仍残留则视为卡死, 走通用自愈
            bool passed = TryPassToNextPipeline(ref item);
            if (passed) {
                CurrentItem = null;
                stuckFrames = 0;
                return;
            }

            stuckFrames++;
            CurrentItem = item;

            //最后兜底: 把物品塞回直连的任意存储, 实在不行才掉到世界
            if (stuckFrames >= RescueDropThreshold && !VaultUtils.isClient) {
                if (TryRescueDeposit(ref item)) {
                    if (item.Stack <= 0) {
                        CurrentItem = null;
                    }
                    else {
                        CurrentItem = item;
                    }
                    stuckFrames = 0;
                }
                else {
                    DropCurrentItem();
                    stuckFrames = 0;
                }
            }
        }

        /// <summary>渐进选路：严格→宽松→前向→回流</summary>
        private bool TryPassToNextPipeline(ref TransportingItem item) {
            int sourceDir = item.SourceDirection;
            bool allowReverse = stuckFrames >= ReverseFlowThreshold && item.ReverseHops < MaxReverseHopsPerItem;
            bool allowAnyForward = stuckFrames >= AnyForwardThreshold;
            bool allowLooseRouting = stuckFrames >= LooseRoutingThreshold;

            int chosenDir;

            //策略1 最近可收输入
            chosenDir = SelectRoutedDirection(sourceDir, item.ItemType, requireReceiveAvailable: true);
            if (chosenDir < 0 && allowLooseRouting) {
                //策略2 任意可达输入
                chosenDir = SelectRoutedDirection(sourceDir, item.ItemType, requireReceiveAvailable: false);
            }
            if (chosenDir < 0 && allowAnyForward) {
                //策略3 任意前向空管
                chosenDir = SelectAnyForwardDirection(sourceDir);
            }
            if (chosenDir < 0 && allowReverse) {
                //策略4 反向回流
                chosenDir = SelectReverseDirection(sourceDir);
                if (chosenDir >= 0) {
                    item.ReverseHops++;
                }
            }

            if (chosenDir < 0) {
                return false;
            }

            var selectedSide = SideStates[chosenDir];
            var nbr = selectedSide.LinkedPipeline;
            if (nbr == null || !nbr.Active || nbr.CurrentItem.HasValue) {
                //目标向已被占则放弃
                return false;
            }

            //完成传递
            item.Progress = 0f;
            item.SourceDirection = (sbyte)OppositeDirection(chosenDir);
            nbr.CurrentItem = item;
            return true;
        }

        /// <summary>路由表选下一跳</summary>
        private int SelectRoutedDirection(int excludeDir, int itemType, bool requireReceiveAvailable) {
            var inputs = ItemPipelineNetwork.GetReachableInputs(Position);
            if (inputs == null || inputs.Count == 0) {
                return -1;
            }

            //inputs 已按距离升序, 优先尝试最近的
            for (int i = 0; i < inputs.Count; i++) {
                var inputPos = inputs[i];
                if (inputPos == Position) {
                    continue;
                }
                if (!ItemPipelineNetwork.TryGetRouting(Position, inputPos, out var entry)) {
                    continue;
                }
                int dir = entry.NextDir;
                if (dir > 3 || dir == excludeDir) {
                    continue;
                }

                var side = SideStates[dir];
                if (side.LinkType != ItemPipelineLinkType.Pipeline) {
                    continue;
                }
                var nbr = side.LinkedPipeline;
                if (nbr == null || nbr.CurrentItem.HasValue) {
                    continue;
                }
                if (requireReceiveAvailable) {
                    if (!TileProcessorLoader.AutoPositionGetTP(inputPos, out ItemPipelineTP inputTP)) {
                        continue;
                    }
                    if (!inputTP.CanReceiveItem(itemType)) {
                        continue;
                    }
                }
                return dir;
            }
            return -1;
        }

        /// <summary>
        /// 任意非来源方向的空邻居管道(在多个候选时随机)
        /// </summary>
        private int SelectAnyForwardDirection(int excludeDir) {
            Span<int> candidates = stackalloc int[4];
            int count = 0;
            for (int i = 0; i < 4; i++) {
                if (i == excludeDir) {
                    continue;
                }
                var side = SideStates[i];
                if (side.LinkType != ItemPipelineLinkType.Pipeline) {
                    continue;
                }
                var nbr = side.LinkedPipeline;
                if (nbr == null || !nbr.Active || nbr.CurrentItem.HasValue) {
                    continue;
                }
                candidates[count++] = i;
            }
            //并行阶段使用线程安全的 Rand(Main.rand 非线程安全)
            return count == 0 ? -1 : candidates[Rand.Next(count)];
        }

        /// <summary>
        /// 反向回流（仅当来源方向的邻居为空时）
        /// </summary>
        private int SelectReverseDirection(int sourceDir) {
            if ((uint)sourceDir > 3u) {
                return -1;
            }
            var side = SideStates[sourceDir];
            if (side.LinkType != ItemPipelineLinkType.Pipeline) {
                return -1;
            }
            var nbr = side.LinkedPipeline;
            if (nbr == null || !nbr.Active || nbr.CurrentItem.HasValue) {
                return -1;
            }
            return sourceDir;
        }

        /// <summary>
        /// 兜底救援: 把物品塞回直连的任意存储 (输出端的"原存储", 输入端的"目标存储", 都可用)
        /// </summary>
        private bool TryRescueDeposit(ref TransportingItem item) {
            for (int i = 0; i < SideStates.Count; i++) {
                var side = SideStates[i];
                if (side.LinkType != ItemPipelineLinkType.Storage) {
                    continue;
                }
                var storage = side.GetStorageProvider();
                if (storage == null || !storage.IsValid) {
                    continue;
                }
                lock (storageGate) {
                    Item toDeposit = new Item(item.ItemType, item.Stack) { prefix = (byte)item.Prefix };
                    if (!storage.CanAcceptItem(toDeposit)) {
                        continue;
                    }
                    int beforeStack = toDeposit.stack;
                    if (storage.DepositItem(toDeposit)) {
                        item.Stack = ResolveRemainingStack(beforeStack, toDeposit);
                        return true;
                    }
                }
            }
            return false;
        }

        private static int OppositeDirection(int dir) => dir switch {
            0 => 1,
            1 => 0,
            2 => 3,
            3 => 2,
            _ => -1
        };

        /// <summary>
        /// 卡死且无法救援时, 把当前物品丢到世界, 避免永久阻塞
        /// </summary>
        private void DropCurrentItem() {
            if (!CurrentItem.HasValue) {
                return;
            }
            var item = CurrentItem.Value;
            Item drop = new Item(item.ItemType, item.Stack) { prefix = (byte)item.Prefix };
            //并行阶段把物品生成与发包延迟到主线程执行
            DeferSpawnItem(new EntitySource_WorldEvent(), HitBox, drop, type => {
                if (VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type);
                }
            });
            CurrentItem = null;
        }
        #endregion

        #region 模式切换与右键交互
        public override bool? RightClick(int i, int j, Tile tile, Player player) {
            if (Mode == ItemPipelineMode.Normal) {
                return null;
            }

            Item item = player.GetItem();
            if (item.type == ModContent.ItemType<ItemFilter>()) {
                ItemFilter = item.Clone();
                var sourceData = item.GetGlobalItem<ItemFilterData>();
                var targetData = ItemFilter.GetGlobalItem<ItemFilterData>();
                targetData.SetItems(sourceData.Items);
                cachedFilterVersion = -1;//强制下次重新缓存

                SoundEngine.PlaySound(SoundID.Grab, CenterInWorld);
                SendData();
                return true;
            }
            return null;
        }

        /// <summary>
        /// 检查物品是否被筛选器允许 (使用 HashSet 缓存大幅减少每帧成本)
        /// </summary>
        private bool IsItemAllowedByFilter(int itemType) {
            //没设置筛选器
            if (ItemFilter == null || ItemFilter.IsAir) {
                return true;
            }
            if (ItemFilter.type != ModContent.ItemType<ItemFilter>()) {
                return true;
            }

            var filterData = ItemFilter.GetGlobalItem<ItemFilterData>();
            //版本变化或首次缓存, 重建集合
            if (filterData.DataVersion != cachedFilterVersion) {
                cachedFilterItemIds.Clear();
                if (filterData.Items != null) {
                    for (int i = 0; i < filterData.Items.Count; i++) {
                        cachedFilterItemIds.Add(filterData.Items[i]);
                    }
                }
                cachedFilterAllowAll = cachedFilterItemIds.Count == 0;
                cachedFilterVersion = filterData.DataVersion;
            }
            if (cachedFilterAllowAll) {
                return true;
            }
            return cachedFilterItemIds.Contains(itemType);
        }

        public void CycleMode() {
            if (!IsEndpoint) {
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.5f }, CenterInWorld);
                string hintText = NotEndpointHintText?.Value ?? "只能在管道末端设置输入输出";
                CombatText.NewText(HitBox, new Color(255, 100, 100), hintText);
                return;
            }

            Mode = Mode switch {
                ItemPipelineMode.Normal => ItemPipelineMode.Output,
                ItemPipelineMode.Output => ItemPipelineMode.Input,
                ItemPipelineMode.Input => ItemPipelineMode.Normal,
                _ => ItemPipelineMode.Normal
            };

            //模式切换属于强拓扑变化(影响 Output/Input 集合)
            ItemPipelineNetwork.MarkDirty();

            SoundEngine.PlaySound(SoundID.MenuTick, CenterInWorld);

            string modeText = Mode switch {
                ItemPipelineMode.Normal => ModeNormalText?.Value ?? "普通",
                ItemPipelineMode.Output => ModeOutputText?.Value ?? "输出",
                ItemPipelineMode.Input => ModeInputText?.Value ?? "输入",
                _ => ""
            };
            CombatText.NewText(HitBox, GetModeColor(), modeText);
            SendData();
        }

        public Color GetModeColor() {
            return Mode switch {
                ItemPipelineMode.Output => new Color(255, 180, 80),
                ItemPipelineMode.Input => new Color(80, 180, 255),
                _ => BaseColor
            };
        }
        #endregion

        #region 网络同步与存储
        public override void SendData(ModPacket data) {
            data.Write((byte)Mode);
            data.Write(CurrentItem.HasValue);
            if (CurrentItem.HasValue) {
                var item = CurrentItem.Value;
                data.Write(item.ItemType);
                data.Write(item.Stack);
                data.Write(item.Prefix);
                data.Write(item.Progress);
                data.Write(item.SourceDirection);
                data.Write(item.ReverseHops);
            }
            ItemIO.Send(ItemFilter ?? new Item(), data);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            ItemPipelineMode newMode = (ItemPipelineMode)reader.ReadByte();
            if (newMode != Mode) {
                Mode = newMode;
                ItemPipelineNetwork.MarkDirty();
            }

            bool hasItem = reader.ReadBoolean();
            if (hasItem) {
                var item = new TransportingItem {
                    ItemType = reader.ReadInt32(),
                    Stack = reader.ReadInt32(),
                    Prefix = reader.ReadInt32(),
                    Progress = reader.ReadSingle(),
                    SourceDirection = reader.ReadSByte(),
                    ReverseHops = reader.ReadByte(),
                    Speed = TransportingItem.DefaultSpeed
                };
                CurrentItem = item;
            }
            else {
                CurrentItem = null;
            }
            ItemFilter = ItemIO.Receive(reader);
            cachedFilterVersion = -1;
        }

        public override void SaveData(TagCompound tag) {
            if (tag == null) {
                return;
            }

            try {
                tag["ItemPipeline_Mode"] = (int)Mode;
                if (CurrentItem.HasValue) {
                    var item = CurrentItem.Value;
                    tag["ItemPipeline_ItemType"] = item.ItemType;
                    tag["ItemPipeline_Stack"] = item.Stack;
                    tag["ItemPipeline_Prefix"] = item.Prefix;
                    tag["ItemPipeline_Progress"] = item.Progress;
                    tag["ItemPipeline_SourceDirection"] = (int)item.SourceDirection;
                }

                try {
                    ItemFilter ??= new Item();
                    tag["ItemPipeline_ItemFilter"] = ItemIO.Save(ItemFilter);
                } catch (Exception ex) {
                    //单独的筛选器序列化失败不应影响主数据保存
                    CWRMod.Instance.Logger.Error($"[ItemPipelineTP:SaveData] save filter failed:{ex.Message}");
                }
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"[ItemPipelineTP:SaveData] an error has occurred:{ex.Message}");
            }
        }

        public override void LoadData(TagCompound tag) {
            //先把可变状态归位, 异常退出时不会留下残缺数据
            Mode = ItemPipelineMode.Normal;
            CurrentItem = null;
            ItemFilter = new Item();
            cachedFilterVersion = -1;

            if (tag == null) {
                ItemPipelineNetwork.MarkDirty();
                return;
            }

            try {
                if (tag.TryGet("ItemPipeline_Mode", out int mode) && Enum.IsDefined(typeof(ItemPipelineMode), mode)) {
                    Mode = (ItemPipelineMode)mode;
                }

                //所有数值都走 TryGet, 缺键或类型不匹配时使用默认值, 避免 GetInt 抛异常
                if (tag.TryGet("ItemPipeline_ItemType", out int itemType) && itemType > ItemID.None && itemType < ItemLoader.ItemCount) {
                    int stack = tag.TryGet("ItemPipeline_Stack", out int s) ? s : 0;
                    int prefix = tag.TryGet("ItemPipeline_Prefix", out int p) ? p : 0;
                    float progress = tag.TryGet("ItemPipeline_Progress", out float prog) ? prog : 0f;
                    int sourceDir = tag.TryGet("ItemPipeline_SourceDirection", out int sd) ? sd : -1;

                    //合理性矫正, 避免脏数据进入运行时
                    if (stack > 0) {
                        progress = MathHelper.Clamp(progress, 0f, 1f);
                        if (sourceDir < -1 || sourceDir > 3) {
                            sourceDir = -1;
                        }

                        CurrentItem = new TransportingItem(itemType, stack, prefix) {
                            Progress = progress,
                            SourceDirection = (sbyte)sourceDir,
                            Speed = TransportingItem.DefaultSpeed
                        };
                    }
                }

                try {
                    if (tag.TryGet<TagCompound>("ItemPipeline_ItemFilter", out var filterTag) && filterTag != null) {
                        ItemFilter = ItemIO.Load(filterTag) ?? new Item();
                    }
                } catch (Exception ex) {
                    CWRMod.Instance.Logger.Error($"[ItemPipelineTP:LoadData] load filter failed:{ex.Message}");
                    ItemFilter = new Item();
                }
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"[ItemPipelineTP:LoadData] an error has occurred:{ex.Message}");
                Mode = ItemPipelineMode.Normal;
                CurrentItem = null;
                ItemFilter = new Item();
            }

            cachedFilterVersion = -1;
            //加载完毕后强制刷新一次路由
            ItemPipelineNetwork.MarkDirty();
        }

        public override void OnKill() {
            //掉落正在传输的物品
            if (CurrentItem.HasValue && !VaultUtils.isClient) {
                var item = CurrentItem.Value;
                Item drop = new Item(item.ItemType, item.Stack) { prefix = (byte)item.Prefix };
                //并行阶段把物品生成与发包延迟到主线程执行
                DeferSpawnItem(new EntitySource_WorldEvent(), HitBox, drop, type => {
                    if (VaultUtils.isServer) {
                        NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type);
                    }
                });
            }
            //从网络中移除自身, 触发路由表重建
            ItemPipelineNetwork.MarkDirty();
        }
        #endregion

        #region 绘制
        public override void PreTileDraw(SpriteBatch spriteBatch) {
            if (Shape == ItemPipelineShape.Cross) {
                return;
            }
            foreach (var side in SideStates) {
                if (side.CanDraw && side.LinkType != ItemPipelineLinkType.Pipeline) {
                    side.Draw(spriteBatch);
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (Shape != ItemPipelineShape.Cross) {
                foreach (var side in SideStates) {
                    if (side.CanDraw && side.LinkType == ItemPipelineLinkType.Pipeline) {
                        side.Draw(spriteBatch);
                    }
                }
            }

            Vector2 drawPos = PosInWorld - Main.screenPosition;
            Color modeColor = GetModeColor();
            Color lightingColor = VaultUtils.MultiStepColorLerp(0.5f, modeColor, Lighting.GetColor(Position.ToPoint()));

            switch (Shape) {
                case ItemPipelineShape.Cross:
                    DrawCross(spriteBatch, drawPos, modeColor, lightingColor);
                    break;
                case ItemPipelineShape.ThreeWay:
                    DrawThreeWay(spriteBatch, drawPos, modeColor, lightingColor);
                    break;
                case ItemPipelineShape.Corner:
                    DrawCorner(spriteBatch, drawPos, modeColor, lightingColor);
                    break;
                case ItemPipelineShape.Endpoint:
                    DrawEndpoint(spriteBatch, drawPos, modeColor, lightingColor);
                    break;
            }
        }

        [VaultLoaden(CWRConstant.UI + "SupertableUIs/InputArrow3")]
        private static Asset<Texture2D> InputArrow = null!;

        public override void FrontDraw(SpriteBatch spriteBatch) {
            if (Mode == ItemPipelineMode.Output && flowAnimator != null && flowAnimator.HasValidPath) {
                flowAnimator.Draw(spriteBatch, GetModeColor());
            }
            DrawTransportingItem(spriteBatch);
            DrawModeIndicator(spriteBatch);
            DrawFilterDisplay(spriteBatch);
        }

        private void DrawFilterDisplay(SpriteBatch spriteBatch) {
            if (ItemFilter == null || ItemFilter.IsAir) return;
            if (ItemFilter.type != ModContent.ItemType<ItemFilter>()) return;
            if (hoverSengs <= 0.01f) return;

            var filterData = ItemFilter.GetGlobalItem<ItemFilterData>();
            if (filterData.Items.Count == 0) return;

            const float maxRadius = 80f;
            float currentRadius = maxRadius * hoverSengs;
            float angleIncrement = MathHelper.TwoPi / filterData.Items.Count;

            Vector2 drawCenter = CenterInWorld - Main.screenPosition;

            for (int i = 0; i < filterData.Items.Count; i++) {
                int itemType = filterData.Items[i];
                if (itemType <= ItemID.None) continue;

                float currentAngle = angleIncrement * i - MathHelper.PiOver2;
                Vector2 offset = new Vector2((float)Math.Cos(currentAngle), (float)Math.Sin(currentAngle)) * currentRadius;
                Vector2 itemPos = drawCenter + offset;

                Color drawColor = VaultUtils.MultiStepColorLerp(hoverSengs, Lighting.GetColor(Position.ToPoint()), Color.White);
                float scale = hoverSengs * 0.8f;

                VaultUtils.SafeLoadItem(itemType);
                VaultUtils.SimpleDrawItem(spriteBatch, itemType, itemPos, itemWidth: 24, scale, 0, drawColor);
            }
        }

        private void DrawCross(SpriteBatch spriteBatch, Vector2 drawPos, Color modeColor, Color lightingColor) {
            Vector2 center = CenterInWorld - Main.screenPosition;
            Vector2 origin = PipelineCross.Size() / 2;
            spriteBatch.Draw(PipelineCrossSide.Value, center, null, lightingColor, 0, origin, 1, SpriteEffects.None, 0);
        }

        private void DrawThreeWay(SpriteBatch spriteBatch, Vector2 drawPos, Color modeColor, Color lightingColor) {
            Rectangle rect = PipelineThreeCrutches.Value.GetRectangle(ShapeRotationID, 4);
            spriteBatch.Draw(PipelineThreeCrutchesSide.Value, drawPos, rect, lightingColor, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
        }

        private void DrawCorner(SpriteBatch spriteBatch, Vector2 drawPos, Color modeColor, Color lightingColor) {
            Rectangle rect = PipelineCorner.Value.GetRectangle(ShapeRotationID, 4);
            spriteBatch.Draw(PipelineCornerSide.Value, drawPos, rect, lightingColor, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
        }

        private void DrawEndpoint(SpriteBatch spriteBatch, Vector2 drawPos, Color modeColor, Color lightingColor) {
            int linkCount = 0;
            int nonPipeLinkCount = 0;
            foreach (var side in SideStates) {
                if (side.LinkType != ItemPipelineLinkType.None) {
                    linkCount++;
                    if (side.LinkType != ItemPipelineLinkType.Pipeline) {
                        nonPipeLinkCount++;
                    }
                }
            }

            if (linkCount != 2 || nonPipeLinkCount == 2 || linkCount == 0) {
                spriteBatch.Draw(PipelineSide.Value, drawPos.GetRectangle(Size), lightingColor);
            }
        }

        private void DrawTransportingItem(SpriteBatch spriteBatch) {
            if (!CurrentItem.HasValue) {
                return;
            }
            var item = CurrentItem.Value;
            if (item.ItemType <= 0) {
                return;
            }
            Main.instance.LoadItem(item.ItemType);

            Vector2 center = CenterInWorld - Main.screenPosition;
            Vector2 offset = Vector2.Zero;

            if (item.SourceDirection >= 0) {
                Vector2 dirOffset = item.SourceDirection switch {
                    0 => new Vector2(0, -8),
                    1 => new Vector2(0, 8),
                    2 => new Vector2(-8, 0),
                    3 => new Vector2(8, 0),
                    _ => Vector2.Zero
                };
                offset = Vector2.Lerp(dirOffset, Vector2.Zero, item.Progress);
            }

            VaultUtils.SimpleDrawItem(spriteBatch, item.ItemType, center + offset, 20, 0.6f, 0, Color.White);
        }

        private void DrawModeIndicator(SpriteBatch spriteBatch) {
            if (Mode == ItemPipelineMode.Normal) return;
            if (InputArrow == null) return;

            Vector2 center = CenterInWorld - Main.screenPosition;
            Color indicatorColor = GetModeColor();
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.3f + 0.7f;

            int storageDir = StorageDirectionIndex;
            if (storageDir >= 0) {
                float baseRotation = storageDir switch {
                    0 => -MathHelper.PiOver2,
                    1 => MathHelper.PiOver2,
                    2 => MathHelper.Pi,
                    3 => 0,
                    _ => 0
                };
                if (Mode == ItemPipelineMode.Output) {
                    baseRotation += MathHelper.Pi;
                }
                DrawArrowTexture(spriteBatch, center, baseRotation, indicatorColor * pulse, 1f);
            }
            else {
                Texture2D px = VaultAsset.placeholder2.Value;
                Rectangle indicatorRect = new Rectangle((int)(center.X - 2), (int)(center.Y - 2), 4, 4);
                spriteBatch.Draw(px, indicatorRect, indicatorColor * pulse);
            }
        }

        internal static void DrawArrowTexture(SpriteBatch spriteBatch, Vector2 position, float rotation, Color color, float scale) {
            if (InputArrow == null) {
                return;
            }
            Texture2D arrowTex = InputArrow.Value;
            Vector2 origin = arrowTex.Size() / 2f;
            spriteBatch.Draw(arrowTex, position, null, color, rotation, origin, scale, SpriteEffects.None, 0);
        }
        #endregion
    }
}
