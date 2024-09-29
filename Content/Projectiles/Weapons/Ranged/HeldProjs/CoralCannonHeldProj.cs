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
            CanCreateCaseEjection = false;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
            LoadingAA_Handgun.clipOut = CWRSound.CaseEjection2 with { Pitch = -0.2f };
        }
        public override void FiringShoot() {
            Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity
                    , ModContent.ProjectileType<SmallCoral>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, 1);
        }
        public override void PostGunDraw(Vector2 drawPos, ref Color lightColor) {
            if (IsKreload) {
                Texture2D value = CWRUtils.GetT2DValue(CWRConstant.Item_Ranged + "CoralCannon_PrimedForAction");
                Main.EntitySpriteDraw(value, drawPos, null, lightColor
                    , Projectile.rotation, value.Size() / 2, Projectile.scale
                    , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            }
        }
    }
}
