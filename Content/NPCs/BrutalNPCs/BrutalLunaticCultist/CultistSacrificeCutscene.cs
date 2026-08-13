using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States;
using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist
{
    /// <summary>
    /// 献祭投技运镜：仅在被抓玩家本机播放（CultistSacrificePlayer 启停）；
    /// 时间轴与 CultistSacrificeGrabState 拍点常量对齐（本地 tick = 状态 t - SealCloseEnd）
    /// </summary>
    internal sealed class CultistSacrificeCutscene : CutsceneClip<NPC>
    {
        //低于死亡运镜（100），可被死亡演出抢占
        public override int Priority => 90;

        //拍点换算到剪辑本地时刻
        private const int Offset = CultistSacrificeGrabState.SealCloseEnd;
        private const int LiftTick = CultistSacrificeGrabState.LiftEnd - Offset;               //40
        private const int Beat1Tick = CultistSacrificeGrabState.Beat1Hit - Offset;             //66
        private const int Beat2Tick = CultistSacrificeGrabState.Beat2Hit - Offset;             //114
        private const int ChargeTick = CultistSacrificeGrabState.FinaleChargeStart - Offset;   //136
        private const int FinaleTick = CultistSacrificeGrabState.FinaleHit - Offset;           //184
        private const int LaunchTick = FinaleTick + 2;                                         //186
        private const int TotalTick = CultistSacrificeGrabState.ReleaseEnd - Offset;           //208

        public override bool CanPlay(Player player, NPC subject)
            => base.CanPlay(player, subject) && subject != null && subject.active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            timeline.Duration = TotalTick;

            //全程盯住阵心（吊升跟随），掷出后回落到玩家
            timeline
                .Add(CameraFocusTrack.Follow(0, LaunchTick, SealFocus, new Vector2(0f, -10f), 0.09f))
                .Add(CameraFocusTrack.Follow(LaunchTick, TotalTick - LaunchTick,
                    context => context.PlayerCenter, Vector2.Zero, 0.07f));

            //锁身推近→连段微距→终结再压→掷出拉开
            timeline
                .Add(new CameraZoomTrack(0, LiftTick, 1f, 1.3f, 0.05f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(LiftTick, ChargeTick - LiftTick, 1.3f, 1.38f, 0.03f))
                .Add(new CameraZoomTrack(ChargeTick, FinaleTick - ChargeTick, 1.38f, 1.52f, 0.045f))
                .Add(new CameraZoomTrack(FinaleTick, TotalTick - FinaleTick, 1.52f, 1.08f, 0.05f, CutsceneEase.CubicOut));

            //拍点震（运镜接管相机后普通震屏可能失效，走时间轴震动）
            timeline
                .Add(new CameraShakeTrack(1, Vector2.Zero, 7f, 0.88f, 14))
                .Add(new CameraShakeTrack(Beat1Tick, Vector2.Zero, 6f, 0.9f, 10))
                .Add(new CameraShakeTrack(Beat2Tick, Vector2.Zero, 6f, 0.9f, 10))
                .Add(new CameraShakeTrack(FinaleTick, Vector2.Zero, 12f, 0.9f, 20));

            //输入锁到掷出为止，掷出后玩家自控落点
            timeline.Add(new InputLockTrack(0, LaunchTick, CutsceneInputLockFlags.All));
        }

        /// <summary>阵心焦点：主体失效时回退玩家中心，避免镜头瞬移</summary>
        private static Vector2 SealFocus(CutsceneContext context) {
            if (!context.TryGetSubject(out NPC boss) || !boss.active
                || !boss.TryGetOverride(out CultistBossAI bossOverride) || bossOverride?.Context == null) {
                return context.PlayerCenter;
            }
            int t = bossOverride.Machine?.CurrentState is CultistSacrificeGrabState grabState
                ? grabState.Timer
                : CultistSacrificeGrabState.ReleaseEnd;
            return CultistSacrificeGrabState.SealCenter(bossOverride.Context, t);
        }
    }
}
