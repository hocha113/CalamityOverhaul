using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Renders
{
    /// <summary>
    /// 黑墙全屏绘制：旧网西侧边界的红黑代码之墙。
    /// 墙体物块只是实心黑砖，视觉全部由 Blackwall.fx 在实体层之后接管；
    /// shader 缺失时回退为纯色叠片，保证墙的存在感不消失
    /// </summary>
    internal class BlackwallRender : RenderHandle
    {
        public override float Weight => 1.5f;

        //红晕外溢的屏幕余量：墙缘离屏这么远之后才停止绘制
        private const float SpillMargin = 220f;

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main,
            GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (!OldNetWorld.Active || Main.dedServ || Main.gameMenu) {
                return;
            }
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return;
            }

            //墙右缘世界x → 屏幕x（含缩放矩阵）
            float wallWorldX = OldNetMetrics.WallCols * 16f;
            Vector2 edgeScreen = Vector2.Transform(
                new Vector2(wallWorldX, 0f) - Main.screenPosition,
                Main.GameViewMatrix.TransformationMatrix);
            int vpW = graphicsDevice.Viewport.Width;
            int vpH = graphicsDevice.Viewport.Height;
            if (edgeScreen.X < -SpillMargin) {
                return;
            }

            Effect shader = EffectLoader.Blackwall?.Value;
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            if (shader != null) {
                shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects / 60f);
                shader.Parameters["uIntensity"]?.SetValue(1f);
                shader.Parameters["uScreenSize"]?.SetValue(new Vector2(vpW, vpH));
                shader.Parameters["uWallScreenX"]?.SetValue(edgeScreen.X);
                shader.Parameters["uSurge"]?.SetValue(Backgrounds.OldNetSkyEvents.Surge);
                shader.CurrentTechnique.Passes[0].Apply();
                spriteBatch.Draw(px, new Rectangle(0, 0, vpW, vpH), Color.White);
            }
            else {
                //CPU 回退：暗红纯色墙体 + 边缘亮线，不至于开着一堵隐形墙
                int bodyRight = (int)MathHelper.Clamp(edgeScreen.X, 0f, vpW);
                if (bodyRight > 0) {
                    spriteBatch.Draw(px, new Rectangle(0, 0, bodyRight, vpH), new Color(46, 4, 8));
                }
                if (edgeScreen.X > -4f && edgeScreen.X < vpW + 4f) {
                    spriteBatch.Draw(px, new Rectangle((int)edgeScreen.X - 2, 0, 4, vpH), new Color(220, 45, 30));
                }
            }

            spriteBatch.End();
        }
    }
}
