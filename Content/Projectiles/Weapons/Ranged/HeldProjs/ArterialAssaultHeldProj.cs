using CalamityMod;
using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ArterialAssaultHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ArterialAssault";
        public override int targetCayItem => ModContent.ItemType<ArterialAssault>();
        public override int targetCWRItem => ModContent.ItemType<ArterialAssaultEcType>();
        public override void SetRangedProperty() {
            HandDistance = 20;
            HandFireDistance = 22;
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
