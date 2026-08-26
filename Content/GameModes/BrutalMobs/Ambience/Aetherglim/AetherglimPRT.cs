using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Aetherglim
{
    /// <summary>
    /// 「珠光」珠光泡：从微光湖面缓升的虹彩小泡。
    /// 材质=珠光泡膜：薄锐环缘承形、偏心高光点、纵横反相微形变，临顶破裂放大退散
    /// </summary>
    internal class PRT_AetherglimPearl : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle4";

        private float huePhase;
        private float wobblePhase;
        private float baseScale;

        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 48;

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            ShouldKillWhenOffScreen = true;
        }

        public PRT_AetherglimPearl Configure(int lifetime, float hueSeed) {
            Lifetime = lifetime;
            huePhase = hueSeed;
            wobblePhase = hueSeed * MathHelper.TwoPi;
            baseScale = Scale;
            return this;
        }

        public override void Reset() {
            base.Reset();
            huePhase = 0f;
            wobblePhase = 0f;
            baseScale = 0f;
        }

        public override void AI() {
            wobblePhase += 0.09f;
            //上升中被看不见的重力涟漪轻推：横向缓摆，纵向缓升
            Velocity.X += MathF.Sin(wobblePhase) * 0.012f;
            Velocity.Y *= 0.992f;
            Velocity.X *= 0.97f;

            float p = LifetimeCompletion;
            if (Time < 14) {
                Opacity = Time / 14f;
            }
            else if (p > 0.86f) {
                //临顶破裂：放大退散
                float pop = (p - 0.86f) / 0.14f;
                Opacity = 1f - pop;
                Scale = baseScale * (1f + pop * 0.55f);
            }
            else {
                Opacity = 1f;
                Scale = baseScale;
            }
            Color = AetherglimFX.Iridescent(huePhase + p * 2.1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            //纵横反相微形变：泡膜的呼吸
            float squish = MathF.Sin(wobblePhase * 1.6f) * 0.07f;
            Vector2 scaleVec = new(Scale * (1f + squish), Scale * (1f - squish));

            //薄锐环缘双层：色相错半拍读作薄膜干涉
            spriteBatch.Draw(tex, drawPos, null, AetherglimFX.Tint(Color, Opacity * 0.85f),
                Rotation, origin, scaleVec, SpriteEffects.None, 0f);
            Color shifted = AetherglimFX.Iridescent(huePhase + LifetimeCompletion * 2.1f + 1.8f);
            spriteBatch.Draw(tex, drawPos, null, AetherglimFX.Tint(shifted, Opacity * 0.4f),
                Rotation, origin, scaleVec * 0.9f, SpriteEffects.None, 0f);
            //偏心高光点
            Vector2 glintOff = new(-Scale * tex.Width * 0.16f, -Scale * tex.Height * 0.16f);
            spriteBatch.Draw(tex, drawPos + glintOff, null, AetherglimFX.Tint(Color.White, Opacity * 0.5f),
                0f, origin, scaleVec * 0.16f, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 微光星屑：引力泡破碎的余韵。
    /// 材质=失重星尘：四芒星明灭、快时沿速度拉伸、慢时反重力缓浮（重力异常的签名）
    /// </summary>
    internal class PRT_AetherglimStarMote : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "StarGlow01";

        private float huePhase;
        private float twinklePhase;
        private float baseScale;

        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 80;

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            ShouldKillWhenOffScreen = true;
        }

        public PRT_AetherglimStarMote Configure(int lifetime, float hueSeed) {
            Lifetime = lifetime;
            huePhase = hueSeed;
            twinklePhase = hueSeed * MathHelper.TwoPi;
            baseScale = Scale;
            return this;
        }

        public override void Reset() {
            base.Reset();
            huePhase = 0f;
            twinklePhase = 0f;
            baseScale = 0f;
        }

        public override void AI() {
            twinklePhase += 0.31f;
            Velocity *= 0.94f;
            //反重力缓浮：星屑不坠落，慢慢向上飘散
            Velocity.Y -= 0.028f;
            float p = LifetimeCompletion;
            Opacity = (1f - p) * (1f - p) * (0.55f + 0.45f * MathF.Abs(MathF.Sin(twinklePhase)));
            Color = AetherglimFX.Iridescent(huePhase + p * 1.4f);
            Scale = baseScale * (0.55f + (1f - p) * 0.45f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            float speed = Velocity.Length();
            //快时沿速度拉伸，慢时正四芒
            Vector2 stretch = new(Scale * (1f + speed * 0.16f), Scale);
            float rot = speed > 0.6f ? Velocity.ToRotation() : twinklePhase * 0.13f;
            spriteBatch.Draw(tex, drawPos, null, AetherglimFX.Tint(Color, Opacity),
                rot, origin, stretch, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, drawPos, null, AetherglimFX.Tint(Color.White, Opacity * 0.5f),
                rot, origin, stretch * 0.42f, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 破膜波前：引力泡爆开瞬间的一圈薄锐环，色散成内外两唇分离扩散
    /// </summary>
    internal class PRT_AetherglimBurstRing : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle4";

        private float huePhase;
        private float baseScale;

        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 8;

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            ShouldKillWhenOffScreen = true;
        }

        public PRT_AetherglimBurstRing Configure(int lifetime, float hueSeed) {
            Lifetime = lifetime;
            huePhase = hueSeed;
            baseScale = Scale;
            return this;
        }

        public override void Reset() {
            base.Reset();
            huePhase = 0f;
            baseScale = 0f;
        }

        public override void AI() {
            float p = LifetimeCompletion;
            Scale = baseScale * (0.3f + VaultUtils.EaseOutCubic(p) * 0.9f);
            Opacity = (1f - p) * (1f - p);
            Color = AetherglimFX.Iridescent(huePhase + p * 1.2f);
            Velocity *= 0.9f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            float p = LifetimeCompletion;
            //色散双唇：外唇偏暖内唇偏冷，随扩散彼此拉开
            Color outer = AetherglimFX.Iridescent(huePhase + 0.7f);
            Color inner = AetherglimFX.Iridescent(huePhase - 0.7f);
            spriteBatch.Draw(tex, drawPos, null, AetherglimFX.Tint(outer, Opacity * 0.7f),
                0f, origin, Scale * (1f + p * 0.12f), SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, drawPos, null, AetherglimFX.Tint(inner, Opacity * 0.7f),
                0f, origin, Scale * (1f - p * 0.10f), SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, drawPos, null, AetherglimFX.Tint(Color.White, Opacity * 0.4f),
                0f, origin, Scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 「珠光」幻光蝶：低频掠过微光空域的蝶形幻光。
    /// 材质=相位残影：两瓣错相开合的翼、滑翔正弦缓沉浮、翼尖偶落星屑，
    /// 通体虹彩缓移并透微光（幻影不是实体，加色层无剪影）
    /// </summary>
    internal class PRT_AetherglimButterfly : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "StarTexture_White";

        private float huePhase;
        private float flapPhase;
        private float baseScale;

        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 3;

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            //从屏缘外飞入、横穿视野，不许出生即被剔除
            ShouldKillWhenOffScreen = false;
        }

        public PRT_AetherglimButterfly Configure(int lifetime, float hueSeed) {
            Lifetime = lifetime;
            huePhase = hueSeed;
            flapPhase = hueSeed * MathHelper.TwoPi;
            baseScale = Scale;
            return this;
        }

        public override void Reset() {
            base.Reset();
            huePhase = 0f;
            flapPhase = 0f;
            baseScale = 0f;
        }

        public override void AI() {
            //滑翔：横速为主，扑翼给纵向脉冲，整体缓沉浮
            float flap = MathF.Sin(Time * 0.19f + flapPhase);
            Velocity.Y = MathF.Sin(Time * 0.035f + flapPhase) * 0.42f - MathF.Max(0f, flap) * 0.22f;
            float p = LifetimeCompletion;
            float fadeIn = MathHelper.Clamp(Time / 40f, 0f, 1f);
            Opacity = fadeIn * MathHelper.Clamp((1f - p) / 0.18f, 0f, 1f) * 0.8f;
            Color = AetherglimFX.Iridescent(huePhase + p * 3.2f);
            //翼尖偶落星屑
            if (Main.rand.NextBool(34) && Opacity > 0.3f) {
                PRTLoader.NewParticle<PRT_AetherglimStarMote>(Position,
                    new Vector2(0f, 0.2f), Color, 0.16f)
                    .Configure(Main.rand.Next(26, 44), huePhase + Time * 0.05f);
            }
            Lighting.AddLight(Position, Color.ToVector3() * 0.16f * Opacity);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            //两瓣错相缩放读作翅膀开合（镜像 PRT_EmpressButterfly 的翼构造）
            float flap = MathF.Abs(MathF.Sin(Time * 0.19f + flapPhase));
            Vector2 wing = new(baseScale * 0.062f * (0.32f + flap * 0.68f), baseScale * 0.046f);
            float lean = Velocity.X * 0.05f;
            spriteBatch.Draw(tex, drawPos, null, AetherglimFX.Tint(Color, Opacity),
                0.6f + lean, origin, wing, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, drawPos, null, AetherglimFX.Tint(Color, Opacity),
                -0.6f + lean, origin, wing, SpriteEffects.FlipHorizontally, 0f);
            //躯干微芒
            spriteBatch.Draw(tex, drawPos, null, AetherglimFX.Tint(Color.White, Opacity * 0.55f),
                lean, origin, wing * 0.34f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
