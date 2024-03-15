using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{ 
    internal class RFungicide : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Fungicide>();
        public override int ProtogenesisID => ModContent.ItemType<FungicideEcType>();
        public override string TargetToolTipItemName => "FungicideEcType";
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<FungicideHeldProj>(16);
            item.damage = 22;
        }

    }
}
