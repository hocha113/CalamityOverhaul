using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.States;
using InnoVault.Cinematics;
using InnoVault.GameSystem;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core
{
    /// <summary>
    /// 摄心镜狱运镜：仅被摄持玩家本地播放（BrainMindSeizePlayer 启停），节拍对齐状态常量
    /// 捕获推近→持环凝视（环心与真身兼收）→终结蓄力再推→掷飞跟身拉远
    /// </summary>
    internal sealed class BrainMindSeizeCutscene : CutsceneClip<NPC>
    {
        //低于死亡演出(100)：死亡运镜可顶替本片
        public override int Priority => 60;

        public override bool CanPlay(Player player, NPC subject)
            => base.CanPlay(player, subject) && subject != null && subject.active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            const int captureIn = 26;
            int reel = BrainMindSeizeState.FinisherReelTick;
            int fling = BrainMindSeizeState.FlingTick;
            int total = BrainMindSeizeState.ComboEndTick + 14;
            timeline.Duration = total;

            //焦点：捕获时压向环心，持环期兼收真身，终结段回聚玩家，掷飞后跟随抛物
            timeline
                .Add(CameraFocusTrack.Follow(0, captureIn, AnchorCenter, Vector2.Zero, 0.10f))
                .Add(CameraFocusTrack.Midpoint(captureIn, reel - captureIn, AnchorCenter, BrainCenter,
                    new Vector2(0f, -20f), 0.06f))
                .Add(CameraFocusTrack.Lerp(reel, fling - reel, MidAnchorBrain, PlayerCenter,
                    Vector2.Zero, 0.09f, CutsceneEase.QuadInOut))
                .Add(CameraFocusTrack.Follow(fling, total - fling, PlayerCenter, Vector2.Zero, 0.08f));

            //推拉：捕获快推→持环缓推→终结顶格→掷飞回拉释放
            timeline
                .Add(new CameraZoomTrack(0, captureIn, 1f, 1.34f, 0.06f, CutsceneEase.QuadOut))
                .Add(new CameraZoomTrack(captureIn, reel - captureIn, 1.34f, 1.42f, 0.03f))
                .Add(new CameraZoomTrack(reel, fling - reel, 1.42f, 1.5f, 0.05f))
                .Add(new CameraZoomTrack(fling, total - fling, 1.5f, 1.06f, 0.045f, CutsceneEase.CubicOut));

            //掷飞帧的镜头重击（运镜接管期间普通震屏失效，走导演震）
            timeline.Add(new CameraShakeTrack(fling, Vector2.Zero, 9f, 0.9f, 18));

            //输入锁到掷飞为止（与受害端 SetControls 双保险；掷飞后立刻还操作）
            timeline.Add(new InputLockTrack(0, fling));
        }

        /// <summary>环心锚点：读摄持者 override 同步槽，主体失效回退玩家中心</summary>
        private static Vector2 AnchorCenter(CutsceneContext context) {
            if (context.TryGetSubject(out NPC brain) && brain.active
                && TryGetMaster(brain, out BrainOfCthulhuAI master)) {
                return new Vector2(master.ai[0], master.ai[1]);
            }
            return context.PlayerCenter;
        }

        private static Vector2 BrainCenter(CutsceneContext context)
            => context.TryGetSubject(out NPC brain) && brain.active ? brain.Center : context.PlayerCenter;

        private static Vector2 MidAnchorBrain(CutsceneContext context)
            => (AnchorCenter(context) + BrainCenter(context)) * 0.5f;

        private static Vector2 PlayerCenter(CutsceneContext context) => context.PlayerCenter;

        private static bool TryGetMaster(NPC brain, out BrainOfCthulhuAI master) {
            master = null;
            if (brain.TryGetOverride(out Dictionary<Type, NPCOverride> overrides)
                && overrides.TryGetValue(typeof(BrainOfCthulhuAI), out NPCOverride brainOverride)
                && brainOverride is BrainOfCthulhuAI found) {
                master = found;
                return true;
            }
            return false;
        }
    }
}
