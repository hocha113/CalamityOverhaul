using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RTelluricGlare : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 180;
            item.SetHeldProj<TelluricGlareHeldProj>();
        }
    }
}
