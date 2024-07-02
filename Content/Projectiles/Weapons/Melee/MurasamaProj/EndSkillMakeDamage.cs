using System.Linq;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.MurasamaProj
{
    //增加这个弹幕的目的是为了以一种低性能的方式实现终结技的伤害
    internal class EndSkillMakeDamage : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public const int canDamageLengSQ = 9000000;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 6000;
            Projectile.tileCollide = Projectile.ignoreWater = false;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = 20;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 5;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (CWRLoad.targetNpcTypes7_1.Contains(target.type)) {
                modifiers.FinalDamage *= 0.1f;
                modifiers.SetMaxDamage(6000);
            }
            if (CWRLoad.WormBodys.Contains(target.type)) {
                modifiers.FinalDamage *= 0.5f;
            }
        }

        public override void OnKill(int timeLeft) => Projectile.Explode(3000, spanSound: false);
    }
}
