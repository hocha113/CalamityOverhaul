using CalamityMod;
using CalamityMod.Items.Weapons.Magic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs
{
    internal class IonBlasterHeldProj : BaseMagicGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "IonBlaster";
        public override int TargetID => ModContent.ItemType<IonBlaster>();
        public override void SetMagicProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandIdleDistanceX = 15;
            HandIdleDistanceY = 3;
            HandFireDistanceX = 15;
            HandFireDistanceY = -5;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
        }

        public override void FiringShoot() {
            Projectile proj = Projectile.NewProjectileDirect(Source, ShootPos, ShootVelocity
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            float manaRatio = (float)Owner.statMana / Owner.statManaMax2;
            bool injectionNerf = Owner.Calamity().astralInjection;
            if (injectionNerf)
                manaRatio = MathHelper.Min(manaRatio, 0.65f);
            proj.scale = 0.75f + 0.75f * manaRatio;
            float manaRatio2 = (float)Owner.statMana / Owner.statManaMax2;
            bool injectionNerf2 = Owner.Calamity().astralInjection;
            if (injectionNerf2)
                manaRatio2 = MathHelper.Min(manaRatio2, 0.65f);
            float damageRatio = 0.2f + 1.4f * manaRatio2;
            proj.damage = (int)(proj.damage * damageRatio);
        }
    }
}
