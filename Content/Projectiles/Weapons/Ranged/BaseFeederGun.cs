using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    /// <summary>
    /// 比<see cref="BaseGun"/>更为复杂的枪基类，用于更加快速且模板化的实现关于弹匣系统的联动
    /// </summary>
    internal abstract class BaseFeederGun : BaseGun
    {
        #region Date
        /// <summary>
        /// 子弹状态，对应枪械的弹匣内容
        /// </summary>
        internal AmmoState AmmoState;
        /// <summary>
        /// 是否自动在一次单次射击后调用后坐力函数
        /// </summary>
        internal bool CanCreateRecoilBool = true;
        /// <summary>
        /// 换弹是否消耗弹药
        /// </summary>
        internal bool BulletConsumption = true;
        /// <summary>
        /// 是否自动在一次单次射击后调用弹匣更新函数，这负责弹药消耗逻辑，如果设置为<see langword="false"/>就需要手动调用<see cref="UpdateMagazineContents"/>以正常执行弹药逻辑
        /// </summary>
        internal bool CanUpdateMagazineContentsInShootBool = true;
        /// <summary>
        /// 枪械的射弹类型是否被弹匣内容强行影响，默认为<see langword="true"/>
        /// </summary>
        protected bool AmmoTypeAffectedByMagazine = true;
        /// <summary>
        /// 装弹提醒，一般来讲会依赖左键的按键事件进行更新
        /// </summary>
        protected bool LoadingReminder;
        /// <summary>
        /// 是否开始装弹
        /// </summary>
        protected bool OnKreload;
        /// <summary>
        /// 是否已经装好了弹药
        /// </summary>
        protected bool IsKreload;
        /// <summary>
        /// 换弹时是否退还剩余子弹
        /// </summary>
        protected bool ReturnRemainingBullets = true;
        /// <summary>
        /// 单次弹药装填最小数量，默认为一发
        /// </summary>
        protected int MinimumAmmoPerReload = 1;
        /// <summary>
        /// 单次弹药装填最大数量，默认为<see cref="CWRItems.AmmoCapacity"/>的值
        /// </summary>
        protected int LoadingQuantity = 0;
        /// <summary>
        /// 换弹延迟计时器
        /// </summary>
        protected int AutomaticCartridgeChangeDelayTime;
        /// <summary>
        /// 装弹计时器
        /// </summary>
        protected int kreloadTimeValue;
        /// <summary>
        /// 装弹所需要的时间，默认为60
        /// </summary>
        protected int kreloadMaxTime = 60;
        /// <summary>
        /// 开火间隔，默认为10
        /// </summary>
        protected int FireTime = 10;
        /// <summary>
        /// 是否可以重复换弹，默认为<see cref="true"/>
        /// </summary>
        protected bool RepeatedCartridgeChange = true;
        /// <summary>
        /// 是否启用后坐力枪体反向制推效果，默认为<see langword="false"/>
        /// </summary>
        protected bool EnableRecoilRetroEffect;
        /// <summary>
        /// 后坐力制推力度模长，推送方向为<see cref="BaseHeldRanged.ShootVelocity"/>的反向，在<see cref="EnableRecoilRetroEffect"/>为<see langword="true"/>时生效，默认为5f
        /// </summary>
        protected float RecoilRetroForceMagnitude = 5;
        /// <summary>
        /// 快速设置抛壳大小，默认为1
        /// </summary>
        protected float EjectCasingProjSize = 1;
        /// <summary>
        /// 是否是一个多发装填，一般来讲应用于弹容量大于1的枪类，开启后影响<see cref="PreFireReloadKreLoad"/>
        /// </summary>
        protected bool MultipleCartridgeLoading;
        /// <summary>
        /// 一个额外的枪体旋转角度矫正值，默认在<see cref="Recover"/>中恢复为0
        /// </summary>
        protected float FeederOffsetRot;
        /// <summary>
        /// 一个额外的枪体中心位置矫正向量，默认在<see cref="Recover"/>中恢复为<see cref="Vector2.Zero"/>，
        /// </summary>
        protected Vector2 FeederOffsetPos;
        /// <summary>
        /// 快捷获取关于是否开启弹匣系统的设置值
        /// </summary>
        protected bool MagazineSystem => CWRServerConfig.Instance.MagazineSystem;

        public override bool OnHandheldDisplayBool {
            get {
                if (WeaponHandheldDisplay) {
                    return true;
                }
                return CanFire || kreloadTimeValue > 0;
            }
        }
        #endregion

        protected int BulletNum {
            get => ModItem.NumberBullets;
            set => ModItem.NumberBullets = value;
        }

        protected SoundStyle loadTheRounds = CWRSound.CaseEjection2;

        /// <summary>
        /// 抛壳的简易实现
        /// </summary>
        public virtual void EjectCasing() {
            Vector2 vr = (Projectile.rotation - Main.rand.NextFloat(-0.1f, 0.1f) * DirSign).ToRotationVector2() * -Main.rand.NextFloat(3, 7) + Owner.velocity;
            int proj = Projectile.NewProjectile(Projectile.parent(), Projectile.Center, vr, ModContent.ProjectileType<GunCasing>(), 10, Projectile.knockBack, Owner.whoAmI);
            if (EjectCasingProjSize != 1) {
                Main.projectile[proj].scale = EjectCasingProjSize;
            }
        }
        /// <summary>
        /// 关于装弹过程中的具体效果实现，返回<see langword="false"/>禁用默认的效果行为
        /// </summary>
        public virtual bool PreReloadEffects(int time, int maxTime) {
            return true;
        }
        /// <summary>
        /// 关于装弹过程中的第一部分音效的执行
        /// </summary>
        public virtual void KreloadSoundCaseEjection() {
            SoundEngine.PlaySound(CWRSound.CaseEjection with { Volume = 0.5f, PitchRange = (-0.05f, 0.05f) }, Projectile.Center);
        }
        /// <summary>
        /// 关于装弹过程中的第二部分音效的执行
        /// </summary>
        public virtual void KreloadSoundloadTheRounds() {
            SoundEngine.PlaySound(loadTheRounds with { Volume = 0.65f, PitchRange = (-0.1f, 0) }, Projectile.Center);
        }
        /// <summary>
        /// 额外的弹药消耗事件，返回<see langword="false"/>禁用默认弹药消耗逻辑的运行
        /// </summary>
        /// <returns></returns>
        public virtual bool PreConsumeAmmoEvent() {
            return true;
        }
        /// <summary>
        /// 装弹过程中的实际事件，比如人物手部动作的处理逻辑，返回<see langword="false"/>禁用默认逻辑的运行
        /// </summary>
        /// <returns></returns>
        public virtual bool PreOnKreloadEvent() {
            return true;
        }
        /// <summary>
        /// 装弹完成后会执行一次该方法，返回默认值<see langword="true"/>以继续执行后续默认的换弹逻辑
        /// </summary>
        public virtual bool KreLoadFulfill() {
            return true;
        }
        /// <summary>
        /// 是否可以进行手动换弹操作，返回<see langword="false"/>阻止玩家进行换弹操作
        /// </summary>
        /// <returns></returns>
        public virtual bool WhetherStartChangingAmmunition() {
            if (!MagazineSystem) {//如果关闭了弹匣系统，枪械将不再可以换弹，因为弹匣不会再发挥作用
                return false;
            }
            return CWRKeySystem.KreLoad_Key.JustPressed && kreloadTimeValue == 0 
                && (!IsKreload || RepeatedCartridgeChange) 
                && BulletNum < ModItem.AmmoCapacity 
                && !onFire && HaveAmmo && ModItem.NoKreLoadTime == 0;
        }

        public override void Recover() {
            FeederOffsetRot = 0;
            FeederOffsetPos = Vector2.Zero;
        }
        /// <summary>
        /// 统一获取枪体在开火时的旋转角，返回值默认在<see cref="InOwner"/>中被获取设置于Projectile.Center
        /// </summary>
        /// <returns></returns>
        public virtual float GetGunInFireRot() {
            return kreloadTimeValue == 0 ? GunOnFireRot : GetGunBodyRotation();
        }
        /// <summary>
        /// 统一获取枪体在开火时的中心位置，返回值默认在<see cref="InOwner"/>中被获取设置于Projectile.rotation
        /// </summary>
        /// <returns></returns>
        public virtual Vector2 GetGunInFirePos() {
            return kreloadTimeValue == 0 ? Owner.Center + Projectile.rotation.ToRotationVector2() * (HandFireDistance + 5) + new Vector2(0, HandFireDistanceY * Math.Sign(Owner.gravDir)) + OffsetPos : GetGunBodyPostion();
        }
        /// <summary>
        /// 统一获取枪体在静置时的旋转角，返回值默认在<see cref="InOwner"/>中被获取设置于Projectile.Center
        /// </summary>
        /// <returns></returns>
        public virtual float GetGunBodyRotation() {
            int value = (int)(10 + FeederOffsetRot);
            return (Owner.direction > 0 ? MathHelper.ToRadians(value) : MathHelper.ToRadians(180 - value));
        }
        /// <summary>
        /// 统一获取枪体在静置时的中心位置，返回值默认在<see cref="InOwner"/>中被获取设置于Projectile.rotation
        /// </summary>
        /// <returns></returns>
        public virtual Vector2 GetGunBodyPostion() {
            return Owner.Center + new Vector2(Owner.direction * HandDistance, HandDistanceY) + FeederOffsetPos;
        }
        /// <summary>
        /// 先行调用，重写它以设置一些特殊状态
        /// </summary>
        public virtual void PreInOwnerUpdate() {
            
        }
        /// <summary>
        /// 最后调用，重写它以设置一些特殊状态
        /// </summary>
        public virtual void PostInOwnerUpdate() {

        }
        /// <summary>
        /// 初始化弹匣状态
        /// </summary>
        protected void InitializeMagazine() {
            CWRItems cwrItem = ModItem;
            if (cwrItem.MagazineContents == null) {
                AmmoState = Owner.GetAmmoState(Item.useAmmo);//再更新一次弹药状态
                cwrItem.MagazineContents = new Item[cwrItem.AmmoCapacity];
                for (int i = 0; i < cwrItem.MagazineContents.Length; i++) {
                    cwrItem.MagazineContents[i] = new Item();
                }
            }
        }
        /// <summary>
        /// 设置自动换弹，在一定条件下让玩家开始换弹，这个方法一般不需要手动调用，枪械会在合适的时候自行调用该逻辑
        /// </summary>
        protected void SetAutomaticCartridgeChange() {
            if (AmmoState.Amount == 0) {
                AmmoState = Owner.GetAmmoState(Item.useAmmo);//更新一次弹药状态以保证换弹流畅
            }
            if (!IsKreload && kreloadTimeValue <= 0 && AutomaticCartridgeChangeDelayTime <= 0 && AmmoState.Amount > 0) {
                OnKreload = true;
                kreloadTimeValue = kreloadMaxTime;
            }
            if (AutomaticCartridgeChangeDelayTime > 0) {
                AutomaticCartridgeChangeDelayTime--;
            }
        }

        public sealed override void InOwner() {
            SetHeld();
            InitializeMagazine();
            PreInOwnerUpdate();
            ArmRotSengsFront = (60 + ArmRotSengsFrontNoFireOffset) * CWRUtils.atoR * SafeGravDir;
            ArmRotSengsBack = (110 + ArmRotSengsBackNoFireOffset) * CWRUtils.atoR * SafeGravDir;
            Projectile.Center = GetGunBodyPostion();
            Projectile.rotation = GetGunBodyRotation();
            Projectile.timeLeft = 2;
            if (GunShootCoolingValue > 0) {
                GunShootCoolingValue--;
            }
            if (!Owner.mouseInterface) {
                if (DownLeft) {
                    Owner.direction = ToMouse.X > 0 ? 1 : -1;
                    Projectile.rotation = GetGunInFireRot();
                    Projectile.Center = GetGunInFirePos();
                    ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 * SafeGravDir - Projectile.rotation) * DirSign * SafeGravDir;
                    if (IsKreload && Projectile.IsOwnedByLocalPlayer()) {//需要子弹，还需要判断是否已经装弹//HaveAmmo && 
                        onFire = true;
                    }
                    SetAutomaticCartridgeChange();
                }
                else {
                    onFire = false;
                }

                if (Owner.Calamity().mouseRight && !onFire && CanRightClick) {//Owner.PressKey()
                    Owner.direction = ToMouse.X > 0 ? 1 : -1;
                    Projectile.rotation = GunOnFireRot;
                    Projectile.Center = Owner.MountedCenter + Projectile.rotation.ToRotationVector2() * HandFireDistance + new Vector2(0, HandFireDistanceY) + OffsetPos;
                    ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 - Projectile.rotation) * DirSign;
                    if (IsKreload && Projectile.IsOwnedByLocalPlayer()) {//HaveAmmo && 
                        onFireR = true;
                    }
                    SetAutomaticCartridgeChange();
                }
                else {
                    onFireR = false;
                }

                if (WhetherStartChangingAmmunition()) {
                    AmmoState = Owner.GetAmmoState(Item.useAmmo);//在装填时更新弹药状态
                    if (AmmoState.Amount >= MinimumAmmoPerReload) {//只有弹药量大于最小弹药量时才可装填
                        OnKreload = true;
                        kreloadTimeValue = kreloadMaxTime;
                    }
                }
            }
            if (OnKreload) {//装弹过程
                if (PreOnKreloadEvent()) {
                    ArmRotSengsFront = (MathHelper.PiOver2 * SafeGravDir - (Projectile.rotation)) * DirSign * SafeGravDir + 0.3f;
                    ArmRotSengsFront += MathF.Sin(Time * 0.3f) * 0.7f * SafeGravDir;
                }
                kreloadTimeValue--;
                if (PreReloadEffects(kreloadTimeValue, kreloadMaxTime)) {
                    if (kreloadTimeValue == kreloadMaxTime - 1) {
                        KreloadSoundCaseEjection();
                    }
                    if (kreloadTimeValue == kreloadMaxTime / 2) {
                        KreloadSoundloadTheRounds();
                    }
                }
                if (kreloadTimeValue == kreloadMaxTime / 3) {
                    AmmoState = Owner.GetAmmoState(Item.useAmmo);//再更新一次弹药状态
                    if (PreConsumeAmmoEvent()) {
                        BulletReturn();
                        LoadBulletsIntoMagazine();//这一次更新弹匣内容，压入子弹
                        if (BulletConsumption) {
                            ExpendedAmmunition();
                        }
                    }
                }
                if (kreloadTimeValue <= 0) {//时间完成后设置装弹状态并准备下一次发射
                    CWRItems wRItems = ModItem;
                    OnKreload = false;
                    IsKreload = true;
                    if (Item.type != ItemID.None) {
                        wRItems.IsKreload = true;
                    }
                    kreloadTimeValue = 0;
                    if (KreLoadFulfill()) {
                        int value = AmmoState.Amount;
                        if (value > wRItems.AmmoCapacity) {
                            value = wRItems.AmmoCapacity;
                        }
                        if (LoadingQuantity > 0) {
                            value = LoadingQuantity;
                        }
                        BulletNum += value;
                        if (BulletNum > wRItems.AmmoCapacity) {
                            BulletNum = wRItems.AmmoCapacity;
                        }
                        if (wRItems.AmmoCapacityInFire) {
                            wRItems.AmmoCapacityInFire = false;
                        }
                    }
                }
            }
            if (Owner.PressKey()) {
                if (!IsKreload && LoadingReminder) {
                    if (!Owner.mouseInterface) {
                        AmmoState = Owner.GetAmmoState(Item.useAmmo);//更新一次弹药状态
                        if (AmmoState.Amount <= 0) {
                            HandleEmptyAmmoEjection();
                        }
                    }
                    LoadingReminder = false;
                }
            }
            else {
                LoadingReminder = true;
            }
            if (Item.type != ItemID.None) {
                IsKreload = ModItem.IsKreload;
            }
            PostInOwnerUpdate();
        }
        /// <summary>
        /// 该弹药物品是否应该判定为一个无限弹药
        /// </summary>
        /// <param name="ammoItem"></param>
        /// <returns></returns>
        public bool AmmunitionIsunlimited(Item ammoItem) {
            bool result = !ammoItem.consumable;
            if (CWRMod.Instance.luiafk != null || CWRMod.Instance.improveGame != null) {
                if (ammoItem.stack >= 3996) {
                    result = true;
                }
            }
            return result;
        }
        /// <summary>
        /// 退还弹匣内非空子弹
        /// </summary>
        public void BulletReturn() {
            if (ModItem.MagazineContents != null && ModItem.MagazineContents.Length > 0 && ReturnRemainingBullets) {
                foreach (Item i in ModItem.MagazineContents) {//在装弹之前返回玩家弹匣中剩余的弹药
                    if (i.stack > 0 && i.type != ItemID.None) {
                        if (i.CWR().AmmoProjectileReturn) {
                            Owner.QuickSpawnItem(Source, new Item(i.type), i.stack);
                        }
                    }
                }
            }
        }
        /// <summary>
        /// 向弹匣中装入子弹的函数，这个函数并不负责消耗逻辑
        /// </summary>
        public virtual void LoadBulletsIntoMagazine() {
            CWRItems cwrItem = ModItem;//获取模组物品实例
            List<Item> loadedItems = new List<Item>();
            int magazineCapacity = cwrItem.AmmoCapacity;
            if (LoadingQuantity > 0) {
                magazineCapacity = LoadingQuantity;
            }
            int accumulatedAmount = 0;
            
            foreach (Item ammoItem in AmmoState.InItemInds) {
                int stack = ammoItem.stack;

                if (stack > magazineCapacity - accumulatedAmount) {
                    stack = magazineCapacity - accumulatedAmount;
                }

                if (AmmunitionIsunlimited(ammoItem)) {//如果该物品不消耗，那么可能是一个无限弹药类型的物品，这里进行特别处理
                    if (CWRIDs.ItemToShootID.ContainsKey(ammoItem.type)) {
                        int newAmmoType = ammoItem.type;
                        if (CWRIDs.ItemToShootID[ammoItem.type] == ProjectileID.Bullet) {
                            newAmmoType = ItemID.MusketBall;
                        }
                        Item newAmmoItem = new Item(newAmmoType, magazineCapacity - accumulatedAmount);
                        newAmmoItem.CWR().AmmoProjectileReturn = false;//因为是无尽弹药类提供的弹药，所以不应该在之后的退弹中被返还
                        loadedItems.Add(newAmmoItem);
                        accumulatedAmount = magazineCapacity;
                        AmmoState.Amount = magazineCapacity;
                        break;
                    }
                }
                if (ammoItem.type > ItemID.None && stack > 0) {
                    loadedItems.Add(new Item(ammoItem.type, stack));
                }
                accumulatedAmount += stack;

                if (accumulatedAmount >= magazineCapacity) {
                    break;
                }
            }

            cwrItem.MagazineContents = loadedItems.ToArray();
        }
        /// <summary>
        /// 在压入弹药后执行，用于处理消耗逻辑
        /// </summary>
        public void ExpendedAmmunition() {
            int ammo = 0;
            int maxAmmo = ModItem.AmmoCapacity;
            if (LoadingQuantity > 0) {
                maxAmmo = LoadingQuantity;
            }
            foreach (Item inds in AmmoState.InItemInds) {
                if (ammo >= maxAmmo) {
                    break;
                }
                if (inds.stack <= 0) {
                    continue;
                }
                if (AmmunitionIsunlimited(inds)) {
                    break;
                }
                if (inds.stack >= maxAmmo) {
                    inds.stack -= maxAmmo - ammo;
                    ammo += maxAmmo;
                } else {
                    ammo += inds.stack;
                    inds.stack = 0;
                }
            }
        }
        /// <summary>
        /// 更新弹匣内容，该逻辑负责弹药的消耗，以及后续的弹药排列处理。如果多次调用，可以制造类似多发消耗的效果
        /// </summary>
        public virtual void UpdateMagazineContents() {
            if (!MagazineSystem) {//如果关闭了弹匣系统，将使用原版的弹药更新机制，因为弹匣不会再发挥作用
                UpdateConsumeAmmo();
                return;
            }
            CWRItems cwritem = ModItem;
            if (cwritem.MagazineContents.Length <= 0) {
                cwritem.MagazineContents = new Item[] { new Item() };
                IsKreload = false;
                BulletNum = 0;
            }
            if (cwritem.MagazineContents[0].stack <= 0) {
                cwritem.MagazineContents[0] = new Item();
                List<Item> items = new List<Item>();
                foreach (Item i in cwritem.MagazineContents) {
                    if (i.type != ItemID.None && i.stack > 0) {
                        items.Add(i);
                    }
                }
                cwritem.MagazineContents = items.ToArray();
            }
            if (cwritem.MagazineContents.Length <= 0) {
                IsKreload = false;
                BulletNum = 0;
                return;
            }
            if (cwritem.MagazineContents[0] == null) {
                cwritem.MagazineContents[0] = new Item();
            }
            cwritem.MagazineContents[0].stack--;
            if (BulletNum > 0) {
                BulletNum--;
            }
        }
        /// <summary>
        /// 空弹时试图开火会发生的事情
        /// </summary>
        public virtual void HandleEmptyAmmoEjection() {
            SoundEngine.PlaySound(CWRSound.Ejection, Projectile.Center);
            CombatText.NewText(Owner.Hitbox, Color.Gold, CWRLocText.GetTextValue("CaseEjection_TextContent"));
        }
        /// <summary>
        /// 在单次开火时运行，优先于<see cref="FiringShoot"/>运行，返回<see langword="false"/>禁用<see cref="FiringShoot"/>的运行
        /// </summary>
        /// <returns></returns>
        public virtual bool PreFiringShoot() {
            return true;
        }
        /// <summary>
        /// 在单次开火时运行，在<see cref="FiringShoot"/>运行后运行，无论<see cref="PreFiringShoot"/>返回什么都会运行
        /// </summary>
        /// <returns></returns>
        public virtual void PostFiringShoot() {
        }
        /// <summary>
        /// 左键单次开火事件
        /// </summary>
        public override void FiringShoot() {
            SpawnGunFireDust(GunShootPos, ShootVelocity);
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
        /// <summary>
        /// 右键单次开火事件
        /// </summary>
        public override void FiringShootR() {
            SpawnGunFireDust(GunShootPos, ShootVelocity);
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
        /// <summary>
        /// 在开火后执行默认的装弹处理逻辑之前执行，返回<see langword="false"/>禁止对
        /// <see cref="LoadingReminder"/>和<see cref="IsKreload"/>以及<see cref="CWRItems.IsKreload"/>的自动处理
        /// </summary>
        /// <returns></returns>
        public virtual bool PreFireReloadKreLoad() {
            return true;
        }

        public bool GetMagazineCanUseAmmoProbability() {
            bool result;
            result = Owner.CanUseAmmoInWeaponShoot(Item);
            return result;
        }

        public sealed override void SpanProj() {
            if ((onFire || onFireR) && GunShootCoolingValue <= 0 && kreloadTimeValue <= 0) {
                if (Owner.Calamity().luxorsGift || ModOwner.TheRelicLuxor > 0) {
                    LuxirEvent();
                }
                //弹容替换在此处执行，将发射内容设置为弹匣第一位的弹药类型
                if (AmmoTypeAffectedByMagazine && MagazineSystem && ModItem.MagazineContents.Length > 0) {
                    if (ModItem.MagazineContents[0] == null) {
                        ModItem.MagazineContents[0] = new Item();
                    }
                    AmmoTypes = ModItem.MagazineContents[0].shoot;
                    if (AmmoTypes == 0) {
                        AmmoTypes = ProjectileID.Bullet;
                    }
                }
                if (BulletNum > 0) {
                    if (PreFiringShoot()) {
                        if (onFire) {
                            FiringShoot();
                        }
                        if (onFireR) {
                            FiringShootR();
                        }
                        if (CGItemBehavior) {
                            CWRMod.CalamityGlobalItemInstance.Shoot(Item, Owner, Source2, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback);
                        }
                        if (EnableRecoilRetroEffect) {
                            OffsetPos -= ShootVelocity.UnitVector() * RecoilRetroForceMagnitude;
                        }
                        if (FiringDefaultSound) {
                            SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
                        }
                        if (fireLight > 0) {
                            Lighting.AddLight(GunShootPos, CWRUtils.MultiStepColorLerp(Main.rand.NextFloat(0.3f, 0.65f), Color.Red, Color.Gold).ToVector3() * Main.rand.NextFloat(0.1f, fireLight));
                        }
                        if (CanCreateRecoilBool) {
                            CreateRecoil();
                        }
                    }
                    PostFiringShoot();
                }
                if (CanUpdateMagazineContentsInShootBool) {
                    //如果关闭了弹匣系统，他将会必定调用一次UpdateMagazineContents
                    if (GetMagazineCanUseAmmoProbability() || MustConsumeAmmunition || !MagazineSystem) {
                        UpdateMagazineContents();
                    }
                }
                if (PreFireReloadKreLoad()) {
                    if (BulletNum <= 0) {
                        SetEmptyMagazine();
                        AutomaticCartridgeChangeDelayTime += FireTime;
                    }
                }

                GunShootCoolingValue += FireTime + 1;
                onFire = false;
            }
        }
        /// <summary>
        /// 设置弹匣打空的后续状态，但如果想完整的将枪械设置为空弹，还需要设置<see cref="CWRItems.MagazineContents"/>的内容
        /// </summary>
        public void SetEmptyMagazine() {
            LoadingReminder = false;//在发射后设置一下装弹提醒开关，防止进行一次有效射击后仍旧弹出提示
            IsKreload = false;
            if (Item.type != ItemID.None) {
                ModItem.IsKreload = false;
            }
            BulletNum = 0;
        }

        #region Utils
        /// <summary>
        /// 截取弹匣的内容，并将弹匣更新为指定的截取内容
        /// </summary>
        /// <param name="cutOutNum"></param>
        public void CutOutMagazine(int cutOutNum) {
            int cumulativeQuantity = 0;
            List<Item> list = new List<Item>();
            foreach (Item i in ModItem.MagazineContents) {
                if (cumulativeQuantity >= cutOutNum || i == null) {
                    break;
                }
                if (i.type == ItemID.None || i.stack <= 0) {
                    continue;
                }
                int stack = i.stack;
                if (stack > cutOutNum - cumulativeQuantity) {
                    stack = cutOutNum - cumulativeQuantity;
                }
                Item ammo = new Item(i.type, stack);
                cumulativeQuantity += stack;
                list.Add(ammo);
            }
            ModItem.MagazineContents = list.ToArray();
        }
        /// <summary>
        /// 一个通用的装弹动作逻辑，一般在<see cref="PostInOwnerUpdate"/>中调用
        /// </summary>
        /// <param name="rot"></param>
        /// <param name="xl"></param>
        /// <param name="yl"></param>
        public void LoadingAnimation(int rot, int xl, int yl) {
            if (kreloadTimeValue > 0) {//设置一个特殊的装弹动作，调整转动角度和中心点，让枪身看起来上抬
                Owner.direction = ToMouse.X > 0 ? 1 : -1;//为了防止抽搐，这里额外设置一次玩家朝向
                FeederOffsetRot = -rot;
                FeederOffsetPos = new Vector2(DirSign * -xl, -yl) * SafeGravDir;
            }
        }
        /// <summary>
        /// 安全获取选定的弹匣弹药内容
        /// </summary>
        /// <returns></returns>
        public Item GetSelectedBullets() {
            if (ModItem.MagazineContents == null) {
                return new Item();
            }
            if (ModItem.MagazineContents.Length <= 0) {
                return new Item();
            }
            if (ModItem.MagazineContents[0] == null) {
                return new Item();
            }
            return ModItem.MagazineContents[0];
        }

        #endregion
    }
}
