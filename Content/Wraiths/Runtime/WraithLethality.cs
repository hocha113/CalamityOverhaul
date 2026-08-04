using CalamityOverhaul.Content.Players;
using CalamityOverhaul.Content.Wraiths.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.Localization;

namespace CalamityOverhaul.Content.Wraiths.Runtime
{
    /// <summary>复苏满格等役鬼规则死亡的统一入口。</summary>
    public static class WraithLethality
    {
        public static void Kill(Player player, WraithDefinition definition,
            LocalizedText reason = null) {
            if (player == null || !player.active || player.dead || definition == null) {
                return;
            }
            if (!Main.dedServ && player.whoAmI == Main.myPlayer) {
                KillLocal(player, definition, reason);
                return;
            }
            if (!VaultUtils.isServer) {
                return;
            }

            reason ??= definition.DeathReason;
            int lethalDamage = System.Math.Max(player.statLifeMax2 * 3, 1000);
            PlayerDeathReason deathReason = PlayerDeathReason.ByCustomReason(
                reason.ToNetworkText(player.name));
            if (player.TryGetOverride(out PlayerDeath playerDeath)) {
                playerDeath.PrepareRuleDeath();
            }
            player.KillMe(deathReason, lethalDamage, 0);
            if (!player.dead) {
                return;
            }
            WraithNet.SendRuleKill(player.whoAmI, definition, reason.Key);
            NetMessage.SendPlayerDeath(player.whoAmI, deathReason, lethalDamage, 0, false);
        }

        private static void KillLocal(Player player, WraithDefinition definition,
            LocalizedText reason) {
            reason ??= definition.DeathReason;
            if (player.TryGetOverride(out PlayerDeath playerDeath)) {
                playerDeath.PrepareRuleDeath();
            }
            PlayerDeathReason deathReason = PlayerDeathReason.ByCustomReason(
                reason.ToNetworkText(player.name));
            player.KillMe(deathReason,
                System.Math.Max(player.statLifeMax2 * 3, 1000), 0);
        }
    }
}
