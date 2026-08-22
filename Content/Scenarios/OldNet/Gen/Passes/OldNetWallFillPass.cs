using CalamityOverhaul.Content.Scenarios.OldNet.Gen.Zones;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Passes
{
    //P70 补墙：地表线以下所有格子统一保证有背景墙。
    //旧网把 worldSurface 压到地板以下让天幕可见（OldNetWorld.OnLoad），代价是原版
    //"无墙空格显示天空"规则在地下同样生效，任何漏刷墙的地下空腔都会透出天幕。
    //结构自带的室内墙（CarveRect/prefab）先落，这里只补 WallID.None 的缺口；
    //实心格也一并垫墙，玩家挖穿地板后露出的仍是带内墙而不是天空
    internal class OldNetWallFillPass : GenPass
    {
        public OldNetWallFillPass() : base("OldNet WallFill", 0.3f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "封补旧网地下墙体...";
            int[] floorTop = OldNetPlans.FloorTop;
            int left = OldNetMetrics.BorderThick;
            int right = Main.maxTilesX - OldNetMetrics.BorderThick;
            int bottom = Main.maxTilesY - OldNetMetrics.BorderThick;
            long filled = 0;
            for (int x = left; x < right; x++) {
                progress.Set((x - left) / (double)(right - left - 1));
                int bandIndex = OldNetMetrics.BandIndexForColumn(x);
                //黑墙体带垫黑曜石墙，其余带沿用各自室内墙
                ushort wall = bandIndex <= 0 ? WallID.ObsidianBrickUnsafe
                    : OldNetZoneStyleMap.RoomWall(bandIndex);
                for (int y = floorTop[x]; y < bottom; y++) {
                    Tile tile = Main.tile[x, y];
                    if (tile.WallType != WallID.None) {
                        continue;
                    }
                    tile.WallType = wall;
                    filled++;
                }
            }
            CWRMod.Instance.Logger.Info($"[OldNet] WallFill filled={filled}");
        }
    }
}
