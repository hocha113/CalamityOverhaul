using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ArterialAssaultHeld : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ArterialAssault";
        public override void SetRangedProperty() {
            DrawArrowMode = -24;
            BowstringData.DeductRectangle = new Rectangle(6, 22, 2, 60);
        }
        public override void SetShootAttribute() {
            Item.useTime = 6;
            if (++fireIndex > 8) {
                Item.useTime = 12;
                fireIndex = 0;
            }
        }
        public override void BowShoot() {
            OrigItemShoot();
        }
    }
}
