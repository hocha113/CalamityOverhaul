using CalamityOverhaul.Content.MeleeModify.Core;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjs
{
    internal class SeashineSwordHeld : BaseKnife
    {
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
                , CWRID.Proj_SeashineSwordProj, Projectile.damage, Projectile.knockBack, Owner.whoAmI);
        }
    }
}
