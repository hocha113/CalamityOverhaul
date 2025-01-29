using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RSubmarineShocker : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<SubmarineShocker>();
        internal static bool canShoot;
        public override void SetDefaults(Item item) {
            item.SetKnifeHeld<SubmarineShockerHeld>();
            item.shootSpeed = 8;
        }
    }
}
