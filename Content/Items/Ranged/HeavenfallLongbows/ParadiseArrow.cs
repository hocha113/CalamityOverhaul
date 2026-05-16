using CalamityOverhaul.Common;
using CalamityOverhaul.Content.DamageModify;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.HeavenfallLongbows
{
    internal class ParadiseArrow : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int TrailCacheLen = 14;

        private Vector2[] trailHistory;
        private int trailHistoryCount;
        private int historyTimer;
        private Vector2[] trailPositions;
        private int currentValidCount;
        private Trail trail;

        private float HueOffset => Projectile.identity * 0.0937f;

        private Color ChromaColor => VaultUtils.MultiStepColorLerp(
            (Projectile.ai[0] * 0.025f + HueOffset) % 1f, HeavenfallLongbow.rainbowColors);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
        }

        public override void SetDefaults() {
            Projectile.height = 24;
            Projectile.width = 24;
            Projectile.DamageType = EndlessDamageClass.Instance;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.MaxUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 7 * Projectile.MaxUpdates;
        }

        public override void AI() {
            Color light = ChromaColor;
            Lighting.AddLight(Projectile.Center, light.ToVector3());

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            NPC target = Projectile.Center.FindClosestNPC(1300);
            if (target != null && Projectile.ai[0] > 30) {
                Projectile.SmoothHomingBehavior(target.Center, 1, 0.3f);
            }

            //历史轨迹推入
            historyTimer++;
            if (historyTimer >= 1) {
                historyTimer = 0;
                PushHistory(Projectile.Center);
            }

            //粒子节流: 每 3 子帧 1 个棱镜碎片 (比旧版每帧 50% 大幅减负)
            if (!VaultUtils.isServer && Projectile.ai[0] % 3 == 0) {
                Vector2 prismVel = Projectile.velocity.RotatedByRandom(0.4f) * Main.rand.NextFloat(0.05f, 0.18f);
                Color prismCol = VaultUtils.MultiStepColorLerp(
                    Main.rand.NextFloat(), HeavenfallLongbow.rainbowColors);
                PRTLoader.AddParticle(new PRT_HeavenfallPrism(
                    Projectile.Center, prismVel, prismCol,
                    Main.rand.NextFloat(0.5f, 0.85f), Main.rand.Next(16, 24),
                    Main.rand.NextFloat(2.5f, 4.5f), shortStretch: true));
            }

            Projectile.ai[0]++;
        }

        private void PushHistory(Vector2 newPos) {
            if (trailHistory == null) {
                trailHistory = new Vector2[TrailCacheLen];
                for (int i = 0; i < TrailCacheLen; i++) {
                    trailHistory[i] = newPos;
                }
            }
            for (int i = TrailCacheLen - 1; i > 0; i--) {
                trailHistory[i] = trailHistory[i - 1];
            }
            trailHistory[0] = newPos;
            if (trailHistoryCount < TrailCacheLen) {
                trailHistoryCount++;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                //命中时极光散射环
                for (int i = 0; i < 4; i++) {
                    float ang = MathHelper.TwoPi * i / 4f + Main.rand.NextFloat(0.2f);
                    Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(2.5f, 4.5f);
                    PRTLoader.AddParticle(new PRT_HeavenfallAurora(
                        target.Center, vel,
                        Main.rand.NextFloat(70f, 110f), Main.rand.NextFloat(16f, 24f),
                        Main.rand.Next(22, 32),
                        huePhase: HueOffset + i * 0.25f, hueSpeed: 0.025f, driftScale: 0.9f));
                }
                //棱镜碎片爆裂
                for (int i = 0; i < 6; i++) {
                    Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 4f);
                    Color col = VaultUtils.MultiStepColorLerp(
                        i / 6f, HeavenfallLongbow.rainbowColors);
                    PRTLoader.AddParticle(new PRT_HeavenfallPrism(
                        target.Center, vel, col,
                        Main.rand.NextFloat(0.7f, 1.2f), Main.rand.Next(22, 32),
                        Main.rand.NextFloat(3.5f, 6f), shortStretch: true));
                }
            }
            Projectile.timeLeft -= 15;
            if (Projectile.timeLeft <= 0) {
                Projectile.timeLeft = 0;
            }
        }

        public override void OnKill(int timeLeft) {
            Projectile.Explode(300);
        }

        public override bool PreDraw(ref Color lightColor) {
            //箭簇剪影: 沿用旧 Extra_98 蒙版, 但改为 R/G/B 三相位色散叠加
            float localOffset = Projectile.identity * 0.1372f;
            float time = Main.GlobalTimeWrappedHourly * 2f;
            Color cR = VaultUtils.MultiStepColorLerp((time + localOffset) % 1f, HeavenfallLongbow.rainbowColors);
            Color cG = VaultUtils.MultiStepColorLerp((time + localOffset + 0.1f) % 1f, HeavenfallLongbow.rainbowColors);
            Color cB = VaultUtils.MultiStepColorLerp((time + localOffset + 0.2f) % 1f, HeavenfallLongbow.rainbowColors);
            cR = Color.Lerp(Color.White, cR, 0.85f);
            cG = Color.Lerp(Color.White, cG, 0.85f);
            cB = Color.Lerp(Color.White, cB, 0.85f);
            cR.A = 0; cG.A = 0; cB.A = 0;

            Vector2 scale = new Vector2(0.5f, 1.6f) * Projectile.scale;
            Texture2D texture = CWRUtils.GetT2DAsset(CWRConstant.Masking + "Extra_98").Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 perp = Projectile.rotation.ToRotationVector2() * 2.4f;

            //R 偏移
            Main.EntitySpriteDraw(texture, drawPos - perp, null, cR, Projectile.rotation, texture.Size() * 0.5f, scale, 0, 0f);
            //B 偏移
            Main.EntitySpriteDraw(texture, drawPos + perp, null, cB, Projectile.rotation, texture.Size() * 0.5f, scale, 0, 0f);
            //G/主层
            Main.EntitySpriteDraw(texture, drawPos, null, cG, Projectile.rotation, texture.Size() * 0.5f, scale, 0, 0f);
            //核心高光
            Color core = Color.White * 0.9f;
            core.A = 0;
            Main.EntitySpriteDraw(texture, drawPos, null, core, Projectile.rotation, texture.Size() * 0.5f, scale * new Vector2(0.45f, 1f), 0, 0f);

            return false;
        }

        //═════════════ Trail ═════════════
        public float WidthFunc(float progress) {
            if (trailHistory == null) {
                return 0f;
            }
            float validRatio = MathF.Max((float)currentValidCount / TrailCacheLen, 0.15f);
            float clipped = MathHelper.Clamp(progress / validRatio, 0f, 1f);
            float noseRise = MathF.Sin(MathF.Min(clipped / 0.1f, 1f) * MathHelper.PiOver2);
            float tailTaper = 1f - MathF.Pow(clipped, 1.6f);
            return MathF.Max(noseRise * tailTaper, 0f) * 40f * Projectile.scale;
        }

        public Color ColorFunc(Vector2 _) => Color.White * Projectile.Opacity;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (trailHistory == null || trailHistoryCount < 3) {
                return;
            }

            Effect shader = EffectLoader.HeavenfallPrismTrail?.Value;
            if (shader == null) {
                return;
            }
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (noise == null) {
                return;
            }

            trailPositions ??= new Vector2[TrailCacheLen];
            trailPositions[0] = Projectile.Center;
            for (int i = 1; i < TrailCacheLen; i++) {
                int histIdx = Math.Min(i - 1, trailHistoryCount - 1);
                trailPositions[i] = trailHistory[Math.Max(histIdx, 0)];
            }
            currentValidCount = Math.Min(trailHistoryCount + 1, TrailCacheLen);

            trail ??= new Trail(trailPositions, WidthFunc, ColorFunc);
            trail.TrailPositions = trailPositions;

            shader.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.05f);
            shader.Parameters["fadeAlpha"]?.SetValue(Projectile.Opacity);
            shader.Parameters["coreIntensity"]?.SetValue(0.7f);
            shader.Parameters["dispersion"]?.SetValue(0.05f);
            shader.Parameters["flowSpeed"]?.SetValue(0.6f);
            shader.Parameters["hueOffset"]?.SetValue(HueOffset);
            shader.Parameters["uNoiseTex"]?.SetValue(noise);

            shader.CurrentTechnique = shader.Techniques["Trail"];

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState old = device.BlendState;
            device.BlendState = BlendState.Additive;
            trail.DrawTrail(shader);
            device.BlendState = old;
        }
    }
}
