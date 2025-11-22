using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class TelluricGlareHeld : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "TelluricGlare";
        public override void SetRangedProperty() {
            EnableCowboySpin = true;
            DrawArrowMode = -26;
            BowstringData.DeductRectangle = new Rectangle(8, 24, 2, 28);
        }
        public override void SetShootAttribute() {
            Item.useTime = 5;
            fireIndex++;
            if (fireIndex > 8) {
                Item.useTime = 30;
                fireIndex = 0;
            }
            AmmoTypes = CWRID.Proj_TelluricGlareArrow;
        }
        public override void BowShoot() {
            Vector2 norlShoot = ShootVelocity.UnitVector();
            FireOffsetPos = norlShoot * -53 +
                norlShoot.GetNormalVector() * Main.rand.Next(-16, 16);
            base.BowShoot();
        }
    }
}
