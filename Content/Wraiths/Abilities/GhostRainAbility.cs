using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Projectiles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Abilities
{
    /// <summary>鬼雨常驻通道：提刀役使时确保雨幕控制器弹体在场。</summary>
    internal sealed class GhostRainAbility : WraithPassiveAbility
    {
        internal const string Key = "GhostRain";

        public override void Update(in WraithAbilityContext context)
            => EnsureController(in context);

        private static GhostRainProj EnsureController(in WraithAbilityContext context) {
            Player player = context.Player;
            if (player == null || player.whoAmI != Main.myPlayer || !player.active || player.dead) {
                return null;
            }

            GhostRainProj existing = GhostRainProj.Find(player.whoAmI);
            if (existing != null) {
                return existing;
            }

            int projectileType = ModContent.ProjectileType<GhostRainProj>();
            int whoAmI = Projectile.NewProjectile(
                player.GetSource_Misc("CWRWraith_GhostRainAbility"),
                player.MountedCenter,
                Vector2.Zero,
                projectileType,
                0,
                0f,
                player.whoAmI,
                0f,
                0f,
                context.Revival);
            return whoAmI >= 0 && whoAmI < Main.maxProjectiles
                ? Main.projectile[whoAmI].ModProjectile as GhostRainProj
                : null;
        }
    }
}
