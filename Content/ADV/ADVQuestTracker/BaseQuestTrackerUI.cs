using CalamityOverhaul.Content.ADV.Common.QuestTrackerStyles;
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
using Terraria.ModLoader.IO;
using static CalamityOverhaul.Content.ADV.Common.BaseDamageTracker;

namespace CalamityOverhaul.Content.ADV.ADVQuestTracker
{
    /// <summary>
    /// 任务追踪UI风格枚举
    /// </summary>
    public enum QuestTrackerStyle
    {
        Brimstone,  //硫磺火风格
        Draedon,    //嘉登科技风格
        Sulfsea     //硫磺海风格
    }

    /// <summary>
    /// 任务追踪UI的通用基类
    /// </summary>
    internal abstract class BaseQuestTrackerUI : UIHandle, ILocalizedModType
    {
        public virtual string LocalizationCategory => "UI";

        //本地化文本
        protected LocalizedText QuestTitle { get; set; }
        protected LocalizedText DamageContribution { get; set; }
        protected LocalizedText RequiredContribution { get; set; }

        //UI参数
        protected const float PanelWidth = 220f;
        protected const float MinPanelHeight = 90f;
        protected const float MaxPanelHeight = 150f;
        protected float currentPanelHeight = MinPanelHeight;
        protected const float Padding = 10f;

        //位置和拖拽
        private bool dragBool;
        private float dragOffset;
        protected float screenYValue = 0;
        protected bool initialize = true;
        protected virtual float ScreenX => 0f;
        protected virtual float ScreenY => screenYValue;

        //动画参数
        protected float slideProgress = 0f;
        protected float pulseTimer = 0f;
        protected float borderGlow = 1f;
        protected float warningPulse = 0f;

        //折叠状态
        protected bool isCollapsed = false;
        protected float collapseProgress = 0f;
        protected const float CollapseAnimationSpeed = 0.12f;
        protected Rectangle collapseButtonRect;
        protected bool collapseButtonHovered = false;
        protected float buttonPositionProgress = 0f;
        protected float buttonGlowPulse = 0f;

        //伤害数据
        protected float cachedContribution = 0f;
        protected const float UpdateInterval = 0.5f;
        protected float updateTimer = 0f;

        //碰撞检测与半透明化
        protected bool isOverlappingWithNPC = false;
        protected float overlappingAlpha = 1f;
        protected const float MinOverlappingAlpha = 0.3f;
        protected const float AlphaTransitionSpeed = 0.1f;

        //样式系统
        protected QuestTrackerStyle currentStyleType = QuestTrackerStyle.Brimstone;
        protected IQuestTrackerStyle currentStyle;
        protected readonly Dictionary<QuestTrackerStyle, IQuestTrackerStyle> styleInstances = new();

        /// <summary>
        /// 设置默认屏幕Y值
        /// </summary>
        internal void SetDefScreenYValue() => initialize = true;

        /// <summary>
        /// 加载UI数据
        /// </summary>
        public new void LoadUIData(TagCompound tag) {
            tag.TryGet(Name + ":" + nameof(screenYValue), out screenYValue);
            tag.TryGet(Name + ":" + nameof(isCollapsed), out isCollapsed);
            LoadUI(tag);
        }

        /// <summary>
        /// 加载UI数据
        /// </summary>
        /// <param name="tag"></param>
        public virtual void LoadUI(TagCompound tag) {

        }

        /// <summary>
        /// 保存UI数据
        /// </summary>
        public new void SaveUIData(TagCompound tag) {
            tag[Name + ":" + nameof(screenYValue)] = screenYValue;
            tag[Name + ":" + nameof(isCollapsed)] = isCollapsed;
            SaveUI(tag);
        }

        /// <summary>
        /// 保存UI数据
        /// </summary>
        /// <param name="tag"></param>
        public virtual void SaveUI(TagCompound tag) {

        }

        /// <summary>
        /// 获取当前应使用的风格（子类可重写）
        /// </summary>
        protected virtual QuestTrackerStyle GetQuestStyle() => QuestTrackerStyle.Brimstone;

        /// <summary>
        /// 获取当前伤害追踪数据
        /// </summary>
        protected abstract (float current, float total, bool isActive) GetTrackingData();

        /// <summary>
        /// 目标NPC类型（如果不需要NPC追踪，返回-1）
        /// </summary>
        public abstract int TargetNPCType { get; }

        public override bool Active => slideProgress > 0f || CanOpne;

        /// <summary>
        /// 是否可以打开UI面板
        /// </summary>
        public virtual bool CanOpne {
            get {
                if (CurrentDamageTrackerInstance == null) {
                    return false;
                }
                if (!CurrentDamageTrackerInstance.NPC.Alives()) {
                    return false;
                }
                if (CurrentDamageTrackerInstance.NPC.type != TargetNPCType) {
                    return false;
                }
                return CurrentDamageTrackerInstance.IsQuestActive(Main.LocalPlayer) && GetDamageTrackingData().isActive;
            }
        }

        /// <summary>
        /// 获取需求的伤害贡献度阈值
        /// </summary>
        protected abstract float GetRequiredContribution();

        public override void SetStaticDefaults() {
            SetupLocalizedTexts();

            //初始化样式实例
            styleInstances[QuestTrackerStyle.Brimstone] = new BrimstoneTrackerStyle();
            styleInstances[QuestTrackerStyle.Draedon] = new DraedonTrackerStyle();
            styleInstances[QuestTrackerStyle.Sulfsea] = new SulfseaTrackerStyle();
            currentStyle = styleInstances[GetQuestStyle()];
        }

        protected abstract void SetupLocalizedTexts();

        /// <summary>
        /// 检测UI面板是否与目标NPC碰撞箱重叠
        /// </summary>
        protected virtual bool CheckNPCOverlap() {
            if (CurrentDamageTrackerInstance?.NPC == null || !CurrentDamageTrackerInstance.NPC.active) {
                return false;
            }

            var otherNPCs = CurrentDamageTrackerInstance.OtherNPCType;
            foreach (var n in Main.ActiveNPCs) {
                if (n.type != TargetNPCType && !otherNPCs.Contains(n.type)) {
                    continue;
                }

                int extend = 80;
                Vector2 npcScreenPos = n.position - Main.screenPosition;
                Rectangle npcScreenRect = new(
                    (int)npcScreenPos.X - extend,
                    (int)npcScreenPos.Y - extend,
                    n.width + extend * 2,
                    n.height + extend * 2
                );

                Rectangle uiRect = DrawPosition.GetRectangle((int)PanelWidth, (int)currentPanelHeight);
                bool result = npcScreenRect.Intersects(uiRect);
                if (result) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 计算文本换行后的高度（不实际绘制）
        /// </summary>
        protected virtual float CalculateTextHeight(string text, float scale = 0.75f, float maxWidth = -1f) {
            if (string.IsNullOrEmpty(text)) {
                return 0f;
            }

            var font = FontAssets.MouseText.Value;
            if (maxWidth <= 0) {
                maxWidth = PanelWidth - 20f;
            }

            List<string> lines = WrapText(text, font, maxWidth, scale);
            float lineSpacing = font.MeasureString("A").Y * scale * 0.9f;

            return lines.Count * lineSpacing;
        }

        /// <summary>
        /// 计算标题换行后需要的高度
        /// </summary>
        protected virtual float CalculateTitleHeight() {
            const float titleScale = 0.75f;
            return CalculateTextHeight(QuestTitle.Value, titleScale);
        }

        /// <summary>
        /// 计算内容区域的总高度（子类可重写以自定义计算）
        /// </summary>
        protected virtual float CalculateContentHeight() {
            const float textScale = 0.65f;

            //计算各部分高度
            float topPadding = 8f;
            float titleHeight = CalculateTitleHeight();
            float titleBottomMargin = 2f;
            float dividerHeight = 2f;
            float dividerBottomMargin = 8f;

            //贡献度文本
            float contributionTextHeight = FontAssets.MouseText.Value.MeasureString("A").Y * textScale;
            float contributionBottomMargin = 15f;

            //需求文本
            float requirementHeight = CalculateTextHeight(RequiredContribution.Value, 0.6f);
            float requirementBottomMargin = 2f;

            //进度条
            float progressBarHeight = 6f;
            float bottomPadding = 8f;

            //总高度
            return topPadding
                + titleHeight + titleBottomMargin
                + dividerHeight + dividerBottomMargin
                + contributionTextHeight + contributionBottomMargin
                + requirementHeight + requirementBottomMargin
                + progressBarHeight
                + bottomPadding;
        }

        /// <summary>
        /// 根据内容动态调整面板高度
        /// </summary>
        protected virtual void UpdatePanelHeight() {
            float contentHeight = CalculateContentHeight();
            currentPanelHeight = Math.Clamp(contentHeight, MinPanelHeight, MaxPanelHeight);
        }

        public override void Update() {
            //初始化屏幕位置
            if (initialize) {
                initialize = false;
                if (screenYValue == 0) {
                    screenYValue = Main.screenHeight / 2f - currentPanelHeight / 2f;
                }
            }

            //切换样式
            QuestTrackerStyle targetStyle = GetQuestStyle();
            if (targetStyle != currentStyleType) {
                currentStyleType = targetStyle;
                if (styleInstances.TryGetValue(targetStyle, out var styleInstance)) {
                    currentStyle = styleInstance;
                    currentStyle.Reset();
                }
            }

            //更新面板高度
            UpdatePanelHeight();

            //展开收起动画
            float targetSlide = CanOpne ? 1f : 0f;
            slideProgress = MathHelper.Lerp(slideProgress, targetSlide, 0.15f);

            if (slideProgress < 0.01f) {
                return;
            }

            //折叠动画
            float targetCollapse = isCollapsed ? 1f : 0f;
            collapseProgress = MathHelper.Lerp(collapseProgress, targetCollapse, CollapseAnimationSpeed);

            //按钮位置过渡动画
            float targetButtonPos = isCollapsed ? 1f : 0f;
            buttonPositionProgress = MathHelper.Lerp(buttonPositionProgress, targetButtonPos, 0.15f);

            //按钮发光脉冲
            buttonGlowPulse += 0.05f;
            if (buttonGlowPulse > MathHelper.TwoPi) {
                buttonGlowPulse -= MathHelper.TwoPi;
            }

            //动画更新
            pulseTimer += 0.03f;
            borderGlow = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f) * 0.3f + 0.7f;

            //更新伤害数据显示
            updateTimer += 0.016f;
            if (updateTimer >= UpdateInterval) {
                updateTimer = 0f;
                var trackingData = GetTrackingData();
                if (trackingData.total > 0) {
                    cachedContribution = trackingData.current / trackingData.total;
                }
            }

            cachedContribution = MathHelper.Clamp(cachedContribution, 0, 1f);

            //如果贡献度低闪烁警告
            float requiredContribution = GetRequiredContribution();
            if (cachedContribution < requiredContribution * 0.5f) {
                warningPulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.5f + 0.5f;
            }
            else {
                warningPulse = 0f;
            }

            //计算面板实际宽度
            float actualPanelWidth = MathHelper.Lerp(PanelWidth, 40f, CWRUtils.EaseInOutCubic(collapseProgress));

            //设置UI位置
            float offsetX = MathHelper.Lerp(-PanelWidth - 50f, ScreenX, CWRUtils.EaseOutCubic(slideProgress));
            DrawPosition = new Vector2(offsetX, ScreenY);
            Size = new Vector2(actualPanelWidth, currentPanelHeight);
            UIHitBox = DrawPosition.GetRectangle((int)actualPanelWidth, (int)currentPanelHeight);

            //更新折叠按钮位置
            UpdateCollapseButton();

            //处理折叠按钮交互
            HandleCollapseButtonInteraction();

            //处理拖拽
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox) && !collapseButtonHovered;
            if (hoverInMainPage) {
                if (keyLeftPressState == KeyPressState.Held) {
                    if (!dragBool) {
                        dragOffset = MousePosition.Y - screenYValue;
                    }
                    dragBool = true;
                }
            }
            if (dragBool) {
                screenYValue = MousePosition.Y - dragOffset;
                if (keyLeftPressState == KeyPressState.Released) {
                    dragBool = false;
                    dragOffset = MousePosition.Y;
                }
            }

            //限制Y坐标范围
            screenYValue = MathHelper.Clamp(screenYValue, 0, Main.screenHeight - currentPanelHeight);

            //检测是否与NPC碰撞箱重叠
            isOverlappingWithNPC = CheckNPCOverlap();

            //平滑过渡透明度
            float targetAlpha = isOverlappingWithNPC ? MinOverlappingAlpha : 1f;
            overlappingAlpha = MathHelper.Lerp(overlappingAlpha, targetAlpha, AlphaTransitionSpeed);

            //更新样式动画
            currentStyle?.Update(UIHitBox, Active);
            currentStyle?.UpdateParticles(DrawPosition, overlappingAlpha);
        }

        /// <summary>
        /// 更新折叠按钮位置
        /// </summary>
        protected virtual void UpdateCollapseButton() {
            int buttonSize = 20;

            //计算按钮X位置的平滑过渡
            float expandedX = DrawPosition.X + PanelWidth - buttonSize - 10;
            float collapsedX = DrawPosition.X + 10;
            float buttonX = MathHelper.Lerp(expandedX, collapsedX, CWRUtils.EaseInOutCubic(buttonPositionProgress));

            int buttonY = (int)(DrawPosition.Y + 8);

            collapseButtonRect = new Rectangle((int)buttonX, buttonY, buttonSize, buttonSize);
        }

        /// <summary>
        /// 处理折叠按钮交互
        /// </summary>
        protected virtual void HandleCollapseButtonInteraction() {
            collapseButtonHovered = collapseButtonRect.Contains(Main.MouseScreen.ToPoint());

            if (collapseButtonHovered && keyLeftPressState == KeyPressState.Pressed) {
                isCollapsed = !isCollapsed;
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
        }

        /// <summary>
        /// 文本绘制配置结构体
        /// </summary>
        protected struct TextDrawConfig
        {
            public string Text;
            public Vector2 Position;
            public Color Color;
            public float Scale;
            public float Alpha;
            public float MaxWidth;

            public TextDrawConfig(string text, Vector2 position, Color color, float scale = 0.75f, float alpha = 1f, float maxWidth = -1f) {
                Text = text;
                Position = position;
                Color = color;
                Scale = scale;
                Alpha = alpha;
                MaxWidth = maxWidth <= 0 ? PanelWidth - 20f : maxWidth;
            }
        }

        /// <summary>
        /// 绘制带换行的文本，返回绘制的总高度
        /// </summary>
        /// <param name="spriteBatch">SpriteBatch</param>
        /// <param name="config">文本绘制配置</param>
        /// <param name="lineEffect">可选的每行文本效果回调</param>
        /// <returns>绘制的总高度</returns>
        protected virtual float DrawWrappedText(
            SpriteBatch spriteBatch,
            TextDrawConfig config,
            Action<SpriteBatch, string, Vector2, Color, float, float, int> lineEffect = null) {
            var font = FontAssets.MouseText.Value;
            List<string> lines = WrapText(config.Text, font, config.MaxWidth - 20, config.Scale);

            float currentY = config.Position.Y;
            float lineSpacing = font.MeasureString("A").Y * config.Scale * 0.9f;

            for (int i = 0; i < lines.Count; i++) {
                Vector2 linePos = new Vector2(config.Position.X, currentY);
                Color lineColor = config.Color * config.Alpha;

                //如果提供了自定义效果，先执行
                lineEffect?.Invoke(spriteBatch, lines[i], linePos, lineColor, config.Scale, config.Alpha, i);

                //绘制主文本（如果lineEffect为null或子类重写时仍需要基础绘制）
                if (lineEffect == null) {
                    Utils.DrawBorderString(spriteBatch, lines[i], linePos, lineColor, config.Scale);
                }

                currentY += lineSpacing;
            }

            return currentY - config.Position.Y;
        }

        /// <summary>
        /// 简化的文本绘制方法
        /// </summary>
        protected float DrawWrappedText(SpriteBatch spriteBatch, string text, Vector2 position, Color color, float scale = 0.75f, float alpha = 1f, float maxWidth = -1f) {
            var config = new TextDrawConfig(text, position, color, scale, alpha, maxWidth);
            return DrawWrappedText(spriteBatch, config);
        }

        /// <summary>
        /// 绘制标题，支持换行和自定义效果
        /// </summary>
        /// <param name="spriteBatch">SpriteBatch</param>
        /// <param name="titlePos">标题起始位置</param>
        /// <param name="alpha">透明度</param>
        /// <param name="titleScale">标题缩放</param>
        /// <returns>标题总高度</returns>
        protected virtual float DrawTitle(SpriteBatch spriteBatch, Vector2 titlePos, float alpha, float titleScale = 0.72f) {
            Color titleColor = currentStyle?.GetTitleColor(alpha) ?? Color.White * alpha;

            var config = new TextDrawConfig(
                QuestTitle.Value,
                titlePos,
                titleColor,
                titleScale,
                alpha
            );

            return DrawWrappedText(spriteBatch, config, DrawTitleLineEffect);
        }

        /// <summary>
        /// 标题行效果
        /// </summary>
        protected virtual void DrawTitleLineEffect(SpriteBatch spriteBatch, string text, Vector2 position, Color color, float scale, float alpha, int lineIndex) {
            //默认实现，简单绘制
            Utils.DrawBorderString(spriteBatch, text, position, color, scale);
        }

        /// <summary>
        /// 绘制任务信息文本，带自动换行
        /// </summary>
        protected float DrawObjectiveText(SpriteBatch spriteBatch, string text, Vector2 position, float alpha, float textScale = 0.62f) {
            Color textColor = currentStyle?.GetTextColor(alpha) ?? Color.White * alpha;
            return DrawWrappedText(spriteBatch, text, position, textColor, textScale, alpha);
        }

        /// <summary>
        /// 绘制提示文本，带自动换行和可选颜色
        /// </summary>
        protected float DrawHintText(SpriteBatch spriteBatch, string text, Vector2 position, Color? customColor, float alpha, float textScale = 0.62f) {
            Color textColor = customColor ?? (currentStyle?.GetTextColor(alpha) ?? Color.White * alpha);
            textColor *= alpha * 0.7f;
            return DrawWrappedText(spriteBatch, text, position, textColor, textScale, alpha);
        }

        //这个鸡巴不能删，这个删了啥都没了
        public override void Draw(SpriteBatch spriteBatch) {
            if (slideProgress < 0.01f) {
                return;
            }

            float alpha = Math.Min(slideProgress * 2f, 1f) * overlappingAlpha;

            //使用样式绘制面板
            if (currentStyle != null) {
                currentStyle.DrawPanel(spriteBatch, UIHitBox, alpha);
                currentStyle.DrawFrame(spriteBatch, UIHitBox, alpha, borderGlow);
            }

            //绘制折叠按钮
            DrawCollapseButton(spriteBatch, alpha);

            //折叠状态下不绘制内容
            if (collapseProgress < 0.99f) {
                float contentAlpha = alpha * (1f - CWRUtils.EaseInOutCubic(collapseProgress));
                DrawContent(spriteBatch, contentAlpha);
            }
        }

        /// <summary>
        /// 绘制折叠按钮
        /// </summary>
        protected virtual void DrawCollapseButton(SpriteBatch spriteBatch, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //获取样式颜色
            Color styleColor = currentStyle?.GetTitleColor(alpha) ?? new Color(100, 220, 255) * alpha;
            Color edgeColor = currentStyle?.GetNumberColor(1f, 1f, alpha) ?? Color.White * alpha;

            //按钮背景使用更柔和的颜色
            float hoverBrightness = collapseButtonHovered ? 0.5f : 0.3f;
            Color buttonBgColor = Color.Lerp(styleColor, new Color(20, 30, 45), 0.6f) * (alpha * hoverBrightness);
            spriteBatch.Draw(pixel, collapseButtonRect, new Rectangle(0, 0, 1, 1), buttonBgColor);

            //按钮发光效果
            if (collapseButtonHovered) {
                float glowIntensity = (float)Math.Sin(buttonGlowPulse * 2f) * 0.2f + 0.3f;
                Color glowColor = styleColor with { A = 0 };
                Rectangle glowRect = collapseButtonRect;
                glowRect.Inflate(2, 2);
                spriteBatch.Draw(pixel, glowRect, new Rectangle(0, 0, 1, 1), glowColor * (glowIntensity * 0.25f));
            }

            //按钮边框使用更低饱和度的颜色
            Color buttonBorderColor = Color.Lerp(styleColor, edgeColor, 0.5f) * alpha;
            if (collapseButtonHovered) {
                buttonBorderColor = Color.Lerp(buttonBorderColor, styleColor, 0.3f);
            }
            DrawButtonBorder(spriteBatch, collapseButtonRect, buttonBorderColor);

            //绘制箭头
            Vector2 buttonCenter = collapseButtonRect.Center.ToVector2();
            DrawCollapseArrow(spriteBatch, buttonCenter, alpha, styleColor);
        }

        /// <summary>
        /// 绘制按钮边框
        /// </summary>
        protected virtual void DrawButtonBorder(SpriteBatch spriteBatch, Rectangle rect, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int thickness = 1;

            //上
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness),
                new Rectangle(0, 0, 1, 1), color * 0.7f);
            //下
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness),
                new Rectangle(0, 0, 1, 1), color * 0.5f);
            //左
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height),
                new Rectangle(0, 0, 1, 1), color * 0.6f);
            //右
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height),
                new Rectangle(0, 0, 1, 1), color * 0.6f);
        }

        /// <summary>
        /// 绘制折叠箭头
        /// </summary>
        protected virtual void DrawCollapseArrow(SpriteBatch spriteBatch, Vector2 center, float alpha, Color styleColor) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //箭头颜色使用更柔和的色调
            Color arrowColor = Color.Lerp(new Color(140, 160, 180), styleColor, 0.25f) * alpha;

            if (collapseButtonHovered) {
                arrowColor = Color.Lerp(arrowColor, styleColor, 0.4f);
            }

            //根据折叠状态决定箭头方向使用平滑过渡
            float currentRotation = MathHelper.Lerp(0f, MathHelper.Pi, CWRUtils.EaseInOutCubic(buttonPositionProgress));

            //箭头顶点
            Vector2 arrowTip = center + new Vector2(5, 0).RotatedBy(currentRotation);
            Vector2 arrowLeft = center + new Vector2(-4, -4).RotatedBy(currentRotation);
            Vector2 arrowRight = center + new Vector2(-4, 4).RotatedBy(currentRotation);

            //绘制箭头线条
            float lineThickness = collapseButtonHovered ? 1.8f : 1.4f;
            DrawArrowLine(spriteBatch, arrowLeft, arrowTip, arrowColor, lineThickness);
            DrawArrowLine(spriteBatch, arrowRight, arrowTip, arrowColor, lineThickness);

            //添加箭头发光效果降低强度
            if (collapseButtonHovered) {
                Color glowColor = styleColor with { A = 0 };
                float glowPulse = (float)Math.Sin(buttonGlowPulse * 3f) * 0.3f + 0.4f;
                DrawArrowLine(spriteBatch, arrowLeft, arrowTip, glowColor * (glowPulse * 0.3f), lineThickness * 1.8f);
                DrawArrowLine(spriteBatch, arrowRight, arrowTip, glowColor * (glowPulse * 0.3f), lineThickness * 1.8f);
            }
        }

        /// <summary>
        /// 绘制箭头线条
        /// </summary>
        protected virtual void DrawArrowLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, float thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float length = edge.Length();

            if (length < 1f) {
                return;
            }

            float rotation = edge.ToRotation();
            spriteBatch.Draw(pixel, start, new Rectangle(0, 0, 1, 1), color, rotation,
                new Vector2(0, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
        }

        /// <summary>
        /// 绘制内容，子类可重写以自定义布局
        /// </summary>
        protected virtual void DrawContent(SpriteBatch spriteBatch, float alpha) {
            var font = FontAssets.MouseText.Value;
            const float titleScale = 0.75f;
            const float textScale = 0.65f;

            //标题
            Vector2 titlePos = DrawPosition + new Vector2(Padding, 8);
            float titleHeight = DrawTitle(spriteBatch, titlePos, alpha, titleScale);

            //分割线
            Vector2 dividerStart = titlePos + new Vector2(0, titleHeight + 2);
            Vector2 dividerEnd = dividerStart + new Vector2(PanelWidth - 20, 0);
            currentStyle?.DrawDivider(spriteBatch, dividerStart, dividerEnd, alpha);

            //伤害贡献度文本
            Vector2 contributionTextPos = dividerStart + new Vector2(0, 8);
            DrawContributionText(spriteBatch, contributionTextPos, alpha, textScale);

            //需求文本
            Vector2 requirementPos = contributionTextPos + new Vector2(0, 15);
            Color textColor = currentStyle?.GetTextColor(alpha) ?? Color.White * alpha;
            float reqHeight = DrawWrappedText(spriteBatch, RequiredContribution.Value, requirementPos, textColor * 0.8f, 0.6f, alpha);

            //进度条
            DrawProgressBar(spriteBatch, requirementPos + new Vector2(0, reqHeight + 2), alpha);
        }

        /// <summary>
        /// 绘制贡献度文本
        /// </summary>
        protected virtual void DrawContributionText(SpriteBatch spriteBatch, Vector2 position, float alpha, float textScale) {
            var font = FontAssets.MouseText.Value;
            Color textColor = currentStyle?.GetTextColor(alpha) ?? Color.White * alpha;

            string contributionText = $"{DamageContribution.Value}: ";
            Utils.DrawBorderString(spriteBatch, contributionText, position, textColor, textScale);

            //百分比显示
            Vector2 percentPos = position + new Vector2(font.MeasureString(contributionText).X * textScale, 0);
            string percentText = $"{cachedContribution:P1}";

            float requiredContribution = GetRequiredContribution();
            Color percentColor = currentStyle?.GetNumberColor(cachedContribution, requiredContribution, alpha) ?? Color.White * alpha;

            if (cachedContribution < requiredContribution) {
                percentColor = Color.Lerp(percentColor, Color.Red, warningPulse * 0.5f);
            }

            Utils.DrawBorderString(spriteBatch, percentText, percentPos, percentColor, 0.75f);
        }

        protected virtual void DrawProgressBar(SpriteBatch spriteBatch, Vector2 position, float alpha) {
            float barWidth = PanelWidth - 20;
            float barHeight = 6;
            Rectangle barRect = new Rectangle((int)position.X, (int)position.Y, (int)barWidth, (int)barHeight);

            currentStyle?.DrawProgressBar(spriteBatch, barRect, cachedContribution, alpha);

            //需求标记线
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float requiredX = position.X + barWidth * GetRequiredContribution();
            Rectangle requirementLine = new Rectangle((int)requiredX - 1, (int)position.Y, 2, (int)barHeight);
            spriteBatch.Draw(pixel, requirementLine, new Rectangle(0, 0, 1, 1), Color.White * (alpha * 0.8f));
        }

        /// <summary>
        /// 将文本按宽度自动换行
        /// </summary>
        protected static List<string> WrapText(string text, DynamicSpriteFont font, float maxWidth, float scale = 1f) {
            List<string> lines = new();

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
    }
}
