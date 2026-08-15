using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Runtime;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.Scenarios.Shepel.CybCourses
{
    //骇客时间教程，SHPC下游
    //12步，全息训练标靶+发电机MK2
    internal class HackTimeTutorialLead : ModSystem, ILocalizedModType
    {
        private static int DummyType => ModContent.NPCType<CybTrainingDummy>();

        private enum Phase { Inactive, Running, FadeOut, Done }

        public string LocalizationCategory => "ADV.Shepel";

        //0进骇客 1锁NPC 2协议 3入队 4退出 5观察
        //6再进 7锁物块 8入队 9退出 10观察 11完成
        private static readonly bool[] StepIsAuto = {
            false, true, false, true,
            false, true, false, true,
            true, false, true, true,
        };

        //需激活的手动步
        private static readonly HashSet<int> StepWantsActive = new() { 0, 6 };
        //需退出的手动步
        private static readonly HashSet<int> StepWantsInactive = new() { 4, 9 };

        private static LocalizedText[] _stepTitles;
        private static LocalizedText[] _stepBodies;
        private static LocalizedText _textWaiting;
        private static LocalizedText _textCalibrating;
        private static LocalizedText _textObserving;
        private static LocalizedText _textNextBtn;
        private static LocalizedText _textKeyUnbound;
        private static LocalizedText _textKeyHintUnbound;
        private const string HackKeyToken = "{0}";

        public override void SetStaticDefaults() {
            _stepTitles = new[] {
                this.GetLocalization("HT_S00_Title", () => "激活骇客模式"),
                this.GetLocalization("HT_S01_Title", () => "锁定NPC目标"),
                this.GetLocalization("HT_S02_Title", () => "骇入协议面板"),
                this.GetLocalization("HT_S03_Title", () => "加入上传队列"),
                this.GetLocalization("HT_S04_Title", () => "退出骇客时间执行"),
                this.GetLocalization("HT_S05_Title", () => "协议执行中"),
                this.GetLocalization("HT_S06_Title", () => "再次进入骇客模式"),
                this.GetLocalization("HT_S07_Title", () => "锁定物块目标"),
                this.GetLocalization("HT_S08_Title", () => "加入物块协议"),
                this.GetLocalization("HT_S09_Title", () => "退出骇客时间执行"),
                this.GetLocalization("HT_S10_Title", () => "物块协议执行中"),
                this.GetLocalization("HT_S11_Title", () => "训练完成"),
            };
            _stepBodies = new[] {
                this.GetLocalization("HT_S00_Body",
                    () => "按下 {0} 键进入骇客时间模式。\n时间将冻结，赛博滤镜叠加于画面。"),
                this.GetLocalization("HT_S01_Body",
                    () => "将光标悬停到高亮的训练标靶上，\n点击左键将其锁定为骇入目标。"),
                this.GetLocalization("HT_S02_Body",
                    () => "右侧面板展示目标的可用骇入协议。\n不同协议消耗不同RAM并产生不同效果。"),
                this.GetLocalization("HT_S03_Body",
                    () => "点击右侧任一协议将其加入左侧上传队列，\n队列在骇客时间内仅排队不会推进。"),
                this.GetLocalization("HT_S04_Body",
                    () => "再次按下 {0} 退出骇客时间，\n协议将在实时世界中开始上传并生效。"),
                this.GetLocalization("HT_S05_Body",
                    () => "上传中... 观察目标遭受协议效果。\n队列清空后将进入下一阶段。"),
                this.GetLocalization("HT_S06_Body",
                    () => "前方走廊有一台热能发电机MK2。\n再次按下 {0} 进入骇客时间扫描物块。"),
                this.GetLocalization("HT_S07_Body",
                    () => "将光标悬停在高亮的发电机上，\n点击左键将其锁定为扫描目标。"),
                this.GetLocalization("HT_S08_Body",
                    () => "右侧面板展示物块专属协议。\n点击任一协议将其加入上传队列。"),
                this.GetLocalization("HT_S09_Body",
                    () => "再次按下 {0} 退出骇客时间，\n物块协议将在实时世界中执行。"),
                this.GetLocalization("HT_S10_Body",
                    () => "上传中... 观察发电机遭受协议效果。\n队列清空后训练即告完成。"),
                this.GetLocalization("HT_S11_Body",
                    () => "骇客协议训练全部完成。\n你已掌握扫描、协议、上传、生效的完整流程。"),
            };
            _textWaiting = this.GetLocalization("HT_Waiting", () => "等待操作…");
            _textCalibrating = this.GetLocalization("HT_Calibrating", () => "正在断开…");
            _textObserving = this.GetLocalization("HT_Observing", () => "上传中…");
            _textNextBtn = this.GetLocalization("HT_NextBtn", () => "跳过 >");
            _textHintStuck = this.GetLocalization("HT_HintStuck", () => "提示：点击 跳过 按钮可强制越过这一步");
            _textKeyUnbound = this.GetLocalization("HT_KeyUnbound", () => "N（临时开关）");
            _textKeyHintUnbound = this.GetLocalization("HT_KeyHintUnbound",
                () => "提示：未绑定骇客时间快捷键时，本教程内可用 [N] 临时开关；建议在 设置 > 控制 中绑定。");
        }

        public static string GetHackToggleKeyDisplay() {
            ModKeybind kb = CWRKeySystem.HackTime_Toggle;
            if (kb != null) {
                var keys = kb.GetAssignedKeys();
                if (keys != null && keys.Count > 0)
                    return $"[{keys[0]}]";
            }
            return $"[{(_textKeyUnbound != null ? _textKeyUnbound.Value : "未绑定·N")}]";
        }

        public static bool IsHackToggleBound() {
            ModKeybind kb = CWRKeySystem.HackTime_Toggle;
            if (kb == null) return false;
            var keys = kb.GetAssignedKeys();
            return keys != null && keys.Count > 0;
        }

        public static string ResolveKeyTokens(string raw)
            => string.IsNullOrEmpty(raw) ? raw : raw.Replace(HackKeyToken, GetHackToggleKeyDisplay());

        private const int CardW = CybCourseCardStyle.CardW;
        private const int CardH = CybCourseCardStyle.CardH;
        private const float AutoStepDuration = 1.6f;
        private const float StuckHintAfter = 12f;
        private const float HackIntroLeadDelay = 0.15f;

        private static Phase _phase = Phase.Inactive;
        private static int _currentStep = 0;
        private static float _cardAnim = 0f;
        private static float _shaderTimer = 0f;
        private static float _highlightPulse = 0f;
        private static float _stepTimer = 0f;
        private static float _stuckTimer = 0f;
        //SHPC衔接计时
        private static float _hackIntroLeadTimer = 0f;
        private static bool _hackIntroAttempted = false;
        private static bool _prevMouseLeft = false;
        //N键兜底边沿
        private static bool _prevFallbackKeyDown = false;
        private static Rectangle _nextBtnRect = Rectangle.Empty;
        private static Rectangle _cardRect = Rectangle.Empty;
        private static LocalizedText _textHintStuck;

        private static int _npcIndex = -1;
        private static Vector2 _npcSpawnPos;

        public override void OnWorldUnload() => ResetForRetry();

        public static void ResetForRetry() {
            CleanupDummy();
            _phase = Phase.Inactive;
            _currentStep = 0;
            _cardAnim = 0f;
            _shaderTimer = 0f;
            _highlightPulse = 0f;
            _stepTimer = 0f;
            _stuckTimer = 0f;
            _hackIntroLeadTimer = 0f;
            _hackIntroAttempted = false;
            _prevMouseLeft = false;
            _nextBtnRect = Rectangle.Empty;
            _cardRect = Rectangle.Empty;
            _npcIndex = -1;
            _prevFallbackKeyDown = false;
        }

        public static void BeginHackTimeTutorial() {
            _phase = Phase.Running;
            _currentStep = 0;
            _cardAnim = 0f;
            _stepTimer = 0f;
            _stuckTimer = 0f;
            SpawnOrFindDummy();
        }

        //左退重试防阻挡
        private static void SpawnOrFindDummy() {
            if (Main.dedServ) return;
            for (int i = 0; i < Main.maxNPCs; i++) {
                if (Main.npc[i].active && Main.npc[i].type == DummyType) {
                    _npcIndex = i;
                    _npcSpawnPos = Main.npc[i].position;
                    return;
                }
            }
            //甲板前段，Y先落在行走面附近再校正；X夹进甲板范围——
            //玩家贴着护沿触发时标靶不许吊在虚空外
            int baseX = (int)Main.LocalPlayer.Center.X + 350;
            int minX = (CybCourseGen.PlatformLeft + 8) * 16;
            int maxX = (CybCourseGen.PlatformRight - 10) * 16;
            int spawnY = (CybCourseGen.SurfaceY - 4) * 16;
            for (int retry = 0; retry < 3; retry++) {
                int spawnX = Math.Clamp(baseX - retry * 50, minX, maxX);
                int idx = NPC.NewNPC(new EntitySource_WorldEvent(), spawnX, spawnY, DummyType);
                if (idx >= 0 && idx < Main.maxNPCs) {
                    float correctY = CybCourseGen.SurfaceY * 16f - Main.npc[idx].height;
                    _npcIndex = idx;
                    _npcSpawnPos = new Vector2(Main.npc[idx].position.X, correctY);
                    Main.npc[idx].position = _npcSpawnPos;
                    Main.npc[idx].dontTakeDamage = true;
                    return;
                }
            }
        }

        public override void PostUpdateNPCs() {
            if (!CybCourseWorld.Active) return;
            if (_npcIndex < 0 || _npcIndex >= Main.maxNPCs) return;
            if (_phase == Phase.Inactive || _phase == Phase.Done) return;

            NPC npc = Main.npc[_npcIndex];
            if (!npc.active || npc.type != DummyType) {
                _npcIndex = -1;
                return;
            }
            npc.velocity = Vector2.Zero;
            npc.position = _npcSpawnPos;
            //step5观察期允许受击
            npc.dontTakeDamage = !(_phase == Phase.Running && _currentStep == 5);
            //step5血量保底
            if (_phase == Phase.Running && _currentStep == 5) {
                if (npc.life < npc.lifeMax / 2)
                    npc.life = npc.lifeMax / 2;
            }
        }

        public override void UpdateUI(GameTime gameTime) {
            if (Main.dedServ || Main.gameMenu) return;
            if (!CybCourseWorld.Active) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _shaderTimer += dt * 0.8f;
            if (_shaderTimer > 100f) _shaderTimer -= 100f;

            AutoTriggerHackIntro(dt);

            bool mouseDown = Main.mouseLeft;
            bool mouseClicked = mouseDown && !_prevMouseLeft;
            _prevMouseLeft = mouseDown;

            if (_cardRect != Rectangle.Empty && _cardRect.Contains(Main.mouseX, Main.mouseY))
                Main.LocalPlayer.mouseInterface = true;

            switch (_phase) {
                case Phase.Running:
                    _highlightPulse += dt;
                    _cardAnim = MathHelper.Lerp(_cardAnim, 1f, 0.16f);
                    _stepTimer += dt;

                    //step1~5保活标靶
                    if (_currentStep >= 1 && _currentStep <= 5) {
                        EnsureDummyAlive();
                    }

                    //未绑定时N键兜底
                    HandleHackToggleKeyFallback();

                    bool isAuto = StepIsAuto[_currentStep];
                    if (isAuto) {
                        if (CheckAutoAdvance() || mouseClicked) {
                            AdvanceStep();
                        }
                        _stuckTimer = 0f;
                    }
                    else {
                        int s = _currentStep;
                        bool wantsActive = StepWantsActive.Contains(s);
                        bool wantsInactive = StepWantsInactive.Contains(s);
                        //自动进/退骇客
                        if (wantsActive && HackTime.Active) {
                            AdvanceStep();
                            break;
                        }
                        if (wantsInactive && !HackTime.Active) {
                            AdvanceStep();
                            break;
                        }
                        //NEXT兜底
                        if (mouseClicked && _nextBtnRect != Rectangle.Empty
                                && _nextBtnRect.Contains(Main.mouseX, Main.mouseY)) {
                            Main.mouseLeft = false;
                            if (wantsActive && !HackTime.Active)
                                HackTime.Activate();
                            else if (wantsInactive && HackTime.Active)
                                HackTime.Deactivate();
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
                        CleanupDummy();
                        WheelTutorialLead.TryStartWheelIntro();
                    }
                    break;

                case Phase.Done:
                    //收尾对话改由转盘段结束后接手
                    WheelTutorialLead.TryStartWheelIntro();
                    break;
            }
        }

        //未绑定HackTime时用N键兜底，已绑定不接管
        private static void HandleHackToggleKeyFallback() {
            if (_phase != Phase.Running) {
                _prevFallbackKeyDown = false;
                return;
            }
            if (IsHackToggleBound()) {
                _prevFallbackKeyDown = false;
                return;
            }
            //编辑界面不触发
            if (Main.editSign || Main.editChest || Main.drawingPlayerChat) {
                _prevFallbackKeyDown = false;
                return;
            }

            bool nowDown = Main.keyState.IsKeyDown(Keys.N);
            if (nowDown && !_prevFallbackKeyDown) {
                HackTime.Toggle();
            }
            _prevFallbackKeyDown = nowDown;
        }

        private static void AutoTriggerHackIntro(float dt) {
            if (_hackIntroAttempted) return;
            if (_phase != Phase.Inactive) return;
            if (!CybTutorialLead.IsTailing) return;
            _hackIntroLeadTimer += dt;
            if (_hackIntroLeadTimer < HackIntroLeadDelay) return;
            if (NarrativeRunner.IsBusy) return;
            NarrativeRouter.Begin<CybCourseHackIntroDialogue>();
            _hackIntroAttempted = true;
        }

        private static bool CheckAutoAdvance() {
            int step = _currentStep;
            var queue = HackTimeUI.Instance?.Queue;
            //step1锁训练标靶
            if (step == 1) {
                int ti = HackTime.SelectedTargetIndex;
                //SelectedTargetIndex上界
                if (ti < 0 || ti >= Main.npc.Length) return false;
                NPC target = Main.npc[ti];
                return target.active && target.type == DummyType;
            }
            //step3队列入队
            if (step == 3)
                return (queue?.Entries.Count ?? 0) > 0;
            //step5须退出骇客且队列空
            if (step == 5)
                return _stepTimer >= 1.5f && !HackTime.Active && (queue?.IsEmpty ?? true);
            //step7锁物块
            if (step == 7)
                return HackTime.Active && HackTime.CurrentScanTarget is TileScannable;
            //step8物块入队
            if (step == 8)
                return (queue?.Entries.Count ?? 0) > 0;
            //step10观察物块
            if (step == 10)
                return _stepTimer >= 1.5f && !HackTime.Active && (queue?.IsEmpty ?? true);
            //step11完成
            if (step == StepIsAuto.Length - 1)
                return _stepTimer >= AutoStepDuration;
            return false;
        }

        private static void EnsureDummyAlive() {
            if (_npcIndex >= 0 && _npcIndex < Main.maxNPCs) {
                NPC npc = Main.npc[_npcIndex];
                if (npc.active && npc.type == DummyType) return;
            }
            _npcIndex = -1;
            SpawnOrFindDummy();
        }

        private static void CleanupDummy() {
            if (_npcIndex >= 0 && _npcIndex < Main.maxNPCs) {
                NPC npc = Main.npc[_npcIndex];
                if (npc.active && npc.type == DummyType)
                    npc.active = false;
                _npcIndex = -1;
            }
        }

        private static void AdvanceStep() {
            _currentStep++;
            _stepTimer = 0f;
            _stuckTimer = 0f;
            _cardAnim = 0f;
            //step6清NPC
            if (_currentStep == 6)
                CleanupDummy();
            if (_currentStep >= StepIsAuto.Length) {
                if (HackTime.Active)
                    HackTime.Deactivate();
                _phase = Phase.FadeOut;
                return;
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            if (_phase != Phase.Running && _phase != Phase.FadeOut) return;
            if (_cardAnim < 0.02f) return;

            int idx = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
            if (idx == -1) return;

            layers.Insert(idx, new LegacyGameInterfaceLayer("CWRMod: CybCourse HackTime Tutorial",
                delegate {
                    DrawOverlay(Main.spriteBatch);
                    return true;
                }, InterfaceScaleType.UI));
        }

        private static void DrawOverlay(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null) return;

            float alpha = MathHelper.Clamp(_cardAnim, 0f, 1f);

            int cx, cy;
            float slideX = 0f, slideY = 0f;
            if (HackTime.Active && HackTime.SelectedTargetIndex < 0
                && !(HackTime.CurrentScanTarget is TileScannable)) {
                //未锁目标放右上
                cx = Main.screenWidth - CardW - 24;
                cy = 96;
                slideX = (1f - alpha) * 24f;
            }
            else {
                //快捷栏下方左侧
                cx = 24;
                cy = 92;
                slideY = (1f - alpha) * 20f;
            }
            //屏幕clamp
            int finalX = (int)MathHelper.Clamp(cx + (int)slideX, 8, Math.Max(8, Main.screenWidth - CardW - 8));
            int finalY = (int)MathHelper.Clamp(cy + (int)slideY, 8, Math.Max(8, Main.screenHeight - CardH - 8));
            var card = new Rectangle(finalX, finalY, CardW, CardH);

            _cardRect = card;
            CybCourseCardStyle.DrawCardBg(sb, card, alpha, _shaderTimer);
            DrawCardContent(sb, card, alpha);
            DrawHighlightForStep(sb, px, alpha);
        }

        private static void DrawCardContent(SpriteBatch sb, Rectangle card, float alpha) {
            int stepIdx = (int)MathHelper.Clamp(_currentStep, 0, StepIsAuto.Length - 1);
            string title = _stepTitles[stepIdx].Value;
            string body = ResolveKeyTokens(_stepBodies[stepIdx].Value);
            bool isAuto = StepIsAuto[stepIdx];
            bool stuck = !isAuto && _stuckTimer >= StuckHintAfter;
            bool keyHint = (stepIdx == 0 || stepIdx == 4 || stepIdx == 6 || stepIdx == 9) && !IsHackToggleBound();

            string counter = $"{stepIdx + 1:D2} / {StepIsAuto.Length:D2}";
            float y = CybCourseCardStyle.DrawHeader(sb, card, alpha, title, counter);
            CybCourseCardStyle.DrawBodyLines(sb, card, ref y, alpha, body);

            //未绑定快捷键提示
            if (keyHint && _textKeyHintUnbound != null) {
                CybCourseCardStyle.DrawKeyHintLines(sb, card, ref y, alpha, _shaderTimer, _textKeyHintUnbound.Value);
            }

            if (stuck && _textHintStuck != null) {
                CybCourseCardStyle.DrawStuckHint(sb, card, alpha, _shaderTimer, _textHintStuck.Value);
            }

            if (isAuto) {
                bool isObservingStep = stepIdx == 5 || stepIdx == 10;
                bool isCompletionStep = stepIdx == StepIsAuto.Length - 1;
                string standby = isCompletionStep
                    ? _textCalibrating.Value
                    : isObservingStep
                        ? _textObserving.Value
                        : _textWaiting.Value;
                CybCourseCardStyle.DrawStatusTag(sb, card, alpha, _shaderTimer, standby);
            }
            else {
                _nextBtnRect = CybCourseCardStyle.DrawNextButton(sb, card, alpha, stuck,
                    _shaderTimer, _textNextBtn.Value, new Point(Main.mouseX, Main.mouseY));
            }
        }

        private static void DrawHighlightForStep(SpriteBatch sb, Texture2D px, float alpha) {
            int stepIdx = (int)MathHelper.Clamp(_currentStep, 0, StepIsAuto.Length - 1);
            //step7发电机
            if (stepIdx == 7) {
                DrawGeneratorHighlight(sb, px, alpha);
                return;
            }
            //step1标靶
            if (stepIdx != 1) return;
            if (_npcIndex < 0 || _npcIndex >= Main.maxNPCs) return;

            NPC npc = Main.npc[_npcIndex];
            if (!npc.active || npc.type != DummyType) return;

            float pulse = 0.6f + 0.4f * MathF.Sin(_highlightPulse * 3.2f);
            Color bracketColor = new Color(80, 220, 245, (int)(200 * alpha));
            Color outlineColor = new Color(
                (int)(70 * pulse), (int)(215 * pulse), (int)(245 * pulse),
                (int)(120 * pulse * alpha));

            //GameView矩阵
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            var npcRect = new Rectangle(
                (int)npc.position.X - 8, (int)npc.position.Y - 8,
                npc.width + 16, npc.height + 16);
            sb.Draw(px, npcRect, outlineColor);
            CybCourseCardStyle.DrawLBrackets(sb, px, npcRect, bracketColor);

            //回UI矩阵
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }

        private static void DrawGeneratorHighlight(SpriteBatch sb, Texture2D px, float alpha) {
            float pulse = 0.6f + 0.4f * MathF.Sin(_highlightPulse * 3.2f);
            Color bracketColor = new Color(80, 220, 245, (int)(200 * alpha));
            Color outlineColor = new Color(
                (int)(70 * pulse), (int)(215 * pulse), (int)(245 * pulse),
                (int)(120 * pulse * alpha));

            //GameView矩阵
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            var rect = new Rectangle(
                CybCourseGen.GenMK2TileLeft * 16 - 4,
                CybCourseGen.GenMK2TileTop * 16 - 4,
                CybCourseGen.GenMK2TileW * 16 + 8,
                CybCourseGen.GenMK2TileH * 16 + 8);
            sb.Draw(px, rect, outlineColor);
            CybCourseCardStyle.DrawLBrackets(sb, px, rect, bracketColor);

            //回UI矩阵
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }
    }
}
