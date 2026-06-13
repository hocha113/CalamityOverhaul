using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV.UIEffect;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.DialogueBoxs.Styles
{
    /// <summary>
    /// 硫磺火风格对话框
    /// </summary>
    internal class BrimstoneDialogueBox : DialogueBoxBase
    {
        public static BrimstoneDialogueBox Instance => UIHandleLoader.GetUIHandleOfType<BrimstoneDialogueBox>();
        public override string LocalizationCategory => "UI";

        // 风格参数
        private const float FixedWidth = 540f;
        private const int ShaderEdgePad = 16;
        protected override float PanelWidth => FixedWidth;

        // 火焰动画
        private float flameTimer = 0f;
        private float emberGlowTimer = 0f;
        private float heatWavePhase = 0f;
        private float infernoPulse = 0f;
        // 着色器单调时间，避免噪声跳变
        private float shaderTime = 0f;

        // 余烬与细灰粒子
        private readonly List<EmberPRT> embers = new();
        private int emberSpawnTimer = 0;
        private readonly List<AshPRT> ashes = new();
        private int ashSpawnTimer = 0;
        private const float ParticleSideMargin = 30f;

        #region 样式配置

        protected override float PortraitScaleMin => 0.82f;
        protected override float TopNameOffsetBase => 12f;
        protected override float TextBlockOffsetBase => 38f;
        protected override int NameGlowCount => 6;
        protected override float NameGlowRadius => 2.2f;

        protected override Color GetSilhouetteColor(ContentDrawContext ctx) => new Color(40, 10, 5) * 0.85f;

        protected override Color GetTextLineColor(ContentDrawContext ctx, int lineIndex) {
            return Color.Lerp(new Color(255, 220, 200), new Color(255, 240, 220), 0.4f) * ctx.ContentAlpha;
        }

        protected override Vector2 ApplyTextLineOffset(ContentDrawContext ctx, Vector2 basePosition, int lineIndex) {
            float heatWobble = (float)Math.Sin(heatWavePhase * 3f + lineIndex * 0.65f) * 0.9f;
            return basePosition + new Vector2(heatWobble, 0);
        }

        protected override void DrawTextLineGlow(ContentDrawContext ctx, string text, Vector2 position, int lineIndex) {
            Color textGlow = new Color(255, 150, 80) * (ctx.ContentAlpha * 0.15f);
            Utils.DrawBorderString(ctx.SpriteBatch, text, position + new Vector2(0, 1), textGlow, TextScale);
        }

        protected override string GetContinueHintText() => $"▶ {ContinueHint.Value} ◀";

        protected override Color GetContinueHintColor(ContentDrawContext ctx, float blink) {
            return new Color(255, 160, 90) * blink * ctx.ContentAlpha;
        }

        protected override Color GetFastHintColor(ContentDrawContext ctx) {
            return new Color(200, 140, 100) * 0.45f * ctx.ContentAlpha;
        }

        #endregion

        #region 绘制实现

        protected override void DrawPortraitFrame(ContentDrawContext ctx, Rectangle frameRect) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            float alpha = ctx.Alpha * ctx.PortraitData.Fade * ctx.PortraitExtraAlpha;

            Color back = new Color(20, 5, 5) * (alpha * 0.88f);
            ctx.SpriteBatch.Draw(vaule, frameRect, new Rectangle(0, 0, 1, 1), back);

            Color edge = new Color(200, 80, 40) * (alpha * 0.75f);
            ctx.SpriteBatch.Draw(vaule, new Rectangle(frameRect.X, frameRect.Y, frameRect.Width, 3), new Rectangle(0, 0, 1, 1), edge);
            ctx.SpriteBatch.Draw(vaule, new Rectangle(frameRect.X, frameRect.Bottom - 3, frameRect.Width, 3), new Rectangle(0, 0, 1, 1), edge * 0.7f);
            ctx.SpriteBatch.Draw(vaule, new Rectangle(frameRect.X, frameRect.Y, 3, frameRect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
            ctx.SpriteBatch.Draw(vaule, new Rectangle(frameRect.Right - 3, frameRect.Y, 3, frameRect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
        }

        protected override void DrawPortraitGlow(ContentDrawContext ctx, Rectangle glowRect) {
            var pd = ctx.PortraitData;
            float flamePulse = (float)Math.Sin(flameTimer * 1.8f + pd.Fade) * 0.5f + 0.5f;
            Color flameRim = new Color(255, 120, 60) * (ctx.ContentAlpha * 0.5f * flamePulse * pd.Fade) * ctx.PortraitExtraAlpha;
            DrawFlameGlow(ctx.SpriteBatch, glowRect, flameRim);
        }

        protected override void DrawNameGlow(ContentDrawContext ctx, Vector2 position, float alpha) {
            Color nameGlow = new Color(255, 140, 80) * alpha * 0.75f;
            for (int i = 0; i < NameGlowCount; i++) {
                float angle = MathHelper.TwoPi * i / NameGlowCount + flameTimer * 0.5f;
                Vector2 offset = angle.ToRotationVector2() * NameGlowRadius * ctx.SwitchEase;
                Utils.DrawBorderString(ctx.SpriteBatch, current.Speaker, position + offset, nameGlow * 0.5f, NameScale);
            }
        }

        protected override void DrawSpeakerName(ContentDrawContext ctx) {
            Vector2 speakerPos = GetSpeakerNamePosition(ctx);
            float nameAlpha = ctx.ContentAlpha * ctx.SwitchEase;

            DrawNameGlow(ctx, speakerPos, nameAlpha);
            Utils.DrawBorderString(ctx.SpriteBatch, current.Speaker, speakerPos, new Color(255, 240, 220) * nameAlpha, NameScale);

            Vector2 divStart = speakerPos + new Vector2(0, 28);
            Vector2 divEnd = divStart + new Vector2(ctx.PanelRect.Width - ctx.LeftOffset - Padding, 0);
            DrawDividerLine(ctx, divStart, divEnd, nameAlpha);
        }

        protected override void DrawDividerLine(ContentDrawContext ctx, Vector2 start, Vector2 end, float alpha) {
            DrawFlameGradientLine(ctx.SpriteBatch, start, end,
                new Color(220, 80, 40) * (alpha * 0.9f),
                new Color(120, 30, 15) * (alpha * 0.1f), 1.5f);
        }

        #endregion

        protected override void StyleUpdate(Vector2 panelPos, Vector2 panelSize) {
            flameTimer += 0.045f;
            emberGlowTimer += 0.038f;
            heatWavePhase += 0.025f;
            infernoPulse += 0.012f;
            shaderTime += 0.016f;
            if (flameTimer > MathHelper.TwoPi) flameTimer -= MathHelper.TwoPi;
            if (emberGlowTimer > MathHelper.TwoPi) emberGlowTimer -= MathHelper.TwoPi;
            if (heatWavePhase > MathHelper.TwoPi) heatWavePhase -= MathHelper.TwoPi;
            if (infernoPulse > MathHelper.TwoPi) infernoPulse -= MathHelper.TwoPi;
            //shader时间在远大于噪声循环周期的数值后再回绕
            if (shaderTime > 10000f) shaderTime -= 10000f;

            //火星:更稀疏更小,营造缓慢升腾的点状余烬
            emberSpawnTimer++;
            if (Active && emberSpawnTimer >= 18 && embers.Count < 14) {
                emberSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(panelPos.X + ParticleSideMargin, panelPos.X + panelSize.X - ParticleSideMargin);
                Vector2 startPos = new(xPos, panelPos.Y + panelSize.Y - 5f);
                var e = new EmberPRT(startPos) {
                    Size = Main.rand.NextFloat(1.1f, 2.0f),
                    RiseSpeed = Main.rand.NextFloat(0.25f, 0.6f),
                    Drift = Main.rand.NextFloat(-0.18f, 0.18f)
                };
                embers.Add(e);
            }
            for (int i = embers.Count - 1; i >= 0; i--) {
                if (embers[i].Update(panelPos, panelSize)) {
                    embers.RemoveAt(i);
                }
            }

            ashSpawnTimer++;
            if (Active && ashSpawnTimer >= 24 && ashes.Count < 14) {
                ashSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(panelPos.X + ParticleSideMargin, panelPos.X + panelSize.X - ParticleSideMargin);
                Vector2 startPos = new(xPos, panelPos.Y + panelSize.Y);
                ashes.Add(new AshPRT(startPos));
            }
            for (int i = ashes.Count - 1; i >= 0; i--) {
                if (ashes[i].Update(panelPos, panelSize)) {
                    ashes.RemoveAt(i);
                }
            }
        }

        protected override void DrawStyle(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, float contentAlpha, float easedProgress) {
            Texture2D vaule = VaultAsset.placeholder2.Value;

            //阴影:漂浮于屏幕的厚重感
            Rectangle shadow = panelRect;
            shadow.Offset(7, 9);
            spriteBatch.Draw(vaule, shadow, new Rectangle(0, 0, 1, 1), new Color(20, 0, 0) * (alpha * 0.65f));

            //专属着色器面板,降级时回退简化底色
            if (EffectLoader.BrimstoneDialogueBox?.Value != null) {
                DrawShaderPanel(spriteBatch, panelRect, alpha);
            }
            else {
                DrawFallbackPanel(spriteBatch, panelRect, alpha);
            }

            //CPU粒子叠层:只保留灰与火星,整体透明度降低
            foreach (var ash in ashes) {
                ash.Draw(spriteBatch, alpha * 0.55f);
            }
            foreach (var ember in embers) {
                ember.Draw(spriteBatch, alpha * 0.7f);
            }

            //定时对话进度指示器
            DrawTimedProgressIndicator(spriteBatch, panelRect, alpha);

            if (current == null || contentAlpha <= 0.01f) {
                return;
            }

            DrawPortraitAndText(spriteBatch, panelRect, alpha, contentAlpha);
        }

        #region 样式工具函数

        //使用BrimstoneDialogueBox着色器渲染面板底图
        private void DrawShaderPanel(SpriteBatch sb, Rectangle rect, float alpha) {
            Effect effect = EffectLoader.BrimstoneDialogueBox.Value;
            Rectangle extRect = rect;
            extRect.Inflate(ShaderEdgePad, ShaderEdgePad);

            //infernoPulse转换为0~1脉动,驱动火焰整体节拍
            float pulse01 = (float)Math.Sin(infernoPulse * 1.8f) * 0.5f + 0.5f;

            effect.Parameters["uTime"]?.SetValue(shaderTime);
            effect.Parameters["uAlpha"]?.SetValue(alpha * 0.97f);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(extRect.Width, extRect.Height));
            effect.Parameters["uEdgePad"]?.SetValue((float)ShaderEdgePad);
            effect.Parameters["uInfernoPulse"]?.SetValue(pulse01);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp, DepthStencilState.None,
                RasterizerState.CullNone, effect, Main.UIScaleMatrix);

            sb.Draw(VaultAsset.placeholder2.Value, extRect, Color.White);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }

        //降级背景:无shader环境沿用原CPU混合底色
        private void DrawFallbackPanel(SpriteBatch sb, Rectangle panelRect, float alpha) {
            Texture2D vaule = VaultAsset.placeholder2.Value;

            int segments = 35;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                Color brimstoneDeep = new Color(25, 5, 5);
                Color brimstoneMid = new Color(80, 15, 10);
                Color brimstoneHot = new Color(140, 35, 20);

                float breathing = (float)Math.Sin(infernoPulse * 1.5f) * 0.5f + 0.5f;
                float flameWave = (float)Math.Sin(flameTimer * 0.6f + t * 2.2f) * 0.5f + 0.5f;

                Color baseColor = Color.Lerp(brimstoneDeep, brimstoneMid, flameWave);
                Color finalColor = Color.Lerp(baseColor, brimstoneHot, t * 0.5f * (0.3f + breathing * 0.7f));
                finalColor *= alpha * 0.92f;

                sb.Draw(vaule, r, new Rectangle(0, 0, 1, 1), finalColor);
            }

            float glowPulse = (float)Math.Sin(emberGlowTimer * 1.5f) * 0.5f + 0.5f;
            Color outerEdge = Color.Lerp(new Color(180, 60, 30), new Color(255, 140, 70), glowPulse) * (alpha * 0.85f);
            sb.Draw(vaule, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge);
            sb.Draw(vaule, new Rectangle(panelRect.X, panelRect.Bottom - 3, panelRect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge * 0.75f);
            sb.Draw(vaule, new Rectangle(panelRect.X, panelRect.Y, 3, panelRect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);
            sb.Draw(vaule, new Rectangle(panelRect.Right - 3, panelRect.Y, 3, panelRect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);
        }

        private static void DrawFlameGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color startColor, Color endColor, float thickness) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) {
                return;
            }
            edge.Normalize();
            float rotation = (float)Math.Atan2(edge.Y, edge.X);
            int segments = Math.Max(1, (int)(length / 10f));
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                Vector2 segPos = start + edge * (length * t);
                float segLength = length / segments;
                Color color = Color.Lerp(startColor, endColor, t);
                spriteBatch.Draw(vaule, segPos, new Rectangle(0, 0, 1, 1), color, rotation, new Vector2(0, 0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0);
            }
        }

        private static void DrawFlameGlow(SpriteBatch sb, Rectangle rect, Color glow) {
            Texture2D vaule = VaultAsset.placeholder2.Value;

            sb.Draw(vaule, rect, new Rectangle(0, 0, 1, 1), glow * 0.2f);

            int border = 2;
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, rect.Width, border), new Rectangle(0, 0, 1, 1), glow * 0.7f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Bottom - border, rect.Width, border), new Rectangle(0, 0, 1, 1), glow * 0.5f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, border, rect.Height), new Rectangle(0, 0, 1, 1), glow * 0.6f);
            sb.Draw(vaule, new Rectangle(rect.Right - border, rect.Y, border, rect.Height), new Rectangle(0, 0, 1, 1), glow * 0.6f);
        }
        #endregion
    }
}
