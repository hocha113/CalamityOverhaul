using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class HyperBlade : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Melee + "BlazingPhantomBlade";//TODO:临时性弹幕，有待考证
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.aiStyle = ProjAIStyleID.Sickle;
            Projectile.alpha = 100;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.ignoreWater = true;
            AIType = ProjectileID.DeathSickle;
            Projectile.MaxUpdates = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15 * Projectile.MaxUpdates;
            Projectile.timeLeft = 150 * Projectile.MaxUpdates;
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, 0f, 0.5f, 0f);

            CWRRef.HomeInOnNPC(Projectile, true, 350f, 15f, 10f);
        }

        public override Color? GetAlpha(Color lightColor) {
            if (Projectile.timeLeft < 85) {
                byte b2 = (byte)(Projectile.timeLeft * 3);
                byte a2 = (byte)(100f * (b2 / 255f));
                return new Color(b2, b2, b2, a2);
            }
            return new Color(255, 255, 255, 100);
        }

        public override bool PreDraw(ref Color lightColor) {
            CWRRef.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor, 1);
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.CursedInferno, 180);
        }
    }
}
