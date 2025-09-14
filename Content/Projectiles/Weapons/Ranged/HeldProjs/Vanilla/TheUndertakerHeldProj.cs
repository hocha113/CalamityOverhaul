﻿using CalamityOverhaul.Content.RangedModify.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    /// <summary>
    /// 夺命枪
    /// </summary>
    internal class TheUndertakerHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.TheUndertaker].Value;
        public override int TargetID => ItemID.TheUndertaker;
        public override void SetRangedProperty() {
            KreloadMaxTime = 35;
            FireTime = 14;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = -3;
            HandIdleDistanceX = 15;
            HandIdleDistanceY = 0;
            HandFireDistanceY = -2;
            RepeatedCartridgeChange = true;
            CanCreateCaseEjection = false;
            GunPressure = 0.3f;
            ControlForce = 0.05f;
            Recoil = 0.6f;
            SpwanGunDustData.splNum = 0.4f;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Revolver;
            if (!MagazineSystem) {
                FireTime += 3;
            }
        }

        public override void KreloadSoundloadTheRounds() {
            base.KreloadSoundloadTheRounds();
            for (int i = 0; i < 6; i++) {
                CaseEjection();
            }
        }
    }
}
