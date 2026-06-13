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
    /// 神源之刃新月剑气波
    /// ai[0] 为尺寸倍率（0 视作 1），大斩切巨型剑气存活更久、衰减更慢
    /// </summary>
    internal class DivineSourceWaveProjectile : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int Lifetime = 46;
        private const float BaseRadius = 150f;
        private const float ThickRatio = 0.62f;
        private const float ArcHalf = 1.95f;
        private const int Segments = 56;
        private const float SpeedDecay = 0.985f;

        private static readonly Color RimColor = new(255, 250, 215);
        private static readonly Color GoldColor = new(255, 208, 90);
        private static readonly Color OrangeColor = new(255, 150, 40);
        private static readonly Color DeepColor = new(220, 80, 12);

        private float traveled;
        private float swingDir = 1f;
        private int lifetime = Lifetime;

        private float SizeMul => Projectile.ai[0] > 0.05f ? Projectile.ai[0] : 1f;
        private bool IsGiant => SizeMul >= 1.3f;

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
            swingDir = Projectile.ai[1] != 0 ? Projectile.ai[1] : 1f;

            if (Main.dedServ) {
                return;
            }

            float dustMul = MathF.Min(SizeMul, 1.7f);
            Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < (int)(26 * dustMul); i++) {
                Vector2 vel = forward.RotatedByRandom(0.85) * Main.rand.NextFloat(3f, 11f) * dustMul;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame, vel);
                dust.scale = Main.rand.NextFloat(1.1f, 1.9f);
                dust.noGravity = true;
                dust.fadeIn = 1.2f;
            }
            for (int i = 0; i < (int)(10 * dustMul); i++) {
                Vector2 vel = forward.RotatedByRandom(1.6) * Main.rand.NextFloat(2f, 6f);
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, vel);
                dust.scale = Main.rand.NextFloat(1.3f, 2.0f);
                dust.noGravity = true;
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
            traveled += Projectile.velocity.Length();
            Projectile.velocity *= SpeedDecay;

            float scale = WaveScale;
            float outerR = BaseRadius * scale;

            if (!Main.dedServ) {
                Vector2 backDir = -Projectile.velocity.SafeNormalize(Vector2.UnitX);

                int trailDust = IsGiant ? 4 : 2;
                for (int i = 0; i < trailDust; i++) {
                    float theta = Main.rand.NextFloat(-0.85f, 0.85f) * ArcHalf;
                    float thick = MaxThick(outerR) * ThickProfile(theta);
                    Vector2 at = Projectile.Center
                        + (Projectile.rotation + theta).ToRotationVector2() * (outerR - thick * Main.rand.NextFloat(0.2f, 0.9f));
                    Dust dust = Dust.NewDustPerfect(at, DustID.GoldFlame);
                    dust.velocity = backDir * Main.rand.NextFloat(1f, 4f) + Main.rand.NextVector2Circular(0.8f, 0.8f);
                    dust.scale = Main.rand.NextFloat(0.8f, 1.4f);
                    dust.noGravity = true;
                }

                if (Main.rand.NextBool(2)) {
                    float hornSign = Main.rand.NextBool() ? 1f : -1f;
                    Vector2 horn = HornPosition(hornSign, outerR);
                    Dust dust = Dust.NewDustPerfect(horn, DustID.GoldCoin);
                    dust.velocity = backDir * Main.rand.NextFloat(0.5f, 2.5f);
                    dust.scale = Main.rand.NextFloat(0.6f, 1.0f);
                    dust.noGravity = true;
                }

                if (Main.rand.NextBool(4)) {
                    float theta = Main.rand.NextFloat(-1f, 1f) * ArcHalf * 0.7f;
                    Vector2 at = Projectile.Center + (Projectile.rotation + theta).ToRotationVector2() * outerR * 0.8f;
                    Dust dust = Dust.NewDustPerfect(at, DustID.Torch);
                    dust.velocity = backDir * Main.rand.NextFloat(2f, 5f);
                    dust.scale = Main.rand.NextFloat(1.0f, 1.6f);
                    dust.noGravity = true;
                }
            }

            float lightMul = Opacity;
            Lighting.AddLight(Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.Zero) * outerR * 0.5f,
                new Vector3(1.0f, 0.74f, 0.30f) * lightMul);
            Lighting.AddLight(HornPosition(1f, outerR), new Vector3(0.55f, 0.40f, 0.15f) * lightMul);
            Lighting.AddLight(HornPosition(-1f, outerR), new Vector3(0.55f, 0.40f, 0.15f) * lightMul);
        }

        private static float MaxThick(float outerR) => outerR * ThickRatio;

        private static float ThickProfile(float theta) =>
            MathF.Pow(MathF.Max(0f, MathF.Cos(theta / ArcHalf * MathHelper.PiOver2)), 0.8f);

        private Vector2 HornPosition(float hornSign, float outerR) =>
            Projectile.Center + (Projectile.rotation + hornSign * ArcHalf).ToRotationVector2() * outerR;

        private static float SmoothStep01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        private void BuildCrescentMesh(Vector2 worldCenter, float rot, float outerR,
            out ColoredVertex[] verts, out short[] inds) {

            verts = new ColoredVertex[Segments * 2];
            float maxThick = MaxThick(outerR);

            for (int i = 0; i < Segments; i++) {
                float t = i / (float)(Segments - 1);
                float theta = (t - 0.5f) * 2f * ArcHalf;
                Vector2 dir = (rot + theta).ToRotationVector2();
                float thick = maxThick * ThickProfile(theta);

                Vector2 outer = worldCenter + dir * outerR - Main.screenPosition;
                Vector2 inner = worldCenter + dir * (outerR - thick) - Main.screenPosition;

                verts[i * 2] = new ColoredVertex(outer, Color.White, new Vector3(t, 0f, 0f));
                verts[i * 2 + 1] = new ColoredVertex(inner, Color.White, new Vector3(t, 1f, 0f));
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

            const int samples = 13;
            Vector2 prev = Vector2.Zero;
            for (int i = 0; i < samples; i++) {
                float t = i / (float)(samples - 1);
                float theta = (t - 0.5f) * 2f * (ArcHalf * 0.88f);
                float thick = maxThick * ThickProfile(theta);
                Vector2 point = Projectile.Center
                    + (Projectile.rotation + theta).ToRotationVector2() * (outerR - thick * 0.45f);

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
            //巨型剑气贯穿衰减更慢，强化大斩切的压迫感
            Projectile.damage = (int)(Projectile.damage * (IsGiant ? 0.85f : 0.7f));

            SoundEngine.PlaySound(SoundID.Item14 with {
                Pitch = IsGiant ? 0.1f : 0.4f,
                Volume = IsGiant ? 0.75f : 0.55f
            }, target.Center);

            if (!Main.dedServ) {
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                for (int i = 0; i < 14; i++) {
                    Vector2 vel = dir.RotatedByRandom(0.9) * Main.rand.NextFloat(3f, 8f);
                    Dust dust = Dust.NewDustPerfect(target.Center, DustID.GoldFlame, vel);
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
                    ai0: IsGiant ? 1.2f : 0.7f);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }

            float outerR = BaseRadius * WaveScale;
            float maxThick = MaxThick(outerR);
            for (int i = 0; i < 22; i++) {
                float theta = Main.rand.NextFloat(-1f, 1f) * ArcHalf;
                float thick = maxThick * ThickProfile(theta);
                Vector2 at = Projectile.Center
                    + (Projectile.rotation + theta).ToRotationVector2() * (outerR - thick * Main.rand.NextFloat(0f, 1f));
                Dust dust = Dust.NewDustPerfect(at, DustID.GoldFlame);
                dust.velocity = Main.rand.NextVector2Circular(2.5f, 2.5f);
                dust.scale = Main.rand.NextFloat(0.8f, 1.5f);
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float opacity = Opacity;
            if (opacity <= 0.01f) {
                return false;
            }

            Effect effect = DivineSourceBladeFX.Crescent;
            if (effect == null) {
                return false;
            }

            DrawCrescentMeshes(Main.spriteBatch, effect, BaseRadius * WaveScale, opacity);
            return false;
        }

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

            Trail.CalculateRenderingMatrices(out Matrix view, out Matrix projection);
            effect.Parameters["WorldViewProjection"]?.SetValue(view * projection);
            effect.Parameters["TotalTime"]?.SetValue((float)Main.GameUpdateCount / 60f);
            effect.Parameters["Dissolve"]?.SetValue(Dissolve);
            effect.Parameters["RimIntensity"]?.SetValue(IsGiant ? 2.1f : 1.8f);
            effect.Parameters["StreakStrength"]?.SetValue(IsGiant ? 0.8f : 0.65f);
            effect.Parameters["FlowOffset"]?.SetValue(traveled / 480f);
            effect.Parameters["RimColor"]?.SetValue(RimColor.ToVector4());
            effect.Parameters["GoldColor"]?.SetValue(GoldColor.ToVector4());
            effect.Parameters["OrangeColor"]?.SetValue(OrangeColor.ToVector4());
            effect.Parameters["DeepColor"]?.SetValue(DeepColor.ToVector4());
            Texture2D noise = DivineSourceBladeFX.Noise;
            if (noise != null) {
                effect.Parameters["NoiseTexture"]?.SetValue(noise);
            }

            ReadOnlySpan<(int idx, float alpha, float scaleMul)> ghosts =
                [(9, 0.10f, 0.86f), (6, 0.20f, 0.92f), (3, 0.34f, 0.97f)];

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

                BuildCrescentMesh(oldCenter, oldRot, outerR * scaleMul, out var gVerts, out var gInds);
                effect.Parameters["Opacity"]?.SetValue(opacity * ghostAlpha);
                effect.Parameters["Dissolve"]?.SetValue(MathHelper.Clamp(Dissolve + (1f - ghostAlpha) * 0.35f, 0f, 1f));

                foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                    pass.Apply();
                    Trail.DrawUserPrimitives(gVerts, gInds, device);
                }
            }

            BuildCrescentMesh(Projectile.Center, Projectile.rotation, outerR, out var verts, out var inds);
            effect.Parameters["Opacity"]?.SetValue(opacity);
            effect.Parameters["Dissolve"]?.SetValue(Dissolve);

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
