using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Scenarios.Helen;
using CalamityOverhaul.Content.Scenarios.Helen.Quest;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Helen.Quest.FishoilQuest
{
    internal sealed class FishoilQuestEntry : EntrustEntryData
    {
        public const string QuestKey = "FishoilQuest";
        public const int FishRequired = 300;

        public static LocalizedText QuestTitle { get; private set; }
        public static LocalizedText QuestSummary { get; private set; }
        public static LocalizedText QuestCategory { get; private set; }
        public static LocalizedText ProgressFormat { get; private set; }
        public static LocalizedText TrackerCollecting { get; private set; }
        public static LocalizedText TrackerReady { get; private set; }
        public static LocalizedText StatusSuspended { get; private set; }
        public static LocalizedText StatusCompleted { get; private set; }
        public static LocalizedText StatusSubmittable { get; private set; }
        public static LocalizedText StatusCollectingFormat { get; private set; }
        public static LocalizedText SubmitButtonLabel { get; private set; }
        public static LocalizedText AwaitingSubmitHint { get; private set; }

        private const int SubmissionWatchdogMax = 240;
        private readonly OceanTrackerWidgetStyle oceanStyle = new();

        private int currentFishCount;
        private bool submissionActive;
        private int submissionWatchdog;
        private Rectangle submitButtonRect;
        private bool submitButtonVisible;
        private float submitButtonHover;

        public FishoilQuestEntry()
            : base(QuestKey, null, null, null) {
        }

        public static int CountAvailableFish(Player player) {
            if (player == null) {
                return 0;
            }

            int total = 0;
            Item[] inv = player.inventory;
            for (int i = 0; i < inv.Length; i++) {
                Item item = inv[i];
                if (item != null && item.stack > 0 && FishoilQuestScenario.CandidateFishTypes.Contains(item.type)) {
                    total += item.stack;
                }
            }

            if (Main.myPlayer == player.whoAmI) {
                Item mouse = Main.mouseItem;
                if (mouse != null && mouse.stack > 0 && FishoilQuestScenario.CandidateFishTypes.Contains(mouse.type)) {
                    total += mouse.stack;
                }
            }

            return total;
        }

        public static int ConsumeAvailableFish(Player player, int amount) {
            if (player == null || amount <= 0) {
                return 0;
            }

            int remaining = amount;
            Item[] inv = player.inventory;
            for (int i = 0; i < inv.Length && remaining > 0; i++) {
                Item item = inv[i];
                if (item == null || item.stack <= 0 || !FishoilQuestScenario.CandidateFishTypes.Contains(item.type)) {
                    continue;
                }

                int consume = Math.Min(remaining, item.stack);
                item.stack -= consume;
                remaining -= consume;
                if (item.stack <= 0) {
                    item.TurnToAir();
                }
            }

            if (remaining > 0 && Main.myPlayer == player.whoAmI) {
                Item mouse = Main.mouseItem;
                if (mouse != null && mouse.stack > 0 && FishoilQuestScenario.CandidateFishTypes.Contains(mouse.type)) {
                    int consume = Math.Min(remaining, mouse.stack);
                    mouse.stack -= consume;
                    remaining -= consume;
                    if (mouse.stack <= 0) {
                        mouse.TurnToAir();
                    }
                }
            }

            return amount - remaining;
        }

        public static bool IsPersistentlyCompleted()
            => HalibutState.Read(Main.LocalPlayer, d => d.FishoilQuestCompleted, d => d.FishoilQuestCompleted);

        public static bool IsAwaitingManualSubmit()
            => HalibutState.Read(Main.LocalPlayer, d => d.FishoilQuestSuspended, d => d.FishoilQuestSuspended);

        public static void ClearAwaitingManualSubmit()
            => HalibutState.Write(Main.LocalPlayer, d => d.FishoilQuestSuspended = false, d => d.FishoilQuestSuspended = false);

        public static void InitLocalization(ILocalizedModType host) {
            QuestTitle = host.GetLocalization(nameof(QuestTitle), () => "鱼油采集");
            QuestSummary = host.GetLocalization(nameof(QuestSummary), () => "收集300条普通鱼交给比目鱼，换取一瓶鱼油");
            QuestCategory = host.GetLocalization(nameof(QuestCategory), () => "比目鱼");
            ProgressFormat = host.GetLocalization(nameof(ProgressFormat), () => "{0}/{1}");
            TrackerCollecting = host.GetLocalization(nameof(TrackerCollecting), () => "还需收集 {0} 条鱼");
            TrackerReady = host.GetLocalization(nameof(TrackerReady), () => "鱼已收集完毕，请关注任务以提交");
            StatusSuspended = host.GetLocalization(nameof(StatusSuspended), () => "已挂起");
            StatusCompleted = host.GetLocalization(nameof(StatusCompleted), () => "已完成");
            StatusSubmittable = host.GetLocalization(nameof(StatusSubmittable), () => "可提交");
            StatusCollectingFormat = host.GetLocalization(nameof(StatusCollectingFormat), () => "收集中 ({0}/{1})");
            SubmitButtonLabel = host.GetLocalization(nameof(SubmitButtonLabel), () => "提交鱼");
            AwaitingSubmitHint = host.GetLocalization(nameof(AwaitingSubmitHint), () => "点击提交以将鱼交给比目鱼");
        }

        public static FishoilQuestEntry Create() {
            FishoilQuestEntry entry = new() {
                TitleText = QuestTitle,
                SummaryText = QuestSummary,
                CategoryText = QuestCategory,
                Priority = 10,
                IsNew = true,
            };
            entry.TrackerStyle = entry.oceanStyle;
            entry.EntryStyle = new OceanEntryStyle();
            entry.OnUnsuspended = entry.ClearSuspendedFlag;
            return entry;
        }

        private void ClearSuspendedFlag() => ClearAwaitingManualSubmit();

        public override void OnUpdate() {
            currentFishCount = CountAvailableFish(Main.LocalPlayer);
            Progress = Math.Clamp(currentFishCount / (float)FishRequired, 0f, 1f);
            ProgressLabel ??= QuestTitle;

            if (IsPersistentlyCompleted()) {
                if (Status != QuestEntryStatus.Completed) {
                    QuestManagerUI.Instance?.SetEntryStatus(QuestKey, QuestEntryStatus.Completed, 1f);
                }
                submissionActive = false;
                submissionWatchdog = 0;
                return;
            }

            if (submissionActive) {
                if (NarrativeRunner.IsScenarioActiveOrPending(NarrativeScenario.GetKey<FishoilSubmitScenario>())) {
                    submissionWatchdog = 0;
                }
                else if (!NarrativeTriggerGate.IsBusy) {
                    submissionActive = false;
                    submissionWatchdog = 0;
                }
                else if (++submissionWatchdog > SubmissionWatchdogMax) {
                    submissionActive = false;
                    submissionWatchdog = 0;
                }
            }

            if ((Status == QuestEntryStatus.Tracked || Status == QuestEntryStatus.Active)
                && currentFishCount >= FishRequired
                && !IsAwaitingManualSubmit()
                && !submissionActive
                && !NarrativeTriggerGate.IsBusy) {
                TriggerSubmissionScenario();
            }
        }

        public override void OnStatusChanged(QuestEntryStatus oldStatus, QuestEntryStatus newStatus) {
            if (oldStatus == QuestEntryStatus.Suspended && newStatus == QuestEntryStatus.Tracked) {
                ClearAwaitingManualSubmit();
            }
        }

        private void TriggerSubmissionScenario() {
            if (NarrativeRouter.Begin<FishoilSubmitScenario>()) {
                submissionActive = true;
                submissionWatchdog = 0;
            }
        }

        public override List<string> GetTrackerDetails() {
            if (currentFishCount >= FishRequired) {
                return IsAwaitingManualSubmit()
                    ? [TrackerReady.Value, AwaitingSubmitHint.Value]
                    : [TrackerReady.Value];
            }

            int remaining = FishRequired - currentFishCount;
            return [string.Format(TrackerCollecting.Value, remaining)];
        }

        public override int GetTrackerExtraHeight()
            => IsAwaitingManualSubmit() && !IsPersistentlyCompleted() && currentFishCount >= FishRequired ? 32 : 0;

        public override bool DrawTrackerContent(SpriteBatch sb, Rectangle contentRect, float alpha) {
            var font = FontAssets.MouseText.Value;
            const float textScale = 0.6f;
            int yOffset = 0;

            foreach (string line in GetTrackerDetails()) {
                Color textColor = currentFishCount >= FishRequired
                    ? new Color(100, 255, 200) * alpha
                    : new Color(180, 230, 250) * alpha;
                Utils.DrawBorderString(sb, line, new Vector2(contentRect.X, contentRect.Y + yOffset), textColor, textScale);
                yOffset += (int)(font.MeasureString("A").Y * textScale) + 2;
            }

            yOffset += 4;
            Rectangle barRect = new(contentRect.X, contentRect.Y + yOffset, contentRect.Width, 12);
            string progressText = string.Format(ProgressFormat.Value, Math.Min(currentFishCount, FishRequired), FishRequired);
            oceanStyle.DrawWidgetProgress(sb, barRect, Progress, progressText, alpha);
            yOffset += 18;

            bool awaitingSubmit = IsAwaitingManualSubmit() && !IsPersistentlyCompleted() && currentFishCount >= FishRequired;
            string statusHint = Status switch {
                QuestEntryStatus.Suspended => StatusSuspended.Value,
                QuestEntryStatus.Completed => StatusCompleted.Value,
                _ => currentFishCount >= FishRequired
                    ? StatusSubmittable.Value
                    : string.Format(StatusCollectingFormat.Value, currentFishCount, FishRequired)
            };
            Color statusColor = Status switch {
                QuestEntryStatus.Suspended => new Color(160, 140, 100) * alpha,
                QuestEntryStatus.Completed => new Color(60, 220, 140) * alpha,
                _ => currentFishCount >= FishRequired
                    ? new Color(100, 255, 200) * alpha
                    : new Color(120, 200, 235) * (alpha * 0.7f)
            };
            Utils.DrawBorderString(sb, statusHint, new Vector2(contentRect.X, contentRect.Y + yOffset), statusColor, 0.5f);
            yOffset += 14;

            submitButtonVisible = false;
            if (!awaitingSubmit) {
                submitButtonHover = 0f;
                return true;
            }

            const int btnH = 22;
            int btnW = Math.Min(contentRect.Width, 132);
            int btnX = contentRect.X + (contentRect.Width - btnW) / 2;
            int btnY = contentRect.Y + yOffset;
            submitButtonRect = new Rectangle(btnX, btnY, btnW, btnH);
            submitButtonVisible = true;

            bool hover = submitButtonRect.Contains(Main.mouseX, Main.mouseY);
            submitButtonHover = MathHelper.Lerp(submitButtonHover, hover ? 1f : 0f, 0.2f);

            Texture2D px = VaultAsset.placeholder2.Value;
            Color fill = Color.Lerp(new Color(20, 80, 110), new Color(40, 160, 195), submitButtonHover) * alpha;
            sb.Draw(px, submitButtonRect, fill);

            Color edge = Color.Lerp(new Color(80, 180, 220), new Color(140, 240, 255), submitButtonHover) * alpha;
            sb.Draw(px, new Rectangle(submitButtonRect.X, submitButtonRect.Y, submitButtonRect.Width, 1), edge);
            sb.Draw(px, new Rectangle(submitButtonRect.X, submitButtonRect.Bottom - 1, submitButtonRect.Width, 1), edge);
            sb.Draw(px, new Rectangle(submitButtonRect.X, submitButtonRect.Y, 1, submitButtonRect.Height), edge);
            sb.Draw(px, new Rectangle(submitButtonRect.Right - 1, submitButtonRect.Y, 1, submitButtonRect.Height), edge);

            string label = SubmitButtonLabel.Value;
            const float scale = 0.7f;
            Vector2 size = font.MeasureString(label) * scale;
            Vector2 pos = new(
                submitButtonRect.X + (submitButtonRect.Width - size.X) / 2f,
                submitButtonRect.Y + (submitButtonRect.Height - size.Y) / 2f);
            Color textC = Color.Lerp(new Color(190, 230, 250), Color.White, submitButtonHover) * alpha;
            Utils.DrawBorderString(sb, label, pos, textC, scale);
            return true;
        }

        public override bool HandleTrackerInput(Rectangle widgetRect, Rectangle contentRect) {
            if (!submitButtonVisible || IsPersistentlyCompleted() || !IsAwaitingManualSubmit()) {
                return false;
            }

            if (!submitButtonRect.Contains(Main.mouseX, Main.mouseY)) {
                return false;
            }

            Main.LocalPlayer.mouseInterface = true;
            if (UIHandleLoader.keyLeftPressState == KeyPressState.Pressed) {
                ClearAwaitingManualSubmit();
                SoundEngine.PlaySound(SoundID.MenuTick);
            }

            return true;
        }
    }
}
