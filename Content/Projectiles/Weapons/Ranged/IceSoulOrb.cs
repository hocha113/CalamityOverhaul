using InnoVault;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class IceSoulOrb : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "IceSoulOrb";
        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 24;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 10;
            Projectile.MaxUpdates = 2;
            Projectile.friendly = true;
        }

        public override void AI() {
            CWRUtils.ClockFrame(ref Projectile.frame, 5, 3);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (Projectile.ai[1] > 0) {
                NPC target = Projectile.Center.FindClosestNPC(600, false, true);
                if (target != null) {
                    float num = target.Center.Distance(Projectile.Center);
                    if (num > 120) {
                        Projectile.ChasingBehavior2(target.Center, 1, 0.22f);
                    }
                    else {
                        Projectile.ChasingBehavior(target.Center, Projectile.velocity.Length());
                    }
                }
            }
            Projectile.ai[2] = Projectile.ai[0] >= 135
                ? Utils.Remap(Projectile.ai[0], 225f, 270, 1.5f, 0f)
                : Utils.Remap(Projectile.ai[0], 135, 225f, 0f, 1.5f);

            Projectile.ai[0]++;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.ai[1] == 0) {
                if (Projectile.velocity.X != oldVelocity.X) {
                    Projectile.velocity.X = -oldVelocity.X * (Utils.Remap(Projectile.ai[0], 0f, 135f, 0.9f, 2f));
                }
                if (Projectile.velocity.Y != oldVelocity.Y) {
                    Projectile.velocity.Y = -oldVelocity.Y * (Utils.Remap(Projectile.ai[0], 0f, 135f, 0.9f, 2f));
                }
                for (int i = 0; i < 3; i++) {
                    Vector2 velocity = new Vector2(Main.rand.NextFloat(-3, 3), -3);
                    Projectile proj = Projectile.NewProjectileDirect(Main.player[Projectile.owner].GetShootState().Source
                    , Projectile.Bottom + new Vector2(Main.rand.Next(-16, 16), 0), velocity
                    , 961, 23, 0f, Main.myPlayer, 0f, Main.rand.NextFloat(0.8f, 1.1f));
                    proj.rotation = velocity.ToRotation();
                    proj.hostile = false;
                    proj.friendly = true;
                    proj.penetrate = -1;
                    proj.usesLocalNPCImmunity = true;
                    proj.localNPCHitCooldown = 20;
                    proj.light = 0.75f;
                }
            }
            Projectile.ai[1]++;
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(value, drawPosition, CWRUtils.GetRec(value, Projectile.frame, 4)
                , Color.White, Projectile.rotation, CWRUtils.GetOrig(value, 4), Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
