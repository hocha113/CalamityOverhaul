using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    /// <summary>雪球炮HeldProj基类，开火期存活，子类覆写UpdateGun</summary>
    internal abstract class BaseSnowCannonHeld : BaseHeldProj
    {
        /// 对应武器ID
        public abstract int TargetItemID { get; }
        public override LocalizedText DisplayName => ItemLoader.GetItem(TargetItemID).DisplayName;

        /// 弹药门控(PickSnowAmmo 时消耗)
        internal static bool AmmoConsumeContext { get; private set; }

        /// 纹理垂直帧数
        protected virtual int FrameCount => 1;
        /// 枪口前向距离
        protected virtual float BarrelLength => 30f;
        /// 枪口法线偏移
        protected virtual float MuzzleNormalOffset => 0f;
        /// 持握中心距玩家
        protected virtual float HoldDistance => 16f;
        /// 收尾未完成则不销毁
        protected virtual bool PendingWork => false;

        /// 开火冷却(仅本实例)
        protected int cooldown;
        /// 后坐位移(表现)
        protected float recoil;

        /// 左键开火意图
        protected bool FireKeyLeft => DownLeft && !Owner.mouseInterface;
        /// 右键开火意图
        protected bool FireKeyRight => DownRight && !DownLeft && !Owner.mouseInterface && !Owner.cursorItemIconEnabled;

        /// 枪口朝向
        protected Vector2 GunForward => Projectile.rotation.ToRotationVector2();
        /// 枪口世界坐标
        protected Vector2 MuzzlePos {
            get {
                Vector2 fwd = GunForward;
                Vector2 normal = new Vector2(-fwd.Y, fwd.X) * DirSign;
                return Projectile.Center + fwd * (BarrelLength - recoil) - normal * MuzzleNormalOffset;
            }
        }

        /// 跨使用状态宿主，按玩家持有规避物品克隆重置
        protected SnowCannonPlayer GunState => Owner.GetModPlayer<SnowCannonPlayer>();

        /// 跨使用冷却判定
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

        /// 子类 SetDefaults 扩展
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
            //无开火意图且无收尾则销毁
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

        /// 每帧开火逻辑
        protected abstract void UpdateGun();

        /// 更新枪位旋转朝向
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

        /// 双臂持枪姿势
        protected virtual void UpdateArms() {
            float armRot = Projectile.rotation - MathHelper.PiOver2 * SafeGravDir;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRot);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.ThreeQuarters, armRot);
        }

        /// 拾取雪球弹药(含伤害击退)
        protected bool PickSnowAmmo(out int damage, out float knockback, bool consume = true)
            => PickSnowAmmo(out _, out damage, out knockback, consume);

        /// 拾取雪球弹药(含弹幕类型)
        protected bool PickSnowAmmo(out int projToShoot, out int damage, out float knockback, bool consume = true) {
            AmmoConsumeContext = consume;
            bool hasAmmo = Owner.PickAmmo(Item, out projToShoot, out _, out damage, out knockback, out _, !consume);
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
