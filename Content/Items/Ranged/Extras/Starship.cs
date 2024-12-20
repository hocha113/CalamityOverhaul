using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.Extras
{
    /// <summary>
    /// 群星巨舰
    /// </summary>
    internal class Starship : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "Starship";
        public override void SetDefaults() {
            Item.SetItemCopySD<Infinity>();
            Item.SetCartridgeGun<StarshipHeld>(300);
        }
    }

    internal class StarshipHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "Starship";
        public override int targetCayItem => ModContent.ItemType<Starship>();
        public override int targetCWRItem => ModContent.ItemType<Starship>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 95;
            FireTime = 2;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 25;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = 0;
            ShootPosToMouLengValue = 14;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            SpwanGunDustMngsData.dustID1 = DustID.AncientLight;
            SpwanGunDustMngsData.dustID2 = DustID.AncientLight;
            SpwanGunDustMngsData.dustID3 = DustID.AncientLight;
            RepeatedCartridgeChange = true;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0.1f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 3;
        }

        public override void FiringShoot() {
            float sengs = MathF.Sin(Time * 0.1f) * 0.2f;
            int value = 1;
            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = ModContent.ProjectileType<ChargedBlast>();
            }
            else {
                Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                value = 2;
            }
            Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity.RotatedBy(sengs), AmmoTypes, WeaponDamage / value, WeaponKnockback, Owner.whoAmI, 0);
            Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity.RotatedBy(-sengs), AmmoTypes, WeaponDamage / value, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
