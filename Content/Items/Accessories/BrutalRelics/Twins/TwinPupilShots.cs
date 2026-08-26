using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Twins
{
    /// <summary>视界的红激光点射弹。高速直线，四相：眼部后坐/拉伸飞行/命中爆/坠火余韵</summary>
    internal class TwinPupilLaser : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 110;
            Projectile.extraUpdates = 2;
            Projectile.DamageType = DamageClass.Generic;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.55f, Volume = 0.4f }, Projectile.Center);
                }
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, TwinPupilTether.LaserColor.ToVector3() * 0.5f);

            //沿途低频逸散火花
            if (!VaultUtils.isServer && Main.rand.NextBool(12)) {
                PRTLoader.NewParticle<PRT_TwinPupilSpark>(Projectile.Center,
                    Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(0.6f, 0.6f),
                    Color.White, 0.75f)?.Configure(12, 0);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => TwinPupilRendNPC.ApplyRendBonus(target, ref modifiers);

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //命中爆
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_TwinPupilSpark>(Projectile.Center, VaultUtils.RandVr(5f, 12f),
                    Color.White, Main.rand.NextFloat(1.1f, 1.8f))?.Configure(16, 0);
            }
            PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, TwinPupilTether.LaserGlow, 0.1f)?
                .Configure(Vector2.One, Main.rand.NextFloat(MathHelper.TwoPi), 0.55f, 12);
            //余韵：带重力的红烬活过弹体
            Player owner = Main.player[Projectile.owner];
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    VaultUtils.RandVr(2f, 5f) - Vector2.UnitY * 2f,
                    TwinPupilTether.LaserColor, Main.rand.NextFloat(0.8f, 1.2f))?
                    .Configure(true, Main.rand.Next(16, 26), owner);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 origin = glow.Size() / 2f;
            float rot = Projectile.rotation + MathHelper.PiOver2;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.16f, 1.6f, 4.6f);

            //拖尾余辉：暗红外层向尾先淡
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 gp = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(glow, gp, null,
                    (TwinPupilTether.LaserColor with { A = 0 }) * (0.5f * fade * fade),
                    rot, origin, new Vector2(0.26f * fade, 0.22f * stretch * fade), SpriteEffects.None, 0);
            }

            //速度拉伸弹头：红晕+白热芯
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(glow, drawPos, null, (TwinPupilTether.LaserGlow with { A = 0 }) * 0.9f,
                rot, origin, new Vector2(0.42f, 0.3f * stretch), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, Color.White with { A = 0 },
                rot, origin, new Vector2(0.2f, 0.16f * stretch), SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>焚瞳的咒焰喷流。近距扩散减速火舌，膨胀判定+咒火点燃；本体不画，绿焰粒子承载</summary>
    internal class TwinPupilFlameJet : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 34;
            Projectile.extraUpdates = 1;
            Projectile.alpha = 255;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.DamageType = DamageClass.Generic;
        }

        public override void AI() {
            Projectile.ai[0]++;
            float progress = Projectile.ai[0] / 68f;

            //扩散减速
            Projectile.velocity *= 0.982f;

            //碰撞箱膨胀(各端由计时确定性同步执行)
            if (Projectile.ai[0] == 16f || Projectile.ai[0] == 32f) {
                Projectile.Resize(Projectile.width + 10, Projectile.height + 10);
            }

            Lighting.AddLight(Projectile.Center, 0.25f, 0.85f, 0.45f);

            if (VaultUtils.isServer) {
                return;
            }

            //咒焰主体：绿火簇
            for (int i = 0; i < 2; i++) {
                Dust dust = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(7f + progress * 13f, 7f + progress * 13f),
                    4, 4, DustID.CursedTorch,
                    Projectile.velocity.X * 0.4f, Projectile.velocity.Y * 0.4f, 100, default,
                    Main.rand.NextFloat(1.3f, 2f));
                dust.noGravity = true;
                dust.fadeIn = 0.4f;
            }
            //热浪柔光
            if (Main.rand.NextBool(5)) {
                PRTLoader.NewParticle<PRT_TwinPupilSpark>(Projectile.Center,
                    Projectile.velocity * 0.15f + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    Color.White, Main.rand.NextFloat(0.7f, 1.2f))?.Configure(14, 1);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => TwinPupilRendNPC.ApplyRendBonus(target, ref modifiers);

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.CursedInferno, 120);

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.Kill();
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.Center, 8, 8, DustID.CursedTorch,
                    0f, -1f, 120, default, 1.2f);
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }

    /// <summary>
    /// 交叉冲锋交点引爆。ai[0]=系绳轴向角(干涉爆纹定向)；
    /// 判定半径逐帧随可见波前扩张(与着色器 FrontAt 同式)，单目标只结算一次
    /// </summary>
    internal class TwinPupilCrossBurst : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const int Life = 26;
        private const int DamageWindow = 12;
        //本地画布半宽(px)，与 shader p∈[-1,1] 的折算基准
        private const float QuadHalf = TwinPupilTether.BurstRadius * 1.5f;

        private float Progress => 1f - Projectile.timeLeft / (float)Life;

        //波前扩张曲线：前快后缓，与着色器同式
        private static float FrontAt(float t) => 1f - MathF.Pow(1f - t, 2.6f);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 40;//寿命内单次结算
            Projectile.DamageType = DamageClass.Generic;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1f, Pitch = -0.1f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.85f, Pitch = 0.2f }, Projectile.Center);
                    TwinsMotion.Shake(Projectile.Center, 6.5f, 12);

                    //双色对转冲击环
                    PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero,
                        TwinPupilTether.LaserColor, 0.24f)?
                        .Configure(Vector2.One, Projectile.ai[0], 1.6f, 18);
                    PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero,
                        TwinPupilTether.FlameColor, 0.2f)?
                        .Configure(Vector2.One, Projectile.ai[0] + 1.2f, 1.3f, 16);
                    //双色火花环交替外掷
                    for (int i = 0; i < 14; i++) {
                        PRTLoader.NewParticle<PRT_TwinPupilSpark>(Projectile.Center,
                            VaultUtils.RandVr(6f, 13f), Color.White,
                            Main.rand.NextFloat(1.2f, 2f))?.Configure(20, i % 2);
                    }
                }
            }

            float flicker = 0.7f + 0.3f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 40f);
            Lighting.AddLight(Projectile.Center,
                Color.Lerp(TwinPupilTether.LaserColor, TwinPupilTether.FlameColor, flicker).ToVector3()
                * (1.4f * (1f - Progress)));
        }

        public override bool? CanDamage() => Progress <= DamageWindow / (float)Life ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //判定半径跟随可见波前，封顶于爆炸半径：波扫到才结算
            float radius = Math.Min(FrontAt(Progress) * QuadHalf, TwinPupilTether.BurstRadius);
            Vector2 closest = new(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.DistanceSquared(closest, Projectile.Center) <= radius * radius;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => TwinPupilRendNPC.ApplyRendBonus(target, ref modifiers);

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //引爆同样撕开创口
            if (target.TryGetGlobalNPC(out TwinPupilRendNPC rend)) {
                rend.ApplyRend();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float t = Progress;
            if (EffectLoader.BRelicTwinBurst?.Value != null) {
                DrawShaderBurst(t);
            }
            else {
                DrawFallbackBurst(t);
            }
            return false;
        }

        /// <summary>双源干涉爆纹：红绿反相条纹随撕裂波前扩张显影</summary>
        private void DrawShaderBurst(float t) {
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Effect shader = EffectLoader.BRelicTwinBurst.Value;
            shader.Parameters["uColorA"]?.SetValue(TwinPupilTether.LaserColor.ToVector3());
            shader.Parameters["uColorB"]?.SetValue(TwinPupilTether.FlameColor.ToVector3());
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["uProgress"]?.SetValue(t);
            shader.Parameters["uOpacity"]?.SetValue(1f);
            shader.Parameters["uSep"]?.SetValue(0.16f);
            shader.Parameters["uFlash"]?.SetValue(MathHelper.Clamp(1f - t * 6.5f, 0f, 1f));
            shader.Parameters["uImage1"]?.SetValue(CWRAsset.PerlinNoise.Value);
            shader.CurrentTechnique.Passes[0].Apply();

            Texture2D quad = VaultAsset.placeholder2.Value;
            float size = QuadHalf * 2f;
            //干涉源沿系绳轴向对置：quad 的 x 轴转到轴向的垂线(两眼交叉的分离轴)
            sb.Draw(quad, Projectile.Center - Main.screenPosition, null, Color.White,
                Projectile.ai[0] + MathHelper.PiOver2, quad.Size() / 2f,
                new Vector2(size / quad.Width, size / quad.Height), SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>回退：双色扩散环+中心白闪(A=0 加色技法进预乘批)</summary>
        private void DrawFallbackBurst(float t) {
            Texture2D ring = CWRAsset.DiffusionCircle.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float frontPx = Math.Min(FrontAt(t) * QuadHalf, TwinPupilTether.BurstRadius * 1.15f);
            float ringScale = frontPx / (ring.Width * 0.45f);
            float fade = 1f - t;

            Main.EntitySpriteDraw(ring, drawPos, null,
                (TwinPupilTether.LaserColor with { A = 0 }) * (0.85f * fade),
                t * 2f, ring.Size() / 2f, ringScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(ring, drawPos, null,
                (TwinPupilTether.FlameColor with { A = 0 }) * (0.7f * fade),
                -t * 2.4f, ring.Size() / 2f, ringScale * 0.86f, SpriteEffects.None, 0);
            float flash = MathHelper.Clamp(1f - t * 6.5f, 0f, 1f);
            if (flash > 0f) {
                Main.EntitySpriteDraw(glow, drawPos, null, (Color.White with { A = 0 }) * flash,
                    0f, glow.Size() / 2f, 3.4f * flash + 1f, SpriteEffects.None, 0);
            }
        }
    }
}
