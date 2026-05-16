using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 棱镜碎片粒子 —— 服务于天堂陨落长弓家族
    /// <br/>以 R/G/B 三次轻微偏移叠加做廉价色散, 再以 SoftGlow 衬白热内核
    /// <br/>替代旧版的 PRT_HeavenfallStar, 视觉更精致, 与新的 HeavenfallPrismTrail 着色器风格统一
    /// </summary>
    internal class PRT_HeavenfallPrism : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "StarTexture_White";
        public override int InGame_World_MaxCount => 6000;

        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        internal static Asset<Texture2D> CoreGlow = null;

        private Color InitialColor;
        private float InitialScale;
        private float Dispersion;
        private float SpinSpeed;
        private bool ShortStretch;

        public PRT_HeavenfallPrism(Vector2 position, Vector2 velocity, Color rainbowColor
            , float scale, int lifetime, float dispersion = 4f, bool shortStretch = false) {
            Position = position;
            Velocity = velocity;
            Color = InitialColor = rainbowColor;
            Scale = InitialScale = scale;
            Lifetime = lifetime;
            Dispersion = dispersion;
            SpinSpeed = Main.rand.NextFloat(-0.06f, 0.06f);
            ShortStretch = shortStretch;
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            //速度衰减 + 轻微飘动
            Velocity *= 0.94f;
            float life = LifetimeCompletion;

            //尺寸: 头部稳定, 尾段快速塌缩
            Scale = InitialScale * (1f - life * life * 0.85f);

            //透明度: 正弦入退场
            Opacity = MathF.Sin(life * MathHelper.Pi);

            //颜色淡向白光再淡向透明, 中段最艳
            Color = Color.Lerp(InitialColor, Color.White, MathHelper.Clamp(life * 1.4f, 0f, 0.65f));

            Rotation = ShortStretch ? Rotation + SpinSpeed : Velocity.ToRotation() + MathHelper.PiOver2;

            Lighting.AddLight(Position, InitialColor.R / 255f * Opacity * 0.4f
                , InitialColor.G / 255f * Opacity * 0.4f, InitialColor.B / 255f * Opacity * 0.4f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Scale < 0.05f || Opacity < 0.02f) {
                return false;
            }

            Texture2D star = PRTLoader.PRT_IDToTexture[ID];
            Texture2D glow = CoreGlow?.Value;

            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = star.Size() * 0.5f;

            //形状: 短而圆的星花 (默认拉伸更小, 更"碎片"感)
            Vector2 stretch = ShortStretch
                ? new Vector2(0.55f, 0.55f) * Scale
                : new Vector2(0.30f, 1.55f) * Scale;

            //R/G/B 三相位偏移做色散光环
            Vector2 perp = new Vector2(MathF.Cos(Rotation + MathHelper.PiOver2)
                , MathF.Sin(Rotation + MathHelper.PiOver2)) * Dispersion;

            //外围 bloom (柔和发光底)
            if (glow != null) {
                float glowScale = Scale * 0.55f;
                Color glowColor = InitialColor * Opacity * 0.55f;
                glowColor.A = 0;
                spriteBatch.Draw(glow, drawPos, null, glowColor, 0f
                    , glow.Size() * 0.5f, glowScale, SpriteEffects.None, 0f);
            }

            //R 通道偏移
            Color rCol = new Color(255, 60, 60, 0) * (Opacity * 0.55f);
            spriteBatch.Draw(star, drawPos - perp, null, rCol, Rotation, origin, stretch
                , SpriteEffects.None, 0f);

            //B 通道偏移
            Color bCol = new Color(60, 90, 255, 0) * (Opacity * 0.55f);
            spriteBatch.Draw(star, drawPos + perp, null, bCol, Rotation, origin, stretch
                , SpriteEffects.None, 0f);

            //G/主色: 居中, 略大, 是粒子身份
            Color mainCol = Color * Opacity;
            mainCol.A = 0;
            spriteBatch.Draw(star, drawPos, null, mainCol, Rotation, origin, stretch * 1.05f
                , SpriteEffects.None, 0f);

            //白热高光内核
            Color core = Color.White * Opacity * 0.9f;
            core.A = 0;
            spriteBatch.Draw(star, drawPos, null, core, Rotation, origin, stretch * 0.5f
                , SpriteEffects.None, 0f);

            return false;
        }
    }
}
