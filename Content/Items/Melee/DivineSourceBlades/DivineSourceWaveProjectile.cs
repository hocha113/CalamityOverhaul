using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.DivineSourceBlades
{
    /// <summary>
    /// 金源灭却刃椭圆剑气波，终结反斩轰出，材质与挥砍刀光同源(TechArc)。
    /// ai[0] 尺寸倍率(0 视作 1)，ai[2] 充能标记(金色支线)
    /// </summary>
    internal class DivineSourceWaveProjectile : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Lifetime = 46;
        private const float BaseRadius = 150f;
        private const float ThickRatio = 0.62f;
        private const float ArcHalf = 1.95f;
        private const int Segments = 56;
        private const float SpeedDecay = 0.992f;
        /// <summary>沿行进方向拉长，垂直方向压扁，剑气读作贴着飞行方向的扁椭圆</summary>
        private const float StretchAlong = 1.22f;
        private const float SquashPerp = 0.6f;
        /// <summary>带外光晕占径向比例，与 DivineSourceTechArc.fx 的 HaloFrac 锁定</summary>
        private const float HaloFrac = 0.26f;
        private static float HaloExpand => HaloFrac / (1f - HaloFrac);

        private int lifetime = Lifetime;

        private float SizeMul => Projectile.ai[0] > 0.05f ? Projectile.ai[0] : 1f;
        private bool IsGiant => SizeMul >= 1.3f;
        private bool Empowered => Projectile.ai[2] > 0.5f;
        private float GoldMix => Empowered ? 0.55f : 0f;

        /// <summary>椭圆弧上 theta 处、径向距离 r 的世界点</summary>
        private static Vector2 EllipsePos(Vector2 center, float rot, float theta, float r) {
            Vector2 local = new(MathF.Cos(theta) * r * StretchAlong, MathF.Sin(theta) * r * SquashPerp);
            return center + local.RotatedBy(rot);
        }

        private int Age => lifetime - Projectile.timeLeft;
        private float LifeT => MathHelper.Clamp(Age / (float)lifetime, 0f, 1f);

        private float WaveScale {
            get {
                float burst = 1f - MathF.Pow(1f - Math.Min(1f, Age / 12f), 3f);
                return (0.55f + 0.45f * burst + 0.32f * LifeT) * SizeMul;
            }
        }

        private float Opacity {
            get {
                float fadeIn = Math.Min(1f, Age / 4f);
                float fadeOut = 1f - SmoothStep01((LifeT - 0.70f) / 0.30f);
                return fadeIn * fadeOut;
            }
        }

        private float Dissolve => SmoothStep01((LifeT - 0.45f) / 0.55f) * 0.85f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 90;
            Projectile.height = 90;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;
        }

        public override void OnSpawn(IEntitySource source) {
            if (Main.dedServ) {
                return;
            }

            float dustMul = MathF.Min(SizeMul, 1.7f);
            Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < (int)(24 * dustMul); i++) {
                Vector2 vel = forward.RotatedByRandom(0.85) * Main.rand.NextFloat(3f, 11f) * dustMul;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch, vel);
                dust.scale = Main.rand.NextFloat(1.1f, 1.9f);
                dust.noGravity = true;
                dust.fadeIn = 1.2f;
            }
            for (int i = 0; i < (int)(7 * dustMul); i++) {
                Vector2 vel = forward.RotatedByRandom(1.6) * Main.rand.NextFloat(2f, 6f);
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Electric, vel);
                dust.scale = Main.rand.NextFloat(0.7f, 1.1f);
                dust.noGravity = true;
            }
            //出膛甩一圈科技屑，充能期掺金
            for (int i = 0; i < (int)(6 * dustMul); i++) {
                bool gold = Empowered && Main.rand.NextBool(2);
                PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center,
                    forward.RotatedByRandom(0.9) * Main.rand.NextFloat(2f, 7f),
                    gold ? DivineSourceBladeFX.AuricGold : DivineSourceBladeFX.CyanBright,
                    Main.rand.NextFloat(0.5f, 0.9f))
                    .Configure(gold ? DivineSourceBladeFX.AuricAmber : DivineSourceBladeFX.AzureBlue,
                        Main.rand.Next(14, 24));
            }
        }

        public override void AI() {
            //首帧按尺寸倍率重设寿命（在 AI 中而非 OnSpawn，保证多人模式各端一致）
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                lifetime = (int)(Lifetime * MathHelper.Clamp(SizeMul, 0.68f, 1.38f));
                Projectile.timeLeft = lifetime;
            }

            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.velocity *= SpeedDecay;

            float scale = WaveScale;
            float outerR = BaseRadius * scale;

            if (!Main.dedServ) {
                Vector2 backDir = -Projectile.velocity.SafeNormalize(Vector2.UnitX);

                int trailDust = IsGiant ? 4 : 2;
                for (int i = 0; i < trailDust; i++) {
                    float theta = Main.rand.NextFloat(-0.85f, 0.85f) * ArcHalf;
                    float thick = MaxThick(outerR) * ThickProfile(theta);
                    Vector2 at = EllipsePos(Projectile.Center, Projectile.rotation, theta,
                        outerR - thick * Main.rand.NextFloat(0.2f, 0.9f));
                    Dust dust = Dust.NewDustPerfect(at, DustID.IceTorch);
                    dust.velocity = backDir * Main.rand.NextFloat(1f, 4f) + Main.rand.NextVector2Circular(0.8f, 0.8f);
                    dust.scale = Main.rand.NextFloat(0.8f, 1.4f);
                    dust.noGravity = true;
                }

                if (Main.rand.NextBool(2)) {
                    float hornSign = Main.rand.NextBool() ? 1f : -1f;
                    Vector2 horn = HornPosition(hornSign, outerR);
                    bool gold = Empowered && Main.rand.NextBool(2);
                    PRTLoader.NewParticle<PRT_CyberSquare>(horn, backDir * Main.rand.NextFloat(0.5f, 2.5f),
                        gold ? DivineSourceBladeFX.AuricGold : DivineSourceBladeFX.CyanBright,
                        Main.rand.NextFloat(0.4f, 0.7f))
                        .Configure(gold ? DivineSourceBladeFX.AuricAmber : DivineSourceBladeFX.AzureBlue,
                            Main.rand.Next(12, 18));
                }

                if (Main.rand.NextBool(4)) {
                    float theta = Main.rand.NextFloat(-1f, 1f) * ArcHalf * 0.7f;
                    Vector2 at = EllipsePos(Projectile.Center, Projectile.rotation, theta, outerR * 0.8f);
                    Dust dust = Dust.NewDustPerfect(at, DustID.Electric);
                    dust.velocity = backDir * Main.rand.NextFloat(2f, 5f);
                    dust.scale = Main.rand.NextFloat(0.6f, 1f);
                    dust.noGravity = true;
                }
            }

            float lightMul = Opacity;
            Vector3 lightCol = Vector3.Lerp(new Vector3(0.24f, 0.52f, 0.9f), new Vector3(0.9f, 0.72f, 0.32f), GoldMix);
            Lighting.AddLight(Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.Zero) * outerR * 0.5f,
                lightCol * lightMul);
            Lighting.AddLight(HornPosition(1f, outerR), lightCol * 0.5f * lightMul);
            Lighting.AddLight(HornPosition(-1f, outerR), lightCol * 0.5f * lightMul);
        }

        private static float MaxThick(float outerR) => outerR * ThickRatio;

        private static float ThickProfile(float theta) =>
            MathF.Pow(MathF.Max(0f, MathF.Cos(theta / ArcHalf * MathHelper.PiOver2)), 0.8f);

        private Vector2 HornPosition(float hornSign, float outerR) =>
            EllipsePos(Projectile.Center, Projectile.rotation, hornSign * ArcHalf, outerR);

        private static float SmoothStep01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        /// <summary>
        /// 椭圆新月带网格，外扩光晕区。
        /// u 取 1-|2t-1| 再压 0.975，腹部吃到刀光 shader 的白热前缘、两角落进深蓝拖尾
        /// </summary>
        private void BuildCrescentMesh(Vector2 worldCenter, float rot, float outerR, float opacity,
            out ColoredVertex[] verts, out short[] inds) {

            verts = new ColoredVertex[Segments * 2];
            float maxThick = MaxThick(outerR);
            Color vcol = new(255, 255, 255, (byte)(255 * MathHelper.Clamp(opacity, 0f, 1f)));

            for (int i = 0; i < Segments; i++) {
                float t = i / (float)(Segments - 1);
                float theta = (t - 0.5f) * 2f * ArcHalf;
                float thick = maxThick * ThickProfile(theta);
                float u = (1f - MathF.Abs(2f * t - 1f)) * 0.975f;

                Vector2 halo = EllipsePos(worldCenter, rot, theta, outerR + thick * HaloExpand) - Main.screenPosition;
                Vector2 inner = EllipsePos(worldCenter, rot, theta, outerR - thick) - Main.screenPosition;

                verts[i * 2] = new ColoredVertex(halo, vcol, new Vector3(u, 0f, 0f));
                verts[i * 2 + 1] = new ColoredVertex(inner, vcol, new Vector3(u, 1f, 0f));
            }

            inds = new short[(Segments - 1) * 6];
            for (int i = 0; i < Segments - 1; i++) {
                int vi = i * 2;
                int ii = i * 6;
                inds[ii] = (short)vi;
                inds[ii + 1] = (short)(vi + 1);
                inds[ii + 2] = (short)(vi + 2);
                inds[ii + 3] = (short)(vi + 2);
                inds[ii + 4] = (short)(vi + 1);
                inds[ii + 5] = (short)(vi + 3);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float outerR = BaseRadius * WaveScale;
            float maxThick = MaxThick(outerR);

            //判定与绘制同源，沿椭圆弧分段扫
            const int samples = 13;
            Vector2 prev = Vector2.Zero;
            for (int i = 0; i < samples; i++) {
                float t = i / (float)(samples - 1);
                float theta = (t - 0.5f) * 2f * (ArcHalf * 0.88f);
                float thick = maxThick * ThickProfile(theta);
                Vector2 point = EllipsePos(Projectile.Center, Projectile.rotation, theta, outerR - thick * 0.45f);

                if (i > 0) {
                    float width = MathF.Max(26f, thick * 0.7f);
                    float collisionPoint = 0f;
                    if (Collision.CheckAABBvLineCollision(
                        targetHitbox.TopLeft(), targetHitbox.Size(),
                        prev, point, width, ref collisionPoint)) {
                        return true;
                    }
                }
                prev = point;
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //巨型剑气贯穿衰减更慢，强化终结斩的压迫感
            Projectile.damage = (int)(Projectile.damage * (IsGiant ? 0.85f : 0.7f));

            SoundEngine.PlaySound(SoundID.Item14 with {
                Pitch = IsGiant ? 0.1f : 0.4f,
                Volume = IsGiant ? 0.75f : 0.55f
            }, target.Center);

            //剑气命中也喂充能
            if (Projectile.owner == Main.myPlayer) {
                Main.player[Projectile.owner].GetModPlayer<DivineSourcePlayer>().AddCharge(0.03f);
            }

            if (!Main.dedServ) {
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                for (int i = 0; i < 14; i++) {
                    Vector2 vel = dir.RotatedByRandom(0.9) * Main.rand.NextFloat(3f, 8f);
                    Dust dust = Dust.NewDustPerfect(target.Center, DustID.IceTorch, vel);
                    dust.scale = Main.rand.NextFloat(1.0f, 1.6f);
                    dust.noGravity = true;
                    dust.fadeIn = 1.1f;
                }
            }

            if (Projectile.owner == Main.myPlayer) {
                Projectile.NewProjectile(
                    Projectile.GetSource_FromAI(),
                    target.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<DivineSourceHitFXProjectile>(),
                    0, 0f, Projectile.owner,
                    ai0: IsGiant ? 1.2f : 0.7f, ai1: Empowered ? 1f : 0f);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }

            float outerR = BaseRadius * WaveScale;
            float maxThick = MaxThick(outerR);
            for (int i = 0; i < 18; i++) {
                float theta = Main.rand.NextFloat(-1f, 1f) * ArcHalf;
                float thick = maxThick * ThickProfile(theta);
                Vector2 at = EllipsePos(Projectile.Center, Projectile.rotation, theta,
                    outerR - thick * Main.rand.NextFloat(0f, 1f));
                Dust dust = Dust.NewDustPerfect(at, DustID.IceTorch);
                dust.velocity = Main.rand.NextVector2Circular(2.5f, 2.5f);
                dust.scale = Main.rand.NextFloat(0.8f, 1.5f);
                dust.noGravity = true;
            }
            //余痕方屑比波体活得久
            for (int i = 0; i < 8; i++) {
                bool gold = Empowered && Main.rand.NextBool(2);
                PRTLoader.NewParticle<PRT_CyberSquare>(
                    Projectile.Center + Main.rand.NextVector2Circular(outerR * 0.5f, outerR * 0.5f),
                    Main.rand.NextVector2Circular(2f, 2f),
                    gold ? DivineSourceBladeFX.AuricGold : DivineSourceBladeFX.CyanBright,
                    Main.rand.NextFloat(0.45f, 0.8f))
                    .Configure(gold ? DivineSourceBladeFX.AuricAmber : DivineSourceBladeFX.AzureBlue,
                        Main.rand.Next(14, 24));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float opacity = Opacity;
            if (opacity <= 0.01f) {
                return false;
            }

            Effect effect = DivineSourceBladeFX.TechArc;
            if (effect == null) {
                return false;
            }

            DrawCrescentMeshes(Main.spriteBatch, effect, BaseRadius * WaveScale, opacity);
            return false;
        }

        /// <summary>与刀光共用 TechArc，FadeOut 驱动块状消散(两角先蚀)，残影靠顶点 alpha 变淡</summary>
        private void DrawCrescentMeshes(SpriteBatch sb, Effect effect, float outerR, float opacity) {
            GraphicsDevice device = Main.instance.GraphicsDevice;
            sb.End();

            BlendState prevBlend = device.BlendState;
            SamplerState prevSampler = device.SamplerStates[0];
            RasterizerState prevRaster = device.RasterizerState;
            DepthStencilState prevDepth = device.DepthStencilState;

            device.BlendState = BlendState.AlphaBlend;
            device.SamplerStates[0] = SamplerState.LinearWrap;
            device.SamplerStates[1] = SamplerState.LinearWrap;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;

            //s1 绑定噪声，shader 侧 register(s1)
            Texture2D noise = DivineSourceBladeFX.PerlinNoise;
            if (noise != null) {
                device.Textures[1] = noise;
            }

            Trail.CalculateRenderingMatrices(out Matrix view, out Matrix projection);
            effect.Parameters["WorldViewProjection"]?.SetValue(view * projection);
            effect.Parameters["TotalTime"]?.SetValue((float)Main.GameUpdateCount / 60f);
            effect.Parameters["SweepT"]?.SetValue(1f);
            effect.Parameters["GlowBoost"]?.SetValue(IsGiant ? 1.5f : 1.3f);
            effect.Parameters["RimIntensity"]?.SetValue(IsGiant ? 1.6f : 1.4f);
            effect.Parameters["EmpowerMix"]?.SetValue(Empowered ? 0.55f : 0f);
            effect.Parameters["LeadColor"]?.SetValue(DivineSourceBladeFX.TechWhite.ToVector4());
            effect.Parameters["CoreColor"]?.SetValue(DivineSourceBladeFX.CyanBright.ToVector4());
            effect.Parameters["BodyColor"]?.SetValue(DivineSourceBladeFX.AzureBlue.ToVector4());
            effect.Parameters["MidColor"]?.SetValue(DivineSourceBladeFX.ElectricBlue.ToVector4());
            effect.Parameters["DeepColor"]?.SetValue(DivineSourceBladeFX.DeepNavy.ToVector4());
            effect.Parameters["AccentColor"]?.SetValue(DivineSourceBladeFX.AuricGold.ToVector4());

            //索引随初速上调后回收，帧距变大时仍保持残影相互重叠，不散成离散重影
            ReadOnlySpan<(int idx, float alpha, float scaleMul)> ghosts =
                [(6, 0.10f, 0.86f), (4, 0.20f, 0.92f), (2, 0.34f, 0.97f)];

            foreach ((int idx, float ghostAlpha, float scaleMul) in ghosts) {
                if (idx >= Projectile.oldPos.Length) {
                    continue;
                }
                Vector2 oldPos = Projectile.oldPos[idx];
                if (oldPos == Vector2.Zero) {
                    continue;
                }

                Vector2 oldCenter = oldPos + Projectile.Size * 0.5f;
                float oldRot = Projectile.oldRot[idx] != 0f ? Projectile.oldRot[idx] : Projectile.rotation;

                BuildCrescentMesh(oldCenter, oldRot, outerR * scaleMul, opacity * ghostAlpha, out var gVerts, out var gInds);
                effect.Parameters["FadeOut"]?.SetValue(
                    MathHelper.Clamp(1f - Dissolve - (1f - ghostAlpha) * 0.35f, 0f, 1f));

                foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                    pass.Apply();
                    Trail.DrawUserPrimitives(gVerts, gInds, device);
                }
            }

            BuildCrescentMesh(Projectile.Center, Projectile.rotation, outerR, opacity, out var verts, out var inds);
            effect.Parameters["FadeOut"]?.SetValue(MathHelper.Clamp(1f - Dissolve, 0f, 1f));

            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                Trail.DrawUserPrimitives(verts, inds, device);
            }

            device.BlendState = prevBlend;
            device.SamplerStates[0] = prevSampler;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
