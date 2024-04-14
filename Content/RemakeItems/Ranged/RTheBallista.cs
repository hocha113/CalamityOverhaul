using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RTheBallista : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.TheBallista>();
        public override int ProtogenesisID => ModContent.ItemType<TheBallistaEcType>();
        public override string TargetToolTipItemName => "TheBallistaEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<TheBallistaHeldProj>();
    }
}
