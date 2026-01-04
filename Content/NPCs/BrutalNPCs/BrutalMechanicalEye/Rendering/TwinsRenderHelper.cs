using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
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

            //如果是激光扫射蓄力，绘制扫射范围预警
            if (context.ChargeType == 4 && context.Target != null && context.ChargeProgress > 0.4f) {
                DrawSweepWarning(context, chargeColor);
            }

            //如果是转阶段蓄力，绘制能量聚集特效
            if (context.ChargeType == 5 && context.ChargeProgress > 0.3f) {
                DrawPhaseTransitionEffect(context, chargeColor);
            }

            //如果是聚焦光束蓄力，绘制锁定指示器
            if (context.ChargeType == 6 && context.Target != null && context.ChargeProgress > 0.2f) {
                DrawFocusedBeamIndicator(context, chargeColor);
            }

            //如果是激光矩阵蓄力，绘制矩阵网格
            if (context.ChargeType == 7 && context.ChargeProgress > 0.3f) {
                DrawLaserMatrixGrid(context, chargeColor);
            }

            //如果是影分身蓄力，绘制分身预警
            if (context.ChargeType == 8 && context.ChargeProgress > 0.2f) {
                DrawShadowDashIndicator(context, chargeColor);
            }

            //如果是火焰风暴蓄力，绘制风暴预警
            if (context.ChargeType == 9 && context.ChargeProgress > 0.3f) {
                DrawFlameStormIndicator(context, chargeColor);
            }

            //如果是合击蓄力，绘制双眼连接线
            if (context.ChargeType == 10 && context.ChargeProgress > 0.3f) {
                DrawCombinedAttackIndicator(context, chargeColor);
            }

            //如果是同步转阶段蓄力，绘制双眼同步特效
            if (context.ChargeType == 11 && context.ChargeProgress > 0.1f) {
                DrawSyncPhaseTransitionEffect(context, chargeColor);
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
                4 => Color.Purple,
                5 => Color.Lerp(Color.OrangeRed, Color.BlueViolet, 0.5f),
                6 => Color.Cyan,
                7 => Color.MediumPurple,
                8 => Color.OrangeRed,
                9 => Color.Orange,
                10 => Color.Lerp(Color.OrangeRed, Color.Cyan, 0.5f),
                11 => Color.Lerp(Color.OrangeRed, Color.BlueViolet, 0.5f),
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

        /// <summary>
        /// 绘制扫射预警范围
        /// </summary>
        private static void DrawSweepWarning(TwinsStateContext context, Color baseColor) {
            if (context.Target == null) {
                return;
            }

            Vector2 direction = (context.Target.Center - context.Npc.Center).SafeNormalize(Vector2.Zero);
            float spreadAngle = MathHelper.PiOver4;
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            Texture2D lineTex = CWRAsset.LightShot.Value;

            //绘制扫射范围弧线
            int arcSegments = 20;
            float arcRadius = 300f * context.ChargeProgress;

            for (int i = 0; i <= arcSegments; i++) {
                float t = i / (float)arcSegments;
                float angle = MathHelper.Lerp(-spreadAngle, spreadAngle, t);
                Vector2 arcDir = direction.RotatedBy(angle);
                Vector2 arcPos = context.Npc.Center + arcDir * arcRadius - Main.screenPosition;

                float alpha = context.ChargeProgress * 0.6f;
                float scale = 0.4f + (1f - System.Math.Abs(t - 0.5f) * 2f) * 0.3f;

                Main.EntitySpriteDraw(
                    glowTex,
                    arcPos,
                    null,
                    baseColor * alpha,
                    0f,
                    glowTex.Size() / 2f,
                    scale,
                    SpriteEffects.None,
                    0
                );
            }

            //绘制扫射方向指示线
            for (int side = -1; side <= 1; side += 2) {
                Vector2 lineDir = direction.RotatedBy(spreadAngle * side);
                float lineLength = arcRadius;

                int segments = (int)(lineLength / 25f);
                for (int i = 0; i < segments; i++) {
                    float segT = i / (float)segments;
                    Vector2 segPos = Vector2.Lerp(context.Npc.Center, context.Npc.Center + lineDir * lineLength, segT) - Main.screenPosition;
                    float alpha = (1f - segT) * context.ChargeProgress * 0.5f;

                    Main.EntitySpriteDraw(
                        lineTex,
                        segPos,
                        null,
                        baseColor * alpha,
                        lineDir.ToRotation(),
                        new Vector2(0, lineTex.Height / 2f),
                        new Vector2(0.3f, 0.25f),
                        SpriteEffects.None,
                        0
                    );
                }
            }
        }

        /// <summary>
        /// 绘制转阶段能量聚集特效
        /// </summary>
        private static void DrawPhaseTransitionEffect(TwinsStateContext context, Color baseColor) {
            Vector2 drawPos = context.Npc.Center - Main.screenPosition;
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            Texture2D circleTex = CWRAsset.DiffusionCircle.Value;

            //根据是魔焰眼还是激光眼调整颜色
            Color eyeColor = context.IsSpazmatism ? Color.OrangeRed : Color.BlueViolet;
            Color mixColor = Color.Lerp(baseColor, eyeColor, context.ChargeProgress);

            //绘制多层收缩圆环
            for (int i = 0; i < 3; i++) {
                float layerProgress = (context.ChargeProgress + i * 0.15f) % 1f;
                float layerScale = 3f - layerProgress * 2.5f;
                float layerAlpha = layerProgress * (1f - layerProgress) * 1.5f;

                Main.EntitySpriteDraw(
                    circleTex,
                    drawPos,
                    null,
                    mixColor * layerAlpha,
                    Main.GlobalTimeWrappedHourly * (2f + i),
                    circleTex.Size() / 2f,
                    layerScale,
                    SpriteEffects.None,
                    0
                );
            }

            //绘制中心强光
            float coreScale = 0.5f + context.ChargeProgress * 2f;
            float coreAlpha = context.ChargeProgress * 0.8f;
            Main.EntitySpriteDraw(
                glowTex,
                drawPos,
                null,
                eyeColor * coreAlpha,
                0f,
                glowTex.Size() / 2f,
                coreScale,
                SpriteEffects.None,
                0
            );

            //绘制脉冲波纹
            float pulsePhase = (Main.GlobalTimeWrappedHourly * 3f) % 1f;
            float pulseScale = 1f + pulsePhase * 3f;
            float pulseAlpha = (1f - pulsePhase) * context.ChargeProgress * 0.4f;
            Main.EntitySpriteDraw(
                circleTex,
                drawPos,
                null,
                mixColor * pulseAlpha,
                0f,
                circleTex.Size() / 2f,
                pulseScale,
                SpriteEffects.None,
                0
            );
        }

        /// <summary>
        /// 绘制聚焦光束锁定指示器
        /// </summary>
        private static void DrawFocusedBeamIndicator(TwinsStateContext context, Color baseColor) {
            if (context.Target == null) {
                return;
            }

            Vector2 drawPos = context.Npc.Center - Main.screenPosition;
            Vector2 direction = (context.Target.Center - context.Npc.Center).SafeNormalize(Vector2.Zero);
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            Texture2D lineTex = CWRAsset.LightShot.Value;

            //绘制锁定圆环
            float ringScale = 1.5f - context.ChargeProgress * 0.8f;
            float ringAlpha = context.ChargeProgress * 0.7f;
            float ringRotation = Main.GlobalTimeWrappedHourly * 4f;

            for (int i = 0; i < 2; i++) {
                Main.EntitySpriteDraw(
                    CWRAsset.DiffusionCircle.Value,
                    drawPos,
                    null,
                    baseColor * ringAlpha * (1f - i * 0.3f),
                    ringRotation * (i == 0 ? 1 : -1),
                    CWRAsset.DiffusionCircle.Value.Size() / 2f,
                    ringScale * (1f + i * 0.2f),
                    SpriteEffects.None,
                    0
                );
            }

            //绘制瞄准激光线
            float lineLength = 350f * context.ChargeProgress;
            int segments = (int)(lineLength / 15f);

            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float segDist = t * lineLength;
                Vector2 segPos = context.Npc.Center + direction * segDist - Main.screenPosition;

                //闪烁效果
                float flicker = 0.7f + 0.3f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 10f + t * 5f);
                float alpha = context.ChargeProgress * flicker * (1f - t * 0.5f);
                float scale = 0.4f + (1f - t) * 0.3f;

                Main.EntitySpriteDraw(
                    lineTex,
                    segPos,
                    null,
                    baseColor * alpha,
                    direction.ToRotation(),
                    new Vector2(0, lineTex.Height / 2f),
                    new Vector2(scale, scale * 0.4f),
                    SpriteEffects.None,
                    0
                );
            }

            //绘制准心
            if (context.ChargeProgress > 0.5f) {
                float crosshairAlpha = (context.ChargeProgress - 0.5f) * 2f * 0.6f;
                Vector2 targetPos = context.Npc.Center + direction * lineLength - Main.screenPosition;

                //四个方向的准心线
                for (int i = 0; i < 4; i++) {
                    float angle = MathHelper.PiOver2 * i;
                    Vector2 offset = angle.ToRotationVector2() * 20f;

                    Main.EntitySpriteDraw(
                        glowTex,
                        targetPos + offset,
                        null,
                        baseColor * crosshairAlpha,
                        0f,
                        glowTex.Size() / 2f,
                        0.3f,
                        SpriteEffects.None,
                        0
                    );
                }
            }
        }

        /// <summary>
        /// 绘制激光矩阵网格
        /// </summary>
        private static void DrawLaserMatrixGrid(TwinsStateContext context, Color baseColor) {
            Vector2 drawPos = context.Npc.Center - Main.screenPosition;
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            Texture2D circleTex = CWRAsset.DiffusionCircle.Value;

            //绘制中心节点
            float coreAlpha = context.ChargeProgress * 0.5f;
            float coreScale = 0.8f + context.ChargeProgress * 0.5f;
            Main.EntitySpriteDraw(
                glowTex,
                drawPos,
                null,
                baseColor * coreAlpha,
                0f,
                glowTex.Size() / 2f,
                coreScale,
                SpriteEffects.None,
                0
            );

            //绘制网格连接线
            int gridPoints = 4;
            float gridRadius = 150f * context.ChargeProgress;
            float rotation = Main.GlobalTimeWrappedHourly * 0.5f;

            for (int i = 0; i < gridPoints; i++) {
                float angle = MathHelper.TwoPi / gridPoints * i + rotation + MathHelper.PiOver4;
                Vector2 pointPos = drawPos + angle.ToRotationVector2() * gridRadius;

                //绘制节点
                float nodeAlpha = context.ChargeProgress * 0.6f;
                float nodeScale = 0.5f + context.ChargeProgress * 0.3f;
                float nodePulse = 0.8f + 0.2f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 5f + i);

                Main.EntitySpriteDraw(
                    glowTex,
                    pointPos,
                    null,
                    baseColor * nodeAlpha * nodePulse,
                    0f,
                    glowTex.Size() / 2f,
                    nodeScale,
                    SpriteEffects.None,
                    0
                );

                //绘制到中心的连接线
                int lineSegments = 8;
                for (int j = 1; j < lineSegments; j++) {
                    float t = j / (float)lineSegments;
                    Vector2 linePos = Vector2.Lerp(drawPos, pointPos, t);
                    float lineAlpha = context.ChargeProgress * 0.4f * (1f - System.Math.Abs(t - 0.5f));

                    Main.EntitySpriteDraw(
                        glowTex,
                        linePos,
                        null,
                        baseColor * lineAlpha,
                        0f,
                        glowTex.Size() / 2f,
                        0.2f,
                        SpriteEffects.None,
                        0
                    );
                }

                //绘制相邻节点连接
                int nextI = (i + 1) % gridPoints;
                float nextAngle = MathHelper.TwoPi / gridPoints * nextI + rotation + MathHelper.PiOver4;
                Vector2 nextPointPos = drawPos + nextAngle.ToRotationVector2() * gridRadius;

                int edgeSegments = 6;
                for (int j = 1; j < edgeSegments; j++) {
                    float t = j / (float)edgeSegments;
                    Vector2 edgePos = Vector2.Lerp(pointPos, nextPointPos, t);
                    float edgeAlpha = context.ChargeProgress * 0.3f;

                    Main.EntitySpriteDraw(
                        glowTex,
                        edgePos,
                        null,
                        baseColor * edgeAlpha,
                        0f,
                        glowTex.Size() / 2f,
                        0.15f,
                        SpriteEffects.None,
                        0
                    );
                }
            }

            //绘制外圈扩散效果
            if (context.ChargeProgress > 0.6f) {
                float ringProgress = (context.ChargeProgress - 0.6f) / 0.4f;
                float ringScale = 1f + ringProgress * 1.5f;
                float ringAlpha = (1f - ringProgress) * 0.4f;

                Main.EntitySpriteDraw(
                    circleTex,
                    drawPos,
                    null,
                    baseColor * ringAlpha,
                    Main.GlobalTimeWrappedHourly * 2f,
                    circleTex.Size() / 2f,
                    ringScale,
                    SpriteEffects.None,
                    0
                );
            }
        }

        /// <summary>
        /// 绘制影分身冲刺指示器
        /// </summary>
        private static void DrawShadowDashIndicator(TwinsStateContext context, Color baseColor) {
            Vector2 drawPos = context.Npc.Center - Main.screenPosition;
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            Texture2D circleTex = CWRAsset.DiffusionCircle.Value;

            //绘制分身位置预览
            int shadowCount = 3;
            float radius = 300f * context.ChargeProgress;

            for (int i = 0; i < shadowCount; i++) {
                float angle = MathHelper.TwoPi / shadowCount * i + MathHelper.PiOver2;
                Vector2 shadowPos = drawPos + angle.ToRotationVector2() * radius;

                //分身轮廓
                float shadowAlpha = context.ChargeProgress * 0.4f;
                float shadowScale = 0.6f + context.ChargeProgress * 0.4f;
                float pulse = 0.7f + 0.3f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 4f + i * 2f);

                Main.EntitySpriteDraw(
                    glowTex,
                    shadowPos,
                    null,
                    baseColor * shadowAlpha * pulse,
                    0f,
                    glowTex.Size() / 2f,
                    shadowScale,
                    SpriteEffects.None,
                    0
                );

                //冲刺方向线
                if (context.Target != null && context.ChargeProgress > 0.5f) {
                    Vector2 targetPos = context.Target.Center - Main.screenPosition;
                    Vector2 toTarget = (targetPos - shadowPos).SafeNormalize(Vector2.Zero);
                    float lineLength = 80f * (context.ChargeProgress - 0.5f) * 2f;

                    int segments = 5;
                    for (int j = 0; j < segments; j++) {
                        float t = j / (float)segments;
                        Vector2 linePos = shadowPos + toTarget * (t * lineLength);
                        float lineAlpha = (1f - t) * context.ChargeProgress * 0.5f;

                        Main.EntitySpriteDraw(
                            glowTex,
                            linePos,
                            null,
                            baseColor * lineAlpha,
                            0f,
                            glowTex.Size() / 2f,
                            0.25f,
                            SpriteEffects.None,
                            0
                        );
                    }
                }
            }

            //中心聚集效果
            float coreScale = 1f + context.ChargeProgress * 0.5f;
            float coreAlpha = context.ChargeProgress * 0.6f;
            Main.EntitySpriteDraw(
                circleTex,
                drawPos,
                null,
                baseColor * coreAlpha,
                Main.GlobalTimeWrappedHourly * 3f,
                circleTex.Size() / 2f,
                coreScale,
                SpriteEffects.None,
                0
            );
        }

        /// <summary>
        /// 绘制火焰风暴指示器
        /// </summary>
        private static void DrawFlameStormIndicator(TwinsStateContext context, Color baseColor) {
            if (context.Target == null) {
                return;
            }

            Vector2 targetPos = context.Target.Center - Main.screenPosition;
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            Texture2D circleTex = CWRAsset.DiffusionCircle.Value;

            //绘制风暴范围圆
            float stormRadius = 350f * context.ChargeProgress;
            float circleAlpha = context.ChargeProgress * 0.3f;

            Main.EntitySpriteDraw(
                circleTex,
                targetPos,
                null,
                baseColor * circleAlpha,
                Main.GlobalTimeWrappedHourly * 2f,
                circleTex.Size() / 2f,
                stormRadius / (circleTex.Width / 2f),
                SpriteEffects.None,
                0
            );

            //绘制旋转火焰点
            int flamePoints = 8;
            float rotation = Main.GlobalTimeWrappedHourly * 3f;

            for (int i = 0; i < flamePoints; i++) {
                float angle = MathHelper.TwoPi / flamePoints * i + rotation;
                float pointRadius = stormRadius * (0.6f + 0.4f * (float)System.Math.Sin(angle * 2f + Main.GlobalTimeWrappedHourly * 5f));
                Vector2 flamePos = targetPos + angle.ToRotationVector2() * pointRadius;

                float flameAlpha = context.ChargeProgress * 0.5f;
                float flameScale = 0.4f + context.ChargeProgress * 0.3f;

                Main.EntitySpriteDraw(
                    glowTex,
                    flamePos,
                    null,
                    baseColor * flameAlpha,
                    0f,
                    glowTex.Size() / 2f,
                    flameScale,
                    SpriteEffects.None,
                    0
                );
            }

            //绘制中心标记
            float centerAlpha = context.ChargeProgress * 0.4f;
            Main.EntitySpriteDraw(
                glowTex,
                targetPos,
                null,
                baseColor * centerAlpha,
                0f,
                glowTex.Size() / 2f,
                0.8f,
                SpriteEffects.None,
                0
            );
        }

        /// <summary>
        /// 绘制合击指示器
        /// </summary>
        private static void DrawCombinedAttackIndicator(TwinsStateContext context, Color baseColor) {
            if (context.Target == null) {
                return;
            }

            Vector2 drawPos = context.Npc.Center - Main.screenPosition;
            Vector2 targetPos = context.Target.Center - Main.screenPosition;
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            Texture2D circleTex = CWRAsset.DiffusionCircle.Value;

            //绘制到目标的冲刺线
            Vector2 direction = (targetPos - drawPos).SafeNormalize(Vector2.Zero);
            float distance = Vector2.Distance(drawPos, targetPos);
            float lineLength = distance * context.ChargeProgress;

            int segments = (int)(lineLength / 20f);
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                Vector2 segPos = Vector2.Lerp(drawPos, targetPos, t * context.ChargeProgress);

                //交替颜色(火焰和激光)
                Color segColor = i % 2 == 0 ? Color.OrangeRed : Color.BlueViolet;
                float segAlpha = (1f - t) * context.ChargeProgress * 0.5f;
                float pulse = 0.7f + 0.3f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 8f + t * 10f);

                Main.EntitySpriteDraw(
                    glowTex,
                    segPos,
                    null,
                    segColor * segAlpha * pulse,
                    0f,
                    glowTex.Size() / 2f,
                    0.3f + (1f - t) * 0.2f,
                    SpriteEffects.None,
                    0
                );
            }

            //绘制碰撞点
            if (context.ChargeProgress > 0.5f) {
                float collisionProgress = (context.ChargeProgress - 0.5f) * 2f;
                float collisionScale = 0.5f + collisionProgress * 1.5f;
                float collisionAlpha = collisionProgress * 0.6f;

                //双色碰撞圆
                Main.EntitySpriteDraw(
                    circleTex,
                    targetPos,
                    null,
                    Color.OrangeRed * collisionAlpha,
                    Main.GlobalTimeWrappedHourly * 3f,
                    circleTex.Size() / 2f,
                    collisionScale,
                    SpriteEffects.None,
                    0
                );

                Main.EntitySpriteDraw(
                    circleTex,
                    targetPos,
                    null,
                    Color.BlueViolet * collisionAlpha * 0.7f,
                    -Main.GlobalTimeWrappedHourly * 2f,
                    circleTex.Size() / 2f,
                    collisionScale * 0.8f,
                    SpriteEffects.None,
                    0
                );
            }

            //本体周围的能量环
            float ringAlpha = context.ChargeProgress * 0.5f;
            float ringScale = 0.8f + context.ChargeProgress * 0.4f;
            Main.EntitySpriteDraw(
                circleTex,
                drawPos,
                null,
                baseColor * ringAlpha,
                Main.GlobalTimeWrappedHourly * 4f,
                circleTex.Size() / 2f,
                ringScale,
                SpriteEffects.None,
                0
            );
        }

        /// <summary>
        /// 绘制同步转阶段特效
        /// </summary>
        private static void DrawSyncPhaseTransitionEffect(TwinsStateContext context, Color baseColor) {
            Vector2 drawPos = context.Npc.Center - Main.screenPosition;
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            Texture2D circleTex = CWRAsset.DiffusionCircle.Value;

            //根据是魔焰眼还是激光眼调整颜色
            Color eyeColor = context.IsSpazmatism ? Color.OrangeRed : Color.BlueViolet;
            Color mixColor = Color.Lerp(baseColor, eyeColor, 0.7f);

            //多层收缩圆环
            for (int i = 0; i < 4; i++) {
                float layerProgress = (context.ChargeProgress + i * 0.12f) % 1f;
                float layerScale = 2.5f - layerProgress * 2f;
                float layerAlpha = layerProgress * (1f - layerProgress) * 1.2f;

                Main.EntitySpriteDraw(
                    circleTex,
                    drawPos,
                    null,
                    mixColor * layerAlpha,
                    Main.GlobalTimeWrappedHourly * (2.5f + i * 0.5f),
                    circleTex.Size() / 2f,
                    layerScale,
                    SpriteEffects.None,
                    0
                );
            }

            //中心强光
            float coreScale = 0.8f + context.ChargeProgress * 1.5f;
            float coreAlpha = context.ChargeProgress * 0.7f;
            Main.EntitySpriteDraw(
                glowTex,
                drawPos,
                null,
                eyeColor * coreAlpha,
                0f,
                glowTex.Size() / 2f,
                coreScale,
                SpriteEffects.None,
                0
            );

            //能量放射线
            if (context.ChargeProgress > 0.3f) {
                int rayCount = 8;
                float rayLength = 100f * (context.ChargeProgress - 0.3f) / 0.7f;
                float rayRotation = Main.GlobalTimeWrappedHourly * 2f;

                for (int i = 0; i < rayCount; i++) {
                    float angle = MathHelper.TwoPi / rayCount * i + rayRotation;
                    Vector2 rayDir = angle.ToRotationVector2();

                    int segments = 6;
                    for (int j = 0; j < segments; j++) {
                        float t = j / (float)segments;
                        Vector2 rayPos = drawPos + rayDir * (30f + t * rayLength);
                        float rayAlpha = (1f - t) * context.ChargeProgress * 0.5f;

                        Main.EntitySpriteDraw(
                            glowTex,
                            rayPos,
                            null,
                            eyeColor * rayAlpha,
                            0f,
                            glowTex.Size() / 2f,
                            0.3f * (1f - t * 0.5f),
                            SpriteEffects.None,
                            0
                        );
                    }
                }
            }

            //寻找另一只眼睛并绘制连接线
            NPC partner = TwinsStateContext.GetPartnerNpc(context.Npc.type);
            if (partner != null && partner.active && context.ChargeProgress > 0.2f) {
                Vector2 partnerPos = partner.Center - Main.screenPosition;
                Vector2 midPoint = (drawPos + partnerPos) / 2f;

                //双眼连接线
                int linkSegments = 12;
                for (int i = 0; i < linkSegments; i++) {
                    float t = i / (float)(linkSegments - 1);
                    Vector2 linkPos = Vector2.Lerp(drawPos, partnerPos, t);

                    //波动效果
                    Vector2 perpendicular = (partnerPos - drawPos).SafeNormalize(Vector2.Zero);
                    perpendicular = new Vector2(-perpendicular.Y, perpendicular.X);
                    float wave = (float)System.Math.Sin(t * MathHelper.TwoPi * 2f + Main.GlobalTimeWrappedHourly * 5f) * 15f * context.ChargeProgress;
                    linkPos += perpendicular * wave;

                    //交替颜色
                    Color linkColor = i % 2 == 0 ? Color.OrangeRed : Color.BlueViolet;
                    float linkAlpha = context.ChargeProgress * 0.6f * (1f - System.Math.Abs(t - 0.5f) * 0.5f);
                    float pulse = 0.8f + 0.2f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 6f + t * 8f);

                    Main.EntitySpriteDraw(
                        glowTex,
                        linkPos,
                        null,
                        linkColor * linkAlpha * pulse,
                        0f,
                        glowTex.Size() / 2f,
                        0.35f,
                        SpriteEffects.None,
                        0
                    );
                }

                //中点能量聚集
                if (context.ChargeProgress > 0.5f) {
                    float midProgress = (context.ChargeProgress - 0.5f) * 2f;
                    float midScale = 0.5f + midProgress * 1f;
                    float midAlpha = midProgress * 0.5f;

                    Main.EntitySpriteDraw(
                        circleTex,
                        midPoint,
                        null,
                        Color.White * midAlpha,
                        Main.GlobalTimeWrappedHourly * 5f,
                        circleTex.Size() / 2f,
                        midScale,
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
