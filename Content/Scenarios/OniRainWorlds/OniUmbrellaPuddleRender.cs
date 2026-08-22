using CalamityOverhaul.Common;
using InnoVault.Actors;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.OniRainWorlds
{
    /// <summary>
    /// 立伞水洼的屏幕空间倒影：拷屏后在水洼椭圆内绕水面线垂直镜像
    /// （纵向压缩的伪透视——扁水洼里映出整把伞与走近的人），
    /// 涟漪扰动、染墨、入水深度衰减、水线一线青沫。<br/>
    /// 压在入雨全屏翻转（2.0）之前，演出期倒影随整屏一并被镜走，语义自洽；
    /// 着色器/RT 不可用时静默跳过，Actor 侧的精灵水洼仍在。
    /// </summary>
    internal sealed class OniUmbrellaPuddleRender : RenderHandle
    {
        public override float Weight => 1.5f;

        /// <summary>伪透视压缩率：洼内每深入 1px，镜像源向上走 13px——7px 半高映出整把伞</summary>
        private const float ReflScale = 13f;

        //墨底与湿亮，与立伞水洼精灵层同源
        private static readonly Vector3 PoolTint = new(16f / 255f, 21f / 255f, 24f / 255f);
        private static readonly Vector3 PoolSheen = new(176f / 255f, 192f / 255f, 196f / 255f);

        public override void EndCaptureDraw(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.gameMenu) {
                return;
            }

            //已获伞的玩家看不见立伞，倒影同步消隐
            if (!OniRainWorldUmbrella.ShouldShowForLocalPlayer()) {
                return;
            }

            //找屏内的立伞（带余量），正常世界至多一把
            OniRainWorldUmbrella target = null;
            Rectangle view = new((int)Main.screenPosition.X - 240, (int)Main.screenPosition.Y - 240,
                Main.screenWidth + 480, Main.screenHeight + 480);
            foreach (OniRainWorldUmbrella umbrella in ActorLoader.GetActiveActors<OniRainWorldUmbrella>()) {
                if (view.Contains(umbrella.Position.ToPoint())) {
                    target = umbrella;
                    break;
                }
            }
            if (target == null) {
                return;
            }

            //技术门禁：RT 不可用时静默跳过，精灵水洼仍在
            if (RenderQualitySafety.ScreenTargetUnavailable()) {
                return;
            }
            Effect fx = EffectLoader.OniPuddleMirror?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null || noise.IsDisposed) {
                return;
            }
            if (screenSwap == null || screenSwap.IsDisposed) {
                return;
            }
            if (Main.screenTarget == null || Main.screenTarget.IsDisposed) {
                return;
            }
            if (!RenderQualitySafety.IsScreenTargetActive(graphicsDevice)) {
                return;
            }

            RenderTargetBinding[] previousTargets = graphicsDevice.GetRenderTargets();

            //拷屏到交换缓冲
            graphicsDevice.SetRenderTarget(screenSwap);
            graphicsDevice.Clear(Color.Transparent);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            spriteBatch.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            spriteBatch.End();

            //镜像合成回写主屏
            graphicsDevice.SetRenderTarget(Main.screenTarget);
            graphicsDevice.Clear(Color.Transparent);
            SetParams(fx, target);
            graphicsDevice.Textures[1] = noise;
            graphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            fx.CurrentTechnique = fx.Techniques["TechPuddle"];
            fx.CurrentTechnique.Passes[0].Apply();
            spriteBatch.Draw(screenSwap, Vector2.Zero, Color.White);
            spriteBatch.End();

            //还原 RT 绑定
            if (previousTargets != null && previousTargets.Length > 0
                && previousTargets[0].RenderTarget != Main.screenTarget) {
                graphicsDevice.SetRenderTargets(previousTargets);
            }
        }

        /// <summary>全参数上载（uniform 是设备全局状态，每个调用点必须全参数重设）</summary>
        private static void SetParams(Effect fx, OniRainWorldUmbrella umbrella) {
            Vector2 screenSize = new(Main.screenWidth, Main.screenHeight);
            float swell = umbrella.PuddleSwell;

            //半径用两点投影差算，天然带缩放
            Vector2 centerPx = WorldToScreen(umbrella.PuddleCenter);
            Vector2 edgePx = WorldToScreen(umbrella.PuddleCenter + new Vector2(
                OniRainWorldUmbrella.PuddleHalfWidth * swell,
                OniRainWorldUmbrella.PuddleHalfHeight * swell));
            Vector2 halfUv = (edgePx - centerPx) / screenSize;
            halfUv = Vector2.Max(halfUv, new Vector2(0.0006f, 0.0006f));

            fx.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.016f);
            fx.Parameters["uPuddleCenter"]?.SetValue(centerPx / screenSize);
            fx.Parameters["uPuddleHalf"]?.SetValue(halfUv);
            fx.Parameters["uScreenTexel"]?.SetValue(new Vector2(1f / screenSize.X, 1f / screenSize.Y));
            fx.Parameters["uReflScale"]?.SetValue(ReflScale);
            //躁动/触发时水面晃得更凶
            fx.Parameters["uWobble"]?.SetValue(0.55f + (swell - 1f) * 2.4f);
            fx.Parameters["uAlpha"]?.SetValue(1f);
            fx.Parameters["uTint"]?.SetValue(PoolTint);
            fx.Parameters["uSheen"]?.SetValue(PoolSheen);
        }

        private static Vector2 WorldToScreen(Vector2 worldPos)
            => Vector2.Transform(worldPos - Main.screenPosition,
                Main.GameViewMatrix.TransformationMatrix);
    }
}
