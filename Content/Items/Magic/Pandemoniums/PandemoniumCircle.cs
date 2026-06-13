using CalamityOverhaul.Common;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Pandemoniums
{
    /// <summary>
    /// 鼠标位置的硫磺火法阵
    /// </summary>
    internal class PandemoniumCircle : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;

        private ref float ExpandTimer => ref Projectile.ai[0];
        private ref float AttackTimer => ref Projectile.ai[1];

        //法阵视觉数据
        private List<RuneData> runes = new();
        private List<LightningData> lightnings = new();
        private float circleRadius = 0f;
        private float circleAlpha = 0f;
        private float rotationAngle = 0f;

        private class RuneData
        {
            public Vector2 Offset;
            public float Rotation;
            public float Scale;
            public float PulsePhase;
            public int FireFrame = 0;
            public float FireFrameCounter = 0;
            public float Alpha = 0f;
        }

        private class LightningData
        {
            public Vector2 StartPos;
            public Vector2 EndPos;
            public float Life;
            public float MaxLife;
            public List<Vector2> SegmentPoints;
            public float Intensity;
        }

        public override void SetDefaults() {
            Projectile.width = 400;
            Projectile.height = 400;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            var result = Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , Projectile.Center, Owner.Center, 120, ref point);
            if (result) {
                return true;
            }
            return base.Colliding(projHitbox, targetHitbox);
        }

        public override void AI() {
            ExpandTimer++;
            AttackTimer++;

            //初始化
            if (ExpandTimer == 1) {
                InitializeRunes();
                SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with {
                    Volume = 1f,
                    Pitch = -0.5f
                }, Projectile.Center);
            }

            //法阵扩展阶段 (0-30帧)
            if (ExpandTimer <= 30f) {
                float progress = ExpandTimer / 30f;
                circleRadius = VaultUtils.EaseOutCubic(progress) * 200f;
                circleAlpha = progress;
            }
            else {
                circleRadius = 200f;
                circleAlpha = 1f;
            }

            //法阵旋转
            rotationAngle += 0.02f;

            //更新符文
            UpdateRunes();

            //生成连接闪电
            if (AttackTimer % 8 == 0 && ExpandTimer > 15f) {
                SpawnPlayerLightning();
            }

            //更新闪电
            UpdateLightnings();

            //持续攻击敌人
            if (AttackTimer % 20 == 0 && ExpandTimer > 30f) {
                AttackNearbyEnemies();
            }

            //硫磺火照明
            float lightIntensity = circleAlpha * 2f;
            Lighting.AddLight(Projectile.Center, 1.5f * lightIntensity, 0.5f * lightIntensity, 0.2f * lightIntensity);

            //生成硫磺火粒子
            SpawnBrimstoneParticles();
        }

        private void InitializeRunes() {
            int runeCount = 12;
            for (int i = 0; i < runeCount; i++) {
                float angle = MathHelper.TwoPi * i / runeCount;
                runes.Add(new RuneData {
                    Offset = angle.ToRotationVector2() * 180f,
                    Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                    Scale = Main.rand.NextFloat(0.6f, 0.9f),
                    PulsePhase = Main.rand.NextFloat(MathHelper.TwoPi),
                    FireFrame = Main.rand.Next(16),
                    FireFrameCounter = 0,
                    Alpha = 0f
                });
            }
        }

        private void UpdateRunes() {
            foreach (var rune in runes) {
                //淡入
                rune.Alpha = MathHelper.Lerp(rune.Alpha, circleAlpha, 0.1f);

                //火焰帧更新
                rune.FireFrameCounter += 0.4f;
                if (rune.FireFrameCounter >= 1f) {
                    rune.FireFrameCounter = 0;
                    rune.FireFrame = (rune.FireFrame + 1) % 16;
                }

                //旋转
                rune.Rotation += 0.03f;
                rune.PulsePhase += 0.08f;
            }
        }

        private void SpawnPlayerLightning() {
            if (!Owner.active || Owner.dead) return;

            //生成从法阵到玩家的闪电
            List<Vector2> points = GenerateLightningPath(Projectile.Center, Owner.Center, 6);

            lightnings.Add(new LightningData {
                StartPos = Projectile.Center,
                EndPos = Owner.Center,
                Life = 0,
                MaxLife = 20,
                SegmentPoints = points,
                Intensity = Main.rand.NextFloat(0.8f, 1f)
            });

            //硫磺火闪电音效
            if (Main.rand.NextBool(3)) {
                SoundEngine.PlaySound(SoundID.Item122 with {
                    Volume = 0.4f,
                    Pitch = -0.3f
                }, Projectile.Center);
            }
        }

        private void UpdateLightnings() {
            for (int i = lightnings.Count - 1; i >= 0; i--) {
                var lightning = lightnings[i];
                lightning.Life++;

                //更新终点位置(跟随玩家)
                if (Owner.active && !Owner.dead) {
                    lightning.EndPos = Owner.Center;
                    lightning.SegmentPoints = GenerateLightningPath(lightning.StartPos, lightning.EndPos, 6);
                }

                if (lightning.Life >= lightning.MaxLife) {
                    lightnings.RemoveAt(i);
                }
            }
        }

        private List<Vector2> GenerateLightningPath(Vector2 start, Vector2 end, int segments) {
            List<Vector2> points = new List<Vector2> { start };
            Vector2 direction = end - start;
            float segmentLength = direction.Length() / segments;

            for (int i = 1; i < segments; i++) {
                float progress = i / (float)segments;
                Vector2 basePos = Vector2.Lerp(start, end, progress);
                Vector2 offset = Main.rand.NextVector2Circular(segmentLength * 0.3f, segmentLength * 0.3f);
                points.Add(basePos + offset);
            }

            points.Add(end);
            return points;
        }

        private void AttackNearbyEnemies() {
            float searchRadius = 400f;
            NPC closestNPC = null;
            float closestDistance = searchRadius;

            foreach (NPC npc in Main.npc) {
                if (!npc.active || !npc.CanBeChasedBy()) continue;

                float distance = Vector2.Distance(Projectile.Center, npc.Center);
                if (distance < closestDistance) {
                    closestDistance = distance;
                    closestNPC = npc;
                }
            }

            if (closestNPC != null && Main.myPlayer == Projectile.owner) {
                //发射硫磺火球
                Vector2 velocity = (closestNPC.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 12f;
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    velocity,
                    ModContent.ProjectileType<PandemoniumFireball>(),
                    Projectile.damage / 2,
                    Projectile.knockBack,
                    Projectile.owner,
                    0,
                    2 //标记为法阵火球
                );

                //生成攻击闪电视觉效果
                List<Vector2> lightningPath = GenerateLightningPath(Projectile.Center, closestNPC.Center, 5);
                lightnings.Add(new LightningData {
                    StartPos = Projectile.Center,
                    EndPos = closestNPC.Center,
                    Life = 0,
                    MaxLife = 15,
                    SegmentPoints = lightningPath,
                    Intensity = Main.rand.NextFloat(0.7f, 1f)
                });
            }
        }

        private void SpawnBrimstoneParticles() {
            if (Main.rand.NextBool(2)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float distance = Main.rand.NextFloat(circleRadius * 0.8f, circleRadius * 1.2f);
                Vector2 spawnPos = Projectile.Center + angle.ToRotationVector2() * distance;

                Dust brimstone = Dust.NewDustPerfect(
                    spawnPos,
                    CWRID.Dust_Brimstone,
                    Vector2.UnitY * Main.rand.NextFloat(-2f, -0.5f),
                    0,
                    default,
                    Main.rand.NextFloat(1.2f, 2f)
                );
                brimstone.noGravity = true;
            }

            //法阵边缘火焰
            if (Main.rand.NextBool(3)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 edgePos = Projectile.Center + angle.ToRotationVector2() * circleRadius;

                Dust fire = Dust.NewDustPerfect(
                    edgePos,
                    DustID.Torch,
                    Main.rand.NextVector2Circular(2f, 2f),
                    0,
                    Color.Red,
                    Main.rand.NextFloat(0.8f, 1.5f)
                );
                fire.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            //法阵消散特效
            for (int i = 0; i < 50; i++) {
                float angle = MathHelper.TwoPi * i / 50f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 8f);

                Dust brimstone = Dust.NewDustPerfect(
                    Projectile.Center,
                    CWRID.Dust_Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(2f, 3f)
                );
                brimstone.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item74 with {
                Volume = 0.8f,
                Pitch = -0.3f
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 center = Projectile.Center - Main.screenPosition;

            //绘制着色器法阵
            DrawBrimstoneDomainShader(sb, center);

            //绘制闪电
            DrawLightnings(sb);

            return false;
        }

        private void DrawBrimstoneDomainShader(SpriteBatch sb, Vector2 center) {
            Effect shader = EffectLoader.BrimstoneDomain?.Value;
            if (shader == null) return;

            Texture2D canvas = CWRAsset.Placeholder_White.Value;
            Texture2D noise = CWRAsset.Extra_193.Value;
            if (canvas == null || noise == null) return;

            //右键法阵较小，使用适当的绘制区域
            float drawRadius = circleRadius * 1.3f;
            float drawDiameter = drawRadius * 2f;

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.016f);
            shader.Parameters["fadeAlpha"]?.SetValue(circleAlpha);
            shader.Parameters["tierLevel"]?.SetValue(1.5f); //右键法阵固定中等层级
            shader.Parameters["expandProgress"]?.SetValue(MathHelper.Clamp(circleAlpha, 0f, 1f));
            shader.Parameters["pulseIntensity"]?.SetValue(0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.3f);

            //稍微柔和的色彩（区分于主法阵）
            shader.Parameters["coreColor"]?.SetValue(new Vector3(1f, 0.31f, 0.16f));
            shader.Parameters["midColor"]?.SetValue(new Vector3(0.78f, 0.2f, 0.12f));
            shader.Parameters["edgeColor"]?.SetValue(new Vector3(0.47f, 0.12f, 0.08f));
            shader.Parameters["voidColor"]?.SetValue(new Vector3(0.16f, 0.04f, 0.04f));
            shader.Parameters["uNoiseTex"]?.SetValue(noise);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            shader.CurrentTechnique.Passes[0].Apply();

            sb.Draw(canvas, center, null, Color.White,
                0f, canvas.Size() * 0.5f, new Vector2(drawDiameter, drawDiameter),
                SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
        }

        private void DrawLightnings(SpriteBatch sb) {
            Texture2D pixel = CWRAsset.Placeholder_White.Value;

            foreach (var lightning in lightnings) {
                float alpha = 1f - (lightning.Life / lightning.MaxLife);
                Color lightningColor = new Color(255, 140, 80, 200) * alpha * lightning.Intensity;

                if (lightning.SegmentPoints != null && lightning.SegmentPoints.Count > 1) {
                    for (int i = 0; i < lightning.SegmentPoints.Count - 1; i++) {
                        Vector2 start = lightning.SegmentPoints[i] - Main.screenPosition;
                        Vector2 end = lightning.SegmentPoints[i + 1] - Main.screenPosition;

                        DrawLine(sb, pixel, start, end, 2.5f, lightningColor);
                        DrawLine(sb, pixel, start, end, 5f, lightningColor * 0.3f);
                    }
                }
            }
        }

        private static void DrawLine(SpriteBatch sb, Texture2D pixel, Vector2 start, Vector2 end, float thickness, Color color) {
            Vector2 diff = end - start;
            float length = diff.Length();
            if (length < 1f) return;
            sb.Draw(pixel, start, new Rectangle(0, 0, 1, 1), color, diff.ToRotation(),
                Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
        }
    }
}
