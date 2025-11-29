using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Campsites
{
    /// <summary>
    /// 老公爵营地管理器
    /// </summary>
    internal class OldDukeCampsiteManager : ModSystem
    {
        private bool hasTriedGenerate;

        public override void PostUpdatePlayers() {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return;
            }

            //检查是否应该生成营地
            if (!hasTriedGenerate && ShouldGenerateCampsite(player)) {
                TryGenerateCampsite();
                hasTriedGenerate = true;
            }
        }

        /// <summary>
        /// 检查是否应该生成营地
        /// </summary>
        private static bool ShouldGenerateCampsite(Player player) {
            if (!player.TryGetADVSave(out var save)) {
                return false;
            }

            //检查玩家是否已经同意合作
            if (!save.OldDukeCooperationAccepted) {
                return false;
            }

            //检查营地是否已经生成
            if (OldDukeCampsite.IsGenerated) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 尝试在地图右侧海岸生成营地
        /// </summary>
        private static void TryGenerateCampsite() {
            //在世界右侧寻找合适的位置
            int worldRightEdge = Main.maxTilesX - 200;//距离右侧边缘200格

            //从地表向下搜索
            int startY = (int)(Main.worldSurface * 0.35f);//从地表上方开始
            int endY = (int)(Main.worldSurface * 1.2f);//到地表下方

            for (int y = startY; y < endY; y++) {
                Tile tile = Main.tile[worldRightEdge, y];
                if (tile == null) {
                    continue;
                }

                //检查是否是实心地面
                if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                    //检查上方是否有足够空间
                    bool hasSpace = true;
                    for (int checkY = y - 1; checkY >= y - 10; checkY--) {
                        Tile checkTile = Main.tile[worldRightEdge, checkY];
                        if (checkTile != null && checkTile.HasTile && Main.tileSolid[checkTile.TileType]) {
                            hasSpace = false;
                            break;
                        }
                    }

                    if (hasSpace) {
                        //找到合适位置，生成营地
                        Vector2 campsitePosition = new Vector2(
                            worldRightEdge * 16 + 8,//转换为像素坐标，并居中
                            y * 16 - 32//稍微抬高一点
                        );

                        OldDukeCampsite.GenerateCampsite(campsitePosition);
                        return;
                    }
                }
            }

            //如果没找到合适位置，使用默认位置
            Vector2 defaultPos = new Vector2(
                (Main.maxTilesX - 200) * 16,
                (int)(Main.worldSurface * 16) - 64
            );
            OldDukeCampsite.GenerateCampsite(defaultPos);
        }

        public override void OnWorldUnload() {
            hasTriedGenerate = false;
            OldDukeCampsite.ClearCampsite();
        }
    }
}
