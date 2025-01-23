using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RContinentalGreatbow : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<ContinentalGreatbow>();
        public override int ProtogenesisID => ModContent.ItemType<ContinentalGreatbowEcType>();
        public override string TargetToolTipItemName => "ContinentalGreatbowEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<ContinentalGreatbowHeldProj>();
    }
}
