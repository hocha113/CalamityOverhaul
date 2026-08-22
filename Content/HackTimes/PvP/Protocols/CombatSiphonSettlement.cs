using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.PvP.Protocols
{
    /// <summary>
    /// 战术榨取的受击结算钩（独立文件，不动既有分派点）。<br/>
    /// <b>钩子端别的源码核对</b>：攻击方客户端算完 PvP 伤害发 msg 117，服务端收包时
    /// 先在自己的防守方副本上跑 <c>Main.player[n].Hurt(HurtInfo, quiet: true)</c>
    /// 再转播（MessageBuffer.cs case 117 ≈L3877），而 <c>Player.Hurt(HurtInfo)</c>
    /// 无条件调 <c>PlayerLoader.OnHurt</c>（Player.cs ≈L34654，无 netMode/quiet 闸）
    /// ：所以本钩子在服务端确实触发，且 <c>info.PvP</c> 与
    /// <c>info.DamageSource.SourcePlayerIndex</c> 都随包到达。<br/>
    /// 转播同时到达每个客户端（发起的攻击方除外），OnHurt 在各端重放：
    /// 服务端分支记账（RAM 是服务端资源），客户端分支只发命中表现。<br/>
    /// 熔断标记/弹道倒戈的本机自伤走 ByCustomReason（SourcePlayerIndex = -1），
    /// 在入口就被滤掉，不会左手倒右手
    /// </summary>
    internal class CombatSiphonSettlement : ModPlayer
    {
        //服务端单线程扫账用的草稿表，不承载状态
        private static readonly List<PlayerHackGrant> scratch = [];

        public override void OnHurt(Player.HurtInfo info) {
            if (!info.PvP || info.Damage <= 0 || info.DamageSource == null) return;
            int attackerIndex = info.DamageSource.SourcePlayerIndex;
            if (attackerIndex < 0 || attackerIndex >= Main.maxPlayers
                || attackerIndex == Player.whoAmI) {
                return;
            }
            Player attacker = Main.player[attackerIndex];
            if (attacker?.active != true) return;

            if (Main.netMode == NetmodeID.MultiplayerClient) {
                EmitHitPresentation(attackerIndex, attacker);
                return;
            }
            SettleOnAuthority(attackerIndex, attacker, info.Damage);
        }

        /// <summary>权威端记账：授予账里找 (本防守方, 该攻击方) 的战术榨取额度</summary>
        private void SettleOnAuthority(int attackerIndex, Player attacker, int damage) {
            PlayerHackAuthority.CollectConfirmed(Player.whoAmI, scratch);
            for (int i = 0; i < scratch.Count; i++) {
                PlayerHackGrant grant = scratch[i];
                if (grant.Hack is not CombatSiphon
                    || grant.CasterIndex != attackerIndex) {
                    continue;
                }
                CombatSiphon.SettleAuthority(grant, attacker, damage);
                //同槽协议同目标不叠（上传队列既有去重），首条即全部
                break;
            }
        }

        /// <summary>客户端命中表现：镜像里有这对 (防守方, 攻击方) 的榨取条目才画</summary>
        private void EmitHitPresentation(int attackerIndex, Player attacker) {
            IReadOnlyList<PlayerHackMirror.MirrorEffect> effects = PlayerHackMirror.All;
            for (int i = 0; i < effects.Count; i++) {
                PlayerHackMirror.MirrorEffect fx = effects[i];
                if (fx.DefenderIndex != Player.whoAmI
                    || fx.CasterIndex != attackerIndex
                    || fx.RemovedReason != null
                    || QuickHackDef.GetByIndex(fx.SlotIndex) is not CombatSiphon) {
                    continue;
                }
                CombatSiphon.EmitDrain(Player, attacker);
                return;
            }
        }
    }
}
