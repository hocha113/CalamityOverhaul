using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV
{
    /// <summary>
    /// 选项数据类
    /// </summary>
    internal class Choice
    {
        public string Text { get; set; }
        public Action OnSelect { get; set; }
        public bool Enabled { get; set; } = true;
        public string DisabledHint { get; set; }

        public Choice(string text, Action onSelect, bool enabled = true, string disabledHint = null) {
            Text = text;
            OnSelect = onSelect;
            Enabled = enabled;
            DisabledHint = disabledHint;
        }
    }

    /// <summary>
    /// ADV选项框UI，参考ResurrectionUI的绘制风格
    /// </summary>
    internal class ADVChoiceBox : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        public static ADVChoiceBox Instance => UIHandleLoader.GetUIHandleOfType<ADVChoiceBox>();

        /// <summary>
        /// 选项框样式枚举
        /// </summary>
        public enum ChoiceBoxStyle
        {
            Default,//默认深蓝科技风格
            Brimstone,//硫磺火风格
            Draedon//嘉登科技风格
        }

        private readonly List<Choice> choices = new();
        private int hoveredIndex = -1;
        private int selectedIndex = -1;
        private bool isSelecting = false;
        private ChoiceBoxStyle currentStyle = ChoiceBoxStyle.Default;

        //动画状态
        private float showProgress = 0f;
        private float hideProgress = 0f;
        private const float ShowDuration = 12f;
        private const float HideDuration = 10f;
        private bool closing = false;

        //选项悬停动画
        private readonly float[] choiceHoverProgress = new float[10];//支持最多10个选项
        private const float HoverSpeed = 0.15f;

        //位置和尺寸
        private Vector2 anchorPosition;
        private Vector2 panelSize;
        private Rectangle panelRect;

        //样式动画参数
        private float styleAnimTimer = 0f;//样式动画计时器

        //布局常量
        private const float MinWidth = 200f;
        private const float MaxWidth = 420f;
        private const float HorizontalPadding = 14f;
        private const float TopPadding = 12f;
        private const float BottomPadding = 14f;
        private const float TitleExtra = 6f;
        private const float DividerSpacing = 8f;
        private const float ChoiceSpacing = 8f;
        private const float ChoiceHeight = 32f;
        private const float ChoicePadding = 8f;

        //本地化文本
        protected static LocalizedText TitleText;
        protected static LocalizedText DisabledHintFormat;

        public override bool Active => choices.Count > 0 || showProgress > 0f;

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "选择");
            DisabledHintFormat = this.GetLocalization(nameof(DisabledHintFormat), () => "（{0}）");
        }

        /// <summary>
        /// 显示选项框
        /// </summary>
        /// <param name="choices">选项列表</param>
        /// <param name="anchorProvider">锚点位置提供者</param>
        /// <param name="style">选项框样式</param>
        public static void Show(List<Choice> choices, Func<Vector2> anchorProvider = null, ChoiceBoxStyle style = ChoiceBoxStyle.Default) {
            var inst = Instance;
            inst.choices.Clear();
            inst.choices.AddRange(choices);
            inst.isSelecting = true;
            inst.closing = false;
            inst.showProgress = 0f;
            inst.hideProgress = 0f;
            inst.hoveredIndex = -1;
            inst.selectedIndex = -1;
            inst.currentStyle = style;
            inst.styleAnimTimer = 0f;

            //重置悬停动画
            for (int i = 0; i < inst.choiceHoverProgress.Length; i++) {
                inst.choiceHoverProgress[i] = 0f;
            }

            //计算锚点位置
            if (anchorProvider != null) {
                inst.anchorPosition = anchorProvider();
            }
            else if (DialogueUIRegistry.Current != null) {
                var rect = DialogueUIRegistry.Current.GetPanelRect();
                if (rect != Rectangle.Empty) {
                    inst.anchorPosition = new Vector2(rect.Center.X, rect.Bottom + 20f);
                }
                else {
                    inst.anchorPosition = new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.65f);
                }
            }
            else {
                inst.anchorPosition = new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.65f);
            }

            //计算面板尺寸
            inst.CalculatePanelSize();
        }

        /// <summary>
        /// 隐藏选项框
        /// </summary>
        public static void Hide() {
            var inst = Instance;
            inst.closing = true;
            inst.hideProgress = 0f;
        }

        private void CalculatePanelSize() {
            if (choices.Count == 0) {
                panelSize = Vector2.Zero;
                return;
            }

            //计算标题尺寸
            string title = TitleText.Value;
            float titleHeight = FontAssets.MouseText.Value.MeasureString(title).Y * 0.9f;

            //计算选项区域所需宽度
            float maxChoiceWidth = 0f;
            foreach (var choice in choices) {
                string text = choice.Text;
                if (!choice.Enabled && !string.IsNullOrEmpty(choice.DisabledHint)) {
                    text += " " + string.Format(DisabledHintFormat.Value, choice.DisabledHint);
                }
                float width = FontAssets.MouseText.Value.MeasureString(text).X * 0.75f;
                if (width > maxChoiceWidth) {
                    maxChoiceWidth = width;
                }
            }

            float contentWidth = Math.Max(maxChoiceWidth + ChoicePadding * 2, MinWidth - HorizontalPadding * 2);
            float panelWidth = Math.Clamp(contentWidth + HorizontalPadding * 2, MinWidth, MaxWidth);

            //计算面板高度
            float dividerHeight = 1.3f;
            float choicesHeight = choices.Count * ChoiceHeight + (choices.Count - 1) * ChoiceSpacing;

            float panelHeight = TopPadding
                + titleHeight + TitleExtra
                + DividerSpacing + dividerHeight
                + DividerSpacing
                + choicesHeight
                + BottomPadding;

            panelSize = new Vector2(panelWidth, panelHeight);
        }

        public override void Update() {
            if (choices.Count == 0 && !closing) {
                return;
            }

            //更新样式动画计时器
            styleAnimTimer += 0.05f;
            if (styleAnimTimer > MathHelper.TwoPi) {
                styleAnimTimer -= MathHelper.TwoPi;
            }

            //动画更新
            if (!closing && showProgress < 1f) {
                showProgress += 1f / ShowDuration;
                showProgress = Math.Clamp(showProgress, 0f, 1f);
            }

            if (closing) {
                if (hideProgress < 1f) {
                    hideProgress += 1f / HideDuration;
                    hideProgress = Math.Clamp(hideProgress, 0f, 1f);

                    if (hideProgress >= 1f) {
                        choices.Clear();
                        closing = false;
                        showProgress = 0f;
                        isSelecting = false;
                    }
                }
            }

            if (closing || showProgress < 0.5f) {
                return;
            }

            //更新面板矩形
            float progress = closing ? 1f - hideProgress : showProgress;
            float eased = closing ? CWRUtils.EaseInCubic(progress) : CWRUtils.EaseOutBack(progress);

            Vector2 drawPos = anchorPosition - new Vector2(panelSize.X / 2f, panelSize.Y / 2f);
            drawPos.Y += (1f - eased) * 60f;

            panelRect = new Rectangle((int)drawPos.X, (int)drawPos.Y, (int)panelSize.X, (int)panelSize.Y);

            //检测鼠标悬停
            Point mousePos = new Point(Main.mouseX, Main.mouseY);
            bool hoverInPanel = panelRect.Contains(mousePos);

            if (hoverInPanel) {
                player.mouseInterface = true;
            }

            int oldHoveredIndex = hoveredIndex;
            hoveredIndex = -1;

            if (hoverInPanel && isSelecting) {
                //计算每个选项的矩形
                float startY = drawPos.Y + TopPadding
                    + FontAssets.MouseText.Value.MeasureString(TitleText.Value).Y * 0.9f
                    + TitleExtra + DividerSpacing * 2 + 1.3f;

                for (int i = 0; i < choices.Count; i++) {
                    float choiceY = startY + i * (ChoiceHeight + ChoiceSpacing);
                    Rectangle choiceRect = new Rectangle(
                        (int)(drawPos.X + HorizontalPadding),
                        (int)choiceY,
                        (int)(panelSize.X - HorizontalPadding * 2),
                        (int)ChoiceHeight
                    );

                    if (choiceRect.Contains(mousePos)) {
                        hoveredIndex = i;

                        //点击处理
                        if (keyLeftPressState == KeyPressState.Pressed) {
                            if (choices[i].Enabled) {
                                selectedIndex = i;
                                SoundEngine.PlaySound(SoundID.MenuTick);
                                choices[i].OnSelect?.Invoke();
                                Hide();
                            }
                            else {
                                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.3f });
                            }
                        }
                        break;
                    }
                }
            }

            //更新悬停动画
            for (int i = 0; i < choiceHoverProgress.Length && i < choices.Count; i++) {
                float target = i == hoveredIndex ? 1f : 0f;
                choiceHoverProgress[i] = MathHelper.Lerp(choiceHoverProgress[i], target, HoverSpeed);
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (showProgress <= 0.01f && !closing) {
                return;
            }

            float progress = closing ? 1f - hideProgress : showProgress;
            if (progress <= 0f) {
                return;
            }

            float alpha = Math.Min(progress * 1.5f, 1f);

            //根据样式选择绘制方法
            switch (currentStyle) {
                case ChoiceBoxStyle.Brimstone:
                    DrawBrimstoneStyle(spriteBatch, alpha);
                    break;
                case ChoiceBoxStyle.Draedon:
                    DrawDraedonStyle(spriteBatch, alpha);
                    break;
                default:
                    DrawDefaultStyle(spriteBatch, alpha);
                    break;
            }
        }

        #region 默认样式绘制
        private void DrawDefaultStyle(SpriteBatch spriteBatch, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //绘制阴影
            Rectangle shadowRect = panelRect;
            shadowRect.Offset(3, 3);
            spriteBatch.Draw(pixel, shadowRect, new Rectangle(0, 0, 1, 1), Color.Black * 0.5f * alpha);

            //绘制背景
            float wave = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f) * 0.05f + 0.95f;
            Color baseA = new Color(14, 22, 38) * (alpha * wave);
            Color baseB = new Color(8, 26, 46) * 0.3f;
            Color bgCol = new Color(
                (byte)Math.Clamp(baseA.R + baseB.R, 0, 255),
                (byte)Math.Clamp(baseA.G + baseB.G, 0, 255),
                (byte)Math.Clamp(baseA.B + baseB.B, 0, 255),
                (byte)Math.Clamp(baseA.A + baseB.A, 0, 255)
            );
            spriteBatch.Draw(pixel, panelRect, new Rectangle(0, 0, 1, 1), bgCol);

            //绘制边框
            Color edgeColor = new Color(70, 180, 230) * (alpha * 0.75f);
            DrawBorder(spriteBatch, panelRect, edgeColor);

            //绘制标题
            Vector2 titlePos = new Vector2(panelRect.X + HorizontalPadding, panelRect.Y + TopPadding);
            string title = TitleText.Value;

            for (int i = 0; i < 4; i++) {
                float ang = MathHelper.TwoPi * i / 4f;
                Vector2 o = ang.ToRotationVector2() * 1.25f;
                Utils.DrawBorderString(spriteBatch, title, titlePos + o, edgeColor * 0.55f, 0.9f);
            }
            Utils.DrawBorderString(spriteBatch, title, titlePos, Color.White * alpha, 0.9f);

            //绘制分割线
            float titleHeight = FontAssets.MouseText.Value.MeasureString(title).Y * 0.9f;
            Vector2 dividerStart = titlePos + new Vector2(0, titleHeight + TitleExtra);
            Vector2 dividerEnd = dividerStart + new Vector2(panelSize.X - HorizontalPadding * 2, 0);
            DrawGradientLine(spriteBatch, dividerStart, dividerEnd, edgeColor * 0.9f, edgeColor * 0.05f, 1.3f);

            //绘制选项
            Vector2 choiceStartPos = dividerStart + new Vector2(0, DividerSpacing + 1.3f);
            DrawDefaultChoices(spriteBatch, choiceStartPos, alpha, edgeColor);

            //绘制装饰星星
            float starTime = Main.GlobalTimeWrappedHourly * 3f;
            Vector2 star1 = new Vector2(panelRect.Right - 18, panelRect.Y + 14);
            float s1a = ((float)Math.Sin(starTime) * 0.5f + 0.5f) * alpha;
            DrawStar(spriteBatch, star1, 3.5f, edgeColor * s1a);
        }

        private void DrawDefaultChoices(SpriteBatch spriteBatch, Vector2 startPos, float alpha, Color edgeColor) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            for (int i = 0; i < choices.Count; i++) {
                var choice = choices[i];
                Vector2 choicePos = startPos + new Vector2(0, i * (ChoiceHeight + ChoiceSpacing));

                //选项背景
                Rectangle choiceRect = new Rectangle(
                    (int)choicePos.X,
                    (int)choicePos.Y,
                    (int)(panelSize.X - HorizontalPadding * 2),
                    (int)ChoiceHeight
                );

                //悬停效果
                float hoverProgress = choiceHoverProgress[i];
                Color choiceBg = choice.Enabled
                    ? Color.Lerp(new Color(20, 35, 50) * 0.3f, new Color(40, 70, 100) * 0.5f, hoverProgress)
                    : new Color(30, 30, 35) * 0.2f;

                spriteBatch.Draw(pixel, choiceRect, new Rectangle(0, 0, 1, 1), choiceBg * alpha);

                //选项边框
                if (hoverProgress > 0.01f) {
                    DrawChoiceBorder(spriteBatch, choiceRect, edgeColor * (hoverProgress * 0.6f * alpha));
                }

                //选项文本
                DrawChoiceText(spriteBatch, choice, choiceRect, alpha, edgeColor, hoverProgress, i);
            }
        }
        #endregion

        #region 硫磺火样式绘制
        private void DrawBrimstoneStyle(SpriteBatch spriteBatch, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //绘制阴影
            Rectangle shadowRect = panelRect;
            shadowRect.Offset(7, 9);
            spriteBatch.Draw(pixel, shadowRect, new Rectangle(0, 0, 1, 1), new Color(20, 0, 0) * (alpha * 0.65f));

            //渐变背景 - 硫磺火深红色
            int segments = 25;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                float flameWave = (float)Math.Sin(styleAnimTimer * 0.6f + t * 2.2f) * 0.5f + 0.5f;
                Color brimstoneDeep = new Color(25, 5, 5);
                Color brimstoneMid = new Color(80, 15, 10);
                Color brimstoneHot = new Color(140, 35, 20);

                Color baseColor = Color.Lerp(brimstoneDeep, brimstoneMid, flameWave);
                Color finalColor = Color.Lerp(baseColor, brimstoneHot, t * 0.5f);
                finalColor *= alpha * 0.92f;

                spriteBatch.Draw(pixel, r, new Rectangle(0, 0, 1, 1), finalColor);
            }

            //火焰脉冲叠加
            float pulseBrightness = (float)Math.Sin(styleAnimTimer * 1.8f) * 0.5f + 0.5f;
            Color pulseOverlay = new Color(120, 25, 15) * (alpha * 0.25f * pulseBrightness);
            spriteBatch.Draw(pixel, panelRect, new Rectangle(0, 0, 1, 1), pulseOverlay);

            //火焰边框
            Color flameEdge = Color.Lerp(new Color(180, 60, 30), new Color(255, 140, 70), pulseBrightness) * (alpha * 0.85f);
            DrawBorder(spriteBatch, panelRect, flameEdge);

            //绘制标题
            Vector2 titlePos = new Vector2(panelRect.X + HorizontalPadding, panelRect.Y + TopPadding);
            string title = TitleText.Value;

            //火焰光晕效果
            Color nameGlow = new Color(255, 140, 80) * alpha * 0.75f;
            for (int i = 0; i < 6; i++) {
                float angle = MathHelper.TwoPi * i / 6f + styleAnimTimer * 0.5f;
                Vector2 offset = angle.ToRotationVector2() * 2.2f;
                Utils.DrawBorderString(spriteBatch, title, titlePos + offset, nameGlow * 0.5f, 0.95f);
            }
            Utils.DrawBorderString(spriteBatch, title, titlePos, new Color(255, 240, 220) * alpha, 0.95f);

            //分隔线
            float titleHeight = FontAssets.MouseText.Value.MeasureString(title).Y * 0.9f;
            Vector2 dividerStart = titlePos + new Vector2(0, titleHeight + TitleExtra);
            Vector2 dividerEnd = dividerStart + new Vector2(panelSize.X - HorizontalPadding * 2, 0);
            DrawGradientLine(spriteBatch, dividerStart, dividerEnd,
                new Color(220, 80, 40) * (alpha * 0.9f),
                new Color(120, 30, 15) * (alpha * 0.1f), 1.5f);

            //绘制选项
            Vector2 choiceStartPos = dividerStart + new Vector2(0, DividerSpacing + 1.3f);
            DrawBrimstoneChoices(spriteBatch, choiceStartPos, alpha, flameEdge);
        }

        private void DrawBrimstoneChoices(SpriteBatch spriteBatch, Vector2 startPos, float alpha, Color flameColor) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            for (int i = 0; i < choices.Count; i++) {
                var choice = choices[i];
                Vector2 choicePos = startPos + new Vector2(0, i * (ChoiceHeight + ChoiceSpacing));

                Rectangle choiceRect = new Rectangle(
                    (int)choicePos.X,
                    (int)choicePos.Y,
                    (int)(panelSize.X - HorizontalPadding * 2),
                    (int)ChoiceHeight
                );

                float hoverProgress = choiceHoverProgress[i];
                Color choiceBg = choice.Enabled
                    ? Color.Lerp(new Color(40, 10, 5) * 0.3f, new Color(100, 25, 15) * 0.5f, hoverProgress)
                    : new Color(30, 20, 15) * 0.2f;

                spriteBatch.Draw(pixel, choiceRect, new Rectangle(0, 0, 1, 1), choiceBg * alpha);

                if (hoverProgress > 0.01f) {
                    DrawChoiceBorder(spriteBatch, choiceRect, flameColor * (hoverProgress * 0.6f * alpha));
                }

                DrawChoiceText(spriteBatch, choice, choiceRect, alpha, flameColor, hoverProgress, i);
            }
        }
        #endregion

        #region 嘉登科技样式绘制
        private void DrawDraedonStyle(SpriteBatch spriteBatch, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //绘制阴影
            Rectangle shadowRect = panelRect;
            shadowRect.Offset(5, 6);
            spriteBatch.Draw(pixel, shadowRect, new Rectangle(0, 0, 1, 1), Color.Black * (alpha * 0.65f));

            //科技背景渐变
            int segments = 25;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                float pulse = (float)Math.Sin(styleAnimTimer * 0.6f + t * 2.0f) * 0.5f + 0.5f;
                Color techDark = new Color(8, 12, 22);
                Color techMid = new Color(18, 28, 42);
                Color techEdge = new Color(35, 55, 85);

                Color blendBase = Color.Lerp(techDark, techMid, pulse);
                Color c = Color.Lerp(blendBase, techEdge, t * 0.45f);
                c *= alpha * 0.92f;

                spriteBatch.Draw(pixel, r, new Rectangle(0, 0, 1, 1), c);
            }

            //全息闪烁覆盖层
            float flicker = (float)Math.Sin(styleAnimTimer * 1.5f) * 0.5f + 0.5f;
            Color hologramOverlay = new Color(15, 30, 45) * (alpha * 0.25f * flicker);
            spriteBatch.Draw(pixel, panelRect, new Rectangle(0, 0, 1, 1), hologramOverlay);

            //科技边框
            Color techEdgeColor = Color.Lerp(new Color(40, 160, 240), new Color(80, 200, 255), flicker) * (alpha * 0.85f);
            DrawBorder(spriteBatch, panelRect, techEdgeColor);

            //绘制标题
            Vector2 titlePos = new Vector2(panelRect.X + HorizontalPadding, panelRect.Y + TopPadding);
            string title = TitleText.Value;

            Color nameGlow = new Color(80, 220, 255) * alpha * 0.8f;
            for (int i = 0; i < 4; i++) {
                float a = MathHelper.TwoPi * i / 4f;
                Vector2 off = a.ToRotationVector2() * 2f;
                Utils.DrawBorderString(spriteBatch, title, titlePos + off, nameGlow * 0.6f, 0.95f);
            }
            Utils.DrawBorderString(spriteBatch, title, titlePos, Color.White * alpha, 0.95f);

            //分隔线
            float titleHeight = FontAssets.MouseText.Value.MeasureString(title).Y * 0.9f;
            Vector2 dividerStart = titlePos + new Vector2(0, titleHeight + TitleExtra);
            Vector2 dividerEnd = dividerStart + new Vector2(panelSize.X - HorizontalPadding * 2, 0);
            DrawGradientLine(spriteBatch, dividerStart, dividerEnd,
                new Color(60, 160, 240) * (alpha * 0.9f),
                new Color(60, 160, 240) * (alpha * 0.08f), 1.5f);

            //绘制选项
            Vector2 choiceStartPos = dividerStart + new Vector2(0, DividerSpacing + 1.3f);
            DrawDraedonChoices(spriteBatch, choiceStartPos, alpha, techEdgeColor);
        }

        private void DrawDraedonChoices(SpriteBatch spriteBatch, Vector2 startPos, float alpha, Color techColor) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            for (int i = 0; i < choices.Count; i++) {
                var choice = choices[i];
                Vector2 choicePos = startPos + new Vector2(0, i * (ChoiceHeight + ChoiceSpacing));

                Rectangle choiceRect = new Rectangle(
                    (int)choicePos.X,
                    (int)choicePos.Y,
                    (int)(panelSize.X - HorizontalPadding * 2),
                    (int)ChoiceHeight
                );

                float hoverProgress = choiceHoverProgress[i];
                Color choiceBg = choice.Enabled
                    ? Color.Lerp(new Color(8, 16, 30) * 0.3f, new Color(20, 40, 65) * 0.5f, hoverProgress)
                    : new Color(15, 15, 20) * 0.2f;

                spriteBatch.Draw(pixel, choiceRect, new Rectangle(0, 0, 1, 1), choiceBg * alpha);

                if (hoverProgress > 0.01f) {
                    DrawChoiceBorder(spriteBatch, choiceRect, techColor * (hoverProgress * 0.6f * alpha));
                }

                //绘制数据流效果
                if (choice.Enabled && hoverProgress > 0.3f) {
                    float dataShift = (float)Math.Sin(styleAnimTimer * 3f + i * 0.5f) * 1.5f;
                    Color dataColor = techColor * (hoverProgress * 0.2f * alpha);
                    spriteBatch.Draw(pixel,
                        new Rectangle((int)(choiceRect.X + dataShift), choiceRect.Y, 1, choiceRect.Height),
                        dataColor);
                }

                DrawChoiceText(spriteBatch, choice, choiceRect, alpha, techColor, hoverProgress, i);
            }
        }
        #endregion

        #region 通用绘制工具
        private void DrawChoiceText(SpriteBatch spriteBatch, Choice choice, Rectangle choiceRect, float alpha, Color edgeColor, float hoverProgress, int index) {
            string text = choice.Text;
            Color textColor = choice.Enabled ? Color.White : new Color(120, 120, 130);

            Vector2 textPos = new Vector2(choiceRect.X + ChoicePadding, choiceRect.Y + ChoiceHeight / 2f);
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.75f;
            textPos.Y -= textSize.Y / 2f;

            //文本发光效果（仅启用的选项）
            if (choice.Enabled && hoverProgress > 0.3f) {
                for (int j = 0; j < 4; j++) {
                    float ang = MathHelper.TwoPi * j / 4f;
                    Vector2 offset = ang.ToRotationVector2() * (1f * hoverProgress);
                    Utils.DrawBorderString(spriteBatch, text, textPos + offset,
                        edgeColor * (0.3f * hoverProgress * alpha), 0.75f);
                }
            }

            Utils.DrawBorderString(spriteBatch, text, textPos, textColor * alpha, 0.75f);

            //禁用提示
            if (!choice.Enabled && !string.IsNullOrEmpty(choice.DisabledHint)) {
                string hint = string.Format(DisabledHintFormat.Value, choice.DisabledHint);
                Vector2 hintSize = FontAssets.MouseText.Value.MeasureString(hint) * 0.65f;
                Vector2 hintPos = new Vector2(
                    choiceRect.Right - ChoicePadding - hintSize.X,
                    textPos.Y + 2f
                );
                Utils.DrawBorderString(spriteBatch, hint, hintPos,
                    new Color(180, 100, 100) * alpha, 0.65f);
            }

            //选项序号
            string indexText = $"{index + 1}.";
            Vector2 indexPos = new Vector2(
                choiceRect.X - 18f,
                textPos.Y
            );
            Utils.DrawBorderString(spriteBatch, indexText, indexPos,
                edgeColor * (0.5f + hoverProgress * 0.5f) * alpha, 0.7f);
        }

        private static void DrawBorder(SpriteBatch spriteBatch, Rectangle rect, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2),
                new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2),
                new Rectangle(0, 0, 1, 1), color * 0.7f);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height),
                new Rectangle(0, 0, 1, 1), color * 0.85f);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height),
                new Rectangle(0, 0, 1, 1), color * 0.85f);

            DrawCornerStar(spriteBatch, new Vector2(rect.X + 8, rect.Y + 8), color * 0.9f);
            DrawCornerStar(spriteBatch, new Vector2(rect.Right - 8, rect.Y + 8), color * 0.9f);
        }

        private static void DrawChoiceBorder(SpriteBatch spriteBatch, Rectangle rect, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1),
                new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1),
                new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height),
                new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height),
                new Rectangle(0, 0, 1, 1), color);
        }

        private static void DrawGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end,
            Color startColor, Color endColor, float thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) return;

            edge.Normalize();
            float rotation = (float)Math.Atan2(edge.Y, edge.X);
            int segments = Math.Max(1, (int)(length / 10f));

            for (int i = 0; i < segments; i++) {
                float t = (float)i / segments;
                Vector2 segPos = start + edge * (length * t);
                float segLength = length / segments;
                Color color = Color.Lerp(startColor, endColor, t);
                spriteBatch.Draw(pixel, segPos, new Rectangle(0, 0, 1, 1), color, rotation,
                    new Vector2(0, 0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0);
            }
        }

        private static void DrawStar(SpriteBatch spriteBatch, Vector2 pos, float size, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color, 0f,
                new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color * 0.8f, MathHelper.PiOver2,
                new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
        }

        private static void DrawCornerStar(SpriteBatch spriteBatch, Vector2 pos, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float size = 4f;
            spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color, 0f,
                new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.3f), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color * 0.7f, MathHelper.PiOver2,
                new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.3f), SpriteEffects.None, 0f);
        }
        #endregion
    }
}
