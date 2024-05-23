using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RBladedgeGreatbow : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<BladedgeRailbow>();
        public override int ProtogenesisID => ModContent.ItemType<BladedgeGreatbowEcType>();
        public override string TargetToolTipItemName => "BladedgeGreatbowEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<BladedgeGreatbowHeldProj>();
    }
}
