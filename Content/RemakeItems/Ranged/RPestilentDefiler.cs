using CalamityOverhaul.Content.Projectiles.Weapons.Ranged;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RPestilentDefiler : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 90;
            item.SetCartridgeGun<PestilentDefilerHeldProj>(65);
        }
    }
}
