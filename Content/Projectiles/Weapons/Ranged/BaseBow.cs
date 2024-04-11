using CalamityMod;
using CalamityMod.Projectiles.Ranged;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal abstract class BaseBow : BaseHeldRanged
    {
        public virtual Texture2D TextureValue => CWRUtils.GetT2DValue(Texture);

        #region Date
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
        /// 是否在<see cref="InOwner"/>执行后自动更新手臂参数，默认为<see langword="true"/>
        /// </summary>
        public bool SetArmRotBool = true;
        /// <summary>
        /// 手持距离，生效于非开火状态下，默认为15
        /// </summary>
        public float HandDistance = 15;
        /// <summary>
        /// 手持距离，生效于非开火状态下，默认为0
        /// </summary>
        public float HandDistanceY = 0;
        /// <summary>
        /// 手持距离，生效于开火状态下，默认为12
        /// </summary>
        public float HandFireDistance = 12;
        /// <summary>
        /// 手持距离，生效于开火状态下，默认为0
        /// </summary>
        public float HandFireDistanceY = 0;
        /// <summary>
        /// 一个开火周期中手臂动画开始的时间，默认为0
        /// </summary>
        public float HandRotStartTime = 0;
        /// <summary>
        /// 一个开火周期中手臂动画的播放速度，默认为0.4f
        /// </summary>
        public float HandRotSpeedSengs = 0.4f;
        /// <summary>
        /// 一个开火周期中手臂动画的播放幅度，默认为0.7f
        /// </summary>
        public float HandRotRange = 0.7f;
        /// <summary>
        /// 是否启用开火动画，默认为<see langword="true"/>
        /// </summary>
        public bool CanFireMotion = true;
        /// <summary>
        /// 开火后是否自动执行弹药消耗逻辑，默认为<see langword="true"/>
        /// </summary>
        public bool CanUpdateConsumeAmmoInShootBool = true;
        /// <summary>
        /// 开火时是否默认播放手持物品的使用音效<see cref="Item.UseSound"/>，但如果准备重写<see cref="SpanProj"/>，这个属性将失去作用，默认为<see langword="true"/>
        /// </summary>
        public bool FiringDefaultSound = true;
        public bool CanFire => Owner.PressKey() || (Owner.PressKey(false) && CanRightClick && !onFire);
        /// <summary>
        /// 是否允许手持状态，如果玩家关闭了手持动画设置，这个值将在非开火状态时返回<see langword="false"/>
        /// </summary>
        public virtual bool OnHandheldDisplayBool {
            get {
                if (WeaponHandheldDisplay) {
                    return true;
                }
                return CanFire;
            }
        }
        /// <summary>
        /// 获取来自物品的生成源
        /// </summary>
        protected IEntitySource Source => new EntitySource_ItemUse_WithAmmo(Owner, Item, Owner.GetShootState().UseAmmoItemType, "CWRBow");
        protected IEntitySource Source2 => new EntitySource_ItemUse_WithAmmo(Owner, Item, Owner.GetShootState().UseAmmoItemType);
        #endregion

        public void SetArmInFire() {
            float backArmRotation = (Projectile.rotation * SafeGravDir + MathHelper.PiOver2) + MathHelper.Pi * DirSign;
            float amountValue = 1 - Projectile.ai[1] / (Item.useTime - HandRotStartTime);
            Player.CompositeArmStretchAmount stretch = amountValue.ToStretchAmount();
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, ArmRotSengsBack * -DirSign);
            Owner.SetCompositeArmFront(true, stretch, backArmRotation);
        }

        public virtual void HandEvent() {
            if (Owner.PressKey()) {
                Owner.direction = ToMouse.X > 0 ? 1 : -1;
                Projectile.rotation = ToMouseA;
                Projectile.Center = Owner.Center + Projectile.rotation.ToRotationVector2() * HandFireDistance + new Vector2(0, HandFireDistanceY);
                ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 - (ToMouseA + 0.5f * DirSign)) * DirSign;
                SetCompositeArm();
                if (HaveAmmo) {
                    onFire = true;
                    Projectile.ai[1]++;
                    if (Projectile.ai[1] > HandRotStartTime && CanFireMotion) {
                        SetArmInFire();
                    } 
                }
            }
            else {
                onFire = false;
            }

            if (Owner.PressKey(false) && CanRightClick && !onFire) {
                Owner.direction = ToMouse.X > 0 ? 1 : -1;
                Projectile.rotation = ToMouseA;
                Projectile.Center = Owner.Center + Projectile.rotation.ToRotationVector2() * HandFireDistance + new Vector2(0, HandFireDistanceY);
                ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 - (ToMouseA + 0.5f * DirSign)) * DirSign;
                SetCompositeArm();
                if (HaveAmmo) {
                    onFireR = true;
                    Projectile.ai[1]++;
                    if (Projectile.ai[1] > HandRotStartTime && CanFireMotion) {
                        SetArmInFire();
                    }
                }
            }
            else {
                onFireR = false;
            }
        }

        public virtual void PreInOwner() {

        }

        public override void InOwner() {
            PreInOwner();

            ArmRotSengsFront = 60 * CWRUtils.atoR;
            ArmRotSengsBack = 110 * CWRUtils.atoR;

            Projectile.Center = Owner.Center + new Vector2(DirSign * HandDistance, HandDistanceY);
            Projectile.rotation = DirSign > 0 ? MathHelper.ToRadians(20) : MathHelper.ToRadians(160);
            Projectile.timeLeft = 2;
            SetHeld();
            SetCompositeArm();
            if (!Owner.mouseInterface) {
                HandEvent();
            }

            PostInOwner();
        }

        public void SetCompositeArm() {
            if (OnHandheldDisplayBool) {
                Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, ArmRotSengsFront * -DirSign);
                Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, ArmRotSengsBack * -DirSign);
            }
        }

        public virtual void PostInOwner() {

        }

        public virtual void BowShoot() {
            Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }

        public virtual void BowShootR() {
            Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }

        /// <summary>
        /// 一个快捷创建属于卢克索饰品的发射事件，如果luxorsGift为<see langword="true"/>,
        /// 或者<see cref="CWRPlayer.TheRelicLuxor"/>大于0，便会调用该方法，在Firing方法之后调用
        /// </summary>
        public virtual void LuxirEvent() {
            float damageMult = 1f;
            if (Item.useTime < 10) {
                damageMult -= (10 - Item.useTime) / 10f;
            }
            int luxirDamage = Owner.ApplyArmorAccDamageBonusesTo(WeaponDamage * damageMult * 0.15f);
            if (luxirDamage > 1) {
                SpanLuxirProj(luxirDamage);
            }
        }

        public virtual int SpanLuxirProj(int luxirDamage) {
            return Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity
                , ModContent.ProjectileType<LuxorsGiftRanged>(), luxirDamage, WeaponKnockback / 2, Owner.whoAmI, 0);
        }

        public override void SpanProj() {
            if (Projectile.ai[1] > Item.useTime) {             
                if (onFire) {
                    BowShoot();
                }
                if (onFireR) {
                    BowShootR();
                }
                if (Owner.Calamity().luxorsGift || Owner.CWR().TheRelicLuxor > 0) {
                    LuxirEvent();
                }
                foreach (var g in CWRMod.CWR_InItemLoader_Set_CanUse_Hook.Enumerate(Item)) {
                    g.CanUseItem(Item, Owner);
                }
                foreach (var g in CWRMod.CWR_InItemLoader_Set_Shoot_Hook.Enumerate(Item)) {
                    g.Shoot(Item, Owner, (EntitySource_ItemUse_WithAmmo)Source2, Projectile.Center, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback);
                }
                if (FiringDefaultSound) {
                    SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
                }
                UpdateConsumeAmmo();
                Projectile.ai[1] = 0;
                onFire = false;
            }
        }

        public sealed override bool PreDraw(ref Color lightColor) {
            if (OnHandheldDisplayBool) {
                BowDraw(ref lightColor);
            }
            return false;
        }

        public virtual void BowDraw(ref Color lightColor) {
            Main.EntitySpriteDraw(TextureValue, Projectile.Center - Main.screenPosition, null, onFire ? Color.White : lightColor
                , Projectile.rotation, TextureValue.Size() / 2, Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }

        public void LimitingAngle(int minrot = 50, int maxrot = 130) {
            float minRot = MathHelper.ToRadians(minrot);
            float maxRot = MathHelper.ToRadians(maxrot);
            Projectile.rotation = MathHelper.Clamp(ToMouseA + MathHelper.Pi, minRot, maxRot) - MathHelper.Pi;
            if (ToMouseA + MathHelper.Pi > MathHelper.ToRadians(270)) {
                Projectile.rotation = minRot - MathHelper.Pi;
            }
            Projectile.Center = Owner.Center + Projectile.rotation.ToRotationVector2() * HandFireDistance;
            ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 - (Projectile.rotation + 0.5f * DirSign)) * DirSign;
            SetCompositeArm();
        }
    }
}
