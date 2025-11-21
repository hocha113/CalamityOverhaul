using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RShroomer : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 135;
            item.SetCartridgeGun<ShroomerHeldProj>(12);
            item.CWR().Scope = true;
        }
    }
}
