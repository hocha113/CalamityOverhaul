using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs
{
    internal class GhastlyVisageHeldProj : BaseMagicGun
    {
        public override string Texture => CWRConstant.Item_Magic + "GhastlyVisage";
        public override int targetCayItem => ModContent.ItemType<GhastlyVisage>();
        public override int targetCWRItem => ModContent.ItemType<GhastlyVisageEcType>();
        public override void SetRangedProperty() {
            Projectile.DamageType = DamageClass.Magic;
            GunPressure = 0;
            HandDistance = 15;
            HandDistanceY = -5;
            Recoil = 0;
            ArmRotSengsFrontNoFireOffset = 13;
            AngleFirearmRest = 0;
        }

        public override void PostInOwnerUpdate() {
            if (onFire) {
                CWRUtils.ClockFrame(ref Projectile.frame, 5, 3);
                Projectile.Center = Owner.MountedCenter + (DirSign > 0 ? new Vector2(13, -20) : new Vector2(-13, -20));
                Projectile.rotation = DirSign > 0 ? 0 : MathHelper.Pi;
                ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 + (DirSign > 0 ? MathHelper.ToRadians(60) : MathHelper.ToRadians(120))) * DirSign;
                if (Time % 5 == 0) {
                    Vector2 vr = CWRUtils.GetRandomVevtor(-120, -60, 3);
                    Projectile.NewProjectile(Owner.parent(), Projectile.Center, vr,
                        ModContent.ProjectileType<SpiritFlame>(), Projectile.damage / 4, 0, Owner.whoAmI, 3);
                }
            }
            else {
                Projectile.frame = 0;
            }
        }

        public override void FiringShoot() {
            int type = ModContent.ProjectileType<GhastlyVisageBall>();
            SoundEngine.PlaySound(in SoundID.Item117, Projectile.position);
            for (int i = 0; i < Main.rand.Next(3, 4); i++) {
                Projectile.NewProjectile(Source2, Projectile.Center,
                ShootVelocity.RotatedByRandom(0.4f).UnitVector() * 3,
                type, WeaponDamage, WeaponKnockback, Owner.whoAmI);
            }
        }

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            Main.EntitySpriteDraw(TextureValue, drawPos, CWRUtils.GetRec(TextureValue, Projectile.frame, 4), lightColor
                , Projectile.rotation, CWRUtils.GetOrig(TextureValue, 4), Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }
    }
}
