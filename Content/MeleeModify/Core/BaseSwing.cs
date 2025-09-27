using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.GameSystem;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.MeleeModify.Core
{
    internal abstract class BaseSwing : BaseHeldProj
    {
        #region Data
        private int dirs;
        private float oldRot;
        private float _meleeSize = 0;
        protected Vector2 vector;
        protected Vector2 startVector;
        protected float inWormBodysDamageFaul = 0.85f;
        /// <summary>
        /// 刀刃攻击是否忽略物块阻挡，默认为<see langword="false"/>
        /// </summary>
        public bool HitIgnoreTile = false;
        /// <summary>
        /// 每次发射事件是否运行全局物品行为，默认为<see langword="true"/>
        /// </summary>
        public bool GlobalItemBehavior = true;
        /// <summary>
        /// 刀刃是否应该受近战缩放影响
        /// </summary>
        public bool AffectedMeleeSize = true;
        /// <summary>
        /// 一个额外近战大小缩放系数
        /// </summary>
        protected float OtherMeleeSize = 1f;
        /// <summary>
        /// 对应的武器近战缩放
        /// </summary>
        public float MeleeSize => AffectedMeleeSize ? _meleeSize * OtherMeleeSize : 1f;
        /// <summary>
        /// 近战缩放对应的渐进中间值
        /// </summary>
        protected float meleeSizeAsymptotic => 1 + (MeleeSize - 1f) / 2f;
        /// <summary>
        /// 刀光弧度全局缩放，默认为1
        /// </summary>
        public float oldLengthOffsetSizeValue = 1f;
        /// <summary>
        /// 是否无视弹幕碰撞箱的大小属性，默认为<see langword="false"/>，而如果为<see langword="true"/>，碰撞箱大小将不会自动影响刀刃的其他设置
        /// </summary>
        public bool IgnoreImpactBoxSize;
        /// <summary>
        /// 自发光
        /// </summary>
        public bool Incandescence;
        /// <summary>
        /// 刀光纹理倾斜采样，默认为<see langword="false"/>
        /// </summary>
        public bool ObliqueSampling = false;
        /// <summary>
        /// 动画帧切换间隔，默认为5
        /// </summary>
        public int CuttingFrmeInterval = 5;
        /// <summary>
        /// 最大动画帧数，默认为1
        /// </summary>
        public int AnimationMaxFrme = 1;
        /// <summary>
        /// 挥舞索引
        /// </summary>
        public ref int SwingIndex => ref Owner.CWR().SwingIndex;
        /// <summary>
        /// 目标物品ID
        /// </summary>
        public virtual int TargetID => ItemID.None;
        /// <summary>
        /// 弹幕实体中心偏离值，默认为60
        /// </summary>
        public float Length = 60;
        /// <summary>
        /// 弹幕实体中心偏离值，默认为60，该属性不应该直接进行更新，仅仅用于还原
        /// </summary>
        public float OrigLength = 60;
        /// <summary>
        /// 旋转角度，默认为MathHelper.ToRadians(3)
        /// </summary>
        public float Rotation;
        /// <summary>
        /// 旋转速度
        /// </summary>
        protected float rotSpeed;
        /// <summary>
        /// 射击时间比例，默认为0.3f
        /// </summary>
        protected float shootSengs = 0.3f;
        /// <summary>
        /// 基本速度
        /// </summary>
        protected float speed;
        /// <summary>
        /// 一个挥舞周期的最大时间，默认为22
        /// </summary>
        protected int maxSwingTime = 22;
        /// /// <summary>
        /// 总时间，记录更新，在每帧的最后更新中自行加1
        /// </summary>
        protected int Time;
        /// <summary>
        /// 弧光的采样点数，默认为15 * <see cref="UpdateRate"/>
        /// </summary>
        protected int drawTrailCount = 15;
        /// <summary>
        /// 绝对中心距离玩家的距离，默认为75
        /// </summary>
        protected int distanceToOwner = 75;
        /// <summary>
        /// 弧光宽度，默认为50
        /// </summary>
        protected float drawTrailTopWidth = 50;
        /// <summary>
        /// 离心量的绘制矫正模长，默认为48
        /// </summary>
        protected float toProjCoreMode = 48;
        /// <summary>
        /// 弧光内宽度，默认为70
        /// </summary>
        protected float drawTrailBtommWidth = 70;
        /// <summary>
        /// 一个垂直于手臂的绘制矫正模长，默认为0
        /// </summary>
        protected float unitOffsetDrawZkMode;
        /// <summary>
        /// 挥舞速度乘算系数，等价于<see cref="SetSwingSpeed(float)"/>参数为1的返回值
        /// </summary>
        protected float SwingMultiplication;
        /// <summary>
        /// 历史旋转角度
        /// </summary>
        protected float[] oldRotate;
        /// <summary>
        /// 历史挥舞模长
        /// </summary>
        protected float[] oldLength;
        /// <summary>
        /// 历史离心距离
        /// </summary>
        protected float[] oldDistanceToOwner;
        /// <summary>
        /// 绘制刀光时是否应用高光渲染，默认为<see langword="false"/>
        /// </summary>
        protected bool drawTrailHighlight = false;
        /// <summary>
        /// 更新率，值为<see cref="Projectile.extraUpdates"/>+1，使用之前请注意<see cref="Projectile.extraUpdates"/>是否已经被正确设置
        /// </summary>
        internal int UpdateRate => Projectile.extraUpdates + 1;
        //不要直接设置这个
        private bool _canDrawSlashTrail;
        /// <summary>
        /// 是否绘制弧光，默认为<see langword="false"/>
        /// </summary>
        protected bool canDrawSlashTrail {
            get {
                if (!CWRServerConfig.Instance.EnableSwordLight) {
                    return false;
                }
                return _canDrawSlashTrail;
            }
            set => _canDrawSlashTrail = value;
        }
        /// <summary>
        /// 控制发射布尔值，在每帧更新末尾自动恢复成<see langword="false"/>
        /// </summary>
        protected bool canShoot;
        /// <summary>
        /// 是否跟随玩家进行朝向纠正，默认为<see langword="true"/>
        /// </summary>
        protected bool canFormOwnerSetDir = true;
        /// <summary>
        /// 玩家朝向将锁定到弹幕的初始朝向，默认为<see langword="false"/>，如果启用这个，那么<see cref="canFormOwnerSetDir"/>将不再具备意义
        /// </summary>
        protected bool ownerOrientationLock = false;
        /// <summary>
        /// 是否自动设置玩家手臂动作，默认为<see langword="true"/>
        /// </summary>
        protected bool canSetOwnerArmBver = true;
        /// <summary>
        /// 额外的矫正刀光采点角度的值，默认为0
        /// </summary>
        protected float overOffsetCachesRoting = 0;
        /// <summary>
        /// 发射射弹的速度模长，默认为6
        /// </summary>
        protected float ShootSpeed = 6f;
        /// <summary>
        /// 绘制的方向矫正值，默认为0
        /// </summary>
        protected float SwingDrawRotingOffset = 0;
        /// <summary>
        /// 这个刀会发射弹幕的ID
        /// </summary>
        protected int ShootID => Item.CWR().SetHeldSwingOrigShootID;
        /// <summary>
        /// 较为稳妥的获取一个正确的刀尖单位方向向量
        /// </summary>
        protected Vector2 safeInSwingUnit => GetOwnerCenter().To(Projectile.Center).UnitVector();
        /// <summary>
        /// 射弹基本速度，受攻速加成影响
        /// </summary>
        protected Vector2 ShootVelocity => UnitToMouseV * ShootSpeed / SwingMultiplication;
        /// <summary>
        /// 绝对的射弹基本速度，受攻速加成影响，其方向由弹幕到玩家的相对位置决定，一般用于长矛类等攻击模式的武器上
        /// </summary>
        protected Vector2 AbsolutelyShootVelocity => vector.UnitVector() * ShootSpeed / SwingMultiplication;
        /// <summary>
        /// 生成抛射物的位置
        /// </summary>
        protected Vector2 ShootSpanPos => GetOwnerCenter() + UnitToMouseV * Length * 0.5f;
        /// <summary>
        /// 射弹源
        /// </summary>
        protected IEntitySource Source => Owner.GetSource_ItemUse(Item);
        /// <summary>
        /// 绘制中是否进行对角线翻转
        /// </summary>
        protected bool inDrawFlipdiagonally;
        /// <summary>
        /// 刀光流形采样图路径
        /// </summary>
        public virtual string trailTexturePath => "";
        /// <summary>
        /// 颜色采样图路径
        /// </summary>
        public virtual string gradientTexturePath => "";
        /// <summary>
        /// 关于弧光绘制的流形图，制造刀光的纹路
        /// </summary>
        public Texture2D TrailTexture => SwingSystem.trailTextures[Type].Value;
        /// <summary>
        /// 着色纹理图，用于制造刀光的颜色
        /// </summary>
        public Texture2D GradientTexture => SwingSystem.gradientTextures[Type].Value;
        //不要试着使用这个属性，这个属性几乎是被废弃的，但我不知道其他的模组或者什么地方会用这个属性，进而可能会引发兼容性问题
        //public override string GlowTexture => base.GlowTexture;
        /// <summary>
        /// 用于获取刀光光效纹理的路径属性，默认为空字符串，即不启用
        /// </summary>
        public virtual string GlowTexturePath => "";
        /// <summary>
        /// 是否可以绘制光效遮罩，如果给<see cref="GlowTexturePath"/>重写了一个合适的路径，将会返回<see cref="true"/>，此时刀体将自动绘制光效遮罩
        /// </summary>
        public bool canDrawGlow { get; private set; }
        //光效遮罩纹理缓存，不要直接设置这个
        private Asset<Texture2D> glowTexValue;
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
        public struct SwingDataStruct
        {
            /// <summary>
            /// 默认值为33
            /// </summary>
            public float starArg = 33;
            /// <summary>
            /// 默认值为4
            /// </summary>
            public float baseSwingSpeed = 4;
            /// <summary>
            /// 默认值为0.08f
            /// </summary>
            public float ler1_UpLengthSengs = 0.08f;
            /// <summary>
            /// 默认值为0.1f
            /// </summary>
            public float ler1_UpSpeedSengs = 0.1f;
            /// <summary>
            /// 默认值为0.012f
            /// </summary>
            public float ler1_UpSizeSengs = 0.012f;
            /// <summary>
            /// 默认值为0.01f
            /// </summary>
            public float ler2_DownLengthSengs = 0.01f;
            /// <summary>
            /// 默认值为0.1f
            /// </summary>
            public float ler2_DownSpeedSengs = 0.1f;
            /// <summary>
            /// 默认值为0
            /// </summary>
            public float ler2_DownSizeSengs = 0;
            /// <summary>
            /// 默认值为0
            /// </summary>
            public int minClampLength = 0;
            /// <summary>
            /// 默认值为0
            /// </summary>
            public int maxClampLength = 0;
            /// <summary>
            /// 默认值为0
            /// </summary>
            public int ler1Time = 0;
            /// <summary>
            /// 默认值为0
            /// </summary>
            public int maxSwingTime = 0;
            /// <summary>
            /// 默认值为1
            /// </summary>
            public float overSpeedUpSengs = 1;
            public SwingDataStruct() { }
        }

        //===== Cinematic Fields Start =====
        //是否启用演出强化
        protected bool EnableCinematics = true;
        //是否启用残影
        protected bool EnableAfterImages = true;
        //残影缓存长度
        protected int AfterImageCacheLength = 7;
        //残影位置
        protected Vector2[] afterImagePos;
        //残影旋转
        protected float[] afterImageRot;
        //残影缩放
        protected float[] afterImageScale;
        //命中停顿计时
        protected int hitPauseTimer;
        //命中停顿持续帧数
        protected int hitPauseDurationOnHit = 4;
        //释放帧脉冲
        protected float cinematicPowerPulse;
        //脉冲衰减速度
        protected float cinematicPulseFade = 0.12f;
        //基础缩放帧缓存
        private float baseFrameScale;
        //全局屏幕震动时间
        protected static float globalShakeTime;
        //全局屏幕震动强度
        protected static float globalShakePower;
        //震动衰减
        protected static float globalShakeFade = 0.9f;
        //===== Cinematic Fields End =====
        #endregion
        public sealed override void SetDefaults() {
            if (!Main.dedServ) {
                canDrawGlow = SwingSystem.glowTextures.TryGetValue(Type, out glowTexValue);
            }
            Length = OrigLength;
            if (PreSetSwingProperty()) {
                Projectile.DamageType = DamageClass.Melee;
                Projectile.width = Projectile.height = 22;
                Projectile.tileCollide = false;
                Projectile.scale = 1f;
                Projectile.friendly = true;
                Projectile.penetrate = -1;
                Rotation = MathHelper.ToRadians(3);
                Projectile.CWR().NotSubjectToSpecialEffects = true;
                SetSwingProperty();
            }
            PostSwingProperty();
            LoadTrailCountData();
            OrigLength = Length;
            InitializeCinematicData();
        }

        #region Utils
        public void LoadTrailCountData() {
            drawTrailCount *= UpdateRate;
            oldRotate = new float[drawTrailCount];
            oldDistanceToOwner = new float[drawTrailCount];
            oldLength = new float[drawTrailCount];
            InitializeCaches();
        }

        public static Vector2 RodingToVer(float radius, float theta) => theta.ToRotationVector2() * radius;

        public float SetSwingSpeed(float speed) => speed / Owner.GetWeaponAttackSpeed(Item);
        /// <summary>
        /// 一个统一的获取玩家中心位置向量的方法，默认按照方法<see cref="CWRUtils.GetPlayerStabilityCenter"/>去返回值，
        /// 绘制使用一个独立的方法<see cref="GetInOwnerDrawOrigPosition"/>，但这个两个方法的默认逻辑是一样的
        /// </summary>
        /// <returns></returns>
        public Vector2 GetOwnerCenter() => Owner.GetPlayerStabilityCenter();

        protected virtual void InitializeCaches() {
            for (int j = drawTrailCount - 1; j >= 0; j--) {
                oldRotate[j] = 100f;
                oldDistanceToOwner[j] = distanceToOwner;
                oldLength[j] = (IgnoreImpactBoxSize ? 22 : Projectile.height) * Projectile.scale;
            }
        }

        public override void OrigItemShoot() {
            Item.shoot = ShootID;
            base.OrigItemShoot();
            Item.shoot = Type;
        }

        /// <summary>
        /// 模拟出一个勉强符合物理逻辑的命中粒子效果，最好不要动这些，这个效果是我凑出来的，我也不清楚这具体的数学逻辑，代码太乱了
        /// </summary>
        /// <param name="target"></param>
        /// <param name="sparkCount"></param>
        /// <param name="rotToTargetSpeedTrengsVumVer"></param>
        /// <param name="newSparkCount"></param>
        protected void HitEffectValue(Entity target, int sparkCount, out Vector2 rotToTargetSpeedTrengsVumVer, out int newSparkCount) {
            Vector2 toTarget = GetOwnerCenter().To(target.Center);
            Vector2 norlToTarget = toTarget.GetNormalVector();
            int ownerToTargetSetDir = Math.Sign(toTarget.X);
            ownerToTargetSetDir = ownerToTargetSetDir != DirSign ? -1 : 1;

            if (rotSpeed > 0) {
                norlToTarget *= -1;
            }
            if (rotSpeed < 0) {
                norlToTarget *= 1;
            }

            int pysCount = PRTLoader.PRT_IDToInGame_World_Count[PRTLoader.GetParticleID(typeof(PRT_Spark))];
            if (pysCount > 120) {
                sparkCount = 10;
            }
            if (pysCount > 220) {
                sparkCount = 8;
            }
            if (pysCount > 350) {
                sparkCount = 6;
            }
            if (pysCount > 500) {
                sparkCount = 3;
            }

            newSparkCount = sparkCount;

            float rotToTargetSpeedSengs = rotSpeed * 3 * ownerToTargetSetDir;
            rotToTargetSpeedTrengsVumVer = norlToTarget.RotatedBy(-rotToTargetSpeedSengs) * 13;
        }

        /// <summary>
        /// 实现刺击行为的逻辑处理，包括长度调整、速度衰减、缩放变化等效果
        /// </summary>
        /// <param name="initialLength">初始刺击长度</param>
        /// <param name="initialSpeedFactor">初始速度因子，用于计算刺击速度</param>
        /// <param name="speedDecayRate">速度衰减速率</param>
        /// <param name="lifetime">刺击的生命周期（帧数）</param>
        /// <param name="initialScale">刺击的初始缩放比例</param>
        /// <param name="scaleFactorDenominator">用于计算刺击缩放比例的分母</param>
        /// <param name="minLength">刺击长度的最小值</param>
        /// <param name="maxLength">刺击长度的最大值</param>
        public void StabBehavior(
            float initialLength = float.MaxValue,
            float initialSpeedFactor = 0.4f,
            float speedDecayRate = 0.015f,
            int lifetime = 0,
            float initialScale = 1,
            float scaleFactorDenominator = 510f,
            int minLength = 60,
            int maxLength = 90,
            bool canDrawSlashTrail = false,
            bool ignoreUpdateCount = false
        ) {
            if (lifetime == 0) {
                lifetime = maxSwingTime;
            }

            // 刺击行为的初始化逻辑
            if (Time == 0) {
                this.canDrawSlashTrail = canDrawSlashTrail;
                if (initialLength != float.MaxValue) {
                    Length = initialLength;
                }
                startVector = Projectile.velocity.UnitVector(); // 初始化方向向量
                speed = 1 + initialSpeedFactor / UpdateRate / SwingMultiplication; // 初始化速度因子
            }

            Length *= speed;
            vector = startVector * Length * SwingMultiplication; // 更新当前刺击的方向向量
            speed -= speedDecayRate / UpdateRate; // 减小速度因子，模拟速度衰减效果

            if (Time >= lifetime * UpdateRate * SwingMultiplication) {
                Projectile.Kill();
            }

            float distanceToOwner = Projectile.Center.To(Owner.Center).Length();
            Projectile.scale = initialScale + distanceToOwner / scaleFactorDenominator;
            if (Time % UpdateRate == UpdateRate - 1 || ignoreUpdateCount) {
                Length = MathHelper.Clamp(Length, minLength, maxLength);
            }
        }

        public virtual void SwingBehavior(float starArg = 33, float baseSwingSpeed = 4
            , float ler1_UpLengthSengs = 0.08f, float ler1_UpSpeedSengs = 0.1f, float ler1_UpSizeSengs = 0.012f
            , float ler2_DownLengthSengs = 0.01f, float ler2_DownSpeedSengs = 0.1f, float ler2_DownSizeSengs = 0
            , int minClampLength = 0, int maxClampLength = 0, int ler1Time = 0, int maxSwingTime = 0, float overSpeedUpSengs = 1) {
            if (minClampLength == 0) {
                minClampLength = (int)(OrigLength * 1.2f);
            }
            if (maxClampLength == 0) {
                maxClampLength = (int)(OrigLength * 1.4f);
            }
            if (maxSwingTime == 0) {
                maxSwingTime = this.maxSwingTime;
            }
            if (ler1Time == 0) {
                ler1Time = (int)(maxSwingTime * 0.4f);
            }

            float speedUp = SwingMultiplication;
            speedUp *= overSpeedUpSengs;

            if (Time == 0) {
                Rotation = MathHelper.ToRadians(starArg * -Owner.direction);
                startVector = RodingToVer(1, Projectile.velocity.ToRotation() - MathHelper.PiOver2 * Projectile.spriteDirection);
                speed = MathHelper.ToRadians(baseSwingSpeed) / speedUp;
            }
            if (Time < ler1Time * speedUp) {
                Length *= 1 + ler1_UpLengthSengs / UpdateRate;
                Rotation += speed * Projectile.spriteDirection;
                speed *= 1 + ler1_UpSpeedSengs / UpdateRate;
                vector = startVector.RotatedBy(Rotation) * Length;
                Projectile.scale += ler1_UpSizeSengs;
            }
            else {
                Length *= 1 - ler2_DownLengthSengs / UpdateRate;
                Rotation += speed * Projectile.spriteDirection;
                speed *= 1 - ler2_DownSpeedSengs / UpdateRate / speedUp;
                vector = startVector.RotatedBy(Rotation) * Length;
                Projectile.scale -= ler2_DownSizeSengs;
            }
            if (Time >= maxSwingTime * UpdateRate * speedUp) {
                Projectile.Kill();
            }
            if (Time % UpdateRate == UpdateRate - 1) {
                Length = MathHelper.Clamp(Length, minClampLength, maxClampLength);
            }
        }

        public virtual void SwingBehavior(SwingDataStruct swingData) =>
            SwingBehavior(
                swingData.starArg,
                swingData.baseSwingSpeed,
                swingData.ler1_UpLengthSengs,
                swingData.ler1_UpSpeedSengs,
                swingData.ler1_UpSizeSengs,
                swingData.ler2_DownLengthSengs,
                swingData.ler2_DownSpeedSengs,
                swingData.ler2_DownSizeSengs,
                swingData.minClampLength,
                swingData.maxClampLength,
                swingData.ler1Time,
                swingData.maxSwingTime,
                swingData.overSpeedUpSengs
            );

        #endregion
        public override bool ShouldUpdatePosition() => false;

        public virtual bool PreSetSwingProperty() { return true; }

        public virtual void SetSwingProperty() { }

        public virtual void PostSwingProperty() { }

        public virtual void Shoot() { }
        /// <summary>
        /// 处理一些与玩家相关的逻辑，比如跟随和初始化一些基本数据，运行在<see cref="SwingAI"/>之前
        /// </summary>
        public virtual void InOwner() {
            if (Time == 0) {
                dirs = Projectile.spriteDirection = Owner.direction;
            }

            Vector2 ownerCenter = GetOwnerCenter();

            Projectile.Calamity().timesPierced = 0;
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;
            Projectile.Center = ownerCenter + vector * (1f + (MeleeSize - 1f) / 2f);

            if (canFormOwnerSetDir) {
                Projectile.spriteDirection = Owner.direction;
            }
            if (canSetOwnerArmBver) {
                Owner.SetCompositeArmFront(true, Length >= 80 ?
                    Player.CompositeArmStretchAmount.Full : Player.CompositeArmStretchAmount.Quarter
                    , (ownerCenter - Projectile.Center).ToRotation() + MathHelper.PiOver2);
            }
            if (ownerOrientationLock) {
                Owner.direction = Projectile.spriteDirection = dirs;
            }

            float toOwnerRoding = (Projectile.Center - ownerCenter).ToRotation();
            Projectile.rotation = Projectile.spriteDirection == 1 ?
                toOwnerRoding + MathHelper.PiOver4 : toOwnerRoding - MathHelper.Pi - MathHelper.PiOver4;
        }

        public virtual bool PreInOwner() { return true; }
        public virtual void PostInOwner() { }
        public virtual void UpdateFrame() {
            if (AnimationMaxFrme > 1) {
                VaultUtils.ClockFrame(ref Projectile.frame, CuttingFrmeInterval, AnimationMaxFrme - 1);
            }
        }
        public virtual void NoServUpdate() {

        }

        public override void Initialize() {
            _meleeSize = 1f;
            if (Item.type != ItemID.None) {
                _meleeSize = Owner.GetAdjustedItemScale(Item);
            }
        }

        //===== Cinematic Methods Start =====
        //初始化演出相关
        protected virtual void InitializeCinematicData() {
            if (!EnableAfterImages) {
                return;
            }
            afterImagePos = new Vector2[AfterImageCacheLength];
            afterImageRot = new float[AfterImageCacheLength];
            afterImageScale = new float[AfterImageCacheLength];
            for (int i = 0; i < AfterImageCacheLength; i++) {
                afterImagePos[i] = Vector2.Zero;
                afterImageRot[i] = 0f;
                afterImageScale[i] = 1f;
            }
        }
        //更新残影
        protected virtual void UpdateAfterImages() {
            if (!EnableAfterImages) {
                return;
            }
            for (int i = AfterImageCacheLength - 1; i > 0; i--) {
                afterImagePos[i] = afterImagePos[i - 1];
                afterImageRot[i] = afterImageRot[i - 1];
                afterImageScale[i] = afterImageScale[i - 1];
            }
            afterImagePos[0] = Projectile.Center;
            afterImageRot[0] = Projectile.rotation;
            afterImageScale[0] = Projectile.scale * MeleeSize;
        }
        //动力曲线，返回0-1
        protected float EaseOutBack(float t) {
            float c1 = 1.70158f;
            float c3 = c1 + 1f;
            return 1 + c3 * (float)Math.Pow(t - 1, 3) + c1 * (float)Math.Pow(t - 1, 2);
        }
        //更新演出曲线
        protected virtual void ApplyCinematicCurves() {
            if (!EnableCinematics) {
                return;
            }
            float total = maxSwingTime * UpdateRate * SwingMultiplication;
            if (total <= 0) {
                return;
            }
            float norm = MathHelper.Clamp(Time / total, 0f, 1f);
            float accel = norm < 0.5f ? 4f * norm * norm * norm : 1f - (float)Math.Pow(-2f * norm + 2f, 3f) / 2f;
            float back = EaseOutBack(MathHelper.Clamp(norm * 1.15f, 0f, 1f));
            float pulse = cinematicPowerPulse;
            cinematicPowerPulse *= (1f - cinematicPulseFade * 0.5f);
            if (cinematicPowerPulse < 0.01f) {
                cinematicPowerPulse = 0f;
            }
            float scalePulse = 1f + 0.002f * back + 0.008f * pulse;
            Projectile.scale = baseFrameScale * scalePulse;
            oldLengthOffsetSizeValue = 1f + 0.25f * accel + 0.35f * pulse;
            drawTrailTopWidth = 50f * (1f + 0.35f * accel + 0.4f * pulse);
            drawTrailBtommWidth = 70f * (1f + 0.15f * accel);
            if (pulse > 0.05f) {
                drawTrailHighlight = true;
            }
            else {
                drawTrailHighlight = false;
            }
        }
        //触发释放脉冲
        protected void TriggerReleasePulse(float power = 1f) {
            cinematicPowerPulse = MathF.Max(cinematicPowerPulse, power);
            TriggerShake(5f * power, 8f * power);
        }
        //命中停顿
        protected void TriggerHitPause() {
            if (hitPauseTimer < hitPauseDurationOnHit) {
                hitPauseTimer = hitPauseDurationOnHit;
            }
        }
        //屏幕震动
        protected void TriggerShake(float power, float time) {
            if (power <= 0 || time <= 0) {
                return;
            }
            globalShakePower = Math.Max(globalShakePower, power);
            globalShakeTime = Math.Max(globalShakeTime, time);
        }
        //在预绘制之前应用震动(仅本地玩家)
        protected void ApplyCameraShake() {
            if (globalShakeTime > 0f && Main.myPlayer == Owner.whoAmI) {
                float progress = globalShakeTime / Math.Max(globalShakePower, 0.0001f);
                float intensity = globalShakePower * progress;
                Vector2 shake = new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 1f));
                if (shake.LengthSquared() > 1f) {
                    shake.Normalize();
                }
                Main.screenPosition += shake * intensity;
            }
        }
        //在更新末端衰减震动
        protected void UpdateShakeDecay() {
            if (globalShakeTime > 0f) {
                globalShakeTime *= globalShakeFade;
                if (globalShakeTime < 0.2f) {
                    globalShakeTime = 0f;
                    globalShakePower = 0f;
                }
            }
        }
        //===== Cinematic Methods End =====

        /// <summary>
        /// 几乎所有的逻辑更新都在这里进行
        /// </summary>
        /// <returns></returns>
        public sealed override bool PreUpdate() {
            bool freeze = false;
            if (hitPauseTimer > 0) {
                hitPauseTimer--;
                freeze = true;
            }
            SwingMultiplication = SetSwingSpeed(1f);
            canShoot = Time == (int)(maxSwingTime * shootSengs * SwingMultiplication * UpdateRate);
            baseFrameScale = Projectile.scale;//记录供演出缩放
            if (!freeze) {
                if (PreInOwner()) {
                    InOwner();
                    SwingAI();
                    if (Projectile.IsOwnedByLocalPlayer() && canShoot) {
                        Shoot();
                        if (GlobalItemBehavior) {
                            foreach (var g in ItemRebuildLoader.ItemLoader_Shoot_Hook.Enumerate(Item)) {
                                g.Shoot(Item, Owner, new EntitySource_ItemUse_WithAmmo(Owner, Item, ShootID), ShootSpanPos, ShootVelocity, ShootID, Projectile.damage, Projectile.knockBack);
                            }
                        }
                        TriggerReleasePulse(1f);//释放瞬间脉冲
                    }
                    if (canDrawSlashTrail) {
                        UpdateCaches();
                    }
                }
                PostInOwner();
                UpdateFrame();
                if (!VaultUtils.isServer) {
                    NoServUpdate();
                }
                UpdateAfterImages();
                Time++;//正常推进时间
            }
            else {
                //命中停顿时仍需保持在玩家手中位置
                InOwner();
            }
            ApplyCinematicCurves();
            rotSpeed = Rotation - oldRot;
            oldRot = Rotation;
            canShoot = false;
            UpdateShakeDecay();
            return false;
        }
        /// <summary>
        /// 用这个函数来处理挥舞相关的逻辑更新，运行在<see cref="InOwner"/>之后，<see cref="UpdateCaches"/>之前
        /// </summary>
        public virtual void SwingAI() {

        }
        /// <summary>
        /// 暂时弃用的
        /// </summary>
        public sealed override void AI() { }

        public override void ModifyDamageHitbox(ref Rectangle hitbox) {
            Vector2 recCenter = hitbox.TopLeft() + hitbox.Size() / 2;
            int wid = (int)(hitbox.Width * MeleeSize);
            int hig = (int)(hitbox.Height * MeleeSize);
            Vector2 newRecPos = recCenter - new Vector2(wid / 2, hig / 2);
            Rectangle newRec = new Rectangle((int)newRecPos.X, (int)newRecPos.Y, wid, hig);
            hitbox = newRec;
        }

        public sealed override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = safeInSwingUnit.X > 0 ? 1 : -1;
            if (target.IsWormBody()) {
                if (Projectile.DamageType != ModContent.GetInstance<TrueMeleeDamageClass>()
                    && Projectile.DamageType != ModContent.GetInstance<TrueMeleeNoSpeedDamageClass>()) {
                    modifiers.FinalDamage /= 2;
                }
                modifiers.FinalDamage *= inWormBodysDamageFaul;
            }
            SwingModifyHitNPC(target, ref modifiers);
        }

        public sealed override bool? CanHitNPC(NPC target) {
            if (!HitIgnoreTile && !Collision.CanHit(GetOwnerCenter(), 1, 1, target.Center, 1, 1)) {
                return false;
            }
            return null;
        }

        public virtual void SwingModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {

        }

        #region Draw
        /// <summary>
        /// 获取刀光绘制中心，这个的返回值不会影响逻辑位置
        /// </summary>
        /// <returns></returns>
        public virtual Vector2 GetInOwnerDrawOrigPosition() => Owner.GetPlayerStabilityCenter();

        public virtual void WarpDraw() {
            List<ColoredVertex> bars = [];
            GetCurrentTrailCount(out float count);

            float w = 1f;
            for (int i = 0; i < count; i++) {
                if (oldRotate[i] == 100f) {
                    continue;
                }

                Vector2 Center = GetInOwnerDrawOrigPosition();
                float factor = 1f - i / count;
                float rotate = oldRotate[i] % MathHelper.TwoPi;
                float twistOrientation = (rotate >= MathHelper.Pi ? rotate - MathHelper.Pi : rotate + MathHelper.Pi) / MathHelper.TwoPi;

                Vector2 Top = Center + oldRotate[i].ToRotationVector2() *
                    (oldLength[i] + drawTrailTopWidth * meleeSizeAsymptotic + oldDistanceToOwner[i]) * meleeSizeAsymptotic;
                Vector2 Bottom = Center + oldRotate[i].ToRotationVector2() *
                    (oldLength[i] - ControlTrailBottomWidth(factor) + oldDistanceToOwner[i]) * meleeSizeAsymptotic;

                bars.Add(new ColoredVertex(Top, new Color(twistOrientation, w, 0f, 25), new Vector3(factor, 0f, w)));
                bars.Add(new ColoredVertex(Bottom, new Color(twistOrientation, w, 0f, 25), new Vector3(factor, 1f, w)));
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied, SamplerState.PointWrap
                , DepthStencilState.Default, RasterizerState.CullNone);

            Matrix projection = Matrix.CreateOrthographicOffCenter(0f, Main.screenWidth, Main.screenHeight, 0f, 0f, 1f);
            Matrix model = Matrix.CreateTranslation(new Vector3(-Main.screenPosition.X, -Main.screenPosition.Y, 0f))
                * Main.GameViewMatrix.TransformationMatrix;
            Effect effect = EffectLoader.KnifeDistortion.Value;
            effect.Parameters["uTransform"].SetValue(model * projection);
            Main.graphics.GraphicsDevice.Textures[0] = TrailTexture;
            Main.graphics.GraphicsDevice.SamplerStates[0] = SamplerState.PointWrap;
            effect.CurrentTechnique.Passes[0].Apply();
            if (bars.Count >= 3) {
                Main.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars.ToArray(), 0, bars.Count - 2);
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(0, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None
                , RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        public virtual void GetCurrentTrailCount(out float count) {
            count = 0f;
            if (oldRotate == null) {
                return;
            }

            for (int i = 0; i < oldRotate.Length; i++) {
                if (oldRotate[i] != 100f) {
                    count += 1f;
                }
            }
        }
        /// <summary>
        /// 在逻辑帧<see cref="PreUpdate"/>中被最后调用，用于更新弧光相关的点数据
        /// </summary>
        public virtual void UpdateCaches() {
            if (Time < 2) {
                return;
            }

            for (int i = drawTrailCount - 1; i > 0; i--) {
                oldRotate[i] = oldRotate[i - 1];
                oldDistanceToOwner[i] = oldDistanceToOwner[i - 1];
                oldLength[i] = oldLength[i - 1];
            }

            oldRotate[0] = (Projectile.Center - GetOwnerCenter()).ToRotation() + overOffsetCachesRoting * Math.Sign(rotSpeed);
            oldDistanceToOwner[0] = distanceToOwner;
            oldLength[0] = (IgnoreImpactBoxSize ? 22 : Projectile.height) * Projectile.scale * oldLengthOffsetSizeValue;
        }

        public void DrawTrailHander(List<VertexPositionColorTexture> bars, GraphicsDevice device, BlendState blendState = null
            , SamplerState samplerState = null, RasterizerState rasterizerState = null) {
            RasterizerState originalState = Main.graphics.GraphicsDevice.RasterizerState;
            BlendState originalBlendState = Main.graphics.GraphicsDevice.BlendState;
            SamplerState originalSamplerState = Main.graphics.GraphicsDevice.SamplerStates[0];

            device.BlendState = blendState ?? originalBlendState;
            device.SamplerStates[0] = samplerState ?? originalSamplerState;
            device.RasterizerState = rasterizerState ?? originalState;

            DrawTrail(bars);

            device.RasterizerState = originalState;
            device.BlendState = originalBlendState;
            device.SamplerStates[0] = originalSamplerState;
            Main.pixelShader.CurrentTechnique.Passes[0].Apply();
        }

        public virtual void DrawTrail(List<VertexPositionColorTexture> bars) {
            Effect effect = EffectLoader.KnifeRendering.Value;

            effect.Parameters["transformMatrix"].SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["drawTrailHighlight"].SetValue(drawTrailHighlight);
            effect.Parameters["obliqueSampling"].SetValue(ObliqueSampling);
            effect.Parameters["sampleTexture"].SetValue(TrailTexture);
            effect.Parameters["gradientTexture"].SetValue(GradientTexture);
            //应用shader，并绘制顶点
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                Main.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars.ToArray(), 0, bars.Count - 2);
                Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
                Main.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars.ToArray(), 0, bars.Count - 2);
            }
        }

        public virtual float ControlTrailBottomWidth(float factor) {
            return drawTrailBtommWidth * Projectile.scale * (0.4f + 0.6f * (float)Math.Sin(MathHelper.Pi * factor));
        }

        public virtual void DrawSlashTrail() {
            List<VertexPositionColorTexture> bars = [];
            GetCurrentTrailCount(out float count);

            for (int i = 0; i < count; i++) {
                if (oldRotate[i] == 100f) {
                    continue;
                }

                float factor = 1f - i / count;
                Vector2 Center = GetInOwnerDrawOrigPosition();
                Vector2 Top = Center + oldRotate[i].ToRotationVector2() * (oldLength[i] + drawTrailTopWidth * meleeSizeAsymptotic + oldDistanceToOwner[i]) * meleeSizeAsymptotic;
                Vector2 Bottom = Center + oldRotate[i].ToRotationVector2() * (oldLength[i] - ControlTrailBottomWidth(factor) + oldDistanceToOwner[i]) * meleeSizeAsymptotic;

                float pulse = cinematicPowerPulse;
                Color topA = new Color(238, 218, 130);
                Color topB = new Color(167, 127, 95);
                Color bottomA = new Color(109, 73, 86);
                Color bottomB = new Color(83, 16, 85);
                float glowLerp = 0.35f + 0.65f * factor + 0.5f * pulse;
                Color topColor = Color.Lerp(topA, topB, 1 - factor) * glowLerp;
                Color bottomColor = Color.Lerp(bottomA, bottomB, 1 - factor) * glowLerp;
                bars.Add(new VertexPositionColorTexture(Top.ToVector3(), topColor, new Vector2(factor, 0)));
                bars.Add(new VertexPositionColorTexture(Bottom.ToVector3(), bottomColor, new Vector2(factor, 1)));
            }

            if (bars.Count > 2) {
                DrawTrailHander(bars, Main.graphics.GraphicsDevice, BlendState.NonPremultiplied, SamplerState.PointWrap, RasterizerState.CullNone);

                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        //绘制残影
        protected virtual void DrawAfterImages(SpriteBatch spriteBatch, Color lightColor) {
            if (!EnableAfterImages || afterImagePos == null) {
                return;
            }

            float total = maxSwingTime * UpdateRate * SwingMultiplication;
            float norm = MathHelper.Clamp(Time / total, 0f, 1f);
            if (norm < 0.1f || norm > 0.9f) {
                return;
            }

            Texture2D texture = TextureValue;
            Rectangle rect = texture.GetRectangle(Projectile.frame, AnimationMaxFrme);
            Vector2 drawOrigin = rect.Size() / 2;
            SpriteEffects effects = Projectile.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            for (int i = 1; i < AfterImageCacheLength; i++) {
                if (afterImageScale[i] <= 0f) {
                    continue;
                }

                Vector2 offsetOwnerPos = safeInSwingUnit.GetNormalVector() * unitOffsetDrawZkMode * Projectile.spriteDirection * MeleeSize;
                float drawRoting = afterImageRot[i];

                float otherRoting = SwingDrawRotingOffset;
                if (Projectile.spriteDirection == -1) {
                    drawRoting += MathHelper.Pi;
                    otherRoting *= -1;
                }
                //烦人的对角线翻转代码，我凑出来了这个效果，它很稳靠，但我仍旧不想细究这其中的数学逻辑
                if (inDrawFlipdiagonally) {
                    effects = Projectile.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                    drawRoting += MathHelper.PiOver2;
                    otherRoting *= -1;
                    offsetOwnerPos *= -1;
                }

                float fade = 1f - i / (float)AfterImageCacheLength;
                Color clr = new Color(255, 230, 180, 0) * fade * 0.55f;

                Vector2 drawPosValue = afterImagePos[i] - RodingToVer(toProjCoreMode, (Projectile.Center - GetOwnerCenter()).ToRotation()) + offsetOwnerPos;
                Vector2 trueDrawPos = drawPosValue - Main.screenPosition + Vector2.UnitY * Projectile.gfxOffY;

                spriteBatch.Draw(texture, trueDrawPos, rect, clr, drawRoting + otherRoting, drawOrigin, afterImageScale[i], effects, 0f);
            }
        }

        public virtual void DrawSwing(SpriteBatch spriteBatch, Color lightColor) {
            Texture2D texture = TextureValue;
            Rectangle rect = texture.GetRectangle(Projectile.frame, AnimationMaxFrme);
            Vector2 drawOrigin = rect.Size() / 2;
            SpriteEffects effects = Projectile.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            Vector2 offsetOwnerPos = safeInSwingUnit.GetNormalVector() * unitOffsetDrawZkMode * Projectile.spriteDirection * MeleeSize;
            float drawRoting = Projectile.rotation;
            float otherRoting = SwingDrawRotingOffset;
            if (Projectile.spriteDirection == -1) {
                drawRoting += MathHelper.Pi;
                otherRoting *= -1;
            }
            //烦人的对角线翻转代码，我凑出来了这个效果，它很稳靠，但我仍旧不想细究这其中的数学逻辑
            if (inDrawFlipdiagonally) {
                effects = Projectile.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                drawRoting += MathHelper.PiOver2;
                otherRoting *= -1;
                offsetOwnerPos *= -1;
            }
            Color color = Projectile.GetAlpha(lightColor);
            if (Incandescence) {
                color = Color.White;
            }
            float shine = 1f + cinematicPowerPulse * 0.6f;
            color = color * shine;

            Vector2 drawPosValue = Projectile.Center - RodingToVer(toProjCoreMode, (Projectile.Center - GetOwnerCenter()).ToRotation()) + offsetOwnerPos;
            Vector2 trueDrawPos = drawPosValue - Main.screenPosition + Vector2.UnitY * Projectile.gfxOffY;

            Main.EntitySpriteDraw(texture, trueDrawPos, new Rectangle?(rect)
                , color, drawRoting + otherRoting, drawOrigin, Projectile.scale * MeleeSize, effects, 0);
            if (canDrawGlow) {
                Main.EntitySpriteDraw(glowTexValue.Value, trueDrawPos, new Rectangle?(rect)
                    , Color.White * shine, drawRoting + otherRoting, drawOrigin, Projectile.scale * MeleeSize, effects, 0);
            }
        }

        public sealed override bool PreDraw(ref Color lightColor) {
            ApplyCameraShake();
            if (canDrawSlashTrail) {
                DrawSlashTrail();
            }
            if (Item.Alives()) {
                Projectile.BeginDyeEffectForWorld(Item.CWR().DyeItemID);
            }
            DrawAfterImages(Main.spriteBatch, lightColor);
            DrawSwing(Main.spriteBatch, lightColor);
            if (Item.Alives()) {
                Projectile.EndDyeEffectForWorld();
            }
            return false;
        }
        #endregion
    }

    internal abstract class BaseKnife : BaseSwing
    {
        public override string Texture => CWRConstant.Placeholder3;
        public override Texture2D TextureValue => TargetID == ItemID.None ? TextureAssets.Projectile[Type].Value : TextureAssets.Item[TargetID].Value;
        public SwingDataStruct SwingData = new SwingDataStruct();
        public SwingAITypeEnum SwingAIType;
        protected bool autoSetShoot;
        protected bool onSound;
        protected Dictionary<int, NPC> onHitNPCs = [];
        public enum SwingAITypeEnum
        {
            None = 0,
            UpAndDown,
            Down,
            Sceptre,
        }
        public sealed override void SetSwingProperty() {
            Projectile.extraUpdates = 4;
            ownerOrientationLock = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 14 * UpdateRate;
            Projectile.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            SetKnifeProperty();
            VaultUtils.SafeLoadItem(TargetID);
        }

        protected void updaTrailTexture() => SwingSystem.trailTextures[Type] = CWRUtils.GetT2DAsset(trailTexturePath);
        protected void updaGradientTexture() => SwingSystem.gradientTextures[Type] = CWRUtils.GetT2DAsset(gradientTexturePath);

        public virtual void SetKnifeProperty() { }

        public sealed override void Initialize() {
            base.Initialize();
            Projectile.DamageType = Item.DamageType;
            maxSwingTime = Item.useTime;
            SwingData.maxSwingTime = maxSwingTime;
            toProjCoreMode = (IgnoreImpactBoxSize ? 22 : Projectile.width) / 2f;
            if (autoSetShoot) {
                ShootSpeed = Item.shootSpeed;
            }
            if (++SwingIndex > 1) {
                SwingIndex = 0;
            }
            KnifeInitialize();
        }

        public virtual void KnifeInitialize() {

        }

        public virtual void WaveUADBehavior() {

        }

        public virtual void SceptreBehavior() {

        }

        public virtual void MeleeEffect() {

        }

        public virtual bool PreSwingAI() {
            return true;
        }

        public sealed override void SwingAI() {
            if (!PreSwingAI()) {
                return;
            }
            switch (SwingAIType) {
                case SwingAITypeEnum.None:
                    SwingBehavior(SwingData);
                    break;
                case SwingAITypeEnum.UpAndDown:
                    SwingDataStruct swingData = SwingData;
                    if (SwingIndex == 1) {
                        inDrawFlipdiagonally = true;
                        swingData.starArg += 120;
                        swingData.baseSwingSpeed *= -1;
                    }
                    WaveUADBehavior();
                    SwingBehavior(swingData);
                    break;
                case SwingAITypeEnum.Down:
                    inDrawFlipdiagonally = true;
                    SwingData.starArg += 120;
                    SwingData.baseSwingSpeed *= -1;
                    SwingBehavior(SwingData);
                    break;
                case SwingAITypeEnum.Sceptre:
                    shootSengs = 0.95f;
                    maxSwingTime = 70;
                    canDrawSlashTrail = false;
                    SwingData.starArg = 13;
                    SwingData.baseSwingSpeed = 2;
                    SwingData.ler1_UpLengthSengs = 0.1f;
                    SwingData.ler1_UpSpeedSengs = 0.1f;
                    SwingData.ler1_UpSizeSengs = 0.062f;
                    SwingData.ler2_DownLengthSengs = 0.01f;
                    SwingData.ler2_DownSpeedSengs = 0.14f;
                    SwingData.ler2_DownSizeSengs = 0;
                    SwingData.minClampLength = 160;
                    SwingData.maxClampLength = 200;
                    SwingData.ler1Time = 8;
                    SwingData.maxSwingTime = 60;
                    SceptreBehavior();
                    SwingBehavior(SwingData);
                    break;
            }
        }

        public sealed override void NoServUpdate() {
            if (Time % UpdateRate == 0) {
                MeleeEffect();
            }
        }

        public sealed override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (onHitNPCs.TryAdd(target.whoAmI, target)) {
                TriggerHitPause();
                TriggerShake(3f, 6f);
                KnifeHitNPC(target, hit, damageDone);
            }
        }
        /// <summary>
        /// 在刀刃击中NPC时运行
        /// </summary>
        /// <param name="target"></param>
        /// <param name="hit"></param>
        /// <param name="damageDone"></param>
        public virtual void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        }

        /// <summary>
        /// 执行适配的大幅度挥击动作，控制挥击速度、尺寸变化及音效播放
        /// </summary>
        /// <param name="initialMeleeSize">初始的近战武器大小倍率</param>
        /// <param name="phase1Ratio">第一阶段持续时间占总挥击时间的比例</param>
        /// <param name="phase2Ratio">第二阶段持续时间占总挥击时间的比例</param>
        /// <param name="phase0SwingSpeed">第一阶段的基础挥击速度（负值表示回收动作）</param>
        /// <param name="phase1SwingSpeed">第二阶段的基础挥击速度</param>
        /// <param name="phase2SwingSpeed">第三阶段的基础挥击速度</param>
        /// <param name="phase0MeleeSizeIncrement">第一阶段每帧增加的武器尺寸倍率</param>
        /// <param name="phase2MeleeSizeIncrement">第三阶段每帧减少的武器尺寸倍率</param>
        /// <param name="swingSound">挥击音效的样式，默认为<see cref="SoundID.item1"/></param>
        public void ExecuteAdaptiveSwing(
            float initialMeleeSize = 0.84f,
            float phase1Ratio = 0.5f,
            float phase2Ratio = 0.6f,
            float phase0SwingSpeed = -0.3f,
            float phase1SwingSpeed = 4.2f,
            float phase2SwingSpeed = 9f,
            float phase0MeleeSizeIncrement = 0.002f,
            float phase2MeleeSizeIncrement = -0.002f,
            SoundStyle swingSound = default,
            bool drawSlash = true) {
            // 初始化时间为0时设置初始武器大小
            if (Time == 0) {
                OtherMeleeSize = initialMeleeSize;
            }

            if (SwingAIType == SwingAITypeEnum.UpAndDown && SwingIndex == 1) {
                phase0SwingSpeed *= -1;
                phase1SwingSpeed *= -1;
                phase2SwingSpeed *= -1;
            }

            // 计算当前挥击速度比例
            float swingSpeedMultiplier = SwingMultiplication;

            // 计算各阶段的结束时间
            int phase1EndTime = (int)(maxSwingTime * phase1Ratio * UpdateRate * swingSpeedMultiplier);
            int phase2EndTime = (int)(maxSwingTime * phase2Ratio * UpdateRate * swingSpeedMultiplier);

            // 第二阶段逻辑：主挥击阶段
            if (Time > phase1EndTime) {
                if (!onSound) {
                    // 播放挥击音效
                    SoundEngine.PlaySound(swingSound == default ? SoundID.Item1 : swingSound, Owner.Center);
                    onSound = true;
                }

                // 启用挥击轨迹绘制
                canDrawSlashTrail = drawSlash;

                // 设置第二阶段的挥击速度
                SwingData.baseSwingSpeed = phase1SwingSpeed;

                // 在进入第二阶段的第一帧计算具体挥击速度
                if (Time == phase1EndTime + 1) {
                    speed = MathHelper.ToRadians(SwingData.baseSwingSpeed) / swingSpeedMultiplier;
                }
            }
            // 第一阶段逻辑：准备挥击阶段
            else {
                // 增大武器尺寸
                OtherMeleeSize += phase0MeleeSizeIncrement;

                // 设置第一阶段的挥击速度
                SwingData.baseSwingSpeed = phase0SwingSpeed;

                // 计算挥击速度
                speed = MathHelper.ToRadians(SwingData.baseSwingSpeed) / swingSpeedMultiplier;

                // 禁用挥击轨迹绘制
                canDrawSlashTrail = false;
            }

            // 第三阶段逻辑：挥击结束阶段
            if (Time > phase2EndTime) {
                // 缩小武器尺寸
                OtherMeleeSize += phase2MeleeSizeIncrement;

                // 设置第三阶段的挥击速度
                SwingData.baseSwingSpeed = phase2SwingSpeed;

                // 在进入第三阶段的第一帧计算具体挥击速度
                if (Time == phase2EndTime + 1) {
                    speed = MathHelper.ToRadians(SwingData.baseSwingSpeed) / swingSpeedMultiplier;
                }
            }
        }
    }
}
