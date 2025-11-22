using CalamityOverhaul.Content.RangedModify.Core;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ContinentalGreatbowHeld : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ContinentalGreatbow";
        public override void SetRangedProperty() {
            BowArrowDrawNum = 3;
            BowstringData.DeductRectangle = new Rectangle(8, 16, 4, 26);
        }
        public override void BowShoot() {
            OrigItemShoot();
        }
    }
}
