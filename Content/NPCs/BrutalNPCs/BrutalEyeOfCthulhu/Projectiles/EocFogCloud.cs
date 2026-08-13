using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Projectiles
{
    /// <summary>
    /// 血雾团锚点：本身无伤害，屏幕着色器按其位置合成遮蔽雾体<br/>
    /// ai[1]=1 时为克眼即将出击的雾团，红脉冲预警（权威端置位+netUpdate）
    /// </summary>
    internal class EocFogCloud : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int MaxLife = 840;
        internal const int BloomTime = 55;
        internal const int FadeTime = 90;
        internal const float BaseRadius = 195f;

        /// <summary>成形进度 0~1</summary>
        internal float BloomProgress {
            get {
                int age = MaxLife - Projectile.timeLeft;
                return MathHelper.Clamp(age / (float)BloomTime, 0f, 1f);
            }
        }

        /// <summary>消散进度 0~1</summary>
        internal float FadeProgress => 1f - MathHelper.Clamp(Projectile.timeLeft / (float)FadeTime, 0f, 1f);

        /// <summary>当前雾半径 px，供着色器与寻路</summary>
        internal float CurrentRadius => BaseRadius * Projectile.scale
            * VaultUtils.EaseOutCubic(BloomProgress) * (1f - FadeProgress * 0.5f);

        /// <summary>当前遮蔽密度 0~1，出击预警时红脉冲调制</summary>
        internal float CurrentDensity {
            get {
                float d = 0.92f * VaultUtils.EaseOutCubic(BloomProgress) * (1f - VaultUtils.EaseInQuad(FadeProgress));
                if (Projectile.ai[1] > 0.5f) {
                    //预警脉冲：密度心跳式起伏，着色器侧读作红throb
                    d *= 0.85f + 0.3f * (float)Math.Sin(Main.timeForVisualEffects * 0.32f);
                }
                return MathHelper.Clamp(d, 0f, 1f);
            }
        }

        /// <summary>预警已开始的本地帧计数，画收缩环</summary>
        private float pulseLocalTimer;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 64;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxLife;
            Projectile.alpha = 255;
            //长寿命战场地形，晚入场玩家也要收到
            Projectile.netImportant = true;
        }

        public override void AI() {
            //缓慢漂移+吸附初速衰减
            Projectile.velocity *= 0.965f;

            //成形涌雾
            if (!VaultUtils.isServer && BloomProgress < 1f && Main.rand.NextBool(3)) {
                EocMotion.MistPuff(Projectile.Center + Main.rand.NextVector2Circular(CurrentRadius * 0.5f, CurrentRadius * 0.5f),
                    1, 1.2f, 0.4f);
            }

            //常驻边缘游丝，低频
            if (!VaultUtils.isServer && Main.rand.NextBool(14) && EocMotion.OnScreen(Projectile.Center, 500f)) {
                EocMotion.MistPuff(Projectile.Center + Main.rand.NextVector2CircularEdge(CurrentRadius * 0.75f, CurrentRadius * 0.75f),
                    1, 0.9f, 0.3f);
            }

            //出击预警
            if (Projectile.ai[1] > 0.5f) {
                pulseLocalTimer++;
                if (!VaultUtils.isServer) {
                    if (pulseLocalTimer == 1f) {
                        SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.9f, Pitch = -0.6f }, Projectile.Center);
                        SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.7f, Pitch = -0.3f }, Projectile.Center);
                    }
                    //向心收拢血丝，出击方向未知但位置确定
                    if (pulseLocalTimer % 2 == 0) {
                        EocMotion.ConvergeStreaks(Projectile.Center, MathHelper.Clamp(pulseLocalTimer / 42f, 0f, 0.74f),
                            CurrentRadius * 1.1f);
                    }
                }
            }
            else {
                pulseLocalTimer = 0f;
            }
        }

        public override bool ShouldUpdatePosition() => true;

        public override bool PreDraw(ref Color lightColor) {
            //雾体由屏幕着色器绘制；这里只画预警环
            if (Projectile.ai[1] > 0.5f && pulseLocalTimer > 0f) {
                float progress = MathHelper.Clamp(pulseLocalTimer / 42f, 0f, 1f);
                EocRenderHelper.DrawTelegraphRing(Main.spriteBatch, Projectile.Center,
                    CurrentRadius * 1.05f, progress, 0.85f);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isServer && EocMotion.OnScreen(Projectile.Center, 600f)) {
                EocMotion.MistPuff(Projectile.Center, 4, 1.3f, 0.4f);
            }
        }
    }
}
