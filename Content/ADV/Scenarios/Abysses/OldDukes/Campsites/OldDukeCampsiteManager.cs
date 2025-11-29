using Terraria;
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
        /// 尝试生成营地
        /// </summary>
        private static void TryGenerateCampsite() {
            //使用位置查找器寻找最佳位置
            Vector2? position = CampsiteLocationFinder.FindBestLocation();

            if (position.HasValue) {
                OldDukeCampsite.GenerateCampsite(position.Value);
            }
            else {
                //如果找不到合适位置，记录警告但不生成营地
            }
        }

        public override void OnWorldUnload() {
            hasTriedGenerate = false;
            OldDukeCampsite.ClearCampsite();
        }
    }
}
