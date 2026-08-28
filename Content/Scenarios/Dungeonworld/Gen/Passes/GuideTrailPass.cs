using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.BossRooms;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L2;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L4;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L6;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Passes
{
    //P58:Boss房引路痕迹(发现引导批,2026-08-27)。
    //三间Boss房都内联跨脊(见各Siting头注释),几何上"走完脊必穿房",缺的是方向读法:
    //玩家进层只能盲选左右,最坏空走近900列。本pass自房门沿层脊向两侧各铺TrailRange列
    //密度渐变的环境痕迹,越近越密,读墙即知方向:
    //  L2 深牢禁室:死铁垂链间距渐收+锈渍垂痕渐密(层做旧签名的定向化)+门洞囚粉染
    //  L4 泄洪堂:灰水线痕随接近爬高+贴地黑线渐密+苔藓渐密+末段深蓝刻度(房内`=`语言外延)
    //  L6 验收堂:脊顶灰漆轨带(天轨馈线,"跟着轨走")+亮橙铆钉刻标渐密+焦油垂滴+门洞警示纹
    //纪律:只动wall/paint与非实心装饰tile(链214不入碰撞),不改碰撞几何,不动既有房间结构;
    //变化源全部为确定性坐标hash,零genRand消耗,R4随机流对本pass无感知,
    //故排进Tasks尾部(P55撒布之后、P80校验之前)不影响任何既有种子结果;
    //痕迹有意写在层染/做旧之上(引导优先级最高);链tile吃P80全域RangeFrame,漆层无需帧修。
    internal class GuideTrailPass : GenPass
    {
        /// <summary>引导痕迹自房门向两侧延伸的列数。取活跃半宽量级:玩家从主竖井/楼梯井
        /// 落到脊上时大概率已在痕迹带内,读密度趋势即知方向;远端密度趋零不糊层氛围</summary>
        private const int TrailRange = 420;

        public GuideTrailPass() : base("Dungeonworld Guide Trails", 0.5f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "铺设引路痕迹...";
            int gaol = 0, flood = 0, proof = 0;
            if (DeepGaolWraithGate.Enabled && GaolBossRoomSiting.LastOrigin is Point gaolOrigin) {
                gaol = PaintGaolTrail(gaolOrigin);
            }
            progress.Set(0.35);
            if (UndrownedGate.Enabled && FloodGallerySiting.LastOrigin is Point floodOrigin) {
                flood = PaintFloodTrail(floodOrigin);
            }
            progress.Set(0.7);
            if (FoundryOverseerGate.Enabled && ProofingHallSiting.LastOrigin is Point proofOrigin) {
                proof = PaintProofTrail(proofOrigin);
            }
            CWRMod.Instance.Logger.Info(
                $"[Dungeonworld] P58 GuideTrail 写入格数 禁室={gaol} 泄洪堂={flood} 验收堂={proof}(0=该线未落房/门禁关)");
            progress.Set(1.0);
        }

        //==================== L2 深牢禁室:死铁与锈越挂越密 ====================

        private static readonly ushort[] GaolWalls = [
            WallID.PinkDungeonUnsafe, WallID.PinkDungeonSlabUnsafe, WallID.PinkDungeonTileUnsafe,
        ];

        private static int PaintGaolTrail(Point origin) {
            LayerBand band = DungeonworldMetrics.Bands[1];
            Rectangle bounds = GaolBossRoom.Bounds(origin);
            int interiorTop = band.SpineInteriorTop;
            int floorTop = band.SpineFloorTop;
            int writes = 0;

            for (int side = 0; side < 2; side++) {
                int dir = side == 0 ? -1 : 1;
                int start = dir < 0 ? bounds.Left - 1 : bounds.Right;
                int chainRun = 0;
                for (int i = 0; i < TrailRange; i++) {
                    int x = start + dir * i;
                    if (x <= DungeonworldMetrics.PlayLeft + 1 || x >= DungeonworldMetrics.PlayRight - 2) {
                        break;
                    }
                    float t = 1f - i / (float)TrailRange;

                    //垂链:间距渐收(远端46列一节,贴门10列一节),顶锚/净空逐格校验
                    chainRun++;
                    if (chainRun >= (int)MathHelper.Lerp(46f, 10f, t)
                        && TryHangSpineChain(x, interiorTop, floorTop, ref writes)) {
                        chainRun = 0;
                    }
                    //锈渍垂痕:独立于链的渐密淌锈(4%→22%),金属越多锈越多的方向读法
                    if (Hash(x, 0x6A01) % 100 < (int)MathHelper.Lerp(4f, 22f, t)) {
                        writes += PaintStreak(x, interiorTop, floorTop,
                            2 + Hash(x, 0x6A02) % 3, PaintID.BrownPaint, GaolWalls);
                    }
                }
            }
            writes += PaintThreshold(origin, bounds, floorTop,
                GaolBossRoom.LeftDoorOffset, GaolBossRoom.RightDoorOffset, GaolBossRoom.DoorHeight,
                archWall: PaintID.DeepPinkPaint, lintel: PaintID.DeepPinkPaint,
                floorA: PaintID.DeepPinkPaint, floorB: PaintID.DeepPinkPaint);
            return writes;
        }

        /// <summary>脊顶挂一节死铁垂链+链根锈渍。顶锚不实心(井口/落口)或途中有物即整根放弃</summary>
        private static bool TryHangSpineChain(int x, int interiorTop, int floorTop, ref int writes) {
            if (!WorldGen.InWorld(x, interiorTop - 1, 5)) {
                return false;
            }
            Tile anchor = Main.tile[x, interiorTop - 1];
            if (!anchor.HasTile || !Main.tileSolid[anchor.TileType]
                || anchor.TileType == TileID.Platforms) {
                return false;
            }
            //链长1~2,链尾距地恒≥3行(脊净高6),不折损玩家包络与洪泛口径
            int len = 1 + Hash(x, 0x6A03) % 2;
            for (int i = 0; i < len; i++) {
                if (Main.tile[x, interiorTop + i].HasTile) {
                    return false;
                }
            }
            int placed = L2Palette.HangChain(x, interiorTop, len);
            if (placed <= 0) {
                return false;
            }
            L2Palette.RustStreak(x, interiorTop + placed, 2 + Hash(x, 0x6A04) % 2);
            writes += placed;
            return true;
        }

        //==================== L4 泄洪堂:水线越爬越高 ====================

        private static readonly ushort[] FloodWalls = [
            WallID.GreenDungeonUnsafe, WallID.GreenDungeonSlabUnsafe, WallID.GreenDungeonTileUnsafe,
        ];

        private static int PaintFloodTrail(Point origin) {
            LayerBand band = DungeonworldMetrics.Bands[3];
            Rectangle bounds = FloodGalleryRoom.Bounds(origin);
            int interiorTop = band.SpineInteriorTop;
            int floorTop = band.SpineFloorTop;
            int writes = 0;

            for (int side = 0; side < 2; side++) {
                int dir = side == 0 ? -1 : 1;
                int start = dir < 0 ? bounds.Left - 1 : bounds.Right;
                for (int i = 0; i < TrailRange; i++) {
                    int x = start + dir * i;
                    if (x <= DungeonworldMetrics.PlayLeft + 1 || x >= DungeonworldMetrics.PlayRight - 2) {
                        break;
                    }
                    float t = 1f - i / (float)TrailRange;

                    //灰水线(满水位痕):高度随接近自地板上1行爬到5行,4列一段hash抖动像真水渍;
                    //远端(t<0.15)改虚线渐隐,痕迹有个自然的头,不在半空戛然而止
                    int rise = 1 + (int)(t * 4f);
                    int lineY = floorTop - rise - Hash(x >> 2, 0x4C01) % 2;
                    if (lineY <= interiorTop) {
                        lineY = interiorTop + 1;
                    }
                    if (t >= 0.15f || Hash(x, 0x4C05) % 100 < 55) {
                        writes += PaintWallCell(x, lineY, PaintID.GrayPaint, FloodWalls);
                    }
                    //贴地黑线(排空位痕):断续渐密(30%→85%)
                    if (Hash(x, 0x4C02) % 100 < (int)MathHelper.Lerp(30f, 85f, t)) {
                        writes += PaintWallCell(x, floorTop - 1, PaintID.BlackPaint, FloodWalls);
                    }
                    //苔藓:地砖面渐密(3%→16%),泡得越久的方向长得越多
                    if (Hash(x, 0x4C03) % 100 < (int)MathHelper.Lerp(3f, 16f, t)) {
                        writes += L4Palette.MossDaub(x, floorTop);
                    }
                    //末30列:深蓝刻度双格竖标(房内`=`水位刻度语言的外延),越近越密
                    if (i <= 30 && i % (i <= 12 ? 4 : 6) == 0) {
                        writes += PaintWallCell(x, floorTop - 2, PaintID.DeepBluePaint, FloodWalls);
                        writes += PaintWallCell(x, floorTop - 3, PaintID.DeepBluePaint, FloodWalls);
                    }
                }
            }
            writes += PaintThreshold(origin, bounds, floorTop,
                FloodGalleryRoom.LeftDoorOffset, FloodGalleryRoom.RightDoorOffset, FloodGalleryRoom.DoorHeight,
                archWall: PaintID.DeepBluePaint, lintel: PaintID.GrayPaint,
                floorA: PaintID.GrayPaint, floorB: PaintID.GrayPaint);
            return writes;
        }

        //==================== L6 验收堂:跟着轨走到头 ====================

        private static readonly ushort[] ProofWalls = [
            WallID.BlueDungeonUnsafe, WallID.BlueDungeonSlabUnsafe, WallID.BlueDungeonTileUnsafe,
        ];

        private static int PaintProofTrail(Point origin) {
            LayerBand band = DungeonworldMetrics.Bands[5];
            Rectangle bounds = ProofingHallRoom.Bounds(origin);
            int interiorTop = band.SpineInteriorTop;
            int floorTop = band.SpineFloorTop;
            int writes = 0;

            for (int side = 0; side < 2; side++) {
                int dir = side == 0 ? -1 : 1;
                int start = dir < 0 ? bounds.Left - 1 : bounds.Right;
                int tickRun = 0;
                for (int i = 0; i < TrailRange; i++) {
                    int x = start + dir * i;
                    if (x <= DungeonworldMetrics.PlayLeft + 1 || x >= DungeonworldMetrics.PlayRight - 2) {
                        break;
                    }
                    float t = 1f - i / (float)TrailRange;

                    //轨带:脊顶第一行连续灰染(天轨馈线本体,与房内rel6导轨带同漆同语义);
                    //远端(t<0.15)改虚线渐隐,轨有个"年久失修断带"的自然收头
                    if (t >= 0.15f || Hash(x, 0x5E03) % 100 < 60) {
                        writes += PaintWallCell(x, interiorTop, PaintID.GrayPaint, ProofWalls);
                    }
                    //铆钉刻标:间距渐收(24→6列),亮橙=层染里机件的跳色,轨越近保养越勤
                    tickRun++;
                    if (tickRun >= (int)MathHelper.Lerp(24f, 6f, t)) {
                        tickRun = 0;
                        writes += PaintWallCell(x, interiorTop + 1, L6Palette.HotPaint, ProofWalls);
                        //刻标下偶发焦油垂滴:轨用久了会漏油(35%)
                        if (Hash(x, 0x5E01) % 100 < 35) {
                            L6Palette.TarDrip(x, interiorTop + 2, 2 + Hash(x, 0x5E02) % 2);
                            writes += 2;
                        }
                    }
                }
            }
            //门洞警示纹:房内检修位"黑/锈橙相间"语言的外延;门楣齿轮块罩黑漆
            //(层染已把齿轮刷亮橙,黑楣才框得出门洞),槛带黑/锈橙相间
            writes += PaintThreshold(origin, bounds, floorTop,
                ProofingHallRoom.LeftDoorOffset, ProofingHallRoom.RightDoorOffset, ProofingHallRoom.DoorHeight,
                archWall: PaintID.GrayPaint, lintel: L6Palette.TarPaint,
                floorA: L6Palette.TarPaint, floorB: L6Palette.RustPaint);
            return writes;
        }

        //==================== 公共:门槛处理与底层写入 ====================

        /// <summary>
        /// 门槛三件:门洞墙面染线色、门楣砖染、门外地板4格槛带(floorA/floorB逐格相间,
        /// 同色传入即纯色带)。让门洞在一屏内从普通脊段里跳出来。
        /// </summary>
        private static int PaintThreshold(Point origin, Rectangle bounds, int floorTop,
            Point leftDoor, Point rightDoor, int doorHeight,
            byte archWall, byte lintel, byte floorA, byte floorB) {
            int writes = 0;
            for (int side = 0; side < 2; side++) {
                Point door = side == 0 ? leftDoor : rightDoor;
                int doorX = origin.X + door.X;
                int doorTopY = origin.Y + door.Y;
                //门洞墙面(3深x门高)
                for (int dx = 0; dx < 3; dx++) {
                    for (int dy = 0; dy < doorHeight; dy++) {
                        Tile t = Main.tile[doorX + dx, doorTopY + dy];
                        if (!t.HasTile && t.WallType != 0) {
                            t.WallColor = archWall;
                            writes++;
                        }
                    }
                }
                //门楣砖(洞顶一行)
                for (int dx = 0; dx < 3; dx++) {
                    if (WorldGen.paintTile(doorX + dx, doorTopY - 1, lintel)) {
                        writes++;
                    }
                }
                //门外地板槛带4格
                int outStart = side == 0 ? bounds.Left - 4 : bounds.Right;
                for (int i = 0; i < 4; i++) {
                    if (WorldGen.paintTile(outStart + i, floorTop, (i & 1) == 0 ? floorA : floorB)) {
                        writes++;
                    }
                }
            }
            return writes;
        }

        /// <summary>染单个空格的墙面(只认本层地牢墙族,骨架实心区wall=0天然跳过)</summary>
        private static int PaintWallCell(int x, int y, byte paint, ushort[] wallFamily) {
            if (!WorldGen.InWorld(x, y, 5)) {
                return 0;
            }
            Tile t = Main.tile[x, y];
            if (t.HasTile || !Contains(wallFamily, t.WallType)) {
                return 0;
            }
            t.WallColor = paint;
            return 1;
        }

        /// <summary>自yTop向下淌len行的垂痕,只染本层墙族空格,遇实心/异墙即停</summary>
        private static int PaintStreak(int x, int yTop, int floorTop, int len,
            byte paint, ushort[] wallFamily) {
            int painted = 0;
            for (int i = 0; i < len && yTop + i < floorTop; i++) {
                int y = yTop + i;
                if (!WorldGen.InWorld(x, y, 5)) {
                    break;
                }
                Tile t = Main.tile[x, y];
                if (t.HasTile || !Contains(wallFamily, t.WallType)) {
                    break;
                }
                t.WallColor = paint;
                painted++;
            }
            return painted;
        }

        private static bool Contains(ushort[] set, ushort value) {
            foreach (ushort v in set) {
                if (v == value) {
                    return true;
                }
            }
            return false;
        }

        //确定性散列(镜像ProofingHallRoom.Hash口径):同种子同落位=逐格同形,零genRand
        private static int Hash(int x, int salt) {
            unchecked {
                int h = (x * 374761393) ^ (salt * 668265263) ^ 0x2E1B;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return h & 0x7FFFFFFF;
            }
        }
    }
}
