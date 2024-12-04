using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.GoreEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core
{
    /// <summary>
    /// 一个改进版的枪基类，这个基类的基础实现会更加快捷和易于模板化
    /// </summary>
    internal abstract class BaseGun : BaseHeldRanged
    {
        #region Date
        protected float oldSetRoting;
        /// <summary>
        /// 一个专用与子类的属性，仅仅用于保证自动抛壳<see cref="AutomaticPolishing"/>会在正确的时机运行
        /// ，一般来讲不要设置它，它会在一次射击中自动恢复为<see langword="true"/>
        /// </summary>
        protected bool automaticPolishingInShootStartFarg;
        /// <summary>
        /// 枪械旋转角矫正
        /// </summary>
        public float OffsetRot;
        /// <summary>
        /// 枪械位置矫正
        /// </summary>
        public Vector2 OffsetPos;
        /// <summary>
        /// 右手角度值，这个值被自动设置，不要手动给它赋值
        /// </summary>
        public float ArmRotSengsFront;
        /// <summary>
        /// 右手角度值，这个值被自动设置，不要手动给它赋值
        /// </summary>
        public float ArmRotSengsBack;
        /// <summary>
        /// 右手角度值矫正
        /// </summary>
        public float ArmRotSengsFrontNoFireOffset;
        /// <summary>
        /// 左手角度值矫正
        /// </summary>
        public float ArmRotSengsBackNoFireOffset;
        /// <summary>
        /// 是否自动在一次单次射击后调用后坐力函数，默认为<see langword="true"/>
        /// </summary>
        internal bool CanCreateRecoilBool = true;
        /// <summary>
        /// 是否自动在一次单次射击后调用开火粒子，默认为<see langword="true"/>
        /// </summary>
        internal bool CanCreateSpawnGunDust = true;
        /// <summary>
        /// 是否自动在一次单次射击后调用抛壳函数，默认为<see langword="true"/>，如果<see cref="AutomaticPolishingEffect"/>为<see langword="true"/>，那么该属性将无效化
        /// </summary>
        internal bool CanCreateCaseEjection = true;
        /// <summary>
        /// 是否启用自动抛弹壳的行为，如果为<see langword="true"/>，那么枪械AI会在合适的时机自行调用<see cref="CaseEjection"/>函数
        /// </summary>
        public bool AutomaticPolishingEffect;
        /// <summary>
        /// 是否在<see cref="InOwner"/>执行后自动更新手臂参数，默认为<see langword="true"/>
        /// </summary>
        public bool SetArmRotBool = true;
        /// <summary>
        /// 枪械是否受到应力缩放，默认为<see langword="true"/>
        /// </summary>
        public bool PressureWhetherIncrease = true;
        /// <summary>
        /// 是否启用惰性旋转角更新，如果为<see langword="true"/>，枪械在开火周期中的旋转角度只会在开火前更新一次
        /// </summary>
        public bool LazyRotationUpdate = false;
        /// <summary>
        /// 是否启用后坐力枪体反向制推效果，默认为<see langword="false"/>
        /// </summary>
        protected bool EnableRecoilRetroEffect;
        /// <summary>
        /// 后坐力制推力度模长，推送方向为<see cref="BaseHeldRanged.ShootVelocity"/>的反向
        /// ，在<see cref="EnableRecoilRetroEffect"/>为<see langword="true"/>时生效，默认为5f
        /// </summary>
        protected float RecoilRetroForceMagnitude = 5;
        /// <summary>
        /// 开火时是否默认播放手持物品的使用音效<see cref="Item.UseSound"/>，但如果准备重写<see cref="SpanProj"/>，这个属性将失去作用，默认为<see langword="true"/>
        /// </summary>
        public bool FiringDefaultSound = true;
        /// <summary>
        /// 这个角度用于设置枪体在玩家非开火阶段的仰角，这个角度是周角而非弧度角，默认为20f
        /// </summary>
        public float AngleFirearmRest = 20f;
        /// <summary>
        /// 枪压，决定开火时的上抬力度，默认为0
        /// </summary>
        public float GunPressure = 0;
        /// <summary>
        /// 控制力度，决定压枪的力度，默认为0.01f
        /// </summary>
        public float ControlForce = 0.01f;
        /// <summary>
        /// 手持距离，生效于非开火状态下，默认为15
        /// </summary>
        public float HandDistance = 15;
        /// <summary>
        /// 手持距离，生效于非开火状态下，默认为0
        /// </summary>
        public float HandDistanceY = 0;
        /// <summary>
        /// 手持距离，生效于开火状态下，默认为20
        /// </summary>
        public float HandFireDistance = 20;
        /// <summary>
        /// 手持距离，生效于开火状态下，默认为-3
        /// </summary>
        public float HandFireDistanceY = -3;
        /// <summary>
        /// 应力范围，默认为10
        /// </summary>
        public float RangeOfStress = 10;
        /// <summary>
        /// 开火时会制造的后坐力模长，默认为1.2f
        /// </summary>
        public float Recoil = 1.2f;
        /// <summary>
        /// 止推模长恢复系数，值越接近1恢复的越加缓慢，默认为0.5f
        /// </summary>
        protected float RecoilOffsetRecoverValue = 0.5f;
        /// <summary>
        /// 该枪械在开火时的一个转动角，用于快捷获取
        /// </summary>
        public virtual float GunOnFireRot => ToMouseA - OffsetRot * DirSign;
        /// <summary>
        /// 发射口的长度矫正值，默认为0
        /// </summary>
        public float ShootPosToMouLengValue = 0;
        /// <summary>
        /// 发射口的竖直方向长度矫正值，默认为0
        /// </summary>
        public float ShootPosNorlLengValue = 0;
        /// <summary>
        /// 光照强度，默认为1，用于控制在开火时制造光火效果的强度，为0时表示关闭
        /// </summary>
        public float FireLight = 1;
        /// <summary>
        /// 是否是一把弩
        /// </summary>
        public bool IsCrossbow;
        /// <summary>
        /// 是否绘制弩箭，默认为<see langword="true"/>，这个属性只有在<see cref="IsCrossbow"/>也为<see langword="true"/>时才会生效
        /// </summary>
        public bool CanDrawCrossArrow = true;
        /// <summary>
        /// 所要绘制的弩箭的缩放比例，默认为0.8f
        /// </summary>
        public float DrawCrossArrowSize = 0.8f;
        /// <summary>
        /// 所要绘制的弩箭的竖直偏移量，该偏移量垂直于弩箭朝向，默认为0
        /// </summary>
        public float DrawCrossArrowNorlMode = 0;
        /// <summary>
        /// 所要绘制的弩箭的方向偏移量，该偏移量等于弩箭朝向，默认为0
        /// </summary>
        public float DrawCrossArrowToMode = 0;
        /// <summary>
        /// 所要绘制的弩箭的旋转角偏移量，默认为0
        /// </summary>
        public float DrawCrossArrowOffsetRot = 0;
        /// <summary>
        /// 箭矢拉伸模长倍率，用于缩放拉箭运动的运动幅度，默认为1
        /// </summary>
        public float DrawCrossArrowDrawingDieLengthRatio = 1;
        /// <summary>
        /// 所要绘制的弩箭的数量，默认为1
        /// </summary>
        public int DrawCrossArrowNum = 1;
        /// <summary>
        /// 自定义绘制中心点，默认为<see cref="Vector2.Zero"/>，即不启用
        /// </summary>
        protected Vector2 CustomDrawOrig = Vector2.Zero;
        /// <summary>
        /// 快速设置抛壳大小，默认为1
        /// </summary>
        protected float EjectCasingProjSize = 1;
        /// <summary>
        /// 快捷获取该枪械的发射口位置
        /// </summary>
        public Vector2 GunShootPos => GetShootPos(ShootPosToMouLengValue, ShootPosNorlLengValue);
        /// <summary>
        /// 玩家是否正在行走
        /// </summary>
        public virtual bool WalkDetection => Owner.velocity.Y == 0 && Math.Abs(Owner.velocity.X) > 0;
        /// <summary>
        /// 应力缩放系数
        /// </summary>
        public float OwnerPressureIncrease => PressureWhetherIncrease ? ModOwner.PressureIncrease : 1;
        /// <summary>
        /// 快速的获取该枪械是否正在进行开火尝试，包括左键或者右键的情况
        /// </summary>
        public override bool CanFire => (DownLeft || DownRight && !onFire && CanRightClick && SafeMousetStart) && SafeMouseInterfaceValue;
        /// <summary>
        /// 是否允许手持状态，如果玩家关闭了手持动画设置，这个值将在非开火状态时返回<see langword="false"/>
        /// </summary>
        public override bool OnHandheldDisplayBool {
            get {
                return !HandheldDisplay && !CanFire ? false : WeaponHandheldDisplay ? true : CanFire;
            }
        }

        internal struct SpwanGunDustMngsDataStruct
        {
            public Vector2 pos = default;
            public Vector2 velocity = default;
            public float splNum = 1f;
            public int dustID1 = 262;
            public int dustID2 = 54;
            public int dustID3 = 53;
            public SpwanGunDustMngsDataStruct() { }
        }

        protected SpwanGunDustMngsDataStruct SpwanGunDustMngsData = new SpwanGunDustMngsDataStruct();

        /// <summary>
        /// 获取来自物品的生成源，该生成源实例会附加CWRGun标签，用于特殊识别
        /// </summary>
        internal EntitySource_ItemUse_WithAmmo Source => new EntitySource_ItemUse_WithAmmo(Owner, Item, UseAmmoItemType, "CWRGunShoot");
        /// <summary>
        /// 获取来自物品的生成源，该生成源仅仅用于派生于物品关系，如果不想发射的弹幕被识别为枪械类射弹并受到特殊加成，使用这个
        /// </summary>
        internal EntitySource_ItemUse_WithAmmo Source2 => new EntitySource_ItemUse_WithAmmo(Owner, Item, UseAmmoItemType);
        /// <summary>
        /// 该枪体使用的实际纹理
        /// </summary>
        public virtual Texture2D TextureValue => CWRUtils.GetT2DValue(Texture);
        #endregion

        /// <summary>
        /// 更新枪压的作用状态
        /// </summary>
        public void UpdateRecoil() {
            OffsetRot -= ControlForce;
            if (OffsetRot <= 0) {
                OffsetRot = 0;
            }
            if (OffsetPos != Vector2.Zero) {
                OffsetPos *= RecoilOffsetRecoverValue;
            }
        }
        /// <summary>
        /// 制造后坐力，这个函数只应该由弹幕主人调用，它不会自动调用，需要重写时在合适的代码片段中调用这个函数
        /// ，以确保制造后坐力的时机正确，一般在<see cref="BaseHeldRanged.SpanProj"/>中调用
        /// </summary>
        /// <returns>返回制造出的后坐力向量</returns>
        public virtual Vector2 CreateRecoil() {
            OffsetRot += GunPressure * OwnerPressureIncrease;
            if (!CWRServerConfig.Instance.ActivateGunRecoil) {
                return Vector2.Zero;
            }
            Vector2 recoilVr = ShootVelocity.UnitVector() * (Recoil * -OwnerPressureIncrease);
            if (Math.Abs(Owner.velocity.X) < RangeOfStress && Math.Abs(Owner.velocity.Y) < RangeOfStress) {
                Owner.velocity += recoilVr;
            }
            return recoilVr;
        }
        /// <summary>
        /// 在枪械的更新周期中的最后被调用，用于复原一些数据
        /// </summary>
        public virtual void Recover() { }

        public override void PostSetRangedProperty() {
            if (IsCrossbow) {
                CanCreateSpawnGunDust = false;
                CanCreateCaseEjection = false;
            }
        }

        /// <summary>
        /// 统一获取枪体在开火时的旋转角，返回值默认在<see cref="InOwner"/>中被获取设置于Projectile.rotation
        /// </summary>
        /// <returns></returns>
        public virtual float GetGunInFireRot() {
            return LazyRotationUpdate ? oldSetRoting : GunOnFireRot;
        }
        /// <summary>
        /// 统一获取枪体在开火时的中心位置，返回值默认在<see cref="InOwner"/>中被获取设置于Projectile.Center
        /// </summary>
        /// <returns></returns>
        public virtual Vector2 GetGunInFirePos() {
            return Owner.GetPlayerStabilityCenter() + Projectile.rotation.ToRotationVector2() * HandFireDistance + new Vector2(0, HandFireDistanceY) + OffsetPos;
        }

        /// <summary>
        /// 统一获取枪体在静置时的旋转角，返回值默认在<see cref="InOwner"/>中被获取设置于Projectile.rotation
        /// </summary>
        /// <returns></returns>
        public virtual float GetGunBodyRot() {
            return Owner.direction > 0 ? MathHelper.ToRadians(AngleFirearmRest) : MathHelper.ToRadians(180 - AngleFirearmRest);
        }
        /// <summary>
        /// 统一获取枪体在静置时的中心位置，返回值默认在<see cref="InOwner"/>中被获取设置于Projectile.Center
        /// </summary>
        /// <returns></returns>
        public virtual Vector2 GetGunBodyPos() {
            return Owner.GetPlayerStabilityCenter()
                + new Vector2(DirSign * HandDistance, HandDistanceY * SafeGravDir) * SafeGravDir;
        }
        /// <summary>
        /// 在开火时被调用，在适当的时机下调用这个函数
        /// </summary>
        protected virtual void SetGunBodyInFire() {
            Owner.direction = LazyRotationUpdate ? oldSetRoting.ToRotationVector2().X > 0 ? 1 : -1 : ToMouse.X > 0 ? 1 : -1;
            //值得一说的是，设置旋转角的操作必须在设置位置之前，因为位置设置需要旋转角的值，否则会造成不必要的延迟帧
            Projectile.rotation = GetGunInFireRot();
            Projectile.Center = GetGunInFirePos();
            ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 * SafeGravDir - Projectile.rotation) * DirSign * SafeGravDir;
        }
        /// <summary>
        /// 在闲置时被调用，一般用于设置非开火状态下的手持状态
        /// </summary>
        public virtual void SetGunBodyHandIdle() {
            ArmRotSengsFront = (60 + ArmRotSengsFrontNoFireOffset) * CWRUtils.atoR * SafeGravDir;
            ArmRotSengsBack = (110 + ArmRotSengsBackNoFireOffset) * CWRUtils.atoR * SafeGravDir;
            Projectile.rotation = GetGunBodyRot();
            Projectile.Center = GetGunBodyPos();
        }

        public override void FiringIncident() {
            if (DownLeft) {
                SetGunBodyInFire();
                if (HaveAmmo) {// && Projectile.IsOwnedByLocalPlayer()
                    if (!onFire) {
                        oldSetRoting = ToMouseA;
                    }
                    onFire = true;
                }
            }
            else {
                onFire = false;
            }

            if (DownRight && !onFire && CanRightClick && SafeMousetStart) {//Owner.PressKey()
                SetGunBodyInFire();
                if (HaveAmmo) {// && Projectile.IsOwnedByLocalPlayer()
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

        public override void InOwner() {
            Projectile.timeLeft = 2;
            PreInOwnerUpdate();

            if (InOwner_HandState_AlwaysSetInFireRoding) {
                SetGunBodyInFire();
            }
            else {
                SetGunBodyHandIdle();
            }

            if (ShootCoolingValue > 0) {
                SetWeaponOccupancyStatus();
                ShootCoolingValue--;
            }

            SetHeld();

            if (SafeMouseInterfaceValue) {
                FiringIncident();
            }
            if (AutomaticPolishingEffect) {
                AutomaticPolishing(Item.useTime);
            }
            PostInOwnerUpdate();
        }
        /// <summary>
        /// 一个自动抛科的行为的二次封装
        /// </summary>
        protected void AutomaticPolishing(int maxTime) {
            if (ShootCoolingValue == maxTime / 2 && maxTime > 0) {
                SoundEngine.PlaySound(CWRSound.Gun_BoltAction with { Volume = 0.5f, PitchRange = (-0.05f, 0.05f) }, Projectile.Center);
                CaseEjection();
                automaticPolishingInShootStartFarg = false;
            }
        }
        /// <summary>
        /// 一个快捷创建发射事件的方法，在<see cref="SpanProj"/>中被调用，<see cref="BaseHeldRanged.onFire"/>为<see cref="true"/>才可能调用。
        /// 值得注意的是，如果需要更强的自定义效果，一般是需要直接重写<see cref="SpanProj"/>的
        /// </summary>
        public virtual void FiringShoot() {
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            _ = UpdateConsumeAmmo();
        }
        /// <summary>
        /// 一个快捷创建发射事件的方法，在<see cref="SpanProj"/>中被调用，<see cref="onFireR"/>为<see cref="true"/>才可能调用。
        /// 值得注意的是，如果需要更强的自定义效果，一般是需要直接重写<see cref="SpanProj"/>的
        /// </summary>
        public virtual void FiringShootR() {
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            _ = UpdateConsumeAmmo();
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
        /// <summary>
        /// 快速创建一个卢克索发射事件的方法，默认在<see cref="LuxirEvent"/>中调用
        /// </summary>
        /// <param name="luxirDamage"></param>
        /// <returns></returns>
        public virtual int SpanLuxirProj(int luxirDamage) {
            return 0;
        }
        /// <summary>
        /// 获取枪口位置，一般用于发射口的矫正
        /// </summary>
        /// <param name="toMouLeng"></param>
        /// <param name="norlLeng"></param>
        /// <returns></returns>
        public virtual Vector2 GetShootPos(float toMouLeng, float norlLeng) {
            Vector2 norlVr = (Projectile.rotation + (DirSign > 0 ? MathHelper.PiOver2 : -MathHelper.PiOver2)).ToRotationVector2();
            return Projectile.Center + Projectile.rotation.ToRotationVector2() * toMouLeng + norlVr * norlLeng;
        }
        /// <summary>
        /// 一个快捷的抛壳方法，需要自行调用
        /// </summary>
        /// <param name="slp"></param>
        public virtual void CaseEjection(float slp = 1) {
            if (CWRMod.Instance.terrariaOverhaul != null && slp == 1
                || !CWRServerConfig.Instance.EnableCasingsEntity) {
                return;
            }
            Vector2 pos = Owner.Top + Owner.Top.To(GunShootPos) / 2;
            Vector2 vr = (Projectile.rotation - Main.rand.NextFloat(-0.1f, 0.1f) * DirSign).ToRotationVector2() * -Main.rand.NextFloat(3, 7) + Owner.velocity;
            Gore.NewGore(Source2, pos, vr, CaseGore.PType, slp == 1 ? EjectCasingProjSize : slp);//这是早该有的改变
        }
        /// <summary>
        /// 一个快捷的创造开火烟尘粒子效果的方法
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="velocity"></param>
        /// <param name="splNum"></param>
        /// <param name="dustID1"></param>
        /// <param name="dustID2"></param>
        /// <param name="dustID3"></param>
        public virtual void SpawnGunFireDust(Vector2 pos = default, Vector2 velocity = default, float splNum = 1f, int dustID1 = 262, int dustID2 = 54, int dustID3 = 53) {
            if (pos == default) {
                pos = GunShootPos;
            }
            if (velocity == default) {
                velocity = ShootVelocity;
            }
            pos += velocity.SafeNormalize(Vector2.Zero) * Projectile.width * Projectile.scale * 0.71f;
            for (int i = 0; i < 30 * splNum; i++) {
                int dustID;
                switch (Main.rand.Next(6)) {
                    case 0:
                        dustID = dustID1;
                        break;
                    case 1:
                    case 2:
                        dustID = dustID2;
                        break;
                    default:
                        dustID = dustID3;
                        break;
                }
                float num = Main.rand.NextFloat(3f, 13f) * splNum;
                float angleRandom = 0.06f;
                Vector2 dustVel = new Vector2(num, 0f).RotatedBy((double)velocity.ToRotation(), default);
                dustVel = dustVel.RotatedBy(0f - angleRandom);
                dustVel = dustVel.RotatedByRandom(2f * angleRandom);
                float scale = Main.rand.NextFloat(0.5f, 1.5f);
                int idx = Dust.NewDust(pos, 1, 1, dustID, dustVel.X, dustVel.Y, 0, default, scale);
                Main.dust[idx].noGravity = true;
                Main.dust[idx].position = pos;
            }
        }

        public virtual void HanderSpwanDust() {
            SpawnGunFireDust(SpwanGunDustMngsData.pos, SpwanGunDustMngsData.velocity, SpwanGunDustMngsData.splNum
                        , SpwanGunDustMngsData.dustID1, SpwanGunDustMngsData.dustID2, SpwanGunDustMngsData.dustID3);
        }

        public virtual void HanderCaseEjection() {
            CaseEjection();
        }

        public virtual void SetShootAttribute() {

        }

        /// <summary>
        /// 在单次开火时运行，优先于<see cref="FiringShoot"/>运行，返回<see langword="false"/>禁用<see cref="FiringShoot"/>的运行
        /// 需要注意的是，该函数将会在服务器与其他客户端上运行，所以在编写功能时需要斟酌调用环境以保证其在多人模式的正确运行
        /// </summary>
        /// <returns></returns>
        public virtual bool PreFiringShoot() {
            return true;
        }
        /// <summary>
        /// 在单次开火时，在<see cref="FiringShoot"/>运行后运行，无论<see cref="PreFiringShoot"/>返回什么都会运行
        /// 需要注意的是，该函数将会在服务器与其他客户端上运行，所以在编写功能时需要斟酌调用环境以保证其在多人模式的正确运行
        /// </summary>
        /// <returns></returns>
        public virtual void PostFiringShoot() { }

        public override void SpanProj() {
            if (ShootCoolingValue <= 0 && (onFire || onFireR)) {
                if (LazyRotationUpdate) {
                    Projectile.rotation = oldSetRoting = ToMouseA;
                }

                if (ForcedConversionTargetAmmoFunc.Invoke()) {
                    AmmoTypes = ToTargetAmmo;
                }

                //在生成射弹前再执行一次 SetGunBodyInFire，以防止因为更新顺序所导致的延迟帧情况
                SetGunBodyInFire();

                SetShootAttribute();

                if (PreFiringShoot()) {
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        if (onFire) {
                            FiringShoot();
                        }
                        if (onFireR) {
                            FiringShootR();
                        }
                        if (CalOwner.luxorsGift || ModOwner.TheRelicLuxor > 0) {
                            LuxirEvent();
                        }
                        if (GlobalItemBehavior) {
                            ItemLoaderInFireSetBaver();
                        }
                    }

                    if (FiringDefaultSound) {
                        HanderPlaySound();
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
                        OffsetPos -= ShootVelocity.UnitVector() * RecoilRetroForceMagnitude;
                    }
                    if (FireLight > 0) {
                        Lighting.AddLight(GunShootPos, VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(0.3f, 0.65f), Color.Red, Color.Gold).ToVector3() * FireLight);
                    }
                }
                PostFiringShoot();
                automaticPolishingInShootStartFarg = true;
                ShootCoolingValue += Item.useTime;
                onFireR = onFire = false;
            }
        }

        public override bool PreUpdate() {
            bool reset = base.PreUpdate();
            if (ModOwner == null) {
                ModOwner = Owner.CWR();
            }
            return reset;
        }

        public override void AI() {
            InOwner();
            if (SetArmRotBool) {
                SetCompositeArm();
            }
            UpdateRecoil();
            if (overNoFireCeahks()) {
                SpanProj();
            }
            Time++;
            Recover();
        }

        public void SetCompositeArm() {
            if (OnHandheldDisplayBool) {
                Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, ArmRotSengsFront * -DirSign);
                if (!Onehanded) {
                    Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, ArmRotSengsBack * -DirSign);
                }
            }
        }

        public sealed override bool PreDraw(ref Color lightColor) {
            if (OnHandheldDisplayBool) {
                Color color = lightColor;
                if (!CWRServerConfig.Instance.WeaponAdaptiveIllumination && CanFire) {
                    color = Color.White;
                }
                Vector2 drawPos = Projectile.Center - Main.screenPosition + SpecialDrawPositionOffset;
                GunDraw(drawPos, ref color);
                PostGunDraw(drawPos, ref color);
            }
            return false;
        }

        public virtual void GunDraw(Vector2 drawPos, ref Color lightColor) {
            Main.EntitySpriteDraw(TextureValue, drawPos, null, lightColor
                , Projectile.rotation, TextureValue.Size() / 2, Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            if (IsCrossbow && CanDrawCrossArrow && CWRServerConfig.Instance.BowArrowDraw) {
                DrawBolt(drawPos);
            }
        }

        public virtual void PostGunDraw(Vector2 drawPos, ref Color lightColor) {

        }

        /// <summary>
        /// 获取拉弓弦的进度比例，用于处理弩箭的动画进度
        /// </summary>
        /// <returns></returns>
        public virtual float GetBoltInFireRatio() {
            float value1 = Projectile.ai[1] * 2;
            if (value1 > Item.useTime) {
                value1 = Item.useTime;
            }
            return value1 / Item.useTime;
        }

        public void DrawBolt(Vector2 drawPos) {
            bool boolvalue = Projectile.ai[1] < Item.useTime - 3;
            if (Item.useTime <= 5) {
                boolvalue = true;
            }

            if (boolvalue) {
                int useAmmoItemType = UseAmmoItemType;
                if (useAmmoItemType == ItemID.None) {
                    return;
                }
                if (useAmmoItemType > 0 && useAmmoItemType < TextureAssets.Item.Length) {
                    Main.instance.LoadItem(useAmmoItemType);
                }

                Texture2D arrowValue = TextureAssets.Item[useAmmoItemType].Value;
                Item arrowItemInds = new Item(useAmmoItemType);

                if (!arrowItemInds.consumable) {
                    int newtype = ItemID.WoodenArrow;
                    if (VaultUtils.ProjectileToSafeAmmoMap.TryGetValue(arrowItemInds.shoot, out int value2)) {
                        newtype = value2;
                    }
                    Main.instance.LoadItem(newtype);
                    arrowValue = TextureAssets.Item[newtype].Value;
                }

                if (ForcedConversionTargetAmmoFunc.Invoke() && ToTargetAmmoInDraw != -1) {
                    arrowValue = TextureAssets.Projectile[ToTargetAmmo].Value;
                    if (ToTargetAmmoInDraw > 0) {
                        arrowValue = TextureAssets.Projectile[ToTargetAmmoInDraw].Value;
                    }
                    if (ISForcedConversionDrawAmmoInversion) {
                        CustomDrawOrig = new Vector2(arrowValue.Width / 2, 0);
                        DrawCrossArrowOffsetRot = MathHelper.Pi;
                    }
                }
                else if (ISForcedConversionDrawAmmoInversion) {
                    CustomDrawOrig = Vector2.Zero;
                    DrawCrossArrowOffsetRot = 0;
                }

                float drawRot = Projectile.rotation + MathHelper.PiOver2;
                float chordCoefficient = GetBoltInFireRatio();
                float lengsOFstValue = chordCoefficient * 8 * DrawCrossArrowDrawingDieLengthRatio + DrawCrossArrowToMode;
                Vector2 inprojRot = Projectile.rotation.ToRotationVector2();
                Vector2 offsetDrawPos = inprojRot * lengsOFstValue;
                Vector2 norlValue = inprojRot.GetNormalVector() * (DrawCrossArrowNorlMode + 2) * Owner.direction;
                Vector2 drawOrig = CustomDrawOrig == Vector2.Zero ? new(arrowValue.Width / 2, arrowValue.Height) : CustomDrawOrig;
                drawPos += offsetDrawPos;

                Color drawColor = Color.White;
                if (CWRServerConfig.Instance.WeaponAdaptiveIllumination || !CanFire) {
                    drawColor = Lighting.GetColor(new Point((int)(Projectile.position.X / 16), (int)(Projectile.position.Y / 16)));
                }

                void drawArrow(float overOffsetRot = 0, Vector2 overOffsetPos = default) => Main.EntitySpriteDraw(arrowValue
                    , drawPos + (overOffsetPos == default ? Vector2.Zero : overOffsetPos) + norlValue
                    , null, drawColor, drawRot + overOffsetRot + DrawCrossArrowOffsetRot, drawOrig, Projectile.scale * DrawCrossArrowSize, SpriteEffects.FlipVertically);

                if (DrawCrossArrowNum == 1) {
                    drawArrow();
                }
                else if (DrawCrossArrowNum == 2) {
                    drawArrow(0.3f * chordCoefficient);
                    drawArrow(-0.3f * chordCoefficient);
                }
                else if (DrawCrossArrowNum == 3) {
                    drawArrow(0.4f * chordCoefficient * Owner.direction);
                    drawArrow();
                    drawArrow(-0.3f * chordCoefficient * Owner.direction);
                }
                else {
                    drawArrow();
                }
            }
        }
    }
}
