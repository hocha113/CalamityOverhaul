using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ShellshooterHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Shellshooter";
        public override int targetCayItem => ModContent.ItemType<Shellshooter>();
        public override int targetCWRItem => ModContent.ItemType<ShellshooterEcType>();
        public override void SetRangedProperty() {
            base.SetRangedProperty();
        }
        public override void BowShoot() {
            //如果这些开发者愿意遵守那该死的开发手册，就不会需要多写这么多该死特判代码
            if (AmmoTypes == ProjectileID.WoodenArrowFriendly) {
                AmmoTypes = ModContent.ProjectileType<Shell>();
            }
            base.BowShoot();
        }
    }
}
