using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class BoomstickHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.Boomstick].Value;
        public override int targetCayItem => ItemID.Boomstick;
        public override int targetCWRItem => ItemID.Boomstick;
        public override void SetRangedProperty() {
            FireTime = 21;
            kreloadMaxTime = 20;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = -6;
            HandIdleDistanceX = 17;
            HandIdleDistanceY = 4;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            Recoil = 2.0f;
            RangeOfStress = 8;
            ArmRotSengsBackNoFireOffset = 30;
            RepeatedCartridgeChange = true;
            Onehanded = true;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Shotgun;
            LoadingAA_Shotgun.loadShellSound = CWRSound.Gun_Shotgun_LoadShell with { Volume = 0.75f };
            LoadingAA_Shotgun.pump = CWRSound.Gun_Clipout with { Volume = 0.6f };
            if (!MagazineSystem) {
                FireTime += 30;
            }
        }

        public override void FiringShoot() {
            for (int i = 0; i < 6; i++) {
                int proj = Projectile.NewProjectile(Source2, ShootPos
                                    , ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.2f, 0.2f)) * Main.rand.NextFloat(0.7f, 1.4f)
                                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].scale += 0.1f;
                Main.projectile[proj].extraUpdates += 1;
                Main.projectile[proj].netUpdate = true;
            }
        }
    }
}
