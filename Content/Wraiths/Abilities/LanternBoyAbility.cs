using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Projectiles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Abilities
{
    internal sealed class LanternBoyAbility : WraithPassiveAbility
    {
        internal const string Key = "LanternBoy";

        public override void Update(in WraithAbilityContext context)
            => EnsureController(in context);

        public override void OnComboBeat(in WraithAbilityContext context,
            in WraithComboBeatEvent beat) {
            LanternBoyProj controller = EnsureController(in context);
            controller?.PublishComboBeat(in beat);
        }

        private static LanternBoyProj EnsureController(in WraithAbilityContext context) {
            Player player = context.Player;
            if (player == null || player.whoAmI != Main.myPlayer || !player.active || player.dead) {
                return null;
            }

            LanternBoyProj existing = LanternBoyProj.Find(player.whoAmI);
            if (existing != null) {
                return existing;
            }

            int projectileType = ModContent.ProjectileType<LanternBoyProj>();
            int whoAmI = Projectile.NewProjectile(
                player.GetSource_Misc("CWRWraith_LanternBoyAbility"),
                player.MountedCenter,
                Vector2.Zero,
                projectileType,
                0,
                0f,
                player.whoAmI,
                0f,
                0f,
                context.Revival);
            LanternBoyProj created = whoAmI >= 0 && whoAmI < Main.maxProjectiles
                ? Main.projectile[whoAmI].ModProjectile as LanternBoyProj
                : null;
            created?.BindVessel(context.VesselItem);
            return created;
        }
    }
}
