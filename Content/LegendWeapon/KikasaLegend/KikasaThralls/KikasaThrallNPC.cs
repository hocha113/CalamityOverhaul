using CalamityOverhaul.Content.Narrative.Common;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaThralls
{
    /// <summary>
    /// 伞奴转化的死亡观测口：HitEffect 全端触发（蠕虫已映射头节点、已去重）。
    /// 服务器没有领域状态，判定与演出全在客户端；生成只由领域主人本机受理，
    /// 其余端做同一份判定只为起本地化水演出——判定谓词是同一份真相，结果各端一致。
    /// </summary>
    internal sealed class KikasaThrallNPC : DeathTrackingNPC
    {
        public override void OnNPCDeath(NPC npc) {
            if (Main.dedServ || !KikasaThrall.IsEligibleCorpse(npc)) {
                return;
            }
            if (!KikasaThrall.TryFindClaimingOwner(npc, out Player owner)
                || !KikasaThrall.ConvertGateOpen(owner.whoAmI)) {
                return;
            }
            //探不到可站立地面就不转化（雨把它冲走了），演出也不起
            if (!KikasaThrall.TryPickReformPoint(npc, out Vector2 reformFeet)) {
                return;
            }

            KikasaThrall.MarkConvertGate(owner.whoAmI);
            KikasaThrallMeltFX.Start(npc, owner.whoAmI);
            if (owner.whoAmI == Main.myPlayer) {
                KikasaThrall.SpawnThrall(owner, npc, reformFeet);
            }
        }
    }
}
