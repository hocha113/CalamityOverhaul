using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans
{
    /// <summary>GlobalProjectile 时缓，不侵入 CWRProjectile</summary>
    internal class SandevistanProjectile : GlobalProjectile
    {
        public override bool PreAI(Projectile projectile) {
            if (!SandevistanTimeSlow.IsActive) {
                return true;
            }
            if (!SandevistanTimeSlow.ShouldAffectProjectile(projectile)) {
                return true;
            }

            int idx = projectile.whoAmI;
            //新弹幕首次记入速度
            if (!SandevistanTimeSlow.ProjHasCache[idx]) {
                SandevistanTimeSlow.ProjCachedVelocities[idx] = projectile.velocity;
                SandevistanTimeSlow.ProjHasCache[idx] = true;
            }

            Vector2 slowVel = SandevistanTimeSlow.ProjCachedVelocities[idx] * SandevistanTimeSlow.SlowFactor;

            //回滚位移后按缩放速度推进
            projectile.position = projectile.oldPosition + slowVel;
            projectile.velocity = slowVel;
            projectile.timeLeft++;

            return false;
        }
    }
}
