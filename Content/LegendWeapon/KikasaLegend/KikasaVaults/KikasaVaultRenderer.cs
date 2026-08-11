using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults
{
    /// <summary>
    /// 湖窗过程化绘制：面板走 KikasaVaultPanel.fx（缺编时 CPU 平底回退），
    /// 沉物用 KikasaItemForm 血水材质，亮件走加色层。
    /// </summary>
    internal static class KikasaVaultRenderer
    {
        private static Texture2D Pixel => VaultAsset.placeholder2.Value;

        /// <summary>
        /// 面板底：撕开的湿纸口子里看血湖。
        /// open 驱动撕开孔径，waterY 是当前水位（开窗时湖水在窗里涨起），
        /// hoverX01 为悬停列在面板内的 uv.x（无悬停给 -1），hoverGlow 是列血光强度
        /// </summary>
        public static void DrawPanel(SpriteBatch sb, Rectangle rect, float alpha, float stir,
            float open, float waterY, float hoverX01, float hoverGlow) {
            if (rect.Width < 4 || rect.Height < 4 || alpha < 0.01f || open <= 0.002f) {
                return;
            }
            Effect effect = EffectLoader.KikasaVaultPanel?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                DrawPanelCPU(sb, rect, alpha, open, waterY);
                return;
            }
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(rect.Width, rect.Height));
            effect.Parameters["uWaterY"]?.SetValue(waterY);
            effect.Parameters["uSlitY"]?.SetValue(KikasaVaultTheme.WaterLineY);
            effect.Parameters["uOpen"]?.SetValue(MathHelper.Clamp(open, 0f, 1f));
            effect.Parameters["uStir"]?.SetValue(MathHelper.Clamp(stir, 0f, 1f));
            effect.Parameters["uHoverX"]?.SetValue(hoverX01);
            effect.Parameters["uHoverGlow"]?.SetValue(MathHelper.Clamp(hoverGlow, 0f, 1f));

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.UIScaleMatrix);
            Main.instance.GraphicsDevice.Textures[1] = noise;
            Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            sb.Draw(Pixel, rect, Color.White);
            RestoreUIBatch(sb);
        }

        //CPU 回退：按孔径裁出的平底 + 动态水线一划，不做同心放大的假羽化

        private static void DrawPanelCPU(SpriteBatch sb, Rectangle rect, float alpha,
            float open, float waterY) {
            float openE = 1f - MathF.Pow(1f - MathHelper.Clamp(open, 0f, 1f), 3f);
            int slitY = rect.Y + (int)(rect.Height * KikasaVaultTheme.WaterLineY);
            int top = (int)MathHelper.Lerp(slitY, rect.Y, MathHelper.Clamp(openE * 1.15f, 0f, 1f));
            int bottom = (int)MathHelper.Lerp(slitY, rect.Bottom,
                MathHelper.Clamp((openE - 0.12f) / 0.88f, 0f, 1f));
            Rectangle vis = new(rect.X, top, rect.Width, Math.Max(2, bottom - top));

            Rectangle shadow = vis;
            shadow.Offset(3, 4);
            sb.Draw(Pixel, shadow, Color.Black * (0.45f * alpha));
            sb.Draw(Pixel, vis, KikasaVaultTheme.PanelBg * (0.94f * alpha));
            int waterPix = rect.Y + (int)(rect.Height * MathHelper.Clamp(waterY, 0f, 1f));
            if (waterPix < vis.Bottom - 2) {
                int wy = Math.Max(waterPix, vis.Y);
                Rectangle water = new(vis.X, wy, vis.Width, vis.Bottom - wy);
                sb.Draw(Pixel, water, KikasaVaultTheme.Deep * (0.5f * alpha));
                DrawLine(sb, new Vector2(vis.Left + 4, wy), new Vector2(vis.Right - 4, wy),
                    1.6f, KikasaVaultTheme.Foam * (0.5f * alpha));
            }
            Color edge = KikasaVaultTheme.Blood * (0.4f * alpha);
            DrawLine(sb, new Vector2(vis.Left, vis.Top), new Vector2(vis.Right, vis.Top), 1.2f, edge);
            DrawLine(sb, new Vector2(vis.Left, vis.Bottom), new Vector2(vis.Right, vis.Bottom), 1.2f, edge * 0.7f);
            DrawLine(sb, new Vector2(vis.Left, vis.Top), new Vector2(vis.Left, vis.Bottom), 1.2f, edge * 0.85f);
            DrawLine(sb, new Vector2(vis.Right, vis.Top), new Vector2(vis.Right, vis.Bottom), 1.2f, edge * 0.85f);
        }

        //==================== 沉物绘制 ====================

        /// <summary>进入血水物品绘制段：Immediate + 噪声挂载；随后逐件 DrawFormItem</summary>
        public static bool BeginItemBatch(SpriteBatch sb, out Effect formEffect) {
            formEffect = EffectLoader.KikasaItemForm?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            bool shaderOk = formEffect != null && noise != null;
            sb.End();
            //非整数缩小适配槽位，Point 采样会闪锯齿，走 Linear
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
            if (shaderOk) {
                Main.instance.GraphicsDevice.Textures[1] = noise;
                Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            }
            return shaderOk;
        }

        public static void EndItemBatch(SpriteBatch sb) => RestoreUIBatch(sb);

        /// <summary>血水态物品：form 1=全血水 0=真身，UI 用斑驳交融模式</summary>
        public static void DrawFormItem(SpriteBatch sb, Effect formEffect, bool shaderOk,
            int itemType, Vector2 center, float form, float seed, float alpha) {

            Main.instance.LoadItem(itemType);
            Texture2D tex = TextureAssets.Item[itemType]?.Value;
            if (tex == null) {
                return;
            }
            Rectangle frame = Main.itemAnimations[itemType]?.GetFrame(tex) ?? tex.Frame();
            float fit = KikasaVaultTheme.SlotFit;
            float scale = MathF.Min(1f, fit / MathF.Max(frame.Width, frame.Height));
            Vector2 origin = frame.Size() * 0.5f;

            Color color;
            if (shaderOk) {
                formEffect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                formEffect.Parameters["uSeed"]?.SetValue(seed);
                formEffect.Parameters["uForm"]?.SetValue(form);
                formEffect.Parameters["uDissolve"]?.SetValue(0f);
                formEffect.Parameters["uScanMode"]?.SetValue(0f);
                formEffect.Parameters["uUvRect"]?.SetValue(new Vector4(
                    frame.X / (float)tex.Width, frame.Y / (float)tex.Height,
                    frame.Width / (float)tex.Width, frame.Height / (float)tex.Height));
                formEffect.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
                formEffect.Parameters["uAspect"]?.SetValue(frame.Width / (float)frame.Height);
                formEffect.CurrentTechnique.Passes[0].Apply();
                color = new Color(255, 255, 255, (byte)(alpha * 255f));
            }
            else {
                color = Color.Lerp(Color.White, KikasaVaultTheme.Blood, form) * alpha;
            }

            sb.Draw(tex, center, frame, color, 0f, origin, scale, SpriteEffects.None, 0f);
        }

        //==================== 加色小件 ====================

        /// <summary>压扁的扩散环（悬停浮圈/提取旋涡）</summary>
        public static void DrawRing(SpriteBatch sb, Vector2 center, float rx, float ry, Color color) {
            Texture2D ring = CWRAsset.DiffusionCircle?.Value;
            if (ring == null) {
                return;
            }
            Vector2 origin = ring.Size() * 0.5f;
            Vector2 scale = new(rx * 2f / ring.Width, ry * 2f / ring.Height);
            sb.Draw(ring, center, null, color, 0f, origin, scale, SpriteEffects.None, 0f);
        }

        /// <summary>软光点</summary>
        public static void DrawGlowDot(SpriteBatch sb, Vector2 center, float radius, Color color) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            Vector2 origin = glow.Size() * 0.5f;
            float s = radius * 2f / glow.Width;
            sb.Draw(glow, center, null, color, 0f, origin, s, SpriteEffects.None, 0f);
        }

        public static void DrawLine(SpriteBatch sb, Vector2 a, Vector2 b, float width, Color color) {
            Vector2 d = b - a;
            float len = d.Length();
            if (len < 0.5f) {
                return;
            }
            float rot = MathF.Atan2(d.Y, d.X);
            sb.Draw(Pixel, a, null, color, rot, new Vector2(0f, 0.5f),
                new Vector2(len / Pixel.Width, width / Pixel.Height), SpriteEffects.None, 0f);
        }

        /// <summary>恢复 UI 默认批次（Deferred + UIScaleMatrix）</summary>
        public static void RestoreUIBatch(SpriteBatch sb) {
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }

        /// <summary>切到加色批次画亮件，用完 RestoreUIBatch</summary>
        public static void BeginAdditive(SpriteBatch sb) {
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }
    }
}
