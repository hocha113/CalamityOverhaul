using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Neutrons;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.NeutronBowProjs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class NeutronGunHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "NeutronGun";
        public override int targetCayItem => NeutronGun.PType;
        public override int targetCWRItem => NeutronGun.PType;

        private float Charge {
            get => ((NeutronGun)Item.ModItem).Charge;
            set => ((NeutronGun)Item.ModItem).Charge = value;
        }

        private int uiframe;
        private int fireIndex;
        private bool canattce;
        public override bool IsLoadingEnabled(Mod mod) {
            return true;//暂时不要在这个版本中出现
        }
        public override void SetRangedProperty() {
            FireTime = 5;
            CanRightClick = true;
            ForcedConversionTargetAmmoFunc = () => true;
            ToTargetAmmo = ModContent.ProjectileType<NeutronBullet>();
            Recoil = 0.45f;
            HandDistance = 35;
            HandDistanceY = 3;
            HandFireDistance = 35;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
            LoadingAA_Handgun.loadingAmmoStarg_y = -16;
            ShootPosNorlLengValue = -2;
            ShootPosToMouLengValue = 10;
            GunPressure = 0.1f;
            ControlForce = 0.03f;
        }

        public override void PostInOwnerUpdate() {
            CWRUtils.ClockFrame(ref Projectile.frame, 5, 5);
            CWRUtils.ClockFrame(ref uiframe, 5, 6);
            if (onFireR) {
                HandDistance = HandFireDistance = 65;
            }
            else {
                HandDistance = HandFireDistance = 35;
            }

            if (canattce && Charge > 0) {
                Charge--;
                if (Charge <= 0) {
                    SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.6f }, Projectile.Center);
                    canattce = false;
                }
            }
        }

        public override void SetShootAttribute() {
            if (onFire) {
                FireTime = 5;
                Recoil = 0.45f;
                GunPressure = 0.1f;
                ControlForce = 0.03f;
                ShootPosToMouLengValue = 10;
                Charge = 0;
                canattce = false;
                Item.UseSound = CWRSound.Gun_AWP_Shoot with { Pitch = -0.1f, Volume = 0.25f };
            }
            else if (onFireR) {
                FireTime = 45;
                Recoil = 1.45f;
                GunPressure = 0.16f;
                ControlForce = 0.01f;
                ShootPosToMouLengValue = -10;
                Item.UseSound = CWRSound.Gun_AWP_Shoot with { Pitch = -0.2f, Volume = 0.3f };
            }
        }

        public override void FiringShoot() {
            //CaseEjection();
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Projectile.NewProjectile(Source, GunShootPos + CWRUtils.randVr(130, 131), ShootVelocity
                , ModContent.ProjectileType<NeutronLaser>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            if (++fireIndex > 2) {
                FireTime = 12;
                fireIndex = 0;
            }
        }

        public override void FiringShootR() {
            int newdamage = (int)(WeaponDamage * (canattce ? 5.6f : 2.6f));
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, newdamage, WeaponKnockback, Owner.whoAmI, 1);
            if (!canattce) {
                Charge += 10;
            }
            if (Charge >= 80) {
                if (!canattce) {
                    SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.2f }, Projectile.Center);
                    SoundEngine.PlaySound(CWRSound.Pecharge with { Pitch = -0.2f, Volume = 0.8f }, Projectile.Center);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Main.MouseWorld
                    , Vector2.Zero, ModContent.ProjectileType<EXNeutronExplosionRanged>(), Projectile.damage, 0);
                }
                canattce = true;
                Charge = 80;
            }
        }

        public override void GunDraw(ref Color lightColor) {
            if (Item != null && !Item.IsAir && Item.type == NeutronGun.PType) {
                NeutronGlaiveHeld.DrawBar(Owner, Charge, uiframe);
            }
            Texture2D setValue = TextureValue;
            if (onFireR) {
                setValue = NeutronGun.ShootGun.Value;
            }
            Main.EntitySpriteDraw(setValue, Projectile.Center - Main.screenPosition
                , CWRUtils.GetRec(setValue, Projectile.frame, 6), lightColor
                , Projectile.rotation, CWRUtils.GetOrig(setValue, 6), Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }
    }
}
