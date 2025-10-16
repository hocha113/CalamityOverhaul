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

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV
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
    /// ADV 选项框 UI，参考 ResurrectionUI 的绘制风格
    /// </summary>
    internal class ADVChoiceBox : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.HalibutText.ADV";

        public static ADVChoiceBox Instance => UIHandleLoader.GetUIHandleOfType<ADVChoiceBox>();

        private readonly List<Choice> choices = new();
        private int hoveredIndex = -1;
        private int selectedIndex = -1;
        private bool isSelecting = false;
        
        // 动画状态
        private float showProgress = 0f;
        private float hideProgress = 0f;
        private const float ShowDuration = 12f;
        private const float HideDuration = 10f;
        private bool closing = false;
        
        // 选项悬停动画
        private readonly float[] choiceHoverProgress = new float[10]; // 支持最多10个选项
        private const float HoverSpeed = 0.15f;
        
        // 位置和尺寸
        private Vector2 anchorPosition;
        private Vector2 panelSize;
        private Rectangle panelRect;
        
        // 布局常量
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

        // 本地化文本
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
        public static void Show(List<Choice> choices, Func<Vector2> anchorProvider = null) {
            var inst = Instance;
            inst.choices.Clear();
            inst.choices.AddRange(choices);
            inst.isSelecting = true;
            inst.closing = false;
            inst.showProgress = 0f;
            inst.hideProgress = 0f;
            inst.hoveredIndex = -1;
            inst.selectedIndex = -1;
            
            // 重置悬停动画
            for (int i = 0; i < inst.choiceHoverProgress.Length; i++) {
                inst.choiceHoverProgress[i] = 0f;
            }
            
            // 计算锚点位置
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
            
            // 计算面板尺寸
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

            // 计算标题尺寸
            string title = TitleText.Value;
            float titleHeight = FontAssets.MouseText.Value.MeasureString(title).Y * 0.9f;
            
            // 计算选项区域所需宽度
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
            
            // 计算面板高度
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

            // 动画更新
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

            // 更新面板矩形
            float progress = closing ? (1f - hideProgress) : showProgress;
            float eased = closing ? EaseInCubic(progress) : EaseOutBack(progress);
            
            Vector2 drawPos = anchorPosition - new Vector2(panelSize.X / 2f, panelSize.Y / 2f);
            drawPos.Y += (1f - eased) * 60f;
            
            panelRect = new Rectangle((int)drawPos.X, (int)drawPos.Y, (int)panelSize.X, (int)panelSize.Y);

            // 检测鼠标悬停
            Point mousePos = new Point(Main.mouseX, Main.mouseY);
            bool hoverInPanel = panelRect.Contains(mousePos);
            
            if (hoverInPanel) {
                player.mouseInterface = true;
            }

            int oldHoveredIndex = hoveredIndex;
            hoveredIndex = -1;

            if (hoverInPanel && isSelecting) {
                // 计算每个选项的矩形
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
                        
                        // 点击处理
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

            // 更新悬停动画
            for (int i = 0; i < choiceHoverProgress.Length && i < choices.Count; i++) {
                float target = i == hoveredIndex ? 1f : 0f;
                choiceHoverProgress[i] = MathHelper.Lerp(choiceHoverProgress[i], target, HoverSpeed);
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (showProgress <= 0.01f && !closing) {
                return;
            }

            float progress = closing ? (1f - hideProgress) : showProgress;
            if (progress <= 0f) {
                return;
            }

            float alpha = Math.Min(progress * 1.5f, 1f);
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            // 绘制阴影
            Rectangle shadowRect = panelRect;
            shadowRect.Offset(3, 3);
            spriteBatch.Draw(pixel, shadowRect, new Rectangle(0, 0, 1, 1), Color.Black * 0.5f * alpha);

            // 绘制背景
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

            // 绘制边框
            Color edgeColor = new Color(70, 180, 230) * (alpha * 0.75f);
            DrawBorder(spriteBatch, panelRect, edgeColor);

            // 绘制标题
            Vector2 titlePos = new Vector2(panelRect.X + HorizontalPadding, panelRect.Y + TopPadding);
            string title = TitleText.Value;
            
            for (int i = 0; i < 4; i++) {
                float ang = MathHelper.TwoPi * i / 4f;
                Vector2 o = ang.ToRotationVector2() * 1.25f;
                Utils.DrawBorderString(spriteBatch, title, titlePos + o, edgeColor * 0.55f, 0.9f);
            }
            Utils.DrawBorderString(spriteBatch, title, titlePos, Color.White * alpha, 0.9f);

            // 绘制分割线
            float titleHeight = FontAssets.MouseText.Value.MeasureString(title).Y * 0.9f;
            Vector2 dividerStart = titlePos + new Vector2(0, titleHeight + TitleExtra);
            Vector2 dividerEnd = dividerStart + new Vector2(panelSize.X - HorizontalPadding * 2, 0);
            DrawGradientLine(spriteBatch, dividerStart, dividerEnd, edgeColor * 0.9f, edgeColor * 0.05f, 1.3f);

            // 绘制选项
            Vector2 choiceStartPos = dividerStart + new Vector2(0, DividerSpacing + 1.3f);
            DrawChoices(spriteBatch, choiceStartPos, alpha, edgeColor);

            // 绘制装饰星星
            float starTime = Main.GlobalTimeWrappedHourly * 3f;
            Vector2 star1 = new Vector2(panelRect.Right - 18, panelRect.Y + 14);
            float s1a = ((float)Math.Sin(starTime) * 0.5f + 0.5f) * alpha;
            DrawStar(spriteBatch, star1, 3.5f, edgeColor * s1a);
        }

        private void DrawChoices(SpriteBatch spriteBatch, Vector2 startPos, float alpha, Color edgeColor) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            
            for (int i = 0; i < choices.Count; i++) {
                var choice = choices[i];
                Vector2 choicePos = startPos + new Vector2(0, i * (ChoiceHeight + ChoiceSpacing));
                
                // 选项背景
                Rectangle choiceRect = new Rectangle(
                    (int)choicePos.X,
                    (int)choicePos.Y,
                    (int)(panelSize.X - HorizontalPadding * 2),
                    (int)ChoiceHeight
                );
                
                // 悬停效果
                float hoverProgress = choiceHoverProgress[i];
                Color choiceBg = choice.Enabled 
                    ? Color.Lerp(new Color(20, 35, 50) * 0.3f, new Color(40, 70, 100) * 0.5f, hoverProgress)
                    : new Color(30, 30, 35) * 0.2f;
                
                spriteBatch.Draw(pixel, choiceRect, new Rectangle(0, 0, 1, 1), choiceBg * alpha);
                
                // 选项边框
                if (hoverProgress > 0.01f) {
                    DrawChoiceBorder(spriteBatch, choiceRect, edgeColor * (hoverProgress * 0.6f * alpha));
                }
                
                // 选项文本
                string text = choice.Text;
                Color textColor = choice.Enabled ? Color.White : new Color(120, 120, 130);
                
                Vector2 textPos = new Vector2(choiceRect.X + ChoicePadding, choiceRect.Y + ChoiceHeight / 2f);
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.75f;
                textPos.Y -= textSize.Y / 2f;
                
                // 文本发光效果（仅启用的选项）
                if (choice.Enabled && hoverProgress > 0.3f) {
                    for (int j = 0; j < 4; j++) {
                        float ang = MathHelper.TwoPi * j / 4f;
                        Vector2 offset = ang.ToRotationVector2() * (1f * hoverProgress);
                        Utils.DrawBorderString(spriteBatch, text, textPos + offset, 
                            edgeColor * (0.3f * hoverProgress * alpha), 0.75f);
                    }
                }
                
                Utils.DrawBorderString(spriteBatch, text, textPos, textColor * alpha, 0.75f);
                
                // 禁用提示
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
                
                // 选项序号
                string indexText = $"{i + 1}.";
                Vector2 indexPos = new Vector2(
                    choiceRect.X - 18f,
                    textPos.Y
                );
                Utils.DrawBorderString(spriteBatch, indexText, indexPos, 
                    edgeColor * (0.5f + hoverProgress * 0.5f) * alpha, 0.7f);
            }
        }

        private void DrawBorder(SpriteBatch spriteBatch, Rectangle rect, Color color) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            
            // 上下左右边框
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), 
                new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), 
                new Rectangle(0, 0, 1, 1), color * 0.7f);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), 
                new Rectangle(0, 0, 1, 1), color * 0.85f);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), 
                new Rectangle(0, 0, 1, 1), color * 0.85f);
            
            // 角落装饰
            DrawCornerStar(spriteBatch, new Vector2(rect.X + 8, rect.Y + 8), color * 0.9f);
            DrawCornerStar(spriteBatch, new Vector2(rect.Right - 8, rect.Y + 8), color * 0.9f);
        }

        private void DrawChoiceBorder(SpriteBatch spriteBatch, Rectangle rect, Color color) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), 
                new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), 
                new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), 
                new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), 
                new Rectangle(0, 0, 1, 1), color);
        }

        private void DrawGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, 
            Color startColor, Color endColor, float thickness) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
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

        private void DrawStar(SpriteBatch spriteBatch, Vector2 pos, float size, Color color) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color, 0f, 
                new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color * 0.8f, MathHelper.PiOver2, 
                new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
        }

        private void DrawCornerStar(SpriteBatch spriteBatch, Vector2 pos, Color color) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            float size = 4f;
            spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color, 0f, 
                new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.3f), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color * 0.7f, MathHelper.PiOver2, 
                new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.3f), SpriteEffects.None, 0f);
        }

        private static float EaseOutBack(float t) {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * (float)Math.Pow(t - 1, 3) + c1 * (float)Math.Pow(t - 1, 2);
        }

        private static float EaseInCubic(float t) {
            return t * t * t;
        }
    }
}
