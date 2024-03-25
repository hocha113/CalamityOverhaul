using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class P90HeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "P90";
        public override int targetCayItem => ModContent.ItemType<P90>();
        public override int targetCWRItem => ModContent.ItemType<P90EcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 2;
            HandDistanceY = 0;
            HandDistance = HandFireDistance = 12;
            HandFireDistanceY = -6;
            ShootPosNorlLengValue = -3;
            ShootPosToMouLengValue = -10;
            RepeatedCartridgeChange = true;
            MustConsumeAmmunition = false;
            Recoil = GunPressure = ControlForce = 0;
        }

        public override void KreloadSoundCaseEjection() {
            base.KreloadSoundCaseEjection();
        }

        public override void KreloadSoundloadTheRounds() {
            base.KreloadSoundloadTheRounds();
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(20, 3, 5);
        }

        public override Vector2 GetGunInFirePos() {
            return kreloadTimeValue == 0 ? base.GetGunInFirePos() : GetGunBodyPostion();//避免玩家试图在装弹时开火而引发动画冲突
        }

        public override float GetGunInFireRot() {
            return kreloadTimeValue == 0 ? base.GetGunInFireRot() : GetGunBodyRotation();//避免玩家试图在装弹时开火而引发动画冲突
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity, Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }

        public override void PostFiringShoot() {
            base.PostFiringShoot();
            EjectCasing();
        }
    }
}
