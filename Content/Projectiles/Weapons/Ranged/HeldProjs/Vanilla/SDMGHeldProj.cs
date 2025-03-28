﻿using CalamityOverhaul.Content.RangedModify.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    /// <summary>
    /// 海豚机枪
    /// </summary>
    internal class SDMGHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.SDMG].Value;
        public override int TargetID => ItemID.SDMG;
        public override void SetRangedProperty() {
            FireTime = 4;
            ShootPosToMouLengValue = 12;
            ShootPosNorlLengValue = 10;
            HandIdleDistanceX = 15;
            HandIdleDistanceY = 0;
            GunPressure = 0.05f;
            ControlForce = 0.05f;
            Recoil = 0.8f;
            RangeOfStress = 48;
            RepeatedCartridgeChange = true;
            KreloadMaxTime = 45;
            CanCreateSpawnGunDust = false;
            LoadingAA_None.Roting = 30;
            LoadingAA_None.gunBodyX = 0;
            LoadingAA_None.gunBodyY = 13;
            if (!MagazineSystem) {
                FireTime += 1;
            }
        }
    }
}
