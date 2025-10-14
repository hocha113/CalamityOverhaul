using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>
    /// 诅咒火鱼技能，喷射诅咒火焰
    /// </summary>
    internal class FishCursed : FishSkill
    {
        public override int UnlockFishID => ItemID.Cursedfish;
        public override int DefaultCooldown => 90 - HalibutData.GetDomainLayer() * 6;

        //火焰喷射计数器
        private int flameCounter = 0;
        private const int FlameInterval = 8;

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            flameCounter++;

            //周期性释放诅咒火焰
            if (flameCounter >= FlameInterval && Cooldown <= 0) {
                flameCounter = 0;
                SetCooldown();

                //发射诅咒火焰
                Vector2 shootDir = velocity.SafeNormalize(Vector2.Zero);
                float spreadBase = 0.25f;

                //根据领域层数增加火焰数量和扩散
                int flameCount = 2 + HalibutData.GetDomainLayer() / 2;

                for (int i = 0; i < flameCount; i++) {
                    float spreadAngle = MathHelper.Lerp(-spreadBase, spreadBase, i / (float)Math.Max(1, flameCount - 1));
                    Vector2 flameVelocity = shootDir.RotatedBy(spreadAngle) * Main.rand.NextFloat(16f, 22f);

                    Projectile.NewProjectile(
                        source,
                        position,
                        flameVelocity,
                        ModContent.ProjectileType<CursedFlameStream>(),
                        (int)(damage * (3f + HalibutData.GetDomainLayer() * 0.8f)),
                        knockback * 2f,
                        player.whoAmI
                    );
                }

                //火焰喷射音效
                SoundEngine.PlaySound(SoundID.Item34 with {
                    Volume = 0.7f,
                    Pitch = -0.4f
                }, position);

                SoundEngine.PlaySound(SoundID.DD2_BetsyFlameBreath with {
                    Volume = 0.5f,
                    Pitch = -0.3f
                }, position);

                //喷射口火焰爆发效果
                SpawnMuzzleFlare(position, shootDir);
            }

            return null;
        }

        //喷射口火焰效果
        private static void SpawnMuzzleFlare(Vector2 position, Vector2 direction) {
            //诅咒火焰爆发
            for (int i = 0; i < 15; i++) {
                float angle = direction.ToRotation() + Main.rand.NextFloat(-0.5f, 0.5f);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 10f);

                Dust flame = Dust.NewDustPerfect(
                    position,
                    DustID.CursedTorch,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                flame.noGravity = true;
                flame.fadeIn = 1.3f;
            }

            //烟雾
            for (int i = 0; i < 8; i++) {
                Vector2 velocity = direction * Main.rand.NextFloat(3f, 7f);
                Dust smoke = Dust.NewDustPerfect(
                    position,
                    DustID.Smoke,
                    velocity,
                    100,
                    new Color(50, 80, 50),
                    Main.rand.NextFloat(1.2f, 2f)
                );
                smoke.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 全局弹幕钩子，添加诅咒火焰效果
    /// </summary>
    internal class FishCursedGlobalProj : GlobalProjectile
    {
        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone) {
            if (projectile.owner.TryGetPlayer(out var player)
                && FishSkill.GetT<FishCursed>().Active(player)) {
                //在这个技能下攻击会附加诅咒火焰效果
                int buffDuration = 240 + HalibutData.GetDomainLayer() * 30;
                target.AddBuff(BuffID.CursedInferno, buffDuration);

                //诅咒火焰感染粒子效果
                SpawnCursedInfectionEffect(target.Center);
            }
        }

        private static void SpawnCursedInfectionEffect(Vector2 position) {
            //绿色诅咒火焰爆发
            for (int i = 0; i < 10; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(5f, 5f);
                Dust cursed = Dust.NewDustPerfect(
                    position + Main.rand.NextVector2Circular(20f, 20f),
                    DustID.CursedTorch,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.3f, 2f)
                );
                cursed.noGravity = true;
                cursed.fadeIn = 1.2f;
            }
        }
    }

    /// <summary>
    /// 诅咒火焰流弹幕
    /// </summary>
    internal class CursedFlameStream : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        //火焰状态
        private enum FlameState
        {
            Burning,    //燃烧状态
            Fading      //消散状态
        }

        private FlameState State {
            get => (FlameState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float FlameLife => ref Projectile.ai[1];

        //火焰粒子系统
        private readonly List<CursedFlameParticle> flameParticles = new();
        private const int MaxParticles = 120;
        private int particleSpawnCounter = 0;

        //火焰拖尾系统
        private readonly List<FlameTrail> trailParticles = new();
        private const int MaxTrailParticles = 80;

        //火焰物理参数
        private const float AirResistance = 0.99f;    //空气阻力
        private const float HeatRise = -0.15f;        //热力上升
        private const float Turbulence = 0.08f;       //湍流强度

        //视觉效果
        private float glowPulse = 0f;
        private float heatDistortion = 0f;
        private readonly List<Vector2> coreTrail = new();
        private const int MaxCoreTrail = 20;

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.penetrate = 4;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.alpha = 255;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI() {
            FlameLife++;

            if (State == FlameState.Burning) {
                BurningPhaseAI();
            }
            else if (State == FlameState.Fading) {
                FadingPhaseAI();
            }

            //更新核心拖尾
            UpdateCoreTrail();

            //更新所有火焰粒子
            UpdateFlameParticles();

            //更新拖尾粒子
            UpdateTrailParticles();

            //辉光脉冲
            glowPulse = (float)Math.Sin(FlameLife * 0.4f) * 0.3f + 0.7f;

            //热力扭曲效果
            heatDistortion = (float)Math.Sin(FlameLife * 0.2f) * 0.5f;

            //诅咒火焰绿色照明
            float lightIntensity = 0.9f * (1f - Projectile.alpha / 255f);
            Lighting.AddLight(Projectile.Center,
                0.3f * lightIntensity,
                1.0f * lightIntensity,
                0.3f * lightIntensity);
        }

        //燃烧状态AI
        private void BurningPhaseAI() {
            //热力上升效果
            Projectile.velocity.Y += HeatRise * 0.5f;

            //空气阻力
            Projectile.velocity *= AirResistance;

            //湍流扰动
            if (FlameLife % 3 == 0) {
                Projectile.velocity += Main.rand.NextVector2Circular(Turbulence, Turbulence);
            }

            //生成火焰粒子
            if (particleSpawnCounter++ % 1 == 0 && flameParticles.Count < MaxParticles) {
                SpawnFlameParticle();
            }

            //生成拖尾粒子
            if (FlameLife % 2 == 0 && trailParticles.Count < MaxTrailParticles) {
                SpawnTrailParticle();
            }

            //周期性诅咒火焰残留
            if (FlameLife % 3 == 0) {
                SpawnCursedResidue();
            }

            //火焰燃烧音效
            if (FlameLife % 25 == 0) {
                SoundEngine.PlaySound(SoundID.Item74 with {
                    Volume = 0.3f,
                    Pitch = -0.5f
                }, Projectile.Center);
            }

            //超时转换为消散
            if (FlameLife > 100 || Projectile.velocity.Length() < 1.5f) {
                EnterFadeState();
            }
        }

        //消散状态AI
        private void FadingPhaseAI() {
            //快速消散
            Projectile.alpha += 12;
            if (Projectile.alpha >= 255) {
                Projectile.Kill();
            }

            //粒子继续更新但不生成新的
            Projectile.velocity *= 0.92f;
        }

        //生成火焰粒子
        private void SpawnFlameParticle() {
            Vector2 baseVel = Projectile.velocity;
            Vector2 particleVel = baseVel * Main.rand.NextFloat(0.3f, 0.9f);
            particleVel += Main.rand.NextVector2Circular(2.5f, 2.5f);
            particleVel.Y += HeatRise * Main.rand.NextFloat(1f, 2f);

            CursedFlameParticle particle = new CursedFlameParticle {
                Position = Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                Velocity = particleVel,
                Size = Main.rand.NextFloat(1.5f, 3.5f),
                Life = 0,
                MaxLife = Main.rand.Next(20, 40),
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                RotationSpeed = Main.rand.NextFloat(-0.2f, 0.2f),
                Opacity = 1f,
                ColorPhase = Main.rand.NextFloat(0f, 1f),
                HeatIntensity = Main.rand.NextFloat(0.7f, 1f)
            };

            flameParticles.Add(particle);
        }

        //生成拖尾粒子
        private void SpawnTrailParticle() {
            Vector2 spawnPos = Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(8f, 20f);
            Vector2 particleVel = Projectile.velocity * Main.rand.NextFloat(0.4f, 0.7f);
            particleVel.Y += HeatRise * 1.5f;

            FlameTrail trail = new FlameTrail {
                Position = spawnPos + Main.rand.NextVector2Circular(8f, 8f),
                Velocity = particleVel,
                Size = Main.rand.NextFloat(1.2f, 2.5f),
                Life = 0,
                MaxLife = Main.rand.Next(12, 25),
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                RotationSpeed = Main.rand.NextFloat(-0.25f, 0.25f),
                Opacity = 0.8f,
                ColorPhase = Main.rand.NextFloat(0f, 1f)
            };

            trailParticles.Add(trail);
        }

        //更新所有火焰粒子
        private void UpdateFlameParticles() {
            for (int i = flameParticles.Count - 1; i >= 0; i--) {
                CursedFlameParticle p = flameParticles[i];
                p.Life++;

                //火焰物理
                p.Velocity.Y += HeatRise * p.HeatIntensity;
                p.Velocity *= 0.96f;

                //湍流效果
                if (p.Life % 2 == 0) {
                    p.Velocity += Main.rand.NextVector2Circular(Turbulence * 2f, Turbulence * 2f);
                }

                p.Position += p.Velocity;
                p.Rotation += p.RotationSpeed;

                //颜色相位变化
                p.ColorPhase += 0.03f;
                if (p.ColorPhase > 1f) p.ColorPhase = 0f;

                //透明度衰减
                float lifeRatio = p.Life / (float)p.MaxLife;
                p.Opacity = (1f - lifeRatio) * p.HeatIntensity;

                //尺寸变化-先膨胀后收缩
                if (lifeRatio < 0.3f) {
                    p.Size *= 1.02f;
                }
                else {
                    p.Size *= 0.97f;
                }

                //移除消逝的粒子
                if (p.Life >= p.MaxLife || p.Opacity <= 0.05f) {
                    flameParticles.RemoveAt(i);
                    continue;
                }

                flameParticles[i] = p;
            }
        }

        //更新拖尾粒子
        private void UpdateTrailParticles() {
            for (int i = trailParticles.Count - 1; i >= 0; i--) {
                FlameTrail p = trailParticles[i];
                p.Life++;

                //热力上升
                p.Velocity.Y += HeatRise * 1.5f;
                p.Velocity *= 0.94f;

                p.Position += p.Velocity;
                p.Rotation += p.RotationSpeed;

                //颜色相位
                p.ColorPhase += 0.04f;
                if (p.ColorPhase > 1f) p.ColorPhase = 0f;

                //透明度衰减
                float lifeRatio = p.Life / (float)p.MaxLife;
                p.Opacity = (1f - lifeRatio) * 0.7f;

                //尺寸衰减
                p.Size *= 0.96f;

                //移除消逝的粒子
                if (p.Life >= p.MaxLife || p.Opacity <= 0.05f) {
                    trailParticles.RemoveAt(i);
                    continue;
                }

                trailParticles[i] = p;
            }
        }

        //进入消散状态
        private void EnterFadeState() {
            State = FlameState.Fading;
            Projectile.velocity *= 0.5f;
            Projectile.timeLeft = 60;
        }

        //碰撞处理-生成火焰爆发效果
        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (State == FlameState.Burning) {
                CreateFlameExplosion(Projectile.Center);

                //爆炸音效
                SoundEngine.PlaySound(SoundID.Item74 with {
                    Volume = 0.6f,
                    Pitch = 0.1f
                }, Projectile.Center);

                SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with {
                    Volume = 0.4f,
                    Pitch = -0.2f
                }, Projectile.Center);

                EnterFadeState();
                return false;
            }

            return true;
        }

        //击中NPC-生成火焰爆发效果
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            CreateFlameExplosion(Projectile.Center);

            //附加诅咒火焰效果
            target.AddBuff(BuffID.CursedInferno, 300 + HalibutData.GetDomainLayer() * 40);

            //击中音效
            SoundEngine.PlaySound(SoundID.NPCHit3 with {
                Volume = 0.5f,
                Pitch = -0.3f
            }, Projectile.Center);
        }

        //创建火焰爆发效果
        private void CreateFlameExplosion(Vector2 position) {
            //爆发式火焰粒子
            int explosionCount = 30 + HalibutData.GetDomainLayer() * 5;

            for (int i = 0; i < explosionCount; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float speed = Main.rand.NextFloat(5f, 15f);
                Vector2 velocity = angle.ToRotationVector2() * speed;

                if (flameParticles.Count < MaxParticles * 2) {
                    CursedFlameParticle explosion = new CursedFlameParticle {
                        Position = position + Main.rand.NextVector2Circular(10f, 10f),
                        Velocity = velocity,
                        Size = Main.rand.NextFloat(2f, 4.5f),
                        Life = 0,
                        MaxLife = Main.rand.Next(25, 50),
                        Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                        RotationSpeed = Main.rand.NextFloat(-0.3f, 0.3f),
                        Opacity = 1f,
                        ColorPhase = Main.rand.NextFloat(0f, 1f),
                        HeatIntensity = Main.rand.NextFloat(0.8f, 1f)
                    };
                    flameParticles.Add(explosion);
                }

                //原版诅咒火焰尘埃
                if (i % 2 == 0) {
                    Dust cursed = Dust.NewDustPerfect(
                        position,
                        DustID.CursedTorch,
                        velocity * 0.7f,
                        100,
                        default,
                        Main.rand.NextFloat(1.5f, 2.5f)
                    );
                    cursed.noGravity = true;
                    cursed.fadeIn = 1.3f;
                }
            }

            //爆炸环形冲击波
            CreateExplosionRing(position);
        }

        //创建爆炸环形冲击波
        private void CreateExplosionRing(Vector2 center) {
            int ringCount = 25;

            for (int i = 0; i < ringCount; i++) {
                float angle = MathHelper.TwoPi * i / ringCount;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(6f, 12f);

                Dust ring = Dust.NewDustPerfect(
                    center,
                    DustID.CursedTorch,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.8f, 3f)
                );
                ring.noGravity = true;
                ring.fadeIn = 1.4f;
            }
        }

        //生成诅咒火焰残留效果
        private void SpawnCursedResidue() {
            Dust residue = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(15f, 15f),
                DustID.CursedTorch,
                Vector2.Zero,
                100,
                default,
                Main.rand.NextFloat(1f, 2f)
            );
            residue.noGravity = true;
            residue.velocity = -Projectile.velocity * 0.15f + new Vector2(0, HeatRise * 2f);
            residue.fadeIn = 1.2f;
        }

        //更新核心拖尾
        private void UpdateCoreTrail() {
            coreTrail.Insert(0, Projectile.Center);
            if (coreTrail.Count > MaxCoreTrail) {
                coreTrail.RemoveAt(coreTrail.Count - 1);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (State == FlameState.Fading && Projectile.alpha > 220) {
                //消散状态几乎完全透明,只绘制粒子
                DrawTrailParticles();
                DrawFlameParticles();
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;

            //绘制拖尾粒子
            DrawTrailParticles();

            //绘制火焰粒子系统
            DrawFlameParticles();

            //绘制核心火焰流
            if (State == FlameState.Burning) {
                DrawFlameCore(sb);
            }

            return false;
        }

        //绘制拖尾粒子
        private void DrawTrailParticles() {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D glowTex = CWRAsset.StarTexture_White.Value;
            Texture2D flameTex = CWRAsset.LightShot.Value;

            //使用加法混合
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            foreach (var particle in trailParticles) {
                Vector2 drawPos = particle.Position - Main.screenPosition;

                //诅咒火焰颜色-绿色到黄绿色渐变
                Color flameColor = Color.Lerp(
                    new Color(100, 255, 100),
                    new Color(180, 255, 120),
                    particle.ColorPhase
                ) * particle.Opacity;

                //绘制拖尾主体
                sb.Draw(
                    flameTex,
                    drawPos,
                    null,
                    flameColor * 0.6f,
                    particle.Rotation,
                    flameTex.Size() / 2f,
                    particle.Size * 0.1f,
                    SpriteEffects.None,
                    0
                );

                //拖尾核心
                Color coreColor = new Color(200, 255, 150) * particle.Opacity * 0.5f;
                sb.Draw(
                    glowTex,
                    drawPos,
                    null,
                    coreColor,
                    particle.Rotation * 1.5f,
                    glowTex.Size() / 2f,
                    particle.Size * 0.06f,
                    SpriteEffects.None,
                    0
                );
            }

            //恢复正常混合
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        //绘制火焰粒子
        private void DrawFlameParticles() {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D glowTex = CWRAsset.StarTexture_White.Value;
            Texture2D maskTex = CWRAsset.LightShot.Value;
            Texture2D extraTex = FishCloud.Fog;

            //使用加法混合模式
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            foreach (var particle in flameParticles) {
                Vector2 drawPos = particle.Position - Main.screenPosition;
                float scale = particle.Size * 0.09f;
                float rotation = particle.Rotation;

                //诅咒火焰颜色，动态绿色渐变
                Color baseColor = Color.Lerp(
                    new Color(80, 255, 80),
                    new Color(150, 255, 100),
                    particle.ColorPhase
                ) * particle.Opacity * particle.HeatIntensity;

                //绘制火焰外层
                Color outerColor = baseColor * 0.6f;
                sb.Draw(
                    extraTex,
                    drawPos,
                    null,
                    outerColor,
                    rotation,
                    extraTex.Size() / 2f,
                    scale * 2.5f,
                    SpriteEffects.None,
                    0
                );

                //绘制火焰主体
                Color flameColor = baseColor * 0.8f;
                sb.Draw(
                    maskTex,
                    drawPos,
                    null,
                    flameColor,
                    rotation * 0.7f,
                    maskTex.Size() / 2f,
                    scale * 1.2f,
                    SpriteEffects.None,
                    0
                );

                //绘制火焰核心，最亮
                Color coreColor = Color.Lerp(
                    new Color(200, 255, 150),
                    new Color(255, 255, 200),
                    particle.ColorPhase
                ) * particle.Opacity;

                sb.Draw(
                    glowTex,
                    drawPos,
                    null,
                    coreColor * 0.9f,
                    rotation * 1.5f,
                    glowTex.Size() / 2f,
                    scale * 0.7f,
                    SpriteEffects.None,
                    0
                );

                //热力扭曲效果-额外光晕
                if (particle.HeatIntensity > 0.8f) {
                    Color heatColor = new Color(180, 255, 120) * particle.Opacity * 0.3f;
                    sb.Draw(
                        glowTex,
                        drawPos + new Vector2(0, heatDistortion * 2f),
                        null,
                        heatColor,
                        rotation * 2f,
                        glowTex.Size() / 2f,
                        scale * 1.3f,
                        SpriteEffects.None,
                        0
                    );
                }
            }

            //恢复正常混合模式
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        //绘制火焰核心流
        private void DrawFlameCore(SpriteBatch sb) {
            if (coreTrail.Count < 2) return;

            Texture2D coreTex = CWRAsset.LightShot.Value;

            //使用加法混合
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            //绘制核心拖尾
            for (int i = 0; i < coreTrail.Count - 1; i++) {
                float progress = 1f - i / (float)coreTrail.Count;
                Vector2 drawPos = coreTrail[i] - Main.screenPosition;

                //计算方向
                Vector2 toNext = coreTrail[i + 1] - coreTrail[i];
                float rotation = toNext.ToRotation();

                //核心颜色-诅咒绿色渐变
                float colorPhase = (FlameLife * 0.1f + i * 0.2f) % 1f;
                Color coreColor = Color.Lerp(
                    new Color(100, 255, 100),
                    new Color(180, 255, 120),
                    colorPhase
                ) * progress * glowPulse * 0.9f;

                float scale = progress * 0.12f;

                sb.Draw(
                    coreTex,
                    drawPos,
                    null,
                    coreColor,
                    rotation,
                    coreTex.Size() / 2f,
                    new Vector2(scale * 3f, scale * 1.2f),
                    SpriteEffects.None,
                    0
                );

                //外层光晕
                Color glowColor = new Color(150, 255, 130) * progress * glowPulse * 0.5f;
                sb.Draw(
                    coreTex,
                    drawPos,
                    null,
                    glowColor,
                    rotation,
                    coreTex.Size() / 2f,
                    new Vector2(scale * 5f, scale * 2.5f),
                    SpriteEffects.None,
                    0
                );
            }

            //绘制火焰头部-最亮
            Vector2 headPos = Projectile.Center - Main.screenPosition;
            Color headColor = new Color(200, 255, 150) * glowPulse;

            sb.Draw(
                coreTex,
                headPos,
                null,
                headColor,
                Projectile.velocity.ToRotation(),
                coreTex.Size() / 2f,
                new Vector2(0.18f, 0.15f),
                SpriteEffects.None,
                0
            );

            //头部强光核心
            Texture2D starTex = CWRAsset.StarTexture_White.Value;
            Color starColor = new Color(255, 255, 200) * glowPulse * 0.9f;
            sb.Draw(
                starTex,
                headPos,
                null,
                starColor,
                FlameLife * 0.15f,
                starTex.Size() / 2f,
                0.1f,
                SpriteEffects.None,
                0
            );

            //恢复正常混合
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        public override void OnKill(int timeLeft) {
            //死亡时残留火焰
            if (State == FlameState.Burning) {
                CreateFlameExplosion(Projectile.Center);
            }

            //残留诅咒火焰效果
            for (int i = 0; i < 20; i++) {
                Dust residue = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(25f, 25f),
                    DustID.CursedTorch,
                    Main.rand.NextVector2Circular(5f, 5f),
                    100,
                    default,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                residue.noGravity = Main.rand.NextBool();
                residue.fadeIn = 1.3f;
            }
        }
    }

    /// <summary>
    /// 诅咒火焰粒子数据结构
    /// </summary>
    internal struct CursedFlameParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Size;
        public int Life;
        public int MaxLife;
        public float Rotation;
        public float RotationSpeed;
        public float Opacity;
        public float ColorPhase;
        public float HeatIntensity;
    }

    /// <summary>
    /// 火焰拖尾粒子数据结构
    /// </summary>
    internal struct FlameTrail
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Size;
        public int Life;
        public int MaxLife;
        public float Rotation;
        public float RotationSpeed;
        public float Opacity;
        public float ColorPhase;
    }
}
