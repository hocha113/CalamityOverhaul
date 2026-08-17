using CalamityOverhaul.Common;
using CalamityOverhaul.Content.TimeFreezes;
using CalamityOverhaul.Content.Wraiths.Core;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 结印盘：役鬼工位，接管上梁原属点鬼簿的那一钩。<br/>
    /// 外环六芒摆六只鬼，内三角三个结印位，三边是两两组合，心是三鬼合鬼。<br/>
    /// 盘座下缘的卷槽里插着点鬼簿——那是图鉴，抽出来只能看，结印仍只在这盘上做
    /// </summary>
    internal sealed class OniSigilUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.OnikiriText";
        public static OniSigilUI Instance => UIHandleLoader.GetUIHandleOfType<OniSigilUI>();

        private const string FreezeReason = "OniSigil";

        #region 本地化
        public static LocalizedText TitleText { get; private set; }
        public static LocalizedText StatusFormat { get; private set; }
        public static LocalizedText CostFormat { get; private set; }
        public static LocalizedText SlotEmptyHint { get; private set; }
        public static LocalizedText SlotFullHint { get; private set; }
        public static LocalizedText PickHint { get; private set; }
        public static LocalizedText UnbindHint { get; private set; }
        public static LocalizedText PendingText { get; private set; }
        public static LocalizedText BoundFormat { get; private set; }
        public static LocalizedText UnboundFormat { get; private set; }
        public static LocalizedText DangerNote { get; private set; }
        public static LocalizedText CloseTagText { get; private set; }
        public static LocalizedText CloseHintFormat { get; private set; }
        public static LocalizedText MeiTabText { get; private set; }
        public static LocalizedText MeiTabHint { get; private set; }
        public static LocalizedText RegisterTabText { get; private set; }
        public static LocalizedText RegisterTabHint { get; private set; }

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "结 印 盘");
            StatusFormat = this.GetLocalization(nameof(StatusFormat),
                () => "结印 {0} / {1} · 侵蚀 {2}%");
            CostFormat = this.GetLocalization(nameof(CostFormat),
                () => "每次役使 复苏 +{0}% · 侵蚀 +{1}%");
            SlotEmptyHint = this.GetLocalization(nameof(SlotEmptyHint),
                () => "点外环选一只鬼，再点空位结印。空位不启用任何能力");
            SlotFullHint = this.GetLocalization(nameof(SlotFullHint),
                () => "三位已满。点其中一位可换成选中的鬼，再点一次卸下");
            PickHint = this.GetLocalization(nameof(PickHint),
                () => "「{0}」已选中 · 点三角上任一位结印");
            UnbindHint = this.GetLocalization(nameof(UnbindHint), () => "点击 卸下");
            PendingText = this.GetLocalization(nameof(PendingText), () => "候 令");
            BoundFormat = this.GetLocalization(nameof(BoundFormat), () => "「{0}」已入结印位");
            UnboundFormat = this.GetLocalization(nameof(UnboundFormat), () => "「{0}」已卸下");
            DangerNote = this.GetLocalization(nameof(DangerNote), () => "将醒 · 慎役");
            CloseTagText = this.GetLocalization(nameof(CloseTagText), () => "收 盘");
            CloseHintFormat = this.GetLocalization(nameof(CloseHintFormat),
                () => "ESC · {0} · 点击盘外 收盘");
            MeiTabText = this.GetLocalization(nameof(MeiTabText), () => "改铭台");
            MeiTabHint = this.GetLocalization(nameof(MeiTabHint), () => "点击 移步");
            RegisterTabText = this.GetLocalization(nameof(RegisterTabText), () => "点鬼簿");
            RegisterTabHint = this.GetLocalization(nameof(RegisterTabHint), () => "点击 展读");
        }
        #endregion

        public override bool CloseOnEscape => true;
        public override float RenderPriority => 2f;
        public override SoundStyle? OpenSound => SoundID.MenuOpen with { Pitch = -0.5f, Volume = 0.55f };
        public override SoundStyle? CloseSound => SilentSwap ? null
            : SoundID.MenuClose with { Pitch = -0.32f, Volume = 0.5f };
        public override Vector2 MousePosition => OnikiriUITheme.UIMouse;

        /// <summary>姊妹屏/图鉴互斥收盘时置位：抑制本屏关闭音，切换只响一声</summary>
        internal bool SilentSwap;

        //====会话绑定：结印是写操作，必须证明手上这把刀还是开屏那把====
        private int interactionSession;
        private int sourceInventorySlot = -1;
        private long sourceInstanceId;
        private int pendingSlot = -1;

        //====交互状态====
        private OniSigilWheel wheel;
        private int selectedNode = -1;
        private int hoverNode = -1;
        private int hoverSlot = -1;
        private int hoverEdge = -1;
        private bool hoverCore;
        private readonly float[] nodeHover = new float[OniSigilWheel.NodeCount];
        private readonly float[] slotHover = new float[OniSigilWheel.SlotCount];
        //收盘木牌，点击关闭，牌绳 Verlet
        private Rectangle closeTagRect;
        private float closeTagHover;
        private bool closeTagWasHovered;
        private Vector2 closeTagAnchor;
        private readonly OniRope closeTagRope = new(5, 22f);
        //吊挂太刀：去改铭台的门
        private readonly OniHangingSwitch meiSwitch = new(SoundID.Unlock with { Pitch = 0.3f, Volume = 0.35f });
        private Vector2 meiSwitchAnchor;
        //盘座下缘的卷槽：点鬼簿插在这里，悬停抽出一截
        private Rectangle nicheRect;
        private float nicheHover;
        private bool nicheWasHovered;

        //====动画状态====
        internal float ShaderTime;
        private float appearEase;

        /// <summary>盘外径；顶梁要靠它算太刀钩的夹持位，故对外公开</summary>
        internal static float BodyRadius(float screenW, float screenH)
            => OniSigilWheel.BodyRadius(screenW, screenH);

        public override void OnEnterWorld() {
            if (IsOpen) {
                Close();
            }
            SnapOpenProgress();
        }

        protected override void OnOpen() {
            interactionSession++;
            CaptureSourceItem();
            Main.playerInventory = false;
            OniTalismanHud.RememberLedger(OniLedgerView.Sigil);
            //姊妹屏互斥：一盘开一台收；静默收台，免得开音+关音同帧叠成两声
            if (OniMeiUI.Instance?.IsOpen ?? false) {
                OniMeiUI.Instance.SilentSwap = true;
                OniMeiUI.Instance.Close();
                OniMeiUI.Instance.SilentSwap = false;
            }
            //两本图鉴各自归位即可，它们不占工位
            if (OniMeiCodexUI.Instance?.IsOpen ?? false) {
                OniMeiCodexUI.Instance.Close();
            }
            meiSwitch.Reset();
            selectedNode = -1;
            pendingSlot = -1;
            appearEase = 0f;
            Array.Clear(nodeHover, 0, nodeHover.Length);
            Array.Clear(slotHover, 0, slotHover.Length);
            LayoutCompute();
            closeTagRope.WarmStart(closeTagAnchor);
            if (VaultUtils.isSinglePlayer) {
                WorldFreezeSystem.Activate(FreezeReason);
            }
        }

        protected override void OnClose() {
            interactionSession++;
            sourceInventorySlot = -1;
            sourceInstanceId = 0;
            pendingSlot = -1;
            if (VaultUtils.isSinglePlayer) {
                WorldFreezeSystem.Deactivate(FreezeReason);
            }
        }

        #region 会话绑定
        private void CaptureSourceItem() {
            Player local = Main.LocalPlayer;
            sourceInventorySlot = local?.selectedItem ?? -1;
            Item item = SourceItem();
            sourceInstanceId = OnikiriData.TryGet(item)?.InstanceId ?? 0;
            if (sourceInstanceId == 0) {
                sourceInventorySlot = -1;
            }
        }

        internal Item SourceItem() {
            Player local = Main.LocalPlayer;
            if (local == null || sourceInventorySlot < 0
                || sourceInventorySlot >= PlayerItemSlotID.InventoryMouseItem
                || sourceInventorySlot >= local.inventory.Length) {
                return null;
            }
            return local.inventory[sourceInventorySlot];
        }

        private bool MaintainSourceItem() {
            Player local = Main.LocalPlayer;
            Item item = SourceItem();
            if (local == null || !local.active || local.dead
                || local.selectedItem != sourceInventorySlot
                || !ReferenceEquals(local.HeldItem, item)
                || sourceInstanceId == 0
                || OnikiriData.TryGet(item)?.InstanceId != sourceInstanceId) {
                Close();
                return false;
            }
            return true;
        }
        #endregion

        public override void Update() {
            if (IsOpen) {
                if (!MaintainSourceItem()) {
                    return;
                }
                player.mouseInterface = true;
            }
        }

        public override void LogicUpdate() {
            if (IsOpen) {
                if (!MaintainSourceItem()) {
                    return;
                }
                player.mouseInterface = true;
                UIInputGuard.SuppressWeaponSwitch();
                if (!player.active || player.dead) {
                    Close();
                }
            }

            float a = OpenProgress;
            if (a < 0.01f) {
                return;
            }
            ShaderTime += 1f / 60f;
            appearEase = MathHelper.Clamp(appearEase + 0.06f, 0f, 1f);

            LayoutCompute();

            //收盘牌摆：绳受风，牌是末端配重
            closeTagRope.Update(closeTagAnchor, null, ShaderTime, 0.26f, endWeight: 0.55f);
            Vector2 tagTop = closeTagRope.End;
            closeTagRect = new Rectangle((int)(tagTop.X - 16f), (int)tagTop.Y - 2, 32, 48);

            //吊挂太刀：点击预演到帧即发起换乘；换乘中挂起交互；驿牌并入命中
            if (meiSwitch.Update(meiSwitchAnchor, MousePosition,
                IsOpen && a > 0.9f && !OniLedgerSwapFX.Running,
                ShaderTime, OnikiriUITheme.HangTachiHit, keyLeftPressState,
                echoBoost: OniRegistry.IsEquippedInDanger,
                OniLedgerBeam.DoorBoardHit(OniLedgerView.Sigil))) {
                OniLedgerSwapFX.Begin(OniLedgerView.Mei);
            }

            UpdateInteraction(a);
        }

        private void LayoutCompute() {
            float sw = OnikiriUITheme.UIScreenW;
            float sh = OnikiriUITheme.UIScreenH;
            float radius = OniSigilWheel.BodyRadius(sw, sh);
            //换乘横滑：盘体随行进让位，顶梁与门挂物不加
            float slide = OniLedgerSwapFX.SlideOf(OniLedgerView.Sigil);
            Vector2 center = new(sw * 0.5f + slide,
                OniLedgerBeam.Height + 26f + radius + (1f - appearEase) * 18f);
            wheel = new OniSigilWheel(center, radius);

            //卷槽凿在盘座下缘：入口是盘的一部分，不是盘旁另摆一块板
            nicheRect = new Rectangle((int)(center.X - 44f),
                (int)(center.Y + radius * 1.02f - 6f), 88, 62);

            closeTagAnchor = new Vector2(center.X + radius + 30f,
                OniLedgerBeam.Height - 2f);
            meiSwitchAnchor = OniLedgerBeam.DoorAnchor(OniLedgerView.Sigil);
        }

        private void UpdateInteraction(float a) {
            bool inputAvailable = IsOpen && a > 0.9f && !OniLedgerSwapFX.Running;
            Vector2 mouse = MousePosition;
            Point mp = mouse.ToPoint();

            int newNode = -1;
            int newSlot = -1;
            hoverCore = false;
            if (inputAvailable) {
                if (wheel.HitNode(mouse, out int node)) {
                    newNode = node;
                }
                else if (wheel.HitSlot(mouse, out int slot)) {
                    newSlot = slot;
                }
                else {
                    hoverCore = wheel.HitCore(mouse);
                }
            }
            if (newNode != hoverNode || newSlot != hoverSlot) {
                if (newNode >= 0 || newSlot >= 0) {
                    SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.25f, Volume = 0.3f });
                }
                hoverNode = newNode;
                hoverSlot = newSlot;
            }
            for (int i = 0; i < nodeHover.Length; i++) {
                float target = i == hoverNode ? 1f : 0f;
                nodeHover[i] += (target - nodeHover[i]) * (target > nodeHover[i] ? 0.22f : 0.12f);
            }
            for (int i = 0; i < slotHover.Length; i++) {
                float target = i == hoverSlot ? 1f : 0f;
                slotHover[i] += (target - slotHover[i]) * (target > slotHover[i] ? 0.22f : 0.12f);
            }
            hoverEdge = inputAvailable && hoverNode < 0 && hoverSlot < 0 ? ResolveHoverEdge(mouse) : -1;

            //教程焦点：第一个结印位（教的是"往哪结"）
            if (Tutorial.OnikiriTutorialLead.IsActive) {
                Vector2 slot0 = wheel.SlotPos(0);
                int hit = (int)wheel.SlotHit;
                Tutorial.OnikiriTutorialTargets.Publish(Tutorial.OnikiriTutorialTargets.Tag_SigilSlot,
                    new Rectangle((int)slot0.X - hit, (int)slot0.Y - hit, hit * 2, hit * 2));
            }

            //收盘牌 hover：拂过时给绳一记横向冲量
            bool tagHovered = inputAvailable && closeTagRect.Contains(mp);
            closeTagHover += ((tagHovered ? 1f : 0f) - closeTagHover) * 0.2f;
            if (tagHovered && !closeTagWasHovered) {
                closeTagRope.Nudge(Main.rand.NextFloat(0.8f, 1.5f) * (Main.rand.NextBool() ? 1f : -1f));
            }
            closeTagWasHovered = tagHovered;

            //卷槽：悬停即抽书，不是图标变亮
            bool nicheHovered = inputAvailable && nicheRect.Contains(mp);
            nicheHover += ((nicheHovered ? 1f : 0f) - nicheHover) * 0.18f;
            if (nicheHovered && !nicheWasHovered) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.1f, Volume = 0.3f });
            }
            nicheWasHovered = nicheHovered;

            if (!inputAvailable || keyLeftPressState != KeyPressState.Pressed) {
                return;
            }
            if (tagHovered) {
                Close();
                return;
            }
            if (nicheHovered) {
                OniRegisterUI.OpenFromSigil();
                return;
            }
            if (hoverNode >= 0) {
                SelectNode(hoverNode);
                return;
            }
            if (hoverSlot >= 0) {
                ClickSlot(hoverSlot);
                return;
            }
            //点盘外收盘
            if (!wheel.HitBoard(mouse) && !meiSwitch.Hovering && !nicheRect.Contains(mp)) {
                Close();
            }
        }

        /// <summary>光标落在三角哪条边上（供底行读说明）</summary>
        private int ResolveHoverEdge(Vector2 mouse) {
            for (int e = 0; e < 3; e++) {
                (int a, int b) = OniSigilWheel.EdgeSlots(e);
                Vector2 p0 = wheel.SlotPos(a);
                Vector2 p1 = wheel.SlotPos(b);
                Vector2 seg = p1 - p0;
                float lenSq = seg.LengthSquared();
                if (lenSq < 1f) {
                    continue;
                }
                float t = MathHelper.Clamp(Vector2.Dot(mouse - p0, seg) / lenSq, 0f, 1f);
                if (Vector2.DistanceSquared(mouse, p0 + seg * t) < 18f * 18f) {
                    return e;
                }
            }
            return -1;
        }

        private void SelectNode(int index) {
            if (selectedNode != index) {
                selectedNode = index;
                SoundEngine.PlaySound(CWRSound.ButtonZero with { Volume = 0.5f });
            }
        }

        /// <summary>
        /// 点结印位：空位落选中的鬼；占位且未选中新鬼则卸下；
        /// 占位又选了别的鬼就直接换——不设额外确认态
        /// </summary>
        private void ClickSlot(int slot) {
            if (pendingSlot >= 0) {
                DenyFeedback();
                return;
            }
            string current = OniRegistry.SlotKey(slot);
            OniGhostEntry picked = SelectedEntry;
            //选中的鬼就在这一位：视作卸下
            bool unbind = picked == null || picked.Key == current;
            string next = unbind ? null : picked.Key;
            if (unbind && string.IsNullOrEmpty(current)) {
                //空位又没选鬼：说清楚下一步该干什么，别沉默
                DenyFeedback();
                VaultUtils.Text(SlotEmptyHint.Value, OnikiriUITheme.TextDim);
                return;
            }
            BeginSlotChange(slot, next,
                unbind ? OniRegistry.EntryOf(current)?.Name?.Invoke() : picked.Name?.Invoke());
        }

        private void BeginSlotChange(int slot, string key, string displayName) {
            if (pendingSlot >= 0 || !MaintainSourceItem()) {
                return;
            }
            int session = interactionSession;
            pendingSlot = slot;
            if (!OniRegistry.TrySetSlot(SourceItem(), slot, key, success =>
                CompleteSlotChange(session, success, key, displayName))) {
                pendingSlot = -1;
                DenyFeedback();
            }
        }

        private void CompleteSlotChange(int session, bool success, string key, string displayName) {
            if (!IsOpen || session != interactionSession) {
                return;
            }
            pendingSlot = -1;
            if (!success) {
                DenyFeedback();
                return;
            }
            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.72f, Volume = 0.46f });
            LocalizedText line = string.IsNullOrEmpty(key) ? UnboundFormat : BoundFormat;
            VaultUtils.Text(line.Format(displayName ?? key ?? string.Empty), OnikiriUITheme.Bright);
        }

        private static void DenyFeedback()
            => SoundEngine.PlaySound(SoundID.Unlock with { Pitch = -0.62f, Volume = 0.38f });

        internal OniGhostEntry SelectedEntry {
            get {
                var entries = OniRegistry.Entries;
                int usable = -1;
                foreach (OniGhostEntry entry in entries) {
                    if (entry.CanEquip && ++usable == selectedNode) {
                        return entry;
                    }
                }
                return null;
            }
        }

        /// <summary>第 index 个外环鬼位对应的目录项（只数可结印的）</summary>
        private static OniGhostEntry NodeEntry(int index) {
            int usable = -1;
            foreach (OniGhostEntry entry in OniRegistry.Entries) {
                if (entry.CanEquip && ++usable == index) {
                    return entry;
                }
            }
            return null;
        }

        public override void Draw(SpriteBatch spriteBatch) {
            float a = OpenProgress;
            if (a < 0.01f) {
                return;
            }
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);

            //====压暗世界 + 绯月（远景视差）====
            Rectangle full = new(0, 0, (int)OnikiriUITheme.UIScreenW + 2, (int)OnikiriUITheme.UIScreenH + 2);
            spriteBatch.Draw(pixel, full, src, Color.Black * (a * 0.7f));
            Vector2 parallax = (OnikiriUITheme.UIMouse - OnikiriUITheme.UIScreenSize * 0.5f) * -0.016f;
            OniRegisterRenderer.DrawMoon(spriteBatch,
                new Vector2(OnikiriUITheme.UIScreenW * 0.84f, 118f) + parallax, a, ShaderTime, 0f);

            //====顶梁（同一夜屋的骨架，不随换乘滑移）====
            OniLedgerBeam.Draw(spriteBatch, a, ShaderTime, OniLedgerView.Sigil, meiSwitch.HoverEase);

            //====盘座 + 六芒骨架====
            OniSigilRenderer.DrawBoard(spriteBatch, in wheel, a, ShaderTime);
            OniSigilRenderer.DrawScrollNiche(spriteBatch, font, nicheRect, nicheHover, a, ShaderTime,
                RegisterTabText.Value);

            float contentA = MathHelper.Clamp((a - 0.35f) / 0.65f, 0f, 1f);
            if (contentA <= 0.01f) {
                return;
            }
            OniSigilRenderer.DrawHexagram(spriteBatch, in wheel, contentA, ShaderTime);
            OniSigilRenderer.DrawEdges(spriteBatch, font, in wheel, contentA, ShaderTime, EdgeName);

            //====三个结印位 + 合鬼心====
            for (int slot = 0; slot < OniSigilWheel.SlotCount; slot++) {
                OniSigilRenderer.DrawSlot(spriteBatch, font, in wheel, slot,
                    OniRegistry.SlotEntry(slot), slotHover[slot], pendingSlot == slot,
                    contentA, ShaderTime);
            }
            bool complete = OniRegistry.EquippedCount >= OniSigilWheel.SlotCount;
            OniSigilRenderer.DrawCore(spriteBatch, font, in wheel, complete,
                complete ? WraithCovenText.BurstName?.Value : null, contentA, ShaderTime);

            //====外环六鬼位====
            for (int i = 0; i < OniSigilWheel.NodeCount; i++) {
                OniGhostEntry entry = NodeEntry(i);
                OniSigilRenderer.DrawNode(spriteBatch, font, in wheel, i, entry,
                    entry == null ? -1 : OniRegistry.SlotOf(entry.Key),
                    i == selectedNode, nodeHover[i], contentA, ShaderTime);
            }

            //====题头 / 状态行 / 底部说明====
            DrawHeader(spriteBatch, font, contentA);
            DrawFooter(spriteBatch, font, contentA);
            OniRegisterRenderer.DrawCloseTag(spriteBatch, font, closeTagRope, contentA,
                closeTagHover, GlobalTimer, CloseTagText.Value);

            //吊挂太刀：荷札上书今名
            string bladeName = Inscriptions.OniMeiRegistry.CurrentBladeName(
                Inscriptions.OniMeiRegistry.DisplayStore)?.DisplayName.Value ?? "";
            OniRegisterRenderer.DrawHangingTachi(spriteBatch, font, meiSwitch, contentA,
                GlobalTimer, bladeName);

            //悬浮说明最后画，压在一切之上
            if (meiSwitch.HoverEase > 0.05f) {
                OniMeiRenderer.DrawSwitchHoverTag(spriteBatch, MousePosition,
                    MeiTabText.Value, MeiTabHint.Value, a * meiSwitch.HoverEase);
            }
            else if (nicheHover > 0.05f) {
                OniMeiRenderer.DrawSwitchHoverTag(spriteBatch, MousePosition,
                    RegisterTabText.Value, RegisterTabHint.Value, a * nicheHover);
            }
        }

        /// <summary>三角某条边的合鬼名：两端都占了才有名字</summary>
        private static string EdgeName(int edge) {
            (int a, int b) = OniSigilWheel.EdgeSlots(edge);
            WraithAbilityKind ka = KindOfSlot(a);
            WraithAbilityKind kb = KindOfSlot(b);
            return WraithCovenText.Pair(ka, kb).Name?.Value;
        }

        private static WraithAbilityKind KindOfSlot(int slot)
            => WraithRegistry.TryGetUsable(OniRegistry.SlotKey(slot), out WraithDefinition def)
                ? def.AbilityKind : WraithAbilityKind.None;

        private void DrawHeader(SpriteBatch sb, DynamicSpriteFont font, float a) {
            string title = TitleText.Value;
            const float TitleScale = 1.08f;
            Vector2 size = font.MeasureString(title) * TitleScale;
            Vector2 pos = new(wheel.Center.X - size.X * 0.5f, OniLedgerBeam.Height + 12f);
            OniBrush.DrawSealGlyph(sb, pos + new Vector2(-24f, size.Y * 0.5f), 13f, a * 0.95f);
            Utils.DrawBorderString(sb, title, pos, OnikiriUITheme.HotWhite * a, TitleScale);
            OniBrush.DrawTaperedSlash(sb,
                new Vector2(pos.X - 10f, pos.Y + size.Y + 6f),
                new Vector2(pos.X + size.X + 10f, pos.Y + size.Y + 4f), 2.2f, 1.6f, a * 0.9f);
        }

        /// <summary>
        /// 底行：优先报光标落处的说明（边/位/鬼），没有悬停就报总况。<br/>
        /// 每一种落点都欠玩家一句话，不许出现"点了什么都不说"
        /// </summary>
        private void DrawFooter(SpriteBatch sb, DynamicSpriteFont font, float a) {
            string line = ResolveFooterLine();
            if (string.IsNullOrEmpty(line)) {
                return;
            }
            const float Scale = 0.7f;
            Vector2 size = font.MeasureString(line) * Scale;
            float y = MathF.Min(wheel.Center.Y + wheel.Radius + 74f,
                OnikiriUITheme.UIScreenH - 46f);
            Utils.DrawBorderString(sb, line,
                new Vector2(wheel.Center.X - size.X * 0.5f, y),
                OnikiriUITheme.TextDim * (a * 0.9f), Scale);

            string hint = string.Format(CloseHintFormat.Value,
                CWRKeySystem.Legend_UIControl.ToTooltipString(CWRKeySystem.Notbound.Value));
            Vector2 hintSize = font.MeasureString(hint) * 0.62f;
            Utils.DrawBorderString(sb, hint,
                new Vector2(wheel.Center.X - hintSize.X * 0.5f,
                    MathF.Min(y + size.Y + 4f, OnikiriUITheme.UIScreenH - 24f)),
                OnikiriUITheme.TextDim * (a * 0.55f), 0.62f);
        }

        private string ResolveFooterLine() {
            if (hoverEdge >= 0) {
                (int a, int b) = OniSigilWheel.EdgeSlots(hoverEdge);
                var pair = WraithCovenText.Pair(KindOfSlot(a), KindOfSlot(b));
                if (pair.Note != null) {
                    return $"{pair.Name.Value} · {pair.Note.Value}";
                }
            }
            if (hoverCore && OniRegistry.EquippedCount >= OniSigilWheel.SlotCount) {
                return $"{WraithCovenText.BurstName.Value} · {WraithCovenText.BurstNote.Value}";
            }
            if (hoverSlot >= 0) {
                OniGhostEntry slotEntry = OniRegistry.SlotEntry(hoverSlot);
                if (slotEntry != null) {
                    return $"{slotEntry.Name?.Invoke()} · {UnbindHint.Value}";
                }
                return SelectedEntry == null ? SlotEmptyHint.Value
                    : PickHint.Format(SelectedEntry.Name?.Invoke() ?? string.Empty);
            }
            OniGhostEntry hovered = hoverNode >= 0 ? NodeEntry(hoverNode) : SelectedEntry;
            if (hovered != null) {
                string cost = CostFormat.Format(
                    (int)MathF.Round(hovered.RevivalCost * 100f),
                    (int)MathF.Round(hovered.ErosionCost * 100f));
                return hovered.InDanger ? $"{cost} · {DangerNote.Value}" : cost;
            }
            if (OniRegistry.FirstFreeSlot() < 0) {
                return SlotFullHint.Value;
            }
            return StatusFormat.Format(OniRegistry.EquippedCount, OniSigilWheel.SlotCount,
                (int)MathF.Round(OniRegistry.Erosion * 100f));
        }
    }
}
