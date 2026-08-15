using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Passes
{
    //P10:天空缓冲清空+层带/隔离带/深渊带实心填充+四周边界(§1.5)
    internal class SkeletonPass : GenPass
    {
        public SkeletonPass() : base("Dungeonworld Skeleton", 3f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "浇筑地牢层带骨架...";
            TileBrush.ResetForNewGen();
            GenClock.Reset();

            int width = Main.maxTilesX;
            int height = Main.maxTilesY;

            //逐行砖色表,换色只动Bands;隔离带由BrickForRow切成上下两半的过渡带
            ushort[] rowBrick = new ushort[height];
            for (int y = 0; y < height; y++) {
                rowBrick[y] = DungeonworldMetrics.BrickForRow(y);
            }

            for (int x = 0; x < width; x++) {
                progress.Set(x / (double)(width - 1));
                //侧边实心带一路填到玩家钳制线,天空带内也不留"能飞进却撞隐形墙"的缝
                bool sideBorder = x < DungeonworldMetrics.PlayLeft
                    || x >= DungeonworldMetrics.PlayRight;
                for (int y = 0; y < height; y++) {
                    bool solid = y >= DungeonworldMetrics.SkyRows
                        || y < DungeonworldMetrics.BorderThick
                        || sideBorder;
                    if (solid) {
                        TileBrush.SetSolid(x, y, rowBrick[y]);
                    }
                    else {
                        TileBrush.ClearCell(x, y, WallID.None);
                    }
                }
            }

            CWRMod.Instance.Logger.Info(
                $"[Dungeonworld] P10 Skeleton solid={TileBrush.SolidWrites} air={TileBrush.ClearWrites}");
        }
    }
}
