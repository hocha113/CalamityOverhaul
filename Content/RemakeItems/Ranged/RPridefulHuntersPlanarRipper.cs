using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RPridefulHuntersPlanarRipper : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<PridefulHuntersPlanarRipper>();
        public override int ProtogenesisID => ModContent.ItemType<PridefulHuntersPlanarRipperEcType>();
        public override string TargetToolTipItemName => "PridefulHuntersPlanarRipperEcType";
        public override void SetDefaults(Item item) {
            item.damage = 33;
            item.SetCartridgeGun<PridefulHuntersPlanarRipperHeldProj>(280);
        }
    }
}
