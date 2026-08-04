using CalamityOverhaul.Content.TimeFreezes;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans
{
    /// <summary>GlobalProjectile 时缓来源登记</summary>
    internal class SandevistanProjectile : GlobalProjectile
    {
        public override void OnSpawn(Projectile projectile, IEntitySource source) {
            SandevistanTimeSlow.ReconcileProjectile(projectile);
        }

        public override bool PreAI(Projectile projectile) {
            SandevistanTimeSlow.ReconcileProjectile(projectile);
            return true;
        }

        public override void PostAI(Projectile projectile) {
            SandevistanTimeSlow.ReconcileProjectile(projectile);
        }

        public override void OnKill(Projectile projectile, int timeLeft) {
            TimeFreezeSystem.ClearProjectileTimeScale<SandevistanTimeSlow>(
                projectile);
        }
    }
}
