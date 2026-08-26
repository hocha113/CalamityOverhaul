using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Ashreign
{
    /// <summary>
    /// 烬雪热浪：岩浆池上方的空气扭曲微光。
    /// 零新 shader，复用 <see cref="EffectLoader.ThermalHeatHaze"/>（多热源屏幕 UV 偏移），
    /// 热源来自 <see cref="AshreignAmbience"/> 的岩浆液面聚簇缓存，强度随在场包络。
    /// EndCapture 拷屏两趟，坐标折算镜像 ThermalHeatHazeRender（C#↔fx MAX_SOURCES=8 对齐）
    /// </summary>
    internal sealed class AshreignHeatHazeRender : RenderHandle
    {
        /// <summary>槽位分配权重 1.80</summary>
        public override float Weight => 1.80f;

        /// <summary>与 ThermalHeatHaze.fx MAX_SOURCES 对齐</summary>
        private const int MaxSources = 8;
        private static readonly Vector4[] sources = new Vector4[MaxSources];

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            float presence = AshreignAmbience.Presence;
            if (presence < 0.04f || AshreignAmbience.HeatSourceCount <= 0) {
                return;
            }
            if (screenSwap == null || Main.screenTarget == null) {
                return;
            }
            if (EffectLoader.ThermalHeatHaze == null || !EffectLoader.ThermalHeatHaze.IsLoaded) {
                return;
            }

            //世界热源 → 归一化屏幕坐标（Zoom 以屏心为锚，投影需换算）
            float screenW = Main.screenWidth;
            float screenH = Main.screenHeight;
            Vector2 zoom = Main.GameViewMatrix.Zoom;
            if (zoom.X <= 0f) zoom.X = 1f;
            if (zoom.Y <= 0f) zoom.Y = 1f;
            Vector2 screenCenterPx = new(screenW * 0.5f, screenH * 0.5f);
            Vector2 viewWorldCenter = Main.screenPosition + screenCenterPx;

            int count = 0;
            for (int i = 0; i < AshreignAmbience.HeatSourceCount && count < MaxSources; i++) {
                Vector4 src = AshreignAmbience.HeatSources[i];
                Vector2 worldPos = new(src.X, src.Y - 24f);//液面略上偏，热气在池上方
                Vector2 screenPx = screenCenterPx + (worldPos - viewWorldCenter) * zoom;
                float intensity = src.Z * presence * 0.62f;
                if (intensity <= 0.03f) {
                    continue;
                }
                float radiusNorm = src.W * 1.35f * zoom.Y / screenH;
                sources[count++] = new Vector4(screenPx.X / screenW, screenPx.Y / screenH,
                    intensity, radiusNorm);
            }
            if (count == 0) {
                return;
            }

            Effect shader = EffectLoader.ThermalHeatHaze.Value;

            //屏→交换 RT
            gd.SetRenderTarget(screenSwap);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            sb.End();

            shader.Parameters["screenSize"]?.SetValue(new Vector2(screenW, screenH));
            shader.Parameters["sources"]?.SetValue(sources);
            shader.Parameters["sourceCount"]?.SetValue(count);
            shader.Parameters["globalTime"]?.SetValue((float)Main.timeForVisualEffects * 0.018f);
            shader.Parameters["uNoise"]?.SetValue(CWRAsset.Extra_193.Value);

            //扭曲后写回主屏
            gd.SetRenderTarget(Main.screenTarget);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            shader.CurrentTechnique.Passes[0].Apply();
            sb.Draw(screenSwap, Vector2.Zero, Color.White);
            sb.End();
        }
    }
}
