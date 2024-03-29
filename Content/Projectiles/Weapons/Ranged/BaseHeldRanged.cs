﻿using CalamityMod;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal abstract class BaseHeldRanged : BaseHeldProj
    {
        /// <summary>
        /// 获取对应的<see cref="CWRPlayer"/>实例，在弹幕初始化时更新这个值
        /// </summary>
        public CWRPlayer ModOwner = null;
        /// <summary>
        /// 获取对应的<see cref="CWRItems"/>实例，在弹幕初始化时更新这个值
        /// </summary>
        public CWRItems ModItem = null;
        /// <summary>
        /// 一个通用的计时器
        /// </summary>
        public ref float Time => ref Projectile.ai[0];
        /// <summary>
        /// 手持物品实例
        /// </summary>
        public Item Item => Owner.ActiveItem();
        /// <summary>
        /// 对源灾厄的物品对象
        /// </summary>
        public virtual int targetCayItem => ItemID.None;
        /// <summary>
        /// 对本身模组的物品对象
        /// </summary>
        public virtual int targetCWRItem => ItemID.None;
        /// <summary>
        /// 远程武器本身是否具有碰撞伤害
        /// </summary>
        public bool CanMelee;
        /// <summary>
        /// 是否必定消耗弹药，默认为<see langword="true"/>
        /// </summary>
        public bool MustConsumeAmmunition = true;
        /// <summary>
        /// 弹药类型
        /// </summary>
        public int AmmoTypes;
        /// <summary>
        /// 射弹速度
        /// </summary>
        public float ScaleFactor = 11f;
        /// <summary>
        /// 获取一个实时的远程伤害
        /// </summary>
        public int WeaponDamage;
        /// <summary>
        /// 获取一个实时的远程击退
        /// </summary>
        public float WeaponKnockback;
        /// <summary>
        /// 是否启用手持开关
        /// </summary>
        public bool WeaponHandheldDisplay => CWRServerConfig.Instance.WeaponHandheldDisplay;
        /// <summary>
        /// 获取射击向量
        /// </summary>
        public Vector2 ShootVelocity => ScaleFactor * UnitToMouseV;
        /// <summary>
        /// 获取射击向量，该属性以远程武器本身的选择角度为基准
        /// </summary>
        public Vector2 ShootVelocityInProjRot => ScaleFactor * Projectile.rotation.ToRotationVector2();
        /// <summary>
        /// 使用者是否拥有弹药
        /// </summary>
        public bool HaveAmmo;
        protected bool onFire;
        protected float ScopeLeng;

        public override bool ShouldUpdatePosition() => false;//一般来讲，不希望这类手持弹幕可以移动，因为如果受到速度更新，弹幕会发生轻微的抽搐

        protected bool UpdateConsumeAmmo(bool preCanConsumeAmmo = true) {
            bool canConsume = Owner.IsRangedAmmoFreeThisShot(new Item(Owner.GetShootState().UseAmmoItemType));
            if (MustConsumeAmmunition) {
                canConsume = false;
            }
            Owner.PickAmmo(Owner.ActiveItem(), out _, out _, out _, out _, out _, canConsume && preCanConsumeAmmo);
            return canConsume;
        }

        protected void UpdateShootState() {
            HaveAmmo = Owner.PickAmmo(Item, out AmmoTypes, out ScaleFactor, out WeaponDamage, out WeaponKnockback, out _, true);
            if (Item.useAmmo == AmmoID.None) {
                WeaponDamage = Owner.GetWeaponDamage(Item);
                WeaponKnockback = Item.knockBack;
                AmmoTypes = Item.shoot;
                if (AmmoTypes == 0 || AmmoTypes == 10) {
                    AmmoTypes = ProjectileID.Bullet;
                }
            }
        }

        public sealed override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.hide = true;
            SetRangedProperty();
        }

        /// <summary>
        /// 用于设置额外的基础属性，在<see cref="SetDefaults"/>中被最后调用
        /// </summary>
        public virtual void SetRangedProperty() {

        }

        public override bool? CanDamage() => CanMelee;

        protected void ScopeSrecen() {
            Owner.scope = false;
            if (CWRKeySystem.ADS_Key.Old) {
                ScopeLeng += 4f;
                if (ScopeLeng > 30) {
                    ScopeLeng = 30;
                }
                Main.SetCameraLerp(0.05f, 10);
                Owner.CWR().OffsetScreenPos = ToMouse.UnitVector() * ScopeLeng;
            }
            else {
                ScopeLeng = 0;
            }
        }

        public override bool PreAI() {
            if (!CheckAlive()) {
                Projectile.Kill();
                return false;
            }
            SetHeld();
            ModItem = Item.CWR();
            ModOwner = Owner.CWR();
            ModOwner.HeldRangedBool = true;
            ModOwner.SafeHeldProjIndex = Projectile.whoAmI;
            if (Owner.PressKey() && !Owner.mouseInterface) {
                Owner.itemTime = 2;
            }
            if (ModItem.Scope) {
                ScopeSrecen();
            }
            else {
                ScopeLeng = 0;
            }
            UpdateShootState();
            return true;
        }

        public override void AI() {
            InOwner();
            if (Projectile.IsOwnedByLocalPlayer()) {
                SpanProj();
            }
            Time++;
        }

        public virtual bool CheckAlive() {
            bool heldBool1 = Item.type != targetCayItem;
            bool heldBool2 = Item.type != targetCWRItem;
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

        public virtual void InOwner() {

        }

        public virtual void SpanProj() {

        }
    }
}
