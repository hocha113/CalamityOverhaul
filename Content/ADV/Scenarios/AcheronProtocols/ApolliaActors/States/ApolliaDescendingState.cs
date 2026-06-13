using InnoVault.Cinematics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.ApolliaActors.States
{
    /// <summary>
    /// 从天而降状态——阿波利娅从高空降落到地面。
    /// 降落过程中产生星流拖尾粒子与尾焰效果，
    /// 着陆时触发屏幕震动与冲击波粒子，表现力量感
    /// </summary>
    internal class ApolliaDescendingState : IApolliaState
    {
        private const int Duration = 50;

        private Vector2 startPos;
        private Vector2 targetPos;
        private int timer;

        /// <param name="target">降落目标地面位置</param>
        public ApolliaDescendingState(Vector2 target) {
            targetPos = target;
            startPos = target - new Vector2(0, 800);
        }

        public void Enter(ApolliaActor actor) {
            timer = 0;
            actor.Position = startPos;
            actor.FrameIndex = 0;
            actor.DescendAlpha = 0f;
            actor.GlowIntensity = 0f;
            actor.UseJumpTexture = true;

            //启动尾焰系统——降落全程拖尾
            actor.JetTrailActive = true;
            //登场运镜由 ApolliaCutscene 驱动，已在 ApolliaActor.StartLandingCutscene 中本地播放
        }

        public IApolliaState Update(ApolliaActor actor) {
            timer++;
            float progress = MathHelper.Clamp((float)timer / Duration, 0f, 1f);
            float eased = 1f - (1f - progress) * (1f - progress); //EaseOutQuad

            //位置插值
            actor.Position = Vector2.Lerp(startPos, targetPos, eased);

            //淡入
            actor.DescendAlpha = MathHelper.Clamp(progress * 3f, 0f, 1f);

            //降落阶段粒子——随接近地面越来越密集
            if (!VaultUtils.isServer) {
                SpawnDescentParticles(actor, progress);
            }

            //尾焰粒子——降落后半段开始喷射
            if (progress > 0.3f) {
                actor.SpawnJetParticle();
                if (progress > 0.6f) {
                    actor.SpawnJetParticle();
                }
            }

            if (progress >= 1f) {
                OnLanding(actor);

                //着陆后根据指令决定下一状态
                //初次降落靠近玩家时使用 walkOnly 模式，全程普通行走不奔跑
                return actor.CurrentCommand switch {
                    HeroCommand.Hold or HeroCommand.Defensive => new ApolliaIdleState(),
                    _ => new ApolliaWalkingState(walkOnly: true),
                };
            }

            return null;
        }

        public void Exit(ApolliaActor actor) {
            actor.UseJumpTexture = false;
            actor.StopJetTrail();
        }

        #region 降落过程粒子

        /// <summary>
        /// 降落过程中的粒子效果——星流电弧 + 火花拖尾，密度随接近地面递增
        /// </summary>
        private static void SpawnDescentParticles(ApolliaActor actor, float progress) {
            //基础电弧粒子——全程持续
            if (Main.GameUpdateCount % 2 == 0) {
                Vector2 dustPos = actor.Center + Main.rand.NextVector2Circular(12, 6);
                Dust electric = Dust.NewDustDirect(dustPos, 1, 1, DustID.Electric,
                    Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(1f, 3f), 150, default, 0.7f);
                electric.noGravity = true;
                electric.velocity *= 0.5f;
            }

            //星流拖尾——从角色身后向上延伸的光流
            int streakCount = progress < 0.5f ? 1 : 2;
            for (int i = 0; i < streakCount; i++) {
                float offsetX = Main.rand.NextFloat(-18f, 18f);
                float streakSpeed = Main.rand.NextFloat(4f, 8f);
                Vector2 streakPos = actor.Center + new Vector2(offsetX, Main.rand.NextFloat(-10f, 10f));
                Dust streak = Dust.NewDustDirect(streakPos, 1, 1, DustID.BlueTorch,
                    Main.rand.NextFloat(-0.3f, 0.3f), streakSpeed, 120, default, Main.rand.NextFloat(0.8f, 1.4f));
                streak.noGravity = true;
                streak.fadeIn = Main.rand.NextFloat(0.8f, 1.4f);
            }

            //加速阶段（后半段）——更多粒子 + 火花
            if (progress > 0.4f) {
                float intensity = (progress - 0.4f) / 0.6f; //0~1

                //侧向散射火花
                int sparkCount = (int)(intensity * 3) + 1;
                for (int i = 0; i < sparkCount; i++) {
                    float angle = MathHelper.PiOver2 + Main.rand.NextFloat(-0.8f, 0.8f);
                    float speed = Main.rand.NextFloat(1.5f, 4f) * intensity;
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;
                    Vector2 sparkPos = actor.Center + new Vector2(Main.rand.NextFloat(-10, 10), 15);
                    Dust spark = Dust.NewDustDirect(sparkPos, 1, 1, DustID.FireworksRGB,
                        vel.X, vel.Y, 100, new Color(100, 180, 255), Main.rand.NextFloat(0.5f, 1f));
                    spark.noGravity = true;
                }

                //白热核心粒子
                if (Main.GameUpdateCount % 3 == 0) {
                    Vector2 corePos = actor.Center + Main.rand.NextVector2Circular(5, 3);
                    Dust core = Dust.NewDustDirect(corePos, 1, 1, DustID.WhiteTorch,
                        0, Main.rand.NextFloat(0.5f, 2f), 80, default, Main.rand.NextFloat(0.6f, 1f));
                    core.noGravity = true;
                }
            }

            //临近着陆——密集光流拖尾
            if (progress > 0.75f) {
                float finalIntensity = (progress - 0.75f) / 0.25f;
                int burstCount = (int)(finalIntensity * 4) + 1;
                for (int i = 0; i < burstCount; i++) {
                    Vector2 burstPos = actor.Center + new Vector2(Main.rand.NextFloat(-20, 20), Main.rand.NextFloat(5, 25));
                    Dust burst = Dust.NewDustDirect(burstPos, 1, 1, DustID.Electric,
                        Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(2f, 6f),
                        100, default, Main.rand.NextFloat(0.8f, 1.5f));
                    burst.noGravity = true;
                    burst.fadeIn = 1.2f;
                }
            }
        }

        #endregion

        #region 着陆冲击效果

        /// <summary>
        /// 着陆瞬间——屏幕震动 + 冲击烟尘 + 辉光爆发 + 音效
        /// </summary>
        private static void OnLanding(ApolliaActor actor) {
            //重型着陆音效
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.7f, Pitch = 0.1f }, actor.Center);
            SoundEngine.PlaySound(SoundID.Item70 with { Volume = 0.5f, Pitch = -0.3f }, actor.Center);

            //着陆震动——叠加到当前 InnoVault 登场运镜上
            if (CutsceneDirector.CurrentClip is ApolliaCutscene) {
                CutsceneDirector.Shake(new Vector2(0, 1), 8f, 0.88f, 25);
            }

            if (!VaultUtils.isServer) {
                Vector2 footPos = actor.Center + new Vector2(0, 20);

                //着陆烟尘——扇形向两侧扩散
                for (int i = 0; i < 20; i++) {
                    float spreadAngle = MathHelper.Pi * 0.15f; //扇形角度
                    float angle = -MathHelper.PiOver2 + Main.rand.NextFloat(-spreadAngle, spreadAngle)
                                  + (i % 2 == 0 ? MathHelper.Pi * 0.4f : -MathHelper.Pi * 0.4f);
                    float speed = Main.rand.NextFloat(2f, 5f);
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;
                    Dust smoke = Dust.NewDustDirect(
                        footPos + new Vector2(Main.rand.NextFloat(-15, 15), Main.rand.NextFloat(-5, 5)),
                        1, 1, DustID.Smoke, vel.X, vel.Y, 150, default, Main.rand.NextFloat(1.2f, 2.2f));
                    smoke.noGravity = true;
                }

                //着陆电弧火花——从落点向四周散射
                for (int i = 0; i < 15; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float speed = Main.rand.NextFloat(3f, 7f);
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;
                    Dust arc = Dust.NewDustDirect(
                        footPos + Main.rand.NextVector2Circular(8, 4),
                        1, 1, DustID.Electric, vel.X, vel.Y, 100, default, Main.rand.NextFloat(0.6f, 1.2f));
                    arc.noGravity = true;
                }

                //地面碎屑——带重力的石块粒子
                for (int i = 0; i < 8; i++) {
                    Vector2 debrisVel = new Vector2(
                        Main.rand.NextFloat(-4f, 4f),
                        Main.rand.NextFloat(-6f, -2f));
                    Dust debris = Dust.NewDustDirect(
                        footPos + new Vector2(Main.rand.NextFloat(-12, 12), 0),
                        1, 1, DustID.Stone, debrisVel.X, debrisVel.Y, 120, default, Main.rand.NextFloat(0.8f, 1.4f));
                    debris.noGravity = false; //受重力
                }

                //中心白热闪光
                for (int i = 0; i < 6; i++) {
                    Vector2 flashPos = footPos + Main.rand.NextVector2Circular(6, 3);
                    Dust flash = Dust.NewDustDirect(flashPos, 1, 1, DustID.WhiteTorch,
                        Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-2f, 0f),
                        60, default, Main.rand.NextFloat(1f, 1.8f));
                    flash.noGravity = true;
                }
            }

            //辉光爆发——着陆瞬间最亮
            actor.GlowIntensity = 1.2f;
            actor.DescendAlpha = 1f;
        }

        #endregion
    }
}
