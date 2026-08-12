using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaCultist
{
    /// <summary>
    /// 鬼奴邪教徒的程序化符文绘制小件：伪符文字形（确定性哈希笔画）与
    /// 水面法阵环（压扁椭圆 + 逐字点亮 + 游走笔尖 + 水面倒影）。
    /// 出水符文环、溶解符文环与水面法阵弹幕共用，全部走加色批、不掷 Main.rand
    /// </summary>
    internal static class KikasaCultistRunes
    {
        /// <summary>水面透视的纵向压扁比</summary>
        internal const float Flatten = 0.30f;

        /// <summary>确定性 0~1 哈希，符文笔画布局与抖动共用（各端一致）</summary>
        internal static float Hash01(float n) {
            float s = MathF.Sin(n * 127.1f + 311.7f) * 43758.547f;
            return s - MathF.Floor(s);
        }

        /// <summary>椭圆槽位：angle 处的水面环上一点（世界系）</summary>
        internal static Vector2 RingSlot(Vector2 center, float radius, float angle)
            => center + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius * Flatten);

        /// <summary>
        /// 一个伪符文字形：3 段确定性短笔画近乎直立地立在水面上，带微弱倒影。
        /// appear 0~1 驱动逐笔点亮，刚点亮的笔画泛白闪
        /// </summary>
        internal static void DrawGlyph(SpriteBatch sb, Texture2D glow, Vector2 pos, float height,
            int runeId, float seed, Color color, Color core, float appear, float alpha) {
            if (alpha <= 0.02f || appear <= 0f) {
                return;
            }
            Vector2 origin = glow.Size() * 0.5f;
            const int strokes = 3;
            for (int k = 0; k < strokes; k++) {
                float show = MathHelper.Clamp(appear * strokes - k, 0f, 1f);
                if (show <= 0f) {
                    continue;
                }
                float h0 = Hash01(runeId * 7.13f + k * 3.7f + seed);
                float h1 = Hash01(runeId * 11.9f + k * 5.1f + seed * 1.7f);
                float h2 = Hash01(runeId * 5.77f + k * 9.3f + seed * 0.6f);
                //笔画：主竖笔 + 两段斜短笔，端点在字形盒内取样
                float ang = -MathHelper.PiOver2 + (h0 - 0.5f) * (k == 0 ? 0.24f : 1.5f);
                float len = height * (k == 0 ? 0.95f : 0.36f + h1 * 0.3f);
                Vector2 off = new((h1 - 0.5f) * height * 0.5f, (h2 - 0.5f) * height * 0.42f);
                Vector2 c = pos + off - new Vector2(0f, height * 0.5f);
                //刚点亮的笔画白闪，随后落回符文色（len 是笔画全长，不走半径×2 语义）
                Color stroke = Color.Lerp(core, color, MathHelper.Clamp((show - 0.6f) * 2.5f + 0.55f, 0f, 1f));
                sb.Draw(glow, c - Main.screenPosition, null, stroke * (alpha * show), ang,
                    origin, new Vector2(len * 1.15f / glow.Width, 2.6f / glow.Height * height * 0.09f), SpriteEffects.None, 0f);
            }
            //水面倒影：整字压扁的一抹淡光
            sb.Draw(glow, pos + new Vector2(0f, height * 0.22f) - Main.screenPosition, null,
                color * (alpha * appear * 0.22f), 0f, origin,
                new Vector2(height * 1.1f / glow.Width, height * 0.3f / glow.Height), SpriteEffects.None, 0f);
        }

        /// <summary>
        /// 水面符文环全套：压扁双环 + count 个字形逐字点亮 + 游走笔尖光点。
        /// litT=点亮进度 0~1；spinPhase 驱动环上游光；调用方须已开加色批
        /// </summary>
        internal static void DrawWaterRing(SpriteBatch sb, Vector2 center, float radius, int count,
            float litT, float spinPhase, float seed, Color main, Color core, float alpha) {
            if (alpha <= 0.02f) {
                return;
            }
            Texture2D ring = CWRAsset.DiffusionCircle?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (ring == null || glow == null) {
                return;
            }

            //压扁双环：外环沉稳、内环略亮，亮度随铭刻进度渐显、半径轻微呼吸
            float breath = 1f + MathF.Sin(spinPhase * 2.3f) * 0.02f;
            float ringGrow = 0.22f + 0.78f * MathHelper.Clamp(litT, 0f, 1f);
            Vector2 rOrigin = ring.Size() * 0.5f;
            Vector2 outerScale = new(radius * 2.3f / ring.Width, radius * 2.3f * Flatten / ring.Height);
            sb.Draw(ring, center - Main.screenPosition, null, main * (alpha * 0.5f * ringGrow), 0f,
                rOrigin, outerScale * breath, SpriteEffects.None, 0f);
            sb.Draw(ring, center - Main.screenPosition, null, main * (alpha * 0.35f * ringGrow), 0f,
                rOrigin, outerScale * (0.72f * breath), SpriteEffects.None, 0f);

            //逐字点亮的符文
            for (int k = 0; k < count; k++) {
                float appear = MathHelper.Clamp(litT * count - k, 0f, 1f);
                if (appear <= 0f) {
                    continue;
                }
                float angle = -MathHelper.PiOver2 + k / (float)count * MathHelper.TwoPi;
                Vector2 slot = RingSlot(center, radius, angle);
                //后半圈的字略暗，读出环的进深
                float depth = 0.75f + 0.25f * MathF.Sin(angle);
                DrawGlyph(sb, glow, slot, 15f, k, seed, main, core, appear, alpha * depth);
            }

            //游走笔尖：沿环滑到当前正在铭刻的字位，写满后转为匀速巡环
            float penAngle = -MathHelper.PiOver2 + (litT < 1f ? litT : spinPhase * 0.35f % 1f) * MathHelper.TwoPi;
            Vector2 pen = RingSlot(center, radius, penAngle);
            Vector2 gOrigin = glow.Size() * 0.5f;
            sb.Draw(glow, pen - Main.screenPosition, null, core * (alpha * 0.85f), 0f,
                gOrigin, new Vector2(10f * 2f / glow.Width), SpriteEffects.None, 0f);
            sb.Draw(glow, pen - Main.screenPosition, null, main * (alpha * 0.5f), 0f,
                gOrigin, new Vector2(20f * 2f / glow.Width, 8f * 2f / glow.Height), SpriteEffects.None, 0f);
        }
    }
}
