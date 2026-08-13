using CalamityOverhaul.Common;
using CalamityOverhaul.Content.QuestLogs.Core;
using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.QuestLogs.Styles.Chronicle
{
    /// <summary>
    /// 「远征纪要」的手工笔刷：纸墨字、压痕凹槽、烫金压线、凿槽刻度、蜡封、手绘墨路、圈注。<br/>
    /// 形状一律交给 <see cref="SvgPathPen"/> 或折线笔身——矩形只出现在着色器载体、
    /// 裁剪与纯色底三处，绝不用来当"边框"和"按钮底盒"
    /// </summary>
    internal static class ChroniclePen
    {
        private static Texture2D Pixel => VaultAsset.placeholder2.Value;
        private static readonly Rectangle PixelSrc = new(0, 0, 1, 1);

        #region 纹样路径

        //整圆，节点窝与刻度环共用
        private const string RingD =
            "M 0,-1 C 0.5523,-1 1,-0.5523 1,0 C 1,0.5523 0.5523,1 0,1"
            + " C -0.5523,1 -1,0.5523 -1,0 C -1,-0.5523 -0.5523,-1 0,-1 Z";

        //手圈：绕一整圈后甩出一条过头的尾巴，起收不闭合——铅笔圈出来的样子
        private const string CircleMarkD =
            "M 0.04,-1 C 0.62,-1.03 1.05,-0.57 1.0,0.04"
            + " C 0.96,0.63 0.53,1.04 -0.06,0.99"
            + " C -0.63,0.94 -1.03,0.49 -0.98,-0.12"
            + " C -0.94,-0.67 -0.55,-1.01 0.07,-0.97"
            + " C 0.39,-0.95 0.66,-0.83 0.86,-0.58";

        //蜡封饼：五瓣不等径的溢蜡边，刻意不对称
        private const string SealBlobD =
            "M 0.02,-0.97 C 0.63,-0.99 1.03,-0.55 0.94,0.09"
            + " C 0.87,0.6 0.47,1.02 -0.11,0.96"
            + " C -0.66,0.9 -1.02,0.44 -0.93,-0.16"
            + " C -0.85,-0.67 -0.5,-0.95 0.02,-0.97 Z";

        //蜡封压纹：一记山形 + 一横，印章咬进蜡里
        private const string SealMarkD =
            "M -0.46,0.3 L 0.0,-0.42 L 0.46,0.3 M -0.26,0.08 L 0.26,0.08";

        //蜡封裂缝：拆封后的一道断口，带岔口
        private const string SealCrackD =
            "M -0.72,-0.18 L -0.16,0.06 L 0.1,-0.2 L 0.68,0.12 M 0.1,-0.2 L 0.2,0.6";

        #endregion

        #region 桌面

        /// <summary>皮革桌板 + 摊开的双页纸，着色器缺席时退回 CPU 纸面</summary>
        public static void DrawSurface(SpriteBatch sb, in QuestLogLayout layout, float alpha, float time) {
            Rectangle full = layout.Full;
            Effect effect = EffectLoader.QuestChronicleBg?.Value;
            if (effect == null) {
                DrawSurfaceFallback(sb, in layout, alpha, time);
                return;
            }

            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(full.Width, full.Height));
            effect.Parameters["uBodyTop"]?.SetValue((float)layout.Rail.Y);
            effect.Parameters["uBodyBottom"]?.SetValue((float)layout.Rail.Bottom);
            effect.Parameters["uGutterX"]?.SetValue((float)layout.Rail.Right);
            effect.Parameters["uColLeather"]?.SetValue(ChroniclePalette.Vec3(ChroniclePalette.Leather));
            effect.Parameters["uColPaper"]?.SetValue(ChroniclePalette.Vec3(ChroniclePalette.Paper));
            effect.Parameters["uColPaperDeep"]?.SetValue(ChroniclePalette.Vec3(ChroniclePalette.PaperDeep));
            effect.Parameters["uColSeal"]?.SetValue(ChroniclePalette.Vec3(ChroniclePalette.Seal));

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp, DepthStencilState.None,
                RasterizerState.CullNone, effect, Main.UIScaleMatrix);

            sb.Draw(Pixel, full, Color.White);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }

        /// <summary>CPU 降级桌面：皮面 + 纸面 + 纤维 + 页缘吃暗 + 中缝，仍不画一条边框</summary>
        private static void DrawSurfaceFallback(SpriteBatch sb, in QuestLogLayout layout, float alpha, float time) {
            Rectangle full = layout.Full;
            sb.Draw(Pixel, full, PixelSrc, ChroniclePalette.Leather * alpha);

            //皮面云斑：几十块随机浅色区，靠散列取位
            for (int i = 0; i < 42; i++) {
                float x = QuestLogTheme.Hash01(i * 13 + 1) * full.Width;
                float y = QuestLogTheme.Hash01(i * 29 + 7) * full.Height;
                float w = 40f + QuestLogTheme.Hash01(i * 37 + 3) * 120f;
                float h = 24f + QuestLogTheme.Hash01(i * 41 + 5) * 70f;
                sb.Draw(Pixel, new Vector2(x, y), PixelSrc,
                    ChroniclePalette.LeatherDeep * (alpha * 0.18f), 0f, Vector2.Zero,
                    new Vector2(w, h), SpriteEffects.None, 0f);
            }

            Rectangle paper = new(14, layout.Rail.Y, full.Width - 28, layout.Rail.Height);
            //纸落在皮面上的贴身投影，只偏不放大
            sb.Draw(Pixel, new Rectangle(paper.X + 3, paper.Y + 4, paper.Width, paper.Height), PixelSrc,
                ChroniclePalette.LeatherDeep * (alpha * 0.5f));
            sb.Draw(Pixel, paper, PixelSrc, ChroniclePalette.Paper * alpha);

            //帘纹纤维：位置与长度全走散列，每帧稳定但读不出规律
            int fibers = Math.Max(24, paper.Height / 14);
            for (int i = 0; i < fibers; i++) {
                float y = paper.Y + paper.Height * QuestLogTheme.Hash01(i * 71 + 11);
                float len = paper.Width * (0.28f + QuestLogTheme.Hash01(i * 53 + 17) * 0.62f);
                float x = paper.X + (paper.Width - len) * QuestLogTheme.Hash01(i * 31 + 23);
                sb.Draw(Pixel, new Vector2(x, y), PixelSrc,
                    ChroniclePalette.PaperDeep * (alpha * 0.08f), 0f, Vector2.Zero,
                    new Vector2(len, 1f), SpriteEffects.None, 0f);
            }

            //页缘吃暗
            for (int i = 0; i < 14; i++) {
                float fade = 1f - i / 14f;
                Color edge = ChroniclePalette.PaperDeep * (alpha * 0.10f * fade * fade);
                sb.Draw(Pixel, new Rectangle(paper.X + i, paper.Y, 1, paper.Height), PixelSrc, edge);
                sb.Draw(Pixel, new Rectangle(paper.Right - i - 1, paper.Y, 1, paper.Height), PixelSrc, edge);
                sb.Draw(Pixel, new Rectangle(paper.X, paper.Y + i, paper.Width, 1), PixelSrc, edge);
                sb.Draw(Pixel, new Rectangle(paper.X, paper.Bottom - i - 1, paper.Width, 1), PixelSrc, edge);
            }

            //装订中缝：沟底吃暗 + 两侧隆起受光
            int gutterX = layout.Rail.Right;
            for (int i = 0; i < 12; i++) {
                float fade = 1f - i / 12f;
                Color shade = ChroniclePalette.LeatherDeep * (alpha * 0.16f * fade * fade);
                sb.Draw(Pixel, new Rectangle(gutterX - i, paper.Y, 1, paper.Height), PixelSrc, shade);
                sb.Draw(Pixel, new Rectangle(gutterX + i, paper.Y, 1, paper.Height), PixelSrc, shade);
            }
            sb.Draw(Pixel, new Rectangle(gutterX - 26, paper.Y, 1, paper.Height), PixelSrc,
                ChroniclePalette.Candle * (alpha * 0.05f));
            sb.Draw(Pixel, new Rectangle(gutterX + 26, paper.Y, 1, paper.Height), PixelSrc,
                ChroniclePalette.Candle * (alpha * 0.05f));
        }

        #endregion

        #region 纸墨字

        /// <summary>
        /// 纸面墨字：单次绘制。纸上禁用四向描边——黑描边叠褐墨会糊成一团
        /// </summary>
        public static void Ink(SpriteBatch sb, DynamicSpriteFont font, string text, Vector2 pos,
            Color color, float scale, float alpha = 1f) {
            if (string.IsNullOrEmpty(text) || alpha <= 0.01f) {
                return;
            }
            sb.DrawString(font, text, pos, color * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        /// <summary>居中墨字</summary>
        public static void InkCentered(SpriteBatch sb, DynamicSpriteFont font, string text, Vector2 center,
            Color color, float scale, float alpha = 1f) {
            if (string.IsNullOrEmpty(text) || alpha <= 0.01f) {
                return;
            }
            Vector2 size = font.MeasureString(text) * scale;
            sb.DrawString(font, text, center - size * 0.5f, color * alpha, 0f, Vector2.Zero,
                scale, SpriteEffects.None, 0f);
        }

        /// <summary>皮面上的浅色刻字：暗压痕 + 亮填漆，皮革对比度不够故保留一层影</summary>
        public static void LeatherInk(SpriteBatch sb, DynamicSpriteFont font, string text, Vector2 pos,
            Color color, float scale, float alpha = 1f) {
            if (string.IsNullOrEmpty(text) || alpha <= 0.01f) {
                return;
            }
            sb.DrawString(font, text, pos + new Vector2(0f, 1.2f), ChroniclePalette.LeatherDeep * (alpha * 0.85f),
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sb.DrawString(font, text, pos, color * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        /// <summary>按宽度断行，返回每行文本</summary>
        public static List<string> Wrap(DynamicSpriteFont font, string text, float maxWidth, float scale) {
            List<string> lines = [];
            if (string.IsNullOrEmpty(text)) {
                return lines;
            }
            foreach (string paragraph in text.Split('\n')) {
                string current = string.Empty;
                foreach (char c in paragraph) {
                    string probe = current + c;
                    if (font.MeasureString(probe).X * scale > maxWidth && current.Length > 0) {
                        lines.Add(current);
                        current = c.ToString();
                    }
                    else {
                        current = probe;
                    }
                }
                lines.Add(current);
            }
            return lines;
        }

        #endregion

        #region 压痕与压线

        /// <summary>
        /// 压痕凹槽：上缘吃暗一线 + 下唇受光一线。<br/>
        /// 分栏与行间一律用它，不用"1px 描边矩形"
        /// </summary>
        public static void Groove(SpriteBatch sb, Vector2 left, float length, float alpha, bool onPaper = true) {
            if (length < 2f || alpha <= 0.01f) {
                return;
            }
            Color dark = onPaper ? ChroniclePalette.PaperDeep : ChroniclePalette.LeatherDeep;
            Color lip = onPaper ? ChroniclePalette.Candle : ChroniclePalette.BrassHi;
            sb.Draw(Pixel, left, PixelSrc, dark * (alpha * 0.55f), 0f, Vector2.Zero,
                new Vector2(length, 1f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, left + new Vector2(0f, 1f), PixelSrc, lip * (alpha * 0.16f), 0f, Vector2.Zero,
                new Vector2(length, 1f), SpriteEffects.None, 0f);
        }

        /// <summary>
        /// 烫金压线：先一道压进纸里的暗痕，再一道金。<br/>
        /// 尾端按 fadeTail 渐隐，两头等亮会读成 css 分割线
        /// </summary>
        public static void GiltRule(SpriteBatch sb, Vector2 left, float length, float alpha,
            float thickness = 1.2f, bool fadeTail = true) {
            if (length < 2f || alpha <= 0.01f) {
                return;
            }
            int steps = Math.Max(6, (int)(length / 18f));
            float segLen = length / steps;
            for (int i = 0; i < steps; i++) {
                float t = i / (float)(steps - 1);
                float fade = fadeTail ? MathHelper.Lerp(1f, 0.06f, t * t) : 1f;
                Vector2 pos = left + new Vector2(i * segLen, 0f);
                sb.Draw(Pixel, pos + new Vector2(0f, thickness), PixelSrc,
                    ChroniclePalette.GoldDeep * (alpha * 0.35f * fade), 0f, Vector2.Zero,
                    new Vector2(segLen + 1f, 1f), SpriteEffects.None, 0f);
                sb.Draw(Pixel, pos, PixelSrc, ChroniclePalette.Gold * (alpha * 0.8f * fade),
                    0f, Vector2.Zero, new Vector2(segLen + 1f, thickness), SpriteEffects.None, 0f);
            }
        }

        /// <summary>行间发丝线：极淡一道，取代列表行底盒</summary>
        public static void HairLine(SpriteBatch sb, Vector2 left, float length, float alpha) {
            if (length < 2f || alpha <= 0.01f) {
                return;
            }
            sb.Draw(Pixel, left, PixelSrc, ChroniclePalette.PaperDeep * (alpha * 0.3f), 0f, Vector2.Zero,
                new Vector2(length, 1f), SpriteEffects.None, 0f);
        }

        /// <summary>一段任意角度的笔身</summary>
        public static void Line(SpriteBatch sb, Vector2 from, Vector2 to, float thickness, Color color, float alpha) {
            Vector2 edge = to - from;
            float len = edge.Length();
            if (len < 0.1f || alpha <= 0.01f) {
                return;
            }
            sb.Draw(Pixel, from, PixelSrc, color * alpha, edge.ToRotation(),
                new Vector2(0f, 0.5f), new Vector2(len + 0.6f, thickness), SpriteEffects.None, 0f);
        }

        #endregion

        #region 凿槽刻度

        /// <summary>
        /// 凿槽式刻度条：槽 + 齿 + 金填。<br/>
        /// 填充前沿按散列啃出毛口，不给一个齐平的矩形端面
        /// </summary>
        public static void Tally(SpriteBatch sb, Rectangle rect, float ratio, int segments, float alpha) {
            if (rect.Width < 8 || alpha <= 0.01f) {
                return;
            }
            ratio = MathHelper.Clamp(ratio, 0f, 1f);

            //槽：底吃暗，下唇受光
            sb.Draw(Pixel, rect, PixelSrc, ChroniclePalette.LeatherDeep * (alpha * 0.55f));
            sb.Draw(Pixel, new Rectangle(rect.X, rect.Bottom, rect.Width, 1), PixelSrc,
                ChroniclePalette.BrassHi * (alpha * 0.18f));

            //金填：逐段填，前沿毛口
            float fillW = rect.Width * ratio;
            int filled = (int)(segments * ratio);
            float segW = rect.Width / (float)segments;
            for (int i = 0; i < segments; i++) {
                float x = rect.X + i * segW;
                float w = segW - 1.4f;
                if (w < 1f) {
                    w = 1f;
                }
                bool on = i < filled;
                if (!on) {
                    continue;
                }
                //每段高度略有出入，像逐格凿进去的
                float shrink = QuestLogTheme.Hash01(i * 17 + 5) * 1.6f;
                sb.Draw(Pixel, new Vector2(x, rect.Y + shrink), PixelSrc,
                    ChroniclePalette.Gold * (alpha * 0.85f), 0f, Vector2.Zero,
                    new Vector2(w, rect.Height - shrink), SpriteEffects.None, 0f);
                sb.Draw(Pixel, new Vector2(x, rect.Y + shrink), PixelSrc,
                    ChroniclePalette.GoldHi * (alpha * 0.30f), 0f, Vector2.Zero,
                    new Vector2(w, 1f), SpriteEffects.None, 0f);
            }
            //末段的半格：宽度按余量走，端面留毛口
            float partial = segments * ratio - filled;
            if (partial > 0.02f && filled < segments) {
                float x = rect.X + filled * segW;
                float w = MathF.Max(1f, (segW - 1.4f) * partial);
                sb.Draw(Pixel, new Vector2(x, rect.Y + 1f), PixelSrc,
                    ChroniclePalette.Gold * (alpha * 0.55f), 0f, Vector2.Zero,
                    new Vector2(w, rect.Height - 2f), SpriteEffects.None, 0f);
            }

            //刻齿：每格一短齿，第五格加长
            for (int i = 0; i <= segments; i++) {
                float x = rect.X + i * segW;
                bool major = i % 5 == 0;
                float h = major ? 5f : 3f;
                Color tick = i <= filled ? ChroniclePalette.GoldDeep : ChroniclePalette.LeatherDeep;
                sb.Draw(Pixel, new Vector2(x, rect.Bottom + 1f), PixelSrc, tick * (alpha * 0.7f),
                    0f, Vector2.Zero, new Vector2(1f, h), SpriteEffects.None, 0f);
            }

            //前沿一点余光，标出"进度停在这里"
            if (ratio > 0.01f && ratio < 0.999f) {
                SvgPathPen.SoftDot(sb, new Vector2(rect.X + fillW, rect.Center.Y), 4.5f,
                    ChroniclePalette.GoldHi, alpha * 0.5f);
            }
        }

        #endregion

        #region 蜡封

        /// <summary>
        /// 蜡封：不等径的溢蜡饼 + 压纹 + 溢出的一滴。<br/>
        /// broken 为真时压上一道裂缝并整体压暗——已拆封
        /// </summary>
        public static void WaxSeal(SpriteBatch sb, Vector2 center, float radius, float alpha,
            int seed, float time, bool broken, bool live = false) {
            if (radius < 3f || alpha <= 0.01f) {
                return;
            }
            //每一枚的倾角与径向都不同，盖章的手不会两次一样
            float tilt = (QuestLogTheme.Hash01(seed * 7 + 1) - 0.5f) * 0.9f;
            float r = radius * (0.92f + QuestLogTheme.Hash01(seed * 11 + 3) * 0.16f);
            float breath = live ? QuestLogTheme.Breath(time, seed * 0.7f, 1.9f) : 0f;

            SvgPath blob = SvgPathPen.Path(SealBlobD);
            //贴身投影，只偏不放大
            SvgPathPen.Stroke(sb, blob, center + new Vector2(1.2f, 1.8f), r, tilt,
                ChroniclePalette.LeatherDeep, r * 0.92f, alpha * 0.35f);
            //蜡体：粗笔当体
            Color body = broken ? ChroniclePalette.SealDeep : ChroniclePalette.Seal;
            SvgPathPen.Stroke(sb, blob, center, r, tilt, body, r * 0.9f, alpha * 0.95f);
            //受光的一侧偏左上，蜡面是凸的
            SvgPathPen.Stroke(sb, blob, center - new Vector2(r * 0.16f, r * 0.2f), r * 0.72f, tilt,
                broken ? ChroniclePalette.Seal : ChroniclePalette.SealHi, r * 0.5f,
                alpha * (broken ? 0.35f : 0.5f + breath * 0.14f));
            //边缘一线深色，蜡冷了会收边
            SvgPathPen.Stroke(sb, blob, center, r, tilt, ChroniclePalette.SealDeep, 1.4f, alpha * 0.8f);

            //压纹：印章咬进去的凹痕，故用暗色
            SvgPath mark = SvgPathPen.Path(SealMarkD);
            SvgPathPen.Stroke(sb, mark, center, r * 0.62f, tilt, ChroniclePalette.SealDeep,
                MathF.Max(1.2f, r * 0.12f), alpha * 0.85f);
            SvgPathPen.Stroke(sb, mark, center - new Vector2(0.6f, 0.8f), r * 0.62f, tilt,
                ChroniclePalette.SealHi, MathF.Max(1f, r * 0.07f), alpha * (broken ? 0.18f : 0.34f));

            if (broken) {
                //裂缝：一道断口带岔口，缝里透纸色
                SvgPath crack = SvgPathPen.Path(SealCrackD);
                SvgPathPen.Stroke(sb, crack, center, r * 0.95f, tilt,
                    ChroniclePalette.SealDeep, 2.2f, alpha * 0.9f);
                SvgPathPen.Stroke(sb, crack, center + new Vector2(0.8f, 0f), r * 0.95f, tilt,
                    ChroniclePalette.PaperDeep, 1f, alpha * 0.5f);
            }
            else {
                //未拆：蜡还亮着，慢呼吸
                SvgPathPen.SoftDot(sb, center - new Vector2(r * 0.2f, r * 0.24f), r * 0.55f,
                    ChroniclePalette.SealHi, alpha * (0.16f + breath * 0.12f));
                //溢出的一小滴，位置由散列定，只在一侧
                float dropAngle = QuestLogTheme.Hash01(seed * 19 + 7) * MathHelper.TwoPi;
                Vector2 drop = center + dropAngle.ToRotationVector2() * r * 0.92f;
                SvgPathPen.Stroke(sb, SvgPathPen.Path(RingD), drop, r * 0.2f, 0f,
                    body, r * 0.2f, alpha * 0.8f);
            }
        }

        #endregion

        #region 节点窝与圈注

        /// <summary>
        /// 凿进纸里的圆窝：暗环 + 右下受光内唇 + 四记测量刻痕。<br/>
        /// 不是一枚浮在纸上的圆片，故没有底色填充
        /// </summary>
        public static void NodeWell(SpriteBatch sb, Vector2 center, float radius, Color ring,
            float alpha, float ringThickness = 1.6f) {
            SvgPath circle = SvgPathPen.Path(RingD);
            //窝底比纸略暗，靠一层极淡的深纸色，不是灰片
            SvgPathPen.Stroke(sb, circle, center, radius * 0.5f, 0f,
                ChroniclePalette.PaperDeep, radius * 1.02f, alpha * 0.22f);
            //受光内唇：凹面的光在右下
            SvgPathPen.Stroke(sb, circle, center + new Vector2(0.9f, 1.2f), radius, 0f,
                ChroniclePalette.Candle, ringThickness * 0.7f, alpha * 0.22f);
            //环身
            SvgPathPen.Stroke(sb, circle, center, radius, 0f, ring, ringThickness, alpha * 0.9f);

            //四记刻痕：测绘的方位标，长度略有出入
            for (int i = 0; i < 4; i++) {
                float ang = MathHelper.PiOver2 * i - MathHelper.PiOver4;
                float len = 3.4f + QuestLogTheme.Hash01(i * 23 + 9) * 2.2f;
                Vector2 dir = ang.ToRotationVector2();
                Line(sb, center + dir * (radius + 1.5f), center + dir * (radius + 1.5f + len),
                    1.2f, ring, alpha * 0.7f);
            }
        }

        /// <summary>45° 影线：未点亮的条目留白但不空白，弦长实算故不出圆外</summary>
        public static void HatchDisc(SpriteBatch sb, Vector2 center, float radius, Color color, float alpha) {
            float step = 4.2f;
            for (float y = -radius + step * 0.5f; y < radius; y += step) {
                float half = MathF.Sqrt(MathF.Max(0f, radius * radius - y * y));
                if (half < 0.6f) {
                    continue;
                }
                //整条影线绕中心转 45°，端点贴着圆
                Vector2 a = center + new Vector2(-half, y).RotatedBy(-MathHelper.PiOver4);
                Vector2 b = center + new Vector2(half, y).RotatedBy(-MathHelper.PiOver4);
                Line(sb, a, b, 1f, color, alpha * 0.42f);
            }
        }

        /// <summary>
        /// 手圈：绕一圈甩出过头的尾巴，倾角随 seed。<br/>
        /// 悬停用它取代"高亮方框"
        /// </summary>
        public static void CircleMark(SpriteBatch sb, Vector2 center, float radius, Color color,
            float alpha, int seed, float reveal = 1f) {
            SvgPath mark = SvgPathPen.Path(CircleMarkD);
            float tilt = (QuestLogTheme.Hash01(seed * 13 + 5) - 0.5f) * 0.7f;
            //笔身 + 亮芯，起笔略重
            SvgPathPen.Stroke(sb, mark, center, radius, tilt, color, 1.9f, alpha * 0.85f,
                0f, MathHelper.Clamp(reveal, 0f, 1f));
        }

        #endregion

        #region 手绘墨路

        /// <summary>
        /// 手绘墨路：折线笔身带确定性抖动，起笔重收笔轻。<br/>
        /// 未通=虚线淡墨，已通=墨底 + 一线金 + 巡行的一点亮
        /// </summary>
        public static void InkRoute(SpriteBatch sb, Vector2 from, Vector2 to, bool unlocked,
            float alpha, int seed, float time) {
            Vector2 delta = to - from;
            float len = delta.Length();
            if (len < 6f || alpha <= 0.01f) {
                return;
            }
            Vector2 dir = delta / len;
            Vector2 perp = new(-dir.Y, dir.X);

            //控制点：两端钉在节点上，中途按散列偏离，笔走不直
            const int Ctrl = 5;
            Span<Vector2> ctrl = stackalloc Vector2[Ctrl];
            float wobble = MathF.Min(7f, len * 0.06f);
            for (int i = 0; i < Ctrl; i++) {
                float t = i / (float)(Ctrl - 1);
                //端点收敛到 0，中段最大
                float envelope = MathF.Sin(t * MathHelper.Pi);
                float off = (QuestLogTheme.Hash01(seed * 31 + i * 7) - 0.5f) * 2f * wobble * envelope;
                float along = (QuestLogTheme.Hash01(seed * 17 + i * 11) - 0.5f) * len * 0.03f * envelope;
                ctrl[i] = from + dir * (len * t + along) + perp * off;
            }

            //Catmull-Rom 细分成笔身
            const int Steps = 18;
            Span<Vector2> pts = stackalloc Vector2[Steps + 1];
            for (int i = 0; i <= Steps; i++) {
                pts[i] = CatmullRom(ctrl, i / (float)Steps);
            }

            Color ink = unlocked ? ChroniclePalette.Ink : ChroniclePalette.InkFaint;

            //洇开：笔身下一层极淡的宽墨，墨吃进纸纤维
            for (int i = 0; i < Steps; i++) {
                Line(sb, pts[i], pts[i + 1], 5.5f, ChroniclePalette.PaperDeep, alpha * 0.10f);
            }

            for (int i = 0; i < Steps; i++) {
                float t = i / (float)Steps;
                //未通=虚线，跳段而不是画一条淡实线
                if (!unlocked && (i % 4 == 2 || i % 4 == 3)) {
                    continue;
                }
                float w = unlocked ? MathHelper.Lerp(2.6f, 1.3f, t) : 1.5f;
                Line(sb, pts[i], pts[i + 1], w, ink, alpha * (unlocked ? 0.9f : 0.55f));
            }

            if (!unlocked) {
                return;
            }

            //一线金压在墨上，略偏一侧当受光边
            for (int i = 0; i < Steps; i++) {
                float t = i / (float)Steps;
                float w = MathHelper.Lerp(1.2f, 0.7f, t);
                Line(sb, pts[i] - perp * 0.7f, pts[i + 1] - perp * 0.7f, w,
                    ChroniclePalette.Gold, alpha * 0.55f);
            }

            //积墨：笔锋停顿处的小点，只落在偏离最大的两处
            for (int i = 1; i < Ctrl - 1; i++) {
                if (QuestLogTheme.Hash01(seed * 41 + i) < 0.45f) {
                    continue;
                }
                SvgPathPen.SoftDot(sb, ctrl[i], 2.6f, ChroniclePalette.Ink, alpha * 0.30f);
            }

            //巡行的一点亮：这条路是通的
            float head = (time * 0.17f + QuestLogTheme.Hash01(seed * 53 + 3)) % 1f;
            int gleam = (int)(head * Steps);
            for (int i = 0; i < 3; i++) {
                int idx = Math.Clamp(gleam + i, 0, Steps - 1);
                float fade = 1f - i / 3f;
                Line(sb, pts[idx], pts[idx + 1], 1.5f, ChroniclePalette.GoldHi, alpha * 0.5f * fade);
            }
            SvgPathPen.SoftDot(sb, pts[Math.Clamp(gleam, 0, Steps)], 3.4f,
                ChroniclePalette.GoldHi, alpha * 0.4f);
        }

        private static Vector2 CatmullRom(Span<Vector2> ctrl, float t) {
            int n = ctrl.Length - 1;
            float scaled = MathHelper.Clamp(t, 0f, 1f) * n;
            int i = Math.Min((int)scaled, n - 1);
            float local = scaled - i;
            Vector2 p0 = ctrl[Math.Max(i - 1, 0)];
            Vector2 p1 = ctrl[i];
            Vector2 p2 = ctrl[i + 1];
            Vector2 p3 = ctrl[Math.Min(i + 2, n)];
            return Vector2.CatmullRom(p0, p1, p2, p3, local);
        }

        #endregion

        #region 黄铜活儿

        /// <summary>
        /// 黄铜牌：两块交叠矩形取并集得切角外形（四角不会被戳穿），
        /// 受光在左上、吃暗在右下，两枚斜置方钉。悬停时一道斜向拉丝光泽慢移
        /// </summary>
        public static void BrassTag(SpriteBatch sb, Rectangle rect, bool hovered, float alpha, float time) {
            if (rect.Width < 6 || alpha <= 0.01f) {
                return;
            }
            int chamfer = Math.Max(2, Math.Min(rect.Width, rect.Height) / 6);
            Rectangle wide = new(rect.X, rect.Y + chamfer, rect.Width, rect.Height - chamfer * 2);
            Rectangle tall = new(rect.X + chamfer, rect.Y, rect.Width - chamfer * 2, rect.Height);

            //贴身投影
            sb.Draw(Pixel, new Rectangle(wide.X + 2, wide.Y + 2, wide.Width, wide.Height), PixelSrc,
                ChroniclePalette.LeatherDeep * (alpha * 0.5f));
            sb.Draw(Pixel, new Rectangle(tall.X + 2, tall.Y + 2, tall.Width, tall.Height), PixelSrc,
                ChroniclePalette.LeatherDeep * (alpha * 0.5f));

            Color body = hovered ? ChroniclePalette.Brass : Color.Lerp(ChroniclePalette.Brass,
                ChroniclePalette.BrassDeep, 0.35f);
            sb.Draw(Pixel, wide, PixelSrc, body * alpha);
            sb.Draw(Pixel, tall, PixelSrc, body * alpha);

            //机加工棱线：左上受光，右下吃暗
            sb.Draw(Pixel, new Rectangle(tall.X, rect.Y, tall.Width, 1), PixelSrc,
                ChroniclePalette.BrassHi * (alpha * 0.55f));
            sb.Draw(Pixel, new Rectangle(rect.X, wide.Y, 1, wide.Height), PixelSrc,
                ChroniclePalette.BrassHi * (alpha * 0.4f));
            sb.Draw(Pixel, new Rectangle(tall.X, rect.Bottom - 1, tall.Width, 1), PixelSrc,
                ChroniclePalette.BrassDeep * (alpha * 0.7f));
            sb.Draw(Pixel, new Rectangle(rect.Right - 1, wide.Y, 1, wide.Height), PixelSrc,
                ChroniclePalette.BrassDeep * (alpha * 0.7f));

            //铆钉：斜置方钉 + 受光点
            for (int i = 0; i < 2; i++) {
                Vector2 rivet = new(i == 0 ? rect.X + 4.5f : rect.Right - 4.5f, rect.Center.Y);
                sb.Draw(Pixel, rivet, PixelSrc, ChroniclePalette.BrassDeep * (alpha * 0.9f),
                    MathHelper.PiOver4, new Vector2(0.5f), new Vector2(2.6f), SpriteEffects.None, 0f);
                sb.Draw(Pixel, rivet - new Vector2(0.6f), PixelSrc, ChroniclePalette.BrassHi * (alpha * 0.6f),
                    MathHelper.PiOver4, new Vector2(0.5f), new Vector2(1.2f), SpriteEffects.None, 0f);
            }

            if (!hovered) {
                return;
            }
            //拉丝光泽：一条斜带缓慢扫过牌面
            float sweep = (time * 0.22f) % 1.4f - 0.2f;
            float x = rect.X + rect.Width * sweep;
            for (int i = -2; i <= 2; i++) {
                float fade = 1f - MathF.Abs(i) / 3f;
                Line(sb, new Vector2(x + i * 2f, rect.Y - 1f),
                    new Vector2(x + i * 2f + rect.Height * 0.5f, rect.Bottom + 1f),
                    2f, ChroniclePalette.BrassHi, alpha * 0.14f * fade);
            }
        }

        #endregion
    }
}
