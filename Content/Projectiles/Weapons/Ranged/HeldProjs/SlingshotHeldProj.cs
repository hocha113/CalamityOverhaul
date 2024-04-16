using CalamityOverhaul.Common;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SlingshotHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "Slingshot";
        public override void SetRangedProperty() {
            base.SetRangedProperty();
        }

        public override void FiringShoot() {
            AmmoTypes = ModContent.ProjectileType<PebbleBall>();
            base.FiringShoot();
        }
    }
}
