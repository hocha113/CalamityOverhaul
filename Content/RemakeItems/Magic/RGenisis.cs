using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Magic
{
    internal class RGenisis : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Genisis>();
        public override int ProtogenesisID => ModContent.ItemType<GenisisEcType>();
        public override string TargetToolTipItemName => "GenisisEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<GenisisHeldProj>();
    }
}
