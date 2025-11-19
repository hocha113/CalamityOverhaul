using CalamityOverhaul.Content.ADV.ADVChoices.Styles;
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

namespace CalamityOverhaul.Content.ADV.ADVChoices
{
    /// <summary>
    /// ADV选项框UI，参考ResurrectionUI的绘制风格
    /// </summary>
    public class ADVChoiceBox : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        public static ADVChoiceBox Instance => UIHandleLoader.GetUIHandleOfType<ADVChoiceBox>();

        /// <summary>
        /// 选项框样式枚举
        /// </summary>
        public enum ChoiceBoxStyle
        {
            Default,    //默认深蓝科技风格
            Brimstone,  //硫磺火风格
            Draedon,    //嘉登科技风格
            Tzeentch    //奸奇魔法风格
        }

        private readonly List<Choice> choices = new();
        private int hoveredIndex = -1;
        private int selectedIndex = -1;
        private bool isSelecting = false;

        //样式系统
        private ChoiceBoxStyle currentStyleType = ChoiceBoxStyle.Default;
        private IChoiceBoxStyle currentStyle;
        private readonly Dictionary<ChoiceBoxStyle, IChoiceBoxStyle> styleInstances = new();

        /// <summary>
        /// 悬停状态变化事件
        /// </summary>
        public static event EventHandler<ChoiceHoverEventArgs> OnHoverChanged;

        /// <summary>
        /// 获取当前悬停的选项索引（-1表示无悬停）
        /// </summary>
        public static int CurrentHoveredIndex => Instance?.hoveredIndex ?? -1;

        /// <summary>
        /// 获取当前悬停的选项对象（如果有）
        /// </summary>
        public static Choice CurrentHoveredChoice {
            get {
                var inst = Instance;
                if (inst == null || inst.hoveredIndex < 0 || inst.hoveredIndex >= inst.choices.Count) {
                    return null;
                }
                return inst.choices[inst.hoveredIndex];
            }
        }

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

        private static Func<Vector2> AnchorProvider = null;

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

            //初始化样式实例
            var inst = Instance;
            inst.styleInstances[ChoiceBoxStyle.Default] = new DefaultChoiceBoxStyle();
            inst.styleInstances[ChoiceBoxStyle.Brimstone] = new BrimstoneChoiceBoxStyle();
            inst.styleInstances[ChoiceBoxStyle.Draedon] = new DraedonChoiceBoxStyle();
            inst.styleInstances[ChoiceBoxStyle.Tzeentch] = new TzeentchChoiceBoxStyle();
            inst.currentStyle = inst.styleInstances[ChoiceBoxStyle.Default];
        }

        /// <summary>
        /// 显示选项框
        /// </summary>
        /// <param name="choices">选项列表</param>
        /// <param name="anchorProvider">锚点位置提供者</param>
        /// <param name="style">选项框样式</param>
        public static void Show(List<Choice> choices, Func<Vector2> anchorProvider = null, ChoiceBoxStyle style = ChoiceBoxStyle.Default) {
            ADVChoiceBox inst = Instance;
            inst.choices.Clear();
            inst.choices.AddRange(choices);
            inst.isSelecting = true;
            inst.closing = false;
            inst.showProgress = 0f;
            inst.hideProgress = 0f;
            inst.hoveredIndex = -1;
            inst.selectedIndex = -1;

            //切换样式
            inst.currentStyleType = style;
            if (inst.styleInstances.TryGetValue(style, out var styleInstance)) {
                inst.currentStyle = styleInstance;
                inst.currentStyle.Reset();
            }

            //重置悬停动画
            for (int i = 0; i < inst.choiceHoverProgress.Length; i++) {
                inst.choiceHoverProgress[i] = 0f;
            }

            AnchorProvider = anchorProvider;

            //在更新锚点之前计算面板尺寸
            inst.CalculatePanelSize();

            //初始化锚点位置
            UpdateAnchorPosition(inst);
        }

        /// <summary>
        /// 隐藏选项框
        /// </summary>
        public static void Hide() {
            var inst = Instance;
            inst.closing = true;
            inst.hideProgress = 0f;

            //清空事件订阅
            OnHoverChanged = null;
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

        /// <summary>
        /// 更新锚点位置
        /// </summary>
        public static void UpdateAnchorPosition(ADVChoiceBox inst) {
            //计算锚点位置
            if (AnchorProvider != null) {
                inst.anchorPosition = AnchorProvider.Invoke();
            }
            else {
                inst.anchorPosition = new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.65f);
            }
        }

        public override void Update() {
            if (choices.Count == 0 && !closing) {
                return;
            }

            //更新样式动画
            if (currentStyle != null) {
                currentStyle.Update(panelRect, Active, closing);
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

            //在动画完成后每帧更新锚点位置
            if (showProgress >= 1f) {
                UpdateAnchorPosition(Instance);
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

            //触发悬停变化事件
            if (oldHoveredIndex != hoveredIndex) {
                Choice oldChoice = oldHoveredIndex >= 0 && oldHoveredIndex < choices.Count ? choices[oldHoveredIndex] : null;
                Choice newChoice = hoveredIndex >= 0 && hoveredIndex < choices.Count ? choices[hoveredIndex] : null;

                OnHoverChanged?.Invoke(this, new ChoiceHoverEventArgs(hoveredIndex, oldHoveredIndex, newChoice, oldChoice));
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

            //使用当前样式绘制
            if (currentStyle != null) {
                currentStyle.Draw(spriteBatch, panelRect, alpha);
            }

            if (choices.Count == 0) {
                return;
            }

            DrawContent(spriteBatch, panelRect, alpha);
        }

        private void DrawContent(SpriteBatch spriteBatch, Rectangle panelRect, float alpha) {
            //绘制标题
            Vector2 titlePos = new Vector2(panelRect.X + HorizontalPadding, panelRect.Y + TopPadding);
            string title = TitleText.Value;

            //使用样式绘制标题装饰
            if (currentStyle != null) {
                currentStyle.DrawTitleDecoration(spriteBatch, titlePos, title, alpha);
            }

            Utils.DrawBorderString(spriteBatch, title, titlePos, Color.White * alpha, 0.9f);

            //绘制分割线
            float titleHeight = FontAssets.MouseText.Value.MeasureString(title).Y * 0.9f;
            Vector2 dividerStart = titlePos + new Vector2(0, titleHeight + TitleExtra);
            Vector2 dividerEnd = dividerStart + new Vector2(panelSize.X - HorizontalPadding * 2, 0);

            if (currentStyle != null) {
                currentStyle.DrawDivider(spriteBatch, dividerStart, dividerEnd, alpha);
            }

            //绘制选项
            Vector2 choiceStartPos = dividerStart + new Vector2(0, DividerSpacing + 1.3f);
            DrawChoices(spriteBatch, choiceStartPos, alpha);
        }

        private void DrawChoices(SpriteBatch spriteBatch, Vector2 startPos, float alpha) {
            Color edgeColor = currentStyle?.GetEdgeColor(alpha) ?? Color.White;

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

                //使用样式绘制选项背景
                if (currentStyle != null) {
                    currentStyle.DrawChoiceBackground(spriteBatch, choiceRect, choice.Enabled, hoverProgress, alpha);
                }

                //选项文本
                DrawChoiceText(spriteBatch, choice, choiceRect, alpha, edgeColor, hoverProgress, i);
            }
        }

        private void DrawChoiceText(SpriteBatch spriteBatch, Choice choice, Rectangle choiceRect, float alpha, Color edgeColor, float hoverProgress, int index) {
            string text = choice.Text;
            Color textColor = choice.Enabled ? Color.White : new Color(80, 80, 85);

            Vector2 textPos = new Vector2(choiceRect.X + ChoicePadding, choiceRect.Y + ChoiceHeight / 2f);
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.75f;
            textPos.Y -= textSize.Y / 2f;

            //文本发光效果
            if (choice.Enabled && hoverProgress > 0.3f && currentStyle != null) {
                Color glowColor = currentStyle.GetTextGlowColor(alpha, hoverProgress);
                for (int j = 0; j < 4; j++) {
                    float ang = MathHelper.TwoPi * j / 4f;
                    Vector2 offset = ang.ToRotationVector2() * (1f * hoverProgress);
                    Utils.DrawBorderString(spriteBatch, text, textPos + offset,
                        glowColor * (0.3f * hoverProgress), 0.75f);
                }
            }

            //绘制主文本
            float textAlpha = choice.Enabled ? alpha : alpha * 0.35f;
            Utils.DrawBorderString(spriteBatch, text, textPos, textColor * textAlpha, 0.75f);

            //禁用提示
            if (!choice.Enabled && !string.IsNullOrEmpty(choice.DisabledHint)) {
                string hint = string.Format(DisabledHintFormat.Value, choice.DisabledHint);
                Vector2 hintSize = FontAssets.MouseText.Value.MeasureString(hint) * 0.65f;
                Vector2 hintPos = new Vector2(
                    choiceRect.Right - ChoicePadding - hintSize.X,
                    textPos.Y + 2f
                );
                Utils.DrawBorderString(spriteBatch, hint, hintPos,
                    new Color(150, 80, 80) * (alpha * 0.6f), 0.65f);
            }

            //选项序号
            string indexText = $"{index + 1}.";
            Vector2 indexPos = new Vector2(
                choiceRect.X - 18f,
                textPos.Y
            );
            Color indexColor = choice.Enabled
                ? edgeColor * (0.5f + hoverProgress * 0.5f)
                : new Color(60, 60, 70) * 0.4f;
            Utils.DrawBorderString(spriteBatch, indexText, indexPos,
                indexColor * alpha, 0.7f);
        }
    }
}
