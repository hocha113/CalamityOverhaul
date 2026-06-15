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

            //缓速期间首次出现或槽位被复用（type/owner/identity 错配）时按需重新抓取原速度
            SandevistanTimeSlow.EnsureProjectileSnapshot(projectile);

            int idx = projectile.whoAmI;
            Vector2 slowVel = SandevistanTimeSlow.ProjCachedVelocities[idx] * SandevistanTimeSlow.SlowFactor;

            //回滚位移后按缩放速度推进
            projectile.position = projectile.oldPosition + slowVel;
            projectile.velocity = slowVel;
            projectile.timeLeft++;

            return false;
        }
    }
}
