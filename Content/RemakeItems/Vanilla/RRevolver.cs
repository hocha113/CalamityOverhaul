using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RRevolver : ItemOverride
    {
        public override int TargetID => ItemID.Revolver;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<RevolverHeldProj>(6);
            item.CWR().CartridgeType = CartridgeUIEnum.Magazines;
        }
    }
}
