using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RAbsoluteZero : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.UseSound = null;
            item.SetKnifeHeld<AbsoluteZeroHeld>();
        }
    }
}
