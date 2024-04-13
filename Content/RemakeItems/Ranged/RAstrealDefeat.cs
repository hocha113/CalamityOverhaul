using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RAstrealDefeat : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<AstrealDefeat>();
        public override int ProtogenesisID => ModContent.ItemType<AstrealDefeatEcType>();
        public override string TargetToolTipItemName => "AstrealDefeatEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<AstrealDefeatHeldProj>();
    }
}
