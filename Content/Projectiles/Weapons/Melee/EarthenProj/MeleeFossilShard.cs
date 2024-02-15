using CalamityMod.Buffs.StatDebuffs;
using CalamityOverhaul.Common;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.EarthenProj
{
    internal class MeleeFossilShard : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj_Ranged + "FossilShard";
        public new string LocalizationCategory => "Projectiles.Ranged";
        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.aiStyle = ProjAIStyleID.Arrow;
            Projectile.timeLeft = 120;
            AIType = ProjectileID.WoodenArrowFriendly;
        }

        public override void AI() {
            if (Projectile.velocity.Y <= -3f) {
                Projectile.velocity.Y = -2.99f;
            }

            Projectile.rotation += Projectile.velocity.Y;
            Projectile.velocity.Y += 0.05f;
            Projectile.velocity.X *= 0.99f;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            CWRNpc.MultipleSegmentsLimitDamage(target, ref modifiers);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<ArmorCrunch>(), 60);
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i <= 2; i++) {
                _ = Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height, DustID.Sand, Projectile.oldVelocity.X * 0.5f, Projectile.oldVelocity.Y * 0.5f);
            }
        }
    }
}
