using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces
{
    /// <summary>SHPC 右键蓄力手持弹幕，绘制武器与枪口</summary>
    internal class SHPCChargeHeldProj : BaseHeldProj
    {
        /// <summary>后坐力最大后退距离（像素）</summary>
        private const float RecoilMaxOffset = 18f;
        /// <summary>后坐力动画总帧数</summary>
        private const int RecoilDuration = 22;
        /// <summary>后退阶段占比（快速后退）</summary>
        private const float RecoilKickRatio = 0.25f;

        /// <summary>SHPCCharge 序列图总帧数</summary>
        private const int ChargeFrameCount = 24;
        /// <summary>前段表示蓄力进度递进的帧数（0~20）</summary>
        private const int ChargeProgressFrames = 21;
        /// <summary>满蓄循环动画每帧停留的游戏帧数</summary>
        private const int LoopFrameTicks = 5;

        /// <summary>是否处于后坐力阶段</summary>
        private bool recoiling;
        /// <summary>后坐力计时器</summary>
        private int recoilTimer;
        /// <summary>触发后坐力时的瞄准方向（锁定）</summary>
        private Vector2 recoilDir;
        /// <summary>当前后坐力偏移量</summary>
        private float recoilOffset;

        /// <summary>蓄力进度 0~1，由 CyberChargeOrbProj 写入</summary>
        public float ChargeProgress;
        /// <summary>满蓄循环动画计时器</summary>
        private int loopAnimTimer;
        /// <summary>当前绘制帧索引</summary>
        private int currentFrame;
        /// <summary>本帧计算出的玩家前手世界坐标（握把锚点）</summary>
        private Vector2 handWorld;

        /// <summary>握把锚点像素（152×70 朝右）</summary>
        private static Vector2 GripPixel => new Vector2(50f, 46f);
        /// <summary>枪口发射点像素坐标</summary>
        private static Vector2 MuzzlePixel => new Vector2(146f, 32f);

        /// <summary>单帧像素坐标变换到世界坐标</summary>
        private Vector2 FramePointToWorld(Vector2 framePixel) {
            Vector2 rel = framePixel - GripPixel;
            if (Owner.direction < 0) {
                rel.Y = -rel.Y; //竖直翻转镜像 Y
            }
            return handWorld
                - Vector2.UnitX.RotatedBy(Projectile.rotation) * recoilOffset
                + rel.RotatedBy(Projectile.rotation) * SHPCOverride.ItemScale;
        }

        /// <summary>枪口世界坐标</summary>
        public Vector2 TipPosition => FramePointToWorld(MuzzlePixel);

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

        /// <summary>触发后坐力并自动消亡</summary>
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

            UpdateAnimationFrame();
        }

        /// <summary>蓄力递进前 21 帧，满蓄循环末三帧</summary>
        private void UpdateAnimationFrame() {
            if (ChargeProgress >= 1f) {
                int loopFrames = ChargeFrameCount - ChargeProgressFrames;
                loopAnimTimer++;
                int loopIdx = loopAnimTimer / LoopFrameTicks % loopFrames;
                currentFrame = ChargeProgressFrames + loopIdx;
            }
            else {
                loopAnimTimer = 0;
                int frame = (int)(ChargeProgress * ChargeProgressFrames);
                currentFrame = Math.Clamp(frame, 0, ChargeProgressFrames - 1);
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
            Projectile.rotation = aimDir.ToRotation();
            Projectile.velocity = Vector2.Zero;
            Projectile.Center = Owner.GetPlayerStabilityCenter();

            // 玩家朝向
            Owner.ChangeDir(Math.Sign(aimDir.X));

            // 手臂指向瞄准方向，并取得对应的前手世界坐标作为握把锚点，
            // 这样武器握把会始终跟随实际手部位置，避免旋转时脱手
            float armRotation = (-aimDir).ToRotation() * Owner.gravDir + MathHelper.PiOver2;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRotation);
            handWorld = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full, armRotation);

            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D chargeTex = TextureAssets.Projectile[Type].Value;
            if (chargeTex == null) return false;

            int frameHeight = chargeTex.Height / ChargeFrameCount;
            Rectangle sourceRect = new Rectangle(0, currentFrame * frameHeight, chargeTex.Width, frameHeight);

            float rotation = Projectile.rotation;
            SpriteEffects sp = Owner.direction < 0
                ? SpriteEffects.FlipVertically
                : SpriteEffects.None;

            // 以握把像素为原点，钉在前手世界坐标上绕其旋转；后坐力沿枪管反向回退
            Vector2 origin = GripPixel;
            Vector2 position = handWorld
                - Vector2.UnitX.RotatedBy(rotation) * recoilOffset
                - Main.screenPosition;
            if (Owner.direction < 0) {
                origin.Y -= 26 * SHPCOverride.ItemScale;
            }
            Main.EntitySpriteDraw(chargeTex, position, sourceRect, lightColor, rotation,
                origin, SHPCOverride.ItemScale, sp);

            return false;
        }
    }
}
