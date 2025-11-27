using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Projectiles.Melee;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class BrimstoneSwordBall : ModProjectile
    {
        public override string Texture => "CalamityMod/Items/Weapons/Melee/BrimstoneSword";
        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.timeLeft = 600;
            AIType = ProjectileID.BoneJavelin;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.MaxUpdates = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10 * Projectile.MaxUpdates;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 4;
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center + Projectile.velocity, Color.Red.ToVector3() * 0.6f);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            if (Main.rand.NextBool(4)) {
                _ = Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height
                    , CWRID.Dust_Brimstone, Projectile.velocity.X * 0.5f, Projectile.velocity.Y * 0.5f);
            }
            if (Projectile.spriteDirection == -1) {
                Projectile.rotation -= MathHelper.ToRadians(90f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.timeLeft > 595) {
                return false;
            }
            CalamityUtils.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor, 1);
            return false;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.NewProjectile(Projectile.FromObjectGetParent(), Projectile.Center, Vector2.Zero
                , ModContent.ProjectileType<Brimblast>(), Projectile.damage, Projectile.knockBack, Main.myPlayer);
            return true;
        }

        public override void OnKill(int timeLeft) {
            for (int k = 0; k < 5; k++) {
                _ = Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height
                    , CWRID.Dust_Brimstone, Projectile.oldVelocity.X * 0.5f, Projectile.oldVelocity.Y * 0.5f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<BrimstoneFlames>(), 180);
            if (Projectile.IsOwnedByLocalPlayer() && Projectile.numHits == 0) {
                _ = Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero
                    , ModContent.ProjectileType<BrimstoneSwordExplosion>(), (int)(Projectile.damage * 0.5), hit.Knockback, Projectile.owner);
            }
            if (Projectile.damage > 1) {
                Projectile.damage = (int)(Projectile.damage * 0.6);
            }
            else {
                Projectile.Kill();
            }
        }
    }
}
