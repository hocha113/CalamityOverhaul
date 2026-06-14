using InnoVault.Actors;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.Machines.Gargoyles
{
    /// <summary>泰伦虫族石像鬼：单体飞行单位，作为鸟群算法(Boids)中的个体参与集群飞行。 每帧由 <see cref="GargoyleBoids"/> 集中计算转向力并写入 <see cref="BoidVelocity"/>， 自身只</summary>
    internal class GargoyleActor : Actor
    {
        [VaultLoaden(CWRConstant.ADV + "Draedon/Gargoyle")]
        public static Texture2D Gargoyle = null!;

        #region 鸟群状态字段

        /// <summary>鸟群算法速度：由 <see cref="GargoyleBoids.UpdateFlock"/> 每帧写入</summary>
        internal Vector2 BoidVelocity;

        /// <summary>深度缩放因子 (0.3~1.2)，模拟远近层次感</summary>
        internal float DepthScale;

        /// <summary>翅膀拍打相位 (0~2π)</summary>
        internal float WingPhase;

        /// <summary>翅膀拍打速度（每帧弧度增量）</summary>
        internal float WingSpeed;

        /// <summary>流道索引，决定个体沿哪条蛀蜒路径飞行</summary>
        internal int SwarmGroup;

        /// <summary>个体噪声种子：，每个个体唯一</summary>
        internal float NoiseSeed;

        /// <summary>当前序列帧索引 (0~3)</summary>
        internal int AnimFrame;

        /// <summary>序列帧计时器（每 <see cref="AnimFrameInterval"/> 帧切换一帧）</summary>
        private int animTimer;

        /// <summary>序列帧切换间隔（游戏帧数）</summary>
        private const int AnimFrameInterval = 6;

        #endregion

        private static uint lastBoidsFrame;

        public override void OnSpawn(params object[] args) {
            Width = 24;
            Height = 24;
            DrawExtendMode = 1200;
            DrawLayer = ActorDrawLayer.AfterTiles;

            //每个个体随机化，制造深度层次和视觉多样性
            DepthScale = Main.rand.NextFloat(0.35f, 1.15f);
            WingPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            WingSpeed = Main.rand.NextFloat(0.12f, 0.22f);
            SwarmGroup = Main.rand.Next(GargoyleBoids.NumStreams);
            NoiseSeed = Main.rand.NextFloat(0f, 100f);

            BoidVelocity = Velocity;

            //随机初始帧与计时器，避免所有个体动画同步
            AnimFrame = Main.rand.Next(4);
            animTimer = Main.rand.Next(AnimFrameInterval);
        }

        public override void AI() {
            if (!MachineWorld.Active) {
                ActorLoader.KillActor(WhoAmI, false);
                return;
            }

            //鸟群算法每游戏帧只运行一次：第一个更新的石像鬼触发集中计算
            if (Main.GameUpdateCount != lastBoidsFrame) {
                lastBoidsFrame = Main.GameUpdateCount;
                var flock = ActorLoader.GetActiveActors<GargoyleActor>();
                GargoyleBoids.UpdateFlock(flock);
            }

            //翅膀动画
            WingPhase += WingSpeed;
            if (WingPhase > MathHelper.TwoPi) {
                WingPhase -= MathHelper.TwoPi;
            }

            //序列帧动画
            animTimer++;
            if (animTimer >= AnimFrameInterval) {
                animTimer = 0;
                AnimFrame = (AnimFrame + 1) % 4;
            }

            //将鸟群速度写入 Actor.Velocity（ActorLoader 会自动执行 Position += Velocity）
            Velocity = BoidVelocity;

            //朝向平滑跟随速度方向：较快的插值让转向更流畅
            if (BoidVelocity.LengthSquared() > 0.1f) {
                float targetRot = BoidVelocity.ToRotation();
                float diff = MathHelper.WrapAngle(targetRot - Rotation);
                Rotation += diff * 0.18f;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            if (Gargoyle == null) return false;

            Texture2D tex = Gargoyle;
            Vector2 drawPos = Center - Main.screenPosition;
            float baseScale = 0.35f * DepthScale;
            float drawRot = Rotation - MathHelper.Pi;
            //翅膀拍打：Y轴缩放脉动
            float wingPulse = MathF.Sin(WingPhase);
            Vector2 scaleVec = new(baseScale, baseScale * (1f + wingPulse * 0.18f));

            //深度明暗：远处暗淡，近处明亮
            float depthNorm = MathHelper.Clamp((DepthScale - 0.35f) / 0.8f, 0f, 1f);
            float brightness = MathHelper.Lerp(0.25f, 0.75f, depthNorm);
            Color bodyColor = new Color(brightness, brightness, brightness * 1.05f) * 0.9f;

            SpriteEffects fx = BoidVelocity.X < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            int frameHeight = tex.Height / 4;
            Rectangle sourceRect = new Rectangle(0, AnimFrame * frameHeight, tex.Width, frameHeight);
            Vector2 origin = new(tex.Width * 0.5f, frameHeight * 0.5f);

            //近处个体绘制运动拖影，增强速度感
            if (DepthScale > 0.6f) {
                Vector2 trailStep = BoidVelocity * 1.5f;
                for (int t = 2; t >= 1; t--) {
                    float trailAlpha = 0.08f / t;
                    spriteBatch.Draw(tex, drawPos - trailStep * t, sourceRect,
                        bodyColor * trailAlpha, drawRot, origin, scaleVec, fx, 0f);
                }
            }

            //主体
            spriteBatch.Draw(tex, drawPos, sourceRect, bodyColor, drawRot, origin, scaleVec, fx, 0f);

            return false;
        }
    }
}
