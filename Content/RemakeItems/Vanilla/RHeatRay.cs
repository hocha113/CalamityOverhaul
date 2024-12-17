using CalamityOverhaul.Content.Projectiles.Weapons.Magic;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RHeatRay : BaseRItem
    {
        public override int TargetID => ItemID.HeatRay;
        public override void SetDefaults(Item item) => item.SetHeldProj<HeatRayHeld>();
    }

    internal class HeatRayHeld : BaseMagicGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.HeatRay].Value;
        public override int targetCayItem => ItemID.HeatRay;
        public override int targetCWRItem => ItemID.HeatRay;
        private int fireIndex;
        public override void SetMagicProperty() {
            ShootPosToMouLengValue = -14;
            ShootPosNorlLengValue = -2;
            HandIdleDistanceX = 15;
            HandIdleDistanceY = 0;
            HandFireDistanceX = 15;
            HandFireDistanceY = -5;
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
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.1f)
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
