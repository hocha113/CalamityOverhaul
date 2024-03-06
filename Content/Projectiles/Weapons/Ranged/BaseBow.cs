using CalamityMod;
using CalamityMod.Projectiles.Ranged;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal abstract class BaseBow : BaseHeldRanged
    {
        /// <summary>
        /// 右手角度值
        /// </summary>
        public float ArmRotSengsFront;
        /// <summary>
        /// 左手角度值
        /// </summary>
        public float ArmRotSengsBack;
        /// <summary>
        /// 是否可以右键
        /// </summary>
        public bool CanRightClick;
        /// <summary>
        /// 是否正在右键开火
        /// </summary>
        protected bool onFireR;
        /// <summary>
        /// 是否在<see cref="InOwner"/>执行后自动更新手臂参数
        /// </summary>
        public bool SetArmRotBool = true;
        /// <summary>
        /// 手持距离，生效于非开火状态下
        /// </summary>
        public float HandDistance = 15;
        /// <summary>
        /// 手持距离，生效于非开火状态下
        /// </summary>
        public float HandDistanceY = 0;
        /// <summary>
        /// 手持距离，生效于开火状态下
        /// </summary>
        public float HandFireDistance = 12;
        /// <summary>
        /// 手持距离，生效于开火状态下
        /// </summary>
        public float HandFireDistanceY = 0;
        /// <summary>
        /// 一个开火周期中手臂动画开始的时间
        /// </summary>
        public float HandRotStartTime = 0;
        /// <summary>
        /// 一个开火周期中手臂动画的播放速度
        /// </summary>
        public float HandRotSpeedSengs = 0.4f;
        /// <summary>
        /// 一个开火周期中手臂动画的播放幅度
        /// </summary>
        public float HandRotRange = 0.7f;

        public virtual void HandEvent() {
            if (Owner.PressKey()) {
                Owner.direction = ToMouse.X > 0 ? 1 : -1;
                Projectile.rotation = ToMouseA;
                Projectile.Center = Owner.Center + Projectile.rotation.ToRotationVector2() * HandFireDistance + new Vector2(0, HandFireDistanceY);
                ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 - (ToMouseA + 0.5f * DirSign)) * DirSign;
                if (HaveAmmo) {
                    onFire = true;
                    Projectile.ai[1]++;
                    if (Projectile.ai[1] > HandRotStartTime)
                        ArmRotSengsFront += MathF.Sin(Time * HandRotSpeedSengs) * HandRotRange;
                }
            }
            else {
                onFire = false;
            }

            if (Owner.PressKey(false) && CanRightClick) {
                Owner.direction = ToMouse.X > 0 ? 1 : -1;
                Projectile.rotation = ToMouseA;
                Projectile.Center = Owner.Center + Projectile.rotation.ToRotationVector2() * HandFireDistance + new Vector2(0, HandFireDistanceY);
                ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 - (ToMouseA + 0.5f * DirSign)) * DirSign;
                if (HaveAmmo) {
                    onFireR = true;
                    Projectile.ai[1]++;
                    if (Projectile.ai[1] > HandRotStartTime)
                        ArmRotSengsFront += MathF.Sin(Time * HandRotSpeedSengs) * HandRotRange;
                }
            }
            else {
                onFireR = false;
            }
        }

        public override void InOwner() {
            ArmRotSengsFront = 60 * CWRUtils.atoR;
            ArmRotSengsBack = 110 * CWRUtils.atoR;

            Projectile.Center = Owner.Center + new Vector2(DirSign * HandDistance, HandDistanceY);
            Projectile.rotation = DirSign > 0 ? MathHelper.ToRadians(20) : MathHelper.ToRadians(160);
            Projectile.timeLeft = 2;
            SetHeld();

            if (!Owner.mouseInterface) {
                HandEvent();
            }

            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, ArmRotSengsFront * -DirSign);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, ArmRotSengsBack * -DirSign);
        }

        public virtual void BowShoot() {
            Projectile.NewProjectile(Owner.parent(), Projectile.Center, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }

        public virtual void BowShootR() {
            Projectile.NewProjectile(Owner.parent(), Projectile.Center, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }

        /// <summary>
        /// 一个快捷创建属于卢克索饰品的发射事件，如果luxorsGift为<see langword="true"/>,
        /// 或者<see cref="CWRPlayer.TheRelicLuxor"/>大于0，便会调用该方法，在Firing方法之后调用
        /// </summary>
        public virtual void LuxirEvent() {
            float damageMult = 1f;
            if (heldItem.useTime < 10) {
                damageMult -= (10 - heldItem.useTime) / 10f;
            }
            int luxirDamage = Owner.ApplyArmorAccDamageBonusesTo(WeaponDamage * damageMult * 0.15f);
            if (luxirDamage > 1) {
                SpanLuxirProj(luxirDamage);
            }
        }

        public virtual int SpanLuxirProj(int luxirDamage) {
            return Projectile.NewProjectile(Owner.parent(), Projectile.Center, ShootVelocity
                , ModContent.ProjectileType<LuxorsGiftRanged>(), luxirDamage, WeaponKnockback / 2, Owner.whoAmI, 0);
        }

        public override void SpanProj() {
            if (Projectile.ai[1] > heldItem.useTime) {
                SoundEngine.PlaySound(heldItem.UseSound, Projectile.Center);
                if (onFire) {
                    BowShoot();
                }
                if (onFireR) {
                    BowShootR();
                }
                if (Owner.Calamity().luxorsGift || Owner.CWR().TheRelicLuxor > 0) {
                    LuxirEvent();
                }
                Projectile.ai[1] = 0;
                onFire = false;
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
