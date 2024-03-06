using CalamityOverhaul.Common;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles
{
    internal class ShockwaveBoom : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public override void SetDefaults() {
            Projectile.width = 4;
            Projectile.height = 4;
            Projectile.friendly = true;
            Projectile.alpha = 0;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 200;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            float num = (180f - Projectile.timeLeft) / 60f;
            float num2 = 1f;
            float num3 = 1f;
            float num4 = 20f;
            if (Projectile.ai[0] > 0f) {
                num3 = Projectile.ai[0];
            }

            if (Projectile.ai[1] > 0f) {
                num4 = Projectile.ai[1];
            }

            if (!Main.dedServ) {
                Filter Shockwaves = Filters.Scene["Shockwave"];
                _ = Shockwaves.GetShader().UseProgress(num).UseOpacity(100f * (1f - (num / 3f)));

                Projectile.localAI[1] += 1f;
                if (Projectile.localAI[1] >= 0f && Projectile.localAI[1] <= 60f && !Shockwaves.IsActive()) {
                    _ = Filters.Scene.Activate("Shockwave", Projectile.Center, new object[0]).GetShader().UseColor(num2, num3, num4).UseTargetPosition(Projectile.Center);
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (!Main.dedServ) {
                Filters.Scene.Activate("Shockwave").Deactivate(new object[0]);
            }
        }
    }
}
