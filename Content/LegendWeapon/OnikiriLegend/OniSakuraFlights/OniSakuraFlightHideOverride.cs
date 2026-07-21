using InnoVault.GameSystem;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Graphics;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniSakuraFlights
{
    /// <summary>樱流隐藏本地玩家绘制</summary>
    internal sealed class OniSakuraFlightHideOverride : PlayerOverride
    {
        public override bool PreDrawPlayers(ref Camera camera, ref IEnumerable<Player> players) {
            int playerIndex = Player.whoAmI;
            if (!OniSakuraFlight.IsPlayerHidden(playerIndex)) {
                return true;
            }

            players = players.Where(player => player.whoAmI != playerIndex);
            return true;
        }
    }
}
