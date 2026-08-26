using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Gen
{
    //结构注册表：全部结构锚点的单一真相（裁决8），gen 端写入、运行时只读
    //联机口径：生成只在生成端跑（联机=服务器），下方列表在客户端恒空——
    //列表只许在服务器/单人逻辑里读；客户端需要的判定一律走两个双端方法
    //（IsHideVolumeAt 纯 tile 签名 / NearbyLanterns 按需扫描），两端结果确定一致
    //复位挂骨架 pass（生成先于 OnWorldLoad，同 KiyumePlans 成规）
    internal static class KiyumeStructures
    {
        //════════ 注册表（冻结接口，装配令 §2；写入方见各行注释） ════════

        /// <summary>撒布禁区（tile 矩形）：井口/桥面/船体等破 FloorTop 语义的位形必登记</summary>
        internal static readonly List<Rectangle> ScatterExclusions = [];
        /// <summary>村落建造预留列区间 [L,R)：村社段/井位在村落 Build 前占位（灯道 P40 自扫不占位）</summary>
        internal static readonly List<(int L, int R, string Tag)> ReservedSpans = [];
        /// <summary>藏身体积（tile 矩形+类别字节，本轮不分级）：S2/S4 结构包登记，P2 服务器侧消费</summary>
        internal static readonly List<(Rectangle Rect, byte Kind)> HideVolumes = [];
        /// <summary>灯位（tile）：灯柱/栈桥孤灯，P5 灯光事件锚点</summary>
        internal static readonly List<Point> LanternPosts = [];
        /// <summary>井沿中心（tile）：P4 灵体锚点 / P5 惊吓</summary>
        internal static readonly List<Point> WellMouths = [];
        /// <summary>鸟居足印（tile 矩形）：C 包登记，P5 事件锚点</summary>
        internal static readonly List<Rectangle> ToriiGates = [];
        /// <summary>村社/山顶祠/路祠足印（tile 矩形）：C 包登记</summary>
        internal static readonly List<Rectangle> Shrines = [];
        /// <summary>主坟（tile）：E 包登记，P5 惊吓锚点</summary>
        internal static Point? GraveMain;
        /// <summary>旱田（守田人场地，裁决17）：E 包登记，P4 守田人 / P5 静默区消费</summary>
        internal static Rectangle? ScarecrowPlot;
        /// <summary>民居门洞（tile）：B 包登记，P4 导演消费（裁决8）</summary>
        internal static readonly List<Point> DoorwayPoints = [];

        /// <summary>骨架 pass 调：每次进梦重生成前全表清空</summary>
        internal static void Reset() {
            ScatterExclusions.Clear();
            ReservedSpans.Clear();
            HideVolumes.Clear();
            LanternPosts.Clear();
            WellMouths.Clear();
            ToriiGates.Clear();
            Shrines.Clear();
            GraveMain = null;
            ScarecrowPlot = null;
            DoorwayPoints.Clear();
        }

        /// <summary>结构规划：TerrainPass 在村落 Build 之前调（生成端）。
        /// 占住不许盖民居的列区间；本包先落出生留白，C/E 包按下方锚行追加</summary>
        internal static void PlanReservations() {
            //出生留白 [602,718)：与 KiyumeVillage 现行 spawnPad 判定同口径（B 包重构后改读本表）
            ReservedSpans.Add((KiyumeMetrics.SpawnReserveLeft, KiyumeMetrics.SpawnReserveRight, "出生留白"));
            //──预留锚：C 信仰轴线（村社段 ShrineSpan）──
            //村社段 [1150,1210)：台基 44 列靠西 + 东端后院空地（E 包社后井用地）
            ReservedSpans.Add((KiyumeMetrics.ShrineSpanL, KiyumeMetrics.ShrineSpanR, "村社段"));
            //──预留锚：E 微区（井位，须在组团定形前占段；灯道 W4 定案不再预留——
            //3 列节点×柱距 26-34 做硬预留会把组团截成恒 1 栋，改 P40 自扫、灯让房）──
            KiyumeMicroSites.PlanReservations();
        }

        /// <summary>撒布禁区命中（生成端用）</summary>
        internal static bool InExclusion(int tileX, int tileY) {
            foreach (Rectangle rect in ScatterExclusions) {
                if (rect.Contains(tileX, tileY)) {
                    return true;
                }
            }
            return false;
        }

        //════════ HideVolume 签名规则常量表 ════════
        //五类签名的实例 tile 由 W2/W3 结构包（B 柜/床下/地窖，D 苇丛/船骸）落地，
        //改任一结构材质必须同步本表对应常量

        //类别字节（登记 HideVolumes 用；裁决9：本轮不分级，P2 只消费 bool）
        internal const byte KindBedGap = 1;    //床底（废止：B 包定案床直贴板间无空隙，签名已除名；字节保留防复用）
        internal const byte KindCabinet = 2;   //柜：2 宽内膛 + 围栏墙柜门 + 朝木顶盖
        internal const byte KindStiltGap = 3;  //床下：高床柱间矮空
        internal const byte KindCellar = 4;    //地窖：王朝墙内膛
        internal const byte KindReeds = 5;     //苇丛：连续苇杆簇
        internal const byte KindWreck = 6;     //船骸：幽木壳内膛

        //床下签名探距：壳底向上探行数 / 柱脚左右探列数（B 包高床落地后按 StiltLift/StiltClearance 核对）
        private const int StiltCeilProbeRows = 3;
        private const int StiltBeamProbeCols = 5;
        //箪笥签名探距：柜膛（高 3）向上到暗朝木顶盖的行数，与 B 包 TryTansu 顶盖同源
        private const int CabinetCeilProbeRows = 3;
        //地窖内膛墙。计划书写作「DynastyWood 墙」——原版只有白/蓝两种王朝墙，
        //白墙让给村社唯一性签名（S3「全村唯一白」），地窖用蓝；B 包若改材质必须同步这里
        private const ushort CellarWall = WallID.BlueDynasty;
        //苇丛签名：苇塘窗 [L,R)（裁决17=[560,600]）内、同行 ±ScanCols 列 ≥MinStalks 根苇杆；
        //窗值与 D 包的 ReedPondSpan 数值同源，改一处必改两处
        private const int ReedWindowL = 560;
        private const int ReedWindowR = 600;
        private const int ReedScanCols = 5;
        private const int ReedMinStalks = 4;
        //船骸签名：滩涂带内幽木壳下的内膛，壳顶向上探行数（D 包船骸落地后核对）
        private const int WreckCeilProbeRows = 4;

        /// <summary>双端藏身判定（裁决9）：纯 tile 几何签名，玩家自身所在区块两端必已同步，
        /// 结果确定一致。五类签名任一命中即真；结构未落地的世界恒 false</summary>
        internal static bool IsHideVolumeAt(Point tileCoord) {
            int x = tileCoord.X;
            int y = tileCoord.Y;
            //签名探距最大 ±5，留 8 格身位；世界边界实心 12 格厚，可玩区不受此挡
            if (!WorldGen.InWorld(x, y, 8)) {
                return false;
            }
            if (Main.tile[x, y].HasTile) {
                //五类签名全部立足「人站在空格里」，实心格直接出局
                return false;
            }
            return CabinetAt(x, y) || CellarAt(x, y)
                || StiltGapAt(x, y) || ReedsAt(x, y) || WreckHullAt(x, y);
        }

        //柜：围栏墙柜门 + 头顶 3 行内暗朝木顶盖。B 包定案柜膛 2 宽、双格皆围栏墙，
        //旧「两侧夹实心」对 2 宽内膛恒假（W4 实锤除名）；全图唯一围栏墙+朝木盖组合=箪笥
        private static bool CabinetAt(int x, int y) {
            return Main.tile[x, y].WallType == WallID.WoodenFence
                && CeilIs(x, y, CabinetCeilProbeRows, TileID.DynastyWood);
        }

        //地窖：王朝墙内膛（民居地板下，B 包地窖是全图唯一用这种墙的位形）
        private static bool CellarAt(int x, int y) {
            return Main.tile[x, y].WallType == CellarWall;
        }

        //床下：高床壳底（幽木）之下、两侧都有木梁柱脚的矮空；
        //要求双侧见柱，单根灯柱/枯树旁的巧合不命中
        private static bool StiltGapAt(int x, int y) {
            if (!CeilIs(x, y, StiltCeilProbeRows, TileID.SpookyWood)) {
                return false;
            }
            return BeamWithin(x, y, -1) && BeamWithin(x, y, 1);
        }

        private static bool BeamWithin(int x, int y, int dir) {
            for (int dx = 1; dx <= StiltBeamProbeCols; dx++) {
                Tile t = Main.tile[x + dir * dx, y];
                if (t.HasTile && t.TileType == TileID.WoodenBeam) {
                    return true;
                }
            }
            return false;
        }

        //苇丛：苇塘窗内、同行邻域凑够苇杆根数（窗外的幽木＝枯树/民居壳，不参与）
        private static bool ReedsAt(int x, int y) {
            if (x < ReedWindowL || x >= ReedWindowR) {
                return false;
            }
            int stalks = 0;
            for (int dx = -ReedScanCols; dx <= ReedScanCols; dx++) {
                Tile t = Main.tile[x + dx, y];
                if (t.HasTile && t.TileType == TileID.SpookyWood) {
                    stalks++;
                }
            }
            return stalks >= ReedMinStalks;
        }

        //船骸：滩涂带内、头顶幽木壳、脚下两行内有实心的内膛（滩涂现无幽木，D 包落骸后生效）
        private static bool WreckHullAt(int x, int y) {
            if (x < KiyumeMetrics.ShoalLeft || x >= KiyumeMetrics.VillageLeft) {
                return false;
            }
            if (!CeilIs(x, y, WreckCeilProbeRows, TileID.SpookyWood)) {
                return false;
            }
            return SolidAt(x, y + 1) || SolidAt(x, y + 2);
        }

        //向上找第一格实心：必须恰是指定类型，中途撞上别的实心即失败
        private static bool CeilIs(int x, int y, int probeRows, ushort type) {
            for (int dy = 1; dy <= probeRows; dy++) {
                Tile t = Main.tile[x, y - dy];
                if (!t.HasTile) {
                    continue;
                }
                return t.TileType == type;
            }
            return false;
        }

        private static bool SolidAt(int x, int y) {
            Tile t = Main.tile[x, y];
            return t.HasTile && Main.tileSolid[t.TileType];
        }

        /// <summary>双端灯具计数：按需扫描镜头附近已加载 tile 的挂灯（方形窗，勿每帧全量调用）。
        /// 只数每盏灯的顶格（样式纵排，frameY%36==0，对源 TileObjectData Style1x2Top）；
        /// 灯柱兜底火把与石灯烛台不计——P5 需要更宽口径时在此扩表</summary>
        internal static int NearbyLanterns(Vector2 worldCenter, int radiusPx) {
            int cx = (int)(worldCenter.X / 16f);
            int cy = (int)(worldCenter.Y / 16f);
            int r = Math.Max(radiusPx, 0) / 16 + 1;
            int minX = Math.Max(cx - r, 1);
            int maxX = Math.Min(cx + r, Main.maxTilesX - 2);
            int minY = Math.Max(cy - r, 1);
            int maxY = Math.Min(cy + r, Main.maxTilesY - 2);
            int count = 0;
            for (int x = minX; x <= maxX; x++) {
                for (int y = minY; y <= maxY; y++) {
                    Tile t = Main.tile[x, y];
                    if (t.HasTile && t.TileType == TileID.HangingLanterns && t.TileFrameY % 36 == 0) {
                        count++;
                    }
                }
            }
            return count;
        }
    }
}
