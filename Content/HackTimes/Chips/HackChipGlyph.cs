using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.HackTimes.Chips
{
    /// <summary>
    /// 协议芯片图标合成器：无贴图资产，逐帧由 SVG 路径描出。<br/>
    /// 外形（切角框 + 引脚）全芯片共用，晶粒纹按协议 Key 取；缺纹样时退回通用电路纹，
    /// 所以新芯片不配纹样也能立刻上线，回头再补。<br/>
    /// 归一 [-1,1] 空间，任意尺寸锐利
    /// </summary>
    internal static class HackChipGlyph
    {
        #region 共用外形

        //切角方框。SvgPathPen 不支持 A 指令，圆角一律用切角或 Q 表达
        private const string FramePath =
            "M -0.70 -0.92 L 0.70 -0.92 L 0.92 -0.70 L 0.92 0.70 "
            + "L 0.70 0.92 L -0.70 0.92 L -0.92 0.70 L -0.92 -0.70 Z";

        //左右各三条引脚
        private const string PinsPath =
            "M -1.08 -0.46 L -0.92 -0.46 M -1.08 0 L -0.92 0 M -1.08 0.46 L -0.92 0.46 "
            + "M 0.92 -0.46 L 1.08 -0.46 M 0.92 0 L 1.08 0 M 0.92 0.46 L 1.08 0.46";

        //通用电路纹：中央晶粒 + 四条折向引脚的走线
        private const string FallbackDie =
            "M -0.32 -0.32 L 0.32 -0.32 L 0.32 0.32 L -0.32 0.32 Z "
            + "M -0.32 -0.14 L -0.66 -0.14 L -0.66 -0.46 "
            + "M -0.32 0.14 L -0.66 0.14 L -0.66 0.46 "
            + "M 0.32 -0.14 L 0.66 -0.14 L 0.66 -0.46 "
            + "M 0.32 0.14 L 0.66 0.14 L 0.66 0.46";

        private static readonly Dictionary<string, string> diePaths = new(StringComparer.Ordinal);

        /// <summary>登记某协议的晶粒纹，Key 用协议类名</summary>
        internal static void Register(string key, string pathData) {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(pathData)) {
                return;
            }
            diePaths[key] = pathData;
        }

        internal static void ClearRegistry() => diePaths.Clear();

        private static string ResolveDie(string key)
            => key != null && diePaths.TryGetValue(key, out string data) ? data : FallbackDie;

        #endregion

        #region 绘制

        //基板底色，冷灰蓝而非纯黑，免得在背包深底里糊成一块
        private static readonly Color Substrate = new(18, 24, 32);
        private static readonly Color SubstrateLit = new(30, 40, 52);
        //引脚金属色
        private static readonly Color PinMetal = new(196, 172, 108);

        /// <summary>
        /// 画一枚芯片
        /// </summary>
        /// <param name="half">半边长（像素），整枚芯片约 2.2×half 宽</param>
        /// <param name="accent">走线主色，取协议类别色</param>
        /// <param name="time">动效相位，一般用 Main.GameUpdateCount * 0.02f</param>
        internal static void Draw(SpriteBatch sb, string dieKey, Vector2 center, float half,
            float alpha, Color accent, float rotation = 0f, float time = 0f) {
            if (alpha <= 0.004f || half <= 0.5f) {
                return;
            }
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null) {
                return;
            }

            DrawSubstrate(sb, pixel, center, half, alpha, rotation);

            float lineWidth = MathF.Max(half * 0.105f, 1f);
            SvgPath pins = SvgPathPen.Path(PinsPath);
            SvgPathPen.Stroke(sb, pins, center, half, rotation,
                PinMetal, lineWidth * 1.15f, alpha * 0.95f);

            //外框：暗底上先立住轮廓，走线才不会读成悬空的线团
            SvgPath frame = SvgPathPen.Path(FramePath);
            SvgPathPen.Stroke(sb, frame, center, half, rotation,
                Color.Lerp(accent, Color.White, 0.15f), lineWidth, alpha * 0.85f);

            //晶粒走线 + 更细的亮芯
            SvgPath die = SvgPathPen.Path(ResolveDie(dieKey));
            SvgPathPen.Stroke(sb, die, center, half, rotation,
                accent, lineWidth * 0.85f, alpha * 0.9f,
                core: Color.Lerp(accent, Color.White, 0.55f));

            //一段亮笔沿走线巡行，读作电流而不是发光贴纸
            float head = time * 0.22f;
            SvgPathPen.StrokeRunner(sb, die, center, half, rotation,
                Color.Lerp(accent, Color.White, 0.75f), lineWidth * 0.7f, alpha * 0.85f,
                head, 0.16f);

            DrawScanLine(sb, pixel, center, half, alpha, accent, rotation, time);
        }

        /// <summary>暗处可寻的背光，世界掉落物专用</summary>
        internal static void DrawBacklight(SpriteBatch sb, Vector2 center, float half,
            Color accent, float alpha) {
            SvgPathPen.SoftDot(sb, center, half * 1.5f, accent, alpha);
        }

        //基板：两块交叠矩形凑出切角轮廓，一块方块会在四角戳出框外
        private static void DrawSubstrate(SpriteBatch sb, Texture2D pixel, Vector2 center,
            float half, float alpha, float rotation) {
            Rectangle src = new(0, 0, 1, 1);
            Vector2 origin = new(0.5f);

            //贴身投影：只偏 1px，不做同心放大的假羽化
            Vector2 shadowOffset = new Vector2(1f, 1.4f).RotatedBy(rotation) * (half / 14f);
            sb.Draw(pixel, center + shadowOffset, src, new Color(4, 6, 9) * (alpha * 0.55f),
                rotation, origin, new Vector2(half * 1.78f, half * 1.36f), SpriteEffects.None, 0f);

            sb.Draw(pixel, center, src, Substrate * alpha, rotation, origin,
                new Vector2(half * 1.74f, half * 1.32f), SpriteEffects.None, 0f);
            sb.Draw(pixel, center, src, Substrate * alpha, rotation, origin,
                new Vector2(half * 1.32f, half * 1.74f), SpriteEffects.None, 0f);

            //顶部受光带，给基板一点厚度
            Vector2 litOffset = new Vector2(0f, -half * 0.62f).RotatedBy(rotation);
            sb.Draw(pixel, center + litOffset, src, SubstrateLit * (alpha * 0.5f),
                rotation, origin, new Vector2(half * 1.62f, half * 0.34f), SpriteEffects.None, 0f);
        }

        //晶粒面上的一道横扫亮线，越过基板就不画
        private static void DrawScanLine(SpriteBatch sb, Texture2D pixel, Vector2 center,
            float half, float alpha, Color accent, float rotation, float time) {
            float sweep = time * 0.35f % 1.6f - 0.3f;
            if (sweep < -0.82f || sweep > 0.82f) {
                return;
            }
            //贴近上下缘时收窄并淡出，读作扫过而不是横切
            float edge = 1f - MathHelper.Clamp((MathF.Abs(sweep) - 0.55f) / 0.27f, 0f, 1f);
            if (edge <= 0.01f) {
                return;
            }
            Vector2 offset = new Vector2(0f, sweep * half).RotatedBy(rotation);
            sb.Draw(pixel, center + offset, new Rectangle(0, 0, 1, 1),
                Color.Lerp(accent, Color.White, 0.5f) * (alpha * 0.30f * edge),
                rotation, new Vector2(0.5f), new Vector2(half * 1.5f * edge, MathF.Max(half * 0.06f, 1f)),
                SpriteEffects.None, 0f);
        }

        #endregion
    }

    internal sealed class HackChipGlyphLoader : ICWRLoader
    {
        void ICWRLoader.UnLoadData() => HackChipGlyph.ClearRegistry();
    }
}
