using CalamityOverhaul.Common;
using InnoVault.GameContent.BaseEntity;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Marbles
{
    /// <summary>
    /// 自写近战挥砍脚手架：仅以 <see cref="BaseHeldProj"/> 提供 Owner 锚定与持握生命周期，
    /// 挥砍弧线、沿刃命中、本体与图元拖尾渲染全部在此自行实现，不依赖任何高层挥砍框架。
    /// 子类只需提供贴图路径与少量挥砍参数。
    /// </summary>
    internal abstract class BaseSwingHeld : BaseHeldProj, IPrimitiveDrawable
    {
        public override string Texture => TexturePath;

        //—— 子类必填 ——
        protected abstract string TexturePath { get; }
        /// <summary>挥砍总扫掠弧度</summary>
        protected abstract float SwingArc { get; }
        /// <summary>手到刀刃中心的锚定距离</summary>
        protected abstract float HoldDistance { get; }
        /// <summary>用于命中判定与拖尾的刀刃长度</summary>
        protected abstract float BladeLength { get; }

        //—— 子类可选覆写 ——
        protected virtual float BladeWidth => 34f;
        protected virtual float TrailWidthMax => 56f;
        protected virtual int TrailLength => 22;
        protected virtual string GradientBar => GraniteMarbleVFX.MarbleBar;
        protected virtual Color TrailColor => GraniteMarbleVFX.MarbleCore;
        protected virtual string TrailBaseImage => CWRConstant.Masking + "SlashFlatBlurHVMirror";
        protected virtual float DrawScale => 1f;
        /// <summary>贴图朝向修正：默认贴图刀尖指向右上（-45°）</summary>
        protected virtual float SpriteRotationOffset => -MathHelper.PiOver4;

        protected float BaseAngle;
        protected float CurrentAngle;
        protected float SwingProgress;
        protected int SwingDir = 1;

        private Vector2[] tipCache;
        private Trail Trail;

        public sealed override void SetDefaults() {
            Projectile.width = Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 120;
            SafeSetDefaults();
        }

        protected virtual void SafeSetDefaults() { }

        /// <summary>挥砍缓动：默认 slow-fast-slow（smoothstep），重武器可覆写</summary>
        protected virtual float SwingEase(float p) => p * p * (3f - 2f * p);

        /// <summary>挥砍开始（第一帧）</summary>
        protected virtual void OnSwingStart() { }

        /// <summary>每帧挥砍推进（p = 0..1, ang = 当前角度）</summary>
        protected virtual void OnSwingUpdate(float p, float ang) { }

        public sealed override void AI() {
            SetHeld();
            int duration = Owner.itemAnimationMax;
            if (duration < 1) {
                duration = 24;
            }
            if (Projectile.timeLeft > duration) {
                Projectile.timeLeft = duration;
            }

            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                BaseAngle = Projectile.velocity.ToRotation();
                SwingDir = Projectile.ai[0] >= 0f ? 1 : -1;
                tipCache = null;
                OnSwingStart();
            }

            float p = 1f - Projectile.timeLeft / (float)duration;
            SwingProgress = p;
            float eased = SwingEase(p);
            CurrentAngle = BaseAngle + SwingDir * MathHelper.Lerp(-SwingArc * 0.5f, SwingArc * 0.5f, eased);

            Vector2 dir = CurrentAngle.ToRotationVector2();
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            Projectile.velocity = dir;
            Projectile.Center = hand + dir * HoldDistance;
            Projectile.rotation = CurrentAngle;
            Owner.heldProj = Projectile.whoAmI;
            SetDirection();

            PushTip(hand + dir * BladeLength);
            OnSwingUpdate(p, CurrentAngle);
        }

        public sealed override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            Vector2 tip = hand + CurrentAngle.ToRotationVector2() * BladeLength;
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), hand, tip, BladeWidth, ref point);
        }

        private void PushTip(Vector2 tip) {
            if (tipCache == null) {
                tipCache = new Vector2[TrailLength];
                for (int i = 0; i < tipCache.Length; i++) {
                    tipCache[i] = tip;
                }
                return;
            }
            for (int i = tipCache.Length - 1; i > 0; i--) {
                tipCache[i] = tipCache[i - 1];
            }
            tipCache[0] = tip;
        }

        public float GetWidthFunc(float completionRatio) {
            float taper = 1f - completionRatio;
            float fade = MathHelper.Clamp(1f - SwingProgress * 0.4f, 0.2f, 1f);
            return taper * TrailWidthMax * Projectile.scale * fade;
        }

        public Color GetColorFunc(Vector2 completionRatio) {
            float fade = MathHelper.Clamp(1f - SwingProgress, 0f, 1f);
            return TrailColor * fade;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 hand = Owner.GetPlayerStabilityCenter() - Main.screenPosition;
            bool left = Owner.direction < 0;
            SpriteEffects fx = left ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            //刀柄锚点放在贴图下角，刀刃朝向 CurrentAngle 伸出
            Vector2 origin = left
                ? new Vector2(tex.Width * 0.85f, tex.Height * 0.85f)
                : new Vector2(tex.Width * 0.15f, tex.Height * 0.85f);
            float rot = left
                ? CurrentAngle - SpriteRotationOffset + MathHelper.Pi
                : CurrentAngle + SpriteRotationOffset;
            Main.EntitySpriteDraw(tex, hand, null, Projectile.GetAlpha(lightColor), rot, origin
                , Projectile.scale * DrawScale, fx, 0);
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (tipCache == null || SwingProgress >= 1f) {
                return;
            }
            Trail ??= new Trail(tipCache, GetWidthFunc, GetColorFunc);
            Trail.TrailPositions = tipCache;

            Effect effect = EffectLoader.GradientTrail.Value;
            GraniteMarbleVFX.ApplyGradientTrail(effect, GradientBar, TrailBaseImage);
            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            Trail?.DrawTrail(effect);
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
        }
    }
}
