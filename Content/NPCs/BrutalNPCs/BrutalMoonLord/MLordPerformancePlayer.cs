using InnoVault.Cinematics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord
{
    /// <summary>死亡演出玩家侧：本地启停终焉运镜（观察核心状态驱动，不发包）</summary>
    internal class MLordPerformancePlayer : ModPlayer
    {
        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            MoonLordCoreAI coreAI = FindPerformanceCore(out NPC core);
            bool playing = CutsceneDirector.CurrentClip is MLordDeathCutscene;
            if (coreAI != null && core != null) {
                if (!playing) {
                    CutsceneDirector.Play<MLordDeathCutscene, NPC>(core, restartSameClip: false);
                }
            }
            else if (playing) {
                CutsceneDirector.Stop();
            }
        }

        /// <summary>死亡演出中的核心，无则 null</summary>
        private static MoonLordCoreAI FindPerformanceCore(out NPC core) {
            core = null;
            int index = MoonLordCoreAI.ActivePerformanceCore;
            if (index < 0 || index >= Main.maxNPCs) {
                MoonLordCoreAI.ActivePerformanceCore = -1;
                return null;
            }
            NPC npc = Main.npc[index];
            if (!npc.active || npc.type != NPCID.MoonLordCore) {
                MoonLordCoreAI.ActivePerformanceCore = -1;
                return null;
            }
            //槽位复用等异常取不到覆写时按演出结束处理（精确索引缺键会抛出）
            if (!npc.TryGetOverride(out MoonLordCoreAI ai) || !ai.InDeathPerformance) {
                MoonLordCoreAI.ActivePerformanceCore = -1;
                return null;
            }
            core = npc;
            return ai;
        }
    }
}
