using CalamityOverhaul.Content.Wraiths.Core;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Wraiths.Runtime
{
    /// <summary>
    /// 规则死亡助手（鬼律第十条"可归因"）：死亡讯息取定义的 <see cref="WraithDefinition.DeathReason"/>，
    /// {0}=玩家名。KillMe 必须在受害者本端执行：权威端的怪谈逻辑对远端玩家判死时经
    /// <c>WraithNet.SendRuleKill</c> 转发。预警拍（"有预警"条款）走 <c>WraithPlayer.StartOmen</c>
    /// </summary>
    public static class WraithLethality
    {
        /// <summary>对玩家执行规则死亡；本地玩家直接执行，远端玩家仅服务器可转发，其余调用被忽略</summary>
        public static void Kill(Player player, WraithDefinition definition) {
            if (player == null || !player.active || player.dead || definition == null) {
                return;
            }
            if (!Main.dedServ && player.whoAmI == Main.myPlayer) {
                KillLocal(player, definition);
                return;
            }
            if (VaultUtils.isServer) {
                WraithNet.SendRuleKill(player.whoAmI, definition);
            }
        }

        /// <summary>受害者本端落刀：足量真伤跳过一切减免，讯息点明所犯之规</summary>
        internal static void KillLocal(Player player, WraithDefinition definition) {
            if (player.dead) {
                return;
            }
            PlayerDeathReason reason = PlayerDeathReason.ByCustomReason(definition.DeathReason.ToNetworkText(player.name));
            player.KillMe(reason, System.Math.Max(player.statLifeMax2 * 3, 1000), 0);
        }
    }
}
