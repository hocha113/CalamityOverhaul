using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.AriaofTheCosmoses
{
    /// 伽马短脉冲，raycast 终点 + AriaGammaRay.fx
    /// ai[0]=1 跟主人(R) / 0 固定点；ai[1]=编队相位
    internal class GammaRayBeam : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int ExpandTime = 4;
        internal const int SustainTime = 26;
        internal const int CollapseTime = 8;
        internal const int TotalLife = ExpandTime + SustainTime + CollapseTime;

        private const float MaxRayLength = 2200f;
        /// <summary>碰撞核宽</summary>
        private const float CoreWidth = 22f;
        /// <summary>顶点条带半宽(给辉光余量)</summary>
        private const float StripHalfWidth = 56f;

        public ref float AnchorOwner => ref Projectile.ai[0];
        public ref float PhaseSeed => ref Projectile.ai[1];

        private int Age;
        private float widthMul;
        private float rayLength;
        private bool hitWall;

        internal static readonly Color ColCore = new(242, 235, 255);
        internal static readonly Color ColViolet = new(155, 107, 255);
        internal static readonly Color ColCheren = new(56, 182, 255);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife + 10;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Age == 0) {
                Projectile.rotation = Projectile.velocity.ToRotation();
                Projectile.velocity = Vector2.Zero;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item75 with { Volume = 0.7f, Pitch = 0.65f, MaxInstances = 5 }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.45f, Pitch = -0.4f, MaxInstances = 5 }, Projectile.Center);
                }
            }

            if (AnchorOwner >= 1f) {
                Player owner = Main.player[Projectile.owner];
                if (!owner.active || owner.dead) {
                    Projectile.Kill();
                    return;
                }
                Projectile.Center = owner.Center;
            }

            Age++;
            if (Age >= TotalLife) {
                Projectile.Kill();
                return;
            }

            //展开过冲→维持→收束
            widthMul = Age < ExpandTime
                ? VaultUtils.EaseOutCubic(Age / (float)ExpandTime)
                : Age > TotalLife - CollapseTime
                    ? 1f - VaultUtils.EaseInQuad((Age - (TotalLife - CollapseTime)) / (float)CollapseTime)
                    : 1f;

            MeasureRayLength();

            if (!VaultUtils.isServer) {
                UpdateVisuals();
            }
        }

        private void MeasureRayLength() {
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            float length = 0f;
            hitWall = false;
            while (length < MaxRayLength) {
                if (Framing.GetTileSafely(Projectile.Center + dir * length).HasSolidTile()) {
                    hitWall = true;
                    break;
                }
                length += 8f;
            }
            rayLength = length;
        }

        private void UpdateVisuals() {
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);

            Lighting.AddLight(Projectile.Center, ColViolet.ToVector3() * widthMul);
            int lightSteps = (int)(rayLength / 60f);
            for (int i = 1; i <= lightSteps; i++) {
                Lighting.AddLight(Projectile.Center + dir * (i * 60f), ColViolet.ToVector3() * 0.7f * widthMul);
            }

            if (Main.rand.NextBool(3)) {
                float along = Main.rand.NextFloat(0.05f, 0.95f);
                Vector2 pos = Projectile.Center + dir * (rayLength * along) + perp * Main.rand.NextFloat(-CoreWidth * 0.5f, CoreWidth * 0.5f);
                PRTLoader.NewParticle<PRT_Spark>(pos, dir.RotatedBy(Main.rand.NextFloat(-0.6f, 0.6f)) * Main.rand.NextFloat(2f, 5f),
                    Color.Lerp(ColViolet, ColCore, Main.rand.NextFloat(0.3f, 0.8f)), Main.rand.NextFloat(0.5f, 1f))
                    ?.Configure(false, Main.rand.Next(8, 14));
            }

            if (hitWall && Main.rand.NextBool(2)) {
                Vector2 hitPos = Projectile.Center + dir * rayLength;
                Vector2 splashVel = (-dir).RotatedBy(Main.rand.NextFloat(-1.1f, 1.1f)) * Main.rand.NextFloat(4f, 10f);
                PRTLoader.NewParticle<PRT_GammaIonize>(hitPos, splashVel,
                    Color.Lerp(ColViolet, ColCheren, Main.rand.NextFloat()), Main.rand.NextFloat(0.4f, 0.8f))
                    ?.Configure(Main.rand.Next(10, 18), Main.rand.NextFloat(MathHelper.TwoPi));
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (rayLength < 4f || widthMul < 0.05f) {
                return false;
            }
            float point = 0f;
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, Projectile.Center + dir * rayLength, CoreWidth * widthMul, ref point);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer && Projectile.numHits <= 2) {
                for (int i = 0; i < 10; i++) {
                    float ang = MathHelper.TwoPi * i / 10f + Main.rand.NextFloat(-0.2f, 0.2f);
                    PRTLoader.NewParticle<PRT_GammaIonize>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                        ang.ToRotationVector2() * Main.rand.NextFloat(4f, 10f),
                        Color.Lerp(ColViolet, ColCore, Main.rand.NextFloat(0.2f, 0.6f)), Main.rand.NextFloat(0.4f, 0.9f))
                        ?.Configure(Main.rand.Next(10, 20), Main.rand.NextFloat(MathHelper.TwoPi));
                }
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_Light>(target.Center, Main.rand.NextVector2Circular(18f, 18f),
                        Color.Lerp(ColViolet, ColCheren, Main.rand.NextFloat()), Main.rand.NextFloat(0.5f, 1f))
                        ?.Configure(Main.rand.Next(16, 28), opacity: 1.4f, squishStrenght: 2f, hueShift: 0.02f);
                }
            }

            Projectile.damage = (int)(Projectile.damage * 0.85f);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (VaultUtils.isServer || widthMul < 0.03f || rayLength < 8f) {
                return;
            }
            Effect effect = EffectLoader.AriaGammaRay?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);

            //条带止于 raycast，热球交 shader
            float stripLen = rayLength;
            Vector2 origin = Projectile.Center - dir * 10f;
            Vector2 tip = Projectile.Center + dir * stripLen;
            float halfW = StripHalfWidth * (0.45f + 0.55f * widthMul);

            //末端收尖，撞墙端略宽
            float tipPinch = hitWall ? 0.32f : 0.06f;

            var verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((origin + perp * halfW).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture((origin - perp * halfW).ToVector3(), Color.White, new Vector2(0f, 1f));
            verts[2] = new VertexPositionColorTexture((tip + perp * halfW * tipPinch).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[3] = new VertexPositionColorTexture((tip - perp * halfW * tipPinch).ToVector3(), Color.White, new Vector2(1f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            float overshoot = Age < ExpandTime + 2 ? 1f - Age / (float)(ExpandTime + 2) : 0f;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["fadeAlpha"]?.SetValue(widthMul);
            effect.Parameters["uOvershoot"]?.SetValue(overshoot);
            effect.Parameters["uHitWall"]?.SetValue(hitWall ? 1f : 0f);
            effect.Parameters["uLengthPx"]?.SetValue(rayLength + 10f);
            effect.Parameters["uStripLenPx"]?.SetValue(stripLen + 10f);
            effect.Parameters["uHalfWidthPx"]?.SetValue(halfW);
            effect.Parameters["seed"]?.SetValue((Projectile.whoAmI * 0.137f + PhaseSeed) % 1f);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);

            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (widthMul < 0.03f) {
                return;
            }
            Texture2D glow = CWRAsset.DiffusionCircle?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (glow == null || star == null) {
                return;
            }

            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Vector2 muzzle = Projectile.Center - Main.screenPosition;
            float flicker = 1f + 0.12f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 46f + PhaseSeed * 9f);

            Main.EntitySpriteDraw(glow, muzzle, null, ColViolet * (0.85f * widthMul), 0f, glow.Size() / 2f,
                0.5f * widthMul * flicker, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, muzzle, null, ColCore * (0.6f * widthMul), 0f, glow.Size() / 2f,
                0.28f * widthMul, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, muzzle, null, ColCore * (0.9f * widthMul), Main.GlobalTimeWrappedHourly * 4f,
                star.Size() / 2f, 0.42f * widthMul * flicker, SpriteEffects.None, 0);

            if (hitWall) {
                Vector2 hitPos = Projectile.Center + dir * rayLength - Main.screenPosition;
                Main.EntitySpriteDraw(glow, hitPos, null, ColCheren * (0.8f * widthMul), 0f, glow.Size() / 2f,
                    0.62f * widthMul * flicker, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, hitPos, null, ColCore * (0.65f * widthMul), 0f, glow.Size() / 2f,
                    0.34f * widthMul, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(star, hitPos, null, ColCore * (0.8f * widthMul), -Main.GlobalTimeWrappedHourly * 5f,
                    star.Size() / 2f, 0.5f * widthMul * flicker, SpriteEffects.None, 0);
            }
        }
    }
}
