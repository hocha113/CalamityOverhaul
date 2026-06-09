using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Marbles
{
    /// <summary>
    /// 大理石冲击波：落点 / 砸地处快速扩张的环状冲击，命中范围内敌人一次
    /// <br/>ai[0] = 计时，ai[1] = 最大半径
    /// </summary>
    internal class MarbleShockwave : ModProjectile, IAdditiveDrawable, IWarpDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        private const int Life = 24;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        private float MaxRadius => Projectile.ai[1] <= 0f ? 120f : Projectile.ai[1];
        private float Progress => MathHelper.Clamp((Life - Projectile.timeLeft) / (float)Life, 0f, 1f);
        private float Radius => MathHelper.SmoothStep(8f, MaxRadius, Progress);

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            Projectile.ai[0]++;
            Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.MarbleCore.ToVector3() * (1f - Progress) * 0.8f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            bool outer = VaultUtils.CircleIntersectsRectangle(Projectile.Center, Radius, targetHitbox);
            bool inner = VaultUtils.CircleIntersectsRectangle(Projectile.Center, Radius * 0.55f, targetHitbox);
            return outer && !inner;
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Texture2D ring = CWRAsset.DiffusionCircle.Value;
            float fade = 1f - Progress;
            float scale = Radius / (ring.Width * 0.5f);

            Color gold = GraniteMarbleVFX.MarbleGold; gold.A = 0;
            Color core = GraniteMarbleVFX.MarbleCore; core.A = 0;
            spriteBatch.Draw(ring, pos, null, gold * fade * 0.85f, Projectile.rotation, ring.Size() / 2f, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(ring, pos, null, core * fade * 0.6f, Projectile.rotation, ring.Size() / 2f, scale * 0.8f, SpriteEffects.None, 0f);
        }

        bool IWarpDrawable.CanDrawCustom() => false;
        void IWarpDrawable.DrawCustom(SpriteBatch spriteBatch) { }
        void IWarpDrawable.Warp() {
            Texture2D ring = CWRAsset.DiffusionCircle.Value;
            float scale = Radius / (ring.Width * 0.5f);
            Color warp = new Color(50, 50, 50) * (1f - Progress) * 0.7f;
            Main.spriteBatch.Draw(ring, Projectile.Center - Main.screenPosition, null, warp, Projectile.rotation
                , ring.Size() / 2f, scale, SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// 大理石碎片：翻滚迸射的石屑，落地反弹一次后碎裂，扬起尘土
    /// </summary>
    internal class MarbleShard : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            Projectile.velocity.Y += 0.32f;
            Projectile.velocity.X *= 0.99f;
            if (Projectile.velocity.Y > 16f) {
                Projectile.velocity.Y = 16f;
            }
            Projectile.rotation += 0.3f * Math.Sign(Projectile.velocity.X);
            Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.MarbleGold.ToVector3() * 0.35f);

            if (Main.rand.NextBool(4) && !VaultUtils.isServer) {
                PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center, Projectile.velocity * 0.1f
                    , GraniteMarbleVFX.MarbleDust, Main.rand.NextFloat(0.25f, 0.45f)).Configure(22, 0.6f, 0.04f);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > 0.5f) {
                    Projectile.velocity.X = -oldVelocity.X * 0.5f;
                }
                if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > 0.5f) {
                    Projectile.velocity.Y = -oldVelocity.Y * 0.45f;
                }
                if (!VaultUtils.isServer) {
                    PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center, Vector2.Zero
                        , GraniteMarbleVFX.MarbleDust, 0.5f).Configure(24, 0.7f, 0.05f);
                }
                return false;
            }
            Projectile.Kill();
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center, Main.rand.NextVector2Circular(2f, 2f)
                    , GraniteMarbleVFX.MarbleDust, Main.rand.NextFloat(0.3f, 0.5f)).Configure(20, 0.7f, 0.05f);
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Color gold = GraniteMarbleVFX.MarbleGold; gold.A = 0;
            Color core = GraniteMarbleVFX.MarbleCore; core.A = 0;
            spriteBatch.Draw(glow, pos, null, gold * 0.6f, 0f, glow.Size() / 2f, 0.4f, SpriteEffects.None, 0f);
            spriteBatch.Draw(star, pos, null, core * 0.8f, Projectile.rotation, star.Size() / 2f, 0.07f, SpriteEffects.None, 0f);
        }
    }
}
