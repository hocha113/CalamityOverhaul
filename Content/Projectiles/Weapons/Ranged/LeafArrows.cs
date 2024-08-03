using CalamityMod;
using CalamityMod.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class LeafArrows : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj_Ranged + "LeafArrow";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.alpha = 255;
            Projectile.penetrate = 1;
            Projectile.extraUpdates = 1;
            Projectile.timeLeft = 300;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.Calamity().pointBlankShotDuration = CalamityGlobalProjectile.DefaultPointBlankDuration;
        }

        public override void AI() {
            NPC target = Projectile.Center.FindClosestNPC(600);
            if (target != null) {
                if (Projectile.timeLeft > 30) {
                    Projectile.ChasingBehavior2(target.Center, 1, 0.05f);
                }
                else {
                    Projectile.ChasingBehavior2(target.Center, 1, 0.25f);
                }
            }

            Projectile.alpha -= 2;
            Projectile.ai[0] = Main.rand.Next(-100, 101) * 0.0025f;
            Projectile.ai[1] = Main.rand.Next(-100, 101) * 0.0025f;
            if (Projectile.localAI[0] == 0f) {
                Projectile.scale += 0.05f;
                if (Projectile.scale > 1.2) {
                    Projectile.localAI[0] = 1f;
                }
            }
            else {
                Projectile.scale -= 0.05f;
                if (Projectile.scale < 0.8) {
                    Projectile.localAI[0] = 0f;
                }
            }
            Projectile.rotation = (float)Math.Atan2(Projectile.velocity.Y, Projectile.velocity.X) + 3.14f;
            if (Projectile.localAI[1] <= 30f) {
                Projectile.localAI[1] += 1f;
                Projectile.velocity.Y *= 0.975f;
                Projectile.velocity.X *= 0.975f;
            }
            else if (Projectile.localAI[1] <= 60f) {
                Projectile.localAI[1] += 1f;
                Projectile.velocity.Y *= 1.025f;
                Projectile.velocity.X *= 1.025f;
            }
            else {
                Projectile.localAI[1] = 0f;
            }
        }

        public override Color? GetAlpha(Color lightColor) {
            return new Color(Main.DiscoR, 203, 103, Projectile.alpha);
        }

        public override bool PreDraw(ref Color lightColor) {
            CalamityUtils.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor, 2);
            return false;
        }

        public override void OnKill(int timeLeft) {
            _ = SoundEngine.PlaySound(SoundID.Grass, Projectile.position);
            Projectile.localAI[1] += 1f;
            for (int i = 0; i < 5; i++) {
                int dust = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.ChlorophyteWeapon, 0f, 0f, 0, new Color(Main.DiscoR, 203, 103), 1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity *= 3f;
                Main.dust[dust].scale = 1.5f;
            }
        }
    }
}
