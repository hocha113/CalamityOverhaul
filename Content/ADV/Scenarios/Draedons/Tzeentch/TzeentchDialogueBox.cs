using CalamityOverhaul.Content.ADV.DialogueBoxs;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Tzeentch
{
    /// <summary>
    /// 奸奇风格对话框
    /// </summary>
    internal class TzeentchDialogueBox : DialogueBoxBase
    {
        public static TzeentchDialogueBox Instance => UIHandleLoader.GetUIHandleOfType<TzeentchDialogueBox>();
        public override string LocalizationCategory => "UI";

        //风格参数
        private const float FixedWidth = 560f;
        protected override float PanelWidth => FixedWidth;

        //奸奇魔法动画参数
        private float warpTimer = 0f;//现实扭曲计时器
        private float changeFlux = 0f;//变化涌动计时器
        private float arcanePhase = 0f;//奥术相位
        private float fateWeave = 0f;//命运编织
        private float colorShift = 0f;//色彩变换
        private float chaosRipple = 0f;//混沌涟漪

        //粒子系统
        private readonly List<WarpFlame> warpFlames = [];//扭曲火焰
        private int warpFlameSpawnTimer = 0;
        private readonly List<MysticRune> mysticRunes = [];//神秘符文
        private int runeSpawnTimer = 0;
        private readonly List<FateThread> fateThreads = [];//命运丝线
        private int threadSpawnTimer = 0;
        private readonly List<ArcaneOrb> arcaneOrbs = [];//奥术法球
        private int orbSpawnTimer = 0;
        private const float ParticleSideMargin = 35f;

        protected override void StyleUpdate(Vector2 panelPos, Vector2 panelSize) {
            //奸奇的时间是扭曲的，速度不一
            warpTimer += 0.062f + (float)Math.Sin(changeFlux * 0.3f) * 0.015f;
            changeFlux += 0.041f;
            arcanePhase += 0.038f + (float)Math.Cos(warpTimer * 0.5f) * 0.012f;
            fateWeave += 0.028f;
            colorShift += 0.052f;
            chaosRipple += 0.033f;

            if (warpTimer > MathHelper.TwoPi) warpTimer -= MathHelper.TwoPi;
            if (changeFlux > MathHelper.TwoPi) changeFlux -= MathHelper.TwoPi;
            if (arcanePhase > MathHelper.TwoPi) arcanePhase -= MathHelper.TwoPi;
            if (fateWeave > MathHelper.TwoPi) fateWeave -= MathHelper.TwoPi;
            if (colorShift > MathHelper.TwoPi) colorShift -= MathHelper.TwoPi;
            if (chaosRipple > MathHelper.TwoPi) chaosRipple -= MathHelper.TwoPi;

            //扭曲火焰生成
            warpFlameSpawnTimer++;
            if (Active && warpFlameSpawnTimer >= 10 && warpFlames.Count < 30) {
                warpFlameSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(panelPos.X + ParticleSideMargin, panelPos.X + panelSize.X - ParticleSideMargin);
                Vector2 startPos = new(xPos, panelPos.Y + panelSize.Y - 8f);
                warpFlames.Add(new WarpFlame(startPos));
            }
            for (int i = warpFlames.Count - 1; i >= 0; i--) {
                if (warpFlames[i].Update(panelPos, panelSize)) {
                    warpFlames.RemoveAt(i);
                }
            }

            //神秘符文生成
            runeSpawnTimer++;
            if (Active && runeSpawnTimer >= 60 && mysticRunes.Count < 6) {
                runeSpawnTimer = 0;
                Vector2 startPos = new(
                    Main.rand.NextFloat(panelPos.X + 50f, panelPos.X + panelSize.X - 50f),
                    Main.rand.NextFloat(panelPos.Y + 70f, panelPos.Y + panelSize.Y - 70f)
                );
                mysticRunes.Add(new MysticRune(startPos));
            }
            for (int i = mysticRunes.Count - 1; i >= 0; i--) {
                if (mysticRunes[i].Update(panelPos, panelSize)) {
                    mysticRunes.RemoveAt(i);
                }
            }

            //命运丝线生成
            threadSpawnTimer++;
            if (Active && threadSpawnTimer >= 35 && fateThreads.Count < 12) {
                threadSpawnTimer = 0;
                Vector2 startPos = new(
                    Main.rand.NextFloat(panelPos.X + 40f, panelPos.X + panelSize.X - 40f),
                    Main.rand.NextFloat(panelPos.Y + 50f, panelPos.Y + panelSize.Y - 50f)
                );
                fateThreads.Add(new FateThread(startPos));
            }
            for (int i = fateThreads.Count - 1; i >= 0; i--) {
                if (fateThreads[i].Update(panelPos, panelSize)) {
                    fateThreads.RemoveAt(i);
                }
            }

            //奥术法球生成
            orbSpawnTimer++;
            if (Active && orbSpawnTimer >= 45 && arcaneOrbs.Count < 8) {
                orbSpawnTimer = 0;
                Vector2 startPos = new(
                    Main.rand.NextFloat(panelPos.X + 60f, panelPos.X + panelSize.X - 60f),
                    Main.rand.NextFloat(panelPos.Y + 60f, panelPos.Y + panelSize.Y - 60f)
                );
                arcaneOrbs.Add(new ArcaneOrb(startPos));
            }
            for (int i = arcaneOrbs.Count - 1; i >= 0; i--) {
                if (arcaneOrbs[i].Update(panelPos, panelSize)) {
                    arcaneOrbs.RemoveAt(i);
                }
            }
        }

        protected override void DrawStyle(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, float contentAlpha, float easedProgress) {
            Texture2D vaule = VaultAsset.placeholder2.Value;

            //扭曲的阴影效果
            Rectangle shadow = panelRect;
            shadow.Offset(8, 10);
            float shadowWarp = (float)Math.Sin(warpTimer * 1.5f) * 3f;
            shadow.Offset((int)shadowWarp, 0);
            spriteBatch.Draw(vaule, shadow, new Rectangle(0, 0, 1, 1), new Color(20, 0, 40) * (alpha * 0.7f));

            //炫彩渐变背景
            int segments = 40;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                //奸奇标志性的蓝紫粉色调
                float colorPhase = colorShift + t * MathHelper.Pi;
                Color tzeentchBlue = new Color(20, 50, 180);      //深蓝
                Color tzeentchPurple = new Color(140, 40, 200);   //紫色
                Color tzeentchPink = new Color(255, 80, 200);     //粉色
                Color tzeentchCyan = new Color(80, 200, 255);     //青色

                //变化之神的色彩永不固定
                float shift1 = (float)Math.Sin(colorPhase) * 0.5f + 0.5f;
                float shift2 = (float)Math.Sin(colorPhase * 1.3f + changeFlux) * 0.5f + 0.5f;
                float shift3 = (float)Math.Sin(colorPhase * 0.7f + arcanePhase) * 0.5f + 0.5f;

                Color c1 = Color.Lerp(tzeentchBlue, tzeentchPurple, shift1);
                Color c2 = Color.Lerp(tzeentchPurple, tzeentchPink, shift2);
                Color c3 = Color.Lerp(tzeentchCyan, tzeentchBlue, shift3);

                Color baseColor = Color.Lerp(c1, c2, t);
                Color finalColor = Color.Lerp(baseColor, c3, (float)Math.Sin(t * MathHelper.Pi) * 0.4f);
                finalColor *= alpha * 0.88f;

                spriteBatch.Draw(vaule, r, new Rectangle(0, 0, 1, 1), finalColor);
            }

            //变化脉冲叠加层
            float changePulse = (float)Math.Sin(changeFlux * 2.1f) * 0.5f + 0.5f;
            Color pulseOverlay = new Color(160, 80, 255) * (alpha * 0.18f * changePulse);
            spriteBatch.Draw(vaule, panelRect, new Rectangle(0, 0, 1, 1), pulseOverlay);

            //绘制现实扭曲波纹
            DrawWarpRipples(spriteBatch, panelRect, alpha * 0.85f);

            //绘制混沌几何图案
            DrawChaosGeometry(spriteBatch, panelRect, alpha * 0.75f);

            //内发光 - 奥术能量
            float arcanePulse = (float)Math.Sin(arcanePhase * 1.7f) * 0.5f + 0.5f;
            Rectangle inner = panelRect;
            inner.Inflate(-8, -8);
            Color arcaneGlow = Color.Lerp(new Color(100, 150, 255), new Color(200, 100, 255), arcanePulse);
            spriteBatch.Draw(vaule, inner, new Rectangle(0, 0, 1, 1), arcaneGlow * (alpha * 0.15f * arcanePulse));

            //绘制奸奇魔法边框
            DrawTzeentchFrame(spriteBatch, panelRect, alpha, arcanePulse);

            //绘制粒子，从后到前
            foreach (var thread in fateThreads) {
                thread.Draw(spriteBatch, alpha * 0.7f);
            }
            foreach (var rune in mysticRunes) {
                rune.Draw(spriteBatch, alpha * 0.85f);
            }
            foreach (var orb in arcaneOrbs) {
                orb.Draw(spriteBatch, alpha * 0.9f);
            }
            foreach (var flame in warpFlames) {
                flame.Draw(spriteBatch, alpha * 0.95f);
            }

            if (current == null || contentAlpha <= 0.01f) {
                return;
            }

            DrawPortraitAndText(spriteBatch, panelRect, alpha, contentAlpha);
        }

        private void DrawPortraitAndText(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, float contentAlpha) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            bool hasPortrait = false;
            PortraitData speakerPortrait = null;
            if (current != null && !string.IsNullOrEmpty(current.Speaker) && portraits.TryGetValue(current.Speaker, out var pd) && pd.Texture != null && pd.Fade > 0.02f) {
                hasPortrait = true;
                speakerPortrait = pd;
            }

            float switchEase = speakerSwitchProgress;
            if (switchEase < 1f) {
                switchEase = CWRUtils.EaseOutCubic(switchEase);
            }
            float portraitAppearScale = MathHelper.Lerp(0.75f, 1f, switchEase);
            float portraitExtraAlpha = MathHelper.Clamp(switchEase, 0f, 1f);

            float leftOffset = Padding;
            float topNameOffset = 12f;
            float textBlockOffsetY = Padding + 40;

            if (hasPortrait) {
                float availHeight = panelRect.Height - 65f;
                float maxPortraitHeight = Math.Clamp(availHeight, 90f, 280f);
                Texture2D ptex = speakerPortrait.Texture;
                float scaleBase = Math.Min(PortraitWidth / ptex.Width, maxPortraitHeight / ptex.Height);
                float scale = scaleBase * portraitAppearScale;
                Vector2 pSize = ptex.Size() * scale;

                //立绘位置添加扭曲效果
                float warpOffsetX = (float)Math.Sin(warpTimer * 1.3f + speakerPortrait.Fade) * 2f;
                float warpOffsetY = (float)Math.Cos(warpTimer * 0.9f + speakerPortrait.Fade) * 1.5f;
                Vector2 pPos = new(
                    panelRect.X + Padding + PortraitInnerPadding + warpOffsetX,
                    panelRect.Y + panelRect.Height - pSize.Y - Padding - 14f + warpOffsetY
                );

                DrawTzeentchPortraitFrame(spriteBatch,
                    new Rectangle((int)(pPos.X - 10), (int)(pPos.Y - 10), (int)(pSize.X + 20), (int)(pSize.Y + 20)),
                    alpha * speakerPortrait.Fade * portraitExtraAlpha);

                Color drawColor = speakerPortrait.BaseColor * contentAlpha * speakerPortrait.Fade * portraitExtraAlpha;
                if (speakerPortrait.Silhouette) {
                    //奸奇的剪影带有魔法色彩
                    Color tzeentchShadow = Color.Lerp(new Color(40, 20, 80), new Color(80, 40, 120), (float)Math.Sin(colorShift) * 0.5f + 0.5f);
                    drawColor = tzeentchShadow * (contentAlpha * speakerPortrait.Fade * portraitExtraAlpha) * 0.85f;
                }

                spriteBatch.Draw(ptex, pPos, null, drawColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

                //魔法光环效果
                float magicPulse = (float)Math.Sin(arcanePhase * 1.9f + speakerPortrait.Fade) * 0.5f + 0.5f;
                Color magicRim = Color.Lerp(new Color(150, 100, 255), new Color(255, 150, 255), magicPulse);
                magicRim *= contentAlpha * 0.6f * magicPulse * speakerPortrait.Fade * portraitExtraAlpha;
                DrawMagicGlow(spriteBatch,
                    new Rectangle((int)pPos.X - 6, (int)pPos.Y - 6, (int)pSize.X + 12, (int)pSize.Y + 12),
                    magicRim);

                leftOffset += PortraitWidth + 24f;
            }

            //绘制说话者名字
            if (current != null && !string.IsNullOrEmpty(current.Speaker)) {
                float nameWarp = (float)Math.Sin(warpTimer * 1.1f) * 3f;
                Vector2 speakerPos = new(
                    panelRect.X + leftOffset + nameWarp,
                    panelRect.Y + topNameOffset - (1f - switchEase) * 8f
                );
                float nameAlpha = contentAlpha * switchEase;

                //奸奇魔法文字光晕
                Color nameGlow1 = Color.Lerp(new Color(150, 100, 255), new Color(255, 150, 255), (float)Math.Sin(colorShift) * 0.5f + 0.5f);
                Color nameGlow2 = Color.Lerp(new Color(100, 200, 255), new Color(200, 100, 255), (float)Math.Sin(colorShift + MathHelper.PiOver2) * 0.5f + 0.5f);

                for (int i = 0; i < 8; i++) {
                    float angle = MathHelper.TwoPi * i / 8f + arcanePhase * 0.5f;
                    Vector2 offset = angle.ToRotationVector2() * 2.5f * switchEase;
                    Color glowColor = Color.Lerp(nameGlow1, nameGlow2, i / 8f) * (nameAlpha * 0.5f);
                    Utils.DrawBorderString(spriteBatch, current.Speaker, speakerPos + offset, glowColor, 0.95f);
                }

                Utils.DrawBorderString(spriteBatch, current.Speaker, speakerPos, Color.White * nameAlpha, 0.95f);

                //魔法分隔线
                Vector2 divStart = speakerPos + new Vector2(0, 30);
                Vector2 divEnd = divStart + new Vector2(panelRect.Width - leftOffset - Padding, 0);
                float lineAlpha = contentAlpha * switchEase;
                DrawMagicGradientLine(spriteBatch, divStart, divEnd,
                    Color.Lerp(new Color(150, 100, 255), new Color(255, 150, 255), (float)Math.Sin(colorShift) * 0.5f + 0.5f) * (lineAlpha * 0.9f),
                    Color.Lerp(new Color(100, 150, 255), new Color(200, 100, 255), (float)Math.Sin(colorShift + 1f) * 0.5f + 0.5f) * (lineAlpha * 0.1f),
                    1.8f);
            }

            //绘制文本
            Vector2 textStart = new(panelRect.X + leftOffset, panelRect.Y + textBlockOffsetY);
            int remaining = visibleCharCount;
            int lineHeight = (int)(font.MeasureString("A").Y * 0.8f) + LineSpacing;
            int maxLines = (int)((panelRect.Height - (textStart.Y - panelRect.Y) - Padding) / lineHeight);

            for (int i = 0; i < wrappedLines.Length && i < maxLines; i++) {
                string fullLine = wrappedLines[i];
                if (string.IsNullOrEmpty(fullLine)) {
                    continue;
                }

                string visLine;
                if (finishedCurrent) {
                    visLine = fullLine;
                }
                else {
                    if (remaining <= 0) {
                        break;
                    }
                    int take = Math.Min(fullLine.Length, remaining);
                    visLine = fullLine[..take];
                    remaining -= take;
                }

                Vector2 linePos = textStart + new Vector2(0, i * lineHeight);
                if (linePos.Y + lineHeight > panelRect.Bottom - Padding) {
                    break;
                }

                //现实扭曲效果 - 文字会微微扭曲
                float warpX = (float)Math.Sin(warpTimer * 2.5f + i * 0.8f) * 1.5f;
                float warpY = (float)Math.Cos(warpTimer * 1.8f + i * 0.6f) * 0.8f;
                Vector2 warpPos = linePos + new Vector2(warpX, warpY);

                //魔法色彩文字
                float colorPhase = colorShift + i * 0.3f;
                Color textColor1 = Color.Lerp(new Color(220, 200, 255), new Color(255, 220, 255), (float)Math.Sin(colorPhase) * 0.5f + 0.5f);
                Color textColor2 = Color.Lerp(new Color(200, 220, 255), new Color(240, 200, 255), (float)Math.Sin(colorPhase + 1f) * 0.5f + 0.5f);
                Color lineColor = Color.Lerp(textColor1, textColor2, 0.5f) * contentAlpha;

                //微弱的魔法光晕
                Color textGlow = Color.Lerp(new Color(180, 140, 255), new Color(255, 180, 255), (float)Math.Sin(arcanePhase + i * 0.4f) * 0.5f + 0.5f);
                Utils.DrawBorderString(spriteBatch, visLine, warpPos + new Vector2(0, 1), textGlow * (contentAlpha * 0.12f), 0.82f);
                Utils.DrawBorderString(spriteBatch, visLine, warpPos, lineColor, 0.82f);
            }

            //继续提示
            if (waitingForAdvance) {
                float blink = (float)Math.Sin(advanceBlinkTimer / 9f * MathHelper.TwoPi) * 0.5f + 0.5f;
                string hint = $"◆ {ContinueHint.Value} ◆";
                Vector2 hintSize = font.MeasureString(hint) * 0.68f;
                Vector2 hintPos = new(panelRect.Right - Padding - hintSize.X, panelRect.Bottom - Padding - hintSize.Y);

                Color hintColor1 = new Color(200, 150, 255) * blink;
                Color hintColor2 = new Color(255, 150, 255) * blink;
                Color hintGlow = Color.Lerp(hintColor1, hintColor2, (float)Math.Sin(colorShift * 2f) * 0.5f + 0.5f) * contentAlpha;
                Utils.DrawBorderString(spriteBatch, hint, hintPos, hintGlow, 0.88f);
            }

            //加速提示
            if (!finishedCurrent) {
                string fast = FastHint.Value;
                Vector2 fastSize = font.MeasureString(fast) * 0.62f;
                Vector2 fastPos = new(panelRect.Right - Padding - fastSize.X, panelRect.Bottom - Padding - fastSize.Y - 20);
                Color fastColor = Color.Lerp(new Color(180, 160, 220), new Color(220, 180, 240), (float)Math.Sin(colorShift) * 0.5f + 0.5f);
                Utils.DrawBorderString(spriteBatch, fast, fastPos, fastColor * (0.4f * contentAlpha), 0.72f);
            }
        }

        #region 样式工具函数
        private void DrawWarpRipples(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            int rippleCount = 9;

            for (int i = 0; i < rippleCount; i++) {
                float t = i / (float)rippleCount;
                float baseY = rect.Y + 30 + t * (rect.Height - 60);

                //现实扭曲幅度
                float amplitude = 8f + (float)Math.Sin((chaosRipple + t * 1.5f) * 2.8f) * 5f;
                float thickness = 2.2f;
                float phase = warpTimer * 2.5f + t * MathHelper.TwoPi;

                int segments = 60;
                Vector2 prevPoint = Vector2.Zero;

                for (int s = 0; s <= segments; s++) {
                    float progress = s / (float)segments;
                    float localPhase = phase + progress * MathHelper.TwoPi * 2.3f;
                    float warpY = baseY + (float)Math.Sin(localPhase) * amplitude;

                    //添加次级扭曲
                    warpY += (float)Math.Cos(localPhase * 1.7f + changeFlux) * (amplitude * 0.3f);

                    Vector2 point = new(rect.X + 15 + progress * (rect.Width - 30), warpY);

                    if (s > 0) {
                        Vector2 diff = point - prevPoint;
                        float len = diff.Length();
                        if (len > 0.01f) {
                            float rot = diff.ToRotation();

                            //变幻的色彩
                            float colorPhase = colorShift + progress + t;
                            Color c1 = new Color(100, 150, 255);
                            Color c2 = new Color(200, 100, 255);
                            Color waveColor = Color.Lerp(c1, c2, (float)Math.Sin(colorPhase * MathHelper.Pi) * 0.5f + 0.5f);
                            waveColor *= alpha * 0.12f;

                            sb.Draw(vaule, prevPoint, new Rectangle(0, 0, 1, 1), waveColor, rot, Vector2.Zero, new Vector2(len, thickness), SpriteEffects.None, 0f);
                        }
                    }
                    prevPoint = point;
                }
            }
        }

        private void DrawChaosGeometry(SpriteBatch sb, Rectangle rect, float alpha) {
            //绘制变幻的几何线条
            int lineCount = 12;
            for (int i = 0; i < lineCount; i++) {
                float t = i / (float)lineCount;
                float angle = t * MathHelper.TwoPi + fateWeave;
                float distance = 40f + (float)Math.Sin(arcanePhase + t * MathHelper.TwoPi) * 15f;

                Vector2 center = new Vector2(rect.X + rect.Width * 0.5f, rect.Y + rect.Height * 0.5f);
                Vector2 p1 = center + angle.ToRotationVector2() * distance;
                Vector2 p2 = center + (angle + MathHelper.Pi).ToRotationVector2() * distance;

                float colorPhase = colorShift + t;
                Color lineColor = Color.Lerp(
                    new Color(100, 150, 255),
                    new Color(200, 100, 255),
                    (float)Math.Sin(colorPhase * MathHelper.Pi) * 0.5f + 0.5f
                ) * (alpha * 0.06f);

                DrawLine(sb, p1, p2, lineColor, 1.5f);
            }
        }

        private static void DrawTzeentchFrame(SpriteBatch sb, Rectangle rect, float alpha, float pulse) {
            Texture2D vaule = VaultAsset.placeholder2.Value;

            //外框 - 变幻的魔法色彩
            float colorPhase = (float)(Main.timeForVisualEffects * 0.05f);
            Color c1 = new Color(150, 100, 255);
            Color c2 = new Color(255, 150, 255);
            Color c3 = new Color(100, 200, 255);

            Color outerEdge = Color.Lerp(
                Color.Lerp(c1, c2, pulse),
                c3,
                (float)Math.Sin(colorPhase * MathHelper.TwoPi) * 0.5f + 0.5f
            ) * (alpha * 0.9f);

            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, rect.Width, 4), new Rectangle(0, 0, 1, 1), outerEdge);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Bottom - 4, rect.Width, 4), new Rectangle(0, 0, 1, 1), outerEdge * 0.75f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, 4, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);
            sb.Draw(vaule, new Rectangle(rect.Right - 4, rect.Y, 4, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);

            //内框发光
            Rectangle inner = rect;
            inner.Inflate(-7, -7);
            Color innerGlow = Color.Lerp(c2, c1, pulse) * (alpha * 0.25f * pulse);
            sb.Draw(vaule, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow);
            sb.Draw(vaule, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow * 0.7f);
            sb.Draw(vaule, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);
            sb.Draw(vaule, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);

            //角落魔法符号
            DrawTzeentchSymbol(sb, new Vector2(rect.X + 14, rect.Y + 14), alpha * 0.95f, colorPhase);
            DrawTzeentchSymbol(sb, new Vector2(rect.Right - 14, rect.Y + 14), alpha * 0.95f, colorPhase + 0.5f);
            DrawTzeentchSymbol(sb, new Vector2(rect.X + 14, rect.Bottom - 14), alpha * 0.65f, colorPhase + 1f);
            DrawTzeentchSymbol(sb, new Vector2(rect.Right - 14, rect.Bottom - 14), alpha * 0.65f, colorPhase + 1.5f);
        }

        private static void DrawTzeentchSymbol(SpriteBatch sb, Vector2 pos, float alpha, float phase) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            float size = 7f;

            //变化之神的符号 - 多重旋转的十字
            Color symbolColor = Color.Lerp(
                new Color(200, 150, 255),
                new Color(255, 150, 255),
                (float)Math.Sin(phase * MathHelper.TwoPi) * 0.5f + 0.5f
            ) * alpha;

            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.PiOver2 * i + phase;
                sb.Draw(vaule, pos, new Rectangle(0, 0, 1, 1), symbolColor, angle,
                    new Vector2(0.5f, 0.5f), new Vector2(size * 1.4f, size * 0.35f), SpriteEffects.None, 0f);
            }

            //中心点
            sb.Draw(vaule, pos, new Rectangle(0, 0, 1, 1), symbolColor * 0.8f, 0f,
                new Vector2(0.5f, 0.5f), new Vector2(size * 0.5f, size * 0.5f), SpriteEffects.None, 0f);
        }

        private static void DrawMagicGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color startColor, Color endColor, float thickness) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) {
                return;
            }

            edge.Normalize();
            float rotation = (float)Math.Atan2(edge.Y, edge.X);
            int segments = Math.Max(1, (int)(length / 12f));

            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                Vector2 segPos = start + edge * (length * t);
                float segLength = length / segments;
                Color color = Color.Lerp(startColor, endColor, t);
                spriteBatch.Draw(vaule, segPos, new Rectangle(0, 0, 1, 1), color, rotation,
                    new Vector2(0, 0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0);
            }
        }

        private static void DrawLine(SpriteBatch sb, Vector2 start, Vector2 end, Color color, float thickness) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) {
                return;
            }

            edge.Normalize();
            float rotation = (float)Math.Atan2(edge.Y, edge.X);
            sb.Draw(vaule, start, new Rectangle(0, 0, 1, 1), color, rotation,
                new Vector2(0, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0);
        }
        #endregion

        #region 粒子内部类
        private class WarpFlame(Vector2 start)
        {
            public Vector2 Pos = start;
            public float Size = Main.rand.NextFloat(3f, 6.5f);
            public float RiseSpeed = Main.rand.NextFloat(0.3f, 0.9f);
            public float Drift = Main.rand.NextFloat(-0.3f, 0.3f);
            public float Life = 0f;
            public float MaxLife = Main.rand.NextFloat(80f, 150f);
            public float Seed = Main.rand.NextFloat(10f);
            public float Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            public float RotationSpeed = Main.rand.NextFloat(-0.08f, 0.08f);
            public float ColorPhase = Main.rand.NextFloat(MathHelper.TwoPi);

            public bool Update(Vector2 panelPos, Vector2 panelSize) {
                Life++;
                float t = Life / MaxLife;

                //扭曲的上升轨迹
                Pos.Y -= RiseSpeed * (0.8f + (float)Math.Sin(t * MathHelper.Pi) * 0.4f);
                Pos.X += (float)Math.Sin(Life * 0.08f + Seed) * Drift;
                Rotation += RotationSpeed;
                ColorPhase += 0.05f;

                if (Life >= MaxLife || Pos.Y < panelPos.Y + 20f) {
                    return true;
                }
                return false;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                Texture2D vaule = VaultAsset.placeholder2.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * Math.PI);
                float scale = Size * (1f + (float)Math.Sin((Life + Seed * 25f) * 0.15f) * 0.2f);

                //奸奇魔法火焰颜色：蓝紫粉混合
                Color c1 = new Color(100, 150, 255);
                Color c2 = new Color(200, 100, 255);
                Color c3 = new Color(255, 150, 255);

                Color flameCore = Color.Lerp(
                    Color.Lerp(c1, c2, (float)Math.Sin(ColorPhase) * 0.5f + 0.5f),
                    c3,
                    t
                ) * (alpha * 0.9f * fade);

                Color flameGlow = Color.Lerp(c2, c1, t) * (alpha * 0.5f * fade);

                //外层光晕
                sb.Draw(vaule, Pos, new Rectangle(0, 0, 1, 1), flameGlow, 0f,
                    new Vector2(0.5f, 0.5f), scale * 2.8f, SpriteEffects.None, 0f);
                //内层核心
                sb.Draw(vaule, Pos, new Rectangle(0, 0, 1, 1), flameCore, Rotation,
                    new Vector2(0.5f, 0.5f), scale, SpriteEffects.None, 0f);
            }
        }

        private class MysticRune(Vector2 start)
        {
            public Vector2 Pos = start;
            public float Size = Main.rand.NextFloat(10f, 18f);
            public float Life = 0f;
            public float MaxLife = Main.rand.NextFloat(150f, 250f);
            public float Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            public float RotationSpeed = Main.rand.NextFloat(-0.03f, 0.03f);
            public float Seed = Main.rand.NextFloat(10f);
            public float Phase = Main.rand.NextFloat(MathHelper.TwoPi);

            public bool Update(Vector2 panelPos, Vector2 panelSize) {
                Life++;
                Rotation += RotationSpeed;
                Phase += 0.04f;

                //轻微漂浮
                Vector2 drift = new Vector2(
                    (float)Math.Sin(Phase + Seed) * 0.3f,
                    (float)Math.Cos(Phase * 1.2f + Seed * 1.3f) * 0.25f
                );
                Pos += drift;

                if (Life >= MaxLife) {
                    return true;
                }
                return false;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                Texture2D vaule = VaultAsset.placeholder2.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * Math.PI);
                float pulse = (float)Math.Sin(Phase) * 0.5f + 0.5f;

                //符文颜色
                Color runeColor = Color.Lerp(
                    new Color(150, 100, 255),
                    new Color(255, 150, 255),
                    pulse
                ) * (alpha * 0.7f * fade);

                //绘制神秘符文（简化的九芒星）
                for (int i = 0; i < 9; i++) {
                    float angle = MathHelper.TwoPi * i / 9f + Rotation;
                    Vector2 offset = angle.ToRotationVector2() * (Size * 0.4f);
                    sb.Draw(vaule, Pos + offset, new Rectangle(0, 0, 1, 1), runeColor * 0.6f, 0f,
                        new Vector2(0.5f, 0.5f), new Vector2(2f, 2f), SpriteEffects.None, 0f);
                }

                //中心
                sb.Draw(vaule, Pos, new Rectangle(0, 0, 1, 1), runeColor, Rotation,
                    new Vector2(0.5f, 0.5f), new Vector2(Size * 0.3f, Size * 0.3f), SpriteEffects.None, 0f);
            }
        }

        private class FateThread(Vector2 start)
        {
            public Vector2 Pos = start;
            public Vector2 Velocity = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.NextFloat(0.2f, 0.6f);
            public float Life = 0f;
            public float MaxLife = Main.rand.NextFloat(100f, 180f);
            public float Seed = Main.rand.NextFloat(10f);
            public float Length = Main.rand.NextFloat(15f, 35f);
            public float Phase = Main.rand.NextFloat(MathHelper.TwoPi);

            public bool Update(Vector2 panelPos, Vector2 panelSize) {
                Life++;
                Phase += 0.06f;

                //命运丝线的飘动
                Vector2 drift = new Vector2(
                    (float)Math.Sin(Phase + Seed) * 0.4f,
                    (float)Math.Cos(Phase * 1.4f + Seed * 1.2f) * 0.3f
                );
                Pos += Velocity * 0.5f + drift;

                if (Life >= MaxLife) {
                    return true;
                }
                return false;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                Texture2D vaule = VaultAsset.placeholder2.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * Math.PI);

                //丝线颜色
                Color threadColor = Color.Lerp(
                    new Color(120, 180, 255),
                    new Color(200, 150, 255),
                    (float)Math.Sin(Phase) * 0.5f + 0.5f
                ) * (alpha * 0.5f * fade);

                Vector2 end = Pos + Velocity.SafeNormalize(Vector2.Zero) * Length;
                DrawLine(sb, Pos, end, threadColor, 1.2f);
            }
        }

        private class ArcaneOrb(Vector2 start)
        {
            public Vector2 Pos = start;
            public Vector2 Velocity = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.NextFloat(0.25f, 0.7f);
            public float Size = Main.rand.NextFloat(6f, 12f);
            public float Life = 0f;
            public float MaxLife = Main.rand.NextFloat(120f, 200f);
            public float Seed = Main.rand.NextFloat(10f);
            public float Phase = Main.rand.NextFloat(MathHelper.TwoPi);

            public bool Update(Vector2 panelPos, Vector2 panelSize) {
                Life++;
                Phase += 0.08f;

                //法球漂浮
                Vector2 drift = new Vector2(
                    (float)Math.Sin(Phase + Seed) * 0.5f,
                    (float)Math.Cos(Phase * 1.5f + Seed * 1.4f) * 0.4f
                );
                Pos += Velocity + drift;

                //边界反弹
                if (Pos.X < panelPos.X + 30f || Pos.X > panelPos.X + panelSize.X - 30f) {
                    Velocity.X *= -0.8f;
                }
                if (Pos.Y < panelPos.Y + 50f || Pos.Y > panelPos.Y + panelSize.Y - 50f) {
                    Velocity.Y *= -0.8f;
                }

                if (Life >= MaxLife) {
                    return true;
                }
                return false;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                Texture2D vaule = VaultAsset.placeholder2.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * Math.PI);
                float pulse = (float)Math.Sin(Phase * 2f) * 0.5f + 0.5f;
                float scale = Size * (0.7f + pulse * 0.5f);

                //法球颜色
                Color orbCore = Color.Lerp(
                    new Color(180, 150, 255),
                    new Color(255, 180, 255),
                    pulse
                ) * (alpha * 0.75f * fade);

                Color orbGlow = Color.Lerp(
                    new Color(120, 180, 255),
                    new Color(200, 150, 255),
                    pulse
                ) * (alpha * 0.4f * fade);

                //外层光晕
                sb.Draw(vaule, Pos, new Rectangle(0, 0, 1, 1), orbGlow, 0f,
                    new Vector2(0.5f, 0.5f), scale * 3.5f, SpriteEffects.None, 0f);
                //内层核心
                sb.Draw(vaule, Pos, new Rectangle(0, 0, 1, 1), orbCore, 0f,
                    new Vector2(0.5f, 0.5f), scale * 1.5f, SpriteEffects.None, 0f);
                //最内层
                sb.Draw(vaule, Pos, new Rectangle(0, 0, 1, 1), Color.White * (alpha * 0.5f * fade), 0f,
                    new Vector2(0.5f, 0.5f), scale * 0.5f, SpriteEffects.None, 0f);
            }
        }
        #endregion

        #region 公共静态绘制函数
        private static void DrawTzeentchPortraitFrame(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D vaule = VaultAsset.placeholder2.Value;

            //深色魔法背景
            Color back = Color.Lerp(new Color(20, 10, 40), new Color(40, 20, 60), (float)Math.Sin(Main.timeForVisualEffects * 0.03f) * 0.5f + 0.5f);
            sb.Draw(vaule, rect, new Rectangle(0, 0, 1, 1), back * (alpha * 0.9f));

            //变幻的魔法边框
            float colorPhase = (float)(Main.timeForVisualEffects * 0.05f);
            Color edge = Color.Lerp(
                new Color(150, 100, 255),
                new Color(255, 150, 255),
                (float)Math.Sin(colorPhase * MathHelper.TwoPi) * 0.5f + 0.5f
            ) * (alpha * 0.8f);

            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, rect.Width, 4), new Rectangle(0, 0, 1, 1), edge);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Bottom - 4, rect.Width, 4), new Rectangle(0, 0, 1, 1), edge * 0.7f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, 4, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
            sb.Draw(vaule, new Rectangle(rect.Right - 4, rect.Y, 4, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
        }

        private static void DrawMagicGlow(SpriteBatch sb, Rectangle rect, Color glow) {
            Texture2D vaule = VaultAsset.placeholder2.Value;

            //内部魔法填充
            sb.Draw(vaule, rect, new Rectangle(0, 0, 1, 1), glow * 0.22f);

            //发光魔法边框
            int border = 3;
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, rect.Width, border), new Rectangle(0, 0, 1, 1), glow * 0.75f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Bottom - border, rect.Width, border), new Rectangle(0, 0, 1, 1), glow * 0.55f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, border, rect.Height), new Rectangle(0, 0, 1, 1), glow * 0.65f);
            sb.Draw(vaule, new Rectangle(rect.Right - border, rect.Y, border, rect.Height), new Rectangle(0, 0, 1, 1), glow * 0.65f);
        }
        #endregion
    }
}
