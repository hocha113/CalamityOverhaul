using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class NurgleTheOfBall : ModProjectile
    {
        public override bool IsLoadingEnabled(Mod mod) => CWRRef.Has;
        public override string Texture => CWRConstant.Cay_Proj_Ranged + "ContagionBall";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 60;
            Projectile.DamageType = DamageClass.Ranged;
        }

        public override void AI() {
            Projectile.tileCollide = Projectile.ai[0] != 1;
            Projectile.rotation += 0.1f;
            if (Projectile.timeLeft < 30) {
                NPC target = Projectile.Center.FindClosestNPC(500);
                if (target != null) {
                    _ = Projectile.ChasingBehavior(target.Center, 23);
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(CWRID.Buff_Plague, 6000);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(CWRID.Buff_Plague, 600);
        }

        public override void OnKill(int timeLeft) {
            Projectile.Explode(180, default, false);
            Vector2 spawnPos = Projectile.Center;
            Vector2 velocity = Main.rand.NextVector2Circular(0.8f, 0.8f);
            float depth = Main.rand.NextFloat(0.3f, 1f);

            PRT_ToxicMist acidMist = new(
                spawnPos,
                velocity,
                Main.rand.NextFloat(0.5f, 0.75f),
                Main.rand.Next(50, 75),
                depth
            );
            PRTLoader.AddParticle(acidMist);
        }

        public override bool PreDraw(ref Color lightColor) {
            CWRRef.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor, 1);
            return false;
        }
    }
}
