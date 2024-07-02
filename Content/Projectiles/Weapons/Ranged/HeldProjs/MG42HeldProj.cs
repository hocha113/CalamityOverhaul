using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class MG42HeldProj : BaseFeederGun, ILoader
    {
        public override string Texture => CWRConstant.Item_Ranged + "MG42";
        static Asset<Texture2D> masking;
        static int MG42;
        float randomShootRotset;
        float shootValue;
        void ILoader.LoadAsset() => masking = CWRUtils.GetT2DAsset(Texture + "_Masking");
        void ILoader.SetupData() => MG42 = ModContent.ItemType<MG42>();
        void ILoader.UnLoadData() {
            masking = null;
            MG42 = ItemID.None;
        }

        public override int targetCayItem => MG42;
        public override int targetCWRItem => MG42;

        public override void SetRangedProperty() {
            FireTime = 4;
            HandDistance = 36;
            HandDistanceY = -4;
            HandFireDistance = 36;
            HandFireDistanceY = -10;
            ShootPosToMouLengValue = 46;
            Recoil = 0.5f;
            CanCreateSpawnGunDust = false;
            ForcedConversionTargetAmmoFunc = () => AmmoTypes == ProjectileID.Bullet;
            ToTargetAmmo = ModContent.ProjectileType<NitroShot>();
        }

        public override float GetGunInFireRot() {
            float rot = base.GetGunInFireRot();
            if (kreloadTimeValue == 0) {
                rot += randomShootRotset;
            }
            return rot;
        }

        public override void PostInOwnerUpdate() {
            if (shootValue > 0) {
                shootValue -= 0.02f;
            }
            if (shootValue > 16) {
                shootValue = 16;
            }
            if (shootValue > 10) {
                if (Main.rand.NextBool(6)) {
                    Vector2 spanPos = GunShootPos + ShootVelocityInProjRot.UnitVector() * Main.rand.NextFloat(-33f, 42f);
                    Dust.NewDust(spanPos, 3, 3, DustID.Smoke, 0, -3, 55, Scale: Main.rand.NextFloat(1, 3));
                }
            }
        }

        public override void SetShootAttribute() {
            randomShootRotset = Main.rand.NextFloat(-0.1f, 0.1f);
            shootValue += 0.4f;
        }

        public override void FiringShoot() {
            int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocityInProjRot
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].ArmorPenetration = 20;
            if (shootValue > 10) {
                Main.projectile[proj].scale *= 2;
            }
        }

        public override void GunDraw(ref Color lightColor) {
            base.GunDraw(ref lightColor);
            Color maskingColor = lightColor;
            if (shootValue > 0) {
                maskingColor = CWRUtils.MultiStepColorLerp(shootValue / 16f, lightColor, Color.Red);
            }
            Main.EntitySpriteDraw(masking.Value, Projectile.Center - Main.screenPosition, null, maskingColor
                , Projectile.rotation, masking.Size() / 2, Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }
    }
}
