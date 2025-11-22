using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RPlagueTaintedSMG : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<PlagueTaintedSMGHeld>(90);
            item.damage = 65;
            item.UseSound = CWRSound.Gun_SMG_Shoot;
            item.CWR().Scope = true;
        }

        public override void ModifyWeaponCrit(Item item, Player player, ref float crit) => crit += 5;
    }
}
