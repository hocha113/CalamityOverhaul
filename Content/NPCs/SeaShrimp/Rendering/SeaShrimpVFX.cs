using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering
{
    /// <summary>
    /// 海虾弹幕水体共用件:深渊色板与两个着色器入口。
    /// 泡体复用 FishronBubble.fx(参数化色板喂深渊色),崩爆环走海虾专属 SeaShrimpCavitation.fx。
    /// 晶蓝(<see cref="SeaShrimpRenderer.CrystalBlue"/>)只留给水晶部位,水体一律走本色板
    /// </summary>
    internal static class SeaShrimpVFX
    {
        /// <summary>近黑深水:暗体与拖尾深端</summary>
        public static readonly Color Deep = new(10, 24, 46);
        /// <summary>中层水体</summary>
        public static readonly Color Body = new(26, 66, 118);
        /// <summary>水膜基色(FishronBubble uTint)</summary>
        public static readonly Color Film = new(58, 150, 196);
        /// <summary>深渊生物光青辉</summary>
        public static readonly Color Glow = new(86, 214, 234);
        /// <summary>泡沫苍白</summary>
        public static readonly Color Foam = new(188, 232, 246);

        /// <summary>FishronBubble 盘径契约:可见半径 = 画布半宽 × 0.42</summary>
        public const float BubbleDiskR = 0.42f;
        /// <summary>SeaShrimpCavitation TechCollapse 契约:终环半径 = 画布半宽 × 0.40</summary>
        public const float CollapseDiskR = 0.40f;

        public static bool BubblePathReady => EffectLoader.FishronBubble?.Value != null
            && CWRAsset.PerlinNoise?.Value != null && VaultAsset.placeholder2?.Value != null;

        public static bool CollapsePathReady => EffectLoader.SeaShrimpCavitation?.Value != null
            && CWRAsset.PerlinNoise?.Value != null && VaultAsset.placeholder2?.Value != null;

        /// <summary>在收集器已开的 Immediate 批里逐泡上参画 quad</summary>
        public static void DrawBubbleInBatch(SpriteBatch sb, Effect fx, Texture2D pixel, in SeaShrimpBubbleBodyParams p) {
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + p.Seed * 0.61f);
            fx.Parameters["uSeed"]?.SetValue(p.Seed * 0.173f);
            fx.Parameters["uWobble"]?.SetValue(p.Wobble);
            fx.Parameters["uArm"]?.SetValue(p.Arm);
            fx.Parameters["uBurst"]?.SetValue(p.Burst);
            fx.Parameters["uFade"]?.SetValue(p.Fade);
            fx.CurrentTechnique.Passes[0].Apply();
            float quad = p.Radius / BubbleDiskR * 2f;
            sb.Draw(pixel, p.Center - Main.screenPosition, null, Color.White, 0f,
                pixel.Size() * 0.5f, new Vector2(quad / pixel.Width, quad / pixel.Height), SpriteEffects.None, 0f);
        }

        /// <summary>
        /// 崩爆环:实体批内拆合批次画一发(合同同 AbyssrendFX.DrawCanvasTech,调用方须处于实体 Deferred 批)。
        /// <paramref name="finalRingPx"/> = 冲击环最终可见半径
        /// </summary>
        public static void DrawCollapse(Vector2 worldCenter, float finalRingPx, float progress, float seed, float fade) {
            Effect fx = EffectLoader.SeaShrimpCavitation?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (fx == null || noise == null || pixel == null) {
                return;
            }
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(seed);
            fx.Parameters["uProgress"]?.SetValue(progress);
            fx.Parameters["fadeAlpha"]?.SetValue(fade);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);

            float quad = finalRingPx / CollapseDiskR * 2f;
            Main.spriteBatch.Draw(pixel, worldCenter - Main.screenPosition, null, Color.White, 0f,
                pixel.Size() * 0.5f, new Vector2(quad / pixel.Width, quad / pixel.Height), SpriteEffects.None, 0f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>冲击环某进度下的可见半径:与 TechCollapse 内 ringR 同式,判定半径据此对齐可见波前</summary>
        public static float CollapseRingRadius(float finalRingPx, float progress) {
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float ringT = 1f - (1f - t) * (1f - t);
            return finalRingPx * MathHelper.Lerp(0.125f, 1f, ringT);
        }
    }
}
