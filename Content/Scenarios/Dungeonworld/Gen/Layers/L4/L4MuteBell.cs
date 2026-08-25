using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L4
{
    //====================================================================
    //哑钟沉窖(Wave-2 A1,WAVE2-BUILDINGS §3.2)
    //第四组服务井壁上一个做旧的门洞,进去是干燥前室;地板正中盖平台的竖井口
    //往下全是水:全淹封存窖水底立着铸废的第八口青铜大钟。潜进钟底挖开的坑道,
    //从钟口上浮,钟腔里是干的:石台、蜡烛、罐、箱、告示,有人住过。
    //
    //挂点:L4Content.PlanAndBuild 步骤5之后、步骤6水体写入之前一行接线
    //(舱段登记必须赶上FillState/settle/AssertBandWater/PaintAging,双水线漆白送);
    //随机消耗集中于本入口(R4)。
    //
    //气室原理(对源L4WaterWorks头注+BuildPlungeWell气龛先例):原版液体只向下/侧向
    //流动,无压力上溯,开口朝下的钟腔在钟口行以上恒为空气;FillState的盲写由
    //AirPockets豁免挡住。恒干与否标"待游戏内检查"。
    //
    //避让纪律:湿port走廊/干link等"已刻画未落账"几何用空气扫描兜底;
    //IMPL-C泄洪堂P30先占位,本体TryReserve自动让位,候选全败=Warn缺席(合法结局)。
    //耗时心算:刻画约2.5千格+水写入约1千格,毫秒级(R5)。
    //====================================================================
    internal static class L4MuteBell
    {
        //===尺寸旋钮(集中常量区)===
        private const int AnteW = 10;   //前室内膛宽
        private const int AnteH = 5;    //前室内膛高
        //钟体总宽15/总高16:钟腔净宽11才摆得开石台上"蜡烛+罐+箱+告示"全套
        //(计划11w x 13h在2D里家具摆不下,偏差记录见WAVE2-BUILDINGS)
        private const int BellW = 15;
        private const int BellH = 16;
        //窖底坑道深4行:钟舌坑+潜行通道(隐士挖的进钟路,3高可泳)
        private const int PitDepth = 4;

        //==================== 主入口 ====================

        /// <summary>
        /// 贴第四组服务井落哑钟沉窖:右翼井优先左翼井备选,每井先试井右侧再试井左侧;
        /// 候选全败Warn缺席。成功时登记水体舱段+钟腔气室豁免,窖体/前室入图(Rooms+2)。
        /// </summary>
        internal static bool TryBuild(LayerBuildContext ctx, int waterline, int nextFloor,
            int serviceLeft, int serviceRight, UnifiedRandom rand) {
            //随机前置一次掷完(R4:消耗集中,失败路径不再掷)
            int cw = rand.Next(32, 35);                 //窖内膛宽
            int ch = rand.Next(22, 27);                 //窖内膛高
            int bellJitter = rand.Next(0, cw - 30 + 1); //钟体横向抖动
            int potStyleA = rand.Next(L4Palette.PotStyleMin, L4Palette.PotStyleMax);
            int potStyleB = rand.Next(L4Palette.PotStyleMin, L4Palette.PotStyleMax);
            int mossSeed = rand.Next(3);

            //前室地板对齐服务井平台节奏(CarveStairWell平台行=NextFloor-4k),跨步进洞地板齐平;
            //再钳到DryFloor+4以下,防前室顶壳蹭到干层房间的预留padding
            int anteFloor = AlignToWellPlatform(waterline - 3, nextFloor);
            if (anteFloor < waterline - 5) {
                anteFloor += 4;
            }
            //窖顶恒在湿带内(计划:Waterline+6起),竖井长度随之自适应
            int shaftLen = waterline + 6 - anteFloor;
            int cellarTop = anteFloor + shaftLen;       //窖内膛顶行
            int cellarFloor = cellarTop + ch;           //窖地板首行
            int surfaceRow = cellarTop + 2;             //满水态水面行(窖顶+2)

            foreach (int wellX in new[] { serviceRight, serviceLeft }) {
                if (wellX < 0) {
                    continue;
                }
                foreach (bool onRight in new[] { true, false }) {
                    int unionW = cw + 4;
                    int unionLeft = onRight
                        ? wellX + DungeonworldMetrics.StairWellWidth + 1
                        : wellX - 1 - unionW;
                    var union = new Rectangle(unionLeft, anteFloor - AnteH - 2,
                        unionW, cellarFloor + PitDepth + 2 - (anteFloor - AnteH - 2));
                    if (HitsShaft(union.Left, union.Right)
                        || !ctx.Grid.CanReserve(union, 0)
                        || AnyAirInside(union)) {
                        continue;
                    }
                    if (!ctx.Grid.TryReserve(union, 0)) {
                        continue; //刚过CanReserve,理论不该到这,双保险
                    }
                    Commit(ctx, union, wellX, onRight, anteFloor, cellarTop, cellarFloor,
                        surfaceRow, cw, ch, bellJitter, potStyleA, potStyleB, mossSeed, rand);
                    return true;
                }
            }
            CWRMod.Instance.Logger.Warn(
                $"[L4MuteBell] 服务井两翼候选全败(左{serviceLeft}/右{serviceRight}),本种子无哑钟(合法缺席)");
            return false;
        }

        //最大的 nextFloor-4k 且 ≤ target:与井内平台行严格同相位
        private static int AlignToWellPlatform(int target, int nextFloor) {
            int k = (nextFloor - target + 3) / 4;
            return nextFloor - 4 * k;
        }

        private static bool HitsShaft(int left, int right)
            => left < DungeonworldMetrics.ShaftLeft + DungeonworldMetrics.ShaftWidth + 4
            && right > DungeonworldMetrics.ShaftLeft - 4;

        //空气扫描:湿port走廊/干link/检修龛这类"已刻画未落账"几何的兜底避让
        private static bool AnyAirInside(Rectangle rect) {
            for (int x = rect.Left; x < rect.Right; x++) {
                for (int y = rect.Top; y < rect.Bottom; y++) {
                    if (WorldGen.InWorld(x, y, 5) && !Main.tile[x, y].HasTile) {
                        return true;
                    }
                }
            }
            return false;
        }

        //==================== 落成 ====================

        private static void Commit(LayerBuildContext ctx, Rectangle union, int wellX, bool onRight,
            int anteFloor, int cellarTop, int cellarFloor, int surfaceRow,
            int cw, int ch, int bellJitter, int potStyleA, int potStyleB, int mossSeed,
            UnifiedRandom rand) {

            var tally = new L4Rooms.Tally();
            BellBuild build = BuildBody(union, onRight, anteFloor, cellarTop, cellarFloor,
                cw, ch, bellJitter, potStyleA, potStyleB, mossSeed, rand, ref tally);

            //服务井侧洞:3高门洞打进井壁(洞沿走廊framing§2.5,门底行与井内平台同相位齐平)
            CarveWellDoor(union, wellX, onRight, anteFloor);

            //舱段登记:满水=窖顶+2,排水=窖底(排空后钟立干窖,坑道存残水,气室豁免自动失义无副作用);
            //Area含坑道行,AssertBandWater的"舱段外游水"审计构造性为零
            L4WaterWorks.Compartment comp = L4WaterWorks.Register("哑钟沉窖",
                build.WetArea, surfaceRow, cellarFloor);
            comp.AirPockets.Add(build.ChamberPocket);

            //入图:前室+窖体两节点;井门记自边(镜像LinkWellSide成规),前室↔窖体走竖井边
            var ante = new RoomNode { Bounds = build.AnteBounds };
            var cellar = new RoomNode { Bounds = build.CellarBounds };
            int anteIdx = ctx.Graph.Rooms.Count;
            ctx.Graph.Rooms.Add(ante);
            int cellarIdx = ctx.Graph.Rooms.Count;
            ctx.Graph.Rooms.Add(cellar);
            ante.Sockets.Add(new DoorSocket(onRight ? SocketSide.Left : SocketSide.Right,
                anteFloor - 3 - build.AnteBounds.Top, SocketKind.Door, 3));
            ante.Sockets.Add(new DoorSocket(SocketSide.Bottom,
                build.ShaftX - build.AnteBounds.Left, SocketKind.PlatformGap, 3));
            cellar.Sockets.Add(new DoorSocket(SocketSide.Top,
                build.ShaftX - build.CellarBounds.Left, SocketKind.ShaftMouth, 3));
            ctx.Graph.Edges.Add(new RoomEdge(anteIdx, anteIdx, SocketKind.Door, EdgeForm.Horizontal));
            ctx.Graph.Edges.Add(new RoomEdge(anteIdx, cellarIdx, SocketKind.ShaftMouth, EdgeForm.StairWell));

            CWRMod.Instance.Logger.Info(
                $"[L4MuteBell] 落成 井x={wellX} 侧={(onRight ? "井右" : "井左")} 前室floor={anteFloor}"
                + $" 窖={build.CellarBounds} 钟origin=({build.BellLeft},{cellarFloor - BellH})"
                + $" 水面row={surfaceRow} 气室={build.ChamberPocket} 家具={tally.Placed}成/{tally.Rejected}拒");
        }

        //门洞3高+过梁/门槛framing+灰漆做旧(几乎涂成岩体的颜色,叙事:封存者不想让人找到)
        private static void CarveWellDoor(Rectangle union, int wellX, bool onRight, int anteFloor) {
            int doorL = onRight ? wellX + DungeonworldMetrics.StairWellWidth : union.Right - 2;
            int doorR = doorL + 3;
            for (int x = doorL; x < doorR; x++) {
                TileBrush.SetSolid(x, anteFloor - 4, L4Palette.Brick);
                TileBrush.SetSolid(x, anteFloor, L4Palette.Brick);
            }
            TileBrush.CarveRect(doorL, anteFloor - 3, doorR, anteFloor, L4Palette.WallBase);
            for (int x = doorL; x < doorR; x++) {
                WorldGen.paintTile(x, anteFloor - 4, L4Palette.HighLinePaint);
                WorldGen.paintTile(x, anteFloor, L4Palette.HighLinePaint);
                if (Main.tile[x, anteFloor - 2].WallType != WallID.None) {
                    WorldGen.paintWall(x, anteFloor - 2, L4Palette.HighLinePaint);
                }
            }
        }

        //==================== 窖体本体(纯几何+家具,不碰舱段登记/图,预览复用) ====================

        internal struct BellBuild
        {
            internal Rectangle AnteBounds;
            internal Rectangle CellarBounds;
            internal Rectangle WetArea;        //舱段Area(窖内膛+坑道行)
            internal Rectangle ChamberPocket;  //钟腔气室豁免矩形
            internal int ShaftX;               //前室↔窖体竖井左列
            internal int BellLeft;
        }

        private static BellBuild BuildBody(Rectangle union, bool onRight, int anteFloor,
            int cellarTop, int cellarFloor, int cw, int ch, int bellJitter,
            int potStyleA, int potStyleB, int mossSeed, UnifiedRandom rand, ref L4Rooms.Tally tally) {

            var build = new BellBuild();
            //1) 整块重盖绿砖(消掉带内杂色,顺带把死角读成"厚壁封存窖")
            for (int x = union.Left; x < union.Right; x++) {
                for (int y = union.Top; y < union.Bottom; y++) {
                    TileBrush.SetSolid(x, y, L4Palette.Brick);
                }
            }

            //2) 前室(贴井侧)+窖体内膛
            int anteIntLeft = onRight ? union.Left + 2 : union.Right - 2 - AnteW;
            build.AnteBounds = new Rectangle(
                onRight ? union.Left : union.Right - AnteW - 4,
                anteFloor - AnteH - 2, AnteW + 4, AnteH + 4);
            TileBrush.CarveRect(anteIntLeft, anteFloor - AnteH, anteIntLeft + AnteW, anteFloor,
                L4Palette.WallBase);
            int cellarIntLeft = union.Left + 2;
            int cellarIntRight = cellarIntLeft + cw;
            build.CellarBounds = new Rectangle(union.Left, cellarTop - 2, cw + 4, ch + 4);
            TileBrush.CarveRect(cellarIntLeft, cellarTop, cellarIntRight, cellarFloor,
                L4Palette.WallSlab);

            //3) 竖井(前室地板正中,3宽):盖平台井口+每4行横档,水面上浮跳出可回攀(F2)
            int shaftX = anteIntLeft + (AnteW - 3) / 2;
            build.ShaftX = shaftX;
            TileBrush.CarveRect(shaftX, anteFloor, shaftX + 3, cellarTop, L4Palette.WallSlab);
            TileBrush.PlatformRow(shaftX, shaftX + 3, anteFloor, L4Palette.PlatformFrameY);
            for (int y = cellarTop - 1; y > anteFloor + 1; y -= 4) {
                TileBrush.PlatformRow(shaftX, shaftX + 3, y, L4Palette.PlatformFrameY);
            }

            //4) 青铜大钟(铜砖,观感待游戏内检查;fallback见计划:金砖/蓝砖+深橙漆)
            int bx = onRight
                ? cellarIntLeft + 9 + bellJitter
                : cellarIntRight - 9 - bellJitter - BellW;
            build.BellLeft = bx;
            BuildBell(bx, cellarFloor, onRight, potStyleA, potStyleB, ref tally);
            build.ChamberPocket = new Rectangle(bx + 2, cellarFloor - 12, 11, 9);

            //5) 舱段Area:窖内膛+坑道行(含钟口渗入区,水审计构造性归零)
            build.WetArea = new Rectangle(cellarIntLeft, cellarTop, cw,
                cellarFloor + PitDepth - cellarTop);

            //6) 前室陈设:封存告示+门侧蜡烛(告示远离竖井口平台,蜡烛贴门)
            int signX = anteIntLeft + (onRight ? AnteW - 3 : 1);
            int candleX = anteIntLeft + (onRight ? 1 : AnteW - 2);
            tally.Add(L4Palette.PlaceSignWithText(signX, anteFloor - 1,
                "闸下封存。勿近，勿听。"), "封存告示", signX, anteFloor - 1);
            tally.Add(L4Palette.TryPlaceTile(candleX, anteFloor - 1,
                TileID.Candles, L4Palette.CandleStyle), "前室蜡烛", candleX, anteFloor - 1);

            //7) 窖壁苔藓斑(水下做旧;双水线/分带墙由PaintAging对全舱段自动补,不重复刷)
            for (int i = 0; i < 6; i++) {
                int mx = cellarIntLeft + 2 + (cw - 4) * i / 5;
                int my = MossRow(cellarTop, cellarFloor, i + mossSeed);
                L4Palette.MossDaub(mx, my);
            }
            L4Palette.MossDaub(cellarIntLeft, cellarFloor - 2);
            L4Palette.MossDaub(cellarIntRight - 1, cellarFloor - 2);
            return build;
        }

        //苔藓落点行:水下带内散布(确定性,不吃随机流)
        private static int MossRow(int top, int floor, int salt)
            => top + 4 + (floor - top - 6) * ((salt * 7) % 5) / 4;

        //==================== 钟体(15w x 16h,开口朝下,钟腔恒干) ====================
        //剖面(F=窖地板首行,b0..b14列):
        //  F-16      crown盖(b5..b9)+黑漆裂缝
        //  F-15/F-14 收分肩线(slope收角)
        //  F-13      顶环(b0..b14,钟腔顶板)
        //  F-12..F-6 钟腔气室(b2..b12,壁厚2;铜板墙=安全墙,腔内无刷怪)
        //  F-5..F-4  石台2行厚(b2..b12,中央b6..b8开喉口;台顶高于内部残水线)
        //  F-3..F-1  钟口渗入区(水,b2..b12)+两座砖礅(b0..b1/b13..b14,立在窖底)
        //  F..F+3    钟舌坑+潜行坑道(隐士挖穿远侧礅下岩,进钟的路;钟舌断落钟口正下方)
        private static void BuildBell(int bx, int cellarFloor, bool onRight,
            int potStyleA, int potStyleB, ref L4Rooms.Tally tally) {
            int f = cellarFloor;
            ushort copper = TileID.CopperBrick;

            //实心毛坯:主体环+肩+冠
            for (int x = bx; x < bx + BellW; x++) {
                for (int y = f - 13; y < f; y++) {
                    TileBrush.SetSolid(x, y, copper);
                }
            }
            for (int x = bx + 3; x < bx + 12; x++) {
                TileBrush.SetSolid(x, f - 15, copper);
            }
            for (int x = bx + 1; x < bx + 14; x++) {
                TileBrush.SetSolid(x, f - 14, copper);
            }
            for (int x = bx + 5; x < bx + 10; x++) {
                TileBrush.SetSolid(x, f - 16, copper);
            }
            //肩线slope收角(F24)
            TileBrush.SetSloped(bx + 4, f - 16, copper, SlopeType.SlopeDownRight);
            TileBrush.SetSloped(bx + 10, f - 16, copper, SlopeType.SlopeDownLeft);
            TileBrush.SetSloped(bx + 2, f - 15, copper, SlopeType.SlopeDownRight);
            TileBrush.SetSloped(bx + 12, f - 15, copper, SlopeType.SlopeDownLeft);
            TileBrush.SetSloped(bx, f - 14, copper, SlopeType.SlopeDownRight);
            TileBrush.SetSloped(bx + 14, f - 14, copper, SlopeType.SlopeDownLeft);

            //钟腔(铜板墙146:金属内膛读法,也免遭PaintAging的地牢墙分带重刷)+喉口+钟口
            TileBrush.CarveRect(bx + 2, f - 12, bx + 13, f - 5, WallID.CopperPlating);
            TileBrush.CarveRect(bx + 6, f - 5, bx + 9, f - 3, WallID.CopperPlating);
            TileBrush.CarveRect(bx + 2, f - 3, bx + 13, f, WallID.CopperPlating);

            //钟口喇叭:礅外缘flare
            TileBrush.SetSloped(bx - 1, f - 4, copper, SlopeType.SlopeDownRight);
            TileBrush.SetSloped(bx + 15, f - 4, copper, SlopeType.SlopeDownLeft);
            //钟口缺角2格:近井侧礅内角碎裂(读"铸废")
            int chipX = onRight ? bx + 1 : bx + 13;
            TileBrush.ClearCell(chipX, f - 3, WallID.CopperPlating);
            TileBrush.ClearCell(chipX, f - 2, WallID.CopperPlating);

            //钟冠黑漆裂缝:冠区实心面上的折线(paint层,§3.2-6)
            for (int y = f - 16; y <= f - 13; y++) {
                int crackX = bx + 7 + (y % 2 == 0 ? 0 : 1);
                if (Main.tile[crackX, y].HasTile) {
                    WorldGen.paintTile(crackX, y, PaintID.BlackPaint);
                }
            }

            //坑道:钟舌坑(钟口正下方)+远侧礅下潜行道+窖底出入口(隐士挖的;
            //礅立在坑道顶的1行地板桥上,不悬空)
            TileBrush.CarveRect(bx + 2, f, bx + 13, f + PitDepth, L4Palette.WallSlab);
            if (onRight) {
                TileBrush.CarveRect(bx + 13, f + 1, bx + 19, f + PitDepth, L4Palette.WallSlab);
                TileBrush.CarveRect(bx + 16, f, bx + 19, f + 1, L4Palette.WallSlab);
            }
            else {
                TileBrush.CarveRect(bx - 4, f + 1, bx + 2, f + PitDepth, L4Palette.WallSlab);
                TileBrush.CarveRect(bx - 4, f, bx - 1, f + 1, L4Palette.WallSlab);
            }

            //钟舌:2x2铜砖残段断落坑内钟口正下方(不用链,避开L4链母题边界)
            int tongueX = onRight ? bx + 3 : bx + 10;
            TileBrush.SetSolid(tongueX, f + 2, copper);
            TileBrush.SetSolid(tongueX + 1, f + 2, copper);
            TileBrush.SetSolid(tongueX, f + 3, copper);
            TileBrush.SetSolid(tongueX + 1, f + 3, copper);
            TileBrush.SetSloped(tongueX + (onRight ? 2 : -1), f + 3, copper,
                onRight ? SlopeType.SlopeDownLeft : SlopeType.SlopeDownRight);

            //钟腔陈设(石台顶行站立):告示+罐在左台,箱+蜡烛在右台
            int stand = f - 6;
            tally.Add(L4Palette.PlaceSignWithText(bx + 2, stand,
                "第八口钟哑了。他们把它沉在这里。我在里面住得很好。"), "钟腔告示", bx + 2, stand);
            tally.Add(WorldGen.PlacePot(bx + 4, stand, TileID.Pots, potStyleA), "钟腔罐", bx + 4, stand);
            tally.Add(WorldGen.PlaceChest(bx + 9, stand, TileID.Containers,
                notNearOtherChests: false, L4Palette.ChestWaterStyle) >= 0, "隐士箱", bx + 9, stand);
            tally.Add(L4Palette.TryPlaceTile(bx + 11, stand, TileID.Candles,
                L4Palette.CandleStyle), "钟腔蜡烛", bx + 11, stand);

            //坑内第二只罐:水下失物读法(挨着钟舌,远离潜行路径)
            int pitPotX = onRight ? bx + 10 : bx + 4;
            tally.Add(WorldGen.PlacePot(pitPotX, f + 3, TileID.Pots, potStyleB), "坑内罐", pitPotX, f + 3);
        }

        //==================== 免接线看样(镜像L4Preview惯例:单人调试,就地盖+手动注水) ====================

        /// <summary>
        /// 在(originX, floorRow)就地盖前室+竖井+满水窖+沉钟,并手动写入满水态
        /// (正式生成走L4WaterWorks.FillState,这里本地复刻含气室豁免的注水)。
        /// 占地约42宽x46高,请在平坦测试世界使用;主世界液体照常模拟,
        /// 钟腔恒干与否在此可直接目验(气室物理的活体测试)。
        /// </summary>
        internal static void BuildPreview(int originX, int floorRow, int seed = 5153) {
            if (Main.netMode != NetmodeID.SinglePlayer) {
                CWRMod.Instance.Logger.Warn("[L4MuteBell] 看样入口仅单人调试用,联机不发tile同步");
            }
            var rand = new UnifiedRandom(seed);
            int cw = 32, ch = 24;
            int anteFloor = floorRow - ch - 10 - 8;
            int cellarTop = anteFloor + 8;
            int cellarFloor = cellarTop + ch;
            int surfaceRow = cellarTop + 2;
            var union = new Rectangle(originX, anteFloor - AnteH - 2, cw + 4,
                cellarFloor + PitDepth + 2 - (anteFloor - AnteH - 2));

            var tally = new L4Rooms.Tally();
            BellBuild build = BuildBody(union, onRight: true, anteFloor, cellarTop, cellarFloor,
                cw, ch, 1, 10, 11, 1, rand, ref tally);
            //看样进路:左侧水平走廊直通前室
            TileBrush.CarveRect(union.Left - 6, anteFloor - 3, union.Left + 2, anteFloor,
                L4Palette.WallBase);

            //手动满水:复刻FillState判据(实心非平台不持液,气室豁免)
            int wet = 0;
            for (int x = build.WetArea.Left; x < build.WetArea.Right; x++) {
                for (int y = System.Math.Max(surfaceRow, build.WetArea.Top); y < build.WetArea.Bottom; y++) {
                    Tile t = Main.tile[x, y];
                    if (t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) {
                        continue;
                    }
                    if (build.ChamberPocket.Contains(x, y)) {
                        continue;
                    }
                    t.LiquidAmount = byte.MaxValue;
                    t.LiquidType = LiquidID.Water;
                    wet++;
                }
            }
            WorldGen.RangeFrame(union.Left - 8, union.Top - 1, union.Right + 1, union.Bottom + 1);
            CWRMod.Instance.Logger.Info(
                $"[L4MuteBell] 看样落成 union={union} 注水={wet}格 气室={build.ChamberPocket}"
                + $" 家具={tally.Placed}成/{tally.Rejected}拒(钟腔恒干请目验,液体会活体沉降)");
        }
    }
}
