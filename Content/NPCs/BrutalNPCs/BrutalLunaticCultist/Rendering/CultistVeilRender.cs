using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering
{
    /// <summary>仪式帷幕全屏后效：向心收束+符带+元素染色+白闪，screenTarget ping-pong</summary>
    internal class CultistVeilRender : RenderHandle
    {
        /// <summary>权重 1.09，紧邻 Prime 后效(1.08)</summary>
        public override float Weight => 1.091f;

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            CultistScreenFX.Update();

            if (!CultistScreenFX.HasAny) {
                return;
            }
            if (screenSwap == null || Main.screenTarget == null) {
                return;
            }
            Effect shader = EffectLoader.CultistVeil?.Value;
            if (shader == null) {
                return;
            }

            Vector2 centerUV = WorldToScreenUV(CultistScreenFX.VeilCenter);
            //中心离屏太远时收拢到屏心防瞎拉
            centerUV.X = MathHelper.Clamp(centerUV.X, -0.4f, 1.4f);
            centerUV.Y = MathHelper.Clamp(centerUV.Y, -0.4f, 1.4f);

            //元素染色按 blend 值内插（0火1冰2雷循环）
            float blend = CultistScreenFX.ElementBlend;
            Vector3 tint = SampleElementTint(blend);

            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["uIntensity"]?.SetValue(CultistScreenFX.VeilIntensity);
            shader.Parameters["uCenter"]?.SetValue(centerUV);
            shader.Parameters["uAspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);
            shader.Parameters["uTint"]?.SetValue(tint);
            shader.Parameters["uFlash"]?.SetValue(CultistScreenFX.CurrentFlash());
            shader.Parameters["uBreak"]?.SetValue(CultistScreenFX.BreakGrade);
            shader.Parameters["uBandRadius"]?.SetValue(PixelsToHeightNorm(560f));
            //噪声显式绑到 s1：SpriteBatch.Draw 会把 s0 覆写成拷屏贴图，
            //参数式贴图绑定实机失效（合同同 ShockRingDraw.Draw）
            gd.Textures[1] = CWRAsset.PerlinNoise.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            //拷屏再shader回写
            gd.SetRenderTarget(screenSwap);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
            sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            sb.End();

            gd.SetRenderTarget(Main.screenTarget);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Immediate, BlendState.Opaque);
            shader.CurrentTechnique.Passes[0].Apply();
            sb.Draw(screenSwap, Vector2.Zero, Color.White);
            sb.End();
        }

        /// <summary>0-3 循环元素混色</summary>
        private static Vector3 SampleElementTint(float blend) {
            Vector3 fire = CultistPalette.FireMain.ToVector3();
            Vector3 ice = CultistPalette.IceMain.ToVector3();
            Vector3 thunder = CultistPalette.ThunderMain.ToVector3();
            int a = (int)blend % 3;
            int b = (a + 1) % 3;
            float t = blend - (int)blend;
            Vector3 ca = a == 0 ? fire : a == 1 ? ice : thunder;
            Vector3 cb = b == 0 ? fire : b == 1 ? ice : thunder;
            return Vector3.Lerp(ca, cb, t);
        }

        private static Vector2 WorldToScreenUV(Vector2 worldPos) {
            float screenW = Main.screenWidth;
            float screenH = Main.screenHeight;
            Vector2 zoom = Main.GameViewMatrix.Zoom;
            if (zoom.X <= 0f) {
                zoom.X = 1f;
            }
            if (zoom.Y <= 0f) {
                zoom.Y = 1f;
            }
            Vector2 screenCenterPx = new(screenW * 0.5f, screenH * 0.5f);
            Vector2 viewWorldCenter = Main.screenPosition + screenCenterPx;
            Vector2 screenPx = screenCenterPx + (worldPos - viewWorldCenter) * zoom;
            return new Vector2(screenPx.X / screenW, screenPx.Y / screenH);
        }

        private static float PixelsToHeightNorm(float pixels) {
            float zoomY = Main.GameViewMatrix.Zoom.Y;
            if (zoomY <= 0f) {
                zoomY = 1f;
            }
            return pixels * zoomY / Main.screenHeight;
        }
    }
}
