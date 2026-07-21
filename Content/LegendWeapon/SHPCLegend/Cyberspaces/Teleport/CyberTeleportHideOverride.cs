using InnoVault.GameSystem;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Graphics;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Teleport
{
    /// <summary>瞬移演出期隐藏本地玩家绘制</summary>
    internal class CyberTeleportHideOverride : PlayerOverride
    {
        public override bool PreDrawPlayers(ref Camera camera, ref IEnumerable<Player> players) {
            //仅本地玩家演出期隐藏
            if (Player.whoAmI != Main.myPlayer) return true;
            if (!CyberTeleport.IsLocalPlayerHidden) return true;
            players = players.Where(p => p.whoAmI != Player.whoAmI);
            return true;
        }
    }
}
