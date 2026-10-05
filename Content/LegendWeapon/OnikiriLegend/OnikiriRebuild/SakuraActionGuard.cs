using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

internal static class SakuraActionGuard
{
    internal static bool CanStart(Player player) => !player.dead && !player.CCed
        && !player.GetModPlayer<SakuraDashPlayer>().IsDashing
        && !player.GetModPlayer<SakuraKiriPlayer>().IsActing
        && player.ownedProjectileCounts[ModContent.ProjectileType<SakuraSlashProj>()] == 0
        && player.ownedProjectileCounts[ModContent.ProjectileType<SakuraSlash2Proj>()] == 0
        && player.ownedProjectileCounts[ModContent.ProjectileType<SakuraDashProj>()] == 0
        && player.ownedProjectileCounts[ModContent.ProjectileType<SakuraReadyStanceProj>()] == 0
        && player.ownedProjectileCounts[ModContent.ProjectileType<SakuraKiriPlayerProj>()] == 0;
}
