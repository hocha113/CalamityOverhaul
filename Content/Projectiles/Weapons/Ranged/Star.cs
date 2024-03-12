using CalamityMod;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    public class Star : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.timeLeft = 1200;
            Projectile.penetrate = 1;
        }

        Player Player => Main.player[Projectile.owner];

        Player Owner => Main.player[Projectile.owner];

        Vector2 OwnerPos => Owner.Center;
        Vector2 ToMou => Main.MouseWorld - OwnerPos;

        int count = 0;
        public override void AI() {
            Lighting.AddLight(Projectile.Center, Color.White.ToVector3());
            count++;
            Projectile.velocity *= 1.01f;
            Projectile.rotation += count/60f;
            for (int i = 0; i < 1; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, 15, 0f, 0f, 1, Color.White, 1f);
                Dust dust1 = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, 57, 0f, 0f, 1, Color.White, 1f);
                Dust dust2 = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, 58, 0f, 0f, 1, Color.White, 1f);
                dust.velocity = Projectile.velocity * 0.8f;
                dust1.velocity = Projectile.velocity * 0.8f;
                dust2.velocity = Projectile.velocity * 0.8f;
            }
        }

        public override Color? GetAlpha(Color lightColor) {
            return Color.White;
        }

        public override void OnKill(int timeLeft) {
            Collision.HitTiles(Projectile.position + Projectile.velocity, Projectile.velocity, Projectile.width, Projectile.height);
            SoundEngine.PlaySound(SoundID.Item10, Projectile.position);
            for (int i = -5; i < 0; i++) {
                    Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Blood, 0f, 0f, 500, Color.White, 1.5f);
                    dust.velocity *= 3;
            }
        }

        public override void Kill(int timeLeft) {
            //爆炸
            SoundEngine.PlaySound(in SoundID.Item14, base.Projectile.Center);
            base.Projectile.position = base.Projectile.Center;
            base.Projectile.width = (base.Projectile.height = 80);
            base.Projectile.position.X = base.Projectile.position.X - (float)(base.Projectile.width / 2);
            base.Projectile.position.Y = base.Projectile.position.Y - (float)(base.Projectile.height / 2);
            base.Projectile.maxPenetrate = -1;
            base.Projectile.penetrate = -1;
            base.Projectile.usesLocalNPCImmunity = false;
            base.Projectile.usesIDStaticNPCImmunity = true;
            base.Projectile.idStaticNPCHitCooldown = 0;
            base.Projectile.Damage();
            for (int i = 0; i < 10; i++) {
                int num = Dust.NewDust(new Vector2(base.Projectile.position.X, base.Projectile.position.Y), base.Projectile.width, base.Projectile.height, DustID.Blood, 0f, 0f, 100, Color.White, 1.2f);
                Main.dust[num].velocity *= 3f;
                if (Main.rand.NextBool(2)) {
                    Main.dust[num].scale = 0.5f;
                    Main.dust[num].fadeIn = 1f + (float)Main.rand.Next(10) * 0.1f;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            TextureAssets.Projectile[Type] = TextureAssets.Projectile[ProjectileID.FallingStar];
            CalamityUtils.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], Color.White, 1);
            return false;
        }
    }
}
