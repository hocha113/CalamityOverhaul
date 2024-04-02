using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 鳄鱼机关枪
    /// </summary>
    internal class RGatligator : BaseRItem
    {
        public override int TargetID => ItemID.Gatligator;
        public override bool IsVanilla => true;
        public override string TargetToolTipItemName => "Wap_Gatligator_Text";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<GatligatorHeldProj>(185);
    }
}
