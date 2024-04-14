using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class FlarewingBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "FlarewingBow";
        public override int targetCayItem => ModContent.ItemType<FlarewingBow>();
        public override int targetCWRItem => ModContent.ItemType<FlarewingBowEcType>();
        public override void SetRangedProperty() {
            BowArrowDrawNum = 3;
        }
        public override void BowShoot() {
            //如果这些开发者愿意遵守那该死的开发手册，就不会需要多写这么多该死特判代码
            if (AmmoTypes == ProjectileID.WoodenArrowFriendly) {
                AmmoTypes = ModContent.ProjectileType<FlareBat>();
            }
            base.BowShoot();
            FireOffsetPos = ShootVelocity.GetNormalVector() * -8;
            base.BowShoot();
            FireOffsetPos = ShootVelocity.GetNormalVector() * 8;
            base.BowShoot();
            FireOffsetPos = Microsoft.Xna.Framework.Vector2.Zero;
        }
    }
}
