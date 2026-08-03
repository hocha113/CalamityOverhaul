using CalamityOverhaul.Content.Players;
using CalamityOverhaul.Content.Wraiths.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.Localization;

namespace CalamityOverhaul.Content.Wraiths.Runtime
{
    /// <summary>
    /// 规则死亡。死因优先专属文案，缺省 <see cref="WraithDefinition.DeathReason"/>。<br/>
    /// omen 仅权威受理；服务器先落真实死亡，再用 RuleKill+PlayerDeathV2 镜像客户端。
    /// </summary>
    public static class WraithLethality
    {
        /// <summary>规则死亡；单人直办，多人由服务器落死并同步。</summary>
        public static void Kill(Player player, WraithDefinition definition, LocalizedText reason = null) {
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
            PlayerDeathReason deathReason = PlayerDeathReason.ByCustomReason(reason.ToNetworkText(player.name));
            if (player.TryGetOverride(out PlayerDeath playerDeath)) {
                playerDeath.PrepareRuleDeath();
            }
            player.KillMe(deathReason, lethalDamage, 0);
            if (player.dead) {
                //先给受害者死亡通行证，再广播标准死亡包；客户端不再回发自杀请求。
                WraithNet.SendRuleKill(player.whoAmI, definition, reason.Key);
                NetMessage.SendPlayerDeath(player.whoAmI, deathReason, lethalDamage, 0, false);
            }
        }

        /// <summary>起预警拍，仅权威；到期落刀，挣脱则 CancelOmen</summary>
        public static void StartOmen(Player victim, WraithDefinition definition, int ticks, LocalizedText reason = null) {
            if (VaultUtils.isClient || victim == null || !victim.active || victim.dead
                || definition == null || ticks <= 0) {
                return;
            }
            //更紧迫现拍压住时不重发镜像
            if (victim.GetModPlayer<WraithPlayer>().BeginOmenAuthority(definition, ticks, reason)
                && VaultUtils.isServer) {
                WraithNet.SendOmenStart(victim.whoAmI, definition, ticks);
            }
        }

        /// <summary>撤预警，仅权威</summary>
        public static void CancelOmen(Player victim) {
            if (VaultUtils.isClient || victim == null || !victim.active) {
                return;
            }
            victim.GetModPlayer<WraithPlayer>().ClearOmenAuthority();
            if (VaultUtils.isServer) {
                WraithNet.SendOmenCancel(victim.whoAmI);
            }
        }

        /// <summary>按键还原死因，空/查无回落定义兜底</summary>
        internal static LocalizedText ResolveReason(WraithDefinition definition, string reasonKey) {
            if (!string.IsNullOrEmpty(reasonKey) && Language.Exists(reasonKey)) {
                return Language.GetText(reasonKey);
            }
            return definition?.DeathReason;
        }

        /// <summary>受害者本端落刀，足量真伤跳过减免</summary>
        internal static void KillLocal(Player player, WraithDefinition definition, LocalizedText reason = null) {
            if (player.dead) {
                return;
            }
            reason ??= definition.DeathReason;
            if (player.TryGetOverride(out PlayerDeath playerDeath)) {
                playerDeath.PrepareRuleDeath();
            }
            PlayerDeathReason deathReason = PlayerDeathReason.ByCustomReason(reason.ToNetworkText(player.name));
            player.KillMe(deathReason, System.Math.Max(player.statLifeMax2 * 3, 1000), 0);
        }
    }
}
