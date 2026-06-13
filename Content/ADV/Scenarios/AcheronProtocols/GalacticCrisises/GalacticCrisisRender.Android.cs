using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.AcheronProtocols
{
    /// <summary>
    /// 战术人形档案展示：阿蒂丝与阿波拉的立绘、信息面板
    /// 从科尔托行星视图平滑过渡为双人档案展示
    /// </summary>
    internal partial class GalacticCrisisRender
    {
        #region 战术人形参数

        private static float androidRevealProgress;
        private static float androidGlitchTimer;
        private static float androidDataScrollTimer;

        //两人立绘的独立动画状态
        private static float artisRevealProgress;
        private static float apolaRevealProgress;

        //信号丢失闪烁
        private static float signalLostBlinkTimer;

        #endregion

        #region 初始化与清理

        private static void InitAndroid() {
            androidRevealProgress = 0f;
            androidGlitchTimer = 0f;
            androidDataScrollTimer = 0f;
            artisRevealProgress = 0f;
            apolaRevealProgress = 0f;
            signalLostBlinkTimer = 0f;
        }

        private static void CleanupAndroid() {
            androidRevealProgress = 0f;
            artisRevealProgress = 0f;
            apolaRevealProgress = 0f;
        }

        #endregion

        #region 逻辑更新

        private static void UpdateAndroidLogic() {
            if (currentPhase != AnimPhase.AndroidProfile) return;

            androidDataScrollTimer += 0.016f;
            signalLostBlinkTimer += 0.05f;

            //全局毛刺效果
            androidGlitchTimer += 0.016f;
        }

        private static void UpdateAndroidProfilePhase() {
            androidRevealProgress = MathF.Min(androidRevealProgress + 0.015f, 1f);
            phaseProgress = androidRevealProgress;

            //阿波拉先出现，阿蒂丝稍后出现
            if (androidRevealProgress > 0.1f) {
                apolaRevealProgress = MathF.Min(apolaRevealProgress + 0.025f, 1f);
            }
            if (androidRevealProgress > 0.3f) {
                artisRevealProgress = MathF.Min(artisRevealProgress + 0.025f, 1f);
            }

            //轻微信号干扰
            glitchIntensity = MathHelper.Lerp(0.01f, 0.05f, androidRevealProgress);
        }

        #endregion

        #region 绘制

        private static void DrawAndroidProfile(SpriteBatch sb, Vector2 center, Rectangle panelRect, float alpha) {
            float revealAlpha = alpha * VaultUtils.EaseOutCubic(androidRevealProgress);

            //面板内部区域（留出标题栏和边框）
            int marginH = 40;
            int marginV = 40;
            Rectangle contentRect = new(
                panelRect.X + marginH,
                panelRect.Y + marginV,
                panelRect.Width - marginH * 2,
                panelRect.Height - marginV * 2
            );

            //双生子共用外框
            DrawTwinOuterFrame(sb, contentRect, revealAlpha);

            //立绘区域占据上方大部分空间
            int portraitZoneHeight = (int)(contentRect.Height * 0.68f);
            //两人的间距很小，凸显双生子的关系
            int gap = 4;
            int halfWidth = (contentRect.Width - gap) / 2;

            //左侧：阿波拉（Apola）
            Rectangle leftPortraitRect = new(contentRect.X, contentRect.Y, halfWidth, portraitZoneHeight);
            DrawAndroidCard(sb, leftPortraitRect, ADVAsset.Apollia, AndroidApolaName.Value,
                apolaRevealProgress, revealAlpha, true);

            //右侧：阿蒂丝（Artis）
            Rectangle rightPortraitRect = new(contentRect.X + halfWidth + gap, contentRect.Y, halfWidth, portraitZoneHeight);
            DrawAndroidCard(sb, rightPortraitRect, ADVAsset.Artis, AndroidArtisName.Value,
                artisRevealProgress, revealAlpha, false);

            //中间的细分割线（仅在立绘区域，非常细淡，暗示两者的联系而非分割）
            DrawTwinDivider(sb, contentRect.X + halfWidth + gap / 2f, contentRect, portraitZoneHeight, revealAlpha);

            //底部共享信息区域
            Rectangle infoRect = new(contentRect.X, contentRect.Y + portraitZoneHeight + 6, contentRect.Width, contentRect.Height - portraitZoneHeight - 6);
            DrawTwinInfoPanel(sb, infoRect, revealAlpha);
        }

        /// <summary>
        /// 绘制双生子共用外框（强调两人是一个整体）
        /// </summary>
        private static void DrawTwinOuterFrame(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            Color techColor = new Color(50, 140, 200);
            float pulse = MathF.Sin(hologramFlicker * 2f) * 0.12f + 0.88f;
            float reveal = VaultUtils.EaseOutCubic(androidRevealProgress);

            //边线展开动画（以中心向外伸展）
            float lineWidth = rect.Width * reveal;
            float lineX = rect.X + (rect.Width - lineWidth) * 0.5f;
            float lineHeight = rect.Height * reveal;
            float lineY = rect.Y + (rect.Height - lineHeight) * 0.5f;

            //上边（顶部加强线，3px亮+1px暗）
            Color topEdge = techColor * (alpha * 0.75f * pulse);
            sb.Draw(pixel, new Vector2(lineX, rect.Y), new Rectangle(0, 0, 1, 1), topEdge, 0f, Vector2.Zero, new Vector2(lineWidth, 3f), SpriteEffects.None, 0f);
            sb.Draw(pixel, new Vector2(lineX, rect.Y + 3), new Rectangle(0, 0, 1, 1), topEdge * 0.35f, 0f, Vector2.Zero, new Vector2(lineWidth, 1f), SpriteEffects.None, 0f);
            //下边细线
            sb.Draw(pixel, new Vector2(lineX, rect.Bottom - 1), new Rectangle(0, 0, 1, 1), techColor * (alpha * 0.45f * pulse), 0f, Vector2.Zero, new Vector2(lineWidth, 1f), SpriteEffects.None, 0f);
            //左右边
            sb.Draw(pixel, new Vector2(rect.X, lineY), new Rectangle(0, 0, 1, 1), techColor * (alpha * 0.55f), 0f, Vector2.Zero, new Vector2(2f, lineHeight), SpriteEffects.None, 0f);
            sb.Draw(pixel, new Vector2(rect.Right - 2, lineY), new Rectangle(0, 0, 1, 1), techColor * (alpha * 0.55f), 0f, Vector2.Zero, new Vector2(2f, lineHeight), SpriteEffects.None, 0f);

            //内层辅助线（很淡，制造双层感）
            Color dim = techColor * (alpha * 0.18f);
            sb.Draw(pixel, new Vector2(rect.X + 4, rect.Y + 4), new Rectangle(0, 0, 1, 1), dim, 0f, Vector2.Zero, new Vector2(rect.Width - 8, 1f), SpriteEffects.None, 0f);
            sb.Draw(pixel, new Vector2(rect.X + 4, rect.Bottom - 5), new Rectangle(0, 0, 1, 1), dim * 0.6f, 0f, Vector2.Zero, new Vector2(rect.Width - 8, 1f), SpriteEffects.None, 0f);

            //四角L形括号（与主面板边框风格统一）
            Color cornerColor = new Color(80, 200, 255) * (alpha * 0.7f * pulse);
            float cLen = 20f;
            DrawAndroidLBracket(sb, pixel, new Vector2(rect.X + 2, rect.Y + 2), cornerColor, cLen, false, false);
            DrawAndroidLBracket(sb, pixel, new Vector2(rect.Right - 2, rect.Y + 2), cornerColor, cLen, true, false);
            DrawAndroidLBracket(sb, pixel, new Vector2(rect.X + 2, rect.Bottom - 2), cornerColor, cLen, false, true);
            DrawAndroidLBracket(sb, pixel, new Vector2(rect.Right - 2, rect.Bottom - 2), cornerColor, cLen, true, true);

            //顶部左侧刻痕（与主面板统一的机械感点缀）
            Color notchColor = techColor * (alpha * 0.55f);
            sb.Draw(pixel, new Rectangle(rect.X + 6, rect.Y, 1, 9), new Rectangle(0, 0, 1, 1), notchColor);
            sb.Draw(pixel, new Rectangle(rect.X + 20, rect.Y, 1, 6), new Rectangle(0, 0, 1, 1), notchColor * 0.6f);
            sb.Draw(pixel, new Rectangle(rect.X + 34, rect.Y, 1, 4), new Rectangle(0, 0, 1, 1), notchColor * 0.35f);
        }

        //战术人形档案局部用L形括号（避免与主文件的同名方法冲突）
        private static void DrawAndroidLBracket(SpriteBatch sb, Texture2D pixel, Vector2 pos, Color color, float len, bool flipX, bool flipY) {
            int lx = flipX ? (int)(pos.X - len + 2) : (int)pos.X;
            int ly = flipY ? (int)(pos.Y - len + 2) : (int)pos.Y;
            sb.Draw(pixel, new Rectangle(lx, (int)pos.Y, (int)len, 2), new Rectangle(0, 0, 1, 1), color);
            sb.Draw(pixel, new Rectangle((int)pos.X, ly, 2, (int)len), new Rectangle(0, 0, 1, 1), color * 0.9f);
        }

        /// <summary>
        /// 绘制双生子之间极淡的分割线（暗示联系而非分割）
        /// </summary>
        private static void DrawTwinDivider(SpriteBatch sb, float x, Rectangle contentRect, int portraitZoneHeight, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            Color divColor = new Color(60, 160, 220);
            float pulse = MathF.Sin(hologramFlicker * 2.5f) * 0.15f + 0.85f;

            //非常细淡的虚线
            float reveal = VaultUtils.EaseOutCubic(androidRevealProgress);
            float lineH = portraitZoneHeight * reveal;
            float lineY = contentRect.Y + (portraitZoneHeight - lineH) * 0.5f;

            int segments = 12;
            float segHeight = lineH / (segments * 2f);
            for (int i = 0; i < segments; i++) {
                float sy = lineY + i * segHeight * 2f;
                sb.Draw(pixel, new Vector2(x, sy), new Rectangle(0, 0, 1, 1),
                    divColor * (alpha * 0.2f * pulse), 0f, Vector2.Zero,
                    new Vector2(1f, segHeight), SpriteEffects.None, 0f);
            }
        }

        /// <summary>
        /// 绘制单个战术人形卡片（立绘占满整个区域，全身展示）
        /// </summary>
        private static void DrawAndroidCard(SpriteBatch sb, Rectangle area, Texture2D portrait,
            string name, float revealProgress, float alpha, bool isLeft) {
            if (revealProgress <= 0.01f) return;

            float cardAlpha = alpha * VaultUtils.EaseOutCubic(revealProgress);

            //半透明背景
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel != null) {
                Color bgColor = new Color(6, 10, 20) * (cardAlpha * 0.4f);
                sb.Draw(pixel, area, new Rectangle(0, 0, 1, 1), bgColor);
            }

            //绘制立绘（全身，填满整个区域）
            if (portrait != null) {
                DrawAndroidPortrait(sb, portrait, area, cardAlpha, revealProgress, isLeft);
            }
        }

        /// <summary>
        /// 绘制战术人形立绘（全身展示，无浮动，带全息投影效果）
        /// </summary>
        private static void DrawAndroidPortrait(SpriteBatch sb, Texture2D portrait, Rectangle rect,
            float alpha, float reveal, bool isLeft) {
            if (portrait == null) return;

            //计算绘制尺寸：保持纵横比，确保全身完整显示在框内
            float texAspect = portrait.Width / (float)portrait.Height;
            float rectAspect = rect.Width / (float)rect.Height;

            int drawWidth, drawHeight;
            if (texAspect > rectAspect) {
                //纹理更宽，以宽度为约束
                drawWidth = rect.Width;
                drawHeight = (int)(drawWidth / texAspect);
            }
            else {
                //纹理更高，以高度为约束
                drawHeight = rect.Height;
                drawWidth = (int)(drawHeight * texAspect);
            }

            //水平居中，垂直底部对齐（脚踩底边）
            Vector2 drawPos = new(
                rect.X + (rect.Width - drawWidth) * 0.5f,
                rect.Bottom - drawHeight
            );

            //出场动画：从侧方滑入（左侧角色从左滑入，右侧从右滑入）
            float slideOffset = (1f - VaultUtils.EaseOutCubic(reveal)) * 50f;
            drawPos.X += isLeft ? -slideOffset : slideOffset;

            float portraitAlpha = alpha * MathHelper.Clamp(reveal * 2f, 0f, 1f);

            Rectangle destRect = new((int)drawPos.X, (int)drawPos.Y, drawWidth, drawHeight);

            //全息投影底色光晕
            Texture2D softGlow = CWRAsset.SoftGlow?.Value;
            if (softGlow != null) {
                Vector2 glowCenter = new(rect.X + rect.Width * 0.5f, rect.Y + rect.Height * 0.5f);
                Vector2 glowOrigin = new(softGlow.Width * 0.5f, softGlow.Height * 0.5f);
                Color glowColor = new Color(40, 120, 200, 0) * (portraitAlpha * 0.12f);
                float glowScale = MathF.Max(drawWidth, drawHeight) / (float)softGlow.Width * 2.2f;
                sb.Draw(softGlow, glowCenter, null, glowColor, 0f, glowOrigin, glowScale, SpriteEffects.None, 0f);
            }

            //轻微全息投影色调
            Color holoTint = new Color(200, 230, 255) * portraitAlpha;
            sb.Draw(portrait, destRect, null, holoTint, 0f, Vector2.Zero, SpriteEffects.None, 0f);

            //全息扫描线覆盖效果
            DrawHoloScanOverlay(sb, rect, portraitAlpha);
        }

        /// <summary>
        /// 绘制全息扫描线覆盖在立绘上
        /// </summary>
        private static void DrawHoloScanOverlay(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            //水平扫描线
            float scanProgress = (androidDataScrollTimer * 0.5f) % 1f;
            float scanY = rect.Y + scanProgress * rect.Height;
            Color scanColor = new Color(80, 200, 255, 0) * (alpha * 0.12f);
            sb.Draw(pixel, new Vector2(rect.X, scanY), new Rectangle(0, 0, 1, 1),
                scanColor, 0f, Vector2.Zero, new Vector2(rect.Width, 2f), SpriteEffects.None, 0f);

            //第二条扫描线（稍慢）
            float scan2Progress = (androidDataScrollTimer * 0.35f + 0.5f) % 1f;
            float scan2Y = rect.Y + scan2Progress * rect.Height;
            sb.Draw(pixel, new Vector2(rect.X, scan2Y), new Rectangle(0, 0, 1, 1),
                scanColor * 0.5f, 0f, Vector2.Zero, new Vector2(rect.Width, 1f), SpriteEffects.None, 0f);
        }

        /// <summary>
        /// 绘制双生子共享信息面板（底部，左右各一列信息）
        /// </summary>
        private static void DrawTwinInfoPanel(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            var font = FontAssets.MouseText.Value;
            Color techColor = new Color(60, 160, 220);
            Color dimTech = new Color(40, 100, 160);

            float textAlpha = alpha * MathHelper.Clamp((androidRevealProgress - 0.3f) * 2.5f, 0f, 1f);
            if (textAlpha <= 0.01f) return;

            //信息区域背景
            Color infoBg = new Color(6, 12, 22) * (textAlpha * 0.5f);
            sb.Draw(pixel, rect, new Rectangle(0, 0, 1, 1), infoBg);

            //上方分隔线
            sb.Draw(pixel, new Vector2(rect.X, rect.Y), new Rectangle(0, 0, 1, 1),
                techColor * (textAlpha * 0.4f), 0f, Vector2.Zero,
                new Vector2(rect.Width, 1f), SpriteEffects.None, 0f);

            int halfWidth = rect.Width / 2;
            float lineSpacing = 20f;

            //===== 左侧：阿波拉信息 =====
            float leftX = rect.X + 12f;
            float lineY = rect.Y + 8f;

            float apolaTextAlpha = textAlpha * MathHelper.Clamp(apolaRevealProgress * 2f, 0f, 1f);
            DrawInfoLabel(sb, font, AndroidCodename.Value, AndroidApolaName.Value,
                new Vector2(leftX, lineY), techColor, apolaTextAlpha, 0.48f);
            lineY += lineSpacing;

            DrawInfoLine(sb, font, AndroidClassLabel.Value,
                new Vector2(leftX, lineY), dimTech, apolaTextAlpha * 0.8f, 0.4f);
            lineY += lineSpacing;

            //状态（闪烁警告）
            float statusBlink = MathF.Sin(signalLostBlinkTimer) * 0.5f + 0.5f;
            Color statusColor = Color.Lerp(new Color(200, 60, 40), new Color(255, 100, 60), statusBlink);
            DrawInfoLabel(sb, font, AndroidStatusLabel.Value, AndroidStatusLost.Value,
                new Vector2(leftX, lineY), statusColor, apolaTextAlpha, 0.48f);
            lineY += lineSpacing;

            DrawSignalLostBar(sb, new Rectangle((int)leftX, (int)lineY, halfWidth - 24, 5), apolaTextAlpha);

            //===== 右侧：阿蒂丝信息 =====
            float rightX = rect.X + halfWidth + 12f;
            lineY = rect.Y + 8f;

            float artisTextAlpha = textAlpha * MathHelper.Clamp(artisRevealProgress * 2f, 0f, 1f);
            DrawInfoLabel(sb, font, AndroidCodename.Value, AndroidArtisName.Value,
                new Vector2(rightX, lineY), techColor, artisTextAlpha, 0.48f);
            lineY += lineSpacing;

            DrawInfoLine(sb, font, AndroidClassLabel.Value,
                new Vector2(rightX, lineY), dimTech, artisTextAlpha * 0.8f, 0.4f);
            lineY += lineSpacing;

            DrawInfoLabel(sb, font, AndroidStatusLabel.Value, AndroidStatusLost.Value,
                new Vector2(rightX, lineY), statusColor, artisTextAlpha, 0.48f);
            lineY += lineSpacing;

            DrawSignalLostBar(sb, new Rectangle((int)rightX, (int)lineY, halfWidth - 24, 5), artisTextAlpha);

            //中间细线分隔左右信息
            float divPulse = MathF.Sin(hologramFlicker * 2.5f) * 0.1f + 0.9f;
            sb.Draw(pixel, new Vector2(rect.X + halfWidth, rect.Y + 4), new Rectangle(0, 0, 1, 1),
                techColor * (textAlpha * 0.2f * divPulse), 0f, Vector2.Zero,
                new Vector2(1f, rect.Height - 8), SpriteEffects.None, 0f);
        }

        /// <summary>
        /// 绘制信息标签行（标签: 值）
        /// </summary>
        private static void DrawInfoLabel(SpriteBatch sb, dynamic font, string label, string value,
            Vector2 pos, Color color, float alpha, float scale) {
            //标签（暗色）
            Color labelColor = new Color(80, 140, 180) * (alpha * 0.7f);
            Utils.DrawBorderString(sb, label + ": ", pos, labelColor, scale);

            //值（亮色）
            float labelWidth = font.MeasureString(label + ": ").X * scale;
            Utils.DrawBorderString(sb, value, pos + new Vector2(labelWidth, 0), color * alpha, scale);
        }

        /// <summary>
        /// 绘制单行信息文本
        /// </summary>
        private static void DrawInfoLine(SpriteBatch sb, dynamic font, string text,
            Vector2 pos, Color color, float alpha, float scale) {
            Utils.DrawBorderString(sb, text, pos, color * alpha, scale);
        }

        /// <summary>
        /// 绘制信号丢失状态条（模拟信号强度动画）
        /// </summary>
        private static void DrawSignalLostBar(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            //背景
            Color bgColor = new Color(15, 5, 5) * (alpha * 0.5f);
            sb.Draw(pixel, rect, new Rectangle(0, 0, 1, 1), bgColor);

            //模拟噪波信号块
            int blockCount = rect.Width / 4;
            for (int i = 0; i < blockCount; i++) {
                float noise = MathF.Sin(androidDataScrollTimer * 8f + i * 1.7f) * 0.5f + 0.5f;
                noise *= MathF.Cos(androidDataScrollTimer * 3f + i * 0.9f) * 0.5f + 0.5f;

                if (noise < 0.3f) continue; //大部分时间为空，模拟信号丢失

                float blockHeight = rect.Height * noise;
                float blockY = rect.Y + (rect.Height - blockHeight);

                Color blockColor = Color.Lerp(new Color(200, 40, 30), new Color(255, 80, 50), noise);
                blockColor *= alpha * 0.6f * noise;

                sb.Draw(pixel, new Vector2(rect.X + i * 4f, blockY), new Rectangle(0, 0, 1, 1),
                    blockColor, 0f, Vector2.Zero, new Vector2(3f, blockHeight), SpriteEffects.None, 0f);
            }

            //边框
            Color borderColor = new Color(200, 50, 40) * (alpha * 0.3f);
            sb.Draw(pixel, new Vector2(rect.X, rect.Y), new Rectangle(0, 0, 1, 1),
                borderColor, 0f, Vector2.Zero, new Vector2(rect.Width, 1f), SpriteEffects.None, 0f);
            sb.Draw(pixel, new Vector2(rect.X, rect.Bottom - 1), new Rectangle(0, 0, 1, 1),
                borderColor, 0f, Vector2.Zero, new Vector2(rect.Width, 1f), SpriteEffects.None, 0f);
        }

        #endregion
    }
}
