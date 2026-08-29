using CalamityOverhaul.Common;
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

    /// <summary>
    /// 吞没投技运镜：仅被吞玩家本人客户端播放，推近半透明腹内，全程锁操控，
    /// 挤压拍震动由玩家侧按同步计数请求。旁观者不被接管镜头
    /// </summary>
    internal sealed class KingSlimeEngulfCutscene : CutsceneClip<NPC>
    {
        /// <summary>运镜时长上限：覆盖消化+高压全程；正常在喷出帧由启停器提前平滑停止</summary>
        internal const int HoldDuration = 280;

        public override int Priority => 90;

        public override bool CanPlay(Player player, NPC subject)
            => base.CanPlay(player, subject) && subject != null && subject.active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            timeline.Duration = HoldDuration;

            //吞没帧冲击：普通震屏被运镜接管吃掉，用时间轴0帧震动补上抓取瞬间的顿挫
            timeline.Add(new CameraShakeTrack(0, Vector2.UnitY, 7f, 0.9f, 14));
            //贴住王体推近：被吞者视角沉进凝胶
            timeline.Add(CameraFocusTrack.Follow(0, HoldDuration, BossCenter, new Vector2(0f, -8f), 0.1f));
            timeline.Add(new CameraZoomTrack(0, 46, 1f, 1.58f, 0.055f, CutsceneEase.CubicOut));
            //高压段再推近一档(按同步抓取相位驱动)
            timeline.Add(new DynamicCameraTrack(0, HoldDuration, context => {
                if (context.TryGetSubject(out NPC npc) && npc.active
                    && KingSlimeAI.TryGetKingAI(npc, out KingSlimeAI king)
                    && (int)king.ai[KingSlimeEngulfState.SlotGrabPhase] >= 2) {
                    context.SetCameraZoom(1.72f, 0.05f);
                }
            }));
            //全程锁操控(仅本人客户端)，喷出后由启停器平滑交还
            timeline.Add(new InputLockTrack(0, HoldDuration, CutsceneInputLockFlags.All));
        }

        private static Vector2 BossCenter(CutsceneContext context)
            => context.TryGetSubject(out NPC npc) && npc.active ? npc.Center : context.PlayerCenter;
    }

    /// <summary>本地玩家侧：入场/死亡/吞没演出时启停对应运镜</summary>
    internal class KingSlimePerformancePlayer : ModPlayer
    {
        /// <summary>吞没运镜期间的镜头震动(运镜接管相机后普通震屏可能失效)</summary>
        internal static void RequestEngulfShake(float intensity, int duration) {
            if (VaultUtils.isServer || !CWRClientConfig.Instance.ScreenVibration) {
                return;
            }
            if (CutsceneDirector.CurrentClip is not KingSlimeEngulfCutscene) {
                return;
            }
            CutsceneDirector.Shake(Vector2.Zero, intensity, 0.9f, duration);
        }

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

            //吞没投技：只有被吞者本人的客户端接管镜头
            NPC engulfBoss = FindEngulfBossHoldingMe();
            bool engulfPlaying = CutsceneDirector.CurrentClip is KingSlimeEngulfCutscene;
            if (engulfBoss != null) {
                if (!engulfPlaying) {
                    CutsceneDirector.Play<KingSlimeEngulfCutscene, NPC>(engulfBoss, restartSameClip: false);
                }
                return;
            }
            if (engulfPlaying) {
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
                if (npc.type != NPCID.KingSlime || (int)npc.ai[2] != (int)KingSlimeStateIndex.Intro
                    || Player.Distance(npc.Center) >= 2300f) {
                    continue;
                }
                //必须验接管在场：原版王把ai[2]当传送计时器用，出生与每次传送后都归0，
                //恰与Intro索引撞值，不验则关闭残酷模式也会误触入场运镜
                if (!KingSlimeAI.TryGetKingAI(npc, out _)) {
                    continue;
                }
                return npc;
            }
            return null;
        }

        /// <summary>
        /// 正吞着本人的王：状态为吞没+接管在场+受害者槽指向自己+处于持人相位(消化/高压)。
        /// 喷出帧(相位3)即返回null→镜头随弹射立刻平滑释放，玩家看着自己飞出去
        /// </summary>
        private NPC FindEngulfBossHoldingMe() {
            foreach (var npc in Main.ActiveNPCs) {
                if (npc.type != NPCID.KingSlime || (int)npc.ai[2] != (int)KingSlimeStateIndex.Engulf) {
                    continue;
                }
                if (!KingSlimeAI.TryGetKingAI(npc, out KingSlimeAI king)) {
                    continue;
                }
                int grabPhase = (int)king.ai[KingSlimeEngulfState.SlotGrabPhase];
                if ((int)king.ai[KingSlimeEngulfState.SlotVictim] - 1 != Player.whoAmI
                    || grabPhase is not 1 and not 2) {
                    continue;
                }
                return npc;
            }
            return null;
        }
    }
}
