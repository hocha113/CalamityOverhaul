using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Magic
{
    internal class REidolicWail : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<EidolicWail>();
        public override int ProtogenesisID => ModContent.ItemType<EidolicWailEcType>();
        public override string TargetToolTipItemName => "EidolicWailEcType";
        public override void SetDefaults(Item item) {
            item.useTime = 95;
            item.damage = 285;
            item.mana = 52;
            item.SetHeldProj<EidolicWailHeldProj>();
        }
    }
}
