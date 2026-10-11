using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

public sealed class SakuraReadyStanceDrawPlayer : ModPlayer
{
    public override void TransformDrawData(ref PlayerDrawSet drawInfo)
    {
        if (SakuraReadyStanceWeaponLayer.GetHeldStance(drawInfo.drawPlayer) != null)
            drawInfo.projectileDrawPosition = -1;
    }
}
