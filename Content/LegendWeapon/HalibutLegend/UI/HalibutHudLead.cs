using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.Atlas;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.SkillWheel;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.Narrative.Guides;
using CalamityOverhaul.Content.Scenarios.Helen;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    /// <summary>
    /// 大比目鱼界面引导：首次完成「初遇比目鱼」后，以"按键/操作占位"方式依次带玩家
    /// 打开图鉴 → 在研究祭坛投鱼研究 → 认识装备栏 → 亲手按键呼出一次技能转盘。
    /// 通过 <see cref="GuideLeadQueue"/> 统一排队，优先级高于委托引导，从初遇演出一开始即占位。
    /// </summary>
    internal class HalibutHudLead : ModSystem, ILocalizedModType, IGuideLead
    {
        public string LocalizationCategory => "Legend.HalibutText";

        private enum Phase
        {
            Inactive,
            HudIntro,
            Research,
            Equip,
            SkillWheel,
            Complete
        }

        #region 本地化
        //阶段1：深渊之眼 HUD
        public static LocalizedText HudTitle { get; private set; }
        public static LocalizedText HudBody { get; private set; }
        public static LocalizedText HudPrompt { get; private set; }
        public static LocalizedText HudOpenBtn { get; private set; }
        //阶段2：研究祭坛
        public static LocalizedText ResearchTitle { get; private set; }
        public static LocalizedText ResearchBody { get; private set; }
        public static LocalizedText ResearchPrompt { get; private set; }
        //阶段3：技能装备栏
        public static LocalizedText EquipTitle { get; private set; }
        public static LocalizedText EquipBody { get; private set; }
        public static LocalizedText EquipWaiting { get; private set; }
        //阶段4：技能转盘
        public static LocalizedText WheelTitle { get; private set; }
        public static LocalizedText WheelBody { get; private set; }
        public static LocalizedText WheelPrompt { get; private set; }
        //通用
        public static LocalizedText SkipBtn { get; private set; }

        public override void SetStaticDefaults() {
            GuideLeadQueue.Register(this);

            HudTitle = this.GetLocalization(nameof(HudTitle), () => "深渊之眼");
            HudBody = this.GetLocalization(nameof(HudBody), () => "手持大比目鱼时它常驻屏幕左下角，显示当前技能、深渊复苏与领域层数");
            HudPrompt = this.GetLocalization(nameof(HudPrompt), () => "左键点击眼睛，或按 {0} 打开深渊图鉴");
            HudOpenBtn = this.GetLocalization(nameof(HudOpenBtn), () => "打开图鉴");

            ResearchTitle = this.GetLocalization(nameof(ResearchTitle), () => "研究祭坛");
            ResearchBody = this.GetLocalization(nameof(ResearchBody), () => "把捕获的鱼投入祭坛研究，即可解锁对应的领域技能");
            ResearchPrompt = this.GetLocalization(nameof(ResearchPrompt), () => "点击高亮的研究祭坛，选一条鱼投入研究");

            EquipTitle = this.GetLocalization(nameof(EquipTitle), () => "技能装备栏");
            EquipBody = this.GetLocalization(nameof(EquipBody), () => "屏幕底部这排凹槽就是装备栏，研究好的技能会放入这里，至多 10 个");
            EquipWaiting = this.GetLocalization(nameof(EquipWaiting), () => "正在研究…完成后技能会自动装入装备栏");

            WheelTitle = this.GetLocalization(nameof(WheelTitle), () => "技能转盘");
            WheelBody = this.GetLocalization(nameof(WheelBody), () => "装备技能后，可在战斗中快速切换当前使用的技能");
            WheelPrompt = this.GetLocalization(nameof(WheelPrompt), () => "按住 {0} 呼出技能转盘试一次");

            SkipBtn = this.GetLocalization(nameof(SkipBtn), () => "跳过");
        }
        #endregion

        private static Phase currentPhase = Phase.Inactive;
        private static float animProgress;
        //当前阶段已停留帧数，用于"卡住一段时间后才出现跳过"
        private static int phaseTimer;
        //装备栏阶段：研究完成、技能入栏后的停留帧，给解锁演出留出时间再转入转盘环节
        private static int holdTimer;
        private const float AnimSpeed = 0.12f;
        //卡顿约 9 秒后才显示低调的"跳过"兜底，平时以行动推进为主
        private const int StuckFramesBeforeSkip = 60 * 9;
        //技能入栏后停留约 2.2 秒，让图鉴的解锁演出播完
        private const int EquipHoldFrames = 130;

        public override void OnWorldUnload() {
            currentPhase = Phase.Inactive;
            animProgress = 0f;
            phaseTimer = 0;
            holdTimer = 0;
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

        private static HalibutSave Save => Main.LocalPlayer.GetModPlayer<HalibutSave>();

        private static void MarkSeen() {
            Main.LocalPlayer.GetModPlayer<StoryPlayer>().Get<HalibutGuideData>().GuideSeen = true;
            currentPhase = Phase.Complete;
            //收尾，避免遗留打开的图鉴
            HalibutAtlas.Instance?.Close();
        }

        private static void SetPhase(Phase phase) {
            currentPhase = phase;
            animProgress = 0f;
            phaseTimer = 0;
            holdTimer = 0;
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

            phaseTimer++;
            switch (currentPhase) {
                case Phase.HudIntro:
                    UpdateHudIntro();
                    break;
                case Phase.Research:
                    UpdateResearch();
                    break;
                case Phase.Equip:
                    UpdateEquip();
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

        //打开图鉴（左键点眼睛 / 按键 / 助手按钮）后进入研究环节
        private static void UpdateHudIntro() {
            if (HalibutAtlas.Instance?.IsOpen == true) {
                SetPhase(Phase.Research);
            }
        }

        //引导玩家点击研究祭坛投鱼：开始研究或已有解锁即推进
        private static void UpdateResearch() {
            HalibutAtlas atlas = HalibutAtlas.Instance;
            if (atlas == null || !atlas.IsOpen) {
                //玩家关掉图鉴 → 退回引导其重新打开
                SetPhase(Phase.HudIntro);
                return;
            }
            HalibutSave save = Save;
            if (save.IsStudying || save.unlocked.Count > 0) {
                SetPhase(Phase.Equip);
            }
        }

        //介绍装备栏：研究完成自动装入（loadout 非空）后稍作停留，再转入转盘环节
        private static void UpdateEquip() {
            HalibutAtlas atlas = HalibutAtlas.Instance;
            if (atlas == null || !atlas.IsOpen) {
                SetPhase(Phase.HudIntro);
                return;
            }
            if (Save.loadout.Count > 0) {
                //留出图鉴解锁演出时间后再切换
                if (++holdTimer > EquipHoldFrames) {
                    StartSkillWheel();
                }
            }
            else {
                holdTimer = 0;
            }
        }

        //引导玩家亲手呼出一次技能转盘：转盘开启即完成
        private static void UpdateSkillWheel() {
            HalibutWheelController ctrl = HalibutWheelController.LocalInstance;
            if (ctrl != null && (ctrl.IsOpen || ctrl.OpenProgress > 0.3f)) {
                MarkSeen();
                return;
            }
            //该阶段聚焦转盘，确保图鉴保持关闭（否则转盘无法呼出）
            if (HalibutAtlas.Instance?.IsOpen == true) {
                HalibutAtlas.Instance.Close();
            }
        }

        private static void OpenAtlasAndAdvance() {
            HalibutAtlas atlas = HalibutAtlas.Instance;
            if (atlas != null && !atlas.IsOpen) {
                atlas.Open();
            }
            SetPhase(Phase.Research);
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
                case Phase.Research:
                    DrawResearch(sb, time);
                    break;
                case Phase.Equip:
                    DrawEquip(sb, time);
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

            const int cardW = 330, cardH = 152;
            float slide = (1f - ease) * 34f;
            float x = MathHelper.Clamp(eye.X + 62f - slide, 16f, HalibutTheme.UIScreenW - cardW - 16f);
            float y = MathHelper.Clamp(eye.Y - cardH - 22f, 16f, HalibutTheme.UIScreenH - cardH - 16f);
            var card = new Rectangle((int)x, (int)y, cardW, cardH);

            DrawCard(sb, card, HalibutTheme.Glow, 0.4f);
            DrawConnector(sb, new Vector2(card.X + 26f, card.Bottom), eye, a, time);

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            float px = card.X + 16f, py = card.Y + 13f, wrap = cardW - 32f;
            HalibutRenderer.DrawGlowText(sb, HudTitle.Value, new Vector2(px, py),
                HalibutTheme.GlowHi * a, HalibutTheme.Glow * (0.4f * a), 0.9f);
            py += 26f;
            DrawDivider(sb, px, py, cardW - 32, HalibutTheme.Glow, a);
            py += 8f;
            py = DrawBody(sb, font, HudBody.Value, px, py, wrap, 0.64f, HalibutTheme.TextDim, a);
            py += 3f;
            string openKey = CWRKeySystem.Legend_UIControl.ToTooltipString(CWRKeySystem.Notbound.Value);
            DrawBody(sb, font, string.Format(HudPrompt.Value, openKey), px, py, wrap, 0.68f, HalibutTheme.GlowHi, a);

            //"打开图鉴"助手按钮：等价于按键/点眼，始终可用
            if (DrawActionButton(sb, card, HudOpenBtn.Value, HalibutTheme.Glow, time)) {
                OpenAtlasAndAdvance();
            }
        }
        #endregion

        #region 阶段2：研究祭坛
        private static void DrawResearch(SpriteBatch sb, float time) {
            HalibutAtlas atlas = HalibutAtlas.Instance;
            if (atlas == null) {
                return;
            }
            float a = animProgress;
            bool panelOpen = atlas.AltarPanelOpen;
            bool altarVisible = !panelOpen && atlas.SeaViewActive;
            Vector2 altar = atlas.AltarCenter;

            if (altarVisible) {
                DrawTargetHighlight(sb, altar, AtlasStudyAltar.Radius + 6f, time, a);
            }

            const int cardW = 322, cardH = 150;
            float x, y;
            if (panelOpen) {
                //选鱼面板占屏时把卡片挪到右上角让位
                x = MathHelper.Clamp(HalibutTheme.UIScreenW - cardW - 20f, 16f, HalibutTheme.UIScreenW - cardW - 16f);
                y = 78f;
            }
            else {
                x = MathHelper.Clamp(altar.X - cardW * 0.5f, 16f, HalibutTheme.UIScreenW - cardW - 16f);
                y = MathHelper.Clamp(altar.Y + AtlasStudyAltar.Radius + 28f, 84f, HalibutTheme.UIScreenH - cardH - 16f);
            }
            var card = new Rectangle((int)x, (int)y, cardW, cardH);

            DrawCard(sb, card, HalibutTheme.Accent, 0.5f);
            if (altarVisible && card.Y > altar.Y) {
                DrawConnector(sb, new Vector2(card.Center.X, card.Y), altar, a, time);
            }

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            float px = card.X + 16f, py = card.Y + 13f, wrap = cardW - 32f;
            HalibutRenderer.DrawGlowText(sb, ResearchTitle.Value, new Vector2(px, py),
                HalibutTheme.Accent * a, HalibutTheme.Accent * (0.35f * a), 0.9f);
            py += 26f;
            DrawDivider(sb, px, py, cardW - 32, HalibutTheme.Accent, a);
            py += 8f;
            py = DrawBody(sb, font, ResearchBody.Value, px, py, wrap, 0.64f, HalibutTheme.TextDim, a);
            py += 3f;
            DrawBody(sb, font, ResearchPrompt.Value, px, py, wrap, 0.66f, HalibutTheme.GlowHi, a);

            if (phaseTimer > StuckFramesBeforeSkip && DrawActionButton(sb, card, SkipBtn.Value, HalibutTheme.TextDim, time)) {
                SetPhase(Phase.Equip);
            }
        }
        #endregion

        #region 阶段3：技能装备栏
        private static void DrawEquip(SpriteBatch sb, float time) {
            HalibutAtlas atlas = HalibutAtlas.Instance;
            if (atlas == null) {
                return;
            }
            float a = animProgress;
            HalibutSave save = Save;

            Rectangle dock = atlas.SeaViewActive ? atlas.DockBounds : Rectangle.Empty;
            if (dock.Width > 0) {
                DrawRegionHighlight(sb, dock, time, a);
            }

            const int cardW = 322, cardH = 152;
            float x = MathHelper.Clamp(HalibutTheme.UIScreenW - cardW - 24f, 16f, HalibutTheme.UIScreenW - cardW - 16f);
            float y = MathHelper.Clamp((HalibutTheme.UIScreenH - cardH) * 0.5f, 72f, HalibutTheme.UIScreenH - cardH - 16f);
            var card = new Rectangle((int)x, (int)y, cardW, cardH);

            DrawCard(sb, card, HalibutTheme.Accent, 0.55f);
            if (dock.Width > 0) {
                DrawConnector(sb, new Vector2(card.X + 24f, card.Bottom), new Vector2(dock.Center.X, dock.Top), a, time);
            }

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            float px = card.X + 16f, py = card.Y + 13f, wrap = cardW - 32f;
            HalibutRenderer.DrawGlowText(sb, EquipTitle.Value, new Vector2(px, py),
                HalibutTheme.Accent * a, HalibutTheme.Accent * (0.35f * a), 0.9f);
            py += 26f;
            DrawDivider(sb, px, py, cardW - 32, HalibutTheme.Accent, a);
            py += 8f;
            py = DrawBody(sb, font, EquipBody.Value, px, py, wrap, 0.64f, HalibutTheme.TextDim, a);
            py += 3f;
            //研究进行中：提示等待自动装入，不打扰、不显啰嗦
            if (save.IsStudying && save.loadout.Count == 0) {
                Color waitCol = Color.Lerp(HalibutTheme.GlowHi, HalibutTheme.Accent, HalibutTheme.Breath(time, 1f, 3f));
                DrawBody(sb, font, EquipWaiting.Value, px, py, wrap, 0.66f, waitCol, a);
            }

            //仅在没有进行中研究、尚无技能入栏、且确实卡住时给跳过兜底（入栏后会自动转入下一步）
            if (!save.IsStudying && save.loadout.Count == 0 && phaseTimer > StuckFramesBeforeSkip
                && DrawActionButton(sb, card, SkipBtn.Value, HalibutTheme.TextDim, time)) {
                StartSkillWheel();
            }
        }
        #endregion

        #region 阶段4：技能转盘
        private static void DrawSkillWheel(SpriteBatch sb, float time) {
            float a = animProgress;
            Vector2 center = new(HalibutTheme.UIScreenW * 0.5f,
                HalibutTheme.UIScreenH * HalibutTheme.WheelAnchorYRatio);

            DrawWheelHint(sb, center, time, a);

            const int cardW = 340, cardH = 152;
            var card = new Rectangle((int)(center.X - cardW * 0.5f), (int)(center.Y - cardH * 0.5f), cardW, cardH);

            DrawCard(sb, card, HalibutTheme.GlowHi, 0.7f);

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            float px = card.X + 16f, py = card.Y + 14f, wrap = cardW - 32f;
            string wheelKey = CWRKeySystem.Halibut_SkillWheel.ToTooltipString(CWRKeySystem.Notbound.Value);

            HalibutRenderer.DrawGlowText(sb, WheelTitle.Value, new Vector2(px, py),
                HalibutTheme.GlowHi * a, HalibutTheme.Glow * (0.4f * a), 0.92f);
            py += 27f;
            DrawDivider(sb, px, py, cardW - 32, HalibutTheme.GlowHi, a);
            py += 8f;
            py = DrawBody(sb, font, WheelBody.Value, px, py, wrap, 0.64f, HalibutTheme.TextDim, a);
            py += 3f;
            DrawBody(sb, font, string.Format(WheelPrompt.Value, wheelKey), px, py, wrap, 0.7f, HalibutTheme.GlowHi, a);

            if (phaseTimer > StuckFramesBeforeSkip && DrawActionButton(sb, card, SkipBtn.Value, HalibutTheme.TextDim, time)) {
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

        //右下角小按钮（助手/跳过），返回是否被点击
        private static bool DrawActionButton(SpriteBatch sb, Rectangle card, string text, Color accent, float time) {
            const int btnW = 98, btnH = 24;
            var rect = new Rectangle(card.Right - btnW - 12, card.Bottom - btnH - 11, btnW, btnH);
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
