﻿using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class AstralBlasterHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "AstralBlaster";
        public override int TargetID => ModContent.ItemType<AstralBlaster>();
        public override void SetRangedProperty() {
            KreloadMaxTime = 90;
            FireTime = 15;
            HandIdleDistanceY = 6;
            HandFireDistanceX = 20;
            HandFireDistanceY = -2;
            ShootPosNorlLengValue = -10;
            ShootPosToMouLengValue = 12;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            RepeatedCartridgeChange = true;
            GunPressure = 0.3f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 25;
            ForcedConversionTargetAmmoFunc = () => AmmoTypes == ProjectileID.Bullet;
            ToTargetAmmo = ModContent.ProjectileType<AstralRound>();
            SpwanGunDustData.splNum = 0.3f;
            SpwanGunDustData.dustID1 = DustID.RedStarfish;
            SpwanGunDustData.dustID2 = DustID.YellowStarDust;
        }
    }
}
