using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Common
{
    /// <summary>中子星扭曲，NeutronWarp 替 CPU 叠绘</summary>
    internal static class NeutronWarpHelper
    {
        /// <summary>NeutronWarp 单次扭曲，替代原 33~133 层 CPU 叠绘</summary>
        /// <param name="screenWidth">屏幕宽 px</param>
        /// <param name="screenHeight">屏幕高 px</param>
        /// <param name="intensity">位移强度 0~1</param>
        /// <param name="progress">生命进度 0~1，控扩张/收缩</param>
        /// <param name="technique">Pass 名，GravitationalVortex / ShockwaveRing / RelativisticJet / GravitationalLens</param>
        /// <param name="radius">UV 归一化半径，默认 0.45</param>
        public static void DrawWarp(
            Vector2 worldCenter,
            float screenWidth,
            float screenHeight,
            float intensity,
            float progress,
            float rotation,
            string technique,
            float radius = 0.45f) {
            if (EffectLoader.NeutronWarp == null) {
                return;
            }

            Effect effect = EffectLoader.NeutronWarp.Value;
            if (effect == null) {
                return;
            }

            effect.Parameters["uTime"]?.SetValue((float)Main.GameUpdateCount * 0.05f);
            effect.Parameters["uIntensity"]?.SetValue(intensity);
            effect.Parameters["uProgress"]?.SetValue(MathHelper.Clamp(progress, 0f, 1f));
            effect.Parameters["uRadius"]?.SetValue(radius);
            effect.Parameters["uRotation"]?.SetValue(rotation);
            effect.CurrentTechnique = effect.Techniques[technique];

            Main.spriteBatch.End();

            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                effect, Main.GameViewMatrix.TransformationMatrix);

            effect.CurrentTechnique.Passes[0].Apply();

            Vector2 screenPos = worldCenter - Main.screenPosition;
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle destRect = new Rectangle(
                (int)(screenPos.X - screenWidth * 0.5f),
                (int)(screenPos.Y - screenHeight * 0.5f),
                (int)screenWidth,
                (int)screenHeight
            );

            Main.spriteBatch.Draw(pixel, destRect, new Rectangle(0, 0, 1, 1), Color.White);

            Main.spriteBatch.End();

            Main.spriteBatch.Begin(0, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
