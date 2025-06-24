﻿using CalamityOverhaul.Content.Projectiles.Weapons.Magic.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RHeatRay : CWRItemOverride
    {
        public override int TargetID => ItemID.HeatRay;
        public override void SetDefaults(Item item) => item.SetHeldProj<HeatRayHeld>();
    }

    internal class HeatRayHeld : BaseMagicGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.HeatRay].Value;
        public override int TargetID => ItemID.HeatRay;
        public override void SetMagicProperty() {
            ShootPosToMouLengValue = -14;
            ShootPosNorlLengValue = -2;
            HandIdleDistanceX = 16;
            HandIdleDistanceY = 0;
            HandFireDistanceX = 16;
            HandFireDistanceY = -4;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            Onehanded = true;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 6;
            InOwner_HandState_AlwaysSetInFireRoding = true;
        }

        public override void SetShootAttribute() {
            Item.useTime = 6;
            if (++fireIndex > 6) {
                Item.useTime = 30;
                fireIndex = 0;
            }
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity.RotatedByRandom(0.1f)
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
