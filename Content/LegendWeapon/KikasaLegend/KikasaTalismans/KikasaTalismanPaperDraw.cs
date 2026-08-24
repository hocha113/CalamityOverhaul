using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans
{
    /// <summary>
    /// 唤雨符纸材质桥（KikasaTalisman.fx）：和纸纤维+折角压边+下缘雨浸在 shader 内；
    /// 缺编退回 <see cref="DrawFallback"/> 的像素纸条+浸边。
    /// 物品图标 / 湖心景祈雨绳 / 风铃微缩共用这一支笔
    /// </summary>
    internal static class KikasaTalismanPaperDraw
    {
        public static bool Available => EffectLoader.KikasaTalisman?.Value != null;

        //符纸色板：纸白/压边深红(血湖系)/浸墨/水光冷青，与沙盒验收口径一致
        public static readonly Color Paper = new(222, 214, 196);
        public static readonly Color Hem = new(112, 28, 38);
        public static readonly Color Ink = new(18, 23, 36);
        public static readonly Color Sheen = new(143, 194, 224);

        private static Texture2D Pixel => VaultAsset.placeholder2.Value;
        private static readonly Rectangle PixelSrc = new(0, 0, 1, 1);

        /// <summary>UI 空间画符纸，top=顶中点，rot=摆角，soak 0~1；批须 UI 批，画完复原</summary>
        public static void DrawUI(SpriteBatch sb, Vector2 top, float rot, Vector2 size,
            float alpha, float soak, float time) {
            Effect effect = EffectLoader.KikasaTalisman?.Value;
            if (effect == null) {
                DrawFallback(sb, top, rot, size, alpha, soak);
                return;
            }
            ApplyParams(effect, size, alpha, soak, time);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp, DepthStencilState.None,
                RasterizerState.CullNone, effect, Main.UIScaleMatrix);

            sb.Draw(Pixel, top, PixelSrc, Color.White,
                rot, new Vector2(0.5f, 0f), size, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }

        /// <summary>世界空间画符纸（掉落物），批须世界批，画完复原</summary>
        public static void DrawWorld(SpriteBatch sb, Vector2 top, float rot, Vector2 size,
            float alpha, float soak, float time) {
            Effect effect = EffectLoader.KikasaTalisman?.Value;
            if (effect == null) {
                DrawFallback(sb, top, rot, size, alpha, soak);
                return;
            }
            ApplyParams(effect, size, alpha, soak, time);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp, DepthStencilState.None,
                RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);

            sb.Draw(Pixel, top, PixelSrc, Color.White,
                rot, new Vector2(0.5f, 0f), size, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private static void ApplyParams(Effect effect, Vector2 size, float alpha, float soak, float time) {
            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uSoak"]?.SetValue(MathHelper.Clamp(soak, 0f, 1f));
            effect.Parameters["uSize"]?.SetValue(size);
            effect.Parameters["uColPaper"]?.SetValue(Paper.ToVector3());
            effect.Parameters["uColHem"]?.SetValue(Hem.ToVector3());
            effect.Parameters["uColInk"]?.SetValue(Ink.ToVector3());
            effect.Parameters["uColSheen"]?.SetValue(Sheen.ToVector3());
        }

        /// <summary>
        /// CPU 回退：纸底+顶折角+双侧压边+下缘浸墨带，在当前批直接画（无批切换）
        /// </summary>
        public static void DrawFallback(SpriteBatch sb, Vector2 top, float rot, Vector2 size,
            float alpha, float soak) {
            Vector2 down = rot.ToRotationVector2().RotatedBy(MathHelper.PiOver2);
            Vector2 side = rot.ToRotationVector2();
            Vector2 center = top + down * size.Y * 0.5f;
            Vector2 o = new(0.5f);

            //纸底
            sb.Draw(Pixel, center, PixelSrc, Paper * (alpha * 0.96f), rot, o, size, SpriteEffects.None, 0f);
            //顶折角压暗
            sb.Draw(Pixel, top + down * 2f, PixelSrc, Paper * 0.62f * alpha, rot, new Vector2(0.5f, 0f),
                new Vector2(size.X, 4f), SpriteEffects.None, 0f);
            //双侧压边
            sb.Draw(Pixel, center - side * (size.X * 0.5f - 0.8f), PixelSrc, Hem * (alpha * 0.75f),
                rot, o, new Vector2(1.6f, size.Y), SpriteEffects.None, 0f);
            sb.Draw(Pixel, center + side * (size.X * 0.5f - 0.8f), PixelSrc, Hem * (alpha * 0.75f),
                rot, o, new Vector2(1.6f, size.Y), SpriteEffects.None, 0f);
            //下缘浸墨带
            float wetH = MathF.Max(size.Y * MathHelper.Clamp(soak, 0f, 1f) * 0.9f, 2f);
            Vector2 wetCenter = top + down * (size.Y - wetH * 0.5f);
            sb.Draw(Pixel, wetCenter, PixelSrc, Color.Lerp(Paper * 0.6f, Ink, 0.6f) * (alpha * 0.9f),
                rot, o, new Vector2(size.X, wetH), SpriteEffects.None, 0f);
            //浸线一划水光
            sb.Draw(Pixel, top + down * (size.Y - wetH), PixelSrc, Sheen * (alpha * 0.5f),
                rot, o, new Vector2(size.X, 1.4f), SpriteEffects.None, 0f);
        }
    }
}
