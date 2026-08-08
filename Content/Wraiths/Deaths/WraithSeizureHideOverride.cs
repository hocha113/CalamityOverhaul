using InnoVault.GameSystem;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Graphics;

namespace CalamityOverhaul.Content.Wraiths.Deaths
{
    /// <summary>夺身演出要求隐藏本体时不画这名玩家——被帘罩住或被吞走的人不该还站在原地。</summary>
    internal class WraithSeizureHideOverride : PlayerOverride
    {
        public override bool PreDrawPlayers(ref Camera camera, ref IEnumerable<Player> players) {
            if (!Player.TryGetModPlayer(out WraithRevivalDeathPlayer seizure)
                || !seizure.HidesPlayerNow) {
                return true;
            }
            int hidden = Player.whoAmI;
            players = players.Where(p => p.whoAmI != hidden);
            return true;
        }
    }
}
