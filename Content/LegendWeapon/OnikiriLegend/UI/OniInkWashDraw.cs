using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 封印札 HUD 簇的底墨横扫(OniHudInkWash.fx)：一笔自屏外扫入的宽幅墨道垫在
    /// 札/墨脉/鞘刀之下,把三件缝进同一幅画。reveal 驱动入场书写,danger 渗鬼火青斑。<br/>
    /// shader 缺席时退回 CPU 粗笔(逐行断墨带 + 中锋两笔),不求精致只求画底不缺席
    /// </summary>
    internal static class OniInkWashDraw
    {
        public static bool Available => EffectLoader.OniHudInkWash?.Value != null;

        /// <summary>
        /// 绘制底墨。dest 为 quad 区域(允许越出屏幕左缘),reveal/danger 皆 0~1 已缓动。
        /// 调用方保证当前批为 Deferred+UIScaleMatrix
        /// </summary>
        public static void Draw(SpriteBatch sb, Rectangle dest, float alpha, float reveal, float danger, float time) {
            if (alpha <= 0.01f || reveal <= 0.001f) {
                return;
            }
            Effect effect = EffectLoader.OniHudInkWash?.Value;
            if (effect == null) {
                DrawFallback(sb, dest, alpha, reveal);
                return;
            }

            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uReveal"]?.SetValue(reveal);
            effect.Parameters["uDanger"]?.SetValue(danger);
            effect.Parameters["uSeed"]?.SetValue(OnikiriUITheme.HudInkWashSeed);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(dest.Width, dest.Height));
            effect.Parameters["uColInk"]?.SetValue(OnikiriUITheme.Ink.ToVector3());
            effect.Parameters["uColDark"]?.SetValue(OnikiriUITheme.Dark.ToVector3());
            effect.Parameters["uColDeep"]?.SetValue(OnikiriUITheme.Deep.ToVector3());
            effect.Parameters["uColBright"]?.SetValue(OnikiriUITheme.Bright.ToVector3());
            effect.Parameters["uColHot"]?.SetValue(OnikiriUITheme.HotWhite.ToVector3());
            effect.Parameters["uColGhost"]?.SetValue(OnikiriUITheme.GhostFire.ToVector3());

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp, DepthStencilState.None,
                RasterizerState.CullNone, effect, Main.UIScaleMatrix);

            sb.Draw(VaultAsset.placeholder2.Value, dest, new Rectangle(0, 0, 1, 1), Color.White);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }

        /// <summary>CPU 粗笔:中线斜势 + 逐行哈希断墨带,行长随 reveal 书写</summary>
        private static void DrawFallback(SpriteBatch sb, Rectangle dest, float alpha, float reveal) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            const int Rows = 9;
            float baseY = dest.Y + dest.Height * 0.40f;

            for (int i = 0; i < Rows; i++) {
                float t = (i + 0.5f) / Rows;
                float hash = OniBrush.Hash01(i * 131 + 17);
                //行带贴着中线上下铺开,外行更短更淡
                float spread = (t - 0.45f) * dest.Height * 0.44f;
                float lenFrac = (0.98f - MathF.Abs(t - 0.45f) * 1.15f) * (0.86f + hash * 0.14f);
                float len = dest.Width * MathHelper.Clamp(lenFrac, 0.15f, 1f) * reveal;
                if (len < 4f) {
                    continue;
                }
                float y = baseY + spread + (hash - 0.5f) * 5f;
                //斜势:行越靠下越向右错
                float x = dest.X + spread * 0.55f + hash * 8f;
                float rowH = dest.Height * (0.055f + hash * 0.03f);
                float rowA = alpha * (0.30f - MathF.Abs(t - 0.45f) * 0.34f);
                if (rowA <= 0.01f) {
                    continue;
                }
                Color col = Color.Lerp(OnikiriUITheme.Ink, OnikiriUITheme.Dark, hash * 0.6f);
                sb.Draw(pixel, new Vector2(x, y), src, col * rowA, 0.015f,
                    new Vector2(0f, 0.5f), new Vector2(len, rowH), SpriteEffects.None, 0f);
            }

            //中锋两笔:给粗糙墨带一点"笔"的骨
            Vector2 start = new(dest.X + 2f, baseY - 2f);
            Vector2 end = new(dest.X + dest.Width * 0.96f, baseY + dest.Height * 0.10f);
            OniBrush.DrawTaperedSlash(sb, start, end, dest.Height * 0.16f, 1.6f, alpha * 0.34f, reveal);
            OniBrush.DrawTaperedSlash(sb, start + new Vector2(6f, 9f), end + new Vector2(-14f, 6f),
                dest.Height * 0.09f, 1.1f, alpha * 0.22f, reveal);
        }
    }
}
