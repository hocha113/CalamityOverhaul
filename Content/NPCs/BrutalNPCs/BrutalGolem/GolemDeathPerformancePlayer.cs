using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.Common;
using InnoVault.Cinematics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem
{
    /// <summary>死亡演出玩家侧：本地启停石像崩解运镜</summary>
    internal class GolemDeathPerformancePlayer : ModPlayer
    {
        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            GolemBodyAI bodyAI = FindPerformanceBody(out NPC body);
            bool playing = CutsceneDirector.CurrentClip is GolemDeathCutscene;

            if (bodyAI != null && body != null) {
                //restartSameClip:false，已播则复用；观众门槛：打过它（含拳/头）或人在附近
                if (!playing && BossDeathSpectator.ShouldWatch(Player, body)) {
                    CutsceneDirector.Play<GolemDeathCutscene, NPC>(body, restartSameClip: false);
                }
            }
            else if (playing) {
                CutsceneDirector.Stop();
            }
        }

        /// <summary>死亡演出中的躯干，无则 null</summary>
        private static GolemBodyAI FindPerformanceBody(out NPC body) {
            body = null;
            int b = GolemBodyAI.ActivePerformanceBody;
            if (b < 0 || b >= Main.maxNPCs) {
                GolemBodyAI.ActivePerformanceBody = -1;
                return null;
            }
            NPC npc = Main.npc[b];
            if (!npc.active || npc.type != NPCID.Golem) {
                GolemBodyAI.ActivePerformanceBody = -1;
                return null;
            }
            GolemBodyAI ai = GolemFacts.FindOverride<GolemBodyAI>(npc);
            if (ai == null || !ai.InDeathPerformance) {
                GolemBodyAI.ActivePerformanceBody = -1;
                return null;
            }
            body = npc;
            return ai;
        }
    }
}
