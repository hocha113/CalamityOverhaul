using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class CursedDartRemake : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            TextureAssets.Projectile[Type] = TextureAssets.Projectile[ProjectileID.CursedDart];
        }

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.timeLeft = 20;
            Projectile.light = 3.0f;
            Projectile.ignoreWater = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 0;
            Projectile.aiStyle = 1;
            Projectile.penetrate = 1;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void AI() {
            if (Projectile.ai[2] > 0) {
                Projectile.timeLeft = 20;
                Projectile.ai[2]--;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            if (Projectile.timeLeft == 10) {
                Projectile.timeLeft = 40;
                if (Projectile.IsOwnedByLocalPlayer() && Projectile.ai[2] <= 0) {
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center
                        , Vector2.Zero, ProjectileID.CursedDartFlame, Projectile.damage / 2, 0.5f, Projectile.owner);
                }
                Projectile.ai[1]++;
                if (Projectile.ai[1] >= 5) {
                    Projectile.Kill();
                }
            }
        }

        public override Color? GetAlpha(Color lightColor) {
            return Color.White;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(39, 600);
        }
    }
}
