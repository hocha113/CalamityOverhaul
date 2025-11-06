using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Common
{
    /// <summary>
    /// 任务接受UI的通用基类，用于显示任务详情和接受/拒绝选项
    /// </summary>
    internal abstract class BaseQuestAcceptUI : UIHandle, ILocalizedModType
    {
        public abstract string LocalizationCategory { get; }

        //本地化文本
        protected LocalizedText QuestTitle { get; set; }
        protected LocalizedText QuestDesc { get; set; }
        protected LocalizedText AcceptText { get; set; }
        protected LocalizedText DeclineText { get; set; }

        //UI控制
        protected float sengs;
        protected float contentFadeProgress;
        protected bool showingQuest;
        protected bool questAccepted;
        protected bool questDeclined;

        //动画
        protected float panelPulse;
        protected float borderGlow;

        //布局常量
        protected const float PanelWidth = 340f;
        protected const float PanelHeight = 240f;
        protected const float Padding = 16f;
        protected const float ButtonHeight = 32f;

        //按钮
        protected Rectangle acceptButtonRect;
        protected Rectangle declineButtonRect;
        protected bool hoveringAccept;
        protected bool hoveringDecline;

        /// <summary>
        /// 设置本地化文本，子类需要实现
        /// </summary>
        protected abstract void SetupLocalizedTexts();

        /// <summary>
        /// 检查是否应该显示任务UI，子类需要实现
        /// </summary>
        protected abstract bool ShouldShowQuest();

        /// <summary>
        /// 当玩家接受任务时调用，子类需要实现
        /// </summary>
        protected abstract void OnQuestAccepted();

        /// <summary>
        /// 当玩家拒绝任务时调用，子类需要实现
        /// </summary>
        protected abstract void OnQuestDeclined();

        public override void SetStaticDefaults() {
            SetupLocalizedTexts();
        }

        public override bool Active {
            get {
                if (questAccepted || questDeclined) {
                    return sengs > 0;
                }

                showingQuest = ShouldShowQuest();
                return showingQuest || sengs > 0;
            }
        }

        public override void Update() {
            //展开/收起动画
            float targetSengs = showingQuest ? 1f : 0f;
            sengs = MathHelper.Lerp(sengs, targetSengs, 0.15f);

            if (Math.Abs(sengs - targetSengs) < 0.01f) {
                sengs = targetSengs;
            }

            if (sengs < 0.01f) {
                if (questAccepted) {
                    questAccepted = false;
                    OnQuestAccepted();
                }
                if (questDeclined) {
                    questDeclined = false;
                    OnQuestDeclined();
                }
                return;
            }

            //内容淡入
            if (sengs > 0.4f && contentFadeProgress < 1f) {
                float adjustedProgress = (sengs - 0.4f) / 0.6f;
                contentFadeProgress = Math.Min(contentFadeProgress + 0.1f, adjustedProgress);
            }
            else if (sengs <= 0.4f && contentFadeProgress > 0f) {
                contentFadeProgress -= 0.15f;
                contentFadeProgress = Math.Clamp(contentFadeProgress, 0f, 1f);
            }

            //动画更新
            panelPulse += 0.03f;
            borderGlow = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f) * 0.3f + 0.7f;

            //位置和尺寸
            Vector2 panelSize = new Vector2(PanelWidth, PanelHeight);
            DrawPosition = new Vector2(Main.screenWidth / 2 - PanelWidth / 2, Main.screenHeight / 2 - PanelHeight / 2);
            Size = panelSize;
            UIHitBox = DrawPosition.GetRectangle(panelSize);
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            if (hoverInMainPage) {
                player.mouseInterface = true;
            }

            //按钮位置
            float buttonY = DrawPosition.Y + PanelHeight - Padding - ButtonHeight;
            float buttonWidth = (PanelWidth - Padding * 3) / 2;

            acceptButtonRect = new Rectangle(
                (int)(DrawPosition.X + Padding),
                (int)buttonY,
                (int)buttonWidth,
                (int)ButtonHeight
            );

            declineButtonRect = new Rectangle(
                (int)(DrawPosition.X + Padding * 2 + buttonWidth),
                (int)buttonY,
                (int)buttonWidth,
                (int)ButtonHeight
            );

            //悬停检测
            hoveringAccept = acceptButtonRect.Contains(MouseHitBox);
            hoveringDecline = declineButtonRect.Contains(MouseHitBox);

            //点击处理
            if (keyLeftPressState == KeyPressState.Pressed) {
                if (hoveringAccept) {
                    SoundEngine.PlaySound(CWRSound.ButtonZero);
                    questAccepted = true;
                    showingQuest = false;
                }
                else if (hoveringDecline) {
                    SoundEngine.PlaySound(SoundID.MenuClose);
                    questDeclined = true;
                    showingQuest = false;
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (sengs < 0.01f) {
                return;
            }

            float alpha = Math.Min(sengs * 2f, 1f);
            DrawPanel(spriteBatch, alpha);

            if (contentFadeProgress > 0.01f) {
                DrawContent(spriteBatch, alpha * contentFadeProgress);
            }
        }

        protected virtual void DrawPanel(SpriteBatch spriteBatch, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //阴影
            Rectangle shadowRect = UIHitBox;
            shadowRect.Offset(5, 5);
            spriteBatch.Draw(pixel, shadowRect, new Rectangle(0, 0, 1, 1), Color.Black * (alpha * 0.6f));

            //背景渐变
            Color bgTop = new Color(30, 20, 20) * (alpha * 0.95f);
            Color bgBottom = new Color(50, 35, 35) * (alpha * 0.95f);
            int segments = 20;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = (int)(DrawPosition.Y + t * PanelHeight);
                int y2 = (int)(DrawPosition.Y + t2 * PanelHeight);
                Rectangle r = new Rectangle((int)DrawPosition.X, y1, (int)PanelWidth, Math.Max(1, y2 - y1));
                Color segColor = Color.Lerp(bgTop, bgBottom, t);
                spriteBatch.Draw(pixel, r, new Rectangle(0, 0, 1, 1), segColor);
            }

            //脉冲叠加
            float pulse = (float)Math.Sin(panelPulse * 1.5f) * 0.5f + 0.5f;
            Color pulseColor = new Color(140, 50, 50) * (alpha * 0.15f * pulse);
            spriteBatch.Draw(pixel, UIHitBox, new Rectangle(0, 0, 1, 1), pulseColor);

            //边框
            DrawBrimstoneFrame(spriteBatch, UIHitBox, alpha, borderGlow);
        }

        /// <summary>
        /// 将文本按宽度自动换行
        /// </summary>
        protected static List<string> WrapText(string text, DynamicSpriteFont font, float maxWidth, float scale = 1f) {
            List<string> lines = [];

            if (string.IsNullOrEmpty(text)) {
                return lines;
            }

            string[] words = text.Split(' ');
            string currentLine = "";

            foreach (string word in words) {
                string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                Vector2 testSize = font.MeasureString(testLine) * scale;

                if (testSize.X > maxWidth && !string.IsNullOrEmpty(currentLine)) {
                    //当前行已满，保存并开始新行
                    lines.Add(currentLine);
                    currentLine = word;
                }
                else {
                    currentLine = testLine;
                }
            }

            //添加最后一行
            if (!string.IsNullOrEmpty(currentLine)) {
                lines.Add(currentLine);
            }

            return lines;
        }

        protected virtual void DrawContent(SpriteBatch spriteBatch, float alpha) {
            var font = FontAssets.MouseText.Value;
            const float titleScale = 0.95f;
            const float descScale = 0.75f;
            const float maxTitleWidth = PanelWidth - Padding * 2;

            //标题
            Vector2 titlePos = DrawPosition + new Vector2(Padding, Padding);
            List<string> titleLines = WrapText(QuestTitle.Value, font, maxTitleWidth, titleScale);

            //标题发光效果
            Color titleGlow = Color.Gold * alpha * 0.6f;
            float currentY = titlePos.Y;

            foreach (string line in titleLines) {
                //发光效果
                for (int i = 0; i < 4; i++) {
                    float angle = MathHelper.TwoPi * i / 4f;
                    Vector2 offset = angle.ToRotationVector2() * 1.8f;
                    Utils.DrawBorderString(spriteBatch, line, new Vector2(titlePos.X, currentY) + offset, titleGlow, titleScale);
                }
                Utils.DrawBorderString(spriteBatch, line, new Vector2(titlePos.X, currentY), Color.White * alpha, titleScale);
                currentY += font.MeasureString(line).Y * titleScale * 0.9f; //行间距
            }

            //分割线
            float titleHeight = currentY - titlePos.Y;
            Vector2 dividerStart = titlePos + new Vector2(0, titleHeight + 4);
            Vector2 dividerEnd = dividerStart + new Vector2(PanelWidth - Padding * 2, 0);
            DrawGradientLine(spriteBatch, dividerStart, dividerEnd, Color.Gold * alpha * 0.8f, Color.Gold * alpha * 0.1f, 1.5f);

            //描述文本
            Vector2 descPos = dividerStart + new Vector2(2, 12);
            string desc = QuestDesc.Value;

            //先按换行符分割
            string[] paragraphs = desc.Split('\n');
            currentY = descPos.Y;
            Color textColor = Color.White * alpha;

            foreach (string paragraph in paragraphs) {
                //每个段落再进行自动换行
                List<string> wrappedLines = WrapText(paragraph, font, maxTitleWidth, descScale);

                foreach (string line in wrappedLines) {
                    Vector2 linePos = new Vector2(descPos.X, currentY);
                    Utils.DrawBorderString(spriteBatch, line, linePos + new Vector2(1, 1), Color.Black * alpha * 0.5f, descScale);
                    Utils.DrawBorderString(spriteBatch, line, linePos, textColor, descScale);
                    currentY += font.MeasureString(line).Y * descScale * 0.9f;
                }
            }

            //绘制按钮
            DrawButton(spriteBatch, acceptButtonRect, AcceptText.Value, hoveringAccept, alpha, true);
            DrawButton(spriteBatch, declineButtonRect, DeclineText.Value, hoveringDecline, alpha, false);
        }

        protected static void DrawButton(SpriteBatch spriteBatch, Rectangle buttonRect, string text, bool hovering, float alpha, bool isAccept) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //按钮背景
            Color bgColor = isAccept
                ? hovering ? new Color(80, 100, 60) : new Color(60, 80, 45)
                : hovering ? new Color(100, 60, 60) : new Color(80, 45, 45);
            bgColor *= alpha * 0.9f;

            spriteBatch.Draw(pixel, buttonRect, new Rectangle(0, 0, 1, 1), bgColor);

            //按钮边框
            Color borderColor = isAccept ? Color.Green : Color.Red;
            borderColor *= alpha * (hovering ? 1f : 0.6f);
            int borderWidth = hovering ? 2 : 1;

            Rectangle topBorder = new Rectangle(buttonRect.X, buttonRect.Y, buttonRect.Width, borderWidth);
            Rectangle bottomBorder = new Rectangle(buttonRect.X, buttonRect.Bottom - borderWidth, buttonRect.Width, borderWidth);
            Rectangle leftBorder = new Rectangle(buttonRect.X, buttonRect.Y, borderWidth, buttonRect.Height);
            Rectangle rightBorder = new Rectangle(buttonRect.Right - borderWidth, buttonRect.Y, borderWidth, buttonRect.Height);

            spriteBatch.Draw(pixel, topBorder, new Rectangle(0, 0, 1, 1), borderColor);
            spriteBatch.Draw(pixel, bottomBorder, new Rectangle(0, 0, 1, 1), borderColor);
            spriteBatch.Draw(pixel, leftBorder, new Rectangle(0, 0, 1, 1), borderColor);
            spriteBatch.Draw(pixel, rightBorder, new Rectangle(0, 0, 1, 1), borderColor);

            //按钮文字
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text);
            Vector2 textPos = buttonRect.Center.ToVector2() - textSize / 2;
            Color textColor = Color.White * alpha * (hovering ? 1.1f : 1f);

            Utils.DrawBorderString(spriteBatch, text, textPos + new Vector2(1, 1), Color.Black * alpha * 0.6f, 1f);
            Utils.DrawBorderString(spriteBatch, text, textPos, textColor, 1f);
        }

        protected static void DrawBrimstoneFrame(SpriteBatch sb, Rectangle rect, float alpha, float pulse) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //外框
            Color outerEdge = Color.Lerp(new Color(180, 60, 30), new Color(255, 140, 70), pulse) * (alpha * 0.85f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge * 0.75f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);
            sb.Draw(pixel, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);

            //内框发光
            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            Color innerGlow = new Color(220, 100, 50) * (alpha * 0.22f * pulse);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow * 0.7f);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);
            sb.Draw(pixel, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);
        }

        protected static void DrawGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color startColor, Color endColor, float thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
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
                spriteBatch.Draw(pixel, segPos, new Rectangle(0, 0, 1, 1), color, rotation, new Vector2(0, 0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0);
            }
        }
    }
}
