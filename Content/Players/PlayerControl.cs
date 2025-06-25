using InnoVault.GameSystem;
using Terraria;

namespace CalamityOverhaul.Content.Players
{
    internal class PlayerControl : PlayerOverride
    {
        public override bool? CanSwitchWeapon() {
            if (Player.CWR().DontSwitchWeaponTime > 0) {
                return false;
            }
            return null;
        }
    }
}
