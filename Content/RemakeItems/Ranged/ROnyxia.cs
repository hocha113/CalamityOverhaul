using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class ROnyxia : CWRItemOverride
    {
        public override void SetDefaults(Item item) => item.SetCartridgeGun<OnyxiaHeld>(280);
        public override bool? On_CanConsumeAmmo(Item weapon, Item ammo, Player player) => Main.rand.NextFloat() > 0.15f;
    }
}
