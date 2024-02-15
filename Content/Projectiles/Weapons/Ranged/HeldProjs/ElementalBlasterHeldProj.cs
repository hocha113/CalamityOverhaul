using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ElementalBlasterHeldProj : BaseHeldRanged
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ElementalBlaster";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.ElementalBlaster>();
        public override int targetCWRItem => ModContent.ItemType<ElementalBlaster>();
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
                    Projectile.rotation = ToMouseA;
                    Projectile.Center = Owner.Center + Projectile.rotation.ToRotationVector2() * 25 + new Vector2(0, -5);
                    armRotSengsBack = armRotSengsFront = (MathHelper.PiOver2 - (ToMouseA + 0.5f * DirSign)) * DirSign;
                    if (HaveAmmo) {
                        onFire = true;
                        Projectile.ai[1]++;
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
            if (onFire && Projectile.ai[1] > heldItem.useTime) {
                SoundEngine.PlaySound(heldItem.UseSound, Projectile.Center);
                int proj = Projectile.NewProjectile(Owner.parent(), Projectile.Center, ShootVelocity
                        , ModContent.ProjectileType<EnergyBlast>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Projectile.NewProjectile(Owner.parent(), Projectile.Center, ShootVelocity
                        , ModContent.ProjectileType<EnergyBlast2>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 1, proj, -60);
                Projectile.NewProjectile(Owner.parent(), Projectile.Center, ShootVelocity
                        , ModContent.ProjectileType<EnergyBlast2>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, -1, proj, 60);
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
