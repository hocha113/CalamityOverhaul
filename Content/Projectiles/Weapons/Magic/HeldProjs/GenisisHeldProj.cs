using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs
{
    internal class GenisisHeldProj : BaseMagicGun
    {
        public override bool IsLoadingEnabled(Mod mod) {
            return false;//TODO:已经废弃，等待重做或者移除
        }
        public override string Texture => CWRConstant.Cay_Wap_Magic + "Genisis";
        //public override int targetCayItem => ModContent.ItemType<Genisis>();
        //public override int targetCWRItem => ModContent.ItemType<GenisisEcType>();
        public override void SetMagicProperty() {
            ShootPosToMouLengValue = 60;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 2;
            HandFireDistance = 15;
            HandFireDistanceY = -5;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
        }

        public override void FiringShoot() {
            int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.25f) * Main.rand.NextFloat(0.9f, 1.3f)
                , ProjectileID.LaserMachinegunLaser, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].extraUpdates += 1;
        }
    }
}
