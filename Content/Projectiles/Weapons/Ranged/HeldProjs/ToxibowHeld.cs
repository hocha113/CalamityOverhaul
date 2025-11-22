using CalamityOverhaul.Content.RangedModify.Core;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ToxibowHeld : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Toxibow";
        public override void SetRangedProperty() => BowstringData.DeductRectangle = new Rectangle(2, 12, 2, 30);
        public override void BowShoot() => OrigItemShoot();
    }
}
