using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using static CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.HalibutUIAsset;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    /// <summary>
    /// 技能介绍面板 - 从主面板右侧滑出展开
    /// </summary>
    internal class SkillTooltipPanel : UIHandle
    {
        public static SkillTooltipPanel Instance => UIHandleLoader.GetUIHandleOfType<SkillTooltipPanel>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None; //手动调用

        //显示控制
        public FishSkill CurrentSkill; //当前要显示的技能
        private bool shouldShow = false; //内部状态：是否应该显示

        //收回延迟
        private int hideDelayTimer = 0; //收回延迟计时器
        private const int HideDelay = 15; //收回延迟
        private bool pendingHide = false; //是否等待收回

        //悬停切换宽容期（在技能图标之间快速移动时保持面板不收回）
        private int lingerTimer = 0; //宽容剩余时间
        private const int LingerDuration = 15; //宽容期

        //意图推断 - 鼠标离开技能浏览区域的独立计时
        private int outsideTimer = 0; //鼠标离开技能区域的时间
        private const int OutsideHideDelay = 30; //离开技能区域多久后允许真正隐藏

        //动画相关
        private float expandProgress = 0f; //展开进度（0-1）
        private const float ExpandDuration = 20f; //展开动画持续帧数
        private float contentFadeProgress = 0f; //内容淡入进度
        private const float ContentFadeDelay = 0.4f; //内容在展开40%后开始淡入

        //面板尺寸
        private float currentWidth = 0f; //当前宽度（动画中）
        private float targetWidth = 0f; //目标宽度
        private const float MinWidth = 8f; //最小宽度（完全收起时）

        //位置相关
        private Vector2 anchorPosition; //锚点位置（主面板右侧）

        //内容布局
        private const int Padding = 12; //内边距
        private const int LineSpacing = 6; //行间距
        private const int IconSize = 48; //图标大小

        //文本滚动
        private float scrollOffset = 0f; //当前滚动偏移
        private float maxScrollOffset = 0f; //最大滚动偏移
        private float scrollVelocity = 0f; //滚动速度（用于平滑）
        private const float ScrollSpeedPerNotch = 48f; //每个滚轮刻度滚动像素
        private const float ScrollDamping = 0.85f; //滚动阻尼

        /// <summary>
        /// 是否正在显示或展开中
        /// </summary>
        public bool IsShowing => shouldShow || expandProgress > 0.01f;

        /// <summary>
        /// 是否完全收起
        /// </summary>
        public bool IsFullyClosed => expandProgress <= 0.01f;

        /// <summary>
        /// 计算技能行区域（用于意图推断）
        /// </summary>
        private static Rectangle GetSkillRegionRect() {
            var panel = HalibutUIPanel.Instance;
            if (panel == null) {
                return Rectangle.Empty;
            }
            if (panel.halibutUISkillSlots == null) {
                return Rectangle.Empty;
            }
            if (panel.halibutUISkillSlots.Count == 0) {
                return Rectangle.Empty;
            }
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;
            foreach (var s in panel.halibutUISkillSlots) {
                var r = s.UIHitBox;
                if (r.Width == 0 || r.Height == 0) {
                    continue;
                }
                if (r.X < minX) {
                    minX = r.X;
                }
                if (r.Y < minY) {
                    minY = r.Y;
                }
                if (r.Right > maxX) {
                    maxX = r.Right;
                }
                if (r.Bottom > maxY) {
                    maxY = r.Bottom;
                }
            }
            if (minX == int.MaxValue) {
                return Rectangle.Empty;
            }
            //添加一定的填充，让技能之间的空白依旧算在浏览区域里
            const int pad = 8;
            var rect = new Rectangle(minX - pad, minY - pad, (maxX - minX) + pad * 2, (maxY - minY) + pad * 2);
            return rect;
        }

        /// <summary>
        /// 鼠标是否在技能浏览区域里（推断为想继续阅读 / 切换技能说明）
        /// </summary>
        private bool MouseInBrowseArea(out Rectangle area) {
            area = GetSkillRegionRect();
            if (area == Rectangle.Empty) {
                return false;
            }
            Vector2 m = Main.MouseScreen;
            return area.Contains((int)m.X, (int)m.Y);
        }

        /// <summary>
        /// 显示指定技能的介绍面板
        /// </summary>
        public void Show(FishSkill skill, Vector2 mainPanelPosition, Vector2 mainPanelSize) {
            if (skill == null) {
                return;
            }
            //检测技能切换
            if (CurrentSkill != skill) {
                CurrentSkill = skill;
                if (!shouldShow) {
                    expandProgress = 0f;
                    contentFadeProgress = 0f;
                }
                else {
                    contentFadeProgress = 0f; //切换重新淡入
                }
                //切换技能时重置滚动
                scrollOffset = 0f;
                scrollVelocity = 0f;
            }
            shouldShow = true;
            pendingHide = false;
            hideDelayTimer = 0;
            lingerTimer = 0;
            outsideTimer = 0;
            anchorPosition = mainPanelPosition + new Vector2(mainPanelSize.X, mainPanelSize.Y / 2);
            targetWidth = TooltipPanel.Width;
        }

        /// <summary>
        /// 外部请求隐藏（会进入宽容 + 外离计时 + 延迟）
        /// </summary>
        public void Hide() {
            if (!shouldShow || pendingHide) {
                return;
            }
            if (lingerTimer < LingerDuration) {
                lingerTimer = LingerDuration;
            }
            pendingHide = true;
        }

        public void ForceHide() {
            shouldShow = false;
            pendingHide = false;
            hideDelayTimer = 0;
            lingerTimer = 0;
            outsideTimer = 0;
            scrollOffset = 0f;
            scrollVelocity = 0f;
        }

        private static float EaseOutBack(float t) {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * (float)Math.Pow(t - 1, 3) + c1 * (float)Math.Pow(t - 1, 2);
        }

        private static float EaseInCubic(float t) {
            return t * t * t;
        }

        public override void Update() {
            // 意图推断：判断鼠标是否仍在技能浏览区域
            bool inBrowse = MouseInBrowseArea(out Rectangle browseRect);
            bool onPanel = IsMouseOnPanel();
            if (shouldShow) {
                if (inBrowse || onPanel) {
                    //保持显示，清空脱离计时
                    outsideTimer = 0;
                    //如果仍在浏览区域且之前处于待隐藏状态，则取消隐藏流程
                    if (pendingHide) {
                        pendingHide = false;
                        hideDelayTimer = 0;
                        lingerTimer = 0;
                    }
                }
                else {
                    //不在浏览区域，并且当前也没有悬停具体技能 -> 记录脱离时间
                    outsideTimer++;
                    if (outsideTimer == OutsideHideDelay) {
                        //达到脱离阈值后才真正进入 Hide 流程
                        Hide();
                    }
                }
            }

            //处理宽容与延迟收回逻辑
            if (pendingHide) {
                if (lingerTimer > 0) {
                    lingerTimer--;
                }
                else {
                    hideDelayTimer++;
                    if (hideDelayTimer >= HideDelay) {
                        shouldShow = false;
                        pendingHide = false;
                        hideDelayTimer = 0;
                        CurrentSkill = null;
                        scrollOffset = 0f;
                        scrollVelocity = 0f;
                    }
                }
            }

            if (shouldShow && CurrentSkill != null) {
                if (expandProgress < 1f) {
                    expandProgress += 1f / ExpandDuration;
                    expandProgress = Math.Clamp(expandProgress, 0f, 1f);
                }
                if (expandProgress > ContentFadeDelay && contentFadeProgress < 1f) {
                    float adjustedProgress = (expandProgress - ContentFadeDelay) / (1f - ContentFadeDelay);
                    contentFadeProgress = Math.Min(contentFadeProgress + 0.1f, adjustedProgress);
                }
            }
            else {
                if (expandProgress > 0f) {
                    expandProgress -= 1f / ExpandDuration;
                    expandProgress = Math.Clamp(expandProgress, 0f, 1f);
                }
                if (contentFadeProgress > 0f) {
                    contentFadeProgress -= 0.15f;
                    contentFadeProgress = Math.Clamp(contentFadeProgress, 0f, 1f);
                }
            }

            //滚动速度阻尼 & 偏移更新（在 Draw 中会重新 clamp）
            if (scrollVelocity != 0f) {
                scrollOffset += scrollVelocity;
                scrollVelocity *= ScrollDamping;
                if (Math.Abs(scrollVelocity) < 0.1f) scrollVelocity = 0f;
            }

            //计算当前宽度（使用缓动函数）
            float easedProgress = shouldShow ? EaseOutBack(expandProgress) : EaseInCubic(expandProgress);
            currentWidth = MinWidth + (targetWidth - MinWidth) * easedProgress;

            //更新位置和尺寸（保持与主面板右侧对齐）
            float panelHeight = TooltipPanel.Height;
            DrawPosition = anchorPosition + new Vector2(-6, -panelHeight / 2 - 18); //-4是为了与主面板稍微重叠
            Size = new Vector2(currentWidth, panelHeight);
            UIHitBox = DrawPosition.GetRectangle(Size);
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);
            if (hoverInMainPage) {
                player.CWR().DontSwitchWeaponTime = 2;
            }
        }

        private bool IsMouseOnPanel() {
            Vector2 m = Main.MouseScreen;
            Rectangle panelRect = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, (int)currentWidth, (int)Size.Y);
            return panelRect.Contains((int)m.X, (int)m.Y);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (expandProgress <= 0.01f) {
                return;
            }
            if (CurrentSkill == null) {
                return;
            }
            float alpha = Math.Min(expandProgress * 2f, 1f);
            Rectangle panelRect = new Rectangle(
                (int)DrawPosition.X,
                (int)DrawPosition.Y,
                (int)currentWidth,
                (int)Size.Y
            );
            Rectangle sourceRect = new Rectangle(
                0,
                0,
                (int)(TooltipPanel.Width * MathHelper.Clamp((currentWidth / targetWidth), 0, 1f)),
                TooltipPanel.Height
            );
            Rectangle shadowRect = panelRect;
            shadowRect.Offset(3, 3);
            Color shadowColor = Color.Black * (alpha * 0.4f);
            spriteBatch.Draw(TooltipPanel, shadowRect, sourceRect, shadowColor);
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f) * 0.05f + 0.95f;
            Color panelColor = Color.White * (alpha * pulse);
            spriteBatch.Draw(TooltipPanel, panelRect, sourceRect, panelColor);
            if (expandProgress > 0.3f) {
                Color glowColor = Color.Gold with { A = 0 } * (alpha * 0.2f * pulse);
                Rectangle glowRect = panelRect;
                glowRect.Inflate(2, 2);
                spriteBatch.Draw(TooltipPanel, glowRect, sourceRect, glowColor);
            }
            if (expandProgress > 0.5f) {
                DrawFloatingStars(spriteBatch, alpha);
            }
            if (expandProgress > ContentFadeDelay && currentWidth > targetWidth * 0.5f) {
                DrawContent(spriteBatch, alpha);
            }
        }

        /// <summary>
        /// 绘制舞动的星星粒子
        /// </summary>
        private void DrawFloatingStars(SpriteBatch spriteBatch, float panelAlpha) {
            float starTime = Main.GlobalTimeWrappedHourly * 4f;
            Vector2 panelCenter = DrawPosition + Size / 2;

            const float starCount = 3;
            //绘制3个围绕面板舞动的星星
            for (int i = 0; i < starCount; i++) {
                float starPhase = starTime + i * MathHelper.TwoPi / starCount;

                //计算椭圆轨迹位置
                float radiusX = currentWidth * 0.4f;
                float radiusY = Size.Y * 0.35f;
                Vector2 starPos = panelCenter + new Vector2(
                    (float)Math.Cos(starPhase) * radiusX,
                    (float)Math.Sin(starPhase) * radiusY
                );

                //星星透明度：根据位置动态变化
                float starAlpha = ((float)Math.Sin(starPhase) * 0.5f + 0.5f) * panelAlpha * 0.6f;

                //星星大小：远近效果
                float starSize = 3f + (float)Math.Sin(starPhase * 2f) * 1f;

                //星星颜色：金色到白色渐变
                Color starColor = Color.Lerp(Color.Gold, Color.White, (float)Math.Sin(starPhase * 2f) * 0.5f + 0.5f);
                starColor *= starAlpha;

                //绘制星星（带轻微的拖尾效果）
                DrawStarWithTrail(spriteBatch, starPos, starSize, starColor, starPhase);
            }
        }

        /// <summary>
        /// 绘制带拖尾效果的星星
        /// </summary>
        private void DrawStarWithTrail(SpriteBatch spriteBatch, Vector2 position, float size, Color color, float rotation) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //绘制拖尾（3个渐弱的残影）
            for (int i = 1; i <= 3; i++) {
                float trailPhase = rotation - i * 0.2f;
                Vector2 panelCenter = DrawPosition + Size / 2;
                float radiusX = currentWidth * 0.4f;
                float radiusY = Size.Y * 0.35f;
                Vector2 trailPos = panelCenter + new Vector2(
                    (float)Math.Cos(trailPhase) * radiusX,
                    (float)Math.Sin(trailPhase) * radiusY
                );

                float trailAlpha = (4 - i) / 4f * 0.4f;
                float trailSize = size * (1f - i * 0.15f);
                DrawStar(spriteBatch, trailPos, trailSize, color * trailAlpha);
            }

            //绘制主星星（更亮）
            DrawStar(spriteBatch, position, size, color);

            //绘制中心高光
            spriteBatch.Draw(pixel, position, new Rectangle(0, 0, 1, 1), color * 0.8f,
                0f, new Vector2(0.5f, 0.5f), new Vector2(size * 0.5f, size * 0.5f), SpriteEffects.None, 0);
        }

        /// <summary>
        /// 绘制面板内容（支持滚动）
        /// </summary>
        private void DrawContent(SpriteBatch spriteBatch, float panelAlpha) {
            if (CurrentSkill?.Icon == null) {
                return;
            }
            //内容透明度
            float contentAlpha = contentFadeProgress * panelAlpha;
            if (contentAlpha <= 0.01f) {
                return;
            }
            //内容区域起始位置
            Vector2 contentStart = DrawPosition + new Vector2(Padding, Padding);
            float availableWidth = currentWidth - Padding * 2;
            //如果宽度不够，不绘制内容
            if (availableWidth < 100) {
                return;
            }

            //1. 绘制技能图标（左上角）
            Vector2 iconPos = contentStart;
            Vector2 iconCenter = iconPos + new Vector2(IconSize / 2);

            //图标旋转光晕
            float glowRotation = Main.GlobalTimeWrappedHourly * 0.5f;
            Color iconGlow = Color.Lerp(Color.Gold, Color.Orange, (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.5f + 0.5f);
            iconGlow = iconGlow with { A = 0 } * (contentAlpha * 0.5f);

            for (int i = 0; i < 4; i++) {
                float angle = glowRotation + MathHelper.PiOver2 * i;
                Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 2f;
                spriteBatch.Draw(CurrentSkill.Icon, iconCenter + offset, null, iconGlow, 0f,
                    CurrentSkill.Icon.Size() / 2, (float)IconSize / CurrentSkill.Icon.Width * 1.1f, SpriteEffects.None, 0);
            }

            //图标主体
            spriteBatch.Draw(CurrentSkill.Icon, iconCenter, null, Color.White * contentAlpha, 0f,
                CurrentSkill.Icon.Size() / 2, (float)IconSize / CurrentSkill.Icon.Width, SpriteEffects.None, 0);

            //2. 绘制技能名称（图标右侧）
            string displayName = CurrentSkill.DisplayName?.Value ?? "未知技能";
            Vector2 namePos = contentStart + new Vector2(IconSize + 12, IconSize / 4);

            //限制名称宽度
            float nameMaxWidth = availableWidth - IconSize - 16;
            if (nameMaxWidth > 0) {
                //名称发光轮廓
                Color nameGlowColor = Color.Gold with { A = 0 } * (contentAlpha * 0.5f);
                for (int i = 0; i < 4; i++) {
                    float angle = MathHelper.TwoPi * i / 4;
                    Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 1.2f;
                    Utils.DrawBorderString(spriteBatch, displayName, namePos + offset, nameGlowColor, 0.85f);
                }

                //名称主体（渐变色）
                Color nameColor = Color.Lerp(Color.Gold, Color.White, 0.4f) * contentAlpha;
                Utils.DrawBorderString(spriteBatch, displayName, namePos, nameColor, 0.85f);
            }

            //3. 绘制装饰分隔线
            Vector2 dividerStart = contentStart + new Vector2(0, IconSize + 12);
            Vector2 dividerEnd = dividerStart + new Vector2(availableWidth, 0);
            DrawGradientLine(spriteBatch, dividerStart, dividerEnd,
                Color.Gold * contentAlpha * 0.7f, Color.Gold * contentAlpha * 0.1f, 1.5f);

            //4. 绘制技能描述
            string tooltip = CurrentSkill.Tooltip?.Value ?? "暂无描述";
            Vector2 tooltipPos = dividerStart + new Vector2(0, 10);

            //计算实际可用文本宽度
            int textMaxWidth = Math.Max(100, (int)availableWidth - 8);

            //使用WordwrapString进行换行
            string[] lines = Utils.WordwrapString(tooltip, FontAssets.MouseText.Value, textMaxWidth + 40, 20, out int lineCount);

            //文本可视区域底部
            float textAreaBottom = DrawPosition.Y + Size.Y - Padding;
            float lineHeight = LineSpacing + 14; //统一行高

            //总文本高度（不限制行数）
            int actualLines = 0;
            for (int i = 0; i < lines.Length; i++) {
                string ln = lines[i];
                if (string.IsNullOrWhiteSpace(ln)) continue;
                actualLines++;
            }
            float totalTextHeight = actualLines * lineHeight;

            //可视高度（超出则需要滚动）
            float visibleHeight = textAreaBottom - tooltipPos.Y;
            bool needScroll = totalTextHeight > visibleHeight + 1f;
            maxScrollOffset = needScroll ? Math.Max(0, totalTextHeight - visibleHeight) : 0f;

            //处理鼠标滚轮输入（仅在鼠标位于面板内部时，并且需要滚动）
            if (needScroll && IsMouseOnPanel()) {
                int delta = PlayerInput.ScrollWheelDelta; // 上滚
                if (delta != 0) {//这里锁定正负1，避免有的人鼠标滚轮灵敏度过高
                    scrollVelocity -= Math.Sign(delta) * (ScrollSpeedPerNotch / 60f); //60 刻度分母
                }
            }

            //应用滚动（包含阻尼已在 Update）
            if (!needScroll) {
                scrollOffset = 0f;
                scrollVelocity = 0f;
            }
            scrollOffset = Math.Clamp(scrollOffset, 0f, maxScrollOffset);

            //绘制每一行文本（按滚动偏移裁剪）
            float currentY = 0f;
            for (int i = 0; i < lines.Length; i++) {
                string raw = lines[i];
                if (string.IsNullOrEmpty(raw)) continue;

                string line = raw.TrimEnd('-', ' ');
                if (string.IsNullOrEmpty(line)) continue;

                //该行在整体文本中的顶部偏移
                float lineTop = currentY;
                float lineBottom = lineTop + lineHeight;
                currentY += lineHeight;

                //剔除不在可视范围的行
                if (lineBottom < scrollOffset || lineTop > scrollOffset + visibleHeight) {
                    continue;
                }

                //行实际显示位置
                float drawY = tooltipPos.Y + (lineTop - scrollOffset);
                if (drawY + 14 < tooltipPos.Y - 4 || drawY > textAreaBottom + 4) {
                    continue; //安全剪裁
                }

                Vector2 linePos = new Vector2(tooltipPos.X + 4, drawY);

                //文字阴影
                Utils.DrawBorderString(spriteBatch, line, linePos + new Vector2(1, 1),
                    Color.Black * contentAlpha * 0.5f, 0.75f);

                //文字主体
                Color textColor = Color.White * contentAlpha;
                Utils.DrawBorderString(spriteBatch, line, linePos, textColor, 0.75f);
            }

            //滚动提示（顶部/底部渐隐遮罩）
            if (needScroll) {
                DrawScrollFades(spriteBatch, new Vector2(DrawPosition.X, tooltipPos.Y), visibleHeight, contentAlpha);
            }

            //5. 绘制装饰星星（在角落轻微闪烁）
            if (contentAlpha > 0.8f) {
                float starTime = Main.GlobalTimeWrappedHourly * 3f;

                //右上角星星
                Vector2 topRightStar = DrawPosition + new Vector2(currentWidth - 12, 12);
                float star1Alpha = ((float)Math.Sin(starTime) * 0.5f + 0.5f) * contentAlpha * 0.7f;
                DrawStar(spriteBatch, topRightStar, 4f, Color.Gold * star1Alpha);

                //右下角星星
                Vector2 bottomRightStar = DrawPosition + new Vector2(currentWidth - 16, Size.Y - 16);
                float star2Alpha = ((float)Math.Sin(starTime + MathHelper.Pi) * 0.5f + 0.5f) * contentAlpha * 0.7f;
                DrawStar(spriteBatch, bottomRightStar, 3f, Color.Gold * star2Alpha);
            }
        }

        private void DrawScrollFades(SpriteBatch spriteBatch, Vector2 tooltipPos, float visibleHeight, float contentAlpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float fadeHeight = 24f;
            float width = currentWidth - Padding * 2 + 8;
            Vector2 leftTop = new Vector2(tooltipPos.X + 2, tooltipPos.Y - 8);

            //顶部淡入（如果还有向上的内容）
            if (scrollOffset > 2f) {
                for (int i = 0; i < fadeHeight; i += 2) {
                    float t = 1f - i / fadeHeight;
                    Color c = Color.Black * (t * 0.35f * contentAlpha);
                    spriteBatch.Draw(pixel, new Rectangle((int)leftTop.X, (int)(leftTop.Y + i - 2), (int)width, 2), c);
                }
            }
            //底部淡入（还有向下内容）
            if (scrollOffset < maxScrollOffset - 2f) {
                float bottomY = tooltipPos.Y + visibleHeight - 2;
                for (int i = 0; i < fadeHeight; i += 2) {
                    float t = 1f - i / fadeHeight;
                    Color c = Color.Black * (t * 0.35f * contentAlpha);
                    spriteBatch.Draw(pixel, new Rectangle((int)leftTop.X, (int)(bottomY - fadeHeight + i), (int)width, 2), c);
                }
            }
        }

        /// <summary>
        /// 绘制渐变线条
        /// </summary>
        private static void DrawGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color startColor, Color endColor, float thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float length = edge.Length();

            if (length < 1f) {
                return;
            }

            edge.Normalize();
            float rotation = (float)Math.Atan2(edge.Y, edge.X);

            //分段绘制渐变
            int segments = Math.Max(1, (int)(length / 10f));
            for (int i = 0; i < segments; i++) {
                float t = (float)i / segments;
                Vector2 segPos = start + edge * (length * t);
                float segLength = length / segments;
                Color color = Color.Lerp(startColor, endColor, t);

                spriteBatch.Draw(pixel, segPos, new Rectangle(0, 0, 1, 1), color,
                    rotation, new Vector2(0, 0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0);
            }
        }

        /// <summary>
        /// 绉绘制星星装饰
        /// </summary>
        private static void DrawStar(SpriteBatch spriteBatch, Vector2 position, float size, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //绘制八芒星
            //横线
            spriteBatch.Draw(pixel, position, new Rectangle(0, 0, 1, 1), color,
                0f, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.25f), SpriteEffects.None, 0);

            //竖线
            spriteBatch.Draw(pixel, position, new Rectangle(0, 0, 1, 1), color,
                MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.25f), SpriteEffects.None, 0);

            //斜线1
            spriteBatch.Draw(pixel, position, new Rectangle(0, 0, 1, 1), color * 0.7f,
                MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(size * 0.7f, size * 0.2f), SpriteEffects.None, 0);

            //斜线2
            spriteBatch.Draw(pixel, position, new Rectangle(0, 0, 1, 1), color * 0.7f,
                -MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(size * 0.7f, size * 0.2f), SpriteEffects.None, 0);
        }
    }
}
