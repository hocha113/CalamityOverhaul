using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L7
{
    //====================================================================
    //L7倒吊教堂层内容入口（Wave-2接缝契约，契约全文见LayerBuildContext头注释）。
    //空间叙事（ROOMS-L7/任务brief）：玩家沿主竖井穿过链束锚点带下行，
    //自前庭西门踏上锁链渡桥，看见整座"朝下生长"的教堂吊在深渊上方。
    //
    //布局（x全部自ShaftLeft推导，y全部自层带行推导，预览可整体搬移）：
    //  [悬吊空腔]←26格空隙→[倒吊教堂142宽]←24格渡桥→[前庭(岩肩内)]→主竖井
    //  空腔顶=带顶+18（锚点带），空腔底=脊内膛顶-2（M0层脊保持原样，本层不碰
    //  竖井/层脊几何，唯一例外是前庭东隧道在竖井西壁开3高口，属自身接驳）。
    //  垂钟井自教堂底探入空腔下部，垂钟龛底=全层最低可通行点（花名册#5）。
    //深渊带薄装（任务brief条目3）：层脊地板下方另凿全封闭剪影厅（不与任何可达
    //空间连通，红线断言兜底），垂链末端+悬空砖构残片+黑暗留白；L7→深渊的
    //正式开口方式归管线路裁决，剪影厅即其预铺演出面。
    //纪律：零陷阱；链束≥3宽且避让2x3通行包络（P80洪泛把链视为不可通行）；
    //撒布全定点=零声明；本入口零genRand消耗（全定点布局，R4决定论最安全解）。
    //====================================================================
    internal static class L7Content
    {
        //===布局常量（相对锚：x=ShaftLeft，y=band.Top；internal给看样入口复用）===
        //悬吊空腔：x[ShaftLeft-218, ShaftLeft-26)，宽192
        internal const int VoidLeftOff = -218;
        internal const int VoidRightOff = -26;
        //空腔顶距带顶18行（其上为链束锚点带，主竖井穿行其中）
        internal const int VoidTopOff = 18;
        //教堂：左缘=空腔左+26（西空隙26≥20【事实§2.4-⑦】），顶=空腔顶+32（链束吊距）
        internal const int CathLeftOff = VoidLeftOff + 26;
        internal const int CathTopOff = VoidTopOff + 32;
        //前庭内膛（岩肩内，介于空腔与主竖井之间）
        private const int VestInteriorH = 9;

        //深渊带剪影厅：顶=带底+4（层脊地板2厚+2余量之下），底=带底+184
        //（世界行5604~5784，距地狱线5800余量16行，红线断言兜底）
        internal const int AbyssTopOff = 4;
        internal const int AbyssBottomOff = 184;

        /// <summary>
        /// L7一条龙构建：空腔→前庭/渡桥→倒吊教堂（镜像+倒相定制）→链束→
        /// 深渊剪影厅→图登记→撒布声明（空）。
        /// <para/>管线路/父级一行接线（LayerContentPass的L7槽位）：
        /// <code>Layers.L7.L7Content.PlanAndBuild(LayerPlans.L7);</code>
        /// <para/>前置依赖：P10骨架+P20（主竖井贯穿L7、层脊）+P30（L7上下文就绪）。
        /// 本入口不注册GenPass、不改管线文件；层脊与主竖井几何原样保留
        /// （P80"每层脊可达"断言与竖井平台桥不受影响）。
        /// </summary>
        internal static void PlanAndBuild(LayerBuildContext ctx) {
            LayerBand band = ctx.Band;
            LayerBand l7 = DungeonworldMetrics.Bands[^1];
            if (band.Top != l7.Top || band.Bottom != l7.Bottom) {
                throw new System.InvalidOperationException(
                    $"[L7] 入口拿到的层带[{band.Top},{band.Bottom})不是L7[{l7.Top},{l7.Bottom})，接线错层");
            }

            //===红线断言（fail loud）：可达空间不越地狱判定带（F21/§1.3）===
            int hellRow = DungeonworldMetrics.Height - 200;
            int voidBottom = band.SpineInteriorTop - 2;
            int abyssBottom = band.Bottom + AbyssBottomOff;
            int deepestReachable = band.Top + CathTopOff + L7InvertedCathedral.TotalDepth;
            if (voidBottom >= band.Bottom || deepestReachable + 8 > voidBottom) {
                throw new System.InvalidOperationException(
                    $"[L7] 垂钟龛底{deepestReachable}逼近空腔底{voidBottom}，悬吊构图被压扁，检查层带行数预算");
            }
            if (deepestReachable >= hellRow) {
                throw new System.InvalidOperationException(
                    $"[L7] 可达最深点{deepestReachable}越过地狱线{hellRow}（管线路裁决：可达不得过y={hellRow}）");
            }
            if (abyssBottom + 8 > hellRow) {
                throw new System.InvalidOperationException(
                    $"[L7] 深渊剪影厅底{abyssBottom}越过地狱线{hellRow}余量红线（ROOMS-L7 §0）");
            }

            BuildComposition(ctx.Grid, ctx.Graph, DungeonworldMetrics.ShaftLeft, band.Top,
                band.SpineInteriorTop, includeAbyss: true);

            //撒布：本层全定点（ROOMS-L7禁用清单），声明为空并留档
            ctx.Scatter.AddRange(L7Style.LayerScatter());
            //Boss舞台开阔区+全空腔列为撒布禁区（§3.2-7；防跨层通用条目误入）
            int voidLeft = DungeonworldMetrics.ShaftLeft + VoidLeftOff;
            LayerPlans.ScatterExclusions.Add(new Rectangle(
                voidLeft - 2, band.Top, 194 + 30, band.Bottom - band.Top));
            CWRMod.Instance.Logger.Info(
                "[L7Content] 撒布声明=0条（本层全定点，ROOMS-L7量产brief禁用清单）；genRand消耗=0（全定点布局）");
        }

        //==================== 主构建（gen与预览共用，坐标全参数化）====================

        /// <summary>
        /// spineInteriorTop=层脊内膛顶行（空腔底=其-2）；includeAbyss=是否凿深渊剪影厅
        /// （预览需在其下预留约190行深度）。
        /// </summary>
        internal static void BuildComposition(OccupancyGrid grid, RoomGraph graph,
            int shaftLeft, int bandTop, int spineInteriorTop, bool includeAbyss) {
            int voidL = shaftLeft + VoidLeftOff;
            int voidR = shaftLeft + VoidRightOff;
            int voidTop = bandTop + VoidTopOff;
            int voidBottom = spineInteriorTop - 2;
            int cathLeft = shaftLeft + CathLeftOff;
            int cathTop = bandTop + CathTopOff;
            int cathRight = cathLeft + L7InvertedCathedral.ArtW;
            int deckRow = cathTop;                     //桥面与教堂顶板同行，走线连续
            int baseIndex = graph.Rooms.Count;

            //整片构图带登记为既成事实（演出型唯一建筑，沿用L1教堂群落先例）
            grid.MarkUnchecked(new Rectangle(voidL - 2, bandTop, shaftLeft - voidL + 2, spineInteriorTop - bandTop));

            //===1) 悬吊空腔：全Tiled墙（群系F13+派系2终层杂怪F28），链束锚点带留实===
            TileBrush.CarveRect(voidL, voidTop, voidR, voidBottom, L7Style.Wall);

            //===2) 前庭（花名册#1，岩肩内开凿；内嵌忏悔位=公共构件换皮保守解）===
            RoomNode vestibule = BuildVestibule(voidR, shaftLeft, deckRow);

            //===3) 锁链渡桥（花名册#2）：平台桥面+桥下悬链承重（护柱转桥下悬柱，
            //     桥面不设阻断，P80包络洪泛不许1格路障）===
            RoomNode bridge = BuildBridge(cathRight, voidR, deckRow, voidTop);

            //===4) 倒吊教堂全套（镜像+倒相定制+垂钟井+垂钟龛+终钟剪影）===
            L7InvertedCathedral.Build(cathLeft, cathTop);

            //===5) 空腔级链束：吊持读法（西端锚顶链落顶板）+侧腹垂链+龛底救援链===
            int chainCells = PlaceVoidChains(voidL, voidTop, cathLeft, cathTop, voidBottom);

            //===6) 深渊带剪影薄装（全封闭，不与可达空间连通）===
            if (includeAbyss) {
                BuildAbyssSilhouette(cathLeft, bandTop + 220 <= voidBottom ? voidBottom : spineInteriorTop);
            }

            //===7) 前庭做旧+图登记===
            L7Style.AgeInvertedInRect(vestibule.Bounds);
            var pod = new RoomNode {
                Bounds = new Rectangle(cathLeft + 119, cathTop + 3, 23, 15),
                Role = RoomRole.Normal,
            };
            var vault = new RoomNode {
                Bounds = new Rectangle(cathLeft, cathTop, 42, 31),
                Role = RoomRole.Treasure,
            };
            var niche = new RoomNode {
                Bounds = new Rectangle(cathLeft + L7InvertedCathedral.TubeWallL,
                    cathTop + L7InvertedCathedral.NicheTop, 18, L7InvertedCathedral.NicheHeight),
                Role = RoomRole.Normal,
            };
            graph.Rooms.Add(vestibule);
            graph.Rooms.Add(bridge);
            graph.Rooms.Add(pod);
            graph.Rooms.Add(vault);
            graph.Rooms.Add(niche);
            //边=实际通行链：前庭→桥→东厢舱(顶板天窗)→终库(舞台+库房天窗)→垂钟龛(井)
            graph.Edges.Add(new RoomEdge(baseIndex, baseIndex + 1, SocketKind.Door, EdgeForm.Horizontal));
            graph.Edges.Add(new RoomEdge(baseIndex + 1, baseIndex + 2, SocketKind.Archway, EdgeForm.Horizontal));
            graph.Edges.Add(new RoomEdge(baseIndex + 2, baseIndex + 3, SocketKind.Archway, EdgeForm.Horizontal));
            graph.Edges.Add(new RoomEdge(baseIndex + 2, baseIndex + 4, SocketKind.PlatformGap, EdgeForm.StairWell));

            CWRMod.Instance.Logger.Info(
                $"[L7Content] 倒吊教堂层落成 空腔=({voidL},{voidTop})~({voidR},{voidBottom})"
                + $" 教堂origin={L7InvertedCathedral.LastOrigin} 空腔链束格={chainCells}"
                + $" nodes+5(前庭/渡桥/东厢舱/终库/垂钟龛,倒吊中殿为不入图名义大节点)"
                + $" edges+4 graphConnected={graph.IsConnected()}");
        }

        //前庭：西门通渡桥、东隧道穿岩肩接主竖井西壁（唯一的竖井接驳口）
        private static RoomNode BuildVestibule(int voidR, int shaftLeft, int deckRow) {
            int inL = voidR + 2;              //内膛左（西壁=voidR..+2）
            int inR = shaftLeft - 6;          //内膛右（东壁+隧道岩体6厚）
            int inTop = deckRow - VestInteriorH;
            TileBrush.CarveRect(inL, inTop, inR, deckRow, L7Style.Wall);
            //西门洞（3高，通桥面）
            TileBrush.CarveRect(voidR, deckRow - 3, inL, deckRow, L7Style.Wall);
            //东隧道（3高，穿岩肩入竖井；竖井z字平台每4行一档，行高对齐可踏入）
            TileBrush.CarveRect(inR, deckRow - 3, shaftLeft, deckRow, L7Style.Wall);
            //隧道口门板（决战前检查点的"门"语义；F4上下实心由岩体构造满足）
            L7Style.PlaceDoorPlate(inR, deckRow - 1);
            L7Style.PaintTileArea(inR, deckRow - 3, inR, deckRow - 1, TileID.ClosedDoor, L7Style.PaintPurple);

            //忏悔位+检查点陈设（长椅/烛台/画/仪式光/告示，全定点）
            int stand = deckRow - 1;
            if (!L7Style.PlaceSignWithText(inL + 2, stand, L7Style.SignVestibule)) {
                CWRMod.Instance.Logger.Warn("[L7] 前庭告示放置失败,跳过");
            }
            if (!L7Style.TryPlaceTile(inL + 4, stand, TileID.Benches, L7Style.StyleBench)) {
                CWRMod.Instance.Logger.Warn("[L7] 前庭长椅放置失败,跳过");
            }
            L7Style.TryPlaceTile(inL + 8, stand, TileID.Candelabras, L7Style.StyleCandelabra);
            if (!L7Style.TryPlaceTile(inL + 12, stand, TileID.Benches, L7Style.StyleBench)) {
                CWRMod.Instance.Logger.Warn("[L7] 忏悔位长椅放置失败,跳过");
            }
            L7Style.TryPlaceTile(inL + 14, stand, TileID.Candles, L7Style.StyleCandle);
            L7Style.PlaceTorch(inL + 1, deckRow - 4, L7Style.TorchDemon);
            L7Style.PlacePainting(inL + 9, deckRow - 6);
            //忏悔位冥紫染圆斑（做旧签名的染色半边）
            L7Style.PurpleWallDisk(inL + 13, deckRow - 3, 2);

            return new RoomNode {
                Bounds = new Rectangle(voidR, inTop - 2, shaftLeft - voidR, VestInteriorH + 4),
                Role = RoomRole.Safe,
            };
        }

        //渡桥：平台桥面（与顶板同行）+两端桥下牛腿+3厚悬链弧（承重读法）
        private static RoomNode BuildBridge(int cathRight, int voidR, int deckRow, int voidTop) {
            int span = voidR - cathRight;
            TileBrush.PlatformRow(cathRight, voidR, deckRow, L7Style.PlatformFrameY);
            //桥下牛腿（两端2列×2行）
            for (int x = cathRight; x < cathRight + 2; x++) {
                TileBrush.SetSolid(x, deckRow + 1, L7Style.Brick);
                TileBrush.SetSolid(x, deckRow + 2, L7Style.Brick);
            }
            for (int x = voidR - 2; x < voidR; x++) {
                TileBrush.SetSolid(x, deckRow + 1, L7Style.Brick);
                TileBrush.SetSolid(x, deckRow + 2, L7Style.Brick);
            }
            //悬链弧：抛物线垂度8，3格厚链带（桥中点视野向下打开=桥面下即深渊）
            for (int i = 0; i < span; i++) {
                int x = cathRight + i;
                double t = (i - (span - 1) * 0.5) / ((span - 1) * 0.5);
                int dip = (int)System.Math.Round(8.0 * (1.0 - t * t));
                for (int k = 0; k < 3; k++) {
                    int y = deckRow + 3 + dip - k;
                    if (y <= deckRow + 2 || Main.tile[x, y].HasTile) {
                        continue;
                    }
                    Tile tile = Main.tile[x, y];
                    tile.HasTile = true;
                    tile.TileType = TileID.Chain;
                    tile.Slope = SlopeType.Solid;
                    tile.IsHalfBlock = false;
                    tile.LiquidAmount = 0;
                }
            }
            //桥头仪式火把（教堂侧落在顶板上，光的引线：前庭→桥→天窗）
            L7Style.PlaceTorch(cathRight - 2, deckRow - 2, L7Style.TorchDemon);

            return new RoomNode {
                Bounds = new Rectangle(cathRight, deckRow - 4, voidR - cathRight, 8),
                Role = RoomRole.Normal,
            };
        }

        //空腔级链束：全部≥3宽；顶板行走线（库房天窗x+16~20以东至渡桥）零遮挡
        private static int PlaceVoidChains(int voidL, int voidTop, int cathLeft, int cathTop, int voidBottom) {
            int cells = 0;
            //吊持主链×2：锚在空腔顶岩（锚点带），落在顶板西端头（天窗以西的
            //死端段，不挡库房天窗→渡桥的洪泛包络走线）
            cells += L7Style.ChainBundle(cathLeft + 2, 3, voidTop, cathTop - voidTop);
            cells += L7Style.ChainBundle(cathLeft + 8, 3, voidTop, cathTop - voidTop);
            //西侧腹垂链：贴船体西舷坠入黑暗（吊具余链读法）
            cells += L7Style.ChainBundle(voidL + 10, 3, voidTop, 84);
            //龛底救援链：垂钟龛地板下缘→空腔底（坠桥幸存者的归途，兼"钟垂在
            //深渊上方"的构图延长线）
            int nicheFloorTop = cathTop + L7InvertedCathedral.NicheTop + 11;
            cells += L7Style.ChainBundleBelowSolid(cathLeft + 69, 3,
                nicheFloorTop, nicheFloorTop + 2, voidBottom - nicheFloorTop - 2);
            return cells;
        }

        //深渊带剪影厅：层脊地板下方全封闭空腔（无墙=黑暗留白），
        //垂链末端+悬空砖构残片；正式开口归管线路裁决，本厅为预铺演出面
        private static void BuildAbyssSilhouette(int cathLeft, int sealTopRef) {
            //以L7带底为基（gen路径），预览路径由L7Preview显式调用重载
            LayerBand band = DungeonworldMetrics.Bands[^1];
            BuildAbyssSilhouetteAt(cathLeft, band.Bottom + AbyssTopOff, band.Bottom + AbyssBottomOff);
            _ = sealTopRef;
        }

        /// <summary>剪影厅本体（预览复用）：[top,bottom)行、教堂正下方x带宽124</summary>
        internal static void BuildAbyssSilhouetteAt(int cathLeft, int top, int bottom) {
            int left = cathLeft + 10;
            int right = cathLeft + 134;
            //无墙清空：纯黑留白（不可达，群系无涉）
            TileBrush.CarveRect(left, top, right, bottom, WallID.None);

            //垂落的巨链末端（锚=厅顶岩体，参差三束）
            L7Style.ChainBundle(left + 8, 3, top, 48);
            L7Style.ChainBundle(left + 44, 4, top, 78);
            L7Style.ChainBundle(left + 84, 3, top, 36);

            //悬空砖构残片：断拱三段（2行厚+两端斜切，§3.2-6"预制破损贴片成组≥3格"）
            void Fragment(int fx, int fw, int fy) {
                TileBrush.SetSloped(fx, fy + 1, L7Style.Brick, SlopeType.SlopeUpRight);
                for (int x = fx + 1; x < fx + fw - 1; x++) {
                    TileBrush.SetSolid(x, fy, L7Style.Brick);
                    TileBrush.SetSolid(x, fy + 1, L7Style.Brick);
                }
                TileBrush.SetSloped(fx + fw - 1, fy + 1, L7Style.Brick, SlopeType.SlopeUpLeft);
            }
            Fragment(left + 24, 10, top + 96);
            Fragment(left + 62, 8, top + 60);
            Fragment(left + 92, 9, top + 122);
            //残片链尾（挂在断拱下的余链）
            L7Style.ChainBundleBelowSolid(left + 26, 3, top + 94, top + 100, 16);

            //残片同调冥紫（剪影在玩家光源下仍读得出层身份）
            L7Style.PurpleSweep(new Rectangle(left, top, right - left, bottom - top));

            CWRMod.Instance.Logger.Info(
                $"[L7Content] 深渊剪影厅落成 [{left},{top})~[{right},{bottom}) 全封闭（红线断言已过）");
        }
    }
}
