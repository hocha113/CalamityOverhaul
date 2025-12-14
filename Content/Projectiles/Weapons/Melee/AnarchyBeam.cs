using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Projectiles.Melee;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class AnarchyBeam : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj_Melee + "BrimlashProj";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 4;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            Projectile.velocity *= 0.99f;
            Projectile.scale += 0.007f;
            if (Projectile.velocity.Length() < 3f) {
                Projectile.Kill();
            }
            Lighting.AddLight(Projectile.Center, (255 - Projectile.alpha) * 0.5f / 255f, (255 - Projectile.alpha) * 0.05f / 255f, (255 - Projectile.alpha) * 0.05f / 255f);
            int brimDust = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, CWRID.Dust_Brimstone, 0f, 0f, 100, default, 1f);
            Main.dust[brimDust].noGravity = true;
            Main.dust[brimDust].velocity *= 0.5f;
            Main.dust[brimDust].velocity += Projectile.velocity * 0.1f;
        }

        public override Color? GetAlpha(Color lightColor) {
            return Color.White;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.timeLeft > 295) {
                return false;
            }
            Projectile.DrawStarTrail(Color.Red, Color.White);
            CWRRef.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor, 1);
            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item69 with { Pitch = 0.8f, Volume = 0.6f }, Projectile.Center);
            int inc;
            for (int i = 4; i < 31; i = inc + 1) {
                float dustX = Projectile.oldVelocity.X * (30f / i);
                float dustY = Projectile.oldVelocity.Y * (30f / i);
                int deathDust = Dust.NewDust(new Vector2(Projectile.oldPosition.X - dustX, Projectile.oldPosition.Y - dustY), 8, 8, CWRID.Dust_Brimstone, Projectile.oldVelocity.X, Projectile.oldVelocity.Y, 100, default, 1.8f);
                Main.dust[deathDust].noGravity = true;
                Dust dust = Main.dust[deathDust];
                dust.velocity *= 0.5f;
                deathDust = Dust.NewDust(new Vector2(Projectile.oldPosition.X - dustX, Projectile.oldPosition.Y - dustY), 8, 8, CWRID.Dust_Brimstone, Projectile.oldVelocity.X, Projectile.oldVelocity.Y, 100, default, 1.4f);
                dust = Main.dust[deathDust];
                dust.velocity *= 0.05f;
                inc = i;
            }

            if (Projectile.IsOwnedByLocalPlayer()) {
                for (int i = 0; i < 13; i++) {
                    _ = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + VaultUtils.RandVr(222), Vector2.Zero
                        , ModContent.ProjectileType<BrimstoneBoom>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<BrimstoneFlames>(), 180);
        }
    }
}
