using Terraria;
using CalamityOverhaul.Content.TimeFreezes;
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

            SandevistanTimeSlow.EnsureProjectileSource(projectile);
            Vector2 slowVel = TimeFreezeSystem.GetEffectiveResumeVelocity(projectile);

            //回滚位移后按缩放速度推进
            projectile.position = projectile.oldPosition + slowVel;
            projectile.velocity = slowVel;
            projectile.timeLeft++;

            return false;
        }
    }
}
