using CalamityOverhaul.Common;
using CalamityOverhaul.Content.EntrustManager;
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
using Terraria.GameContent;
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
            _textCalibrating = this.GetLocalization("RW_Calibrating", () => "SYNCHRONIZED...");
            _textNextBtn = this.GetLocalization("RW_NextBtn", () => "NEXT  >");
            _textHintStuck = this.GetLocalization("RW_HintStuck", () => "HINT: 点击 NEXT 按钮可强制跳过");
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
            //骇客键未绑定时如实报未绑定——骇客教程那句"N 临时开关"只在它自己运行期有效
            string hackKey = HackTimeTutorialLead.IsHackToggleBound()
                ? HackTimeTutorialLead.GetHackToggleKeyDisplay()
                : CWRKeySystem.Notbound.Value;
            return raw.Replace(WheelKeyToken, GetWheelKeyDisplay())
                .Replace(HackKeyToken, hackKey);
        }

        private const int CardW = 310;
        private const int CardH = 118;
        private const int EdgePad = 8;
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
            DrawCardBg(sb, card, alpha);
            DrawCardContent(sb, px, card, alpha);
            DrawWheelHighlight(sb, px, alpha);
        }

        private static void DrawCardBg(SpriteBatch sb, Rectangle card, float alpha) {
            Effect effect = EffectLoader.EntrustGuideCard?.Value;
            if (effect != null) {
                Rectangle ext = card;
                ext.Inflate(EdgePad, EdgePad);
                effect.Parameters["uTime"]?.SetValue(_shaderTimer);
                effect.Parameters["uAlpha"]?.SetValue(alpha * 0.96f);
                effect.Parameters["uResolution"]?.SetValue(new Vector2(ext.Width, ext.Height));
                effect.Parameters["uEdgePad"]?.SetValue((float)EdgePad);
                effect.Parameters["uVariant"]?.SetValue(1f);
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
                sb.Draw(VaultAsset.placeholder2.Value, card, new Color(0, 8, 18, (int)(200 * alpha)));
                BaseManagerStyle.StrokeRect(sb, card, 1, new Color(50, 160, 200, (int)(120 * alpha)));
            }
        }

        private static void DrawCardContent(SpriteBatch sb, Texture2D px, Rectangle card, float alpha) {
            var font = FontAssets.MouseText.Value;
            float titleSc = 0.84f;
            float bodySc = 0.70f;
            float subSc = 0.58f;
            float lineT = font.MeasureString("A").Y * titleSc + 2f;
            float lineB = font.MeasureString("A").Y * bodySc + 1f;

            int stepIdx = (int)MathHelper.Clamp(_currentStep, 0, StepIsAuto.Length - 1);
            string title = _stepTitles[stepIdx].Value;
            string body = ResolveKeyTokens(_stepBodies[stepIdx].Value);
            bool isAuto = StepIsAuto[stepIdx];
            bool stuck = !isAuto && _stuckTimer >= StuckHintAfter;
            //转盘键没绑定时前两步都无法靠操作完成，立即亮出琥珀提示
            bool keyHint = stepIdx <= 1 && !IsWheelKeyBound();
            float px2 = card.X + 14f;
            float py = card.Y + 12f;

            string counter = $"{stepIdx + 1:D2} / {StepIsAuto.Length:D2}";
            float counterW = font.MeasureString(counter).X * subSc;
            Utils.DrawBorderString(sb, counter,
                new Vector2(card.Right - 14f - counterW, py),
                new Color(70, 155, 175, (int)(150 * alpha)), subSc);

            Utils.DrawBorderString(sb, title, new Vector2(px2, py),
                new Color(80, 220, 245, (int)(255 * alpha)), titleSc);
            py += lineT + 2f;

            BaseManagerStyle.FillRect(sb,
                new Rectangle((int)px2, (int)py, CardW - 28, 1),
                new Color(45, 130, 155, (int)(90 * alpha)));
            py += 6f;

            int bodyWrapW = (int)((CardW - 28) / bodySc);
            foreach (string line in body.Split('\n')) {
                string[] wrapped = VaultUtils.WrapTextArray(line, font, bodyWrapW, 99, out _);
                foreach (string wl in wrapped) {
                    if (string.IsNullOrEmpty(wl)) continue;
                    Utils.DrawBorderString(sb, wl.TrimEnd('-', ' '), new Vector2(px2, py),
                        new Color(175, 215, 225, (int)(215 * alpha)), bodySc);
                    py += lineB;
                }
            }

            //未绑定提示
            if (keyHint && _textKeyHintUnbound != null) {
                float pulseKey = 0.75f + 0.25f * MathF.Sin(_shaderTimer * 10f);
                int wrapW = (int)((CardW - 28) / subSc);
                string[] wrapped = VaultUtils.WrapTextArray(_textKeyHintUnbound.Value, font, wrapW, 99, out _);
                foreach (string wl in wrapped) {
                    if (string.IsNullOrEmpty(wl)) continue;
                    Utils.DrawBorderString(sb, wl.TrimEnd('-', ' '), new Vector2(px2, py),
                        new Color(255, 195, 90, (int)(220 * alpha * pulseKey)), subSc);
                    py += lineB - 1f;
                }
            }

            if (stuck && _textHintStuck != null) {
                float pulseHint = 0.7f + 0.3f * MathF.Sin(_shaderTimer * 14f);
                Utils.DrawBorderString(sb, _textHintStuck.Value,
                    new Vector2(px2, card.Bottom - 36f),
                    new Color(255, 110, 90, (int)(220 * alpha * pulseHint)), subSc);
            }

            if (isAuto) {
                float blink = 0.72f + 0.28f * MathF.Sin(_shaderTimer * 22f);
                float sbW = font.MeasureString(_textCalibrating.Value).X * subSc;
                Utils.DrawBorderString(sb, _textCalibrating.Value,
                    new Vector2(card.Right - 14f - sbW, card.Bottom - 16f),
                    new Color(60, 190, 200, (int)(200 * alpha * blink)), subSc);
            }
            else {
                DrawNextButton(sb, card, alpha, stuck);
            }
        }

        private static void DrawNextButton(SpriteBatch sb, Rectangle card, float alpha, bool stuck) {
            const int btnW = 72, btnH = 20, margin = 10;
            var btn = new Rectangle(card.Right - btnW - margin, card.Bottom - btnH - margin, btnW, btnH);
            _nextBtnRect = btn;

            Vector2 uiMouse = RadialWheelHub.UIMouse;
            bool hovered = btn.Contains((int)uiMouse.X, (int)uiMouse.Y);
            float emphasize = stuck ? 0.85f + 0.15f * MathF.Sin(_shaderTimer * 14f) : 0f;
            Color bgColor = hovered
                ? new Color(40, 155, 180, (int)(210 * alpha))
                : new Color(18 + (int)(40 * emphasize), 72, 92, (int)((150 + 50 * emphasize) * alpha));
            Color borderColor = hovered
                ? new Color(100, 220, 245, (int)(200 * alpha))
                : new Color(50 + (int)(80 * emphasize), 150, 180, (int)((120 + 80 * emphasize) * alpha));
            Color textColor = hovered
                ? new Color(200, 250, 255, (int)(255 * alpha))
                : new Color(110 + (int)(80 * emphasize), 205, 225, (int)((195 + 60 * emphasize) * alpha));

            BaseManagerStyle.FillRect(sb, btn, bgColor);
            BaseManagerStyle.StrokeRect(sb, btn, 1, borderColor);
            BaseManagerStyle.DrawCenteredText(sb, _textNextBtn.Value, btn.Center.ToVector2(), textColor, 0.60f);
        }

        /// <summary>
        /// 环形高亮标记转盘位置；中心取 Hub 排布后的 ScreenAnchor——
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
            DrawLBrackets(sb, px, rect, bracketColor);
        }

        private static void DrawLBrackets(SpriteBatch sb, Texture2D px, Rectangle r, Color c) {
            const int len = 14;
            const int thick = 2;
            sb.Draw(px, new Rectangle(r.Left, r.Top, len, thick), c);
            sb.Draw(px, new Rectangle(r.Left, r.Top, thick, len), c);
            sb.Draw(px, new Rectangle(r.Right - len, r.Top, len, thick), c);
            sb.Draw(px, new Rectangle(r.Right - thick, r.Top, thick, len), c);
            sb.Draw(px, new Rectangle(r.Left, r.Bottom - thick, len, thick), c);
            sb.Draw(px, new Rectangle(r.Left, r.Bottom - len, thick, len), c);
            sb.Draw(px, new Rectangle(r.Right - len, r.Bottom - thick, len, thick), c);
            sb.Draw(px, new Rectangle(r.Right - thick, r.Bottom - len, thick, len), c);
        }
    }
}
