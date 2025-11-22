using CalamityOverhaul.Content.RangedModify.Core;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class DarkechoGreatbowHeld : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "DarkechoGreatbow";
        public override void SetRangedProperty() {
            BowArrowDrawNum = 2;
            DrawArrowMode = -24;
            BowstringData.DeductRectangle = new Rectangle(2, 6, 2, 54);
        }
        public override void BowShoot() => OrigItemShoot();
    }
}
