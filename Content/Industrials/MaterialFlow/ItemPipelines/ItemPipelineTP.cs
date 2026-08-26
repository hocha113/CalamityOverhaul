using CalamityOverhaul.Content.Industrials.ElectricPowers.ItemFilters;
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
    internal class ItemPipelineTP : TileProcessor, ICWRLoader, IItemFilterHost
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
        /// <summary>阶段一，放宽路由</summary>
        private const int LooseRoutingThreshold = 60;
        /// <summary>阶段二，任意前向空管</summary>
        private const int AnyForwardThreshold = 180;
        /// <summary>阶段三，允许反向回流</summary>
        private const int ReverseFlowThreshold = 360;
        /// <summary>阶段四，投存储或掉落(60s)</summary>
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

        /// <summary>筛选名单，O(1)，空=全放行</summary>
        internal ItemFilterSet Filter = new();

        /// <summary>悬停动画进度</summary>
        internal float hoverSengs;

        /// <summary>跨岛共享存储互斥锁</summary>
        private static readonly object storageGate = new();

        /// <summary>网络脏标记：权威端物流状态变了，待节流刷新</summary>
        private bool netDirty;
        /// <summary>脏刷新节流(帧)，防止繁忙管线触发 InnoVault 的发包峰值惩罚(超限静默禁发一秒)</summary>
        private int netSyncCooldown;
        private const int NetSyncInterval = 10;
        /// <summary>上次发送时的名单修改版本，-1=从未发送；仅本端自比较，禁跨网比较</summary>
        private int lastSentFilterRevision = -1;
        #endregion

        /// <summary>标记本管物流状态已变化，由权威端在 Update 尾部节流合批发送</summary>
        internal void MarkNetDirty() => netDirty = true;

        #region 初始化和更新
        public override void SetProperty() {
            SideStates = [
                new ItemPipelineSideState(new Point16(0, -1), 0),//上
                new ItemPipelineSideState(new Point16(0, 1), 1), //下
                new ItemPipelineSideState(new Point16(-1, 0), 2),//左
                new ItemPipelineSideState(new Point16(1, 0), 3)  //右
            ];
            Filter = new ItemFilterSet();
            sideStatesInitialized = false;

            //新管标脏路由
            ItemPipelineNetwork.MarkDirty();
        }

        /// <summary>邻管 hand-off，连通岛并行(岛内串行)</summary>
        public override ParallelExecutionKind ParallelKind => ParallelExecutionKind.Grouped;

        /// <summary>声明四向相邻格，使同一连通管网的管道落入同一并行岛屿</summary>
        public override void CollectGroupLinks(ref TPGroupLinkBuilder builder) {
            builder.Link(Position.X, Position.Y - 1);
            builder.Link(Position.X, Position.Y + 1);
            builder.Link(Position.X - 1, Position.Y);
            builder.Link(Position.X + 1, Position.Y);
        }

        /// <summary>并行前主线程重建路由，防并发碰表</summary>
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

            //路由重建已上移 PreParallel

            //物流是权威端(服务器/单人)专属：抽取/存入会真实改动箱子与机器库存，
            //客户端跑这套会污染本地箱子镜像并把漂移状态推回服务器
            if (!VaultUtils.isClient) {
                switch (Mode) {
                    case ItemPipelineMode.Output:
                        UpdateOutputMode();
                        break;
                    case ItemPipelineMode.Input:
                        UpdateInputMode();
                        break;
                        //Normal 仅通道
                }
            }

            //推进物品与卡死自愈(客户端仅表现)
            UpdateTransportingItem();

            //输出端流动动画(纯视觉，专用服务器跳过)
            if (Mode == ItemPipelineMode.Output && !Main.dedServ) {
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

            //权威端节流刷新脏状态，客户端由此看到抽取/移交/存入的结果
            if (netSyncCooldown > 0) {
                netSyncCooldown--;
            }
            if (netDirty && netSyncCooldown <= 0 && VaultUtils.isServer) {
                netDirty = false;
                netSyncCooldown = NetSyncInterval;
                SendData();
            }
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

            //端点变中继则取消模式(各端本地同样推导，权威端再广播兜底)
            if (Mode != ItemPipelineMode.Normal && !IsEndpoint) {
                Mode = ItemPipelineMode.Normal;
                MarkNetDirty();
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

                //跨岛共享存储互斥
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
                        var chestSnap = ChestNetSync.Capture(storage);
                        Item withdrawn = storage.WithdrawItem(storedItem.type, extractAmount);
                        if (withdrawn != null && !withdrawn.IsAir) {
                            CurrentItem = new TransportingItem(withdrawn.type, withdrawn.stack, withdrawn.prefix) {
                                SourceDirection = (sbyte)side.DirectionIndex
                            };
                            MarkNetDirty();
                            SyncChestChanges(chestSnap);
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>网内是否有可收该类型的输入端</summary>
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

                //跨岛共享存储互斥
                lock (storageGate) {
                    Item toDeposit = new Item(item.ItemType, item.Stack) { prefix = (byte)item.Prefix };
                    if (!storage.CanAcceptItem(toDeposit)) {
                        continue;
                    }
                    int beforeStack = toDeposit.stack;
                    var chestSnap = ChestNetSync.Capture(storage);
                    if (storage.DepositItem(toDeposit)) {
                        int remaining = ResolveRemainingStack(beforeStack, toDeposit);
                        if (remaining <= 0) {
                            CurrentItem = null;
                        }
                        else {
                            //部分存入，剩余自愈
                            item.Stack = remaining;
                            CurrentItem = item;
                        }
                        MarkNetDirty();
                        SyncChestChanges(chestSnap);
                        return;
                    }
                }
            }

            //拒收记时，上游先绕开
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

            //客户端仅表现：推进段内进度到中心后停住，等待权威端的脏刷新包
            //完成跨管移交/存入/自愈；本地不做移交，避免与服务器选路分叉产生幽灵物品
            if (VaultUtils.isClient) {
                if (item.Progress < 1f) {
                    item.Progress = Math.Min(item.Progress + item.Speed, 1f);
                    CurrentItem = item;
                }
                stuckFrames = 0;
                return;
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

            //兜底塞回或掉落
            if (stuckFrames >= RescueDropThreshold && !VaultUtils.isClient) {
                if (TryRescueDeposit(ref item)) {
                    if (item.Stack <= 0) {
                        CurrentItem = null;
                    }
                    else {
                        CurrentItem = item;
                    }
                    stuckFrames = 0;
                    MarkNetDirty();
                }
                else {
                    DropCurrentItem();
                    stuckFrames = 0;
                }
            }
        }

        /// <summary>渐进选路，严格→宽松→前向→回流</summary>
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
            //两端都标脏，客户端靠节流刷新看到移交结果
            MarkNetDirty();
            nbr.MarkNetDirty();
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

        /// <summary>非来源向空邻管，多候选随机</summary>
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
            //并行用线程安全 Rand
            return count == 0 ? -1 : candidates[Rand.Next(count)];
        }

        /// <summary>反向回流，仅来源邻为空</summary>
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

        /// <summary>兜底塞回直连存储</summary>
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
                    var chestSnap = ChestNetSync.Capture(storage);
                    if (storage.DepositItem(toDeposit)) {
                        item.Stack = ResolveRemainingStack(beforeStack, toDeposit);
                        SyncChestChanges(chestSnap);
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

        /// <summary>箱子槽位差异广播；发送经 Defer 转到主线程(管道按岛并行更新，网络流非线程安全)</summary>
        private void SyncChestChanges(in ChestNetSync.Snapshot snapshot) {
            if (!snapshot.IsValid) {
                return;
            }
            List<int> changed = ChestNetSync.CollectChanged(snapshot);
            if (changed == null) {
                return;
            }
            int chestIndex = snapshot.ChestIndex;
            Defer(() => ChestNetSync.SendChanged(chestIndex, changed));
        }

        /// <summary>卡死无救援则丢世界</summary>
        private void DropCurrentItem() {
            if (!CurrentItem.HasValue) {
                return;
            }
            var item = CurrentItem.Value;
            Item drop = new Item(item.ItemType, item.Stack) { prefix = (byte)item.Prefix };
            //并行阶段延后到主线程
            DeferSpawnItem(new EntitySource_WorldEvent(), HitBox, drop, type => {
                if (VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type);
                }
            });
            CurrentItem = null;
            MarkNetDirty();
        }
        #endregion

        #region 模式切换与右键交互

        ItemFilterSet IItemFilterHost.Filter => Filter;
        public string FilterHostName => Lang.GetItemNameValue(ModContent.ItemType<ItemPipeline>());
        public bool FilterHostAlive => Active;
        public Vector2? FilterHostWorldCenter => CenterInWorld;
        public void OnFilterChanged() => SendData();

        public override bool? RightClick(int i, int j, Tile tile, Player player) {
            if (Mode == ItemPipelineMode.Normal) {
                return null;
            }

            Item item = player.GetItem();

            //手持过滤卡，装名单到本管
            //TP右键经InnoVault总线在所有端各自执行(卡片名单已随物品NetSend同步)，
            //推送只留权威端一份，客户端不要再用本地状态顶回服务器
            if (item.ModItem is ItemFilter card) {
                Filter.CopyFrom(card.Filter);

                SoundEngine.PlaySound(SoundID.Grab, CenterInWorld);
                if (!VaultUtils.isServer) {
                    CombatText.NewText(HitBox, GetModeColor(), ItemFilterEditorUI.InstalledText.Value);
                }
                if (!VaultUtils.isClient) {
                    SendData();
                }
                return true;
            }

            //空手右键端点开名单编辑
            if (!item.Alives()) {
                if (player.whoAmI == Main.myPlayer) {
                    ItemFilterEditorUI.Instance?.ToggleFor(this);
                }
                return true;
            }

            return null;
        }

        /// <summary>检查物品是否被筛选名单放行(空名单=全部放行)</summary>
        private bool IsItemAllowedByFilter(int itemType) => Filter.Matches(itemType);

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

            //模式切换=强拓扑变化
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
            //名单只在有变化或全量场景(加入世界快照序列化期间 InitializeWorld 为真)时搭载：
            //物流脏刷新频率高(≤6次/秒)而名单可达500项(约2KB)，逐包全量搭载会成为最大流量热点
            bool sendFilter = TileProcessorNetWork.InitializeWorld || lastSentFilterRevision != Filter.Revision;
            data.Write(sendFilter);
            if (sendFilter) {
                Filter.Write(data);
                if (!TileProcessorNetWork.InitializeWorld) {
                    lastSentFilterRevision = Filter.Revision;
                }
            }
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            //先读完所有字段再做取舍，避免共享读取流错位
            ItemPipelineMode newMode = (ItemPipelineMode)reader.ReadByte();

            bool hasItem = reader.ReadBoolean();
            TransportingItem wireItem = default;
            if (hasItem) {
                wireItem = new TransportingItem {
                    ItemType = reader.ReadInt32(),
                    Stack = reader.ReadInt32(),
                    Prefix = reader.ReadInt32(),
                    Progress = reader.ReadSingle(),
                    SourceDirection = reader.ReadSByte(),
                    ReverseHops = reader.ReadByte(),
                    Speed = TransportingItem.DefaultSpeed
                };
                //脏包/空气物品挡在管外:无效负载按"无在管物品"处理,空传输不再入管(反馈十二·#32)
                if (wireItem.ItemType <= 0 || wireItem.ItemType >= ItemLoader.ItemCount
                    || wireItem.Stack <= 0 || !float.IsFinite(wireItem.Progress)) {
                    hasItem = false;
                }
            }
            //名单按线上标志位读取，未搭载则保留当前名单
            if (reader.ReadBoolean()) {
                Filter.Read(reader);
            }

            if (newMode != Mode) {
                Mode = newMode;
                ItemPipelineNetwork.MarkDirty();
            }

            //在管物品是服务器权威：客户端推送(模式/名单编辑)不得覆盖服务器的在管物品，
            //否则玩家点管子那一瞬的过期本地状态会把移交中的物品复制或抹掉
            if (VaultUtils.isServer) {
                MarkNetDirty();//补发合并后的真实状态给所有客户端(含发送者)
                return;
            }

            CurrentItem = hasItem ? wireItem : null;
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
                    Filter.Save(tag, "ItemPipeline_Filter");
                } catch (Exception ex) {
                    //筛选器序列化失败不影响主档
                    CWRMod.Instance.Logger.Error($"[ItemPipelineTP:SaveData] save filter failed:{ex.Message}");
                }
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"[ItemPipelineTP:SaveData] an error has occurred:{ex.Message}");
            }
        }

        public override void LoadData(TagCompound tag) {
            //先归位可变状态
            Mode = ItemPipelineMode.Normal;
            CurrentItem = null;
            Filter = new ItemFilterSet();

            if (tag == null) {
                ItemPipelineNetwork.MarkDirty();
                return;
            }

            try {
                if (tag.TryGet("ItemPipeline_Mode", out int mode) && Enum.IsDefined(typeof(ItemPipelineMode), mode)) {
                    Mode = (ItemPipelineMode)mode;
                }

                //数值走 TryGet，防脏键抛异常
                if (tag.TryGet("ItemPipeline_ItemType", out int itemType) && itemType > ItemID.None && itemType < ItemLoader.ItemCount) {
                    int stack = tag.TryGet("ItemPipeline_Stack", out int s) ? s : 0;
                    int prefix = tag.TryGet("ItemPipeline_Prefix", out int p) ? p : 0;
                    float progress = tag.TryGet("ItemPipeline_Progress", out float prog) ? prog : 0f;
                    int sourceDir = tag.TryGet("ItemPipeline_SourceDirection", out int sd) ? sd : -1;

                    //合理性矫正
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
                    //新格式优先；旧档整卡物品由垫片回填
                    if (!Filter.TryLoad(tag, "ItemPipeline_Filter")
                        && tag.TryGet<TagCompound>("ItemPipeline_ItemFilter", out var filterTag) && filterTag != null
                        && ItemIO.Load(filterTag) is Item legacyCard && legacyCard.ModItem is ItemFilter card) {
                        Filter.CopyFrom(card.Filter);
                    }
                } catch (Exception ex) {
                    CWRMod.Instance.Logger.Error($"[ItemPipelineTP:LoadData] load filter failed:{ex.Message}");
                    Filter = new ItemFilterSet();
                }
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"[ItemPipelineTP:LoadData] an error has occurred:{ex.Message}");
                Mode = ItemPipelineMode.Normal;
                CurrentItem = null;
                Filter = new ItemFilterSet();
            }

            //加载后强制刷路由
            ItemPipelineNetwork.MarkDirty();
        }

        public override void OnKill() {
            //掉落在传物品
            if (CurrentItem.HasValue && !VaultUtils.isClient) {
                var item = CurrentItem.Value;
                Item drop = new Item(item.ItemType, item.Stack) { prefix = (byte)item.Prefix };
                //并行阶段延后到主线程
                DeferSpawnItem(new EntitySource_WorldEvent(), HitBox, drop, type => {
                    if (VaultUtils.isServer) {
                        NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type);
                    }
                });
            }
            //离网，刷路由
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

        [VaultLoaden(CWRConstant.UI + "InputArrow3")]
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
            if (Filter.IsEmpty || hoverSengs <= 0.01f) {
                return;
            }

            IReadOnlyList<int> filterItems = Filter.OrderedItems;
            const float maxRadius = 80f;
            float currentRadius = maxRadius * hoverSengs;
            float angleIncrement = MathHelper.TwoPi / filterItems.Count;

            Vector2 drawCenter = CenterInWorld - Main.screenPosition;
            //黑名单以警示红着色区分
            Color modeTint = Filter.Mode == ItemFilterMode.Whitelist
                ? Color.White
                : ItemFilterTheme.AccentBlacklist;

            for (int i = 0; i < filterItems.Count; i++) {
                int itemType = filterItems[i];
                if (itemType <= ItemID.None) continue;

                float currentAngle = angleIncrement * i - MathHelper.PiOver2;
                Vector2 offset = new Vector2((float)Math.Cos(currentAngle), (float)Math.Sin(currentAngle)) * currentRadius;
                Vector2 itemPos = drawCenter + offset;

                Color drawColor = VaultUtils.MultiStepColorLerp(hoverSengs, Lighting.GetColor(Position.ToPoint()), modeTint);
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
