using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Magic
{
    internal class RLazhar : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Lazhar>();
        public override int ProtogenesisID => ModContent.ItemType<LazharEcType>();
        public override string TargetToolTipItemName => "LazharEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<LazharHeldProj>();
    }
}
