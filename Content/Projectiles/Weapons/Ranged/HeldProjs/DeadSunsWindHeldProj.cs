using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Terraria.Audio;
using Terraria.ModLoader;
using Terraria;
using CalamityOverhaul.Content.Items.Ranged;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class DeadSunsWindHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "DeadSunsWind";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.DeadSunsWind>();
        public override int targetCWRItem => ModContent.ItemType<DeadSunsWind>();

        public override void SetRangedProperty() {
            HandDistance = 30;
            HandFireDistance = 30;
            HandFireDistanceY = -10;
        }

        public override void FiringShoot() {
            SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
            Projectile.NewProjectile(Owner.parent(), Projectile.Center, ShootVelocity
                    , Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            _ = UpdateConsumeAmmo();
        }
    }
}
