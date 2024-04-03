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
    internal class CursedDartRemake : ModProjectile
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
            Projectile.timeLeft = 20 + Main.rand.Next(20);
            Projectile.light = 3.0f;
            Projectile.ignoreWater = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 0;
            Projectile.aiStyle = 1;
            Projectile.penetrate = 1;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        int lifenum = 5;
        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            if (Projectile.timeLeft == 10) {
                Projectile.timeLeft = 40;
                Projectile.NewProjectile(Projectile.parent(), Projectile.Center, new Vector2(0,0), ProjectileID.CursedDartFlame,
                        Projectile.damage / 2, 0.5f, Projectile.owner);
                lifenum -= 1;
            }
            if (lifenum == 0) Projectile.Kill();
        }

        public override Color? GetAlpha(Color lightColor) {
            return Color.White;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(39, 600);
        }
    }
}
