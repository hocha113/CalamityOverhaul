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
    /// 比目鱼界面引导、初遇后按键占位串图鉴→祭坛→装备栏→转盘
    /// 经 <see cref="GuideLeadQueue"/> 排队，高于委托，初遇演出起占位
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
        //阶段1、深渊之眼HUD
        public static LocalizedText HudTitle { get; private set; }
        public static LocalizedText HudBody { get; private set; }
        public static LocalizedText HudPrompt { get; private set; }
        public static LocalizedText HudOpenBtn { get; private set; }
        //阶段2、研究祭坛
        public static LocalizedText ResearchTitle { get; private set; }
        public static LocalizedText ResearchBody { get; private set; }
        public static LocalizedText ResearchPrompt { get; private set; }
        //阶段3、技能装备栏
        public static LocalizedText EquipTitle { get; private set; }
        public static LocalizedText EquipBody { get; private set; }
        public static LocalizedText EquipWaiting { get; private set; }
        //阶段4、技能转盘
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
        //当前阶段停留帧，卡住一段时间后才出跳过
        private static int phaseTimer;
        //装备栏停留帧、解锁演完再进转盘
        private static int holdTimer;
        private const float AnimSpeed = 0.12f;
        //约9秒卡住才出低调跳过，平时靠行动推进
        private const int StuckFramesBeforeSkip = 60 * 9;
        //入栏后约2.2秒，等图鉴解锁演完
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
        /// 占位、有鱼+已FirstMet（OnTriggered即置）+未看过
        /// 演出起占队列，压委托引导
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
        /// 就绪、占位+手持鱼+初遇演完+无对话过场
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

            //统一排队、未轮到则待命，异常残留收起
            if (!GuideLeadQueue.IsHolder(this)) {
                if (currentPhase != Phase.Inactive && currentPhase != Phase.Complete) {
                    currentPhase = Phase.Inactive;
                    animProgress = 0f;
                }
                return;
            }

            //轮到本引导（就绪才授）、未开始则起步
            if (currentPhase == Phase.Inactive) {
                SetPhase(Phase.HudIntro);
            }
            //未手持/已死时暂停推进与绘制，不重置
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
            //已装备却抢呼转盘则跳到转盘收尾
            if (HalibutWheelController.LocalInstance?.IsOpen == true) {
                SetPhase(Phase.SkillWheel);
                return;
            }
            if (HalibutAtlas.Instance?.IsOpen == true) {
                SetPhase(Phase.Research);
            }
        }

        //点祭坛投鱼、开研究或已解锁即推进
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

        //介绍装备栏、入栏后稍停再转盘
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

        //亲手呼转盘、开启即完成
        private static void UpdateSkillWheel() {
            HalibutWheelController ctrl = HalibutWheelController.LocalInstance;
            if (ctrl != null && (ctrl.IsOpen || ctrl.OpenProgress > 0.3f)) {
                MarkSeen();
                return;
            }
            //转盘阶段关图鉴，否则呼不出
            if (HalibutAtlas.Instance?.IsOpen == true) {
                HalibutAtlas.Instance.Close();
            }
        }

        private static void OpenAtlasAndAdvance() {
            HalibutAtlas atlas = HalibutAtlas.Instance;
            if (atlas == null) {
                return;//图鉴不可用则不前进，留在本阶段
            }
            if (!atlas.IsOpen) {
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
            //暂停态不绘制，避免脱离HUD语境的悬浮卡
            if (!StillActive()) {
                return;
            }
            //插在原版鼠标文本层前，盖过UIHandle
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

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            const int cardW = 336;
            float contentW = cardW - 32f;
            string openKey = CWRKeySystem.Legend_UIControl.ToTooltipString(CWRKeySystem.Notbound.Value);
            GLine[] body = {
                new(HudBody.Value, 0.74f, HalibutTheme.TextDim),
                new(string.Format(HudPrompt.Value, openKey), 0.78f, HalibutTheme.GlowHi),
            };
            int cardH = MeasureCardH(font, 0.9f, body, contentW);

            float slide = (1f - ease) * 34f;
            float x = MathHelper.Clamp(eye.X + 62f - slide, 16f, HalibutTheme.UIScreenW - cardW - 16f);
            float y = MathHelper.Clamp(eye.Y - cardH - 22f, 16f, HalibutTheme.UIScreenH - cardH - 16f);
            var card = new Rectangle((int)x, (int)y, cardW, cardH);

            DrawCard(sb, card, HalibutTheme.Glow, 0.4f);
            DrawConnector(sb, new Vector2(card.X + 26f, card.Bottom), eye, a, time);
            DrawCardContent(sb, font, card, HudTitle.Value, 0.9f, HalibutTheme.GlowHi, HalibutTheme.Glow, body, a);

            //打开图鉴助手钮、等价按键/点眼
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
            Vector2 altar = atlas.AltarCenter;
            //祭坛不可见/切领域/选鱼占屏时不画高亮连线
            bool altarVisible = !panelOpen && atlas.SeaViewActive
                && altar.Y > 60f && altar.Y < HalibutTheme.UIScreenH - 40f;

            if (altarVisible) {
                DrawTargetHighlight(sb, altar, AtlasStudyAltar.Radius + 6f, time, a);
            }

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            const int cardW = 330;
            float contentW = cardW - 32f;
            GLine[] body = {
                new(ResearchBody.Value, 0.74f, HalibutTheme.TextDim),
                new(ResearchPrompt.Value, 0.78f, HalibutTheme.GlowHi),
            };
            int cardH = MeasureCardH(font, 0.9f, body, contentW);

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
            DrawCardContent(sb, font, card, ResearchTitle.Value, 0.9f, HalibutTheme.Accent, HalibutTheme.Accent, body, a);

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
            //装备坞收起/切视图后不画高亮连线
            bool dockVisible = dock.Width > 0 && dock.Top > 40f && dock.Top < HalibutTheme.UIScreenH;
            if (dockVisible) {
                DrawRegionHighlight(sb, dock, time, a);
            }

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            const int cardW = 330;
            float contentW = cardW - 32f;
            bool waiting = save.IsStudying && save.loadout.Count == 0;
            GLine[] body = waiting
                ? new GLine[] {
                    new(EquipBody.Value, 0.74f, HalibutTheme.TextDim),
                    new(EquipWaiting.Value, 0.76f, Color.Lerp(HalibutTheme.GlowHi, HalibutTheme.Accent, HalibutTheme.Breath(time, 1f, 3f))),
                }
                : new GLine[] {
                    new(EquipBody.Value, 0.74f, HalibutTheme.TextDim),
                };
            int cardH = MeasureCardH(font, 0.9f, body, contentW);

            float x = MathHelper.Clamp(HalibutTheme.UIScreenW - cardW - 24f, 16f, HalibutTheme.UIScreenW - cardW - 16f);
            float y = MathHelper.Clamp((HalibutTheme.UIScreenH - cardH) * 0.5f, 72f, HalibutTheme.UIScreenH - cardH - 16f);
            var card = new Rectangle((int)x, (int)y, cardW, cardH);

            DrawCard(sb, card, HalibutTheme.Accent, 0.55f);
            if (dockVisible) {
                DrawConnector(sb, new Vector2(card.X + 24f, card.Bottom), new Vector2(dock.Center.X, dock.Top), a, time);
            }
            DrawCardContent(sb, font, card, EquipTitle.Value, 0.9f, HalibutTheme.Accent, HalibutTheme.Accent, body, a);

            //无研究/无入栏且卡住时才给跳过（入栏会自动下一步）
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

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            const int cardW = 348;
            float contentW = cardW - 32f;
            string wheelKey = CWRKeySystem.Halibut_SkillWheel.ToTooltipString(CWRKeySystem.Notbound.Value);
            GLine[] body = {
                new(WheelBody.Value, 0.74f, HalibutTheme.TextDim),
                new(string.Format(WheelPrompt.Value, wheelKey), 0.8f, HalibutTheme.GlowHi),
            };
            int cardH = MeasureCardH(font, 0.92f, body, contentW);
            var card = new Rectangle((int)(center.X - cardW * 0.5f), (int)(center.Y - cardH * 0.5f), cardW, cardH);

            DrawCard(sb, card, HalibutTheme.GlowHi, 0.7f);
            DrawCardContent(sb, font, card, WheelTitle.Value, 0.92f, HalibutTheme.GlowHi, HalibutTheme.GlowHi, body, a);

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

        //一段卡片正文行
        private readonly struct GLine
        {
            public readonly string Text;
            public readonly float Scale;
            public readonly Color Color;
            public GLine(string text, float scale, Color color) {
                Text = text; Scale = scale; Color = color;
            }
        }

        //按实际换行测量一段文字的高度
        private static float MeasureWrapH(DynamicSpriteFont font, string text, float scale, float wrapPx) {
            if (string.IsNullOrEmpty(text)) {
                return 0f;
            }
            int wrapW = Math.Max(8, (int)(wrapPx / scale));
            int n = 0;
            foreach (string s in VaultUtils.WrapTextArray(text, font, wrapW, 99, out _)) {
                if (!string.IsNullOrEmpty(s)) n++;
            }
            return Math.Max(n, 1) * (font.MeasureString("A").Y * scale + 3f);
        }

        //按标题+正文算卡片高，防大字体/英文换行溢出
        private static int MeasureCardH(DynamicSpriteFont font, float titleScale, GLine[] body, float contentW) {
            float la = font.MeasureString("A").Y;
            float h = 13f + (la * titleScale + 8f) + 8f;//顶距 + 标题 + 分割线
            foreach (GLine gl in body) {
                h += MeasureWrapH(font, gl.Text, gl.Scale, contentW) + 4f;
            }
            return (int)MathF.Ceiling(h + 40f);//底部按钮预留
        }

        //绘制卡片标题 + 分割线 + 正文（与 MeasureCardH 对齐）
        private static void DrawCardContent(SpriteBatch sb, DynamicSpriteFont font, Rectangle card,
            string title, float titleScale, Color titleColor, Color accent, GLine[] body, float a) {
            float px = card.X + 16f, py = card.Y + 13f, wrap = card.Width - 32f;
            HalibutRenderer.DrawGlowText(sb, title, new Vector2(px, py), titleColor * a, accent * (0.4f * a), titleScale);
            py += font.MeasureString("A").Y * titleScale + 8f;
            DrawDivider(sb, px, py, card.Width - 32, accent, a);
            py += 8f;
            foreach (GLine gl in body) {
                py = DrawBody(sb, font, gl.Text, px, py, wrap, gl.Scale, gl.Color, a) + 4f;
            }
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
