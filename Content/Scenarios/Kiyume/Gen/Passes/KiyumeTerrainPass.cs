using System;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Gen.Passes
{
    //P30 地表：逐带材质贴面 + 深处血石基底 + 血湖灌水 + 湖畔村轮廓
    //骨架 pass 已按带表浇过体块，这里只重写表层那十几行与湖盆，别整列重刷
    internal class KiyumeTerrainPass : GenPass
    {
        //深处基底行：整个世界坐在血石上——湖不是局部现象，是这地方的底子
        private const int DeepBaseRow = 640;
        //表层贴面厚度（行）
        private const int VeneerRows = 14;

        public KiyumeTerrainPass() : base("Kiyume Terrain", 1f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            KiyumePlans.Report(progress, "血湖退到岸线以下...");
            var log = CWRMod.Instance.Logger;
            log.Info("[Kiyume] Terrain start");

            int width = Main.maxTilesX;
            int bottom = Main.maxTilesY - KiyumeMetrics.BorderThick;

            for (int x = KiyumeMetrics.BorderThick; x < width - KiyumeMetrics.BorderThick; x++) {
                progress.Set(x / (double)(width - 1) * 0.55);
                int band = KiyumeMetrics.BandIndexForColumn(x);
                int floorTop = KiyumePlans.FloorTopAt(x);

                for (int d = 0; d < VeneerRows; d++) {
                    int y = floorTop + d;
                    if (y >= bottom) {
                        break;
                    }
                    KiyumeTileBrush.SetSolid(x, y, Veneer(band, d));
                }
                for (int y = Math.Max(DeepBaseRow, floorTop); y < bottom; y++) {
                    KiyumeTileBrush.SetSolid(x, y, TileID.Crimstone);
                }
            }
            log.Info("[Kiyume] Terrain veneer done");

            KiyumePlans.Report(progress, "湖水回到它该在的高度...");
            FillLake();
            progress.Set(0.72);
            log.Info($"[Kiyume] Terrain lake={KiyumeTileBrush.LiquidWrites}");

            KiyumePlans.Report(progress, "岸上的房子还站着...");
            KiyumeVillage.Build(progress);
            //村子削平过地基，出生行要按回写后的规划重取
            Main.spawnTileY = KiyumePlans.FloorTopAt(KiyumeMetrics.SpawnX);
            progress.Set(1.0);

            log.Info(
                $"[Kiyume] Terrain 湖格={KiyumeTileBrush.LiquidWrites}"
                + $" 民居={KiyumeVillage.Huts} 望楼={KiyumeVillage.Towers} 灯={KiyumeVillage.Torches}"
                + $" spawn=({Main.spawnTileX},{Main.spawnTileY})");
        }

        //逐带表层材质：湖底淤泥 / 滩涂湿沙 / 村落烬灰 / 枯林干土 / 远山裸岩
        private static ushort Veneer(int band, int depth) => band switch {
            0 => depth < 3 ? TileID.Mud : TileID.Crimstone,
            1 => depth < 1 ? TileID.CrimsonHardenedSand : depth < 7 ? TileID.Mud : TileID.Crimstone,
            2 => depth < 1 ? TileID.Ash : depth < 9 ? TileID.Dirt : TileID.Stone,
            3 => depth < 2 ? TileID.Ash : depth < 12 ? TileID.Dirt : TileID.Stone,
            _ => TileID.Stone,
        };

        //湖盆灌到统一水面行；岸线自己浮出来——地面高过水面的列就是滩
        //NormalUpdates=false 液体不流动，构造性铺设即定型；游得太深会淹死，那就是西界的劝返
        private static void FillLake() {
            int surface = KiyumeMetrics.LakeSurfaceRow;
            int right = KiyumeMetrics.ShoalLeft + 40;
            for (int x = KiyumeMetrics.BorderThick; x < right; x++) {
                int floorTop = KiyumePlans.FloorTopAt(x);
                if (floorTop <= surface) {
                    continue;
                }
                for (int y = surface; y < floorTop; y++) {
                    KiyumeTileBrush.SetWater(x, y);
                }
            }
        }
    }
}
