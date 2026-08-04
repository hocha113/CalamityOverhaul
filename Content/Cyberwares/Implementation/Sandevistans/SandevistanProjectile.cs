using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans
{
    /// <summary>GlobalProjectile 时缓来源登记</summary>
    internal class SandevistanProjectile : GlobalProjectile
    {
        public override void OnSpawn(Projectile projectile, IEntitySource source) {
            if (SandevistanTimeSlow.IsActive
                && SandevistanTimeSlow.ShouldAffectProjectile(projectile)) {
                SandevistanTimeSlow.EnsureProjectileSource(projectile);
            }
        }

        public override bool PreAI(Projectile projectile) {
            if (SandevistanTimeSlow.IsActive
                && SandevistanTimeSlow.ShouldAffectProjectile(projectile)) {
                SandevistanTimeSlow.EnsureProjectileSource(projectile);
            }
            return true;
        }
    }
}
