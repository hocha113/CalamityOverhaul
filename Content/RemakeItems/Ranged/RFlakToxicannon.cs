using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RFlakToxicannon : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 62;
            item.SetCartridgeGun<FlakToxicannonHeldProj>(80);
            item.CWR().Scope = true;
        }
    }
}
