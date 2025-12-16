using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class EnforcerFlame : ModProjectile
    {
        public override string Texture => "CalamityMod/Projectiles/Healing/EssenceFlame";
        public override bool IsLoadingEnabled(Mod mod) => CWRRef.Has;
        public Player Owner => Main.player[Projectile.owner];
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }
        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 150;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override void AI() {
            if (!Owner.Alives()) {
                Projectile.Kill();
                return;
            }

            VaultUtils.ClockFrame(ref Projectile.frame, 5, 3);
            float scaleModd = Main.mouseTextColor / 200f - 0.35f;
            scaleModd *= 0.2f;
            Projectile.scale = scaleModd + 0.95f;

            Vector2 toOwner = Projectile.position.To(Owner.position);
            float projDistance = toOwner.Length() / 80f;
            if (projDistance > 17.3333f) {
                projDistance = 17.3333f;
            }
            if (Projectile.ai[0] == 0) {
                Projectile.velocity = toOwner.UnitVector() * projDistance;
            }

            Projectile.rotation += Projectile.velocity.X * 0.03f;
            if (Projectile.rotation > MathHelper.ToRadians(12)) {
                Projectile.rotation = MathHelper.ToRadians(12);
            }
            if (Projectile.rotation < MathHelper.ToRadians(-12)) {
                Projectile.rotation = MathHelper.ToRadians(-12);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => target.AddBuff(BuffID.Frostburn, 180);

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
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Rectangle rectangle = texture.GetRectangle(Projectile.frame, 4);
            Vector2 drawOrigin = rectangle.Size() / 2;

            float bmtSize = 1f;
            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = (Projectile.oldPos[k] - Main.screenPosition) + Projectile.Size / 2;
                Color color = Color.White * (float)(((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length) / 2);
                Main.EntitySpriteDraw(texture, drawPos, rectangle, color, Projectile.rotation, drawOrigin, Projectile.scale * bmtSize, SpriteEffects.None, 0);
                bmtSize -= 0.02f;
            }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, rectangle, Color.White, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
