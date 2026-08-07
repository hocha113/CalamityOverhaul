using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Runtime;
using Terraria;

namespace CalamityOverhaul.Content.Wraiths.Deaths
{
    /// <summary>
    /// 复苏满格夺身流程的静态门面：状态查询与权威启动入口。<br/>
    /// 具体演出由 <c>WraithRevivalDeathPlayer</c> 状态机承担。
    /// </summary>
    internal static class WraithRevivalDeath
    {
        /// <summary>玩家是否正被厉鬼夺身（演出进行中）。夺身期间一切役鬼能力停止结算。</summary>
        internal static bool IsSeized(Player player)
            => player != null && player.active
                && player.TryGetModPlayer(out WraithRevivalDeathPlayer seizure)
                && seizure.Active;

        /// <summary>权威端启动夺身：复苏满格后由 <see cref="WraithPlayer"/> 调用。</summary>
        internal static void StartSeizure(Player player, string key) {
            if (player == null || !player.active || player.dead
                || !WraithRegistry.TryGet(key, out WraithDefinition definition)) {
                return;
            }
            if (player.TryGetModPlayer(out WraithRevivalDeathPlayer seizure)
                && seizure.TryBeginAuthority(definition)) {
                return;
            }
            //状态机不可用时兜底为直接规则死亡，保证复苏满格必有代价
            WraithLethality.Kill(player, definition);
        }
    }
}
