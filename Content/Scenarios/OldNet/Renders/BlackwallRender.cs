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
            //⑥ 大潮锋面世界x → 屏幕x：与 uWallScreenX 完全同口径换算（跨文件契约）；
            //无潮时锋面 = 大负值，行为与旧早退完全一致
            Vector2 tideFrontScreen = Vector2.Transform(
                new Vector2(Backgrounds.OldNetSkyEvents.TideFrontWorldX, 0f) - Main.screenPosition,
                Main.GameViewMatrix.TransformationMatrix);
            int vpW = graphicsDevice.Viewport.Width;
            int vpH = graphicsDevice.Viewport.Height;
            //早退条件：墙缘或潮锋任一在屏附近才画
            if (edgeScreen.X < -SpillMargin && tideFrontScreen.X < -SpillMargin) {
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
                //涌动合成值：常规涌动与大潮前奏取 max
                shader.Parameters["uSurge"]?.SetValue(Backgrounds.OldNetSkyEvents.SurgeComposed);
                shader.Parameters["uTideFrontX"]?.SetValue(tideFrontScreen.X);
                shader.Parameters["uTidePhase"]?.SetValue(Backgrounds.OldNetSkyEvents.TidePhase);
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

            //⑥ 过境拍：锋面扫过玩家列的贴地椭圆冲击环（烬红板，克制的一拍）。
            //ShockRingDraw 契约要求调用方处于实体批（Deferred AlphaBlend + GameViewMatrix）
            float cross = Backgrounds.OldNetSkyEvents.TideCrossFlash;
            if (cross > 0.01f) {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                    null, Main.GameViewMatrix.TransformationMatrix);
                float grow = 1f - cross;
                ShockRingDraw.Draw(spriteBatch, Backgrounds.OldNetSkyEvents.TideCrossPos,
                    30f + grow * 150f, 10f,
                    new Color(255, 120, 70), new Color(205, 62, 34), new Color(120, 25, 18),
                    cross * 0.85f, squish: 0.4f, innerGlow: 0.15f, timeSeed: 3.7f);
                spriteBatch.End();
            }
        }
    }
}
