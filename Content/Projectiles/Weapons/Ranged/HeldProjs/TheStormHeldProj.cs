using CalamityMod;
using CalamityMod.Projectiles.Ranged;
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
    internal class TheStormHeldProj : BaseHeldRanged
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "TheStorm";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.TheStorm>();
        public override int targetCWRItem => ModContent.ItemType<TheStormEcType>();
        public override void InOwner() {
            CWRUtils.ClockFrame(ref Projectile.frame, 5, 8);

            float armRotSengsFront = 60 * CWRUtils.atoR;
            float armRotSengsBack = 110 * CWRUtils.atoR;

            Projectile.Center = Owner.Center + new Vector2(DirSign * 12, 0);
            Projectile.rotation = DirSign > 0 ? MathHelper.ToRadians(20) : MathHelper.ToRadians(160);
            Projectile.timeLeft = 2;
            SetHeld();

            if (!Owner.mouseInterface) {
                if (Owner.PressKey()) {
                    Owner.direction = ToMouse.X > 0 ? 1 : -1;
                    float minRot = MathHelper.ToRadians(50);
                    float maxRot = MathHelper.ToRadians(130);
                    Projectile.rotation = MathHelper.Clamp(ToMouseA + MathHelper.Pi, minRot, maxRot) - MathHelper.Pi;
                    if (ToMouseA + MathHelper.Pi > MathHelper.ToRadians(270)) {
                        Projectile.rotation = minRot - MathHelper.Pi;
                    }
                    Projectile.Center = Owner.Center + Projectile.rotation.ToRotationVector2() * 12;
                    armRotSengsBack = armRotSengsFront = (MathHelper.PiOver2 - (Projectile.rotation + 0.5f * DirSign)) * DirSign;
                    if (HaveAmmo && Projectile.IsOwnedByLocalPlayer()) {
                        onFire = true;
                        Projectile.ai[1]++;
                        armRotSengsFront += MathF.Sin(Time * 0.7f) * 0.7f;
                    }
                }
                else {
                    onFire = false;
                }
            }

            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRotSengsFront * -DirSign);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, armRotSengsBack * -DirSign);
        }

        public override void SpanProj() {
            if (onFire && Projectile.ai[1] > Item.useTime) {
                SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
                for (int i = 0; i < 5; i++) {
                    if (CalamityUtils.CheckWoodenAmmo(AmmoTypes, Owner)) {
                        AmmoTypes = ModContent.ProjectileType<Bolt>();
                    }
                    Vector2 spanPos = Projectile.Center + new Vector2(Main.rand.Next(-320, 320), Main.rand.Next(-732, -623));
                    Vector2 vr = spanPos.To(Main.MouseWorld).UnitVector().RotatedBy(Main.rand.NextFloat(-0.12f, 0.12f)) * Main.rand.NextFloat(0.6f, 1.52f) * 27;
                    int proj = Projectile.NewProjectile(Owner.parent(), spanPos, vr, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                    Main.projectile[proj].noDropItem = true;
                    Main.projectile[proj].CWR().SpanTypes = (byte)SpanTypesEnum.TheStorm;
                }
                UpdateConsumeAmmo();
                Projectile.ai[1] = 0;
                onFire = false;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = CWRUtils.GetT2DValue(Texture);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, CWRUtils.GetRec(value, Projectile.frame, 9), onFire ? Color.White : lightColor
                , Projectile.rotation, CWRUtils.GetOrig(value, 9), Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            return false;
        }
    }
}
