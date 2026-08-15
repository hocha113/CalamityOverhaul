using CalamityOverhaul.Content.Cyberwares.UIs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.UI;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Guides;
using InnoVault.Narrative.Runtime;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.Scenarios.Shepel.CybCourses
{
    //SHPC超梦教程，IGuideLead登记
    //GuidePriority=0子世界内独占引导队列
    internal class CybTutorialLead : ModSystem, ILocalizedModType, IGuideLead
    {
        private enum Phase { Inactive, Running, FadeOut, Done }

        public string LocalizationCategory => "ADV.Shepel";

        private static readonly (string TargetKey, bool IsAuto)[] StepMeta =
        {
            (null,              false),
            ("SHPC.Core",       false),
            ("SHPC.Sector.0",   false),
            ("SHPC.Sector.1",   false),
            ("SHPC.Sector.2",   false),
            ("SHPC.Sector.3",   false),
            (null,              true),
        };

        private static LocalizedText[] _stepTitles;
        private static LocalizedText[] _stepBodies;
        private static LocalizedText _textCalibrating;
        private static LocalizedText _textNextBtn;

        public override void SetStaticDefaults() {
            GuideLeadQueue.Register(this);
            _stepTitles = new[] {
                this.GetLocalization("Step0_Title", () => "连接 SHPC"),
                this.GetLocalization("Step1_Title", () => "核心节点"),
                this.GetLocalization("Step2_Title", () => "赛博空间"),
                this.GetLocalization("Step3_Title", () => "模块改装"),
                this.GetLocalization("Step4_Title", () => "义体植入"),
                this.GetLocalization("Step5_Title", () => "神经链路"),
                this.GetLocalization("Step6_Title", () => "校准完成"),
            };
            _stepBodies = new[] {
                this.GetLocalization("Step0_Body", () => "将SHPC装备至武器栏并持握，HUD核心节点即会出现在屏幕左下角"),
                this.GetLocalization("Step1_Body", () => "点击左下角的核心节点可展开或收起操作面板"),
                this.GetLocalization("Step2_Body", () => "部署并管理多层赛博空间层叠结构\n点击高亮的扇区即可打开该面板"),
                this.GetLocalization("Step3_Body", () => "为SHPC安装或拆卸改造零件\n点击高亮的扇区即可打开该面板"),
                this.GetLocalization("Step4_Body", () => "查看并管理你的身体增强模块\n点击高亮的扇区即可打开该界面"),
                this.GetLocalization("Step5_Body", () => "与SHPC建立直连通讯，开启对话\n点击高亮的扇区即可与其对话"),
                this.GetLocalization("Step6_Body", () => "所有接口已解析完毕\n神经链路稳定，SHPC已就绪"),
            };
            _textCalibrating = this.GetLocalization("Calibrating", () => "校准中…");
            _textNextBtn = this.GetLocalization("NextBtn", () => "跳过 >");
            _textHintStuck = this.GetLocalization("HintStuck", () => "提示：点一下高亮的目标区域");
        }

        private const int CardW = CybCourseCardStyle.CardW;
        private const int CardH = CybCourseCardStyle.CardH;
        private const float AutoStepDuration = 1.6f;
        private const float StuckHintAfter = 12f;

        public static bool IsDone => _phase == Phase.Done;
        //IsTailing供HackTime衔接
        public static bool IsTailing => _phase == Phase.FadeOut || _phase == Phase.Done;

        #region 引导排队
        int IGuideLead.GuidePriority => 0;//GuidePriority=0独占子世界引导队列
        bool IGuideLead.GuideReserving => CybCourseWorld.Active;
        bool IGuideLead.GuideReady => CybCourseWorld.Active;
        void IGuideLead.OnGuideAbandoned() { }
        #endregion

        private static Phase _phase = Phase.Inactive;
        private static int _currentStep = 0;
        private static float _cardAnim = 0f;
        private static float _shaderTimer = 0f;
        private static float _highlightPulse = 0f;
        private static float _stepTimer = 0f;
        private static float _stuckTimer = 0f;
        private static bool _introAttempted = false;
        private static bool _prevMouseLeft = false;
        private static Rectangle _nextBtnRect = Rectangle.Empty;
        private static Rectangle _cardRect = Rectangle.Empty;
        private static int _lastPinned = -1;
        //卡片Y平滑，避面板展开跳
        private static float _smoothCardY = 0f;
        private static LocalizedText _textHintStuck;

        public override void OnWorldUnload() => ResetForRetry();

        public static void ResetForRetry() {
            _phase = Phase.Inactive;
            _currentStep = 0;
            _cardAnim = 0f;
            _shaderTimer = 0f;
            _highlightPulse = 0f;
            _stepTimer = 0f;
            _stuckTimer = 0f;
            _introAttempted = false;
            _prevMouseLeft = false;
            _nextBtnRect = Rectangle.Empty;
            _cardRect = Rectangle.Empty;
            _lastPinned = -1;
            _smoothCardY = 0f;
        }

        public static void BeginSHPCTutorial() {
            _phase = Phase.Running;
            _currentStep = 0;
            _cardAnim = 0f;
            _stepTimer = 0f;
            _stuckTimer = 0f;
            _lastPinned = -1;
            _smoothCardY = SHPCHUDTargets.CorePos.Y - CardH + 8;
        }

        public override void UpdateUI(GameTime gameTime) {
            if (Main.dedServ || Main.gameMenu) return;
            if (!CybCourseWorld.Active) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _shaderTimer += dt * 0.8f;
            if (_shaderTimer > 100f) _shaderTimer -= 100f;

            AutoTriggerIntro();

            //卡片Y平滑
            float targetCardY = ComputeTargetCardY();
            _smoothCardY = MathHelper.Lerp(_smoothCardY, targetCardY, 0.15f);

            bool mouseDown = Main.mouseLeft;
            bool mouseClicked = mouseDown && !_prevMouseLeft;
            _prevMouseLeft = mouseDown;

            if (_cardRect != Rectangle.Empty && _cardRect.Contains(Main.mouseX, Main.mouseY)) {
                Main.LocalPlayer.mouseInterface = true;
            }

            switch (_phase) {
                case Phase.Running:
                    _highlightPulse += dt;
                    _cardAnim = MathHelper.Lerp(_cardAnim, 1f, 0.16f);

                    _stepTimer += dt;
                    bool isAuto = StepMeta[_currentStep].IsAuto;
                    if (isAuto) {
                        if (CheckAutoAdvance() || mouseClicked) {
                            AdvanceStep();
                        }
                        _stuckTimer = 0f;
                    }
                    else {
                        //step0持握
                        if (_currentStep == 0 && SHPCUI.Instance?.Active == true) {
                            AdvanceStep();
                            break;
                        }
                        //step1展开
                        if (_currentStep == 1 && SHPCUI.Instance?.IsExpanded == true) {
                            AdvanceStep();
                            break;
                        }
                        //step2~5扇区
                        if (_currentStep >= 2 && _currentStep <= 5) {
                            int targetSector = GetTargetSectorForStep(_currentStep);
                            int pinned = SHPCUI.Instance?.PinnedSector ?? -1;
                            bool completed = false;
                            if (targetSector == SHPCUI.CyberDomainSectorIndex
                                || targetSector == SHPCUI.ModifySectorIndex) {
                                completed = pinned == targetSector && _lastPinned != targetSector;
                            }
                            else if (targetSector == SHPCUI.CyberwareSectorIndex) {
                                completed = CyberwareUI.Instance?.Active == true;
                            }
                            else if (targetSector == SHPCUI.TalkSectorIndex) {
                                completed = NarrativeRunner.IsBusy;
                            }
                            if (completed) {
                                _lastPinned = pinned;
                                AdvanceStep();
                                break;
                            }
                            _lastPinned = pinned;
                        }
                        //NEXT兜底
                        if (mouseClicked && _nextBtnRect != Rectangle.Empty
                            && _nextBtnRect.Contains(Main.mouseX, Main.mouseY)) {
                            Main.mouseLeft = false;
                            if (_currentStep == 0 && SHPCUI.Instance?.Active != true)
                                ForceEquipSHPC();
                            if (_currentStep == 1)
                                SHPCUI.Instance?.ForceExpand();
                            AdvanceStep();
                            break;
                        }
                        _stuckTimer += dt;
                    }
                    break;

                case Phase.FadeOut:
                    _cardAnim = MathHelper.Lerp(_cardAnim, 0f, 0.18f);
                    if (_cardAnim < 0.02f) {
                        _cardAnim = 0f;
                        _phase = Phase.Done;
                    }
                    break;
            }
        }

        private static void AutoTriggerIntro() {
            if (_introAttempted) return;
            if (NarrativeRunner.IsBusy) return;
            NarrativeRouter.Begin<CybCourseIntroDialogue>();
            _introAttempted = true;
        }

        private static float ComputeTargetCardY() {
            Vector2 corePos = SHPCHUDTargets.CorePos;
            float defaultY = corePos.Y - CardH + 8;
            var ui = SHPCUI.Instance;
            if (ui == null || !ui.Active) return defaultY;
            int pinned = ui.PinnedSector;
            if (pinned != SHPCUI.CyberDomainSectorIndex && pinned != SHPCUI.ModifySectorIndex)
                return defaultY;
            float panelH = pinned == SHPCUI.ModifySectorIndex ? SHPCModPanel.PanelH : SHPCCyberPanel.PanelH;
            float panelTop = corePos.Y - panelH + 6f;
            return panelTop - CardH - 12f;
        }

        private static bool CheckAutoAdvance() {
            if (_currentStep == StepMeta.Length - 1)
                return _stepTimer >= AutoStepDuration;
            return false;
        }

        private static int GetTargetSectorForStep(int step) => step switch {
            2 => SHPCUI.CyberDomainSectorIndex,
            3 => SHPCUI.ModifySectorIndex,
            4 => SHPCUI.CyberwareSectorIndex,
            5 => SHPCUI.TalkSectorIndex,
            _ => -1,
        };

        /// <summary>把背包里的 SHPC 挑回手上；转盘段也复用</summary>
        internal static void ForceEquipSHPC() {
            Player p = Main.LocalPlayer;
            if (p == null || p.dead) return;
            for (int i = 0; i < 10; i++) {
                if (p.inventory[i].type == SHPCOverride.ID) {
                    p.selectedItem = i;
                    return;
                }
            }
            //只扫0~49，勿触钱币弹药槽50~57
            for (int i = 10; i < 50; i++) {
                if (p.inventory[i].type == SHPCOverride.ID) {
                    var tmp = p.inventory[p.selectedItem];
                    p.inventory[p.selectedItem] = p.inventory[i];
                    p.inventory[i] = tmp;
                    return;
                }
            }
        }

        private static void AdvanceStep() {
            _currentStep++;
            _stepTimer = 0f;
            _stuckTimer = 0f;
            _cardAnim = 0f;
            _lastPinned = SHPCUI.Instance?.PinnedSector ?? -1;
            if (_currentStep >= StepMeta.Length) {
                _phase = Phase.FadeOut;
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            if (_phase != Phase.Running && _phase != Phase.FadeOut) return;
            if (_cardAnim < 0.02f) return;

            int idx = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
            if (idx == -1) return;

            layers.Insert(idx, new LegacyGameInterfaceLayer("CWRMod: CybCourse Tutorial Lead",
                delegate {
                    DrawOverlay(Main.spriteBatch);
                    return true;
                }, InterfaceScaleType.UI));
        }

        private static void DrawOverlay(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null) return;

            float alpha = MathHelper.Clamp(_cardAnim, 0f, 1f);
            int stepIdx = (int)MathHelper.Clamp(_currentStep, 0, StepMeta.Length - 1);
            string targetKey = StepMeta[stepIdx].TargetKey;

            Vector2 corePos = SHPCHUDTargets.CorePos;
            int cx = (int)(corePos.X + SHPCTheme.ButtonOuterR + 18f);
            int cy = (int)corePos.Y - CardH + 8;
            float slideX = (1f - alpha) * 30f;
            int finalX = cx + (int)slideX;
            //屏幕clamp
            finalX = (int)MathHelper.Clamp(finalX, 8, Math.Max(8, Main.screenWidth - CardW - 8));
            int finalY = (int)MathHelper.Clamp(_smoothCardY, 8, Math.Max(8, Main.screenHeight - CardH - 8));
            var card = new Rectangle(finalX, finalY, CardW, CardH);

            _cardRect = card;
            CybCourseCardStyle.DrawCardBg(sb, card, alpha, _shaderTimer);
            DrawCardContent(sb, card, alpha);
            DrawHighlightForStep(sb, px, targetKey, alpha);
        }

        private static void DrawCardContent(SpriteBatch sb, Rectangle card, float alpha) {
            int stepIdx = (int)MathHelper.Clamp(_currentStep, 0, StepMeta.Length - 1);
            string title = _stepTitles[stepIdx].Value;
            string body = _stepBodies[stepIdx].Value;
            bool isAuto = StepMeta[stepIdx].IsAuto;
            bool stuck = !isAuto && _stuckTimer >= StuckHintAfter;

            string counter = $"{stepIdx + 1:D2} / {StepMeta.Length:D2}";
            float y = CybCourseCardStyle.DrawHeader(sb, card, alpha, title, counter);
            CybCourseCardStyle.DrawBodyLines(sb, card, ref y, alpha, body);

            if (stuck && _textHintStuck != null) {
                CybCourseCardStyle.DrawStuckHint(sb, card, alpha, _shaderTimer, _textHintStuck.Value);
            }

            if (!isAuto) {
                _nextBtnRect = CybCourseCardStyle.DrawNextButton(sb, card, alpha, stuck,
                    _shaderTimer, _textNextBtn.Value, new Point(Main.mouseX, Main.mouseY));
            }
            else {
                CybCourseCardStyle.DrawStatusTag(sb, card, alpha, _shaderTimer, _textCalibrating.Value);
            }
        }

        private static void DrawHighlightForStep(SpriteBatch sb, Texture2D px, string targetKey, float alpha) {
            if (string.IsNullOrEmpty(targetKey)) return;
            if (!CybTutorialRegistry.TryGet(targetKey, out var target)) return;

            float pulse = 0.6f + 0.4f * MathF.Sin(_highlightPulse * 3.2f);
            Color hColor = new Color(
                (int)(70 * pulse), (int)(215 * pulse), (int)(245 * pulse),
                (int)(175 * pulse * alpha));
            Color bracketColor = new Color(80, 220, 245, (int)(200 * alpha));

            Vector2 corePos = SHPCHUDTargets.CorePos;

            if (targetKey == "SHPC.Core") {
                SHPCRenderer.DrawArc(sb, px, corePos,
                    SHPCTheme.CoreRingR + 4f, SHPCTheme.CoreRingR + 14f,
                    0f, MathHelper.TwoPi, hColor);
            }
            else if (targetKey.StartsWith("SHPC.Sector.")
                && int.TryParse(targetKey[12..], out int idx)) {
                SHPCHUDTargets.GetSectorAngles(idx, out float a0, out float a1);
                float expand = 3f + 4f * MathF.Sin(_highlightPulse * 3.2f);
                SHPCRenderer.DrawArc(sb, px, corePos,
                    SHPCTheme.ButtonInnerR - 5f,
                    SHPCTheme.ButtonOuterR + 8f + expand,
                    a0, a1, hColor);
            }

            Rectangle rect = target.GetScreenRect();
            CybCourseCardStyle.DrawLBrackets(sb, px, rect, bracketColor, len: 12);
        }
    }
}
