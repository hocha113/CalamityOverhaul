using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.States;
using InnoVault.Cinematics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime
{
    /// <summary>
    /// 水晶囚舞运镜：仅在被抓玩家客户端播放。
    /// 成茧急推→华尔兹随拍呼吸→终结蓄力紧逼→掷飞回拉；锁输入到终结拍为止(掷飞后交还操控)。
    /// </summary>
    internal sealed class QueenWaltzGrabCutscene : CutsceneClip<NPC>
    {
        //低于各死亡运镜(100)：同屏他boss死亡演出优先
        public override int Priority => 90;

        public override bool CanPlay(Player player, NPC subject)
            => base.CanPlay(player, subject) && subject != null && subject.active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            //时长只是保底(受害端扫描失效即 Stop)，放宽以免时停等边缘把演出拖过期后被重播
            timeline.Duration = 520;

            //全程追焦：囚舞期皇后与晶茧的中点，掷飞后追被抛的玩家
            timeline.Add(CameraFocusTrack.Follow(0, timeline.Duration, WaltzFocus, new Vector2(0f, -24f), 0.1f));

            //成茧急推(顿帧感)
            timeline.Add(new CameraZoomTrack(0, QueenCrystalPrisonWaltzState.CocoonTime, 1f, 1.32f, 0.12f, CutsceneEase.CubicOut));
            //华尔兹段：踢拍呼吸与终结蓄力紧逼(读皇后同步时钟驱动)
            timeline.Add(new DynamicCameraTrack(QueenCrystalPrisonWaltzState.CocoonTime,
                timeline.Duration - QueenCrystalPrisonWaltzState.CocoonTime, WaltzZoomPulse));

            //锁操控到终结拍：掷飞后立即交还(飞行中可调整姿态)
            timeline.Add(new InputLockTrack(0, QueenCrystalPrisonWaltzState.FinisherTick, CutsceneInputLockFlags.All));
        }

        /// <summary>焦点：囚舞期取皇后与晶茧中点；终结掷飞后追玩家(抛物线是收尾爽点)；主体失效退玩家</summary>
        private static Vector2 WaltzFocus(CutsceneContext context) {
            if (!context.TryGetSubject(out NPC queen) || !queen.active) {
                return context.PlayerCenter;
            }
            if (QueenCrystalPrisonWaltzState.GrabTick(queen) >= QueenCrystalPrisonWaltzState.FinisherTick) {
                return context.PlayerCenter;
            }
            Projectile prison = QueenCrystalPrisonWaltzState.FindPrison(queen);
            if (prison == null) {
                return queen.Center;
            }
            return (queen.Center + prison.Center) * 0.5f;
        }

        /// <summary>缩放脉动：踢拍近点鼓一口气，终结蓄力段持续贴近，掷飞后放开</summary>
        private static void WaltzZoomPulse(CutsceneContext context) {
            if (!context.TryGetSubject(out NPC queen) || !queen.active) {
                return;
            }
            int t = QueenCrystalPrisonWaltzState.GrabTick(queen);
            float zoom = 1.32f;

            foreach (int k in QueenCrystalPrisonWaltzState.KickTicks) {
                int dist = Math.Abs(t - k);
                if (dist < 10) {
                    zoom += 0.08f * (1f - dist / 10f);
                }
            }

            if (t >= QueenCrystalPrisonWaltzState.FinisherChargeTick && t < QueenCrystalPrisonWaltzState.FinisherTick) {
                float p = (t - QueenCrystalPrisonWaltzState.FinisherChargeTick)
                    / (float)(QueenCrystalPrisonWaltzState.FinisherTick - QueenCrystalPrisonWaltzState.FinisherChargeTick);
                zoom += 0.13f * p;
            }
            else if (t >= QueenCrystalPrisonWaltzState.FinisherTick) {
                //掷飞：镜头松开回拉
                float p = MathHelper.Clamp((t - QueenCrystalPrisonWaltzState.FinisherTick) / 26f, 0f, 1f);
                zoom = MathHelper.Lerp(1.45f, 1.08f, p);
            }

            context.SetCameraZoom(zoom, 0.12f);
        }
    }
}
