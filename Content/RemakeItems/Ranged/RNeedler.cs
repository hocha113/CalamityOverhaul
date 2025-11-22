using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RNeedler : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 40;
            item.SetCartridgeGun<NeedlerHeld>(50);
        }
    }
}
