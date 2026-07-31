using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDismembers;
using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Projectiles;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Attunements
{
    internal sealed class HeadlessShadeAttunement : WraithAttunement
    {
        internal const string Key = "HeadlessShade";
        internal const float HuntRange = 620f;

        internal static bool CanHunt(NPC npc)
            => npc.CanBeChasedBy()
               && !OniDismember.IsLocked(npc.whoAmI);

        public override void Update(in WraithAttunementContext context) {
            Player player = context.Player;
            if (player == null || player.whoAmI != Main.myPlayer || !player.active || player.dead) {
                return;
            }

            int projectileType = ModContent.ProjectileType<HeadlessShadeProj>();
            if (player.ownedProjectileCounts[projectileType] > 0) {
                return;
            }

            float mastery = MathHelper.Clamp(context.Mastery, 0f, 1f);
            int weaponDamage = Math.Max(player.GetWeaponDamage(context.VesselItem), 1);
            int damage = Math.Max((int)(weaponDamage * MathHelper.Lerp(0.55f, 0.90f, mastery)), 1);
            float knockback = player.GetWeaponKnockback(context.VesselItem)
                * MathHelper.Lerp(0.65f, 1f, mastery);

            Projectile.NewProjectile(
                player.GetSource_ItemUse(context.VesselItem),
                player.Center,
                Vector2.Zero,
                projectileType,
                damage,
                knockback,
                player.whoAmI,
                0f,
                0f,
                mastery);
        }
    }
}