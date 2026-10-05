
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

public sealed class SakuraKiriWeaponLayer : PlayerDrawLayer
{
    public override Position GetDefaultPosition() => new BeforeParent(PlayerDrawLayers.ArmOverItem);

    public override Transformation Transform => PlayerDrawLayers.TorsoGroup;

    public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) => !drawInfo.headOnlyRender
        && drawInfo.shadow == 0f
        && drawInfo.drawPlayer.GetModPlayer<SakuraKiriPlayer>().ActiveAction is { } action
        && action.DrawState is SakuraKiriDrawState.Charge or SakuraKiriDrawState.Disappear or SakuraKiriDrawState.Sheathe;

    protected override void Draw(ref PlayerDrawSet drawInfo) =>
        drawInfo.drawPlayer.GetModPlayer<SakuraKiriPlayer>().ActiveAction?.DrawHeldWeapon(ref drawInfo);
}

/// <summary>仅第一段掠过时让武器处于身体后层；不改变起手蓄力的原有层级。</summary>
public sealed class SakuraKiriBackWeaponLayer : PlayerDrawLayer
{
    public override Position GetDefaultPosition() => new BeforeParent(PlayerDrawLayers.Skin);
    public override Transformation Transform => PlayerDrawLayers.TorsoGroup;
    public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) => !drawInfo.headOnlyRender
        && drawInfo.shadow == 0f
        && drawInfo.drawPlayer.GetModPlayer<SakuraKiriPlayer>().ActiveAction is { DrawState: SakuraKiriDrawState.Reappear };

    protected override void Draw(ref PlayerDrawSet drawInfo) =>
        drawInfo.drawPlayer.GetModPlayer<SakuraKiriPlayer>().ActiveAction?.DrawHeldWeapon(ref drawInfo);
}
