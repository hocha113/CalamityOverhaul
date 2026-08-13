using InnoVault.Cinematics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron
{
    /// <summary>死亡运镜：跟随坠海全程，只聚焦与轻推变焦，不锁玩家输入</summary>
    internal sealed class FishronDeathCutscene : CutsceneClip<NPC>
    {
        public override int Priority => 100;

        public override bool CanPlay(Player player, NPC subject)
            => base.CanPlay(player, subject) && subject != null && subject.active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            //与 FishronDeathState 的节奏对齐：挣扎→坠落→搁浅（地形不同坠落时长有浮动，取上限）
            const int total = 620;
            timeline.Duration = total;

            timeline
                .Add(CameraFocusTrack.Follow(0, 72, BossCenter, new Vector2(0f, 0f), 0.05f))
                .Add(CameraFocusTrack.Follow(72, 106, BossCenter, new Vector2(0f, 60f), 0.07f))
                .Add(CameraFocusTrack.Follow(178, total - 178, BossCenter, new Vector2(0f, -30f), 0.05f));

            timeline
                .Add(new CameraZoomTrack(0, 72, 1f, 1.18f, 0.04f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(72, 106, 1.18f, 1.32f, 0.05f))
                .Add(new CameraZoomTrack(178, total - 178, 1.32f, 1.1f, 0.04f, CutsceneEase.CubicOut));
        }

        private static Vector2 BossCenter(CutsceneContext context)
            => context.TryGetSubject(out NPC boss) && boss.active ? boss.Center : context.PlayerCenter;
    }

    /// <summary>死亡演出玩家侧：本地启停运镜</summary>
    internal class FishronDeathPerformancePlayer : ModPlayer
    {
        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            DukeFishronAI bossAI = FindPerformanceBoss(out NPC boss);
            bool playing = CutsceneDirector.CurrentClip is FishronDeathCutscene;

            if (bossAI != null && boss != null) {
                if (!playing) {
                    CutsceneDirector.Play<FishronDeathCutscene, NPC>(boss, restartSameClip: false);
                }
            }
            else if (playing) {
                CutsceneDirector.Stop();
            }
        }

        /// <summary>死亡演出中的公爵，无则 null</summary>
        private static DukeFishronAI FindPerformanceBoss(out NPC boss) {
            boss = null;
            int idx = DukeFishronAI.ActivePerformanceBoss;
            if (idx < 0 || idx >= Main.maxNPCs) {
                DukeFishronAI.ActivePerformanceBoss = -1;
                return null;
            }
            NPC npc = Main.npc[idx];
            if (!npc.active || npc.type != NPCID.DukeFishron) {
                DukeFishronAI.ActivePerformanceBoss = -1;
                return null;
            }
            DukeFishronAI ai = npc.GetOverride<DukeFishronAI>();
            if (ai == null || !ai.InDeathPerformance) {
                DukeFishronAI.ActivePerformanceBoss = -1;
                return null;
            }
            boss = npc;
            return ai;
        }
    }
}
