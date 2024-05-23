using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic
{
    internal class MarshmallowProj : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Magic + "MarshmallowProj";

        private NPC OnHItTarget;
        private Vector2 OffsetPos;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 260;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.penetrate = -1;
        }

        public override bool PreAI() {
            CWRUtils.ClockFrame(ref Projectile.frame, 5, 4);
            return true;
        }

        public override void AI() {
            if (Projectile.ai[0] == 0) {
                Projectile.velocity *= 0.99f;
                if (Projectile.timeLeft < 200) {
                    Projectile.velocity = Projectile.velocity.UnitVector() * 13;
                    Projectile.ai[0] = 1;
                }
            }
            else if (Projectile.ai[0] == 1) {
                NPC target = Projectile.Center.FindClosestNPC(600, false, true);
                if (target != null) {
                    Projectile.ChasingBehavior2(target.Center, 1, 0.35f);
                }
            }
            else if (Projectile.ai[0] == 2) {
                if (!OnHItTarget.Alives()) {
                    Projectile.Kill();
                    return;
                }
                Projectile.Center = OnHItTarget.Center + OffsetPos;
                Projectile.scale += 0.01f;
                CWRParticle particle2 = new SmokeParticle(Projectile.Center + CWRUtils.randVr(Main.rand.NextFloat(0, 11.7f) * Projectile.scale), Vector2.Zero
               , CWRUtils.MultiStepColorLerp(Main.rand.NextFloat(), Main.DiscoColor, new Color(Main.DiscoG, 0, 255))
               , 32, Main.rand.NextFloat(0.2f, 1.1f), 0.5f, 0.1f);
                CWRParticleHandler.AddParticle(particle2);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (OnHItTarget == null) {
                OnHItTarget = target;
                OffsetPos = Projectile.Center.To(target.Center) * Main.rand.NextFloat(0.7f, 1);
                Projectile.ai[0] = 2;
            }
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 55; i++) {
                CWRParticle particle2 = new SmokeParticle(Projectile.Center + CWRUtils.randVr(Main.rand.NextFloat(0, 11.7f) * Projectile.scale), Vector2.Zero
              , CWRUtils.MultiStepColorLerp(Main.rand.NextFloat(), Main.DiscoColor, new Color(Main.DiscoG, 0, 255))
              , 32, Main.rand.NextFloat(0.2f, 1.1f), 0.5f, 0.1f);
                CWRParticleHandler.AddParticle(particle2);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = CWRUtils.GetT2DValue(Texture);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, CWRUtils.GetRec(value, Projectile.frame, 5)
                , Color.White, Projectile.rotation, CWRUtils.GetOrig(value, 5), Projectile.scale, Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);
            return false;
        }
    }
}
