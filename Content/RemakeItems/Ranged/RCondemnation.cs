using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RCondemnation : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Condemnation>();
        public override int ProtogenesisID => ModContent.ItemType<CondemnationEcType>();
        public override string TargetToolTipItemName => "CondemnationEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<CondemnationHeldProj>();
    }
}
