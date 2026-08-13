using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Common
{
    /// <summary>
    /// 共享参数化冲击环（ShockRing.fx）绘制入口，替代 Ring01 灰度图的环形消费点；
    /// 合同同 CultistRenderHelper.DrawSigil：调用方须处于实体绘制批（Deferred AlphaBlend），
    /// 内部切 Immediate+Additive 画 quad 后还原；着色器缺失时走 DiffusionCircle 精灵回退
    /// </summary>
    internal static class ShockRingDraw
    {
        /// <param name="sb">当前处于实体批的 SpriteBatch</param>
        /// <param name="worldPos">环心世界坐标</param>
        /// <param name="radiusPx">环半径（世界px）</param>
        /// <param name="thicknessPx">环带基准厚度（世界px）</param>
        /// <param name="bright">波前亮缘色</param>
        /// <param name="main">环带主体色</param>
        /// <param name="deep">内侧尾波/残波色</param>
        /// <param name="alpha">整体透明度 0~1</param>
        /// <param name="tearPx">撕裂位移幅度（世界px），≤0 时取厚度的 0.9 倍——环缘不许是干净数学圆</param>
        /// <param name="squish">Y 透视压缩，1=正圆，贴地环常用 0.4</param>
        /// <param name="innerGlow">环内残波 0~1，绽放类给小值、预警类给 0</param>
        /// <param name="timeSeed">时间种子，错开多实例噪声相位</param>
        public static void Draw(SpriteBatch sb, Vector2 worldPos, float radiusPx, float thicknessPx,
            Color bright, Color main, Color deep, float alpha,
            float tearPx = -1f, float squish = 1f, float innerGlow = 0f, float timeSeed = 0f) {
            if (alpha <= 0.01f || radiusPx < 2f) {
                return;
            }

            thicknessPx = MathHelper.Max(thicknessPx, 2f);
            if (tearPx < 0f) {
                tearPx = thicknessPx * 0.9f;
            }
            squish = MathHelper.Clamp(squish, 0.05f, 1f);

            Effect effect = EffectLoader.ShockRing?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || canvas == null || noise == null) {
                DrawFallback(sb, worldPos, radiusPx, bright, main, alpha, squish);
                return;
            }

            //quad 半径带撕裂/厚度余量，护栏在 0.86 归零，这里按 0.82 折算防切边
            float halfPx = (radiusPx + thicknessPx * 2.4f + tearPx) / 0.82f;
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + timeSeed);
            effect.Parameters["uRadius"]?.SetValue(radiusPx / halfPx);
            effect.Parameters["uThickness"]?.SetValue(thicknessPx / halfPx);
            effect.Parameters["uTear"]?.SetValue(tearPx / halfPx);
            effect.Parameters["uSquish"]?.SetValue(squish);
            effect.Parameters["uAlpha"]?.SetValue(MathHelper.Clamp(alpha, 0f, 1f));
            effect.Parameters["uInnerGlow"]?.SetValue(MathHelper.Clamp(innerGlow, 0f, 1f));
            effect.Parameters["uColBright"]?.SetValue(bright.ToVector3());
            effect.Parameters["uColMain"]?.SetValue(main.ToVector3());
            effect.Parameters["uColDeep"]?.SetValue(deep.ToVector3());

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            effect.CurrentTechnique.Passes[0].Apply();

            //方形 quad，椭圆压缩在 shader 内完成
            float quadSize = halfPx * 2f;
            sb.Draw(canvas, worldPos - Main.screenPosition, null, Color.White, 0f, canvas.Size() * 0.5f,
                quadSize / canvas.Width, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>精灵回退：有机热斑环+薄锐缘（DiffusionCircle5/4，皆黑底加色安全）</summary>
        private static void DrawFallback(SpriteBatch sb, Vector2 worldPos, float radiusPx,
            Color bright, Color main, float alpha, float squish) {
            Texture2D body = CWRUtils.GetT2DAsset(CWRConstant.Masking + "DiffusionCircle5")?.Value;
            Texture2D rim = CWRUtils.GetT2DAsset(CWRConstant.Masking + "DiffusionCircle4")?.Value;
            if (body == null || rim == null) {
                return;
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Vector2 drawPos = worldPos - Main.screenPosition;
            //加色批源因子是 SourceAlpha：强制 A=255，防调用方传 A=0 色导致回退层整层消失
            Color mainA = main with { A = 255 };
            Color brightA = bright with { A = 255 };
            //DiffusionCircle5 环带在 0.39R、DiffusionCircle4 在 0.95R，按可见半径折算缩放
            float bodyScale = radiusPx / (body.Width * 0.5f * 0.39f);
            float rimScale = radiusPx / (rim.Width * 0.5f * 0.95f);
            //SpriteBatch 先缩放后旋转：squish<1 的贴地椭圆一旋转就整个掀斜，只有正圆才许用随机朝向
            float bodyRot = squish >= 0.99f ? worldPos.X * 0.013f : 0f;
            sb.Draw(body, drawPos, null, mainA * (alpha * 0.85f), bodyRot,
                body.Size() * 0.5f, new Vector2(bodyScale, bodyScale * squish), SpriteEffects.None, 0f);
            sb.Draw(rim, drawPos, null, brightA * (alpha * 0.6f), 0f,
                rim.Size() * 0.5f, new Vector2(rimScale * 1.04f, rimScale * 1.04f * squish), SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
