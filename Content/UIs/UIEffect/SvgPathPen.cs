using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Globalization;
using Terraria;

namespace CalamityOverhaul.Content.UIs.UIEffect
{
    /// <summary>
    /// 解析好的 SVG 路径,归一 [-1,1] 空间的折线组 + 累计弧长(供按笔序揭示)
    /// </summary>
    internal sealed class SvgPath
    {
        /// <summary>各子路径的折线点列</summary>
        internal readonly Vector2[][] Lines;
        /// <summary>与 <see cref="Lines"/> 同形的累计弧长,首项恒为 0</summary>
        internal readonly float[][] Arcs;
        /// <summary>全路径总弧长</summary>
        internal readonly float Total;

        internal SvgPath(Vector2[][] lines) {
            Lines = lines;
            Arcs = new float[lines.Length][];
            float total = 0f;
            for (int i = 0; i < lines.Length; i++) {
                Vector2[] pts = lines[i];
                float[] arc = new float[pts.Length];
                for (int j = 1; j < pts.Length; j++) {
                    arc[j] = arc[j - 1] + Vector2.Distance(pts[j - 1], pts[j]);
                }
                Arcs[i] = arc;
                total += arc.Length > 0 ? arc[^1] : 0f;
            }
            Total = total;
        }
    }

    /// <summary>
    /// SVG 路径笔:d 串解析成归一折线后用像素笔铺段,支持按弧长揭示与笔锋辉光。<br/>
    /// 支持 M/L/H/V/C/Q/Z 与其相对形式,弧线指令 A 不支持。<br/>
    /// 无贴图纹章的共用底座(鬼切稽古符 / 比目鱼引航海图)
    /// </summary>
    internal static class SvgPathPen
    {
        //曲线离散段数。一段贝塞尔在 40px 级字形上 8 段已看不出折点,再密只是白烧 draw call
        private const int CubicSeg = 8;
        private const int QuadSeg = 8;
        //折点补方点的转角阈值(dot),密折线靠段头 0.6px 过长自封口,不必逐点补
        private const float JointCos = 0.86f;

        private static readonly Dictionary<string, SvgPath> cache = new(StringComparer.Ordinal);

        private static Texture2D Pixel => VaultAsset.placeholder2.Value;
        private static readonly Rectangle PixelSrc = new(0, 0, 1, 1);

        /// <summary>取(必要时解析)一条路径,同一 d 串只解析一次</summary>
        internal static SvgPath Path(string data) {
            if (string.IsNullOrEmpty(data)) {
                return null;
            }
            if (!cache.TryGetValue(data, out SvgPath path)) {
                path = new SvgPath(Flatten(data));
                cache[data] = path;
            }
            return path;
        }

        internal static void ClearCache() => cache.Clear();

        /// <summary>
        /// 描边一条路径的弧长窗口 <c>[from, to]</c>,窗口不到末端时笔锋处补一点软辉
        /// </summary>
        /// <param name="core">非空则在笔身上叠一道更细的亮芯</param>
        internal static void Stroke(SpriteBatch sb, SvgPath path, Vector2 center, float scale,
            float rotation, Color color, float thickness, float alpha,
            float from = 0f, float to = 1f, Color? core = null) {
            if (path == null || path.Total <= 0f || alpha <= 0.004f || scale <= 0.01f) {
                return;
            }
            Texture2D pixel = Pixel;
            if (pixel == null) {
                return;
            }
            float lo = MathHelper.Clamp(from, 0f, 1f) * path.Total;
            float hi = MathHelper.Clamp(to, 0f, 1f) * path.Total;
            if (hi - lo <= 0.0001f) {
                return;
            }

            float walked = 0f;
            Vector2 head = center;
            bool clipped = false;

            for (int i = 0; i < path.Lines.Length; i++) {
                Vector2[] pts = path.Lines[i];
                float[] arc = path.Arcs[i];
                if (pts.Length < 2) {
                    //单点子路径按点凿处理
                    if (walked >= lo && walked <= hi) {
                        Vector2 dot = Transform(pts[0], center, scale, rotation);
                        sb.Draw(pixel, dot, PixelSrc, color * alpha, rotation + MathHelper.PiOver4,
                            new Vector2(0.5f), new Vector2(thickness * 1.6f), SpriteEffects.None, 0f);
                    }
                    continue;
                }

                for (int j = 1; j < pts.Length; j++) {
                    float segStart = walked;
                    float segLen = arc[j] - arc[j - 1];
                    walked += segLen;
                    if (segLen <= 0.0001f || walked <= lo || segStart >= hi) {
                        continue;
                    }
                    float t0 = MathHelper.Clamp((lo - segStart) / segLen, 0f, 1f);
                    float t1 = MathHelper.Clamp((hi - segStart) / segLen, 0f, 1f);
                    Vector2 head0 = Transform(Vector2.Lerp(pts[j - 1], pts[j], t0), center, scale, rotation);
                    Vector2 head1 = Transform(Vector2.Lerp(pts[j - 1], pts[j], t1), center, scale, rotation);
                    DrawSegment(sb, pixel, head0, head1, color, thickness, alpha);
                    if (core.HasValue) {
                        DrawSegment(sb, pixel, head0, head1, core.Value, thickness * 0.42f, alpha * 0.85f);
                    }
                    //只在真拐角补方点,圆弧那种密折线补了也看不见
                    if (t0 <= 0f && j >= 2 && IsCorner(pts[j - 2], pts[j - 1], pts[j])) {
                        sb.Draw(pixel, head0, PixelSrc, color * alpha, rotation,
                            new Vector2(0.5f), new Vector2(thickness), SpriteEffects.None, 0f);
                    }
                    head = head1;
                    clipped = t1 < 1f;
                }
            }

            if (clipped && to < 0.999f) {
                SoftDot(sb, head, thickness * 2.4f, color, alpha * 0.8f);
            }
        }

        /// <summary>循环巡行的一段亮笔,窗口越过末端时自动接回起点</summary>
        internal static void StrokeRunner(SpriteBatch sb, SvgPath path, Vector2 center, float scale,
            float rotation, Color color, float thickness, float alpha,
            float head, float span, Color? core = null) {
            head -= MathF.Floor(head);
            float tail = head + MathHelper.Clamp(span, 0f, 1f);
            Stroke(sb, path, center, scale, rotation, color, thickness, alpha,
                head, MathF.Min(tail, 1f), core);
            if (tail > 1f) {
                Stroke(sb, path, center, scale, rotation, color, thickness, alpha,
                    0f, tail - 1f, core);
            }
        }

        /// <summary>笔锋软辉,A=0 预乘加法(与各域 Brush 无关,保持本文件零外部依赖)</summary>
        internal static void SoftDot(SpriteBatch sb, Vector2 center, float radius, Color color, float alpha) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            if (glow == null || alpha <= 0.004f) {
                return;
            }
            sb.Draw(glow, center, null, new Color(color.R, color.G, color.B, 0) * alpha, 0f,
                glow.Size() * 0.5f, radius * 2f / glow.Width, SpriteEffects.None, 0f);
        }

        private static void DrawSegment(SpriteBatch sb, Texture2D pixel, Vector2 from, Vector2 to,
            Color color, float thickness, float alpha) {
            Vector2 edge = to - from;
            float len = edge.Length();
            if (len < 0.05f) {
                return;
            }
            sb.Draw(pixel, from, PixelSrc, color * alpha, edge.ToRotation(),
                new Vector2(0f, 0.5f), new Vector2(len + 0.6f, thickness), SpriteEffects.None, 0f);
        }

        private static bool IsCorner(Vector2 prev, Vector2 mid, Vector2 next) {
            Vector2 a = mid - prev;
            Vector2 b = next - mid;
            if (a.LengthSquared() < 1e-8f || b.LengthSquared() < 1e-8f) {
                return false;
            }
            return Vector2.Dot(Vector2.Normalize(a), Vector2.Normalize(b)) < JointCos;
        }

        private static Vector2 Transform(Vector2 point, Vector2 center, float scale, float rotation)
            => center + (point * scale).RotatedBy(rotation);

        #region d 串解析
        private static Vector2[][] Flatten(string d) {
            List<Vector2[]> subPaths = [];
            List<Vector2> current = [];
            Vector2 cursor = Vector2.Zero;
            Vector2 subStart = Vector2.Zero;
            char command = '\0';
            int i = 0;

            void Flush() {
                if (current.Count > 0) {
                    subPaths.Add([.. current]);
                    current.Clear();
                }
            }

            while (true) {
                SkipSeparators(d, ref i);
                if (i >= d.Length) {
                    break;
                }
                int consumedFrom = i;
                char c = d[i];
                if (char.IsLetter(c)) {
                    command = c;
                    i++;
                }
                else if (command == 'M') {
                    command = 'L';
                }
                else if (command == 'm') {
                    command = 'l';
                }
                else if (command == '\0') {
                    break;
                }

                bool relative = char.IsLower(command);
                switch (char.ToUpperInvariant(command)) {
                    case 'M': {
                        if (!ReadPoint(d, ref i, cursor, relative, out Vector2 target)) {
                            return Finish(subPaths, current);
                        }
                        Flush();
                        cursor = subStart = target;
                        current.Add(cursor);
                        break;
                    }
                    case 'L': {
                        if (!ReadPoint(d, ref i, cursor, relative, out Vector2 target)) {
                            return Finish(subPaths, current);
                        }
                        cursor = target;
                        current.Add(cursor);
                        break;
                    }
                    case 'H': {
                        if (!TryReadNumber(d, ref i, out float x)) {
                            return Finish(subPaths, current);
                        }
                        cursor = new Vector2(relative ? cursor.X + x : x, cursor.Y);
                        current.Add(cursor);
                        break;
                    }
                    case 'V': {
                        if (!TryReadNumber(d, ref i, out float y)) {
                            return Finish(subPaths, current);
                        }
                        cursor = new Vector2(cursor.X, relative ? cursor.Y + y : y);
                        current.Add(cursor);
                        break;
                    }
                    case 'C': {
                        if (!ReadPoint(d, ref i, cursor, relative, out Vector2 c1)
                            || !ReadPoint(d, ref i, cursor, relative, out Vector2 c2)
                            || !ReadPoint(d, ref i, cursor, relative, out Vector2 end)) {
                            return Finish(subPaths, current);
                        }
                        AppendCubic(current, cursor, c1, c2, end);
                        cursor = end;
                        break;
                    }
                    case 'Q': {
                        if (!ReadPoint(d, ref i, cursor, relative, out Vector2 ctrl)
                            || !ReadPoint(d, ref i, cursor, relative, out Vector2 end)) {
                            return Finish(subPaths, current);
                        }
                        AppendQuadratic(current, cursor, ctrl, end);
                        cursor = end;
                        break;
                    }
                    case 'Z': {
                        if (current.Count > 0) {
                            current.Add(subStart);
                        }
                        cursor = subStart;
                        Flush();
                        break;
                    }
                    default:
                        //不支持的指令(A 等)直接停,避免读出错位坐标
                        return Finish(subPaths, current);
                }

                //本轮一个字符都没吃掉说明串畸形(如 Z 后跟数字),再转就是死循环
                if (i == consumedFrom) {
                    return Finish(subPaths, current);
                }
            }

            return Finish(subPaths, current);
        }

        private static Vector2[][] Finish(List<Vector2[]> subPaths, List<Vector2> current) {
            if (current.Count > 0) {
                subPaths.Add([.. current]);
            }
            return [.. subPaths];
        }

        private static void AppendCubic(List<Vector2> into, Vector2 p0, Vector2 c1, Vector2 c2, Vector2 p1) {
            for (int s = 1; s <= CubicSeg; s++) {
                float t = s / (float)CubicSeg;
                float u = 1f - t;
                into.Add(u * u * u * p0 + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * p1);
            }
        }

        private static void AppendQuadratic(List<Vector2> into, Vector2 p0, Vector2 ctrl, Vector2 p1) {
            for (int s = 1; s <= QuadSeg; s++) {
                float t = s / (float)QuadSeg;
                float u = 1f - t;
                into.Add(u * u * p0 + 2f * u * t * ctrl + t * t * p1);
            }
        }

        private static bool ReadPoint(string d, ref int i, Vector2 cursor, bool relative, out Vector2 point) {
            point = Vector2.Zero;
            if (!TryReadNumber(d, ref i, out float x) || !TryReadNumber(d, ref i, out float y)) {
                return false;
            }
            point = relative ? cursor + new Vector2(x, y) : new Vector2(x, y);
            return true;
        }

        private static bool TryReadNumber(string d, ref int i, out float value) {
            SkipSeparators(d, ref i);
            int begin = i;
            if (i < d.Length && (d[i] == '+' || d[i] == '-')) {
                i++;
            }
            while (i < d.Length && char.IsDigit(d[i])) {
                i++;
            }
            if (i < d.Length && d[i] == '.') {
                i++;
                while (i < d.Length && char.IsDigit(d[i])) {
                    i++;
                }
            }
            if (i == begin) {
                value = 0f;
                return false;
            }
            return float.TryParse(d[begin..i], NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static void SkipSeparators(string d, ref int i) {
            while (i < d.Length && (char.IsWhiteSpace(d[i]) || d[i] == ',')) {
                i++;
            }
        }
        #endregion
    }

    internal sealed class SvgPathPenLoader : ICWRLoader
    {
        void ICWRLoader.UnLoadData() => SvgPathPen.ClearCache();
    }
}
