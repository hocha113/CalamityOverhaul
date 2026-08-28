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
        /// <summary>受风倾斜 -1..1，焰尖权重（0=无风，往生轮珠焰不吹风）</summary>
        public float Lean;
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
                effect.Parameters["uLean"]?.SetValue(cell.Lean);
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

        /// <summary>0..100 画布路径上的循环巡行亮笔（换算同 <see cref="StrokePath100"/>）</summary>
        internal static void RunnerPath100(SpriteBatch sb, SvgPath path, Vector2 center, float halfSize,
            Color color, float thickness, float alpha, float head, float span, Color? core = null) {
            if (path == null) {
                return;
            }
            float unit = halfSize / 50f;
            Vector2 origin = center - new Vector2(50f, 50f) * unit;
            SvgPathPen.StrokeRunner(sb, path, origin, unit, 0f, color, thickness, alpha, head, span, core);
        }

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

        //——引魂灯线稿（0..100 画布，多子路径分层描）——

        /// <summary>
        /// 主骨架：宝顶→顶柱→翘檐（右檐尾带挂钩）→垂柱→八角灯身→托盘→三足。
        /// StrokeRunner 巡行走的也是这条
        /// </summary>
        internal const string LanternPath =
            "M50,3 Q45,8 50,13 Q55,8 50,3 Z" +
            " M50,13 L50,18" +
            " M24,30 Q30,19 50,17 Q70,19 76,30" +
            " M24,30 Q21,27 20,22" +
            " M76,30 Q79,27 80,22 Q82,25 80,27" +
            " M34,29 L36,35 M66,29 L64,35" +
            " M36,35 L30,44 L30,58 L36,68 L64,68 L70,58 L70,44 L64,35 Z" +
            " M44,68 L44,72 M56,68 L56,72 M40,72 L60,72" +
            " M42,72 L37,82 M58,72 L63,82 M50,72 L50,84" +
            " M34,82 L40,82 M60,82 L66,82 M47,84 L53,84";

        /// <summary>焰室窗拱（单独描，取更亮的 accent 调）</summary>
        internal const string LanternWindowPath =
            "M35,41 Q35,37 50,37 Q65,37 65,41 L65,59 Q65,63 50,63 Q35,63 35,59 Z";

        /// <summary>细部次笔：内檐线 / 骨条 / 腰箍 / 铆点（单点子路径作点凿）</summary>
        internal const string LanternDetailPath =
            "M28,31 Q50,24 72,31" +
            " M33,40 L33,62 M67,40 L67,62" +
            " M30,51 L35,51 M65,51 L70,51" +
            " M30,44 M70,44 M30,58 M70,58 M36,35 M64,35 M36,68 M64,68";

        /// <summary>檐角吊铃（局部坐标，挂点在原点向下垂，配合旋转作摆动）</summary>
        internal const string LanternBellPath =
            "M0,0 L0,6 M-4,12 Q-4,6 0,6 Q4,6 4,12 L5.5,15 L-5.5,15 Z M0,15 L0,19";

        /// <summary>吊铃挂点（相对灯身矩形，右檐尾挂钩下缘）</summary>
        internal static Vector2 LanternBellHook(Rectangle lantern)
            => new(lantern.X + lantern.Width * 0.80f, lantern.Y + lantern.Height * 0.27f);

        /// <summary>灯罩内焰室矩形（相对灯身矩形）：焰根落在窗拱下缘，焰尖舔到拱顶</summary>
        internal static Rectangle LanternFlameRect(Rectangle lantern) {
            int w = (int)(lantern.Width * 0.60f);
            int h = (int)(lantern.Height * 0.50f);
            return new Rectangle(lantern.Center.X - w / 2, (int)(lantern.Y + lantern.Height * 0.26f), w, h);
        }

        /// <summary>
        /// 氛围层画布契约（改此处必须同步改 BlessingLantern.fx 顶部常量）：
        /// 宽 2.6×灯宽、高 2.05×灯高，焰室中心落在画布 UV(0.5, 0.62)
        /// </summary>
        internal static Rectangle LanternAmbientRect(Rectangle lantern) {
            Vector2 fc = LanternFlameRect(lantern).Center.ToVector2();
            int w = (int)(lantern.Width * 2.6f);
            int h = (int)(lantern.Height * 2.05f);
            return new Rectangle((int)(fc.X - w / 2f), (int)(fc.Y - h * 0.62f), w, h);
        }

        /// <summary>
        /// 引魂灯氛围层：焰室光晕 / 灯窗漏光 / 地面光池 / 升腾魂雾 / 上浮余烬。
        /// shader 缺编时回退为焰室软辉 + 压扁地光两笔
        /// </summary>
        internal static void DrawLanternAmbient(SpriteBatch sb, Rectangle lantern, float seed,
            float lit, float hover, float pulse, float alpha) {
            if (alpha <= 0.01f) {
                return;
            }
            Effect effect = EffectLoader.BlessingLantern?.Value;
            if (effect == null) {
                Vector2 fc = LanternFlameRect(lantern).Center.ToVector2();
                float g = 0.28f + 0.72f * lit;
                DrawGlow(sb, fc, lantern.Width * (1.7f + pulse * 0.8f), BlessingTheme.Ember, 0.32f * g * alpha);
                DrawGlow(sb, fc, lantern.Width * (3.0f + pulse * 1.4f), BlessingTheme.Accent, 0.16f * g * alpha);
                Texture2D glowTex = CWRAsset.SoftGlow?.Value;
                if (glowTex != null) {
                    Vector2 gp = new(fc.X, lantern.Bottom + 6f);
                    Color c = BlessingTheme.Ember;
                    sb.Draw(glowTex, gp, null, new Color(c.R, c.G, c.B, (byte)0) * (0.28f * g * alpha),
                        0f, glowTex.Size() / 2f,
                        new Vector2(lantern.Width * 1.8f, lantern.Width * 0.45f) / glowTex.Width,
                        SpriteEffects.None, 0f);
                }
                return;
            }

            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uSeed"]?.SetValue(seed);
            effect.Parameters["uLit"]?.SetValue(lit);
            effect.Parameters["uHover"]?.SetValue(hover);
            effect.Parameters["uPulse"]?.SetValue(pulse);
            effect.Parameters["uAccent"]?.SetValue(BlessingTheme.Accent.ToVector3());
            effect.Parameters["uEmber"]?.SetValue(BlessingTheme.Ember.ToVector3());
            ShaderQuad(sb, effect, LanternAmbientRect(lantern));
        }
    }
}
