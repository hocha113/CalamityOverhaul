using CalamityMod.Projectiles.Turret;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class IceExplosionFriend : IceExplosion
    {
        public override string Texture => "CalamityMod/Projectiles/Summon/SmallAresArms/MinionPlasmaGas";
        public override void SetDefaults() {
            base.SetDefaults();
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 20;
        }
        //用击退值设定敌我的想法简直是天才
        public override bool PreAI() => true;
    }
}
