using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RSubmarineShocker : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<SubmarineShocker>();
        public override int ProtogenesisID => ModContent.ItemType<SubmarineShockerEcType>();
        public override string TargetToolTipItemName => "SubmarineShockerEcType";
        public override void SetDefaults(Item item) {
            item.SetKnifeHeld<SubmarineShockerHeld>();
            item.shootSpeed = 8;
        }
    }
}
