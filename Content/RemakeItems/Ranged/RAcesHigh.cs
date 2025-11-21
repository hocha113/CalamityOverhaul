using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RAcesHigh : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 375;
            item.SetCartridgeGun<AcesHighHeldProj>(90);
        }
    }
}
