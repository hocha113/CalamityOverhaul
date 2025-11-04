using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.ADV.Scenarios.Common.BaseDamageTracker;

namespace CalamityOverhaul.Content.ADV.Scenarios.Common
{
    /// <summary>
    /// 任务追踪UI的通用基类，用于显示战斗中的任务进度
    /// </summary>
    internal abstract class BaseQuestTrackerUI : UIHandle, ILocalizedModType
    {
        public abstract string LocalizationCategory { get; }

        //本地化文本
        protected LocalizedText QuestTitle { get; set; }
        protected LocalizedText DamageContribution { get; set; }
        protected LocalizedText RequiredContribution { get; set; }

        //UI参数
        protected const float PanelWidth = 220f;
        protected const float PanelHeight = 90f;
        protected virtual float ScreenX => 0f;
        protected virtual float ScreenY => Main.screenHeight / 2f - PanelHeight / 2f;

        //动画参数
        protected float slideProgress = 0f;
        protected float pulseTimer = 0f;
        protected float borderGlow = 1f;
        protected float warningPulse = 0f;

        //伤害数据
        protected float cachedContribution = 0f;
        protected const float UpdateInterval = 0.5f;
        protected float updateTimer = 0f;

        //碰撞检测与半透明化
        protected bool isOverlappingWithNPC = false;
        protected float overlappingAlpha = 1f;
        protected const float MinOverlappingAlpha = 0.3f;
        protected const float AlphaTransitionSpeed = 0.15f;

        /// <summary>
        /// 获取当前伤害追踪数据
        /// </summary>
        protected abstract (float current, float total, bool isActive) GetTrackingData();

        /// <summary>
        /// 目标NPC类型
        /// </summary>
        public abstract int TargetNPCType { get; }

        public override bool Active => slideProgress > 0f || CanOpne;

        /// <summary>
        /// 是否可以打开UI面板
        /// </summary>
        public virtual bool CanOpne {
            get {
                if (CurrentDamageTrackerInstance == null) {
                    return false;//没有正在进行的任务追踪实例
                }
                if (!CurrentDamageTrackerInstance.NPC.Alives()) {
                    return false;//目标NPC不存在或已死亡
                }
                if (CurrentDamageTrackerInstance.NPC.type != TargetNPCType) {
                    return false;//目标NPC类型不匹配
                }
                //获取战斗状态
                return CurrentDamageTrackerInstance.IsQuestActive(Main.LocalPlayer) && GetDamageTrackingData().isActive;
            }
        }

        /// <summary>
        /// 获取需求的伤害贡献度阈值
        /// </summary>
        protected abstract float GetRequiredContribution();

        public override void SetStaticDefaults() {
            SetupLocalizedTexts();
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
                //将NPC的世界坐标转换为屏幕坐标
                Vector2 npcScreenPos = n.position - Main.screenPosition;
                Rectangle npcScreenRect = new(
                    (int)npcScreenPos.X - extend,
                    (int)npcScreenPos.Y - extend,
                    n.width + extend * 2,
                    n.height + extend * 2
                );

                //获取UI面板的屏幕坐标矩形
                Rectangle uiRect = DrawPosition.GetRectangle((int)PanelWidth, (int)PanelHeight);
                //检测两个矩形是否相交
                bool result = npcScreenRect.Intersects(uiRect);
                if (result) {
                    return true;
                }
            }

            return false;
        }

        public override void Update() {
            //展开/收起动画
            float targetSlide = CanOpne ? 1f : 0f;
            slideProgress = MathHelper.Lerp(slideProgress, targetSlide, 0.15f);

            if (slideProgress < 0.01f) {
                return;
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

            cachedContribution = MathHelper.Clamp(cachedContribution, 0, 1f);//确保在0-1范围内

            //如果贡献度低，闪烁警告
            float requiredContribution = GetRequiredContribution();
            if (cachedContribution < requiredContribution * 0.5f) {
                warningPulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.5f + 0.5f;
            }
            else {
                warningPulse = 0f;
            }

            //设置UI位置
            float offsetX = MathHelper.Lerp(-PanelWidth - 50f, ScreenX, CWRUtils.EaseOutCubic(slideProgress));
            DrawPosition = new Vector2(offsetX, ScreenY);
            Size = new Vector2(PanelWidth, PanelHeight);
            UIHitBox = DrawPosition.GetRectangle((int)PanelWidth, (int)PanelHeight);

            //检测是否与NPC碰撞箱重叠
            isOverlappingWithNPC = CheckNPCOverlap();

            //平滑过渡透明度
            float targetAlpha = isOverlappingWithNPC ? MinOverlappingAlpha : 1f;
            overlappingAlpha = MathHelper.Lerp(overlappingAlpha, targetAlpha, AlphaTransitionSpeed);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (slideProgress < 0.01f) {
                return;
            }

            //应用基于重叠状态的透明度
            float alpha = Math.Min(slideProgress * 2f, 1f) * overlappingAlpha;
            DrawPanel(spriteBatch, alpha);
            DrawContent(spriteBatch, alpha);
        }

        protected virtual void DrawPanel(SpriteBatch spriteBatch, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //阴影
            Rectangle shadowRect = UIHitBox;
            shadowRect.Offset(3, 3);
            spriteBatch.Draw(pixel, shadowRect, new Rectangle(0, 0, 1, 1), Color.Black * (alpha * 0.5f));

            //背景渐变 (硫磺火风格)
            int segments = 15;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = (int)(DrawPosition.Y + t * PanelHeight);
                int y2 = (int)(DrawPosition.Y + t2 * PanelHeight);
                Rectangle r = new Rectangle((int)DrawPosition.X, y1, (int)PanelWidth, Math.Max(1, y2 - y1));

                Color deep = new Color(30, 15, 15);
                Color mid = new Color(70, 30, 25);
                Color hot = new Color(120, 50, 35);

                float wave = (float)Math.Sin(pulseTimer * 1.2f + t * 2f) * 0.5f + 0.5f;
                Color c = Color.Lerp(Color.Lerp(deep, mid, wave), hot, t * 0.5f);
                c *= alpha;

                spriteBatch.Draw(pixel, r, new Rectangle(0, 0, 1, 1), c);
            }

            //火焰脉冲效果
            float pulse = (float)Math.Sin(pulseTimer * 2f) * 0.5f + 0.5f;
            Color pulseColor = new Color(140, 40, 25) * (alpha * 0.15f * pulse);
            spriteBatch.Draw(pixel, UIHitBox, new Rectangle(0, 0, 1, 1), pulseColor);

            //边框
            DrawBrimstoneFrame(spriteBatch, UIHitBox, alpha, borderGlow);
        }

        protected virtual void DrawContent(SpriteBatch spriteBatch, float alpha) {
            var font = FontAssets.MouseText.Value;

            //标题
            Vector2 titlePos = DrawPosition + new Vector2(10, 8);
            Color titleColor = new Color(255, 220, 180) * alpha;
            Utils.DrawBorderString(spriteBatch, QuestTitle.Value, titlePos, titleColor, 0.75f);

            //分割线
            Vector2 dividerStart = titlePos + new Vector2(0, 22);
            Vector2 dividerEnd = dividerStart + new Vector2(PanelWidth - 20, 0);
            DrawGradientLine(spriteBatch, dividerStart, dividerEnd,
                Color.OrangeRed * alpha * 0.8f, Color.OrangeRed * alpha * 0.1f, 1.2f);

            //伤害贡献度文本
            Vector2 contributionTextPos = dividerStart + new Vector2(0, 10);
            string contributionText = $"{DamageContribution.Value}: ";
            Utils.DrawBorderString(spriteBatch, contributionText, contributionTextPos,
                Color.White * alpha, 0.65f);

            //百分比显示
            Vector2 percentPos = contributionTextPos + new Vector2(font.MeasureString(contributionText).X * 0.65f, 0);
            string percentText = $"{cachedContribution:P1}";

            //根据进度改变颜色
            float requiredContribution = GetRequiredContribution();
            Color percentColor;
            if (cachedContribution >= requiredContribution) {
                percentColor = Color.Lerp(Color.Yellow, Color.LimeGreen, (cachedContribution - requiredContribution) / (1f - requiredContribution));
            }
            else {
                percentColor = Color.Lerp(Color.Red, Color.Orange, cachedContribution / requiredContribution);
                //低于要求时闪烁警告
                percentColor = Color.Lerp(percentColor, Color.Red, warningPulse * 0.5f);
            }

            Utils.DrawBorderString(spriteBatch, percentText, percentPos, percentColor * alpha, 0.75f);

            //需求文本
            Vector2 requirementPos = contributionTextPos + new Vector2(0, 18);
            Utils.DrawBorderString(spriteBatch, RequiredContribution.Value, requirementPos,
                Color.Gray * alpha, 0.6f);

            //进度条
            DrawProgressBar(spriteBatch, requirementPos + new Vector2(0, 14), alpha);
        }

        protected virtual void DrawProgressBar(SpriteBatch spriteBatch, Vector2 position, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            float barWidth = PanelWidth - 20;
            float barHeight = 6;

            //背景
            Rectangle barBg = new Rectangle((int)position.X, (int)position.Y, (int)barWidth, (int)barHeight);
            spriteBatch.Draw(pixel, barBg, new Rectangle(0, 0, 1, 1), Color.Black * (alpha * 0.6f));

            //填充进度
            float fillWidth = barWidth * Math.Min(cachedContribution, 1f);
            if (fillWidth > 0) {
                Rectangle barFill = new Rectangle((int)position.X, (int)position.Y, (int)fillWidth, (int)barHeight);

                float requiredContribution = GetRequiredContribution();
                Color fillColor;
                if (cachedContribution >= requiredContribution) {
                    fillColor = Color.Lerp(new Color(255, 180, 80), new Color(80, 255, 120), (cachedContribution - requiredContribution) / (1f - requiredContribution));
                }
                else {
                    fillColor = Color.Lerp(new Color(180, 50, 50), new Color(255, 140, 60), cachedContribution / requiredContribution);
                }

                spriteBatch.Draw(pixel, barFill, new Rectangle(0, 0, 1, 1), fillColor * alpha);
            }

            //需求标记线
            float requiredX = position.X + barWidth * GetRequiredContribution();
            Rectangle requirementLine = new Rectangle((int)requiredX - 1, (int)position.Y, 2, (int)barHeight);
            spriteBatch.Draw(pixel, requirementLine, new Rectangle(0, 0, 1, 1), Color.White * (alpha * 0.8f));

            //边框
            spriteBatch.Draw(pixel, new Rectangle(barBg.X, barBg.Y, barBg.Width, 1),
                new Rectangle(0, 0, 1, 1), Color.OrangeRed * (alpha * 0.6f));
            spriteBatch.Draw(pixel, new Rectangle(barBg.X, barBg.Bottom - 1, barBg.Width, 1),
                new Rectangle(0, 0, 1, 1), Color.OrangeRed * (alpha * 0.6f));
            spriteBatch.Draw(pixel, new Rectangle(barBg.X, barBg.Y, 1, barBg.Height),
                new Rectangle(0, 0, 1, 1), Color.OrangeRed * (alpha * 0.6f));
            spriteBatch.Draw(pixel, new Rectangle(barBg.Right - 1, barBg.Y, 1, barBg.Height),
                new Rectangle(0, 0, 1, 1), Color.OrangeRed * (alpha * 0.6f));
        }

        protected static void DrawBrimstoneFrame(SpriteBatch sb, Rectangle rect, float alpha, float pulse) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //外框
            Color outerEdge = Color.Lerp(new Color(180, 60, 30), new Color(255, 140, 70), pulse) * (alpha * 0.85f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0, 0, 1, 1), outerEdge);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0, 0, 1, 1), outerEdge * 0.75f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);
            sb.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);

            //内框发光
            Rectangle inner = rect;
            inner.Inflate(-5, -5);
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
