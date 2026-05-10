using InnoVault.TileProcessors;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Industrials.MaterialFlow.ItemPipelines
{
    /// <summary>
    /// 路由项 - 表示从某管道到达某输入端点的下一跳信息
    /// </summary>
    internal readonly struct RoutingEntry
    {
        /// <summary>下一跳方向(0上1下2左3右)；255 代表自身就是输入端</summary>
        public readonly byte NextDir;
        /// <summary>到目标输入端的跳数(便于按距离排序)</summary>
        public readonly ushort Distance;

        public RoutingEntry(byte nextDir, ushort distance) {
            NextDir = nextDir;
            Distance = distance;
        }
    }

    /// <summary>
    /// 物流管道网络的全局路由管理器
    /// <para>设计目标：</para>
    /// <list type="bullet">
    /// <item>用一次性的多源BFS生成"任意管道→任意输入端的下一跳"路由表，运行时查询O(1)。</item>
    /// <item>仅在拓扑变化时按节流间隔重建，远端管道仍正常更新自身状态，但不再每帧重复全网BFS。</item>
    /// <item>由 SideState 主动标脏（连接类型变化、模式切换、管道生灭），最大延迟由强制重建保底。</item>
    /// </list>
    /// </summary>
    internal static class ItemPipelineNetwork
    {
        //每个管道位置 -> {输入端点位置 -> 路由项}
        private static readonly Dictionary<Point16, Dictionary<Point16, RoutingEntry>> RoutingTables = [];
        //每个管道位置 -> 按距离升序排序的输入端点列表 (运行期热路径只读, 重建时统一替换)
        private static readonly Dictionary<Point16, List<Point16>> SortedInputsPerPipeline = [];
        //当前已知的所有输入端点(按位置)
        private static readonly HashSet<Point16> KnownInputs = [];

        //拓扑版本号(脏标记即递增)
        private static int TopologyVersion = 0;
        //上次成功重建对应的拓扑版本
        private static int CachedVersion = -1;
        //上次重建的帧
        private static int LastRebuildFrame = -100000;

        //最小重建间隔(标脏后等待此间隔再重建,避免连串放置时连续抖动)
        private const int MinRebuildIntervalFrames = 12;
        //强制重建间隔(无脏标记也定期刷新,容错任何遗漏的脏标记)
        private const int MaxRebuildIntervalFrames = 600;

        //BFS 复用容器, 避免反复分配
        private static readonly Queue<(ItemPipelineTP tp, ushort dist)> BfsQueue = new(64);
        private static readonly HashSet<Point16> BfsVisited = [];

        private static int _itemPipelineTPID = -1;
        private static int ItemPipelineTPID {
            get {
                if (_itemPipelineTPID < 0) {
                    _itemPipelineTPID = TPUtils.GetID<ItemPipelineTP>();
                }
                return _itemPipelineTPID;
            }
        }

        public static int CurrentTopologyVersion => TopologyVersion;

        /// <summary>
        /// 通知网络拓扑发生变化，下一次 EnsureBuilt 会按节流策略重建
        /// </summary>
        public static void MarkDirty() {
            unchecked { TopologyVersion++; }
        }

        /// <summary>
        /// 在每帧由任意一个管道调用 - 按需重建，已经重建则极轻量
        /// </summary>
        public static void EnsureBuilt() {
            int currentFrame = (int)Main.GameUpdateCount;
            bool topologyChanged = TopologyVersion != CachedVersion;
            int sinceRebuild = currentFrame - LastRebuildFrame;
            bool minIntervalOk = sinceRebuild >= MinRebuildIntervalFrames;
            bool forceRebuild = sinceRebuild >= MaxRebuildIntervalFrames;

            if (forceRebuild || (topologyChanged && minIntervalOk)) {
                Rebuild();
                CachedVersion = TopologyVersion;
                LastRebuildFrame = currentFrame;
            }
        }

        private static int OppositeDir(int dir) => dir switch {
            0 => 1, 1 => 0, 2 => 3, 3 => 2, _ => -1
        };

        /// <summary>
        /// 全量重建路由表
        /// </summary>
        private static void Rebuild() {
            RoutingTables.Clear();
            SortedInputsPerPipeline.Clear();
            KnownInputs.Clear();

            //收集所有活跃的输入端点
            List<ItemPipelineTP> inputs = [];
            int targetID = ItemPipelineTPID;
            foreach (var baseTP in TileProcessorLoader.TP_InWorld) {
                if (baseTP == null || !baseTP.Active) {
                    continue;
                }
                if (baseTP.ID != targetID) {
                    continue;
                }
                if (baseTP is not ItemPipelineTP tp) {
                    continue;
                }
                if (tp.Mode == ItemPipelineMode.Input) {
                    inputs.Add(tp);
                    KnownInputs.Add(tp.Position);
                }
            }

            if (inputs.Count == 0) {
                return;
            }

            //从每个输入端点单独BFS, 在所有可达管道上记录"下一跳指向该输入"
            foreach (var input in inputs) {
                BfsQueue.Clear();
                BfsVisited.Clear();

                BfsQueue.Enqueue((input, 0));
                BfsVisited.Add(input.Position);

                //输入端自身也写入路由表(距离0,方向哨兵), 方便外部判断是否可达
                EnsureTable(input.Position)[input.Position] = new RoutingEntry(byte.MaxValue, 0);

                while (BfsQueue.Count > 0) {
                    var (cur, dist) = BfsQueue.Dequeue();
                    var sides = cur.SideStates;
                    if (sides == null) {
                        continue;
                    }

                    for (int dir = 0; dir < 4; dir++) {
                        var side = sides[dir];
                        if (side.LinkType != ItemPipelineLinkType.Pipeline) {
                            continue;
                        }
                        var nbr = side.LinkedPipeline;
                        if (nbr == null || !nbr.Active) {
                            continue;
                        }
                        if (BfsVisited.Contains(nbr.Position)) {
                            continue;
                        }
                        BfsVisited.Add(nbr.Position);

                        //从邻居走"backDir"方向回到我们 = 邻居指向当前输入端的下一跳方向
                        int backDir = OppositeDir(dir);
                        ushort newDist = (ushort)(dist + 1);

                        var nbrTable = EnsureTable(nbr.Position);
                        nbrTable[input.Position] = new RoutingEntry((byte)backDir, newDist);

                        BfsQueue.Enqueue((nbr, newDist));
                    }
                }
            }

            //按距离升序整理每个管道的输入列表(运行期遍历更高效)
            foreach (var (pos, table) in RoutingTables) {
                List<Point16> sortedInputs = new(table.Count);
                foreach (var key in table.Keys) {
                    sortedInputs.Add(key);
                }
                if (sortedInputs.Count > 1) {
                    var localTable = table;
                    sortedInputs.Sort((a, b) => localTable[a].Distance.CompareTo(localTable[b].Distance));
                }
                SortedInputsPerPipeline[pos] = sortedInputs;
            }
        }

        private static Dictionary<Point16, RoutingEntry> EnsureTable(Point16 pipelinePos) {
            if (!RoutingTables.TryGetValue(pipelinePos, out var table)) {
                RoutingTables[pipelinePos] = table = [];
            }
            return table;
        }

        /// <summary>
        /// 从指定管道到指定输入端的下一跳
        /// </summary>
        public static bool TryGetRouting(Point16 fromPipeline, Point16 toInput, out RoutingEntry entry) {
            entry = default;
            return RoutingTables.TryGetValue(fromPipeline, out var table)
                   && table.TryGetValue(toInput, out entry);
        }

        /// <summary>
        /// 获取从指定管道按"距离升序"可达的所有输入端点列表 (只读, 不要修改)
        /// </summary>
        public static List<Point16> GetReachableInputs(Point16 fromPipeline) {
            return SortedInputsPerPipeline.TryGetValue(fromPipeline, out var list) ? list : null;
        }

        /// <summary>
        /// 是否存在任意已知输入端
        /// </summary>
        public static bool HasAnyKnownInput() => KnownInputs.Count > 0;

        /// <summary>
        /// 在世界卸载或重置时清理状态
        /// </summary>
        public static void Clear() {
            RoutingTables.Clear();
            SortedInputsPerPipeline.Clear();
            KnownInputs.Clear();
            TopologyVersion = 0;
            CachedVersion = -1;
            LastRebuildFrame = -100000;
            BfsQueue.Clear();
            BfsVisited.Clear();
            _itemPipelineTPID = -1;
        }
    }
}
