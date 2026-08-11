using CalamityOverhaul.Content.Cyberwares;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.PvP.Protocols
{
    /// <summary>
    /// 义体离线的收益抑制旁路。<c>CyberwarePlayer</c> 没有"临时禁用"的公开入口
    /// （公开面只有装配查询与授权装卸），本文件用 <see cref="MonoModHooks"/>
    /// 把它的两条效果 Update 通道（PostUpdate 的 UpdateEquipped 循环、
    /// PostUpdateEquips 的 PostUpdateEquipped 循环）接到旁路上：<br/>
    /// 离线在册 → 跳过原方法，只放行防火墙类义体（豁免名单在
    /// <see cref="CyberwareOffline.IsFirewallExempt"/>）；未在册 → 原样放行。<br/>
    /// <b>为什么敢整段跳过</b>：ProfileInitialized == true 时原方法体只剩义体循环
    /// （联机档案重试只在未初始化分支跑），抑制判定把未初始化档案排除在外。<br/>
    /// <b>为什么只压本机</b>：义体收益的真值端是拥有者客户端（本机结算契约），
    /// 帐本也只在防守方本机非空——远端与服务端的判定天然为 false，原样放行。<br/>
    /// 收尾者如愿意在 CyberwarePlayer 循环里内联一行抑制标记检查，可删本文件换一行 if
    /// </summary>
    internal sealed class CyberwareOfflineHook : ModSystem
    {
        private delegate void OrigCyberUpdate(CyberwarePlayer self);
        private delegate void CyberUpdateDetour(OrigCyberUpdate orig,
            CyberwarePlayer self);

        public override void Load() {
            //MonoModHooks 随模组卸载自动摘钩，无需手动移除
            MethodInfo postUpdate = typeof(CyberwarePlayer)
                .GetMethod(nameof(CyberwarePlayer.PostUpdate));
            MethodInfo postUpdateEquips = typeof(CyberwarePlayer)
                .GetMethod(nameof(CyberwarePlayer.PostUpdateEquips));
            if (postUpdate != null) {
                MonoModHooks.Add(postUpdate,
                    new CyberUpdateDetour(PostUpdateDetour));
            }
            if (postUpdateEquips != null) {
                MonoModHooks.Add(postUpdateEquips,
                    new CyberUpdateDetour(PostUpdateEquipsDetour));
            }
        }

        private static void PostUpdateDetour(OrigCyberUpdate orig,
            CyberwarePlayer self) {
            if (!Suppressed(self)) {
                orig(self);
                return;
            }
            RunExemptLoop(self, postEquips: false);
        }

        private static void PostUpdateEquipsDetour(OrigCyberUpdate orig,
            CyberwarePlayer self) {
            if (!Suppressed(self)) {
                orig(self);
                return;
            }
            RunExemptLoop(self, postEquips: true);
        }

        /// <summary>本实例是否处于离线抑制：本机玩家 + 档案已初始化 + 效果在册</summary>
        private static bool Suppressed(CyberwarePlayer cyberware) {
            Player player = cyberware?.Player;
            if (player == null || Main.dedServ || player.whoAmI != Main.myPlayer
                || !cyberware.ProfileInitialized) {
                return false;
            }
            return PvPDefenderLocal.HasEffect<CyberwareOffline>();
        }

        /// <summary>抑制期的替身循环：只驱动防火墙类义体（豁免），其余全部断电</summary>
        private static void RunExemptLoop(CyberwarePlayer cyberware, bool postEquips) {
            Item[] equipped = cyberware.EquippedCyberwares;
            if (equipped == null) {
                return;
            }
            for (int i = 0; i < equipped.Length; i++) {
                if (equipped[i]?.ModItem is not BaseCyberware ware
                    || !CyberwareOffline.IsFirewallExempt(ware)) {
                    continue;
                }
                if (postEquips) {
                    ware.PostUpdateEquipped(cyberware.Player);
                }
                else {
                    ware.UpdateEquipped(cyberware.Player);
                }
            }
        }
    }
}
