using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.UIs.MainMenuOverUIs
{
    /// <summary>
    /// ED 致谢界面的矢量绘制层：1 像素白纹理 + 参数化线/弧/辉光 + 着色器背板。
    /// 背景走 AckBackdrop.fx / AckFinale.fx，缺失时回退 CPU 暗色绘制；结构参考 HalibutRenderer
    /// </summary>
    internal static class AckRenderer
    {
        public static Texture2D Pixel => VaultAsset.placeholder2.Value;
        private static DynamicSpriteFont Font => FontAssets.MouseText.Value;

        public static Vector2 AngleDir(float angle) => new(MathF.Cos(angle), MathF.Sin(angle));

        #region 线段与圆弧
        public static void DrawLine(SpriteBatch sb, Vector2 start, Vector2 end, float thickness, Color color) {
            Vector2 diff = end - start;
            float length = diff.Length();
            if (length < 0.5f) {
                return;
            }
            sb.Draw(Pixel, start, new Rectangle(0, 0, 1, 1), color, diff.ToRotation(),
                new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
        }

        /// <summary>三层叠加模拟辉光的直线段</summary>
        public static void DrawGlowLine(SpriteBatch sb, Vector2 start, Vector2 end, float thickness, Color color) {
            DrawLine(sb, start, end, thickness + 3.5f, color * 0.16f);
            DrawLine(sb, start, end, thickness + 1.4f, color * 0.42f);
            DrawLine(sb, start, end, thickness, color);
        }

        /// <summary>渐变分割线</summary>
        public static void DrawGradientLine(SpriteBatch sb, Vector2 start, Vector2 end,
            Color startColor, Color endColor, float thickness) {
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) {
                return;
            }
            edge /= length;
            int segments = Math.Max(1, (int)(length / 9f));
            for (int i = 0; i < segments; i++) {
                float t = (float)i / segments;
                Vector2 segPos = start + edge * (length * t);
                Color color = Color.Lerp(startColor, endColor, t);
                DrawLine(sb, segPos, segPos + edge * (length / segments + 0.5f), thickness, color);
            }
        }

        /// <summary>用径向线段填充环形扇区，自适应分段无缝拼接</summary>
        public static void DrawArc(SpriteBatch sb, Vector2 center,
            float rIn, float rOut, float aStart, float aEnd, Color color) {
            if (aEnd <= aStart) {
                return;
            }
            float midR = (rIn + rOut) * 0.5f;
            int steps = Math.Max((int)((aEnd - aStart) * midR / 2.5f), 3);
            float aStep = (aEnd - aStart) / steps;
            float lineThick = MathF.Max(aStep * midR + 0.8f, 1.5f);
            for (int i = 0; i <= steps; i++) {
                float a = aStart + i * aStep;
                Vector2 dir = AngleDir(a);
                DrawLine(sb, center + dir * rIn, center + dir * rOut, lineThick, color);
            }
        }

        /// <summary>程序化软边圆盘</summary>
        public static void DrawDisc(SpriteBatch sb, Vector2 center, float radius, float softPad, Color color) {
            if (radius <= 0f) {
                return;
            }
            DrawArc(sb, center, radius, radius + softPad, 0f, MathHelper.TwoPi, color * 0.25f);
            DrawArc(sb, center, MathF.Max(radius - 0.6f, 0f), radius + softPad * 0.5f, 0f, MathHelper.TwoPi, color * 0.55f);
            DrawArc(sb, center, 0f, radius, 0f, MathHelper.TwoPi, color);
        }

        /// <summary>多层径向柔光</summary>
        public static void DrawSoftGlow(SpriteBatch sb, Vector2 center, float radius, Color color) {
            for (int i = 0; i < 4; i++) {
                float t = i / 4f;
                DrawArc(sb, center, 0f, radius * (0.35f + t * 0.65f), 0f, MathHelper.TwoPi, color * (0.22f * (1f - t)));
            }
        }

        /// <summary>旋转 45° 的菱形点饰</summary>
        public static void DrawDiamond(SpriteBatch sb, Vector2 center, float size, Color color) {
            sb.Draw(Pixel, center, new Rectangle(0, 0, 1, 1), color, MathHelper.PiOver4,
                new Vector2(0.5f, 0.5f), new Vector2(size, size), SpriteEffects.None, 0f);
        }

        /// <summary>L 形角括号（明日方舟取景框母题），dir 决定开口朝向象限</summary>
        public static void DrawBracket(SpriteBatch sb, Vector2 corner, float armLen, float thickness,
            int dirX, int dirY, Color color) {
            DrawLine(sb, corner, corner + new Vector2(dirX * armLen, 0f), thickness, color);
            DrawLine(sb, corner, corner + new Vector2(0f, dirY * armLen), thickness, color);
        }
        #endregion

        #region 文字
        /// <summary>四向辉光描边文字</summary>
        public static void DrawGlowText(SpriteBatch sb, string text, Vector2 pos,
            Color textColor, Color glowColor, float scale, float glowRadius = 1.4f) {
            if (string.IsNullOrEmpty(text)) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                Vector2 offset = AngleDir(MathHelper.TwoPi * i / 4f) * glowRadius;
                Utils.DrawBorderString(sb, text, pos + offset, glowColor, scale);
            }
            Utils.DrawBorderString(sb, text, pos, textColor, scale);
        }

        public static Vector2 DrawGlowTextCentered(SpriteBatch sb, string text, Vector2 center,
            Color textColor, Color glowColor, float scale, float glowRadius = 1.4f) {
            if (string.IsNullOrEmpty(text)) {
                return Vector2.Zero;
            }
            Vector2 size = Font.MeasureString(text) * scale;
            DrawGlowText(sb, text, center - size * 0.5f, textColor, glowColor, scale, glowRadius);
            return size;
        }

        /// <summary>字距拉开的文字（用于拉丁文小标签的留白质感），返回总宽度</summary>
        public static float DrawTrackedText(SpriteBatch sb, string text, Vector2 pos,
            Color color, float scale, float tracking) {
            if (string.IsNullOrEmpty(text)) {
                return 0f;
            }
            float x = pos.X;
            foreach (char c in text) {
                string s = c.ToString();
                Utils.DrawBorderString(sb, s, new Vector2(x, pos.Y), color, scale);
                x += Font.MeasureString(s).X * scale + tracking;
            }
            return x - pos.X - tracking;
        }

        public static float MeasureTracked(string text, float scale, float tracking) {
            if (string.IsNullOrEmpty(text)) {
                return 0f;
            }
            float w = 0f;
            foreach (char c in text) {
                w += Font.MeasureString(c.ToString()).X * scale + tracking;
            }
            return w - tracking;
        }
        #endregion

        #region 组合元素
        /// <summary>角色的拉丁文副标签，营造方舟式中英双行的留白感</summary>
        public static string RoleTag(CreditRole role) => role switch {
            CreditRole.Artist => "ARTIST",
            CreditRole.CodeAssistance => "CODE ASSISTANCE",
            CreditRole.Musician => "MUSIC",
            CreditRole.BalanceTester => "BALANCE TEST",
            _ => "DONORS",
        };

        /// <summary>
        /// 分节标题，自上而下三层互不重叠：
        /// 元信息小行（竖纹 + 序号/总数 + 拉丁标签） → 大号角色名（清晰描边，无彩色偏移辉光） → 全宽分割线。
        /// 分割线在文字下方独占一行，线上的菱形与游走高光不会被任何元素遮挡
        /// </summary>
        public static void DrawSectionHeader(SpriteBatch sb, int index, int total, CreditRole role,
            string headerText, float leftX, float headerTop, float headerHeight, float contentRight,
            float alpha, float reveal, float time) {
            Color roleCol = AckTheme.RoleColor(role);
            float ease = AckTheme.EaseOutCubic(reveal);
            float rise = (1f - ease) * 12f;

            //元信息小行
            float metaY = headerTop + 6f + rise;
            DrawLine(sb, new Vector2(leftX, metaY + 1f), new Vector2(leftX, metaY + 14f), 2f, roleCol * (alpha * 0.9f));
            string idx = (index + 1).ToString("00");
            const float idxScale = 0.82f;
            Utils.DrawBorderString(sb, idx, new Vector2(leftX + 9f, metaY), AckTheme.Text * alpha, idxScale);
            float metaX = leftX + 9f + Font.MeasureString(idx).X * idxScale + 6f;
            metaX += DrawTrackedText(sb, "/ " + total.ToString("00"), new Vector2(metaX, metaY + 2f),
                AckTheme.TextFaint * alpha, 0.62f, 1f) + 16f;
            DrawTrackedText(sb, RoleTag(role), new Vector2(metaX, metaY + 2f),
                roleCol * (alpha * 0.85f * ease), 0.62f, 2.4f);

            //大号角色名
            const float nameScale = 1.3f;
            float nameY = headerTop + 26f + rise;
            Color nameCol = Color.Lerp(AckTheme.Text, roleCol, 0.25f);
            Utils.DrawBorderString(sb, headerText, new Vector2(leftX, nameY), nameCol * alpha, nameScale);
            float nameH = Font.MeasureString(headerText).Y * nameScale;

            //全宽分割线，文字之下独占一行
            float lineY = nameY + nameH + 8f;
            float curLen = MathF.Max(0f, contentRight - leftX) * AckTheme.EaseOutQuint(reveal);
            if (curLen > 6f) {
                DrawDiamond(sb, new Vector2(leftX, lineY), 5f, roleCol * (alpha * 0.95f));
                DrawGradientLine(sb, new Vector2(leftX + 7f, lineY), new Vector2(leftX + curLen, lineY),
                    roleCol * (alpha * 0.65f), roleCol * (alpha * 0.02f), 1.5f);
                //沿线游走的高光段（清晰线段，非像素堆叠辉光）
                float travel = (time * 0.12f + index * 0.27f) % 1f;
                float hx = leftX + 7f + travel * MathF.Max(0f, curLen - 14f);
                DrawLine(sb, new Vector2(hx - 7f, lineY), new Vector2(hx + 7f, lineY),
                    1.8f, AckTheme.AccentHi * (alpha * 0.5f * ease));
            }
        }

        /// <summary>单列名字行：左侧细引导点 + 名字</summary>
        public static void DrawName(SpriteBatch sb, string name, Vector2 pos, Color baseColor, float alpha, float scale) {
            if (alpha < 0.01f) {
                return;
            }
            DrawDiamond(sb, pos + new Vector2(0f, 8f), 2.4f, AckTheme.Accent * (alpha * 0.55f));
            Utils.DrawBorderString(sb, name, pos + new Vector2(14f, 0f), baseColor * alpha, scale);
        }

        /// <summary>居中名字（捐赠者网格单元），超过 maxWidth 时按比例缩小以免越列重叠</summary>
        public static void DrawNameCentered(SpriteBatch sb, string name, Vector2 center,
            Color baseColor, float alpha, float scale, float maxWidth = 0f) {
            if (alpha < 0.01f) {
                return;
            }
            Vector2 raw = Font.MeasureString(name);
            float s = scale;
            if (maxWidth > 1f && raw.X * s > maxWidth) {
                s = maxWidth / raw.X;
            }
            Vector2 size = raw * s;
            Utils.DrawBorderString(sb, name, center - size * 0.5f, baseColor * alpha, s);
        }

        /// <summary>展示级居中文字：着色器辉光球作底 + 清晰描边正文（替代彩色偏移辉光的拼接观感）</summary>
        public static void DrawDisplayText(SpriteBatch sb, string text, Vector2 center,
            Color textColor, Color glowColor, float scale, float glowAlpha) {
            if (string.IsNullOrEmpty(text)) {
                return;
            }
            Vector2 size = Font.MeasureString(text) * scale;
            if (glowAlpha > 0.01f) {
                DrawGlowOrb(sb, center, MathF.Max(size.X, size.Y) * 0.62f, glowColor, glowAlpha, 1.9f);
            }
            Utils.DrawBorderString(sb, text, center - size * 0.5f, textColor, scale);
        }

        /// <summary>标志绘制：着色器软辉光作底 + 本体</summary>
        public static void DrawLogo(SpriteBatch sb, Texture2D logo, Vector2 center, float scale, float alpha, Color glow) {
            if (logo == null) {
                return;
            }
            Vector2 origin = logo.Size() * 0.5f;
            DrawGlowOrb(sb, center, logo.Width * scale * 0.62f, glow, alpha * 0.55f, 2.2f);
            sb.Draw(logo, center, null, Color.White * alpha, 0f, origin, scale, SpriteEffects.None, 0f);
        }

        /// <summary>屏幕四角取景括号 + 上下边中点刻度，reveal 驱动入场</summary>
        public static void DrawScreenFrame(SpriteBatch sb, float screenW, float screenH, float alpha, float reveal, float time) {
            float ease = AckTheme.EaseOutCubic(reveal);
            float m = 30f;
            float arm = 26f * ease;
            float th = 1.6f;
            Color col = AckTheme.Accent * (alpha * 0.55f);
            DrawBracket(sb, new Vector2(m, m), arm, th, 1, 1, col);
            DrawBracket(sb, new Vector2(screenW - m, m), arm, th, -1, 1, col);
            DrawBracket(sb, new Vector2(m, screenH - m), arm, th, 1, -1, col);
            DrawBracket(sb, new Vector2(screenW - m, screenH - m), arm, th, -1, -1, col);

            //上下边中点的呼吸刻度
            float breath = 0.5f + 0.5f * MathF.Sin(time * 1.6f);
            float tick = 7f * ease;
            Color tickCol = AckTheme.AccentHi * (alpha * (0.3f + breath * 0.3f));
            DrawLine(sb, new Vector2(screenW * 0.5f - tick, m), new Vector2(screenW * 0.5f + tick, m), th, tickCol);
            DrawLine(sb, new Vector2(screenW * 0.5f - tick, screenH - m), new Vector2(screenW * 0.5f + tick, screenH - m), th, tickCol);
        }
        #endregion

        #region 着色器背板
        /// <summary>用 AckBackdrop.fx 绘制全屏 ED 氛围背景；缺失时回退 CPU 纵向渐变 + 暗角</summary>
        public static void DrawBackdrop(SpriteBatch sb, Rectangle rect, float alpha, float progress, Color accent) {
            if (rect.Width < 4 || rect.Height < 4 || alpha < 0.01f) {
                return;
            }
            Effect effect = EffectLoader.AckBackdrop?.Value;
            if (effect == null) {
                DrawBackdropCPU(sb, rect, alpha);
                return;
            }
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(rect.Width, rect.Height));
            effect.Parameters["uProgress"]?.SetValue(AckTheme.Saturate(progress));
            effect.Parameters["uAccent"]?.SetValue(accent.ToVector3());
            ShaderQuad(sb, effect, rect);
        }

        private static void DrawBackdropCPU(SpriteBatch sb, Rectangle rect, float alpha) {
            int bands = 32;
            for (int i = 0; i < bands; i++) {
                float t = i / (float)bands;
                Color c = Color.Lerp(AckTheme.Void, AckTheme.Base, MathF.Pow(t, 0.7f));
                c = Color.Lerp(c, AckTheme.Panel, MathF.Pow(t, 3f) * 0.5f);
                int y0 = rect.Y + (int)(t * rect.Height);
                int y1 = rect.Y + (int)((i + 1) / (float)bands * rect.Height);
                sb.Draw(Pixel, new Rectangle(rect.X, y0, rect.Width, Math.Max(1, y1 - y0)),
                    new Rectangle(0, 0, 1, 1), c * (0.96f * alpha));
            }
            //暗角
            int edge = (int)(rect.Height * 0.18f);
            for (int i = 0; i < edge; i++) {
                float a = (1f - i / (float)edge) * 0.5f * alpha;
                Color vc = Color.Black * a;
                sb.Draw(Pixel, new Rectangle(rect.X, rect.Y + i, rect.Width, 1), new Rectangle(0, 0, 1, 1), vc);
                sb.Draw(Pixel, new Rectangle(rect.X, rect.Bottom - 1 - i, rect.Width, 1), new Rectangle(0, 0, 1, 1), vc);
            }
        }

        /// <summary>用 AckFinale.fx 绘制谢幕辉光场；缺失时回退 CPU 同心柔光</summary>
        public static void DrawFinaleAura(SpriteBatch sb, Vector2 center, float radius,
            float alpha, float intensity, Color accent) {
            if (radius < 2f || alpha < 0.01f) {
                return;
            }
            Effect effect = EffectLoader.AckFinale?.Value;
            if (effect == null) {
                for (int i = 0; i < 5; i++) {
                    float t = i / 5f;
                    DrawArc(sb, center, 0f, radius * (0.3f + t * 0.7f), 0f, MathHelper.TwoPi,
                        accent * (0.16f * (1f - t) * intensity * alpha));
                }
                return;
            }
            Rectangle rect = new((int)(center.X - radius), (int)(center.Y - radius), (int)(radius * 2f), (int)(radius * 2f));
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(rect.Width, rect.Height));
            effect.Parameters["uIntensity"]?.SetValue(AckTheme.Saturate(intensity));
            effect.Parameters["uAccent"]?.SetValue(accent.ToVector3());
            ShaderQuad(sb, effect, rect);
        }

        /// <summary>用 AckGlow.fx 绘制软径向辉光球；缺失时回退 CPU 同心柔光</summary>
        public static void DrawGlowOrb(SpriteBatch sb, Vector2 center, float radius, Color color, float alpha, float falloff = 2.4f) {
            if (radius < 1f || alpha < 0.01f) {
                return;
            }
            Effect effect = EffectLoader.AckGlow?.Value;
            if (effect == null) {
                DrawSoftGlow(sb, center, radius, color * AckTheme.Saturate(alpha));
                return;
            }
            Rectangle rect = new((int)(center.X - radius), (int)(center.Y - radius), (int)(radius * 2f), (int)(radius * 2f));
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uAlpha"]?.SetValue(AckTheme.Saturate(alpha));
            effect.Parameters["uFalloff"]?.SetValue(falloff);
            effect.Parameters["uAccent"]?.SetValue(color.ToVector3());
            ShaderQuad(sb, effect, rect);
        }

        public static void DrawEffectQuad(SpriteBatch sb, Effect effect, Rectangle dest) => ShaderQuad(sb, effect, dest);

        /// <summary>切到 Immediate 模式应用效果绘制四边形后恢复 Deferred</summary>
        private static void ShaderQuad(SpriteBatch sb, Effect effect, Rectangle dest) {
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.UIScaleMatrix);
            sb.Draw(Pixel, dest, Color.White);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }
        #endregion
    }
}
