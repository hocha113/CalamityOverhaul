using CalamityMod;
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
    internal class AlluvionHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Alluvion";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Alluvion>();
        public override int targetCWRItem => ModContent.ItemType<AlluvionEcType>();
        private SlotId accumulator;
        public override void SetRangedProperty() {
            CanRightClick = true;
            HandFireDistance = 20;
            BowArrowDrawNum = 5;
            ForcedConversionTargetAmmoFunc = () => CalamityUtils.CheckWoodenAmmo(AmmoTypes, Owner);
            ToTargetAmmo = ModContent.ProjectileType<Voidragon>();
        }

        public override void PostInOwner() {
            BowArrowDrawBool = true;
            if (onFireR) {
                Projectile.rotation = -MathHelper.PiOver2;
                Projectile.Center = Owner.Center + Projectile.rotation.ToRotationVector2() * 12;
                ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 - (Projectile.rotation + 0.5f * DirSign)) * DirSign;
                SetCompositeArm();
            }
            if (Owner.ownedProjectileCounts[ModContent.ProjectileType<TyphoonOnSpan>()] == 0) {
                if (SoundEngine.TryGetActiveSound(accumulator, out var sound)) {
                    sound.Stop();
                    accumulator = SlotId.Invalid;
                }
            }
            else {
                BowArrowDrawBool = false;
            }
        }

        public override void HanderPlaySound() {
            if (Owner.ownedProjectileCounts[ModContent.ProjectileType<TyphoonOnSpan>()] == 0) {
                if (onFireR) {
                    accumulator = SoundEngine.PlaySound(CWRSound.Accumulator with { Pitch = -0.7f }, Projectile.Center);
                    return;
                }
                SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
            }
        }

        public override void SetShootAttribute() {
            BowArrowDrawNum = 5;
        }

        public override void BowShoot() {
            if (CalamityUtils.CheckWoodenAmmo(AmmoTypes, Owner)) {
                AmmoTypes = ModContent.ProjectileType<TorrentialArrow>();
                for (int i = 0; i < 5; i++) {
                    int proj = Projectile.NewProjectile(Source, Projectile.Center + UnitToMouseV.GetNormalVector() * (-2 + i) * 17
                        , ShootVelocity.UnitVector() * 17, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                    Main.projectile[proj].CWR().SpanTypes = (byte)SpanTypesEnum.Alluvion;
                    Main.projectile[proj].SetArrowRot();
                }
            }
            else {
                for (int i = 0; i < 5; i++) {
                    int proj = Projectile.NewProjectile(Source, Projectile.Center + UnitToMouseV.GetNormalVector() * (-2 + i) * 12
                        , ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                    Main.projectile[proj].CWR().SpanTypes = (byte)SpanTypesEnum.Alluvion;
                    Main.projectile[proj].SetArrowRot();
                }
            }
        }

        public override void BowShootR() {
            if (Owner.ownedProjectileCounts[ModContent.ProjectileType<TyphoonOnSpan>()] == 0) {
                Projectile.NewProjectile(Source, Projectile.Center, Vector2.Zero
                    , ModContent.ProjectileType<TyphoonOnSpan>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
            }
        }
    }
}
