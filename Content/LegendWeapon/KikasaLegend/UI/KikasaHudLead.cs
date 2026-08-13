using CalamityOverhaul.Common;
using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
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
    /// 鬼伞五步引导：首次持伞后串起 开域 → 沉入 → 湖窗 → 鬼奴 → 异化。
    /// 卡片底板走 KikasaHud.fx 的 TechCard 湿纸技法，与水鏡同一张皮；
    /// 推进全靠玩家真实操作，键未绑定或卡住时放出跳过。
    /// 经 <see cref="GuideLeadQueue"/> 排队，晚于比目鱼(10)、早于义体(15)
    /// </summary>
    internal class KikasaHudLead : ModSystem, ILocalizedModType, IGuideLead
    {
        public string LocalizationCategory => "Legend.KikasaText";

        private enum Phase { Inactive, Domain, Sink, VaultWin, Summon, Mutate, Complete }

        //五个教学步的相位序，计数与跳过推进共用
        private static readonly Phase[] StepOrder =
            [Phase.Domain, Phase.Sink, Phase.VaultWin, Phase.Summon, Phase.Mutate];

        #region 本地化
        public static LocalizedText DomainTitle { get; private set; }
        public static LocalizedText DomainBody { get; private set; }
        public static LocalizedText DomainPrompt { get; private set; }
        public static LocalizedText SinkTitle { get; private set; }
        public static LocalizedText SinkBody { get; private set; }
        public static LocalizedText SinkPrompt { get; private set; }
        public static LocalizedText VaultTitle { get; private set; }
        public static LocalizedText VaultBody { get; private set; }
        public static LocalizedText VaultPrompt { get; private set; }
        public static LocalizedText SummonTitle { get; private set; }
        public static LocalizedText SummonBody { get; private set; }
        public static LocalizedText SummonPrompt { get; private set; }
        public static LocalizedText SummonNoMemory { get; private set; }
        public static LocalizedText MutateTitle { get; private set; }
        public static LocalizedText MutateBody { get; private set; }
        public static LocalizedText MutatePrompt { get; private set; }
        public static LocalizedText SkipBtn { get; private set; }
        public static LocalizedText KeyUnbound { get; private set; }

        public override void SetStaticDefaults() {
            GuideLeadQueue.Register(this);

            DomainTitle = this.GetLocalization(nameof(DomainTitle), () => "撑开血湖");
            DomainBody = this.GetLocalization(nameof(DomainBody),
                () => "鬼伞的一切都长在血湖领域里——湖藏、沉溺、鬼奴，都得先让湖涨起来。");
            DomainPrompt = this.GetLocalization(nameof(DomainPrompt), () => "手持鬼伞按 {0}");

            SinkTitle = this.GetLocalization(nameof(SinkTitle), () => "沉入湖藏");
            SinkBody = this.GetLocalization(nameof(SinkBody),
                () => "湖是你的私库。手里拿着东西按沉入键，它就沉进湖底；光标指着活物按同一个键，湖会伸手把它按下去。");
            SinkPrompt = this.GetLocalization(nameof(SinkPrompt),
                () => "等水涨到脚边，持物或指着生物按 {0}");

            VaultTitle = this.GetLocalization(nameof(VaultTitle), () => "开湖窗取物");
            VaultBody = this.GetLocalization(nameof(VaultBody),
                () => "沉下去的东西悬在血水里漂着。持鬼伞开湖窗，点一件，湖把它送回你手边。");
            VaultPrompt = this.GetLocalization(nameof(VaultPrompt), () => "持鬼伞按 {0}，或点击左下水鏡");

            SummonTitle = this.GetLocalization(nameof(SummonTitle), () => "驱使鬼奴");
            SummonBody = this.GetLocalization(nameof(SummonBody),
                () => "湖永远记着最后一只溺死在它手里的生物。召出它的鬼奴替你出手，再按一次遣返。");
            SummonPrompt = this.GetLocalization(nameof(SummonPrompt), () => "按 {0}");
            SummonNoMemory = this.GetLocalization(nameof(SummonNoMemory),
                () => "湖还没记住能驱使的活物——先沉一只 boss，或跳过这步。");

            MutateTitle = this.GetLocalization(nameof(MutateTitle), () => "鬼雨异化");
            MutateBody = this.GetLocalization(nameof(MutateBody),
                () => "血湖有表里两面。满水位时倒转它，翻进冷雨那一侧；再倒转一次就翻回来。");
            MutatePrompt = this.GetLocalization(nameof(MutatePrompt), () => "按 {0}（默认鼠标中键）");

            SkipBtn = this.GetLocalization(nameof(SkipBtn), () => "跳过");
            KeyUnbound = this.GetLocalization(nameof(KeyUnbound),
                () => "这一步的按键未绑定；请先在 设置 > 控制 里绑定，或点击跳过。");
        }
        #endregion

        private const int CardW = 340;
        private const int EdgePad = 8;
        //约9秒卡住才出低调跳过；键未绑定时立即放出
        private const int StuckFramesBeforeSkip = 60 * 9;

        private static Phase currentPhase = Phase.Inactive;
        private static float animProgress;
        private static int phaseTimer;
        private static float shaderTimer;
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

        /// <summary>教学卡当前在讲：HUD 的干湖提示行让位，别两处重复同一句按键话</summary>
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

        /// <summary>会话可用；不满足只暂停不重置。湖窗开着不算占用——它本身是教学的一步</summary>
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
                SetPhase(Phase.Domain);
            }
            //前提暂时不满足（骇客/全屏 UI/死亡）：暂停推进与绘制，不回退
            if (!SessionUsable()) {
                return;
            }

            phaseTimer++;
            animProgress = MathHelper.Lerp(animProgress, 1f, 0.12f);

            KikasaDomainPlayer domain = Domain;
            //域中途收合：后续步全指望湖在，退回第一步重讲
            if (currentPhase != Phase.Domain && !domain.AnyActive) {
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
                case Phase.VaultWin:
                    if (KikasaVaultUI.Instance?.IsOpen == true) {
                        AdvanceStep();
                    }
                    break;
                case Phase.Summon:
                    if (Main.LocalPlayer.GetModPlayer<KikasaServantPlayer>().FindActiveServant() != null) {
                        AdvanceStep();
                    }
                    break;
                case Phase.Mutate:
                    if (domain.Phase == KikasaDomainPhase.Flipping || domain.IsRainForm) {
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

            //第一步给水鏡一个脉冲环：领域的读数都长在这。伞没拿在手上时鏡不在场，环也不画
            if (currentPhase == Phase.Domain && KikasaHud.Instance?.Active == true) {
                Vector2 mirror = KikasaHud.Anchor;
                float pulse = KikasaHudTheme.Breath(time, 1.3f, 3f);
                KikasaVaultRenderer.DrawRing(sb, mirror,
                    KikasaHudTheme.RimHalfW + 14f + pulse * 5f, 18f,
                    KikasaHudTheme.Glow(rain) * ((0.35f + pulse * 0.2f) * alpha));
            }

            //====== 键位与文案 ======
            ModKeybind actionKey = currentPhase switch {
                Phase.Domain => CWRKeySystem.Legend_Domain,
                Phase.Sink => CWRKeySystem.Kikasa_Sink,
                Phase.VaultWin => CWRKeySystem.Legend_UIControl,
                Phase.Summon => CWRKeySystem.Kikasa_Summon,
                _ => CWRKeySystem.Kikasa_DomainMutate,
            };
            //异化键有原生中键兜底，不算未绑定
            bool keyBound = currentPhase == Phase.Mutate
                || !CWRKeySystem.IsKeybindUnbound(actionKey);
            string keyText = actionKey.ToTooltipString(CWRKeySystem.Notbound.Value);

            (string title, string body, string promptFmt) = currentPhase switch {
                Phase.Domain => (DomainTitle.Value, DomainBody.Value, DomainPrompt.Value),
                Phase.Sink => (SinkTitle.Value, SinkBody.Value, SinkPrompt.Value),
                Phase.VaultWin => (VaultTitle.Value, VaultBody.Value, VaultPrompt.Value),
                Phase.Summon => (SummonTitle.Value, SummonBody.Value, SummonPrompt.Value),
                _ => (MutateTitle.Value, MutateBody.Value, MutatePrompt.Value),
            };
            string promptText = string.Format(promptFmt, keyText);

            //召唤步的死路提示：湖没记住可驱使的生物时给出路
            int memory = Main.LocalPlayer.GetModPlayer<KikasaServantPlayer>().LastDrownedType;
            bool summonDeadEnd = currentPhase == Phase.Summon
                && (memory <= 0 || !KikasaServantIndex.TryGet(memory, out _));
            string subText = !keyBound ? KeyUnbound.Value
                : summonDeadEnd ? SummonNoMemory.Value : null;

            //====== 量高排版 ======
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            const float titleSc = 0.84f;
            const float bodySc = 0.70f;
            const float subSc = 0.58f;
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

            //====== 卡位：默认悬在水鏡上方；湖窗开着时让位到右上 ======
            float cardX, cardY;
            bool vaultOpen = KikasaVaultUI.Instance?.IsOpen == true;
            if (vaultOpen) {
                cardX = MathHelper.Clamp(KikasaHudTheme.UIScreenW - CardW - 20f,
                    16f, Math.Max(16f, KikasaHudTheme.UIScreenW - CardW - 16f));
                cardY = 78f;
            }
            else {
                Vector2 mirror = KikasaHud.Anchor;
                cardX = MathHelper.Clamp(mirror.X - 30f,
                    16f, Math.Max(16f, KikasaHudTheme.UIScreenW - CardW - 16f));
                cardY = MathHelper.Clamp(mirror.Y - 78f - cardH - 20f,
                    16f, Math.Max(16f, KikasaHudTheme.UIScreenH - cardH - 16f));
            }
            float slideY = (1f - alpha) * 16f;
            Rectangle card = new((int)cardX, (int)(cardY + slideY), CardW, (int)cardH);

            DrawCardBg(sb, card, alpha, rain);
            //连线：卡底垂到水鏡顶；湖窗模式不画，鏡不在场（伞没拿手上）也不画
            if (!vaultOpen && KikasaHud.Instance?.Active == true) {
                DrawDashedLine(sb, new Vector2(card.X + 26f, card.Bottom),
                    KikasaHud.Anchor + new Vector2(0f, -(KikasaHudTheme.MirrorH * 0.5f + 4f)),
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

            //键未绑定/召唤死路时立即放出跳过，否则约9秒后才出
            if ((!keyBound || summonDeadEnd || phaseTimer > StuckFramesBeforeSkip)
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

        //湿纸卡底：KikasaHud.fx TechCard；缺编回退平底 + 边线
        private static void DrawCardBg(SpriteBatch sb, Rectangle card, float alpha, float rain) {
            Effect effect = EffectLoader.KikasaHud?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect != null && noise != null && effect.Techniques["TechCard"] != null) {
                Rectangle ext = card;
                ext.Inflate(EdgePad, EdgePad);
                effect.CurrentTechnique = effect.Techniques["TechCard"];
                effect.Parameters["uTime"]?.SetValue(shaderTimer);
                effect.Parameters["uAlpha"]?.SetValue(alpha * 0.96f);
                effect.Parameters["uResolution"]?.SetValue(new Vector2(ext.Width, ext.Height));
                effect.Parameters["uTear"]?.SetValue(alpha);
                effect.Parameters["uRain"]?.SetValue(rain);
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.LinearClamp, DepthStencilState.None,
                    RasterizerState.CullNone, effect, Main.UIScaleMatrix);
                Main.instance.GraphicsDevice.Textures[1] = noise;
                Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
                sb.Draw(VaultAsset.placeholder2.Value, ext, Color.White);
                KikasaVaultRenderer.RestoreUIBatch(sb);
            }
            else {
                sb.Draw(VaultAsset.placeholder2.Value, card,
                    KikasaHudTheme.Void(rain) * (0.9f * alpha));
                Color edge = KikasaHudTheme.Accent(rain) * (0.5f * alpha);
                KikasaVaultRenderer.DrawLine(sb, new Vector2(card.Left, card.Top),
                    new Vector2(card.Right, card.Top), 1f, edge);
                KikasaVaultRenderer.DrawLine(sb, new Vector2(card.Left, card.Bottom),
                    new Vector2(card.Right, card.Bottom), 1f, edge * 0.7f);
                KikasaVaultRenderer.DrawLine(sb, new Vector2(card.Left, card.Top),
                    new Vector2(card.Left, card.Bottom), 1f, edge * 0.85f);
                KikasaVaultRenderer.DrawLine(sb, new Vector2(card.Right, card.Top),
                    new Vector2(card.Right, card.Bottom), 1f, edge * 0.85f);
            }
        }

        //虚线连接线：卡片指向水鏡
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
            const float sc = 0.62f;
            Vector2 size = font.MeasureString(label) * sc;
            int btnW = (int)size.X + 22;
            const int btnH = 20, margin = 10;
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
