using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.AcheronProtocols
{
    /// <summary>
    /// 灭绝令效果：三阶段恒星毁灭动画、波纹、警告文本 阶段0：恒星变红 → 阶段1：猛烈闪烁 → 阶段2：缩小消失
    /// </summary>
    internal partial class GalacticCrisisRender
    {
        #region 灭绝令参数

        private static float extinctionProgress;
        private static float extinctionFlashTimer;
        private static float extinctionWaveRadius;

        //阶段时间常量（单位：秒）
        private const float ExtinctionReddenDuration = 0.6f;
        private const float ExtinctionFlashDuration = 0.8f;
        private const float ExtinctionFadeDuration = 0.5f;

        #endregion

        #region 初始化

        private static void InitExtinction() {
            extinctionProgress = 0f;
            extinctionFlashTimer = 0f;
            extinctionWaveRadius = 0f;

            //重置所有恒星的灭绝令状态
            foreach (var star in galaxyStars) {
                star.ExtinctionMarked = false;
                star.ExtinctionLerp = 0f;
                star.ExtinctionStage = 0;
                star.ExtinctionStageTimer = 0f;
            }
        }

        #endregion

        #region 逻辑更新

        private static void UpdateExtinctionLogic() {
            //在灭绝令阶段和闲置阶段都持续更新恒星动画
            if (currentPhase != AnimPhase.ExtinctionProtocol && currentPhase != AnimPhase.Idle) return;

            UpdateExtinctionStarAnimations();
        }

        private static void UpdateExtinctionPhase() {
            extinctionProgress = MathF.Min(extinctionProgress + 0.011f, 1f);
            extinctionFlashTimer += 0.08f;
            phaseProgress = extinctionProgress;

            if (extinctionProgress < 0.3f) {
                glitchIntensity = MathHelper.Lerp(0.3f, 0.1f, extinctionProgress / 0.3f);
            }

            extinctionWaveRadius = GalaxyRadius * VaultUtils.EaseOutCubic(extinctionProgress) * 0.9f;
            MarkStarsForExtinction();
        }

        /// <summary>

        /// 灭绝令波纹扫过恒星时标记它们 泰拉也在清理名单中，波纹到达其径向距离时一并标记

        /// </summary>
        private static void MarkStarsForExtinction() {
            float waveNormalized = extinctionWaveRadius / GalaxyRadius;
            foreach (var star in galaxyStars) {
                if (star.ExtinctionMarked) continue;

                float starOuterDistance = 1f - star.RadialDistance;
                if (starOuterDistance < waveNormalized) {
                    star.ExtinctionMarked = true;
                    star.ExtinctionLerp = 0f;
                    star.ExtinctionStage = 0;
                    star.ExtinctionStageTimer = 0f;
                }
            }

            //泰拉也会被灭绝令波纹标记
            if (!terraExtinctionMarked) {
                float terraOuterDistance = 1f - TerraRadialDistance;
                if (terraOuterDistance < waveNormalized) {
                    terraExtinctionMarked = true;
                    terraExtinctionLerp = 0f;
                    terraExtinctionStage = 0;
                    terraExtinctionStageTimer = 0f;
                }
            }
        }

        /// <summary>

        /// 更新所有被标记恒星和泰拉的三阶段毁灭动画

        /// </summary>
        private static void UpdateExtinctionStarAnimations() {
            float dt = 0.016f;
            foreach (var star in galaxyStars) {
                if (!star.ExtinctionMarked) continue;

                star.ExtinctionStageTimer += dt;

                switch (star.ExtinctionStage) {
                    case 0:
                        star.ExtinctionLerp = MathF.Min(star.ExtinctionStageTimer / ExtinctionReddenDuration, 1f);
                        if (star.ExtinctionLerp >= 1f) {
                            star.ExtinctionStage = 1;
                            star.ExtinctionStageTimer = 0f;
                        }
                        break;
                    case 1:
                        if (star.ExtinctionStageTimer >= ExtinctionFlashDuration) {
                            star.ExtinctionStage = 2;
                            star.ExtinctionStageTimer = 0f;
                        }
                        break;
                    case 2:
                        if (star.ExtinctionStageTimer > ExtinctionFadeDuration) {
                            star.ExtinctionStageTimer = ExtinctionFadeDuration;
                        }
                        break;
                }
            }

            //泰拉的灭绝令动画（与恒星相同的三阶段）
            if (terraExtinctionMarked) {
                terraExtinctionStageTimer += dt;
                switch (terraExtinctionStage) {
                    case 0:
                        terraExtinctionLerp = MathF.Min(terraExtinctionStageTimer / ExtinctionReddenDuration, 1f);
                        if (terraExtinctionLerp >= 1f) {
                            terraExtinctionStage = 1;
                            terraExtinctionStageTimer = 0f;
                        }
                        break;
                    case 1:
                        if (terraExtinctionStageTimer >= ExtinctionFlashDuration) {
                            terraExtinctionStage = 2;
                            terraExtinctionStageTimer = 0f;
                        }
                        break;
                    case 2:
                        if (terraExtinctionStageTimer > ExtinctionFadeDuration) {
                            terraExtinctionStageTimer = ExtinctionFadeDuration;
                        }
                        break;
                }
            }
        }

        #endregion

        #region 绘制

        private static void DrawExtinctionOverlay(SpriteBatch sb, Vector2 center, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            float flash = MathF.Sin(extinctionFlashTimer) * 0.3f + 0.7f;

            //绘制灭绝令波纹前沿
            DrawExtinctionWaveFront(sb, center, alpha);

            //灭绝令警告文本
            if (extinctionProgress > 0.05f) {
                DrawExtinctionWarningText(sb, alpha, flash);
            }
        }

        private static void DrawExtinctionWaveFront(SpriteBatch sb, Vector2 center, float alpha) {
            if (extinctionWaveRadius <= 5f) return;

            float waveRingRadius = GalaxyRadius - extinctionWaveRadius;
            if (waveRingRadius <= 0f || waveRingRadius >= GalaxyRadius) return;

            Texture2D softGlow = CWRAsset.SoftGlow?.Value;
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int wavePoints = 28;
            float wavePulse = MathF.Sin(extinctionFlashTimer * 4f) * 0.3f + 0.7f;

            for (int i = 0; i < wavePoints; i++) {
                float angle = MathHelper.TwoPi * i / wavePoints;
                Vector2 pos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * waveRingRadius;

                if (softGlow != null) {
                    Color waveColor = new Color(255, 40, 20);
                    waveColor.A = 0;
                    float waveAlpha = alpha * 0.2f * wavePulse;
                    Vector2 origin = new(softGlow.Width * 0.5f, softGlow.Height * 0.5f);
                    sb.Draw(softGlow, pos, null, waveColor * waveAlpha, 0f,
                        origin, 0.18f, SpriteEffects.None, 0f);
                }
                else if (pixel != null) {
                    Color dotColor = new Color(255, 50, 30) * (alpha * 0.3f * wavePulse);
                    dotColor.A = 0;
                    sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1),
                        dotColor, angle, new Vector2(0.5f),
                        new Vector2(8f, 3f), SpriteEffects.None, 0f);
                }
            }

            //波纹环细线
            if (pixel != null) {
                int ringSegments = 36;
                Color ringColor = new Color(200, 40, 30) * (alpha * 0.15f * wavePulse);
                for (int i = 0; i < ringSegments; i++) {
                    float angle = MathHelper.TwoPi * i / ringSegments;
                    Vector2 pos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * waveRingRadius;
                    float nextAngle = MathHelper.TwoPi * (i + 1) / ringSegments;
                    Vector2 nextPos = center + new Vector2(MathF.Cos(nextAngle), MathF.Sin(nextAngle)) * waveRingRadius;
                    Vector2 dir = nextPos - pos;
                    float segAngle = MathF.Atan2(dir.Y, dir.X);
                    float segLen = dir.Length();
                    sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1),
                        ringColor, segAngle, Vector2.Zero,
                        new Vector2(segLen, 1f), SpriteEffects.None, 0f);
                }
            }
        }

        private static void DrawExtinctionWarningText(SpriteBatch sb, float alpha, float flash) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            Rectangle panelRect = GetPanelRect();
            float textFade = extinctionProgress < 0.3f
                ? extinctionProgress / 0.3f
                : 1f;
            float textAlpha = alpha * flash * textFade;
            string warningText = ExtinctionProtocolWarning.Value;
            var font = FontAssets.MouseText.Value;
            Vector2 textSize = font.MeasureString(warningText) * 0.55f;
            Vector2 textPos = new(
                panelRect.X + (panelRect.Width - textSize.X) * 0.5f,
                panelRect.Bottom - 30f
            );

            Rectangle warnBg = new(panelRect.X + 6, panelRect.Bottom - 36, panelRect.Width - 12, 26);
            sb.Draw(pixel, warnBg, new Rectangle(0, 0, 1, 1), new Color(80, 10, 10) * (textAlpha * 0.5f));

            for (int g = 0; g < 4; g++) {
                float gAngle = MathHelper.TwoPi * g / 4f;
                Vector2 gOffset = new Vector2(MathF.Cos(gAngle), MathF.Sin(gAngle)) * 1.2f;
                Utils.DrawBorderString(sb, warningText, textPos + gOffset,
                    new Color(200, 50, 30) * (textAlpha * 0.4f), 0.55f);
            }
            Utils.DrawBorderString(sb, warningText, textPos, Color.White * textAlpha, 0.55f);
        }

        #endregion
    }
}
