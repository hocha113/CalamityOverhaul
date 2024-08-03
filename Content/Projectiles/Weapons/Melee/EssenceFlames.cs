using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class EssenceFlames : ModProjectile
    {
        public override string Texture => "CalamityMod/Projectiles/Healing/EssenceFlame";
        public new string LocalizationCategory => "Projectiles.Melee";
        public Player Owner => Main.player[Projectile.owner];
        public Texture2D mainValue => CWRUtils.GetT2DValue(Texture);
        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 150;
            Projectile.DamageType = DamageClass.Melee;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 15;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void AI() {
            float scaleModd = Main.mouseTextColor / 200f - 0.35f;
            scaleModd *= 0.2f;
            Projectile.scale = scaleModd + 0.95f;

            Vector2 toOwner = Projectile.position.To(Owner.position);
            float projDistance = toOwner.Length() / 80f;
            if (projDistance > 17.3333f)
                projDistance = 17.3333f;
            if (Projectile.ai[0] == 0)
                Projectile.velocity = toOwner.UnitVector() * projDistance;
            Projectile.rotation += Projectile.velocity.X * 0.03f;
            if (Projectile.rotation > MathHelper.ToRadians(12)) {
                Projectile.rotation = MathHelper.ToRadians(12);
            }
            if (Projectile.rotation < MathHelper.ToRadians(-12)) {
                Projectile.rotation = MathHelper.ToRadians(-12);
            }
        }

        public override Color? GetAlpha(Color lightColor) {
            return new Color(200, 200, 200, Projectile.alpha);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn, 180);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item74, Projectile.Center);
            Projectile.Explode(75);
            for (int i = 0; i < 5; i++) {
                int essenceDust = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.ShadowbeamStaff, 0f, 0f, 100, default, 2f);
                Main.dust[essenceDust].velocity *= 3f;
                if (Main.rand.NextBool()) {
                    Main.dust[essenceDust].scale = 0.5f;
                    Main.dust[essenceDust].fadeIn = 1f + Main.rand.Next(10) * 0.1f;
                }
            }
            for (int j = 0; j < 8; j++) {
                int essenceDust2 = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.ShadowbeamStaff, 0f, 0f, 100, default, 3f);
                Main.dust[essenceDust2].noGravity = true;
                Main.dust[essenceDust2].velocity *= 5f;
                essenceDust2 = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.ShadowbeamStaff, 0f, 0f, 100, default, 2f);
                Main.dust[essenceDust2].velocity *= 2f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            CWRUtils.ClockFrame(ref Projectile.frame, 5, 3);
            Main.EntitySpriteDraw(mainValue, Projectile.Center - Main.screenPosition, CWRUtils.GetRec(mainValue, Projectile.frame, 4)
                , Color.White, Projectile.rotation, CWRUtils.GetOrig(mainValue, 4), Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
