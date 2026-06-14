using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.ApolliaActors.States
{
    /// <summary>行走状态：根据指令驱动阿波利娅的移动行为： Follow：跟随玩家；Hold：到达后切换到空闲。 默认模式下距离远时自动加速到奔跑，接近时减速；遇到障碍时飞行越过。 walkOnly 模式下全程普通行走速度，不加速奔跑且不触发飞行（</summary>
    internal class ApolliaWalkingState : IApolliaState
    {
        private const float WalkSpeed = 1.8f;
        private const float RunSpeed = 4.5f;
        private const float SprintDistance = 300f;
        private const float ArrivalDistance = 60f;
        private const int WalkFrameIntervalSlow = 6;
        private const int WalkFrameIntervalFast = 3;
        private const int TotalFrames = 11;
        /// <summary>进入行走后需要在地面行走多少帧才开始检测障碍，防止着陆后立刻再次飞行</summary>
        private const int PostLandingGrace = 15;

        private readonly bool walkOnly;
        private float currentSpeed;
        private int frameCounter;
        private int stepSoundTimer;
        private int groundFrames;

        /// <param name="walkOnly">为 true 时全程普通行走速度，不会加速奔跑</param>
        public ApolliaWalkingState(bool walkOnly = false) {
            this.walkOnly = walkOnly;
        }

        public void Enter(ApolliaActor actor) {
            frameCounter = 0;
            stepSoundTimer = 0;
            groundFrames = 0;
            currentSpeed = WalkSpeed;
            actor.FrameIndex = 1;
            actor.UseJumpTexture = false;
        }

        public IApolliaState Update(ApolliaActor actor) {
            stepSoundTimer++;

            Vector2 moveTarget = Main.LocalPlayer.Center;
            float distX = Math.Abs(actor.Center.X - moveTarget.X);
            int dir = Math.Sign(moveTarget.X - actor.Center.X);
            if (dir == 0) dir = -1;
            actor.WalkDirection = dir;

            //到达判定
            if (distX <= ArrivalDistance) {
                return ResolveArrivedState(actor);
            }

            //障碍检测：walkOnly 模式（过场行走）完全跳过，不触发飞行
            //正常模式需要在地面行走足够帧数后才触发，防止着陆后立刻再次起飞
            if (!walkOnly) {
                if (actor.OnGround) {
                    groundFrames++;
                }
                if (groundFrames > PostLandingGrace && actor.OnGround
                    && (actor.IsWallAhead(dir) || actor.IsGapAhead(dir))) {
                    return new ApolliaFlyingState(moveTarget);
                }
            }

            //速度曲线：walkOnly 模式始终慢走，否则远距离奔跑、接近时减速
            float targetSpeed;
            float speedRatio;
            if (walkOnly) {
                targetSpeed = WalkSpeed;
                speedRatio = 0f;
            }
            else {
                float speedFactor = MathHelper.Clamp((distX - ArrivalDistance) / (SprintDistance - ArrivalDistance), 0f, 1f);
                targetSpeed = MathHelper.Lerp(WalkSpeed, RunSpeed, speedFactor);
                speedRatio = (targetSpeed - WalkSpeed) / (RunSpeed - WalkSpeed);
            }
            currentSpeed = MathHelper.Lerp(currentSpeed, targetSpeed, 0.08f);

            //动画帧速度跟随移动速度
            int frameInterval = (int)MathHelper.Lerp(WalkFrameIntervalSlow, WalkFrameIntervalFast, speedRatio);
            frameInterval = Math.Max(frameInterval, WalkFrameIntervalFast);

            frameCounter++;
            if (frameCounter >= frameInterval) {
                frameCounter = 0;
                actor.FrameIndex++;
                if (actor.FrameIndex >= TotalFrames) {
                    actor.FrameIndex = 1;
                }
            }

            //移动
            actor.Position.X += currentSpeed * dir;

            //地面吸附
            actor.SnapToGround();

            //脚步声：奔跑时更频繁
            int soundInterval = (int)MathHelper.Lerp(20, 10, speedRatio);
            if (stepSoundTimer % soundInterval == 0) {
                float pitch = MathHelper.Lerp(0.4f, 0.6f, speedRatio);
                SoundEngine.PlaySound(SoundID.Run with { Volume = 0.3f, Pitch = pitch }, actor.Center);
            }

            return null;
        }

        public void Exit(ApolliaActor actor) { }

        private static IApolliaState ResolveArrivedState(ApolliaActor actor) {
            return actor.CurrentCommand switch {
                HeroCommand.Hold => new ApolliaIdleState(),
                _ => new ApolliaArrivedState(),
            };
        }
    }
}
