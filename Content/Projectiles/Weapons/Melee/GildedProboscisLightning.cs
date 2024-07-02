using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class GildedProboscisLightning : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Default;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            if (++Projectile.ai[0] > 12) {
                NPC target = Projectile.Center.FindClosestNPC(1900);
                Vector2 vr = Main.player[Projectile.owner].Center.To(Projectile.Center).UnitVector() * 5;
                if (target != null) {
                    vr = Projectile.Center.To(target.Center).UnitVector() * 5;
                }
                Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), Projectile.Center, vr
                    , ModContent.ProjectileType<GildedProboscisLightningArc>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
                Projectile.ai[0] = 0;
            }
        }
    }
}
