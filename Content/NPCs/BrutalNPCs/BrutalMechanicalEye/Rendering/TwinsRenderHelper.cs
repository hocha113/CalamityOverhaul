using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Rendering
{
    /// <summary>绘制辅助，蓄力/本体残影/热感</summary>
    internal static class TwinsRenderHelper
    {
        #region 蓄力特效

        /// <summary>按 ChargeType 绘制蓄力特效</summary>
        public static void DrawChargeEffect(SpriteBatch spriteBatch, TwinsStateContext context) {
            if (!context.IsCharging || context.ChargeProgress <= 0) {
                return;
            }

            Vector2 drawPos = context.Npc.Center - Main.screenPosition;
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            Texture2D circleTex = CWRAsset.DiffusionCircle.Value;

            Color chargeColor = GetChargeColor(context.ChargeType);

            spriteBatch.End();

            //能量汇聚涡
            DrawChargeVortex(spriteBatch, context, chargeColor);

            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.Additive,
                SamplerState.AnisotropicClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                Main.GameViewMatrix.TransformationMatrix
            );

            DrawGlowEffect(glowTex, drawPos, chargeColor, context.ChargeProgress);

            DrawCircleEffect(circleTex, drawPos, chargeColor, context.ChargeProgress);

            if (context.ChargeType == 2 && context.Target != null && context.ChargeProgress > 0.3f) {
                DrawAimLine(context, chargeColor);
            }

            if (context.ChargeType == 4 && context.Target != null && context.ChargeProgress > 0.4f) {
                DrawSweepWarning(context, chargeColor);
            }

            if (context.ChargeType == 5 && context.ChargeProgress > 0.3f) {
                DrawPhaseTransitionEffect(context, chargeColor);
            }

            if (context.ChargeType == 6 && context.Target != null && context.ChargeProgress > 0.2f) {
                DrawFocusedBeamIndicator(context, chargeColor);
            }

            if (context.ChargeType == 7 && context.ChargeProgress > 0.3f) {
                DrawLaserMatrixGrid(context, chargeColor);
            }

            if (context.ChargeType == 8 && context.ChargeProgress > 0.2f) {
                DrawShadowDashIndicator(context, chargeColor);
            }

            if (context.ChargeType == 9 && context.ChargeProgress > 0.3f) {
                DrawFlameStormIndicator(context, chargeColor);
            }

            if (context.ChargeType == 10 && context.ChargeProgress > 0.3f) {
                DrawCombinedAttackIndicator(context, chargeColor);
            }

            if (context.ChargeType == 11 && context.ChargeProgress > 0.1f) {
                DrawSyncPhaseTransitionEffect(context, chargeColor);
            }

            if (context.ChargeType == 12 && context.ChargeProgress > 0.15f) {
                DrawTetherSweepWarning(context, chargeColor);
            }

            if (context.ChargeType == 13 && context.ChargeProgress > 0.15f) {
                DrawScissorRayWarning(context, chargeColor);
            }

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

        /// <summary>蓄力汇聚涡，缺着色器则跳过</summary>
        private static void DrawChargeVortex(SpriteBatch spriteBatch, TwinsStateContext context, Color chargeColor) {
            if (CalamityOverhaul.Common.EffectLoader.TwinsChargeVortex?.Value == null) {
                return;
            }

            Effect shader = CalamityOverhaul.Common.EffectLoader.TwinsChargeVortex.Value;
            shader.Parameters["uColor"]?.SetValue(chargeColor.ToVector3());
            shader.Parameters["uSecondaryColor"]?.SetValue(Color.Lerp(chargeColor, Color.White, 0.55f).ToVector3());
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["uProgress"]?.SetValue(MathHelper.Clamp(context.ChargeProgress, 0f, 1f));
            shader.Parameters["uIntensity"]?.SetValue(0.45f + context.ChargeProgress * 0.75f);
            shader.Parameters["uOpacity"]?.SetValue(1f);
            shader.Parameters["uImage1"]?.SetValue(CWRAsset.Extra_193.Value);

            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            shader.CurrentTechnique.Passes[0].Apply();

            Texture2D quad = VaultAsset.placeholder2.Value;
            const float size = 380f;
            spriteBatch.Draw(quad, context.Npc.Center - Main.screenPosition, null, Color.White,
                0f, quad.Size() / 2f, new Vector2(size / quad.Width, size / quad.Height), SpriteEffects.None, 0f);

            spriteBatch.End();
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
                12 => new Color(140, 215, 255),
                13 => Color.Lerp(new Color(255, 110, 35), new Color(120, 200, 255), 0.5f),
                _ => Color.White
            };
        }

        /// <summary>链锁预警虚线</summary>
        private static void DrawTetherSweepWarning(TwinsStateContext context, Color baseColor) {
            NPC partner = TwinsStateContext.GetPartnerNpc(context.Npc.type);
            if (partner == null || !partner.active) {
                return;
            }

            Texture2D lineTex = CWRAsset.LightShot.Value;
            Vector2 start = context.Npc.Center;
            Vector2 end = partner.Center;
            Vector2 dir = (end - start).SafeNormalize(Vector2.Zero);
            float totalDist = Vector2.Distance(start, end);
            float pulse = 0.7f + 0.3f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 14f);

            int segments = (int)(totalDist / 46f);
            for (int i = 0; i < segments; i++) {
                float t = (i + (Main.GlobalTimeWrappedHourly * 2f % 1f)) / segments;
                if (t > 1f) {
                    continue;
                }
                Vector2 segPos = Vector2.Lerp(start, end, t) - Main.screenPosition;
                float alpha = context.ChargeProgress * 0.65f * pulse;
                Main.EntitySpriteDraw(lineTex, segPos, null, baseColor * alpha,
                    dir.ToRotation(), new Vector2(0, lineTex.Height / 2f),
                    new Vector2(0.16f, 0.22f), SpriteEffects.None, 0);
            }
        }

        /// <summary>剪刀死光预警虚线</summary>
        private static void DrawScissorRayWarning(TwinsStateContext context, Color baseColor) {
            Texture2D lineTex = CWRAsset.LightShot.Value;
            Vector2 rayDir = (context.Npc.rotation + MathHelper.PiOver2).ToRotationVector2();
            float rayLength = 1500f * context.ChargeProgress;
            float pulse = 0.65f + 0.35f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 16f);

            int segments = (int)(rayLength / 52f);
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                Vector2 segPos = context.Npc.Center + rayDir * rayLength * t - Main.screenPosition;
                float alpha = (1f - t * 0.6f) * context.ChargeProgress * 0.7f * pulse;
                float scale = 0.34f - t * 0.12f;
                Main.EntitySpriteDraw(lineTex, segPos, null, baseColor * alpha,
                    rayDir.ToRotation(), new Vector2(0, lineTex.Height / 2f),
                    new Vector2(scale, scale * 0.6f), SpriteEffects.None, 0);
            }
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

            for (int side = -1; side <= 1; side += 2) {
                Vector2 lineDir = direction.RotatedBy(spreadAngle / 2 * side);
                float lineLength = 400f * context.ChargeProgress;
                Vector2 lineEnd = context.Npc.Center + lineDir * lineLength;

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

        private static void DrawSweepWarning(TwinsStateContext context, Color baseColor) {
            if (context.Target == null) {
                return;
            }

            Vector2 direction = (context.Target.Center - context.Npc.Center).SafeNormalize(Vector2.Zero);
            float spreadAngle = MathHelper.PiOver4;
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            Texture2D lineTex = CWRAsset.LightShot.Value;

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

        private static void DrawPhaseTransitionEffect(TwinsStateContext context, Color baseColor) {
            Vector2 drawPos = context.Npc.Center - Main.screenPosition;
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            Texture2D circleTex = CWRAsset.DiffusionCircle.Value;

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

        private static void DrawFocusedBeamIndicator(TwinsStateContext context, Color baseColor) {
            if (context.Target == null) {
                return;
            }

            Vector2 drawPos = context.Npc.Center - Main.screenPosition;
            Vector2 direction = (context.Target.Center - context.Npc.Center).SafeNormalize(Vector2.Zero);
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            Texture2D lineTex = CWRAsset.LightShot.Value;

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

            float lineLength = 350f * context.ChargeProgress;
            int segments = (int)(lineLength / 15f);

            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float segDist = t * lineLength;
                Vector2 segPos = context.Npc.Center + direction * segDist - Main.screenPosition;

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

            if (context.ChargeProgress > 0.5f) {
                float crosshairAlpha = (context.ChargeProgress - 0.5f) * 2f * 0.6f;
                Vector2 targetPos = context.Npc.Center + direction * lineLength - Main.screenPosition;

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

        private static void DrawLaserMatrixGrid(TwinsStateContext context, Color baseColor) {
            Vector2 drawPos = context.Npc.Center - Main.screenPosition;
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            Texture2D circleTex = CWRAsset.DiffusionCircle.Value;

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

            int gridPoints = 4;
            float gridRadius = 150f * context.ChargeProgress;
            float rotation = Main.GlobalTimeWrappedHourly * 0.5f;

            for (int i = 0; i < gridPoints; i++) {
                float angle = MathHelper.TwoPi / gridPoints * i + rotation + MathHelper.PiOver4;
                Vector2 pointPos = drawPos + angle.ToRotationVector2() * gridRadius;

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

        private static void DrawFlameStormIndicator(TwinsStateContext context, Color baseColor) {
            if (context.Target == null) {
                return;
            }

            Vector2 targetPos = context.Target.Center - Main.screenPosition;
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            Texture2D circleTex = CWRAsset.DiffusionCircle.Value;

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

        private static void DrawCombinedAttackIndicator(TwinsStateContext context, Color baseColor) {
            if (context.Target == null) {
                return;
            }

            Vector2 drawPos = context.Npc.Center - Main.screenPosition;
            Vector2 targetPos = context.Target.Center - Main.screenPosition;
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            Texture2D circleTex = CWRAsset.DiffusionCircle.Value;

            Vector2 direction = (targetPos - drawPos).SafeNormalize(Vector2.Zero);
            float distance = Vector2.Distance(drawPos, targetPos);
            float lineLength = distance * context.ChargeProgress;

            int segments = (int)(lineLength / 20f);
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                Vector2 segPos = Vector2.Lerp(drawPos, targetPos, t * context.ChargeProgress);

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

            if (context.ChargeProgress > 0.5f) {
                float collisionProgress = (context.ChargeProgress - 0.5f) * 2f;
                float collisionScale = 0.5f + collisionProgress * 1.5f;
                float collisionAlpha = collisionProgress * 0.6f;

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

        private static void DrawSyncPhaseTransitionEffect(TwinsStateContext context, Color baseColor) {
            Vector2 drawPos = context.Npc.Center - Main.screenPosition;
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            Texture2D circleTex = CWRAsset.DiffusionCircle.Value;

            Color eyeColor = context.IsSpazmatism ? Color.OrangeRed : Color.BlueViolet;
            Color mixColor = Color.Lerp(baseColor, eyeColor, 0.7f);

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

            NPC partner = TwinsStateContext.GetPartnerNpc(context.Npc.type);
            if (partner != null && partner.active && context.ChargeProgress > 0.2f) {
                Vector2 partnerPos = partner.Center - Main.screenPosition;
                Vector2 midPoint = (drawPos + partnerPos) / 2f;

                int linkSegments = 12;
                for (int i = 0; i < linkSegments; i++) {
                    float t = i / (float)(linkSegments - 1);
                    Vector2 linkPos = Vector2.Lerp(drawPos, partnerPos, t);

                    Vector2 perpendicular = (partnerPos - drawPos).SafeNormalize(Vector2.Zero);
                    perpendicular = new Vector2(-perpendicular.Y, perpendicular.X);
                    float wave = (float)System.Math.Sin(t * MathHelper.TwoPi * 2f + Main.GlobalTimeWrappedHourly * 5f) * 15f * context.ChargeProgress;
                    linkPos += perpendicular * wave;

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

        /// <summary>本体+拖尾，叠热感与 DashStretch/AfterimageBoost</summary>
        public static void DrawNpcBody(
            SpriteBatch spriteBatch,
            NPC npc,
            Texture2D texture,
            int frameIndex,
            float rotation,
            TwinsStateContext context = null
        ) {
            Rectangle frame = texture.Frame(1, 4, 0, frameIndex);
            Vector2 origin = frame.Size() / 2f;
            SpriteEffects effects = npc.spriteDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            float drawRotation = rotation + MathHelper.PiOver2;

            float dashStretch = context?.DashStretch ?? 0f;
            float afterimageBoost = context?.AfterimageBoost ?? 0f;

            //速度拉伸 squash
            float speedFactor = MathHelper.Clamp(npc.velocity.Length() / 30f, 0f, 1f);
            float stretchAmount = dashStretch * speedFactor;
            Vector2 scaleVec = new Vector2(
                npc.scale * (1f - stretchAmount * 0.16f),
                npc.scale * (1f + stretchAmount * 0.34f)
            );

            //残影不套滤镜
            //冲刺残影加亮加密
            Color afterimageTint = Color.White;
            if (afterimageBoost > 0.01f && context != null) {
                Color theme = context.IsSpazmatism ? new Color(255, 110, 35) : new Color(120, 200, 255);
                afterimageTint = Color.Lerp(Color.White, theme, afterimageBoost * 0.45f);
            }
            float baseTrailOpacity = 0.2f + afterimageBoost * 0.3f;
            for (int i = 0; i < npc.oldPos.Length; i++) {
                float trailOpacity = baseTrailOpacity * (1f - (float)i / npc.oldPos.Length);
                Vector2 drawPos = npc.oldPos[i] + npc.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(
                    texture,
                    drawPos,
                    frame,
                    afterimageTint * trailOpacity,
                    drawRotation,
                    origin,
                    scaleVec,
                    effects,
                    0
                );
            }

            Vector2 mainDrawPos = npc.Center - Main.screenPosition;

            //外圈描边
            MechBossThermalRenderer.DrawOutlineHaloByController(
                spriteBatch, texture, mainDrawPos, frame,
                drawRotation, origin, npc.scale, effects, npc.whoAmI);

            //热感着色器
            float seed = (npc.whoAmI % 64) / 64f;
            bool shaderApplied = MechBossThermalRenderer.BeginThermalShaderByController(
                spriteBatch, texture, frame, npc.whoAmI, seed);

            spriteBatch.Draw(
                texture,
                mainDrawPos,
                frame,
                Color.White,
                drawRotation,
                origin,
                scaleVec,
                effects,
                0f
            );

            if (shaderApplied) {
                MechBossThermalRenderer.EndThermalShader(spriteBatch);
            }
        }

        #endregion
    }
}
