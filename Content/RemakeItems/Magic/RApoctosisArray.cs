using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Magic
{
    internal class RApoctosisArray : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<ApoctosisArray>();
        public override int ProtogenesisID => ModContent.ItemType<ApoctosisArrayEcType>();
        public override string TargetToolTipItemName => "ApoctosisArrayEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<ApoctosisArrayHeldProj>();
    }
}
