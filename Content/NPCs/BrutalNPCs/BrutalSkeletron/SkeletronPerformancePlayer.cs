using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core;
using InnoVault.Cinematics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron
{
    /// <summary>死亡演出玩家侧：本地启停运镜</summary>
    internal class SkeletronPerformancePlayer : ModPlayer
    {
        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            SkeletronHeadAI headAI = FindPerformanceHead(out NPC head);

            bool playing = CutsceneDirector.CurrentClip is SkeletronDeathCutscene;
            if (headAI != null && head != null) {
                if (!playing) {
                    CutsceneDirector.Play<SkeletronDeathCutscene, NPC>(head, restartSameClip: false);
                }
            }
            else if (playing) {
                CutsceneDirector.Stop();
            }
        }

        /// <summary>死亡演出中的头部，无则 null</summary>
        private static SkeletronHeadAI FindPerformanceHead(out NPC head) {
            head = null;
            int h = SkeletronHeadAI.ActivePerformanceHead;
            if (h < 0 || h >= Main.maxNPCs) {
                SkeletronHeadAI.ActivePerformanceHead = -1;
                return null;
            }
            NPC npc = Main.npc[h];
            if (!npc.active || npc.type != NPCID.SkeletronHead) {
                SkeletronHeadAI.ActivePerformanceHead = -1;
                return null;
            }
            //槽位复用等异常取不到覆写时按演出结束处理（精确索引缺键会抛出）
            if (!npc.TryGetOverride(out SkeletronHeadAI ai) || npc.ai[SkeletronAiSlots.HeadPhase] != SkeletronPhase.DeathShow) {
                SkeletronHeadAI.ActivePerformanceHead = -1;
                return null;
            }
            head = npc;
            return ai;
        }
    }
}
