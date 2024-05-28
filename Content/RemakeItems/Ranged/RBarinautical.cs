using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RBarinautical : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Barinautical>();
        public override int ProtogenesisID => ModContent.ItemType<BarinauticalEcType>();
        public override string TargetToolTipItemName => "BarinauticalEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<BarinauticalHeldProj>();
    }
}
