using InnoVault.TileProcessors;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Suppressors
{
    /// <summary>
    /// 宁静力场的刷怪压制:玩家处于任一运转中力场的半径内时,自然刷怪归零。<br/>
    /// EditSpawnRate 由执行刷怪的一端调用(服务器/单人),TP 状态经锚定同步,无需额外网络处理
    /// </summary>
    internal class PacifierSpawnSuppression : GlobalNPC
    {
        //缓存TP的ID,避免每次全列表类型判断
        private static int pacifierTPID = -1;

        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns) {
            if (pacifierTPID < 0) {
                pacifierTPID = TPUtils.GetID<PacifierTowerTP>();
            }

            float radiusSQ = PacifierTowerTP.SuppressRadius * PacifierTowerTP.SuppressRadius;
            foreach (var baseTP in TileProcessorLoader.TP_InWorld) {
                if (baseTP.ID != pacifierTPID || baseTP is not PacifierTowerTP pacifier) {
                    continue;
                }
                if (!pacifier.SuppressActive) {
                    continue;
                }
                if (player.Center.DistanceSQ(pacifier.CenterInWorld) > radiusSQ) {
                    continue;
                }

                maxSpawns = 0;
                spawnRate *= 30;
                return;
            }
        }
    }
}
