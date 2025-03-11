using CalamityMod;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using static CalamityOverhaul.CWRUtils;
using static CalamityOverhaul.CWRUtils.AnimationCurvePart;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs
{
    internal abstract class BaseThrowable : BaseHeldProj
    {
        #region Data
        protected float OverallProgress => 1 - Projectile.timeLeft / (float)TotalLifetime; // 总体进度
        protected float CurrentThrowProgress => 1 - Projectile.timeLeft / (float)TotalLifetime; // 当前投掷进度
        protected float ThrowStorageProgress => 1 - ThrowStorage / (float)ChargeUpTime; // 投掷存储进度
        protected ref float ReturnProgress => ref Projectile.ai[0]; // 返回进度
        protected ref float BounceCount => ref Projectile.ai[1]; // 弹跳计数
        protected int ThrowStorage {
            get => Projectile.timeLeft - TotalLifetime;
            set => Projectile.timeLeft = TotalLifetime + value;
        }
        /// <summary>
        /// 不要试图访问或者设置这个字段，该字段用于控制实体的二次设置
        /// </summary>
        private bool onSet;
        /// <summary>
        /// 该字段在实体的二次设置中被赋值，用于判断该弹幕是否受到潜伏攻击的加强
        /// </summary>
        internal bool stealthStrike;
        /// <summary>
        /// 绘制的旋转矫正值，默认为0
        /// </summary>
        public float OffsetRoting = 0;
        /// <summary>
        /// 蓄力时的手持距离模长，默认为-40
        /// </summary>
        public float HandOnTwringMode = -40;
        /// <summary>
        /// 蓄力充能时间，默认为10
        /// </summary>
        public int ChargeUpTime = 10;
        /// <summary>
        /// 实体的生命周期，默认为240
        /// </summary>
        public int TotalLifetime = 240;
        /// <summary>
        /// 使用拖尾绘制
        /// </summary>
        public bool UseDrawTrail;
        /// <summary>
        /// 拖尾颜色衰减，默认为0.8f
        /// </summary>
        public float TrailColorAttenuation = 0.8f;
        /// <summary>
        /// 关于收手的曲线动画拟合数据体
        /// </summary>
        protected static StartData startDataPullback = new StartData() {
            startX = 0,
            startHeight = 0,
            heightShift = MathHelper.PiOver4 * -1.2f,
            degree = 2
        };
        private AnimationCurvePart pullback = new AnimationCurvePart(PartValue, startDataPullback);
        /// <summary>
        /// 关于出手的曲线动画拟合数据体
        /// </summary>
        protected static StartData startDataThrowout = new StartData() {
            startX = 0.7f,
            startHeight = MathHelper.PiOver4 * -1.2f,
            heightShift = MathHelper.PiOver4 * 1.2f + MathHelper.PiOver2,
            degree = 3
        };
        private AnimationCurvePart throwout = new AnimationCurvePart(PartValue, startDataThrowout);

        private Func<float, Vector2> _onThrowingGetCenter;
        /// <summary>
        /// 获取蓄力时实体的位置
        /// </summary>
        protected Func<float, Vector2> OnThrowingGetCenter {
            get {
                if (_onThrowingGetCenter == null) {
                    _onThrowingGetCenter = (float armRotation) => Owner.GetPlayerStabilityCenter() +
                    Vector2.UnitY.RotatedBy(armRotation * Owner.gravDir) * HandOnTwringMode * Owner.gravDir;
                }
                return _onThrowingGetCenter;
            }
            set => _onThrowingGetCenter = value;
        }

        private Func<float, float> _onThrowingGetRotation;
        /// <summary>
        /// 获取蓄力时实体的旋转角
        /// </summary>
        protected Func<float, float> OnThrowingGetRotation {
            get {
                if (_onThrowingGetRotation == null) {
                    _onThrowingGetRotation = (float armRotation) => (armRotation - MathHelper.PiOver2) * Owner.gravDir;
                }
                return _onThrowingGetRotation;
            }
            set => _onThrowingGetRotation = value;
        }

        protected delegate float ToFloatData();

        private ToFloatData armAnticipationMovement;
        /// <summary>
        /// 一个委托字段，存储一个获取拟合曲线对应高度的逻辑，如果值为<see langword="null"/>，那么会自动赋值为一个默认的拟合逻辑
        /// </summary>
        protected ToFloatData ArmAnticipationMovement {
            get {
                if (armAnticipationMovement == null) {
                    armAnticipationMovement = () => EvaluateCurve(ThrowStorageProgress, [pullback, throwout]);
                }
                return armAnticipationMovement;
            }
            set {
                armAnticipationMovement = value;
            }
        }
        #endregion
        private static float PartValue(float amount, int degree) => 1f - (float)Math.Pow(1f - amount, degree);
        /// <summary>
        /// 更新<see cref="pullback"/>与<see cref="throwout"/>的值，目前为止没有地方会调用它，一般用于调试时调用
        /// </summary>
        protected void UpdatePartFunc() {
            pullback = new AnimationCurvePart(PartValue, startDataPullback);
            throwout = new AnimationCurvePart(PartValue, startDataThrowout);
        }

        public sealed override void SetDefaults() {
            Projectile.width = 38;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLifetime + ChargeUpTime;
            Projectile.DamageType = CWRLoad.RogueDamageClass;
            Projectile.ignoreWater = true;
            SetThrowable();
        }
        /// <summary>
        /// 设置实体的初始数据
        /// </summary>
        public virtual void SetThrowable() {

        }
        /// <summary>
        /// 二次设置实体的初始数据，这个方法会在<see cref="AI"/>中调用一次，一般用于设置需要访问玩家或者目标物品的逻辑
        /// </summary>
        public virtual void PostSetThrowable() {

        }

        public override bool ShouldUpdatePosition() {
            return ThrowStorageProgress >= 1;
        }

        public override bool? CanDamage() {
            return ThrowStorageProgress < 1 ? false : null;
        }

        public virtual void OnThrowing() {
            SetDirection();
            float armRotation = ArmAnticipationMovement.Invoke() * Owner.direction;
            Owner.heldProj = Projectile.whoAmI;
            Projectile.Center = OnThrowingGetCenter.Invoke(armRotation);
            Projectile.rotation = OnThrowingGetRotation.Invoke(armRotation);
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, MathHelper.Pi + armRotation);
        }

        public virtual void ThrowOut() {
            SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);
            Projectile.Center = Owner.Center + UnitToMouseV * 8;
            Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 17.5f;
            Projectile.tileCollide = true;
        }

        public virtual void ReturnTrip() {
            Projectile.tileCollide = false;
            Vector2 toPlayer = Projectile.Center.To(Owner.GetPlayerStabilityCenter());
            Projectile.velocity = Projectile.velocity.Length() * toPlayer.UnitVector();
            if (toPlayer.Length() < 24f || Projectile.numHits >= 5) {
                Projectile.Kill();
            }
        }

        public virtual bool PreDeparture() {
            return true;
        }

        public virtual void Departure() {
            if (Projectile.soundDelay <= 0) {
                SoundEngine.PlaySound(SoundID.Item7, Projectile.Center);
                Projectile.soundDelay = 8;
            }

            Projectile.rotation += (MathHelper.PiOver4 / 4f + MathHelper.PiOver4 / 2f *
                Math.Clamp(CurrentThrowProgress * 2f, 0, 1)) * Math.Sign(Projectile.velocity.X);

            if (BounceCount == 0f) {
                if (Projectile.velocity.Length() < 2f) {
                    ReturnProgress = 1f;
                    Projectile.numHits = 0;
                }

                if (ReturnProgress == 0f && Projectile.velocity.Length() > 2f
                    && Projectile.timeLeft < (205 + ChargeUpTime)) {
                    Projectile.velocity *= 0.88f;
                }
            }

            if (ReturnProgress == 1f && Projectile.velocity.Length() < 20f) {
                Projectile.velocity *= 1.1f;
            }
        }

        public virtual bool PreThrowOut() {
            return true;
        }

        public virtual void FlyToMovementAI() {
            if (PreDeparture()) {
                Departure();
            }

            if (ReturnProgress == 1f) {
                if (PreReturnTrip()) {
                    ReturnTrip();
                }
            }
        }

        public virtual bool PreReturnTrip() {
            return true;
        }

        public sealed override void AI() {
            if (!onSet) {
                stealthStrike = Owner.Calamity().StealthStrikeAvailable();
                PostSetThrowable();
                onSet = true;
            }

            if (ThrowStorageProgress < 1) {
                OnThrowing();
                if (UseDrawTrail) {//锁定所有拖尾位置到弹幕自身，防止丢出的瞬间影响视觉效果
                    for (int i = 0; i < Projectile.oldPos.Length; i++) {
                        Projectile.oldPos[i] = Projectile.position;
                    }
                }
                return;
            }

            if (Projectile.timeLeft == TotalLifetime) {
                if (PreThrowOut()) {
                    ThrowOut();
                }
            }

            FlyToMovementAI();

            PostUpdate();
        }

        public virtual void PostUpdate() {

        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.velocity = Projectile.Center.To(Owner.GetPlayerStabilityCenter()).UnitVector() * Projectile.oldVelocity.Length();
            ReturnProgress = 1f;
            return false;
        }

        public virtual void DrawThrowable(Color lightColor) {
            Main.EntitySpriteDraw(TextureValue, Projectile.Center - Main.screenPosition, null, lightColor
                , Projectile.rotation + (MathHelper.PiOver4 + OffsetRoting) * (Projectile.velocity.X > 0 ? 1 : -1)
                , TextureValue.Size() / 2, Projectile.scale, Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically, 0);
        }

        public virtual void DrawTrail(Color lightColor) {
            float sengs = 1f;
            foreach (var pos in Projectile.oldPos) {
                sengs *= TrailColorAttenuation;
                Main.EntitySpriteDraw(TextureValue, pos + Projectile.Size / 2 - Main.screenPosition, null, lightColor * sengs
                , Projectile.rotation + (MathHelper.PiOver4 + OffsetRoting) * (Projectile.velocity.X > 0 ? 1 : -1)
                , TextureValue.Size() / 2, Projectile.scale, Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically, 0);
            }
        }

        public sealed override bool PreDraw(ref Color lightColor) {
            if (UseDrawTrail && ThrowStorageProgress >= 1) {
                DrawTrail(lightColor);
            }
            DrawThrowable(lightColor);
            return false;
        }
    }
}
