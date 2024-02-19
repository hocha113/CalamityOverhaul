using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Terraria.Audio;
using Terraria.ModLoader;
using Terraria;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityMod.Projectiles.Ranged;
using System.Security.Cryptography.X509Certificates;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SeasSearingHeldProj : BaseHeldGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SeasSearing";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.SeasSearing>();
        public override int targetCWRItem => ModContent.ItemType<SeasSearing>();
        public override float ControlForce => 0.05f;
        public override float GunPressure => 0.2f;
        public override float Recoil => 1.5f;
        private const int maxFireCount = 7;
        private int indexFire;
        private bool onFireR;
        public override void InOwner() {
            float armRotSengsFront = 60 * CWRUtils.atoR;
            float armRotSengsBack = 110 * CWRUtils.atoR;

            Projectile.Center = Owner.Center + new Vector2(DirSign * 20, 0);
            Projectile.rotation = DirSign > 0 ? MathHelper.ToRadians(20) : MathHelper.ToRadians(160);
            Projectile.timeLeft = 2;
            SetHeld();

            if (!Owner.mouseInterface) {
                if (Owner.PressKey()) {
                    Owner.direction = ToMouse.X > 0 ? 1 : -1;
                    Projectile.rotation = GunOnFireRot;
                    Projectile.Center = Owner.Center + Projectile.rotation.ToRotationVector2() * 20 + new Vector2(0, -3) + offsetPos;
                    armRotSengsBack = armRotSengsFront = (MathHelper.PiOver2 - Projectile.rotation) * DirSign;
                    if (HaveAmmo) {
                        onFire = true;
                        Projectile.ai[1]++;
                        if (Projectile.ai[1] == 10) {
                            SoundEngine.PlaySound(CWRSound.CaseEjection with { Volume = 0.6f, MaxInstances = 2 }, Projectile.Center);
                            for (int i = 0; i < maxFireCount; i++) {
                                Vector2 vr = (Projectile.rotation - Main.rand.NextFloat(-0.1f, 0.1f) * DirSign)
                                    .ToRotationVector2() * -Main.rand.NextFloat(3, 7) + Owner.velocity;
                                Projectile.NewProjectile(Projectile.parent(), Projectile.Center + ShootVelocity * 2, vr, ModContent.ProjectileType<GunCasing>()
                                    , 10, Projectile.knockBack, Owner.whoAmI);
                            }
                        }
                        if (Projectile.ai[1] > 40) {
                            Projectile.ai[2] = 0;
                            indexFire = 0;
                        }
                    }
                }
                else {
                    onFire = false;
                }

                if (Owner.PressKey(false) && !onFire) {
                    Owner.direction = ToMouse.X > 0 ? 1 : -1;
                    Projectile.rotation = GunOnFireRot;
                    Projectile.Center = Owner.Center + Projectile.rotation.ToRotationVector2() * 20 + new Vector2(0, -3) + offsetPos;
                    armRotSengsBack = armRotSengsFront = (MathHelper.PiOver2 - Projectile.rotation) * DirSign;
                    if (HaveAmmo) {
                        onFireR = true;
                        Projectile.ai[1]++;
                    }
                }
                else {
                    onFireR = false;
                }
            }

            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRotSengsFront * -DirSign);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, armRotSengsBack * -DirSign);
        }

        public override void SpanProj() {
            if (onFire && Projectile.ai[1] > heldItem.useTime && Projectile.ai[2] == 0) {
                SoundEngine.PlaySound(heldItem.UseSound, Projectile.Center);
                Vector2 gundir = Projectile.rotation.ToRotationVector2();

                Projectile.NewProjectile(Owner.parent(), Projectile.Center + gundir * 3, ShootVelocity
                    , ModContent.ProjectileType<SeasSearingBubble>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);

                _ = UpdateConsumeAmmo();
                _ = CreateRecoil();

                indexFire++;
                if (indexFire > maxFireCount) {
                    Projectile.ai[2]++;
                }
                Projectile.ai[1] = 0;
                onFire = false;
            }

            if (onFireR && Projectile.ai[1] > heldItem.useTime * 4) {
                SoundEngine.PlaySound(heldItem.UseSound, Projectile.Center);
                Vector2 gundir = Projectile.rotation.ToRotationVector2();

                Projectile.NewProjectile(Owner.parent(), Projectile.Center + gundir * 3, ShootVelocity
                    , ModContent.ProjectileType<SeasSearingSecondary>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);

                _ = UpdateConsumeAmmo();
                _ = CreateRecoil();

                Projectile.ai[1] = 0;
                onFireR = false;
            }
        }
    }
}
