using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Projectiles
{
    /// <summary>皇后弹幕公用工具</summary>
    internal static class QueenProjHelper
    {
        /// <summary>清空皇后所有在场弹幕(服务端)，阶段转换/大招/死亡的公平阀</summary>
        public static void ClearQueenProjectiles() {
            int beam = ModContent.ProjectileType<QueenPrismBeamProj>();
            int shard = ModContent.ProjectileType<QueenShardProj>();
            int meteor = ModContent.ProjectileType<QueenGelMeteorProj>();
            int spire = ModContent.ProjectileType<QueenCrystalSpireProj>();
            int chandelier = ModContent.ProjectileType<QueenChandelierProj>();
            int gale = ModContent.ProjectileType<QueenGaleFieldProj>();
            int royal = ModContent.ProjectileType<QueenRoyalChandelierProj>();
            int prison = ModContent.ProjectileType<QueenCrystalPrisonProj>();

            foreach (var p in Main.ActiveProjectiles) {
                if (p.type == beam || p.type == shard || p.type == meteor
                    || p.type == spire || p.type == chandelier || p.type == gale
                    || p.type == royal || p.type == prison) {
                    p.Kill();
                }
            }
        }
    }
}
