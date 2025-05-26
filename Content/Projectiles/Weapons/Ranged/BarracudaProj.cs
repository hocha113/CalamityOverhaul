using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class BarracudaProj : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj_Ranged + "MechanicalBarracuda";

        private float offsetRot;
        private bool damageZengs;
        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
            Projectile.alpha = 255;
            Projectile.DamageType = DamageClass.Ranged;
        }

        public override void AI() {
            VaultUtils.ClockFrame(ref Projectile.frame, 6, 3);
            Projectile.rotation = Projectile.velocity.ToRotation();
            NPC target = Projectile.Center.FindClosestNPC(300);
            bool mousL = Main.player[Projectile.owner].PressKey();
            float leng = Projectile.Center.Distance(Main.player[Projectile.owner].Center);
            if (Projectile.ai[0] == 0) {
                if (target != null) {
                    Projectile.ChasingBehavior(target.Center, Projectile.velocity.Length());
                    if (Projectile.Center.Distance(target.Center) < 32) {
                        Projectile.ai[0] = 2;
                        if (!damageZengs) {
                            offsetRot = Main.rand.NextFloat(MathHelper.TwoPi) + Main.player[Projectile.owner].Center.To(Projectile.Center).ToRotation();
                            damageZengs = true;
                        }
                    }
                }
                if (!mousL) {
                    Projectile.ai[0] = 1;
                }
            }
            if (Projectile.ai[0] == 1) {
                Projectile.ChasingBehavior(Main.player[Projectile.owner].Center, 25);
                if (leng < 32) {
                    Projectile.Kill();
                }
            }
            if (Projectile.ai[0] == 2) {
                Projectile.rotation = offsetRot;
                if (target.Alives()) {
                    Projectile.Center = target.Center;
                    Projectile.velocity = Vector2.Zero;
                }
                else {
                    Projectile.ai[0] = 1;
                }
                if (!mousL) {
                    Projectile.ai[0] = 1;
                }
            }
            if (mousL) {
                Projectile.timeLeft = 300;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Main.spriteBatch.Draw(texture, Projectile.Center - Main.screenPosition, texture.GetRectangle(Projectile.frame, 4)
                , lightColor, Projectile.rotation, VaultUtils.GetOrig(texture, 4), Projectile.scale, Projectile.rotation.ToRotationVector2().X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically, 0f);
            return false;
        }
    }
}
