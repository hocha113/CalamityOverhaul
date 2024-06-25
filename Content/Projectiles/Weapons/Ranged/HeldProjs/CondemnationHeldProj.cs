using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class CondemnationHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Condemnation";
        public override int targetCayItem => ModContent.ItemType<Condemnation>();
        public override int targetCWRItem => ModContent.ItemType<CondemnationEcType>();

        private int fireIndex;
        private int fireIndex2 = 20;
        private int fireIndex3;
        private bool canFireR;
        public override void SetRangedProperty() {
            HandDistance = 30;
            HandDistanceY = 6;
            HandFireDistance = 30;
            ShootPosToMouLengValue = 50;
            ShootPosNorlLengValue = -5;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            DrawCrossArrowNorlMode = 6;
            DrawCrossArrowToMode = -6;
            DrawCrossArrowDrawingDieLengthRatio = 5;
            IsCrossbow = true;
            CanRightClick = true;
            ForcedConversionTargetAmmoFunc = () => true;
            ISForcedConversionDrawAmmoInversion = true;
            ToTargetAmmo = ModContent.ProjectileType<TheArrowPunishment>();
        }

        public override void PostInOwnerUpdate() {
            if (onFireR) {
                for (int i = 0; i < 2; i++) {
                    Dust chargeMagic = Dust.NewDustPerfect(GunShootPos + Main.rand.NextVector2Circular(20f, 20f), 267);
                    chargeMagic.velocity = (GunShootPos - chargeMagic.position) * 0.1f + Owner.velocity;
                    chargeMagic.scale = Main.rand.NextFloat(1f, 1.5f);
                    chargeMagic.color = Projectile.GetAlpha(Color.White);
                    chargeMagic.noGravity = true;
                }
            }
        }

        public override void FiringShoot() {
            SoundEngine.PlaySound(CWRSound.Gun_Crossbow_Shoot, Projectile.Center);
            canFireR = false;
            fireIndex2 = 21;
            fireIndex3 = 0;
            Item.useTime = 10;
            CanDrawCrossArrow = true;
            int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            CWRUtils.SetArrowRot(proj);
            if (Main.rand.NextBool(3)) {
                Main.projectile[proj].ai[0] = 1;
                Main.projectile[proj].scale -= 0.3f;
            }
            else {
                Main.projectile[proj].ArmorPenetration = 100;
            }

            _ = UpdateConsumeAmmo();
            _ = CreateRecoil();

            if (++fireIndex > 6) {
                Item.useTime = 22;
                fireIndex = 0;
                SoundEngine.PlaySound(SoundID.Item108 with { Volume = SoundID.Item108.Volume * 0.3f });
                SoundStyle sound = new("CalamityMod/Sounds/Custom/AbilitySounds/BrimflameRecharge");
                SoundEngine.PlaySound(sound with { Volume = 0.8f, PitchRange = (-0.1f, 0.1f) });
                for (int i = 0; i < 36; i++) {
                    Dust chargeMagic = Dust.NewDustPerfect(GunShootPos, 267);
                    chargeMagic.velocity = (MathHelper.TwoPi * i / 36f).ToRotationVector2() * 5f + Owner.velocity;
                    chargeMagic.scale = Main.rand.NextFloat(1f, 1.5f);
                    chargeMagic.color = Color.Violet;
                    chargeMagic.noGravity = true;
                }
            }
        }

        public override void FiringShootR() {
            Item.useTime = fireIndex2;
            CanDrawCrossArrow = false;
            if (canFireR) {
                Item.useTime = 1;
                AmmoTypes = ModContent.ProjectileType<CondemnationArrowHoming>();
                int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                CWRUtils.SetArrowRot(proj);
                _ = UpdateConsumeAmmo();

                fireIndex3++;
                if (fireIndex3 >= 13) {
                    canFireR = false;
                    fireIndex2 = 21;
                    fireIndex3 = 0;
                }
            }
            fireIndex2--;
            SoundEngine.PlaySound(SoundID.Item108 with { Volume = SoundID.Item108.Volume * 0.3f });
            if (fireIndex2 <= 5 && !canFireR) {
                SoundEngine.PlaySound(new("CalamityMod/Sounds/Custom/AbilitySounds/BrimflameRecharge"));
                for (int i = 0; i < 5; i++) {
                    float angle = MathHelper.Pi * 1.5f - i * MathHelper.TwoPi / 5f;
                    float nextAngle = MathHelper.Pi * 1.5f - (i + 2) * MathHelper.TwoPi / 5f;
                    Vector2 start = angle.ToRotationVector2();
                    Vector2 end = nextAngle.ToRotationVector2();
                    for (int j = 0; j < 40; j++) {
                        Dust starDust = Dust.NewDustPerfect(GunShootPos, 267);
                        starDust.scale = 2.5f;
                        starDust.velocity = Vector2.Lerp(start, end, j / 40f) * 16f;
                        starDust.color = Color.Crimson;
                        starDust.noGravity = true;
                    }
                }
                CanDrawCrossArrow = true;
                canFireR = true;
                return;
            }
            if (!canFireR) {
                for (int i = 0; i < 36; i++) {
                    Dust chargeMagic = Dust.NewDustPerfect(GunShootPos, 267);
                    chargeMagic.velocity = (MathHelper.TwoPi * i / 36f).ToRotationVector2() * 5f + Owner.velocity;
                    chargeMagic.scale = Main.rand.NextFloat(1f, 1.5f);
                    chargeMagic.color = Color.Violet;
                    chargeMagic.noGravity = true;
                }
            }
        }
    }
}
