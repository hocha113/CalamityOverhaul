using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjs;
using Terraria;

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
