using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RTelluricGlare : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.TelluricGlare>();
        public override int ProtogenesisID => ModContent.ItemType<TelluricGlareEcType>();
        public override string TargetToolTipItemName => "TelluricGlareEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<TelluricGlareHeldProj>();
    }
}
