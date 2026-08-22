using CalamityOverhaul.Common;
using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI.Panorama;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI.ServantWheel;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.Narrative.Guides;
using CalamityOverhaul.Content.QuestLogs;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI
{
    /// <summary>
    /// 鬼伞五步引导：首次持伞后串起 开域 → 沉溺 → 湖心景 → 转盘号令 → 鬼梦。
    /// 卡片底板走 KikasaScene.fx 的 TechCard 湿纸技法（入口已迁 KikasaPanoramaRenderer）；
    /// 推进全靠玩家真实操作，键未绑定或卡住时放出跳过。
    /// 经 <see cref="GuideLeadQueue"/> 排队，晚于比目鱼(10)、早于义体(15)
    /// </summary>
    internal class KikasaHudLead : ModSystem, ILocalizedModType, IGuideLead
    {
        public string LocalizationCategory => "Legend.KikasaText";

        private enum Phase { Inactive, Domain, Sink, Panorama, Wheel, Dream, Complete }

        //五个教学步的相位序，计数与跳过推进共用
        private static readonly Phase[] StepOrder =
            [Phase.Domain, Phase.Sink, Phase.Panorama, Phase.Wheel, Phase.Dream];

        #region 本地化
        public static LocalizedText DomainTitle { get; private set; }
        public static LocalizedText DomainBody { get; private set; }
        public static LocalizedText DomainPrompt { get; private set; }
        public static LocalizedText SinkTitle { get; private set; }
        public static LocalizedText SinkBody { get; private set; }
        public static LocalizedText SinkPrompt { get; private set; }
        public static LocalizedText PanoramaTitle { get; private set; }
        public static LocalizedText PanoramaBody { get; private set; }
        public static LocalizedText PanoramaPrompt { get; private set; }
        public static LocalizedText WheelTitle { get; private set; }
        public static LocalizedText WheelBody { get; private set; }
        public static LocalizedText WheelPrompt { get; private set; }
        public static LocalizedText WheelNoMemory { get; private set; }
        public static LocalizedText DreamTitle { get; private set; }
        public static LocalizedText DreamBody { get; private set; }
        public static LocalizedText DreamPrompt { get; private set; }
        public static LocalizedText SkipBtn { get; private set; }
        public static LocalizedText KeyUnbound { get; private set; }

        public override void SetStaticDefaults() {
            GuideLeadQueue.Register(this);

            DomainTitle = this.GetLocalization(nameof(DomainTitle), () => "Raise the Blood Lake");
            DomainBody = this.GetLocalization(nameof(DomainBody),
                () => "Everything the umbrella owns grows in the blood lake — the hoard, the drowning, the shades. Let the water rise first.");
            DomainPrompt = this.GetLocalization(nameof(DomainPrompt),
                () => "Hold the umbrella and press {0}");

            SinkTitle = this.GetLocalization(nameof(SinkTitle), () => "Sink Something");
            SinkBody = this.GetLocalization(nameof(SinkBody),
                () => "The lake is your private vault. Press the sink key with an item in hand and it sinks to the lakebed; point at a creature and the lake drags it under — drowned bosses become sunken shades, yours forever.");
            SinkPrompt = this.GetLocalization(nameof(SinkPrompt),
                () => "Once the water reaches your feet, press {0} holding an item or pointing at a foe");

            PanoramaTitle = this.GetLocalization(nameof(PanoramaTitle), () => "Open the Lakeheart");
            PanoramaBody = this.GetLocalization(nameof(PanoramaBody),
                () => "One screen holds it all: the hound and the gold flame up top, three seats of shades on the waterline, and the hoard on the lakebed below.");
            PanoramaPrompt = this.GetLocalization(nameof(PanoramaPrompt),
                () => "Click the wind chime at the bottom-left, or hold the umbrella and press {0}");

            WheelTitle = this.GetLocalization(nameof(WheelTitle), () => "Command the Shades");
            WheelBody = this.GetLocalization(nameof(WheelBody),
                () => "Seated shades surface on their own when the lake is ready. The wheel calls them out or holds them back mid-fight — fewer afield, harder each one hits.");
            WheelPrompt = this.GetLocalization(nameof(WheelPrompt),
                () => "Hold {0} to open the wheel, release on a seat to toggle it");
            WheelNoMemory = this.GetLocalization(nameof(WheelNoMemory),
                () => "No shades in the codex yet — drown a boss first, or skip this step.");

            DreamTitle = this.GetLocalization(nameof(DreamTitle), () => "Sink into the Ghost Dream");
            DreamBody = this.GetLocalization(nameof(DreamBody),
                () => "When the lake is full and its vigor past half, the reflection wakes on its own. Hold the mutate key and it pulls you under — hold left-click in the dream to call hounds, press again to return.");
            DreamPrompt = this.GetLocalization(nameof(DreamPrompt),
                () => "At full water, hold {0} (middle mouse by default)");

            SkipBtn = this.GetLocalization(nameof(SkipBtn), () => "Skip");
            KeyUnbound = this.GetLocalization(nameof(KeyUnbound),
                () => "The key for this step is unbound; bind it in Settings > Controls, or click skip.");
        }
        #endregion

        private const int CardW = 340;
        //约9秒卡住才出低调跳过；键未绑定时立即放出
        private const int StuckFramesBeforeSkip = 60 * 9;

        private static Phase currentPhase = Phase.Inactive;
        private static float animProgress;
        private static int phaseTimer;
        //Sink 步基线：进相位时的湖藏数与记忆
        private static int sinkBaselineCount;
        private static int sinkBaselineMemory;

        #region 引导排队协议
        int IGuideLead.GuidePriority => 12;//晚于比目鱼(10)，早于义体(15)
        bool IGuideLead.GuideReserving => Reserving;
        bool IGuideLead.GuideReady => Ready;
        //饥饿保底放弃时收尾记看过，否则占位会永久压制低优先级引导
        void IGuideLead.OnGuideAbandoned() => MarkSeen();
        #endregion

        private static KikasaGuideData Guide
            => Main.LocalPlayer.GetModPlayer<StoryPlayer>().Get<KikasaGuideData>();

        /// <summary>教学卡当前在讲：HUD 的提示行让位，别两处重复同一句按键话</summary>
        internal static bool CardVisible
            => Array.IndexOf(StepOrder, currentPhase) >= 0
            && GuideLeadQueue.IsHolder(ModContent.GetInstance<KikasaHudLead>());

        /// <summary>占位：存活 + 未看过 + 背包里有鬼伞</summary>
        private static bool Reserving {
            get {
                Player p = Main.LocalPlayer;
                if (p == null || !p.active) {
                    return false;
                }
                if (Guide.GuideSeen) {
                    return false;
                }
                return p.HasItem(ModContent.ItemType<KikasaItem>());
            }
        }

        /// <summary>就绪：占位 + 无对话/过场 + 会话可用</summary>
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

        /// <summary>会话可用；不满足只暂停不重置。湖心景开着不算占用，它本身是教学的一步</summary>
        private static bool SessionUsable() {
            Player p = Main.LocalPlayer;
            if (p == null || !p.active || p.dead) {
                return false;
            }
            if (HackTime.Active) {
                return false;
            }
            if (QuestLog.Instance?.IsOpen == true || QuestManagerUI.Instance?.IsOpen == true) {
                return false;
            }
            return true;
        }

        private static KikasaDomainPlayer Domain
            => Main.LocalPlayer.GetModPlayer<KikasaDomainPlayer>();

        private static void MarkSeen() {
            Guide.GuideSeen = true;
            currentPhase = Phase.Complete;
            animProgress = 0f;
        }

        private static void SetPhase(Phase phase) {
            currentPhase = phase;
            animProgress = 0f;
            phaseTimer = 0;
            if (phase == Phase.Sink) {
                Player p = Main.LocalPlayer;
                sinkBaselineCount = p.GetModPlayer<KikasaVaultPlayer>().Stored.Count;
                sinkBaselineMemory = p.GetModPlayer<KikasaServantPlayer>().LastDrownedType;
            }
        }

        /// <summary>跳到下一教学步；最后一步之后收尾</summary>
        private static void AdvanceStep() {
            int idx = Array.IndexOf(StepOrder, currentPhase);
            if (idx < 0 || idx >= StepOrder.Length - 1) {
                MarkSeen();
                return;
            }
            SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.3f, Volume = 0.5f });
            SetPhase(StepOrder[idx + 1]);
        }

        public override void OnWorldUnload() {
            currentPhase = Phase.Inactive;
            animProgress = 0f;
            phaseTimer = 0;
        }

        public override void UpdateUI(GameTime gameTime) {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            //统一排队、未轮到则待命，异常残留收起
            if (!GuideLeadQueue.IsHolder(this)) {
                if (currentPhase != Phase.Inactive && currentPhase != Phase.Complete) {
                    currentPhase = Phase.Inactive;
                    animProgress = 0f;
                }
                return;
            }

            if (currentPhase == Phase.Inactive) {
                SetPhase(Phase.Domain);
            }
            //前提暂时不满足（骇客/全屏 UI/死亡）：暂停推进与绘制，不回退
            if (!SessionUsable()) {
                return;
            }

            phaseTimer++;
            animProgress = MathHelper.Lerp(animProgress, 1f, 0.12f);

            KikasaDomainPlayer domain = Domain;
            //域中途收合：沉溺与入梦都指望湖在，退回第一步重讲；
            //湖心景与转盘不吃湖（任意湖态都开得了），不回退
            if ((currentPhase == Phase.Sink || currentPhase == Phase.Dream) && !domain.AnyActive) {
                SetPhase(Phase.Domain);
                return;
            }

            switch (currentPhase) {
                case Phase.Domain:
                    if (domain.AnyActive) {
                        AdvanceStep();
                    }
                    break;
                case Phase.Sink: {
                    Player p = Main.LocalPlayer;
                    bool sank = p.GetModPlayer<KikasaVaultPlayer>().Stored.Count > sinkBaselineCount;
                    bool drowned = p.GetModPlayer<KikasaServantPlayer>().LastDrownedType != sinkBaselineMemory;
                    if (sank || drowned) {
                        AdvanceStep();
                    }
                    break;
                }
                case Phase.Panorama:
                    if (KikasaPanoramaUI.Instance?.IsOpen == true) {
                        AdvanceStep();
                    }
                    break;
                case Phase.Wheel:
                    if (KikasaServantWheelController.LocalInstance?.IsOpen == true) {
                        AdvanceStep();
                    }
                    break;
                case Phase.Dream:
                    if (domain.InDreamPhase) {
                        SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.45f, Volume = 0.55f });
                        MarkSeen();
                    }
                    break;
            }
        }

        //==================== 绘制 ====================

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            if (Array.IndexOf(StepOrder, currentPhase) < 0) {
                return;
            }
            if (!GuideLeadQueue.IsHolder(this) || !SessionUsable() || animProgress < 0.02f) {
                return;
            }
            int idx = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
            if (idx == -1) {
                return;
            }
            layers.Insert(idx, new LegacyGameInterfaceLayer("CWRMod: Kikasa HUD Guide",
                delegate {
                    DrawOverlay(Main.spriteBatch);
                    return true;
                }, InterfaceScaleType.UI));
        }

        private static void DrawOverlay(SpriteBatch sb) {
            float alpha = MathHelper.Clamp(animProgress, 0f, 1f);
            float rain = MathHelper.Clamp(Domain.RainBlend, 0f, 1f);
            float time = Main.GlobalTimeWrappedHourly;

            //第一步给掌中风铃的铃身一个脉冲环：铃即领域的读数。伞没拿在手上时风铃不在场，环也不画；
            //第三步同样用脉冲环指路，点铃即开湖心景
            bool ringStep = currentPhase == Phase.Domain || currentPhase == Phase.Panorama;
            if (ringStep && KikasaHud.Instance?.Active == true) {
                float pulse = KikasaHudTheme.Breath(time, 1.3f, 3f);
                KikasaVaultRenderer.DrawRing(sb, KikasaHud.BellAnchor,
                    KikasaHudTheme.BellSize * 0.5f + 12f + pulse * 4f, 12f,
                    KikasaHudTheme.Glow(rain) * ((0.35f + pulse * 0.2f) * alpha));
            }

            //====== 键位与文案 ======
            //异化键有原生中键兜底，不算未绑定
            ModKeybind actionKey = currentPhase switch {
                Phase.Domain => CWRKeySystem.Legend_Domain,
                Phase.Sink => CWRKeySystem.Kikasa_Sink,
                Phase.Panorama => CWRKeySystem.Legend_UIControl,
                Phase.Wheel => CWRKeySystem.RadialWheel_Key,
                _ => CWRKeySystem.Kikasa_DomainMutate,
            };
            bool keyBound = actionKey == null || currentPhase == Phase.Dream
                || !CWRKeySystem.IsKeybindUnbound(actionKey);
            string keyText = actionKey == null
                ? string.Empty
                : actionKey.ToTooltipString(CWRKeySystem.Notbound.Value);

            (string title, string body, string promptFmt) = currentPhase switch {
                Phase.Domain => (DomainTitle.Value, DomainBody.Value, DomainPrompt.Value),
                Phase.Sink => (SinkTitle.Value, SinkBody.Value, SinkPrompt.Value),
                Phase.Panorama => (PanoramaTitle.Value, PanoramaBody.Value, PanoramaPrompt.Value),
                Phase.Wheel => (WheelTitle.Value, WheelBody.Value, WheelPrompt.Value),
                _ => (DreamTitle.Value, DreamBody.Value, DreamPrompt.Value),
            };
            string promptText = string.IsNullOrEmpty(keyText)
                ? promptFmt
                : string.Format(promptFmt, keyText);

            //转盘步死路：册里还没有沉影，开盘也只有空席可看
            bool wheelDeadEnd = currentPhase == Phase.Wheel
                && Main.LocalPlayer.GetModPlayer<KikasaServantPlayer>().BuildCodexKeys().Count == 0;
            string subText = !keyBound ? KeyUnbound.Value
                : wheelDeadEnd ? WheelNoMemory.Value : null;

            //====== 量高排版（字号跟全域字体规范：正文 ≥0.8） ======
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            const float titleSc = 0.95f;
            const float bodySc = 0.8f;
            const float subSc = 0.75f;
            float lineT = font.MeasureString("A").Y * titleSc + 2f;
            float lineB = font.MeasureString("A").Y * bodySc + 1f;

            int bodyWrapW = (int)((CardW - 28) / bodySc);
            List<string> bodyLines = WrapLines(font, body, bodyWrapW);
            List<string> promptLines = WrapLines(font, promptText, bodyWrapW);
            List<string> subLines = subText != null
                ? WrapLines(font, subText, (int)((CardW - 28) / subSc))
                : null;
            float cardH = 12f + lineT + 2f + 7f
                + bodyLines.Count * lineB + 4f
                + promptLines.Count * lineB
                + (subLines?.Count ?? 0) * (lineB - 1f)
                + 38f;

            //====== 卡位：默认悬在风铃上方；湖心景开着时让位到右上 ======
            float cardX, cardY;
            bool panoOpen = KikasaPanoramaUI.Instance?.IsOpen == true;
            if (panoOpen) {
                cardX = MathHelper.Clamp(KikasaHudTheme.UIScreenW - CardW - 20f,
                    16f, Math.Max(16f, KikasaHudTheme.UIScreenW - CardW - 16f));
                cardY = 78f;
            }
            else {
                Vector2 chime = KikasaHud.Anchor;
                cardX = MathHelper.Clamp(chime.X - 30f,
                    16f, Math.Max(16f, KikasaHudTheme.UIScreenW - CardW - 16f));
                cardY = MathHelper.Clamp(
                    chime.Y - (KikasaHudTheme.ChimeH * 0.5f + 10f) - cardH - 8f,
                    16f, Math.Max(16f, KikasaHudTheme.UIScreenH - cardH - 16f));
            }
            float slideY = (1f - alpha) * 16f;
            Rectangle card = new((int)cardX, (int)(cardY + slideY), CardW, (int)cardH);

            KikasaPanoramaRenderer.DrawCardBg(sb, card, alpha, rain);
            //连线：卡底垂到风铃檐钩顶；湖心景让位时不画，风铃不在场也不画
            if (!panoOpen && KikasaHud.Instance?.Active == true) {
                DrawDashedLine(sb, new Vector2(card.X + 26f, card.Bottom),
                    KikasaHud.Anchor + new Vector2(0f, -(KikasaHudTheme.ChimeH * 0.5f + 6f)),
                    KikasaHudTheme.Accent(rain) * (0.45f * alpha), time);
            }

            //====== 内容 ======
            float px = card.X + 14f;
            float py = card.Y + 12f;
            Color titleCol = KikasaHudTheme.Glow(rain);
            Color bodyCol = KikasaHudTheme.TextDim(rain);
            Color promptCol = KikasaHudTheme.Text(rain);
            Color accent = KikasaHudTheme.Accent(rain);

            int stepIdx = Array.IndexOf(StepOrder, currentPhase);
            string counter = $"{stepIdx + 1:00} / {StepOrder.Length:00}";
            float counterW = font.MeasureString(counter).X * subSc;
            Utils.DrawBorderString(sb, counter,
                new Vector2(card.Right - 14f - counterW, py),
                KikasaHudTheme.TextDim(rain) * (0.7f * alpha), subSc);
            Utils.DrawBorderString(sb, title, new Vector2(px, py), titleCol * alpha, titleSc);
            py += lineT + 2f;

            KikasaVaultRenderer.DrawLine(sb, new Vector2(px, py),
                new Vector2(px + CardW - 28f, py), 1f, accent * (0.4f * alpha));
            py += 7f;

            foreach (string wl in bodyLines) {
                Utils.DrawBorderString(sb, wl, new Vector2(px, py), bodyCol * (0.9f * alpha), bodySc);
                py += lineB;
            }
            py += 4f;

            float promptPulse = 0.8f + 0.2f * MathF.Sin(time * 4.2f);
            foreach (string wl in promptLines) {
                Utils.DrawBorderString(sb, wl, new Vector2(px, py),
                    promptCol * (alpha * promptPulse), bodySc);
                py += lineB;
            }

            if (subLines != null) {
                float pulseKey = 0.75f + 0.25f * MathF.Sin(time * 7f);
                foreach (string wl in subLines) {
                    Utils.DrawBorderString(sb, wl, new Vector2(px, py),
                        KikasaHudTheme.Glow(rain) * (0.85f * alpha * pulseKey), subSc);
                    py += lineB - 1f;
                }
            }

            //键未绑定/转盘死路时立即放出跳过，否则约9秒后才出
            if ((!keyBound || wheelDeadEnd || phaseTimer > StuckFramesBeforeSkip)
                && DrawSkipButton(sb, card, alpha, rain)) {
                AdvanceStep();
            }
        }

        private static List<string> WrapLines(DynamicSpriteFont font, string text, int wrapW) {
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

        //虚线连接线：卡片指向风铃
        private static void DrawDashedLine(SpriteBatch sb, Vector2 from, Vector2 to,
            Color color, float time) {
            Vector2 dir = to - from;
            float len = dir.Length();
            if (len < 8f) {
                return;
            }
            dir /= len;
            const float dash = 5f, gap = 4f;
            float offset = (time * 18f) % (dash + gap);
            for (float t = offset; t < len; t += dash + gap) {
                float end = MathF.Min(t + dash, len);
                KikasaVaultRenderer.DrawLine(sb, from + dir * t, from + dir * end, 1.2f, color);
            }
        }

        private static bool DrawSkipButton(SpriteBatch sb, Rectangle card, float alpha, float rain) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string label = SkipBtn.Value;
            const float sc = 0.75f;
            Vector2 size = font.MeasureString(label) * sc;
            int btnW = (int)size.X + 22;
            const int btnH = 22, margin = 10;
            Rectangle btn = new(card.Right - btnW - margin, card.Bottom - btnH - margin, btnW, btnH);

            Vector2 uiMouse = KikasaHudTheme.UIMouse;
            bool hovered = btn.Contains((int)uiMouse.X, (int)uiMouse.Y);
            Color bg = KikasaHudTheme.Deep(rain) * ((hovered ? 0.95f : 0.7f) * alpha);
            Color border = KikasaHudTheme.Accent(rain) * ((hovered ? 0.85f : 0.45f) * alpha);
            Color textCol = (hovered ? KikasaHudTheme.Text(rain) : KikasaHudTheme.TextDim(rain)) * alpha;

            sb.Draw(VaultAsset.placeholder2.Value, btn, bg);
            KikasaVaultRenderer.DrawLine(sb, new Vector2(btn.Left, btn.Top),
                new Vector2(btn.Right, btn.Top), 1f, border);
            KikasaVaultRenderer.DrawLine(sb, new Vector2(btn.Left, btn.Bottom),
                new Vector2(btn.Right, btn.Bottom), 1f, border * 0.7f);
            KikasaVaultRenderer.DrawLine(sb, new Vector2(btn.Left, btn.Top),
                new Vector2(btn.Left, btn.Bottom), 1f, border * 0.85f);
            KikasaVaultRenderer.DrawLine(sb, new Vector2(btn.Right, btn.Top),
                new Vector2(btn.Right, btn.Bottom), 1f, border * 0.85f);
            Vector2 textPos = btn.Center.ToVector2() - size * 0.5f;
            Utils.DrawBorderString(sb, label, textPos, textCol, sc);

            if (hovered) {
                Main.LocalPlayer.mouseInterface = true;
                if (Main.mouseLeft && Main.mouseLeftRelease) {
                    Main.mouseLeftRelease = false;
                    return true;
                }
            }
            return false;
        }
    }
}
