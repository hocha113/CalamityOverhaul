using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RangedModify.Core;
using ReLogic.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class AlluvionHeld : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Alluvion";
        private SlotId accumulator;
        public override void SetRangedProperty() {
            CanRightClick = true;
            BowArrowDrawNum = 5;
            DrawArrowMode = -30;
            ForcedConversionTargetAmmoFunc = () => Owner.IsWoodenAmmo(AmmoTypes);
            ToTargetAmmo = CWRID.Proj_TorrentialArrow;
        }

        public override void PostInOwner() {
            BowArrowDrawBool = onFire;
            if (onFireR) {
                Projectile.rotation = -MathHelper.PiOver2;
                Projectile.Center = Owner.GetPlayerStabilityCenter() + Projectile.rotation.ToRotationVector2() * HandheldDistance;
                ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 - (Projectile.rotation + 0.5f * DirSign)) * DirSign;
                SetCompositeArm();
            }
            if (Owner.ownedProjectileCounts[ModContent.ProjectileType<TyphoonOnSpan>()] == 0) {
                if (SoundEngine.TryGetActiveSound(accumulator, out var sound)) {
                    sound.Stop();
                    accumulator = SlotId.Invalid;
                }
            }
        }

        public override void HanderPlaySound() {
            if (Owner.ownedProjectileCounts[ModContent.ProjectileType<TyphoonOnSpan>()] == 0) {
                if (onFireR) {
                    accumulator = SoundEngine.PlaySound(CWRSound.Accumulator with { Pitch = 0.3f }, Projectile.Center);
                    return;
                }
                SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
            }
        }

        public override void BowShoot() {
            if (AmmoTypes == ToTargetAmmo) {
                for (int i = 0; i < 5; i++) {
                    int proj = Projectile.NewProjectile(Source, Projectile.Center + UnitToMouseV.GetNormalVector() * (-2 + i) * 12
                        , ShootVelocity.UnitVector() * 17, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                    Main.projectile[proj].CWR().SpanTypes = (byte)SpanTypesEnum.Alluvion;
                    Main.projectile[proj].SetArrowRot();
                }
            }
            else {
                for (int i = 0; i < 5; i++) {
                    int proj = Projectile.NewProjectile(Source, Projectile.Center + UnitToMouseV.GetNormalVector() * (-2 + i) * 8
                        , ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                    Main.projectile[proj].CWR().SpanTypes = (byte)SpanTypesEnum.Alluvion;
                    Main.projectile[proj].SetArrowRot();
                }
            }
        }

        public override void BowShootR() {
            if (Owner.ownedProjectileCounts[ModContent.ProjectileType<TyphoonOnSpan>()] == 0) {
                Projectile.NewProjectile(Source, Projectile.Center, Vector2.Zero
                    , ModContent.ProjectileType<TyphoonOnSpan>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.identity);
            }
        }
    }
}
