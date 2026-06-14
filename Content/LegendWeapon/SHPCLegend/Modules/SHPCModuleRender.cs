using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules
{
    /// <summary>改件图标滤镜，CyberpunkItemFilter 双调+描边</summary>
    internal static class SHPCModuleRender
    {
        //缓存上一个绘制循环传入的变换矩阵，便于End阶段恢复
        private static Matrix _lastTransform;
        private static SamplerState _lastSampler;
        private static bool _active;

        /// <summary>启用滤镜，后续 Draw 走着色器</summary>
        /// <param name="sb">活动的SpriteBatch</param>
        /// <param name="tint">识别色，双色调高光/描边</param>
        /// <param name="texSize">物品贴图（或当前帧）像素尺寸</param>
        /// <param name="transform">当前spritebatch采用的变换矩阵，传<see cref="Main.UIScaleMatrix"/>即可</param>
        /// <param name="intensity">滤镜强度，0关闭，1完整</param>
        public static bool Begin(SpriteBatch sb, Color tint, Vector2 texSize, Matrix transform, float intensity = 1f) {
            Effect effect = EffectLoader.CyberpunkItemFilter?.Value;
            if (effect == null) {
                return false;
            }

            float time = (float)Main.GameUpdateCount / 60f;
            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uTint"]?.SetValue(tint.ToVector3());
            effect.Parameters["uTexSize"]?.SetValue(texSize);
            effect.Parameters["uIntensity"]?.SetValue(intensity);

            _lastTransform = transform;
            _lastSampler = SamplerState.PointClamp;
            _active = true;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                _lastSampler, DepthStencilState.None,
                RasterizerState.CullNone, effect, transform);
            return true;
        }

        /// <summary>关闭滤镜恢复默认 Begin</summary>
        public static void End(SpriteBatch sb) {
            if (!_active) {
                return;
            }
            _active = false;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None,
                RasterizerState.CullCounterClockwise, null, _lastTransform);
        }

        /// <summary>一次性绘滤镜图标，内部 Begin/End</summary>
        public static void DrawIcon(SpriteBatch sb, Item item, Vector2 center,
            float maxSize, Color tint, float alpha, Matrix transform, float intensity = 1f) {
            if (item == null || item.IsAir) {
                return;
            }
            Main.instance.LoadItem(item.type);
            Texture2D tex = TextureAssets.Item[item.type]?.Value;
            if (tex == null) {
                return;
            }
            Rectangle frame = Main.itemAnimations[item.type] != null
                ? Main.itemAnimations[item.type].GetFrame(tex)
                : tex.Bounds;
            float scale = MathF.Min(maxSize / frame.Width, maxSize / frame.Height);
            if (scale > 1f) {
                scale = 1f;
            }

            Vector2 origin = new(frame.Width * 0.5f, frame.Height * 0.5f);
            //整 tex 尺寸做像素步进
            Vector2 texSize = new(tex.Width, tex.Height);

            if (Begin(sb, tint, texSize, transform, intensity)) {
                sb.Draw(tex, center, frame, Color.White * alpha, 0f, origin, scale, SpriteEffects.None, 0f);
                End(sb);
            }
            else {
                //着色器未加载时降级为直接绘制
                sb.Draw(tex, center, frame, Color.White * alpha, 0f, origin, scale, SpriteEffects.None, 0f);
            }
        }
    }
}
