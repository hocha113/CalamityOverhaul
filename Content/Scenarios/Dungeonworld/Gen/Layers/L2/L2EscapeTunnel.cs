using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L2
{
    //越狱者的洞(WAVE2-BUILDINGS §3.3):某间干草囚室天花的裂砖后面,藏着一条
    //挖了一辈子的越狱洞。之字上爬到L2带顶,尽头小室的天花是教堂地基色的蓝砖,
    //凿痕密布,一格没能凿进去;挖洞人自己死在小室里。每世界至多1条。
    //
    //集成形态(锚定三处):
    //  1.L2CellRow干草室 → RegisterHostCell 登记候选宿主(零随机零几何);
    //  2.L2Content.PlanAndBuild开头 → Reset(回放制防跨生成残留);
    //  3.L2Content层流末端 → Build(随机消耗集中于此,R4)。
    //
    //纪律:
    //  ·密洞不入ctx.Graph:洪泛从出生点到不了裂砖塞后面(P80无逐房染色断言,
    //    入图不会硬报错,但绳列使2宽竖井对2x3包络本就不透),不入图省掉一个
    //    永远"未连通"的图节点;先例=牢栅藏物室的封闭缝室,数百格未访问体素
    //    对全局95%覆盖率是噪声;
    //  ·几何一律"先CanReserve全案,再MarkUnchecked落账,后刻画"
    //    (镜像IntersticePlanner先规划后刻画),某段放不下即截断:
    //    小室提前到已建段末,奖励不丢;连小室都放不下才以裂砖塌面封口;
    //  ·绳=tile213生成期直写(SHPCCradleGen/Z4Content同款先例,
    //    镜像L2Palette.HangChain契约:顶节永远贴实心正下方,遇实心即停);
    //  ·ctx.Grid管辖=[PlayLeft,PlayRight)x[band.Top,band.Bottom),
    //    预留越带自动被拒,隔离带一格不凿(§1.2)。
    internal static class L2EscapeTunnel
    {
        //==================== 候选宿主登记(L2CellRow干草室调用) ====================

        private struct HostCell
        {
            internal Rectangle Cell;   //囚室内膛(y=内膛顶行,h=净高;地板行=Bottom)
            internal int HayLeft;      //干草铺左缘列
            internal int HayWidth;     //干草铺宽2~3
        }

        private static readonly List<HostCell> Candidates = [];

        /// <summary>回放制重置(L2Content.PlanAndBuild开头调用,镜像L6MachineSlots.Reset纪律)</summary>
        internal static void Reset() => Candidates.Clear();

        /// <summary>干草囚室登记为候选宿主;cellInterior=囚室内膛矩形</summary>
        internal static void RegisterHostCell(Rectangle cellInterior, int hayLeft, int hayWidth)
            => Candidates.Add(new HostCell { Cell = cellInterior, HayLeft = hayLeft, HayWidth = hayWidth });

        //==================== 洞体参数 ====================

        //竖段高度档:计划书草案4~7按实测带几何上调(囚室天花~355行到带顶+8~230行,
        //爬升≈120行远超草案估算的28~40),段数与竖段高度同步放大,见实施偏差记录
        private const int VMin = 8, VMax = 15;
        private const int HMin = 6, HMax = 14;
        //歇脚龛=横段末端越过竖洞口多挖的4列死角
        private const int PocketWidth = 4;
        private const int MaxSegs = 30;
        private const int ChamberW = 8, ChamberH = 5;
        private const int ScratchCap = 8;

        private struct Seg
        {
            internal bool Vertical;
            internal Rectangle Rect;   //空气区;竖段含向上打穿的横段地板行
            internal int Pocket;       //横段末端歇脚龛宽(0=无)
            internal int Dir;          //横段行进方向+1/-1;竖段0
        }

        //==================== 主入口(L2Content层流末端调用) ====================

        internal static void Build(LayerBuildContext ctx, UnifiedRandom rand) {
            if (Candidates.Count == 0) {
                CWRMod.Instance.Logger.Warn("[L2EscapeTunnel] 本次生成无干草囚室候选,越狱洞缺席");
                return;
            }
            //genRand打乱候选序,取首个能起洞的宿主(决定论F22)
            var order = new List<HostCell>(Candidates);
            for (int i = order.Count - 1; i > 0; i--) {
                int j = rand.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            foreach (HostCell host in order) {
                if (TryDig(ctx, host, rand)) {
                    return;
                }
            }
            CWRMod.Instance.Logger.Warn(
                $"[L2EscapeTunnel] {order.Count}间候选全数无法起洞(上方包络被占),越狱洞缺席");
        }

        private static bool TryDig(LayerBuildContext ctx, HostCell host, UnifiedRandom rand) {
            int ceilTop = host.Cell.Y;
            int cellL = host.Cell.X;
            int cellR = host.Cell.X + host.Cell.Width;
            //裂塞2宽,压在干草铺外角正上方:站上干草(+1行)跳起恰可够到塞后的绳尾
            bool hayAtLeft = host.HayLeft <= cellL + 1;
            int plugX = hayAtLeft ? host.HayLeft : host.HayLeft + host.HayWidth - 2;
            plugX = System.Math.Clamp(plugX, cellL, cellR - 2);

            List<Seg> segs = Plan(ctx, rand, plugX, ceilTop, hayAtLeft,
                out Rectangle chamber, out bool chamberAtTop);
            if (segs == null) {
                return false;
            }
            Carve(ctx, host, rand, segs, chamber, chamberAtTop, plugX, ceilTop);
            return true;
        }

        //==================== 规划(纯CanReserve探路,零tile写入零落账) ====================

        private static List<Seg> Plan(LayerBuildContext ctx, UnifiedRandom rand, int plugX,
            int ceilTop, bool hayAtLeft, out Rectangle chamber, out bool chamberAtTop) {
            chamber = Rectangle.Empty;
            chamberAtTop = false;
            //小室地板行=带顶+8:天花蓝砖行恰在band.Top+2,上方仍余2行本带实心+12行隔离带
            int chamberFloor = ctx.Band.Top + 8;

            var segs = new List<Seg>();
            //V1:预留矩形下缘收3行,避开宿主排自己的padding标记带(刻画照常穿过,
            //那两行本就在宿主的账里,镜像IntersticePlanner井段同款处理)
            int vh1 = rand.Next(VMin, VMax + 1);
            int hf = ceilTop - 2 - vh1;
            if (!ctx.Grid.CanReserve(new Rectangle(plugX, hf, 2, vh1 - 3), 1)) {
                return null;
            }
            segs.Add(new Seg { Vertical = true, Rect = new Rectangle(plugX, hf, 2, vh1) });

            int dir = hayAtLeft ? 1 : -1;
            int vx = plugX;
            int nicheBudget = 2;
            for (int guard = 0; guard < MaxSegs; guard++) {
                //横段(3高):含1列尾靠;之字每层反向
                int walk = rand.Next(HMin, HMax + 1);
                bool niche = nicheBudget > 0 && segs.Count >= 3 && rand.NextBool(2);
                int len = walk + (niche ? PocketWidth : 0);
                int hL = dir > 0 ? vx - 1 : vx + 3 - len;
                var h = new Rectangle(hL, hf - 3, len, 3);
                if (!ctx.Grid.CanReserve(h, 1)) {
                    return Truncate(ctx, segs, out chamber, out chamberAtTop);
                }
                if (niche) {
                    nicheBudget--;
                }
                segs.Add(new Seg { Vertical = false, Rect = h, Pocket = niche ? PocketWidth : 0, Dir = dir });

                //下一竖洞口:横段远端回退2列,再让出龛宽(龛是洞口之外的死角)
                int punchX = dir > 0 ? hL + len - 2 - (niche ? PocketWidth : 0)
                                     : hL + (niche ? PocketWidth : 0);
                int remaining = hf - 3 - chamberFloor;
                if (remaining < 4) {
                    return Truncate(ctx, segs, out chamber, out chamberAtTop);
                }
                if (remaining <= VMax) {
                    //末竖段直抵小室地板行,自小室地板洞钻入
                    var vf = new Rectangle(punchX, chamberFloor, 2, remaining);
                    if (!ctx.Grid.CanReserve(vf, 1)
                        || !TryTopChamber(ctx, punchX, dir, out chamber)) {
                        return Truncate(ctx, segs, out chamber, out chamberAtTop);
                    }
                    segs.Add(new Seg { Vertical = true, Rect = vf });
                    chamberAtTop = true;
                    return segs;
                }
                //中途竖段:高度封顶remaining-7,保证末段仍有"横3+竖≥4"的余量
                int vh = System.Math.Min(rand.Next(VMin, VMax + 1), remaining - 7);
                var v = new Rectangle(punchX, hf - 3 - vh, 2, vh);
                if (!ctx.Grid.CanReserve(v, 1)) {
                    return Truncate(ctx, segs, out chamber, out chamberAtTop);
                }
                segs.Add(new Seg { Vertical = true, Rect = v });
                vx = punchX;
                hf = hf - 3 - vh;
                dir = -dir;
            }
            return Truncate(ctx, segs, out chamber, out chamberAtTop);
        }

        //带顶小室:洞口在近端,主体向行进方向展开;放不下换向再试一次
        private static bool TryTopChamber(LayerBuildContext ctx, int punchX, int dir, out Rectangle chamber) {
            foreach (int d in new[] { dir, -dir }) {
                int ciL = d > 0 ? punchX : punchX + 2 - ChamberW;
                var interior = new Rectangle(ciL, ctx.Band.Top + 3, ChamberW, ChamberH);
                //预留含天花蓝砖行与地板行;padding后上缘=band.Top+1,仍在带内
                if (ctx.Grid.CanReserve(new Rectangle(ciL - 1, interior.Y - 1,
                    ChamberW + 2, ChamberH + 2), 1)) {
                    chamber = interior;
                    return true;
                }
            }
            chamber = Rectangle.Empty;
            return false;
        }

        //截断:小室提前平接在最后一段横段尽头(同地板行);再放不下=无小室(封口形态)
        private static List<Seg> Truncate(LayerBuildContext ctx, List<Seg> segs,
            out Rectangle chamber, out bool chamberAtTop) {
            chamber = Rectangle.Empty;
            chamberAtTop = false;
            int lastH = segs.FindLastIndex(static s => !s.Vertical);
            if (lastH < 0) {
                //连一段横段都没有:裸竖井断头不成立,弃此宿主
                return null;
            }
            segs.RemoveRange(lastH + 1, segs.Count - lastH - 1);
            Seg h = segs[lastH];
            int hFloor = h.Rect.Bottom;
            int ciL = h.Dir > 0 ? h.Rect.Right : h.Rect.X - ChamberW;
            var interior = new Rectangle(ciL, hFloor - ChamberH, ChamberW, ChamberH);
            if (ctx.Grid.CanReserve(new Rectangle(ciL, interior.Y - 1, ChamberW, ChamberH + 1), 1)) {
                chamber = interior;
            }
            else if (h.Pocket > 0) {
                //封口形态:末段歇脚龛让位给塌面+退置奖励,免得龛内容撞进封砖
                Seg fixup = h;
                fixup.Pocket = 0;
                segs[lastH] = fixup;
            }
            return segs;
        }

        //==================== 刻画(全案落账后一次成型) ====================

        private static void Carve(LayerBuildContext ctx, HostCell host, UnifiedRandom rand,
            List<Seg> segs, Rectangle chamber, bool chamberAtTop, int plugX, int ceilTop) {
            //统一落账(含padding1,镜像TryReserve的标记口径,填充体系自动让路)
            foreach (Seg s in segs) {
                Mark(ctx, s.Rect);
            }
            if (chamber != Rectangle.Empty) {
                Mark(ctx, new Rectangle(chamber.X - 1, chamber.Y - 1,
                    chamber.Width + 2, chamber.Height + 2));
            }

            int placed = 0, rejected = 0, scratches = 0, carved = 0;

            //裂塞:天花两行换裂粉砖(仍实心,洪泛照旧不透;"裂=可挖"的既有语言)
            for (int dx = 0; dx < 2; dx++) {
                TileBrush.SetSolid(plugX + dx, ceilTop - 2, L2Palette.CrackedBrick);
                TileBrush.SetSolid(plugX + dx, ceilTop - 1, L2Palette.CrackedBrick);
                //塞下锈痕渗进囚室,做旧签名指路
                L2Palette.RustStreak(plugX + dx, ceilTop, rand.Next(2, 4));
            }
            //垫脚罐斜靠干草边(叙事点缀,放不下不计失败)
            int cellFloor = host.Cell.Bottom;
            int potX = host.HayLeft <= host.Cell.X + 1
                ? host.HayLeft + host.HayWidth : host.HayLeft - 1;
            WorldGen.PlacePot(potX, cellFloor - 1, TileID.Pots,
                rand.Next(L2Palette.PotStyleMin, L2Palette.PotStyleMax));

            //洞体
            foreach (Seg s in segs) {
                TileBrush.CarveRect(s.Rect.X, s.Rect.Y, s.Rect.Right, s.Rect.Bottom, L2Palette.WallBase);
                carved += s.Rect.Width * s.Rect.Height;
            }

            //小室
            if (chamber != Rectangle.Empty) {
                CarveChamber(ctx, rand, segs, chamber, chamberAtTop,
                    ref placed, ref rejected, ref scratches);
                carved += chamber.Width * chamber.Height;
            }
            else {
                SealDeadEnd(segs, rand, ref placed, ref rejected);
            }

            //歇脚龛与计数划痕(横段装修)
            foreach (Seg s in segs) {
                if (s.Vertical) {
                    continue;
                }
                int floor = s.Rect.Bottom;
                if (s.Pocket > 0) {
                    int pkL = s.Dir > 0 ? s.Rect.Right - s.Pocket : s.Rect.X;
                    Place(L2Palette.TryPlaceTile(pkL + 1, floor - 1, TileID.Candles,
                        L2Palette.CandleStyle), "龛残烛", pkL + 1, floor - 1, ref placed, ref rejected);
                    Place(WorldGen.PlacePot(pkL + 2, floor - 1, TileID.Pots,
                        rand.Next(L2Palette.PotStyleMin, L2Palette.PotStyleMax)),
                        "龛罐", pkL + 2, floor - 1, ref placed, ref rejected);
                    if (scratches < ScratchCap) {
                        ScratchGroup(pkL, floor - 2, rand.Next(4, 6));
                        scratches++;
                    }
                }
                else if (scratches < ScratchCap && rand.NextBool(2) && s.Rect.Width >= 8) {
                    ScratchGroup(s.Rect.X + rand.Next(1, s.Rect.Width - 6), floor - 2, rand.Next(4, 7));
                    scratches++;
                }
            }

            //绳:每竖段一根,顶节贴实心正下方(横段天花或小室蓝砖),向下穿层到下方地板
            foreach (Seg s in segs) {
                if (!s.Vertical) {
                    continue;
                }
                bool finalV = chamberAtTop && s.Rect.Y == chamber.Bottom;
                int ropeTop = finalV ? chamber.Y : s.Rect.Y - 3;
                HangRope(s.Rect.X, ropeTop, s.Rect.Height + 6);
            }

            int topRow = chamberAtTop ? chamber.Y - 1 : chamber != Rectangle.Empty
                ? chamber.Y : segs[^1].Rect.Y;
            string form = chamberAtTop ? "完整(蓝砖小室)" : chamber != Rectangle.Empty ? "缩短(中途小室)" : "封口(无小室)";
            CWRMod.Instance.Logger.Info(
                $"[L2EscapeTunnel] 落成 host=({host.Cell.X},{host.Cell.Y}) 段数={segs.Count}"
                + $" 爬升={ceilTop - topRow} 形态={form} 划痕组={scratches}"
                + $" 家具={placed}成/{rejected}拒 刻画={carved}格(不入房间图)");
        }

        private static void CarveChamber(LayerBuildContext ctx, UnifiedRandom rand, List<Seg> segs,
            Rectangle chamber, bool atTop, ref int placed, ref int rejected, ref int scratches) {
            TileBrush.CarveRect(chamber.X, chamber.Y, chamber.Right, chamber.Bottom, L2Palette.WallBase);
            bool holeLeft;
            if (atTop) {
                //天花一行手工铺蓝砖:教堂地基的同色砖引用,非穿透(真实L1蓝砖在13+行外)
                for (int x = chamber.X - 1; x <= chamber.Right; x++) {
                    TileBrush.SetSolid(x, chamber.Y - 1, TileID.BlueDungeonBrick);
                }
                //凿痕:蓝砖面白漆细划,凿了一辈子一格没进
                for (int x = chamber.X; x < chamber.Right; x++) {
                    if (!rand.NextBool(3)) {
                        WorldGen.paintTile(x, chamber.Y - 1, PaintID.WhitePaint);
                    }
                }
                holeLeft = chamber.X == segs[^1].Rect.X;
            }
            else {
                //平接形态:入口在贴横段的一侧
                Seg lastH = segs[segs.FindLastIndex(static s => !s.Vertical)];
                holeLeft = lastH.Dir > 0;
            }

            //内容一列排开:洞口/入口侧留空,骨堆|木桶箱|告示
            //(8宽室=洞口2+骨1+箱2+告示2已满配,2宽罐无处落脚:
            // holeLeft侧罐必跨右壁、holeRight侧罐必撞先放的告示,二审删罐)
            int floor = chamber.Bottom;
            int c0 = chamber.X;
            int boneX = holeLeft ? c0 + 2 : c0 + 5;
            int chestX = c0 + 3;
            int signX = holeLeft ? c0 + 5 : c0 + 1;
            Place(WorldGen.PlaceSmallPile(boneX, floor - 1,
                rand.Next(L2Palette.SmallBone1x1Min, L2Palette.SmallBone1x1Max), 0),
                "囚徒遗骸", boneX, floor - 1, ref placed, ref rejected);
            Place(WorldGen.PlaceChest(chestX, floor - 1, TileID.Containers,
                notNearOtherChests: false, L2Palette.ChestBarrelStyle) >= 0,
                "木桶箱", chestX, floor - 1, ref placed, ref rejected);
            Place(PlaceSignWithText(signX, floor - 1,
                atTop ? "数到第四千次钟响。蓝砖还是蓝砖。" : "数到第四千次钟响。还没见到蓝砖。"),
                "遗言告示", signX, floor - 1, ref placed, ref rejected);
            if (scratches < ScratchCap) {
                ScratchGroup(c0 + (holeLeft ? 5 : 1), chamber.Bottom - 2, 5);
                scratches++;
            }
        }

        //封口形态:横段尽头2列3行裂砖塌面,奖励退置洞内
        private static void SealDeadEnd(List<Seg> segs, UnifiedRandom rand,
            ref int placed, ref int rejected) {
            Seg h = segs[segs.FindLastIndex(static s => !s.Vertical)];
            int floor = h.Rect.Bottom;
            int sealL = h.Dir > 0 ? h.Rect.Right - 2 : h.Rect.X;
            for (int dx = 0; dx < 2; dx++) {
                for (int dy = 1; dy <= 3; dy++) {
                    TileBrush.SetSolid(sealL + dx, floor - dy, L2Palette.CrackedBrick);
                }
            }
            //退置奖励:避开段首竖洞口(2列)与封砖(2列),段宽不足9时只封不放
            if (h.Rect.Width >= 9) {
                int chestX = h.Dir > 0 ? h.Rect.X + 4 : h.Rect.Right - 6;
                Place(WorldGen.PlaceChest(chestX, floor - 1, TileID.Containers,
                    notNearOtherChests: false, L2Palette.ChestBarrelStyle) >= 0,
                    "木桶箱(塌方前)", chestX, floor - 1, ref placed, ref rejected);
            }
        }

        //==================== 原语 ====================

        //落账口径与TryReserve(padding1)一致;段间/段与小室的重叠标记幂等无害
        private static void Mark(LayerBuildContext ctx, Rectangle rect)
            => ctx.Grid.MarkUnchecked(new Rectangle(rect.X - 1, rect.Y - 1,
                rect.Width + 2, rect.Height + 2));

        /// <summary>
        /// 自(x,yTop)向下直写绳tile,遇实心即停,返回节数。
        /// 调用方保证yTop上方实心(顶锚构造成立);镜像L2Palette.HangChain契约,
        /// 生成期直写先例=SHPCCradleGen锚绳/Z4Content垂绳。
        /// </summary>
        private static int HangRope(int x, int yTop, int maxLen) {
            int hung = 0;
            for (int i = 0; i < maxLen; i++) {
                int y = yTop + i;
                if (!WorldGen.InWorld(x, y, 5)) {
                    break;
                }
                Tile tile = Main.tile[x, y];
                if (tile.HasTile) {
                    break;
                }
                tile.HasTile = true;
                tile.TileType = TileID.Rope;
                tile.Slope = SlopeType.Solid;
                tile.IsHalfBlock = false;
                tile.LiquidAmount = 0;
                hung++;
            }
            return hung;
        }

        //计数划痕:棕漆短竖线成组(与锈渍垂痕同构不同义,限本洞独有,不进撒布)
        private static void ScratchGroup(int x, int y, int strokes) {
            for (int k = 0; k < strokes; k++) {
                WorldGen.paintWall(x + k, y, L2Palette.RustPaint);
                if ((k & 1) == 0) {
                    WorldGen.paintWall(x + k, y - 1, L2Palette.RustPaint);
                }
            }
        }

        //告示牌+文本(L2Palette无此helper,镜像L4Palette/L6Palette同款6行实现)
        private static bool PlaceSignWithText(int x, int standRow, string text) {
            if (!WorldGen.PlaceSign(x, standRow, TileID.Signs)) {
                return false;
            }
            int sign = Sign.ReadSign(x, standRow);
            if (sign >= 0) {
                Sign.TextSign(sign, text);
            }
            return true;
        }

        private static void Place(bool ok, string what, int x, int y,
            ref int placed, ref int rejected) {
            if (ok) {
                placed++;
            }
            else {
                rejected++;
                CWRMod.Instance.Logger.Warn($"[L2EscapeTunnel] {what}放置失败 at ({x},{y})");
            }
        }

        //==================== 免接线看样入口(镜像L2Preview惯例,单人调试用) ====================

        /// <summary>
        /// 在(originX, floorRow)处就地盖看样:底部假干草囚室+缩比爬升(约56行)+带顶蓝砖小室。
        /// 不注册GenPass、不入图;仅单人调试(联机不发tile同步)。
        /// </summary>
        internal static void BuildPreview(int originX, int floorRow, int seed = 1919) {
            if (Main.netMode != NetmodeID.SinglePlayer) {
                CWRMod.Instance.Logger.Warn("[L2EscapeTunnel] 看样入口仅单人调试用,联机不发tile同步");
            }
            var rand = new UnifiedRandom(seed);
            var area = new Rectangle(originX - 45, floorRow - 68, 90, 72);
            for (int x = area.Left; x < area.Right; x++) {
                for (int y = area.Top; y < area.Bottom; y++) {
                    TileBrush.SetSolid(x, y, L2Palette.Brick);
                }
            }
            //假带上下文:带顶=看样区顶,洞体规划照走正式路径;区外两翼整体标死防越界
            var band = new LayerBand("L2越狱洞看样", area.Top, area.Height, L2Palette.Brick, L2Palette.WallBase);
            var ctx = new LayerBuildContext(band);
            ctx.Grid.MarkUnchecked(new Rectangle(DungeonworldMetrics.PlayLeft, area.Top,
                area.Left - DungeonworldMetrics.PlayLeft, area.Height));
            ctx.Grid.MarkUnchecked(new Rectangle(area.Right, area.Top,
                DungeonworldMetrics.PlayRight - area.Right, area.Height));

            //假干草囚室(8宽x6高内膛+干草铺,正式排的最小要素)
            int cellL = originX - 4;
            int ceil = floorRow - 6;
            TileBrush.CarveRect(cellL, ceil, cellL + 8, floorRow, L2Palette.WallBase);
            int hayL = cellL + 1;
            for (int dx = 0; dx < 3; dx++) {
                TileBrush.SetSolid(hayL + dx, floorRow - 1, TileID.HayBlock);
            }
            Reset();
            RegisterHostCell(new Rectangle(cellL, ceil, 8, 6), hayL, 3);
            Build(ctx, rand);
            Reset();
            WorldGen.RangeFrame(area.Left - 1, area.Top - 1, area.Right + 1, area.Bottom + 1);
            CWRMod.Instance.Logger.Info($"[L2EscapeTunnel] 看样落成 area={area}");
        }
    }
}
