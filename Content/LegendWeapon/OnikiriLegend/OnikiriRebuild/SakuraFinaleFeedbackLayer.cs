using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

/// <summary>Draws local burst particles without touching Terraria's screen render target.</summary>
internal sealed class SakuraFinaleFeedbackLayer : ModSystem
{
    public override void PostDrawTiles()
    {
        if (Main.dedServ || Main.gameMenu) return;
        ModContent.GetInstance<SakuraFinaleFeedback>().DrawSparks(Main.spriteBatch);
    }
}
