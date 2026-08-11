using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Cyberwares.Skills;
using CalamityOverhaul.Content.Cyberwares.UIs;
using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.UI;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.Narrative.Guides;
using CalamityOverhaul.Content.QuestLogs;
using CalamityOverhaul.Content.Scenarios.Shepel.CybCourses;
using CalamityOverhaul.Content.UIs.RadialWheels;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.Cyberwares.Tutorial
{
    /// <summary>
    /// 义体转盘引导：首次装上带主动技能的义体后，教"转盘选定 → 触发键释放"两步
    /// <br/>触发靠每帧轮询装配表而非装机事件——权威端事件本机收不到，读档也不会补发
    /// <br/>经 <see cref="GuideLeadQueue"/> 排队，晚于比目鱼、早于委托
    /// </summary>
    internal class CyberwareGuideLead : ModSystem, ILocalizedModType, IGuideLead
    {
        public string LocalizationCategory => "UI";

        private enum Phase { Inactive, SelectSkill, TriggerSkill, Complete }

        public static LocalizedText SelectTitle { get; private set; }
        public static LocalizedText SelectBody { get; private set; }
        public static LocalizedText SelectPrompt { get; private set; }
        public static LocalizedText TriggerTitle { get; private set; }
        public static LocalizedText TriggerBody { get; private set; }
        public static LocalizedText TriggerPrompt { get; private set; }
        public static LocalizedText SkipBtn { get; private set; }
        public static LocalizedText WheelKeyUnbound { get; private set; }
        public static LocalizedText TriggerKeyUnbound { get; private set; }

        public override void SetStaticDefaults() {
            GuideLeadQueue.Register(this);
            SelectTitle = this.GetLocalization(nameof(SelectTitle), () => "义体技能已接入");
            SelectBody = this.GetLocalization(nameof(SelectBody),
                () => "检测到主动义体。按住 {0} 呼出快捷转盘，所有够格的盘会一起出现，光标离谁近就归谁。");
            SelectPrompt = this.GetLocalization(nameof(SelectPrompt),
                () => "把光标甩向义体扇区，左键选定或松手确认");
            TriggerTitle = this.GetLocalization(nameof(TriggerTitle), () => "释放技能");
            TriggerBody = this.GetLocalization(nameof(TriggerBody),
                () => "选定只是把技能装进指尖，释放靠触发键。蓄力型按住再松手。");
            TriggerPrompt = this.GetLocalization(nameof(TriggerPrompt),
                () => "按 {0} 释放当前义体技能");
            SkipBtn = this.GetLocalization(nameof(SkipBtn), () => "跳过");
            WheelKeyUnbound = this.GetLocalization(nameof(WheelKeyUnbound),
                () => "快捷转盘键未绑定，转盘无法呼出；请先在 设置 > 控制 中绑定，或点击跳过。");
            TriggerKeyUnbound = this.GetLocalization(nameof(TriggerKeyUnbound),
                () => "触发键未绑定；请先在 设置 > 控制 中绑定，或点击跳过。");
        }

        private const int CardW = 330;
        private const int EdgePad = 8;
        //约9秒卡住才出低调跳过；键未绑定时立即放出
        private const int StuckFramesBeforeSkip = 60 * 9;

        private static Phase currentPhase = Phase.Inactive;
        private static float animProgress;
        private static int phaseTimer;
        private static float shaderTimer;
        private static float highlightPulse;
        //SelectSkill 基线：进相位时的选定 id 与"见过转盘展开"
        private static string selectBaselineId = string.Empty;
        private static bool sawWheelOpen;
        //TriggerSkill 基线：进相位时的触发计数
        private static uint triggerBaseline;

        #region 引导排队协议
        int IGuideLead.GuidePriority => 15;//晚于比目鱼(10)，早于委托(20)
        bool IGuideLead.GuideReserving => Reserving;
        bool IGuideLead.GuideReady => Ready;
        //饥饿保底放弃时收尾记看过，否则占位会永久压制委托引导
        void IGuideLead.OnGuideAbandoned() => MarkSeen();
        #endregion

        private static CyberwareGuideData Guide
            => Main.LocalPlayer.GetModPlayer<StoryPlayer>().Get<CyberwareGuideData>();

        /// <summary>本机装配表里是否有带主动技能的义体</summary>
        private static bool HasActiveCyberware() {
            Player p = Main.LocalPlayer;
            CyberwarePlayer cp = p?.GetModPlayer<CyberwarePlayer>();
            if (cp?.EquippedCyberwares == null) {
                return false;
            }
            for (int i = 0; i < CyberwarePlayer.SlotCount; i++) {
                if (cp.EquippedCyberwares[i]?.ModItem is BaseCyberware c && c.ActiveSkill != null) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>占位：存活 + 未看过 + 装着主动义体</summary>
        private static bool Reserving {
            get {
                Player p = Main.LocalPlayer;
                if (p == null || !p.active) {
                    return false;
                }
                if (Guide.GuideSeen) {
                    return false;
                }
                return HasActiveCyberware();
            }
        }

        /// <summary>就绪：占位 + 回到普通战斗状态（无过场/超梦/诊所/骇客/全屏 UI）</summary>
        private static bool Ready {
            get {
                if (!Reserving) {
                    return false;
                }
                if (NarrativeTriggerGate.IsBusy || InnoVault.Cinematics.CutsceneDirector.IsPlaying) {
                    return false;
                }
                return SessionUsable();
            }
        }

        /// <summary>会话可用；不满足只暂停不重置</summary>
        private static bool SessionUsable() {
            Player p = Main.LocalPlayer;
            if (p == null || !p.active || p.dead) {
                return false;
            }
            //超梦里另有成套教程
            if (CybCourseWorld.Active) {
                return false;
            }
            //诊所/义体管理等全屏 UI 开着时不叠引导
            if (CyberwareUI.Instance?.Active == true) {
                return false;
            }
            if (QuestLog.Instance?.visible == true || QuestManagerUI.Instance?.IsOpen == true) {
                return false;
            }
            //骇客时间自带整套界面，转盘也被它挡着
            if (HackTime.Active) {
                return false;
            }
            return true;
        }

        private static void MarkSeen() {
            Guide.GuideSeen = true;
            currentPhase = Phase.Complete;
            animProgress = 0f;
        }

        private static void SetPhase(Phase phase) {
            currentPhase = phase;
            animProgress = 0f;
            phaseTimer = 0;
        }

        public override void OnWorldUnload() {
            currentPhase = Phase.Inactive;
            animProgress = 0f;
            phaseTimer = 0;
            sawWheelOpen = false;
            selectBaselineId = string.Empty;
        }

        public override void UpdateUI(GameTime gameTime) {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            shaderTimer += (float)gameTime.ElapsedGameTime.TotalSeconds * 0.8f;
            if (shaderTimer > 100f) shaderTimer -= 100f;

            //统一排队、未轮到则待命，异常残留收起
            if (!GuideLeadQueue.IsHolder(this)) {
                if (currentPhase != Phase.Inactive && currentPhase != Phase.Complete) {
                    currentPhase = Phase.Inactive;
                    animProgress = 0f;
                }
                return;
            }

            if (currentPhase == Phase.Inactive) {
                StartSelectPhase();
            }

            //前提暂时不满足（骇客/全屏 UI/死亡）：暂停推进与绘制，不回退
            if (!SessionUsable()) {
                return;
            }

            phaseTimer++;
            animProgress = MathHelper.Lerp(animProgress, 1f, 0.12f);
            highlightPulse += (float)gameTime.ElapsedGameTime.TotalSeconds;

            switch (currentPhase) {
                case Phase.SelectSkill:
                    UpdateSelectSkill();
                    break;
                case Phase.TriggerSkill:
                    UpdateTriggerSkill();
                    break;
            }
        }

        private static void StartSelectPhase() {
            SetPhase(Phase.SelectSkill);
            sawWheelOpen = false;
            selectBaselineId = CyberwareSkillRadialController.LocalInstance?.CurrentSkillId ?? string.Empty;
        }

        private static void UpdateSelectSkill() {
            CyberwareSkillRadialController ctrl = CyberwareSkillRadialController.LocalInstance;
            if (ctrl == null) {
                return;
            }
            if (ctrl.OpenProgress > 0.5f) {
                sawWheelOpen = true;
            }
            //选定变化立即过；只有一个技能且已选中时，完整开合一次也算学会
            bool selectionChanged = !string.Equals(ctrl.CurrentSkillId ?? string.Empty,
                selectBaselineId, StringComparison.Ordinal);
            bool openAndClosed = sawWheelOpen && !ctrl.IsOpen && ctrl.OpenProgress < 0.1f;
            if (selectionChanged || openAndClosed) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.35f, Volume = 0.5f });
                SetPhase(Phase.TriggerSkill);
                triggerBaseline = CyberwareSkillRadialController.LocalSkillTriggerCount;
            }
        }

        private static void UpdateTriggerSkill() {
            if (CyberwareSkillRadialController.LocalSkillTriggerCount != triggerBaseline) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.45f, Volume = 0.55f });
                MarkSeen();
            }
        }

        private static bool IsKeyBound(ModKeybind kb) {
            var keys = kb?.GetAssignedKeys();
            return keys != null && keys.Count > 0;
        }

        #region 绘制

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            if (currentPhase != Phase.SelectSkill && currentPhase != Phase.TriggerSkill) return;
            if (!GuideLeadQueue.IsHolder(this)) return;
            if (!SessionUsable()) return;
            if (animProgress < 0.02f) return;

            int idx = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
            if (idx == -1) return;

            layers.Insert(idx, new LegacyGameInterfaceLayer("CWRMod: Cyberware Wheel Guide",
                delegate {
                    DrawOverlay(Main.spriteBatch);
                    return true;
                }, InterfaceScaleType.UI));
        }

        private static void DrawOverlay(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null) return;

            float alpha = MathHelper.Clamp(animProgress, 0f, 1f);
            bool selectPhase = currentPhase == Phase.SelectSkill;

            //义体盘中心由 Hub 排布：与武器盘并存时它占最底那格，画死 0.72 会指错
            //盘未开时 ScreenAnchor 可能是改分辨率前的旧值，改用实时锚点
            CyberwareSkillRadialController ctrl = CyberwareSkillRadialController.LocalInstance;
            Vector2 wheelCenter = ctrl != null && ctrl.OpenProgress > 0.01f
                ? ctrl.ScreenAnchor
                : RadialWheelHub.ResolveAnchor();

            if (selectPhase) {
                DrawWheelHighlight(sb, px, wheelCenter, alpha);
            }

            //键位与提示文本
            ModKeybind actionKey = selectPhase ? CWRKeySystem.RadialWheel_Key : CWRKeySystem.CyberwareSkill_Key;
            bool keyBound = IsKeyBound(actionKey);
            string keyText = actionKey.ToTooltipString(CWRKeySystem.Notbound.Value);
            string title = selectPhase ? SelectTitle.Value : TriggerTitle.Value;
            string bodyText = selectPhase
                ? string.Format(SelectBody.Value, keyText)
                : TriggerBody.Value;
            string promptText = selectPhase
                ? SelectPrompt.Value
                : string.Format(TriggerPrompt.Value, keyText);
            string unboundText = !keyBound
                ? (selectPhase ? WheelKeyUnbound.Value : TriggerKeyUnbound.Value)
                : null;

            var font = FontAssets.MouseText.Value;
            const float titleSc = 0.84f;
            const float bodySc = 0.70f;
            const float subSc = 0.58f;
            float lineT = font.MeasureString("A").Y * titleSc + 2f;
            float lineB = font.MeasureString("A").Y * bodySc + 1f;

            //按内容量出卡高，避免本地化长文溢出
            int bodyWrapW = (int)((CardW - 28) / bodySc);
            List<string> bodyLines = WrapLines(font, bodyText, bodyWrapW);
            List<string> promptLines = WrapLines(font, promptText, bodyWrapW);
            List<string> unboundLines = unboundText != null
                ? WrapLines(font, unboundText, (int)((CardW - 28) / subSc))
                : null;
            float cardH = 12f + lineT + 2f + 7f
                + bodyLines.Count * lineB + 4f
                + promptLines.Count * lineB
                + (unboundLines?.Count ?? 0) * (lineB - 1f)
                + 38f;

            //锚在义体盘上方，让开盘身
            float footprint = SHPCTheme.ButtonOuterR + 34f;
            float cardX = MathHelper.Clamp(wheelCenter.X - CardW * 0.5f,
                16f, Math.Max(16f, RadialWheelHub.UIScreenW - CardW - 16f));
            float cardY = MathHelper.Clamp(wheelCenter.Y - footprint - cardH - 16f,
                16f, Math.Max(16f, RadialWheelHub.UIScreenH - cardH - 16f));
            float slideY = (1f - alpha) * 18f;
            var card = new Rectangle((int)cardX, (int)(cardY + slideY), CardW, (int)cardH);

            DrawCardBg(sb, card, alpha);

            //内容
            float px2 = card.X + 14f;
            float py = card.Y + 12f;
            string counter = selectPhase ? "01 / 02" : "02 / 02";
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
            py += 7f;

            foreach (string wl in bodyLines) {
                Utils.DrawBorderString(sb, wl, new Vector2(px2, py),
                    new Color(175, 215, 225, (int)(215 * alpha)), bodySc);
                py += lineB;
            }
            py += 4f;

            float promptPulse = 0.8f + 0.2f * MathF.Sin(shaderTimer * 6f);
            foreach (string wl in promptLines) {
                Utils.DrawBorderString(sb, wl, new Vector2(px2, py),
                    new Color(120, 235, 255, (int)(235 * alpha * promptPulse)), bodySc);
                py += lineB;
            }

            if (unboundLines != null) {
                float pulseKey = 0.75f + 0.25f * MathF.Sin(shaderTimer * 10f);
                foreach (string wl in unboundLines) {
                    Utils.DrawBorderString(sb, wl, new Vector2(px2, py),
                        new Color(255, 195, 90, (int)(220 * alpha * pulseKey)), subSc);
                    py += lineB - 1f;
                }
            }

            //键未绑定时立即放出跳过，否则约9秒后才出
            if ((!keyBound || phaseTimer > StuckFramesBeforeSkip)
                && DrawSkipButton(sb, card, alpha)) {
                MarkSeen();
            }
        }

        private static List<string> WrapLines(ReLogic.Graphics.DynamicSpriteFont font, string text, int wrapW) {
            List<string> result = [];
            foreach (string line in text.Split('\n')) {
                string[] wrapped = VaultUtils.WrapTextArray(line, font, wrapW, 99, out _);
                foreach (string wl in wrapped) {
                    if (string.IsNullOrEmpty(wl)) continue;
                    result.Add(wl.TrimEnd('-', ' '));
                }
            }
            return result;
        }

        private static void DrawCardBg(SpriteBatch sb, Rectangle card, float alpha) {
            Effect effect = EffectLoader.EntrustGuideCard?.Value;
            if (effect != null) {
                Rectangle ext = card;
                ext.Inflate(EdgePad, EdgePad);
                effect.Parameters["uTime"]?.SetValue(shaderTimer);
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

        /// <summary>盘位脉冲环 + 括角，标记转盘将出现/已在的位置</summary>
        private static void DrawWheelHighlight(SpriteBatch sb, Texture2D px, Vector2 center, float alpha) {
            float pulse = 0.6f + 0.4f * MathF.Sin(highlightPulse * 3.2f);
            Color hColor = new Color(
                (int)(70 * pulse), (int)(215 * pulse), (int)(245 * pulse),
                (int)(150 * pulse * alpha));
            Color bracketColor = new Color(80, 220, 245, (int)(200 * alpha));

            float ringR = SHPCTheme.ButtonOuterR + 20f;
            float expand = 2f + 3f * MathF.Sin(highlightPulse * 3.2f);
            SHPCRenderer.DrawArcStroke(sb, px, center, ringR + expand,
                0f, MathHelper.TwoPi, 1.6f, hColor);

            int half = (int)(ringR + 12f);
            var rect = new Rectangle((int)center.X - half, (int)center.Y - half, half * 2, half * 2);
            const int len = 14;
            const int thick = 2;
            sb.Draw(px, new Rectangle(rect.Left, rect.Top, len, thick), bracketColor);
            sb.Draw(px, new Rectangle(rect.Left, rect.Top, thick, len), bracketColor);
            sb.Draw(px, new Rectangle(rect.Right - len, rect.Top, len, thick), bracketColor);
            sb.Draw(px, new Rectangle(rect.Right - thick, rect.Top, thick, len), bracketColor);
            sb.Draw(px, new Rectangle(rect.Left, rect.Bottom - thick, len, thick), bracketColor);
            sb.Draw(px, new Rectangle(rect.Left, rect.Bottom - len, thick, len), bracketColor);
            sb.Draw(px, new Rectangle(rect.Right - len, rect.Bottom - thick, len, thick), bracketColor);
            sb.Draw(px, new Rectangle(rect.Right - thick, rect.Bottom - len, thick, len), bracketColor);
        }

        private static bool DrawSkipButton(SpriteBatch sb, Rectangle card, float alpha) {
            var font = FontAssets.MouseText.Value;
            string label = SkipBtn.Value;
            const float sc = 0.62f;
            Vector2 size = font.MeasureString(label) * sc;
            int btnW = (int)size.X + 22;
            const int btnH = 20, margin = 10;
            var btn = new Rectangle(card.Right - btnW - margin, card.Bottom - btnH - margin, btnW, btnH);

            Vector2 uiMouse = RadialWheelHub.UIMouse;
            bool hovered = btn.Contains((int)uiMouse.X, (int)uiMouse.Y);
            Color bg = hovered
                ? new Color(40, 155, 180, (int)(210 * alpha))
                : new Color(18, 72, 92, (int)(150 * alpha));
            Color border = hovered
                ? new Color(100, 220, 245, (int)(200 * alpha))
                : new Color(50, 150, 180, (int)(120 * alpha));
            Color textCol = hovered
                ? new Color(200, 250, 255, (int)(255 * alpha))
                : new Color(110, 205, 225, (int)(195 * alpha));

            BaseManagerStyle.FillRect(sb, btn, bg);
            BaseManagerStyle.StrokeRect(sb, btn, 1, border);
            BaseManagerStyle.DrawCenteredText(sb, label, btn.Center.ToVector2(), textCol, sc);

            if (hovered) {
                Main.LocalPlayer.mouseInterface = true;
                if (Main.mouseLeft && Main.mouseLeftRelease) {
                    Main.mouseLeftRelease = false;
                    return true;
                }
            }
            return false;
        }

        #endregion
    }
}
