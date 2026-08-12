using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.MiningMachines
{
    /// <summary>
    /// 矿机升级模块图标合成器:无贴图资产,逐帧由 SVG 路径描出。<br/>
    /// 外形(切角钢牌 + 四角铆钉)全模块共用,功能纹按 Key 取;缺纹样时退回通用钻齿纹,
    /// 新模块不配纹样也能立刻上线。归一 [-1,1] 空间,任意尺寸锐利
    /// </summary>
    internal static class MiningModuleGlyph
    {
        #region 共用外形
        //切角钢牌轮廓。SvgPathPen 不支持 A 指令,圆角一律用切角或 Q 表达
        private const string PlatePath =
            "M -0.62 -0.86 L 0.62 -0.86 L 0.86 -0.62 L 0.86 0.62 "
            + "L 0.62 0.86 L -0.62 0.86 L -0.86 0.62 L -0.86 -0.62 Z";

        //四角铆钉:单点子路径按点凿绘制
        private const string RivetsPath =
            "M -0.68 -0.68 M 0.68 -0.68 M 0.68 0.68 M -0.68 0.68";

        //通用钻齿纹:居中钻杆 + 两侧咬合齿
        private const string FallbackDie =
            "M 0 -0.52 L 0 0.24 M -0.14 0.24 L 0 0.56 L 0.14 0.24 "
            + "M -0.42 -0.30 L -0.20 -0.18 L -0.42 -0.06 "
            + "M 0.42 -0.30 L 0.20 -0.18 L 0.42 -0.06";

        private static readonly Dictionary<string, string> diePaths = new(StringComparer.Ordinal);

        /// <summary>登记某模块的功能纹,Key 用物品类名</summary>
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
        //基板铁灰,带一点暖锈调,和工业域的锈铁面板同族
        private static readonly Color Substrate = new(26, 22, 20);
        private static readonly Color SubstrateLit = new(44, 38, 34);
        //铆钉与包边的黄铜色
        private static readonly Color Brass = new(148, 118, 62);

        /// <summary>
        /// 画一枚模块铭牌
        /// </summary>
        /// <param name="half">半边长(像素),整枚约 1.8×half 宽</param>
        /// <param name="accent">功能纹主色,模块各自指定</param>
        /// <param name="time">动效相位,一般用 Main.GameUpdateCount * 0.02f</param>
        internal static void Draw(SpriteBatch sb, string dieKey, Vector2 center, float half,
            float alpha, Color accent, float rotation = 0f, float time = 0f) {
            if (alpha <= 0.004f || half <= 0.5f) {
                return;
            }
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null) {
                return;
            }

            DrawPlate(sb, pixel, center, half, alpha, rotation);

            float lineWidth = MathF.Max(half * 0.105f, 1f);

            //钢牌包边:黄铜色描一圈,先立住轮廓
            SvgPath plate = SvgPathPen.Path(PlatePath);
            SvgPathPen.Stroke(sb, plate, center, half, rotation,
                Brass, lineWidth, alpha * 0.9f);

            //铆钉
            SvgPath rivets = SvgPathPen.Path(RivetsPath);
            SvgPathPen.Stroke(sb, rivets, center, half, rotation,
                Color.Lerp(Brass, Color.White, 0.25f), lineWidth * 0.9f, alpha * 0.9f);

            //功能纹 + 更细的亮芯
            SvgPath die = SvgPathPen.Path(ResolveDie(dieKey));
            SvgPathPen.Stroke(sb, die, center, half, rotation,
                accent, lineWidth * 0.9f, alpha * 0.92f,
                core: Color.Lerp(accent, Color.White, 0.5f));

            //一段亮笔沿功能纹缓行,读作通电运转而不是发光贴纸;节奏比芯片沉
            float head = time * 0.12f;
            SvgPathPen.StrokeRunner(sb, die, center, half, rotation,
                Color.Lerp(accent, Color.White, 0.7f), lineWidth * 0.7f, alpha * 0.8f,
                head, 0.14f);

            DrawSheen(sb, pixel, center, half, alpha, rotation, time);
        }

        /// <summary>暗处可寻的背光,世界掉落物专用</summary>
        internal static void DrawBacklight(SpriteBatch sb, Vector2 center, float half,
            Color accent, float alpha) {
            SvgPathPen.SoftDot(sb, center, half * 1.5f, accent, alpha);
        }

        //基板:两块交叠矩形凑出切角轮廓,一块方块会在四角戳出框外
        private static void DrawPlate(SpriteBatch sb, Texture2D pixel, Vector2 center,
            float half, float alpha, float rotation) {
            Rectangle src = new(0, 0, 1, 1);
            Vector2 origin = new(0.5f);

            //贴身投影:只偏 1px,不做同心放大的假羽化
            Vector2 shadowOffset = new Vector2(1f, 1.4f).RotatedBy(rotation) * (half / 14f);
            sb.Draw(pixel, center + shadowOffset, src, new Color(5, 4, 3) * (alpha * 0.55f),
                rotation, origin, new Vector2(half * 1.78f, half * 1.36f), SpriteEffects.None, 0f);

            sb.Draw(pixel, center, src, Substrate * alpha, rotation, origin,
                new Vector2(half * 1.74f, half * 1.32f), SpriteEffects.None, 0f);
            sb.Draw(pixel, center, src, Substrate * alpha, rotation, origin,
                new Vector2(half * 1.32f, half * 1.74f), SpriteEffects.None, 0f);

            //顶部受光带,给钢牌一点厚度
            Vector2 litOffset = new Vector2(0f, -half * 0.62f).RotatedBy(rotation);
            sb.Draw(pixel, center + litOffset, src, SubstrateLit * (alpha * 0.5f),
                rotation, origin, new Vector2(half * 1.62f, half * 0.34f), SpriteEffects.None, 0f);
        }

        //拉丝金属的慢速斜向光泽,比芯片的扫描线沉稳
        private static void DrawSheen(SpriteBatch sb, Texture2D pixel, Vector2 center,
            float half, float alpha, float rotation, float time) {
            float sweep = time * 0.16f % 2.2f - 1.1f;
            if (sweep < -0.82f || sweep > 0.82f) {
                return;
            }
            float edge = 1f - MathHelper.Clamp((MathF.Abs(sweep) - 0.5f) / 0.32f, 0f, 1f);
            if (edge <= 0.01f) {
                return;
            }
            //斜置的一道窄亮带,顺着切角方向扫过
            Vector2 offset = new Vector2(sweep * half * 0.8f, sweep * half).RotatedBy(rotation);
            sb.Draw(pixel, center + offset, new Rectangle(0, 0, 1, 1),
                new Color(220, 205, 180) * (alpha * 0.16f * edge),
                rotation + MathHelper.PiOver4 * 0.5f, new Vector2(0.5f),
                new Vector2(half * 1.4f * edge, MathF.Max(half * 0.05f, 1f)),
                SpriteEffects.None, 0f);
        }
        #endregion
    }

    internal sealed class MiningModuleGlyphLoader : ICWRLoader
    {
        void ICWRLoader.UnLoadData() => MiningModuleGlyph.ClearRegistry();
    }
}
