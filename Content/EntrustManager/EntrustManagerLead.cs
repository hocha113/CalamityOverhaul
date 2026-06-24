using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI;
using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.EntrustManager
{
    internal class EntrustManagerLead : ModSystem, ILocalizedModType
    {
        private enum LeadPhase
        {
            Inactive,
            KeyPrompt,
            PanelIntro,
            StyleButtonPrompt,
            TrackPromptInPanel,
            TrackerWidgetIntro,
            SuspendInfoInPanel,
            Complete
        }

        public string LocalizationCategory => "UI";

        #region 本地化

        public static LocalizedText TextKeyPromptBound { get; private set; }
        public static LocalizedText TextKeyPromptWarnTitle { get; private set; }
        public static LocalizedText TextKeyPromptDefaultKey { get; private set; }
        public static LocalizedText TextKeyPromptBindHint { get; private set; }
        public static LocalizedText TextKeyPromptConfirmBtn { get; private set; }
        public static LocalizedText TextPanelIntroTitle { get; private set; }
        public static LocalizedText TextRightClickLabel { get; private set; }
        public static LocalizedText TextRightClickAction { get; private set; }
        public static LocalizedText TextRightClickDesc { get; private set; }
        public static LocalizedText TextMiddleClickLabel { get; private set; }
        public static LocalizedText TextMiddleClickAction { get; private set; }
        public static LocalizedText TextMiddleClickDesc { get; private set; }
        public static LocalizedText TextStyleButtonTitle { get; private set; }
        public static LocalizedText TextStyleButtonLabel { get; private set; }
        public static LocalizedText TextStyleButtonAction { get; private set; }
        public static LocalizedText TextStyleButtonDesc { get; private set; }
        //阶段4：关注引导
        public static LocalizedText TextTrackPromptTitle { get; private set; }
        public static LocalizedText TextTrackPromptHintLabel { get; private set; }
        public static LocalizedText TextTrackPromptHintAction { get; private set; }
        public static LocalizedText TextTrackPromptDesc { get; private set; }
        public static LocalizedText TextTrackPromptNextBtn { get; private set; }
        //阶段5：追踪栏介绍
        public static LocalizedText TextTrackerIntroTitle { get; private set; }
        public static LocalizedText TextTrackerIntroLine1 { get; private set; }
        public static LocalizedText TextTrackerIntroLine2 { get; private set; }
        public static LocalizedText TextTrackerIntroLine3 { get; private set; }
        public static LocalizedText TextTrackerIntroNextBtn { get; private set; }
        //阶段6：挂起说明
        public static LocalizedText TextSuspendIntroTitle { get; private set; }
        public static LocalizedText TextSuspendIntroHintLabel { get; private set; }
        public static LocalizedText TextSuspendIntroHintAction { get; private set; }
        public static LocalizedText TextSuspendIntroDesc1 { get; private set; }
        public static LocalizedText TextSuspendIntroDesc2 { get; private set; }
        public static LocalizedText TextConfirmBtn { get; private set; }

        public override void SetStaticDefaults() {
            TextKeyPromptBound = this.GetLocalization(nameof(TextKeyPromptBound), () => "按 [{0}] 打开委托面板");
            TextKeyPromptWarnTitle = this.GetLocalization(nameof(TextKeyPromptWarnTitle), () => "⚠  委托快捷键尚未绑定！");
            TextKeyPromptDefaultKey = this.GetLocalization(nameof(TextKeyPromptDefaultKey), () => "当前按 [{0}]（默认键）可打开委托面板");
            TextKeyPromptBindHint = this.GetLocalization(nameof(TextKeyPromptBindHint), () => "建议前往  设置 → 控制  中绑定自定义按键");
            TextKeyPromptConfirmBtn = this.GetLocalization(nameof(TextKeyPromptConfirmBtn), () => "我知道了");
            TextPanelIntroTitle = this.GetLocalization(nameof(TextPanelIntroTitle), () => "委托操作说明");
            TextRightClickLabel = this.GetLocalization(nameof(TextRightClickLabel), () => "右键单击委托条目");
            TextRightClickAction = this.GetLocalization(nameof(TextRightClickAction), () => " →  关注委托");
            TextRightClickDesc = this.GetLocalization(nameof(TextRightClickDesc), () => "     左侧追踪窗口将持续显示任务进度");
            TextMiddleClickLabel = this.GetLocalization(nameof(TextMiddleClickLabel), () => "中键单击委托条目");
            TextMiddleClickAction = this.GetLocalization(nameof(TextMiddleClickAction), () => " →  挂起委托");
            TextMiddleClickDesc = this.GetLocalization(nameof(TextMiddleClickDesc), () => "     暂时隐藏该委托，不在追踪窗口中显示");
            TextStyleButtonTitle = this.GetLocalization(nameof(TextStyleButtonTitle), () => "样式按钮提示");
            TextStyleButtonLabel = this.GetLocalization(nameof(TextStyleButtonLabel), () => "左键单击顶部小按钮");
            TextStyleButtonAction = this.GetLocalization(nameof(TextStyleButtonAction), () => " →  切换界面样式");
            TextStyleButtonDesc = this.GetLocalization(nameof(TextStyleButtonDesc), () => "     可以在荒漠、嘉登与森林风格之间循环切换");
            TextTrackPromptTitle = this.GetLocalization(nameof(TextTrackPromptTitle), () => "关注感兴趣的委托");
            TextTrackPromptHintLabel = this.GetLocalization(nameof(TextTrackPromptHintLabel), () => "右键单击委托");
            TextTrackPromptHintAction = this.GetLocalization(nameof(TextTrackPromptHintAction), () => " →  设为已关注");
            TextTrackPromptDesc = this.GetLocalization(nameof(TextTrackPromptDesc), () => "     被关注的委托会被固定显示在屏幕左侧的追踪栏中，方便随时查看进度");
            TextTrackPromptNextBtn = this.GetLocalization(nameof(TextTrackPromptNextBtn), () => "下一步");
            TextTrackerIntroTitle = this.GetLocalization(nameof(TextTrackerIntroTitle), () => "委托追踪栏");
            TextTrackerIntroLine1 = this.GetLocalization(nameof(TextTrackerIntroLine1), () => "屏幕左侧的追踪栏会常驻显示所有被关注的委托");
            TextTrackerIntroLine2 = this.GetLocalization(nameof(TextTrackerIntroLine2), () => "按住左键拖动它，可以在垂直方向调整位置");
            TextTrackerIntroLine3 = this.GetLocalization(nameof(TextTrackerIntroLine3), () => "打开委托管理器时追踪栏会自动收起，避免遮挡");
            TextTrackerIntroNextBtn = this.GetLocalization(nameof(TextTrackerIntroNextBtn), () => "下一步");
            TextSuspendIntroTitle = this.GetLocalization(nameof(TextSuspendIntroTitle), () => "挂起不感兴趣的委托");
            TextSuspendIntroHintLabel = this.GetLocalization(nameof(TextSuspendIntroHintLabel), () => "中键单击委托");
            TextSuspendIntroHintAction = this.GetLocalization(nameof(TextSuspendIntroHintAction), () => " →  挂起委托");
            TextSuspendIntroDesc1 = this.GetLocalization(nameof(TextSuspendIntroDesc1), () => "     挂起后的委托不会出现在左侧追踪栏中，适合暂时搁置");
            TextSuspendIntroDesc2 = this.GetLocalization(nameof(TextSuspendIntroDesc2), () => "     可在  已挂起  选项卡中找到它们并恢复关注");
            TextConfirmBtn = this.GetLocalization(nameof(TextConfirmBtn), () => "明白了");
        }

        #endregion

        private static LeadPhase currentPhase = LeadPhase.Inactive;
        private static float animProgress = 0f;
        private static float shaderTimer = 0f;

        //阶段内部帧计数
        private static int phaseTickTimer;
        //进入阶段时关注/挂起计数快照
        private static int trackedSnapshot;
        private static int suspendedSnapshot;
        //>0 时玩家操作或兜底触发，倒计后推进
        private static int autoAdvanceDelay;
        //自动推进倒计总时长
        private static int autoAdvanceDelayTotal;

        //回避比目鱼界面引导期间的累计等待帧
        private static int halibutDeferTimer;
        //回避比目鱼引导的最长等待（保底），约2分钟，防止其卡住时无限等待
        private const int HalibutDeferTimeout = 60 * 120;

        //防呆兜底超时，60 帧≈1 秒
        private const int Phase4SoftTimeout = 60 * 30;
        private const int Phase5SoftTimeout = 60 * 35;
        private const int Phase6SoftTimeout = 60 * 25;
        private const int AutoActionConfirmDelay = 36;
        private const int AutoFallbackAdvanceDelay = 60;

        private const float AnimSpeed = 0.12f;
        //着色器边框扩展，与 ForestPanel 一致
        private const int EdgePad = 8;

        //卡片底部确认按钮预留高度
        private const int CardFooterReserve = 30;

        //阶段1卡片尺寸
        private const int CardW1 = 320;
        private const int CardH1_Bound = 92;
        private const int CardH1_Unbound = 138;
        //阶段2卡片尺寸
        private const int CardW2 = 318;
        private const int CardH2 = 176;
        //阶段3卡片尺寸
        private const int CardW3 = 286;
        private const int CardH3 = 138;
        //与样式切换按钮位置一致
        private const int StyleButtonOffsetFromPanelRight = 180;
        private const int StyleButtonTop = 36;
        private const int StyleButtonSize = 26;

        public override void OnWorldUnload() {
            currentPhase = LeadPhase.Inactive;
            animProgress = 0f;
            halibutDeferTimer = 0;
            ResetPhaseGuards();
        }

        //阶段切换时复位计时器与快照
        private static void ResetPhaseGuards() {
            phaseTickTimer = 0;
            autoAdvanceDelay = 0;
            autoAdvanceDelayTotal = 0;
            var ui = QuestManagerUI.Instance;
            trackedSnapshot = ui?.CountByStatus(QuestEntryStatus.Tracked) ?? 0;
            suspendedSnapshot = ui?.CountByStatus(QuestEntryStatus.Suspended) ?? 0;
        }

        //启动一个自动推进倒计时
        private static void StartAutoAdvance(int delay) {
            autoAdvanceDelay = delay;
            autoAdvanceDelayTotal = delay;
        }

        public override void UpdateUI(GameTime gameTime) {
            if (Main.gameMenu) return;
            var ui = QuestManagerUI.Instance;
            if (ui == null) return;

            shaderTimer += 0.004f;
            if (shaderTimer > 100f) shaderTimer -= 100f;

            switch (currentPhase) {
                case LeadPhase.Inactive:
                    if (ui.HasAnyEntry && !Main.LocalPlayer.GetModPlayer<StoryPlayer>().Get<EntrustGuideData>().GuideSeen) {
                        //回避比目鱼界面引导：其进行或待触发期间先按兵不动，结束后再出现
                        if (HalibutHudLead.ShouldDeferEntrust) {
                            //过时保底：等待过久则接管并强制结束比目鱼引导，避免无限等待与两套引导叠加
                            if (++halibutDeferTimer < HalibutDeferTimeout) {
                                break;
                            }
                            HalibutHudLead.ForceComplete();
                        }
                        halibutDeferTimer = 0;
                        currentPhase = LeadPhase.KeyPrompt;
                        animProgress = 0f;
                        ResetPhaseGuards();
                    }
                    break;

                case LeadPhase.KeyPrompt:
                    animProgress = MathHelper.Lerp(animProgress, 1f, AnimSpeed);
                    if (ui.IsOpen) {
                        currentPhase = LeadPhase.PanelIntro;
                        animProgress = 0f;
                        ResetPhaseGuards();
                    }
                    break;

                case LeadPhase.PanelIntro:
                    animProgress = MathHelper.Lerp(animProgress, 1f, AnimSpeed);
                    if (!ui.IsOpen) {
                        currentPhase = LeadPhase.KeyPrompt;
                        animProgress = 0f;
                        ResetPhaseGuards();
                    }
                    break;

                case LeadPhase.StyleButtonPrompt:
                    animProgress = MathHelper.Lerp(animProgress, 1f, AnimSpeed);
                    if (!ui.IsOpen) {
                        currentPhase = LeadPhase.KeyPrompt;
                        animProgress = 0f;
                        ResetPhaseGuards();
                    }
                    break;

                case LeadPhase.TrackPromptInPanel:
                    animProgress = MathHelper.Lerp(animProgress, 1f, AnimSpeed);
                    //面板被关闭时退回按键提示阶段
                    if (!ui.IsOpen) {
                        currentPhase = LeadPhase.KeyPrompt;
                        animProgress = 0f;
                        ResetPhaseGuards();
                        break;
                    }
                    UpdateTrackPhaseGuard(ui);
                    break;

                case LeadPhase.TrackerWidgetIntro:
                    animProgress = MathHelper.Lerp(animProgress, 1f, AnimSpeed);
                    UpdateTrackerIntroGuard();
                    break;

                case LeadPhase.SuspendInfoInPanel:
                    animProgress = MathHelper.Lerp(animProgress, 1f, AnimSpeed);
                    if (!ui.IsOpen) {
                        currentPhase = LeadPhase.KeyPrompt;
                        animProgress = 0f;
                        ResetPhaseGuards();
                        break;
                    }
                    UpdateSuspendPhaseGuard(ui);
                    break;

                case LeadPhase.Complete:
                    break;
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            if (currentPhase != LeadPhase.KeyPrompt && currentPhase != LeadPhase.PanelIntro
                && currentPhase != LeadPhase.StyleButtonPrompt && currentPhase != LeadPhase.TrackPromptInPanel
                && currentPhase != LeadPhase.TrackerWidgetIntro && currentPhase != LeadPhase.SuspendInfoInPanel) return;
            int idx = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
            if (idx == -1) return;

            layers.Insert(idx, new LegacyGameInterfaceLayer(
                "CWRMod: Entrust Guide Lead",
                delegate {
                    var sb = Main.spriteBatch;
                    if (currentPhase == LeadPhase.KeyPrompt)
                        DrawKeyPromptCard(sb);
                    else if (currentPhase == LeadPhase.PanelIntro)
                        DrawPanelIntroCard(sb);
                    else if (currentPhase == LeadPhase.StyleButtonPrompt)
                        DrawStyleButtonPromptCard(sb);
                    else if (currentPhase == LeadPhase.TrackPromptInPanel)
                        DrawTrackPromptCard(sb);
                    else if (currentPhase == LeadPhase.TrackerWidgetIntro)
                        DrawTrackerIntroCard(sb);
                    else if (currentPhase == LeadPhase.SuspendInfoInPanel)
                        DrawSuspendIntroCard(sb);
                    return true;
                },
                InterfaceScaleType.UI
            ));
        }

        private static void MarkGuideSeen() {
            Main.LocalPlayer.GetModPlayer<StoryPlayer>().Get<EntrustGuideData>().GuideSeen = true;
            currentPhase = LeadPhase.Complete;
        }

        //防呆兜底

        //阶段4：关注操作或超时自动关注
        private static void UpdateTrackPhaseGuard(QuestManagerUI ui) {
            phaseTickTimer++;

            //自动推进倒计
            if (autoAdvanceDelay > 0) {
                autoAdvanceDelay--;
                if (autoAdvanceDelay == 0)
                    StartTrackerWidgetIntro();
                return;
            }

            //玩家成功关注
            int trackedNow = ui.CountByStatus(QuestEntryStatus.Tracked);
            if (trackedNow > trackedSnapshot) {
                StartAutoAdvance(AutoActionConfirmDelay);
                return;
            }

            //超时兜底自动关注第一条
            if (phaseTickTimer > Phase4SoftTimeout) {
                string key = ui.TryGetFirstTrackableKey();
                if (key != null && ui.SetEntryStatus(key, QuestEntryStatus.Tracked)) {
                    StartAutoAdvance(AutoFallbackAdvanceDelay);
                }
                else {
                    //无可关注条目直接推进
                    StartTrackerWidgetIntro();
                }
            }
        }

        //阶段5：长时间无操作推进阶段6
        private static void UpdateTrackerIntroGuard() {
            phaseTickTimer++;
            if (autoAdvanceDelay > 0) {
                autoAdvanceDelay--;
                if (autoAdvanceDelay == 0)
                    StartSuspendIntro();
                return;
            }
            if (phaseTickTimer > Phase5SoftTimeout) {
                autoAdvanceDelay = AutoActionConfirmDelay;
            }
        }

        //阶段6：挂起操作或超时完成引导
        private static void UpdateSuspendPhaseGuard(QuestManagerUI ui) {
            phaseTickTimer++;

            if (autoAdvanceDelay > 0) {
                autoAdvanceDelay--;
                if (autoAdvanceDelay == 0)
                    MarkGuideSeen();
                return;
            }

            //玩家成功挂起任意委托
            int suspendedNow = ui.CountByStatus(QuestEntryStatus.Suspended);
            if (suspendedNow > suspendedSnapshot) {
                autoAdvanceDelay = AutoActionConfirmDelay;
                return;
            }

            //超时兜底直接结束
            if (phaseTickTimer > Phase6SoftTimeout) {
                autoAdvanceDelay = AutoActionConfirmDelay;
            }
        }

        private static void AdvanceFromKeyPrompt() {
            var ui = QuestManagerUI.Instance;
            if (ui == null) return;

            if (!ui.IsOpen)
                ui.TogglePanel();

            currentPhase = LeadPhase.PanelIntro;
            animProgress = 0f;
            ResetPhaseGuards();
        }

        private static string GetBoundKeyName() {
            if (CWRKeySystem.QuestManager_Key == null) return null;
            var keys = CWRKeySystem.QuestManager_Key.GetAssignedKeys();
            return keys.Count > 0 ? keys[0] : null;
        }

        //阶段1按键提示卡

        private static void DrawKeyPromptCard(SpriteBatch sb) {
            string boundKey = GetBoundKeyName();
            bool hasBind = boundKey != null;
            string displayKey = hasBind ? boundKey : "K";
            int cardH = hasBind ? CardH1_Bound : CardH1_Unbound;

            float slideY = (1f - animProgress) * 65f;
            float x = 20f;
            float y = Main.screenHeight - cardH - 20f + slideY;
            float alpha = animProgress;
            var card = new Rectangle((int)x, (int)y, CardW1, cardH);

            DrawCardBackground(sb, card, 0f, alpha);

            var font = FontAssets.MouseText.Value;
            float px = x + 14f, py = y + 11f;

            if (hasBind) {
                //单行：已绑定
                string line = TextKeyPromptBound.Format(displayKey);
                int wrapW = (int)((CardW1 - 28) / 0.85f);
                string[] wrapped = VaultUtils.WrapTextArray(line, font, wrapW, 99, out _);
                foreach (string wl in wrapped) {
                    if (string.IsNullOrEmpty(wl)) continue;
                    Utils.DrawBorderString(sb, wl.TrimEnd('-', ' '), new Vector2(px, py),
                        new Color(255, 255, 230, (int)(255 * alpha)), 0.85f);
                    py += font.MeasureString("A").Y * 0.85f + 2f;
                }
            }
            else {
                float warnScale = 0.82f;
                float subScale1 = 0.73f;
                float subScale2 = 0.63f;
                float lineH_w = font.MeasureString("A").Y * warnScale + 2f;
                float lineH_1 = font.MeasureString("A").Y * subScale1 + 2f;

                //警告标题
                float blink = 0.84f + MathF.Sin(shaderTimer * 52f) * 0.16f;
                var warnColor = new Color(
                    (int)(255 * blink),
                    (int)(175 * blink),
                    (int)(25 * blink),
                    (int)(255 * alpha));
                Utils.DrawBorderString(sb, TextKeyPromptWarnTitle.Value,
                    new Vector2(px, py), warnColor, warnScale);

                py += lineH_w + 2f;

                //按键提示
                string keyLine = TextKeyPromptDefaultKey.Format(displayKey);
                int keyWrapW = (int)((CardW1 - 28) / subScale1);
                string[] keyWrapped = VaultUtils.WrapTextArray(keyLine, font, keyWrapW, 99, out _);
                foreach (string wl in keyWrapped) {
                    if (string.IsNullOrEmpty(wl)) continue;
                    Utils.DrawBorderString(sb, wl.TrimEnd('-', ' '), new Vector2(px, py),
                        new Color(235, 225, 200, (int)(245 * alpha)), subScale1);
                    py += lineH_1;
                }
                py += 1f;

                //绑定引导
                int hintWrapW = (int)((CardW1 - 28) / subScale2);
                string[] hintWrapped = VaultUtils.WrapTextArray(TextKeyPromptBindHint.Value, font, hintWrapW, 99, out _);
                foreach (string wl in hintWrapped) {
                    if (string.IsNullOrEmpty(wl)) continue;
                    Utils.DrawBorderString(sb, wl.TrimEnd('-', ' '), new Vector2(px, py),
                        new Color(165, 155, 115, (int)(195 * alpha)), subScale2);
                    py += font.MeasureString("A").Y * subScale2 + 2f;
                }
            }

            if (DrawConfirmButton(sb, card, alpha, TextKeyPromptConfirmBtn.Value))
                AdvanceFromKeyPrompt();
        }

        //阶段2说明卡

        private static void DrawPanelIntroCard(SpriteBatch sb) {
            var ui = QuestManagerUI.Instance;
            if (ui == null) return;

            float slideX = (1f - animProgress) * 80f;
            float x = ui.PanelRightEdge + 15f - slideX;
            float y = (Main.screenHeight - CardH2) * 0.5f;
            float alpha = animProgress;
            var card = new Rectangle((int)x, (int)y, CardW2, CardH2);

            DrawCardBackground(sb, card, 1f, alpha);

            //左侧三角箭头
            DrawLeftArrow(sb, new Vector2(x - 8f, y + CardH2 * 0.5f), alpha);

            var font = FontAssets.MouseText.Value;
            float titleScale = 0.80f;
            float bodyScale = 0.68f;
            float subScale = 0.62f;
            float px = x + 14f, py = y + 11f;
            float lineH_t = font.MeasureString("A").Y * titleScale + 2f;
            float lineH_b = font.MeasureString("A").Y * bodyScale + 2f;
            float lineH_s = font.MeasureString("A").Y * subScale + 2f;

            //标题
            Utils.DrawBorderString(sb, TextPanelIntroTitle.Value,
                new Vector2(px, py),
                new Color(230, 225, 100, (int)(255 * alpha)), titleScale);
            py += lineH_t + 2f;

            //分割线
            BaseManagerStyle.FillRect(sb,
                new Rectangle((int)px, (int)py, CardW2 - 28, 1),
                new Color(130, 125, 70, (int)(130 * alpha)));
            py += 6f;

            //关注说明
            float rightKeyW = font.MeasureString(TextRightClickLabel.Value).X * bodyScale;
            Utils.DrawBorderString(sb, TextRightClickLabel.Value,
                new Vector2(px, py),
                new Color(95, 210, 255, (int)(240 * alpha)), bodyScale);
            Utils.DrawBorderString(sb, TextRightClickAction.Value,
                new Vector2(px + rightKeyW, py),
                new Color(200, 240, 255, (int)(240 * alpha)), bodyScale);
            py += lineH_b;
            int descWrapW = (int)((CardW2 - 28) / subScale);
            string[] followWrapped = VaultUtils.WrapTextArray(TextRightClickDesc.Value, font, descWrapW, 99, out _);
            foreach (string wl in followWrapped) {
                if (string.IsNullOrEmpty(wl)) continue;
                Utils.DrawBorderString(sb, wl.TrimEnd('-', ' '), new Vector2(px, py),
                    new Color(130, 165, 175, (int)(200 * alpha)), subScale);
                py += lineH_s;
            }
            py += 6f;

            //挂起说明
            float midKeyW = font.MeasureString(TextMiddleClickLabel.Value).X * bodyScale;
            Utils.DrawBorderString(sb, TextMiddleClickLabel.Value,
                new Vector2(px, py),
                new Color(130, 220, 145, (int)(240 * alpha)), bodyScale);
            Utils.DrawBorderString(sb, TextMiddleClickAction.Value,
                new Vector2(px + midKeyW, py),
                new Color(195, 240, 195, (int)(240 * alpha)), bodyScale);
            py += lineH_b;
            string[] suspendWrapped = VaultUtils.WrapTextArray(TextMiddleClickDesc.Value, font, descWrapW, 99, out _);
            foreach (string wl in suspendWrapped) {
                if (string.IsNullOrEmpty(wl)) continue;
                Utils.DrawBorderString(sb, wl.TrimEnd('-', ' '), new Vector2(px, py),
                    new Color(120, 155, 120, (int)(200 * alpha)), subScale);
                py += lineH_s;
            }

            if (DrawConfirmButton(sb, card, alpha))
                StartStyleButtonPrompt();
        }

        //阶段3样式按钮提示

        private static void StartStyleButtonPrompt() {
            currentPhase = LeadPhase.StyleButtonPrompt;
            animProgress = 0f;
            ResetPhaseGuards();
        }

        private static Rectangle GetStyleSwitchGuideRect(QuestManagerUI ui) {
            return new Rectangle(
                ui.PanelRightEdge - StyleButtonOffsetFromPanelRight,
                StyleButtonTop,
                StyleButtonSize,
                StyleButtonSize);
        }

        private static void DrawStyleButtonPromptCard(SpriteBatch sb) {
            var ui = QuestManagerUI.Instance;
            if (ui == null) return;

            Rectangle styleRect = GetStyleSwitchGuideRect(ui);
            float alpha = animProgress;
            DrawStyleButtonHighlight(sb, styleRect, alpha);

            float slideX = (1f - animProgress) * 70f;
            float x = MathHelper.Clamp(styleRect.Right + 16f + slideX, 20f, Main.screenWidth - CardW3 - 20f);
            float y = MathHelper.Clamp(styleRect.Y - 8f, 20f, Main.screenHeight - CardH3 - 20f);
            var card = new Rectangle((int)x, (int)y, CardW3, CardH3);

            DrawCardBackground(sb, card, 0.5f, alpha);
            DrawLeftArrow(sb, new Vector2(x - 8f, y + 28f), alpha);

            var font = FontAssets.MouseText.Value;
            float titleScale = 0.78f;
            float bodyScale = 0.66f;
            float subScale = 0.60f;
            float px = x + 14f, py = y + 10f;
            float lineH_t = font.MeasureString("A").Y * titleScale + 2f;
            float lineH_b = font.MeasureString("A").Y * bodyScale + 2f;
            float lineH_s = font.MeasureString("A").Y * subScale + 2f;

            Utils.DrawBorderString(sb, TextStyleButtonTitle.Value,
                new Vector2(px, py),
                new Color(230, 225, 100, (int)(255 * alpha)), titleScale);
            py += lineH_t + 2f;

            BaseManagerStyle.FillRect(sb,
                new Rectangle((int)px, (int)py, CardW3 - 28, 1),
                new Color(130, 125, 70, (int)(130 * alpha)));
            py += 6f;

            float keyW = font.MeasureString(TextStyleButtonLabel.Value).X * bodyScale;
            Utils.DrawBorderString(sb, TextStyleButtonLabel.Value,
                new Vector2(px, py),
                new Color(245, 190, 95, (int)(240 * alpha)), bodyScale);
            Utils.DrawBorderString(sb, TextStyleButtonAction.Value,
                new Vector2(px + keyW, py),
                new Color(255, 230, 170, (int)(240 * alpha)), bodyScale);
            py += lineH_b;

            int descWrapW = (int)((CardW3 - 28) / subScale);
            string[] wrapped = VaultUtils.WrapTextArray(TextStyleButtonDesc.Value, font, descWrapW, 99, out _);
            foreach (string wl in wrapped) {
                if (string.IsNullOrEmpty(wl)) continue;
                Utils.DrawBorderString(sb, wl.TrimEnd('-', ' '), new Vector2(px, py),
                    new Color(175, 150, 105, (int)(205 * alpha)), subScale);
                py += lineH_s;
            }

            bool clickedStyleButton = styleRect.Contains(Main.mouseX, Main.mouseY)
                && Main.mouseLeft && !Main.mouseLeftRelease;
            if (clickedStyleButton || DrawConfirmButton(sb, card, alpha))
                StartTrackPrompt();
        }

        private static void DrawStyleButtonHighlight(SpriteBatch sb, Rectangle styleRect, float alpha) {
            float pulse = 0.65f + MathF.Sin(shaderTimer * 44f) * 0.35f;
            Rectangle glowRect = styleRect;
            glowRect.Inflate(5, 5);
            BaseManagerStyle.StrokeRect(sb, glowRect, 2,
                new Color(255, 205, 90, (int)(210 * alpha * pulse)));
            glowRect.Inflate(3, 3);
            BaseManagerStyle.StrokeRect(sb, glowRect, 1,
                new Color(255, 230, 140, (int)(120 * alpha * pulse)));
        }

        //阶段4关注引导卡

        //阶段4卡片尺寸
        private const int CardW4 = 318;
        private const int CardH4 = 152;

        private static void StartTrackPrompt() {
            currentPhase = LeadPhase.TrackPromptInPanel;
            animProgress = 0f;
            ResetPhaseGuards();
        }

        private static void DrawTrackPromptCard(SpriteBatch sb) {
            var ui = QuestManagerUI.Instance;
            if (ui == null) return;

            float slideX = (1f - animProgress) * 80f;
            float x = ui.PanelRightEdge + 15f - slideX;
            float y = (Main.screenHeight - CardH4) * 0.5f;
            float alpha = animProgress;
            var card = new Rectangle((int)x, (int)y, CardW4, CardH4);

            DrawCardBackground(sb, card, 1.5f, alpha);
            DrawLeftArrow(sb, new Vector2(x - 8f, y + CardH4 * 0.5f), alpha);

            var font = FontAssets.MouseText.Value;
            float titleScale = 0.80f;
            float bodyScale = 0.68f;
            float subScale = 0.62f;
            float px = x + 14f, py = y + 11f;
            float lineH_t = font.MeasureString("A").Y * titleScale + 2f;
            float lineH_b = font.MeasureString("A").Y * bodyScale + 2f;
            float lineH_s = font.MeasureString("A").Y * subScale + 2f;

            Utils.DrawBorderString(sb, TextTrackPromptTitle.Value,
                new Vector2(px, py),
                new Color(230, 225, 100, (int)(255 * alpha)), titleScale);
            py += lineH_t + 2f;

            BaseManagerStyle.FillRect(sb,
                new Rectangle((int)px, (int)py, CardW4 - 28, 1),
                new Color(130, 125, 70, (int)(130 * alpha)));
            py += 6f;

            float keyW = font.MeasureString(TextTrackPromptHintLabel.Value).X * bodyScale;
            Utils.DrawBorderString(sb, TextTrackPromptHintLabel.Value,
                new Vector2(px, py),
                new Color(95, 210, 255, (int)(240 * alpha)), bodyScale);
            Utils.DrawBorderString(sb, TextTrackPromptHintAction.Value,
                new Vector2(px + keyW, py),
                new Color(200, 240, 255, (int)(240 * alpha)), bodyScale);
            py += lineH_b;

            int descWrapW = (int)((CardW4 - 28) / subScale);
            string[] wrapped = VaultUtils.WrapTextArray(TextTrackPromptDesc.Value, font, descWrapW, 99, out _);
            foreach (string wl in wrapped) {
                if (string.IsNullOrEmpty(wl)) continue;
                Utils.DrawBorderString(sb, wl.TrimEnd('-', ' '), new Vector2(px, py),
                    new Color(135, 170, 180, (int)(205 * alpha)), subScale);
                py += lineH_s;
            }

            if (DrawConfirmButton(sb, card, alpha, TextTrackPromptNextBtn.Value))
                StartTrackerWidgetIntro();
        }

        //阶段5追踪栏介绍

        //阶段5卡片尺寸
        private const int CardW5 = 312;
        private const int CardH5 = 174;

        private static void StartTrackerWidgetIntro() {
            //收起管理器面板，聚焦追踪栏
            var ui = QuestManagerUI.Instance;
            if (ui != null && ui.IsOpen) ui.TogglePanel();
            currentPhase = LeadPhase.TrackerWidgetIntro;
            animProgress = 0f;
            ResetPhaseGuards();
        }

        private static void DrawTrackerIntroCard(SpriteBatch sb) {
            var widget = EntrustTrackerWidget.Instance;

            //追踪栏外接矩形，不可用时用左侧预估区域
            Rectangle trackerRect;
            if (widget != null && widget.GetTrackerBounds() is { Width: > 0 } bounds) {
                trackerRect = bounds;
            }
            else {
                trackerRect = new Rectangle(8, (int)(Main.screenHeight * 0.35f), 220, 100);
            }

            DrawTrackerHighlight(sb, trackerRect, animProgress);

            float slideX = (1f - animProgress) * 70f;
            float x = MathHelper.Clamp(trackerRect.Right + 18f + slideX, 20f, Main.screenWidth - CardW5 - 20f);
            float y = MathHelper.Clamp(trackerRect.Y - 4f, 20f, Main.screenHeight - CardH5 - 20f);
            float alpha = animProgress;
            var card = new Rectangle((int)x, (int)y, CardW5, CardH5);

            DrawCardBackground(sb, card, 0.25f, alpha);
            DrawLeftArrow(sb, new Vector2(x - 8f, MathHelper.Clamp(trackerRect.Y + trackerRect.Height * 0.5f,
                y + 14f, y + CardH5 - 14f)), alpha);

            var font = FontAssets.MouseText.Value;
            float titleScale = 0.80f;
            float bodyScale = 0.66f;
            float px = x + 14f, py = y + 11f;
            float lineH_t = font.MeasureString("A").Y * titleScale + 2f;
            float lineH_b = font.MeasureString("A").Y * bodyScale + 2f;

            Utils.DrawBorderString(sb, TextTrackerIntroTitle.Value,
                new Vector2(px, py),
                new Color(255, 200, 110, (int)(255 * alpha)), titleScale);
            py += lineH_t + 2f;

            BaseManagerStyle.FillRect(sb,
                new Rectangle((int)px, (int)py, CardW5 - 28, 1),
                new Color(160, 130, 70, (int)(140 * alpha)));
            py += 6f;

            int descWrapW = (int)((CardW5 - 28) / bodyScale);
            DrawBulletLine(sb, font, TextTrackerIntroLine1.Value, ref py, px, bodyScale, descWrapW,
                new Color(225, 235, 245, (int)(235 * alpha)),
                new Color(255, 200, 120, (int)(240 * alpha)), alpha);
            DrawBulletLine(sb, font, TextTrackerIntroLine2.Value, ref py, px, bodyScale, descWrapW,
                new Color(190, 210, 230, (int)(220 * alpha)),
                new Color(255, 200, 120, (int)(240 * alpha)), alpha);
            DrawBulletLine(sb, font, TextTrackerIntroLine3.Value, ref py, px, bodyScale, descWrapW,
                new Color(170, 195, 215, (int)(210 * alpha)),
                new Color(255, 200, 120, (int)(240 * alpha)), alpha);

            if (DrawConfirmButton(sb, card, alpha, TextTrackerIntroNextBtn.Value))
                StartSuspendIntro();
        }

        private static void DrawTrackerHighlight(SpriteBatch sb, Rectangle rect, float alpha) {
            float pulse = 0.65f + MathF.Sin(shaderTimer * 38f) * 0.35f;
            Rectangle glowRect = rect;
            glowRect.Inflate(4, 4);
            BaseManagerStyle.StrokeRect(sb, glowRect, 2,
                new Color(255, 205, 110, (int)(195 * alpha * pulse)));
            glowRect.Inflate(3, 3);
            BaseManagerStyle.StrokeRect(sb, glowRect, 1,
                new Color(255, 230, 160, (int)(110 * alpha * pulse)));
        }

        private static void DrawBulletLine(SpriteBatch sb, ReLogic.Graphics.DynamicSpriteFont font, string text,
            ref float py, float px, float scale, int wrapWidth,
            Color textColor, Color bulletColor, float alpha) {
            //绘制项目符号
            string bullet = "·";
            float bulletW = font.MeasureString(bullet).X * scale + 4f;
            Utils.DrawBorderString(sb, bullet, new Vector2(px, py), bulletColor, scale);

            string[] wrapped = VaultUtils.WrapTextArray(text, font, wrapWidth, 99, out _);
            float lineH = font.MeasureString("A").Y * scale + 2f;
            bool first = true;
            foreach (string wl in wrapped) {
                if (string.IsNullOrEmpty(wl)) continue;
                Utils.DrawBorderString(sb, wl.TrimEnd('-', ' '),
                    new Vector2(px + bulletW, py), textColor, scale);
                py += lineH;
                first = false;
            }
            if (first) py += lineH;
        }

        //阶段6挂起说明卡

        //阶段6卡片尺寸
        private const int CardW6 = 318;
        private const int CardH6 = 170;

        private static void StartSuspendIntro() {
            //重新打开面板关联挂起操作
            var ui = QuestManagerUI.Instance;
            if (ui != null && !ui.IsOpen) ui.TogglePanel();
            currentPhase = LeadPhase.SuspendInfoInPanel;
            animProgress = 0f;
            ResetPhaseGuards();
        }

        private static void DrawSuspendIntroCard(SpriteBatch sb) {
            var ui = QuestManagerUI.Instance;
            if (ui == null) return;

            float slideX = (1f - animProgress) * 80f;
            float x = ui.PanelRightEdge + 15f - slideX;
            float y = (Main.screenHeight - CardH6) * 0.5f;
            float alpha = animProgress;
            var card = new Rectangle((int)x, (int)y, CardW6, CardH6);

            DrawCardBackground(sb, card, 1f, alpha);
            DrawLeftArrow(sb, new Vector2(x - 8f, y + CardH6 * 0.5f), alpha);

            var font = FontAssets.MouseText.Value;
            float titleScale = 0.80f;
            float bodyScale = 0.68f;
            float subScale = 0.62f;
            float px = x + 14f, py = y + 11f;
            float lineH_t = font.MeasureString("A").Y * titleScale + 2f;
            float lineH_b = font.MeasureString("A").Y * bodyScale + 2f;
            float lineH_s = font.MeasureString("A").Y * subScale + 2f;

            Utils.DrawBorderString(sb, TextSuspendIntroTitle.Value,
                new Vector2(px, py),
                new Color(180, 235, 165, (int)(255 * alpha)), titleScale);
            py += lineH_t + 2f;

            BaseManagerStyle.FillRect(sb,
                new Rectangle((int)px, (int)py, CardW6 - 28, 1),
                new Color(110, 150, 100, (int)(140 * alpha)));
            py += 6f;

            float keyW = font.MeasureString(TextSuspendIntroHintLabel.Value).X * bodyScale;
            Utils.DrawBorderString(sb, TextSuspendIntroHintLabel.Value,
                new Vector2(px, py),
                new Color(130, 220, 145, (int)(240 * alpha)), bodyScale);
            Utils.DrawBorderString(sb, TextSuspendIntroHintAction.Value,
                new Vector2(px + keyW, py),
                new Color(195, 240, 195, (int)(240 * alpha)), bodyScale);
            py += lineH_b;

            int descWrapW = (int)((CardW6 - 28) / subScale);
            string[] wrapped1 = VaultUtils.WrapTextArray(TextSuspendIntroDesc1.Value, font, descWrapW, 99, out _);
            foreach (string wl in wrapped1) {
                if (string.IsNullOrEmpty(wl)) continue;
                Utils.DrawBorderString(sb, wl.TrimEnd('-', ' '), new Vector2(px, py),
                    new Color(120, 155, 120, (int)(200 * alpha)), subScale);
                py += lineH_s;
            }
            string[] wrapped2 = VaultUtils.WrapTextArray(TextSuspendIntroDesc2.Value, font, descWrapW, 99, out _);
            foreach (string wl in wrapped2) {
                if (string.IsNullOrEmpty(wl)) continue;
                Utils.DrawBorderString(sb, wl.TrimEnd('-', ' '), new Vector2(px, py),
                    new Color(120, 155, 120, (int)(200 * alpha)), subScale);
                py += lineH_s;
            }

            if (DrawConfirmButton(sb, card, alpha))
                MarkGuideSeen();
        }

        //着色器背景与降级

        private static void DrawCardBackground(SpriteBatch sb, Rectangle card, float variant, float alpha) {
            Effect effect = EffectLoader.EntrustGuideCard?.Value;
            if (effect != null) {
                Rectangle ext = card;
                ext.Inflate(EdgePad, EdgePad);

                effect.Parameters["uTime"]?.SetValue(shaderTimer);
                effect.Parameters["uAlpha"]?.SetValue(alpha * 0.96f);
                effect.Parameters["uResolution"]?.SetValue(new Vector2(ext.Width, ext.Height));
                effect.Parameters["uEdgePad"]?.SetValue((float)EdgePad);
                effect.Parameters["uVariant"]?.SetValue(variant);

                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.AnisotropicClamp, DepthStencilState.None,
                    RasterizerState.CullNone, effect, Main.UIScaleMatrix);
                sb.Draw(VaultAsset.placeholder2.Value, ext, Color.White);
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    SamplerState.AnisotropicClamp, DepthStencilState.None,
                    RasterizerState.CullNone, null, Main.UIScaleMatrix);
            }
            else {
                //降级纯色背景
                BaseManagerStyle.FillRect(sb, card, new Color(0, 0, 0, (int)(200 * alpha)));
                BaseManagerStyle.StrokeRect(sb, card, 1, new Color(160, 160, 160, (int)(120 * alpha)));
            }
        }

        //辅助 UI

        private static bool DrawConfirmButton(SpriteBatch sb, Rectangle card, float alpha, string text = null) {
            const int btnW = 78, btnH = 20, margin = 8;
            var rect = new Rectangle(card.Right - btnW - margin, card.Bottom - btnH - margin, btnW, btnH);

            //按钮上方分隔线
            int sepY = rect.Y - 6;
            BaseManagerStyle.FillRect(sb,
                new Rectangle(card.X + 12, sepY, card.Width - 24, 1),
                new Color(120, 120, 120, (int)(80 * alpha)));

            bool hovered = rect.Contains(Main.mouseX, Main.mouseY);
            BaseManagerStyle.FillRect(sb, rect, new Color(22, 58, 22, (int)((hovered ? 215 : 140) * alpha)));
            BaseManagerStyle.StrokeRect(sb, rect, 1, new Color(90, 185, 90, (int)(145 * alpha)));

            //自动推进进度条
            if (autoAdvanceDelay > 0 && autoAdvanceDelayTotal > 0) {
                float progress = 1f - autoAdvanceDelay / (float)autoAdvanceDelayTotal;
                int barW = (int)(rect.Width * MathHelper.Clamp(progress, 0f, 1f));
                BaseManagerStyle.FillRect(sb,
                    new Rectangle(rect.X, rect.Bottom - 2, barW, 2),
                    new Color(180, 255, 180, (int)(220 * alpha)));
            }

            string buttonText = text ?? TextConfirmBtn.Value;
            var textColor = new Color(175, 240, 175, (int)(255 * alpha));
            Vector2 ts = FontAssets.MouseText.Value.MeasureString(buttonText) * 0.62f;
            Utils.DrawBorderString(sb, buttonText,
                new Vector2(rect.X + (rect.Width - ts.X) * 0.5f, rect.Y + (rect.Height - ts.Y) * 0.5f),
                textColor, 0.62f);
            if (hovered) Main.LocalPlayer.mouseInterface = true;
            return hovered && Main.mouseLeft && !Main.mouseLeftRelease;
        }

        private static void DrawLeftArrow(SpriteBatch sb, Vector2 tip, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            var color = new Color(100, 200, 225, (int)(160 * alpha));
            for (int i = 0; i < 7; i++) {
                int halfH = 7 - i;
                sb.Draw(px, new Rectangle((int)tip.X + i, (int)tip.Y - halfH, 1, halfH * 2), color);
            }
        }
    }
}


