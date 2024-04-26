using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RNorfleet : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Norfleet>();
        public override int ProtogenesisID => ModContent.ItemType<NorfleetEcType>();
        public override string TargetToolTipItemName => "NorfleetEcType";
        public override void SetDefaults(Item item) {
            item.damage = 330;
            item.SetCartridgeGun<NorfleetHeldProj>(8);
        }
    }
}
