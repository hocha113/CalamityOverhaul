using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Rogue;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.JusticeUnveileds
{
    internal class JusticeUnveiledExplode : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile + "JusticeUnveiledExplode";
        public const int maxFrame = 14;
        private int frameIndex = 0;
        private int time;
        private readonly List<ExplosionWave> explosionWaves = new();
        private readonly List<ImpactSpark> impactSparks = new();

        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> MaskLaserLine = null;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 1800;//缩小打击范围
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = false;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            if (time == 0) {
                if (Main.zenithWorld) {
                    SoundEngine.PlaySound(SpearOfLonginus.AT, Projectile.Center);
                }
                else {
                    //多重音效叠加（降低音量）
                    SoundEngine.PlaySound(CWRSound.JustStrike with { Volume = 0.8f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.DD2_LightningBugZap with {
                        Pitch = -0.4f,
                        Volume = 0.6f
                    }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Thunder with {
                        Pitch = 0.3f,
                        Volume = 0.4f
                    }, Projectile.Center);
                }

                if (CWRServerConfig.Instance.ScreenVibration) {
                    PunchCameraModifier modifier = new PunchCameraModifier(Projectile.Center,
                            Main.rand.NextVector2Unit(), 12f, 6f, 25, 1000f, FullName);//弱化震屏
                    Main.instance.CameraModifiers.Add(modifier);
                }
            }

            if (++time < 6) {
                return;
            }

            //初始化爆炸特效
            if (time == 6) {
                InitializeExplosionEffects();

                if (Projectile.ai[0].TryGetNPC(out var target2)) {
                    int size = 1800;
                    Point pos = target2.Center.ToPoint() - new Point(size / 2, size / 2);
                    Rectangle hitBox = new Rectangle(pos.X, pos.Y, size, size);
                    foreach (var n in Main.ActiveNPCs) {
                        if (!n.Hitbox.Intersects(hitBox) || n.whoAmI == target2.whoAmI) {
                            continue;
                        }
                        JusticeUnveiled.SpawnCrossMarker(n, Projectile.owner);
                    }
                }
            }

            if (++Projectile.frameCounter > 3) {
                frameIndex++;

                //关键帧触发
                if (frameIndex == 4) {
                    TriggerMainImpact();
                }

                if (frameIndex == 8) {
                    TriggerSecondaryImpact();
                }

                if (frameIndex >= maxFrame) {
                    Projectile.Kill();
                    frameIndex = 0;
                }
                Projectile.frameCounter = 0;
            }

            Projectile.scale += 0.055f;//稍微减缓缩放

            if (Projectile.ai[1] < 3 && Projectile.ai[2] == 0) {//减少光柱强度
                Projectile.ai[1]++;
            }
            if (frameIndex > 8) {
                Projectile.ai[1] = 1;
            }
            if (Projectile.ai[2] > 0 && Projectile.ai[1] > 0) {
                Projectile.ai[1]--;
            }

            //更新特效
            UpdateExplosionEffects();

            //强化照明（稍微降低）
            float lightIntensity = (float)Math.Sin(frameIndex / (float)maxFrame * MathHelper.Pi);
            Lighting.AddLight(Projectile.Center,
                1.5f * lightIntensity,
                1.2f * lightIntensity,
                0.4f * lightIntensity);
        }

        private void InitializeExplosionEffects() {
            //生成初始冲击波（减少数量）
            for (int i = 0; i < 2; i++) {
                explosionWaves.Add(new ExplosionWave(Projectile.Center, i * 10f));
            }

            //生成火花效果（减少数量）
            for (int i = 0; i < 50; i++) {
                float angle = MathHelper.TwoPi * i / 50f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(12f, 24f);
                impactSparks.Add(new ImpactSpark(Projectile.Center, velocity));
            }

            //大量粒子爆发（减少数量）
            SpawnExplosionParticles(60);
        }

        private void TriggerMainImpact() {
            //主要冲击（弱化）
            if (CWRServerConfig.Instance.ScreenVibration) {
                PunchCameraModifier modifier = new PunchCameraModifier(Projectile.Center,
                    Main.rand.NextVector2Unit(), 18f, 8f, 30, 1200f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }

            //音效（降低音量）
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with {
                Volume = 1.0f,
                Pitch = -0.3f
            }, Projectile.Center);

            //额外冲击波
            for (int i = 0; i < 1; i++) {
                explosionWaves.Add(new ExplosionWave(Projectile.Center, i * 15f));
            }

            SpawnExplosionParticles(100);
        }

        private void TriggerSecondaryImpact() {
            //次级冲击（弱化）
            if (CWRServerConfig.Instance.ScreenVibration) {
                PunchCameraModifier modifier = new PunchCameraModifier(Projectile.Center,
                    Main.rand.NextVector2Unit(), 10f, 6f, 20, 1000f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }

            SoundEngine.PlaySound(SoundID.Item62 with {
                Volume = 0.8f,
                Pitch = -0.2f
            }, Projectile.Center);

            SpawnExplosionParticles(50);
        }

        private void SpawnExplosionParticles(int count) {
            for (int i = 0; i < count; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 30f);

                //金色火花
                BasePRT spark = new PRT_Light(
                    Projectile.Center + Main.rand.NextVector2Circular(40f, 40f),
                    velocity,
                    Main.rand.NextFloat(0.6f, 1.2f),
                    Color.Lerp(Color.Gold, Color.OrangeRed, Main.rand.NextFloat()),
                    Main.rand.Next(15, 30),
                    1, 1.3f, hueShift: 0.0f
                );
                PRTLoader.AddParticle(spark);
            }
        }

        private void UpdateExplosionEffects() {
            //更新冲击波
            for (int i = explosionWaves.Count - 1; i >= 0; i--) {
                explosionWaves[i].Update();
                if (explosionWaves[i].ShouldRemove()) {
                    explosionWaves.RemoveAt(i);
                }
            }

            //更新火花
            for (int i = impactSparks.Count - 1; i >= 0; i--) {
                impactSparks[i].Update();
                if (impactSparks[i].ShouldRemove()) {
                    impactSparks.RemoveAt(i);
                }
            }
        }

        public override bool? CanHitNPC(NPC target) {
            if (!(frameIndex == 4 || frameIndex == 8)) {
                return false;
            }
            return base.CanHitNPC(target);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (time < 6) {
                return false;
            }

            //绘制冲击波
            foreach (var wave in explosionWaves) {
                wave.Draw(Main.spriteBatch);
            }

            //绘制火花
            foreach (var spark in impactSparks) {
                spark.Draw(Main.spriteBatch);
            }

            //绘制光柱
            DrawLightBeam();

            //绘制主体
            DrawMainExplosion(lightColor);

            return false;
        }

        private void DrawLightBeam() {
            Color drawColor = Color.Lerp(Color.Gold, Color.OrangeRed,
                (float)Math.Sin(frameIndex / (float)maxFrame * MathHelper.Pi));
            drawColor.A = 0;

            //主光柱（减弱宽度）
            Main.EntitySpriteDraw(MaskLaserLine.Value, Projectile.Bottom - Main.screenPosition, null, drawColor
                , Projectile.rotation - MathHelper.PiOver2, MaskLaserLine.Value.Size() / 2
                , new Vector2(4000, Projectile.ai[1] * 0.04f * Projectile.scale), SpriteEffects.None, 0);

            //附加光柱增强效果（稍微弱化）
            Color accentColor = Color.Lerp(Color.Yellow, Color.White,
                (float)Math.Sin(Main.GlobalTimeWrappedHourly * 10f) * 0.5f + 0.5f);
            accentColor.A = 0;
            accentColor *= 0.5f;

            Main.EntitySpriteDraw(MaskLaserLine.Value, Projectile.Bottom - Main.screenPosition, null, accentColor
                , Projectile.rotation - MathHelper.PiOver2, MaskLaserLine.Value.Size() / 2
                , new Vector2(4000, Projectile.ai[1] * 0.025f * Projectile.scale), SpriteEffects.None, 0);
        }

        private void DrawMainExplosion(Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Rectangle rectangle = value.GetRectangle(frameIndex, maxFrame);
            Vector2 origin = new Vector2(rectangle.Width / 2, rectangle.Height);
            Vector2 drawPos = Projectile.Bottom - Main.screenPosition + new Vector2(0, -22 * Projectile.scale) + new Vector2(0, -Projectile.height / 3);
            //发光层
            Color glowColor = Color.Lerp(Color.Gold, Color.Yellow,
                (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f) * 0.5f + 0.5f);
            glowColor.A = 0;

            for (int i = 0; i < 2; i++) {//减少绘制层数
                float glowScale = Projectile.scale * (1f + i * 0.08f);
                float glowAlpha = (1f - i * 0.25f) * 0.4f;

                Main.spriteBatch.Draw(value, drawPos
                    , rectangle, glowColor * glowAlpha, Projectile.rotation, origin
                    , glowScale, SpriteEffects.None, 0);
            }

            //主体
            Main.spriteBatch.Draw(value, drawPos
                , rectangle, Color.White, Projectile.rotation, origin
                , Projectile.scale, SpriteEffects.None, 0);
        }
    }
}
