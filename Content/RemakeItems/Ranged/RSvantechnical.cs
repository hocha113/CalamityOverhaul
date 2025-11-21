using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RSvantechnical : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<SvantechnicalHeldProj>(880);
            item.CWR().Scope = true;
        }

        public override bool? On_CanConsumeAmmo(Item weapon, Item ammo, Player player) => true;
    }
}
