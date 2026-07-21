using InnoVault.GameSystem;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Graphics;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFlashSteps
{
    /// <summary>冲刺期隐藏本地玩家绘制,见 OniFlashStep.HidePlayerDuringDash</summary>
    internal class OniFlashStepHideOverride : PlayerOverride
    {
        public override bool PreDrawPlayers(ref Camera camera, ref IEnumerable<Player> players) {
            //仅本地玩家在冲刺帧内被隐藏；其它玩家由各自客户端各自决定

            if (Player.whoAmI != Main.myPlayer) {
                return true;
            }
            if (!OniFlashStep.LocalPlayerHidden) {
                return true;
            }
            players = players.Where(p => p.whoAmI != Player.whoAmI);
            return true;
        }
    }
}
