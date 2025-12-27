using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System.Linq;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.MurasamaProj
{
    internal class MuraExecutionCut : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private Vector2 collidingStart;
        private Vector2 collidingEnd;
        private Vector2 origVer;
        private bool set;
        public override void SetStaticDefaults() => CWRLoad.ProjValue.ImmuneFrozen[Type] = true;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.ignoreWater = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 10;
        }

        public override bool? CanHitNPC(NPC target) {
            if (Projectile.ai[0] < Projectile.ai[1] || Projectile.ai[0] > Projectile.ai[1] + 30) {
                return false;
            }
            return base.CanHitNPC(target);
        }

        public override void AI() {
            if (!set && Projectile.ai[0] > Projectile.ai[1]) {
                origVer = Projectile.velocity;
                Projectile.velocity = Vector2.Zero;
                collidingStart = Projectile.Center - origVer.UnitVector() * 1200;
                collidingEnd = Projectile.Center + origVer.UnitVector() * 2200;
                if (!VaultUtils.isServer) {
                    float maxNum = 233;
                    for (int i = 0; i < maxNum; i++) {
                        Vector2 ver = origVer.UnitVector() * (i + 10.2f);
                        Color color = VaultUtils.MultiStepColorLerp(i / maxNum, Color.DarkRed, Color.IndianRed);
                        BasePRT spark = new PRT_Spark(Projectile.Center, ver, false, 119, 2.3f, color);
                        //不要在屏幕外面就消除了，否则玩家什么都看不到
                        spark.ShouldKillWhenOffScreen = false;
                        PRTLoader.AddParticle(spark);
                    }
                }
                set = true;
            }

            Projectile.ai[0]++;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (CWRLoad.WormBodys.Contains(target.type)) {
                modifiers.FinalDamage *= 0.2f;
            }
            if (CWRLoad.targetNpcTypes7_1.Contains(target.type)) {
                modifiers.FinalDamage *= 0.4f;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , collidingStart, collidingEnd, 38, ref point);
        }

        public override bool PreDraw(ref Color lightColor) {
            return false;
        }
    }
}
