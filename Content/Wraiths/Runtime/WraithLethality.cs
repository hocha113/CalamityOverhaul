using CalamityOverhaul.Content.Wraiths.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.Localization;

namespace CalamityOverhaul.Content.Wraiths.Runtime
{
    /// <summary>
    /// 规则死亡助手（鬼律第十条）。<br/>
    /// 可归因：死亡讯息优先用规则专属文案（{0}=玩家名，各鬼经
    /// <c>WraithDefinition.LoadExtraLocalization</c> 装载），缺省回落定义的
    /// <see cref="WraithDefinition.DeathReason"/> 兜底。<br/>
    /// 有预警：预警拍（omen）为服务器权威——<see cref="StartOmen"/>/<see cref="CancelOmen"/>
    /// 只在权威端受理，倒计时与死亡判定在权威侧推进（见 <c>WraithPlayer</c>），
    /// 受害者本端经 <c>WraithNet.OmenStart/OmenCancel</c> 收演出镜像。<br/>
    /// KillMe 必须在受害者本端执行：权威端对远端玩家判死经 <c>WraithNet.SendRuleKill</c> 转发
    /// </summary>
    public static class WraithLethality
    {
        /// <summary>
        /// 对玩家执行规则死亡；reason 为空取定义兜底死因。
        /// 权威端对远端玩家转发，受害者本端直接执行，其余调用被忽略
        /// </summary>
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

        /// <summary>
        /// 开始一段死亡预警（鬼律第十条"有预警拍"）；仅权威端受理。
        /// 倒计时在权威侧推进，到期以 reason（缺省定义兜底）执行规则死亡；
        /// 玩家挣脱规则时权威端调用 <see cref="CancelOmen"/> 撤拍
        /// </summary>
        public static void StartOmen(Player victim, WraithDefinition definition, int ticks, LocalizedText reason = null) {
            if (VaultUtils.isClient || victim == null || !victim.active || victim.dead
                || definition == null || ticks <= 0) {
                return;
            }
            //被更紧迫的现拍压住时不重发镜像,两侧节拍保持一致
            if (victim.GetModPlayer<WraithPlayer>().BeginOmenAuthority(definition, ticks, reason)
                && VaultUtils.isServer) {
                WraithNet.SendOmenStart(victim.whoAmI, definition, ticks);
            }
        }

        /// <summary>取消预警（玩家挣脱了规则）；仅权威端受理，死亡与离场由权威侧自动撤拍</summary>
        public static void CancelOmen(Player victim) {
            if (VaultUtils.isClient || victim == null || !victim.active) {
                return;
            }
            victim.GetModPlayer<WraithPlayer>().ClearOmenAuthority();
            if (VaultUtils.isServer) {
                WraithNet.SendOmenCancel(victim.whoAmI);
            }
        }

        /// <summary>按本地化键还原专属死因；键空/查无回落定义兜底（联机 RuleKill 转发用）</summary>
        internal static LocalizedText ResolveReason(WraithDefinition definition, string reasonKey) {
            if (!string.IsNullOrEmpty(reasonKey) && Language.Exists(reasonKey)) {
                return Language.GetText(reasonKey);
            }
            return definition?.DeathReason;
        }

        /// <summary>受害者本端落刀：足量真伤跳过一切减免，讯息点明所犯之规</summary>
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
