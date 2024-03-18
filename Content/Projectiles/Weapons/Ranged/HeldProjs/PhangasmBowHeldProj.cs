using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class PhangasmBowHeldProj : BaseHeldRanged
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "PhangasmBow";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Phangasm>();
        public override int targetCWRItem => ModContent.ItemType<PhangasmEcType>();
        private const int maxIndex = 8;
        private int Index {
            get => (int)Projectile.ai[1];
            set => Projectile.ai[1] = value;
        }

        public override void InOwner() {
            float armRotSengsFront = 60 * CWRUtils.atoR;
            float armRotSengsBack = 110 * CWRUtils.atoR;

            Projectile.Center = Owner.Center + new Vector2(DirSign * 12, 0);
            Projectile.rotation = DirSign > 0 ? MathHelper.ToRadians(20) : MathHelper.ToRadians(160);
            Projectile.timeLeft = 2;
            SetHeld();

            if (Owner.PressKey()) {
                Owner.direction = ToMouse.X > 0 ? 1 : -1;
                Projectile.rotation = ToMouseA;
                Projectile.Center = Owner.Center + Projectile.rotation.ToRotationVector2() * 12;
                armRotSengsBack = armRotSengsFront = (MathHelper.PiOver2 - (ToMouseA + 0.5f * DirSign)) * DirSign;
                if (HaveAmmo) {
                    if (++Projectile.frameCounter > 2) {
                        Index++;
                        if (Index > maxIndex) {
                            Index = 0;
                        }
                        onFire = true;
                        Projectile.frameCounter = 0;
                    }
                }
            }
            else {
                onFire = false;
                Index = maxIndex;
            }

            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRotSengsFront * -DirSign);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, armRotSengsBack * -DirSign);
        }

        public override void SpanProj() {
            if (onFire && Index == maxIndex / 2) {
                SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
                for (int i = 0; i < 5; i++) {
                    int proj = Projectile.NewProjectile(Owner.parent(), Projectile.Center
                        , ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.03f, 0.03f)) * Main.rand.NextFloat(0.8f, 1.12f)
                        , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
                    Main.projectile[proj].noDropItem = true;
                    Main.projectile[proj].CWR().SpanTypes = (byte)SpanTypesEnum.Phantom;
                }
                onFire = false;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = CWRUtils.GetT2DValue(Texture);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, CWRUtils.GetRec(value, Index, 9), onFire ? Color.White : lightColor
                , Projectile.rotation, CWRUtils.GetOrig(value, 9), Projectile.scale, SpriteEffects.None);
            return false;
        }
    }
}
