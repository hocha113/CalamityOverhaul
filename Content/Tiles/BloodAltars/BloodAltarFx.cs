using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Tiles.BloodAltars
{
    /// <summary>
    /// 祭坛演出的绘制层：三个技法都是纯 PS 的世界 quad（SpriteBatch.Immediate + 白像素），
    /// 没有顶点缓冲，也就不涉及 GetTransfromMatrix 的世界/屏幕坐标陷阱。<br/>
    /// 色阶与 BloodAltarRite.fx 里的 ColDry/ColDeep/ColWet 一一对应，改一处要改两处
    /// </summary>
    internal static class BloodAltarFx
    {
        /// <summary>焦干血</summary>
        public static readonly Color ColDry = new(42, 4, 7);
        /// <summary>深血</summary>
        public static readonly Color ColDeep = new(107, 11, 18);
        /// <summary>湿血</summary>
        public static readonly Color ColWet = new(168, 18, 28);

        /// <summary>TP 的三个绘制层跑在框架已经开好的批里，自绘完必须原样还回去</summary>
        private static void RestoreTPBatch(SpriteBatch sb) {
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                , DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private static bool BeginShaderPass(SpriteBatch sb, string technique, out Effect effect) {
            effect = EffectLoader.BloodAltarRite?.Value;
            Texture2D noise = CWRAsset.NoiseSoft01?.Value;
            if (effect == null || noise == null) {
                effect = null;
                return false;
            }

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.CurrentTechnique = effect.Techniques[technique];
            return true;
        }

        private static void EndShaderPass(SpriteBatch sb) {
            sb.End();
            RestoreTPBatch(sb);
        }

        // ============================ 碗内血面 ============================

        public static void DrawPool(SpriteBatch sb, BloodAltarRite rite, Vector2 bowlCenter) {
            if (rite.Fill <= 0.01f) {
                return;
            }

            Rectangle dest = QuadRect(bowlCenter + new Vector2(0f, PoolHeight * 0.5f - 3f), PoolWidth, PoolHeight);
            if (!BeginShaderPass(sb, "TechPool", out Effect effect)) {
                DrawPoolFallback(sb, rite, dest);
                return;
            }

            effect.Parameters["uSeed"]?.SetValue(rite.Seed);
            effect.Parameters["uFill"]?.SetValue(rite.Fill);
            effect.Parameters["uBoil"]?.SetValue(rite.Boil);
            effect.Parameters["uPulse"]?.SetValue(rite.PulseWave);
            effect.Parameters["uFlash"]?.SetValue(rite.Flash);
            effect.Parameters["uRipple0"]?.SetValue(rite.GetRipple(0));
            effect.Parameters["uRipple1"]?.SetValue(rite.GetRipple(1));
            effect.Parameters["uRipple2"]?.SetValue(rite.GetRipple(2));
            effect.Parameters["uRipple3"]?.SetValue(rite.GetRipple(3));
            effect.CurrentTechnique.Passes[0].Apply();
            sb.Draw(VaultAsset.placeholder2.Value, dest, Color.White);
            EndShaderPass(sb);
        }

        /// <summary>无着色器时的血面：横向浓淡条 + 液面亮线，不落纯色矩形</summary>
        private static void DrawPoolFallback(SpriteBatch sb, BloodAltarRite rite, Rectangle dest) {
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null) {
                return;
            }

            int top = dest.Bottom - (int)(dest.Height * rite.Fill);
            for (int y = top; y < dest.Bottom; y += 2) {
                float d = (y - top) / MathF.Max(1f, dest.Bottom - top);
                float inset = 1f - 0.22f * d;
                int w = (int)(dest.Width * inset);
                Color c = Color.Lerp(ColWet, ColDry, d) * 0.9f;
                sb.Draw(pixel, new Rectangle(dest.Center.X - w / 2, y, w, 2), c);
            }
            sb.Draw(pixel, new Rectangle(dest.X + 3, top, dest.Width - 6, 1), ColWet * 0.85f);
        }

        // ============================= 血柱 =============================

        public static void DrawGeyser(SpriteBatch sb, BloodAltarRite rite, Vector2 rootPos) {
            if (rite.Rise <= 0.01f || rite.Drain >= 0.99f) {
                return;
            }

            float length = rite.ColumnLength;
            Rectangle dest = QuadRect(rootPos + new Vector2(0f, -length * 0.5f), rite.ColumnWidth, length);
            if (!BeginShaderPass(sb, "TechGeyser", out Effect effect)) {
                DrawGeyserFallback(sb, rite, rootPos, length);
                return;
            }

            effect.Parameters["uSeed"]?.SetValue(rite.Seed);
            effect.Parameters["uRise"]?.SetValue(rite.Rise);
            effect.Parameters["uDrain"]?.SetValue(rite.Drain);
            effect.Parameters["uFlash"]?.SetValue(rite.Flash);
            effect.CurrentTechnique.Passes[0].Apply();
            sb.Draw(VaultAsset.placeholder2.Value, dest, Color.White);
            EndShaderPass(sb);
        }

        /// <summary>无着色器时的血柱：沿柱堆一列越往上越窄的血团</summary>
        private static void DrawGeyserFallback(SpriteBatch sb, BloodAltarRite rite, Vector2 rootPos, float length) {
            Texture2D blob = CWRAsset.Extra_98?.Value;
            if (blob == null) {
                return;
            }

            Vector2 origin = blob.Size() * 0.5f;
            int steps = 18;
            for (int i = 0; i < steps; i++) {
                float along = i / (float)(steps - 1);
                if (along > rite.Rise || along < rite.Drain) {
                    continue;
                }
                Vector2 pos = rootPos - new Vector2(0f, along * length) - Main.screenPosition;
                float w = (1f - along * 0.55f) * rite.ColumnWidth / blob.Width * 1.4f;
                Color c = Color.Lerp(ColDeep, ColWet, MathF.Min(1f, along * 2.2f));
                sb.Draw(blob, pos, null, c * 0.9f, 0f, origin, new Vector2(w, w * 1.6f), SpriteEffects.None, 0f);
            }
        }

        // =========================== 地面血纹环 ===========================

        public static void DrawSigil(SpriteBatch sb, BloodAltarRite rite, Vector2 groundPos) {
            if (rite.Sigil <= 0.01f) {
                return;
            }

            float r = SigilRadius * rite.Sigil;
            Rectangle dest = QuadRect(groundPos, r * 2f, r * 2f * SigilFlatten);
            if (!BeginShaderPass(sb, "TechSigil", out Effect effect)) {
                return;
            }

            effect.Parameters["uSeed"]?.SetValue(rite.Seed);
            effect.Parameters["uOpen"]?.SetValue(rite.SigilOpen);
            effect.Parameters["uPulse"]?.SetValue(rite.PulseWave);
            effect.Parameters["uAspect"]?.SetValue(1f / SigilFlatten);
            effect.CurrentTechnique.Passes[0].Apply();
            sb.Draw(VaultAsset.placeholder2.Value, dest, Color.White * rite.Sigil);
            EndShaderPass(sb);
        }

        // ============================== 通用 ==============================

        /// <summary>碗口血面 quad 尺寸（像素）</summary>
        public const float PoolWidth = 46f;
        public const float PoolHeight = 22f;
        public const float SigilRadius = 104f;
        /// <summary>地面血纹的竖向压扁量，读成"贴在地上"而不是立着的环</summary>
        public const float SigilFlatten = 0.42f;

        private static Rectangle QuadRect(Vector2 worldCenter, float width, float height) {
            Vector2 screen = worldCenter - Main.screenPosition;
            return new Rectangle(
                (int)MathF.Round(screen.X - width * 0.5f),
                (int)MathF.Round(screen.Y - height * 0.5f),
                (int)MathF.Ceiling(width),
                (int)MathF.Ceiling(height));
        }
    }
}
