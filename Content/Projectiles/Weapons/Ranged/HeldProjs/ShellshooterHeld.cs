using CalamityOverhaul.Content.RangedModify.Core;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ShellshooterHeld : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Shellshooter";
        public override void SetRangedProperty() => BowstringData.DeductRectangle = new Rectangle(4, 14, 2, 18);
        public override void BowShoot() => OrigItemShoot();
    }
}
