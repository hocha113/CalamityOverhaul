using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RStormDragoon : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 68;
            item.SetCartridgeGun<StormDragoonHeld>(225);
        }
        public override bool? On_CanConsumeAmmo(Item weapon, Item ammo, Player player) => Main.rand.NextFloat() > 0.2f;
    }
}
