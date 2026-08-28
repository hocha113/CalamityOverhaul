using CalamityOverhaul.Common;
using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.Blessings.UI
{
    /// <summary>一盏待画的魂焰：珠位与引魂灯共用</summary>
    internal struct FlameCell
    {
        public Rectangle Rect;
        public float Seed;
        public float Lit;
        public float Alpha;
    }

    /// <summary>
    /// 祝福界面绘制件：往生轮盘底（<see cref="EffectLoader.AsuraBlessWheel"/>）、
    /// 魂焰（<see cref="EffectLoader.AsuraBlessFlame"/>）、符纹矢量与亮环。
    /// 批处理契约照 GameModeRenderer.ShaderQuad：End → Immediate+effect → 画 quad → 恢复 Deferred；
    /// shader 缺编时一律走诚实 CPU 回退
    /// </summary>
    internal static class BlessingRenderer
    {
        private static Texture2D Pixel => VaultAsset.placeholder2.Value;
        private static readonly Rectangle One = new(0, 0, 1, 1);

        //——往生轮盘底——

        /// <summary>全屏盘底：黑暗虚空 + 缓旋墨轮。alpha 挂开合进度</summary>
        internal static void DrawWheelBackground(SpriteBatch sb, float alpha, float burning) {
            if (alpha <= 0.01f) {
                return;
            }
            var full = new Rectangle(0, 0, (int)BlessingTheme.UIScreenW, (int)BlessingTheme.UIScreenH);
            Effect effect = EffectLoader.AsuraBlessWheel?.Value;
            if (effect == null) {
                //回退：黑幕 + 双细环
                sb.Draw(Pixel, full, One, Color.Black * (0.86f * alpha));
                Vector2 c = BlessingTheme.WheelCenter;
                float r = BlessingTheme.WheelRadius;
                DrawRing(sb, c, r, 2.5f, BlessingTheme.Accent * (0.7f * alpha));
                DrawRing(sb, c, r * 0.62f, 1.5f, BlessingTheme.Accent * (0.35f * alpha));
                return;
            }

            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(full.Width, full.Height));
            effect.Parameters["uCenter"]?.SetValue(BlessingTheme.WheelCenter);
            effect.Parameters["uRadius"]?.SetValue(BlessingTheme.WheelRadius);
            effect.Parameters["uAccent"]?.SetValue(BlessingTheme.Accent.ToVector3());
            effect.Parameters["uEmber"]?.SetValue(BlessingTheme.Ember.ToVector3());
            effect.Parameters["uBurning"]?.SetValue(burning);
            ShaderQuad(sb, effect, full);
        }

        //——魂焰——

        /// <summary>
        /// 一组魂焰共用一次 Immediate 批（Immediate 逐 Draw 冲刷，参数可逐盏改）；
        /// shader 缺编时回退为软辉两层叠焰
        /// </summary>
        internal static void DrawFlames(SpriteBatch sb, List<FlameCell> cells) {
            if (cells.Count == 0) {
                return;
            }
            Effect effect = EffectLoader.AsuraBlessFlame?.Value;
            if (effect == null) {
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (glow == null) {
                    return;
                }
                foreach (FlameCell cell in cells) {
                    if (cell.Lit <= 0.01f || cell.Alpha <= 0.01f) {
                        continue;
                    }
                    Vector2 c = cell.Rect.Center.ToVector2();
                    float s = cell.Rect.Width / (float)glow.Width;
                    Color ember = BlessingTheme.Ember;
                    Color accent = BlessingTheme.Accent;
                    sb.Draw(glow, c, null, new Color(accent.R, accent.G, accent.B, (byte)0) * (0.55f * cell.Alpha * cell.Lit),
                        0f, glow.Size() / 2f, s, SpriteEffects.None, 0f);
                    sb.Draw(glow, c - new Vector2(0f, cell.Rect.Height * 0.08f), null,
                        new Color(ember.R, ember.G, ember.B, (byte)0) * (0.8f * cell.Alpha * cell.Lit),
                        0f, glow.Size() / 2f, s * 0.55f, SpriteEffects.None, 0f);
                }
                return;
            }

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.UIScaleMatrix);
            Vector3 accentV = BlessingTheme.Accent.ToVector3();
            Vector3 emberV = BlessingTheme.Ember.ToVector3();
            foreach (FlameCell cell in cells) {
                if (cell.Lit <= 0.01f || cell.Alpha <= 0.01f) {
                    continue;
                }
                effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                effect.Parameters["uAlpha"]?.SetValue(cell.Alpha);
                effect.Parameters["uSeed"]?.SetValue(cell.Seed);
                effect.Parameters["uLit"]?.SetValue(cell.Lit);
                effect.Parameters["uAccent"]?.SetValue(accentV);
                effect.Parameters["uEmber"]?.SetValue(emberV);
                sb.Draw(Pixel, cell.Rect, One, Color.White);
            }
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }

        //——符纹矢量——

        /// <summary>
        /// 本系统的路径统一作于 0..100 画布，而 <see cref="SvgPathPen"/> 期望 [-1,1] 坐标
        /// （Transform 是 center + point × scale，无归一化）。此处做一次线性换算：
        /// 坐标 50 落在 center，halfSize 为半尺寸像素
        /// </summary>
        internal static void StrokePath100(SpriteBatch sb, SvgPath path, Vector2 center, float halfSize,
            Color color, float thickness, float alpha, Color? core = null) {
            if (path == null) {
                return;
            }
            float unit = halfSize / 50f;
            Vector2 origin = center - new Vector2(50f, 50f) * unit;
            SvgPathPen.Stroke(sb, path, origin, unit, 0f, color, thickness, alpha, 0f, 1f, core);
        }

        /// <summary>珠心符纹描边；core 非空时叠亮芯。halfSize 为半尺寸像素</summary>
        internal static void DrawSigil(SpriteBatch sb, Blessing blessing, Vector2 center, float halfSize,
            Color color, float thickness, float alpha, Color? core = null)
            => StrokePath100(sb, SvgPathPen.Path(blessing.SigilPath), center, halfSize, color, thickness, alpha, core);

        //——通用矢量件（镜像 GameModeRenderer 的亮色配方；暗层禁假羽化）——

        internal static void DrawLine(SpriteBatch sb, Vector2 start, Vector2 end, float thickness, Color color) {
            Vector2 delta = end - start;
            float len = delta.Length();
            if (len < 0.5f) {
                return;
            }
            sb.Draw(Pixel, start, One, color, delta.ToRotation(),
                new Vector2(0f, 0.5f), new Vector2(len, thickness), SpriteEffects.None, 0f);
        }

        internal static void DrawRing(SpriteBatch sb, Vector2 center, float radius, float thickness, Color color) {
            const int seg = 40;
            Vector2 prev = center + new Vector2(radius, 0f);
            for (int i = 1; i <= seg; i++) {
                float ang = MathHelper.TwoPi * i / seg;
                Vector2 next = center + ang.ToRotationVector2() * radius;
                DrawLine(sb, prev, next, thickness, color);
                prev = next;
            }
        }

        /// <summary>细亮环 + 宽淡环两 pass 叠辉光</summary>
        internal static void DrawRingPasses(SpriteBatch sb, Vector2 c, float radius, Color col, float alpha) {
            if (alpha <= 0.01f || radius < 2f) {
                return;
            }
            DrawRing(sb, c, radius, 2f, col * (alpha * 0.95f));
            DrawRing(sb, c, radius, 5f, col * (alpha * 0.28f));
        }

        /// <summary>一点软辉（A=0 加法），亮色专用</summary>
        internal static void DrawGlow(SpriteBatch sb, Vector2 center, float diameter, Color col, float alpha) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null || alpha <= 0.01f) {
                return;
            }
            sb.Draw(glow, center, null, new Color(col.R, col.G, col.B, (byte)0) * alpha,
                0f, glow.Size() / 2f, diameter / glow.Width, SpriteEffects.None, 0f);
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

        //——引魂灯线稿——

        /// <summary>灯body：吊环 + 灯笼罩 + 底座，SvgPathPen 归一路径</summary>
        internal const string LanternPath =
            "M50,6 Q42,12 50,18 Q58,12 50,6 Z M50,18 L50,26 M34,32 Q50,20 66,32 L69,66 Q50,80 31,66 Z M40,88 L60,88 M44,80 L44,88 M56,80 L56,88";

        /// <summary>灯罩内焰室矩形（相对灯身矩形）</summary>
        internal static Rectangle LanternFlameRect(Rectangle lantern) {
            int w = (int)(lantern.Width * 0.62f);
            int h = (int)(lantern.Height * 0.52f);
            return new Rectangle(lantern.Center.X - w / 2, (int)(lantern.Y + lantern.Height * 0.26f), w, h);
        }
    }
}
