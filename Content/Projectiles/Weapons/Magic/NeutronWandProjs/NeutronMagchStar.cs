using CalamityMod;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.NeutronWandProjs
{
    internal class NeutronMagchStar : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Magic + "MagicStar2";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.MaxUpdates = 4;
            Projectile.penetrate = 13 * Projectile.MaxUpdates;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 3;
            Projectile.tileCollide = false;
            Projectile.alpha = 0;
            Projectile.timeLeft = 300;
            Projectile.ArmorPenetration = 80;
        }

        public override void AI() {
            if (Projectile.alpha > 0) {
                Projectile.alpha -= 25;
            }
            Lighting.AddLight(Projectile.Center, Color.Blue.ToVector3());
            Projectile.rotation += Projectile.velocity.X * 0.1f;
            BasePRT spark2 = new PRT_HeavenfallStar(Projectile.Center + VaultUtils.RandVr(8)
                        , Projectile.velocity.UnitVector() * Main.rand.Next(6, 16), false
                        , 7, Main.rand.NextFloat(0.2f, 0.3f), Color.BlueViolet);
            PRTLoader.AddParticle(spark2);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<VoidErosion>(), 1200);
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 6; i++) {
                Vector2 vr = VaultUtils.RandVr(6);
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.BlueTorch, vr.X, vr.Y);
                Main.dust[Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.FireworkFountain_Blue, vr.X, vr.Y)].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            CalamityUtils.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], Color.White, 1);
            return false;
        }
    }
}
