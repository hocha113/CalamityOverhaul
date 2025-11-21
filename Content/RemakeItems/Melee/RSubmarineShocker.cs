using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RSubmarineShocker : CWRItemOverride
    {
        internal static bool canShoot;
        public override void SetDefaults(Item item) {
            item.damage = 80;
            item.SetKnifeHeld<SubmarineShockerHeld>();
            item.shootSpeed = 8;
        }
    }
}
