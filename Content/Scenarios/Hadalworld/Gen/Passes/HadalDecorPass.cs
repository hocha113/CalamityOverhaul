using CalamityOverhaul.Content.Scenarios.Hadalworld.Gen.Core;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Hadalworld.Gen.Passes
{
    //P30:装饰第一版(蓝图§10):浅海珊瑚海草、洞窟钟乳石、碎石堆、
    //花岗岩堆、鲸落骨堆、晶簇、微光蘑菇。全走原版放置校验,拒绝即计数跳过,
    //绝不强写帧(镜像Dungeonworld §3.2-1);样式号均对TML源核实
    internal class HadalDecorPass : GenPass
    {
        public HadalDecorPass() : base("Hadalworld Decor", 2f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "深海装饰散布...";
            HadalTerrainModel model = HadalGenContext.Model;
            HadalTerrainPlan plan = model.Plan;
            var rand = WorldGen.genRand;
            int playLeft = model.P.PlayLeft;
            int playRight = model.P.PlayRight;

            //——浅海:珊瑚(礁丘偏置60%)+海燕麦——
            for (int i = 0; i < 1000; i++) {
                int x = SampleSunlitX(plan, rand, playLeft, playRight, reefBias: true);
                int floor = FindFloor(x, HadalworldMetrics.SeaLevelRow + 4, 200);
                bool ok = false;
                if (floor > 0 && IsSandFamily(Main.tile[x, floor].TileType)) {
                    WorldGen.PlaceTile(x, floor - 1, TileID.Coral, mute: true);
                    ok = Main.tile[x, floor - 1].HasTile && Main.tile[x, floor - 1].TileType == TileID.Coral;
                }
                HadalGenContext.CountDecor("珊瑚", ok);
                progress.Set(0.1 * i / 1000.0);
            }
            for (int i = 0; i < 800; i++) {
                int x = SampleSunlitX(plan, rand, playLeft, playRight, reefBias: false);
                int floor = FindFloor(x, HadalworldMetrics.SeaLevelRow + 4, 200);
                bool ok = false;
                if (floor > 0 && IsSandFamily(Main.tile[x, floor].TileType)) {
                    WorldGen.PlaceTile(x, floor - 1, TileID.SeaOats, mute: true, forced: false, -1, rand.Next(5));
                    ok = Main.tile[x, floor - 1].HasTile && Main.tile[x, floor - 1].TileType == TileID.SeaOats;
                }
                HadalGenContext.CountDecor("海燕麦", ok);
            }
            progress.Set(0.2);

            //——钟乳石/石笋:PlaceTight自动认材质(石/砂岩/花岗岩变体,对源核实)——
            for (int i = 0; i < 3200; i++) {
                int x = rand.Next(playLeft + 2, playRight - 2);
                int y = rand.Next(700, 4700);
                if (Main.tile[x, y].HasTile || Main.tile[x, y + 1].HasTile) {
                    HadalGenContext.CountDecor("钟乳石", false);
                    continue;
                }
                bool ceiling = SolidAt(x, y - 1);
                bool floorBelow = SolidAt(x, y + 2) && !Main.tile[x, y + 1].HasTile;
                if (!ceiling && !floorBelow) {
                    HadalGenContext.CountDecor("钟乳石", false);
                    continue;
                }
                WorldGen.PlaceTight(x, ceiling ? y : y + 1);
                HadalGenContext.CountDecor("钟乳石", Main.tile[x, ceiling ? y : y + 1].TileType == TileID.Stalactite
                    && Main.tile[x, ceiling ? y : y + 1].HasTile);
            }
            progress.Set(0.4);

            //——碎石堆(小堆185两排随机)——
            for (int i = 0; i < 2200; i++) {
                int x = rand.Next(playLeft + 2, playRight - 2);
                int floor = FindFloor(x, rand.Next(1300, 4650), 60);
                bool ok = false;
                if (floor > 0) {
                    WorldGen.PlaceSmallPile(x, floor - 1, rand.Next(6), rand.Next(2), 185);
                    ok = Main.tile[x, floor - 1].HasTile;
                }
                HadalGenContext.CountDecor("碎石堆", ok);
            }
            //——花岗岩堆(深渊调性:小堆185样式34-37+大堆187样式9-13,对源核实)——
            for (int i = 0; i < 900; i++) {
                int x = rand.Next(playLeft + 3, playRight - 3);
                int floor = FindFloor(x, rand.Next(2750, 4700), 70);
                bool ok = false;
                if (floor > 0) {
                    if (rand.NextBool(3)) {
                        WorldGen.PlaceTile(x, floor - 1, TileID.LargePiles2, mute: true, forced: false, -1, 9 + rand.Next(5));
                    }
                    else {
                        WorldGen.PlaceSmallPile(x, floor - 1, 34 + rand.Next(4), 1, 185);
                    }
                    ok = Main.tile[x, floor - 1].HasTile;
                }
                HadalGenContext.CountDecor("花岗堆", ok);
            }
            progress.Set(0.6);

            //——骨堆:V底终腔鲸落场加密+超深渊/下厅散布(大骨堆186样式22-25,对源核实)——
            (float bulbX, float bulbY, float bulbRx, _) = plan.VEndBulb;
            for (int i = 0; i < 130; i++) {
                int x = (int)bulbX + rand.Next(-(int)(bulbRx + 26), (int)(bulbRx + 27));
                int floor = FindFloor(x, (int)bulbY - 14, 46);
                bool ok = false;
                if (floor > 0) {
                    WorldGen.PlaceTile(x, floor - 1, TileID.LargePiles, mute: true, forced: false, -1, 22 + rand.Next(4));
                    ok = Main.tile[x, floor - 1].HasTile;
                }
                HadalGenContext.CountDecor("鲸落骨堆", ok);
            }
            for (int i = 0; i < 320; i++) {
                int x = rand.Next(playLeft + 3, playRight - 3);
                int floor = FindFloor(x, rand.Next(2) == 0 ? rand.Next(4120, 4700) : rand.Next(3400, 4050), 60);
                bool ok = false;
                if (floor > 0) {
                    WorldGen.PlaceTile(x, floor - 1, TileID.LargePiles, mute: true, forced: false, -1, 22 + rand.Next(4));
                    ok = Main.tile[x, floor - 1].HasTile;
                }
                HadalGenContext.CountDecor("散骨堆", ok);
            }
            progress.Set(0.75);

            //——晶簇(裸露宝石178样式0-6,对源核实):午夜以下洞窟地面——
            for (int i = 0; i < 900; i++) {
                int x = rand.Next(playLeft + 2, playRight - 2);
                int floor = FindFloor(x, rand.Next(1800, 4700), 50);
                bool ok = false;
                if (floor > 0) {
                    WorldGen.PlaceTile(x, floor - 1, TileID.ExposedGems, mute: true, forced: false, -1, rand.Next(7));
                    ok = Main.tile[x, floor - 1].HasTile && Main.tile[x, floor - 1].TileType == TileID.ExposedGems;
                }
                HadalGenContext.CountDecor("晶簇", ok);
            }
            progress.Set(0.85);

            //——微光蘑菇:蘑菇草(核心层微光斑)顶面45%长小发光蘑菇——
            int shroomOk = 0, shroomTry = 0;
            for (int x = playLeft; x < playRight; x++) {
                for (int y = 300; y < HadalworldMetrics.DeepestPlayableRow; y++) {
                    Tile tile = Main.tile[x, y];
                    if (!tile.HasTile || tile.TileType != TileID.MushroomGrass || Main.tile[x, y - 1].HasTile) {
                        continue;
                    }
                    shroomTry++;
                    if (!rand.NextBool(9, 20)) {
                        continue;
                    }
                    WorldGen.PlaceTile(x, y - 1, TileID.MushroomPlants, mute: true);
                    if (Main.tile[x, y - 1].HasTile) {
                        shroomOk++;
                    }
                }
            }
            HadalGenContext.Decor["微光蘑菇"] = (shroomOk, shroomTry);
            progress.Set(1.0);

            CWRMod.Instance.Logger.Info($"[Hadalworld] P30 Decor {HadalGenContext.DecorSummary()}");
        }

        //礁丘偏置采样:60%截取礁丘±带宽,余下全域
        private static int SampleSunlitX(HadalTerrainPlan plan, Terraria.Utilities.UnifiedRandom rand,
            int playLeft, int playRight, bool reefBias) {
            if (reefBias && plan.Reefs.Count > 0 && rand.Next(5) >= 2) {
                (int rx, _, int rw) = plan.Reefs[rand.Next(plan.Reefs.Count)];
                return Utils.Clamp(rx + rand.Next(-rw, rw + 1), playLeft + 2, playRight - 3);
            }
            return rand.Next(playLeft + 2, playRight - 2);
        }

        private static bool IsSandFamily(ushort type)
            => type == TileID.Sand || type == TileID.HardenedSand || type == TileID.Sandstone;

        private static bool SolidAt(int x, int y) {
            Tile tile = Main.tile[x, y];
            return tile.HasTile && Main.tileSolid[tile.TileType];
        }

        /// <summary>自yStart向下找首个"实心且上方为空"的地板行,找不到返回-1</summary>
        private static int FindFloor(int x, int yStart, int maxDown) {
            int y = yStart;
            //起点埋在实心里先上浮
            int guard = 0;
            while (y > 70 && Main.tile[x, y].HasTile && guard++ < 40) {
                y--;
            }
            if (guard >= 40) {
                return -1;
            }
            for (int dy = 0; dy < maxDown; dy++) {
                int yy = y + dy;
                if (yy >= HadalworldMetrics.DeepestPlayableRow) {
                    return -1;
                }
                if (SolidAt(x, yy) && !Main.tile[x, yy - 1].HasTile) {
                    return yy;
                }
            }
            return -1;
        }
    }
}
