using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using System.Linq;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class BMGFIRE : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.MaxUpdates = 3;
            Projectile.timeLeft = 16 * Projectile.MaxUpdates;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10 * Projectile.MaxUpdates;
        }

        public override bool? CanDamage() {
            return Projectile.ai[0] > 0 ? false : base.CanDamage();
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (CWRLoad.targetNpcTypes7.Contains(target.type) || CWRLoad.targetNpcTypes7_1.Contains(target.type)) {
                modifiers.FinalDamage *= 0.6f;
                modifiers.SetMaxDamage(1500 + Main.rand.Next(-30, 30));
            }
            if (CWRLoad.WormBodys.Contains(target.target)) {
                modifiers.FinalDamage *= 0.5f;
            }
            if (target.type == CWRLoad.Yharon) {
                modifiers.FinalDamage *= 0.35f;
            }
        }

        public override void AI() {
            BaseParticle particle = new DRK_Light(Projectile.Center, Projectile.velocity, Main.rand.NextFloat(0.3f, 0.7f), Color.Red, 22, 0.2f);
            DRKLoader.AddParticle(particle);
            BaseParticle particle2 = new DRK_Smoke(Projectile.Center + Projectile.velocity * Main.rand.NextFloat(0.3f, 1.7f), Projectile.velocity
                , CWRUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.Gold)
                , 32, Main.rand.NextFloat(0.2f, 1.1f), 0.5f, 0.1f);
            DRKLoader.AddParticle(particle2);
            if (Main.rand.NextBool(Projectile.timeLeft / 3 + 1)) {
                float slp = (100 - Projectile.timeLeft) * 0.1f;
                if (slp > 5) {
                    slp = 5;
                }
                BaseParticle particle3 = new DRK_Light(Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(5.3f, 25.7f) * slp
                    , Projectile.velocity, Main.rand.NextFloat(0.3f, 0.7f), Color.Red, 22, 0.2f);
                DRKLoader.AddParticle(particle3);
                BaseParticle particle4 = new DRK_Smoke(Projectile.Center + Projectile.velocity * Main.rand.NextFloat(0.3f, 1.7f) + Main.rand.NextVector2Unit() * Main.rand.NextFloat(3.3f, 15.7f)
                    , Projectile.velocity, CWRUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.Gold)
                    , 32, Main.rand.NextFloat(0.2f, 1.1f) * slp, 0.5f, 0.1f);
                DRKLoader.AddParticle(particle4);
            }

            Projectile.ai[0]--;
        }
    }
}
