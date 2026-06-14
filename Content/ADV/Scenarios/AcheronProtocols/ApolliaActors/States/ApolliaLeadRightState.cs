using System;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.ApolliaActors.States
{
    /// <summary>
    /// 引路状态：阿波利娅向地图右侧持续前进，引导玩家前往要塞。 行为完全自持，与场景/对话代码零耦合：<br/> · 正常地形：全速奔跑前进，动画帧与速度同步<br/> · 遇到墙壁或深坑：即时切换飞行弧线越障，落地后自动恢复本状态<br/> · 无"到达"终点：由外部调用 <see cref="ApolliaActor.TransitionTo"/> 切换退出
    /// </summary>
    internal class ApolliaLeadRightState : IApolliaState
    {
        private const int MoveDirection = 1; //始终向右
        private const float WalkSpeed = 2.0f;
        private const float RunSpeed = 4.5f;
        private const float SpeedLerpRate = 0.05f;
        private const int WalkFrameIntervalSlow = 6;
        private const int WalkFrameIntervalFast = 3;
        private const int TotalWalkFrames = 11;
        /// <summary>落地后需要在地面行走的帧数，才开始检测障碍，防止着陆瞬间再次起飞</summary>
        private const int PostLandingGrace = 20;
        /// <summary>飞行时虚拟目标与当前位置的水平偏移（足够远</summary>
        private const float FlyLookaheadX = 1200f;

        private float currentSpeed;
        private int frameCounter;
        private int stepSoundTimer;
        private int groundFrames;

        public void Enter(ApolliaActor actor) {
            currentSpeed = WalkSpeed;
            frameCounter = 0;
            stepSoundTimer = 0;
            groundFrames = 0;
            actor.WalkDirection = MoveDirection;
            actor.FrameIndex = 1;
            actor.UseJumpTexture = false;
        }

        public IApolliaState Update(ApolliaActor actor) {
            stepSoundTimer++;
            actor.WalkDirection = MoveDirection;

            if (actor.OnGround) {
                groundFrames++;
            }

            //紧急逃逸：无论宽限期，若当前身体已陷入实心方块则立即起飞
            //（IsWallAhead 的前瞻有时因帧率/速度差异未能及时拦截，此处作兜底）
            if (actor.IsEmbeddedInSolid()) {
                Vector2 escapeTarget = new(actor.Center.X + FlyLookaheadX, actor.Center.Y);
                return new ApolliaFlyingState(escapeTarget, onLand: _ => new ApolliaLeadRightState());
            }

            //障碍检测：落地宽限期后才触发飞行，避免着陆抖动
            if (groundFrames > PostLandingGrace && actor.OnGround
                && (actor.IsWallAhead(MoveDirection) || actor.IsGapAhead(MoveDirection))) {
                Vector2 flyTarget = new(actor.Center.X + FlyLookaheadX, actor.Center.Y);
                //落地后回到新的 ApolliaLeadRightState 实例，继续向右引路
                return new ApolliaFlyingState(flyTarget, onLand: _ => new ApolliaLeadRightState());
            }

            //加速到奔跑速度
            currentSpeed = MathHelper.Lerp(currentSpeed, RunSpeed, SpeedLerpRate);
            float speedRatio = MathHelper.Clamp((currentSpeed - WalkSpeed) / (RunSpeed - WalkSpeed), 0f, 1f);

            //行走动画帧
            int frameInterval = Math.Max(
                (int)MathHelper.Lerp(WalkFrameIntervalSlow, WalkFrameIntervalFast, speedRatio),
                WalkFrameIntervalFast);

            frameCounter++;
            if (frameCounter >= frameInterval) {
                frameCounter = 0;
                actor.FrameIndex++;
                if (actor.FrameIndex >= TotalWalkFrames) {
                    actor.FrameIndex = 1;
                }
            }

            //水平移动 + 地面吸附
            actor.Position.X += currentSpeed * MoveDirection;
            actor.SnapToGround();

            //脚步声
            int soundInterval = (int)MathHelper.Lerp(20f, 10f, speedRatio);
            if (soundInterval > 0 && stepSoundTimer % soundInterval == 0) {
                float pitch = MathHelper.Lerp(0.4f, 0.6f, speedRatio);
                SoundEngine.PlaySound(SoundID.Run with { Volume = 0.3f, Pitch = pitch }, actor.Center);
            }

            return null;
        }

        public void Exit(ApolliaActor actor) { }
    }
}
