using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RDrataliornus : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 136;
            item.SetHeldProj<DrataliornusHeldProj>();
        }
    }
}
