using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ID;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RRevolver : BaseRItem
    {
        public override int TargetID => ItemID.Revolver;
        public override bool IsVanilla => true;
        public override string TargetToolTipItemName => "Wap_Revolver_Text";
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<RevolverHeldProj>(6);
            item.CWR().CartridgeEnum = CartridgeUIEnum.Magazines;
        }
    }
}
