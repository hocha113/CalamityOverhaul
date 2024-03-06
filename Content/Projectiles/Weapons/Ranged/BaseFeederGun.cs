using CalamityMod;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using static Humanizer.In;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    /// <summary>
    /// 比<see cref="BaseGun"/>更为复杂的枪基类，用于更加快速且模板化的实现关于弹匣系统的联动
    /// </summary>
    internal abstract class BaseFeederGun : BaseGun
    {
        /// <summary>
        /// 子弹状态，对应枪械的弹匣内容
        /// </summary>
        internal AmmoState AmmoState;
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
        /// 单次弹药装填最小数量，默认为一发
        /// </summary>
        protected int MinimumAmmoPerReload = 1;
        /// <summary>
        /// 装弹计时器
        /// </summary>
        protected int kreloadTimeValue;
        /// <summary>
        /// 装弹所需要的时间，默认为手持物品对象的<see cref="Item.useTime"/>
        /// </summary>
        protected int kreloadMaxTime = 60;
        /// <summary>
        /// 开火间隔，默认为10
        /// </summary>
        protected int FireTime = 10;
        /// <summary>
        /// 是否可以重复换弹
        /// </summary>
        protected bool RepeatedCartridgeChange;
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

        protected int BulletNum {
            get => Item.CWR().NumberBullets;
            set => Item.CWR().NumberBullets = value;
        }

        protected SoundStyle loadTheRounds = CWRSound.CaseEjection2;

        public override void SetRangedProperty() {
            base.SetRangedProperty();
            kreloadMaxTime = Item.useTime;
        }
        /// <summary>
        /// 抛壳的简易实现
        /// </summary>
        public virtual void EjectCasing() {
            Vector2 vr = (Projectile.rotation - Main.rand.NextFloat(-0.1f, 0.1f) * DirSign).ToRotationVector2() * -Main.rand.NextFloat(3, 7) + Owner.velocity;
            Projectile.NewProjectile(Projectile.parent(), Projectile.Center, vr, ModContent.ProjectileType<GunCasing>(), 10, Projectile.knockBack, Owner.whoAmI);
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
            SoundEngine.PlaySound(CWRSound.CaseEjection with { Volume = 0.6f }, Projectile.Center);
        }
        /// <summary>
        /// 关于装弹过程中的第二部分音效的执行
        /// </summary>
        public virtual void KreloadSoundloadTheRounds() {
            SoundEngine.PlaySound(loadTheRounds, Projectile.Center);
            EjectCasing();
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
        /// 装弹完成后会执行一次该方法
        /// </summary>
        public virtual void OnKreLoad() {
            BulletNum = Item.CWR().AmmoCapacity;
            if (Item.CWR().AmmoCapacityInFire) {
                Item.CWR().AmmoCapacityInFire = false;
            }
        }
        /// <summary>
        /// 是否可以进行换弹操作，返回<see langword="false"/>阻止玩家进行换弹操作
        /// </summary>
        /// <returns></returns>
        public virtual bool WhetherStartChangingAmmunition() {
            return CWRKeySystem.KreLoad_Key.JustPressed && kreloadTimeValue == 0 
                && (!IsKreload || RepeatedCartridgeChange) 
                && BulletNum < Item.CWR().AmmoCapacity 
                && !onFire && HaveAmmo && Item.CWR().NoKreLoadTime == 0;
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
            return kreloadTimeValue == 0 ? Owner.Center + Projectile.rotation.ToRotationVector2() * (HandFireDistance + 5) + new Vector2(0, HandFireDistanceY) + OffsetPos : GetGunBodyPostion();
        }
        /// <summary>
        /// 统一获取枪体在静置时的旋转角，返回值默认在<see cref="InOwner"/>中被获取设置于Projectile.Center
        /// </summary>
        /// <returns></returns>
        public virtual float GetGunBodyRotation() {
            return (DirSign > 0 ? MathHelper.ToRadians(10) : MathHelper.ToRadians(170)) + FeederOffsetRot;
        }
        /// <summary>
        /// 统一获取枪体在静置时的中心位置，返回值默认在<see cref="InOwner"/>中被获取设置于Projectile.rotation
        /// </summary>
        /// <returns></returns>
        public virtual Vector2 GetGunBodyPostion() {
            return Owner.Center + new Vector2(DirSign * HandDistance, HandDistanceY) + FeederOffsetPos;
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

        public sealed override void InOwner() {
            if (Item.CWR().MagazineContents == null) {
                AmmoState = Owner.GetAmmoState(Item.useAmmo);//再更新一次弹药状态
                LoadBulletsIntoMagazine();//这一次更新弹匣内容，压入子弹
            }
            PreInOwnerUpdate();
            ArmRotSengsFront = (60 + ArmRotSengsFrontNoFireOffset) * CWRUtils.atoR;
            ArmRotSengsBack = (110 + ArmRotSengsBackNoFireOffset) * CWRUtils.atoR;
            Projectile.Center = GetGunBodyPostion();
            Projectile.rotation = GetGunBodyRotation();
            Projectile.timeLeft = 2;
            SetHeld();

            if (!Owner.mouseInterface) {
                if (Owner.PressKey()) {
                    Owner.direction = ToMouse.X > 0 ? 1 : -1;
                    Projectile.rotation = GetGunInFireRot();
                    Projectile.Center = GetGunInFirePos();
                    ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 - (Projectile.rotation)) * DirSign;
                    if (HaveAmmo && IsKreload && Projectile.IsOwnedByLocalPlayer()) {//需要子弹，还需要判断是否已经装弹
                        onFire = true;
                        Projectile.ai[1]++;
                    }
                }
                else {
                    onFire = false;
                }

                if (Owner.Calamity().mouseRight && !onFire && CanRightClick) {//Owner.PressKey()
                    Owner.direction = ToMouse.X > 0 ? 1 : -1;
                    Projectile.rotation = GunOnFireRot;
                    Projectile.Center = Owner.MountedCenter + Projectile.rotation.ToRotationVector2() * HandFireDistance + new Vector2(0, HandFireDistanceY) + OffsetPos;
                    ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 - Projectile.rotation) * DirSign;
                    if (HaveAmmo && IsKreload && Projectile.IsOwnedByLocalPlayer()) {
                        onFireR = true;
                        Projectile.ai[1]++;
                    }
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

                if (OnKreload) {//装弹过程
                    if (PreOnKreloadEvent()) {
                        ArmRotSengsFront = (MathHelper.PiOver2 - (Projectile.rotation)) * DirSign + 0.3f;
                        ArmRotSengsFront += MathF.Sin(Time * 0.3f) * 0.7f;
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
                        LoadBulletsIntoMagazine();//这一次更新弹匣内容，压入子弹
                        if (PreConsumeAmmoEvent()) {
                            for (int i = 0; i < Item.CWR().AmmoCapacity; i++) {
                                UpdateConsumeAmmo();
                            }
                        }
                    }
                    if (kreloadTimeValue <= 0) {//时间完成后设置装弹状态并准备下一次发射
                        OnKreload = false;
                        IsKreload = true;
                        if (Item.type != ItemID.None) {
                            Item.CWR().IsKreload = true;
                        }
                        kreloadTimeValue = 0;
                        OnKreLoad();
                    }
                }

                if (Owner.PressKey()) {
                    if (!IsKreload && LoadingReminder) {
                        HandleEmptyAmmoEjection();
                        LoadingReminder = false;
                    }
                }
                else {
                    LoadingReminder = true;
                }
            }

            if (Item.type != ItemID.None)
                IsKreload = Item.CWR().IsKreload;

            PostInOwnerUpdate();
        }
        /// <summary>
        /// 向弹匣中装入子弹的函数
        /// </summary>
        public virtual void LoadBulletsIntoMagazine() {
            List<Item> loadedItems = new List<Item>();
            int magazineCapacity = Item.CWR().AmmoCapacity;
            int accumulatedAmount = 0;

            foreach (Item ammoItem in AmmoState.InItemInds) {
                int stack = ammoItem.stack;

                if (stack > magazineCapacity - accumulatedAmount) {
                    stack = magazineCapacity - accumulatedAmount;
                }

                loadedItems.Add(new Item(ammoItem.type, stack));
                accumulatedAmount += stack;

                if (accumulatedAmount >= magazineCapacity) {
                    break;
                }
            }

            Item.CWR().MagazineContents = loadedItems.ToArray();
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
            if (BulletNum > 0) {
                BulletNum--;
            }
        }
        /// <summary>
        /// 左键单次开火事件
        /// </summary>
        public override void FiringShoot() {
            SpawnGunFireDust(GunShootPos, ShootVelocity);
            Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
        /// <summary>
        /// 右键单次开火事件
        /// </summary>
        public override void FiringShootR() {
            SpawnGunFireDust(GunShootPos, ShootVelocity);
            Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
        /// <summary>
        /// 在开火后执行默认的装弹处理逻辑之前执行，返回<see langword="false"/>禁止对
        /// <see cref="LoadingReminder"/>和<see cref="IsKreload"/>以及<see cref="CWRItems.IsKreload"/>的自动处理
        /// </summary>
        /// <returns></returns>
        public virtual bool PreFireReloadKreLoad() {
            return true;
        }

        public virtual void UpdateMagazineContents() {
            if (AmmoTypeAffectedByMagazine) {
                CWRItems cwritem = Item.CWR();
                if (cwritem.MagazineContents[0] == null) {
                    cwritem.MagazineContents[0] = new Item();
                }
                if (cwritem.MagazineContents[0].stack <= 0) {
                    cwritem.MagazineContents[0].TurnToAir();
                    // 使用循环逐个赋值，避免使用 Array.Copy 导致的重复元素问题
                    for (int i = 1; i < cwritem.MagazineContents.Length; i++) {
                        cwritem.MagazineContents[i - 1] = cwritem.MagazineContents[i];
                    }
                    // 最后一个元素设为新的 Item
                    cwritem.MagazineContents[cwritem.MagazineContents.Length - 1] = new Item();
                }
                AmmoTypes = cwritem.MagazineContents[0].shoot;
                cwritem.MagazineContents[0].stack--;
            }
        }

        public override void SpanProj() {
            if ((onFire || onFireR) && Projectile.ai[1] > FireTime && kreloadTimeValue <= 0) {
                if (Owner.Calamity().luxorsGift || Owner.CWR().TheRelicLuxor > 0) {
                    LuxirEvent();
                }
                UpdateMagazineContents();
                if (PreFiringShoot()) {
                    if (onFire) {
                        FiringShoot();
                    }
                    if (onFireR) {
                        FiringShootR();
                    }
                    if (FiringDefaultSound) {
                        SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
                    }
                }
                PostFiringShoot();
                CreateRecoil();
                if (PreFireReloadKreLoad()) {
                    if (BulletNum <= 0) {
                        LoadingReminder = false;//在发射后设置一下装弹提醒开关，防止进行一次有效射击后仍旧弹出提示
                        IsKreload = false;
                        if (Item.type != ItemID.None) {
                            Item.CWR().IsKreload = false;
                        }
                        BulletNum = 0;
                    }
                }
                
                Projectile.ai[1] = 0;
                onFire = false;
            }
        }
    }
}
