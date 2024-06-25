using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class HoarfrostBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "HoarfrostBow";
        public override int targetCayItem => ModContent.ItemType<HoarfrostBow>();
        public override int targetCWRItem => ModContent.ItemType<HoarfrostBowEcType>();
        public override void SetRangedProperty() {
            BowArrowDrawNum = 2;
        }
        public override void BowShoot() {
            //如果这些开发者愿意遵守那该死的开发手册，就不会需要多写这么多该死特判代码
            if (AmmoTypes == ProjectileID.WoodenArrowFriendly) {
                AmmoTypes = ModContent.ProjectileType<MistArrow>();
            }
            base.BowShoot();
            FireOffsetPos = ShootVelocity.GetNormalVector() * Main.rand.NextFloat(-6, 6);
            FireOffsetVector = ShootVelocity.UnitVector().RotatedByRandom(0.22f) * Main.rand.NextFloat(0.8f, 1.3f);
            base.BowShoot();
        }
    }
}
