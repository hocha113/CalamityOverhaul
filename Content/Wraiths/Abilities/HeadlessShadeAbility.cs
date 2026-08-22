using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDismembers;
using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Marks;
using CalamityOverhaul.Content.Wraiths.Projectiles;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Abilities
{
    internal sealed class HeadlessShadeAbility : WraithPassiveAbility
    {
        internal const string Key = "HeadlessShade";
        internal const float HuntRange = 620f;

        /// <summary>「按住了斩」：鬼影优先扑向动弹不得的猎物，那一刀不会落空。</summary>
        internal static readonly WraithSynergyRule PinnedHunt = new() {
            Id = "HeadlessShade.PinnedHunt",
            Trigger = WraithMark.Gripped,
            Channel = WraithSynergyChannel.TargetBias,
            Name = () => WraithCovenText.HandShadeName,
            Note = () => WraithCovenText.HandShadeNote,
            UiPriority = 20,
        };

        internal static bool CanHunt(NPC npc)
            => npc.CanBeChasedBy() && !OniDismember.IsLocked(npc.whoAmI);

        public override void Update(in WraithAbilityContext context) {
            Player player = context.Player;
            if (player == null || player.whoAmI != Main.myPlayer || !player.active || player.dead) {
                return;
            }
            int projectileType = ModContent.ProjectileType<HeadlessShadeProj>();
            if (player.ownedProjectileCounts[projectileType] > 0) {
                return;
            }
            float revival = MathHelper.Clamp(context.Revival, 0f, 1f);
            int weaponDamage = Math.Max(player.GetWeaponDamage(context.VesselItem), 1);
            int damage = Math.Max((int)(weaponDamage * MathHelper.Lerp(0.55f, 0.90f, revival)), 1);
            float knockback = player.GetWeaponKnockback(context.VesselItem)
                * MathHelper.Lerp(0.65f, 1f, revival);
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
                revival);
        }
    }
}
