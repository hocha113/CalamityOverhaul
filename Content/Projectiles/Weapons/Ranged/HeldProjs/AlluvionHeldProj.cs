using CalamityMod;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class AlluvionHeldProj : BaseHeldRanged
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Alluvion";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Alluvion>();
        public override int targetCWRItem => ModContent.ItemType<Alluvion>();
        private bool onFireR;
        private SlotId accumulator;
        public override void InOwner() {
            float armRotSengsFront = 60 * CWRUtils.atoR;
            float armRotSengsBack = 110 * CWRUtils.atoR;

            Projectile.Center = Owner.Center + new Vector2(DirSign * 12, 0);
            Projectile.rotation = DirSign > 0 ? MathHelper.ToRadians(20) : MathHelper.ToRadians(160);
            Projectile.timeLeft = 2;
            SetHeld();

            if (!Owner.mouseInterface) {
                if (Owner.PressKey()) {
                    Owner.direction = ToMouse.X > 0 ? 1 : -1;
                    Projectile.rotation = ToMouseA;
                    Projectile.Center = Owner.Center + Projectile.rotation.ToRotationVector2() * 12;
                    armRotSengsBack = armRotSengsFront = (MathHelper.PiOver2 - (ToMouseA + 0.5f * DirSign)) * DirSign;
                    if (HaveAmmo && Projectile.IsOwnedByLocalPlayer()) {
                        onFire = true;
                        Projectile.ai[1]++;
                        armRotSengsFront += MathF.Sin(Time * 0.4f) * 0.7f;
                    }
                }
                else {
                    onFire = false;
                    Projectile.ai[1] = 0;
                }

                if (Owner.PressKey(false) && !onFire) {
                    Owner.direction = ToMouse.X > 0 ? 1 : -1;
                    Projectile.rotation = -MathHelper.PiOver2;
                    Projectile.Center = Owner.Center + Projectile.rotation.ToRotationVector2() * 12;
                    armRotSengsBack = armRotSengsFront = (MathHelper.PiOver2 - (Projectile.rotation + 0.5f * DirSign)) * DirSign;
                    onFireR = true;
                }
                else {
                    onFireR = false;
                }
            }

            if (Owner.ownedProjectileCounts[ModContent.ProjectileType<TyphoonOnSpan>()] == 0) {
                if (SoundEngine.TryGetActiveSound(accumulator, out var sound)) {
                    sound.Stop();
                    accumulator = SlotId.Invalid;
                }
            }

            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRotSengsFront * -DirSign);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, armRotSengsBack * -DirSign);
        }

        public override void SpanProj() {
            if (onFire && Projectile.ai[1] > Item.useTime) {
                SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
                if (CalamityUtils.CheckWoodenAmmo(AmmoTypes, Owner)) {
                    AmmoTypes = ModContent.ProjectileType<TorrentialArrow>();
                    for (int i = 0; i < 5; i++) {
                        int proj = Projectile.NewProjectile(Owner.parent()
                            , Projectile.Center + UnitToMouseV.GetNormalVector() * (-2 + i) * 17
                            , ShootVelocity.UnitVector() * 17, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                        Main.projectile[proj].CWR().SpanTypes = (byte)SpanTypesEnum.Alluvion;
                    }
                }
                else {
                    for (int i = 0; i < 5; i++) {
                        int proj = Projectile.NewProjectile(Owner.parent()
                            , Projectile.Center + UnitToMouseV.GetNormalVector() * (-2 + i) * 12
                            , ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                        Main.projectile[proj].CWR().SpanTypes = (byte)SpanTypesEnum.Alluvion;
                    }
                }
                
                Projectile.ai[1] = 0;
                onFire = false;
            }
            if (onFireR && Owner.ownedProjectileCounts[ModContent.ProjectileType<TyphoonOnSpan>()] == 0) {
                accumulator = SoundEngine.PlaySound(CWRSound.Accumulator with { Pitch = -0.7f }, Projectile.Center);
                Projectile.NewProjectile(Owner.parent(), Projectile.Center, Vector2.Zero
                    , ModContent.ProjectileType<TyphoonOnSpan>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = CWRUtils.GetT2DValue(Texture);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, onFire ? Color.White : lightColor
                , Projectile.rotation, value.Size() / 2, Projectile.scale, SpriteEffects.None);
            return false;
        }
    }
}
