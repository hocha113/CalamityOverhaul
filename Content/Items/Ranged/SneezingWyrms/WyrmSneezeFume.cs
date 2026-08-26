using CalamityOverhaul.Content.Items.Magic.WheezingWyrms;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.SneezingWyrms
{
    /// <summary>
    /// 龙嚏烟云。浓烟漂浮无伤，撞墙贴停不消散，烧尽前散作薄烟。
    /// 烟体三瓣雾叠绘防贴纸感。<br/>
    /// ai0=烟色浓度(0~1)，ai1=扰动种子
    /// </summary>
    internal class WyrmSneezeFume : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "Fog")]
        private static Asset<Texture2D> FogTex = null;

        private const int LifeTime = 90;

        private float Density => Projectile.ai[0];
        private float Seed => Projectile.ai[1];
        private int Elapsed => LifeTime - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override bool? CanDamage() => false;

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.velocity = Vector2.Zero;
            return false;
        }

        public override void AI() {
            int elapsed = Elapsed;
            Projectile.velocity *= 0.94f;
            Projectile.velocity = Projectile.velocity.RotatedBy(MathF.Sin(elapsed * 0.11f + Seed) * 0.02f);
            Projectile.velocity.Y -= 0.008f;
            Projectile.rotation += (Seed % 1f - 0.5f) * 0.024f;
            Projectile.scale = 0.9f + elapsed * 0.006f;

            if (!VaultUtils.isServer && Main.rand.NextBool(7)) {
                PRTLoader.NewParticle<PRT_WyrmSmoke>(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f)
                    , Projectile.velocity * 0.3f - Vector2.UnitY * 0.25f
                    , new Color(96, 88, 82) * 0.45f, Main.rand.NextFloat(0.12f, 0.2f))
                    ?.Configure(Main.rand.Next(18, 30), 0.05f);
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_WyrmSmoke>(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f)
                    , -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.8f)
                    , new Color(112, 102, 94) * 0.5f, Main.rand.NextFloat(0.14f, 0.22f))
                    ?.Configure(Main.rand.Next(26, 42), 0.07f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D fog = FogTex?.Value;
            if (fog == null) {
                return false;
            }

            int elapsed = Elapsed;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            SpriteEffects fx = (int)Seed % 2 == 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            float fadeIn = MathF.Min(elapsed / 4f, 1f);
            float fadeOut = MathF.Min(Projectile.timeLeft / 18f, 1f);
            float fogA = fadeIn * fadeOut * (0.7f + Density * 0.15f);
            if (fogA <= 0f) {
                return false;
            }

            Color fogCol = new Color(98, 90, 84);
            float fogScale = Projectile.scale * 0.38f;
            for (int i = 0; i < 3; i++) {
                float ph = Seed * 1.3f + i * 2.09f;
                Vector2 off = (ph + Projectile.rotation * (i % 2 == 0 ? 1f : -1f)).ToRotationVector2() * (4f + i * 9f) * Projectile.scale;
                Main.EntitySpriteDraw(fog, pos + off, null, fogCol * (fogA * (1f - i * 0.2f))
                    , Projectile.rotation + ph, fog.Size() * 0.5f, fogScale * (1f - i * 0.22f), fx, 0);
            }
            return false;
        }
    }
}
