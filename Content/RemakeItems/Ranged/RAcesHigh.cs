using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RAcesHigh : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<AcesHigh>();
        public override void SetDefaults(Item item) {
            item.damage = 375;
            item.SetCartridgeGun<AcesHighHeldProj>(90);
        }
    }
}
