using CalamityOverhaul.Content.RangedModify.Core;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class FlarewingBowHeld : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "FlarewingBow";
        public override void SetRangedProperty() {
            BowArrowDrawNum = 3;
            DrawArrowMode = -24;
            BowstringData.DeductRectangle = new Rectangle(2, 14, 2, 44);
        }
        public override void BowShoot() => OrigItemShoot();
    }
}
