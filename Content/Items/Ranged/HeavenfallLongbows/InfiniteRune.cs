using CalamityOverhaul.Content.DamageModify;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.HeavenfallLongbows
{
    internal class InfiniteRune : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 63;
        }

        public override void SetDefaults() {
            Projectile.width = 44;
            Projectile.height = 44;
            Projectile.DamageType = EndlessDamageClass.Instance;
            Projectile.tileCollide = false;
            Projectile.alpha = 255;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            //引爆前先撒一圈极光丝带作为视觉冲击 (Explode 之后 PRT 仍正常播放, 因为 PRTLoader 是独立的渲染系统)
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 10; i++) {
                    float ang = MathHelper.TwoPi * i / 10f + Main.rand.NextFloat(-0.08f, 0.08f);
                    Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(6f, 11f);
                    PRTLoader.AddParticle(new PRT_HeavenfallAurora(
                        Projectile.Center, vel,
                        Main.rand.NextFloat(150f, 230f), Main.rand.NextFloat(26f, 36f),
                        Main.rand.Next(40, 60),
                        huePhase: i / 10f, hueSpeed: 0.028f, driftScale: 1.5f));
                }
                //内圈细密棱镜碎片
                for (int i = 0; i < 18; i++) {
                    float ang = MathHelper.TwoPi * i / 18f;
                    Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(3f, 7f);
                    Color col = VaultUtils.MultiStepColorLerp(
                        i / 18f, HeavenfallLongbow.rainbowColors);
                    PRTLoader.AddParticle(new PRT_HeavenfallPrism(
                        Projectile.Center, vel, col,
                        Main.rand.NextFloat(1.0f, 1.6f), Main.rand.Next(30, 45),
                        Main.rand.NextFloat(4f, 7f), shortStretch: true));
                }
            }

            Projectile.Explode(1600, spanSound: false);
            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor) {
            return false;
        }
    }
}
