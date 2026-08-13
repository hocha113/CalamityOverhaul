using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.States;
using InnoVault.Cinematics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Core
{
    /// <summary>入场运镜：跟随汇聚点→拔塔推近→扣冠定格，不锁输入</summary>
    internal sealed class KingSlimeIntroCutscene : CutsceneClip<NPC>
    {
        public override int Priority => 60;

        public override bool CanPlay(Player player, NPC subject)
            => base.CanPlay(player, subject) && subject != null && subject.active
                && player.Distance(subject.Center) < 2300f;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            timeline.Duration = KingSlimeIntroState.IntroEnd;

            timeline
                .Add(CameraFocusTrack.Follow(0, KingSlimeIntroState.GatherEnd,
                    BossCenter, new Vector2(0f, 20f), 0.04f))
                .Add(CameraFocusTrack.Follow(KingSlimeIntroState.GatherEnd,
                    KingSlimeIntroState.CondenseEnd - KingSlimeIntroState.GatherEnd,
                    BossCenter, new Vector2(0f, -30f), 0.055f))
                .Add(CameraFocusTrack.Follow(KingSlimeIntroState.CondenseEnd,
                    KingSlimeIntroState.IntroEnd - KingSlimeIntroState.CondenseEnd,
                    BossCenter, new Vector2(0f, -10f), 0.06f));

            timeline
                .Add(new CameraZoomTrack(0, KingSlimeIntroState.GatherEnd, 1f, 1.1f, 0.03f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(KingSlimeIntroState.GatherEnd,
                    KingSlimeIntroState.CrownHitFrame - KingSlimeIntroState.GatherEnd, 1.1f, 1.2f, 0.04f))
                .Add(new CameraZoomTrack(KingSlimeIntroState.CrownHitFrame,
                    KingSlimeIntroState.IntroEnd - KingSlimeIntroState.CrownHitFrame, 1.2f, 1f, 0.04f, CutsceneEase.CubicOut));
        }

        private static Vector2 BossCenter(CutsceneContext context)
            => context.TryGetSubject(out NPC npc) && npc.active ? npc.Center : context.PlayerCenter;
    }

    /// <summary>死亡运镜：挣扎推近→忍者逃逸特写→终融拉远</summary>
    internal sealed class KingSlimeDeathCutscene : CutsceneClip<NPC>
    {
        public override int Priority => 100;

        public override bool CanPlay(Player player, NPC subject)
            => base.CanPlay(player, subject) && subject != null && subject.active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            timeline.Duration = KingSlimeDeathState.ActMeltEnd;

            timeline
                .Add(CameraFocusTrack.Follow(0, KingSlimeDeathState.ActAgonyEnd,
                    BossCenter, new Vector2(0f, 0f), 0.05f))
                .Add(CameraFocusTrack.Follow(KingSlimeDeathState.ActAgonyEnd,
                    KingSlimeDeathState.ActStruggleEnd - KingSlimeDeathState.ActAgonyEnd,
                    BossCenter, new Vector2(0f, -14f), 0.06f))
                .Add(CameraFocusTrack.Follow(KingSlimeDeathState.ActStruggleEnd,
                    KingSlimeDeathState.ActNinjaEnd - KingSlimeDeathState.ActStruggleEnd,
                    BossCenter, new Vector2(0f, -6f), 0.08f))
                .Add(CameraFocusTrack.Follow(KingSlimeDeathState.ActNinjaEnd,
                    KingSlimeDeathState.ActMeltEnd - KingSlimeDeathState.ActNinjaEnd,
                    BossCenter, new Vector2(0f, 26f), 0.05f));

            timeline
                .Add(new CameraZoomTrack(0, KingSlimeDeathState.ActAgonyEnd, 1f, 1.28f, 0.035f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(KingSlimeDeathState.ActAgonyEnd,
                    KingSlimeDeathState.ActStruggleEnd - KingSlimeDeathState.ActAgonyEnd, 1.28f, 1.45f, 0.045f))
                .Add(new CameraZoomTrack(KingSlimeDeathState.ActStruggleEnd,
                    KingSlimeDeathState.ActNinjaEnd - KingSlimeDeathState.ActStruggleEnd, 1.45f, 1.66f, 0.06f))
                .Add(new CameraZoomTrack(KingSlimeDeathState.ActNinjaEnd,
                    KingSlimeDeathState.ActMeltEnd - KingSlimeDeathState.ActNinjaEnd, 1.66f, 1.24f, 0.045f, CutsceneEase.CubicOut));

            //全程锁操作，交还前松开
            timeline.Add(new InputLockTrack(0, KingSlimeDeathState.ActMeltEnd,
                CutsceneInputLockFlags.Movement | CutsceneInputLockFlags.Jump | CutsceneInputLockFlags.UseItem));
        }

        private static Vector2 BossCenter(CutsceneContext context)
            => context.TryGetSubject(out NPC npc) && npc.active ? npc.Center : context.PlayerCenter;
    }

    /// <summary>本地玩家侧：入场/死亡演出时启停对应运镜</summary>
    internal class KingSlimePerformancePlayer : ModPlayer
    {
        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            //死亡演出优先
            NPC deathBoss = FindDeathPerformanceBoss();
            bool deathPlaying = CutsceneDirector.CurrentClip is KingSlimeDeathCutscene;
            if (deathBoss != null) {
                if (!deathPlaying) {
                    CutsceneDirector.Play<KingSlimeDeathCutscene, NPC>(deathBoss, restartSameClip: false);
                }
                return;
            }
            if (deathPlaying) {
                CutsceneDirector.Stop();
            }

            //入场演出
            NPC introBoss = FindIntroBoss();
            bool introPlaying = CutsceneDirector.CurrentClip is KingSlimeIntroCutscene;
            if (introBoss != null) {
                if (!introPlaying) {
                    CutsceneDirector.Play<KingSlimeIntroCutscene, NPC>(introBoss, restartSameClip: false);
                }
            }
            else if (introPlaying) {
                CutsceneDirector.Stop();
            }
        }

        private static NPC FindDeathPerformanceBoss() {
            int idx = KingSlimeAI.ActivePerformanceIndex;
            if (idx < 0 || idx >= Main.maxNPCs) {
                return null;
            }
            NPC npc = Main.npc[idx];
            if (!npc.active || npc.type != NPCID.KingSlime
                || (int)npc.ai[2] != (int)KingSlimeStateIndex.Death) {
                KingSlimeAI.ActivePerformanceIndex = -1;
                return null;
            }
            return npc;
        }

        private NPC FindIntroBoss() {
            foreach (var npc in Main.ActiveNPCs) {
                if (npc.type == NPCID.KingSlime && (int)npc.ai[2] == (int)KingSlimeStateIndex.Intro
                    && Player.Distance(npc.Center) < 2300f) {
                    return npc;
                }
            }
            return null;
        }
    }
}
