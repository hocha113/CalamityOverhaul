using InnoVault.GameSystem;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Graphics;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Restart
{
    /// <summary>重启奇点段隐藏本地玩家绘制</summary>
    internal class CyberRestartHideOverride : PlayerOverride
    {
        public override bool PreDrawPlayers(ref Camera camera, ref IEnumerable<Player> players) {
            if (Player.whoAmI != Main.myPlayer) return true;
            if (!CyberRestart.IsLocalPlayerHidden) return true;
            players = players.Where(p => p.whoAmI != Player.whoAmI);
            return true;
        }
    }
}
