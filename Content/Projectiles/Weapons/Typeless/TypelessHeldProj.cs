using CalamityMod.Items.Weapons.Typeless;
using CalamityMod.Projectiles.Typeless;
using CalamityOverhaul.Content.Items.Typeless;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Typeless
{
    /// <summary>
    /// 星光之眼
    /// </summary>
    internal class LunicEyeHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Typeless + "LunicEye";
        public override int targetCayItem => ModContent.ItemType<LunicEye>();
        public override int targetCWRItem => ModContent.ItemType<LunicEyeEcType>();
        public override void SetRangedProperty() {
            HandFireDistanceX = 30;
            HandFireDistanceY = 0;
            ShootPosNorlLengValue = -2;
            ShootPosToMouLengValue = 24;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            RangeOfStress = 25;
            Onehanded = true;
            CanCreateSpawnGunDust = false;
            CanCreateCaseEjection = false;
            ForcedConversionTargetAmmoFunc = () => true;
            ToTargetAmmo = ModContent.ProjectileType<LunicBeam>();
        }
    }

    /// <summary>
    /// 马格努斯之眼
    /// </summary>
    internal class EyeofMagnusHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Typeless + "EyeofMagnus";
        public override int targetCayItem => ModContent.ItemType<EyeofMagnus>();
        public override int targetCWRItem => ModContent.ItemType<EyeofMagnusEcType>();
        public override void SetRangedProperty() {
            HandFireDistanceX = 30;
            HandFireDistanceY = 0;
            ShootPosNorlLengValue = -2;
            ShootPosToMouLengValue = 24;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            RangeOfStress = 25;
            Onehanded = true;
            CanCreateSpawnGunDust = false;
            CanCreateCaseEjection = false;
            ForcedConversionTargetAmmoFunc = () => true;
            ToTargetAmmo = ModContent.ProjectileType<MagnusBeam>();
        }
    }

    /// <summary>
    /// 美学魔杖
    /// </summary>
    internal class AestheticusHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Typeless + "Aestheticus";
        public override int targetCayItem => ModContent.ItemType<Aestheticus>();
        public override int targetCWRItem => ModContent.ItemType<AestheticusEcType>();

        public override void SetRangedProperty() {
            HandFireDistanceX = 40;
            HandFireDistanceY = 0;
            ShootPosNorlLengValue = 6;
            ShootPosToMouLengValue = 24;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            RangeOfStress = 25;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            Onehanded = true;
            CanCreateSpawnGunDust = false;
            CanCreateCaseEjection = false;
            ForcedConversionTargetAmmoFunc = () => true;
            ToTargetAmmo = ModContent.ProjectileType<CursorProj>();
        }

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            float rot = DirSign > 0 ? MathHelper.PiOver4 : MathHelper.PiOver4 * 3;
            Main.EntitySpriteDraw(TextureValue, drawPos, null, lightColor
                , Projectile.rotation + rot, TextureValue.Size() / 2, Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally);
        }
    }
}
