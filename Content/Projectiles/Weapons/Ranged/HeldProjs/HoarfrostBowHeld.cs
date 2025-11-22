using CalamityOverhaul.Content.RangedModify.Core;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class HoarfrostBowHeld : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "HoarfrostBow";
        public override void SetRangedProperty() {
            BowArrowDrawNum = 2;
            BowstringData.DeductRectangle = new Rectangle(4, 14, 2, 34);
        }
        public override void BowShoot() => OrigItemShoot();
    }
}
