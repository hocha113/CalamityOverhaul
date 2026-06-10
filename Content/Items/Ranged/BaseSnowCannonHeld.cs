using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    /// <summary>
    /// 雪球炮系列武器的手持弹幕基类
    /// <br/>由武器的 <see cref="ModItem.Shoot"/> 在使用瞬间生成，
    /// 只在开火期间存活：按键全部松开且没有收尾动作时立即销毁，不承担常态手持显示
    /// <br/>负责持握姿态、手臂复合动画、枪口定位、后坐表现与雪球弹药消耗，
    /// 具体开火行为由子类在 <see cref="BaseHeldProj.Initialize"/> 与 <see cref="UpdateGun"/> 中实现
    /// <br/>注意：跨使用的冷却与充能不要放在弹幕实例字段上（弹幕销毁即丢失），应存放在武器的 ModItem 上
    /// </summary>
    internal abstract class BaseSnowCannonHeld : BaseHeldProj
    {
        /// <summary>对应的武器物品ID，物品切换后手持弹幕会自动销毁</summary>
        public abstract int TargetItemID { get; }
        public override LocalizedText DisplayName => ItemLoader.GetItem(TargetItemID).DisplayName;

        /// <summary>
        /// 弹药消耗上下文开关：物品使用本身不消耗雪球（各武器的 ModItem.CanConsumeAmmo 返回该值），
        /// 只有手持弹幕通过 <see cref="PickSnowAmmo"/> 主动拾取时才放行消耗
        /// </summary>
        internal static bool AmmoConsumeContext { get; private set; }

        /// <summary>纹理的垂直帧数</summary>
        protected virtual int FrameCount => 1;
        /// <summary>枪口到持握中心的前向距离</summary>
        protected virtual float BarrelLength => 30f;
        /// <summary>枪口在垂直枪管方向上的偏移（正值朝枪管上方）</summary>
        protected virtual float MuzzleNormalOffset => 0f;
        /// <summary>持握中心到玩家中心的距离</summary>
        protected virtual float HoldDistance => 16f;
        /// <summary>
        /// 是否还有未完成的收尾动作（剩余点射、待释放的蓄能、炮口动画等），
        /// 为真时即使松开按键也暂不销毁
        /// </summary>
        protected virtual bool PendingWork => false;

        /// <summary>通用开火冷却计数（仅本次存活期间有效，跨使用的冷却请用 <see cref="TimeReady"/> 时间戳）</summary>
        protected int cooldown;
        /// <summary>后坐位移量，开火时增大并自动衰减，仅影响表现</summary>
        protected float recoil;

        /// <summary>是否按住左键且未点击UI（开火意图判定）</summary>
        protected bool FireKeyLeft => DownLeft && !Owner.mouseInterface;
        /// <summary>是否按住右键且未点击UI、未与左键冲突</summary>
        protected bool FireKeyRight => DownRight && !DownLeft && !Owner.mouseInterface && !Owner.cursorItemIconEnabled;

        /// <summary>当前枪口朝向单位向量</summary>
        protected Vector2 GunForward => Projectile.rotation.ToRotationVector2();
        /// <summary>枪口世界坐标</summary>
        protected Vector2 MuzzlePos {
            get {
                Vector2 fwd = GunForward;
                Vector2 normal = new Vector2(-fwd.Y, fwd.X) * DirSign;
                return Projectile.Center + fwd * (BarrelLength - recoil) - normal * MuzzleNormalOffset;
            }
        }

        /// <summary>基于 <see cref="Main.GameUpdateCount"/> 的跨使用冷却判定</summary>
        protected static bool TimeReady(uint readyTime) => Main.GameUpdateCount >= readyTime;

        public sealed override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.timeLeft = 2;
            SetGunDefaults();
        }

        /// <summary>子类的额外属性初始化</summary>
        protected virtual void SetGunDefaults() { }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => false;

        public override bool PreUpdate() {
            if (Item == null || Item.IsAir || Item.type != TargetItemID) {
                Projectile.Kill();
                return false;
            }
            if (!Owner.active || Owner.dead || Owner.CCed) {
                Projectile.Kill();
                return false;
            }
            //没有任何开火意图与收尾动作时销毁，让玩家回归普通的持物状态
            if (!DownLeft && !DownRight && !PendingWork) {
                Projectile.Kill();
                return false;
            }
            return true;
        }

        public sealed override void AI() {
            SetHeld();
            Projectile.timeLeft = 2;

            UpdateHoldPose();
            UpdateArms();

            if (cooldown > 0) {
                cooldown--;
            }
            if (recoil > 0) {
                recoil *= 0.8f;
                if (recoil < 0.1f) {
                    recoil = 0;
                }
            }

            UpdateGun();
        }

        /// <summary>每帧的开火与个性化逻辑，运行于姿态更新之后</summary>
        protected abstract void UpdateGun();

        /// <summary>更新枪体位置、旋转与玩家朝向</summary>
        protected virtual void UpdateHoldPose() {
            Projectile.rotation = ToMouseA;
            Vector2 fwd = GunForward;
            Owner.ChangeDir(fwd.X >= 0 ? 1 : -1);

            Projectile.Center = Owner.GetPlayerStabilityCenter()
                + fwd * (HoldDistance - recoil)
                + new Vector2(0, 2 * Owner.gravDir);

            Owner.itemRotation = MathF.Atan2(fwd.Y * DirSign, fwd.X * DirSign);
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;
        }

        /// <summary>设置玩家双臂的持枪姿势</summary>
        protected virtual void UpdateArms() {
            float armRot = Projectile.rotation - MathHelper.PiOver2 * SafeGravDir;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRot);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.ThreeQuarters, armRot);
        }

        /// <summary>
        /// 拾取雪球弹药并获取弹药加成后的伤害数据
        /// </summary>
        /// <param name="damage">含武器与弹药加成的伤害</param>
        /// <param name="knockback">击退</param>
        /// <param name="consume">是否实际消耗一颗弹药</param>
        /// <returns>背包中是否还有可用弹药</returns>
        protected bool PickSnowAmmo(out int damage, out float knockback, bool consume = true) {
            AmmoConsumeContext = consume;
            bool hasAmmo = Owner.PickAmmo(Item, out _, out _, out damage, out knockback, out _, !consume);
            AmmoConsumeContext = false;
            return hasAmmo;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureValue;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            SpriteEffects fx = DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            Main.EntitySpriteDraw(tex, drawPos, tex.GetRectangle(Projectile.frame, FrameCount), lightColor
                , Projectile.rotation, tex.GetOrig(FrameCount), Projectile.scale, fx, 0);
            return false;
        }
    }
}
