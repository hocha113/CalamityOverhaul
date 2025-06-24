using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RSubmarineShocker : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<SubmarineShocker>();
        internal static bool canShoot;
        public override void SetDefaults(Item item) {
            item.damage = 80;
            item.SetKnifeHeld<SubmarineShockerHeld>();
            item.shootSpeed = 8;
        }
    }
}
