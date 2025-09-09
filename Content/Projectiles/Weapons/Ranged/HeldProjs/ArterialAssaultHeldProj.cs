using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ArterialAssaultHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ArterialAssault";
        public override int TargetID => ModContent.ItemType<ArterialAssault>();
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
