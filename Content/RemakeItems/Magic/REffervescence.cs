using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Magic
{
    internal class REffervescence : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Effervescence>();
        public override int ProtogenesisID => ModContent.ItemType<EffervescenceEcType>();
        public override string TargetToolTipItemName => "EffervescenceEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<EffervescenceHeldProj>();
    }
}
