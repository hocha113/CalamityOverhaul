using CalamityOverhaul.Content.ADV.EntrustManager;
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

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Quest.FishoilQuest
{
    /// <summary>鱼油委托条目，追踪收集进度并触发提交</summary>
    internal class FishoilQuestEntry : EntrustEntryData
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

        /// <summary>当前背包中的鱼总数（每帧刷新）</summary>
        private int currentFishCount;
        /// <summary>提交场景是否正在进行</summary>
        private bool submissionActive;
        /// <summary>提交场景看门狗，超时强制清 submissionActive</summary>
        private int submissionWatchdog;
        /// <summary>看门狗上限240帧，覆盖入队+首帧UI延迟</summary>
        private const int SubmissionWatchdogMax = 240;

        private readonly OceanTrackerWidgetStyle oceanStyle = new();

        public FishoilQuestEntry()
            : base(QuestKey, null, null, null) {
        }

        /// <summary>候选鱼总数，含 <see cref="Main.mouseItem"/></summary>
        public static int CountAvailableFish(Player player) {
            if (player == null) return 0;
            int total = 0;
            var inv = player.inventory;
            for (int i = 0; i < inv.Length; i++) {
                Item item = inv[i];
                if (item != null && item.stack > 0
                    && FishoilQuestScenario.CandidateFishTypes.Contains(item.type)) {
                    total += item.stack;
                }
            }
            //计入鼠标物品，避免拖鱼瞬间触发/消耗不一致
            if (Main.myPlayer == player.whoAmI) {
                Item mouse = Main.mouseItem;
                if (mouse != null && mouse.stack > 0
                    && FishoilQuestScenario.CandidateFishTypes.Contains(mouse.type)) {
                    total += mouse.stack;
                }
            }
            return total;
        }

        /// <summary>从玩家背包与鼠标中按需消耗候选鱼，返回实际成功消耗的数量</summary>
        public static int ConsumeAvailableFish(Player player, int amount) {
            if (player == null || amount <= 0) return 0;
            int remaining = amount;
            var inv = player.inventory;
            for (int i = 0; i < inv.Length && remaining > 0; i++) {
                Item item = inv[i];
                if (item == null || item.stack <= 0) continue;
                if (!FishoilQuestScenario.CandidateFishTypes.Contains(item.type)) continue;
                int consume = Math.Min(remaining, item.stack);
                item.stack -= consume;
                remaining -= consume;
                if (item.stack <= 0) item.TurnToAir();
            }
            if (remaining > 0 && Main.myPlayer == player.whoAmI) {
                Item mouse = Main.mouseItem;
                if (mouse != null && mouse.stack > 0
                    && FishoilQuestScenario.CandidateFishTypes.Contains(mouse.type)) {
                    int consume = Math.Min(remaining, mouse.stack);
                    mouse.stack -= consume;
                    remaining -= consume;
                    if (mouse.stack <= 0) mouse.TurnToAir();
                }
            }
            return amount - remaining;
        }

        /// <summary>持久化完成标志，自动触发逻辑优先读此</summary>
        public static bool IsPersistentlyCompleted() {
            if (Main.LocalPlayer.TryGetADVSave(out var save)) {
                return save.Get<HalibutADVData>().FishoilQuestCompleted;
            }
            return false;
        }

        /// <summary>等待手动提交，拒交后置位，由侧边栏按钮重触发</summary>
        public static bool IsAwaitingManualSubmit() {
            if (Main.LocalPlayer.TryGetADVSave(out var save)) {
                return save.Get<HalibutADVData>().FishoilQuestSuspended;
            }
            return false;
        }

        /// <summary>清除"等待手动提交"持久化标志</summary>
        public static void ClearAwaitingManualSubmit() {
            if (Main.LocalPlayer.TryGetADVSave(out var save)) {
                save.Get<HalibutADVData>().FishoilQuestSuspended = false;
            }
        }

        /// <summary>InitLocalization，SetStaticDefaults 外调</summary>
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

        /// <summary>创建并配置一个新的任务条目实例</summary>
        public static FishoilQuestEntry Create() {
            var entry = new FishoilQuestEntry {
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

        private void ClearSuspendedFlag() {
            if (Main.LocalPlayer.TryGetADVSave(out var save)) {
                save.Get<HalibutADVData>().FishoilQuestSuspended = false;
            }
        }

        public override void OnUpdate() {
            //统计背包+鼠标中的候选鱼数量
            currentFishCount = CountAvailableFish(Main.LocalPlayer);

            Progress = Math.Clamp(currentFishCount / (float)FishRequired, 0f, 1f);
            ProgressLabel ??= QuestTitle;

            //已经持久化完成，确保任务条目状态同步并不再触发任何提交流程
            if (IsPersistentlyCompleted()) {
                if (Status != QuestEntryStatus.Completed) {
                    QuestManagerUI.Instance?.SetEntryStatus(QuestKey, QuestEntryStatus.Completed, 1f);
                }
                submissionActive = false;
                submissionWatchdog = 0;
                return;
            }

            //场景结束后或长时间未激活时，重置提交标志（看门狗）
            if (submissionActive) {
                if (ScenarioManager.IsActive(nameof(FishoilSubmitScenario))) {
                    submissionWatchdog = 0;
                }
                else if (!ScenarioManager.IsActive()) {
                    //无活跃场景，提交已结束或入队后被清空
                    submissionActive = false;
                    submissionWatchdog = 0;
                }
                else if (++submissionWatchdog > SubmissionWatchdogMax) {
                    //别的场景把队列长时间占着，强制释放避免永久卡住
                    submissionActive = false;
                    submissionWatchdog = 0;
                }
            }

            //当任务处于激活/关注状态且鱼够了，自动触发提交场景。
            //Suspended/Completed 状态以及"等待手动提交"标志置位时都不再自动触发
            if ((Status == QuestEntryStatus.Tracked || Status == QuestEntryStatus.Active)
                && currentFishCount >= FishRequired
                && !IsAwaitingManualSubmit()
                && !submissionActive
                && !ScenarioManager.IsActive()) {
                TriggerSubmissionScenario();
            }
        }

        public override void OnStatusChanged(QuestEntryStatus oldStatus, QuestEntryStatus newStatus) {
            //从挂起恢复为关注时，清除持久化挂起标记
            if (oldStatus == QuestEntryStatus.Suspended && newStatus == QuestEntryStatus.Tracked) {
                ClearSuspendedFlag();
                //已经持久化完成则不再触发
                if (IsPersistentlyCompleted()) {
                    return;
                }
                //此处不直接读 currentFishCount，因为状态可能在 OnUpdate 之外变更，
                //OnUpdate 下一帧自然会重新评估并触发，无需重复路径
            }
        }

        /// <summary>启动提交场景，统一通过此方法以确保 submissionActive 标志正确设置</summary>
        private void TriggerSubmissionScenario() {
            ScenarioManager.Reset<FishoilSubmitScenario>();
            ScenarioManager.Start<FishoilSubmitScenario>();
            //无论 Start 是直接启动还是入队，统一置位以避免重复触发
            submissionActive = true;
            submissionWatchdog = 0;
        }

        public override List<string> GetTrackerDetails() {
            if (currentFishCount >= FishRequired) {
                //鱼已收集够：若处于等待手动提交状态，多展示一行提示，
                //顺便让侧边栏自动加大高度以容纳提交按钮
                if (IsAwaitingManualSubmit()) {
                    return [TrackerReady.Value, AwaitingSubmitHint.Value];
                }
                return [TrackerReady.Value];
            }
            int remaining = FishRequired - currentFishCount;
            return [string.Format(TrackerCollecting.Value, remaining)];
        }

        /// <summary>
        /// 等待手动提交时为侧边栏额外预留按钮高度，
        /// 否则按钮会因 widget 高度不足而被直接判定为越界绘制不出来
        /// </summary>
        public override int GetTrackerExtraHeight() {
            return IsAwaitingManualSubmit() && !IsPersistentlyCompleted() && currentFishCount >= FishRequired
                ? 32
                : 0;
        }

        /// <summary>提交按钮的最近一次绘制矩形，供 HandleTrackerInput 命中测试</summary>
        private Rectangle submitButtonRect;
        /// <summary>本帧提交按钮是否实际绘制并可点击</summary>
        private bool submitButtonVisible;
        /// <summary>提交按钮悬停动画进度</summary>
        private float submitButtonHover;

        public override bool DrawTrackerContent(SpriteBatch sb, Rectangle contentRect, float alpha) {
            var font = FontAssets.MouseText.Value;
            float textScale = 0.6f;
            int yOffset = 0;

            //收集进度文本
            var details = GetTrackerDetails();
            foreach (string line in details) {
                Color textColor = currentFishCount >= FishRequired
                    ? new Color(100, 255, 200) * alpha
                    : new Color(180, 230, 250) * alpha;
                Utils.DrawBorderString(sb, line,
                    new Vector2(contentRect.X, contentRect.Y + yOffset),
                    textColor, textScale);
                yOffset += (int)(font.MeasureString("A").Y * textScale) + 2;
            }

            yOffset += 4;

            //进度条
            Rectangle barRect = new(contentRect.X, contentRect.Y + yOffset, contentRect.Width, 12);
            string progressText = string.Format(ProgressFormat.Value, Math.Min(currentFishCount, FishRequired), FishRequired);
            oceanStyle.DrawWidgetProgress(sb, barRect, Progress, progressText, alpha);

            yOffset += 18;

            //是否进入"等待手动提交"展示分支
            bool awaitingSubmit = IsAwaitingManualSubmit()
                && !IsPersistentlyCompleted()
                && currentFishCount >= FishRequired;

            //底部当前状态提示
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
            Utils.DrawBorderString(sb, statusHint,
                new Vector2(contentRect.X, contentRect.Y + yOffset),
                statusColor, 0.5f);

            yOffset += 14;

            //提交按钮：仅在等待手动提交且鱼数达标时绘制
            submitButtonVisible = false;
            if (awaitingSubmit) {
                int btnH = 22;
                int btnW = Math.Min(contentRect.Width, 132);
                int btnX = contentRect.X + (contentRect.Width - btnW) / 2;
                int btnY = contentRect.Y + yOffset;
                //widget 已加高，不额外校验越界
                submitButtonRect = new Rectangle(btnX, btnY, btnW, btnH);
                submitButtonVisible = true;

                bool hover = submitButtonRect.Contains(Main.mouseX, Main.mouseY);
                float hoverTarget = hover ? 1f : 0f;
                submitButtonHover = MathHelper.Lerp(submitButtonHover, hoverTarget, 0.2f);

                var px = VaultAsset.placeholder2.Value;
                Color baseFill = new Color(20, 80, 110);
                Color hoverFill = new Color(40, 160, 195);
                Color fill = Color.Lerp(baseFill, hoverFill, submitButtonHover) * alpha;
                sb.Draw(px, submitButtonRect, fill);

                //边框
                Color edge = Color.Lerp(new Color(80, 180, 220), new Color(140, 240, 255), submitButtonHover) * alpha;
                sb.Draw(px, new Rectangle(submitButtonRect.X, submitButtonRect.Y, submitButtonRect.Width, 1), edge);
                sb.Draw(px, new Rectangle(submitButtonRect.X, submitButtonRect.Bottom - 1, submitButtonRect.Width, 1), edge);
                sb.Draw(px, new Rectangle(submitButtonRect.X, submitButtonRect.Y, 1, submitButtonRect.Height), edge);
                sb.Draw(px, new Rectangle(submitButtonRect.Right - 1, submitButtonRect.Y, 1, submitButtonRect.Height), edge);

                //文字居中
                string label = SubmitButtonLabel.Value;
                float scale = 0.7f;
                Vector2 size = font.MeasureString(label) * scale;
                Vector2 pos = new(
                    submitButtonRect.X + (submitButtonRect.Width - size.X) / 2f,
                    submitButtonRect.Y + (submitButtonRect.Height - size.Y) / 2f);
                Color textC = Color.Lerp(new Color(190, 230, 250), Color.White, submitButtonHover) * alpha;
                Utils.DrawBorderString(sb, label, pos, textC, scale);
            }
            else {
                submitButtonHover = 0f;
            }

            return true;
        }

        public override bool HandleTrackerInput(Rectangle widgetRect, Rectangle contentRect) {
            //仅在按钮当前可见时才进行命中检测
            if (!submitButtonVisible) return false;
            //已完成或不再等待手动提交，直接放行
            if (IsPersistentlyCompleted() || !IsAwaitingManualSubmit()) return false;

            if (!submitButtonRect.Contains(Main.mouseX, Main.mouseY)) return false;

            //悬停期间始终消费输入，避免拖拽手势误判
            Main.LocalPlayer.mouseInterface = true;

            if (UIHandleLoader.keyLeftPressState == KeyPressState.Pressed) {
                //清除挂起标志，让 OnUpdate 在下一帧按常规路径自动触发提交场景
                ClearAwaitingManualSubmit();
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
            return true;
        }
    }
}
