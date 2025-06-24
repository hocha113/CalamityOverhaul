using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RRevolver : CWRItemOverride
    {
        public override int TargetID => ItemID.Revolver;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<RevolverHeldProj>(6);
            item.CWR().CartridgeType = CartridgeUIEnum.Magazines;
        }
    }
}
