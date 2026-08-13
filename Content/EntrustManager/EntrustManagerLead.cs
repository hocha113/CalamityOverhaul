using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.Narrative.Guides;
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
    internal class EntrustManagerLead : ModSystem, ILocalizedModType, IGuideLead
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
        //阶段4 关注引导
        public static LocalizedText TextTrackPromptTitle { get; private set; }
        public static LocalizedText TextTrackPromptHintLabel { get; private set; }
        public static LocalizedText TextTrackPromptHintAction { get; private set; }
        public static LocalizedText TextTrackPromptDesc { get; private set; }
        public static LocalizedText TextTrackPromptNextBtn { get; private set; }
        //阶段5 追踪栏介绍
        public static LocalizedText TextTrackerIntroTitle { get; private set; }
        public static LocalizedText TextTrackerIntroLine1 { get; private set; }
        public static LocalizedText TextTrackerIntroLine2 { get; private set; }
        public static LocalizedText TextTrackerIntroLine3 { get; private set; }
        public static LocalizedText TextTrackerIntroNextBtn { get; private set; }
        //阶段6 挂起说明
        public static LocalizedText TextSuspendIntroTitle { get; private set; }
        public static LocalizedText TextSuspendIntroHintLabel { get; private set; }
        public static LocalizedText TextSuspendIntroHintAction { get; private set; }
        public static LocalizedText TextSuspendIntroDesc1 { get; private set; }
        public static LocalizedText TextSuspendIntroDesc2 { get; private set; }
        public static LocalizedText TextConfirmBtn { get; private set; }

        public override void SetStaticDefaults() {
            GuideLeadQueue.Register(this);
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
            TextStyleButtonLabel = this.GetLocalization(nameof(TextStyleButtonLabel), () => "左键单击高亮的小按钮");
            TextStyleButtonAction = this.GetLocalization(nameof(TextStyleButtonAction), () => " →  切换界面样式");
            TextStyleButtonDesc = this.GetLocalization(nameof(TextStyleButtonDesc), () => "     可以在几套界面风格之间循环切换");
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

        private static int phaseTickTimer;
        private static int trackedSnapshot;
        private static int suspendedSnapshot;
        //>0 操作或兜底后倒计推进
        private static int autoAdvanceDelay;
        private static int autoAdvanceDelayTotal;

        //兜底超时 60帧≈1s
        private const int Phase4SoftTimeout = 60 * 30;
        private const int Phase5SoftTimeout = 60 * 35;
        private const int Phase6SoftTimeout = 60 * 25;
        private const int AutoActionConfirmDelay = 36;
        private const int AutoFallbackAdvanceDelay = 60;

        private const float AnimSpeed = 0.12f;
        private const int EdgePad = 8;

        //按换行测高，防英文溢出
        private const int CardTopPad = 11;
        private const int CardPadX = 14;
        private const int CardFooter = 38;

        //各阶段卡宽，高动态
        private const int CardW1 = 320;
        private const int CardW2 = 318;
        private const int CardW3 = 316;

        /// <summary>
        /// 委托已内嵌进全屏任务书，PanelRightEdge 是整块内容区的右缘，
        /// 直接 +15 会把卡片顶出屏幕——统一夹回屏内，卡片浮在书页右侧
        /// </summary>
        private static float ClampCardX(float desiredX, int cardW)
            => MathF.Min(desiredX, Main.screenWidth - cardW - 20f);

        public override void OnWorldUnload() {
            currentPhase = LeadPhase.Inactive;
            animProgress = 0f;
            ResetPhaseGuards();
        }

        #region 引导排队协议
        int IGuideLead.GuidePriority => 20;//晚于比目鱼界面引导
        bool IGuideLead.GuideReserving => Reserving;
        bool IGuideLead.GuideReady => Ready;
        void IGuideLead.OnGuideAbandoned() => MarkGuideSeen();

        //有委托且未看过引导则占位
        private static bool Reserving {
            get {
                Player p = Main.LocalPlayer;
                if (p == null || !p.active) {
                    return false;
                }
                var ui = QuestManagerUI.Instance;
                if (ui == null || !ui.HasAnyEntry) {
                    return false;
                }
                return !p.GetModPlayer<StoryPlayer>().Get<EntrustGuideData>().GuideSeen;
            }
        }

        //就绪=占位且无对话/过场
        private static bool Ready {
            get {
                if (!Reserving) {
                    return false;
                }
                return !NarrativeTriggerGate.IsBusy && !InnoVault.Cinematics.CutsceneDirector.IsPlaying;
            }
        }
        #endregion

        private static void ResetPhaseGuards() {
            phaseTickTimer = 0;
            autoAdvanceDelay = 0;
            autoAdvanceDelayTotal = 0;
            var ui = QuestManagerUI.Instance;
            trackedSnapshot = ui?.CountByStatus(QuestEntryStatus.Tracked) ?? 0;
            suspendedSnapshot = ui?.CountByStatus(QuestEntryStatus.Suspended) ?? 0;
        }

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

            //未轮到则待命，异常残留则收起
            if (!GuideLeadQueue.IsHolder(this)) {
                if (currentPhase != LeadPhase.Inactive && currentPhase != LeadPhase.Complete) {
                    currentPhase = LeadPhase.Inactive;
                    animProgress = 0f;
                    ResetPhaseGuards();
                }
                return;
            }

            //轮到本引导则起步
            if (currentPhase == LeadPhase.Inactive) {
                currentPhase = LeadPhase.KeyPrompt;
                animProgress = 0f;
                ResetPhaseGuards();
            }

            switch (currentPhase) {
                case LeadPhase.Inactive:
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


        //阶段4 关注或超时自动关注
        private static void UpdateTrackPhaseGuard(QuestManagerUI ui) {
            phaseTickTimer++;

            if (autoAdvanceDelay > 0) {
                autoAdvanceDelay--;
                if (autoAdvanceDelay == 0)
                    StartTrackerWidgetIntro();
                return;
            }

            int trackedNow = ui.CountByStatus(QuestEntryStatus.Tracked);
            if (trackedNow > trackedSnapshot) {
                StartAutoAdvance(AutoActionConfirmDelay);
                return;
            }

            if (phaseTickTimer > Phase4SoftTimeout) {
                string key = ui.TryGetFirstTrackableKey();
                if (key != null && ui.SetEntryStatus(key, QuestEntryStatus.Tracked)) {
                    StartAutoAdvance(AutoFallbackAdvanceDelay);
                }
                else {
                    StartTrackerWidgetIntro();
                }
            }
        }

        //阶段5 久无操作→阶段6
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

        //阶段6 挂起或超时收尾
        private static void UpdateSuspendPhaseGuard(QuestManagerUI ui) {
            phaseTickTimer++;

            if (autoAdvanceDelay > 0) {
                autoAdvanceDelay--;
                if (autoAdvanceDelay == 0)
                    MarkGuideSeen();
                return;
            }

            int suspendedNow = ui.CountByStatus(QuestEntryStatus.Suspended);
            if (suspendedNow > suspendedSnapshot) {
                autoAdvanceDelay = AutoActionConfirmDelay;
                return;
            }

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


        private static void DrawKeyPromptCard(SpriteBatch sb) {
            string boundKey = GetBoundKeyName();
            bool hasBind = boundKey != null;
            string displayKey = hasBind ? boundKey : "K";
            float alpha = animProgress;
            var font = FontAssets.MouseText.Value;
            float contentW = CardW1 - CardPadX * 2;

            var lines = new List<CL>();
            if (hasBind) {
                lines.Add(CL.Wrap(TextKeyPromptBound.Format(displayKey), 0.92f, new Color(255, 255, 230, 255)));
            }
            else {
                //警告标题可换行，保留闪烁
                lines.Add(CL.Wrap(TextKeyPromptWarnTitle.Value, 0.86f, new Color(255, 175, 25, 255), blink: true));
                lines.Add(CL.Gap(2f));
                lines.Add(CL.Wrap(TextKeyPromptDefaultKey.Format(displayKey), 0.83f, new Color(235, 225, 200, 245)));
                lines.Add(CL.Gap(1f));
                lines.Add(CL.Wrap(TextKeyPromptBindHint.Value, 0.73f, new Color(165, 155, 115, 195)));
            }

            int cardH = CardTopPad + MeasureCardBody(lines, font, contentW) + CardFooter;
            float slideY = (1f - animProgress) * 65f;
            float x = 20f;
            float y = Main.screenHeight - cardH - 20f + slideY;
            var card = new Rectangle((int)x, (int)y, CardW1, cardH);

            DrawCardBackground(sb, card, 0f, alpha);
            DrawCardBody(sb, lines, font, card.X + CardPadX, card.Y + CardTopPad, contentW, alpha);

            if (DrawConfirmButton(sb, card, alpha, TextKeyPromptConfirmBtn.Value))
                AdvanceFromKeyPrompt();
        }


        private static void DrawPanelIntroCard(SpriteBatch sb) {
            var ui = QuestManagerUI.Instance;
            if (ui == null) return;
            float alpha = animProgress;
            var font = FontAssets.MouseText.Value;
            float contentW = CardW2 - CardPadX * 2;

            var lines = new List<CL> {
                CL.Title(TextPanelIntroTitle.Value, 0.84f, new Color(230, 225, 100, 255)),
                CL.Divider(new Color(130, 125, 70, 130)),
                CL.KeyAction(TextRightClickLabel.Value, new Color(95, 210, 255, 240),
                    TextRightClickAction.Value, new Color(200, 240, 255, 240), 0.78f),
                CL.Wrap(TextRightClickDesc.Value, 0.72f, new Color(130, 165, 175, 200)),
                CL.Gap(6f),
                CL.KeyAction(TextMiddleClickLabel.Value, new Color(130, 220, 145, 240),
                    TextMiddleClickAction.Value, new Color(195, 240, 195, 240), 0.78f),
                CL.Wrap(TextMiddleClickDesc.Value, 0.72f, new Color(120, 155, 120, 200)),
            };

            int cardH = CardTopPad + MeasureCardBody(lines, font, contentW) + CardFooter;
            float slideX = (1f - animProgress) * 80f;
            float x = ClampCardX(ui.PanelRightEdge + 15f, CardW2) - slideX;
            float y = (Main.screenHeight - cardH) * 0.5f;
            var card = new Rectangle((int)x, (int)y, CardW2, cardH);

            DrawCardBackground(sb, card, 1f, alpha);
            DrawLeftArrow(sb, new Vector2(x - 8f, y + cardH * 0.5f), alpha);
            DrawCardBody(sb, lines, font, card.X + CardPadX, card.Y + CardTopPad, contentW, alpha);

            if (DrawConfirmButton(sb, card, alpha))
                StartStyleButtonPrompt();
        }


        private static void StartStyleButtonPrompt() {
            currentPhase = LeadPhase.StyleButtonPrompt;
            animProgress = 0f;
            ResetPhaseGuards();
        }

        /// <summary>样式切换按钮现由任务书持有，问书要真实命中区而不是按旧面板推算</summary>
        private static Rectangle GetStyleSwitchGuideRect() {
            var book = QuestLogs.QuestLog.Instance;
            if (book == null || !book.IsOpen) {
                return Rectangle.Empty;
            }
            return book.CurrentStyle.GetStyleSwitchButtonRect(book.CurrentLayout.LegacyChrome);
        }

        private static void DrawStyleButtonPromptCard(SpriteBatch sb) {
            var ui = QuestManagerUI.Instance;
            if (ui == null) return;

            Rectangle styleRect = GetStyleSwitchGuideRect();
            if (styleRect.Width <= 0) {
                //按钮定位不到就别对着空气讲解，直接进下一阶段
                StartTrackPrompt();
                return;
            }
            float alpha = animProgress;
            DrawStyleButtonHighlight(sb, styleRect, alpha);

            var font = FontAssets.MouseText.Value;
            float contentW = CardW3 - CardPadX * 2;
            var lines = new List<CL> {
                CL.Title(TextStyleButtonTitle.Value, 0.84f, new Color(230, 225, 100, 255)),
                CL.Divider(new Color(130, 125, 70, 130)),
                CL.KeyAction(TextStyleButtonLabel.Value, new Color(245, 190, 95, 240),
                    TextStyleButtonAction.Value, new Color(255, 230, 170, 240), 0.78f),
                CL.Wrap(TextStyleButtonDesc.Value, 0.72f, new Color(175, 150, 105, 205)),
            };
            int cardH = CardTopPad + MeasureCardBody(lines, font, contentW) + CardFooter;

            float slideX = (1f - animProgress) * 70f;
            float x = MathHelper.Clamp(styleRect.Right + 16f + slideX, 20f, Main.screenWidth - CardW3 - 20f);
            float y = MathHelper.Clamp(styleRect.Y - 8f, 20f, Main.screenHeight - cardH - 20f);
            var card = new Rectangle((int)x, (int)y, CardW3, cardH);

            DrawCardBackground(sb, card, 0.5f, alpha);
            DrawLeftArrow(sb, new Vector2(x - 8f, y + 28f), alpha);
            DrawCardBody(sb, lines, font, card.X + CardPadX, card.Y + CardTopPad, contentW, alpha);

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


        private const int CardW4 = 318;

        private static void StartTrackPrompt() {
            currentPhase = LeadPhase.TrackPromptInPanel;
            animProgress = 0f;
            ResetPhaseGuards();
        }

        private static void DrawTrackPromptCard(SpriteBatch sb) {
            var ui = QuestManagerUI.Instance;
            if (ui == null) return;
            float alpha = animProgress;
            var font = FontAssets.MouseText.Value;
            float contentW = CardW4 - CardPadX * 2;

            var lines = new List<CL> {
                CL.Title(TextTrackPromptTitle.Value, 0.84f, new Color(230, 225, 100, 255)),
                CL.Divider(new Color(130, 125, 70, 130)),
                CL.KeyAction(TextTrackPromptHintLabel.Value, new Color(95, 210, 255, 240),
                    TextTrackPromptHintAction.Value, new Color(200, 240, 255, 240), 0.78f),
                CL.Wrap(TextTrackPromptDesc.Value, 0.72f, new Color(135, 170, 180, 205)),
            };

            int cardH = CardTopPad + MeasureCardBody(lines, font, contentW) + CardFooter;
            float slideX = (1f - animProgress) * 80f;
            float x = ClampCardX(ui.PanelRightEdge + 15f, CardW4) - slideX;
            float y = (Main.screenHeight - cardH) * 0.5f;
            var card = new Rectangle((int)x, (int)y, CardW4, cardH);

            DrawCardBackground(sb, card, 1.5f, alpha);
            DrawLeftArrow(sb, new Vector2(x - 8f, y + cardH * 0.5f), alpha);
            DrawCardBody(sb, lines, font, card.X + CardPadX, card.Y + CardTopPad, contentW, alpha);

            if (DrawConfirmButton(sb, card, alpha, TextTrackPromptNextBtn.Value))
                StartTrackerWidgetIntro();
        }


        private const int CardW5 = 312;

        private static void StartTrackerWidgetIntro() {
            var ui = QuestManagerUI.Instance;
            if (ui != null && ui.IsOpen) ui.TogglePanel();
            currentPhase = LeadPhase.TrackerWidgetIntro;
            animProgress = 0f;
            ResetPhaseGuards();
        }

        private static void DrawTrackerIntroCard(SpriteBatch sb) {
            var widget = EntrustTrackerWidget.Instance;

            //追踪栏外接矩形，否则左侧预估
            Rectangle trackerRect;
            if (widget != null && widget.GetTrackerBounds() is { Width: > 0 } bounds) {
                trackerRect = bounds;
            }
            else {
                trackerRect = new Rectangle(8, (int)(Main.screenHeight * 0.35f), 220, 100);
            }

            DrawTrackerHighlight(sb, trackerRect, animProgress);

            float alpha = animProgress;
            var font = FontAssets.MouseText.Value;
            float contentW = CardW5 - CardPadX * 2;
            var lines = new List<CL> {
                CL.Title(TextTrackerIntroTitle.Value, 0.84f, new Color(255, 200, 110, 255)),
                CL.Divider(new Color(160, 130, 70, 140)),
                CL.Bullet(TextTrackerIntroLine1.Value, 0.76f, new Color(225, 235, 245, 235), new Color(255, 200, 120, 240)),
                CL.Bullet(TextTrackerIntroLine2.Value, 0.76f, new Color(190, 210, 230, 220), new Color(255, 200, 120, 240)),
                CL.Bullet(TextTrackerIntroLine3.Value, 0.76f, new Color(170, 195, 215, 210), new Color(255, 200, 120, 240)),
            };
            int cardH = CardTopPad + MeasureCardBody(lines, font, contentW) + CardFooter;

            float slideX = (1f - animProgress) * 70f;
            float x = MathHelper.Clamp(trackerRect.Right + 18f + slideX, 20f, Main.screenWidth - CardW5 - 20f);
            float y = MathHelper.Clamp(trackerRect.Y - 4f, 20f, Main.screenHeight - cardH - 20f);
            var card = new Rectangle((int)x, (int)y, CardW5, cardH);

            DrawCardBackground(sb, card, 0.25f, alpha);
            DrawLeftArrow(sb, new Vector2(x - 8f, MathHelper.Clamp(trackerRect.Y + trackerRect.Height * 0.5f,
                y + 14f, y + cardH - 14f)), alpha);
            DrawCardBody(sb, lines, font, card.X + CardPadX, card.Y + CardTopPad, contentW, alpha);

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


        private const int CardW6 = 318;

        private static void StartSuspendIntro() {
            var ui = QuestManagerUI.Instance;
            if (ui != null && !ui.IsOpen) ui.TogglePanel();
            currentPhase = LeadPhase.SuspendInfoInPanel;
            animProgress = 0f;
            ResetPhaseGuards();
        }

        private static void DrawSuspendIntroCard(SpriteBatch sb) {
            var ui = QuestManagerUI.Instance;
            if (ui == null) return;
            float alpha = animProgress;
            var font = FontAssets.MouseText.Value;
            float contentW = CardW6 - CardPadX * 2;

            var lines = new List<CL> {
                CL.Title(TextSuspendIntroTitle.Value, 0.84f, new Color(180, 235, 165, 255)),
                CL.Divider(new Color(110, 150, 100, 140)),
                CL.KeyAction(TextSuspendIntroHintLabel.Value, new Color(130, 220, 145, 240),
                    TextSuspendIntroHintAction.Value, new Color(195, 240, 195, 240), 0.78f),
                CL.Wrap(TextSuspendIntroDesc1.Value, 0.72f, new Color(120, 155, 120, 200)),
                CL.Wrap(TextSuspendIntroDesc2.Value, 0.72f, new Color(120, 155, 120, 200)),
            };

            int cardH = CardTopPad + MeasureCardBody(lines, font, contentW) + CardFooter;
            float slideX = (1f - animProgress) * 80f;
            float x = ClampCardX(ui.PanelRightEdge + 15f, CardW6) - slideX;
            float y = (Main.screenHeight - cardH) * 0.5f;
            var card = new Rectangle((int)x, (int)y, CardW6, cardH);

            DrawCardBackground(sb, card, 1f, alpha);
            DrawLeftArrow(sb, new Vector2(x - 8f, y + cardH * 0.5f), alpha);
            DrawCardBody(sb, lines, font, card.X + CardPadX, card.Y + CardTopPad, contentW, alpha);

            if (DrawConfirmButton(sb, card, alpha))
                MarkGuideSeen();
        }

        //测高与绘制共用行表

        private enum CLKind { Title, Divider, KeyAction, Wrap, Bullet, Gap }

        //行色.A=峰值，再乘渐显
        private readonly struct CL
        {
            public readonly CLKind Kind;
            public readonly string A;
            public readonly string B;
            public readonly float Scale;
            public readonly Color C1;
            public readonly Color C2;
            public readonly float GapPx;
            public readonly bool Blink;

            private CL(CLKind kind, string a, string b, float scale, Color c1, Color c2, float gap, bool blink) {
                Kind = kind; A = a; B = b; Scale = scale; C1 = c1; C2 = c2; GapPx = gap; Blink = blink;
            }

            public static CL Title(string t, float sc, Color c, bool blink = false) => new(CLKind.Title, t, null, sc, c, default, 0f, blink);
            public static CL Divider(Color c) => new(CLKind.Divider, null, null, 0f, c, default, 0f, false);
            public static CL KeyAction(string label, Color labelColor, string action, Color actionColor, float sc)
                => new(CLKind.KeyAction, label, action, sc, labelColor, actionColor, 0f, false);
            public static CL Wrap(string t, float sc, Color c, bool blink = false) => new(CLKind.Wrap, t, null, sc, c, default, 0f, blink);
            public static CL Bullet(string t, float sc, Color textColor, Color bulletColor) => new(CLKind.Bullet, t, null, sc, textColor, bulletColor, 0f, false);
            public static CL Gap(float px) => new(CLKind.Gap, null, null, 0f, default, default, px, false);
        }

        private static Color Fade(Color c, float a) => new(c.R, c.G, c.B, (int)(c.A * a));

        private static Color ApplyBlink(Color c) {
            float blink = 0.84f + MathF.Sin(shaderTimer * 52f) * 0.16f;
            return new Color((int)(c.R * blink), (int)(c.G * blink), (int)(c.B * blink), c.A);
        }

        private static int CountWrapLines(string text, ReLogic.Graphics.DynamicSpriteFont font, float scale, float widthPx) {
            if (string.IsNullOrEmpty(text)) return 0;
            int wrapW = Math.Max(8, (int)(widthPx / scale));
            string[] arr = VaultUtils.WrapTextArray(text, font, wrapW, 99, out _);
            int n = 0;
            foreach (string s in arr) {
                if (!string.IsNullOrEmpty(s)) n++;
            }
            return Math.Max(n, 1);
        }

        private static float LineHeight(in CL l, ReLogic.Graphics.DynamicSpriteFont font, float contentW) {
            float la = font.MeasureString("A").Y;
            switch (l.Kind) {
                case CLKind.Title:
                    return la * l.Scale + 4f;
                case CLKind.Divider:
                    return 7f;
                case CLKind.Gap:
                    return l.GapPx;
                case CLKind.KeyAction: {
                    float lh = la * l.Scale + 2f;
                    float w = font.MeasureString(l.A).X * l.Scale
                        + (string.IsNullOrEmpty(l.B) ? 0f : font.MeasureString(l.B).X * l.Scale);
                    //标签+动作放不下则折两行
                    return w <= contentW ? lh : lh * 2f;
                }
                case CLKind.Wrap:
                    return string.IsNullOrEmpty(l.A) ? 0f : CountWrapLines(l.A, font, l.Scale, contentW) * (la * l.Scale + 2f);
                case CLKind.Bullet: {
                    float bulletW = font.MeasureString("·").X * l.Scale + 4f;
                    return CountWrapLines(l.A, font, l.Scale, contentW - bulletW) * (la * l.Scale + 2f);
                }
            }
            return 0f;
        }

        private static int MeasureCardBody(List<CL> lines, ReLogic.Graphics.DynamicSpriteFont font, float contentW) {
            float h = 0f;
            foreach (CL l in lines) {
                h += LineHeight(l, font, contentW);
            }
            return (int)MathF.Ceiling(h);
        }

        private static void DrawCardBody(SpriteBatch sb, List<CL> lines, ReLogic.Graphics.DynamicSpriteFont font,
            float x, float y, float contentW, float alpha) {
            float la = font.MeasureString("A").Y;
            foreach (CL l in lines) {
                switch (l.Kind) {
                    case CLKind.Title: {
                        Color c = l.Blink ? ApplyBlink(l.C1) : l.C1;
                        Utils.DrawBorderString(sb, l.A, new Vector2(x, y), Fade(c, alpha), l.Scale);
                        break;
                    }
                    case CLKind.Divider:
                        BaseManagerStyle.FillRect(sb, new Rectangle((int)x, (int)y, (int)contentW, 1), Fade(l.C1, alpha));
                        break;
                    case CLKind.KeyAction: {
                        float lh = la * l.Scale + 2f;
                        float labelW = font.MeasureString(l.A).X * l.Scale;
                        float actionW = string.IsNullOrEmpty(l.B) ? 0f : font.MeasureString(l.B).X * l.Scale;
                        Utils.DrawBorderString(sb, l.A, new Vector2(x, y), Fade(l.C1, alpha), l.Scale);
                        if (!string.IsNullOrEmpty(l.B)) {
                            if (labelW + actionW <= contentW) {
                                Utils.DrawBorderString(sb, l.B, new Vector2(x + labelW, y), Fade(l.C2, alpha), l.Scale);
                            }
                            else {
                                Utils.DrawBorderString(sb, l.B, new Vector2(x, y + lh), Fade(l.C2, alpha), l.Scale);
                            }
                        }
                        break;
                    }
                    case CLKind.Wrap: {
                        if (!string.IsNullOrEmpty(l.A)) {
                            Color wc = l.Blink ? ApplyBlink(l.C1) : l.C1;
                            int wrapW = Math.Max(8, (int)(contentW / l.Scale));
                            float lh = la * l.Scale + 2f, yy = y;
                            foreach (string s in VaultUtils.WrapTextArray(l.A, font, wrapW, 99, out _)) {
                                if (string.IsNullOrEmpty(s)) continue;
                                Utils.DrawBorderString(sb, s.TrimEnd('-', ' '), new Vector2(x, yy), Fade(wc, alpha), l.Scale);
                                yy += lh;
                            }
                        }
                        break;
                    }
                    case CLKind.Bullet: {
                        float bulletW = font.MeasureString("·").X * l.Scale + 4f;
                        Utils.DrawBorderString(sb, "·", new Vector2(x, y), Fade(l.C2, alpha), l.Scale);
                        int wrapW = Math.Max(8, (int)((contentW - bulletW) / l.Scale));
                        float lh = la * l.Scale + 2f, yy = y;
                        foreach (string s in VaultUtils.WrapTextArray(l.A, font, wrapW, 99, out _)) {
                            if (string.IsNullOrEmpty(s)) continue;
                            Utils.DrawBorderString(sb, s.TrimEnd('-', ' '), new Vector2(x + bulletW, yy), Fade(l.C1, alpha), l.Scale);
                            yy += lh;
                        }
                        break;
                    }
                }
                y += LineHeight(l, font, contentW);
            }
        }


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
                BaseManagerStyle.FillRect(sb, card, new Color(0, 0, 0, (int)(200 * alpha)));
                BaseManagerStyle.StrokeRect(sb, card, 1, new Color(160, 160, 160, (int)(120 * alpha)));
            }
        }


        private static bool DrawConfirmButton(SpriteBatch sb, Rectangle card, float alpha, string text = null) {
            const int btnH = 22, margin = 8;
            const float btnTextScale = 0.68f;
            string buttonText = text ?? TextConfirmBtn.Value;
            Vector2 ts = FontAssets.MouseText.Value.MeasureString(buttonText) * btnTextScale;
            //按钮宽随文字，防英文溢出
            int btnW = Math.Clamp((int)ts.X + 22, 78, card.Width - 24);
            var rect = new Rectangle(card.Right - btnW - margin, card.Bottom - btnH - margin, btnW, btnH);

            int sepY = rect.Y - 6;
            BaseManagerStyle.FillRect(sb,
                new Rectangle(card.X + 12, sepY, card.Width - 24, 1),
                new Color(120, 120, 120, (int)(80 * alpha)));

            bool hovered = rect.Contains(Main.mouseX, Main.mouseY);
            BaseManagerStyle.FillRect(sb, rect, new Color(22, 58, 22, (int)((hovered ? 215 : 140) * alpha)));
            BaseManagerStyle.StrokeRect(sb, rect, 1, new Color(90, 185, 90, (int)(145 * alpha)));

            if (autoAdvanceDelay > 0 && autoAdvanceDelayTotal > 0) {
                float progress = 1f - autoAdvanceDelay / (float)autoAdvanceDelayTotal;
                int barW = (int)(rect.Width * MathHelper.Clamp(progress, 0f, 1f));
                BaseManagerStyle.FillRect(sb,
                    new Rectangle(rect.X, rect.Bottom - 2, barW, 2),
                    new Color(180, 255, 180, (int)(220 * alpha)));
            }

            var textColor = new Color(175, 240, 175, (int)(255 * alpha));
            Utils.DrawBorderString(sb, buttonText,
                new Vector2(rect.X + (rect.Width - ts.X) * 0.5f, rect.Y + (rect.Height - ts.Y) * 0.5f),
                textColor, btnTextScale);
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


