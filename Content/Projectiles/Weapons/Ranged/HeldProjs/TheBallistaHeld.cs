using CalamityOverhaul.Content.RangedModify.Core;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class TheBallistaHeld : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "TheBallista";
        public override void SetRangedProperty() => BowstringData.DeductRectangle = new Rectangle(8, 20, 2, 32);
        public override void BowShoot() => OrigItemShoot();
    }
}
