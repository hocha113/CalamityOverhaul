using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.States;
using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord
{
    /// <summary>
    /// 掌中处刑运镜（仅被抓玩家本端）：攥握急推近→随手拖上头颅面前紧咬→
    /// 坍缩再挤一档→甩落急拉远回玩家。输入锁全程，甩出后立即归还操作
    /// </summary>
    internal sealed class MLordGrabCutscene : CutsceneClip<NPC>
    {
        //低于死亡运镜（100）：处刑中若入终焉时刻，终焉接管
        public override int Priority => 50;

        public override bool CanPlay(Player player, NPC subject)
            => base.CanPlay(player, subject) && subject != null && subject.active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            timeline.Duration = MLordPalmExecutionState.RecoverEnd + 20;

            //焦点：攥握点急吸→头颅面前驻留→甩出后回追玩家
            timeline
                .Add(CameraFocusTrack.Follow(0, 36, GripCenter, new Vector2(0f, -20f), 0.14f))
                .Add(CameraFocusTrack.Follow(36, MLordPalmExecutionState.ReleaseTick - 36,
                    FaceCenter, new Vector2(0f, 10f), 0.08f))
                .Add(CameraFocusTrack.Follow(MLordPalmExecutionState.ReleaseTick,
                    timeline.Duration - MLordPalmExecutionState.ReleaseTick,
                    context => context.PlayerCenter, new Vector2(0f, 0f), 0.07f));

            //缩放：顿帧急咬 1.22→拖近 1.42→凝视缓咬 1.47→坍缩挤到 1.52→甩落急退 1.05→缓释回 1
            timeline
                .Add(new CameraZoomTrack(0, 10, 1f, 1.22f, 0.16f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(10, 38, 1.22f, 1.42f, 0.07f))
                .Add(new CameraZoomTrack(48, MLordPalmExecutionState.CollapseStart - 48, 1.42f, 1.47f, 0.04f))
                .Add(new CameraZoomTrack(MLordPalmExecutionState.CollapseStart,
                    MLordPalmExecutionState.ReleaseTick - MLordPalmExecutionState.CollapseStart,
                    1.47f, 1.52f, 0.05f))
                .Add(new CameraZoomTrack(MLordPalmExecutionState.ReleaseTick, 20, 1.52f, 1.05f, 0.12f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(MLordPalmExecutionState.ReleaseTick + 20,
                    timeline.Duration - MLordPalmExecutionState.ReleaseTick - 20, 1.05f, 1f, 0.05f));

            //震屏节拍：贴脸死光出束、甩落终击
            timeline
                .Add(new CameraShakeTrack(MLordPalmExecutionState.RaySpawnTick + MLordPalmExecutionState.RayTelegraph,
                    new Vector2(1f, 0f), 7f, 0.9f, 16))
                .Add(new CameraShakeTrack(MLordPalmExecutionState.ReleaseTick, new Vector2(0f, 1f), 11f, 0.9f, 20));

            //锁输入到甩出为止（甩出后玩家立刻拿回操作调整落点）
            timeline.Add(new InputLockTrack(0, MLordPalmExecutionState.ReleaseTick + 6, CutsceneInputLockFlags.All));
        }

        /// <summary>抓握手当前位置（缺位退回玩家中心）</summary>
        private static Vector2 GripCenter(CutsceneContext context) {
            if (context.TryGetSubject(out NPC core) && core.active
                && core.TryGetOverride(out MoonLordCoreAI coreAI)) {
                int handIndex = (int)coreAI.ai[MLordAiSlots.OvGrabHand] - 1;
                if (handIndex >= 0 && handIndex < Main.maxNPCs && Main.npc[handIndex].active) {
                    return Main.npc[handIndex].Center;
                }
            }
            return context.PlayerCenter;
        }

        /// <summary>头颅面前的舞台点（头焊接位推导，免扫部件）</summary>
        private static Vector2 FaceCenter(CutsceneContext context) {
            if (context.TryGetSubject(out NPC core) && core.active) {
                return core.Center + MLordDirector.HeadWeldOffset
                    + MLordPalmExecutionState.HoldOffset * 0.55f;
            }
            return context.PlayerCenter;
        }
    }
}
