using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RRubicoPrime : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<RubicoPrime>();
        public override int ProtogenesisID => ModContent.ItemType<RubicoPrimeEcType>();
        public override string TargetToolTipItemName => "RubicoPrimeEcType";
        public override void SetDefaults(Item item) {
            item.useTime = 20;
            item.SetCartridgeGun<RubicoPrimeHeldProj>(80);
        }
        
    }
}
