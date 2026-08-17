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
    /// 点鬼交互是「拾印在手」：点外环拾起役鬼印随光标走，落在结印位上捺下；
    /// 持印时空位鬼火相邀、将成之边先以虚线预演。<br/>
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
        public static LocalizedText SwapHint { get; private set; }
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
                () => "点外环拾一枚役鬼印，再点空位落印。空位不启用任何能力");
            SlotFullHint = this.GetLocalization(nameof(SlotFullHint),
                () => "三位已满。持印点一位换印，空手点一位卸下");
            PickHint = this.GetLocalization(nameof(PickHint),
                () => "「{0}」在手 · 点结印位落印 · 点空处收回");
            SwapHint = this.GetLocalization(nameof(SwapHint), () => "点击 换上「{0}」");
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
        private int hoverNode = -1;
        private int hoverSlot = -1;
        private int hoverEdge = -1;
        private bool hoverCore;
        private readonly float[] nodeHover = new float[OniSigilWheel.NodeCount];
        private readonly float[] slotHover = new float[OniSigilWheel.SlotCount];

        //====持印在手====
        private int carriedNode = -1;
        private float carryEase;
        private Vector2 carryPos;
        private Vector2 carryVel;
        /// <summary>候令压印：请求在途时印被按在这一槽上</summary>
        private int pressSlot = -1;
        private float pressEase;

        //====槽反馈====
        private readonly float[] slotDeny = new float[OniSigilWheel.SlotCount];
        private readonly float[] slotStamp = new float[OniSigilWheel.SlotCount];
        private readonly int[] slotShake = new int[OniSigilWheel.SlotCount];

        //====合鬼边墨线与三印崩====
        private readonly float[] edgeFlow = new float[3];
        private readonly int[] edgeFlowOrigin = new int[3];
        private readonly OniSigilEdgeView[] edgeViews = new OniSigilEdgeView[3];
        private readonly string[] prevSlotKeys = new string[OniSigilWheel.SlotCount];
        private const float BurstFrames = 54f;
        private float burstAnim = -1f;

        //====盘内批注：回执写在盘上，不进聊天栏====
        private const float NoteFrames = 150f;
        private string noteText;
        private float noteTimer = -1f;
        private Color noteColor;

        //====飞回印：卸下/被换下的印掷回环位====
        private string flyKey;
        private Vector2 flyFrom;
        private int flyNode = -1;
        private float flyT = -1f;

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
            //点鬼簿若还摊着也静默收:防 HUD/按键路径下盘与卷叠开
            if (OniRegisterUI.Instance?.IsOpen ?? false) {
                OniRegisterUI.Instance.SilentSwap = true;
                OniRegisterUI.Instance.Close();
                OniRegisterUI.Instance.SilentSwap = false;
            }
            meiSwitch.Reset();
            pendingSlot = -1;
            appearEase = 0f;
            carriedNode = -1;
            carryEase = 0f;
            carryVel = Vector2.Zero;
            pressSlot = -1;
            pressEase = 0f;
            burstAnim = -1f;
            noteTimer = -1f;
            flyT = -1f;
            flyNode = -1;
            Array.Clear(nodeHover, 0, nodeHover.Length);
            Array.Clear(slotHover, 0, slotHover.Length);
            Array.Clear(slotDeny, 0, slotDeny.Length);
            Array.Clear(slotStamp, 0, slotStamp.Length);
            Array.Clear(slotShake, 0, slotShake.Length);
            //边墨初始化：已成立的边直接通着，开屏不重播跑线
            for (int i = 0; i < OniSigilWheel.SlotCount; i++) {
                prevSlotKeys[i] = OniRegistry.SlotKey(i);
            }
            for (int e = 0; e < 3; e++) {
                edgeFlow[e] = string.IsNullOrEmpty(EdgeName(e)) ? 0f : 1f;
                edgeFlowOrigin[e] = 0;
            }
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
            pressSlot = -1;
            carriedNode = -1;
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
            AdvanceFx();
        }

        private void LayoutCompute() {
            float sw = OnikiriUITheme.UIScreenW;
            float sh = OnikiriUITheme.UIScreenH;
            float radius = OniSigilWheel.BodyRadius(sw, sh);
            //换乘横滑：盘体随行进让位，顶梁与门挂物不加
            float slide = OniLedgerSwapFX.SlideOf(OniLedgerView.Sigil);
            //题头独立在盘上方，盘心随之下移一档
            Vector2 center = new(sw * 0.5f + slide,
                OniLedgerBeam.Height + 44f + radius + (1f - appearEase) * 18f);
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

            if (!inputAvailable) {
                return;
            }
            //右键收印
            if (keyRightPressState == KeyPressState.Pressed && carriedNode >= 0) {
                CancelCarry();
                return;
            }
            if (keyLeftPressState != KeyPressState.Pressed) {
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
                //再点原位＝放回；点别的鬼＝换着拾
                if (carriedNode == hoverNode) {
                    CancelCarry();
                }
                else {
                    PickUp(hoverNode);
                }
                return;
            }
            if (hoverSlot >= 0) {
                ClickSlot(hoverSlot);
                return;
            }
            if (hoverCore) {
                //点合鬼心：心跳应一声，说明在底行常驻
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.4f, Volume = 0.35f });
                return;
            }
            //持印点空处（盘内外皆）＝收印；空手点盘外＝收盘
            if (carriedNode >= 0) {
                CancelCarry();
                return;
            }
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

        //====================== 持印 ======================

        private void PickUp(int index) {
            if (NodeEntry(index) == null) {
                return;
            }
            carriedNode = index;
            carryEase = 0f;
            carryPos = wheel.NodePos(index);
            carryVel = Vector2.Zero;
            SoundEngine.PlaySound(CWRSound.ButtonZero with { Volume = 0.5f });
        }

        /// <summary>收印：在手的印掷回环位</summary>
        private void CancelCarry() {
            if (carriedNode < 0) {
                return;
            }
            OniGhostEntry entry = CarriedEntry;
            if (entry != null) {
                flyKey = entry.Key;
                flyFrom = carryPos;
                flyNode = carriedNode;
                flyT = 0f;
            }
            carriedNode = -1;
            SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.2f, Volume = 0.3f });
        }

        /// <summary>
        /// 点结印位：持印落印/换印；空手点占位卸下；
        /// 持着某鬼点它自己的位也是卸下——不设额外确认态
        /// </summary>
        private void ClickSlot(int slot) {
            if (pendingSlot >= 0) {
                Deny(slot);
                return;
            }
            string current = OniRegistry.SlotKey(slot);
            OniGhostEntry picked = CarriedEntry;
            bool unbind = picked == null || picked.Key == current;
            string next = unbind ? null : picked.Key;
            if (unbind && string.IsNullOrEmpty(current)) {
                //空位又空手：凿圈一震，批注说清下一步，别沉默
                Deny(slot);
                PostNote(SlotEmptyHint.Value, OnikiriUITheme.TextDim);
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
            pressSlot = slot;
            if (!OniRegistry.TrySetSlot(SourceItem(), slot, key, success =>
                CompleteSlotChange(session, success, slot, key, displayName))) {
                pendingSlot = -1;
                pressSlot = -1;
                Deny(slot);
            }
        }

        private void CompleteSlotChange(int session, bool success, int slot, string key, string displayName) {
            if (!IsOpen || session != interactionSession) {
                return;
            }
            pendingSlot = -1;
            pressSlot = -1;
            if (!success) {
                Deny(slot);
                return;
            }
            bool bound = !string.IsNullOrEmpty(key);
            if (bound) {
                //印已落盘，手上自然消携（落印定妆由状态差分接手）
                carriedNode = -1;
            }
            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.72f, Volume = 0.46f });
            LocalizedText line = bound ? BoundFormat : UnboundFormat;
            PostNote(line.Format(displayName ?? key ?? string.Empty), OnikiriUITheme.Bright);
        }

        /// <summary>拒绝反馈：凿圈红闪+槽体一震+闷响——点击必有可见回应</summary>
        private void Deny(int slot) {
            if (slot >= 0 && slot < slotDeny.Length) {
                slotDeny[slot] = 1f;
                slotShake[slot] = 14;
            }
            DenyFeedback();
        }

        private static void DenyFeedback()
            => SoundEngine.PlaySound(SoundID.Unlock with { Pitch = -0.62f, Volume = 0.38f });

        private void PostNote(string text, Color color) {
            noteText = text;
            noteTimer = 0f;
            noteColor = color;
        }

        //====================== 表现推进 ======================

        private void AdvanceFx() {
            //持印弹簧：印追手，带一点惯性拖
            if (carriedNode >= 0) {
                carryEase = MathF.Min(1f, carryEase + 0.10f);
                Vector2 target = MousePosition + new Vector2(0f, -4f);
                carryVel = carryVel * 0.70f + (target - carryPos) * 0.17f;
                carryPos += carryVel;
            }
            else {
                carryEase = 0f;
                carryVel = Vector2.Zero;
            }
            //候令压印缓动
            float pressTarget = pressSlot >= 0 ? 1f : 0f;
            pressEase += (pressTarget - pressEase) * 0.30f;

            //槽反馈衰减
            for (int i = 0; i < OniSigilWheel.SlotCount; i++) {
                slotDeny[i] *= 0.88f;
                slotStamp[i] = MathF.Max(0f, slotStamp[i] - 1f / 26f);
                if (slotShake[i] > 0) {
                    slotShake[i]--;
                }
            }

            DiffSlotStates();

            //边墨流通：成边生长，退位排干
            for (int e = 0; e < 3; e++) {
                bool formed = !string.IsNullOrEmpty(EdgeName(e));
                edgeFlow[e] = formed
                    ? MathF.Min(1f, edgeFlow[e] + 1f / 26f)
                    : MathF.Max(0f, edgeFlow[e] - 1f / 12f);
            }

            //三印崩节拍
            if (burstAnim >= 0f) {
                burstAnim += 1f;
                if (burstAnim > BurstFrames) {
                    burstAnim = -1f;
                }
            }
            //盘内批注
            if (noteTimer >= 0f) {
                noteTimer += 1f;
                if (noteTimer > NoteFrames) {
                    noteTimer = -1f;
                }
            }
            //飞回印
            if (flyT >= 0f) {
                flyT += 1f / 20f;
                if (flyT >= 1f) {
                    flyT = -1f;
                    flyNode = -1;
                }
            }
        }

        /// <summary>
        /// 状态差分：结印真值一变（含服务器回执与外部改动），落印定妆/旧印飞回/
        /// 邻边重新跑线/三印齐崩全部由这里点火——表现永远跟着权威状态走
        /// </summary>
        private void DiffSlotStates() {
            int prevCount = 0;
            for (int i = 0; i < OniSigilWheel.SlotCount; i++) {
                if (!string.IsNullOrEmpty(prevSlotKeys[i])) {
                    prevCount++;
                }
            }
            bool changed = false;
            for (int i = 0; i < OniSigilWheel.SlotCount; i++) {
                string now = OniRegistry.SlotKey(i) ?? string.Empty;
                string was = prevSlotKeys[i] ?? string.Empty;
                if (now == was) {
                    continue;
                }
                changed = true;
                //旧印离位→掷回环位；已在手上或挪去别槽的不飞
                if (was.Length > 0 && OniRegistry.SlotOf(was) < 0 && CarriedEntry?.Key != was) {
                    int node = NodeIndexOfKey(was);
                    if (node >= 0) {
                        flyKey = was;
                        flyFrom = wheel.SlotPos(i);
                        flyNode = node;
                        flyT = 0f;
                    }
                }
                if (now.Length > 0) {
                    slotStamp[i] = 1f;
                    //本位落新印：相邻两边的墨自本位起笔重新流通
                    for (int e = 0; e < 3; e++) {
                        (int a, int b) = OniSigilWheel.EdgeSlots(e);
                        if (a != i && b != i) {
                            continue;
                        }
                        edgeFlowOrigin[e] = a == i ? 0 : 1;
                        edgeFlow[e] = 0f;
                    }
                }
                prevSlotKeys[i] = now;
            }
            if (!changed) {
                return;
            }
            int nowCount = OniRegistry.EquippedCount;
            if (nowCount >= OniSigilWheel.SlotCount && prevCount < OniSigilWheel.SlotCount) {
                burstAnim = 0f;
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.95f, Volume = 0.55f });
            }
        }

        /// <summary>在手役鬼（外环第 carriedNode 位）</summary>
        internal OniGhostEntry CarriedEntry {
            get {
                if (carriedNode < 0) {
                    return null;
                }
                return NodeEntry(carriedNode);
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

        /// <summary>某只鬼在外环的位序；不在名录返回 -1</summary>
        private static int NodeIndexOfKey(string key) {
            if (string.IsNullOrEmpty(key)) {
                return -1;
            }
            int usable = -1;
            foreach (OniGhostEntry entry in OniRegistry.Entries) {
                if (!entry.CanEquip) {
                    continue;
                }
                usable++;
                if (entry.Key == key) {
                    return usable;
                }
            }
            return -1;
        }

        //====================== 绘制 ======================

        public override void Draw(SpriteBatch spriteBatch) {
            float a = OpenProgress;
            if (a < 0.01f) {
                return;
            }
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 mouse = MousePosition;

            //====压暗世界 + 绯月（远景视差）====
            Rectangle full = new(0, 0, (int)OnikiriUITheme.UIScreenW + 2, (int)OnikiriUITheme.UIScreenH + 2);
            spriteBatch.Draw(pixel, full, src, Color.Black * (a * 0.7f));
            Vector2 parallax = (OnikiriUITheme.UIMouse - OnikiriUITheme.UIScreenSize * 0.5f) * -0.016f;
            OniRegisterRenderer.DrawMoon(spriteBatch,
                new Vector2(OnikiriUITheme.UIScreenW * 0.84f, 118f) + parallax, a, ShaderTime, 0f);

            //====顶梁（同一夜屋的骨架，不随换乘滑移）====
            OniLedgerBeam.Draw(spriteBatch, a, ShaderTime, OniLedgerView.Sigil, meiSwitch.HoverEase);

            //====盘座（shader 漆盘）+ 卷槽====
            OniSigilRenderer.DrawBoard(spriteBatch, in wheel, a, ShaderTime);
            OniSigilRenderer.DrawScrollNiche(spriteBatch, font, nicheRect, nicheHover, a, ShaderTime,
                RegisterTabText.Value);

            float contentA = MathHelper.Clamp((a - 0.35f) / 0.65f, 0f, 1f);
            if (contentA <= 0.01f) {
                return;
            }
            OniSigilRenderer.DrawHexagram(spriteBatch, in wheel, contentA, ShaderTime);

            //====合鬼边（成边流通/预演虚线/崩闪）====
            BuildEdgeViews();
            OniSigilRenderer.DrawEdges(spriteBatch, font, in wheel, edgeViews, contentA, ShaderTime);

            //====三个结印位====
            for (int slot = 0; slot < OniSigilWheel.SlotCount; slot++) {
                OniSigilSlotView view = BuildSlotView(slot);
                OniSigilRenderer.DrawSlot(spriteBatch, font, in wheel, slot, in view,
                    contentA, ShaderTime);
            }

            //====三印崩收束墨线 + 合鬼心====
            float burstT = burstAnim < 0f ? -1f : burstAnim / BurstFrames;
            OniSigilRenderer.DrawBurstThreads(spriteBatch, in wheel, burstT, contentA, ShaderTime);
            bool complete = OniRegistry.EquippedCount >= OniSigilWheel.SlotCount;
            OniSigilRenderer.DrawCore(spriteBatch, font, in wheel, complete,
                complete ? WraithCovenText.BurstName?.Value : null, burstT, contentA, ShaderTime);

            //====悬停引路：这只鬼与盘上谁有专属反应====
            DrawHoverPartnerThreads(spriteBatch, contentA);

            //====外环六鬼位====
            for (int i = 0; i < OniSigilWheel.NodeCount; i++) {
                OniGhostEntry entry = NodeEntry(i);
                OniSigilRenderer.DrawNode(spriteBatch, font, in wheel, i, entry,
                    entry == null ? -1 : OniRegistry.SlotOf(entry.Key),
                    i == carriedNode, nodeHover[i], mouse, contentA, ShaderTime);
            }

            //====飞回印 / 持印邀请线 / 在手印====
            if (flyT >= 0f && flyNode >= 0) {
                OniSigilRenderer.DrawFlyBackSeal(spriteBatch, flyKey, flyFrom,
                    wheel.NodePos(flyNode), flyT, wheel.NodeHit * 0.78f, contentA);
            }
            OniGhostEntry carried = CarriedEntry;
            if (carried != null && pressSlot < 0) {
                for (int s = 0; s < OniSigilWheel.SlotCount; s++) {
                    float strength = hoverSlot == s ? 0.75f : 0.26f;
                    OniSigilRenderer.DrawDashedLine(spriteBatch, carryPos, wheel.SlotPos(s),
                        OnikiriUITheme.GhostDim, OnikiriUITheme.GhostFire,
                        contentA * carryEase * strength, ShaderTime, s * 2.3f);
                }
            }
            OniSigilRenderer.DrawCarriedSeal(spriteBatch, carryPos, carryVel, carried,
                wheel.NodeHit * 0.78f, carryEase,
                pressSlot >= 0 ? pressEase : 0f,
                pressSlot >= 0 ? wheel.SlotPos(pressSlot) : carryPos,
                contentA, ShaderTime);

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

        /// <summary>汇总三条边的本帧表现量（成边流通/持印预演/崩闪）</summary>
        private void BuildEdgeViews() {
            OniGhostEntry carried = CarriedEntry;
            float burstT = burstAnim < 0f ? -1f : burstAnim / BurstFrames;
            for (int e = 0; e < 3; e++) {
                (int a, int b) = OniSigilWheel.EdgeSlots(e);
                OniSigilEdgeView v = default;
                v.Name = EdgeName(e);
                v.Flow = edgeFlow[e];
                v.FlowOrigin = edgeFlowOrigin[e];
                //预演：持印悬停空位，这条边另一端已有印——先看懂再落印
                if (carried != null && hoverSlot >= 0 && (a == hoverSlot || b == hoverSlot)
                    && string.IsNullOrEmpty(OniRegistry.SlotKey(hoverSlot))) {
                    int other = a == hoverSlot ? b : a;
                    OniGhostEntry otherEntry = OniRegistry.SlotEntry(other);
                    if (otherEntry != null && otherEntry.Key != carried.Key) {
                        (LocalizedText name, _) = WraithCovenText.Pair(
                            KindOfKey(carried.Key), KindOfKey(otherEntry.Key));
                        v.PreviewName = name?.Value;
                        v.Preview = slotHover[hoverSlot];
                    }
                }
                //三印崩：三边顺序过闪
                if (burstT >= 0f) {
                    float local = burstT - e * 0.11f;
                    v.Flash = MathF.Exp(-(local - 0.09f) * (local - 0.09f) * 260f);
                }
                edgeViews[e] = v;
            }
        }

        private OniSigilSlotView BuildSlotView(int slot) {
            OniGhostEntry carried = CarriedEntry;
            OniSigilSlotView v = new() {
                Entry = OniRegistry.SlotEntry(slot),
                Hover = slotHover[slot],
                Pending = pendingSlot == slot,
                Press = pressSlot == slot ? pressEase : 0f,
                Invite = carried != null ? carryEase : 0f,
                PreviewEntry = carried != null && hoverSlot == slot ? carried : null,
                DenyFlash = slotDeny[slot],
                StampFlash = slotStamp[slot],
            };
            //占位槽不做鬼火邀请（它给的是换印预览）
            if (v.Entry != null) {
                v.Invite = 0f;
            }
            if (slotShake[slot] > 0) {
                float k = slotShake[slot] / 14f;
                v.Shake = new Vector2(MathF.Sin(slotShake[slot] * 1.9f) * 3.2f * k, 0f);
            }
            return v;
        }

        /// <summary>空手悬停外环鬼位：与盘上役鬼有专属反应的，鬼火虚线先指给玩家看</summary>
        private void DrawHoverPartnerThreads(SpriteBatch sb, float contentA) {
            if (CarriedEntry != null) {
                return;
            }
            for (int i = 0; i < OniSigilWheel.NodeCount; i++) {
                if (nodeHover[i] <= 0.05f) {
                    continue;
                }
                OniGhostEntry entry = NodeEntry(i);
                //在盘上的鬼自己的边会亮，不必再引
                if (entry == null || OniRegistry.IsEquipped(entry.Key)) {
                    continue;
                }
                for (int s = 0; s < OniSigilWheel.SlotCount; s++) {
                    OniGhostEntry other = OniRegistry.SlotEntry(s);
                    if (other == null || other.Key == entry.Key) {
                        continue;
                    }
                    (LocalizedText name, _) = WraithCovenText.Pair(
                        KindOfKey(entry.Key), KindOfKey(other.Key));
                    //只提示专属反应；「相唤」是底噪，不值一条线
                    if (name == null || name == WraithCovenText.CallName) {
                        continue;
                    }
                    OniSigilRenderer.DrawDashedLine(sb, wheel.NodePos(i), wheel.SlotPos(s),
                        OnikiriUITheme.GhostDim, OnikiriUITheme.GhostFire,
                        contentA * nodeHover[i] * 0.55f, ShaderTime, i * 1.3f + s);
                }
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
            => KindOfKey(OniRegistry.SlotKey(slot));

        private static WraithAbilityKind KindOfKey(string key)
            => WraithRegistry.TryGetUsable(key, out WraithDefinition def)
                ? def.AbilityKind : WraithAbilityKind.None;

        private void DrawHeader(SpriteBatch sb, DynamicSpriteFont font, float a) {
            string title = TitleText.Value;
            const float TitleScale = 1.08f;
            Vector2 size = font.MeasureString(title) * TitleScale;
            Vector2 pos = new(wheel.Center.X - size.X * 0.5f, OniLedgerBeam.Height + 10f);
            OniBrush.DrawSealGlyph(sb, pos + new Vector2(-24f, size.Y * 0.5f), 13f, a * 0.95f);
            Utils.DrawBorderString(sb, title, pos, OnikiriUITheme.HotWhite * a, TitleScale);
            OniBrush.DrawTaperedSlash(sb,
                new Vector2(pos.X - 10f, pos.Y + size.Y + 4f),
                new Vector2(pos.X + size.X + 10f, pos.Y + size.Y + 2f), 2.2f, 1.6f, a * 0.9f);
        }

        /// <summary>
        /// 底行：批注在时批注优先（回执写在盘上），其次报光标落处的说明（边/位/鬼），
        /// 没有悬停就报总况。每一种落点都欠玩家一句话，不许出现"点了什么都不说"
        /// </summary>
        private void DrawFooter(SpriteBatch sb, DynamicSpriteFont font, float a) {
            float y = MathF.Min(wheel.Center.Y + wheel.Radius + 74f,
                OnikiriUITheme.UIScreenH - 46f);
            float lineH;
            if (noteTimer >= 0f && !string.IsNullOrEmpty(noteText)) {
                OniSigilRenderer.DrawNote(sb, font, new Vector2(wheel.Center.X, y + 9f),
                    noteText, noteTimer / NoteFrames, noteColor, a);
                lineH = font.MeasureString(noteText).Y * 0.72f;
            }
            else {
                string line = ResolveFooterLine();
                if (string.IsNullOrEmpty(line)) {
                    return;
                }
                const float Scale = 0.7f;
                Vector2 size = font.MeasureString(line) * Scale;
                Utils.DrawBorderString(sb, line,
                    new Vector2(wheel.Center.X - size.X * 0.5f, y),
                    OnikiriUITheme.TextDim * (a * 0.9f), Scale);
                lineH = size.Y;
            }

            string hint = string.Format(CloseHintFormat.Value,
                CWRKeySystem.Legend_UIControl.ToTooltipString(CWRKeySystem.Notbound.Value));
            Vector2 hintSize = font.MeasureString(hint) * 0.62f;
            Utils.DrawBorderString(sb, hint,
                new Vector2(wheel.Center.X - hintSize.X * 0.5f,
                    MathF.Min(y + lineH + 4f, OnikiriUITheme.UIScreenH - 24f)),
                OnikiriUITheme.TextDim * (a * 0.55f), 0.62f);
        }

        private string ResolveFooterLine() {
            OniGhostEntry carried = CarriedEntry;
            if (hoverEdge >= 0) {
                (int a, int b) = OniSigilWheel.EdgeSlots(hoverEdge);
                (LocalizedText name, LocalizedText note) = WraithCovenText.Pair(KindOfSlot(a), KindOfSlot(b));
                if (note != null) {
                    return $"{name.Value} · {note.Value}";
                }
            }
            if (hoverCore && OniRegistry.EquippedCount >= OniSigilWheel.SlotCount) {
                return $"{WraithCovenText.BurstName.Value} · {WraithCovenText.BurstNote.Value}";
            }
            if (hoverSlot >= 0) {
                OniGhostEntry slotEntry = OniRegistry.SlotEntry(hoverSlot);
                if (carried != null) {
                    if (slotEntry == null) {
                        return PickHint.Format(carried.Name?.Invoke() ?? string.Empty);
                    }
                    if (slotEntry.Key == carried.Key) {
                        return $"{slotEntry.Name?.Invoke()} · {UnbindHint.Value}";
                    }
                    return SwapHint.Format(carried.Name?.Invoke() ?? string.Empty);
                }
                if (slotEntry != null) {
                    return $"{slotEntry.Name?.Invoke()} · {UnbindHint.Value}";
                }
                return SlotEmptyHint.Value;
            }
            if (carried != null) {
                return PickHint.Format(carried.Name?.Invoke() ?? string.Empty);
            }
            OniGhostEntry hovered = hoverNode >= 0 ? NodeEntry(hoverNode) : null;
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
