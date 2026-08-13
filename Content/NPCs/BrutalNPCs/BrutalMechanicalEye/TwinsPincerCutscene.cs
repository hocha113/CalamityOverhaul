using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye
{
    /// <summary>
    /// 钳形投技运镜，仅在被抓玩家本地播放(由 <see cref="TwinsGrabPerformancePlayer"/> 启停)；
    /// 片段起点即交扣瞬间，镜头压向钳口交点并推近，弹射段松开
    /// </summary>
    internal sealed class TwinsPincerCutscene : CutsceneClip<NPC>
    {
        //低于死亡演出，高于常规演出
        public override int Priority => 90;

        //夹合10+束缚26+喷灼78+蓄势10+弹射16=140，留裕量给节拍抖动
        private const int HoldLength = 150;
        private const int TailLength = 50;
        private const int Total = HoldLength + TailLength;

        public override bool CanPlay(Player player, NPC subject)
            => base.CanPlay(player, subject) && subject != null && subject.active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            timeline.Duration = Total;

            //全程聚焦钳口(双眼中点)，主体失效则退回玩家
            timeline.Add(CameraFocusTrack.Follow(0, Total, ClampCenter, new Vector2(0f, -10f), 0.09f));

            //交扣猛推近→束缚期缓慢回吐→弹射段松开
            timeline
                .Add(new CameraZoomTrack(0, 22, 1f, 1.34f, 0.07f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(22, HoldLength - 22, 1.34f, 1.26f, 0.03f))
                .Add(new CameraZoomTrack(HoldLength, TailLength, 1.26f, 1.02f, 0.05f, CutsceneEase.QuadInOut));

            //交扣震一记
            timeline.Add(new CameraShakeTrack(2, Vector2.Zero, 7f, 0.9f, 12));

            //全程锁定移动/跳跃/物品/交互/辅助动作
            timeline.Add(new InputLockTrack(0, Total,
                CutsceneInputLockFlags.Movement | CutsceneInputLockFlags.Jump
                | CutsceneInputLockFlags.UseItem | CutsceneInputLockFlags.UseTile
                | CutsceneInputLockFlags.Utility));
        }

        /// <summary>钳口交点：双眼中点，缺搭档退回主体，主体失效退回玩家</summary>
        private static Vector2 ClampCenter(CutsceneContext context) {
            if (!context.TryGetSubject(out NPC eye) || !eye.active) {
                return context.PlayerCenter;
            }
            NPC partner = TwinsStateContext.GetPartnerNpc(eye.type);
            if (partner != null && partner.active) {
                return (eye.Center + partner.Center) * 0.5f;
            }
            return eye.Center;
        }
    }
}
