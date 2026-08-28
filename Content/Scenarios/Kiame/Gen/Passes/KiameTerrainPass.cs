using System;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Kiame.Gen.Passes
{
    //P30 地表：逐带材质贴面 + 深处石底 + 洼地灌黑水 + 洼底淤泥
    //骨架 pass 已按带表浇过体块，这里只重写表层那十几行与洼地，别整列重刷
    //材质纪律：全程不用血石/黑檀石等邪恶块——水样式归子世界场景效果接管，不许被群系抢走
    internal class KiameTerrainPass : GenPass
    {
        //表层贴面厚度（行）
        private const int VeneerRows = 12;

        public KiameTerrainPass() : base("Kiame Terrain", 1f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            KiamePlans.Report(progress, "泥地喝饱了水...");
            var log = CWRMod.Instance.Logger;
            log.Info("[Kiame] Terrain start");

            int width = Main.maxTilesX;
            int bottom = Main.maxTilesY - KiameMetrics.BorderThick;

            for (int x = KiameMetrics.BorderThick; x < width - KiameMetrics.BorderThick; x++) {
                progress.Set(x / (double)(width - 1) * 0.7);
                int band = KiameMetrics.BandIndexForColumn(x);
                int floorTop = KiamePlans.FloorTopAt(x);

                for (int d = 0; d < VeneerRows; d++) {
                    int y = floorTop + d;
                    if (y >= bottom) {
                        break;
                    }
                    KiameTileBrush.SetSolid(x, y, Veneer(band, d));
                }
                for (int y = Math.Max(KiameMetrics.DeepBaseRow, floorTop); y < bottom; y++) {
                    KiameTileBrush.SetSolid(x, y, TileID.Stone);
                }
                //地表以下满铺自然背景墙：没有墙的地体会被天光判定打穿，
                //挖开一格就漏光（洼水上方刻意不铺，露天池塘不该有墙）
                for (int y = floorTop + 1; y < bottom; y++) {
                    KiameTileBrush.SetWall(x, y, UnderWall(band, y, floorTop));
                }
            }
            log.Info("[Kiame] Terrain veneer done");

            KiamePlans.Report(progress, "洼里的黑水不肯走...");
            FillPools();
            progress.Set(1.0);

            log.Info($"[Kiame] Terrain 洼水格={KiameTileBrush.LiquidWrites} 洼数={KiamePlans.Pools.Count}");
        }

        //逐带表层材质（全深色，亮土不上台面）：
        //台地烬皮裸岩 / 村带烬面盖深泥 / 洼原厚泥黏土 / 泽地深泥黏土 / 预留岭烬皮裸岩
        private static ushort Veneer(int band, int depth) => band switch {
            0 => depth < 1 ? TileID.Ash : TileID.Stone,
            1 or 3 => depth < 2 ? TileID.Ash : TileID.Mud,
            2 => depth < 3 ? TileID.Mud : depth < 8 ? TileID.ClayBlock : TileID.Mud,
            4 => depth < 4 ? TileID.Mud : depth < 10 ? TileID.ClayBlock : TileID.Mud,
            _ => depth < 1 ? TileID.Ash : TileID.Stone,
        };

        //地下自然墙：泥系带表层挂泥墙，其余挂土墙，深处石底换洞穴石墙
        private static ushort UnderWall(int band, int y, int floorTop) {
            if (y >= KiameMetrics.DeepBaseRow + 4) {
                return WallID.Cave7Unsafe;
            }
            int depth = y - floorTop;
            return band is >= 1 and <= 4 && depth < 6 ? WallID.MudUnsafe : WallID.DirtUnsafe;
        }

        //洼地灌水到各自登记的水面行；NormalUpdates=false 液体不流动，构造性铺设即定型
        //洼底再压两行淤泥：雨水泡出来的坑，底不是干土
        private static void FillPools() {
            foreach (KiamePoolSpan pool in KiamePlans.Pools) {
                for (int x = pool.Left; x <= pool.Right; x++) {
                    int floorTop = KiamePlans.FloorTopAt(x);
                    if (floorTop <= pool.SurfaceRow) {
                        continue;
                    }
                    for (int y = pool.SurfaceRow; y < floorTop; y++) {
                        KiameTileBrush.SetWater(x, y);
                    }
                    for (int d = 0; d < 2; d++) {
                        KiameTileBrush.SetSolid(x, floorTop + d, TileID.Mud);
                    }
                }
            }
        }
    }
}
