using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans
{
    /// <summary>单笔：折线点列（归一空间）+ 归一宽；单点即朱点。由定义的 BuildGlyph 组装</summary>
    internal readonly struct KikasaGlyphStroke(Vector2[] points, float width)
    {
        public readonly Vector2[] Points = points;
        public readonly float Width = width;
        public bool IsDot => Points.Length == 1;
    }

    /// <summary>
    /// 唤雨符符文库：雨字头家族篆意笔画，程序化笔画数据（键=符 Key），湿墨笔意；<br/>
    /// 归一 [-1,1] 空间，任意尺寸锐利。家族共相=顶上一弯"雨盖"，
    /// 盖下各符独有"落相"；点凿走符墨身份色，线走深墨。<br/>
    /// 笔画数据"定义自带"：<see cref="KikasaTalismanDefinition.BuildGlyph"/> 提供，
    /// 注册期（<see cref="KikasaTalismanRegistry"/>.LoadData）经 <see cref="Register"/> 收进本缓存；
    /// 未配纹样的键落到伞形 fallback
    /// </summary>
    internal static class KikasaTalismanGlyph
    {
        //====笔画构建 API（开放给定义子类）====

        /// <summary>直线/折线笔，xy 成对</summary>
        internal static KikasaGlyphStroke L(float width, params float[] xy) {
            Vector2[] pts = new Vector2[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) {
                pts[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            }
            return new KikasaGlyphStroke(pts, width);
        }

        /// <summary>朱点</summary>
        internal static KikasaGlyphStroke Dot(float size, float x, float y)
            => new([new Vector2(x, y)], size);

        /// <summary>圆弧笔，角度弧度制，y 向下为正</summary>
        internal static KikasaGlyphStroke Arc(float width, float cx, float cy, float r,
            float a0, float a1, int seg = 12) {
            Vector2[] pts = new Vector2[seg + 1];
            for (int i = 0; i <= seg; i++) {
                float a = MathHelper.Lerp(a0, a1, i / (float)seg);
                pts[i] = new Vector2(cx + MathF.Cos(a) * r, cy + MathF.Sin(a) * r);
            }
            return new KikasaGlyphStroke(pts, width);
        }

        /// <summary>雨盖：家族共相，一弯浅拱盖住上带（伞与云的双读）</summary>
        internal static KikasaGlyphStroke Canopy(float width, float cy = -0.42f, float r = 0.52f)
            => Arc(width, 0f, cy, r, 3.42f, 6.00f, 12);

        //====中央缓存（注册期填充）====

        private static readonly Dictionary<string, KikasaGlyphStroke[]> glyphs = [];

        /// <summary>收录一张符的字形；空键/空笔画忽略（保持 fallback）</summary>
        internal static void Register(string key, KikasaGlyphStroke[] strokes) {
            if (string.IsNullOrEmpty(key) || strokes == null || strokes.Length == 0) {
                return;
            }
            glyphs[key] = strokes;
        }

        /// <summary>卸载清空，随注册表同步生命周期</summary>
        internal static void ClearRegistry() => glyphs.Clear();

        //未知键兜底：伞形小章（盖+柄+一滴），新符不配纹样也能立刻上线
        private static readonly KikasaGlyphStroke[] fallback = [
            Canopy(0.12f),
            L(0.11f, 0.00f, -0.20f, 0.00f, 0.55f),
            Dot(0.11f, 0.30f, 0.30f),
        ];

        private static KikasaGlyphStroke[] Get(string key)
            => key != null && glyphs.TryGetValue(key, out KikasaGlyphStroke[] data) ? data : fallback;

        /// <summary>笔数，仪式/揭示节拍用</summary>
        public static int StrokeCount(string key) => Get(key).Length;

        //====渲染====

        private static Texture2D Pixel => VaultAsset.placeholder2.Value;
        private static readonly Rectangle PixelSrc = new(0, 0, 1, 1);

        /// <summary>湿墨宽度剖面：起笔重（前 15% 自 0.66 涨满），收笔轻（收细至 0.38）</summary>
        private static float BrushProfile(float t) {
            float press = 0.66f + 0.34f * MathF.Min(t / 0.15f, 1f);
            return press * MathHelper.Lerp(1f, 0.38f, MathF.Pow(MathHelper.Clamp(t, 0f, 1f), 1.30f));
        }

        /// <summary>归一点→屏幕点</summary>
        private static Vector2 Map(Vector2 p, Vector2 center, float half, float rotation)
            => center + (rotation == 0f ? p * half : (p * half).RotatedBy(rotation));

        /// <summary>
        /// 符文主绘：纸上湿墨。线走深墨+身份色细芯，点凿整颗走身份色（朱点语义）；
        /// reveal&lt;0 完成态，0~1 按笔序揭示（书符演出用）
        /// </summary>
        public static void DrawInk(SpriteBatch sb, string key, Vector2 center, float size,
            float alpha, Color ink, Color accent, float time, float rotation = 0f, float reveal = -1f) {
            if (alpha <= 0.01f || size < 4f) {
                return;
            }
            KikasaGlyphStroke[] strokes = Get(key);
            float half = size * 0.5f;
            //墨未干似的极轻呼吸
            float breath = 0.93f + 0.07f * MathF.Sin(time * 1.6f + center.X * 0.04f);
            Color body = ink * (alpha * 0.95f * breath);
            Color core = accent * (alpha * 0.60f * breath);
            bool revealing = reveal >= 0f;
            float perStroke = revealing
                ? MathHelper.Clamp(reveal, 0f, 1f) * strokes.Length : strokes.Length;

            for (int i = 0; i < strokes.Length; i++) {
                float local = revealing ? MathHelper.Clamp(perStroke - i, 0f, 1f) : 1f;
                if (local <= 0f) {
                    break;
                }
                KikasaGlyphStroke stroke = strokes[i];
                if (stroke.IsDot) {
                    //朱点：身份色整颗 + 亮芯，落笔带回弹
                    float pop = local < 1f ? 1f + (1f - local) * 0.40f : 1f;
                    Vector2 pos = Map(stroke.Points[0], center, half, rotation);
                    float s = MathF.Max(stroke.Width * half * pop, 1.8f);
                    float rot = MathHelper.PiOver4 + rotation;
                    Vector2 o = new(0.5f);
                    sb.Draw(Pixel, pos, PixelSrc, accent * (alpha * 0.92f * breath), rot, o,
                        new Vector2(s), SpriteEffects.None, 0f);
                    sb.Draw(Pixel, pos, PixelSrc, Color.Lerp(accent, Color.White, 0.45f) * (alpha * 0.50f),
                        rot, o, new Vector2(s * 0.5f), SpriteEffects.None, 0f);
                    continue;
                }

                int segCount = stroke.Points.Length - 1;
                int visible = local >= 1f ? segCount : Math.Max((int)MathF.Ceiling(local * segCount), 1);
                for (int s = 0; s < visible; s++) {
                    float tm = (s + 0.5f) / segCount;
                    Vector2 a = Map(stroke.Points[s], center, half, rotation);
                    Vector2 b = Map(stroke.Points[s + 1], center, half, rotation);
                    //末段被揭示进度截断，笔锋停在书写点
                    if (local < 1f && s == visible - 1) {
                        float f = local * segCount - s;
                        b = Vector2.Lerp(a, b, MathHelper.Clamp(f, 0.05f, 1f));
                    }
                    float thick = MathF.Max(stroke.Width * half * BrushProfile(tm), 1.1f);
                    DrawSeg(sb, a, b, body, thick);
                    DrawSeg(sb, a, b, core, thick * 0.40f);
                }
            }
        }

        /// <summary>
        /// 虚影预览：隔段留白的淡描形（候选扇未选中/空位预演用），不带朱点实色
        /// </summary>
        public static void DrawGhost(SpriteBatch sb, string key, Vector2 center, float size,
            float alpha, Color color, float rotation = 0f) {
            if (alpha <= 0.01f || size < 4f) {
                return;
            }
            KikasaGlyphStroke[] strokes = Get(key);
            float half = size * 0.5f;
            Color faint = color * (alpha * 0.55f);
            foreach (KikasaGlyphStroke stroke in strokes) {
                if (stroke.IsDot) {
                    Vector2 pos = Map(stroke.Points[0], center, half, rotation);
                    sb.Draw(Pixel, pos, PixelSrc, faint, MathHelper.PiOver4 + rotation, new Vector2(0.5f),
                        new Vector2(MathF.Max(stroke.Width * half * 0.8f, 1.2f)), SpriteEffects.None, 0f);
                    continue;
                }
                for (int i = 0; i < stroke.Points.Length - 1; i++) {
                    if ((i & 1) == 1) {
                        continue;
                    }
                    Vector2 a = Map(stroke.Points[i], center, half, rotation);
                    Vector2 b = Map(stroke.Points[i + 1], center, half, rotation);
                    DrawSeg(sb, a, b, faint, MathF.Max(stroke.Width * half * 0.30f, 0.8f));
                }
            }
        }

        private static void DrawSeg(SpriteBatch sb, Vector2 a, Vector2 b, Color color, float thick) {
            Vector2 edge = b - a;
            float len = edge.Length();
            if (len < 0.5f) {
                return;
            }
            sb.Draw(Pixel, a, PixelSrc, color, edge.ToRotation(), new Vector2(0f, 0.5f),
                new Vector2(len + 0.7f, thick), SpriteEffects.None, 0f);
        }
    }
}
