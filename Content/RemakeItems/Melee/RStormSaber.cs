using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RStormSaber : CWRItemOverride
    {
        public override void SetDefaults(Item item) => item.SetKnifeHeld<StormSaberHeld>();
    }
}
