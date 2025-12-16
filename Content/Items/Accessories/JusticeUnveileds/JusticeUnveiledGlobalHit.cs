using InnoVault.GameContent.BaseEntity;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.JusticeUnveileds
{
    internal class JusticeUnveiledGlobalHit : GlobalProjectile
    {
        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone) {
            JusticeUnveiled.OnHitNPCSpwanProj(Main.player[projectile.owner], projectile, target, hit);
        }
    }

    internal class JUZenithWorldTime : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = false;
        }
        public override void AI() => Projectile.Center = Owner.GetPlayerStabilityCenter();
        public override bool PreDraw(ref Color lightColor) {
            CWRUtils.DrawRageEnergyChargeBar(Owner, 255, Projectile.timeLeft / 300f);
            return false;
        }
    }
}
