using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RFungicide : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<FungicideHeldProj>(16);
            item.damage = 22;
        }
    }
}
