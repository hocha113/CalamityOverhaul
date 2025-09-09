using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class TelluricGlareHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "TelluricGlare";
        public override int TargetID => ModContent.ItemType<TelluricGlare>();
        public override void SetRangedProperty() {
            DrawArrowMode = -26;
            BowstringData.DeductRectangle = new Rectangle(8, 24, 2, 28);
        }
        public override void SetShootAttribute() {
            Item.useTime = 5;
            fireIndex++;
            if (fireIndex > 8) {
                Item.useTime = 30;
                fireIndex = 0;
            }
            AmmoTypes = ModContent.ProjectileType<TelluricGlareArrow>();
        }
        public override void BowShoot() {
            Vector2 norlShoot = ShootVelocity.UnitVector();
            FireOffsetPos = norlShoot * -53 +
                norlShoot.GetNormalVector() * Main.rand.Next(-16, 16);
            base.BowShoot();
        }
    }
}
