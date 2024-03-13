using CalamityOverhaul.Common;
using Terraria.ModLoader;
using Terraria;
using CalamityMod;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.DawnshatterAzureProj
{
    internal class MakeDamage : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public const int canDamageLengSQ = 9000000;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 6000;
            Projectile.tileCollide = Projectile.ignoreWater = false;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = 45;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 1;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            Projectile.Calamity().timesPierced = 0;
            modifiers.DefenseEffectiveness *= 0;
        }
    }
}
