using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Summon
{
    internal class BloodflareSouls : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj_Ranged + "BloodflareSoul";

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.timeLeft = 120;
            Projectile.alpha = 0;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            CWRUtils.ClockFrame(ref Projectile.frameCounter, 5, 3);
            Projectile.alpha += 15;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            if (Projectile.timeLeft < 60) {
                NPC target = Projectile.Center.FindClosestNPC(600);
                if (target != null) {
                    Projectile.ChasingBehavior2(target.Center, 1.01f, 0.1f);
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundEngine.PlaySound(in SoundID.NPCDeath39, Projectile.position);
            for (float i = 0; i < MathHelper.TwoPi; i += 0.05f) {
                Vector2 vr = i.ToRotationVector2() * Main.rand.Next(6, 7);
                Dust dust = Dust.NewDustDirect(Projectile.position, 32, 32
                , DustID.Blood, vr.X, vr.Y);
                dust.scale = Main.rand.NextFloat(1, 1.2f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D mainValue = CWRUtils.GetT2DValue(Texture);
            Main.EntitySpriteDraw(
                mainValue,
                Projectile.Center - Main.screenPosition,
                CWRUtils.GetRec(mainValue, Projectile.frameCounter, 4),
                Color.White * (Projectile.alpha / 255f),
                Projectile.rotation,
                CWRUtils.GetOrig(mainValue, 4),
                Projectile.scale,
                SpriteEffects.None,
                0
                );
            return false;
        }
    }
}
