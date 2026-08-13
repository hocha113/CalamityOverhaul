using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Core;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Rendering
{
    /// <summary>
    /// 世纪之花全屏后效：绽放花环(着色器)+冲击闪光+丛林暮色罩。
    /// 客户端由状态推送数据，此处只消费，不新增网络包
    /// </summary>
    internal class PlanteraBloomRender : RenderHandle
    {
        /// <summary>权重 1.085，在 Prime(1.08) 与扭曲(1.2) 之间</summary>
        public override float Weight => 1.085f;

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            PlanteraScreenFX.Update();

            if (!PlanteraScreenFX.HasAny) {
                return;
            }
            if (Main.screenTarget == null) {
                return;
            }

            gd.SetRenderTarget(Main.screenTarget);

            //暮色罩：整幕压向湿绿的暗
            if (PlanteraScreenFX.DuskIntensity > 0.02f) {
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
                Texture2D quad = VaultAsset.placeholder2.Value;
                Color dusk = new Color(8, 18, 6) * (0.34f * PlanteraScreenFX.DuskIntensity);
                sb.Draw(quad, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), dusk);
                sb.End();
            }

            //绽放花环
            if (PlanteraScreenFX.AnyRingActive) {
                DrawRings(sb);
            }

            //冲击闪光
            if (PlanteraScreenFX.FlashActive) {
                DrawFlash(sb);
            }
        }

        private static void DrawRings(SpriteBatch sb) {
            Effect shader = EffectLoader.PlanteraBloom?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            Texture2D quad = VaultAsset.placeholder2.Value;

            for (int i = 0; i < PlanteraScreenFX.MaxRings; i++) {
                ref readonly var ring = ref PlanteraScreenFX.Rings[i];
                if (!ring.Active) {
                    continue;
                }

                float t = ring.Age / (float)ring.Life;
                Vector2 screenPos = WorldToScreenPx(ring.WorldCenter);
                float radiusPx = ring.MaxRadiusPx * ZoomY();
                //quad 半径按 uProgress 归一(环画在 0.82 半径处)
                float quadSize = radiusPx * 2f / 0.82f;

                if (shader == null || noise == null) {
                    //回退：软光圈扩散
                    sb.Begin(SpriteSortMode.Deferred, BlendState.Additive);
                    Texture2D glow = CWRAsset.SoftGlow.Value;
                    Color col = (ring.Phase2 ? PlanteraRenderHelper.GlowMagenta : PlanteraRenderHelper.GlowGreen)
                        with { A = 0 } * ((1f - t) * 0.5f);
                    sb.Draw(glow, screenPos, null, col, 0f, glow.Size() / 2f,
                        quadSize * VaultUtils.EaseOutCubic(t) / glow.Width, SpriteEffects.None, 0f);
                    sb.End();
                    continue;
                }

                sb.Begin(SpriteSortMode.Immediate, BlendState.Additive);
                shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                shader.Parameters["uProgress"]?.SetValue(VaultUtils.EaseOutCubic(t));
                shader.Parameters["uIntensity"]?.SetValue(1f - t * t);
                shader.Parameters["uPhase2"]?.SetValue(ring.Phase2 ? 1f : 0f);
                shader.Parameters["uGapOn"]?.SetValue(0f);
                shader.Parameters["uGap1"]?.SetValue(0f);
                shader.Parameters["uGap2"]?.SetValue(0f);
                shader.Parameters["uGapCos"]?.SetValue(1f);
                shader.Parameters["seed"]?.SetValue(i * 0.37f);
                //噪声显式绑到 s1：SpriteBatch.Draw 会把 s0 覆写成画布贴图，
                //参数式贴图绑定实机失效（合同同 ShockRingDraw.Draw）
                GraphicsDevice gd = Main.instance.GraphicsDevice;
                gd.Textures[1] = noise;
                gd.SamplerStates[1] = SamplerState.LinearWrap;
                shader.CurrentTechnique.Passes[0].Apply();

                sb.Draw(quad, screenPos, null, Color.White, 0f, quad.Size() / 2f,
                    quadSize / quad.Width, SpriteEffects.None, 0f);
                sb.End();
            }
        }

        private static void DrawFlash(SpriteBatch sb) {
            float t = PlanteraScreenFX.FlashAge / (float)PlanteraScreenFX.FlashLife;
            float strength = PlanteraScreenFX.FlashIntensity * (1f - t) * (1f - t);
            if (strength <= 0.01f) {
                return;
            }

            Vector2 center = WorldToScreenPx(PlanteraScreenFX.FlashWorldCenter);
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D quad = VaultAsset.placeholder2.Value;
            Color flashGreen = new Color(190, 255, 160, 0);

            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive);
            //全幕轻罩
            sb.Draw(quad, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                flashGreen * (strength * 0.30f));
            //爆心辐射光团
            float coreScale = Main.screenWidth * 1.15f / glow.Width;
            sb.Draw(glow, center, null, flashGreen * (strength * 0.85f),
                0f, glow.Size() / 2f, coreScale * (0.55f + t * 0.5f), SpriteEffects.None, 0f);
            sb.Draw(glow, center, null, Color.White with { A = 0 } * (strength * 0.55f),
                0f, glow.Size() / 2f, coreScale * 0.3f, SpriteEffects.None, 0f);
            sb.End();
        }

        /// <summary>世界→屏幕像素(含缩放)</summary>
        private static Vector2 WorldToScreenPx(Vector2 worldPos) {
            Vector2 zoom = Main.GameViewMatrix.Zoom;
            if (zoom.X <= 0f) {
                zoom.X = 1f;
            }
            if (zoom.Y <= 0f) {
                zoom.Y = 1f;
            }
            Vector2 screenCenter = new(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f);
            Vector2 viewWorldCenter = Main.screenPosition + screenCenter;
            return screenCenter + (worldPos - viewWorldCenter) * zoom;
        }

        private static float ZoomY() {
            float zoomY = Main.GameViewMatrix.Zoom.Y;
            return zoomY <= 0f ? 1f : zoomY;
        }
    }
}
