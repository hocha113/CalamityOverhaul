using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RShredder : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Shredder>();
        public override int ProtogenesisID => ModContent.ItemType<ShredderEcType>();
        public override string TargetToolTipItemName => "ShredderEcType";
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<ShredderHeldProj>(300);
            item.CWR().Scope = true;
        }
    }
}
