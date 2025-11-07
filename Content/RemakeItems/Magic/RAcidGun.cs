using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Magic
{
    internal class RAcidGun : CWRItemOverride
    {
        public override string SafeName => GetCalItem("AcidGun");
        public override void SetDefaults(Item item) {
            item.damage = 16;
            item.SetHeldProj<AcidGunHeldProj>();
        }
    }
}
