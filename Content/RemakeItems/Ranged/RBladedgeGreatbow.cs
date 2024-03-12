using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RBladedgeGreatbow : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<BladedgeGreatbow>();
        public override int ProtogenesisID => ModContent.ItemType<BladedgeGreatbowEcType>();
        public override string TargetToolTipItemName => "BladedgeGreatbowEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<BladedgeGreatbowHeldProj>();
    }
}
