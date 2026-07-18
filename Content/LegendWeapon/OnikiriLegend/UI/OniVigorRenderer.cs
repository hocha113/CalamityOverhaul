using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 气力墨脉绘制：shader 负责湿墨、宣纸纤维、干笔飞白与消耗残痕，
    /// CPU 只压住连接墨丝和朱印，shader 缺失时退回简化笔触
    /// </summary>
    internal static class OniVigorRenderer
    {
        private const float ShapeSeed = 0.371f;

        private static Texture2D Pixel => VaultAsset.placeholder2.Value;
        private static readonly Rectangle PixelSource = new(0, 0, 1, 1);

        public static bool Available => EffectLoader.OniVigorInk?.Value != null;

        public static void Draw(SpriteBatch spriteBatch, Rectangle destination, Vector2 linkFrom,
            float alpha, float time, float fill, float trailFill, float flow,
            float spendPulse, float gainPulse, float fullPulse) {
            Vector4 stroke = OnikiriUITheme.VigorStroke;
            Vector2 strokeStart = new(destination.X + stroke.X, destination.Y + stroke.Z);
            Vector2 strokeEnd = new(destination.X + stroke.Y, destination.Y + stroke.Z);

            //札面与墨脉之间只牵一根软墨丝，纸札可摆、读数本体保持稳定
            OniBrush.DrawGradientLine(spriteBatch, linkFrom, strokeStart,
                OnikiriUITheme.Deep * (alpha * 0.18f),
                OnikiriUITheme.Seal * (alpha * 0.76f), 1.15f);

            Effect effect = EffectLoader.OniVigorInk?.Value;
            if (effect != null) {
                DrawShader(spriteBatch, effect, destination, alpha, time, fill,
                    MathHelper.Clamp(Math.Max(fill, trailFill), 0f, 1f), flow,
                    spendPulse, gainPulse, fullPulse);
            }
            else {
                DrawFallback(spriteBatch, strokeStart, strokeEnd, alpha, fill, trailFill);
            }

            float flash = Math.Max(fullPulse, spendPulse * 0.35f);
            if (flash > 0.02f) {
                OniBrush.DrawBacklight(spriteBatch, strokeStart, 13f + flash * 8f,
                    fullPulse > spendPulse ? OnikiriUITheme.HotWhite : OnikiriUITheme.Bright,
                    alpha * flash * 0.42f);
            }

            float sealIntegrity = MathHelper.Clamp(fill / 0.32f, 0.24f, 1f);
            float sealScale = 11f * (1f + gainPulse * 0.04f + fullPulse * 0.08f);
            float sealRot = flow * 0.025f + (float)System.Math.Sin(time * 15f) * spendPulse * 0.025f;
            OniBrush.DrawSealGlyph(spriteBatch, strokeStart, sealScale, alpha * 0.96f, sealRot, sealIntegrity);
        }

        private static void DrawShader(SpriteBatch spriteBatch, Effect effect, Rectangle destination,
            float alpha, float time, float fill, float trailFill, float flow,
            float spendPulse, float gainPulse, float fullPulse) {
            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(destination.Width, destination.Height));
            effect.Parameters["uStroke"]?.SetValue(OnikiriUITheme.VigorStroke);
            effect.Parameters["uFill"]?.SetValue(MathHelper.Clamp(fill, 0f, 1f));
            effect.Parameters["uTrailFill"]?.SetValue(trailFill);
            effect.Parameters["uFlow"]?.SetValue(MathHelper.Clamp(flow, -1f, 1f));
            effect.Parameters["uSpendPulse"]?.SetValue(MathHelper.Clamp(spendPulse, 0f, 1f));
            effect.Parameters["uGainPulse"]?.SetValue(MathHelper.Clamp(gainPulse, 0f, 1f));
            effect.Parameters["uFullPulse"]?.SetValue(MathHelper.Clamp(fullPulse, 0f, 1f));
            effect.Parameters["uSeed"]?.SetValue(ShapeSeed);
            effect.Parameters["uColPaper"]?.SetValue(OnikiriUITheme.Paper.ToVector3());
            effect.Parameters["uColInk"]?.SetValue(OnikiriUITheme.Ink.ToVector3());
            effect.Parameters["uColDeep"]?.SetValue(OnikiriUITheme.Deep.ToVector3());
            effect.Parameters["uColBright"]?.SetValue(OnikiriUITheme.Bright.ToVector3());
            effect.Parameters["uColHot"]?.SetValue(OnikiriUITheme.HotWhite.ToVector3());

            GraphicsDevice graphicsDevice = Main.graphics.GraphicsDevice;
            Texture previousTexture = graphicsDevice.Textures[1];
            SamplerState previousSampler = graphicsDevice.SamplerStates[1];
            Texture2D noise = OnikiriAssets.NoiseSoft01?.Value ?? Pixel;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp, DepthStencilState.None,
                RasterizerState.CullNone, effect, Main.UIScaleMatrix);
            graphicsDevice.Textures[1] = noise;
            graphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            spriteBatch.Draw(Pixel, destination, PixelSource, Color.White);
            spriteBatch.End();
            graphicsDevice.Textures[1] = previousTexture;
            graphicsDevice.SamplerStates[1] = previousSampler;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }

        private static void DrawFallback(SpriteBatch spriteBatch, Vector2 start, Vector2 end,
            float alpha, float fill, float trailFill) {
            OniBrush.DrawGradientLine(spriteBatch, start, end,
                OnikiriUITheme.TextDim * (alpha * 0.24f),
                OnikiriUITheme.Dark * (alpha * 0.08f), 2.2f);

            Vector2 edge = end - start;
            float clampedFill = MathHelper.Clamp(fill, 0f, 1f);
            float clampedTrail = MathHelper.Clamp(Math.Max(fill, trailFill), 0f, 1f);
            if (clampedTrail > clampedFill + 0.005f) {
                OniBrush.DrawGradientLine(spriteBatch, start + edge * clampedFill, start + edge * clampedTrail,
                    OnikiriUITheme.Bright * (alpha * 0.58f),
                    OnikiriUITheme.Deep * (alpha * 0.05f), 3.2f);
            }
            if (clampedFill > 0.005f) {
                OniBrush.DrawTaperedSlash(spriteBatch, start, start + edge * clampedFill,
                    5.8f, 1.5f, alpha * 0.92f);
            }
        }
    }
}
