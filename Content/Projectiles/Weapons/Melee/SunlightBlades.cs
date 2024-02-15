using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class SunlightBlades : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Melee + "BlazingPhantomBlade";
        public new string LocalizationCategory => "Projectiles.Melee";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 3;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.alpha = 100;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 190;
            Projectile.rotation = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, 0.25f, 0.25f, 0f);
            Projectile.rotation += 0.5f;
            Projectile.velocity *= 1.01f;
        }

        public override Color? GetAlpha(Color lightColor) {
            if (Projectile.timeLeft < 85) {
                byte b = (byte)(Projectile.timeLeft * 3);
                byte alpha = (byte)(100f * (b / 255f));
                return new Color(b, b, b, alpha);
            }

            return new Color(255, 255, 255, 100);
        }

        public override bool PreDraw(ref Color lightColor) {
            CalamityUtils.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor);
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<HolyFlames>(), 180);
        }
    }
}
