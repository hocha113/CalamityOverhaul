using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV.EntrustManager;
using CalamityOverhaul.Content.Cyberwares.UIs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.UI;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.CybCourses
{
    //超梦教程引导层，负责自动触发开场对话、管理教学步骤状态，并绘制教学卡片和高亮标注
    //复用EntrustGuideCard着色器（variant=1青色版），不修改任何被教学的目标UI代码
    internal class CybTutorialLead : ModSystem, ILocalizedModType
    {
        private enum Phase { Inactive, Running, FadeOut, Done }

        public string LocalizationCategory => "ADV.Shepel";

        //各步骤的固定元数据（目标键和推进方式）
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

        //各步骤的本地化标题与正文
        private static LocalizedText[] _stepTitles;
        private static LocalizedText[] _stepBodies;
        private static LocalizedText _textCalibrating;
        private static LocalizedText _textNextBtn;

        public override void SetStaticDefaults() {
            _stepTitles = new[] {
                this.GetLocalization("Step0_Title", () => "连接 SHPC"),
                this.GetLocalization("Step1_Title", () => "核心节点"),
                this.GetLocalization("Step2_Title", () => "CYBER DOMAIN"),
                this.GetLocalization("Step3_Title", () => "MODIFY"),
                this.GetLocalization("Step4_Title", () => "CYBERWARE"),
                this.GetLocalization("Step5_Title", () => "TALK"),
                this.GetLocalization("Step6_Title", () => "ENGRAM CALIBRATED"),
            };
            _stepBodies = new[] {
                this.GetLocalization("Step0_Body", () => "将SHPC装备至武器栏并持握，HUD核心节点即会出现在屏幕左下角。"),
                this.GetLocalization("Step1_Body", () => "点击左下角的核心节点可展开或收起操作面板。"),
                this.GetLocalization("Step2_Body", () => "网域 — 部署并管理多层赛博空间层叠结构。\n点击高亮的扇区即可锁定该面板。"),
                this.GetLocalization("Step3_Body", () => "改造 — 为SHPC安装或拆卸改造零件。\n点击高亮的扇区即可锁定该面板。"),
                this.GetLocalization("Step4_Body", () => "赛博改装 — 查看并管理你的机体增强模块。\n点击高亮的扇区即可打开义体界面。"),
                this.GetLocalization("Step5_Body", () => "神经链路 — 与SHPC建立直连通讯，开启对话。\n点击高亮的扇区即可开启对话。"),
                this.GetLocalization("Step6_Body", () => "所有接口已解析完毕。\n神经链路稳定，SHPC已就绪。"),
            };
            _textCalibrating = this.GetLocalization("Calibrating", () => "CALIBRATING...");
            _textNextBtn = this.GetLocalization("NextBtn", () => "NEXT  >");
            _textHintStuck = this.GetLocalization("HintStuck", () => "// HINT: 试着点击高亮的目标区域");
        }

        private const int CardW = 310;
        private const int CardH = 118;
        private const int EdgePad = 8;
        //自动步骤的统一倒计时（缩短至1.6s，节奏紧凑且不失仪式感）
        private const float AutoStepDuration = 1.6f;
        //手动步骤无操作多少秒后给红色提示
        private const float StuckHintAfter = 12f;

        public static bool IsDone => _phase == Phase.Done;
        //SHPC教学的最后一步（FadeOut或Done阶段）：允许下游HackTime教学提前接入
        public static bool IsTailing => _phase == Phase.FadeOut || _phase == Phase.Done;

        private static Phase _phase = Phase.Inactive;
        private static int _currentStep = 0;
        private static float _cardAnim = 0f;
        private static float _shaderTimer = 0f;
        private static float _highlightPulse = 0f;
        private static float _stepTimer = 0f;
        //当前手动步骤累计的无操作时间，用于触发卡死提示
        private static float _stuckTimer = 0f;
        private static bool _introAttempted = false;
        private static bool _prevMouseLeft = false;
        private static Rectangle _nextBtnRect = Rectangle.Empty;
        private static Rectangle _cardRect = Rectangle.Empty;
        //手动步骤实际推进时记录上一次pinnedSector，避免重复推进
        private static int _lastPinned = -1;
        //卡片Y轴平滑插值，用于规避展开的三级面板
        private static float _smoothCardY = 0f;
        //本地化：卡死提示
        private static LocalizedText _textHintStuck;

        public override void OnWorldUnload() => ResetForRetry();

        //完整重置教程状态，可被OnWorldUnload与RETRY软重启复用
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

        //由CybCourseIntroDialogue.OnScenarioComplete()在对话结束后调用
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

            //平滑插值卡片Y轴，规避已展开的三级面板
            float targetCardY = ComputeTargetCardY();
            _smoothCardY = MathHelper.Lerp(_smoothCardY, targetCardY, 0.15f);

            bool mouseDown = Main.mouseLeft;
            bool mouseClicked = mouseDown && !_prevMouseLeft;
            _prevMouseLeft = mouseDown;

            //卡片可见时屏蔽世界点击，防止玩家在操作引导界面时误触武器
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
                        //自动步骤同样允许点击 / 任意键 / 空格跳过等待
                        if (CheckAutoAdvance() || mouseClicked) {
                            AdvanceStep();
                        }
                        _stuckTimer = 0f;
                    }
                    else {
                        //step 0：玩家自行持握SHPC时自动推进，无需点击NEXT
                        if (_currentStep == 0 && SHPCUI.Instance?.Active == true) {
                            AdvanceStep();
                            break;
                        }
                        //step 1：玩家点击核心展开操作面板时自动推进
                        if (_currentStep == 1 && SHPCUI.Instance?.IsExpanded == true) {
                            AdvanceStep();
                            break;
                        }
                        //step 2~5：固定面板用 PinnedSector 推进，普通按钮用自身打开后的状态推进
                        if (_currentStep >= 2 && _currentStep <= 5) {
                            int targetSector = GetTargetSectorForStep(_currentStep);
                            int pinned = SHPCUI.Instance?.PinnedSector ?? -1;
                            bool completed = false;
                            if (targetSector == SHPCUI.CyberDomainSectorIndex
                                || targetSector == SHPCUI.ModifySectorIndex) {
                                //pinned变化即认为玩家确实点过；锁定目标扇区即推进
                                completed = pinned == targetSector && _lastPinned != targetSector;
                            }
                            else if (targetSector == SHPCUI.CyberwareSectorIndex) {
                                completed = CyberwareUI.Instance?.Active == true;
                            }
                            else if (targetSector == SHPCUI.TalkSectorIndex) {
                                completed = ScenarioManager.IsActive();
                            }
                            if (completed) {
                                _lastPinned = pinned;
                                AdvanceStep();
                                break;
                            }
                            _lastPinned = pinned;
                        }
                        //NEXT按钮：手动跳过当前步骤的兜底通道
                        if (mouseClicked && _nextBtnRect != Rectangle.Empty
                            && _nextBtnRect.Contains(Main.mouseX, Main.mouseY)) {
                            Main.mouseLeft = false;
                            //玩家直接点NEXT跳过装备步骤，强制将SHPC移至持握槽
                            if (_currentStep == 0 && SHPCUI.Instance?.Active != true)
                                ForceEquipSHPC();
                            //玩家直接点NEXT跳过核心点击步骤，强制展开操作面板
                            if (_currentStep == 1)
                                SHPCUI.Instance?.ForceExpand();
                            AdvanceStep();
                            break;
                        }
                        //无操作时累计卡死计时
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

        //开场对话尚未触发时自动启动，_introAttempted 在 OnWorldUnload 中重置，天然防重入
        private static void AutoTriggerIntro() {
            if (_introAttempted) return;
            if (ScenarioManager.IsActive()) return;
            ScenarioManager.Start<CybCourseIntroDialogue>();
            _introAttempted = true;
        }

        //计算卡片目标Y坐标，若有固定面板展开则将卡片移至面板顶部以上
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

        //各自动推进步骤的完成条件（仅IsAuto=true的步骤会调用此方法）
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

        //在热键栏或背包中找SHPC并装备到当前持握槽
        private static void ForceEquipSHPC() {
            Player p = Main.LocalPlayer;
            if (p == null || p.dead) return;
            for (int i = 0; i < 10; i++) {
                if (p.inventory[i].type == SHPCOverride.ID) {
                    p.selectedItem = i;
                    return;
                }
            }
            for (int i = 10; i < 58; i++) {
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
            //跨步骤时重置上一帧锁定状态，避免连续推进
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

            //卡片位置固定在SHPC HUD右侧，从下往上偏移
            Vector2 corePos = SHPCHUDTargets.CorePos;
            int cx = (int)(corePos.X + SHPCTheme.ButtonOuterR + 18f);
            int cy = (int)(corePos.Y) - CardH + 8;
            float slideX = (1f - alpha) * 30f;
            int finalX = cx + (int)slideX;
            //屏幕边界 clamp，防止低分辨率被推出屏幕
            finalX = (int)MathHelper.Clamp(finalX, 8, Math.Max(8, Main.screenWidth - CardW - 8));
            int finalY = (int)MathHelper.Clamp(_smoothCardY, 8, Math.Max(8, Main.screenHeight - CardH - 8));
            var card = new Rectangle(finalX, finalY, CardW, CardH);

            _cardRect = card;
            DrawCardBg(sb, card, alpha);
            DrawCardContent(sb, px, card, alpha);
            DrawHighlightForStep(sb, px, targetKey, alpha);
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

            int stepIdx = (int)MathHelper.Clamp(_currentStep, 0, StepMeta.Length - 1);
            string title = _stepTitles[stepIdx].Value;
            string body = _stepBodies[stepIdx].Value;
            bool isAuto = StepMeta[stepIdx].IsAuto;
            bool stuck = !isAuto && _stuckTimer >= StuckHintAfter;
            float px2 = card.X + 14f;
            float py = card.Y + 12f;

            //步骤计数
            string counter = $"{stepIdx + 1:D2} / {StepMeta.Length:D2}";
            float counterW = font.MeasureString(counter).X * subSc;
            Utils.DrawBorderString(sb, counter,
                new Vector2(card.Right - 14f - counterW, py),
                new Color(70, 155, 175, (int)(150 * alpha)), subSc);

            //标题（青色）
            Utils.DrawBorderString(sb, title, new Vector2(px2, py),
                new Color(80, 220, 245, (int)(255 * alpha)), titleSc);
            py += lineT + 2f;

            //分割线
            BaseManagerStyle.FillRect(sb,
                new Rectangle((int)px2, (int)py, CardW - 28, 1),
                new Color(45, 130, 155, (int)(90 * alpha)));
            py += 6f;

            //正文（支持换行）
            int bodyWrapW = (int)((CardW - 28) / bodySc);
            foreach (string line in body.Split('\n')) {
                string[] bodyWrapped = Utils.WordwrapString(line, font, bodyWrapW, 99, out _);
                foreach (string wl in bodyWrapped) {
                    if (string.IsNullOrEmpty(wl)) continue;
                    Utils.DrawBorderString(sb, wl.TrimEnd('-', ' '), new Vector2(px2, py),
                        new Color(175, 215, 225, (int)(215 * alpha)), bodySc);
                    py += lineB;
                }
            }

            //卡死提示：玩家长时间未操作时附加红色提示
            if (stuck && _textHintStuck != null) {
                float pulse = 0.7f + 0.3f * MathF.Sin(_shaderTimer * 14f);
                Utils.DrawBorderString(sb, _textHintStuck.Value,
                    new Vector2(px2, card.Bottom - 36f),
                    new Color(255, 110, 90, (int)(220 * alpha * pulse)), subSc);
            }

            //底部按钮区
            if (!isAuto) {
                DrawNextButton(sb, card, alpha, stuck);
            }
            else {
                float blink = 0.72f + 0.28f * MathF.Sin(_shaderTimer * 22f);
                float sbW = font.MeasureString(_textCalibrating.Value).X * subSc;
                Utils.DrawBorderString(sb, _textCalibrating.Value,
                    new Vector2(card.Right - 14f - sbW, card.Bottom - 16f),
                    new Color(60, 190, 200, (int)(200 * alpha * blink)), subSc);
            }
        }

        private static void DrawNextButton(SpriteBatch sb, Rectangle card, float alpha, bool stuck) {
            const int btnW = 72, btnH = 20, margin = 10;
            var btn = new Rectangle(card.Right - btnW - margin, card.Bottom - btnH - margin, btnW, btnH);
            _nextBtnRect = btn;

            bool hovered = btn.Contains(Main.mouseX, Main.mouseY);
            //卡死时强化按钮高亮，让玩家明确兜底入口
            float emphasize = stuck ? (0.85f + 0.15f * MathF.Sin(_shaderTimer * 14f)) : 0f;
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

            //目标区域L角括号
            Rectangle rect = target.GetScreenRect();
            DrawLBrackets(sb, px, rect, bracketColor);
        }

        //绘制四角L形括号标注框
        private static void DrawLBrackets(SpriteBatch sb, Texture2D px, Rectangle r, Color c) {
            const int len = 12;
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
