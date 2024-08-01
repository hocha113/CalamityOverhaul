using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class GodKillers : ModProjectile
    {
        public new string LocalizationCategory => "Projectiles.Typeless";

        public override string Texture => CWRConstant.Projectile_Melee + "GodKiller";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 46;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.alpha = 255;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
            Projectile.MaxUpdates = 3;
        }

        public override void AI() {
            if (Projectile.alpha > 0) {
                Projectile.alpha -= 5;
            }
            if (Projectile.ai[2] > 0) {
                Projectile.ai[2]--;
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            if (Projectile.owner == Main.myPlayer && Projectile.timeLeft <= 3) {
                Projectile.tileCollide = false;
                Projectile.ai[1] = 0f;
                Projectile.alpha = 255;
                Projectile.position.X = Projectile.position.X + Projectile.width / 2;
                Projectile.position.Y = Projectile.position.Y + Projectile.height / 2;
                Projectile.width = 100;
                Projectile.height = 100;
                Projectile.position.X = Projectile.position.X - Projectile.width / 2;
                Projectile.position.Y = Projectile.position.Y - Projectile.height / 2;
                Projectile.knockBack = 5f;
            }
            else if (Projectile.velocity.LengthSquared() > 4) {
                for (int i = 0; i < 2; i++) {
                    float num = 0f;
                    float num2 = 0f;
                    if (i == 1) {
                        num = Projectile.velocity.X * 0.5f;
                        num2 = Projectile.velocity.Y * 0.5f;
                    }

                    int num3 = Dust.NewDust(new Vector2(Projectile.position.X + 3f + num, Projectile.position.Y + 3f + num2) - Projectile.velocity * 0.5f
                        , Projectile.width - 8, Projectile.height - 8, DustID.ShadowbeamStaff, 0f, 0f, 100);
                    Main.dust[num3].scale *= 1f + Main.rand.Next(5) * 0.1f;
                    Main.dust[num3].velocity *= 0.2f;
                    Main.dust[num3].noGravity = true;
                }
            }

            //CalamityUtils.HomeInOnNPC(Projectile, ignoreTiles: true, 1300f, 12f, 25);
        }

        public override void PostDraw(Color lightColor) {
            Main.EntitySpriteDraw(origin: new Vector2(11f, 23f), texture: ModContent.Request<Texture2D>(CWRConstant.Projectile_Melee + "GodKillerGlow").Value
                , position: Projectile.Center - Main.screenPosition, sourceRectangle: null, color: Color.White, rotation: Projectile.rotation, scale: 1f, effects: SpriteEffects.None);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(in SoundID.Item89, Projectile.position);
            for (int i = 0; i < 5; i++) {
                int num = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.Butterfly, 0f, 0f, 100, default, 1.5f);
                Main.dust[num].velocity *= 3f;
                if (Main.rand.NextBool()) {
                    Main.dust[num].scale = 0.5f;
                    Main.dust[num].fadeIn = 1f + Main.rand.Next(10) * 0.1f;
                }
            }

            for (int j = 0; j < 10; j++) {
                int num2 = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.ShadowbeamStaff, 0f, 0f, 100, default, 2f);
                Main.dust[num2].noGravity = true;
                Main.dust[num2].velocity *= 5f;
                num2 = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.ShadowbeamStaff, 0f, 0f, 100, default, 1.5f);
                Main.dust[num2].velocity *= 2f;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return Projectile.timeLeft > 80 || Projectile.ai[2] > 0 ? false : null;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<GodSlayerInferno>(), 120);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<GodSlayerInferno>(), 120);
        }

        public override bool PreDraw(ref Color lightColor) {
            CalamityUtils.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor, 2);
            return false;
        }
    }
}
