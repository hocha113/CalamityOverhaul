using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class CoralCannonHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "CoralCannon";
        public override int targetCayItem => ModContent.ItemType<CoralCannon>();
        public override int targetCWRItem => ModContent.ItemType<CoralCannonEcType>();
        public override void SetRangedProperty() {
            HandIdleDistanceX = 20;
            kreloadMaxTime = 80;
            Recoil = 2.8f;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 12;
            RecoilOffsetRecoverValue = 0.9f;
            CanCreateCaseEjection = false;
            LoadingAmmoAnimation_AlwaysSetInFireRoding = true;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
            LoadingAA_Handgun.clipOut = CWRSound.CaseEjection2 with { Pitch = -0.2f };
            FireTime = MagazineSystem ? 10 : 90;
            ForcedConversionTargetAmmoFunc = () => true;
            ToTargetAmmo = ModContent.ProjectileType<SmallCoral>();
        }

        public override void PostGunDraw(Vector2 drawPos, ref Color lightColor) {
            if (IsKreload && MagazineSystem) {
                Texture2D value = CWRUtils.GetT2DValue(CWRConstant.Item_Ranged + "CoralCannon_PrimedForAction");
                Main.EntitySpriteDraw(value, drawPos, null, lightColor
                    , Projectile.rotation, value.Size() / 2, Projectile.scale
                    , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            }
        }
    }
}
