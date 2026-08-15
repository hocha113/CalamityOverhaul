using CalamityOverhaul.Content.Narrative.Common;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaThralls
{
    /// <summary>
    /// 伞奴转化的死亡观测口：HitEffect 全端触发（蠕虫已映射头节点、已去重）。
    /// 服务器没有领域状态，判定与演出全在客户端；生成只由领域主人本机受理，
    /// 其余端做同一份判定只为起本地化水演出——判定谓词是同一份真相，结果各端一致。
    /// <para/>
    /// 这一帧只认领不动手：真身可能还在播自己的死亡演出（灾厄 boss 常在 CheckDead 里留一条命），
    /// 化水与重组交给 <see cref="KikasaThrall.Watch"/> 等它离场，免得快照与真身同台
    /// </summary>
    internal sealed class KikasaThrallNPC : DeathTrackingNPC
    {
        public override void OnNPCDeath(NPC npc) {
            if (Main.dedServ || !KikasaThrall.IsEligibleCorpse(npc)) {
                return;
            }
            if (!KikasaThrall.TryFindClaimingOwner(npc, out Player owner)) {
                return;
            }
            bool boss = KikasaThrall.IsBossCorpse(npc);
            if (!KikasaThrall.ConvertGateOpen(owner.whoAmI, boss)) {
                return;
            }

            KikasaThrall.MarkConvertGate(owner.whoAmI);
            KikasaThrall.Watch(npc, owner.whoAmI, boss);
        }
    }
}
