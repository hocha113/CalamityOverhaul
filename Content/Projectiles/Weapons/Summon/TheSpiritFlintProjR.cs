using CalamityOverhaul.Common;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Summon
{
    internal class TheSpiritFlintProjR : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Summon + "TheSpiritFlintProj";
        Player Owner => Main.player[Projectile.owner];
        ref float Time => ref Projectile.ai[0];
        ref float State => ref Projectile.ai[1];
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.friendly = true;
            Projectile.timeLeft = 660;
        }

        public override bool? CanDamage() {
            return base.CanDamage();
        }

        public override void OnKill(int timeLeft) {
            base.OnKill(timeLeft);
        }

        public override void AI() {
            
        }
    }
}
