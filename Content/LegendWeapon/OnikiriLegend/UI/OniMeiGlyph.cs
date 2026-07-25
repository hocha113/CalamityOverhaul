using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>字形样式包,一入口三态:阴刻常态 / Lit 点亮 / ChiselReveal 凿现中</summary>
    internal struct OniMeiGlyphStyle
    {
        public float Alpha;
        public float Rotation;
        /// <summary>0~1 点亮强度,槽内注入 Accent 辉光</summary>
        public float Lit;
        /// <summary>点亮/填金色(绯红或金,由铭的阶决定)</summary>
        public Color Accent;
        /// <summary>0~1 金象嵌填缝,常态金阶=1</summary>
        public float Inlay;
        /// <summary>&lt;0 完成态;0~1 按笔序凿现(仪式用)</summary>
        public float ChiselReveal;
        public float Time;

        /// <summary>常态阴刻</summary>
        public static OniMeiGlyphStyle Engraved(float alpha, float rotation = 0f) => new() {
            Alpha = alpha,
            Rotation = rotation,
            ChiselReveal = -1f,
            Accent = OnikiriUITheme.Bright,
        };
    }

    /// <summary>
    /// 铭文字形库:家纹式几何纹章,程序化笔画数据(键=铭 Key),鏨刻笔意;<br/>
    /// 归一 [-1,1] 空间,任意尺寸锐利;笔画数组序即笔序,凿现按其揭示
    /// </summary>
    internal static class OniMeiGlyph
    {
        //====笔画数据====

        /// <summary>单笔:折线点列(归一空间) + 归一宽;单点即点凿</summary>
        private readonly struct Stroke(Vector2[] points, float width)
        {
            public readonly Vector2[] Points = points;
            public readonly float Width = width;
            public bool IsDot => Points.Length == 1;
        }

        private static readonly Dictionary<string, Stroke[]> glyphs = BuildGlyphs();

        /// <summary>直线/折线笔,xy 成对</summary>
        private static Stroke L(float width, params float[] xy) {
            Vector2[] pts = new Vector2[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) {
                pts[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            }
            return new Stroke(pts, width);
        }

        /// <summary>点凿</summary>
        private static Stroke Dot(float size, float x, float y)
            => new([new Vector2(x, y)], size);

        /// <summary>圆弧笔,角度弧度制,y 向下为正</summary>
        private static Stroke Arc(float width, float cx, float cy, float r, float a0, float a1, int seg = 12) {
            Vector2[] pts = new Vector2[seg + 1];
            for (int i = 0; i <= seg; i++) {
                float a = MathHelper.Lerp(a0, a1, i / (float)seg);
                pts[i] = new Vector2(cx + MathF.Cos(a) * r, cy + MathF.Sin(a) * r);
            }
            return new Stroke(pts, width);
        }

        /// <summary>蛇行缠绕笔(倶利伽罗),绕竖轴的正弦盘旋</summary>
        private static Stroke Serpent(float width) {
            const int Seg = 14;
            Vector2[] pts = new Vector2[Seg + 1];
            for (int i = 0; i <= Seg; i++) {
                float t = i / (float)Seg;
                pts[i] = new Vector2(MathF.Sin(t * 3f * MathF.PI) * (0.58f - t * 0.10f), -0.72f + t * 1.44f);
            }
            return new Stroke(pts, width);
        }

        private static Dictionary<string, Stroke[]> BuildGlyphs() => new() {
            //髭切:一横带须钩,颔下两须
            [nameof(MeiHigekiri)] = [
                L(0.16f, -0.80f, -0.12f, 0.70f, -0.18f),
                L(0.11f, 0.70f, -0.18f, 0.80f, 0.02f, 0.72f, 0.26f, 0.50f, 0.44f, 0.28f, 0.50f),
                L(0.09f, -0.42f, 0.12f, -0.58f, 0.52f),
                L(0.09f, -0.04f, 0.15f, -0.16f, 0.56f),
            ],
            //鬼切:双角上挑压一道斜断,眉弓一弧,眼位一凿
            [nameof(MeiOnikiri)] = [
                L(0.13f, -0.50f, -0.20f, -0.72f, -0.78f),
                L(0.13f, 0.32f, -0.16f, 0.55f, -0.72f),
                L(0.10f, -0.50f, -0.20f, -0.28f, -0.08f, -0.05f, -0.04f, 0.18f, -0.08f, 0.32f, -0.16f),
                L(0.17f, -0.88f, 0.62f, 0.86f, 0.10f),
                Dot(0.15f, -0.08f, 0.28f),
            ],
            //狮子之子:开口环(吼)+三放射齿,环中一睛
            [nameof(MeiShishinoko)] = [
                Arc(0.12f, -0.05f, 0.05f, 0.55f, 0.70f, 5.58f, 16),
                L(0.09f, 0.50f, -0.28f, 0.86f, -0.50f),
                L(0.09f, 0.56f, 0.00f, 0.95f, 0.00f),
                L(0.09f, 0.50f, 0.30f, 0.86f, 0.52f),
                Dot(0.13f, -0.22f, -0.08f),
            ],
            //友切:一笔断为错位两半,当中一线细切,断口两点
            [nameof(MeiTomokiri)] = [
                L(0.15f, -0.85f, -0.22f, -0.06f, -0.10f),
                L(0.15f, 0.12f, 0.16f, 0.82f, 0.30f),
                L(0.05f, 0.02f, -0.50f, 0.06f, 0.52f),
                Dot(0.10f, -0.02f, -0.28f),
                Dot(0.10f, 0.08f, 0.36f),
            ],
            //风樋:三条Y错开的风线,首线末端加大回卷(小尺寸仍见钩)
            [nameof(MeiKazehi)] = [
                L(0.12f, -0.86f, -0.52f, -0.28f, -0.56f, 0.28f, -0.58f, 0.70f, -0.50f),
                L(0.10f, -0.88f, -0.04f, -0.28f, -0.02f, 0.22f, 0.02f, 0.58f, 0.06f),
                L(0.09f, -0.78f, 0.44f, -0.28f, 0.42f, 0.12f, 0.46f, 0.40f, 0.52f),
                L(0.08f, 0.70f, -0.50f, 0.90f, -0.62f, 0.96f, -0.82f, 0.78f, -0.92f),
            ],
            //血樋:一道粗槽,一缕垂线,三滴渐远
            [nameof(MeiChihi)] = [
                L(0.20f, -0.80f, -0.16f, 0.80f, -0.22f),
                L(0.06f, -0.30f, -0.12f, -0.30f, 0.10f),
                Dot(0.14f, -0.30f, 0.20f),
                Dot(0.12f, 0.04f, 0.40f),
                Dot(0.10f, 0.32f, 0.64f),
            ],
            //不动:剑竖+加宽镡,四焰外撇张角(与倶利蛇行区分)
            [nameof(MeiFudo)] = [
                L(0.17f, 0.00f, -0.82f, 0.00f, 0.72f),
                L(0.13f, -0.40f, -0.34f, 0.40f, -0.36f),
                L(0.09f, -0.36f, 0.06f, -0.72f, -0.28f),
                L(0.09f, -0.32f, 0.42f, -0.68f, 0.22f),
                L(0.09f, 0.36f, -0.02f, 0.72f, -0.36f),
                L(0.09f, 0.32f, 0.38f, 0.68f, 0.14f),
            ],
            //倶利伽罗:细剑+加大振幅蛇行,龙首外挑颚
            [nameof(MeiKurikara)] = [
                L(0.13f, 0.00f, -0.86f, 0.00f, 0.80f),
                Serpent(0.12f),
                Dot(0.15f, 0.48f, -0.70f),
                L(0.09f, 0.48f, -0.70f, 0.78f, -0.88f),
            ],
            //铁截:短粗横截+断口斜斫
            [nameof(MeiTessetsu)] = [
                L(0.18f, -0.70f, -0.08f, 0.55f, -0.12f),
                L(0.12f, 0.20f, -0.45f, 0.55f, -0.12f, 0.72f, 0.20f),
                Dot(0.12f, -0.55f, 0.18f),
            ],
            //旧首:圆颅弧+一斜断
            [nameof(MeiKyushu)] = [
                Arc(0.12f, -0.05f, -0.18f, 0.42f, -0.80f, 3.90f, 14),
                L(0.14f, -0.55f, 0.10f, 0.62f, -0.35f),
                Dot(0.11f, -0.12f, -0.22f),
            ],
            //息合:两弧对合(憋住合拍)
            [nameof(MeiIkiai)] = [
                Arc(0.10f, -0.22f, 0.00f, 0.42f, 1.20f, 5.20f, 12),
                Arc(0.10f, 0.22f, 0.00f, 0.42f, 4.40f, 8.00f, 12),
                Dot(0.12f, 0.00f, 0.00f),
            ],
            //虚吼:开环缺齿+空睛
            [nameof(MeiKyoko)] = [
                Arc(0.11f, 0.00f, 0.05f, 0.58f, 0.90f, 5.40f, 14),
                L(0.08f, 0.48f, -0.20f, 0.78f, -0.42f),
                L(0.08f, 0.48f, 0.22f, 0.78f, 0.42f),
                Dot(0.10f, -0.18f, -0.05f),
            ],
            //假切:错位两半+虚线细切(比友切更碎)
            [nameof(MeiKarikiri)] = [
                L(0.13f, -0.82f, -0.28f, -0.10f, -0.05f),
                L(0.13f, 0.18f, 0.08f, 0.85f, 0.35f),
                L(0.04f, -0.05f, -0.55f, 0.02f, 0.55f),
                Dot(0.09f, -0.18f, -0.35f),
                Dot(0.09f, 0.22f, 0.42f),
                Dot(0.08f, 0.00f, 0.05f),
            ],
            //默切:错位两半压低+沉点
            [nameof(MeiMokukiri)] = [
                L(0.14f, -0.78f, 0.05f, -0.08f, 0.18f),
                L(0.14f, 0.10f, 0.28f, 0.78f, 0.42f),
                Dot(0.14f, 0.02f, 0.55f),
            ],
            //焦樋:两风线+末端焦钩上挑
            [nameof(MeiKogehi)] = [
                L(0.11f, -0.85f, -0.35f, -0.20f, -0.40f, 0.40f, -0.38f, 0.72f, -0.28f),
                L(0.10f, -0.80f, 0.20f, -0.15f, 0.18f, 0.35f, 0.22f, 0.55f, 0.30f),
                L(0.09f, 0.72f, -0.28f, 0.88f, -0.48f, 0.70f, -0.62f),
            ],
            //闲樋:单线缓弧+歇点
            [nameof(MeiKanhi)] = [
                L(0.11f, -0.88f, -0.10f, -0.20f, -0.18f, 0.30f, -0.12f, 0.70f, 0.00f),
                Arc(0.08f, 0.55f, 0.28f, 0.22f, -0.40f, 2.20f, 10),
                Dot(0.11f, 0.72f, 0.35f),
            ],
            //滞樋:粗槽+黏滴挤近
            [nameof(MeiTodohi)] = [
                L(0.22f, -0.75f, -0.05f, 0.70f, -0.10f),
                Dot(0.16f, -0.20f, 0.18f),
                Dot(0.15f, -0.02f, 0.32f),
                Dot(0.14f, 0.18f, 0.48f),
            ],
            //谢樋:斜槽+两瓣落点
            [nameof(MeiShiorihi)] = [
                L(0.14f, -0.70f, -0.35f, 0.55f, 0.15f),
                L(0.08f, -0.15f, -0.05f, -0.40f, 0.35f),
                L(0.08f, 0.10f, 0.05f, 0.35f, 0.42f),
                Dot(0.11f, 0.50f, 0.55f),
            ],
            //潮樋:波浪槽+三潮点
            [nameof(MeiShiohi)] = [
                L(0.12f, -0.85f, -0.20f, -0.40f, -0.35f, 0.05f, -0.15f, 0.45f, -0.30f, 0.80f, -0.18f),
                Dot(0.12f, -0.25f, 0.25f),
                Dot(0.11f, 0.15f, 0.42f),
                Dot(0.10f, 0.50f, 0.60f),
            ],
            //痺雕:剑竖+短镡+两侧麻点
            [nameof(MeiShibori)] = [
                L(0.15f, 0.00f, -0.78f, 0.00f, 0.68f),
                L(0.10f, -0.30f, -0.30f, 0.30f, -0.32f),
                Dot(0.11f, -0.48f, 0.10f),
                Dot(0.11f, 0.48f, 0.10f),
                Dot(0.10f, -0.42f, 0.38f),
                Dot(0.10f, 0.42f, 0.38f),
            ],
            //镇鸣:竖骨+两侧抑振短弧
            [nameof(MeiChinmei)] = [
                L(0.14f, 0.00f, -0.80f, 0.00f, 0.70f),
                Arc(0.08f, -0.42f, 0.00f, 0.28f, -1.00f, 1.00f, 8),
                Arc(0.08f, 0.42f, 0.00f, 0.28f, 2.10f, 4.20f, 8),
                Dot(0.10f, 0.00f, -0.35f),
            ],
            //止足:竖骨+底横止步
            [nameof(MeiAshidome)] = [
                L(0.15f, 0.00f, -0.82f, 0.00f, 0.45f),
                L(0.14f, -0.45f, 0.55f, 0.45f, 0.55f),
                L(0.08f, -0.28f, 0.35f, -0.28f, 0.70f),
                L(0.08f, 0.28f, 0.35f, 0.28f, 0.70f),
            ],
            //余炎:细剑+短蛇行+余烬两点
            [nameof(MeiYoen)] = [
                L(0.12f, 0.00f, -0.80f, 0.00f, 0.72f),
                L(0.09f, -0.28f, -0.40f, 0.32f, -0.10f, -0.22f, 0.25f, 0.30f, 0.50f),
                Dot(0.12f, 0.38f, -0.55f),
                Dot(0.10f, -0.35f, 0.45f),
            ],
        };

        //未知键兜底:似符非字的一枚简章
        private static readonly Stroke[] fallback = [
            Arc(0.11f, 0f, 0f, 0.55f, -1.2f, 3.6f, 12),
            L(0.12f, 0.0f, -0.5f, 0.0f, 0.55f),
            Dot(0.12f, 0.3f, 0.3f),
        ];

        private static Stroke[] Get(string key)
            => key != null && glyphs.TryGetValue(key, out Stroke[] data) ? data : fallback;

        /// <summary>笔数,凿仪式的落鏨拍数</summary>
        public static int StrokeCount(string key) => Get(key).Length;

        //====渲染====

        private static Texture2D Pixel => VaultAsset.placeholder2.Value;
        private static readonly Rectangle PixelSrc = new(0, 0, 1, 1);

        /// <summary>鏨刻宽度剖面:入锋一口咬进(前 12% 自 0.62 涨满),随后收细至 0.34</summary>
        private static float CarveProfile(float t) {
            float bite = 0.62f + 0.38f * MathF.Min(t / 0.12f, 1f);
            return bite * MathHelper.Lerp(1f, 0.34f, MathF.Pow(MathHelper.Clamp(t, 0f, 1f), 1.35f));
        }

        /// <summary>归一点→屏幕点</summary>
        private static Vector2 Map(Vector2 p, Vector2 center, float half, float rotation)
            => center + (rotation == 0f ? p * half : (p * half).RotatedBy(rotation));

        /// <summary>
    	/// 一入口三态绘制。center=章心,size=外接直径。<br/>
        /// ChiselReveal&gt;=0 时按笔序揭示(供仪式),否则完成态(阴刻+可选点亮/填金)
        /// </summary>
        public static void Draw(SpriteBatch sb, string key, Vector2 center, float size, in OniMeiGlyphStyle style) {
            if (style.Alpha <= 0.01f || size < 4f) {
                return;
            }
            Stroke[] strokes = Get(key);
            float half = size * 0.5f;
            bool chisel = style.ChiselReveal >= 0f;
            float perStroke = chisel ? MathHelper.Clamp(style.ChiselReveal, 0f, 1f) * strokes.Length : strokes.Length;

            for (int i = 0; i < strokes.Length; i++) {
                float local = chisel ? MathHelper.Clamp(perStroke - i, 0f, 1f) : 1f;
                if (local <= 0f) {
                    break;
                }
                //凿现余温:刚凿完的笔带 BurnHot→冷却
                float heat = chisel ? MathHelper.Clamp(1f - (perStroke - i - 1f) * 0.55f, 0f, 1f) : 0f;
                DrawStroke(sb, strokes[i], center, half, style, local, heat);
            }
        }

        /// <summary>试铭粉笔稿:白粉虚线描形,落鏨前的预览</summary>
        public static void DrawChalk(SpriteBatch sb, string key, Vector2 center, float size, float alpha, float rotation = 0f) {
            if (alpha <= 0.01f || size < 4f) {
                return;
            }
            Stroke[] strokes = Get(key);
            float half = size * 0.5f;
            Color chalk = OnikiriUITheme.Paper * (alpha * 0.55f);
            foreach (Stroke stroke in strokes) {
                if (stroke.IsDot) {
                    Vector2 pos = Map(stroke.Points[0], center, half, rotation);
                    sb.Draw(Pixel, pos, PixelSrc, chalk, MathHelper.PiOver4 + rotation, new Vector2(0.5f),
                        new Vector2(stroke.Width * half * 0.8f), SpriteEffects.None, 0f);
                    continue;
                }
                //隔段留白成虚线
                for (int i = 0; i < stroke.Points.Length - 1; i++) {
                    if ((i & 1) == 1) {
                        continue;
                    }
                    Vector2 a = Map(stroke.Points[i], center, half, rotation);
                    Vector2 b = Map(stroke.Points[i + 1], center, half, rotation);
                    DrawSeg(sb, a, b, chalk, MathF.Max(stroke.Width * half * 0.32f, 0.8f));
                }
            }
        }

        /// <summary>
        /// 拓片反白:墨面留白字,笔画走亮色(金阶鎏金)+白热细芯,无受光缘无深芯;
        /// 錾样物品图标等小尺寸暗底场景用,最小笔宽兜底保清晰
        /// </summary>
        public static void DrawRubbing(SpriteBatch sb, string key, Vector2 center, float size, float alpha,
            bool gold, float time, float rotation = 0f) {
            if (alpha <= 0.01f || size < 4f) {
                return;
            }
            Stroke[] strokes = Get(key);
            float half = size * 0.5f;
            //拓墨未干似的极轻呼吸
            float breath = 0.92f + 0.08f * MathF.Sin(time * 1.8f + center.X * 0.05f);
            Color bodyBase = gold ? OnikiriUITheme.GoldInlay : OnikiriUITheme.Paper;
            Color body = bodyBase * (alpha * 0.94f * breath);
            Color core = Color.Lerp(bodyBase, OnikiriUITheme.HotWhite, 0.7f) * (alpha * 0.5f * breath);

            foreach (Stroke stroke in strokes) {
                if (stroke.IsDot) {
                    Vector2 pos = Map(stroke.Points[0], center, half, rotation);
                    float s = MathF.Max(stroke.Width * half, 1.8f);
                    float rot = MathHelper.PiOver4 + rotation;
                    sb.Draw(Pixel, pos, PixelSrc, body, rot, new Vector2(0.5f), new Vector2(s), SpriteEffects.None, 0f);
                    sb.Draw(Pixel, pos, PixelSrc, core, rot, new Vector2(0.5f), new Vector2(s * 0.55f), SpriteEffects.None, 0f);
                    continue;
                }
                int segCount = stroke.Points.Length - 1;
                for (int i = 0; i < segCount; i++) {
                    float tm = (i + 0.5f) / segCount;
                    Vector2 a = Map(stroke.Points[i], center, half, rotation);
                    Vector2 b = Map(stroke.Points[i + 1], center, half, rotation);
                    float thick = MathF.Max(stroke.Width * half * CarveProfile(tm), 1.2f);
                    DrawSeg(sb, a, b, body, thick);
                    DrawSeg(sb, a, b, core, thick * 0.45f);
                }
            }
        }

        /// <summary>凿现中的落鏨点(屏幕空间),火星/微震锚点;非凿现态返回章心</summary>
        public static Vector2 GetChiselPoint(string key, Vector2 center, float size, float rotation, float reveal) {
            Stroke[] strokes = Get(key);
            float half = size * 0.5f;
            float per = MathHelper.Clamp(reveal, 0f, 1f) * strokes.Length;
            int idx = Math.Min((int)per, strokes.Length - 1);
            Stroke stroke = strokes[idx];
            float local = MathHelper.Clamp(per - idx, 0f, 1f);
            if (stroke.IsDot) {
                return Map(stroke.Points[0], center, half, rotation);
            }
            float f = local * (stroke.Points.Length - 1);
            int seg = Math.Min((int)f, stroke.Points.Length - 2);
            Vector2 p = Vector2.Lerp(stroke.Points[seg], stroke.Points[seg + 1], f - seg);
            return Map(p, center, half, rotation);
        }

        //====单笔====

        private static void DrawStroke(SpriteBatch sb, in Stroke stroke, Vector2 center, float half,
            in OniMeiGlyphStyle style, float sweep, float heat) {
            float alpha = style.Alpha;
            //凹槽受光:高光固定自左上来,凿口下缘接住光
            Vector2 lightOff = new Vector2(0.62f, 0.86f) * MathF.Max(1f, half / 20f);

            if (stroke.IsDot) {
                //点凿:入拍带 1.45→1 的砸落回弹
                float pop = sweep < 1f ? 1f + (1f - sweep) * 0.45f : 1f;
                Vector2 pos = Map(stroke.Points[0], center, half, style.Rotation);
                float s = stroke.Width * half * pop;
                float rot = MathHelper.PiOver4 + style.Rotation;
                Vector2 o = new(0.5f);
                sb.Draw(Pixel, pos + lightOff, PixelSrc, OnikiriUITheme.Paper * (alpha * 0.22f), rot, o, new Vector2(s), SpriteEffects.None, 0f);
                sb.Draw(Pixel, pos, PixelSrc, OnikiriUITheme.Ink * (alpha * 0.95f), rot, o, new Vector2(s), SpriteEffects.None, 0f);
                sb.Draw(Pixel, pos, PixelSrc, new Color(8, 2, 5) * alpha, rot, o, new Vector2(s * 0.55f), SpriteEffects.None, 0f);
                if (style.Inlay > 0.02f) {
                    sb.Draw(Pixel, pos, PixelSrc, Color.Lerp(OnikiriUITheme.GoldDeep, OnikiriUITheme.GoldInlay, 0.6f) * (alpha * 0.9f * style.Inlay),
                        rot, o, new Vector2(s * 0.5f), SpriteEffects.None, 0f);
                }
                if (style.Lit > 0.02f) {
                    sb.Draw(Pixel, pos, PixelSrc, style.Accent * (alpha * 0.75f * style.Lit), rot, o, new Vector2(s * 0.8f), SpriteEffects.None, 0f);
                }
                if (heat > 0.02f) {
                    sb.Draw(Pixel, pos, PixelSrc, OnikiriUITheme.BurnHot * (alpha * 0.8f * heat), rot, o, new Vector2(s * 0.7f), SpriteEffects.None, 0f);
                }
                return;
            }

            int segCount = stroke.Points.Length - 1;
            int visible = sweep >= 1f ? segCount : Math.Max((int)MathF.Ceiling(sweep * segCount), 1);
            for (int i = 0; i < visible; i++) {
                float t0 = i / (float)segCount;
                float tm = (i + 0.5f) / segCount;
                Vector2 a = Map(stroke.Points[i], center, half, style.Rotation);
                Vector2 b = Map(stroke.Points[i + 1], center, half, style.Rotation);
                //末段被 sweep 截断,笔锋停在凿点
                if (sweep < 1f && i == visible - 1) {
                    float f = sweep * segCount - i;
                    b = Vector2.Lerp(a, b, MathHelper.Clamp(f, 0.05f, 1f));
                }
                float thick = MathF.Max(stroke.Width * half * CarveProfile(tm), 0.9f);

                //受光缘(先画,在槽旁而非槽下)
                sb.Draw(Pixel, a + lightOff, PixelSrc, OnikiriUITheme.Paper * (alpha * 0.20f * (0.4f + CarveProfile(t0) * 0.6f)),
                    (b - a).ToRotation(), new Vector2(0f, 0.5f), new Vector2((b - a).Length() + 0.7f, thick * 0.45f), SpriteEffects.None, 0f);
                //槽体两层:墨壳 + 更深的芯
                DrawSeg(sb, a, b, OnikiriUITheme.Ink * (alpha * 0.95f), thick);
                DrawSeg(sb, a, b, new Color(8, 2, 5) * (alpha * 0.9f), thick * 0.5f);
                //金填缝
                if (style.Inlay > 0.02f) {
                    float shimmer = 0.8f + 0.2f * MathF.Sin(style.Time * 1.7f + tm * 9f);
                    DrawSeg(sb, a, b, Color.Lerp(OnikiriUITheme.GoldDeep, OnikiriUITheme.GoldInlay, 0.35f + 0.45f * shimmer)
                        * (alpha * 0.85f * style.Inlay), thick * 0.42f);
                }
                //点亮:槽内注光,三层堆宽
                if (style.Lit > 0.02f) {
                    float breath = 0.85f + 0.15f * MathF.Sin(style.Time * 2.4f + tm * 4f);
                    float lit = style.Lit * breath;
                    DrawSeg(sb, a, b, style.Accent * (alpha * 0.12f * lit), thick * 2.6f);
                    DrawSeg(sb, a, b, style.Accent * (alpha * 0.30f * lit), thick * 1.5f);
                    DrawSeg(sb, a, b, Color.Lerp(style.Accent, OnikiriUITheme.HotWhite, 0.55f) * (alpha * 0.55f * lit), thick * 0.5f);
                }
                //凿现余温:新割开的口子还没冷
                if (heat > 0.02f) {
                    DrawSeg(sb, a, b, OnikiriUITheme.BurnHot * (alpha * 0.45f * heat), thick * 0.6f);
                }
            }

            //凿现前锋:白热一粒压在笔锋上
            if (sweep < 1f && sweep > 0.01f && heat > 0.02f) {
                float f = sweep * segCount;
                int seg = Math.Min((int)f, segCount - 1);
                Vector2 tip = Map(Vector2.Lerp(stroke.Points[seg], stroke.Points[seg + 1],
                    MathHelper.Clamp(f - seg, 0f, 1f)), center, half, style.Rotation);
                sb.Draw(Pixel, tip, PixelSrc, OnikiriUITheme.HotWhite * (style.Alpha * 0.95f), MathHelper.PiOver4,
                    new Vector2(0.5f), new Vector2(3.2f), SpriteEffects.None, 0f);
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
