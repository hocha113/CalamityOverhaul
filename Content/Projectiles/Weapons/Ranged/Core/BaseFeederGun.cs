using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.OtherMods.ImproveGame;
using CalamityOverhaul.Content.UIs;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core
{
    /// <summary>
    /// 比<see cref="BaseGun"/>更为复杂的枪基类，用于更加快速且模板化的实现关于弹匣系统的联动
    /// </summary>
    internal abstract class BaseFeederGun : BaseGun
    {
        #region Data
        /// <summary>
        /// 子弹状态，对应枪械的弹匣内容
        /// </summary>
        internal AmmoState AmmoState;
        /// <summary>
        /// 换弹是否消耗弹药
        /// </summary>
        internal bool BulletConsumption = true;
        /// <summary>
        /// 是否自动在一次单次射击后调用弹匣更新函数，这负责弹药消耗逻辑
        /// ，如果设置为<see langword="false"/>就需要手动调用<see cref="UpdateMagazineContents"/>以正常执行弹药逻辑
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
        /// 单次弹药装填最大数量，默认为0，即不启用
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
        /// 装弹时间的内部基本值
        /// </summary>
        protected int _kreloadMaxTime = 60;
        /// <summary>
        /// 装弹所需要的时间，默认为60
        /// </summary>
        protected int kreloadMaxTime {
            get => _kreloadMaxTime + extraKreloadMaxTime;
            set => _kreloadMaxTime = value;
        }
        /// <summary>
        /// 额外装弹时间，默认为0，完成一次装弹后自动回归于0
        /// </summary>
        protected int extraKreloadMaxTime = 0;
        /// <summary>
        /// 开火间隔，默认为10
        /// </summary>
        protected int FireTime = 10;
        /// <summary>
        /// 是否可以重复换弹，默认为<see cref="true"/>
        /// </summary>
        protected bool RepeatedCartridgeChange = true;
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
        /// <summary>
        /// 关于霰弹枪开火是否打断装填动作
        /// </summary>
        protected bool _inShotgun_FireForcedReloadInterruption;
        /// <summary>
        /// 关于霰弹枪开火是否打断装填动作
        /// </summary>
        public bool InShotgun_FireForcedReloadInterruption {
            get {
                return CWRServerConfig.Instance.ShotgunFireForcedReloadInterruption ? true : _inShotgun_FireForcedReloadInterruption;
            }
            set => _inShotgun_FireForcedReloadInterruption = value;
        }

        public override bool OnHandheldDisplayBool {
            get {
                bool reset = WeaponHandheldDisplay;
                if (!HandheldDisplay) {
                    reset = false;
                }
                return reset ? true : CanFire || kreloadTimeValue > 0;
            }
        }

        public enum LoadingAmmoAnimationEnum
        {
            None,
            Shotgun,
            Handgun,
            Revolver,
        }

        public LoadingAmmoAnimationEnum LoadingAmmoAnimation = LoadingAmmoAnimationEnum.None;

        public struct LoadingAA_None_Struct
        {
            /// <summary>
            /// 默认为50
            /// </summary>
            public int loadingAA_None_Roting = 50;
            /// <summary>
            /// 默认为3
            /// </summary>
            public int loadingAA_None_X = 3;
            /// <summary>
            /// 默认为25
            /// </summary>
            public int loadingAA_None_Y = 25;

            public LoadingAA_None_Struct() { }
        }

        /// <summary>
        /// 是否启用默认的换弹行为，默认为<see langword="true"/>
        /// </summary>
        public bool NO_EEMONG_LOADINGNONESET = true;
        public LoadingAA_None_Struct LoadingAA_None = new LoadingAA_None_Struct();

        public struct LoadingAA_Shotgun_Struct
        {
            public SoundStyle loadShellSound;
            public SoundStyle pump;
            /// <summary>
            /// 默认为15
            /// </summary>
            public int pumpCoolingValue;
            /// <summary>
            /// 默认为30
            /// </summary>
            public int loadingAmmoStarg_rot;
            /// <summary>
            /// 默认为0
            /// </summary>
            public int loadingAmmoStarg_x;
            /// <summary>
            /// 默认为13
            /// </summary>
            public int loadingAmmoStarg_y;
        }

        public LoadingAA_Shotgun_Struct LoadingAA_Shotgun = new LoadingAA_Shotgun_Struct() {
            loadShellSound = CWRSound.Gun_Shotgun_LoadShell with { Volume = 0.75f },
            pump = CWRSound.Gun_Shotgun_Pump with { Volume = 0.6f },
            pumpCoolingValue = 15,
            loadingAmmoStarg_rot = 30,
            loadingAmmoStarg_x = 0,
            loadingAmmoStarg_y = 13,
        };

        public struct LoadingAA_Handgun_Struct
        {
            public SoundStyle clipOut;
            public SoundStyle clipLocked;
            public SoundStyle slideInShoot;
            public float level1;
            public float level2;
            public float level3;
            /// <summary>
            /// 默认为-20
            /// </summary>
            public int feederOffsetRot;
            /// <summary>
            /// 默认为6
            /// </summary>
            public int loadingAmmoStarg_x;
            /// <summary>
            /// 默认为-6
            /// </summary>
            public int loadingAmmoStarg_y;
        }

        public LoadingAA_Handgun_Struct LoadingAA_Handgun = new LoadingAA_Handgun_Struct() {
            clipOut = CWRSound.Gun_HandGun_ClipOut with { Volume = 0.65f },
            clipLocked = CWRSound.Gun_HandGun_ClipLocked with { Volume = 0.65f },
            slideInShoot = CWRSound.Gun_HandGun_SlideInShoot with { Volume = 0.65f },
            level1 = 50 / 60f,
            level2 = 40 / 60f,
            level3 = 10 / 60f,
            feederOffsetRot = -20,
            loadingAmmoStarg_x = 6,
            loadingAmmoStarg_y = -6,
        };

        public struct LoadingAA_Revolver_Struct
        {
            public float Rotationratio;
            public SoundStyle Sound;
            public int ArmRotSengsFrontOffsetRotOver;
            public float ArmRotSengsFrontStartRotOver;
            public int loadingAmmoStarg_x;
            public int loadingAmmoStarg_y;
        }

        public LoadingAA_Revolver_Struct LoadingAA_Revolver = new LoadingAA_Revolver_Struct() {
            Rotationratio = 30,
            Sound = CWRSound.CaseEjection,
            ArmRotSengsFrontOffsetRotOver = 30,
            ArmRotSengsFrontStartRotOver = 0.3f,
            loadingAmmoStarg_x = 3,
            loadingAmmoStarg_y = 5,
        };

        protected int BulletNum {
            get => ModItem.NumberBullets;
            set => ModItem.NumberBullets = value;
        }

        protected SoundStyle caseEjections = CWRSound.CaseEjection;
        protected SoundStyle loadTheRounds = CWRSound.CaseEjection2;

        private bool old_ManualReloadDown;
        /// <summary>
        /// 玩家是否准备进行手动换弹，目前为止，只应该在手动换弹的逻辑中使用，考虑到网络优化，
        /// 此值为惰性更新，使用时需要谨慎以保证多人模式正常运行
        /// </summary>
        protected bool ManualReloadStart { get; private set; }

        protected float loadingAA_VolumeValue => CWRServerConfig.Instance.LoadingAA_Volume;

        #endregion

        public bool AmmunitionIsBeingLoaded() => kreloadTimeValue > 0;

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
            SoundEngine.PlaySound(caseEjections with { Volume = 0.5f * loadingAA_VolumeValue, PitchRange = (-0.05f, 0.05f) }, Projectile.Center);
        }
        /// <summary>
        /// 关于装弹过程中的第二部分音效的执行
        /// </summary>
        public virtual void KreloadSoundloadTheRounds() {
            SoundEngine.PlaySound(loadTheRounds with { Volume = 0.65f * loadingAA_VolumeValue, PitchRange = (-0.1f, 0) }, Projectile.Center);
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
            if (!MagazineSystem || CWRUtils.isServer) {//如果关闭了弹匣系统，枪械将不再可以换弹，因为弹匣不会再发挥作用
                return false;
            }

            bool canReload = kreloadTimeValue == 0
                && (!IsKreload || RepeatedCartridgeChange)
                && BulletNum < ModItem.AmmoCapacity
                && !onFire && HaveAmmo && ModItem.NoKreLoadTime == 0;

            if (Projectile.IsOwnedByLocalPlayer() && canReload) {
                ManualReloadStart = CWRKeySystem.KreLoad_Key.JustPressed;
                if (ManualReloadStart != old_ManualReloadDown) {
                    NetUpdate();
                }
                old_ManualReloadDown = ManualReloadStart;
            }

            if (!canReload) {
                ManualReloadStart = false;
            }

            return ManualReloadStart;
        }

        public override void Recover() {
            FeederOffsetRot = 0;
            FeederOffsetPos = Vector2.Zero;
        }

        public override float GetGunInFireRot() {
            return kreloadTimeValue == 0 ? GunOnFireRot : GetGunBodyRot();
        }

        public override Vector2 GetGunInFirePos() {
            return kreloadTimeValue == 0 ? Owner.GetPlayerStabilityCenter() + Projectile.rotation.ToRotationVector2()
                * (HandFireDistance + 5) + new Vector2(0, HandFireDistanceY * Math.Sign(Owner.gravDir)) + OffsetPos : GetGunBodyPos();
        }

        public override float GetGunBodyRot() {
            int art = 10;
            if (SafeGravDir < 0) {
                art = 350;
            }
            int value = (int)(art + FeederOffsetRot);
            return Owner.direction > 0 ? MathHelper.ToRadians(value) : MathHelper.ToRadians(180 - value);
        }

        public override Vector2 GetGunBodyPos() {
            Vector2 handOffset = new Vector2(Owner.direction * HandDistance, HandDistanceY * SafeGravDir);
            return Owner.GetPlayerStabilityCenter() + FeederOffsetPos + handOffset;
        }

        /// <summary>
        /// 初始化弹匣状态
        /// </summary>
        protected void InitializeMagazine() {
            if (ModItem.MagazineContents == null) {
                AmmoState = Owner.GetAmmoState(Item.useAmmo);//再更新一次弹药状态
                ModItem.MagazineContents = new Item[ModItem.AmmoCapacity];
                for (int i = 0; i < ModItem.MagazineContents.Length; i++) {
                    ModItem.MagazineContents[i] = new Item();
                }
            }
            if (Item.type != ItemID.None) {
                IsKreload = ModItem.IsKreload;
                if (!MagazineSystem) {
                    IsKreload = true;
                }
            }
        }
        /// <summary>
        /// 设置自动换弹，在一定条件下让玩家开始换弹，这个方法一般不需要手动调用，枪械会在合适的时候自行调用该逻辑
        /// </summary>
        protected void SetAutomaticCartridgeChange(bool ignoreKreLoad = false) {
            if (AmmoState.Amount == 0) {
                AmmoState = Owner.GetAmmoState(Item.useAmmo);//更新一次弹药状态以保证换弹流畅
            }

            if ((!IsKreload || ignoreKreLoad) && kreloadTimeValue <= 0 && AutomaticCartridgeChangeDelayTime <= 0
                && AmmoState.Amount > 0 && !ModOwner.NoCanAutomaticCartridgeChange
                && ModItem.NoKreLoadTime == 0 && !CartridgeHolderUI.Instance.OnMainP) {
                OnKreload = true;
                kreloadTimeValue = kreloadMaxTime;
            }

            if (AutomaticCartridgeChangeDelayTime > 0) {
                AutomaticCartridgeChangeDelayTime--;
            }
        }

        public override void PostSetRangedProperty() {
            Get_LoadingAmmoAnimation_PostSetRangedProperty();
        }

        //把动作的预处理集中基类里面，虽然会让这里的一切变得更加的臃肿，但至少方便了子类的实现和自定义化
        #region Get_LoadingAmmoAnimation

        private void Get_LoadingAmmoAnimation_PostSetRangedProperty() {
            if (LoadingAmmoAnimation == LoadingAmmoAnimationEnum.Shotgun) {
                LoadingQuantity = 1;
            }
            else if (LoadingAmmoAnimation == LoadingAmmoAnimationEnum.Revolver) {
                caseEjections = LoadingAA_Revolver.Sound;
            }
        }

        private void Get_LoadingAmmoAnimation_PreInOwnerUpdate() {
            if (LoadingAmmoAnimation == LoadingAmmoAnimationEnum.None && NO_EEMONG_LOADINGNONESET) {
                LoadingAnimation(LoadingAA_None.loadingAA_None_Roting, LoadingAA_None.loadingAA_None_X, LoadingAA_None.loadingAA_None_Y);
            }
            else if (LoadingAmmoAnimation == LoadingAmmoAnimationEnum.Shotgun) {
                LoadingAnimation(LoadingAA_Shotgun.loadingAmmoStarg_rot
                    , LoadingAA_Shotgun.loadingAmmoStarg_x, LoadingAA_Shotgun.loadingAmmoStarg_y);
            }
            else if (LoadingAmmoAnimation == LoadingAmmoAnimationEnum.Revolver) {
                LoadingAnimation((int)(Time * LoadingAA_Revolver.Rotationratio)
                    , LoadingAA_Revolver.loadingAmmoStarg_x, LoadingAA_Revolver.loadingAmmoStarg_y);
            }
        }

        private void Get_LoadingAmmoAnimation_PostInOwnerUpdate() {
            if (LoadingAmmoAnimation == LoadingAmmoAnimationEnum.None && NO_EEMONG_LOADINGNONESET) {
                return;
            }
            else if (LoadingAmmoAnimation == LoadingAmmoAnimationEnum.Revolver) {
                if (kreloadTimeValue > 0) {
                    ArmRotSengsFront = (MathHelper.PiOver2 * SafeGravDir
                        - MathHelper.ToRadians(LoadingAA_Revolver.ArmRotSengsFrontOffsetRotOver))
                        * SafeGravDir + LoadingAA_Revolver.ArmRotSengsFrontStartRotOver;
                    SetCompositeArm();
                }
            }
        }

        private bool? Get_LoadingAmmoAnimation_PreOnKreloadEvent() {
            if (LoadingAmmoAnimation == LoadingAmmoAnimationEnum.None && NO_EEMONG_LOADINGNONESET) {
                return true;
            }
            else if (LoadingAmmoAnimation == LoadingAmmoAnimationEnum.Handgun) {
                ArmRotSengsFront = (MathHelper.PiOver2 * SafeGravDir - Projectile.rotation) * DirSign * SafeGravDir + 0.3f;
                FeederOffsetRot = LoadingAA_Handgun.feederOffsetRot;
                FeederOffsetPos = new Vector2(DirSign * LoadingAA_Handgun.loadingAmmoStarg_x, LoadingAA_Handgun.loadingAmmoStarg_y) * SafeGravDir;
                Projectile.Center = GetGunBodyPos();
                Projectile.rotation = GetGunBodyRot();
                int value1 = (int)(kreloadMaxTime * LoadingAA_Handgun.level1);
                int value2 = (int)(kreloadMaxTime * LoadingAA_Handgun.level3);
                if (kreloadTimeValue >= value1) {
                    ArmRotSengsFront += (kreloadTimeValue - value1) * CWRUtils.atoR * 6;
                }
                if (kreloadTimeValue >= value2 && kreloadTimeValue <= value2 * 2) {
                    ArmRotSengsFront += (kreloadTimeValue - value2) * CWRUtils.atoR * 6;
                }
                return false;
            }
            return null;
        }

        private bool Get_LoadingAmmoAnimation_PreConsumeAmmo() {
            if (LoadingAmmoAnimation == LoadingAmmoAnimationEnum.None && NO_EEMONG_LOADINGNONESET) {
                return true;
            }
            else if (LoadingAmmoAnimation == LoadingAmmoAnimationEnum.Shotgun) {
                return false;
            }
            return true;
        }

        private bool Get_LoadingAmmoAnimation_KreLoadFulfill() {
            if (LoadingAmmoAnimation == LoadingAmmoAnimationEnum.None && NO_EEMONG_LOADINGNONESET) {
                return true;
            }
            else if (LoadingAmmoAnimation == LoadingAmmoAnimationEnum.Shotgun) {
                if (BulletNum < ModItem.AmmoCapacity) {
                    if (BulletNum == 0) {
                        BulletReturn();
                    }
                    LoadingQuantity = BulletNum + 1;
                    LoadBulletsIntoMagazine();
                    LoadingQuantity = 1;
                    ExpendedAmmunition();
                    if (CanFire && InShotgun_FireForcedReloadInterruption) {
                        SoundEngine.PlaySound(LoadingAA_Shotgun.pump with { Volume = 0.4f * loadingAA_VolumeValue, Pitch = -0.1f }, Projectile.Center);
                        ShootCoolingValue = 30;
                        extraKreloadMaxTime = 10;
                    }
                    else {
                        OnKreload = true;
                        kreloadTimeValue = kreloadMaxTime;
                        extraKreloadMaxTime = 0;
                    }
                }
                return true;
            }
            return true;
        }

        private bool? Get_LoadingAmmoAnimation_PreReloadEffects(int time, int maxTime) {
            if (LoadingAmmoAnimation == LoadingAmmoAnimationEnum.None && NO_EEMONG_LOADINGNONESET) {
                return true;
            }
            else if (LoadingAmmoAnimation == LoadingAmmoAnimationEnum.Shotgun) {
                if (time == 1) {
                    SoundEngine.PlaySound(LoadingAA_Shotgun.loadShellSound with { Volume = loadingAA_VolumeValue }, Projectile.Center);
                    if (BulletNum == ModItem.AmmoCapacity) {
                        SoundEngine.PlaySound(LoadingAA_Shotgun.pump with { Volume = loadingAA_VolumeValue }, Projectile.Center);
                        ShootCoolingValue += LoadingAA_Shotgun.pumpCoolingValue;
                    }
                }
                return false;
            }
            else if (LoadingAmmoAnimation == LoadingAmmoAnimationEnum.Handgun) {
                if (time == (int)(maxTime * LoadingAA_Handgun.level1)) {
                    SoundEngine.PlaySound(LoadingAA_Handgun.clipOut with { Volume = 0.65f * loadingAA_VolumeValue }, Projectile.Center);
                }
                if (time == (int)(maxTime * LoadingAA_Handgun.level2)) {
                    SoundEngine.PlaySound(LoadingAA_Handgun.clipLocked with { Volume = 0.65f * loadingAA_VolumeValue }, Projectile.Center);
                }
                if (time == (int)(maxTime * LoadingAA_Handgun.level3)) {
                    SoundEngine.PlaySound(LoadingAA_Handgun.slideInShoot with { Volume = 0.65f * loadingAA_VolumeValue }, Projectile.Center);
                }
                return false;
            }
            return null;
        }

        #endregion

        public override BitsByte SandBitsByte(BitsByte flags) {
            flags = base.SandBitsByte(flags);
            flags[5] = IsKreload;
            flags[6] = ManualReloadStart;
            return flags;
        }

        public override void ReceiveBitsByte(BitsByte flags) {
            base.ReceiveBitsByte(flags);
            IsKreload = flags[5];
            ManualReloadStart = flags[6];
        }

        protected override void setBaseFromeAI() {
            Owner.direction = LazyRotationUpdate ? oldSetRoting.ToRotationVector2().X > 0 ? 1 : -1 : ToMouse.X > 0 ? 1 : -1;
            Projectile.rotation = LazyRotationUpdate ? oldSetRoting : GetGunInFireRot();
            Projectile.Center = GetGunInFirePos();
            ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 * SafeGravDir - Projectile.rotation) * DirSign * SafeGravDir;
            if (MagazineSystem) {
                SetAutomaticCartridgeChange();
            }
            else {
                BulletNum = 2;
            }
        }

        public sealed override void InOwner() {
            SetHeld();
            InitializeMagazine();
            Get_LoadingAmmoAnimation_PreInOwnerUpdate();
            PreInOwnerUpdate();

            ArmRotSengsFront = (60 + ArmRotSengsFrontNoFireOffset) * CWRUtils.atoR * SafeGravDir;
            ArmRotSengsBack = (110 + ArmRotSengsBackNoFireOffset) * CWRUtils.atoR * SafeGravDir;
            Projectile.Center = GetGunBodyPos();
            Projectile.rotation = GetGunBodyRot();
            Projectile.timeLeft = 2;

            if (ShootCoolingValue > 0) {
                SetWeaponOccupancyStatus();
                ShootCoolingValue--;
            }
            if (ModItem.NoKreLoadTime > 0) {
                ModItem.NoKreLoadTime--;
            }

            if (SafeMouseInterfaceValue) {
                if (DownLeft) {
                    setBaseFromeAI();
                    if (IsKreload) {// && Projectile.IsOwnedByLocalPlayer()
                        if (!onFire) {
                            oldSetRoting = ToMouseA;
                        }
                        onFire = true;
                    }
                }
                else {
                    onFire = false;
                }

                if (DownRight && !onFire && CanRightClick && SafeMousetStart
                    && (!CartridgeHolderUI.Instance.OnMainP || SafeMousetStart2)) {//Owner.PressKey()
                    setBaseFromeAI();
                    if (IsKreload) {//&& Projectile.IsOwnedByLocalPlayer()
                        if (!onFireR) {
                            oldSetRoting = ToMouseA;
                        }
                        SafeMousetStart2 = true;
                        onFireR = true;
                    }
                }
                else {
                    SafeMousetStart2 = false;
                    onFireR = false;
                }

                if (WhetherStartChangingAmmunition()) {
                    AmmoState = Owner.GetAmmoState(Item.useAmmo);//在装填时更新弹药状态
                    if (AmmoState.Amount >= MinimumAmmoPerReload) {//只有弹药量大于最小弹药量时才可装填
                        OnKreload = true;
                        kreloadTimeValue = kreloadMaxTime;
                        extraKreloadMaxTime = 0;
                    }
                }
            }

            if (OnKreload) {//装弹过程
                bool result3 = PreOnKreloadEvent();
                if (LoadingAmmoAnimation != LoadingAmmoAnimationEnum.None) {
                    bool? result4 = Get_LoadingAmmoAnimation_PreOnKreloadEvent();
                    if (result4.HasValue) {
                        result3 = result4.Value;
                    }
                }
                if (result3) {
                    ArmRotSengsFront = (MathHelper.PiOver2 * SafeGravDir - Projectile.rotation) * DirSign * SafeGravDir + 0.3f;
                    ArmRotSengsFront += MathF.Sin(Time * 0.3f) * 0.7f * SafeGravDir;
                }
                kreloadTimeValue--;

                bool result = PreReloadEffects(kreloadTimeValue, kreloadMaxTime);
                if (LoadingAmmoAnimation != LoadingAmmoAnimationEnum.None) {
                    bool? result5 = Get_LoadingAmmoAnimation_PreReloadEffects(kreloadTimeValue, kreloadMaxTime);
                    if (result5.HasValue) {
                        result = result5.Value;
                    }
                }

                if (result) {
                    if (kreloadTimeValue == kreloadMaxTime - 1) {
                        KreloadSoundCaseEjection();
                    }
                    if (kreloadTimeValue == kreloadMaxTime / 2) {
                        KreloadSoundloadTheRounds();
                    }
                }

                ModOwner.PlayerIsKreLoadTime = 2;

                if (kreloadTimeValue <= 0) {//时间完成后设置装弹状态并准备下一次发射
                    AmmoState = Owner.GetAmmoState(Item.useAmmo);//再更新一次弹药状态
                    if (PreConsumeAmmoEvent() && Get_LoadingAmmoAnimation_PreConsumeAmmo()) {
                        BulletReturn();
                        LoadBulletsIntoMagazine();//这一次更新弹匣内容，压入子弹
                        if (BulletConsumption) {
                            ExpendedAmmunition();
                        }
                    }

                    OnKreload = false;
                    IsKreload = true;
                    if (Item.type != ItemID.None) {
                        ModItem.IsKreload = true;
                    }
                    kreloadTimeValue = 0;

                    bool result2 = KreLoadFulfill();
                    if (LoadingAmmoAnimation != LoadingAmmoAnimationEnum.None) {
                        result2 = Get_LoadingAmmoAnimation_KreLoadFulfill();
                    }

                    if (result2) {
                        int value = AmmoState.Amount;
                        if (value > ModItem.AmmoCapacity) {
                            value = ModItem.AmmoCapacity;
                        }
                        if (LoadingQuantity > 0) {
                            value = LoadingQuantity;
                        }
                        BulletNum += value;
                        if (BulletNum > ModItem.AmmoCapacity) {
                            BulletNum = ModItem.AmmoCapacity;
                        }
                        ModItem.SpecialAmmoState = SpecialAmmoStateEnum.ordinary;
                    }
                }
                SetWeaponOccupancyStatus();
            }

            if (DownLeft) {
                if (!IsKreload && LoadingReminder) {
                    if (!Owner.mouseInterface && kreloadTimeValue <= 0) {
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

            Get_LoadingAmmoAnimation_PostInOwnerUpdate();
            if (AutomaticPolishingEffect && automaticPolishingInShootStartFarg) {
                AutomaticPolishing(FireTime);

            }
            PostInOwnerUpdate();
            Projectile.netUpdate = true;
        }

        /// <summary>
        /// 退还弹匣内非空子弹
        /// </summary>
        public void BulletReturn() {
            if (ModItem.MagazineContents != null && ModItem.MagazineContents.Length > 0 && ReturnRemainingBullets
                && Projectile.IsOwnedByLocalPlayer()/*这个操作只能在弹幕主人身上来完成，否则会导致多次给予子弹*/) {
                foreach (Item i in ModItem.MagazineContents) {//在装弹之前返回玩家弹匣中剩余的弹药
                    if (i.stack <= 0 || i.type == ItemID.None) {
                        continue;
                    }
                    if (i.CWR().AmmoProjectileReturn) {
                        Owner.QuickSpawnItem(Source, new Item(i.type), i.stack);
                    }
                }
            }
        }
        /// <summary>
        /// 向弹匣中装入子弹的函数，这个函数并不负责消耗逻辑
        /// </summary>
        public virtual void LoadBulletsIntoMagazine() {
            CWRItems cwrItem = ModItem;//获取模组物品实例
            List<Item> loadedItems = [];
            int magazineCapacity = cwrItem.AmmoCapacity;
            if (LoadingQuantity > 0) {
                magazineCapacity = LoadingQuantity;
            }
            int accumulatedAmount = 0;

            if (CWRMod.Suitableversion_improveGame) {
                // 更好的体验适配 - 如果有弹药链，转到单独的弹药装载
                var ammoChain = Item.GetQotAmmoChain();
                if (ammoChain is not null && Owner.LoadFromAmmoChain(Item, ammoChain, Item.useAmmo
                    , magazineCapacity, out var pushedAmmo, out int ammoCount)) {
                    cwrItem.MagazineContents = pushedAmmo.ToArray();
                    AmmoState.Amount = ammoCount;
                    return;
                }
            }

            foreach (Item ammoItem in AmmoState.InItemInds) {
                int stack = ammoItem.stack;

                if (stack > magazineCapacity - accumulatedAmount) {
                    stack = magazineCapacity - accumulatedAmount;
                }

                if (CWRUtils.IsAmmunitionUnlimited(ammoItem)) {//如果该物品不消耗，那么可能是一个无限弹药类型的物品，这里进行特别处理
                    if (CWRLoad.ItemToShootID.ContainsKey(ammoItem.type)) {
                        int newAmmoType = ammoItem.type;
                        if (CWRLoad.ProjectileToSafeAmmoMap.TryGetValue(ammoItem.shoot, out int value2)) {
                            newAmmoType = value2;
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
                if (CWRUtils.IsAmmunitionUnlimited(inds)) {
                    break;
                }
                if (inds.stack >= maxAmmo) {
                    inds.stack -= maxAmmo - ammo;
                    ammo += maxAmmo;
                }
                else {
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
                List<Item> items = [];
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
        /// 左键单次开火事件，在网络模式中只会被弹幕主人调用
        /// </summary>
        public override void FiringShoot() {
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
        /// <summary>
        /// 右键单次开火事件，在网络模式中只会被弹幕主人调用
        /// </summary>
        public override void FiringShootR() {
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

        //重写这个函数，因为FeederGun和BaseGun的实现原理不一样，为了保持效果正确，封装并重写这部分的逻辑
        public override float GetBoltInFireRatio() {
            float value1 = Projectile.ai[1] * 2;
            if (value1 > FireTime) {
                value1 = FireTime;
            }
            return value1 / FireTime;
        }

        public bool GetMagazineCanUseAmmoProbability() {
            bool result;
            result = Owner.CanUseAmmoInWeaponShoot(Item);
            return result;
        }

        public sealed override void SpanProj() {
            if ((onFire || onFireR) && ShootCoolingValue <= 0 && kreloadTimeValue <= 0) {
                if (LazyRotationUpdate) {
                    Projectile.rotation = oldSetRoting = ToMouseA;
                }

                //弹容替换在此处执行，将发射内容设置为弹匣第一位的弹药类型
                if (AmmoTypeAffectedByMagazine && MagazineSystem
                    && ModItem.MagazineContents.Length > 0 && Projectile.IsOwnedByLocalPlayer()) {
                    if (ModItem.MagazineContents[0] == null) {
                        ModItem.MagazineContents[0] = new Item();
                    }
                    AmmoTypes = ModItem.MagazineContents[0].shoot;
                    if (AmmoTypes == 0) {
                        AmmoTypes = ProjectileID.Bullet;
                    }
                }

                bool canShoot = BulletNum > 0;
                if (!CWRServerConfig.Instance.MagazineSystem) {
                    if (AmmoTypes == ProjectileID.None && Item.useAmmo != AmmoID.None) {
                        if (LoadingReminder) {
                            HandleEmptyAmmoEjection();
                            LoadingReminder = false;
                        }
                        canShoot = false;
                    }
                }

                if (canShoot) {
                    if (ForcedConversionTargetAmmoFunc.Invoke()) {
                        AmmoTypes = ToTargetAmmo;
                    }

                    if (PreFiringShoot()) {
                        SetShootAttribute();

                        if (Projectile.IsOwnedByLocalPlayer()) {
                            if (Owner.Calamity().luxorsGift || ModOwner.TheRelicLuxor > 0) {
                                LuxirEvent();
                            }
                            if (onFire) {
                                FiringShoot();
                            }
                            if (onFireR) {
                                FiringShootR();
                            }
                            if (GlobalItemBehavior) {
                                ItemLoaderInFireSetBaver();
                            }
                        }

                        if (CanCreateSpawnGunDust) {
                            HanderSpwanDust();
                        }
                        if (CanCreateCaseEjection && !AutomaticPolishingEffect) {
                            HanderCaseEjection();
                        }
                        if (CanCreateRecoilBool) {
                            CreateRecoil();
                        }
                        if (EnableRecoilRetroEffect) {
                            OffsetPos -= ShootVelocityInProjRot.UnitVector() * RecoilRetroForceMagnitude;
                        }
                        if (FiringDefaultSound) {
                            HanderPlaySound();
                        }
                        if (FireLight > 0) {
                            Color fireColor = CWRUtils.MultiStepColorLerp(Main.rand.NextFloat(0.3f, 0.65f), Color.Red, Color.Gold);
                            Vector3 fireColorToVr3 = fireColor.ToVector3() * Main.rand.NextFloat(0.1f, FireLight);
                            Lighting.AddLight(GunShootPos, fireColorToVr3);
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
                if (MagazineSystem) {
                    if (PreFireReloadKreLoad()) {
                        if (BulletNum <= 0) {
                            SetEmptyMagazine();
                            AutomaticCartridgeChangeDelayTime += FireTime;
                        }
                    }
                }
                automaticPolishingInShootStartFarg = true;
                ShootCoolingValue += FireTime + 1;
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
            List<Item> list = [];
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
                FeederOffsetRot = -rot * SafeGravDir;
                FeederOffsetPos = new Vector2(DirSign * -xl, -yl * SafeGravDir);
            }
        }
        /// <summary>
        /// 安全获取选定的弹匣弹药内容
        /// </summary>
        /// <returns></returns>
        public Item GetSelectedBullets() {
            return ModItem.MagazineContents == null
                ? new Item()
                : ModItem.MagazineContents.Length <= 0
                ? new Item()
                : ModItem.MagazineContents[0] == null ? new Item() : ModItem.MagazineContents[0];
        }

        #endregion
    }
}
