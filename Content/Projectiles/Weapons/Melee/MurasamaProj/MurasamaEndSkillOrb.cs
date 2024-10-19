using CalamityOverhaul.Content.Particles;
using InnoVault.PRT;
using System.Linq;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.MurasamaProj
{
    internal class MurasamaEndSkillOrb : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private Vector2 origVer;
        private bool set;
        private bool hasDamage;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.ignoreWater = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override bool? CanHitNPC(NPC target) {
            if (Projectile.ai[0] < Projectile.ai[1] || Projectile.ai[0] > Projectile.ai[1] + 30 || !hasDamage) {
                return false;
            }
            return base.CanHitNPC(target);
        }

        public override void AI() {
            hasDamage = EndSkillEffectStart.CanDealDamageToNPCs();
            if (!set && Projectile.ai[0] > Projectile.ai[1]) {
                origVer = Projectile.velocity;
                Projectile.velocity = Vector2.Zero;
                float maxNum = 233;
                for (int i = 0; i < maxNum; i++) {
                    Vector2 ver = origVer.UnitVector() * ((i / maxNum) * maxNum + 0.1f);
                    Color color = CWRUtils.MultiStepColorLerp(i / maxNum, Color.DarkRed, Color.IndianRed);
                    BasePRT spark2 = new PRT_Spark(Projectile.Center, ver, false, 119, 2.3f, color);
                    PRTLoader.AddParticle(spark2);
                }
                set = true;
            }
            Projectile.ai[0]++;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (CWRLoad.WormBodys.Contains(target.type)) {
                modifiers.FinalDamage *= 0.4f;
            }
            if (CWRLoad.targetNpcTypes7_1.Contains(target.type)) {
                modifiers.FinalDamage *= 0.5f;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , Projectile.Center - origVer.UnitVector() * 1200
                , Projectile.Center + origVer.UnitVector() * 2200, 38, ref point);
        }

        public override bool PreDraw(ref Color lightColor) {
            return false;
        }
    }
}
