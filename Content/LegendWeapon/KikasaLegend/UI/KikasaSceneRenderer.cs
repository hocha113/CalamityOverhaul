using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI
{
    /// <summary>
    /// 湖畔村图的锐利前景笔：近景民居/鸟居/檐灯/恶犬三态/芦苇/装裱轴。
    /// 配方沿用点鬼簿悬件的"粗笔当体、细笔作线、亮芯作光"——
    /// 犬用一条粗脊线当身体，细笔挑腿尾，烬目交给加色层单画。
    /// 路径全在归一 [-1,1] 空间，A 弧指令不可用（Flatten 会静默截断），曲线走 C/Q
    /// </summary>
    internal static class KikasaSceneRenderer
    {
        //==================== 鸟居 ====================
        //内倾双柱 + 反翘笠木 + 岛木 + 貫 + 额束

        private const string ToriiPillars =
            "M -0.58 0.95 L -0.46 -0.52 M 0.58 0.95 L 0.46 -0.52";

        private const string ToriiBeams =
            "M -0.95 -0.60 Q 0 -0.82 0.95 -0.60 "
            + "M -0.78 -0.47 L 0.78 -0.47 "
            + "M -0.66 -0.12 L 0.66 -0.12 "
            + "M 0 -0.47 L 0 -0.12";

        //==================== 民居 ====================
        //甲：曲脊悬山顶 + 门洞；乙：矮阔庑殿顶 + 方窗

        private const string HouseARoof =
            "M -1 0.02 L -0.62 -0.78 Q 0 -1.02 0.62 -0.78 L 1 0.02";

        private const string HouseABody =
            "M -0.86 -0.02 L 0.86 -0.02 "
            + "M -0.68 0.0 L -0.68 0.92 M 0.68 0.0 L 0.68 0.92 "
            + "M -0.78 0.92 L 0.78 0.92 "
            + "M -0.10 0.92 L -0.10 0.40 L 0.24 0.40 L 0.24 0.92";

        private const string HouseBRoof =
            "M -1 0.10 L -0.52 -0.66 L 0.52 -0.66 L 1 0.10";

        private const string HouseBBody =
            "M -0.82 0.14 L 0.82 0.14 "
            + "M -0.62 0.14 L -0.62 0.92 M 0.62 0.14 L 0.62 0.92 "
            + "M -0.72 0.92 L 0.72 0.92 "
            + "M -0.30 0.34 L -0.30 0.62 L 0.04 0.62 L 0.04 0.34 L -0.30 0.34";

        //==================== 檐灯 ====================

        private const string LanternPath =
            "M 0 -1 L 0 -0.66 "
            + "M -0.4 -0.5 Q -0.62 0 -0.4 0.5 Q 0 0.72 0.4 0.5 Q 0.62 0 0.4 -0.5 Q 0 -0.72 -0.4 -0.5 "
            + "M -0.46 -0.16 L 0.46 -0.16 M -0.46 0.16 L 0.46 0.16";

        //==================== 恶犬三态（侧影朝左，望着湖） ====================
        //坐·垂首

        private const string HoundIdleBody =
            "M -0.88 -0.34 Q -0.62 -0.50 -0.40 -0.46 Q -0.05 -0.54 0.28 -0.34 "
            + "Q 0.62 -0.02 0.66 0.42 L 0.60 0.92";

        private const string HoundIdleLimbs =
            "M -0.70 -0.28 Q -0.55 0.10 -0.42 0.90 "
            + "M -0.28 -0.02 L -0.22 0.90 "
            + "M 0.60 0.50 Q 0.28 0.62 0.18 0.90 "
            + "M 0.64 0.55 Q 0.98 0.42 1.05 0.05 "
            + "M -0.55 -0.50 L -0.44 -0.78 L -0.30 -0.48";

        //坐·昂首（倒影醒着 / 被注视）

        private const string HoundAlertBody =
            "M -0.80 -0.64 Q -0.58 -0.76 -0.40 -0.62 Q -0.06 -0.58 0.28 -0.36 "
            + "Q 0.62 -0.02 0.66 0.42 L 0.60 0.92";

        private const string HoundAlertLimbs =
            "M -0.62 -0.50 Q -0.52 0.10 -0.42 0.90 "
            + "M -0.28 -0.02 L -0.22 0.90 "
            + "M 0.60 0.50 Q 0.28 0.62 0.18 0.90 "
            + "M 0.64 0.55 Q 1.00 0.36 1.02 -0.04 "
            + "M -0.50 -0.76 L -0.40 -1.00 L -0.26 -0.72";

        //立·仰天嚎（身处鬼梦）

        private const string HoundHowlBody =
            "M -0.66 -1.02 Q -0.46 -1.10 -0.34 -0.92 Q -0.20 -0.62 0.02 -0.44 "
            + "Q 0.42 -0.26 0.66 0.06";

        private const string HoundHowlLimbs =
            "M -0.26 -0.36 L -0.36 0.92 M -0.10 -0.30 L -0.06 0.92 "
            + "M 0.50 -0.02 L 0.44 0.92 M 0.66 0.06 L 0.70 0.92 "
            + "M 0.66 0.10 Q 0.88 0.30 0.86 0.58 "
            + "M -0.44 -0.98 L -0.32 -1.18 L -0.22 -0.92";

        //犬目在各姿态里的归一位置

        private static readonly Vector2 EyeIdle = new(-0.64f, -0.38f);
        private static readonly Vector2 EyeAlert = new(-0.58f, -0.64f);
        private static readonly Vector2 EyeHowl = new(-0.44f, -0.94f);

        //==================== 芦苇 ====================

        private const string ReedBlades =
            "M 0 1 Q -0.06 0.40 0.08 -0.52 "
            + "M 0.10 1 Q 0.16 0.30 -0.10 -0.82 "
            + "M -0.12 1 Q -0.20 0.48 -0.34 -0.18 "
            + "M 0.02 -0.10 Q 0.22 -0.28 0.34 -0.50";

        //==================== 绘制 ====================

        /// <summary>鸟居：柱暗笔、梁亮芯</summary>
        public static void DrawTorii(SpriteBatch sb, Vector2 pos, float scale,
            Color body, Color core, float alpha) {
            SvgPath pillars = SvgPathPen.Path(ToriiPillars);
            SvgPath beams = SvgPathPen.Path(ToriiBeams);
            SvgPathPen.Stroke(sb, pillars, pos, scale, 0f, body, 2.2f, alpha);
            SvgPathPen.Stroke(sb, beams, pos, scale, 0f, body, 1.6f, alpha, core: core);
        }

        /// <summary>民居线稿：脊粗身细</summary>
        public static void DrawHouse(SpriteBatch sb, Vector2 pos, float scale,
            bool variantB, Color body, float alpha) {
            SvgPath roof = SvgPathPen.Path(variantB ? HouseBRoof : HouseARoof);
            SvgPath walls = SvgPathPen.Path(variantB ? HouseBBody : HouseABody);
            SvgPathPen.Stroke(sb, roof, pos, scale, 0f, body, 2.0f, alpha);
            SvgPathPen.Stroke(sb, walls, pos, scale, 0f, body, 1.0f, alpha * 0.85f);
        }

        /// <summary>檐灯轮廓；灯芯亮光由调用方在加色批画</summary>
        public static void DrawLantern(SpriteBatch sb, Vector2 pos, float scale,
            Color body, float alpha) {
            SvgPath lantern = SvgPathPen.Path(LanternPath);
            SvgPathPen.Stroke(sb, lantern, pos, scale, 0f, body, 1.1f, alpha);
        }

        /// <summary>
        /// 恶犬：三姿态按权重叠画（权重和≤1 时用暗色多画无害，读感是姿态渐变）。
        /// 粗脊线当体、细笔挑腿尾；呼吸=极小幅整体缩放
        /// </summary>
        public static void DrawHound(SpriteBatch sb, Vector2 pos, float scale,
            float idleA, float alertA, float howlA,
            Color body, Color edge, float alpha, float time) {
            float breath = 1f + MathF.Sin(time * 1.1f) * 0.012f;
            float s = scale * breath;
            DrawHoundPose(sb, HoundIdleBody, HoundIdleLimbs, pos, s, body, edge, alpha * idleA);
            DrawHoundPose(sb, HoundAlertBody, HoundAlertLimbs, pos, s, body, edge, alpha * alertA);
            DrawHoundPose(sb, HoundHowlBody, HoundHowlLimbs, pos, s, body, edge, alpha * howlA);
        }

        private static void DrawHoundPose(SpriteBatch sb, string bodyPath, string limbPath,
            Vector2 pos, float scale, Color body, Color edge, float alpha) {
            if (alpha <= 0.01f) {
                return;
            }
            SvgPath spine = SvgPathPen.Path(bodyPath);
            SvgPath limbs = SvgPathPen.Path(limbPath);
            //粗笔当体：脊线三层加宽垫出体量，暗色不吃加色规则
            SvgPathPen.Stroke(sb, spine, pos, scale, 0f, body, 4.6f, alpha * 0.92f);
            SvgPathPen.Stroke(sb, spine, pos, scale, 0f, body, 2.6f, alpha);
            SvgPathPen.Stroke(sb, limbs, pos, scale, 0f, body, 1.5f, alpha * 0.95f);
            //背脊一线受光
            SvgPathPen.Stroke(sb, spine, pos, scale, 0f, edge, 0.9f, alpha * 0.30f);
        }

        /// <summary>犬目锚点（世界侧加色层画烬目用），按姿态权重插值</summary>
        public static Vector2 HoundEyeAnchor(Vector2 pos, float scale,
            float idleA, float alertA, float howlA) {
            float sum = MathF.Max(idleA + alertA + howlA, 0.001f);
            Vector2 uv = (EyeIdle * idleA + EyeAlert * alertA + EyeHowl * howlA) / sum;
            return pos + uv * scale;
        }

        /// <summary>芦苇一丛：随风微摆</summary>
        public static void DrawReeds(SpriteBatch sb, Vector2 pos, float scale,
            Color body, float alpha, float time, float seed, bool flip) {
            SvgPath reeds = SvgPathPen.Path(ReedBlades);
            float sway = MathF.Sin(time * 0.8f + seed * 11.3f) * 0.05f;
            float s = flip ? -scale : scale;
            SvgPathPen.Stroke(sb, reeds, pos, s, sway, body, 1.2f, alpha);
        }

        /// <summary>
        /// 装裱轴：横卷左右两根卷杆（暗杆 + 亮芯 + 上下轴头），
        /// 画开合时轴杆贴着画心两缘走
        /// </summary>
        public static void DrawRollers(SpriteBatch sb, Rectangle canvas,
            Color bar, Color core, float alpha) {
            foreach (float x in (Span<float>)[canvas.Left - 7f, canvas.Right + 7f]) {
                Vector2 top = new(x, canvas.Top - 9f);
                Vector2 bottom = new(x, canvas.Bottom + 9f);
                KikasaVaults.KikasaVaultRenderer.DrawLine(sb, top, bottom, 4.6f, bar * alpha);
                KikasaVaults.KikasaVaultRenderer.DrawLine(sb, top, bottom, 1.4f, core * (alpha * 0.55f));
                //轴头：两端一节短粗杆
                KikasaVaults.KikasaVaultRenderer.DrawLine(sb, top - new Vector2(0f, 6f), top,
                    7f, bar * alpha);
                KikasaVaults.KikasaVaultRenderer.DrawLine(sb, bottom, bottom + new Vector2(0f, 6f),
                    7f, bar * alpha);
            }
        }
    }
}
