using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.GameModes.UI
{
    /// <summary>
    /// 模式标签绘制：shader 旗身（<see cref="EffectLoader.GameModeTab"/>）+ CPU 矢量回退 + 切换演出大字。
    /// 批处理契约照 TBUGRenderer.ShaderQuad：End → Immediate+effect → 画 quad → 恢复 Deferred
    /// </summary>
    internal static class GameModeRenderer
    {
        private static Texture2D Pixel => VaultAsset.placeholder2.Value;
        private static readonly Rectangle One = new(0, 0, 1, 1);

        internal static void DrawTab(SpriteBatch sb, Rectangle rect, GameModeFace face,
            float lit, float hover, float burst, bool burstOn, float disabled, float alpha) {
            if (alpha <= 0.01f || rect.Width < 4 || rect.Height < 4) {
                return;
            }

            Effect effect = EffectLoader.GameModeTab?.Value;
            if (effect == null) {
                DrawTabFallback(sb, rect, face, lit, disabled, alpha);
                return;
            }

            Color accent = GameModeTheme.Accent(face);
            Color ember = GameModeTheme.Ember(face);
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(rect.Width, rect.Height));
            effect.Parameters["uMode"]?.SetValue((float)face);
            effect.Parameters["uLit"]?.SetValue(lit);
            effect.Parameters["uHover"]?.SetValue(hover);
            effect.Parameters["uBurst"]?.SetValue(burst);
            effect.Parameters["uBurstOn"]?.SetValue(burstOn ? 1f : 0f);
            effect.Parameters["uDisabled"]?.SetValue(disabled);
            effect.Parameters["uAccent"]?.SetValue(accent.ToVector3());
            effect.Parameters["uEmber"]?.SetValue(ember.ToVector3());
            ShaderQuad(sb, effect, rect);
        }

        /// <summary>shader 缺编时的诚实矢量回退：漆底 + 边线 + 模式线稿</summary>
        private static void DrawTabFallback(SpriteBatch sb, Rectangle rect, GameModeFace face,
            float lit, float disabled, float alpha) {
            Color baseCol = GameModeTheme.NightBase * (0.94f * alpha);
            sb.Draw(Pixel, rect, One, baseCol);

            Color iconCol = Color.Lerp(GameModeTheme.BoneDim, GameModeTheme.Accent(face), lit);
            iconCol = Color.Lerp(iconCol, Color.Gray * 0.6f, disabled) * alpha;
            Color rim = iconCol * 0.8f;

            //1px 边
            sb.Draw(Pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), One, rim);
            sb.Draw(Pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), One, rim * 0.6f);
            sb.Draw(Pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), One, rim * 0.7f);
            sb.Draw(Pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), One, rim * 0.7f);

            Vector2 c = rect.Center.ToVector2();
            float s = rect.Width * 0.30f;
            if (face == GameModeFace.Brutal) {
                //三道斜痕
                Vector2 dir = new Vector2(-0.46f, 0.89f) * s;
                Vector2 perp = new Vector2(-dir.Y, dir.X) / s * (s * 0.42f);
                for (int i = -1; i <= 1; i++) {
                    DrawLine(sb, c - dir + perp * i, c + dir + perp * i, 2f, iconCol);
                }
            }
            else if (face == GameModeFace.Asura) {
                //环 + 三棱的线稿近似
                const int seg = 20;
                float r = s * 0.9f;
                Vector2 prev = c + new Vector2(r, 0f);
                for (int i = 1; i <= seg; i++) {
                    float ang = MathHelper.TwoPi * i / seg;
                    Vector2 next = c + ang.ToRotationVector2() * r;
                    DrawLine(sb, prev, next, 2f, iconCol);
                    prev = next;
                }
                for (int i = 0; i < 3; i++) {
                    float ang = -MathHelper.PiOver2 + MathHelper.TwoPi * i / 3f;
                    Vector2 d = ang.ToRotationVector2();
                    DrawLine(sb, c + d * r, c + d * (r + s * 0.55f), 2f, iconCol);
                }
            }
            else {
                //镰月线稿近似：外弧 + 一粒坠星
                const int seg = 14;
                float r = s * 0.95f;
                Vector2 prev = c + (-MathHelper.PiOver2 * 1.4f).ToRotationVector2() * r;
                for (int i = 1; i <= seg; i++) {
                    float ang = MathHelper.Lerp(-MathHelper.PiOver2 * 1.4f, MathHelper.PiOver2 * 1.4f, i / (float)seg);
                    Vector2 next = c + ang.ToRotationVector2() * r;
                    DrawLine(sb, prev, next, 2.5f, iconCol);
                    prev = next;
                }
                sb.Draw(Pixel, new Rectangle((int)(c.X + s * 0.55f) - 2, (int)(c.Y - s * 0.8f) - 2, 4, 4), One, iconCol);
            }
        }

        private static void DrawLine(SpriteBatch sb, Vector2 start, Vector2 end, float thickness, Color color) {
            Vector2 delta = end - start;
            float len = delta.Length();
            if (len < 0.5f) {
                return;
            }
            sb.Draw(Pixel, start, One, color, delta.ToRotation(),
                new Vector2(0f, 0.5f), new Vector2(len, thickness), SpriteEffects.None, 0f);
        }

        private static void ShaderQuad(SpriteBatch sb, Effect effect, Rectangle dest) {
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.UIScaleMatrix);
            sb.Draw(Pixel, dest, One, Color.White);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }

        /// <summary>切换演出：屏幕上三分之一处的大字 + 字下扫开的主色细线</summary>
        internal static void DrawCeremonyLine(SpriteBatch sb) {
            if (!GameModeCeremony.LineActive) {
                return;
            }

            float t = GameModeCeremony.LineProgress;
            float aIn = MathHelper.SmoothStep(0f, 1f, Math.Clamp(t / 0.10f, 0f, 1f));
            float aOut = MathHelper.SmoothStep(0f, 1f, Math.Clamp((1f - t) / 0.22f, 0f, 1f));
            float a = aIn * aOut;
            if (a <= 0.01f) {
                return;
            }

            string text = GameModeText.ToggleLine(GameModeCeremony.LineFace, GameModeCeremony.LineEnabled).Value;
            var font = FontAssets.DeathText.Value;
            Vector2 size = font.MeasureString(text);

            float scale = 0.86f + 0.14f * MathHelper.SmoothStep(0f, 1f, Math.Clamp(t / 0.22f, 0f, 1f));
            float maxW = GameModeTheme.UIScreenW * 0.86f;
            if (size.X * scale > maxW) {
                scale = maxW / size.X;
            }

            Vector2 pos = new(GameModeTheme.UIScreenW * 0.5f, GameModeTheme.UIScreenH * 0.30f - t * 16f);
            Color accent = GameModeTheme.Accent(GameModeCeremony.LineFace);
            Color textCol = Color.Lerp(accent, GameModeTheme.BoneDim, GameModeCeremony.LineEnabled ? 0f : 0.4f);

            Utils.DrawBorderStringBig(sb, text, pos, textCol * a, scale, 0.5f, 0.5f);

            //字下细线随进度扫开
            float ruleT = MathHelper.SmoothStep(0f, 1f, Math.Clamp(t / 0.32f, 0f, 1f));
            int ruleW = (int)(size.X * scale * ruleT);
            if (ruleW > 2) {
                var rule = new Rectangle((int)(pos.X - ruleW / 2f),
                    (int)(pos.Y + size.Y * scale * 0.5f + 6f), ruleW, 2);
                sb.Draw(Pixel, rule, One, accent * (a * 0.8f));
            }
        }
    }
}
