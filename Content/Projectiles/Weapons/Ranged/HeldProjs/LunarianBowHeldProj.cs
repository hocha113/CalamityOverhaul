using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class LunarianBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "LunarianBow";
        public override int TargetID => ModContent.ItemType<LunarianBow>();
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
