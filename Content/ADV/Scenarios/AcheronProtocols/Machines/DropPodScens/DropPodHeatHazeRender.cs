using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.Machines.DropPodScens
{
    /// <summary>空降仓热浪 RenderHandle：EndCapture <see cref="EffectLoader.DropPodHeatHaze"/> 全屏 UV 偏移</summary>
    internal class DropPodHeatHazeRender : RenderHandle
    {
        /// <summary>屏幕归一化坐标 0~1</summary>
        private static Vector2 hazeCenter;

        /// <summary>扭曲强度 0~1</summary>
        private static float hazeIntensity;

        public override float Weight => 1.05f;

        /// <summary><see cref="DropPodActor"/> 每帧同步热浪数据</summary>
        internal static void SyncHazeData(Vector2 screenCenter, float intensity) {
            hazeCenter = screenCenter;
            hazeIntensity = intensity;
        }

        internal static void Reset() {
            hazeCenter = Vector2.Zero;
            hazeIntensity = 0f;
        }

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            if (!DropPodWorld.Active || hazeIntensity <= 0.01f)
                return;
            if (EffectLoader.DropPodHeatHaze == null || !EffectLoader.DropPodHeatHaze.IsLoaded)
                return;
            if (screenSwap == null || Main.screenTarget == null)
                return;

            Effect shader = EffectLoader.DropPodHeatHaze.Value;

            //screenTarget → screenSwap
            gd.SetRenderTarget(screenSwap);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            sb.End();

            //着色器参数
            shader.Parameters["screenSize"]?.SetValue(new Vector2(Main.screenWidth, Main.screenHeight));
            shader.Parameters["hazeCenter"]?.SetValue(hazeCenter);
            shader.Parameters["hazeIntensity"]?.SetValue(hazeIntensity);
            shader.Parameters["globalTime"]?.SetValue((float)Main.timeForVisualEffects * 0.02f);
            shader.Parameters["uNoise"]?.SetValue(CWRAsset.Extra_193.Value);

            //screenSwap → screenTarget
            gd.SetRenderTarget(Main.screenTarget);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            shader.CurrentTechnique.Passes[0].Apply();
            sb.Draw(screenSwap, Vector2.Zero, Color.White);
            sb.End();
        }
    }
}
