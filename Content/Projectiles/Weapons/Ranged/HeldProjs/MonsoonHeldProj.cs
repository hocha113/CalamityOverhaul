using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using ReLogic.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class MonsoonHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Monsoon";
        public override int targetCayItem => ModContent.ItemType<Monsoon>();
        public override int targetCWRItem => ModContent.ItemType<MonsoonEcType>();
        private SlotId accumulator;
        public override void SetRangedProperty() {
            CanRightClick = true;
            HandDistance = 18;
            HandFireDistance = 24;
            BowArrowDrawNum = 5;
            DrawArrowMode = -26;
            BowstringData.DeductRectangle = new Rectangle(2, 16, 2, 46);
        }

        public override void PostInOwner() {
            CanFireMotion = true;
            FiringDefaultSound = true;
            BowArrowDrawBool = onFire;
            BowstringData.CanDraw = BowstringData.CanDeduct = onFire;
            if (onFireR) {
                CanFireMotion = false;
                Owner.direction = ToMouse.X > 0 ? 1 : -1;
                Projectile.rotation = -MathHelper.PiOver2;
                Projectile.Center = Owner.GetPlayerStabilityCenter() + Projectile.rotation.ToRotationVector2() * HandFireDistance;
                ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 - (Projectile.rotation + 0.5f * DirSign)) * DirSign;
                Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, ArmRotSengsFront * -DirSign);
                Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, ArmRotSengsBack * -DirSign);
            }

            if (Owner.ownedProjectileCounts[ModContent.ProjectileType<MonsoonOnSpan>()] == 0) {
                if (SoundEngine.TryGetActiveSound(accumulator, out var sound)) {
                    sound.Stop();
                    accumulator = SlotId.Invalid;
                }
            }
        }

        public override void HanderPlaySound() {
            if (Owner.ownedProjectileCounts[ModContent.ProjectileType<MonsoonOnSpan>()] == 0) {
                if (onFireR) {
                    accumulator = SoundEngine.PlaySound(CWRSound.Accumulator with { Pitch = -0.3f }, Projectile.Center);
                    return;
                }
                SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
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
                Projectile.NewProjectile(Source, Projectile.Center, Vector2.Zero
                    , ModContent.ProjectileType<MonsoonOnSpan>(), WeaponDamage
                    , WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
            }
        }
    }
}
