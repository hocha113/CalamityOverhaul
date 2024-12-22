using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Particles;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core
{
    internal abstract class BaseSwing : BaseHeldProj
    {
        #region Data
        private int dirs;
        private float oldRot;
        private float _meleeSize = 0;
        private bool isInitialize;
        protected Vector2 vector;
        protected Vector2 startVector;
        protected float inWormBodysDamageFaul = 0.85f;
        /// <summary>
        /// 主纹理资源
        /// </summary>
        public virtual Texture2D TextureValue => CWRUtils.GetT2DValue(Texture);
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
        public float MeleeSize => AffectedMeleeSize ? (_meleeSize * OtherMeleeSize) : 1f;
        /// <summary>
        /// 近战缩放对应的渐进中间值
        /// </summary>
        protected float meleeSizeAsymptotic => (1 + (MeleeSize - 1f) / 2f);
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
        /// 这个手持刀对应的物品实例
        /// </summary>
        public Item Item => Owner.GetItem();
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
        /// 弧光的采样点数，默认为15 * <see cref="updateCount"/>
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
        /// 绘制刀光时是否应用高光渲染，默认为<see langword="true"/>
        /// </summary>
        protected bool drawTrailHighlight = true;
        /// <summary>
        /// 更新率，值为<see cref="Projectile.extraUpdates"/>+1，使用之前请注意<see cref="Projectile.extraUpdates"/>是否已经被正确设置
        /// </summary>
        internal int updateCount => Projectile.extraUpdates + 1;
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
        /// 较为稳妥的获取一个正确的刀尖单位方向向量
        /// </summary>
        protected Vector2 safeInSwingUnit => GetOwnerCenter().To(Projectile.Center).UnitVector();
        /// <summary>
        /// 射弹基本速度，受攻速加成影响
        /// </summary>
        protected Vector2 ShootVelocity => UnitToMouseV * ShootSpeed / SwingMultiplication;
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
        }

        #region Utils
        public void LoadTrailCountData() {
            drawTrailCount *= updateCount;
            oldRotate = new float[drawTrailCount];
            oldDistanceToOwner = new float[drawTrailCount];
            oldLength = new float[drawTrailCount];
            InitializeCaches();
        }

        public Vector2 RodingToVer(float radius, float theta) => theta.ToRotationVector2() * radius;

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
                Length *= 1 + ler1_UpLengthSengs / updateCount;
                Rotation += speed * Projectile.spriteDirection;
                speed *= 1 + ler1_UpSpeedSengs / updateCount;
                vector = startVector.RotatedBy(Rotation) * Length;
                Projectile.scale += ler1_UpSizeSengs;
            }
            else {
                Length *= 1 - ler2_DownLengthSengs / updateCount;
                Rotation += speed * Projectile.spriteDirection;
                speed *= 1 - ler2_DownSpeedSengs / updateCount / speedUp;
                vector = startVector.RotatedBy(Rotation) * Length;
                Projectile.scale -= ler2_DownSizeSengs;
            }
            if (Time >= maxSwingTime * updateCount * speedUp) {
                Projectile.Kill();
            }
            if (Time % updateCount == updateCount - 1) {
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

        public virtual bool PreInOwnerUpdate() { return true; }
        public virtual void PostInOwnerUpdate() { }
        public virtual void Initialize() { }
        public virtual void UpdateFrame() {
            if (AnimationMaxFrme > 1) {
                CWRUtils.ClockFrame(ref Projectile.frame, CuttingFrmeInterval, AnimationMaxFrme - 1);
            }
        }
        public virtual void NoServUpdate() {

        }

        /// <summary>
        /// 几乎所有的逻辑更新都在这里进行
        /// </summary>
        /// <returns></returns>
        public sealed override bool PreUpdate() {
            SwingMultiplication = SetSwingSpeed(1f);
            canShoot = Time == (int)(maxSwingTime * shootSengs * SwingMultiplication * updateCount);
            if (!isInitialize) {
                _meleeSize = 1f;
                if (Item.type != ItemID.None) {
                    _meleeSize = Owner.GetAdjustedItemScale(Item);
                }
                Initialize();
                isInitialize = true;
            }
            if (PreInOwnerUpdate()) {
                InOwner();
                SwingAI();
                if (Projectile.IsOwnedByLocalPlayer() && canShoot) {
                    Shoot();
                }
                if (canDrawSlashTrail) {
                    UpdateCaches();
                }
            }
            PostInOwnerUpdate();
            UpdateFrame();
            if (!VaultUtils.isServer) {
                NoServUpdate();
            }
            rotSpeed = Rotation - oldRot;
            oldRot = Rotation;
            canShoot = false;
            Time++;
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

                Vector2 Top = Center + (oldRotate[i].ToRotationVector2() *
                    (oldLength[i] + drawTrailTopWidth * meleeSizeAsymptotic + oldDistanceToOwner[i])) * meleeSizeAsymptotic;
                Vector2 Bottom = Center + (oldRotate[i].ToRotationVector2() *
                    (oldLength[i] - ControlTrailBottomWidth(factor) + oldDistanceToOwner[i])) * meleeSizeAsymptotic;

                bars.Add(new ColoredVertex(Top, new Color(twistOrientation, w, 0f, 25), new Vector3(factor, 0f, w)));
                bars.Add(new ColoredVertex(Bottom, new Color(twistOrientation, w, 0f, 25), new Vector3(factor, 1f, w)));
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied, SamplerState.PointWrap
                , DepthStencilState.Default, RasterizerState.CullNone);

            Matrix projection = Matrix.CreateOrthographicOffCenter(0f, Main.screenWidth, Main.screenHeight, 0f, 0f, 1f);
            Matrix model = Matrix.CreateTranslation(new Vector3(-Main.screenPosition.X, -Main.screenPosition.Y, 0f))
                * Main.GameViewMatrix.TransformationMatrix;
            Effect effect = CWRUtils.GetEffectValue("KnifeDistortion");
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
            Effect effect = CWRUtils.GetEffectValue("KnifeRendering");

            effect.Parameters["transformMatrix"].SetValue(GetTransfromMaxrix());
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

        public virtual Matrix GetTransfromMaxrix() {
            Matrix world = Matrix.CreateTranslation(-Main.screenPosition.Vec3());
            Matrix view = Main.GameViewMatrix.TransformationMatrix;
            Matrix projection = Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1, 1);
            return world * view * projection;
        }

        public virtual float ControlTrailBottomWidth(float factor) {
            return drawTrailBtommWidth * Projectile.scale;
        }

        public virtual void DrawSlashTrail() {
            List<VertexPositionColorTexture> bars = [];
            GetCurrentTrailCount(out float count);

            for (int i = 0; i < count; i++) {
                if (oldRotate[i] == 100f)
                    continue;

                float factor = 1f - i / count;
                Vector2 Center = GetInOwnerDrawOrigPosition();
                Vector2 Top = Center + (oldRotate[i].ToRotationVector2() * (oldLength[i] + drawTrailTopWidth * meleeSizeAsymptotic + oldDistanceToOwner[i])) * meleeSizeAsymptotic;
                Vector2 Bottom = Center + (oldRotate[i].ToRotationVector2() * (oldLength[i] - ControlTrailBottomWidth(factor) + oldDistanceToOwner[i])) * meleeSizeAsymptotic;

                var topColor = Color.Lerp(new Color(238, 218, 130, 200), new Color(167, 127, 95, 0), 1 - factor);
                var bottomColor = Color.Lerp(new Color(109, 73, 86, 200), new Color(83, 16, 85, 0), 1 - factor);
                bars.Add(new VertexPositionColorTexture(Top.Vec3(), topColor, new Vector2(factor, 0)));
                bars.Add(new VertexPositionColorTexture(Bottom.Vec3(), bottomColor, new Vector2(factor, 1)));
            }

            if (bars.Count > 2) {
                DrawTrailHander(bars, Main.graphics.GraphicsDevice, BlendState.NonPremultiplied, SamplerState.PointWrap, RasterizerState.CullNone);

                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        public virtual void DrawSwing(SpriteBatch spriteBatch, Color lightColor) {
            Texture2D texture = TextureValue;
            Rectangle rect = CWRUtils.GetRec(texture, Projectile.frame, AnimationMaxFrme);
            Vector2 drawOrigin = rect.Size() / 2;
            SpriteEffects effects = Projectile.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            Vector2 offsetOwnerPos = safeInSwingUnit.GetNormalVector() * unitOffsetDrawZkMode * Projectile.spriteDirection * MeleeSize;
            float drawRoting = Projectile.rotation;
            if (Projectile.spriteDirection == -1) {
                drawRoting += MathHelper.Pi;
            }
            //烦人的对角线翻转代码，我凑出来了这个效果，它很稳靠，但我仍旧不想细究这其中的数学逻辑
            if (inDrawFlipdiagonally) {
                effects = Projectile.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                drawRoting += MathHelper.PiOver2;
                offsetOwnerPos *= -1;
            }

            Vector2 drawPosValue = Projectile.Center - RodingToVer(toProjCoreMode, (Projectile.Center - GetOwnerCenter()).ToRotation()) + offsetOwnerPos;
            Color color = Projectile.GetAlpha(lightColor);
            if (Incandescence || !CWRServerConfig.Instance.WeaponAdaptiveIllumination) {
                color = Color.White;
            }

            Vector2 trueDrawPos = drawPosValue - Main.screenPosition + Vector2.UnitY * Projectile.gfxOffY;

            Main.EntitySpriteDraw(texture, trueDrawPos, new Rectangle?(rect)
                , color, drawRoting, drawOrigin, Projectile.scale * MeleeSize, effects, 0);
            if (canDrawGlow) {
                Main.EntitySpriteDraw(glowTexValue.Value, trueDrawPos, new Rectangle?(rect)
                    , Color.White, drawRoting, drawOrigin, Projectile.scale * MeleeSize, effects, 0);
            }
        }

        public sealed override bool PreDraw(ref Color lightColor) {
            if (canDrawSlashTrail) {
                DrawSlashTrail();
            }
            DrawSwing(Main.spriteBatch, lightColor);
            return false;
        }
        #endregion
    }
}
