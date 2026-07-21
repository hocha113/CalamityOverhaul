using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Stones.Granites
{
    /// <summary>
    /// 花岗共享晶片。无 ai；轻追踪 380px，MaxUpdates=2，寿命 55tick，穿透 1
    /// </summary>
    internal class GraniteCrystalShard : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        private Trail Trail;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 55;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.MaxUpdates = 2;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 22;
        }

        public override void AI() {
            Projectile.ai[0]++;
            NPC target = Projectile.Center.FindClosestNPC(380f);
            if (target != null) {
                Vector2 desired = Projectile.Center.To(target.Center).UnitVector() * 11f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.06f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Projectile.timeLeft < 18) {
                Projectile.scale = Projectile.timeLeft / 18f;
            }
            Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.GraniteCore.ToVector3() * 0.5f);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_GraniteShard>(Projectile.Center
                    , Main.rand.NextVector2Circular(2.6f, 2.2f) - Vector2.UnitY * Main.rand.NextFloat(1f, 2.5f)
                    , GraniteMarbleVFX.GraniteSpark, Main.rand.NextFloat(0.5f, 0.8f))
                    .Configure(Main.rand.Next(24, 36));
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_GraniteVolt>(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f)
                    , Main.rand.NextVector2Unit() * 2f, GraniteMarbleVFX.GraniteCore
                    , Main.rand.NextFloat(0.24f, 0.4f)).Configure(Main.rand.Next(3, 6));
            }
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero
                , GraniteMarbleVFX.GraniteSpark, 0.35f).Configure(12, 1f, 1.2f);
        }

        public float GetWidthFunc(float completionRatio) {
            //半宽上限 6.5px，贴 12px 体
            float progress = completionRatio > 0.5f ? 1f - completionRatio : completionRatio;
            return progress * 2f * Projectile.scale * 6.5f;
        }

        public Color GetColorFunc(Vector2 completionRatio) => Color.White * Projectile.Opacity;

        void IPrimitiveDrawable.DrawPrimitives() {
            float fade = MathHelper.Clamp(Projectile.timeLeft / 18f, 0f, 1f);
            GraniteMarbleVFX.DrawGraniteArcTrailFromOldPos(Projectile, ref Trail
                , GetWidthFunc, GetColorFunc, fade);
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            //Line 竖向，+PiOver2
            Texture2D sliver = CWRAsset.Line.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = sliver.Size() / 2f;
            float rot = Projectile.rotation + MathHelper.PiOver2;
            float s = Projectile.scale;

            Color deep = GraniteMarbleVFX.GraniteDeep; deep.A = 0;
            Color core = GraniteMarbleVFX.GraniteCore; core.A = 0;
            Color spark = GraniteMarbleVFX.GraniteSpark; spark.A = 0;

            spriteBatch.Draw(glow, pos, null, deep * 0.55f * s, 0f, glow.Size() / 2f, s * 0.5f, SpriteEffects.None, 0f);
            spriteBatch.Draw(sliver, pos, null, core * 0.65f * s, rot + 0.42f, origin, new Vector2(0.07f, 0.10f) * s, SpriteEffects.None, 0f);
            spriteBatch.Draw(sliver, pos, null, core * 0.65f * s, rot - 0.42f, origin, new Vector2(0.07f, 0.10f) * s, SpriteEffects.None, 0f);
            spriteBatch.Draw(sliver, pos, null, spark * 0.95f * s, rot, origin, new Vector2(0.10f, 0.17f) * s, SpriteEffects.None, 0f);
            spriteBatch.Draw(sliver, pos, null, Color.White * 0.8f * s, rot, origin, new Vector2(0.045f, 0.13f) * s, SpriteEffects.None, 0f);
        }
    }
}
