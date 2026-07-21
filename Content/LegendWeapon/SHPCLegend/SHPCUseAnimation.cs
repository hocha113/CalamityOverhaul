using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules;
using InnoVault.GameSystem;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend
{
    /// <summary>左键持握动画，右键由 <see cref="Cyberspaces.SHPCChargeHeldProj"/> 接管，激光无后坐</summary>
    internal class SHPCUseAnimation : AimedHoldAnimation
    {
        public override int TargetID => SHPCOverride.ID;
        /// <summary>持握原点偏移，随缩放</summary>
        public override Vector2 HoldOrigin => new Vector2(-56, 10) * SHPCOverride.ItemScale;
        /// <summary>后坐力最大回退，随缩放</summary>
        public override float RecoilStrength => 8f * SHPCOverride.ItemScale;
        /// <summary>右键不走本动画</summary>
        public override bool ShouldAnimate(Item item, Player player) => player.altFunctionUse != 2;
        /// <summary>激光模式无后坐</summary>
        public override bool RecoilActive(Item item, Player player) => !SHPCModificationSystem.Resolve(player).LaserMode;
    }
}
