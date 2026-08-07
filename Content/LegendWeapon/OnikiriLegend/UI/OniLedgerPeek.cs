using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 翻页待出的帘角:屏缘一角前幕常掀着一线,缝里是邻屋——比本屋更深一层的暗,
    /// 渗着它的招牌光(点鬼簿=绯月红+鬼火青,改铭台=烛暖+金),缝内提示物随光标半速视差。
    /// 静息只占屏缘十几像素,悬停掀大,点击即自同侧发起墨扫换乘(与行进方位同源:簿在西,台在东)。
    /// 深度三件套:卷檐圆筒明暗 / 檐影投进缝里 / 缝光溢上前幕。绘法 shader 优先,缺则 CPU 行带
    /// </summary>
    internal sealed class OniLedgerPeek(OniLedgerView target, float side)
    {
        /// <summary>quad 宽(缝+卷檐+余晖的总预算;实际常驻可见只有靠缘一窄条)</summary>
        public const float QuadW = 84f;

        private readonly OniLedgerView target = target;
        /// <summary>-1=屏左缘(通点鬼簿),+1=屏右缘(通改铭台)</summary>
        private readonly float side = side;
        private float hoverEase;
        private float press;
        private bool wasHovered;

        /// <summary>本帧 quad 区(UI 空间,含透明预算;空即本屏不画)</summary>
        public Rectangle Area { get; private set; }
        /// <summary>本帧悬停(点外收台判定要避开它)</summary>
        public bool Hovering { get; private set; }
        /// <summary>悬停缓动 0~1</summary>
        public float HoverEase => hoverEase;

        /// <summary>
        /// 开缝宽(px),与 OniLedgerPeek.fx 同式——shader 里改波形必同步此处,
        /// 缝内提示物与命中都按它取样
        /// </summary>
        public static float GapW(float y01, float lift, float quadW, float time, float seed, float stir) {
            float wave = MathF.Pow(MathF.Abs(MathF.Sin(MathHelper.Pi * y01)), 1.35f);
            float open01 = 0.30f + 0.70f * wave;
            float wob = 1f + (0.05f + stir * 0.10f) * MathF.Sin(time * 0.8f + y01 * 5f + seed);
            float liftE = lift * lift * (3f - 2f * lift);
            return MathHelper.Lerp(quadW * 0.16f, quadW * 0.52f, liftE) * open01 * wob;
        }

        /// <summary>开屏复位</summary>
        public void Reset() {
            hoverEase = 0f;
            press = 0f;
            Hovering = false;
            wasHovered = false;
            Area = Rectangle.Empty;
        }

        /// <summary>推进一帧;返回 true 的那一帧发起换乘</summary>
        public bool Update(Rectangle area, Vector2 mouse, bool interactive, KeyPressState leftPress) {
            Area = area;
            if (press > 0f) {
                press = MathF.Max(press - 1f / 14f, 0f);
            }
            if (area.Height < 100) {
                Hovering = false;
                wasHovered = false;
                hoverEase *= 0.8f;
                return false;
            }

            //命中:贴屏缘一窄条,随掀起加宽(用缓动值,免得边界抖动)
            float hitW = 16f + hoverEase * 30f;
            Rectangle hit = side < 0f
                ? new Rectangle(area.X, area.Y, (int)hitW, area.Height)
                : new Rectangle((int)(area.Right - hitW), area.Y, (int)hitW, area.Height);
            bool hoverNow = interactive && hit.Contains(mouse.ToPoint());
            if (hoverNow && !wasHovered) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.32f, Volume = 0.26f });
            }
            Hovering = hoverNow;
            wasHovered = hoverNow;
            hoverEase += ((hoverNow ? 1f : 0f) - hoverEase) * 0.16f;

            if (hoverNow && leftPress == KeyPressState.Pressed) {
                press = 1f;
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.1f, Volume = 0.4f });
                return true;
            }
            return false;
        }

        public void Draw(SpriteBatch sb, float alpha, float time, float seed, Vector2 parallax, float stir) {
            if (alpha <= 0.01f || Area.Height < 100) {
                return;
            }
            Effect effect = EffectLoader.OniLedgerPeek?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect != null && noise != null) {
                DrawShaderVeil(sb, effect, noise, alpha, time, seed, stir);
            }
            else {
                DrawFallbackVeil(sb, alpha, time, seed, stir);
            }
            DrawRoomHints(sb, alpha, time, seed, parallax, stir);
        }

        private void DrawShaderVeil(SpriteBatch sb, Effect effect, Texture2D noise,
            float alpha, float time, float seed, float stir) {
            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uLift"]?.SetValue(hoverEase);
            effect.Parameters["uPress"]?.SetValue(press);
            effect.Parameters["uFlip"]?.SetValue(side < 0f ? 1f : -1f);
            effect.Parameters["uStir"]?.SetValue(stir);
            effect.Parameters["uSeed"]?.SetValue(seed);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(Area.Width, Area.Height));
            effect.Parameters["uColInk"]?.SetValue(OnikiriUITheme.Ink.ToVector3());
            effect.Parameters["uColPaper"]?.SetValue(OnikiriUITheme.Paper.ToVector3());
            effect.Parameters["uColAccent"]?.SetValue(AccentColor().ToVector3());
            effect.Parameters["uColGlint"]?.SetValue(GlintColor().ToVector3());
            effect.Parameters["uColHot"]?.SetValue(OnikiriUITheme.HotWhite.ToVector3());

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearWrap, DepthStencilState.None,
                RasterizerState.CullNone, effect, Main.UIScaleMatrix);
            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            sb.Draw(VaultAsset.placeholder2.Value, Area, new Rectangle(0, 0, 1, 1), Color.White);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }

        /// <summary>CPU 降级:4px 行带铺缝+檐,读得出"掀角有屋"即可</summary>
        private void DrawFallbackVeil(SpriteBatch sb, float alpha, float time, float seed, float stir) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Color accent = AccentColor();
            float edgeX = side < 0f ? Area.X : Area.Right;
            float rollW = 7f + 5f * hoverEase;
            for (int y = 0; y < Area.Height; y += 4) {
                float y01 = y / (float)Area.Height;
                float endFade = MathHelper.Clamp(y01 / 0.1f, 0f, 1f)
                    * MathHelper.Clamp((1f - y01) / 0.1f, 0f, 1f);
                if (endFade <= 0.02f) {
                    continue;
                }
                float gap = GapW(y01, hoverEase, Area.Width, time, seed, stir);
                float rowY = Area.Y + y;
                float inward = -side;
                float x0 = side < 0f ? edgeX : edgeX - gap;
                //缝内暗 + 邻屋光
                sb.Draw(pixel, new Rectangle((int)x0, (int)rowY, (int)gap, 4), src,
                    OnikiriUITheme.Ink * (alpha * 0.9f * endFade));
                float glowW = gap * 0.55f;
                float gx = side < 0f ? edgeX : edgeX - glowW;
                sb.Draw(pixel, new Rectangle((int)gx, (int)rowY, (int)glowW, 4), src,
                    accent * (alpha * 0.30f * endFade));
                //卷檐:亮带+缘线
                float rollX = edgeX + inward * gap;
                sb.Draw(pixel, new Vector2(rollX, rowY), src,
                    Color.Lerp(OnikiriUITheme.Ink, OnikiriUITheme.Paper, 0.15f) * (alpha * 0.9f * endFade),
                    0f, new Vector2(side < 0f ? 0f : 1f, 0f), new Vector2(rollW, 4f), SpriteEffects.None, 0f);
                sb.Draw(pixel, new Vector2(rollX + inward * rollW, rowY), src,
                    OnikiriUITheme.Paper * (alpha * 0.35f * endFade),
                    0f, new Vector2(0.5f, 0f), new Vector2(1.2f, 4f), SpriteEffects.None, 0f);
            }
        }

        /// <summary>缝内提示物:邻屋的招牌剪影,半速视差,掀得越开越读得清</summary>
        private void DrawRoomHints(SpriteBatch sb, float alpha, float time, float seed, Vector2 parallax, float stir) {
            float hintA = alpha * (0.35f + 0.65f * hoverEase);
            if (hintA <= 0.03f) {
                return;
            }
            float edgeX = side < 0f ? Area.X : Area.Right;
            float inward = -side;
            Vector2 lag = parallax * 0.5f;
            //缝内取位:x 按开缝宽的比例,y 按带高比例
            Vector2 At(float y01, float frac) {
                float gap = GapW(y01, hoverEase, Area.Width, time, seed, stir);
                return new Vector2(edgeX + inward * gap * frac, Area.Y + Area.Height * y01) + lag;
            }

            if (target == OniLedgerView.Register) {
                //绯月一粒 + 名录竖行的墨点 + 鬼火偶闪
                float breath = 0.7f + 0.3f * MathF.Sin(time * 0.9f + seed);
                Vector2 moon = At(0.20f, 0.5f);
                OniBrush.DrawSoftDot(sb, moon, 7.5f, OnikiriUITheme.Deep, hintA * 0.8f * breath);
                OniBrush.DrawSoftDot(sb, moon, 3f, OnikiriUITheme.Bright, hintA * 0.7f * breath);
                Texture2D pixel = VaultAsset.placeholder2.Value;
                Rectangle src = new(0, 0, 1, 1);
                foreach (float colFrac in new[] { 0.32f, 0.62f }) {
                    for (int i = 0; i < 4; i++) {
                        float y01 = 0.40f + i * 0.095f + (colFrac > 0.5f ? 0.045f : 0f);
                        Vector2 dash = At(y01, colFrac);
                        sb.Draw(pixel, dash, src, OnikiriUITheme.Paper * (hintA * 0.30f), 0f,
                            new Vector2(0.5f), new Vector2(1.6f, 6.5f), SpriteEffects.None, 0f);
                    }
                }
                float flick = MathF.Max(0f, MathF.Sin(time * 1.6f + seed * 5f) - 0.72f) / 0.28f;
                if (flick > 0.01f) {
                    OniBrush.DrawSoftDot(sb, At(0.80f, 0.42f), 3.4f,
                        OnikiriUITheme.GhostFire, hintA * 0.55f * flick);
                }
            }
            else {
                //金压线一竖 + 刀影一掠 + 烛暖坠底
                Vector2 goldTop = At(0.36f, 0.5f);
                Vector2 goldBot = At(0.68f, 0.5f);
                OniBrush.DrawGradientLine(sb, goldTop, goldBot,
                    OnikiriUITheme.GoldInlay * (hintA * 0.5f), OnikiriUITheme.GoldDeep * (hintA * 0.15f), 1.2f);
                OniBrush.DrawSoftDot(sb, goldTop, 2.6f, OnikiriUITheme.GoldInlay, hintA * 0.5f);
                OniBrush.DrawSoftStreak(sb, At(0.52f, 0.45f), -side * 1.18f, 24f, 1.1f,
                    OnikiriUITheme.Paper, hintA * 0.14f, 0.4f);
                float breath = 0.7f + 0.3f * MathF.Sin(time * 1.1f + seed * 2f);
                OniBrush.DrawSoftDot(sb, At(0.83f, 0.4f), 9f,
                    OnikiriUITheme.CandleWarm, hintA * 0.5f * breath);
            }
        }

        private Color AccentColor() => target == OniLedgerView.Register
            ? Color.Lerp(OnikiriUITheme.Deep, OnikiriUITheme.Bright, 0.30f)
            : OnikiriUITheme.CandleWarm;

        private Color GlintColor() => target == OniLedgerView.Register
            ? Color.Lerp(OnikiriUITheme.GhostDim, OnikiriUITheme.GhostFire, 0.5f)
            : OnikiriUITheme.GoldInlay;
    }
}
