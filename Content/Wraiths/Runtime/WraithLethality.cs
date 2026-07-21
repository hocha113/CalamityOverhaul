using CalamityOverhaul.Content.Wraiths.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.Localization;

namespace CalamityOverhaul.Content.Wraiths.Runtime
{
    /// <summary>
    /// 规则死亡。死因优先专属文案，缺省 <see cref="WraithDefinition.DeathReason"/>。<br/>
    /// omen 仅权威受理；KillMe 须受害者本端，远端经 SendRuleKill
    /// </summary>
    public static class WraithLethality
    {
        /// <summary>规则死亡；权威对远端转发，受害者本端直办</summary>
        public static void Kill(Player player, WraithDefinition definition, LocalizedText reason = null) {
            if (player == null || !player.active || player.dead || definition == null) {
                return;
            }
            if (!Main.dedServ && player.whoAmI == Main.myPlayer) {
                KillLocal(player, definition, reason);
                return;
            }
            if (VaultUtils.isServer) {
                WraithNet.SendRuleKill(player.whoAmI, definition, reason?.Key);
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
            PlayerDeathReason deathReason = PlayerDeathReason.ByCustomReason(reason.ToNetworkText(player.name));
            player.KillMe(deathReason, System.Math.Max(player.statLifeMax2 * 3, 1000), 0);
        }
    }
}
