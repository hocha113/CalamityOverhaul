using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class LunarianBowHeld : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "LunarianBow";
        public override void SetRangedProperty() {
            BowArrowDrawNum = 2;
            fireIndex = 0;
            BowstringData.DeductRectangle = new Rectangle(6, 8, 2, 40);
        }
        public override void SetShootAttribute() {
            Item.useTime = 10;
            if (++fireIndex >= 5) {
                Item.useTime = 50;
                fireIndex = 0;
            }
        }
        public override void BowShoot() => OrigItemShoot();
    }
}
