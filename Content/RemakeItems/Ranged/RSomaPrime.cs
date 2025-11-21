using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RSomaPrime : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 300;
            item.SetCartridgeGun<SomaPrimeHeldProj>(600);
        }
        public override bool? On_CanConsumeAmmo(Item weapon, Item ammo, Player player) => Main.rand.NextFloat() > 0.1f;
    }
}
