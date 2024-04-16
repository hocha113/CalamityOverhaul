using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SlingshotHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "Slingshot";
        public override int targetCayItem => ModContent.ItemType<Slingshot>();
        public override int targetCWRItem => ModContent.ItemType<Slingshot>();
        public override void SetRangedProperty() {
            HandFireDistance = HandDistance = 10;
            Recoil = 0;
        }

        public override void FiringShoot() {
            AmmoTypes = ModContent.ProjectileType<PebbleBall>();
            base.FiringShoot();
        }
    }
}
