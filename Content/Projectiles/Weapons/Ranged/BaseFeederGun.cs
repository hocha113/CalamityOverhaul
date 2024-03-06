using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Microsoft.Xna.Framework;
using System;
using Terraria.Audio;
using Terraria.ModLoader;
using Terraria;
using Terraria.ID;
using CalamityMod;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal abstract class BaseFeederGun : BaseGun
    {
        /// <summary>
        /// 装弹提醒，一般来讲会依赖左键的按键事件进行更新
        /// </summary>
        protected bool loadingReminder;
        /// <summary>
        /// 是否开始装弹
        /// </summary>
        protected bool onKreload;
        /// <summary>
        /// 是否已经装好了弹药
        /// </summary>
        protected bool isKreload;
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
        protected int fireTime = 10;
        /// <summary>
        /// 是否可以重复换弹
        /// </summary>
        protected bool RepeatedCartridgeChange;
        /// <summary>
        /// 一个额外的枪体旋转角度矫正值，默认在<see cref="Recover"/>中恢复为0
        /// </summary>
        protected float FeederOffsetRot;
        /// <summary>
        /// 一个额外的枪体中心位置矫正向量，默认在<see cref="Recover"/>中恢复为<see cref="Vector2.Zero"/>，
        /// </summary>
        protected Vector2 FeederOffsetPos;

        protected int BulletNum {
            get => heldItem.CWR().NumberBullets;
            set => heldItem.CWR().NumberBullets = value;
        }

        protected SoundStyle loadTheRounds = CWRSound.CaseEjection2;

        public override void SetRangedProperty() {
            base.SetRangedProperty();
            kreloadMaxTime = heldItem.useTime;
        }
        /// <summary>
        /// 抛壳的简易实现
        /// </summary>
        public virtual void EjectionCase() {
            Vector2 vr = (Projectile.rotation - Main.rand.NextFloat(-0.1f, 0.1f) * DirSign).ToRotationVector2() * -Main.rand.NextFloat(3, 7) + Owner.velocity;
            Projectile.NewProjectile(Projectile.parent(), Projectile.Center, vr, ModContent.ProjectileType<GunCasing>(), 10, Projectile.knockBack, Owner.whoAmI);
        }
        /// <summary>
        /// 关于装弹过程中的具体效果实现，返回<see langword="false"/>禁用默认的效果行为
        /// </summary>
        public virtual bool PreKreloadSoundEffcet(int time, int maxItem) {
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
            EjectionCase();
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
            BulletNum = heldItem.CWR().AmmoCapacity;
            if (heldItem.CWR().AmmoCapacityInFire) {
                heldItem.CWR().AmmoCapacityInFire = false;
            }
        }

        public virtual bool WhetherStartChangingAmmunition() {
            return Owner.PressKey(false) && kreloadTimeValue == 0 
                && (!isKreload || RepeatedCartridgeChange) 
                && BulletNum < heldItem.CWR().AmmoCapacity 
                && !onFire && HaveAmmo && heldItem.CWR().NoKreLoadTime == 0;
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
            return kreloadTimeValue == 0 ? (Owner.Center + Projectile.rotation.ToRotationVector2() * (HandFireDistance + 5) + new Vector2(0, HandFireDistanceY)) : GetGunBodyPostion();
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

        public override void InOwner() {
            PreInOwnerUpdate();
            ArmRotSengsFront = 30 * CWRUtils.atoR;
            ArmRotSengsBack = 150 * CWRUtils.atoR;
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
                    if (HaveAmmo && isKreload) {//需要子弹，还需要判断是否已经装弹
                        onFire = true;
                        Projectile.ai[1]++;
                    }
                }
                else {
                    onFire = false;
                }

                if (WhetherStartChangingAmmunition()) {
                    onKreload = true;
                    kreloadTimeValue = kreloadMaxTime;
                }

                if (onKreload) {//装弹过程
                    if (PreOnKreloadEvent()) {
                        ArmRotSengsFront = (MathHelper.PiOver2 - (Projectile.rotation)) * DirSign + 0.3f;
                        ArmRotSengsFront += MathF.Sin(Time * 0.3f) * 0.7f;
                    }
                    kreloadTimeValue--;
                    if (PreKreloadSoundEffcet(kreloadTimeValue, kreloadMaxTime)) {
                        if (kreloadTimeValue == kreloadMaxTime - 1) {
                            KreloadSoundCaseEjection();
                        }
                        if (kreloadTimeValue == kreloadMaxTime / 2) {
                            KreloadSoundloadTheRounds();
                        }
                        if (kreloadTimeValue == kreloadMaxTime / 3) {
                            if (PreConsumeAmmoEvent()) {
                                for (int i = 0; i < heldItem.CWR().AmmoCapacity; i++) {
                                    UpdateConsumeAmmo();
                                }
                            }
                        }
                    }
                    if (kreloadTimeValue <= 0) {//时间完成后设置装弹状态并准备下一次发射
                        onKreload = false;
                        isKreload = true;
                        if (heldItem.type != ItemID.None) {
                            heldItem.CWR().IsKreload = true;
                        }
                        OnKreLoad();
                        kreloadTimeValue = 0;
                    }
                }

                if (Owner.PressKey()) {
                    if (!isKreload && loadingReminder) {
                        SoundEngine.PlaySound(CWRSound.Ejection, Projectile.Center);
                        CombatText.NewText(Owner.Hitbox, Color.Gold, CWRLocText.GetTextValue("CaseEjection_TextContent"));
                        loadingReminder = false;
                    }
                }
                else {
                    loadingReminder = true;
                }
            }

            if (heldItem.type != ItemID.None)
                isKreload = heldItem.CWR().IsKreload;
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
        /// 单次开火事件
        /// </summary>
        public override void FiringShoot() {
            SpawnGunFireDust(GunShootPos, ShootVelocity);
            Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
        /// <summary>
        /// 在开火后执行默认的装弹处理逻辑之前执行，返回<see langword="false"/>禁止对
        /// <see cref="loadingReminder"/>和<see cref="isKreload"/>以及<see cref="CWRItems.IsKreload"/>的自动处理
        /// </summary>
        /// <returns></returns>
        public virtual bool PreFireReloadKreLoad() {
            return true;
        }

        public override void SpanProj() {
            if (onFire && Projectile.ai[1] > fireTime && kreloadTimeValue <= 0) {
                if (Owner.Calamity().luxorsGift || Owner.CWR().TheRelicLuxor > 0) {
                    LuxirEvent();
                }
                if (PreFiringShoot()) {
                    FiringShoot();
                    if (FiringDefaultSound) {
                        SoundEngine.PlaySound(heldItem.UseSound, Projectile.Center);
                    }
                }
                PostFiringShoot();
                CreateRecoil();
                if (PreFireReloadKreLoad()) {
                    loadingReminder = false;//在发射后设置一下装弹提醒开关，防止进行一次有效射击后仍旧弹出提示
                    isKreload = false;
                    if (heldItem.type != ItemID.None) {
                        heldItem.CWR().IsKreload = false;
                    }
                }
                Projectile.ai[1] = 0;
                onFire = false;
            }
        }
    }
}
