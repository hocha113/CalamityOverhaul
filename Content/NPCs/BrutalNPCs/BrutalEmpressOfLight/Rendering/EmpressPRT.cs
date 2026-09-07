using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering
{
    /// <summary>
    /// 加色批染色工具：InnoVault AdditiveBlend 批源因子是 SourceAlpha，
    /// A=0 会让整张消失（VFX.md 樱流事故同款）；包络必须写进 A，rgb 保持本色
    /// </summary>
    internal static class EmpressPRTDraw
    {
        public static Color Tint(Color rgb, float envelope)
            => rgb with { A = (byte)(255f * MathHelper.Clamp(envelope, 0f, 1f)) };

        /// <summary>形态取色：昼是白金核心带三成光谱（白光碎成彩），夜走光谱（dayBlend 由生成方传入，各端本地）</summary>
        public static Color FormHue(float hue, float dayBlend, float lum = 0.62f) {
            Color night = Main.hslToRgb(hue % 1f, 1f, lum);
            Color core = Color.Lerp(new Color(255, 236, 200), new Color(255, 252, 244), MathHelper.Clamp((lum - 0.4f) * 2f, 0f, 1f));
            Color day = Color.Lerp(core, Main.hslToRgb(hue % 1f, 1f, 0.7f), 0.35f);
            return Color.Lerp(night, day, MathHelper.Clamp(dayBlend, 0f, 1f));
        }
    }

    /// <summary>棱彩闪尘：速度拉伸的四芒光点，夜色相沿寿命缓移，昼锁金白</summary>
    internal class PRT_EmpressSpark : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "StarTexture_White";
        private float hue;
        private float baseScale;
        private float dayBlend;

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            ShouldKillWhenOffScreen = true;
        }

        public PRT_EmpressSpark Configure(int lifeTime, float hueSeed, float dayBlend = 0f) {
            Lifetime = lifeTime;
            hue = hueSeed;
            baseScale = Scale;
            this.dayBlend = dayBlend;
            return this;
        }

        public override void Reset() {
            base.Reset();
            hue = 0f;
            baseScale = 0f;
            dayBlend = 0f;
        }

        public override void AI() {
            Velocity *= 0.93f;
            float fade = 1f - LifetimeCompletion;
            Opacity = fade * fade;
            //夜：色相随寿命缓移，闪尘像折出的光斑在换色；昼：金白族内随寿命降温
            Color drift = EmpressPRTDraw.FormHue(hue + LifetimeCompletion * 0.22f, dayBlend, 0.62f - LifetimeCompletion * 0.1f * dayBlend);
            Color = Color.Lerp(Color, drift, 0.2f);
            Scale = baseScale * (0.5f + fade * 0.5f);
            Rotation = Velocity.X * 0.04f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            //速度拉伸：运动即各向异性
            float speed = Velocity.Length();
            Vector2 stretch = new(Scale * 0.06f * (1f + speed * 0.14f), Scale * 0.05f);
            float rot = speed > 0.5f ? Velocity.ToRotation() : Rotation;
            spriteBatch.Draw(tex, drawPos, null, EmpressPRTDraw.Tint(Color, Opacity), rot, origin, stretch, SpriteEffects.None, 0);
            //白核
            spriteBatch.Draw(tex, drawPos, null, EmpressPRTDraw.Tint(Color.White, Opacity * 0.55f), rot, origin, stretch * 0.45f, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>光羽尘：缓浮缓摆的柔光瓣，死亡绽散与环境逸散用</summary>
    internal class PRT_EmpressPetalDust : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        private float hue;
        private float swayPhase;
        private float baseScale;
        private float dayBlend;

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            ShouldKillWhenOffScreen = true;
        }

        public PRT_EmpressPetalDust Configure(int lifeTime, float hueSeed, float dayBlend = 0f) {
            Lifetime = lifeTime;
            hue = hueSeed;
            swayPhase = hueSeed * MathHelper.TwoPi;
            baseScale = Scale;
            this.dayBlend = dayBlend;
            return this;
        }

        public override void Reset() {
            base.Reset();
            hue = 0f;
            swayPhase = 0f;
            baseScale = 0f;
            dayBlend = 0f;
        }

        public override void AI() {
            //横向摆动+缓升，光羽的飘
            Velocity.X += (float)Math.Sin(Time * 0.11f + swayPhase) * 0.045f;
            Velocity *= 0.985f;
            float rise = (float)Math.Sin(LifetimeCompletion * MathHelper.Pi);
            Opacity = rise * 0.9f;
            Color = EmpressPRTDraw.FormHue(hue + LifetimeCompletion * 0.12f, dayBlend, 0.66f);
            Scale = baseScale * (0.7f + rise * 0.4f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            //纵横比压成瓣形，随摆动翻面
            float flip = 0.55f + 0.45f * (float)Math.Abs(Math.Sin(Time * 0.11f + swayPhase));
            spriteBatch.Draw(tex, drawPos, null, EmpressPRTDraw.Tint(Color, Opacity), Rotation, origin,
                new Vector2(Scale * 0.42f, Scale * 0.42f * flip), SpriteEffects.None, 0);
            spriteBatch.Draw(tex, drawPos, null, EmpressPRTDraw.Tint(Color.White, Opacity * 0.35f), Rotation, origin,
                new Vector2(Scale * 0.2f, Scale * 0.2f * flip), SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>折射涟漪环：一圈扩散的细环，位移闪现与图案落点标记用；
    /// 有机热斑环体+薄锐缘色散镶边（Ring01 灰度图已禁用，见 VFX.md）</summary>
    internal class PRT_EmpressRipple : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle5";
        [VaultLoaden(CWRConstant.Masking + "DiffusionCircle4")]
        internal static Asset<Texture2D> RimRing = null;
        private float hue;
        private float baseScale;
        private float dayBlend;

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            ShouldKillWhenOffScreen = true;
        }

        public PRT_EmpressRipple Configure(int lifeTime, float hueSeed, float dayBlend = 0f) {
            Lifetime = lifeTime;
            hue = hueSeed;
            baseScale = Scale;
            this.dayBlend = dayBlend;
            //环体贴图不对称，随机朝向防连环盖同章
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        public override void Reset() {
            base.Reset();
            hue = 0f;
            baseScale = 0f;
            dayBlend = 0f;
        }

        public override void AI() {
            float p = LifetimeCompletion;
            Scale = baseScale * (0.15f + VaultUtils.EaseOutCubic(p) * 0.85f);
            Opacity = (1f - p) * (1f - p);
            Color prism = EmpressPRTDraw.FormHue(hue, dayBlend, 0.7f);
            Color = Color.Lerp(Color.White, prism, p);
            Velocity *= 0.9f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 drawPos = Position - Main.screenPosition;
            //可见环半径与旧 Ring01 对齐：Ring01 环带在 0.83R/128px，DiffusionCircle5 在 0.39R/256px
            float bodyScale = Scale * 1.06f;
            spriteBatch.Draw(tex, drawPos, null, EmpressPRTDraw.Tint(Color, Opacity), Rotation,
                tex.Size() / 2f, bodyScale, SpriteEffects.None, 0);
            //薄锐缘双层色散镶边：外暖内冷，棱彩折射的材质签名
            if (RimRing?.Value is Texture2D rim) {
                float rimScale = Scale * 0.72f;
                Color outerC = EmpressPRTDraw.FormHue(hue + 0.06f, dayBlend, 0.66f);
                Color innerC = EmpressPRTDraw.FormHue(hue + 0.94f, dayBlend, 0.58f);
                spriteBatch.Draw(rim, drawPos, null, EmpressPRTDraw.Tint(outerC, Opacity * 0.55f), 0f,
                    rim.Size() / 2f, rimScale * 1.07f, SpriteEffects.None, 0);
                spriteBatch.Draw(rim, drawPos, null, EmpressPRTDraw.Tint(innerC, Opacity * 0.55f), 0f,
                    rim.Size() / 2f, rimScale * 0.93f, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>光之蝶：死亡绽散专属，扑翼上升的小小光蝶（棱彩草蛉的回响）</summary>
    internal class PRT_EmpressButterfly : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "StarTexture_White";
        private float hue;
        private float flapPhase;
        private float baseScale;
        private float dayBlend;

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            ShouldKillWhenOffScreen = false;//死亡演出镜头可能拉远，不许中途消失
        }

        public PRT_EmpressButterfly Configure(int lifeTime, float hueSeed, float dayBlend = 0f) {
            Lifetime = lifeTime;
            hue = hueSeed;
            flapPhase = hueSeed * MathHelper.TwoPi;
            baseScale = Scale;
            this.dayBlend = dayBlend;
            return this;
        }

        public override void Reset() {
            base.Reset();
            hue = 0f;
            flapPhase = 0f;
            baseScale = 0f;
            dayBlend = 0f;
        }

        public override void AI() {
            //扑翼：周期性上升脉冲+横漂
            float flap = (float)Math.Sin(Time * 0.23f + flapPhase);
            Velocity.Y -= 0.062f + Math.Max(0f, flap) * 0.075f;
            Velocity.X += (float)Math.Cos(Time * 0.09f + flapPhase) * 0.05f;
            Velocity *= 0.96f;
            float p = LifetimeCompletion;
            Opacity = MathHelper.Clamp(Time / 12f, 0f, 1f) * (1f - p * p);
            Color = EmpressPRTDraw.FormHue(hue + p * 0.3f, dayBlend, 0.68f);
            Lighting.AddLight(Position, Color.ToVector3() * 0.22f * Opacity);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            //两瓣错相缩放读作翅膀开合
            float flap = (float)Math.Abs(Math.Sin(Time * 0.23f + flapPhase));
            Vector2 wing = new(baseScale * 0.05f * (0.35f + flap * 0.65f), baseScale * 0.038f);
            spriteBatch.Draw(tex, drawPos, null, EmpressPRTDraw.Tint(Color, Opacity), 0.6f, origin, wing, SpriteEffects.None, 0);
            spriteBatch.Draw(tex, drawPos, null, EmpressPRTDraw.Tint(Color, Opacity), -0.6f, origin, wing, SpriteEffects.FlipHorizontally, 0);
            spriteBatch.Draw(tex, drawPos, null, EmpressPRTDraw.Tint(Color.White, Opacity * 0.6f), 0f, origin, wing * 0.4f, SpriteEffects.None, 0);
            return false;
        }
    }
}
