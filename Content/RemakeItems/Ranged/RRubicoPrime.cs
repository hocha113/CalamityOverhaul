using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RRubicoPrime : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 820;
            item.useTime = 20;
            item.SetCartridgeGun<RubicoPrimeHeld>(80);
        }

    }
}
