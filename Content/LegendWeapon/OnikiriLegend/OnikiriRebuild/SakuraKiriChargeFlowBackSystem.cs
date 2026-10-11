
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

public sealed class SakuraKiriChargeFlowBackSystem : ModSystem
{
    public override void PostDrawTiles()
    {
        if (Main.dedServ || Main.gameMenu) return;
        foreach (Player player in Main.ActivePlayers)
        {
            SakuraKiriPlayerProj action = player.GetModPlayer<SakuraKiriPlayer>().ActiveAction;
            if (action?.DrawState == SakuraKiriDrawState.Charge)
                action.DrawChargeFlow(false);
        }
    }
}
