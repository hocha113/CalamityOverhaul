using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class BlossomFluxHeldProj : BaseHeldRanged
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "BlossomFlux";

        private bool onFireR;
        internal int onFireSmogTime;
        internal int onFireCooldSmogTime;
        internal float onFireCooldSmogTime2;

        public override bool CheckAlive() {
            bool heldBool1 = Item.type != ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.BlossomFlux>();
            bool heldBool2 = Item.type != ModContent.ItemType<BlossomFluxEcType>();
            if (CWRServerConfig.Instance.ForceReplaceResetContent) {//如果开启了强制替换
                if (heldBool1) {//只需要判断原版的物品
                    return false;
                }
            }
            else {//如果没有开启强制替换
                if (heldBool2) {
                    return false;
                }
            }
            return true;
        }

        public override void InOwner() {
            float armRotSengsFront = 60 * CWRUtils.atoR;
            float armRotSengsBack = 110 * CWRUtils.atoR;

            Projectile.Center = Owner.Center + new Vector2(DirSign * 12, 0);
            Projectile.rotation = DirSign > 0 ? MathHelper.ToRadians(20) : MathHelper.ToRadians(160);
            Projectile.timeLeft = 2;
            SetHeld();

            if (Owner.PressKey()) {
                onFire = true;
                Owner.direction = ToMouse.X > 0 ? 1 : -1;
                Projectile.rotation = ToMouseA;
                Projectile.Center = Owner.Center + Projectile.rotation.ToRotationVector2() * 12;
                armRotSengsBack = armRotSengsFront = (MathHelper.PiOver2 - (ToMouseA + 0.5f * DirSign)) * DirSign;
                if (HaveAmmo) {
                    float sengs = MathF.Sin(Time * (0.6f - onFireCooldSmogTime * 0.01f)) * 0.7f;
                    armRotSengsFront += sengs;
                    onFireSmogTime++;
                }
            }
            else {
                onFire = false;
                onFireSmogTime = 0;
                onFireCooldSmogTime = 60;
            }

            if (Owner.PressKey(false) && !onFire) {
                onFireR = true;
                Owner.direction = ToMouse.X > 0 ? 1 : -1;
                Projectile.rotation = ToMouseA;
                Projectile.Center = Owner.Center + Projectile.rotation.ToRotationVector2() * 12;
                armRotSengsBack = armRotSengsFront = (MathHelper.PiOver2 - (ToMouseA + 0.5f * DirSign)) * DirSign;
            }
            else {
                onFireR = false;
            }

            if (Owner.CWR().OnHit) {
                if (onFireCooldSmogTime <= Item.useTime) {
                    SoundEngine.PlaySound(CWRSound.Peuncharge with { Volume = 0.3f }, Projectile.Center);
                }
                onFireCooldSmogTime = 60;
            }

            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRotSengsFront * -DirSign);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, armRotSengsBack * -DirSign);
        }

        public override void SpanProj() {
            if (onFireR) {
                if (Owner.ownedProjectileCounts[ModContent.ProjectileType<SporeBombOnSpan>()] == 0) {
                    Projectile.NewProjectile(Owner.parent(), Projectile.Center, Vector2.Zero, 
                        ModContent.ProjectileType<SporeBombOnSpan>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
                }
                return;
            }
            if (onFire) {
                WeaponKnockback = Owner.GetWeaponKnockback(Owner.ActiveItem(), WeaponKnockback);
                if (onFireSmogTime > onFireCooldSmogTime && HaveAmmo) {
                    SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
                    Projectile.NewProjectile(Owner.parent(), Projectile.Center, UnitToMouseV * 13
                        , ModContent.ProjectileType<LeafArrows>(), WeaponDamage, WeaponKnockback, Owner.whoAmI);
                    Owner.PickAmmo(Owner.ActiveItem(), out AmmoTypes, out ScaleFactor, out WeaponDamage, out WeaponKnockback, out _);
                    onFireSmogTime = 0;
                }
                if (Time % 10 == 0) {
                    onFireCooldSmogTime -= 1;
                    if (onFireCooldSmogTime < Item.useTime) {
                        onFireCooldSmogTime = Item.useTime;
                    }
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = CWRUtils.GetT2DValue(Texture);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, onFire? Color.White : lightColor
                , Projectile.rotation, CWRUtils.GetOrig(value), Projectile.scale, SpriteEffects.None);
            return false;
        }
    }
}
