﻿using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using static Humanizer.In;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class FetidEmesisHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "FetidEmesis";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.FetidEmesis>();
        public override int targetCWRItem => ModContent.ItemType<FetidEmesisEcType>();
        public override void SetRangedProperty() {
            FireTime = 7;
            ControlForce = 0.06f;
            GunPressure = 0.12f;
            Recoil = 0.75f;
            HandDistance = 20;
            HandFireDistance = 20;
            HandFireDistanceY = -3;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 5;
            CanRightClick = true;
        }

        public override void FiringShoot() {
            FireTime = 7;
            GunPressure = 0.12f;
            Recoil = 0.75f;
            int proj = Projectile.NewProjectile(Owner.parent(), GunShootPos
                , ShootVelocityInProjRot.RotatedBy(Main.rand.NextFloat(-0.02f, 0.02f))
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].CWR().SpanTypes = (byte)SpanTypesEnum.FetidEmesis;

            if (++Projectile.ai[2] > 8) {
                Projectile.NewProjectile(Owner.parent(), GunShootPos, Vector2.Zero
                , ModContent.ProjectileType<FetidEmesisOnSpan>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
                Projectile.ai[2] = 0;
                _ = CreateRecoil();//执行两次，它会造成两倍的后坐力效果
            }
        }

        public override void FiringShootR() {
            FireTime = 22;
            GunPressure = 0.25f;
            Recoil = 1.75f;
            int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                        , ModContent.ProjectileType<EmesisGore>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].penetrate = 8;
            Main.projectile[proj].extraUpdates += 1;
            for (int i = 0; i < 55; i++) {
                Dust dust = Dust.NewDustDirect(GunShootPos, 10, 10, DustID.Shadowflame);
                dust.velocity = ShootVelocity.RotatedByRandom(MathHelper.ToRadians(15f)) * Main.rand.NextFloat(0.6f, 1);
                dust.noGravity = true;
            }
        }
    }
}
