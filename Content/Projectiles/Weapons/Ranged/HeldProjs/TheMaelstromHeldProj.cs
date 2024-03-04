using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using System;
using Terraria.Audio;
using Terraria.ModLoader;
using Terraria;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class TheMaelstromHeldProj : BaseHeldRanged
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "TheMaelstrom";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.TheMaelstrom>();
        public override int targetCWRItem => ModContent.ItemType<TheMaelstrom>();
        private bool onFireR;
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
                    Projectile.Center = Owner.Center + Projectile.rotation.ToRotationVector2() * 22;
                    armRotSengsBack = armRotSengsFront = (MathHelper.PiOver2 - (ToMouseA + 0.5f * DirSign)) * DirSign;
                    if (HaveAmmo && Projectile.IsOwnedByLocalPlayer()) {
                        onFire = true;
                        Projectile.ai[1]++;
                        armRotSengsFront += MathF.Sin(Time * 0.5f) * 0.7f;
                    }
                }
                else {
                    onFire = false;
                }

                if (Owner.PressKey(false) && !onFire) {
                    Owner.direction = ToMouse.X > 0 ? 1 : -1;
                    Projectile.rotation = ToMouseA;
                    Projectile.Center = Owner.Center + Projectile.rotation.ToRotationVector2() * 12;
                    armRotSengsBack = armRotSengsFront = (MathHelper.PiOver2 - (ToMouseA + 0.5f * DirSign)) * DirSign;
                    if (HaveAmmo && Projectile.IsOwnedByLocalPlayer()) {
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
            if (onFire && Projectile.ai[1] > heldItem.useTime) {
                SoundEngine.PlaySound(SoundID.Item5, Projectile.Center);
                Projectile.NewProjectile(Owner.parent(), Projectile.Center, UnitToMouseV * 13
                    , ModContent.ProjectileType<TheMaelstromTheArrow>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Projectile.ai[1] = 0;
                onFire = false;
            }
            if (onFireR && Owner.ownedProjectileCounts[ModContent.ProjectileType<TheMaelstromSharkOnSpan>()] == 0) {
                SoundEngine.PlaySound(SoundID.Item66, Projectile.Center);
                Projectile.NewProjectile(Owner.parent(), Projectile.Center, Vector2.Zero
                    , ModContent.ProjectileType<TheMaelstromSharkOnSpan>(), WeaponDamage * 2, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
                Projectile.ai[1] = 0;
                onFire = false;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = CWRUtils.GetT2DValue(Texture);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, onFire ? Color.White : lightColor
                , Projectile.rotation, value.Size() / 2, Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            return false;
        }
    }
}
