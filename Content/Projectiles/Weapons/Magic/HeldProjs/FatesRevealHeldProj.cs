using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs
{
    internal class FatesRevealHeldProj : BaseMagicGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "FatesReveal";
        public override int TargetID => ModContent.ItemType<FatesReveal>();
        public override void SetMagicProperty() {
            GunPressure = 0;
            HandIdleDistanceX = 15;
            HandIdleDistanceY = -5;
            HandFireDistanceX = 50;
            Recoil = 0;
            ArmRotSengsFrontNoFireOffset = 13;
            AngleFirearmRest = 0;
        }

        public override void PostInOwner() {
            if (onFire) {
                if (Time % 5 == 0) {
                    Vector2 vr = CWRUtils.GetRandomVevtor(-120, -60, 3);
                    Projectile.NewProjectile(Source2, Projectile.Center, vr,
                        ModContent.ProjectileType<SpiritFlame>(), Projectile.damage / 4, 0, Owner.whoAmI, 3);
                }
            }
        }

        public override void HanderPlaySound() {
            SoundEngine.PlaySound(in SoundID.Item117, Projectile.position);
        }

        public override void FiringShoot() {
            int type = ModContent.ProjectileType<HatredFire>();
            for (int i = 0; i < 2; i++) {
                Projectile.NewProjectile(Source, Projectile.Center,
                ShootVelocity.RotatedBy(MathHelper.ToRadians(Main.rand.Next(-20, 20))).UnitVector() * 7,
                type, Projectile.damage, 2, Owner.whoAmI);
            }
            Vector2 vr = CWRUtils.GetRandomVevtor(-120, -60, 3);
            Projectile.NewProjectile(Source, Projectile.Center + Projectile.rotation.ToRotationVector2() * 36 + Main.rand.NextVector2Unit() * 16,
                vr, ModContent.ProjectileType<SpiritFlame>(), Projectile.damage / 4, 0, Owner.whoAmI, 3);
        }

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            float offsetRot = 0;
            if (DownLeft) {
                offsetRot = DirSign > 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4;
            }
            Main.EntitySpriteDraw(TextureValue, drawPos, null, lightColor, Projectile.rotation + offsetRot
                , TextureValue.Size() / 2, Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }
    }
}
