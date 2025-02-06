using CalamityMod;
using CalamityMod.CalPlayer;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RangedModify.Core
{
    public abstract class BaseHeldRanged : BaseHeldProj
    {
        #region Date
        /// <summary>
        /// 获取对应的<see cref="CWRPlayer"/>实例，在弹幕初始化时更新这个值
        /// </summary>
        public CWRPlayer ModOwner = null;
        /// <summary>
        /// 获取对应的<see cref="CalamityPlayer"/>实例，在弹幕初始化时更新这个值
        /// </summary>
        public CalamityPlayer CalOwner;
        /// <summary>
        /// 获取对应的<see cref="CWRItems"/>实例，在弹幕初始化时更新这个值
        /// </summary>
        public CWRItems ModItem = null;
        /// <summary>
        /// 每次发射事件是否运行全局物品行为，默认为<see cref="true"/>
        /// </summary>
        public bool GlobalItemBehavior = true;
        /// <summary>
        /// 一个通用的开火计数器
        /// </summary>
        public int fireIndex;
        /// <summary>
        /// 一个通用的计时器
        /// </summary>
        public ref float Time => ref Projectile.ai[0];
        /// <summary>
        /// 目标物品对象
        /// </summary>
        public virtual int TargetID => ItemID.None;
        /// <summary>
        /// 右手角度值
        /// </summary>
        public float ArmRotSengsFront;
        /// <summary>
        /// 左手角度值
        /// </summary>
        public float ArmRotSengsBack;
        /// <summary>
        /// 远程武器本身是否具有碰撞伤害
        /// </summary>
        public bool CanMelee;
        /// <summary>
        /// 是否必定消耗弹药，默认为<see langword="true"/>
        /// </summary>
        public bool MustConsumeAmmunition = true;
        /// <summary>
        /// 在非开火的手持闲置期间是否始终使用开火状态的状态设置，包括武器旋转角度和武器位置设置，默认为<see langword="false"/>
        /// </summary>
        public bool InOwner_HandState_AlwaysSetInFireRoding;
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
        public float ShootSpeedModeFactor = 11f;
        /// <summary>
        /// 获取一个实时的远程伤害
        /// </summary>
        public int WeaponDamage;
        /// <summary>
        /// 获取一个实时的远程击退
        /// </summary>
        public float WeaponKnockback;
        /// <summary>
        /// 获取远程攻速加成系数
        /// </summary>
        public float AttackSpeed => Owner.GetAttackSpeed<RangedDamageClass>() + Item.GetPrefixState().shootSpeedMult - 1f;
        /// <summary>
        /// 是否启用手持开关
        /// </summary>
        public bool WeaponHandheldDisplay => CWRServerConfig.Instance.WeaponHandheldDisplay;
        /// <summary>
        /// 是否自动设置手臂状态
        /// </summary>
        public virtual bool OnHandheldDisplayBool => true;
        /// <summary>
        /// 是否处于开火时间
        /// </summary>
        public override bool CanFire => false;
        /// <summary>
        /// 手持的绘制矫正值，一般只在绘制函数中获取这个属性的值以确保更新即时
        /// </summary>
        public Vector2 SpecialDrawPositionOffset => CanFire ? Vector2.Zero : Owner.CWR().SpecialDrawPositionOffset;
        /// <summary>
        /// 发射口位置
        /// </summary>
        public virtual Vector2 ShootPos => Projectile.Center;
        /// <summary>
        /// 武器适应性缩放，默认为1
        /// </summary>
        public float Scaling = 1;
        /// <summary>
        /// 是否启用手持动画，默认为<see langword="true"/>
        /// </summary>
        public bool HandheldDisplay = true;
        /// <summary>
        /// 是否是单手手持动作，默认为<see langword="false"/>
        /// </summary>
        public bool Onehanded;//这就引申出来一个有趣的问题：泰拉人到底是左撇子还是右撇子？那边是右手？
        /// <summary>
        /// 开火冷切计时器
        /// </summary>
        public float ShootCoolingValue {
            get => Projectile.ai[1];
            set => Projectile.ai[1] = value;
        }
        /// <summary>
        /// 获取射击向量
        /// </summary>
        public Vector2 ShootVelocity => ShootSpeedModeFactor * UnitToMouseV;
        /// <summary>
        /// 获取射击向量，该属性以远程武器本身的选择角度为基准
        /// </summary>
        public Vector2 ShootVelocityInProjRot => ShootSpeedModeFactor * Projectile.rotation.ToRotationVector2();
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
        /// 弹药转化目标
        /// </summary>
        protected int ToTargetAmmo;
        /// <summary>
        /// 关于绘制的弹药转化目标，如果值大于0，则会在绘制效果上覆盖<see cref="ToTargetAmmo"/>，默认为0
        /// </summary>
        protected int ToTargetAmmoInDraw = 0;
        /// <summary>
        /// 一个委托变量，用于决定什么弹药会被转化，与<see cref="ToTargetAmmo"/>配合使用
        /// </summary>
        protected Func<bool> ForcedConversionTargetAmmoFunc = () => false;
        /// <summary>
        /// <see cref="ForcedConversionTargetAmmoFunc"/>为<see langword="true"/>时是否让弹药倒转
        /// </summary>
        protected bool ISForcedConversionDrawAmmoInversion;
        /// <summary>
        /// 是否可以右键，默认为<see langword="false"/>
        /// </summary>
        public bool CanRightClick;
        /// <summary>
        /// 鼠标是否处在空闲实际
        /// </summary>
        public bool SafeMousetStart => !Owner.cursorItemIconEnabled && Owner.cursorItemIconID == 0 || SafeMousetStart2;// && !Main.mouseText
        /// <summary>
        /// 如果设置了随时转向，那么实时同步鼠标状态让其在队友眼里面正常些
        /// </summary>
        public override bool CanMouseNet => InOwner_HandState_AlwaysSetInFireRoding;
        /// <summary>
        /// 获取来自物品的生成源
        /// </summary>
        public virtual EntitySource_ItemUse_WithAmmo Source => new EntitySource_ItemUse_WithAmmo(Owner, Item, UseAmmoItemType);
        /// <summary>
        /// 获取来自物品的生成源副本
        /// </summary>
        public virtual EntitySource_ItemUse_WithAmmo Source2 => new EntitySource_ItemUse_WithAmmo(Owner, Item, UseAmmoItemType);
        /// <summary>
        /// 一个额外附属值，用于矫正<see cref="SafeMousetStart"/>的连续，这个值应该在合适的时机被恢复为默认值<see langword="false"/>
        /// </summary>
        public bool SafeMousetStart2;
        private bool _safeMouseInterfaceValue;
        private bool _old_safeMouseInterfaceValue;
        public bool SafeMouseInterfaceValue => _safeMouseInterfaceValue;
        /// <summary>
        /// 自定义本地化键
        /// </summary>
        public override LocalizedText DisplayName {
            get {
                if (TargetID <= ItemID.None) {
                    return base.DisplayName;
                }
                return TargetID < ItemID.Count ?
                    Language.GetText("ItemName." + ItemID.Search.GetName(TargetID))
                    : ItemLoader.GetItem(TargetID).GetLocalization("DisplayName");
            }
        }
        #endregion

        /// <summary>
        /// 发送一个比特体，存储8个栏位的布尔值，
        /// 如果子类准备重写，需要尊重父类的使用逻辑，当前已经占用至4号位
        /// </summary>
        /// <param name="flags"></param>
        /// <returns></returns>
        public override BitsByte SandBitsByte(BitsByte flags) {
            flags = base.SandBitsByte(flags);
            flags[2] = _safeMouseInterfaceValue;
            flags[3] = onFire;
            flags[4] = onFireR;
            return flags;
        }
        /// <summary>
        /// 接受一个比特体，最多处理8个布尔属性的网络更新，
        /// 如果子类准备重写，需要尊重父类的使用逻辑，当前已经占用至4号位
        /// </summary>
        /// <param name="flags"></param>
        public override void ReceiveBitsByte(BitsByte flags) {
            base.ReceiveBitsByte(flags);
            _safeMouseInterfaceValue = flags[2];
            onFire = flags[3];
            onFireR = flags[4];
        }

        public virtual bool CanSpanProj() => !CalOwner.profanedCrystalBuffs;

        public override bool ShouldUpdatePosition() => false;//一般来讲，不希望这类手持弹幕可以移动，因为如果受到速度更新，弹幕会发生轻微的抽搐

        protected bool UpdateConsumeAmmo(bool preCanConsumeAmmo = true) {
            bool canConsume = Owner.IsRangedAmmoFreeThisShot(new Item(UseAmmoItemType));
            if (MustConsumeAmmunition) {
                canConsume = false;
            }
            if (Item.useAmmo == AmmoID.None) {
                return false;
            }

            Owner.PickAmmo(Item, out _, out _, out _, out _, out _, canConsume && preCanConsumeAmmo);
            return canConsume;
        }

        protected void UpdateShootState() {
            if (Item.useAmmo == AmmoID.None) {
                WeaponDamage = Owner.GetWeaponDamage(Item);
                WeaponKnockback = Item.knockBack;
                AmmoTypes = Item.shoot;
                ShootSpeedModeFactor = Item.shootSpeed;
                UseAmmoItemType = ItemID.None;
                HaveAmmo = true;
                if (AmmoTypes == 0 || AmmoTypes == 10) {
                    AmmoTypes = ProjectileID.Bullet;
                }
                return;
            }

            HaveAmmo = Owner.PickAmmo(Item, out AmmoTypes, out ShootSpeedModeFactor, out WeaponDamage, out WeaponKnockback, out UseAmmoItemType, true);
        }

        public virtual void SetCompositeArm() {
            if (OnHandheldDisplayBool) {
                Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, ArmRotSengsFront * -DirSign);
                if (!Onehanded) {
                    Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, ArmRotSengsBack * -DirSign);
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
            Projectile.CWR().NotSubjectToSpecialEffects = true;
            CWRUtils.SafeLoadItem(TargetID);
            CWRUtils.SafeLoadProj(ToTargetAmmo);
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
            if (CWRKeySystem.ADS_Key.Old) {
                ScopeLeng += 4f;
                if (ScopeLeng > 40f) {
                    ScopeLeng = 40f;
                }
                Main.SetCameraLerp(0.15f, 60);
                ModOwner.OffsetScreenPos = ToMouse.UnitVector() * ScopeLeng;
            }
            else {
                ScopeLeng = 0;
            }
        }

        private void UpdateSafeMouseInterfaceValue() {
            if (!CanFire) {//只有在玩家不进行开火尝试时才能更改空闲状态
                if (Projectile.IsOwnedByLocalPlayer()) {
                    _safeMouseInterfaceValue = !Owner.CWR().uiMouseInterface;
                    if (_old_safeMouseInterfaceValue != _safeMouseInterfaceValue) {
                        NetUpdate();
                    }
                    _old_safeMouseInterfaceValue = _safeMouseInterfaceValue;
                }
                if (!_safeMouseInterfaceValue) {//如果鼠标已经被锁定为非空闲状态，那么开火状态也需要锁定为关
                    onFire = onFireR = false;
                    ShootCoolingValue = 0;
                }
            }
        }

        private void UpdateRogueStealth() {
            bool noAvailable = false;
            if (CWRMod.Instance.narakuEye != null) {
                noAvailable = (bool)CWRMod.Instance.narakuEye.Call(Owner);
                if (CalOwner.StealthStrikeAvailable()) {
                    noAvailable = false;
                }
            }
            if (!noAvailable) {
                CalOwner.rogueStealth = 0;
                if (CalOwner.stealthUIAlpha > 0.02f) {
                    CalOwner.stealthUIAlpha -= 0.02f;
                }
            }
        }

        public void SetWeaponOccupancyStatus() {
            Owner.itemTime = 2;
            Owner.CWR().DontSwitchWeaponTime = 2;
        }

        public override bool PreUpdate() {
            SetHeld();
            UpdateSafeMouseInterfaceValue();
            if (CanFire && _safeMouseInterfaceValue) {
                SetWeaponOccupancyStatus();
                UpdateRogueStealth();
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
            if (CanSpanProj()) {
                SpanProj();
            }
            Time++;
        }

        public override bool ExtraPreSet() {
            if (Item.type != TargetID) { //只需要判断原版的物品
                Projectile.Kill();
                return false;
            }

            if (Owner.CCed || !Owner.active || Owner.dead) {
                Projectile.Kill();
                return false;
            }

            ModItem = Item.CWR();
            ModOwner = Owner.CWR();
            CalOwner = Owner.Calamity();
            return true;
        }
        public virtual void InOwner() {

        }
        /// <summary>
        /// 一个快捷创建手持事件的方法，在<see cref="InOwner"/>中被调用，值得注意的是，如果需要更强的自定义效果，一般是需要直接重写<see cref="InOwner"/>的
        /// </summary>
        public virtual void FiringIncident() {

        }

        public virtual void HanderPlaySound() {
            SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
        }

        public virtual void SpanProj() {

        }

        public override void OrigItemShoot() {
            GlobalItemBehavior = false;
            if (onFireR) {
                Owner.altFunctionUse = 2;
            }
            if (Item.type < ItemID.Count) {
                ItemCheck_Shoot_Method.Invoke(Owner, [Owner.whoAmI, Item, WeaponDamage]);
            }
            else if (CombinedHooks.CanShoot(Owner, Item)
                && CombinedHooks.Shoot(Owner, Item, Source, ShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback)) {
                Projectile.NewProjectile(Source, ShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI);
            }
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
                    , ShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback);
            }
        }
    }
}
