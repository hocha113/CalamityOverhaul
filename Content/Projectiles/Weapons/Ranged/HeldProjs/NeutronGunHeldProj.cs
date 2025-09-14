﻿using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Neutrons;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.NeutronBowProjs;
using CalamityOverhaul.Content.RangedModify.Core;
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
        public override int TargetID => NeutronGun.ID;
        private float Charge {
            get => ((NeutronGun)Item.ModItem).Charge;
            set => ((NeutronGun)Item.ModItem).Charge = value;
        }
        private int uiframe;
        private bool canattce;
        public override void SetRangedProperty() {
            FireTime = 5;
            CanRightClick = true;
            ForcedConversionTargetAmmoFunc = () => true;
            ToTargetAmmo = ModContent.ProjectileType<NeutronBullet>();
            Recoil = 0.45f;
            HandIdleDistanceX = 35;
            HandIdleDistanceY = 3;
            HandFireDistanceX = 35;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
            LoadingAA_Handgun.gunBodyY = -16;
            ShootPosNorlLengValue = -2;
            ShootPosToMouLengValue = 10;
            GunPressure = 0.1f;
            ControlForce = 0.03f;
            CanCreateSpawnGunDust = false;
        }

        public override void PostInOwner() {
            VaultUtils.ClockFrame(ref Projectile.frame, 5, 6);
            VaultUtils.ClockFrame(ref uiframe, 5, 6);
            HandIdleDistanceX = onFireR ? (HandFireDistanceX = 65) : (HandFireDistanceX = 35);

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
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Projectile.NewProjectile(Source, ShootPos + VaultUtils.RandVr(130, 131), ShootVelocity
                , ModContent.ProjectileType<NeutronLaser>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            if (++fireIndex > 2) {
                FireTime = 12;
                fireIndex = 0;
            }
        }

        public override void FiringShootR() {
            int newdamage = (int)(WeaponDamage * (canattce ? 5.6f : 2.6f));
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity, AmmoTypes, newdamage, WeaponKnockback, Owner.whoAmI, 1);
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

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            if (Item != null && !Item.IsAir && Item.type == NeutronGun.ID) {
                NeutronGlaiveHeldAlt.DrawBar(Owner, Charge, uiframe);
            }
            Texture2D setValue = TextureValue;
            if (onFireR) {
                setValue = NeutronGun.ShootGun.Value;
            }
            Main.EntitySpriteDraw(setValue, drawPos
                , setValue.GetRectangle(Projectile.frame, 7), lightColor
                , Projectile.rotation, VaultUtils.GetOrig(setValue, 7), Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }
    }
}
