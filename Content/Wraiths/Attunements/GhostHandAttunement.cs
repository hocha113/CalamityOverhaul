using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Projectiles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Attunements
{
    /// <summary>维持玩家背后的焦黑枯手；抓取循环由弹体自己管理。</summary>
    internal sealed class GhostHandAttunement : WraithAttunement
    {
        internal const string Key = "GhostHand";
        internal const float GrabRange = 300f;

        internal static bool CanGrab(NPC npc, Vector2 center)
            => npc.CanBeChasedBy() && !npc.boss
               && Vector2.DistanceSquared(npc.Center, center) < GrabRange * GrabRange;

        public override void Update(in WraithAttunementContext context) {
            Player player = context.Player;
            if (player == null || player.whoAmI != Main.myPlayer || !player.active || player.dead) {
                return;
            }

            int projectileType = ModContent.ProjectileType<GhostHandProj>();
            if (player.ownedProjectileCounts[projectileType] > 0) {
                return;
            }

            Projectile.NewProjectile(
                player.GetSource_Misc("CWRWraith_GhostHandAttunement"),
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