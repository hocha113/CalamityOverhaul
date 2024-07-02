using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class HolyColliderHolyFires : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj_Melee + "HolyColliderHolyFire";
        public new string LocalizationCategory => "Projectiles.Melee";

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 4;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 90;
        }

        public override bool? CanHitNPC(NPC target) {
            return Projectile.timeLeft < 75 && target.CanBeChasedBy(Projectile);
        }

        public override void AI() {
            Projectile.frameCounter++;
            if (Projectile.frameCounter > 6) {
                Projectile.frame++;
                Projectile.frameCounter = 0;
            }
            if (Projectile.frame > 3) {
                Projectile.frame = 0;
            }

            float toO = Projectile.position.To(Main.player[Projectile.owner].position).Length() / 50f;
            if (toO > 17)
                toO = 17;
            if (Projectile.timeLeft > 60) {
                Projectile.ChasingBehavior(Main.player[Projectile.owner].Center, toO);
            }
            else {
                NPC npc = Projectile.Center.FindClosestNPC(300);
                if (npc != null) {
                    Projectile.velocity *= 1.01f;
                    Projectile.ChasingBehavior(npc.Center, Projectile.velocity.Length());
                }
                else {
                    Projectile.ChasingBehavior(Main.player[Projectile.owner].Center, toO);
                }
            }
        }

        public override Color? GetAlpha(Color lightColor) {
            return new Color(250, 150, 0, Projectile.alpha);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture2D13 = ModContent.Request<Texture2D>(Texture).Value;
            int framing = ModContent.Request<Texture2D>(Texture).Value.Height / Main.projFrames[Projectile.type];
            int y6 = framing * Projectile.frame;
            Main.spriteBatch.Draw(texture2D13, Projectile.Center - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY), new Microsoft.Xna.Framework.Rectangle?(new Rectangle(0, y6, texture2D13.Width, framing)), Projectile.GetAlpha(lightColor), Projectile.rotation, new Vector2(texture2D13.Width / 2f, framing / 2f), Projectile.scale, SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            _ = SoundEngine.PlaySound(SoundID.Item20, Projectile.position);
            Projectile.position.X = Projectile.position.X + Projectile.width / 2;
            Projectile.position.Y = Projectile.position.Y + Projectile.height / 2;
            Projectile.width = 150;
            Projectile.height = 150;
            Projectile.position.X = Projectile.position.X - Projectile.width / 2;
            Projectile.position.Y = Projectile.position.Y - Projectile.height / 2;
            for (int i = 0; i < 5; i++) {
                int holy = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.CopperCoin, 0f, 0f, 100, default, 2f);
                if (Main.rand.NextBool()) {
                    Main.dust[holy].scale = 0.5f;
                    Main.dust[holy].fadeIn = 1f + (Main.rand.Next(10) * 0.1f);
                }
            }
            for (int j = 0; j < 10; j++) {
                int holy2 = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.CopperCoin, 0f, 0f, 100, default, 3f);
                Main.dust[holy2].noGravity = true;
                _ = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.CopperCoin, 0f, 0f, 100, default, 2f);
            }
        }
    }
}
