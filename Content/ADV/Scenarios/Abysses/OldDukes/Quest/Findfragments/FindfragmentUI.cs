using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Items;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Quest.Findfragments
{
    /// <summary>
    /// 寻找海洋碎片任务追踪UI
    /// </summary>
    internal class FindFragmentUI : BaseQuestTrackerUI
    {
        public static FindFragmentUI Instance => UIHandleLoader.GetUIHandleOfType<FindFragmentUI>();

        public override string LocalizationCategory => "UI";

        public override int TargetNPCType => -1;

        private bool dragBool;
        private float dragOffset;
        private float screenYValue = 0;
        protected override float ScreenY => screenYValue;

        //硫磺海风格动画参数
        private float toxicWavePhase;
        private float sulfurPulse;
        private float miasmaTimer;
        private float bubbleTimer;

        //本地化文本
        public static LocalizedText ObjectiveText { get; private set; }
        public static LocalizedText CollectFragmentsText { get; private set; }
        public static LocalizedText CurrentFragmentsText { get; private set; }
        public static LocalizedText ReturnToCampsiteText { get; private set; }
        public static LocalizedText QuestCompleteText { get; private set; }

        private bool initialize;
        internal void SetDefScreenYValue() => initialize = true;

        public new void LoadUIData(TagCompound tag) {
            tag.TryGet(Name + ":" + nameof(screenYValue), out screenYValue);
        }

        public new void SaveUIData(TagCompound tag) {
            tag[Name + ":" + nameof(screenYValue)] = screenYValue;
        }

        public override void SetStaticDefaults() {
            base.SetStaticDefaults();

            ObjectiveText = this.GetLocalization(nameof(ObjectiveText), () => "目标");
            CollectFragmentsText = this.GetLocalization(nameof(CollectFragmentsText), () => "收集海洋残片");
            CurrentFragmentsText = this.GetLocalization(nameof(CurrentFragmentsText), () => "当前拥有");
            ReturnToCampsiteText = this.GetLocalization(nameof(ReturnToCampsiteText), () => "返回营地提交");
            QuestCompleteText = this.GetLocalization(nameof(QuestCompleteText), () => "任务完成！");
        }

        protected override void SetupLocalizedTexts() {
            QuestTitle = this.GetLocalization(nameof(QuestTitle), () => "深渊在呼唤");
            DamageContribution = this.GetLocalization(nameof(DamageContribution), () => "收集进度");
            RequiredContribution = this.GetLocalization(nameof(RequiredContribution), () => "目标: 收集777块海洋残片");
        }

        public override bool CanOpne {
            get {
                if (Main.LocalPlayer == null || !Main.LocalPlayer.active) {
                    return false;
                }

                if (!Main.LocalPlayer.TryGetADVSave(out var save)) {
                    return false;
                }

                //只有在任务触发后且未完成前显示
                if (!save.OldDukeFindFragmentsQuestTriggered) {
                    return false;
                }

                if (save.OldDukeFindFragmentsQuestCompleted) {
                    return false;
                }

                return true;
            }
        }

        protected override (float current, float total, bool isActive) GetTrackingData() {
            if (!Main.LocalPlayer.TryGetADVSave(out var save)) {
                return (0, 777, false);
            }

            float current = GetFragmentCount();
            float total = 777;
            bool isActive = save.OldDukeFindFragmentsQuestTriggered && !save.OldDukeFindFragmentsQuestCompleted;

            return (current, total, isActive);
        }

        /// <summary>
        /// 获取玩家背包中的海洋残片数量
        /// </summary>
        private static int GetFragmentCount() {
            int count = 0;
            Player player = Main.LocalPlayer;
            int fragmentType = ModContent.ItemType<Oceanfragments>();

            for (int i = 0; i < player.inventory.Length; i++) {
                if (player.inventory[i].type == fragmentType) {
                    count += player.inventory[i].stack;
                }
            }

            return count;
        }

        protected override float GetRequiredContribution() {
            return 777;
        }

        protected override void UpdatePanelHeight() {
            base.UpdatePanelHeight();
            currentPanelHeight += 50f;
        }

        public override void Update() {
            base.Update();

            //更新硫磺海风格动画
            toxicWavePhase += 0.022f;
            sulfurPulse += 0.015f;
            miasmaTimer += 0.032f;
            bubbleTimer += 0.025f;

            if (toxicWavePhase > MathHelper.TwoPi) toxicWavePhase -= MathHelper.TwoPi;
            if (sulfurPulse > MathHelper.TwoPi) sulfurPulse -= MathHelper.TwoPi;
            if (miasmaTimer > MathHelper.TwoPi) miasmaTimer -= MathHelper.TwoPi;
            if (bubbleTimer > MathHelper.TwoPi) bubbleTimer -= MathHelper.TwoPi;
        }

        protected override void DrawPanel(SpriteBatch spriteBatch, float alpha) {
            if (initialize) {
                initialize = false;
                screenYValue = Main.screenHeight / 2f - currentPanelHeight / 2f;
            }

            Texture2D pixel = VaultAsset.placeholder2.Value;

            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);
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

            screenYValue = MathHelper.Clamp(screenYValue, 0, Main.screenHeight - currentPanelHeight);

            //阴影
            Rectangle shadowRect = UIHitBox;
            shadowRect.Offset(6, 8);
            spriteBatch.Draw(pixel, shadowRect, new Rectangle(0, 0, 1, 1), Color.Black * (alpha * 0.6f));

            //硫磺海渐变背景
            DrawSulfurBackground(spriteBatch, alpha);

            //瘴气覆盖层
            float miasmaEffect = (float)Math.Sin(miasmaTimer * 1.1f) * 0.5f + 0.5f;
            Color miasmaTint = new Color(45, 55, 20) * (alpha * 0.4f * miasmaEffect);
            spriteBatch.Draw(pixel, UIHitBox, new Rectangle(0, 0, 1, 1), miasmaTint);

            //毒性波浪覆盖
            DrawToxicWaveOverlay(spriteBatch, UIHitBox, alpha * 0.85f);

            //硫磺海边框
            DrawSulfurFrame(spriteBatch, UIHitBox, alpha, borderGlow);

            //气泡装饰
            DrawBubbleDecoration(spriteBatch, UIHitBox, alpha);
        }

        private void DrawSulfurBackground(SpriteBatch spriteBatch, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int segs = 30;

            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int y1 = (int)(DrawPosition.Y + t * currentPanelHeight);
                int y2 = (int)(DrawPosition.Y + t2 * currentPanelHeight);
                Rectangle r = new Rectangle((int)DrawPosition.X, y1, (int)PanelWidth, Math.Max(1, y2 - y1));

                Color sulfurDeep = new Color(12, 18, 8);
                Color toxicMid = new Color(28, 38, 15);
                Color acidEdge = new Color(65, 85, 30);

                float breathing = (float)Math.Sin(sulfurPulse) * 0.5f + 0.5f;
                Color blendBase = Color.Lerp(sulfurDeep, toxicMid, (float)Math.Sin(pulseTimer * 0.5f + t * 1.4f) * 0.5f + 0.5f);
                Color c = Color.Lerp(blendBase, acidEdge, t * 0.7f * (0.3f + breathing * 0.7f));
                c *= alpha * 0.92f;

                spriteBatch.Draw(pixel, r, new Rectangle(0, 0, 1, 1), c);
            }
        }

        private void DrawToxicWaveOverlay(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int bands = 5;

            for (int i = 0; i < bands; i++) {
                float t = i / (float)bands;
                float y = rect.Y + 15 + t * (rect.Height - 30);
                float amp = 5f + (float)Math.Sin((toxicWavePhase + t) * 2.2f) * 3.5f;
                float thickness = 1.8f;
                int segments = 35;
                Vector2 prev = Vector2.Zero;

                for (int s = 0; s <= segments; s++) {
                    float p = s / (float)segments;
                    float localY = y + (float)Math.Sin(toxicWavePhase * 2.2f + p * MathHelper.TwoPi * 1.3f + t) * amp;
                    Vector2 point = new(rect.X + 8 + p * (rect.Width - 16), localY);

                    if (s > 0) {
                        Vector2 diff = point - prev;
                        float len = diff.Length();
                        if (len > 0.01f) {
                            float rot = diff.ToRotation();
                            Color c = new Color(60, 90, 30) * (alpha * 0.08f);
                            sb.Draw(pixel, prev, new Rectangle(0, 0, 1, 1), c, rot, Vector2.Zero, new Vector2(len, thickness), SpriteEffects.None, 0f);
                        }
                    }
                    prev = point;
                }
            }
        }

        private void DrawSulfurFrame(SpriteBatch sb, Rectangle rect, float alpha, float pulse) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color edge = Color.Lerp(new Color(70, 100, 35), new Color(130, 160, 65), pulse) * (alpha * 0.85f);

            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge * 0.75f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.88f);
            sb.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.88f);

            Rectangle inner = rect;
            inner.Inflate(-5, -5);
            Color innerC = new Color(140, 170, 70) * (alpha * 0.22f * pulse);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC * 0.7f);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.88f);
            sb.Draw(pixel, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.88f);

            DrawCornerStar(sb, new Vector2(rect.X + 10, rect.Y + 10), alpha * 0.9f, pulse);
            DrawCornerStar(sb, new Vector2(rect.Right - 10, rect.Y + 10), alpha * 0.9f, pulse);
            DrawCornerStar(sb, new Vector2(rect.X + 10, rect.Bottom - 10), alpha * 0.65f, pulse);
            DrawCornerStar(sb, new Vector2(rect.Right - 10, rect.Bottom - 10), alpha * 0.65f, pulse);
        }

        private static void DrawCornerStar(SpriteBatch sb, Vector2 pos, float a, float pulse) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float size = 5f + (float)Math.Sin(pulse * MathHelper.TwoPi) * 1f;
            Color c = new Color(160, 190, 80) * (a * (0.8f + pulse * 0.2f));

            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.8f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
        }

        private void DrawBubbleDecoration(SpriteBatch spriteBatch, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            for (int i = 0; i < 4; i++) {
                float offset = (bubbleTimer + i * MathHelper.PiOver2) % MathHelper.TwoPi;
                float yPos = rect.Y + 20 + (float)Math.Sin(offset) * 15f + i * 30f;
                float xPos = rect.X + 15 + i * 60f;

                if (yPos > rect.Y + 10 && yPos < rect.Bottom - 10) {
                    float bubbleSize = 3f + (float)Math.Sin(offset * 2f) * 1.5f;
                    Color bubbleColor = new Color(140, 180, 70) * (alpha * 0.35f);

                    spriteBatch.Draw(pixel, new Vector2(xPos, yPos), new Rectangle(0, 0, 1, 1), bubbleColor, 0f,
                        new Vector2(0.5f), new Vector2(bubbleSize), SpriteEffects.None, 0f);
                }
            }
        }

        protected override void DrawContent(SpriteBatch spriteBatch, float alpha) {
            var font = FontAssets.MouseText.Value;
            const float titleScale = 0.72f;
            const float textScale = 0.62f;

            //标题
            Vector2 titlePos = DrawPosition + new Vector2(10, 8);
            Color titleColor = new Color(160, 190, 80) * alpha;

            Color titleGlow = new Color(140, 180, 70) * (alpha * 0.6f);
            for (int i = 0; i < 4; i++) {
                float a = MathHelper.TwoPi * i / 4f;
                Vector2 off = a.ToRotationVector2() * 1.5f;
                Utils.DrawBorderString(spriteBatch, QuestTitle.Value, titlePos + off, titleGlow * 0.5f, titleScale);
            }

            Utils.DrawBorderString(spriteBatch, QuestTitle.Value, titlePos, titleColor, titleScale);

            //分隔线
            float titleHeight = font.MeasureString(QuestTitle.Value).Y * titleScale;
            Vector2 dividerStart = titlePos + new Vector2(0, titleHeight + 4);
            Vector2 dividerEnd = dividerStart + new Vector2(PanelWidth - 20, 0);
            DrawGradientLine(spriteBatch, dividerStart, dividerEnd,
                new Color(100, 140, 50) * (alpha * 0.9f), new Color(100, 140, 50) * (alpha * 0.08f), 1.3f);

            if (!Main.LocalPlayer.TryGetADVSave(out var save)) {
                return;
            }

            bool questCompleted = save.OldDukeFindFragmentsQuestCompleted;
            Vector2 contentPos = dividerStart + new Vector2(0, 12);

            if (questCompleted) {
                DrawQuestCompleted(spriteBatch, contentPos, alpha, textScale);
            }
            else {
                DrawQuestInProgress(spriteBatch, contentPos, alpha, textScale);
            }
        }

        private void DrawQuestInProgress(SpriteBatch spriteBatch, Vector2 startPos, float alpha, float textScale) {
            int fragmentCount = GetFragmentCount();

            //目标文本
            string objectiveText = $"{ObjectiveText.Value}: {CollectFragmentsText.Value}";
            Color objectiveColor = new Color(200, 220, 150) * alpha;
            Utils.DrawBorderString(spriteBatch, objectiveText, startPos, objectiveColor, textScale);

            //当前数量
            Vector2 countPos = startPos + new Vector2(0, 18);
            string countText = $"{CurrentFragmentsText.Value}: {fragmentCount}/777";

            Color countColor;
            if (fragmentCount >= 777) {
                float pulse = (float)Math.Sin(pulseTimer * 3f) * 0.3f + 0.7f;
                countColor = Color.LimeGreen * (alpha * pulse);
            }
            else {
                countColor = new Color(180, 200, 130) * alpha;
            }

            Utils.DrawBorderString(spriteBatch, countText, countPos, countColor, textScale);

            //返回提示
            if (fragmentCount >= 777) {
                Vector2 returnPos = countPos + new Vector2(0, 18);
                float blink = (float)Math.Sin(pulseTimer * 4f) * 0.5f + 0.5f;
                Color returnColor = new Color(160, 220, 100) * (alpha * blink);
                Utils.DrawBorderString(spriteBatch, $"> {ReturnToCampsiteText.Value} <", returnPos, returnColor, textScale * 1.1f);
            }

            //进度条
            Vector2 progressBarPos = startPos + new Vector2(0, 65);
            DrawProgressBar(spriteBatch, progressBarPos, alpha);
        }

        private void DrawQuestCompleted(SpriteBatch spriteBatch, Vector2 startPos, float alpha, float textScale) {
            float pulse = (float)Math.Sin(pulseTimer * 2f) * 0.3f + 0.7f;
            Color completeColor = Color.Lerp(new Color(180, 220, 100), Color.LimeGreen, pulse) * alpha;

            Utils.DrawBorderString(spriteBatch, QuestCompleteText.Value, startPos, completeColor, textScale * 1.2f);

            Vector2 progressBarPos = startPos + new Vector2(0, 35);
            DrawProgressBar(spriteBatch, progressBarPos, alpha);
        }

        protected override void DrawProgressBar(SpriteBatch spriteBatch, Vector2 position, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            float barWidth = PanelWidth - 20;
            float barHeight = 8;

            Rectangle barBg = new Rectangle((int)position.X, (int)position.Y, (int)barWidth, (int)barHeight);
            spriteBatch.Draw(pixel, barBg, new Rectangle(0, 0, 1, 1), new Color(10, 15, 8) * (alpha * 0.8f));

            var (current, total, _) = GetTrackingData();
            float progress = total > 0 ? current / total : 0f;
            float fillWidth = barWidth * Math.Min(progress, 1f);

            if (fillWidth > 0) {
                Rectangle barFill = new Rectangle((int)position.X + 1, (int)position.Y + 1, (int)fillWidth - 2, (int)barHeight - 2);

                Color fillStart = new Color(100, 140, 50);
                Color fillEnd = new Color(160, 190, 80);

                int segmentCount = 20;
                for (int i = 0; i < segmentCount; i++) {
                    float t = i / (float)segmentCount;
                    float t2 = (i + 1) / (float)segmentCount;
                    int x1 = (int)(barFill.X + t * barFill.Width);
                    int x2 = (int)(barFill.X + t2 * barFill.Width);

                    Color segColor = Color.Lerp(fillStart, fillEnd, t);
                    float pulse = (float)Math.Sin(pulseTimer * 2f + t * MathHelper.Pi) * 0.3f + 0.7f;

                    spriteBatch.Draw(pixel, new Rectangle(x1, barFill.Y, Math.Max(1, x2 - x1), barFill.Height),
                        segColor * (alpha * pulse));
                }

                Color glowColor = new Color(160, 190, 80) * (alpha * 0.4f);
                spriteBatch.Draw(pixel, new Rectangle(barFill.X, barFill.Y - 1, barFill.Width, 1), glowColor);
                spriteBatch.Draw(pixel, new Rectangle(barFill.X, barFill.Bottom, barFill.Width, 1), glowColor);
            }

            Color borderColor = new Color(100, 140, 50) * (alpha * 0.8f);
            spriteBatch.Draw(pixel, new Rectangle(barBg.X, barBg.Y, barBg.Width, 1), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(barBg.X, barBg.Bottom - 1, barBg.Width, 1), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(barBg.X, barBg.Y, 1, barBg.Height), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(barBg.Right - 1, barBg.Y, 1, barBg.Height), borderColor);
        }
    }
}
