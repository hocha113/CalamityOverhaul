using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class NurgleTheOfBall : ModProjectile
    {
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
                NPC target = Projectile.Center.FindClosestNPC(1300);
                if (target != null) {
                    _ = Projectile.ChasingBehavior(target.Center, 23);
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<Plague>(), 6000);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<Plague>(), 600);
        }

        public override void OnKill(int timeLeft) {
            Projectile.Explode(180, SoundID.Item14);
            int inc;
            for (int i = 4; i < 31; i = inc + 1) {
                float oldXPos = Projectile.oldVelocity.X * (30f / i);
                float oldYPos = Projectile.oldVelocity.Y * (30f / i);
                int killDust = Dust.NewDust(new Vector2(Projectile.oldPosition.X - oldXPos, Projectile.oldPosition.Y - oldYPos), 8, 8, DustID.Blood, Projectile.oldVelocity.X, Projectile.oldVelocity.Y, 100, default, 1.8f);
                Main.dust[killDust].noGravity = true;
                Dust dust2 = Main.dust[killDust];
                dust2.velocity *= 0.5f;
                dust2.color = Color.GreenYellow;
                killDust = Dust.NewDust(new Vector2(Projectile.oldPosition.X - oldXPos, Projectile.oldPosition.Y - oldYPos), 8, 8, DustID.JungleSpore, Projectile.oldVelocity.X, Projectile.oldVelocity.Y, 100, default, 1.4f);
                dust2 = Main.dust[killDust];
                dust2.velocity *= 0.05f;
                dust2.noGravity = true;
                inc = i;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            CalamityUtils.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor, 1);
            return false;
        }
    }
}
