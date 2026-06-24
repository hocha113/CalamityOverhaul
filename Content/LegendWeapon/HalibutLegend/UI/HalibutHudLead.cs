using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.Atlas;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.Narrative.Guides;
using CalamityOverhaul.Content.Scenarios.Helen;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    /// <summary>
    /// 大比目鱼界面引导：首次完成「初遇比目鱼」后，依次介绍深渊之眼 HUD、技能装备栏与技能转盘
    /// （转盘只能用快捷键呼出，必须显式告知，否则极易被玩家全程忽略）
    /// 通过 <see cref="GuideLeadQueue"/> 统一排队：本引导优先级高于委托引导，且从初遇演出一开始就占位，
    /// 因此委托引导只会在本引导结束后才登场，无需两边互相引用
    /// </summary>
    internal class HalibutHudLead : ModSystem, ILocalizedModType, IGuideLead
    {
        public string LocalizationCategory => "Legend.HalibutText";

        private enum Phase
        {
            Inactive,
            HudIntro,
            AtlasEquip,
            SkillWheel,
            Complete
        }

        #region 本地化
        //阶段1：深渊之眼 HUD
        public static LocalizedText HudTitle { get; private set; }
        public static LocalizedText HudLine1 { get; private set; }
        public static LocalizedText HudLine2 { get; private set; }
        public static LocalizedText HudOpenPrompt { get; private set; }
        public static LocalizedText HudOpenBtn { get; private set; }
        //阶段2：技能海域与装备栏
        public static LocalizedText AtlasTitle { get; private set; }
        public static LocalizedText AtlasLine1 { get; private set; }
        public static LocalizedText AtlasLine2 { get; private set; }
        public static LocalizedText AtlasLine3 { get; private set; }
        public static LocalizedText AtlasDockLabel { get; private set; }
        public static LocalizedText AtlasNextBtn { get; private set; }
        //阶段3：技能转盘（仅快捷键）
        public static LocalizedText WheelTitle { get; private set; }
        public static LocalizedText WheelLine1 { get; private set; }
        public static LocalizedText WheelLine2 { get; private set; }
        public static LocalizedText WheelLine3 { get; private set; }
        public static LocalizedText WheelWarn { get; private set; }
        public static LocalizedText WheelDoneBtn { get; private set; }

        public override void SetStaticDefaults() {
            GuideLeadQueue.Register(this);
            HudTitle = this.GetLocalization(nameof(HudTitle), () => "深渊之眼");
            HudLine1 = this.GetLocalization(nameof(HudLine1), () => "手持大比目鱼时，这只眼会常驻在屏幕左下角");
            HudLine2 = this.GetLocalization(nameof(HudLine2), () => "它显示当前选用的领域技能、深渊复苏进度与领域层数");
            HudOpenPrompt = this.GetLocalization(nameof(HudOpenPrompt), () => "左键点击眼睛，或按 {0} 打开「深渊图鉴」");
            HudOpenBtn = this.GetLocalization(nameof(HudOpenBtn), () => "打开图鉴");

            AtlasTitle = this.GetLocalization(nameof(AtlasTitle), () => "技能海域 与 装备栏");
            AtlasLine1 = this.GetLocalization(nameof(AtlasLine1), () => "顶部是研究祭坛：投入捕获的鱼，研究完成即可点亮对应技能");
            AtlasLine2 = this.GetLocalization(nameof(AtlasLine2), () => "屏幕底部这一排凹槽就是「装备栏」，最多放入 10 个技能");
            AtlasLine3 = this.GetLocalization(nameof(AtlasLine3), () => "长按拖拽技能到装备栏，或在技能详情卡中点击「装备」");
            AtlasDockLabel = this.GetLocalization(nameof(AtlasDockLabel), () => "装 备 栏");
            AtlasNextBtn = this.GetLocalization(nameof(AtlasNextBtn), () => "下一步");

            WheelTitle = this.GetLocalization(nameof(WheelTitle), () => "技能转盘 · 仅可快捷键呼出");
            WheelLine1 = this.GetLocalization(nameof(WheelLine1), () => "战斗中按住 {0} 呼出技能转盘，从装备栏快速切换当前技能");
            WheelLine2 = this.GetLocalization(nameof(WheelLine2), () => "移动光标到对应扇区、松开按键即可选定，右键取消");
            WheelLine3 = this.GetLocalization(nameof(WheelLine3), () => "装备栏为空时转盘不会响应——记得先在祭坛研究并装备技能");
            WheelWarn = this.GetLocalization(nameof(WheelWarn), () => "切记：转盘只能用快捷键呼出，界面上没有任何按钮——别让它被遗忘");
            WheelDoneBtn = this.GetLocalization(nameof(WheelDoneBtn), () => "我记住了");
        }
        #endregion

        private static Phase currentPhase = Phase.Inactive;
        private static float animProgress;
        //本引导进行/待触发期间应让委托引导回避
        private const float AnimSpeed = 0.12f;

        public override void OnWorldUnload() {
            currentPhase = Phase.Inactive;
            animProgress = 0f;
        }

        #region 引导排队协议
        int IGuideLead.GuidePriority => 10;//先于委托引导
        bool IGuideLead.GuideReserving => Reserving;
        bool IGuideLead.GuideReady => Ready;
        //保底被放弃时直接收尾，停止占位
        void IGuideLead.OnGuideAbandoned() => MarkSeen();

        /// <summary>
        /// 占位条件：拥有比目鱼、已触发初遇（FirstMet 在 OnTriggered 即置位，早于演出结束）、尚未看过。
        /// 从演出一开始就占住队列，压制委托引导抢先。
        /// </summary>
        private static bool Reserving {
            get {
                Player p = Main.LocalPlayer;
                if (p == null || !p.active) {
                    return false;
                }
                if (HasSeen) {
                    return false;
                }
                if (!p.TryGetOverride<HalibutPlayer>(out var hp) || !hp.HasHalubut) {
                    return false;
                }
                return HalibutStorySync.ReadHalibut(d => d.FirstMet, d => d.FirstMet);
            }
        }

        /// <summary>
        /// 就绪条件：占位之上，手持比目鱼、初遇已演完、无对话/过场干扰
        /// </summary>
        private static bool Ready {
            get {
                if (!Reserving) {
                    return false;
                }
                if (NarrativeTriggerGate.IsBusy || InnoVault.Cinematics.CutsceneDirector.IsPlaying) {
                    return false;
                }
                if (!StillActive()) {
                    return false;
                }
                return HalibutStorySync.ReadHalibut(d => d.PostFirstMetIsComplete, d => d.PostFirstMetIsComplete);
            }
        }
        #endregion

        private static bool HasSeen
            => Main.LocalPlayer.GetModPlayer<StoryPlayer>().Get<HalibutGuideData>().GuideSeen;

        //手持比目鱼且存活，HUD 在场，引导才有意义
        private static bool StillActive() {
            Player p = Main.LocalPlayer;
            return p != null && p.active && !p.dead
                && p.TryGetOverride<HalibutPlayer>(out var hp) && hp.HeldHalibut;
        }

        private static void MarkSeen() {
            Main.LocalPlayer.GetModPlayer<StoryPlayer>().Get<HalibutGuideData>().GuideSeen = true;
            currentPhase = Phase.Complete;
            //收尾，避免遗留打开的图鉴
            HalibutAtlas.Instance?.Close();
        }

        private static void SetPhase(Phase phase) {
            currentPhase = phase;
            animProgress = 0f;
        }

        public override void UpdateUI(GameTime gameTime) {
            if (Main.gameMenu) {
                return;
            }

            //统一排队：未轮到本引导则按兵不动（异常残留则收起）
            if (!GuideLeadQueue.IsHolder(this)) {
                if (currentPhase != Phase.Inactive && currentPhase != Phase.Complete) {
                    currentPhase = Phase.Inactive;
                    animProgress = 0f;
                }
                return;
            }

            //轮到本引导（队列仅在就绪时授予）：未开始则起步
            if (currentPhase == Phase.Inactive) {
                SetPhase(Phase.HudIntro);
            }
            //暂时不可见（未手持/已死）时暂停推进与绘制，不重置，等恢复
            if (!StillActive()) {
                return;
            }

            switch (currentPhase) {
                case Phase.HudIntro:
                    UpdateHudIntro();
                    break;
                case Phase.AtlasEquip:
                    UpdateAtlasEquip();
                    break;
                case Phase.SkillWheel:
                    UpdateSkillWheel();
                    break;
                case Phase.Complete:
                    break;
            }

            if (currentPhase != Phase.Inactive && currentPhase != Phase.Complete) {
                animProgress = MathHelper.Lerp(animProgress, 1f, AnimSpeed);
            }
        }

        private static void UpdateHudIntro() {
            //玩家自行打开了图鉴（左键点眼睛 / 按键）→ 进入装备栏介绍
            if (HalibutAtlas.Instance?.IsOpen == true) {
                SetPhase(Phase.AtlasEquip);
            }
        }

        private static void UpdateAtlasEquip() {
            //玩家把图鉴关掉 → 退回上一步，重新引导其打开
            if (HalibutAtlas.Instance == null || !HalibutAtlas.Instance.IsOpen) {
                SetPhase(Phase.HudIntro);
            }
        }

        private static void UpdateSkillWheel() {
            //该阶段聚焦转盘，确保图鉴保持关闭
            if (HalibutAtlas.Instance?.IsOpen == true) {
                HalibutAtlas.Instance.Close();
            }
        }

        private static void OpenAtlasAndAdvance() {
            HalibutAtlas atlas = HalibutAtlas.Instance;
            if (atlas != null && !atlas.IsOpen) {
                atlas.Open();
            }
            SetPhase(Phase.AtlasEquip);
        }

        private static void StartSkillWheel() {
            HalibutAtlas.Instance?.Close();
            SetPhase(Phase.SkillWheel);
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            if (currentPhase == Phase.Inactive || currentPhase == Phase.Complete) {
                return;
            }
            //暂停态（未手持比目鱼/已死）不绘制，避免脱离 HUD 语境的悬浮卡
            if (!StillActive()) {
                return;
            }
            //插在原版鼠标文本层之前，从而绘制在所有 UIHandle（HUD/图鉴/转盘）之上
            int idx = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
            if (idx == -1) {
                return;
            }
            layers.Insert(idx, new LegacyGameInterfaceLayer(
                "CWRMod: Halibut HUD Guide",
                delegate {
                    DrawCurrent(Main.spriteBatch);
                    return true;
                },
                InterfaceScaleType.UI));
        }

        private static void DrawCurrent(SpriteBatch sb) {
            float time = Main.GlobalTimeWrappedHourly;
            switch (currentPhase) {
                case Phase.HudIntro:
                    DrawHudIntro(sb, time);
                    break;
                case Phase.AtlasEquip:
                    DrawAtlasEquip(sb, time);
                    break;
                case Phase.SkillWheel:
                    DrawSkillWheel(sb, time);
                    break;
            }
        }

        #region 阶段1：深渊之眼
        private static void DrawHudIntro(SpriteBatch sb, float time) {
            float a = animProgress;
            float ease = VaultUtils.EaseOutCubic(a);
            Vector2 eye = HalibutHud.Anchor;

            DrawTargetHighlight(sb, eye, 30f, time, a);

            const int cardW = 340, cardH = 188;
            float slide = (1f - ease) * 34f;
            float x = MathHelper.Clamp(eye.X + 62f - slide, 16f, HalibutTheme.UIScreenW - cardW - 16f);
            float y = MathHelper.Clamp(eye.Y - cardH - 22f, 16f, HalibutTheme.UIScreenH - cardH - 16f);
            var card = new Rectangle((int)x, (int)y, cardW, cardH);

            DrawCard(sb, card, HalibutTheme.Glow, 0.4f);
            DrawConnector(sb, new Vector2(card.X + 26f, card.Bottom), eye, a, time);

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            float px = card.X + 16f, py = card.Y + 14f, wrap = cardW - 32f;

            HalibutRenderer.DrawGlowText(sb, HudTitle.Value, new Vector2(px, py),
                HalibutTheme.GlowHi * a, HalibutTheme.Glow * (0.4f * a), 0.92f);
            py += 27f;
            DrawDivider(sb, px, py, cardW - 32, HalibutTheme.Glow, a);
            py += 9f;

            py = DrawBody(sb, font, HudLine1.Value, px, py, wrap, 0.66f, HalibutTheme.Text, a);
            py = DrawBody(sb, font, HudLine2.Value, px, py, wrap, 0.66f, HalibutTheme.TextDim, a);
            py += 5f;
            string openKey = CWRKeySystem.Legend_UIControl.ToTooltipString(CWRKeySystem.Notbound.Value);
            DrawBody(sb, font, string.Format(HudOpenPrompt.Value, openKey), px, py, wrap, 0.68f, HalibutTheme.GlowHi, a);

            if (DrawButton(sb, card, HudOpenBtn.Value, HalibutTheme.Glow, time)) {
                OpenAtlasAndAdvance();
            }
        }
        #endregion

        #region 阶段2：技能海域与装备栏
        private static void DrawAtlasEquip(SpriteBatch sb, float time) {
            HalibutAtlas atlas = HalibutAtlas.Instance;
            if (atlas == null) {
                return;
            }
            float a = animProgress;
            float ease = VaultUtils.EaseOutCubic(a);

            Rectangle dock = atlas.SeaViewActive ? atlas.DockBounds : Rectangle.Empty;
            if (dock.Width > 0) {
                DrawRegionHighlight(sb, dock, time, a);
                HalibutRenderer.DrawGlowTextCentered(sb, AtlasDockLabel.Value,
                    new Vector2(dock.Center.X, dock.Top - 15f),
                    HalibutTheme.Accent * a, HalibutTheme.Deep * (0.4f * a), 0.78f);
            }

            const int cardW = 322, cardH = 198;
            float slide = (1f - ease) * 40f;
            float x = MathHelper.Clamp(HalibutTheme.UIScreenW - cardW - 24f + slide,
                16f, HalibutTheme.UIScreenW - cardW - 16f);
            float y = MathHelper.Clamp((HalibutTheme.UIScreenH - cardH) * 0.5f,
                72f, HalibutTheme.UIScreenH - cardH - 16f);
            var card = new Rectangle((int)x, (int)y, cardW, cardH);

            DrawCard(sb, card, HalibutTheme.Accent, 0.55f);
            if (dock.Width > 0) {
                DrawConnector(sb, new Vector2(card.X + 24f, card.Bottom),
                    new Vector2(dock.Center.X, dock.Top), a, time);
            }

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            float px = card.X + 16f, py = card.Y + 14f, wrap = cardW - 32f;

            HalibutRenderer.DrawGlowText(sb, AtlasTitle.Value, new Vector2(px, py),
                HalibutTheme.Accent * a, HalibutTheme.Accent * (0.35f * a), 0.92f);
            py += 27f;
            DrawDivider(sb, px, py, cardW - 32, HalibutTheme.Accent, a);
            py += 9f;

            py = DrawBody(sb, font, AtlasLine1.Value, px, py, wrap, 0.64f, HalibutTheme.Text, a);
            py = DrawBody(sb, font, AtlasLine2.Value, px, py, wrap, 0.64f, HalibutTheme.GlowHi, a);
            DrawBody(sb, font, AtlasLine3.Value, px, py, wrap, 0.64f, HalibutTheme.TextDim, a);

            if (DrawButton(sb, card, AtlasNextBtn.Value, HalibutTheme.Glow, time)) {
                StartSkillWheel();
            }
        }
        #endregion

        #region 阶段3：技能转盘
        private static void DrawSkillWheel(SpriteBatch sb, float time) {
            float a = animProgress;
            Vector2 center = new(HalibutTheme.UIScreenW * 0.5f,
                HalibutTheme.UIScreenH * HalibutTheme.WheelAnchorYRatio);

            DrawWheelHint(sb, center, time, a);

            const int cardW = 436, cardH = 240;
            var card = new Rectangle((int)(center.X - cardW * 0.5f), (int)(center.Y - cardH * 0.5f), cardW, cardH);

            DrawCard(sb, card, HalibutTheme.GlowHi, 0.7f);

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            float px = card.X + 18f, py = card.Y + 15f, wrap = cardW - 36f;
            string wheelKey = CWRKeySystem.Halibut_SkillWheel.ToTooltipString(CWRKeySystem.Notbound.Value);

            HalibutRenderer.DrawGlowText(sb, WheelTitle.Value, new Vector2(px, py),
                HalibutTheme.GlowHi * a, HalibutTheme.Glow * (0.4f * a), 0.94f);
            py += 28f;
            DrawDivider(sb, px, py, cardW - 36, HalibutTheme.GlowHi, a);
            py += 9f;

            py = DrawBody(sb, font, string.Format(WheelLine1.Value, wheelKey), px, py, wrap, 0.66f, HalibutTheme.Text, a);
            py = DrawBody(sb, font, WheelLine2.Value, px, py, wrap, 0.66f, HalibutTheme.TextDim, a);
            py = DrawBody(sb, font, WheelLine3.Value, px, py, wrap, 0.66f, HalibutTheme.Text, a);
            py += 4f;
            Color warnCol = Color.Lerp(HalibutTheme.Accent, HalibutTheme.Caustic, HalibutTheme.Breath(time, 2f, 4f));
            DrawBody(sb, font, WheelWarn.Value, px, py, wrap, 0.66f, warnCol, a);

            if (DrawButton(sb, card, WheelDoneBtn.Value, HalibutTheme.GlowHi, time)) {
                MarkSeen();
            }
        }

        //示意转盘形态的旋转环（非真实转盘，仅作引导背景）
        private static void DrawWheelHint(SpriteBatch sb, Vector2 center, float time, float a) {
            float ease = VaultUtils.EaseOutCubic(a);
            HalibutRenderer.DrawSoftGlow(sb, center, 230f * ease, HalibutTheme.Mid * (0.45f * a));
            HalibutRenderer.DrawRing(sb, center, 206f * ease, 1.6f, HalibutTheme.Glow * (0.35f * a));
            HalibutRenderer.DrawRing(sb, center, 150f * ease, 1.2f, HalibutTheme.Teal * (0.6f * a));
            float rot = time * 0.5f;
            for (int i = 0; i < 6; i++) {
                float a0 = rot + i * MathHelper.TwoPi / 6f;
                HalibutRenderer.DrawArcStroke(sb, center, 218f * ease, a0, a0 + 0.42f, 1.4f,
                    HalibutTheme.GlowHi * (0.45f * a));
            }
        }
        #endregion

        #region 通用绘制
        private static void DrawCard(SpriteBatch sb, Rectangle card, Color accent, float depth) {
            float a = animProgress;
            HalibutRenderer.DrawSeaPanel(sb, card, a, depth, 0f, 0.6f);
            HalibutRenderer.DrawOrnateFrame(sb, card, Color.Lerp(accent, HalibutTheme.Glow, 0.35f),
                a * 0.95f, Main.GlobalTimeWrappedHourly, 12f);
        }

        //返回换行后新的 y
        private static float DrawBody(SpriteBatch sb, DynamicSpriteFont font, string text,
            float x, float y, float wrapPx, float scale, Color color, float a) {
            if (string.IsNullOrEmpty(text)) {
                return y;
            }
            string[] wrapped = VaultUtils.WrapTextArray(text, font, (int)(wrapPx / scale), 99, out _);
            float lineH = font.MeasureString("A").Y * scale + 3f;
            foreach (string wl in wrapped) {
                if (string.IsNullOrEmpty(wl)) {
                    continue;
                }
                string line = wl.TrimEnd('-', ' ');
                Utils.DrawBorderString(sb, line, new Vector2(x + 1f, y + 1f), Color.Black * (0.5f * a), scale);
                Utils.DrawBorderString(sb, line, new Vector2(x, y), color * a, scale);
                y += lineH;
            }
            return y;
        }

        private static bool DrawButton(SpriteBatch sb, Rectangle card, string text, Color accent, float time) {
            const int btnW = 132, btnH = 28;
            var rect = new Rectangle((int)(card.Center.X - btnW * 0.5f), card.Bottom - btnH - 12, btnW, btnH);
            bool hovered = rect.Contains(HalibutTheme.UIMouse.ToPoint());
            HalibutRenderer.DrawCapsuleButton(sb, rect, text, accent, hovered, false, animProgress, time);
            if (hovered) {
                Main.LocalPlayer.mouseInterface = true;
                if (Main.mouseLeft && Main.mouseLeftRelease) {
                    Main.mouseLeftRelease = false;
                    return true;
                }
            }
            return false;
        }

        private static void DrawDivider(SpriteBatch sb, float x, float y, float w, Color col, float a) {
            HalibutRenderer.DrawPearl(sb, new Vector2(x + 1.5f, y), 1.7f, HalibutTheme.Caustic, 0.85f * a);
            HalibutRenderer.DrawGradientLine(sb, new Vector2(x + 6f, y), new Vector2(x + w, y),
                col * (0.6f * a), col * (0.04f * a), 1.2f);
        }

        private static void DrawTargetHighlight(SpriteBatch sb, Vector2 center, float radius, float time, float a) {
            float pulse = HalibutTheme.Breath(time, 1.3f, 3f);
            HalibutRenderer.DrawRing(sb, center, radius + pulse * 4f, 1.6f,
                HalibutTheme.GlowHi * ((0.5f + pulse * 0.3f) * a));
            HalibutRenderer.DrawRing(sb, center, radius + 8f + pulse * 5f, 1f,
                HalibutTheme.Glow * ((0.25f + pulse * 0.2f) * a));
        }

        private static void DrawRegionHighlight(SpriteBatch sb, Rectangle rect, float time, float a) {
            float pulse = HalibutTheme.Breath(time, 0.7f, 3f);
            Rectangle r = rect;
            r.Inflate(4, 4);
            HalibutRenderer.DrawDashedRectBorder(sb, r,
                HalibutTheme.Accent * ((0.7f + pulse * 0.3f) * a), 1.6f, 7f, 5f, time * -26f);
            r.Inflate(3, 3);
            HalibutRenderer.DrawDashedRectBorder(sb, r,
                HalibutTheme.Accent * (0.2f * a), 1f, 7f, 5f, time * -26f);
        }

        private static void DrawConnector(SpriteBatch sb, Vector2 from, Vector2 to, float a, float time) {
            HalibutRenderer.DrawDashedLine(sb, from, to, HalibutTheme.Glow * (0.5f * a),
                1.2f, 5f, 4f, time * -18f);
            HalibutRenderer.DrawDisc(sb, to, 2f, 1.4f, HalibutTheme.Caustic * (0.8f * a));
        }
        #endregion
    }
}
