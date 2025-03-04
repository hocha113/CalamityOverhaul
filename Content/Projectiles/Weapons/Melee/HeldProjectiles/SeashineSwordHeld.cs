using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.MeleeModify.Core;
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
            SwingData.baseSwingSpeed = 3.2f;
            drawTrailBtommWidth = 16;
            distanceToOwner = 10;
            drawTrailTopWidth = 30;
            drawTrailCount = 8;
            Length = 36;
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity * 2.5f
                , ModContent.ProjectileType<SeashineSwordProj>(), Projectile.damage, Projectile.knockBack, Owner.whoAmI);
        }
    }
}
