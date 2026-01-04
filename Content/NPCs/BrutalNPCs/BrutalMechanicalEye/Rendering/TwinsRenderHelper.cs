using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Rendering
{
    /// <summary>
    /// 双子魔眼绘制辅助类
    /// 负责绘制各种视觉特效
    /// </summary>
    internal static class TwinsRenderHelper
    {
        #region 蓄力特效

        /// <summary>
        /// 绘制蓄力特效
        /// </summary>
        public static void DrawChargeEffect(SpriteBatch spriteBatch, TwinsStateContext context) {
            if (!context.IsCharging || context.ChargeProgress <= 0) {
                return;
            }

            Vector2 drawPos = context.Npc.Center - Main.screenPosition;
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            Texture2D circleTex = CWRAsset.DiffusionCircle.Value;

            //根据蓄力类型选择颜色
            Color chargeColor = GetChargeColor(context.ChargeType);

            //开启叠加混合模式
            spriteBatch.End();
            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.Additive,
                SamplerState.AnisotropicClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                Main.GameViewMatrix.TransformationMatrix
            );

            //绘制内圈光晕
            DrawGlowEffect(glowTex, drawPos, chargeColor, context.ChargeProgress);

            //绘制扩散圆环
            DrawCircleEffect(circleTex, drawPos, chargeColor, context.ChargeProgress);

            //如果是扇形激光蓄力，绘制攻击方向预警线
            if (context.ChargeType == 2 && context.Target != null && context.ChargeProgress > 0.3f) {
                DrawAimLine(context, chargeColor);
            }

            //恢复正常混合模式
            spriteBatch.End();
            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                Main.DefaultSamplerState,
                DepthStencilState.None,
                Main.Rasterizer,
                null,
                Main.GameViewMatrix.TransformationMatrix
            );
        }

        private static Color GetChargeColor(int chargeType) {
            return chargeType switch {
                1 => Color.OrangeRed,
                2 => Color.BlueViolet,
                3 => Color.Orange,
                4 => Color.IndianRed,
                _ => Color.White
            };
        }

        private static void DrawGlowEffect(Texture2D glowTex, Vector2 drawPos, Color color, float progress) {
            float glowScale = 1.5f + progress * 1.5f;
            float glowAlpha = progress * 0.6f;
            Main.EntitySpriteDraw(
                glowTex,
                drawPos,
                null,
                color * glowAlpha,
                0f,
                glowTex.Size() / 2f,
                glowScale,
                SpriteEffects.None,
                0
            );
        }

        private static void DrawCircleEffect(Texture2D circleTex, Vector2 drawPos, Color color, float progress) {
            float circleScale = 0.5f + progress * 2f;
            float circleAlpha = (1f - progress) * 0.8f;
            float circleRotation = Main.GlobalTimeWrappedHourly * 2f;

            //第一个圆环
            Main.EntitySpriteDraw(
                circleTex,
                drawPos,
                null,
                color * circleAlpha,
                circleRotation,
                circleTex.Size() / 2f,
                circleScale,
                SpriteEffects.None,
                0
            );

            //第二个反方向旋转的圆环
            Main.EntitySpriteDraw(
                circleTex,
                drawPos,
                null,
                color * circleAlpha * 0.5f,
                -circleRotation * 0.7f,
                circleTex.Size() / 2f,
                circleScale * 0.8f,
                SpriteEffects.None,
                0
            );
        }

        private static void DrawAimLine(TwinsStateContext context, Color baseColor) {
            if (context.Target == null) {
                return;
            }

            Vector2 direction = (context.Target.Center - context.Npc.Center).SafeNormalize(Vector2.Zero);
            float spreadAngle = MathHelper.ToRadians(50);
            Texture2D lineTex = CWRAsset.LightShot.Value;

            //绘制扇形边界线
            for (int side = -1; side <= 1; side += 2) {
                Vector2 lineDir = direction.RotatedBy(spreadAngle / 2 * side);
                float lineLength = 400f * context.ChargeProgress;
                Vector2 lineEnd = context.Npc.Center + lineDir * lineLength;

                //绘制虚线效果
                int segments = (int)(lineLength / 20f);
                for (int i = 0; i < segments; i++) {
                    float t = i / (float)segments;
                    Vector2 segPos = Vector2.Lerp(context.Npc.Center, lineEnd, t) - Main.screenPosition;
                    float alpha = (1f - t) * context.ChargeProgress * 0.8f;
                    float scale = 0.3f + (1f - t) * 0.4f;

                    Main.EntitySpriteDraw(
                        lineTex,
                        segPos,
                        null,
                        baseColor * alpha,
                        lineDir.ToRotation(),
                        new Vector2(0, lineTex.Height / 2f),
                        new Vector2(scale, scale * 0.5f),
                        SpriteEffects.None,
                        0
                    );
                }
            }
        }

        #endregion

        #region 本体绘制

        /// <summary>
        /// 绘制NPC本体和拖尾
        /// </summary>
        public static void DrawNpcBody(
            SpriteBatch spriteBatch,
            NPC npc,
            Texture2D texture,
            int frameIndex,
            float rotation
        ) {
            Rectangle frame = texture.Frame(1, 4, 0, frameIndex);
            Vector2 origin = frame.Size() / 2f;
            SpriteEffects effects = npc.spriteDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            float drawRotation = rotation + MathHelper.PiOver2;

            //绘制拖尾残影
            for (int i = 0; i < npc.oldPos.Length; i++) {
                float trailOpacity = 0.2f * (1f - (float)i / npc.oldPos.Length);
                Vector2 drawPos = npc.oldPos[i] + npc.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(
                    texture,
                    drawPos,
                    frame,
                    Color.White * trailOpacity,
                    drawRotation,
                    origin,
                    npc.scale,
                    effects,
                    0
                );
            }

            //绘制本体
            Vector2 mainDrawPos = npc.Center - Main.screenPosition;
            Main.EntitySpriteDraw(
                texture,
                mainDrawPos,
                frame,
                Color.White,
                drawRotation,
                origin,
                npc.scale,
                effects,
                0
            );
        }

        #endregion
    }
}
