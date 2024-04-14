using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RHoarfrostBow : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.HoarfrostBow>();
        public override int ProtogenesisID => ModContent.ItemType<HoarfrostBowEcType>();
        public override string TargetToolTipItemName => "HoarfrostBowEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<HoarfrostBowHeldProj>();
    }
}
