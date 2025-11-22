using CalamityOverhaul.Content.RangedModify.Core;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class GoobowHeld : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Goobow";
        public override void BowShoot() {
            base.BowShoot();
            AmmoTypes = CWRID.Proj_SlimeStream;
            FireOffsetPos = ShootVelocity.GetNormalVector() * -5;
            base.BowShoot();
            FireOffsetPos = ShootVelocity.GetNormalVector() * 5;
            base.BowShoot();
            FireOffsetPos = Vector2.Zero;
        }
    }
}
