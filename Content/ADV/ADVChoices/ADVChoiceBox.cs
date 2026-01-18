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
            Tzeentch,   //奸奇魔法风格
            Sulfsea     //硫磺海风格
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

        #region 定时系统

        /// <summary>
        /// 当前定时配置
        /// </summary>
        private ChoiceBoxTimedConfig timedConfig;

        /// <summary>
        /// 剩余时间（帧）
        /// </summary>
        private int timedRemainingFrames;

        /// <summary>
        /// 总时间（帧）
        /// </summary>
        private int timedTotalFrames;

        /// <summary>
        /// 是否为定时选项框
        /// </summary>
        public bool IsTimed => timedConfig != null && timedTotalFrames > 0;

        /// <summary>
        /// 获取定时选项框的剩余时间比例（0~1，1为刚开始）
        /// </summary>
        public float TimedProgress => timedTotalFrames > 0 ? timedRemainingFrames / (float)timedTotalFrames : 0f;

        /// <summary>
        /// 获取定时选项框的已过时间比例（0~1，1为结束）
        /// </summary>
        public float TimedElapsed => 1f - TimedProgress;

        /// <summary>
        /// 获取剩余帧数（用于继承到其他系统）
        /// </summary>
        public static int RemainingFrames => Instance?.timedRemainingFrames ?? 0;

        /// <summary>
        /// 是否正在显示定时选项框
        /// </summary>
        public static bool IsTimedActive => Instance?.IsTimed == true && Instance?.isSelecting == true;

        /// <summary>
        /// 进度条默认颜色
        /// </summary>
        private static readonly Color DefaultProgressColor = new(100, 200, 255);
        private static readonly Color DefaultWarningColor = new(255, 150, 80);
        private static readonly Color DefaultDangerColor = new(255, 80, 80);

        /// <summary>
        /// 进度条粗细
        /// </summary>
        private const float ProgressBorderThickness = 3.5f;

        /// <summary>
        /// 进度条透明度
        /// </summary>
        private const float ProgressAlpha = 0.95f;

        /// <summary>
        /// 进度条发光层数
        /// </summary>
        private const int ProgressGlowLayers = 3;

        #endregion

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
            inst.styleInstances[ChoiceBoxStyle.Sulfsea] = new SulfseaChoiceBoxStyle();
            inst.currentStyle = inst.styleInstances[ChoiceBoxStyle.Default];
        }

        /// <summary>
        /// 显示选项框
        /// </summary>
        /// <param name="choices">选项列表</param>
        /// <param name="anchorProvider">锚点位置提供者</param>
        /// <param name="style">选项框样式</param>
        public static void Show(List<Choice> choices, Func<Vector2> anchorProvider = null, ChoiceBoxStyle style = ChoiceBoxStyle.Default) {
            Show(choices, anchorProvider, style, null);
        }

        /// <summary>
        /// 显示定时选项框
        /// </summary>
        /// <param name="choices">选项列表</param>
        /// <param name="timedConfig">定时配置</param>
        /// <param name="anchorProvider">锚点位置提供者</param>
        /// <param name="style">选项框样式</param>
        public static void ShowTimed(List<Choice> choices, ChoiceBoxTimedConfig timedConfig, Func<Vector2> anchorProvider = null, ChoiceBoxStyle style = ChoiceBoxStyle.Default) {
            Show(choices, anchorProvider, style, timedConfig);
        }

        /// <summary>
        /// 显示定时选项框（从剩余帧数继承）
        /// </summary>
        /// <param name="choices">选项列表</param>
        /// <param name="remainingFrames">剩余帧数</param>
        /// <param name="onTimeExpired">时间耗尽回调</param>
        /// <param name="anchorProvider">锚点位置提供者</param>
        /// <param name="style">选项框样式</param>
        public static void ShowTimedFromFrames(List<Choice> choices, int remainingFrames, Action onTimeExpired = null, Func<Vector2> anchorProvider = null, ChoiceBoxStyle style = ChoiceBoxStyle.Default) {
            var config = ChoiceBoxTimedConfig.FromRemainingFrames(remainingFrames, onTimeExpired);
            Show(choices, anchorProvider, style, config);
        }

        /// <summary>
        /// 显示选项框（完整参数）
        /// </summary>
        private static void Show(List<Choice> choices, Func<Vector2> anchorProvider, ChoiceBoxStyle style, ChoiceBoxTimedConfig timedConfig) {
            ADVChoiceBox inst = Instance;
            inst.choices.Clear();
            inst.choices.AddRange(choices);
            inst.isSelecting = true;
            inst.closing = false;
            inst.showProgress = 0f;
            inst.hideProgress = 0f;
            inst.hoveredIndex = -1;
            inst.selectedIndex = -1;

            //设置定时配置
            inst.timedConfig = timedConfig;
            if (timedConfig != null) {
                inst.timedTotalFrames = (int)(timedConfig.Duration * 60f);
                inst.timedRemainingFrames = inst.timedTotalFrames;
            }
            else {
                inst.timedTotalFrames = 0;
                inst.timedRemainingFrames = 0;
            }

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

            //重置定时状态
            inst.timedConfig = null;
            inst.timedTotalFrames = 0;
            inst.timedRemainingFrames = 0;

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
            currentStyle?.Update(panelRect, Active, closing);

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

                        //清理定时状态
                        timedConfig = null;
                        timedTotalFrames = 0;
                        timedRemainingFrames = 0;
                    }
                }
            }

            if (closing || showProgress < 0.5f) {
                return;
            }

            //更新定时逻辑（只有在选项框完全显示后才开始计时）
            if (showProgress >= 1f) {
                UpdateTimedLogic();
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

        /// <summary>
        /// 更新定时逻辑
        /// </summary>
        private void UpdateTimedLogic() {
            if (!IsTimed || !isSelecting) {
                return;
            }

            if (timedRemainingFrames > 0) {
                timedRemainingFrames--;

                //触发进度更新回调
                timedConfig.OnProgressUpdate?.Invoke(TimedProgress);

                //时间耗尽
                if (timedRemainingFrames <= 0) {
                    //触发时间耗尽回调
                    timedConfig.OnTimeExpired?.Invoke();

                    //隐藏选项框
                    Hide();
                }
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

            //绘制定时进度条（在内容之后绘制，作为叠加层）
            if (IsTimed && showProgress >= 1f && !closing) {
                DrawTimedProgressIndicator(spriteBatch, panelRect, alpha);
            }
        }

        #region 定时进度条绘制

        /// <summary>
        /// 绘制定时进度指示器（环绕边框渐变）
        /// </summary>
        private void DrawTimedProgressIndicator(SpriteBatch spriteBatch, Rectangle panelRect, float alpha) {
            if (timedConfig == null || !timedConfig.ShowProgressIndicator) {
                return;
            }

            float progress = TimedProgress;
            Color baseProgressColor = GetTimedProgressColor(progress);

            //添加呼吸效果（时间越少脉动越快）
            float pulseSpeed = MathHelper.Lerp(1.5f, 6f, 1f - progress);
            float pulse = (float)Math.Sin(Main.GameUpdateCount * 0.1f * pulseSpeed) * 0.2f + 0.8f;

            //添加流动效果
            float flowOffset = Main.GameUpdateCount * 0.02f * MathHelper.Lerp(1f, 3f, 1f - progress);

            //绘制多层发光效果（从外到内）
            for (int layer = ProgressGlowLayers - 1; layer >= 0; layer--) {
                float layerAlpha = (1f - layer / (float)ProgressGlowLayers) * 0.4f;
                float layerThickness = ProgressBorderThickness + layer * 2.5f;
                Color layerColor = baseProgressColor * (alpha * ProgressAlpha * layerAlpha * pulse);

                DrawProgressBorderWithFlow(spriteBatch, panelRect, progress, layerColor, layerThickness, flowOffset, layer > 0);
            }

            //绘制主进度条（最亮的核心层）
            Color coreColor = baseProgressColor * (alpha * ProgressAlpha * pulse);
            DrawProgressBorderWithFlow(spriteBatch, panelRect, progress, coreColor, ProgressBorderThickness, flowOffset, false);

            //绘制角落发光点
            DrawProgressCornerGlow(spriteBatch, panelRect, progress, baseProgressColor * (alpha * pulse));

            //绘制进度头部的亮点（追踪点）
            DrawProgressHead(spriteBatch, panelRect, progress, baseProgressColor * alpha, flowOffset);
        }

        /// <summary>
        /// 绘制带流动效果的进度边框
        /// </summary>
        private void DrawProgressBorderWithFlow(SpriteBatch spriteBatch, Rectangle panelRect, float progress, Color color, float thickness, float flowOffset, bool isGlowLayer) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //计算总周长
            float totalPerimeter = 2 * (panelRect.Width + panelRect.Height);
            float visibleLength = totalPerimeter * progress;

            //分段长度
            float topLength = panelRect.Width;
            float rightLength = panelRect.Height;
            float bottomLength = panelRect.Width;
            float leftLength = panelRect.Height;

            float drawnLength = 0f;

            //流动效果的alpha调制
            float flowAlphaBase = isGlowLayer ? 0.6f : 1f;

            //1. 绘制顶部边框（从左到右）
            if (drawnLength < visibleLength) {
                float segmentToDraw = Math.Min(topLength, visibleLength - drawnLength);
                float segmentProgress = segmentToDraw / topLength;

                DrawProgressSegmentWithFlow(spriteBatch, pixel,
                    new Vector2(panelRect.X, panelRect.Y),
                    new Vector2(panelRect.X + panelRect.Width * segmentProgress, panelRect.Y),
                    thickness, color, 1f * flowAlphaBase, MathHelper.Lerp(1f, 0.85f, segmentProgress) * flowAlphaBase,
                    flowOffset, false);

                drawnLength += topLength;
            }

            //2. 绘制右侧边框（从上到下）
            if (drawnLength < visibleLength) {
                float segmentToDraw = Math.Min(rightLength, visibleLength - drawnLength);
                float segmentProgress = segmentToDraw / rightLength;

                DrawProgressSegmentWithFlow(spriteBatch, pixel,
                    new Vector2(panelRect.Right - thickness, panelRect.Y),
                    new Vector2(panelRect.Right - thickness, panelRect.Y + panelRect.Height * segmentProgress),
                    thickness, color, 0.85f * flowAlphaBase, MathHelper.Lerp(0.85f, 0.7f, segmentProgress) * flowAlphaBase,
                    flowOffset, true);

                drawnLength += rightLength;
            }

            //3. 绘制底部边框（从右到左）
            if (drawnLength < visibleLength) {
                float segmentToDraw = Math.Min(bottomLength, visibleLength - drawnLength);
                float segmentProgress = segmentToDraw / bottomLength;

                DrawProgressSegmentWithFlow(spriteBatch, pixel,
                    new Vector2(panelRect.Right, panelRect.Bottom - thickness),
                    new Vector2(panelRect.Right - panelRect.Width * segmentProgress, panelRect.Bottom - thickness),
                    thickness, color, 0.7f * flowAlphaBase, MathHelper.Lerp(0.7f, 0.55f, segmentProgress) * flowAlphaBase,
                    flowOffset, false);

                drawnLength += bottomLength;
            }

            //4. 绘制左侧边框（从下到上）
            if (drawnLength < visibleLength) {
                float segmentToDraw = Math.Min(leftLength, visibleLength - drawnLength);
                float segmentProgress = segmentToDraw / leftLength;

                DrawProgressSegmentWithFlow(spriteBatch, pixel,
                    new Vector2(panelRect.X, panelRect.Bottom),
                    new Vector2(panelRect.X, panelRect.Bottom - panelRect.Height * segmentProgress),
                    thickness, color, 0.55f * flowAlphaBase, MathHelper.Lerp(0.55f, 0.4f, segmentProgress) * flowAlphaBase,
                    flowOffset, true);
            }
        }

        /// <summary>
        /// 绘制进度头部的追踪亮点
        /// </summary>
        private void DrawProgressHead(SpriteBatch spriteBatch, Rectangle panelRect, float progress, Color color, float flowOffset) {
            if (progress <= 0.01f) return;

            Texture2D pixel = CWRAsset.SoftGlow.Value;

            //计算进度头部位置
            float totalPerimeter = 2 * (panelRect.Width + panelRect.Height);
            float currentLength = totalPerimeter * progress;

            Vector2 headPos = GetPositionOnBorder(panelRect, currentLength);

            //绘制多层发光的追踪点
            float baseSize = pixel.Width / 2f;

            //脉动效果
            float headPulse = (float)Math.Sin(Main.GameUpdateCount * 0.15f) * 0.3f + 0.7f;

            //外层大发光
            float outerSize = baseSize * 2.5f * headPulse;
            Rectangle outerRect = new(
                (int)(headPos.X - outerSize / 2),
                (int)(headPos.Y - outerSize / 2),
                (int)outerSize,
                (int)outerSize
            );
            spriteBatch.Draw(pixel, outerRect, null, color with { A = 0 } * 0.15f);

            //中层发光
            float midSize = baseSize * 1.5f * headPulse;
            Rectangle midRect = new(
                (int)(headPos.X - midSize / 2),
                (int)(headPos.Y - midSize / 2),
                (int)midSize,
                (int)midSize
            );
            spriteBatch.Draw(pixel, midRect, null, color with { A = 0 } * 0.4f);

            //核心亮点
            float coreSize = baseSize * 0.8f;
            Rectangle coreRect = new(
                (int)(headPos.X - coreSize / 2),
                (int)(headPos.Y - coreSize / 2),
                (int)coreSize,
                (int)coreSize
            );
            spriteBatch.Draw(pixel, coreRect, null, Color.White with { A = 0 } * 0.9f);
        }

        /// <summary>
        /// 根据边框上的长度获取位置
        /// </summary>
        private Vector2 GetPositionOnBorder(Rectangle rect, float length) {
            float topLength = rect.Width;
            float rightLength = rect.Height;
            float bottomLength = rect.Width;

            //顶部
            if (length <= topLength) {
                return new Vector2(rect.X + length, rect.Y);
            }
            length -= topLength;

            //右侧
            if (length <= rightLength) {
                return new Vector2(rect.Right, rect.Y + length);
            }
            length -= rightLength;

            //底部
            if (length <= bottomLength) {
                return new Vector2(rect.Right - length, rect.Bottom);
            }
            length -= bottomLength;

            //左侧
            return new Vector2(rect.X, rect.Bottom - length);
        }

        /// <summary>
        /// 获取当前定时进度的颜色
        /// </summary>
        private Color GetTimedProgressColor(float progress) {
            Color baseColor = timedConfig?.ProgressColor ?? DefaultProgressColor;
            Color warningColor = timedConfig?.WarningColor ?? DefaultWarningColor;
            Color dangerColor = timedConfig?.DangerColor ?? DefaultDangerColor;

            float warningThreshold = timedConfig?.WarningThreshold ?? 0.35f;
            float dangerThreshold = timedConfig?.DangerThreshold ?? 0.15f;

            if (progress <= dangerThreshold) {
                return dangerColor;
            }
            else if (progress <= warningThreshold) {
                float t = (progress - dangerThreshold) / (warningThreshold - dangerThreshold);
                return Color.Lerp(dangerColor, warningColor, t);
            }
            else {
                float t = (progress - warningThreshold) / (1f - warningThreshold);
                return Color.Lerp(warningColor, baseColor, t);
            }
        }

        /// <summary>
        /// 绘制带流动效果的进度条段
        /// </summary>
        private void DrawProgressSegmentWithFlow(
            SpriteBatch spriteBatch,
            Texture2D pixel,
            Vector2 start,
            Vector2 end,
            float thickness,
            Color color,
            float startAlpha,
            float endAlpha,
            float flowOffset,
            bool vertical) {
            if (vertical) {
                float height = Math.Abs(end.Y - start.Y);
                if (height < 1f) return;

                float minY = Math.Min(start.Y, end.Y);
                int segments = Math.Max(1, (int)(height / 6f));

                for (int i = 0; i < segments; i++) {
                    float t = i / (float)segments;
                    float t2 = (i + 1) / (float)segments;
                    float y1 = minY + height * t;
                    float y2 = minY + height * t2;

                    //添加流动的明暗变化
                    float flowWave = (float)Math.Sin((t + flowOffset) * MathHelper.TwoPi * 2f) * 0.15f + 0.85f;
                    float segAlpha = MathHelper.Lerp(startAlpha, endAlpha, t) * flowWave;

                    Rectangle rect = new((int)start.X, (int)y1, (int)thickness, (int)(y2 - y1 + 1));
                    spriteBatch.Draw(pixel, rect, new Rectangle(0, 0, 1, 1), color * segAlpha);
                }
            }
            else {
                float width = Math.Abs(end.X - start.X);
                if (width < 1f) return;

                float minX = Math.Min(start.X, end.X);
                int segments = Math.Max(1, (int)(width / 6f));

                for (int i = 0; i < segments; i++) {
                    float t = i / (float)segments;
                    float t2 = (i + 1) / (float)segments;
                    float x1 = minX + width * t;
                    float x2 = minX + width * t2;

                    //添加流动的明暗变化
                    float flowWave = (float)Math.Sin((t + flowOffset) * MathHelper.TwoPi * 2f) * 0.15f + 0.85f;
                    float segAlpha = MathHelper.Lerp(startAlpha, endAlpha, t) * flowWave;

                    Rectangle rect = new((int)x1, (int)start.Y, (int)(x2 - x1 + 1), (int)thickness);
                    spriteBatch.Draw(pixel, rect, new Rectangle(0, 0, 1, 1), color * segAlpha);
                }
            }
        }

        /// <summary>
        /// 绘制进度条角落的发光效果
        /// </summary>
        private void DrawProgressCornerGlow(SpriteBatch spriteBatch, Rectangle panelRect, float progress, Color color) {
            if (progress <= 0.01f) return;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            float glowSize = ProgressBorderThickness * 3f;

            //脉动效果
            float cornerPulse = (float)Math.Sin(Main.GameUpdateCount * 0.08f) * 0.2f + 0.8f;

            //计算总周长
            float totalPerimeter = 2 * (panelRect.Width + panelRect.Height);
            float visibleLength = totalPerimeter * progress;

            //顶部起始点发光（左上角）
            float cornerAlpha = MathHelper.Clamp(progress * 3f, 0f, 1f) * 0.7f * cornerPulse;
            DrawCornerGlow(spriteBatch, pixel, new Vector2(panelRect.X, panelRect.Y), glowSize, color * cornerAlpha);

            //右上角
            if (visibleLength > panelRect.Width) {
                float rightTopAlpha = MathHelper.Clamp((visibleLength - panelRect.Width) / panelRect.Height, 0f, 1f) * 0.6f * cornerPulse;
                DrawCornerGlow(spriteBatch, pixel, new Vector2(panelRect.Right, panelRect.Y), glowSize, color * rightTopAlpha);
            }

            //右下角
            if (visibleLength > panelRect.Width + panelRect.Height) {
                float rightBottomAlpha = MathHelper.Clamp((visibleLength - panelRect.Width - panelRect.Height) / panelRect.Width, 0f, 1f) * 0.5f * cornerPulse;
                DrawCornerGlow(spriteBatch, pixel, new Vector2(panelRect.Right, panelRect.Bottom), glowSize, color * rightBottomAlpha);
            }

            //左下角
            if (visibleLength > 2 * panelRect.Width + panelRect.Height) {
                float leftBottomAlpha = MathHelper.Clamp((visibleLength - 2 * panelRect.Width - panelRect.Height) / panelRect.Height, 0f, 1f) * 0.4f * cornerPulse;
                DrawCornerGlow(spriteBatch, pixel, new Vector2(panelRect.X, panelRect.Bottom), glowSize, color * leftBottomAlpha);
            }
        }

        /// <summary>
        /// 绘制单个角落的发光
        /// </summary>
        private void DrawCornerGlow(SpriteBatch spriteBatch, Texture2D pixel, Vector2 position, float size, Color color) {
            //外层大发光
            float outerSize = size * 1.8f;
            Rectangle outerRect = new(
                (int)(position.X - outerSize / 2),
                (int)(position.Y - outerSize / 2),
                (int)outerSize,
                (int)outerSize
            );
            spriteBatch.Draw(pixel, outerRect, new Rectangle(0, 0, 1, 1), color * 0.2f);

            //中层发光
            Rectangle glowRect = new(
                (int)(position.X - size / 2),
                (int)(position.Y - size / 2),
                (int)size,
                (int)size
            );
            spriteBatch.Draw(pixel, glowRect, new Rectangle(0, 0, 1, 1), color * 0.5f);

            //中心更亮
            Rectangle centerRect = new(
                (int)(position.X - size / 4),
                (int)(position.Y - size / 4),
                (int)(size / 2),
                (int)(size / 2)
            );
            spriteBatch.Draw(pixel, centerRect, new Rectangle(0, 0, 1, 1), color * 0.8f);
        }

        #endregion

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
