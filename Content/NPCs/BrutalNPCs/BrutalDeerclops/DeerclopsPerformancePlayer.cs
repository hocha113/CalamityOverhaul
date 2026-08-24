using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Core;
using InnoVault.Cinematics;
using InnoVault.GameSystem;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops
{
    /// <summary>死亡演出玩家侧：本地观察独眼巨鹿死亡态，启停运镜</summary>
    internal class DeerclopsPerformancePlayer : ModPlayer
    {
        /// <summary>死亡运镜期间的镜头震动(运镜接管相机后普通震屏可能失效)</summary>
        internal static void RequestShake(float intensity, int duration) {
            if (VaultUtils.isServer || !CWRClientConfig.Instance.ScreenVibration) {
                return;
            }
            if (CutsceneDirector.CurrentClip is not DeerclopsDeathCutscene) {
                return;
            }
            CutsceneDirector.Shake(Vector2.Zero, intensity, 0.9f, duration);
        }

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            NPC deer = FindDeathPerformanceDeer();
            bool playing = CutsceneDirector.CurrentClip is DeerclopsDeathCutscene;

            if (deer != null) {
                //restartSameClip:false，已播则复用
                if (!playing) {
                    CutsceneDirector.Play<DeerclopsDeathCutscene, NPC>(deer, restartSameClip: false);
                }
            }
            else if (playing) {
                CutsceneDirector.Stop();
            }
        }

        /// <summary>处于死亡演出态且被本模组接管的独眼巨鹿，无则null</summary>
        private static NPC FindDeathPerformanceDeer() {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type != NPCID.Deerclops) {
                    continue;
                }
                if ((int)npc.ai[2] != (int)DeerclopsStateIndex.Death) {
                    continue;
                }
                //必须确认接管在场：原版AI的ai[2]是homeTileX，可能撞值
                if (!npc.TryGetOverride(out Dictionary<Type, NPCOverride> overrides)
                    || !overrides.TryGetValue(typeof(DeerclopsAI), out NPCOverride deerOverride)
                    || deerOverride is not DeerclopsAI) {
                    continue;
                }
                return npc;
            }
            return null;
        }
    }
}
