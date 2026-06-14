using Terraria;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.Machines.DropPodScens
{
    /// <summary>
    /// 空降仓子世界生成：完全空旷的世界，不放置任何方块
    /// </summary>
    internal class DropPodGen : GenPass
    {
        public DropPodGen() : base("DropPodWorldGen", 1f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "Preparing drop zone...";
            int worldWidth = Main.maxTilesX;
            int worldHeight = Main.maxTilesY;

            //清空整个世界，确保完全空旷
            for (int x = 0; x < worldWidth; x++) {
                for (int y = 0; y < worldHeight; y++) {
                    Main.tile[x, y].ClearEverything();
                }
                progress.Set((float)x / worldWidth);
            }

            //设置出生点在世界中央偏上
            Main.spawnTileX = worldWidth / 2;
            Main.spawnTileY = worldHeight / 2;
        }
    }
}
