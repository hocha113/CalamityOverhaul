using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Prefabs;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L5
{
    //====================================================================
    //沉眠巨兽·龙骨大厅(Wave-2 A1,WAVE2-BUILDINGS §3.1)
    //"骨头本身就是房":L5地层间死岩里一具完整巨兽骸骨,脊线是顶、肋弓是柱,
    //胸腔正中双链吊骨台,台上嵌一颗还在发光的生命水晶(它的心脏);
    //一端头骨(眼窝骨灯+颌骨锁金箱),一端尾椎递减嵌进端墙。
    //
    //挂点:L5Content.PlanAndBuild 层流末端(墙变体混斑之前)一行接线,
    //随机消耗全部集中于本入口(R4);候选窗逐个 ctx.Grid 预留,
    //全败=Warn缺席(该种子无巨兽,世界合法)。
    //
    //避让纪律:斜降坑道等"已刻画未落账"几何(RouteDescent只CanReserve不预留)
    //用空气扫描兜底;本体楼梯井足印自行MarkUnchecked,防P52填充器覆压。
    //耗时心算:刻画约5千格+家具约35件,毫秒级(R5)。
    //====================================================================
    internal static class L5Leviathan
    {
        //===观感旋钮(集中常量区,骨白观感待游戏内检查后调)===
        private const int InteriorWMin = 96, InteriorWMax = 104;
        private const int InteriorHMin = 30, InteriorHMax = 34;
        //宿主带地板到兽腔顶的落深(之字楼梯井长度来源)
        private const int DropMin = 25, DropMax = 45;
        //主竖井加宽避让(比HitsShaft的±3宽得多:大地标不贴层动脉)
        private const int ShaftAvoid = 40;
        //肋弓与心脏台净距下限(§3.2-7吊物净空)
        private const int RibHeartGap = 7;
        //心脏台顶行离地高度:悬空吊台,台下4行可通行,
        //且水晶底格在地面站立采掘距离内(原版tileRangeY=4),不用另搭登台阶梯
        private const int HeartPlatRise = 6;

        //==================== 头骨prefab(24w x 20h,面朝左) ====================
        //几何字符:'#'=骨块(brick参数传Bone) '.'=空+WallTiled ' '=透明
        //语义槽:'L'=眼窝骨灯;颌骨箱走代码直放(PlaceChest要登记箱实体,不进图例)
        private const int SkullW = 24;
        private const int SkullH = 20;
        //颌骨箱(锁金箱2x2)在art内的底格坐标(面朝左版)
        private const int ChestArtX = 4;
        private const int ChestArtY = 17;

        private static readonly string[] SkullArtLeft = [
            "      ############      ", //0  颅顶
            "    ################    ", //1
            "   ##################   ", //2
            "  ####################  ", //3
            " #####################  ", //4
            " ###################### ", //5
            " ###################### ", //6
            " ##.....############### ", //7  眼窝顶
            " ##..L..############### ", //8  眼窝骨灯
            " ##.....############### ", //9
            " ##.....##############  ", //10 眼窝底
            " ..###################  ", //11 鼻腔缺口
            " ..##################   ", //12
            " ####################   ", //13 上颌
            " #.#.#.###############  ", //14 齿列
            " ........#############  ", //15 口腔
            " ........#############  ", //16
            " ........############   ", //17 箱底格行
            " ####################   ", //18 下颌
            "  ##################    ", //19 底座
        ];

        private static Prefab _skullLeft;
        private static Prefab _skullRight;

        /// <summary>面朝左的头骨(落大厅右端用)</summary>
        private static Prefab SkullLeft => _skullLeft ??= ParseSkull(SkullArtLeft, "L5兽首_朝左");
        /// <summary>面朝右的头骨(落大厅左端用,文本级水平镜像后重解析)</summary>
        private static Prefab SkullRight => _skullRight ??= ParseSkull(FlipX(SkullArtLeft), "L5兽首_朝右");

        private static PrefabLegend _legend;
        private static PrefabLegend Legend => _legend ??= new PrefabLegend()
            .Add(new PrefabSlotDef {
                Ch = 'L', Name = "眼窝骨灯", TileType = TileID.HangingLanterns,
                Style = L5Palette.LanternBone, TopAnchor = true, ClearanceBelow = 2, MirrorCh = 'L',
            });

        private static Prefab ParseSkull(string[] art, string name) {
            Prefab prefab = Prefab.Parse(name, art, Legend);
            SkullSelfCheck(art, name);
            return prefab;
        }

        //哨兵断言:行数/底座/口腔/箱座错位在解析期即炸(镜像L1CathedralPrefab.SelfCheck)
        private static void SkullSelfCheck(string[] art, string name) {
            if (art.Length != SkullH || art[0].Length != SkullW) {
                throw new System.InvalidOperationException($"[L5Leviathan] {name} 尺寸{art[0].Length}x{art.Length}!={SkullW}x{SkullH}");
            }
            bool mirrored = art[8][SkullW - 1 - 5] == 'L';
            int chestX = mirrored ? SkullW - 1 - ChestArtX - 1 : ChestArtX;
            int mouthCol = mirrored ? SkullW - 1 : 0;
            if (!mirrored && art[8][5] != 'L') {
                throw new System.InvalidOperationException($"[L5Leviathan] {name} 眼窝灯槽错位");
            }
            if (art[ChestArtY][chestX] != '.' || art[ChestArtY][chestX + 1] != '.'
                || art[ChestArtY + 1][chestX] != '#' || art[ChestArtY + 1][chestX + 1] != '#') {
                throw new System.InvalidOperationException($"[L5Leviathan] {name} 颌骨箱座错位");
            }
            if (art[15][mouthCol] != ' ' || art[16][mouthCol] != ' ' || art[17][mouthCol] != ' ') {
                throw new System.InvalidOperationException($"[L5Leviathan] {name} 口腔进路被堵");
            }
            if (art[SkullH - 1][10] != '#') {
                throw new System.InvalidOperationException($"[L5Leviathan] {name} 底座哨兵失败");
            }
        }

        //文本级水平镜像:行内倒序+斜切对偶(水平对1↔2,3↔4;槽字符位置自动跟随)
        private static string[] FlipX(string[] art) {
            var result = new string[art.Length];
            for (int i = 0; i < art.Length; i++) {
                char[] row = art[i].ToCharArray();
                System.Array.Reverse(row);
                for (int j = 0; j < row.Length; j++) {
                    row[j] = row[j] switch { '1' => '2', '2' => '1', '3' => '4', '4' => '3', _ => row[j] };
                }
                result[i] = new string(row);
            }
            return result;
        }

        //==================== 主入口 ====================

        /// <summary>
        /// 在地层间死岩落龙骨大厅:floors[3]→floors[4]带优先,floors[2]→floors[3]备选;
        /// 每带5个等距候选x窗,逐个过栅格预留+空气扫描+宿主房扫描,全败Warn缺席。
        /// 成功时兽腔入图(Rooms+1,Edge经宿主房楼梯井),返回true。
        /// </summary>
        internal static bool TryBuild(LayerBuildContext ctx, int[] floors, UnifiedRandom rand) {
            if (floors == null || floors.Length < 5) {
                CWRMod.Instance.Logger.Error("[L5Leviathan] floors数组异常,弃");
                return false;
            }
            //随机前置一次掷完(R4:消耗集中,失败路径不再掷)
            int iw = rand.Next(InteriorWMin, InteriorWMax + 1);
            int ih = rand.Next(InteriorHMin, InteriorHMax + 1);
            int drop = rand.Next(DropMin, DropMax + 1);
            int salt = rand.Next(5);
            int ribSpacing = rand.Next(10, 13);
            int pileCount = rand.Next(6, 11);
            int urnCount = rand.Next(2, 4);

            int boundsW = iw + DungeonworldMetrics.RoomShellThick * 2;
            int boundsH = ih + DungeonworldMetrics.RoomShellThick * 2;

            foreach (int stratum in new[] { 3, 2 }) {
                int gapTop = floors[stratum];
                int gapBottom = floors[stratum + 1];
                int top = gapTop + drop;
                if (top + boundsH + 4 > gapBottom - 36) {
                    continue; //竖向挤不下(构造上不该发生,保险)
                }
                int spanL = DungeonworldMetrics.PlayLeft + 12;
                int spanR = DungeonworldMetrics.PlayRight - 12 - boundsW;
                for (int k = 0; k < 5; k++) {
                    int left = spanL + (spanR - spanL) * ((salt + k) % 5) / 4;
                    var bounds = new Rectangle(left, top, boundsW, boundsH);
                    if (HitsShaftWide(bounds.Left, bounds.Right)
                        || !ctx.Grid.CanReserve(bounds, DungeonworldMetrics.RoomPadding)
                        || AnyAirInside(bounds, DungeonworldMetrics.RoomPadding)) {
                        continue;
                    }
                    if (!FindHostWell(ctx, bounds, out int hostIdx, out int wellX)) {
                        continue;
                    }
                    if (!ctx.Grid.TryReserve(bounds, DungeonworldMetrics.RoomPadding)) {
                        continue; //CanReserve刚过,理论不该到这,双保险
                    }
                    Commit(ctx, bounds, hostIdx, wellX, rand, ribSpacing, pileCount, urnCount, stratum);
                    return true;
                }
            }
            CWRMod.Instance.Logger.Warn("[L5Leviathan] 两带候选窗全败,本种子无巨兽(合法缺席,非硬错误)");
            return false;
        }

        private static bool HitsShaftWide(int left, int right)
            => left < DungeonworldMetrics.ShaftLeft + DungeonworldMetrics.ShaftWidth + ShaftAvoid
            && right > DungeonworldMetrics.ShaftLeft - ShaftAvoid;

        //空气扫描:P10全图浇实心,死岩带里的空气=别家已刻画未落账的几何(斜降坑道等),让路
        private static bool AnyAirInside(Rectangle rect, int pad) {
            for (int x = rect.Left - pad; x < rect.Right + pad; x++) {
                for (int y = rect.Top - pad; y < rect.Bottom + pad; y++) {
                    if (WorldGen.InWorld(x, y, 5) && !Main.tile[x, y].HasTile) {
                        return true;
                    }
                }
            }
            return false;
        }

        //==================== 宿主房与井列扫描 ====================

        //扫既有房间图:包络上方20~90行、x重叠的房作宿主;在重叠段做空列扫描
        //(3列地板实心非裂砖非平台+上方3行无家具tile),再校验井道竖条(栅格空闲+无既有空气)
        private static bool FindHostWell(LayerBuildContext ctx, Rectangle bounds,
            out int hostIdx, out int wellX) {
            hostIdx = -1;
            wellX = -1;
            int iL = bounds.Left + DungeonworldMetrics.RoomShellThick;
            int iR = bounds.Right - DungeonworldMetrics.RoomShellThick;
            int hallMid = (iL + iR) / 2;

            for (int i = 0; i < ctx.Graph.Rooms.Count; i++) {
                RoomNode room = ctx.Graph.Rooms[i];
                int dy = bounds.Top - room.FloorTop;
                if (dy < 20 || dy > 90) {
                    continue;
                }
                int overlapL = System.Math.Max(room.InteriorLeft + 1, iL + 8);
                int overlapR = System.Math.Min(room.InteriorRight - 1, iR - 8);
                for (int wx = overlapL; wx + 3 <= overlapR; wx++) {
                    if (System.Math.Abs(wx + 1 - hallMid) < 24) {
                        //中带净区:胸腔中心随头骨端在厅中线两侧漂移约13列,
                        //加宽到±24构造性覆盖两种朝向的心脏净区(±10)
                        continue;
                    }
                    if (!FloorColumnsUsable(room, wx)) {
                        continue;
                    }
                    //井道竖条从宿主padding圈下缘起查:RoomPlacer预留含padding(2),
                    //贴Bounds.Bottom起查必撞宿主自留账=永远失败(镜像BuildBoneWell+2/RouteDescent+3成规);
                    //跳过的2行在宿主账里,刻画照常穿过;其下若有斜降stub残余空气仍在扫描域内
                    var strip = new Rectangle(wx - 1,
                        room.Bounds.Bottom + DungeonworldMetrics.RoomPadding,
                        DungeonworldMetrics.StairWellWidth + 2,
                        bounds.Top - room.Bounds.Bottom - DungeonworldMetrics.RoomPadding);
                    if (strip.Height <= 0 || !ctx.Grid.CanReserve(strip, 0) || AnyAirInside(strip, 0)) {
                        continue;
                    }
                    hostIdx = i;
                    wellX = wx;
                    return true;
                }
            }
            return false;
        }

        //空列预检:比IntersticePlanner固定偏移多一道防砸家具/防踩裂砖假地板
        private static bool FloorColumnsUsable(RoomNode room, int wx) {
            for (int dx = 0; dx < DungeonworldMetrics.StairWellWidth; dx++) {
                int x = wx + dx;
                Tile floor = Main.tile[x, room.FloorTop];
                if (!floor.HasTile || !Main.tileSolid[floor.TileType]
                    || floor.TileType == TileID.Platforms
                    || floor.TileType == L5Palette.CrackedBrick) {
                    return false;
                }
                for (int dy = 1; dy <= 3; dy++) {
                    if (Main.tile[x, room.FloorTop - dy].HasTile) {
                        return false;
                    }
                }
            }
            return true;
        }

        //==================== 落成 ====================

        private static void Commit(LayerBuildContext ctx, Rectangle bounds, int hostIdx,
            int wellX, UnifiedRandom rand, int ribSpacing, int pileCount, int urnCount, int stratum) {
            RoomNode host = ctx.Graph.Rooms[hostIdx];

            //井道足印自行落账:防P52夹层/P54副翼把已凿井道当实心岩用
            //(与FindHostWell同一竖条:自宿主padding圈下缘起,上2行已在宿主账里)
            var strip = new Rectangle(wellX - 1,
                host.Bounds.Bottom + DungeonworldMetrics.RoomPadding,
                DungeonworldMetrics.StairWellWidth + 2,
                bounds.Top - host.Bounds.Bottom - DungeonworldMetrics.RoomPadding);
            ctx.Grid.MarkUnchecked(strip);

            var tally = new L5Rooms.Tally();
            Stats stats = BuildCavity(bounds, wellX, rand, ribSpacing, pileCount, urnCount, ref tally);

            //入图+接驳:兽腔为图节点,宿主房地板井口→之字楼梯井落兽腔地板
            var hall = new RoomNode { Bounds = bounds };
            int hallIdx = ctx.Graph.Rooms.Count;
            ctx.Graph.Rooms.Add(hall);
            var gap = new DoorSocket(SocketSide.Bottom, wellX - host.Bounds.Left,
                SocketKind.PlatformGap, DungeonworldMetrics.StairWellWidth);
            host.Sockets.Add(gap);
            CorridorRouter.RouteToFloorBelow(host, gap, hall.FloorTop,
                L5Palette.PlatformBone, L5Palette.WallTiled);
            ctx.Graph.Edges.Add(new RoomEdge(hostIdx, hallIdx, SocketKind.PlatformGap, EdgeForm.StairWell));

            CWRMod.Instance.Logger.Info(
                $"[L5Leviathan] 落成 origin=({bounds.X},{bounds.Y}) 带={stratum}→{stratum + 1}"
                + $" 内膛={bounds.Width - 4}x{bounds.Height - 4} 宿主={host.Bounds} 井x={wellX}"
                + $" 肋={stats.RibsBuilt}建/{stats.RibsSkipped}弃 心脏={(stats.HeartPlaced ? "水晶" : "降级金箱")}"
                + $" 头骨面向={(stats.SkullOnRight ? "左" : "右")} 家具={tally.Placed}成/{tally.Rejected}拒");
        }

        private struct Stats
        {
            internal int RibsBuilt;
            internal int RibsSkipped;
            internal bool HeartPlaced;
            internal bool SkullOnRight;
        }

        //==================== 兽腔本体(壳→骨架→心脏→装修,纯几何无图操作) ====================

        private static Stats BuildCavity(Rectangle bounds, int wellX, UnifiedRandom rand,
            int ribSpacing, int pileCount, int urnCount, ref L5Rooms.Tally tally) {
            var stats = new Stats();
            int shell = DungeonworldMetrics.RoomShellThick;
            int iL = bounds.Left + shell;
            int iR = bounds.Right - shell;
            int iT = bounds.Top + shell;
            int floorTop = bounds.Bottom - shell;

            //1) 壳与内膛:整包络重盖粉砖,内膛开进WallTiled("更老的骨窖区"语义)
            for (int x = bounds.Left; x < bounds.Right; x++) {
                for (int y = bounds.Top; y < bounds.Bottom; y++) {
                    TileBrush.SetSolid(x, y, L5Palette.Brick);
                }
            }
            TileBrush.CarveRect(iL, iT, iR, floorTop, L5Palette.WallTiled);

            //2) 头骨落在离井更远的一端(构造性保证口腔/眼窝不被楼梯井打穿)
            stats.SkullOnRight = wellX + 1 < (iL + iR) / 2;
            int skullLeft = stats.SkullOnRight ? iR - SkullW - 1 : iL + 1;
            int skullTop = floorTop - SkullH;
            Prefab skull = stats.SkullOnRight ? SkullLeft : SkullRight;
            skull.StampGeometry(skullLeft, skullTop, L5Palette.Bone, L5Palette.WallTiled, L5Palette.PlatformBone);
            FurnishReport skullReport = skull.PlaceFurniture(skullLeft, skullTop);
            tally.Placed += skullReport.Placed;
            tally.Rejected += skullReport.Rejected;
            //颌骨锁金箱:PlaceChest登记箱实体(图例槽走PlaceObject不建实体,故代码直放)
            int chestX = skullLeft + (stats.SkullOnRight ? ChestArtX : SkullW - 1 - ChestArtX - 1);
            tally.Add(PlaceLockedGoldChest(chestX, skullTop + ChestArtY), "颌骨金箱", chestX, skullTop + ChestArtY);

            //3) 脊线:内膛顶行骨块连排+每3列垂1格椎骨凸;井穿脊处留5列断椎(井膛3+两翼各1)
            //(叙事:盗掘者从断椎处破入);头骨端以颈椎柱接上颅顶
            int spineY = iT;
            int neckX = stats.SkullOnRight ? skullLeft + 6 : skullLeft + SkullW - 8;
            int spineL = stats.SkullOnRight ? iL : neckX;
            int spineR = stats.SkullOnRight ? neckX + 2 : iR;
            for (int x = spineL; x < spineR; x++) {
                if (x >= wellX - 1 && x < wellX + DungeonworldMetrics.StairWellWidth + 1) {
                    continue; //断椎缺口
                }
                TileBrush.SetSolid(x, spineY, L5Palette.Bone);
                if ((x - iL) % 3 == 0) {
                    TileBrush.SetSolid(x, spineY + 1, L5Palette.Bone);
                }
            }
            //颈椎:2宽骨柱自脊线端头垂到颅顶(art行0的穹顶起于col6,两朝向都落在顶骨上)
            for (int y = spineY + 1; y < skullTop; y++) {
                TileBrush.SetSolid(neckX, y, L5Palette.Bone);
                TileBrush.SetSolid(neckX + 1, y, L5Palette.Bone);
            }

            //4) 尾椎:近端(头骨对侧)三节递减,后两节嵌进端墙壳(骨咬砖,壳厚2可雕)
            BuildTail(stats.SkullOnRight ? iL : iR - 1, stats.SkullOnRight ? 1 : -1, spineY);

            //5) 肋弓:自脊下缓弧外错(每3行错1列)的2宽骨弧,以胸腔中心(肋跨中点,
            //非厅中线:头骨占去一端26列)对称排布;扫过井区/出界的整根跳过(不留半截肋)
            int ribSpanL = stats.SkullOnRight ? iL + 3 : skullLeft + SkullW + 3;
            int ribSpanR = stats.SkullOnRight ? skullLeft - 3 : iR - 3;
            int heartX = (ribSpanL + ribSpanR) / 2; //胸腔正中=心脏吊台所在
            for (int side = -1; side <= 1; side += 2) {
                for (int k = 0; k < 4; k++) {
                    int topX = heartX + side * (RibHeartGap + 1 + k * ribSpacing) - (side < 0 ? 1 : 0);
                    if (DrawRib(topX, side, spineY, floorTop, ribSpanL, ribSpanR, wellX)) {
                        stats.RibsBuilt++;
                    }
                    else {
                        stats.RibsSkipped++;
                    }
                }
            }

            //6) 心脏:胸腔正中4宽2厚骨台悬空吊挂(双绷链锚脊线锚台面,L5链母题限定形态),
            //台上直放生命水晶,失败降级锁金箱(计划降级线)
            int platY = floorTop - HeartPlatRise;
            for (int dx = -1; dx <= 2; dx++) {
                TileBrush.SetSolid(heartX + dx, platY, L5Palette.Bone);
                TileBrush.SetSolid(heartX + dx, platY + 1, L5Palette.Bone);
            }
            HangChainTo(heartX - 1, spineY, platY);
            HangChainTo(heartX + 2, spineY, platY);
            stats.HeartPlaced = TryPlaceHeart(heartX, platY - 1);
            if (!stats.HeartPlaced) {
                tally.Add(PlaceLockedGoldChest(heartX, platY - 1), "心脏降级金箱", heartX, platY - 1);
                CWRMod.Instance.Logger.Warn($"[L5Leviathan] 生命水晶直放失败,降级锁金箱 at ({heartX},{platY - 1})");
            }

            //7) 装修:骨堆/瓮铺地,骨灯笼吊椎骨间,尘白全腔水洗+地表尘斑
            FurnishFloor(iL, iR, floorTop, heartX, wellX, skullLeft, rand, pileCount, urnCount, ref tally);
            int lanterns = 0;
            foreach (int off in new[] { -15, 15, -26, 26 }) {
                int lx = heartX + off;
                if (lanterns >= 3 || lx <= ribSpanL || lx >= ribSpanR
                    || (lx >= wellX - 2 && lx <= wellX + 4)) {
                    continue;
                }
                if (L5Palette.TryPlaceObject(lx, spineY + 1, TileID.HangingLanterns, L5Palette.LanternBone)) {
                    lanterns++;
                    tally.Placed++;
                }
            }
            L5Palette.DustWallWash(iL, iT, iR, floorTop);
            L5Palette.DustFloorRun(iL + 2, floorTop, iR - iL - 4);
            return stats;
        }

        //尾椎三节:2x2→2x2→1x2递减,逐节下斜并吃进端墙壳("最后几节还埋在岩里");
        //wallInner=内膛端列,dir=内膛方向(+1尾在左端,-1尾在右端)
        private static void BuildTail(int wallInner, int dir, int spineY) {
            int[] offsetStart = [0, -1, -1]; //各节相对壁面的内膛向起始偏移(负=嵌进壳)
            int[] widths = [2, 2, 1];
            for (int seg = 0; seg < 3; seg++) {
                int y0 = spineY + 2 + seg * 3;
                for (int dx = 0; dx < widths[seg]; dx++) {
                    int cx = wallInner + (offsetStart[seg] + dx) * dir;
                    TileBrush.SetSolid(cx, y0, L5Palette.Bone);
                    TileBrush.SetSolid(cx, y0 + 1, L5Palette.Bone);
                }
            }
        }

        //肋弓:先干跑算路径(每3行向外错1列的缓弧,§3.2-6唯一许可的轮廓形状;
        //更陡的错步会把扫掠宽度撑爆头骨侧的净跨),
        //扫掠区间出界/撞井即整根放弃;通过后落骨+柱脚slope收角(F24)+嵌地一格
        private static bool DrawRib(int topX, int dir, int spineY, int floorTop,
            int spanL, int spanR, int wellX) {
            int h = floorTop - spineY - 1;
            var path = new List<int>(h);
            int x = topX;
            for (int t = 1; t <= h; t++) {
                if (t % 3 == 0) {
                    x += dir;
                }
                path.Add(x);
            }
            int minX = System.Math.Min(topX, x);
            int maxX = System.Math.Max(topX, x) + 1;
            if (minX < spanL || maxX > spanR
                || (maxX >= wellX - 2 && minX <= wellX + DungeonworldMetrics.StairWellWidth + 1)) {
                return false;
            }
            for (int t = 0; t < path.Count; t++) {
                TileBrush.SetSolid(path[t], spineY + 1 + t, L5Palette.Bone);
                TileBrush.SetSolid(path[t] + 1, spineY + 1 + t, L5Palette.Bone);
            }
            //柱脚:嵌进地板一格+两侧slope收角(目标格空才补,不吃掉散件)
            int footX = path[^1];
            TileBrush.SetSolid(footX, floorTop, L5Palette.Bone);
            TileBrush.SetSolid(footX + 1, floorTop, L5Palette.Bone);
            if (!Main.tile[footX - 1, floorTop - 1].HasTile) {
                TileBrush.SetSloped(footX - 1, floorTop - 1, L5Palette.Bone, SlopeType.SlopeDownLeft);
            }
            if (!Main.tile[footX + 2, floorTop - 1].HasTile) {
                TileBrush.SetSloped(footX + 2, floorTop - 1, L5Palette.Bone, SlopeType.SlopeDownRight);
            }
            return true;
        }

        //绷链:自脊线下首个空行落到台面正上方(锚顶=脊骨,锚底=骨台,承重形态合规)
        private static void HangChainTo(int x, int spineY, int platY) {
            int y0 = spineY + 1;
            while (y0 < platY && Main.tile[x, y0].HasTile) {
                y0++;
            }
            if (y0 < platY) {
                L5Palette.TautChain(x, y0, platY - y0);
            }
        }

        //生命水晶直放:镜像原版AddLifeCrystal的PlaceTile路径(2x2,站立行),落地后核对
        private static bool TryPlaceHeart(int x, int standRow) {
            WorldGen.PlaceTile(x, standRow, TileID.Heart, mute: true, forced: true);
            for (int dx = -1; dx <= 2; dx++) {
                for (int dy = -2; dy <= 0; dy++) {
                    Tile t = Main.tile[x + dx, standRow + dy];
                    if (t.HasTile && t.TileType == TileID.Heart) {
                        return true;
                    }
                }
            }
            return false;
        }

        //锁金箱+占位补给(镜像L5Rooms.PlaceGoldChest形制,正式战利品表归M4)
        private static bool PlaceLockedGoldChest(int x, int standRow) {
            int index = WorldGen.PlaceChest(x, standRow, TileID.Containers,
                notNearOtherChests: false, L5Palette.ChestLockedGold);
            if (index < 0) {
                return false;
            }
            Chest chest = Main.chest[index];
            chest.item[0] = new Item();
            chest.item[0].SetDefaults(ItemID.GoldCoin);
            chest.item[0].stack = 5;
            chest.item[1] = new Item();
            chest.item[1].SetDefaults(ItemID.HealingPotion);
            chest.item[1].stack = 2;
            return true;
        }

        //地面散件:骨堆(大小轮换)+瓮,均布加抖动;让开井柱落点与头骨占地(免噪声拒绝日志)
        private static void FurnishFloor(int iL, int iR, int floorTop, int heartX, int wellX,
            int skullLeft, UnifiedRandom rand, int pileCount, int urnCount, ref L5Rooms.Tally tally) {
            int stand = floorTop - 1;
            for (int i = 0; i < pileCount; i++) {
                int x = iL + 4 + (iR - iL - 8) * i / System.Math.Max(1, pileCount - 1)
                    + rand.Next(-2, 3);
                if ((x >= wellX - 2 && x <= wellX + 4)
                    || (x >= skullLeft - 1 && x < skullLeft + SkullW + 1)) {
                    continue;
                }
                bool ok = i % 2 == 0
                    ? L5Palette.PlaceLargeBones(x, stand, rand)
                    : L5Palette.PlaceSmallBones(x, stand, rand);
                tally.Add(ok, "腔底骨堆", x, stand);
            }
            for (int i = 0; i < urnCount; i++) {
                int x = heartX + (i == 0 ? -12 : i == 1 ? 12 : -20) + rand.Next(-2, 3);
                if (x <= iL + 2 || x >= iR - 2 || (x >= wellX - 2 && x <= wellX + 4)
                    || (x >= skullLeft - 1 && x < skullLeft + SkullW + 1)) {
                    continue;
                }
                tally.Add(L5Palette.PlaceUrn(x, stand, rand), "腔底瓮", x, stand);
            }
        }

        //==================== 免接线看样(镜像L5Preview惯例:单人调试,脚下就地盖) ====================

        /// <summary>
        /// 在(originX, spineFloor)就地盖一座完整兽腔+假宿主房+入腔楼梯井。
        /// 占地约116宽x96高,请在平坦测试世界使用;不注册pass、不入图、联机不发tile同步。
        /// </summary>
        internal static void BuildPreview(int originX, int spineFloor, int seed = 5152) {
            if (Main.netMode != NetmodeID.SinglePlayer) {
                CWRMod.Instance.Logger.Warn("[L5Leviathan] 看样入口仅单人调试用,联机不发tile同步");
            }
            var rand = new UnifiedRandom(seed);
            var area = new Rectangle(originX - 4, spineFloor - 92, 116, 96);
            for (int x = area.Left; x < area.Right; x++) {
                for (int y = area.Top; y < area.Bottom; y++) {
                    TileBrush.SetSolid(x, y, L5Palette.Brick);
                }
            }

            //假宿主房(内膛30x10)+固定落深;井列压在尾端侧,头骨自然落远端(镜像正式选位逻辑)
            var host = new RoomNode { Bounds = new Rectangle(originX + 6, area.Top + 4, 34, 14) };
            L5Rooms.StampAndCarve(host, L5Palette.WallSlab);
            var bounds = new Rectangle(originX, host.Bounds.Bottom + 26, 108, 38);
            int wellX = host.InteriorLeft + 2;

            var tally = new L5Rooms.Tally();
            Stats stats = BuildCavity(bounds, wellX, rand, 11, 8, 3, ref tally);
            var hall = new RoomNode { Bounds = bounds };
            var gap = new DoorSocket(SocketSide.Bottom, wellX - host.Bounds.Left,
                SocketKind.PlatformGap, DungeonworldMetrics.StairWellWidth);
            host.Sockets.Add(gap);
            CorridorRouter.RouteToFloorBelow(host, gap, hall.FloorTop,
                L5Palette.PlatformBone, L5Palette.WallTiled);

            WorldGen.RangeFrame(area.Left - 1, area.Top - 1, area.Right + 1, area.Bottom + 1);
            CWRMod.Instance.Logger.Info(
                $"[L5Leviathan] 看样落成 bounds={bounds} 肋={stats.RibsBuilt}建/{stats.RibsSkipped}弃"
                + $" 心脏={(stats.HeartPlaced ? "水晶" : "降级金箱")} 家具={tally.Placed}成/{tally.Rejected}拒");
        }
    }
}
