using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using ReLogic.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class MonsoonHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Monsoon";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Monsoon>();
        public override int targetCWRItem => ModContent.ItemType<MonsoonEcType>();
        private SlotId accumulator;
        public override void SetRangedProperty() {
            CanRightClick = true;
            HandFireDistance = 20;
            BowArrowDrawNum = 5;
        }

        public override void PostInOwner() {
            CanFireMotion = true;
            FiringDefaultSound = true;
            BowArrowDrawBool = true;
            if (onFireR) {
                FiringDefaultSound = CanFireMotion = false;
                Owner.direction = ToMouse.X > 0 ? 1 : -1;
                Projectile.rotation = -MathHelper.PiOver2;
                Projectile.Center = Owner.Center + Projectile.rotation.ToRotationVector2() * 12;
                ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 - (Projectile.rotation + 0.5f * DirSign)) * DirSign;
            }
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, ArmRotSengsFront * -DirSign);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, ArmRotSengsBack * -DirSign);
            if (Owner.ownedProjectileCounts[ModContent.ProjectileType<MonsoonOnSpan>()] == 0) {
                if (SoundEngine.TryGetActiveSound(accumulator, out var sound)) {
                    sound.Stop();
                    accumulator = SlotId.Invalid;
                }
            }
            else {
                BowArrowDrawBool = false;
            }
        }

        public override void BowShoot() {
            for (int i = 0; i < 5; i++) {
                if (Main.rand.NextBool(13)) {
                    AmmoTypes = ModContent.ProjectileType<MiniSharkron>();
                }
                if (Main.rand.NextBool(13)) {
                    AmmoTypes = ModContent.ProjectileType<TyphoonArrow>();
                }
                Projectile.NewProjectile(Source, Projectile.Center + UnitToMouseV.GetNormalVector() * (-2 + i) * 10
                    , ShootVelocity.UnitVector() * 17, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
        }

        public override void BowShootR() {
            if (Owner.ownedProjectileCounts[ModContent.ProjectileType<MonsoonOnSpan>()] == 0) {
                accumulator = SoundEngine.PlaySound(CWRSound.Accumulator with { Pitch = 0.3f }, Projectile.Center);
                Projectile.NewProjectile(Owner.parent(), Projectile.Center, Vector2.Zero
                    , ModContent.ProjectileType<MonsoonOnSpan>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
            }
        }
    }
}
