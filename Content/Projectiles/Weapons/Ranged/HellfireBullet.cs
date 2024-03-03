using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using CalamityOverhaul.Common;
using static Humanizer.In;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class HellfireBullet : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "HellfireBullet";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.timeLeft = 1200;
            Projectile.light = 3.0f;
            Projectile.ignoreWater = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 0;
            Projectile.aiStyle = 0;
            Projectile.penetrate = 1;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, 174, 0f, 0f, 100, Color.Red, 0.8f);
            dust.velocity += Projectile.velocity / 2;
            dust.noGravity = true;
        }

        public override Color? GetAlpha(Color lightColor) {
            return Color.White;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(24, 600);
        }


        public override void OnKill(int timeLeft) {
            Collision.HitTiles(Projectile.position + Projectile.velocity, Projectile.velocity, Projectile.width, Projectile.height);
            SoundEngine.PlaySound(SoundID.Item10, Projectile.position);
            for (int i = 0; i < 10; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, 174, 0f, 0f, 100, Color.Red, 0.7f);
                Dust dust1 = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, 213, 0f, 0f, 100, Color.Yellow, 0.7f);
                dust.velocity *= 1.5f;
                dust1.velocity *= 1.5f;
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
            //for (int i = 0; i < 10; i++) {
            //    int num = Dust.NewDust(new Vector2(base.Projectile.position.X, base.Projectile.position.Y), base.Projectile.width, base.Projectile.height, 174, 0f, 0f, 10, Color.White, 0.2f);
            //    Main.dust[num].velocity *= 2f;
            //    if (Main.rand.NextBool(2)) {
            //        Main.dust[num].scale = 0.1f;
            //        Main.dust[num].fadeIn = 1f + (float)Main.rand.Next(10) * 0.1f;
            //    }
            //}
        }
    }
}
