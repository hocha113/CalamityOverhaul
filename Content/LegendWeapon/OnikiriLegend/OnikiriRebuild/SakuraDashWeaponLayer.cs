using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

/// <summary>武器夹在玩家身体与前臂之间；刀光仍由弹幕 overPlayers 层绘制。</summary>
public sealed class SakuraDashWeaponLayer : PlayerDrawLayer
{
    public override Position GetDefaultPosition() => new BeforeParent(PlayerDrawLayers.ArmOverItem);

    public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) => !drawInfo.headOnlyRender
        && drawInfo.shadow == 0f
        && drawInfo.drawPlayer.GetModPlayer<SakuraDashPlayer>().ActiveDash is { ShowFinisherWeapon: true };

    protected override void Draw(ref PlayerDrawSet drawInfo) =>
        drawInfo.drawPlayer.GetModPlayer<SakuraDashPlayer>().ActiveDash?.DrawHeldWeapon(ref drawInfo);
}
