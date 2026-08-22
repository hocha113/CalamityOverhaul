using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.UI;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.UI.DomainWheel;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.UIs.RadialWheels;
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
    //快捷转盘教程，骇客时间下游、收尾上游
    //3步：呼出转盘、选层、中心即骇客入口
    internal class WheelTutorialLead : ModSystem, ILocalizedModType
    {
        private enum Phase { Inactive, Running, FadeOut, Done }

        public string LocalizationCategory => "ADV.Shepel";

        //0呼出转盘 1选层 2中心说明(自动)
        private static readonly bool[] StepIsAuto = { false, false, true };

        private static LocalizedText[] _stepTitles;
        private static LocalizedText[] _stepBodies;
        private static LocalizedText _textCalibrating;
        private static LocalizedText _textNextBtn;
        private static LocalizedText _textHintStuck;
        private static LocalizedText _textKeyHintUnbound;
        //{0}转盘键 {1}骇客键
        private const string WheelKeyToken = "{0}";
        private const string HackKeyToken = "{1}";

        public override void SetStaticDefaults() {
            _stepTitles = new[] {
                this.GetLocalization("RW_S00_Title", () => "呼出快捷转盘"),
                this.GetLocalization("RW_S01_Title", () => "选择领域层级"),
                this.GetLocalization("RW_S02_Title", () => "中心：骇客时间"),
            };
            _stepBodies = new[] {
                this.GetLocalization("RW_S00_Body",
                    () => "按住 {0} 呼出快捷转盘。\n所有够格的盘会一起出现，光标离谁近就归谁。"),
                this.GetLocalization("RW_S01_Body",
                    () => "保持按住，把光标甩向任一层级扇区，\n左键选定或直接松手确认，领域随即展开。"),
                this.GetLocalization("RW_S02_Body",
                    () => "转盘正中央就是骇客时间的快捷入口，\n单击它等同于按下 {1}。全部训练到此完成。"),
            };
            _textCalibrating = this.GetLocalization("RW_Calibrating", () => "同步完成");
            _textNextBtn = this.GetLocalization("RW_NextBtn", () => "跳过 >");
            _textHintStuck = this.GetLocalization("RW_HintStuck", () => "提示：点击 跳过 按钮可强制越过这一步");
            _textKeyHintUnbound = this.GetLocalization("RW_KeyHintUnbound",
                () => "提示：快捷转盘键未绑定，转盘无法呼出；请在 设置 > 控制 中绑定，或点 NEXT 跳过。");
        }

        /// <summary>转盘键显示名，未绑定时给占位</summary>
        public static string GetWheelKeyDisplay() {
            ModKeybind kb = CWRKeySystem.RadialWheel_Key;
            if (kb != null) {
                var keys = kb.GetAssignedKeys();
                if (keys != null && keys.Count > 0)
                    return $"[{keys[0]}]";
            }
            return CWRKeySystem.Notbound.Value;
        }

        public static bool IsWheelKeyBound() {
            ModKeybind kb = CWRKeySystem.RadialWheel_Key;
            if (kb == null) return false;
            var keys = kb.GetAssignedKeys();
            return keys != null && keys.Count > 0;
        }

        /// <summary>{0}转盘键、{1}骇客键，台词与卡片共用</summary>
        public static string ResolveKeyTokens(string raw) {
            if (string.IsNullOrEmpty(raw)) return raw;
            //骇客键未绑定时如实报未绑定，骇客教程那句"N 临时开关"只在它自己运行期有效
            string hackKey = HackTimeTutorialLead.IsHackToggleBound()
                ? HackTimeTutorialLead.GetHackToggleKeyDisplay()
                : CWRKeySystem.Notbound.Value;
            return raw.Replace(WheelKeyToken, GetWheelKeyDisplay())
                .Replace(HackKeyToken, hackKey);
        }

        private const int CardW = CybCourseCardStyle.CardW;
        private const int CardH = CybCourseCardStyle.CardH;
        private const float AutoStepDuration = 1.6f;
        private const float StuckHintAfter = 12f;
        private const float OutroHackTimeFadeThreshold = 0.02f;

        private static Phase _phase = Phase.Inactive;
        private static int _currentStep = 0;
        private static float _cardAnim = 0f;
        private static float _shaderTimer = 0f;
        private static float _highlightPulse = 0f;
        private static float _stepTimer = 0f;
        private static float _stuckTimer = 0f;
        private static bool _introStarted = false;
        private static bool _outroStarted = false;
        private static bool _prevMouseLeft = false;
        private static Rectangle _nextBtnRect = Rectangle.Empty;
        private static Rectangle _cardRect = Rectangle.Empty;

        public override void OnWorldUnload() => ResetForRetry();

        public static void ResetForRetry() {
            _phase = Phase.Inactive;
            _currentStep = 0;
            _cardAnim = 0f;
            _shaderTimer = 0f;
            _highlightPulse = 0f;
            _stepTimer = 0f;
            _stuckTimer = 0f;
            _introStarted = false;
            _outroStarted = false;
            _prevMouseLeft = false;
            _nextBtnRect = Rectangle.Empty;
            _cardRect = Rectangle.Empty;
        }

        /// <summary>
        /// 骇客段收尾后由 <see cref="HackTimeTutorialLead"/> 反复调用；
        /// 骇客滤镜没退干净就等（WheelCanOpen 里也有 !HackTime.Active，这道闸不能省）
        /// </summary>
        public static void TryStartWheelIntro() {
            if (_introStarted) return;
            if (HackTime.Active || HackTime.Intensity > OutroHackTimeFadeThreshold) return;
            if (NarrativeRunner.IsBusy) return;
            if (CybCourseCompletePanel.Visible) return;
            _introStarted = true;
            NarrativeRouter.Begin<CybCourseWheelIntroDialogue>();
        }

        public static void BeginWheelTutorial() {
            _phase = Phase.Running;
            _currentStep = 0;
            _cardAnim = 0f;
            _stepTimer = 0f;
            _stuckTimer = 0f;
            //干净起点：领域若在前面课程里已被打开，先收掉，否则"选层"一步会瞬间自完成
            if (Cyberspace.Active) {
                Cyberspace.Deactivate();
            }
            RadialWheelHub.CloseAll(silent: true);
            //没手持 SHPC 则转盘永远呼不出，进段先拉回
            if (SHPCUI.Instance?.Active != true) {
                CybTutorialLead.ForceEquipSHPC();
            }
        }

        public override void UpdateUI(GameTime gameTime) {
            if (Main.dedServ || Main.gameMenu) return;
            if (!CybCourseWorld.Active) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _shaderTimer += dt * 0.8f;
            if (_shaderTimer > 100f) _shaderTimer -= 100f;

            bool mouseDown = Main.mouseLeft;
            bool mouseClicked = mouseDown && !_prevMouseLeft;
            _prevMouseLeft = mouseDown;

            Vector2 uiMouse = RadialWheelHub.UIMouse;
            if (_cardRect != Rectangle.Empty && _cardRect.Contains((int)uiMouse.X, (int)uiMouse.Y))
                Main.LocalPlayer.mouseInterface = true;

            switch (_phase) {
                case Phase.Running:
                    _highlightPulse += dt;
                    _cardAnim = MathHelper.Lerp(_cardAnim, 1f, 0.16f);
                    _stepTimer += dt;

                    //骇客时间介入（玩家按了 N）：转盘被强制收起，只暂停推进不回退
                    if (HackTime.Active) {
                        break;
                    }

                    bool isAuto = StepIsAuto[_currentStep];
                    if (isAuto) {
                        if (CheckAutoAdvance() || mouseClicked) {
                            AdvanceStep();
                        }
                        _stuckTimer = 0f;
                    }
                    else {
                        //step0 呼出转盘
                        if (_currentStep == 0
                            && SHPCDomainWheelController.LocalInstance?.IsOpen == true) {
                            AdvanceStep();
                            break;
                        }
                        //step1 领域展开即算选层成功，不绑死某一层
                        if (_currentStep == 1 && Cyberspace.Active) {
                            AdvanceStep();
                            break;
                        }
                        //NEXT兜底
                        if (mouseClicked && _nextBtnRect != Rectangle.Empty
                            && _nextBtnRect.Contains((int)uiMouse.X, (int)uiMouse.Y)) {
                            Main.mouseLeft = false;
                            //跳过呼盘时先把 SHPC 拉回手上，后续步骤才有落点
                            if (_currentStep == 0 && SHPCUI.Instance?.Active != true) {
                                CybTutorialLead.ForceEquipSHPC();
                            }
                            //跳过选层时替玩家把领域点亮，后一步的说明才有落点
                            if (_currentStep == 1 && !Cyberspace.Active) {
                                Cyberspace.Activate(Main.LocalPlayer);
                            }
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
                        TryStartOutro();
                    }
                    break;

                case Phase.Done:
                    TryStartOutro();
                    break;
            }
        }

        private static void TryStartOutro() {
            if (_outroStarted) return;
            if (HackTime.Active || HackTime.Intensity > OutroHackTimeFadeThreshold) return;
            if (NarrativeRunner.IsBusy) return;
            if (CybCourseCompletePanel.Visible) return;
            _outroStarted = true;
            NarrativeRouter.Begin<CybCourseOutroDialogue>();
        }

        private static bool CheckAutoAdvance() {
            if (_currentStep == StepIsAuto.Length - 1)
                return _stepTimer >= AutoStepDuration;
            return false;
        }

        private static void AdvanceStep() {
            _currentStep++;
            _stepTimer = 0f;
            _stuckTimer = 0f;
            _cardAnim = 0f;
            if (_currentStep >= StepIsAuto.Length) {
                //收尾：领域与转盘都收掉，把干净画面交给结课对话
                if (Cyberspace.Active) {
                    Cyberspace.Deactivate();
                }
                RadialWheelHub.CloseAll(silent: true);
                _phase = Phase.FadeOut;
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            if (_phase != Phase.Running && _phase != Phase.FadeOut) return;
            if (_cardAnim < 0.02f) return;

            int idx = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
            if (idx == -1) return;

            layers.Insert(idx, new LegacyGameInterfaceLayer("CWRMod: CybCourse Wheel Tutorial",
                delegate {
                    DrawOverlay(Main.spriteBatch);
                    return true;
                }, InterfaceScaleType.UI));
        }

        private static void DrawOverlay(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null) return;

            float alpha = MathHelper.Clamp(_cardAnim, 0f, 1f);

            //固定左上，永远不压到中央偏下的转盘
            const int cx = 24;
            const int cy = 92;
            float slideY = (1f - alpha) * 20f;
            int finalX = (int)MathHelper.Clamp(cx, 8, Math.Max(8, RadialWheelHub.UIScreenW - CardW - 8));
            int finalY = (int)MathHelper.Clamp(cy + slideY, 8, Math.Max(8, RadialWheelHub.UIScreenH - CardH - 8));
            var card = new Rectangle(finalX, finalY, CardW, CardH);

            _cardRect = card;
            CybCourseCardStyle.DrawCardBg(sb, card, alpha, _shaderTimer);
            DrawCardContent(sb, card, alpha);
            DrawWheelHighlight(sb, px, alpha);
        }

        private static void DrawCardContent(SpriteBatch sb, Rectangle card, float alpha) {
            int stepIdx = (int)MathHelper.Clamp(_currentStep, 0, StepIsAuto.Length - 1);
            string title = _stepTitles[stepIdx].Value;
            string body = ResolveKeyTokens(_stepBodies[stepIdx].Value);
            bool isAuto = StepIsAuto[stepIdx];
            bool stuck = !isAuto && _stuckTimer >= StuckHintAfter;
            //转盘键没绑定时前两步都无法靠操作完成，立即亮出琥珀提示
            bool keyHint = stepIdx <= 1 && !IsWheelKeyBound();

            string counter = $"{stepIdx + 1:D2} / {StepIsAuto.Length:D2}";
            float y = CybCourseCardStyle.DrawHeader(sb, card, alpha, title, counter);
            CybCourseCardStyle.DrawBodyLines(sb, card, ref y, alpha, body);

            //未绑定提示
            if (keyHint && _textKeyHintUnbound != null) {
                CybCourseCardStyle.DrawKeyHintLines(sb, card, ref y, alpha, _shaderTimer, _textKeyHintUnbound.Value);
            }

            if (stuck && _textHintStuck != null) {
                CybCourseCardStyle.DrawStuckHint(sb, card, alpha, _shaderTimer, _textHintStuck.Value);
            }

            if (isAuto) {
                CybCourseCardStyle.DrawStatusTag(sb, card, alpha, _shaderTimer, _textCalibrating.Value);
            }
            else {
                Vector2 uiMouse = RadialWheelHub.UIMouse;
                _nextBtnRect = CybCourseCardStyle.DrawNextButton(sb, card, alpha, stuck,
                    _shaderTimer, _textNextBtn.Value, new Point((int)uiMouse.X, (int)uiMouse.Y));
            }
        }

        /// <summary>
        /// 环形高亮标记转盘位置；中心取 Hub 排布后的 ScreenAnchor
        /// 子世界不剥离义体，两盘并存时 SHPC 盘会被顶上去，画死 0.72 就指错了
        /// </summary>
        private static void DrawWheelHighlight(SpriteBatch sb, Texture2D px, float alpha) {
            int stepIdx = (int)MathHelper.Clamp(_currentStep, 0, StepIsAuto.Length - 1);
            if (stepIdx > 1) return;

            //盘未开时 ScreenAnchor 可能是改分辨率前的旧值，改用实时锚点
            SHPCDomainWheelController ctrl = SHPCDomainWheelController.LocalInstance;
            Vector2 center = ctrl != null && ctrl.OpenProgress > 0.01f
                ? ctrl.ScreenAnchor
                : RadialWheelHub.ResolveAnchor();

            float pulse = 0.6f + 0.4f * MathF.Sin(_highlightPulse * 3.2f);
            Color hColor = new Color(
                (int)(70 * pulse), (int)(215 * pulse), (int)(245 * pulse),
                (int)(150 * pulse * alpha));
            Color bracketColor = new Color(80, 220, 245, (int)(200 * alpha));

            //盘身外一圈脉冲环；step1 盘已展开，收窄避免盖住盘面
            float ringR = SHPCTheme.ButtonOuterR + (stepIdx == 0 ? 26f : 14f);
            float expand = 2f + 3f * MathF.Sin(_highlightPulse * 3.2f);
            SHPCRenderer.DrawArcStroke(sb, px, center, ringR + expand,
                0f, MathHelper.TwoPi, 1.6f, hColor);

            //方形括角标记占位
            int half = (int)(ringR + 12f);
            var rect = new Rectangle((int)center.X - half, (int)center.Y - half, half * 2, half * 2);
            CybCourseCardStyle.DrawLBrackets(sb, px, rect, bracketColor);
        }
    }
}
