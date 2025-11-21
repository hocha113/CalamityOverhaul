using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RShredder : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<ShredderHeldProj>(300);
            item.CWR().Scope = true;
        }
    }
}
