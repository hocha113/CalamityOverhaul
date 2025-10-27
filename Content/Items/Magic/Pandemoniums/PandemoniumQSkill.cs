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
    /// Q技能：硫磺火天罚，超必杀
    /// 召唤大量硫磺火柱从天而降，覆盖鼠标周围大范围区域
    /// </summary>
    internal class PandemoniumQSkill : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private Player Owner => Main.player[Projectile.owner];
        private ref float Phase => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];

        //技能参数
        private Vector2 targetCenter;
        private const float SkillRadius = 600f;
        private const int PillarCount = 20;
        private const int Duration = 180;

        //视觉效果
        private List<PillarSpawnData> pillarSpawnQueue = new();
        private List<RuneRingData> runeRings = new();
        private float warningIntensity = 0f;

        [VaultLoaden(CWRConstant.Masking + "Fire")]
        private static Asset<Texture2D> RuneAsset = null;
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> StarTexture = null;
        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        private static Asset<Texture2D> GlowAsset = null;

        private class PillarSpawnData
        {
            public Vector2 Position;
            public float SpawnTime;
            public bool Spawned;
        }

        private class RuneRingData
        {
            public Vector2 Position;
            public float Radius;
            public float Rotation;
            public float Alpha;
            public int FireFrame;
            public float FireFrameCounter;
        }

        public override void SetDefaults() {
            Projectile.width = 1200;
            Projectile.height = 1200;
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
                targetCenter = Main.MouseWorld;
                Projectile.Center = targetCenter;
                InitializeSkill();
                
                //超必杀启动音效
                SoundEngine.PlaySound(SoundID.DD2_BetsyScream with {
                    Volume = 1.5f,
                    Pitch = -0.6f
                }, targetCenter);
                
                SoundEngine.PlaySound(SoundID.DD2_DarkMageCastHeal with {
                    Volume = 1.3f,
                    Pitch = -0.5f
                }, targetCenter);
            }

            //阶段0：预警阶段 (0-60帧)
            if (Phase == 0) {
                WarningPhase();
                if (Timer >= 60) {
                    Phase = 1;
                    Timer = 0;
                }
            }
            //阶段1：火柱降临 (60-150帧)
            else if (Phase == 1) {
                PillarPhase();
                if (Timer >= 90) {
                    Phase = 2;
                }
            }
            //阶段2：余波 (150-180帧)
            else {
                AftermathPhase();
            }

            //生成火柱弹幕
            SpawnPillars();

            //超强照明
            float lightIntensity = 3f + warningIntensity * 2f;
            Lighting.AddLight(targetCenter, 2.5f * lightIntensity, 0.8f * lightIntensity, 0.4f * lightIntensity);
        }

        private void InitializeSkill() {
            //初始化火柱生成队列
            for (int i = 0; i < PillarCount; i++) {
                float angle = MathHelper.TwoPi * i / PillarCount + Main.rand.NextFloat(-0.2f, 0.2f);
                float distance = Main.rand.NextFloat(SkillRadius * 0.3f, SkillRadius);
                Vector2 pos = targetCenter + angle.ToRotationVector2() * distance;

                pillarSpawnQueue.Add(new PillarSpawnData {
                    Position = pos,
                    SpawnTime = 60f + i * 3f + Main.rand.NextFloat(0, 10f), //预警后开始生成
                    Spawned = false
                });
            }

            //初始化符文环
            for (int i = 0; i < 3; i++) {
                runeRings.Add(new RuneRingData {
                    Position = targetCenter,
                    Radius = 200f + i * 150f,
                    Rotation = 0,
                    Alpha = 0,
                    FireFrame = Main.rand.Next(16),
                    FireFrameCounter = 0
                });
            }
        }

        private void WarningPhase() {
            //预警强度脉冲
            warningIntensity = (float)Math.Sin(Timer * 0.2f) * 0.5f + 0.5f;

            //符文环淡入并旋转
            foreach (var ring in runeRings) {
                ring.Alpha = MathHelper.Lerp(ring.Alpha, 1f, 0.05f);
                ring.Rotation += 0.02f;

                //火焰帧更新
                ring.FireFrameCounter += 0.5f;
                if (ring.FireFrameCounter >= 1f) {
                    ring.FireFrameCounter = 0;
                    ring.FireFrame = (ring.FireFrame + 1) % 16;
                }
            }

            //预警粒子
            if (Main.rand.NextBool(2)) {
                SpawnWarningParticles();
            }

            //预警音效
            if (Timer % 20 == 0) {
                SoundEngine.PlaySound(SoundID.DD2_BetsyFireballImpact with {
                    Volume = 0.4f + warningIntensity * 0.3f,
                    Pitch = -0.7f + warningIntensity * 0.2f
                }, targetCenter);
            }
        }

        private void PillarPhase() {
            //符文环加速旋转
            foreach (var ring in runeRings) {
                ring.Rotation += 0.05f;
                ring.FireFrameCounter += 0.6f;
                if (ring.FireFrameCounter >= 1f) {
                    ring.FireFrameCounter = 0;
                    ring.FireFrame = (ring.FireFrame + 1) % 16;
                }
            }

            //持续火焰音效
            if (Timer % 10 == 0) {
                SoundEngine.PlaySound(SoundID.Item74 with {
                    Volume = 0.8f,
                    Pitch = Main.rand.NextFloat(-0.5f, -0.2f)
                }, targetCenter);
            }
        }

        private void AftermathPhase() {
            //符文环淡出
            foreach (var ring in runeRings) {
                ring.Alpha *= 0.95f;
                ring.Rotation += 0.03f;
                
                //更新火焰帧
                ring.FireFrameCounter += 0.4f;
                if (ring.FireFrameCounter >= 1f) {
                    ring.FireFrameCounter = 0;
                    ring.FireFrame = (ring.FireFrame + 1) % 16;
                }
            }

            warningIntensity *= 0.9f;
        }

        private void SpawnPillars() {
            if (Main.myPlayer != Projectile.owner) return;

            //遍历生成队列
            for (int i = pillarSpawnQueue.Count - 1; i >= 0; i--) {
                var data = pillarSpawnQueue[i];
                
                if (!data.Spawned && Projectile.timeLeft <= (Duration - data.SpawnTime)) {
                    //生成火柱弹幕
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        data.Position,
                        Vector2.Zero,
                        ModContent.ProjectileType<PandemoniumFirePillar>(),
                        Projectile.damage * 10,
                        Projectile.knockBack,
                        Projectile.owner
                    );
                    
                    data.Spawned = true;
                }
            }
        }

        private void SpawnWarningParticles() {
            for (int i = 0; i < 5; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float distance = Main.rand.NextFloat(SkillRadius);
                Vector2 pos = targetCenter + angle.ToRotationVector2() * distance;

                Dust warning = Dust.NewDustPerfect(
                    pos,
                    (int)CalamityDusts.Brimstone,
                    Vector2.UnitY * Main.rand.NextFloat(-2f, -0.5f),
                    0,
                    default,
                    Main.rand.NextFloat(2f, 3f)
                );
                warning.noGravity = true;
                warning.fadeIn = 1.5f;
            }
        }

        public override void OnKill(int timeLeft) {
            //终结爆发
            for (int i = 0; i < 100; i++) {
                float angle = MathHelper.TwoPi * i / 100f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(10f, 20f);

                Dust brimstone = Dust.NewDustPerfect(
                    targetCenter,
                    (int)CalamityDusts.Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(3f, 5f)
                );
                brimstone.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;

            //绘制预警区域
            if (Phase == 0) {
                DrawWarningArea(sb);
            }

            //绘制符文环
            DrawRuneRings(sb);

            return false;
        }

        private void DrawWarningArea(SpriteBatch sb) {
            if (!(GlowAsset?.IsLoaded ?? false)) return;

            Vector2 screenCenter = targetCenter - Main.screenPosition;
            float pulse = (float)Math.Sin(Timer * 0.3f) * 0.4f + 0.6f;

            //红色预警光晕
            for (int i = 0; i < 4; i++) {
                float scale = (SkillRadius / GlowAsset.Value.Width) * (2f + i * 0.3f);
                float alpha = warningIntensity * (0.3f - i * 0.06f) * pulse;

                sb.Draw(
                    GlowAsset.Value,
                    screenCenter,
                    null,
                    new Color(255, 50, 30) with { A = 0 } * alpha,
                    Timer * 0.01f + i * MathHelper.PiOver4,
                    GlowAsset.Value.Size() / 2,
                    scale,
                    SpriteEffects.None,
                    0
                );
            }
        }

        private void DrawRuneRings(SpriteBatch sb) {
            if (!(RuneAsset?.IsLoaded ?? false)) return;

            int frameWidth = RuneAsset.Value.Width / 4;
            int frameHeight = RuneAsset.Value.Height / 4;

            foreach (var ring in runeRings) {
                if (ring.Alpha < 0.01f) continue;

                Vector2 screenCenter = ring.Position - Main.screenPosition;
                int runeCount = 24;

                for (int i = 0; i < runeCount; i++) {
                    float angle = ring.Rotation + MathHelper.TwoPi * i / runeCount;
                    Vector2 pos = screenCenter + angle.ToRotationVector2() * ring.Radius;

                    int frameX = ring.FireFrame % 4;
                    int frameY = ring.FireFrame / 4;
                    Rectangle fireFrame = new Rectangle(frameX * frameWidth, frameY * frameHeight, frameWidth, frameHeight);

                    Color fireColor = new Color(180, 60, 40) * ring.Alpha;
                    fireColor.A = 0;

                    sb.Draw(
                        RuneAsset.Value,
                        pos,
                        fireFrame,
                        fireColor,
                        angle + MathHelper.PiOver2,
                        new Vector2(frameWidth, frameHeight) / 2f,
                        0.8f,
                        SpriteEffects.None,
                        0
                    );
                }
            }
        }
    }
}
