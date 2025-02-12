using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.MeleeModify.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class ForsakenSaberHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<ForsakenSaber>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "AegisBlade_Bar";
        public override void SetKnifeProperty() {
            canDrawSlashTrail = true;
            drawTrailHighlight = false;
            drawTrailCount = 4;
            SwingData.starArg = 54;
            SwingData.baseSwingSpeed = 5f;
            drawTrailBtommWidth = 20;
            distanceToOwner = 22;
            drawTrailTopWidth = 30;
            Length = 40;
        }

        public override void Shoot() {
            if (Main.rand.NextBool(3)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Teleporter);
            }
            Vector2 spreadVelocity = ShootVelocity.RotatedByRandom(MathHelper.ToRadians(15f)) * Main.rand.NextFloat(0.8f, 1.2f);
            Projectile.NewProjectile(Source, ShootSpanPos, spreadVelocity, ModContent.ProjectileType<SandBlade>(), Projectile.damage / 2, Projectile.knockBack * 0.5f, Owner.whoAmI);
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity, ModContent.ProjectileType<SandBlade>(), Projectile.damage / 2, Projectile.knockBack * 0.5f, Owner.whoAmI);
        }
    }
}
