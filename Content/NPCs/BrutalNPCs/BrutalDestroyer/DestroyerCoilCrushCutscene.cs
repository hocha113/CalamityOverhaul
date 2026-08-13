using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using InnoVault.Cinematics;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer
{
    /// <summary>钢环绞缠运镜：只在被抓玩家本端播放；节拍对齐 DestroyerGrabPlayer 的本地时间线</summary>
    internal sealed class DestroyerCoilCrushCutscene : CutsceneClip<NPC>
    {
        //低于死亡演出运镜(100)，死亡片段可随时顶掉本片段
        public override int Priority => 90;

        public override bool CanPlay(Player player, NPC subject)
            => base.CanPlay(player, subject)
            && subject != null && subject.active && subject.type == NPCID.TheDestroyer
            && (int)subject.ai[2] == (int)DestroyerStateIndex.CoilCrush
            && (int)subject.ai[3] == player.whoAmI;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            timeline.Duration = 268;

            //聚焦环心，终结后交还给玩家
            timeline
                .Add(CameraFocusTrack.Follow(0, 214, RingCenter, new Vector2(0f, -10f), 0.09f))
                .Add(CameraFocusTrack.Lerp(214, 54, RingCenter, ctx => ctx.PlayerCenter, default, 0.08f));

            //收环推近，贯穿瞬间略拉远，释放后回常
            timeline
                .Add(new CameraZoomTrack(0, 30, 1f, 1.3f, 0.05f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(30, 156, 1.3f, 1.38f, 0.03f))
                .Add(new CameraZoomTrack(186, 30, 1.38f, 1.18f, 0.06f))
                .Add(new CameraZoomTrack(216, 52, 1.18f, 1f, 0.045f, CutsceneEase.CubicOut));

            //锁操控直到兜底击飞帧之后
            timeline.Add(new InputLockTrack(0, 244, CutsceneInputLockFlags.All));

            //节拍震屏：合拢/十字两拍/收紧两拍/终结
            timeline
                .Add(new CameraShakeTrack(24, Vector2.Zero, 8f, 0.9f, 18))
                .Add(new CameraShakeTrack(64, Vector2.Zero, 4.5f, 0.9f, 12))
                .Add(new CameraShakeTrack(90, Vector2.Zero, 4.5f, 0.9f, 12))
                .Add(new CameraShakeTrack(118, Vector2.Zero, 5f, 0.9f, 12))
                .Add(new CameraShakeTrack(150, Vector2.Zero, 5.5f, 0.9f, 12))
                .Add(new CameraShakeTrack(204, Vector2.Zero, 13f, 0.88f, 26));
        }

        //环心从头ai槽读取，主体失效时回退玩家中心防镜头跳原点
        private static Vector2 RingCenter(CutsceneContext context)
            => context.TryGetSubject(out NPC head) && head.active
                ? new Vector2(head.ai[0], head.ai[1])
                : context.PlayerCenter;
    }
}
