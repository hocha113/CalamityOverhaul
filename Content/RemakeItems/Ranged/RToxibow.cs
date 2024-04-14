using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RToxibow : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Toxibow>();
        public override int ProtogenesisID => ModContent.ItemType<ToxibowEcType>();
        public override string TargetToolTipItemName => "ToxibowEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<ToxibowHeldProj>();
    }
}
