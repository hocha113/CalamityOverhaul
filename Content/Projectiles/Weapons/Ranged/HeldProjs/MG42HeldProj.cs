using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class MG42HeldProj : BaseFeederGun, ICWRLoader
    {
        public override string Texture => CWRConstant.Item_Ranged + "MG42";
        public override int TargetID => ModContent.ItemType<MG42>();
        private static Asset<Texture2D> masking;
        private float randomShootRotset;
        private float shootValue;
        void ICWRLoader.LoadAsset() => masking = CWRUtils.GetT2DAsset(Texture + "_Masking");
        void ICWRLoader.UnLoadData() => masking = null;
        public override void SetRangedProperty() {
            FireTime = 4;
            HandIdleDistanceX = 36;
            HandIdleDistanceY = -4;
            HandFireDistanceX = 36;
            HandFireDistanceY = -10;
            ShootPosToMouLengValue = 46;
            Recoil = 0.5f;
            CanCreateSpawnGunDust = false;
            ForcedConversionTargetAmmoFunc = () => AmmoTypes == ProjectileID.Bullet;
            ToTargetAmmo = ModContent.ProjectileType<NitroShot>();
        }

        public override void NetHeldSend(BinaryWriter writer) {
            base.NetHeldSend(writer);
            writer.Write(randomShootRotset);
        }

        public override void NetHeldReceive(BinaryReader reader) {
            base.NetHeldReceive(reader);
            randomShootRotset = reader.ReadSingle();
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
                    Vector2 spanPos = ShootPos + ShootVelocityInProjRot.UnitVector() * Main.rand.NextFloat(-33f, 42f);
                    Dust.NewDust(spanPos, 3, 3, DustID.Smoke, 0, -3, 55, Scale: Main.rand.NextFloat(1, 3));
                }
            }
        }

        public override void SetShootAttribute() {
            if (Projectile.IsOwnedByLocalPlayer()) {
                randomShootRotset = Main.rand.NextFloat(-0.1f, 0.1f);
                NetUpdate();
            }
            shootValue += 0.4f;
        }

        public override void FiringShoot() {
            int proj = Projectile.NewProjectile(Source, ShootPos, ShootVelocityInProjRot
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].ArmorPenetration = 20;
            if (shootValue > 10) {
                Main.projectile[proj].scale *= 2;
            }
        }

        public override void PostGunDraw(Vector2 drawPos, ref Color lightColor) {
            Color maskingColor = lightColor;
            if (shootValue > 0) {
                maskingColor = VaultUtils.MultiStepColorLerp(shootValue / 16f, lightColor, Color.Red);
            }
            Main.EntitySpriteDraw(masking.Value, drawPos, null, maskingColor
                , Projectile.rotation, masking.Size() / 2, Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }
    }
}
