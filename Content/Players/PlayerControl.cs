using CalamityOverhaul.Content.Players.Core;
using Terraria;

namespace CalamityOverhaul.Content.Players
{
    internal class PlayerControl : PlayerSet
    {
        public override bool? CanSwitchWeapon(Player player) {
            if (player.CWR().DontSwitchWeaponTime > 0) {
                return false;
            }           
            return null;
        }
    }
}
