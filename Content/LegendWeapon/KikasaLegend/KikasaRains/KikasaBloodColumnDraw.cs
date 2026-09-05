using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains
{
    /// <summary>
    /// 血柱绘制辅助:普攻血柱(<see cref="KikasaBloodColumn"/>)与血形态三泉(<see cref="KikasaInkGeyser"/>)
    /// 共用同一根 TechColumn quad 与同一套精灵回退。画布契约:根钉在水线/地面,
    /// 上端留 1.6W 冠部绽开余幅,下端沉到线下 1.2W(判定根在水下);
    /// 横向 quadW=WidthPx/0.31(xc∈[-1,1] 半宽 0.31 折算,可见全宽≈1.1×WidthPx,判定 0.7× 藏在体内)
    /// </summary>
    internal static class KikasaBloodColumnDraw
    {
        /// <summary>冠部余幅与水下余幅(柱宽倍数),与 shader 内 crown 1.6 / 水下 -1.2 的范围同源</summary>
        private const float PadTopW = 1.6f;
        private const float PadBottomW = 1.2f;

        /// <summary>起柱过冲上限,quad 高度按此预留</summary>
        public const float RiseOvershoot = 1.08f;

        /// <summary>
        /// 着色器路径。fullHeightPx=满柱高(定 quad),heightNowPx=当前柱高(含过冲),
        /// collapseT 塌回进度由 shader 自算下坠与根部颈缩,ke 喂冠量,mound 根部溅裙强度,
        /// fallback 两翼回落帘强度(液体到顶后往回落的那一层,与芯反向流)
        /// </summary>
        public static void DrawQuad(SpriteBatch sb, Effect fx, Texture2D canvas, Vector2 rootPos,
            float widthPx, float fullHeightPx, float heightNowPx, float collapseT,
            float seed, float fade, float ke, float mound, float fallback) {
            widthPx = MathF.Max(widthPx, 8f);
            float padTop = widthPx * PadTopW;
            float padBottom = widthPx * PadBottomW;
            float bodySpan = fullHeightPx * RiseOvershoot;
            float quadH = padTop + bodySpan + padBottom;
            float ws = widthPx / quadH;

            fx.Parameters["uSeed"]?.SetValue(seed);
            fx.Parameters["uFade"]?.SetValue(fade);
            fx.Parameters["uWScale"]?.SetValue(ws);
            fx.Parameters["uRootV"]?.SetValue((padTop + bodySpan) / quadH);
            fx.Parameters["uHeightW"]?.SetValue(MathF.Max(heightNowPx, 1f) / widthPx);
            fx.Parameters["uCollapse"]?.SetValue(MathHelper.Clamp(collapseT, 0f, 1f));
            fx.Parameters["uKe"]?.SetValue(MathHelper.Clamp(ke, 0f, 1f));
            fx.Parameters["uMound"]?.SetValue(MathHelper.Clamp(mound, 0f, 1f));
            fx.Parameters["uFallback"]?.SetValue(MathHelper.Clamp(fallback, 0f, 1f));
            fx.Parameters["uColBody"]?.SetValue(KikasaInk.BloodBody.ToVector3());
            fx.Parameters["uColDeep"]?.SetValue(KikasaInk.BloodDeep.ToVector3());
            fx.Parameters["uColBright"]?.SetValue(KikasaInk.BloodBright.ToVector3());
            fx.Parameters["uColSheen"]?.SetValue(KikasaInk.BloodSheen.ToVector3());
            fx.CurrentTechnique = fx.Techniques["TechColumn"];
            fx.CurrentTechnique.Passes[0].Apply();

            float quadW = widthPx / 0.31f;
            Vector2 topLeft = rootPos - new Vector2(quadW * 0.5f, padTop + bodySpan) - Main.screenPosition;
            sb.Draw(canvas, topLeft, null, Color.White, 0f, Vector2.Zero,
                new Vector2(quadW / canvas.Width, quadH / canvas.Height), SpriteEffects.None, 0f);
        }

        /// <summary>
        /// 精灵回退:根部溅丘(坐进水里)→柱身(暗缘+血体+亮芯细线)→头部圆冠;
        /// 塌回按进度整柱下坠+根部收窄,不淡出
        /// </summary>
        public static void DrawFallback(SpriteBatch sb, Vector2 rootPos, float widthPx,
            float heightNowPx, float collapseT, float seed, float life, float fade) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null || heightNowPx <= 2f || widthPx <= 2f) {
                return;
            }
            float drop = collapseT * collapseT;
            float h = heightNowPx * (1f - drop * 0.9f);
            float w = widthPx * (1f - collapseT * 0.4f);
            Vector2 basePos = rootPos - Main.screenPosition + new Vector2(0f, 6f);
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 columnOrigin = new(tex.Width * 0.5f, tex.Height);
            float sway = MathF.Sin(life * 0.23f + seed * 3f) * 0.02f;
            float alpha = fade * (1f - collapseT * 0.35f);

            sb.Draw(tex, basePos + new Vector2(0f, 2f), null,
                KikasaInk.BloodDeep * (0.8f * alpha * (1f - collapseT)), 0f, origin,
                new Vector2(w * 1.9f / tex.Width, 18f / tex.Height), SpriteEffects.None, 0f);
            sb.Draw(tex, basePos, null, KikasaInk.BloodDeep * (0.85f * alpha), sway,
                columnOrigin, new Vector2(w * 1.18f / tex.Width, h * 1.02f / tex.Height), SpriteEffects.None, 0f);
            sb.Draw(tex, basePos, null, KikasaInk.BloodBody * (0.95f * alpha), sway,
                columnOrigin, new Vector2(w / tex.Width, h / tex.Height), SpriteEffects.None, 0f);
            sb.Draw(tex, basePos + new Vector2(-w * 0.18f, 0f), null,
                KikasaInk.BloodBright * (0.45f * alpha), sway,
                columnOrigin, new Vector2(w * 0.16f / tex.Width, h * 0.85f / tex.Height), SpriteEffects.None, 0f);
            Vector2 head = basePos - new Vector2(0f, h);
            sb.Draw(tex, head, null, KikasaInk.BloodBody * (0.9f * alpha), 0f, origin,
                new Vector2(w * 0.9f / tex.Width, w * 0.6f / tex.Height), SpriteEffects.None, 0f);
            sb.Draw(tex, head + new Vector2(-w * 0.12f, -2f), null,
                (KikasaInk.BloodSheen with { A = 0 }) * (0.3f * alpha), 0f, origin,
                new Vector2(w * 0.28f / tex.Width, 3f / tex.Height), SpriteEffects.None, 0f);
        }
    }
}
