using CalamityMod.Particles;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SpectreRifleHeldProj : BaseHeldGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "SpectreRifle";
        public override float ControlForce => 0.035f;
        public override float GunPressure => 0.75f;
        public override float Recoil => 7f;
        public override bool CheckAlive() {
            return heldItem.type == ModContent.ItemType<SpectreRifle>();
        }

        public override void InOwner() {
            float armRotSengsFront = 60 * CWRUtils.atoR;
            float armRotSengsBack = 110 * CWRUtils.atoR;

            Projectile.Center = Owner.Center + new Vector2(DirSign * 30, 3);
            Projectile.rotation = DirSign > 0 ? MathHelper.ToRadians(20) : MathHelper.ToRadians(160);
            Projectile.timeLeft = 2;
            SetHeld();

            if (!Owner.mouseInterface) {
                if (Owner.PressKey()) {
                    Owner.direction = ToMouse.X > 0 ? 1 : -1;
                    Projectile.rotation = GunOnFireRot;
                    Projectile.Center = Owner.Center + Projectile.rotation.ToRotationVector2() * 30 + new Vector2(0, -8);
                    armRotSengsBack = armRotSengsFront = (MathHelper.PiOver2 - (Projectile.rotation)) * DirSign;
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
                Vector2 pos = Projectile.Center + ShootVelocity.UnitVector() * 33 + ShootVelocity.GetNormalVector() * 5 * DirSign;
                for (int i = 0; i < 12; i++) {
                    int sparkLifetime = Main.rand.Next(22, 36);
                    float sparkScale = Main.rand.NextFloat(1f, 1.5f);
                    Color sparkColor = Color.WhiteSmoke;
                    Vector2 sparkVelocity = ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.1f, 0.1f)) * Main.rand.NextFloat(0.3f, 1.2f);
                    SparkParticle spark = new SparkParticle(pos, sparkVelocity, false, sparkLifetime, sparkScale, sparkColor);
                    GeneralParticleHandler.SpawnParticle(spark);
                }
                Projectile.NewProjectile(Owner.parent(), Projectile.Center, ShootVelocity
                        , ModContent.ProjectileType<LostSoulBullet>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);

                UpdateConsumeAmmo();
                CreateRecoil();

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
