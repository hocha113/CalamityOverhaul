using CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.Machines.DropPodScens;
using InnoVault.Actors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.Machines.LandingScens
{
    /// <summary>
    /// 机械世界着陆空降仓Actor：坠毁在地面的空降仓， 带有烟雾、火花、金属嘎吱声等环境效果， 等待玩家按下左键后弹出
    /// </summary>
    internal class MachineWorldLandingActor : Actor
    {
        #region 状态枚举

        private enum LandingPhase
        {
            /// <summary>初始坠落撞击阶段：短暂的剧烈震动和闪光</summary>
            Impact,
            /// <summary>冷却稳定阶段：烟雾弥漫，火花四溅，等待玩家操作</summary>
            Idle,
            /// <summary>弹出阶段：舱门打开，玩家弹出</summary>
            Eject,
            /// <summary>完成：Actor逐渐消散</summary>
            Done
        }

        #endregion

        #region 字段

        private LandingPhase phase;
        private int phaseTimer;

        //撞击效果
        private float impactFlashAlpha;
        private float impactShakeIntensity;
        private Vector2 shakeOffset;
        private const int ImpactDuration = 45;

        //空降仓视觉
        private float podTilt; //仓体歪斜角度（坠毁后略微倾斜）
        private float heatGlow; //残余灼烧辉光强度 0~1
        private float crackEmission; //裂缝发光强度

        //烟雾粒子
        private readonly List<SmokeParticle> smokeParticles = [];
        private const int MaxSmokeParticles = 80;

        //火花粒子
        private readonly List<SparkParticle> sparkParticles = [];
        private const int MaxSparkParticles = 40;

        //金属嘎吱声计时
        private int creakSoundTimer;

        //提示闪烁
        private float hintAlpha;
        private int hintBlinkTimer;
        private bool hintVisible;

        //弹出效果
        private float ejectProgress;
        private const int EjectDuration = 40;
        private float doorOpenAngle;
        private bool playerEjected;

        #endregion

        public override void OnSpawn(params object[] args) {
            Width = 240;
            Height = 280;
            DrawExtendMode = 400;
            DrawLayer = ActorDrawLayer.Default;

            phase = LandingPhase.Impact;
            phaseTimer = 0;
            impactFlashAlpha = 1f;
            impactShakeIntensity = 8f;
            shakeOffset = Vector2.Zero;
            podTilt = Main.rand.NextFloat(-0.08f, 0.08f);
            heatGlow = 0.8f;
            crackEmission = 0.6f;
            creakSoundTimer = 0;
            hintAlpha = 0f;
            hintBlinkTimer = 0;
            hintVisible = true;
            ejectProgress = 0f;
            doorOpenAngle = 0f;
            playerEjected = false;

            //初始撞击音效
            SoundEngine.PlaySound(SoundID.Item14 with {
                Volume = 1.2f,
                Pitch = -0.6f
            }, Center);
            SoundEngine.PlaySound(SoundID.NPCHit4 with {
                Volume = 0.9f,
                Pitch = -0.8f
            }, Center);
        }

        public override void AI() {
            if (!MachineWorld.Active) {
                ActorLoader.KillActor(WhoAmI);
                return;
            }

            phaseTimer++;

            switch (phase) {
                case LandingPhase.Impact:
                    UpdateImpact();
                    break;
                case LandingPhase.Idle:
                    UpdateIdle();
                    break;
                case LandingPhase.Eject:
                    UpdateEject();
                    break;
                case LandingPhase.Done:
                    UpdateDone();
                    break;
            }

            //持续效果：烟雾和火花（Done阶段仍然更新以排空剩余粒子）
            UpdateSmokeParticles();
            UpdateSparkParticles();
            if (phase != LandingPhase.Done) {
                UpdateCreakSound();
            }

            //冷却效果：灼烧辉光逐渐消退
            if (heatGlow > 0.05f) {
                heatGlow *= 0.997f;
            }
            if (crackEmission > 0.1f) {
                crackEmission *= 0.998f;
            }
        }

        #region 阶段更新

        private void UpdateImpact() {
            //撞击闪光衰减
            impactFlashAlpha = MathHelper.Lerp(1f, 0f, (float)phaseTimer / ImpactDuration);

            //震动衰减
            float shakeDecay = 1f - (float)phaseTimer / ImpactDuration;
            impactShakeIntensity = 8f * shakeDecay * shakeDecay;
            shakeOffset = new Vector2(
                Main.rand.NextFloat(-impactShakeIntensity, impactShakeIntensity),
                Main.rand.NextFloat(-impactShakeIntensity, impactShakeIntensity));

            //撞击阶段大量烟尘
            if (phaseTimer < 20 && phaseTimer % 2 == 0) {
                for (int i = 0; i < 3; i++) {
                    SpawnImpactSmoke();
                }
                for (int i = 0; i < 2; i++) {
                    SpawnSpark();
                }
            }

            //撞击尘土Dust
            if (phaseTimer < 15 && phaseTimer % 3 == 0) {
                for (int i = 0; i < 8; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 8f);
                    vel.Y = -Math.Abs(vel.Y) * 0.6f; //偏向向上弹起
                    Dust dust = Dust.NewDustDirect(Center + new Vector2(Main.rand.NextFloat(-60, 60), 100),
                        1, 1, DustID.Smoke, vel.X, vel.Y, 150, default, Main.rand.NextFloat(1.5f, 3f));
                    dust.noGravity = true;
                    dust.fadeIn = 1.5f;
                }
            }

            if (phaseTimer >= ImpactDuration) {
                phase = LandingPhase.Idle;
                phaseTimer = 0;
                impactFlashAlpha = 0f;
                impactShakeIntensity = 0f;
                shakeOffset = Vector2.Zero;
            }
        }

        private void UpdateIdle() {
            //轻微持续震动
            float microShake = 0.3f;
            shakeOffset = new Vector2(
                Main.rand.NextFloat(-microShake, microShake),
                Main.rand.NextFloat(-microShake, microShake));

            //定期生成烟雾
            if (phaseTimer % 8 == 0 && smokeParticles.Count < MaxSmokeParticles) {
                SpawnIdleSmoke();
            }

            //偶尔火花
            if (Main.rand.NextBool(30) && sparkParticles.Count < MaxSparkParticles) {
                SpawnSpark();
            }

            //提示文字闪烁
            hintBlinkTimer++;
            if (hintBlinkTimer % 40 < 30) {
                hintAlpha = MathHelper.Lerp(hintAlpha, 1f, 0.08f);
            }
            else {
                hintAlpha = MathHelper.Lerp(hintAlpha, 0.3f, 0.08f);
            }
            hintVisible = true;

            //检测玩家左键点击
            Player player = Main.LocalPlayer;
            if (player != null && player.active && !player.dead) {
                if (player.TryGetOverride<MachineWorldLandingPlayer>(out var landingPlayer)) {
                    if (landingPlayer.LandingActive && !landingPlayer.EjectAnimating
                        && landingPlayer.ClickedThisFrame) {
                        //触发弹出
                        phase = LandingPhase.Eject;
                        phaseTimer = 0;
                        hintVisible = false;
                        hintAlpha = 0f;

                        //弹出音效
                        SoundEngine.PlaySound(SoundID.Item14 with {
                            Volume = 0.8f,
                            Pitch = 0.3f
                        }, Center);
                        SoundEngine.PlaySound(SoundID.Item62 with {
                            Volume = 0.7f,
                            Pitch = 0.1f
                        }, Center);

                        landingPlayer.TriggerEject(Center);

                        //触发弹出屏幕闪光
                        MachineWorldLandingDrawSystem.TriggerEjectFlash();
                    }
                }
            }
        }

        private void UpdateEject() {
            ejectProgress = MathHelper.Clamp((float)phaseTimer / EjectDuration, 0f, 1f);
            float easedProgress = VaultUtils.EaseOutBack(ejectProgress);

            //舱门打开动画
            doorOpenAngle = MathHelper.Lerp(0f, MathHelper.PiOver4 * 1.5f, easedProgress);

            //弹出时产生大量烟雾和火花
            if (phaseTimer < 15) {
                for (int i = 0; i < 2; i++) {
                    SpawnEjectSmoke();
                }
                SpawnSpark();
            }

            //弹出冲击震动
            if (phaseTimer < 10) {
                float ejectShake = 3f * (1f - phaseTimer / 10f);
                shakeOffset = new Vector2(
                    Main.rand.NextFloat(-ejectShake, ejectShake),
                    Main.rand.NextFloat(-ejectShake, ejectShake));
            }
            else {
                shakeOffset = Vector2.Zero;
            }

            //弹出Dust效果：向上喷射
            if (phaseTimer < 10 && phaseTimer % 2 == 0) {
                for (int i = 0; i < 6; i++) {
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-10f, -4f));
                    Dust dust = Dust.NewDustDirect(Center + new Vector2(Main.rand.NextFloat(-30, 30), -80),
                        1, 1, DustID.Smoke, vel.X, vel.Y, 100, default, Main.rand.NextFloat(1.5f, 2.5f));
                    dust.noGravity = true;
                }
            }

            if (!playerEjected && phaseTimer >= 5) {
                playerEjected = true;
            }

            if (phaseTimer >= EjectDuration) {
                phase = LandingPhase.Done;
                phaseTimer = 0;
            }
        }

        private void UpdateDone() {
            //空降仓保持在原地，偶尔冒少量烟
            if (phaseTimer % 30 == 0 && smokeParticles.Count < 10) {
                SpawnIdleSmoke();
            }

            shakeOffset = Vector2.Zero;
        }

        #endregion

        #region 粒子管理

        private void UpdateSmokeParticles() {
            for (int i = smokeParticles.Count - 1; i >= 0; i--) {
                smokeParticles[i].Update();
                if (smokeParticles[i].IsDead) {
                    smokeParticles.RemoveAt(i);
                }
            }
        }

        private void UpdateSparkParticles() {
            for (int i = sparkParticles.Count - 1; i >= 0; i--) {
                sparkParticles[i].Update();
                if (sparkParticles[i].IsDead) {
                    sparkParticles.RemoveAt(i);
                }
            }
        }

        private void UpdateCreakSound() {
            creakSoundTimer++;
            //不规则间隔播放金属嘎吱声
            if (creakSoundTimer > 80 + Main.rand.Next(120)) {
                creakSoundTimer = 0;
                SoundEngine.PlaySound(SoundID.NPCHit4 with {
                    Volume = 0.3f,
                    Pitch = Main.rand.NextFloat(-0.5f, 0.5f)
                }, Center);
            }
        }

        private void SpawnImpactSmoke() {
            if (smokeParticles.Count >= MaxSmokeParticles) return;

            Vector2 spawnPos = Center + new Vector2(
                Main.rand.NextFloat(-80, 80),
                Main.rand.NextFloat(60, 120));

            Vector2 velocity = new Vector2(
                Main.rand.NextFloat(-3f, 3f),
                Main.rand.NextFloat(-4f, -1f));

            smokeParticles.Add(new SmokeParticle {
                Position = spawnPos,
                Velocity = velocity,
                Color = Color.Lerp(new Color(80, 80, 80), new Color(40, 40, 40), Main.rand.NextFloat()),
                Alpha = Main.rand.NextFloat(0.5f, 0.9f),
                Scale = Main.rand.NextFloat(4f, 8f),
                Life = 0,
                MaxLife = Main.rand.Next(60, 120),
                RotationSpeed = Main.rand.NextFloat(-0.02f, 0.02f),
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi)
            });
        }

        private void SpawnIdleSmoke() {
            if (smokeParticles.Count >= MaxSmokeParticles) return;

            //从仓体裂缝和底部冒出
            Vector2 spawnPos = Center + new Vector2(
                Main.rand.NextFloat(-50, 50),
                Main.rand.NextFloat(-20, 80));

            Vector2 velocity = new Vector2(
                Main.rand.NextFloat(-0.8f, 0.8f),
                Main.rand.NextFloat(-1.5f, -0.3f));

            smokeParticles.Add(new SmokeParticle {
                Position = spawnPos,
                Velocity = velocity,
                Color = Color.Lerp(new Color(60, 60, 70), new Color(90, 90, 100), Main.rand.NextFloat()),
                Alpha = Main.rand.NextFloat(0.2f, 0.5f),
                Scale = Main.rand.NextFloat(2f, 5f),
                Life = 0,
                MaxLife = Main.rand.Next(80, 160),
                RotationSpeed = Main.rand.NextFloat(-0.01f, 0.01f),
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi)
            });
        }

        private void SpawnEjectSmoke() {
            if (smokeParticles.Count >= MaxSmokeParticles) return;

            //从仓顶喷射
            Vector2 spawnPos = Center + new Vector2(
                Main.rand.NextFloat(-40, 40),
                Main.rand.NextFloat(-120, -60));

            Vector2 velocity = new Vector2(
                Main.rand.NextFloat(-2f, 2f),
                Main.rand.NextFloat(-6f, -2f));

            smokeParticles.Add(new SmokeParticle {
                Position = spawnPos,
                Velocity = velocity,
                Color = Color.Lerp(new Color(200, 200, 200), new Color(150, 150, 160), Main.rand.NextFloat()),
                Alpha = Main.rand.NextFloat(0.5f, 0.8f),
                Scale = Main.rand.NextFloat(3f, 6f),
                Life = 0,
                MaxLife = Main.rand.Next(40, 80),
                RotationSpeed = Main.rand.NextFloat(-0.03f, 0.03f),
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi)
            });
        }

        private void SpawnSpark() {
            if (sparkParticles.Count >= MaxSparkParticles) return;

            //从仓体随机位置迸出
            Vector2 spawnPos = Center + new Vector2(
                Main.rand.NextFloat(-60, 60),
                Main.rand.NextFloat(-40, 80));

            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 6f);
            velocity.Y -= 2f; //偏向向上

            sparkParticles.Add(new SparkParticle {
                Position = spawnPos,
                Velocity = velocity,
                Color = Color.Lerp(new Color(255, 200, 100), new Color(255, 150, 50), Main.rand.NextFloat()),
                Alpha = Main.rand.NextFloat(0.7f, 1f),
                Scale = Main.rand.NextFloat(0.5f, 1.5f),
                Life = 0,
                MaxLife = Main.rand.Next(15, 40)
            });
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            if (DropPod.DropPodAsset == null || !DropPod.DropPodAsset.IsLoaded) return false;

            Vector2 drawCenter = Center - Main.screenPosition + shakeOffset;

            //绘制地面撞击痕迹
            DrawCraterEffect(spriteBatch, drawCenter);

            //绘制烟雾（在仓体后面）
            DrawSmokeParticles(spriteBatch);

            //绘制残余灼烧辉光
            DrawHeatGlow(spriteBatch, drawCenter);

            //绘制空降仓主体
            DrawCrashedPod(spriteBatch, drawCenter);

            //绘制裂缝发光效果
            DrawCrackGlow(spriteBatch, drawCenter);

            //绘制火花
            DrawSparkParticles(spriteBatch);

            //绘制撞击闪光
            if (impactFlashAlpha > 0.01f) {
                DrawImpactFlash(spriteBatch, drawCenter);
            }

            //绘制弹出效果
            if (phase == LandingPhase.Eject) {
                DrawEjectEffect(spriteBatch, drawCenter);
            }

            //绘制操作提示
            if (hintVisible && hintAlpha > 0.01f) {
                DrawHintText(spriteBatch, drawCenter);
            }

            return false;
        }

        private void DrawCraterEffect(SpriteBatch sb, Vector2 drawCenter) {
            if (CWRAsset.SoftGlow == null || CWRAsset.SoftGlow.IsDisposed) return;

            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 craterPos = drawCenter + new Vector2(0, 100);

            //暗色撞击坑阴影
            Color craterColor = new Color(20, 18, 15) * 0.4f;
            float craterScale = 160f / (glow.Width * 0.5f);
            sb.Draw(glow, craterPos, null, craterColor with { A = 0 }, 0f,
                glow.Size() * 0.5f, new Vector2(craterScale * 2f, craterScale * 0.5f), SpriteEffects.None, 0f);
        }

        private void DrawHeatGlow(SpriteBatch sb, Vector2 drawCenter) {
            if (CWRAsset.SoftGlow == null || CWRAsset.SoftGlow.IsDisposed) return;
            if (heatGlow < 0.05f) return;

            Texture2D glow = CWRAsset.SoftGlow.Value;

            //橙红灼烧环绕光
            float pulse = MathF.Sin(phaseTimer * 0.06f) * 0.15f + 0.85f;
            Color heatColor = new Color(255, 120, 40) * (heatGlow * 0.3f * pulse);
            float scale = 120f / (glow.Width * 0.5f);
            sb.Draw(glow, drawCenter, null, heatColor with { A = 0 }, 0f,
                glow.Size() * 0.5f, scale, SpriteEffects.None, 0f);

            //仓顶灼烧点
            Vector2 topPos = drawCenter - new Vector2(0, 80);
            Color topHeatColor = new Color(255, 180, 80) * (heatGlow * 0.4f * pulse);
            float topScale = 60f / (glow.Width * 0.5f);
            sb.Draw(glow, topPos, null, topHeatColor with { A = 0 }, 0f,
                glow.Size() * 0.5f, topScale, SpriteEffects.None, 0f);
        }

        private void DrawCrashedPod(SpriteBatch sb, Vector2 drawCenter) {
            Texture2D podTex = DropPod.DropPodAsset.Value;
            Vector2 origin = podTex.Size() * 0.5f;

            //外层光晕：冷色调，坠毁后偏暗
            float glowPulse = MathF.Sin(phaseTimer * 0.03f) * 0.1f + 0.9f;
            Color ambientGlow = new Color(40, 60, 100) * (0.2f * glowPulse);
            DrawSoftGlow(sb, drawCenter, ambientGlow, 90f);

            //主体绘制：带倾斜
            Color podColor = Color.White;
            //坠毁后颜色略微偏暗偏橙（烧焦感）
            podColor = Color.Lerp(podColor, new Color(200, 180, 160), heatGlow * 0.3f);
            sb.Draw(podTex, drawCenter, null, podColor, podTilt, origin, 1f, SpriteEffects.None, 0f);
        }

        private void DrawCrackGlow(SpriteBatch sb, Vector2 drawCenter) {
            if (CWRAsset.SoftGlow == null || CWRAsset.SoftGlow.IsDisposed) return;
            if (crackEmission < 0.05f) return;

            Texture2D glow = CWRAsset.SoftGlow.Value;

            //在仓体上模拟裂缝发光：几个不同位置的小光点
            float flicker = MathF.Sin(phaseTimer * 0.12f) * 0.3f + 0.7f;
            Color crackColor = new Color(255, 100, 30) * (crackEmission * 0.5f * flicker);
            float crackScale = 15f / (glow.Width * 0.5f);

            Vector2[] crackPositions = [
                drawCenter + new Vector2(-25, -30),
                drawCenter + new Vector2(18, 10),
                drawCenter + new Vector2(-10, 50),
                drawCenter + new Vector2(30, -60),
            ];

            foreach (var pos in crackPositions) {
                sb.Draw(glow, pos, null, crackColor with { A = 0 }, 0f,
                    glow.Size() * 0.5f, crackScale * Main.rand.NextFloat(0.8f, 1.2f), SpriteEffects.None, 0f);
            }
        }

        private void DrawSmokeParticles(SpriteBatch sb) {
            if (CWRAsset.SoftGlow == null || CWRAsset.SoftGlow.IsDisposed) return;

            Texture2D glow = CWRAsset.SoftGlow.Value;

            foreach (var p in smokeParticles) {
                float lifeRatio = (float)p.Life / p.MaxLife;
                //烟雾先浓后淡
                float alpha = p.Alpha * MathF.Sin(lifeRatio * MathHelper.Pi);
                Color c = p.Color * alpha;
                float scale = p.Scale * (1f + lifeRatio * 1.5f) * 0.05f;
                Vector2 drawPos = p.Position - Main.screenPosition;
                sb.Draw(glow, drawPos, null, c with { A = (byte)(c.A * 0.3f) }, p.Rotation,
                    glow.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            }
        }

        private void DrawSparkParticles(SpriteBatch sb) {
            if (CWRAsset.SoftGlow == null || CWRAsset.SoftGlow.IsDisposed) return;

            Texture2D glow = CWRAsset.SoftGlow.Value;

            foreach (var p in sparkParticles) {
                float lifeRatio = (float)p.Life / p.MaxLife;
                float alpha = p.Alpha * (1f - lifeRatio * lifeRatio);
                Color c = p.Color * alpha;
                float scale = p.Scale * (1f - lifeRatio * 0.5f) * 0.03f;
                Vector2 drawPos = p.Position - Main.screenPosition;
                //火花用椭圆形拉伸表示运动方向
                float rot = p.Velocity.ToRotation();
                sb.Draw(glow, drawPos, null, c with { A = 0 }, rot,
                    glow.Size() * 0.5f, new Vector2(scale * 2.5f, scale), SpriteEffects.None, 0f);
            }
        }

        private void DrawImpactFlash(SpriteBatch sb, Vector2 drawCenter) {
            if (CWRAsset.SoftGlow == null || CWRAsset.SoftGlow.IsDisposed) return;

            Texture2D glow = CWRAsset.SoftGlow.Value;

            //白色中心闪光
            Color flashColor = Color.White * (impactFlashAlpha * 0.8f);
            float flashScale = 200f / (glow.Width * 0.5f) * (1f + impactFlashAlpha * 0.5f);
            sb.Draw(glow, drawCenter, null, flashColor with { A = 0 }, 0f,
                glow.Size() * 0.5f, flashScale, SpriteEffects.None, 0f);

            //橙色外圈
            Color outerColor = new Color(255, 150, 50) * (impactFlashAlpha * 0.5f);
            float outerScale = flashScale * 1.5f;
            sb.Draw(glow, drawCenter, null, outerColor with { A = 0 }, 0f,
                glow.Size() * 0.5f, outerScale, SpriteEffects.None, 0f);
        }

        private void DrawEjectEffect(SpriteBatch sb, Vector2 drawCenter) {
            if (CWRAsset.SoftGlow == null || CWRAsset.SoftGlow.IsDisposed) return;

            Texture2D glow = CWRAsset.SoftGlow.Value;

            //舱门打开时的蒸汽光效
            float ejectGlow = MathF.Sin(ejectProgress * MathHelper.Pi);
            Color steamColor = new Color(200, 220, 255) * (ejectGlow * 0.6f);
            Vector2 doorPos = drawCenter - new Vector2(0, 80);
            float steamScale = 80f / (glow.Width * 0.5f) * (1f + ejectProgress);
            sb.Draw(glow, doorPos, null, steamColor with { A = 0 }, 0f,
                glow.Size() * 0.5f, steamScale, SpriteEffects.None, 0f);
        }

        private void DrawHintText(SpriteBatch sb, Vector2 drawCenter) {
            //在仓体上方显示"按下左键弹出"提示
            string hintText = "[ 按下左键弹出 ]";
            var font = Terraria.GameContent.FontAssets.MouseText.Value;
            Vector2 textSize = font.MeasureString(hintText);
            Vector2 textPos = drawCenter - new Vector2(textSize.X * 0.5f, 180);

            //发光背景
            Color glowColor = new Color(80, 150, 255) * (hintAlpha * 0.3f);
            DrawSoftGlow(sb, textPos + textSize * 0.5f, glowColor, 60f);

            //描边文字
            Color shadowColor = Color.Black * (hintAlpha * 0.8f);
            Color textColor = Color.Lerp(new Color(180, 220, 255), new Color(100, 180, 255),
                MathF.Sin(phaseTimer * 0.08f) * 0.5f + 0.5f) * hintAlpha;

            //四方向描边
            for (int dx = -1; dx <= 1; dx++) {
                for (int dy = -1; dy <= 1; dy++) {
                    if (dx == 0 && dy == 0) continue;
                    sb.DrawString(font, hintText, textPos + new Vector2(dx * 2, dy * 2), shadowColor);
                }
            }
            sb.DrawString(font, hintText, textPos, textColor);
        }

        private static void DrawSoftGlow(SpriteBatch sb, Vector2 center, Color color, float radius) {
            if (CWRAsset.SoftGlow == null || CWRAsset.SoftGlow.IsDisposed) return;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float scale = radius / (glow.Width * 0.5f);
            sb.Draw(glow, center, null, color with { A = 0 }, 0f, glow.Size() * 0.5f, scale, SpriteEffects.None, 0f);
        }

        #endregion

        #region 内部类

        private class SmokeParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public Color Color;
            public float Alpha;
            public float Scale;
            public int Life;
            public int MaxLife;
            public float Rotation;
            public float RotationSpeed;
            public bool IsDead => Life >= MaxLife;

            public void Update() {
                Life++;
                Position += Velocity;
                Velocity *= 0.97f;
                Velocity.Y -= 0.03f; //烟雾上升
                Velocity.X += Main.rand.NextFloat(-0.1f, 0.1f);
                Rotation += RotationSpeed;
            }
        }

        private class SparkParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public Color Color;
            public float Alpha;
            public float Scale;
            public int Life;
            public int MaxLife;
            public bool IsDead => Life >= MaxLife;

            public void Update() {
                Life++;
                Position += Velocity;
                Velocity *= 0.95f;
                Velocity.Y += 0.15f; //火花受重力
            }
        }

        #endregion
    }
}
