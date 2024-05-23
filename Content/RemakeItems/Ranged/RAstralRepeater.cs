using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RAstralRepeater : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<AstralBow>();
        public override int ProtogenesisID => ModContent.ItemType<AstralRepeaterEcType>();
        public override string TargetToolTipItemName => "AstralRepeaterEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<AstralRepeaterHeldProj>();
    }
}
