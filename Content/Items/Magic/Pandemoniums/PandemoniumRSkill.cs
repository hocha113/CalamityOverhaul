using CalamityMod.Dusts;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Pandemoniums
{
    /// <summary>
    /// R技能：万魔终焉，终极必杀
    /// 在玩家周围生成巨型硫磺火法阵，召唤无数闪电和火球轰炸全屏敌人
    /// </summary>
    internal class PandemoniumRSkill : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private Player Owner => Main.player[Projectile.owner];
        private ref float Phase => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];

        //技能参数
        private const int Duration = 300; //5秒持续时间
        private const float MaxCircleRadius = 1200f;
        private float currentRadius = 0f;

        //视觉效果
        private List<RuneLayerData> runeLayers = new();
        private List<LightningStrikeData> lightningStrikes = new();
        private List<FireOrbData> fireOrbs = new();
        private float intensity = 0f;
        private Vector2 centerPos;

        [VaultLoaden(CWRConstant.Masking + "Fire")]
        private static Asset<Texture2D> RuneAsset = null;
        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        private static Asset<Texture2D> GlowAsset = null;

        private class RuneLayerData
        {
            public float Radius;
            public float Rotation;
            public int RuneCount;
            public float Alpha;
            public int FireFrame;
            public float FireFrameCounter;
            public float RotationSpeed;
        }

        private class LightningStrikeData
        {
            public Vector2 TargetPos;
            public float Life;
            public float MaxLife;
            public List<Vector2> LightningPath;
            public Color LightningColor;
        }

        private class FireOrbData
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public Color OrbColor;
            public float Scale;
        }

        public override void SetDefaults() {
            Projectile.width = 2400;
            Projectile.height = 2400;
            Projectile.friendly = false; //控制器本身不造成伤害
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Duration;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Timer++;

            //初始化
            if (Timer == 1) {
                centerPos = Owner.Center;
                Projectile.Center = centerPos;
                InitializeSkill();

                //终极必杀启动音效组合
                SoundEngine.PlaySound(SoundID.DD2_BetsyScream with {
                    Volume = 2f,
                    Pitch = -0.8f
                }, centerPos);

                SoundEngine.PlaySound(SoundID.DD2_DarkMageSummonSkeleton with {
                    Volume = 1.8f,
                    Pitch = -0.6f
                }, centerPos);

                SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with {
                    Volume = 1.5f,
                    Pitch = -0.7f
                }, centerPos);
            }

            //跟随玩家
            centerPos = Owner.Center;
            Projectile.Center = centerPos;

            //阶段控制
            if (Phase == 0) {
                //启动阶段 (0-60帧)
                StartupPhase();
                if (Timer >= 60) Phase = 1;
            }
            else if (Phase == 1) {
                //主攻击阶段 (60-240帧)
                MainAttackPhase();
                if (Timer >= 240) Phase = 2;
            }
            else {
                //结束阶段 (240-300帧)
                EndPhase();
            }

            //更新所有效果
            UpdateRuneLayers();
            UpdateLightningStrikes();
            UpdateFireOrbs();

            //生成攻击
            if (Phase == 1) {
                SpawnAttacks();
            }

            //超强照明
            float lightPulse = (float)Math.Sin(Timer * 0.1f) * 0.3f + 0.7f;
            float lightIntensity = intensity * 5f * lightPulse;
            Lighting.AddLight(centerPos, 3f * lightIntensity, 1f * lightIntensity, 0.5f * lightIntensity);

            //屏幕震动
            if (Phase == 1) {
                Owner.GetModPlayer<CWRPlayer>().ScreenShakeValue = Math.Max(
                    Owner.GetModPlayer<CWRPlayer>().ScreenShakeValue,
                    intensity * 4f * (float)Math.Sin(Timer * 0.2f)
                );
            }
        }

        private void InitializeSkill() {
            //初始化5层符文环
            for (int i = 0; i < 5; i++) {
                runeLayers.Add(new RuneLayerData {
                    Radius = 300f + i * 200f,
                    Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                    RuneCount = 16 + i * 8,
                    Alpha = 0f,
                    FireFrame = Main.rand.Next(16),
                    FireFrameCounter = 0,
                    RotationSpeed = 0.02f * (i % 2 == 0 ? 1 : -1)
                });
            }
        }

        private void StartupPhase() {
            float progress = Timer / 60f;
            intensity = CWRUtils.EaseOutCubic(progress);
            currentRadius = MaxCircleRadius * intensity;

            //符文环淡入
            foreach (var layer in runeLayers) {
                layer.Alpha = intensity;
            }

            //启动粒子
            if (Main.rand.NextBool(2)) {
                SpawnStartupParticles();
            }

            //启动音效
            if (Timer % 15 == 0) {
                SoundEngine.PlaySound(SoundID.Item74 with {
                    Volume = 0.5f + intensity * 0.5f,
                    Pitch = -0.6f + intensity * 0.3f
                }, centerPos);
            }
        }

        private void MainAttackPhase() {
            intensity = 1f;
            currentRadius = MaxCircleRadius;

            //主阶段持续音效
            if (Timer % 30 == 0) {
                SoundEngine.PlaySound(SoundID.DD2_BetsyFireballImpact with {
                    Volume = 0.8f,
                    Pitch = Main.rand.NextFloat(-0.6f, -0.3f)
                }, centerPos);
            }
        }

        private void EndPhase() {
            float progress = (Timer - 240f) / 60f;
            intensity = 1f - CWRUtils.EaseInCubic(progress);
            currentRadius = MaxCircleRadius * intensity;

            //符文环淡出
            foreach (var layer in runeLayers) {
                layer.Alpha = intensity;
            }
        }

        private void UpdateRuneLayers() {
            foreach (var layer in runeLayers) {
                layer.Rotation += layer.RotationSpeed * intensity;

                //火焰帧更新
                layer.FireFrameCounter += 0.5f + intensity * 0.3f;
                if (layer.FireFrameCounter >= 1f) {
                    layer.FireFrameCounter = 0;
                    layer.FireFrame = (layer.FireFrame + 1) % 16;
                }
            }
        }

        private void UpdateLightningStrikes() {
            for (int i = lightningStrikes.Count - 1; i >= 0; i--) {
                var lightning = lightningStrikes[i];
                lightning.Life++;

                if (lightning.Life >= lightning.MaxLife) {
                    lightningStrikes.RemoveAt(i);
                }
            }
        }

        private void UpdateFireOrbs() {
            for (int i = fireOrbs.Count - 1; i >= 0; i--) {
                var orb = fireOrbs[i];
                orb.Life++;
                orb.Position += orb.Velocity;

                //寻找敌人
                NPC closestNPC = null;
                float closestDist = 400f;

                foreach (NPC npc in Main.npc) {
                    if (!npc.active || !npc.CanBeChasedBy()) continue;
                    float dist = Vector2.Distance(orb.Position, npc.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closestNPC = npc;
                    }
                }

                if (closestNPC != null) {
                    Vector2 toTarget = (closestNPC.Center - orb.Position).SafeNormalize(Vector2.Zero);
                    orb.Velocity = Vector2.Lerp(orb.Velocity, toTarget * 15f, 0.08f);
                }

                if (orb.Life >= orb.MaxLife) {
                    fireOrbs.RemoveAt(i);
                }
            }
        }

        private void SpawnAttacks() {
            if (Main.myPlayer != Projectile.owner) return;

            //每2帧生成一次攻击
            if (Timer % 2 == 0) {
                //寻找目标
                List<NPC> validTargets = new();
                foreach (NPC npc in Main.npc) {
                    if (npc.active && npc.CanBeChasedBy() && npc.Distance(centerPos) < MaxCircleRadius) {
                        validTargets.Add(npc);
                    }
                }

                if (validTargets.Count > 0) {
                    NPC target = validTargets[Main.rand.Next(validTargets.Count)];

                    //随机选择攻击方式
                    int attackType = Main.rand.Next(3);

                    switch (attackType) {
                        case 0: //闪电打击
                            SpawnLightningStrike(target);
                            break;
                        case 1: //火球轰炸
                            SpawnFireballBarrage(target);
                            break;
                        case 2: //组合攻击
                            SpawnLightningStrike(target);
                            SpawnFireOrb(target.Center);
                            break;
                    }
                }
            }

            //环境火球
            if (Timer % 5 == 0) {
                SpawnAmbientFireOrbs();
            }
        }

        private void SpawnLightningStrike(NPC target) {
            //生成闪电弹幕
            Vector2 strikePos = target.Center + new Vector2(Main.rand.NextFloat(-50f, 50f), -800f);

            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                strikePos,
                Vector2.Zero,
                ModContent.ProjectileType<PandemoniumLightning>(),
                (int)(Projectile.damage * 11.5f),
                Projectile.knockBack,
                Projectile.owner,
                1, //标记为R技能闪电
                target.whoAmI
            );

            //视觉闪电
            List<Vector2> lightningPath = GenerateLightningPath(strikePos, target.Center, 8);
            lightningStrikes.Add(new LightningStrikeData {
                TargetPos = target.Center,
                Life = 0,
                MaxLife = 20,
                LightningPath = lightningPath,
                LightningColor = new Color(255, 140, 80)
            });

            //闪电音效
            if (Main.rand.NextBool(3)) {
                SoundEngine.PlaySound(SoundID.Item122 with {
                    Volume = 0.7f,
                    Pitch = Main.rand.NextFloat(-0.4f, -0.1f)
                }, target.Center);
            }
        }

        private void SpawnFireballBarrage(NPC target) {
            //发射3个火球
            for (int i = 0; i < 3; i++) {
                Vector2 spawnOffset = new Vector2(Main.rand.NextFloat(-200f, 200f), -Main.rand.NextFloat(400f, 600f));
                Vector2 spawnPos = target.Center + spawnOffset;
                Vector2 velocity = (target.Center - spawnPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(12f, 16f);

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    spawnPos,
                    velocity,
                    ModContent.ProjectileType<PandemoniumFireball>(),
                    Projectile.damage,
                    Projectile.knockBack,
                    Projectile.owner,
                    0,
                    3 //标记为R技能火球
                );
            }
        }

        private void SpawnFireOrb(Vector2 targetPos) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 spawnPos = centerPos + angle.ToRotationVector2() * Main.rand.NextFloat(400f, 800f);
            Vector2 velocity = (targetPos - spawnPos).SafeNormalize(Vector2.Zero) * 8f;

            fireOrbs.Add(new FireOrbData {
                Position = spawnPos,
                Velocity = velocity,
                Life = 0,
                MaxLife = 120,
                OrbColor = Main.rand.Next(3) switch {
                    0 => new Color(255, 140, 70),
                    1 => new Color(255, 100, 50),
                    _ => new Color(200, 60, 30)
                },
                Scale = Main.rand.NextFloat(1.2f, 1.8f)
            });
        }

        private void SpawnAmbientFireOrbs() {
            for (int i = 0; i < 3; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 spawnPos = centerPos + angle.ToRotationVector2() * Main.rand.NextFloat(600f, 1000f);

                fireOrbs.Add(new FireOrbData {
                    Position = spawnPos,
                    Velocity = (centerPos - spawnPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(3f, 6f),
                    Life = 0,
                    MaxLife = 100,
                    OrbColor = new Color(255, 120, 60),
                    Scale = Main.rand.NextFloat(0.8f, 1.2f)
                });
            }
        }

        private void SpawnStartupParticles() {
            for (int i = 0; i < 8; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float distance = Main.rand.NextFloat(currentRadius * 0.5f, currentRadius);
                Vector2 pos = centerPos + angle.ToRotationVector2() * distance;

                Dust brimstone = Dust.NewDustPerfect(
                    pos,
                    (int)CalamityDusts.Brimstone,
                    Vector2.UnitY * Main.rand.NextFloat(-3f, -1f),
                    0,
                    default,
                    Main.rand.NextFloat(2f, 4f)
                );
                brimstone.noGravity = true;
                brimstone.fadeIn = 2f;
            }
        }

        private List<Vector2> GenerateLightningPath(Vector2 start, Vector2 end, int segments) {
            List<Vector2> points = new List<Vector2> { start };
            float segmentLength = Vector2.Distance(start, end) / segments;

            for (int i = 1; i < segments; i++) {
                float progress = i / (float)segments;
                Vector2 basePos = Vector2.Lerp(start, end, progress);
                Vector2 offset = Main.rand.NextVector2Circular(segmentLength * 0.4f, segmentLength * 0.4f);
                points.Add(basePos + offset);
            }

            points.Add(end);
            return points;
        }

        public override void OnKill(int timeLeft) {
            //终结大爆发
            for (int i = 0; i < 200; i++) {
                float angle = MathHelper.TwoPi * i / 200f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(15f, 30f);

                Dust brimstone = Dust.NewDustPerfect(
                    centerPos,
                    (int)CalamityDusts.Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(4f, 6f)
                );
                brimstone.noGravity = true;
            }

            //终结音效
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with {
                Volume = 2f,
                Pitch = -0.7f
            }, centerPos);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 screenCenter = centerPos - Main.screenPosition;

            //绘制底层暗影
            DrawBaseGlow(sb, screenCenter);

            //绘制符文层
            DrawRuneLayers(sb, screenCenter);

            //绘制几何图形
            DrawGeometry(sb, screenCenter);

            //绘制闪电
            DrawLightningStrikes(sb);

            //绘制火球
            DrawFireOrbs(sb);

            //绘制顶层辉光
            DrawTopGlow(sb, screenCenter);

            return false;
        }

        private void DrawBaseGlow(SpriteBatch sb, Vector2 screenCenter) {
            if (!(GlowAsset?.IsLoaded ?? false)) return;

            //多层暗影光晕
            for (int i = 0; i < 6; i++) {
                float scale = (currentRadius / GlowAsset.Value.Width) * (3f + i * 0.4f);
                float alpha = intensity * (0.2f - i * 0.03f);
                float rotation = Timer * 0.005f * (i % 2 == 0 ? 1 : -1);

                sb.Draw(
                    GlowAsset.Value,
                    screenCenter,
                    null,
                    new Color(40, 10, 10) with { A = 0 } * alpha,
                    rotation,
                    GlowAsset.Value.Size() / 2,
                    scale,
                    SpriteEffects.None,
                    0
                );
            }
        }

        private void DrawRuneLayers(SpriteBatch sb, Vector2 screenCenter) {
            if (!(RuneAsset?.IsLoaded ?? false)) return;

            int frameWidth = RuneAsset.Value.Width / 4;
            int frameHeight = RuneAsset.Value.Height / 4;

            foreach (var layer in runeLayers) {
                if (layer.Alpha < 0.01f) continue;

                for (int i = 0; i < layer.RuneCount; i++) {
                    float angle = layer.Rotation + MathHelper.TwoPi * i / layer.RuneCount;
                    Vector2 pos = screenCenter + angle.ToRotationVector2() * layer.Radius * (currentRadius / MaxCircleRadius);

                    int frameX = layer.FireFrame % 4;
                    int frameY = layer.FireFrame / 4;
                    Rectangle fireFrame = new Rectangle(frameX * frameWidth, frameY * frameHeight, frameWidth, frameHeight);

                    float pulse = (float)Math.Sin(Timer * 0.1f + i) * 0.3f + 0.7f;
                    Color fireColor = new Color(180, 60, 40) * layer.Alpha * pulse;
                    fireColor.A = 0;

                    sb.Draw(
                        RuneAsset.Value,
                        pos,
                        fireFrame,
                        fireColor,
                        angle + MathHelper.PiOver2,
                        new Vector2(frameWidth, frameHeight) / 2f,
                        0.9f,
                        SpriteEffects.None,
                        0
                    );
                }
            }
        }

        private void DrawGeometry(SpriteBatch sb, Vector2 screenCenter) {
            Texture2D pixel = CWRAsset.Placeholder_White.Value;

            //绘制多个旋转的几何图形
            float rotation1 = Timer * 0.01f;
            float rotation2 = Timer * -0.015f;

            Color geometryColor = new Color(120, 30, 20) * intensity;

            //外层大六芒星
            DrawHexagram(sb, pixel, screenCenter, currentRadius * 0.8f, 4f, geometryColor * 0.6f, rotation1);

            //中层五芒星
            DrawPentagram(sb, pixel, screenCenter, currentRadius * 0.6f, 3.5f, geometryColor * 0.7f, rotation2);

            //内层八边形
            DrawOctagon(sb, pixel, screenCenter, currentRadius * 0.4f, 3f, geometryColor * 0.8f, rotation1 * 1.5f);
        }

        private void DrawLightningStrikes(SpriteBatch sb) {
            Texture2D pixel = CWRAsset.Placeholder_White.Value;

            foreach (var lightning in lightningStrikes) {
                float alpha = 1f - (lightning.Life / lightning.MaxLife);

                if (lightning.LightningPath != null && lightning.LightningPath.Count > 1) {
                    for (int i = 0; i < lightning.LightningPath.Count - 1; i++) {
                        Vector2 start = lightning.LightningPath[i] - Main.screenPosition;
                        Vector2 end = lightning.LightningPath[i + 1] - Main.screenPosition;

                        DrawLine(sb, pixel, start, end, 3f, lightning.LightningColor * alpha);
                        DrawLine(sb, pixel, start, end, 6f, lightning.LightningColor * alpha * 0.3f);
                    }
                }
            }
        }

        private void DrawFireOrbs(SpriteBatch sb) {
            if (!(GlowAsset?.IsLoaded ?? false)) return;

            foreach (var orb in fireOrbs) {
                Vector2 drawPos = orb.Position - Main.screenPosition;
                float lifeRatio = 1f - (orb.Life / orb.MaxLife);
                float scale = orb.Scale * lifeRatio;

                //外层辉光
                sb.Draw(
                    GlowAsset.Value,
                    drawPos,
                    null,
                    orb.OrbColor with { A = 0 } * lifeRatio * 0.6f,
                    orb.Life * 0.1f,
                    GlowAsset.Value.Size() / 2,
                    scale * 0.5f,
                    SpriteEffects.None,
                    0
                );

                //核心
                sb.Draw(
                    GlowAsset.Value,
                    drawPos,
                    null,
                    Color.White with { A = 0 } * lifeRatio * 0.4f,
                    orb.Life * 0.15f,
                    GlowAsset.Value.Size() / 2,
                    scale * 0.3f,
                    SpriteEffects.None,
                    0
                );
            }
        }

        private void DrawTopGlow(SpriteBatch sb, Vector2 screenCenter) {
            if (!(GlowAsset?.IsLoaded ?? false)) return;

            float pulse = (float)Math.Sin(Timer * 0.15f) * 0.4f + 0.6f;

            //核心辉光
            for (int i = 0; i < 4; i++) {
                float scale = (currentRadius / GlowAsset.Value.Width) * (1.5f + i * 0.3f);
                float alpha = intensity * pulse * (0.4f - i * 0.08f);
                float rotation = -Timer * 0.02f * (i + 1);

                Color glowColor = i switch {
                    0 => new Color(255, 80, 40),
                    1 => new Color(200, 50, 30),
                    2 => new Color(150, 40, 25),
                    _ => new Color(100, 25, 15)
                };

                sb.Draw(
                    GlowAsset.Value,
                    screenCenter,
                    null,
                    glowColor with { A = 0 } * alpha,
                    rotation,
                    GlowAsset.Value.Size() / 2,
                    scale,
                    SpriteEffects.None,
                    0
                );
            }
        }

        private void DrawHexagram(SpriteBatch sb, Texture2D pixel, Vector2 center, float radius, float thickness, Color color, float rotation) {
            for (int t = 0; t < 2; t++) {
                Vector2[] vertices = new Vector2[3];
                for (int i = 0; i < 3; i++) {
                    float angle = rotation + (t * MathHelper.Pi) + i * MathHelper.TwoPi / 3f;
                    vertices[i] = center + angle.ToRotationVector2() * radius;
                }

                for (int i = 0; i < 3; i++) {
                    DrawLine(sb, pixel, vertices[i], vertices[(i + 1) % 3], thickness, color);
                }
            }
        }

        private void DrawPentagram(SpriteBatch sb, Texture2D pixel, Vector2 center, float radius, float thickness, Color color, float rotation) {
            int points = 5;
            Vector2[] vertices = new Vector2[points];

            for (int i = 0; i < points; i++) {
                float angle = rotation + i * MathHelper.TwoPi / points - MathHelper.PiOver2;
                vertices[i] = center + angle.ToRotationVector2() * radius;
            }

            for (int i = 0; i < points; i++) {
                DrawLine(sb, pixel, vertices[i], vertices[(i + 2) % points], thickness, color);
            }
        }

        private void DrawOctagon(SpriteBatch sb, Texture2D pixel, Vector2 center, float radius, float thickness, Color color, float rotation) {
            int sides = 8;
            Vector2 prev = center + rotation.ToRotationVector2() * radius;

            for (int i = 1; i <= sides; i++) {
                float angle = rotation + i * MathHelper.TwoPi / sides;
                Vector2 curr = center + angle.ToRotationVector2() * radius;
                DrawLine(sb, pixel, prev, curr, thickness, color);
                prev = curr;
            }
        }

        private void DrawLine(SpriteBatch sb, Texture2D pixel, Vector2 start, Vector2 end, float thickness, Color color) {
            Vector2 diff = end - start;
            float length = diff.Length();
            if (length < 1f) return;

            sb.Draw(
                pixel,
                start,
                new Rectangle(0, 0, 1, 1),
                color,
                diff.ToRotation(),
                Vector2.Zero,
                new Vector2(length, thickness),
                SpriteEffects.None,
                0f
            );
        }
    }
}
