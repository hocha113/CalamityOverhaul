using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class SeashineSwordHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<SeashineSword>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "BrinyBaron_Bar";
        public override void SetKnifeProperty() {
            canDrawSlashTrail = true;
            SwingData.starArg = 54;
            SwingData.baseSwingSpeed = 4f;
            drawTrailBtommMode = 16;
            distanceToOwner = 10;
            trailTopWidth = 30;
            trailCount = 8;
            Length = 36;
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity * 2.5f
                , ModContent.ProjectileType<SeashineSwordProj>(), Projectile.damage / 2, Projectile.knockBack, Owner.whoAmI);
        }
    }
}
