using CalamityMod;
using CalamityMod.CalPlayer;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal abstract class BaseHeldRanged : BaseHeldProj
    {
        #region Date
        /// <summary>
        /// 获取对应的<see cref="CWRPlayer"/>实例，在弹幕初始化时更新这个值
        /// </summary>
        public CWRPlayer ModOwner = null;
        protected CalamityPlayer CalPlayer;
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
        /// 所使用的弹药对应的物品ID
        /// </summary>
        public int UseAmmoItemType;
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
        public virtual bool OnHandheldDisplayBool => true;
        /// <summary>
        /// 是否处于开火时间
        /// </summary>
        public virtual bool CanFire => false;
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
        /// <summary>
        /// 是否正在左键开火
        /// </summary>
        protected bool onFire;
        /// <summary>
        /// 是否正在右键开火
        /// </summary>
        protected bool onFireR;
        /// <summary>
        /// 屏幕位移模长
        /// </summary>
        protected float ScopeLeng;
        /// <summary>
        /// 是否可以右键，默认为<see langword="false"/>
        /// </summary>
        public bool CanRightClick;
        /// <summary>
        /// 鼠标是否处在空闲实际
        /// </summary>
        public bool SafeMousetStart => !Owner.cursorItemIconEnabled && Owner.cursorItemIconID == 0 && !Main.mouseText || SafeMousetStart2;
        /// <summary>
        /// 一个额外附属值，用于矫正<see cref="SafeMousetStart"/>的连续，这个值应该在合适的时机被恢复为默认值<see langword="false"/>
        /// </summary>
        public bool SafeMousetStart2;
        bool _safeMouseInterfaceValue;
        bool _old_safeMouseInterfaceValue;
        public bool SafeMouseInterfaceValue {
            get {
                return _safeMouseInterfaceValue;
            }
        }
        #endregion

        public override void SendExtraAI(BinaryWriter writer) {
            base.SendExtraAI(writer);
            BitsByte flags = new BitsByte();
            flags[0] = _safeMouseInterfaceValue;
            flags[1] = onFire;
            flags[2] = onFireR;
            writer.Write(flags);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            base.ReceiveExtraAI(reader);
            BitsByte flags = reader.ReadByte();
            _safeMouseInterfaceValue = flags[0];
            onFire = flags[1];
            onFireR = flags[2];
        }

        public override bool ShouldUpdatePosition() => false;//一般来讲，不希望这类手持弹幕可以移动，因为如果受到速度更新，弹幕会发生轻微的抽搐

        protected bool UpdateConsumeAmmo(bool preCanConsumeAmmo = true) {
            bool canConsume = Owner.IsRangedAmmoFreeThisShot(new Item(UseAmmoItemType));
            if (MustConsumeAmmunition) {
                canConsume = false;
            }
            if (Item.useAmmo == AmmoID.None) {
                return false;
            }
            Owner.PickAmmo(Owner.ActiveItem(), out _, out _, out _, out _, out _, canConsume && preCanConsumeAmmo);
            return canConsume;
        }

        protected void UpdateShootState() {
            if (Item.useAmmo == AmmoID.None) {
                WeaponDamage = Owner.GetWeaponDamage(Item);
                WeaponKnockback = Item.knockBack;
                AmmoTypes = Item.shoot;
                ScaleFactor = Item.shootSpeed;
                UseAmmoItemType = ItemID.None;
                HaveAmmo = true;
                if (AmmoTypes == 0 || AmmoTypes == 10) {
                    AmmoTypes = ProjectileID.Bullet;
                }
                return;
            }
            HaveAmmo = Owner.PickAmmo(Item, out AmmoTypes, out ScaleFactor, out WeaponDamage, out WeaponKnockback, out UseAmmoItemType, true);
        }

        public sealed override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.hide = true;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
            CWRUtils.SafeLoadItem(targetCayItem);
            PreSetRangedProperty();
            SetRangedProperty();
            PostSetRangedProperty();
        }

        /// <summary>
        /// 用于设置额外的基础属性，在<see cref="SetDefaults"/>中于<see cref="SetRangedProperty"/>之前调用
        /// 非必要时不建议重写使用这个重载，而是使用<see cref="SetRangedProperty"/>
        /// </summary>
        public virtual void PreSetRangedProperty() {

        }
        /// <summary>
        /// 用于设置额外的基础属性，在<see cref="SetDefaults"/>中于<see cref="SetRangedProperty"/>之后调用
        /// 非必要时不建议重写使用这个重载，而是使用<see cref="SetRangedProperty"/>
        /// </summary>
        public virtual void PostSetRangedProperty() {

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
                ModOwner.OffsetScreenPos = ToMouse.UnitVector() * ScopeLeng;
            }
            else {
                ScopeLeng = 0;
            }
        }

        void UpdateSafeMouseInterfaceValue() {
            if (!CanFire) {//只有在玩家不进行开火尝试时才能更改空闲状态
                _safeMouseInterfaceValue = !Owner.mouseInterface;
                if (_old_safeMouseInterfaceValue != _safeMouseInterfaceValue) {
                    Projectile.netUpdate = true;
                }
                if (!_safeMouseInterfaceValue) {//如果鼠标已经被锁定为非空闲状态，那么开火状态也需要锁定为关
                    onFire = onFireR = false;
                    Projectile.ai[1] = 0;
                }
                _old_safeMouseInterfaceValue = _safeMouseInterfaceValue;
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
            CalPlayer = Owner.Calamity();
            UpdateSafeMouseInterfaceValue();
            if (CanFire && _safeMouseInterfaceValue) {
                Owner.itemTime = 2;
                CalPlayer.rogueStealth = 0;
                if (CalPlayer.stealthUIAlpha > 0.02f) {
                    CalPlayer.stealthUIAlpha -= 0.02f;
                }
            }
            if (ModItem.Scope && Projectile.IsOwnedByLocalPlayer()) {
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
            if (Owner.CCed || !Owner.active || Owner.dead) {
                return false;
            }

            return true;
        }

        public virtual void InOwner() {

        }

        public virtual void SpanProj() {

        }

        internal void ItemLoaderInFireSetBaver() {
            foreach (var g in CWRMod.CWR_InItemLoader_Set_CanUse_Hook.Enumerate(Item)) {
                g.CanUseItem(Item, Owner);
            }
            foreach (var g in CWRMod.CWR_InItemLoader_Set_UseItem_Hook.Enumerate(Item)) {
                g.UseItem(Item, Owner);
            }
            foreach (var g in CWRMod.CWR_InItemLoader_Set_Shoot_Hook.Enumerate(Item)) {
                g.Shoot(Item, Owner, new EntitySource_ItemUse_WithAmmo(Owner, Item, UseAmmoItemType)
                    , Projectile.Center, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback);
            }
        }
    }
}
