using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class AntiMaterielRifleHeldProj : TyrannysEndHeldProj
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "AntiMaterielRifle";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.AntiMaterielRifle>();
        public override int targetCWRItem => ModContent.ItemType<AntiMaterielRifle>();
        public override void SetRangedProperty() {
            kreloadMaxTime = 120;
            fireTime = 40;
            ControlForce = 0.04f;
            GunPressure = 0.25f;
            Recoil = 4.5f;
            HandDistance = 35;
            RangeOfStress = 25;
            RepeatedCartridgeChange = true;
        }
        public override void OnSpanProjFunc() {
            SoundEngine.PlaySound(heldItem.UseSound.Value with { Pitch = 0.3f }, Projectile.Center);
            DragonsBreathRifleHeldProj.SpawnGunDust(Projectile, Projectile.Center, ShootVelocity);
            Projectile.NewProjectile(Owner.parent(), Projectile.Center, ShootVelocity
                    , ModContent.ProjectileType<BMGBullet>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, 1);
        }
    }
}
