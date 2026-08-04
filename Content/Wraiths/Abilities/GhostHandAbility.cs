using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Projectiles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Abilities
{
    internal sealed class GhostHandAbility : WraithPassiveAbility
    {
        internal const string Key = "GhostHand";
        internal const float GrabRange = 300f;

        internal static bool CanGrab(NPC npc, Vector2 center)
            => npc.CanBeChasedBy() && !npc.boss && !npc.HasBuff<Buffs.GhostGripDebuff>()
                && Vector2.DistanceSquared(npc.Center, center) < GrabRange * GrabRange;

        public override void Update(in WraithAbilityContext context) {
            Player player = context.Player;
            if (player == null || player.whoAmI != Main.myPlayer || !player.active || player.dead) {
                return;
            }
            int projectileType = ModContent.ProjectileType<GhostHandProj>();
            if (player.ownedProjectileCounts[projectileType] > 0) {
                return;
            }
            Projectile.NewProjectile(
                player.GetSource_Misc("CWRWraith_GhostHandAbility"),
                player.Center,
                Vector2.Zero,
                projectileType,
                0,
                0f,
                player.whoAmI,
                0f,
                0f,
                context.Mastery);
        }
    }
}
