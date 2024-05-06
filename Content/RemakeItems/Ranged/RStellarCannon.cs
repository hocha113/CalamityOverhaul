using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RStellarCannon : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<StellarCannon>();
        public override int ProtogenesisID => ModContent.ItemType<StellarCannonEcType>();
        public override string TargetToolTipItemName => "StellarCannonEcType";
        public override void SetDefaults(Item item) {
            item.damage = 115;
            item.useAmmo = AmmoID.FallenStar;
            item.SetCartridgeGun<StellarCannonHeldProj>(30);
        }
    }
}
