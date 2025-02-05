using Terraria.Audio;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.Core
{
    internal abstract class BaseMagicAction : BaseMagicGun
    {
        protected int useAnimation;
        public sealed override void SetMagicProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandFireDistanceX = 0;
            HandFireDistanceY = 0;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            Onehanded = true;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
        }

        public override void Initialize() => useAnimation = Item.useAnimation;

        public override bool CanSpanProj() {
            bool reset = base.CanSpanProj();
            if (Item.useLimitPerAnimation.HasValue) {
                if (fireIndex > Item.useLimitPerAnimation) {
                    if (--useAnimation <= 0) {
                        fireIndex = 0;
                        return reset;
                    }
                    return false;
                }
            }
            else {
                if (--useAnimation <= 0) {
                    fireIndex = 0;
                    return reset;
                }
                return false;
            }
            return reset;
        }

        public override void HanderPlaySound() {
            if (Item.ModItem != null && !Item.ModItem.CanUseItem(Owner)) {
                return;
            }
            useAnimation -= Item.useTime;
            if (useAnimation <= 0) {
                SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
                useAnimation = Item.useAnimation;
            }
        }

        public override void FiringShoot() {
            if (Item.ModItem != null && !Item.ModItem.CanUseItem(Owner)) {
                return;
            }
            fireIndex++;
            OrigItemShoot();
        }

        public override void FiringShootR() {
            if (Item.ModItem != null && !Item.ModItem.CanUseItem(Owner)) {
                return;
            }
            fireIndex++;
            Owner.altFunctionUse = 2;
            OrigItemShoot();
        }
    }
}
