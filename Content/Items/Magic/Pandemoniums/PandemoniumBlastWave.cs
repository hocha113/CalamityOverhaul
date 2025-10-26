using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Pandemoniums
{
    /// <summary>
    /// 爆炸波
    /// </summary>
    internal class PandemoniumBlastWave : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private ref float ExpandTimer => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            ExpandTimer++;
            float progress = ExpandTimer / 60f;

            Projectile.scale = progress * 15f;
            Projectile.width = Projectile.height = (int)(50 + progress * 600f);

            if (Main.rand.NextBool(1)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float distance = progress * 400f;
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * distance;

                Dust d = Dust.NewDustPerfect(pos, DustID.Torch, angle.ToRotationVector2() * 5f,
                    100, Color.OrangeRed, 2.5f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 3.0f * progress, 1.0f * progress, 0.5f * progress);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 360);
            target.AddBuff(BuffID.Ichor, 300);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!(CWRAsset.SoftGlow?.IsLoaded ?? false)) return false;

            SpriteBatch sb = Main.spriteBatch;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 center = Projectile.Center - Main.screenPosition;
            float progress = ExpandTimer / 60f;
            float alpha = 1f - progress;

            Color c1 = new Color(255, 200, 100, 0) * alpha;
            Color c2 = new Color(255, 100, 50, 0) * alpha * 0.8f;
            Color c3 = new Color(200, 50, 30, 0) * alpha * 0.6f;

            sb.Draw(glow, center, null, c3, 0, glow.Size() / 2, Projectile.scale * 1.2f, 0, 0);
            sb.Draw(glow, center, null, c2, 0, glow.Size() / 2, Projectile.scale, 0, 0);
            sb.Draw(glow, center, null, c1, 0, glow.Size() / 2, Projectile.scale * 0.7f, 0, 0);

            return false;
        }
    }
}
