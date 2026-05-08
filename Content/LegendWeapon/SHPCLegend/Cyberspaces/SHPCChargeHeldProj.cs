using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces
{
    /// <summary>
    /// SHPC 右键蓄力时的手持弹幕
    /// <br/>负责绘制武器贴图、控制手臂动画、提供枪口位置
    /// </summary>
    internal class SHPCChargeHeldProj : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;

        /// <summary>
        /// 武器贴图偏移量
        /// </summary>
        private static Vector2 GunOffset => new(26f, -10f);

        /// <summary>
        /// 枪口距离武器中心的前方距离（像素）
        /// </summary>
        private const float TipDistance = 90f;

        /// <summary>后坐力最大后退距离（像素）</summary>
        private const float RecoilMaxOffset = 18f;
        /// <summary>后坐力动画总帧数</summary>
        private const int RecoilDuration = 22;
        /// <summary>后退阶段占比（快速后退）</summary>
        private const float RecoilKickRatio = 0.25f;

        /// <summary>是否处于后坐力阶段</summary>
        private bool recoiling;
        /// <summary>后坐力计时器</summary>
        private int recoilTimer;
        /// <summary>触发后坐力时的瞄准方向（锁定）</summary>
        private Vector2 recoilDir;
        /// <summary>当前后坐力偏移量</summary>
        private float recoilOffset;

        /// <summary>
        /// 枪口世界坐标，供 CyberChargeOrbProj 查询
        /// </summary>
        public Vector2 TipPosition {
            get {
                float perpY = GunOffset.Y * Owner.direction;
                return Projectile.Center
                    + Vector2.UnitX.RotatedBy(Projectile.rotation) * (TipDistance - recoilOffset)
                    + Vector2.UnitX.RotatedBy(Projectile.rotation + MathHelper.PiOver2) * perpY;
            }
        }

        public override void SetDefaults() {
            Projectile.width = 70;
            Projectile.height = 70;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.hide = true;
        }

        public override bool? CanDamage() => false;

        /// <summary>
        /// 外部调用：触发后坐力动画，之后自动消亡
        /// </summary>
        public void TriggerRecoil() {
            if (recoiling) return;
            recoiling = true;
            recoilTimer = 0;
            recoilDir = UnitToMouseV;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            SetHeld();

            if (recoiling) {
                AI_Recoil();
            }
            else {
                AI_Charging();
            }
        }

        private void AI_Charging() {
            if (!DownRight) {
                TriggerRecoil();
            }
            //由CyberChargeOrbProj统一管理生命周期，这里只保持存活
            Projectile.timeLeft = 60;

            //瞄准方向
            Vector2 aimDir = UnitToMouseV;
            UpdateGunState(aimDir, 0f);
        }

        private void AI_Recoil() {
            recoilTimer++;

            // 后坐力曲线：快速后退 → 缓慢回弹
            int kickFrames = (int)(RecoilDuration * RecoilKickRatio);
            if (recoilTimer <= kickFrames) {
                // 后退阶段：使用缓出插值快速到达最大偏移
                float t = (float)recoilTimer / kickFrames;
                float ease = 1f - (1f - t) * (1f - t); // easeOutQuad
                recoilOffset = RecoilMaxOffset * ease;
            }
            else {
                // 回弹阶段：缓慢回到原位
                float t = (float)(recoilTimer - kickFrames) / (RecoilDuration - kickFrames);
                float ease = t * t; // easeInQuad
                recoilOffset = RecoilMaxOffset * (1f - ease);
            }

            if (recoilTimer >= RecoilDuration) {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 10;
            UpdateGunState(recoilDir, recoilOffset);
        }

        private void UpdateGunState(Vector2 aimDir, float backOffset) {
            float rotation = aimDir.ToRotation();

            Projectile.rotation = rotation;
            Projectile.velocity = Vector2.Zero;
            Projectile.Center = Owner.GetPlayerStabilityCenter();

            // 玩家手臂与朝向
            Owner.ChangeDir(Math.Sign(aimDir.X));
            // 手臂方向需要考虑后坐力偏移：手臂跟随枪身后退
            Vector2 armTarget = Owner.GetPlayerStabilityCenter() + aimDir * (40f - backOffset);
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full,
                (Owner.GetPlayerStabilityCenter() - armTarget).ToRotation() * Owner.gravDir + MathHelper.PiOver2);
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D weaponTex = TextureAssets.Item[SHPCOverride.ID].Value;
            if (weaponTex == null) return false;

            float rotation = Projectile.rotation;
            float perpY = GunOffset.Y * Owner.direction;
            // 绘制位置沿瞄准方向后退 recoilOffset
            Vector2 position = Owner.Center - Main.screenPosition
                + Vector2.UnitX.RotatedBy(rotation) * (GunOffset.X - recoilOffset)
                + Vector2.UnitX.RotatedBy(rotation + MathHelper.PiOver2) * perpY;

            SpriteEffects sp = Owner.direction < 0
                ? SpriteEffects.FlipVertically
                : SpriteEffects.None;

            Main.EntitySpriteDraw(weaponTex, position, null, lightColor, rotation,
                weaponTex.Size() / 2f, 1f, sp);

            return false;
        }
    }
}
