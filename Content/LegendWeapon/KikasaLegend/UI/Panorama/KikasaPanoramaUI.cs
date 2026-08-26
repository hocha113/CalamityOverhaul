using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
using CalamityOverhaul.Content.UIs;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI.Panorama
{
    /// <summary>
    /// 湖心景：鬼伞主界面。整屏是一幅血湖夜景剖面，分区即空间位置
    /// 湖上住两鬼（左岸恶犬=鬼梦、右水金焰=鬼火，湖力条横在两鬼之间，一条读数解释两道门），
    /// 水线摆三席影位（席间水脉=组合边，拾影在手点放编成），
    /// 浅水横着收集册（沉溺过的宿敌永久入册），湖底铺着湖藏四十格（点击提取）。
    /// 持鬼伞按 <see cref="CWRKeySystem.Legend_UIControl"/> 或点风铃展开；
    /// 点恶犬在就绪时直接入梦；点哪都有回应，非热区点击落一圈墨涟漪
    /// </summary>
    internal class KikasaPanoramaUI : UIHandle, ILocalizedModType, IFullScreenUIHandle
    {
        public string LocalizationCategory => "Legend.KikasaText";

        public static KikasaPanoramaUI Instance => UIHandleLoader.GetUIHandleOfType<KikasaPanoramaUI>();

        FullScreenUIDomain IFullScreenUIHandle.FullScreenDomain => FullScreenUIDomain.Kikasa;

        //全屏景画在各 HUD（默认 1）之上，对齐鬼切全屏的 2 档惯例
        public override float RenderPriority => 2f;

        #region 本地化
        public static LocalizedText Title { get; private set; }
        public static LocalizedText HoundTitle { get; private set; }
        public static LocalizedText WispTitle { get; private set; }
        public static LocalizedText ReflectAwake { get; private set; }
        public static LocalizedText ReflectAsleep { get; private set; }
        public static LocalizedText InDreamLine { get; private set; }
        public static LocalizedText DreamReturnFormat { get; private set; }
        public static LocalizedText DreamEnterKeyFormat { get; private set; }
        public static LocalizedText DreamEnterClick { get; private set; }
        public static LocalizedText NeedDomainFormat { get; private set; }
        public static LocalizedText NeedFullWater { get; private set; }
        public static LocalizedText HoundBonusFormat { get; private set; }
        public static LocalizedText WispBurning { get; private set; }
        public static LocalizedText WispIdle { get; private set; }
        public static LocalizedText WispBoil { get; private set; }
        public static LocalizedText WispQuenchedLine { get; private set; }
        public static LocalizedText WispBonusFormat { get; private set; }
        public static LocalizedText WispIgniteClick { get; private set; }
        public static LocalizedText WispSnuffClick { get; private set; }
        public static LocalizedText ActionDreamTag { get; private set; }
        public static LocalizedText ActionIgniteTag { get; private set; }
        public static LocalizedText ActionSnuffTag { get; private set; }
        public static LocalizedText EdgeDreamFire { get; private set; }
        public static LocalizedText EdgeBoilRain { get; private set; }
        public static LocalizedText EdgeRainNightmare { get; private set; }
        public static LocalizedText EdgeTriSeal { get; private set; }
        public static LocalizedText SeatsTitle { get; private set; }
        public static LocalizedText SeatEmptyLine { get; private set; }
        public static LocalizedText SeatPickHint { get; private set; }
        public static LocalizedText SeatPlaceHint { get; private set; }
        public static LocalizedText SeatHeldLineFormat { get; private set; }
        public static LocalizedText RosterTallyFormat { get; private set; }
        public static LocalizedText RosterSeatTallyFormat { get; private set; }
        public static LocalizedText RosterUnknown { get; private set; }
        public static LocalizedText RosterUnknownHintFormat { get; private set; }
        public static LocalizedText RosterSeatedTag { get; private set; }
        public static LocalizedText RosterPickHint { get; private set; }
        public static LocalizedText NotePlacedFormat { get; private set; }
        public static LocalizedText NoteUnslottedFormat { get; private set; }
        public static LocalizedText NoteNotCollected { get; private set; }
        public static LocalizedText NotePickFirst { get; private set; }
        public static LocalizedText NoteArmsNoStock { get; private set; }
        public static LocalizedText TalisTitle { get; private set; }
        public static LocalizedText TalisEmptyLine { get; private set; }
        public static LocalizedText TalisNoneOwned { get; private set; }
        public static LocalizedText TalisNeedUmbrella { get; private set; }
        public static LocalizedText TalisSwapHint { get; private set; }
        public static LocalizedText TalisHangHint { get; private set; }
        public static LocalizedText TalisTakeDownLabel { get; private set; }
        public static LocalizedText TalisAlreadyHung { get; private set; }
        public static LocalizedText TalisCurrentTag { get; private set; }
        public static LocalizedText TalisStackRule { get; private set; }
        public static LocalizedText TalisDragSwapHint { get; private set; }
        public static LocalizedText TalisDragMoveHint { get; private set; }
        public static LocalizedText NoteFuHungFormat { get; private set; }
        public static LocalizedText NoteFuTakenFormat { get; private set; }
        public static LocalizedText VaultTitle { get; private set; }
        public static LocalizedText VaultCountFormat { get; private set; }
        public static LocalizedText VaultEmptyHint { get; private set; }
        public static LocalizedText VaultExtractHint { get; private set; }
        public static LocalizedText VaultViewOnlyLine { get; private set; }
        public static LocalizedText ArmsStockFormat { get; private set; }
        public static LocalizedText ThrallLineFormat { get; private set; }
        public static LocalizedText FooterHintFormat { get; private set; }

        public override void SetStaticDefaults() {
            Title = this.GetLocalization(nameof(Title), () => "Lakeheart");
            HoundTitle = this.GetLocalization(nameof(HoundTitle), () => "The Hound \u00b7 Ghost Dream");
            WispTitle = this.GetLocalization(nameof(WispTitle), () => "The Gold Flame \u00b7 Ghost Fire");
            ReflectAwake = this.GetLocalization(nameof(ReflectAwake), () => "The reflection is awake");
            ReflectAsleep = this.GetLocalization(nameof(ReflectAsleep), () => "The reflection sleeps");
            InDreamLine = this.GetLocalization(nameof(InDreamLine), () => "You are inside the dream");
            DreamReturnFormat = this.GetLocalization(nameof(DreamReturnFormat),
                () => "Press {0} again to return");
            //动作文案点名目标：按键路径与点击路径分行各说各的（反馈九：[点击] 不知点谁）
            DreamEnterKeyFormat = this.GetLocalization(nameof(DreamEnterKeyFormat),
                () => "Hold {0} to enter the Ghost Dream");
            DreamEnterClick = this.GetLocalization(nameof(DreamEnterClick),
                () => "Click the hound to enter the Ghost Dream");
            NeedDomainFormat = this.GetLocalization(nameof(NeedDomainFormat),
                () => "Press {0} to raise the blood lake");
            NeedFullWater = this.GetLocalization(nameof(NeedFullWater),
                () => "The lake has not fully risen");
            HoundBonusFormat = this.GetLocalization(nameof(HoundBonusFormat),
                () => "Dream hounds: cap {0} \u00b7 bite {1}%");
            WispBurning = this.GetLocalization(nameof(WispBurning),
                () => "Burning \u2014 scorching along the waterline");
            WispIdle = this.GetLocalization(nameof(WispIdle),
                () => "Unlit \u2014 the flame waits on your word");
            WispBoil = this.GetLocalization(nameof(WispBoil),
                () => "Boiling on through the rain");
            WispQuenchedLine = this.GetLocalization(nameof(WispQuenchedLine),
                () => "The ghost rain is pressing it out");
            WispBonusFormat = this.GetLocalization(nameof(WispBonusFormat),
                () => "Scorch {0}s \u00b7 flame reach {1}px");
            WispIgniteClick = this.GetLocalization(nameof(WispIgniteClick),
                () => "Click the gold flame to light the ghost fire");
            WispSnuffClick = this.GetLocalization(nameof(WispSnuffClick),
                () => "Click the gold flame to draw the fire back");
            //动作牌：可操作态浮在鬼身旁的具名小牌，点牌=点鬼
            ActionDreamTag = this.GetLocalization(nameof(ActionDreamTag), () => "Enter the Dream");
            ActionIgniteTag = this.GetLocalization(nameof(ActionIgniteTag), () => "Ignite");
            ActionSnuffTag = this.GetLocalization(nameof(ActionSnuffTag), () => "Snuff");
            EdgeDreamFire = this.GetLocalization(nameof(EdgeDreamFire), () => "Dreamfire");
            EdgeBoilRain = this.GetLocalization(nameof(EdgeBoilRain), () => "Boil-Rain");
            EdgeRainNightmare = this.GetLocalization(nameof(EdgeRainNightmare), () => "Rain-Mare");
            EdgeTriSeal = this.GetLocalization(nameof(EdgeTriSeal), () => "Three Shades Still the Lake");
            SeatsTitle = this.GetLocalization(nameof(SeatsTitle), () => "Three Seats of Shades");
            SeatEmptyLine = this.GetLocalization(nameof(SeatEmptyLine),
                () => "A vacant seat \u2014 pick a shade from the codex below");
            SeatPickHint = this.GetLocalization(nameof(SeatPickHint),
                () => "Click to pick up \u00b7 right-click to unseat");
            SeatPlaceHint = this.GetLocalization(nameof(SeatPlaceHint), () => "Click to seat it here");
            SeatHeldLineFormat = this.GetLocalization(nameof(SeatHeldLineFormat),
                () => "Held back \u2014 recall it on the wheel ({0})");
            //图鉴口径写明，别让玩家把收录进度当同时出战上限（反馈三·#36）
            RosterTallyFormat = this.GetLocalization(nameof(RosterTallyFormat),
                () => "Codex {0} / {1}");
            RosterSeatTallyFormat = this.GetLocalization(nameof(RosterSeatTallyFormat),
                () => "Seats {0} / {1}");
            RosterUnknown = this.GetLocalization(nameof(RosterUnknown), () => "An undrowned shade");
            RosterUnknownHintFormat = this.GetLocalization(nameof(RosterUnknownHintFormat),
                () => "Point at the foe and press {0} while the lake is ready");
            RosterSeatedTag = this.GetLocalization(nameof(RosterSeatedTag), () => "Seated");
            RosterPickHint = this.GetLocalization(nameof(RosterPickHint), () => "Click to pick it up");
            NotePlacedFormat = this.GetLocalization(nameof(NotePlacedFormat),
                () => "{0} settled into the seat");
            NoteUnslottedFormat = this.GetLocalization(nameof(NoteUnslottedFormat),
                () => "{0} returned to the lakebed");
            NoteNotCollected = this.GetLocalization(nameof(NoteNotCollected),
                () => "The lake has not drowned it yet");
            NotePickFirst = this.GetLocalization(nameof(NotePickFirst),
                () => "Pick a shade from the codex first");
            NoteArmsNoStock = this.GetLocalization(nameof(NoteArmsNoStock),
                () => "No originals in the hoard \u2014 it cannot take form");
            TalisTitle = this.GetLocalization(nameof(TalisTitle), () => "Raincall Rope");
            TalisEmptyLine = this.GetLocalization(nameof(TalisEmptyLine),
                () => "An empty knot \u2014 click to open the talisman case");
            TalisNoneOwned = this.GetLocalization(nameof(TalisNoneOwned),
                () => "The talisman case is empty \u2014 scribe a Raincall Talisman by the water first");
            TalisNeedUmbrella = this.GetLocalization(nameof(TalisNeedUmbrella),
                () => "Hold Kikasa in hand \u2014 the talismans answer only the umbrella");
            TalisSwapHint = this.GetLocalization(nameof(TalisSwapHint),
                () => "Click to swap \u00b7 drag onto another knot to relocate \u00b7 right-click to take down");
            TalisHangHint = this.GetLocalization(nameof(TalisHangHint), () => "Click to hang it here");
            TalisTakeDownLabel = this.GetLocalization(nameof(TalisTakeDownLabel), () => "Take down");
            TalisAlreadyHung = this.GetLocalization(nameof(TalisAlreadyHung),
                () => "It already hangs on another knot");
            TalisCurrentTag = this.GetLocalization(nameof(TalisCurrentTag), () => "Hanging here");
            //牌序真相一行说清（反馈八·#24 审计结论）：数值全生效，滴的墨色与专属滴效只归先认领的那张
            TalisStackRule = this.GetLocalization(nameof(TalisStackRule),
                () => "Every hung talisman keeps working, even with its ink hidden; each drop takes one talisman's color and signature effect, leftmost first");
            TalisDragSwapHint = this.GetLocalization(nameof(TalisDragSwapHint),
                () => "Release to trade knots");
            TalisDragMoveHint = this.GetLocalization(nameof(TalisDragMoveHint),
                () => "Release to move it here");
            NoteFuHungFormat = this.GetLocalization(nameof(NoteFuHungFormat),
                () => "\u300c{0}\u300d rose onto the raincall rope");
            NoteFuTakenFormat = this.GetLocalization(nameof(NoteFuTakenFormat),
                () => "\u300c{0}\u300d returned to the talisman case");
            VaultTitle = this.GetLocalization(nameof(VaultTitle), () => "Lake Hoard");
            VaultCountFormat = this.GetLocalization(nameof(VaultCountFormat), () => "Sunk {0} / {1}");
            VaultEmptyHint = this.GetLocalization(nameof(VaultEmptyHint),
                () => "The lakebed is empty \u2014 only the sound of water");
            VaultExtractHint = this.GetLocalization(nameof(VaultExtractHint), () => "Click to retrieve");
            VaultViewOnlyLine = this.GetLocalization(nameof(VaultViewOnlyLine),
                () => "The lake has not risen to your feet \u2014 look, don't reach");
            ArmsStockFormat = this.GetLocalization(nameof(ArmsStockFormat),
                () => "Servant originals \u00d7{0}");
            ThrallLineFormat = this.GetLocalization(nameof(ThrallLineFormat),
                () => "Umbrella thralls: cap {0} \u00b7 convert every {1}s");
            FooterHintFormat = this.GetLocalization(nameof(FooterHintFormat),
                () => "Esc / {0} close \u00b7 hold an item and press {1} to sink it \u00b7 {2} servant wheel");
        }
        #endregion

        public override bool Active => IsOpen || OpenProgress > 0.01f;

        public override bool CloseOnEscape => true;

        public override SoundStyle? OpenSound
            => SoundID.SplashWeak with { Volume = 0.5f, Pitch = -0.45f };

        public override SoundStyle? CloseSound
            => SoundID.SplashWeak with { Volume = 0.4f, Pitch = -0.75f };

        //==================== 状态 ====================

        private enum HoverKind { None, Hound, Wisp, Seat, Roster, Vault, Help, Talis, TalisFan }

        private HoverKind hoverKind = HoverKind.None;
        private int hoverIndex = -1;
        private float houndHover;
        private float wispHover;
        private float helpHover;

        /// <summary>页脚右端的「?」重看教程钮，命中与绘制共用一份几何</summary>
        private static Rectangle HelpRect
            => new((int)(KikasaPanoramaTheme.UIScreenW - 54f),
                (int)(KikasaPanoramaTheme.FooterY - 8f), 32, 32);
        private readonly float[] seatHover = new float[KikasaServantPlayer.SlotCount];
        private readonly List<float> rosterHover = [];
        private readonly List<float> vaultHover = [];

        //拾影在手：carryKey=0 空手；carryOriginSeat=-1 表示从册里拾的
        private int carryKey;
        private int carryOriginSeat = -1;
        private Vector2 carryPos;
        private Vector2 carryVel;

        //席位表现：定妆/拒绝/横震，真值差分点火
        private readonly float[] seatStamp = new float[KikasaServantPlayer.SlotCount];
        private readonly float[] seatDeny = new float[KikasaServantPlayer.SlotCount];
        private readonly int[] seatShake = new int[KikasaServantPlayer.SlotCount];
        private readonly int[] lastSeatKeys = new int[KikasaServantPlayer.SlotCount];

        //祈雨绳：符位表现同一套差分语汇；fanSlot=-1 扇合拢
        private readonly float[] talisHover = new float[KikasaTalismanStore.SlotCount];
        private readonly float[] talisStamp = new float[KikasaTalismanStore.SlotCount];
        private readonly float[] talisDeny = new float[KikasaTalismanStore.SlotCount];
        private readonly int[] talisShake = new int[KikasaTalismanStore.SlotCount];
        private readonly string[] lastTalisKeys = new string[KikasaTalismanStore.SlotCount];
        private int fanSlot = -1;
        //候选扇内容：符 Key，null 项=摘下位
        private readonly List<string> fanKeys = [];
        private readonly List<float> fanHover = [];

        //拖符换位：按下已挂结先记拖候，移过阈值才算真拖；原地松手仍是点击开扇
        private int talisDragSlot = -1;
        private bool talisDragActive;
        private Vector2 talisDragStart;

        //被顶掉的沉影掷回册位
        private struct Flyer
        {
            public int Key;
            public Vector2 From;
            public Vector2 To;
            public int Timer;
        }

        private const int FlyerLife = 22;
        private readonly List<Flyer> flyers = [];

        //屏内批注（回执不进聊天）
        private string noteText = string.Empty;
        private int noteTimer;
        private Color noteColor;
        private const int NoteFrames = 150;

        //涟漪：非热区点击=墨涟漪（暗色普通批），事件=水涟漪（加色批）
        private struct Ripple
        {
            public Vector2 Pos;
            public int Timer;
            public bool Ink;
        }

        private const int RippleLife = 26;
        private readonly List<Ripple> ripples = [];

        //册序：注册表全条目（含未沉）+ 已沉械奴，绘制/命中共用
        private readonly List<int> rosterKeys = [];
        private readonly List<bool> rosterCollected = [];

        private float stir;
        private int uncollectedShake = -1;
        private int uncollectedShakeTimer;

        private KikasaDomainPlayer Domain => player.GetModPlayer<KikasaDomainPlayer>();
        private KikasaVaultPlayer Vault => player.GetModPlayer<KikasaVaultPlayer>();
        private KikasaServantPlayer Servant => player.GetModPlayer<KikasaServantPlayer>();

        //M 键与风铃点击都走这里：别的全屏界面（任务书等）开着时不抢屏
        public override void Open() {
            if (!FullScreenUIHub.TryClaimScreen(this)) {
                return;
            }
            base.Open();
        }

        protected override void OnOpen() {
            Main.playerInventory = false;
            hoverKind = HoverKind.None;
            hoverIndex = -1;
            houndHover = wispHover = helpHover = 0f;
            Array.Clear(seatHover, 0, seatHover.Length);
            Array.Clear(seatStamp, 0, seatStamp.Length);
            Array.Clear(seatDeny, 0, seatDeny.Length);
            Array.Clear(seatShake, 0, seatShake.Length);
            rosterHover.Clear();
            vaultHover.Clear();
            flyers.Clear();
            ripples.Clear();
            carryKey = 0;
            carryOriginSeat = -1;
            noteTimer = 0;
            stir = 0.5f;
            uncollectedShake = -1;
            //真值基线取当前席位，开屏瞬间不虚报定妆
            KikasaServantPlayer servant = Servant;
            for (int i = 0; i < KikasaServantPlayer.SlotCount; i++) {
                lastSeatKeys[i] = servant.SlotKeyAt(i);
            }
            //祈雨绳基线同理
            Array.Clear(talisHover, 0, talisHover.Length);
            Array.Clear(talisStamp, 0, talisStamp.Length);
            Array.Clear(talisDeny, 0, talisDeny.Length);
            Array.Clear(talisShake, 0, talisShake.Length);
            fanSlot = -1;
            fanKeys.Clear();
            fanHover.Clear();
            talisDragSlot = -1;
            talisDragActive = false;
            KikasaTalismanStore talisStore = KikasaTalismanRegistry.DisplayStore;
            for (int i = 0; i < KikasaTalismanStore.SlotCount; i++) {
                lastTalisKeys[i] = talisStore?.Get(i);
            }
        }

        //==================== 更新 ====================

        public override void Update() {
            float a = OpenProgress;
            if (a < 0.01f) {
                return;
            }
            if (IsOpen && (player.dead || !player.active || HackTime.Active)) {
                Close();
            }

            BuildRoster();
            DiffSeatStates();
            DiffTalisStates();
            MaintainFan();

            Vector2 mouse = KikasaPanoramaTheme.UIMouse;
            bool inputAvailable = IsOpen && a > 0.9f;

            if (IsOpen) {
                //全屏界面：指针与滚轮两把锁每帧常驻
                player.mouseInterface = true;
                UIInputGuard.SuppressWeaponSwitch();
                PlayerInput.LockVanillaMouseScroll("CalamityOverhaul/KikasaPanorama");
                //吞掉背包开关键:原版 ToggleInv 在逻辑帧先跑、CloseOnEscape 在界面层后跑,
                //同帧会先开背包再关湖心景,表现即"ESC 优先呼背包"(反馈八·#3)。
                //每帧压 releaseInventory,ESC 只负责关本界面,关完下一帧背包键即恢复
                player.releaseInventory = false;
            }

            ResolveHover(mouse, inputAvailable);
            UpdateCarrySpring(mouse);

            if (inputAvailable && keyLeftPressState == KeyPressState.Pressed) {
                HandleLeftClick(mouse);
            }
            if (inputAvailable && keyRightPressState == KeyPressState.Pressed) {
                HandleRightClick(mouse);
            }
            UpdateTalisDrag(mouse, inputAvailable);

            //悬停缓动
            houndHover = MathHelper.Lerp(houndHover, hoverKind == HoverKind.Hound ? 1f : 0f, 0.16f);
            wispHover = MathHelper.Lerp(wispHover, hoverKind == HoverKind.Wisp ? 1f : 0f, 0.16f);
            helpHover = MathHelper.Lerp(helpHover, hoverKind == HoverKind.Help ? 1f : 0f, 0.16f);
            for (int i = 0; i < seatHover.Length; i++) {
                bool on = hoverKind == HoverKind.Seat && hoverIndex == i;
                seatHover[i] = MathHelper.Lerp(seatHover[i], on ? 1f : 0f, 0.2f);
                seatStamp[i] *= 0.9f;
                seatDeny[i] *= 0.88f;
                if (seatShake[i] > 0) {
                    seatShake[i]--;
                }
            }
            AlignLerpList(rosterHover, rosterKeys.Count);
            for (int i = 0; i < rosterHover.Count; i++) {
                bool on = hoverKind == HoverKind.Roster && hoverIndex == i;
                rosterHover[i] = MathHelper.Lerp(rosterHover[i], on ? 1f : 0f, 0.2f);
            }
            for (int i = 0; i < talisHover.Length; i++) {
                bool on = hoverKind == HoverKind.Talis && hoverIndex == i;
                talisHover[i] = MathHelper.Lerp(talisHover[i], on ? 1f : 0f, 0.2f);
                talisStamp[i] *= 0.9f;
                talisDeny[i] *= 0.88f;
                if (talisShake[i] > 0) {
                    talisShake[i]--;
                }
            }
            AlignLerpList(fanHover, fanKeys.Count);
            for (int i = 0; i < fanHover.Count; i++) {
                bool on = hoverKind == HoverKind.TalisFan && hoverIndex == i;
                fanHover[i] = MathHelper.Lerp(fanHover[i], on ? 1f : 0f, 0.22f);
            }
            AlignLerpList(vaultHover, Vault.Stored.Count);
            for (int i = 0; i < vaultHover.Count; i++) {
                bool on = hoverKind == HoverKind.Vault && hoverIndex == i;
                vaultHover[i] = MathHelper.Lerp(vaultHover[i], on ? 1f : 0f, 0.18f);
            }

            //杂项推进
            float restStir = Domain.Phase == KikasaDomainPhase.Opening
                || Domain.Phase == KikasaDomainPhase.Closing ? 0.45f : 0.12f;
            if (carryKey != 0) {
                restStir = MathF.Max(restStir, 0.3f);
            }
            stir = MathHelper.Lerp(stir, restStir, 0.06f);
            if (noteTimer > 0) {
                noteTimer--;
            }
            if (uncollectedShakeTimer > 0 && --uncollectedShakeTimer == 0) {
                uncollectedShake = -1;
            }
            for (int i = ripples.Count - 1; i >= 0; i--) {
                Ripple r = ripples[i];
                if (++r.Timer >= RippleLife) {
                    ripples.RemoveAt(i);
                }
                else {
                    ripples[i] = r;
                }
            }
            for (int i = flyers.Count - 1; i >= 0; i--) {
                Flyer f = flyers[i];
                if (++f.Timer >= FlyerLife) {
                    flyers.RemoveAt(i);
                }
                else {
                    flyers[i] = f;
                }
            }
        }

        private static void AlignLerpList(List<float> list, int count) {
            while (list.Count < count) {
                list.Add(0f);
            }
            if (list.Count > count) {
                list.RemoveRange(count, list.Count - count);
            }
        }

        /// <summary>册序重建：注册表全条目按进度序（未沉留座），已沉械奴排末尾</summary>
        private void BuildRoster() {
            rosterKeys.Clear();
            rosterCollected.Clear();
            KikasaServantPlayer servant = Servant;
            foreach (KikasaServantIndex.ServantEntry entry in KikasaServantIndex.AllEntries) {
                rosterKeys.Add(entry.CanonicalType);
                rosterCollected.Add(servant.IsCollected(entry.CanonicalType));
            }
            foreach (int key in servant.BuildCodexKeys()) {
                if (key < 0) {
                    rosterKeys.Add(key);
                    rosterCollected.Add(true);
                }
            }
        }

        /// <summary>
        /// 席位真值差分：外部改动（转盘/自动落座/沉溺入席）与本屏点放走同一条表现路径
        /// 落影定妆、离席掷回册位
        /// </summary>
        private void DiffSeatStates() {
            KikasaServantPlayer servant = Servant;
            for (int i = 0; i < KikasaServantPlayer.SlotCount; i++) {
                int now = servant.SlotKeyAt(i);
                int was = lastSeatKeys[i];
                if (now == was) {
                    continue;
                }
                if (now != 0) {
                    seatStamp[i] = 1f;
                    AddRipple(KikasaPanoramaTheme.SeatPos(i), ink: false);
                }
                if (was != 0 && was != carryKey) {
                    //被顶掉/卸下的旧影掷回它的册位（在手的不飞）
                    int rosterIdx = rosterKeys.IndexOf(was);
                    if (rosterIdx >= 0) {
                        flyers.Add(new Flyer {
                            Key = was,
                            From = KikasaPanoramaTheme.SeatPos(i),
                            To = RosterPos(rosterIdx),
                        });
                    }
                }
                lastSeatKeys[i] = now;
            }
        }

        private Vector2 RosterPos(int index)
            => new(KikasaPanoramaTheme.RosterX(index, rosterKeys.Count), KikasaPanoramaTheme.RosterY);

        //==================== 祈雨绳状态 ====================

        /// <summary>手中是否握着鬼伞（符位表挂在伞上，没伞看得见摸不着）</summary>
        private bool HoldingKikasa => KikasaData.TryGet(player.GetItem()) != null;

        /// <summary>
        /// 符位真值差分：本屏点挂、服务器回执、外部改动全走同一条表现路径
        /// 挂上定妆+批注，摘下批注；基线在 OnOpen 重置
        /// </summary>
        private void DiffTalisStates() {
            KikasaTalismanStore store = KikasaTalismanRegistry.DisplayStore;
            for (int i = 0; i < KikasaTalismanStore.SlotCount; i++) {
                string now = store?.Get(i);
                string was = lastTalisKeys[i];
                if (now == was) {
                    continue;
                }
                if (now != null) {
                    talisStamp[i] = 1f;
                    AddRipple(KikasaPanoramaTheme.TalisStripCenter(i), ink: false);
                    if (KikasaTalismanRegistry.TryGet(now, out KikasaTalismanDefinition hung)) {
                        PostNote(string.Format(NoteFuHungFormat.Value, hung.DisplayName.Value),
                            KikasaHudTheme.Glow(Rain));
                    }
                    SoundEngine.PlaySound(SoundID.Item76 with { Volume = 0.32f, Pitch = 0.35f, MaxInstances = 2 });
                }
                else if (was != null) {
                    if (KikasaTalismanRegistry.TryGet(was, out KikasaTalismanDefinition taken)) {
                        PostNote(string.Format(NoteFuTakenFormat.Value, taken.DisplayName.Value),
                            KikasaHudTheme.TextDim(Rain));
                    }
                    SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.4f, Pitch = -0.35f });
                }
                lastTalisKeys[i] = now;
            }
        }

        /// <summary>扇的存续检查：不持伞立即合拢；扇开着时持续对齐候选内容</summary>
        private void MaintainFan() {
            if (fanSlot < 0) {
                return;
            }
            if (!HoldingKikasa) {
                CloseFan(playSound: false);
                return;
            }
            BuildFanKeys();
            if (fanKeys.Count == 0) {
                CloseFan(playSound: false);
            }
        }

        /// <summary>候选扇内容：符箧全部已录 Key（SortOrder 序），符位有主时末尾加一张摘下位</summary>
        private void BuildFanKeys() {
            fanKeys.Clear();
            foreach (KikasaTalismanDefinition definition in KikasaTalismanOwned.GetOwnedOrdered(player)) {
                fanKeys.Add(definition.Key);
            }
            if (KikasaTalismanRegistry.DisplayStore?.Get(fanSlot) != null) {
                fanKeys.Add(null);
            }
        }

        private void OpenFan(int slot) {
            fanSlot = slot;
            BuildFanKeys();
            if (fanKeys.Count == 0) {
                fanSlot = -1;
                //符箧空空：拒绝也要指路
                PostNote(TalisNoneOwned.Value, KikasaHudTheme.Accent(Rain));
                DenyTalis(slot);
                return;
            }
            SoundEngine.PlaySound(SoundID.MenuOpen with { Volume = 0.35f, Pitch = 0.15f });
        }

        private void CloseFan(bool playSound = true) {
            if (fanSlot < 0) {
                return;
            }
            fanSlot = -1;
            fanKeys.Clear();
            fanHover.Clear();
            if (playSound) {
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.3f, Pitch = 0.1f });
            }
        }

        /// <summary>符位拒绝反馈：红闪+横震+闷响，与席位同一套语汇</summary>
        private void DenyTalis(int slot, bool playSound = true) {
            if (slot >= 0 && slot < talisDeny.Length) {
                talisDeny[slot] = 1f;
                talisShake[slot] = 14;
            }
            if (playSound) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.55f, Pitch = -0.6f });
            }
        }

        //==================== 悬停解析 ====================

        private void ResolveHover(Vector2 mouse, bool inputAvailable) {
            if (!inputAvailable) {
                SetHover(HoverKind.None, -1);
                return;
            }
            //候选扇浮在最上层，开着时先吃命中
            if (fanSlot >= 0) {
                for (int i = 0; i < fanKeys.Count; i++) {
                    if (KikasaPanoramaTheme.TalisFanHit(fanSlot, i, fanKeys.Count).Contains(mouse.ToPoint())) {
                        SetHover(HoverKind.TalisFan, i);
                        return;
                    }
                }
            }
            //祈雨绳符位
            for (int i = 0; i < KikasaTalismanStore.SlotCount; i++) {
                if (KikasaPanoramaTheme.TalisSlotHit(i).Contains(mouse.ToPoint())) {
                    SetHover(HoverKind.Talis, i);
                    return;
                }
            }
            //席位（圆命中）
            for (int i = 0; i < KikasaServantPlayer.SlotCount; i++) {
                if ((mouse - KikasaPanoramaTheme.SeatPos(i)).Length() < KikasaPanoramaTheme.SeatHitR) {
                    SetHover(HoverKind.Seat, i);
                    return;
                }
            }
            //册条目
            for (int i = 0; i < rosterKeys.Count; i++) {
                if ((mouse - RosterPos(i)).Length() < KikasaPanoramaTheme.RosterHitR) {
                    SetHover(HoverKind.Roster, i);
                    return;
                }
            }
            //湖藏格
            List<Item> stored = Vault.Stored;
            float half = KikasaPanoramaTheme.VaultFit * 0.5f + 9f;
            for (int i = 0; i < stored.Count; i++) {
                Vector2 c = KikasaPanoramaTheme.VaultCell(i);
                if (MathF.Abs(mouse.X - c.X) < half && MathF.Abs(mouse.Y - c.Y) < half) {
                    SetHover(HoverKind.Vault, i);
                    return;
                }
            }
            //两鬼：鬼身与名牌/动作牌一体成区，点字即点鬼
            KikasaDomainPlayer domain = Domain;
            float rain = Rain;
            if (KikasaPanoramaTheme.HoundHit.Contains(mouse.ToPoint())
                || BuildHoundPlate(domain, rain).Bounds.Contains(mouse.ToPoint())) {
                SetHover(HoverKind.Hound, 0);
                return;
            }
            if (KikasaPanoramaTheme.WispHit.Contains(mouse.ToPoint())
                || BuildWispPlate(domain, rain).Bounds.Contains(mouse.ToPoint())) {
                SetHover(HoverKind.Wisp, 0);
                return;
            }
            //页脚「?」重看钮
            if (HelpRect.Contains(mouse.ToPoint())) {
                SetHover(HoverKind.Help, 0);
                return;
            }
            SetHover(HoverKind.None, -1);
        }

        /// <summary>只在真正换到新热区时嘀一声。先清 None 再 Set 会让每帧都像刚进入</summary>
        private void SetHover(HoverKind kind, int index) {
            if (hoverKind == kind && hoverIndex == index) {
                return;
            }
            if (kind != HoverKind.None) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.25f, Volume = 0.3f });
            }
            hoverKind = kind;
            hoverIndex = index;
        }

        private void UpdateCarrySpring(Vector2 mouse) {
            if (carryKey == 0) {
                return;
            }
            //阻尼弹簧追光标，拾影带一点水的惯性
            Vector2 toMouse = mouse - carryPos;
            carryVel = carryVel * 0.78f + toMouse * 0.16f;
            carryPos += carryVel;
        }

        //==================== 点击分发 ====================

        private void HandleLeftClick(Vector2 mouse) {
            //扇开着时点向别处=合扇（点中扇/绳位在各自分支处理）
            if (fanSlot >= 0 && hoverKind != HoverKind.TalisFan && hoverKind != HoverKind.Talis) {
                CloseFan();
                return;
            }
            switch (hoverKind) {
                case HoverKind.Talis:
                    ClickTalisSlot(hoverIndex);
                    return;
                case HoverKind.TalisFan:
                    ClickFanItem(hoverIndex);
                    return;
                case HoverKind.Seat:
                    ClickSeat(hoverIndex);
                    return;
                case HoverKind.Roster:
                    ClickRoster(hoverIndex);
                    return;
                case HoverKind.Vault:
                    ClickVault(hoverIndex);
                    return;
                case HoverKind.Hound:
                    ClickHound();
                    return;
                case HoverKind.Wisp:
                    ClickWisp();
                    return;
                case HoverKind.Help:
                    //重看教程：清进度当场重讲，RestartFromHelp 里会合掉本屏
                    SoundEngine.PlaySound(SoundID.MenuOpen with { Volume = 0.45f, Pitch = 0.2f });
                    KikasaHudLead.RestartFromHelp();
                    return;
            }
            //空处：持影=收手（把影放回），空手=墨涟漪答话
            if (carryKey != 0) {
                CancelCarry();
                return;
            }
            AddRipple(mouse, ink: true);
            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.3f, Pitch = -0.7f, MaxInstances = 2 });
        }

        private void HandleRightClick(Vector2 mouse) {
            //拖着符时右键=收手，不落到摘符分支
            if (talisDragSlot >= 0) {
                talisDragSlot = -1;
                talisDragActive = false;
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.35f, Pitch = -0.7f });
                return;
            }
            if (fanSlot >= 0) {
                CloseFan();
                return;
            }
            if (carryKey != 0) {
                CancelCarry();
                return;
            }
            //右键已挂符位=直接摘下（结果批注由真值差分给）
            if (hoverKind == HoverKind.Talis
                && KikasaTalismanRegistry.DisplayStore?.Get(hoverIndex) != null) {
                if (!HoldingKikasa) {
                    PostNote(TalisNeedUmbrella.Value, KikasaHudTheme.Accent(Rain));
                    DenyTalis(hoverIndex);
                    return;
                }
                int slot = hoverIndex;
                if (!KikasaTalismanRegistry.TakeDownHeld(slot,
                    ok => { if (!ok) { DenyTalis(slot); } })) {
                    DenyTalis(slot);
                }
                return;
            }
            if (hoverKind == HoverKind.Seat && Servant.SlotKeyAt(hoverIndex) != 0) {
                int key = Servant.SlotKeyAt(hoverIndex);
                if (Servant.ClearSlot(hoverIndex)) {
                    PostNote(string.Format(NoteUnslottedFormat.Value,
                        KikasaServantPlayer.KeyDisplayName(key)), KikasaHudTheme.TextDim(Rain));
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.4f, Pitch = -0.6f });
                }
                return;
            }
            AddRipple(mouse, ink: true);
        }

        /// <summary>点席位：持影落影/换影；空手拾走驻影；空手点空席给指路批注</summary>
        private void ClickSeat(int seat) {
            KikasaServantPlayer servant = Servant;
            int occupant = servant.SlotKeyAt(seat);

            if (carryKey != 0) {
                if (occupant == carryKey) {
                    //把影放回它自己的席：收手不折腾
                    CancelCarry();
                    return;
                }
                if (servant.TrySetSlot(seat, carryKey)) {
                    if (carryKey < 0 && KikasaServantPlayer.CountStoredArms(Vault, -carryKey) <= 0) {
                        //械奴没原件：落席成立但凝不出形，先把话说明
                        PostNote(NoteArmsNoStock.Value, KikasaHudTheme.Accent(Rain));
                    }
                    else {
                        PostNote(string.Format(NotePlacedFormat.Value,
                            KikasaServantPlayer.KeyDisplayName(carryKey)), KikasaHudTheme.Glow(Rain));
                    }
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.5f, Pitch = -0.3f });
                    carryKey = 0;
                    carryOriginSeat = -1;
                }
                else {
                    Deny(seat);
                }
                return;
            }

            if (occupant != 0) {
                //空手拾走驻影（离席拾起：席位腾出，落回可再驻）
                carryKey = occupant;
                carryOriginSeat = seat;
                carryPos = KikasaPanoramaTheme.SeatPos(seat);
                carryVel = Vector2.Zero;
                servant.ClearSlot(seat);
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.5f, Pitch = -0.5f });
                return;
            }

            //空手点空席：指路而不是沉默
            PostNote(NotePickFirst.Value, KikasaHudTheme.TextDim(Rain));
            Deny(seat, playSound: false);
            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.4f, Pitch = -0.6f });
        }

        /// <summary>点册条目：已沉拾起（持影时换拾），未沉拒绝并指路</summary>
        private void ClickRoster(int index) {
            if (!rosterCollected[index]) {
                uncollectedShake = index;
                uncollectedShakeTimer = 14;
                PostNote(NoteNotCollected.Value, KikasaHudTheme.Accent(Rain));
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = -0.55f });
                return;
            }
            int key = rosterKeys[index];
            if (carryKey == key) {
                CancelCarry();
                return;
            }
            //持着别的影时直接换拾，少一次收手
            carryKey = key;
            carryOriginSeat = -1;
            carryPos = RosterPos(index);
            carryVel = Vector2.Zero;
            SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.5f, Pitch = -0.4f });
        }

        /// <summary>点湖藏：湖就绪当帧提取并合画看破水演出；未就绪只读拒答</summary>
        private void ClickVault(int index) {
            KikasaVaultPlayer vault = Vault;
            if (!vault.LakeReady) {
                PostNote(VaultViewOnlyLine.Value, KikasaHudTheme.TextDim(Rain));
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.55f, Pitch = -0.7f, MaxInstances = 2 });
                return;
            }
            Vector2 cell = KikasaPanoramaTheme.VaultCell(index);
            if (vault.BeginExtract(index)) {
                AddRipple(cell, ink: false);
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.55f, Pitch = 0.05f });
                //演出是主角：合画让位，破水点就在脚边
                Close();
            }
        }

        /// <summary>点恶犬：梦中提示归返键；拉得动直接合画入梦；差哪一步就把哪一步说清</summary>
        private void ClickHound() {
            KikasaDomainPlayer domain = Domain;
            string mutateKey = CWRKeySystem.Kikasa_DomainMutate
                .ToTooltipString(CWRKeySystem.Notbound.Value);
            if (domain.Phase == KikasaDomainPhase.Dreaming) {
                PostNote(string.Format(DreamReturnFormat.Value, mutateKey), KikasaHudTheme.Glow(Rain));
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.4f, Pitch = -0.3f });
                return;
            }
            if (domain.DreamPullReady) {
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.6f, Pitch = -0.6f });
                Close();
                KikasaDomain.TryDreamPull(player, out _);
                return;
            }
            //拒绝也要说清差哪一步（与名牌状态行同一份口径）
            PostNote(HoundBlockReason(domain), KikasaHudTheme.Accent(Rain));
            AddRipple(KikasaPanoramaTheme.HoundPos, ink: true);
            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = -0.55f });
        }

        /// <summary>点金焰：点燃/收火号令（统一走 KikasaWisp.TryToggle）；点不着说清差哪一步</summary>
        private void ClickWisp() {
            KikasaDomainPlayer domain = Domain;
            if (KikasaWisps.KikasaWisp.TryToggle(player)) {
                //受理：确认拍由命令本体播，这里只落屏内涟漪与结果批注
                PostNote(KikasaUIText.WispStateLine(domain),
                    domain.WispFireActive
                        ? KikasaWisps.KikasaWisp.Tint(KikasaWisps.KikasaWisp.GoldBody)
                        : KikasaHudTheme.TextDim(Rain));
                AddRipple(KikasaPanoramaTheme.WispPos + new Vector2(0f, 44f), ink: false);
                return;
            }
            //点不着：差哪一步就说哪一步
            string reason = KikasaUIText.WispBlockReason(domain) ?? KikasaUIText.NeedSettleLine.Value;
            PostNote(reason, KikasaHudTheme.Accent(Rain));
            AddRipple(KikasaPanoramaTheme.WispPos + new Vector2(0f, 44f), ink: true);
            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = -0.55f });
        }

        /// <summary>
        /// 点符位：持伞时开合候选扇；没伞把话说清并拒绝。
        /// 已挂结按下先记拖候，开扇挪到松手时结算（拖过阈值走换位，原地松手照旧开扇）
        /// </summary>
        private void ClickTalisSlot(int slot) {
            if (!HoldingKikasa) {
                PostNote(TalisNeedUmbrella.Value, KikasaHudTheme.Accent(Rain));
                DenyTalis(slot);
                return;
            }
            if (KikasaTalismanRegistry.DisplayStore?.Get(slot) != null) {
                talisDragSlot = slot;
                talisDragActive = false;
                talisDragStart = KikasaPanoramaTheme.UIMouse;
                return;
            }
            if (fanSlot == slot) {
                CloseFan();
                return;
            }
            OpenFan(slot);
        }

        /// <summary>光标下的符位下标，未命中 -1（拖拽落点用，几何与悬停同源）</summary>
        private static int TalisSlotAt(Vector2 mouse) {
            for (int i = 0; i < KikasaTalismanStore.SlotCount; i++) {
                if (KikasaPanoramaTheme.TalisSlotHit(i).Contains(mouse.ToPoint())) {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// 拖符换位推进：按住越过阈值即起拖（合扇+拾音），松手按落点结算——
        /// 落在别的绳结上互换/挪结（服务器拒绝落红闪），落空或拖回原结收手；
        /// 原地松手等价旧点击语义（开合候选扇）
        /// </summary>
        private void UpdateTalisDrag(Vector2 mouse, bool inputAvailable) {
            if (talisDragSlot < 0) {
                return;
            }
            //拖候期条件破裂（合屏/失伞/符被外部摘走）直接收手
            if (!inputAvailable || !HoldingKikasa
                || KikasaTalismanRegistry.DisplayStore?.Get(talisDragSlot) == null) {
                talisDragSlot = -1;
                talisDragActive = false;
                return;
            }
            if (keyLeftPressState is KeyPressState.Pressed or KeyPressState.Held) {
                if (!talisDragActive && (mouse - talisDragStart).Length() > 10f) {
                    talisDragActive = true;
                    CloseFan(playSound: false);
                    SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.4f, Pitch = -0.45f });
                }
                return;
            }

            //松手结算
            int source = talisDragSlot;
            bool dragged = talisDragActive;
            talisDragSlot = -1;
            talisDragActive = false;
            if (!dragged) {
                if (fanSlot == source) {
                    CloseFan();
                }
                else {
                    OpenFan(source);
                }
                return;
            }
            int target = TalisSlotAt(mouse);
            if (target < 0 || target == source) {
                //拖空处/拖回原结=收手不折腾，落一声轻响作回应
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.35f, Pitch = -0.7f });
                return;
            }
            if (!KikasaTalismanRegistry.SwapHeld(source, target,
                ok => { if (!ok) { DenyTalis(target); } })) {
                DenyTalis(target);
            }
        }

        /// <summary>
        /// 点候选：发挂/摘请求后合扇；成功表现由真值差分点火，
        /// 失败（含服务器拒绝的回执）落符位红闪
        /// </summary>
        private void ClickFanItem(int index) {
            if (fanSlot < 0 || index < 0 || index >= fanKeys.Count) {
                return;
            }
            int slot = fanSlot;
            string key = fanKeys[index];
            KikasaTalismanStore store = KikasaTalismanRegistry.DisplayStore;

            //摘下位
            if (key == null) {
                if (!KikasaTalismanRegistry.TakeDownHeld(slot,
                    ok => { if (!ok) { DenyTalis(slot); } })) {
                    DenyTalis(slot);
                }
                CloseFan();
                return;
            }
            //点到已挂在本位的符：等于合扇
            if (store?.Get(slot) == key) {
                CloseFan();
                return;
            }
            //已挂他位：换位走绳上拖拽，扇内直接把话说清
            if (store != null && store.Contains(key)) {
                PostNote(TalisAlreadyHung.Value, KikasaHudTheme.Accent(Rain));
                DenyTalis(slot);
                return;
            }
            if (!KikasaTalismanRegistry.HangHeld(slot, key,
                ok => { if (!ok) { DenyTalis(slot); } })) {
                DenyTalis(slot);
            }
            CloseFan(playSound: false);
        }

        private void CancelCarry() {
            //拾自席上的影收手=卸下（席在拾起时已腾出），把结果写成批注；册里拾的收手不聒噪
            if (carryOriginSeat >= 0 && carryKey != 0) {
                PostNote(string.Format(NoteUnslottedFormat.Value,
                    KikasaServantPlayer.KeyDisplayName(carryKey)), KikasaHudTheme.TextDim(Rain));
            }
            carryKey = 0;
            carryOriginSeat = -1;
            SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.45f, Pitch = -0.7f });
        }

        /// <summary>拒绝反馈：席圈红闪+横震+闷响，点击必有可见回应</summary>
        private void Deny(int seat, bool playSound = true) {
            if (seat >= 0 && seat < seatDeny.Length) {
                seatDeny[seat] = 1f;
                seatShake[seat] = 14;
            }
            if (playSound) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.55f, Pitch = -0.6f });
            }
        }

        private void PostNote(string text, Color color) {
            noteText = text;
            noteColor = color;
            noteTimer = NoteFrames;
        }

        private void AddRipple(Vector2 pos, bool ink) {
            if (ripples.Count < 12) {
                ripples.Add(new Ripple { Pos = pos, Ink = ink });
            }
        }

        /// <summary>翻转期把镜面预览混进浸染，色先于形态半步（与风铃 HUD 同一份换算）</summary>
        private float Rain => KikasaHudTheme.EffectiveRain(Domain);

        //==================== 两鬼名牌 ====================
        //常驻只留 名牌+状态行+动作牌，数值与键位全走悬停面板；
        //布局/命中/绘制同源，点字即点鬼（反馈九：提示文字点不动）

        /// <summary>鬼名牌一帧布局：名牌 + 状态行 + 可选动作牌</summary>
        private readonly struct GhostPlate
        {
            public string Title { get; init; }
            public string Status { get; init; }
            public Color StatusColor { get; init; }
            /// <summary>动作牌文字，null=本态无动作</summary>
            public string Tag { get; init; }
            public Vector2 TitlePos { get; init; }
            public Vector2 StatusPos { get; init; }
            public Rectangle TagRect { get; init; }
            /// <summary>名牌+状态+动作牌的总包围盒（并入鬼的热区）</summary>
            public Rectangle Bounds { get; init; }
        }

        private const float PlateTitleScale = 1.0f;
        private const float PlateStatusScale = 0.9f;
        private const float PlateTagScale = 0.9f;

        /// <summary>恶犬入不了梦时差的那一步（状态行与点击拒答共用一份口径）</summary>
        private string HoundBlockReason(KikasaDomainPlayer domain) {
            if (!domain.AnyActive) {
                return string.Format(NeedDomainFormat.Value,
                    CWRKeySystem.Legend_Domain.ToTooltipString(CWRKeySystem.Notbound.Value));
            }
            if (domain.RiseT < 0.999f) {
                return NeedFullWater.Value;
            }
            //满水但非 Open 稳态：正处翻转/入梦演出等过渡
            return KikasaUIText.NeedSettleLine.Value;
        }

        private GhostPlate BuildHoundPlate(KikasaDomainPlayer domain, float rain) {
            string status;
            Color col;
            string tag = null;
            if (domain.Phase == KikasaDomainPhase.Dreaming) {
                status = InDreamLine.Value;
                col = KikasaHudTheme.Glow(rain);
            }
            else if (domain.DreamPullReady) {
                status = ReflectAwake.Value;
                col = new Color(235, 150, 90);
                tag = ActionDreamTag.Value;
            }
            else {
                status = HoundBlockReason(domain);
                col = KikasaHudTheme.TextDim(rain);
            }
            Vector2 pos = KikasaPanoramaTheme.HoundPos;
            Vector2 anchor = new(pos.X + KikasaPanoramaTheme.HoundHeight * 1.05f,
                pos.Y - KikasaPanoramaTheme.HoundHeight * 0.55f);
            return LayoutPlate(anchor, rightAlign: false, HoundTitle.Value, status, col, tag);
        }

        private GhostPlate BuildWispPlate(KikasaDomainPlayer domain, float rain) {
            string status;
            Color col;
            string tag = null;
            string block = KikasaUIText.WispBlockReason(domain);
            if (domain.WispFireActive) {
                status = KikasaUIText.WispStateLine(domain);
                col = KikasaWisps.KikasaWisp.Tint(KikasaWisps.KikasaWisp.GoldBody);
                tag = ActionSnuffTag.Value;
            }
            else if (block == null) {
                status = WispIdle.Value;
                col = KikasaHudTheme.TextDim(rain);
                tag = ActionIgniteTag.Value;
            }
            else {
                status = block;
                col = KikasaHudTheme.TextDim(rain);
            }
            Vector2 pos = KikasaPanoramaTheme.WispPos;
            //栈放金焰左侧，行向火收拢（右对齐）
            Vector2 anchor = new(pos.X - 66f, pos.Y - 58f);
            return LayoutPlate(anchor, rightAlign: true, WispTitle.Value, status, col, tag);
        }

        /// <summary>名牌排版：anchor 为左上角（rightAlign 时为右上角），包围盒外扩 6px</summary>
        private static GhostPlate LayoutPlate(Vector2 anchor, bool rightAlign, string title,
            string status, Color statusColor, string tag) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 titleSize = font.MeasureString(title) * PlateTitleScale;
            Vector2 statusSize = font.MeasureString(status) * PlateStatusScale;
            float statusY = anchor.Y + titleSize.Y + 4f;
            float maxW = MathF.Max(titleSize.X, statusSize.X);
            float bottom = statusY + statusSize.Y;
            Rectangle tagRect = Rectangle.Empty;
            if (tag != null) {
                Vector2 tagSize = font.MeasureString(tag) * PlateTagScale;
                int tagW = (int)(tagSize.X + 24f);
                int tagH = (int)(tagSize.Y + 10f);
                tagRect = new Rectangle((int)(rightAlign ? anchor.X - tagW : anchor.X),
                    (int)(bottom + 8f), tagW, tagH);
                maxW = MathF.Max(maxW, tagW);
                bottom = tagRect.Bottom;
            }
            float left = rightAlign ? anchor.X - maxW : anchor.X;
            return new GhostPlate {
                Title = title,
                Status = status,
                StatusColor = statusColor,
                Tag = tag,
                TitlePos = new Vector2(rightAlign ? anchor.X - titleSize.X : anchor.X, anchor.Y),
                StatusPos = new Vector2(rightAlign ? anchor.X - statusSize.X : anchor.X, statusY),
                TagRect = tagRect,
                Bounds = new Rectangle((int)left - 6, (int)anchor.Y - 6,
                    (int)maxW + 12, (int)(bottom - anchor.Y) + 12),
            };
        }

        /// <summary>
        /// 名牌绘制：题 1.0 + 状态 0.9 + 动作牌。
        /// 动作牌=实底+贴身投影+边线+呼吸辉光（与悬浮面板同族材质），读得出可点
        /// </summary>
        private static void DrawGhostPlate(SpriteBatch sb, in GhostPlate plate, float hover,
            float a, float rain, float time, float seed) {
            Utils.DrawBorderString(sb, plate.Title, plate.TitlePos,
                KikasaHudTheme.Text(rain) * a, PlateTitleScale);
            Utils.DrawBorderString(sb, plate.Status, plate.StatusPos,
                plate.StatusColor * a, PlateStatusScale);
            if (plate.Tag == null) {
                return;
            }
            Rectangle tag = plate.TagRect;
            float breath = KikasaPanoramaTheme.Breath(time, seed, 2.2f);
            Color glow = KikasaHudTheme.Glow(rain);
            Texture2D px = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            //牌下垫一枚呼吸辉光示意可点
            KikasaVaultRenderer.BeginAdditive(sb);
            KikasaVaultRenderer.DrawGlowDot(sb, tag.Center.ToVector2(), tag.Width * 0.62f,
                glow * ((0.10f + breath * 0.07f + hover * 0.12f) * a));
            KikasaVaultRenderer.RestoreUIBatch(sb);
            sb.Draw(px, new Rectangle(tag.X + 2, tag.Y + 2, tag.Width, tag.Height), src,
                Color.Black * (0.4f * a));
            sb.Draw(px, tag, src, KikasaHudTheme.Void(rain) * (0.92f * a));
            Color edge = glow * ((0.45f + breath * 0.15f + hover * 0.35f) * a);
            KikasaVaultRenderer.DrawLine(sb, new Vector2(tag.X, tag.Y),
                new Vector2(tag.Right, tag.Y), 1.2f, edge);
            KikasaVaultRenderer.DrawLine(sb, new Vector2(tag.X, tag.Bottom),
                new Vector2(tag.Right, tag.Bottom), 1.2f, edge * 0.7f);
            KikasaVaultRenderer.DrawLine(sb, new Vector2(tag.X, tag.Y),
                new Vector2(tag.X, tag.Bottom), 1.2f, edge * 0.85f);
            KikasaVaultRenderer.DrawLine(sb, new Vector2(tag.Right, tag.Y),
                new Vector2(tag.Right, tag.Bottom), 1.2f, edge * 0.85f);
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 tSize = font.MeasureString(plate.Tag) * PlateTagScale;
            Utils.DrawBorderString(sb, plate.Tag,
                new Vector2(tag.Center.X - tSize.X * 0.5f, tag.Center.Y - tSize.Y * 0.5f),
                Color.Lerp(glow, KikasaHudTheme.Text(rain), hover) * a, PlateTagScale);
        }

        //==================== 绘制 ====================

        public override void Draw(SpriteBatch sb) {
            float a = OpenProgress;
            if (a < 0.01f) {
                return;
            }
            KikasaDomainPlayer domain = Domain;
            KikasaVaultPlayer vault = Vault;
            KikasaServantPlayer servant = Servant;
            float time = Main.GlobalTimeWrappedHourly;
            float rain = Rain;
            float rise = domain.AnyActive ? domain.RiseProgress : 0f;
            float waterUv = KikasaPanoramaTheme.WaterUv(rise);
            float uiW = KikasaPanoramaTheme.UIScreenW;
            float uiH = KikasaPanoramaTheme.UIScreenH;
            Rectangle full = new(0, 0, (int)uiW + 2, (int)uiH + 2);
            float waterPixY = uiH * waterUv;
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            //1 背景：整屏血湖夜景（水面辉光恒满，湖力已随主动化裁撤）
            float effStir = MathHelper.Clamp(stir + (1f - a) * 0.4f, 0f, 1f);
            KikasaPanoramaRenderer.DrawBackdrop(sb, full, a, rain, waterUv, 1f - rise,
                effStir, 1f, domain.WispT,
                vault.Stored.Count / (float)KikasaVaultPlayer.Capacity);

            //铺开到能看清内容才落笔
            float detailA = MathHelper.Clamp((a - 0.45f) / 0.55f, 0f, 1f);
            if (detailA < 0.02f) {
                return;
            }

            //2 题头
            DrawHeader(sb, font, detailA, rain, domain, time);

            //2.5 天带祈雨绳（符位与挂符）
            DrawTalisZone(sb, font, detailA, rain, time);

            //3 两鬼
            DrawHoundGhost(sb, detailA, rain, domain, waterPixY, time);
            DrawWispGhost(sb, detailA, rain, domain, time);

            //4 编成区：席位 + 组合边 + 收集册
            DrawEdges(sb, font, detailA, rain, time);
            DrawSeats(sb, font, detailA, rain, servant, waterPixY, time);
            DrawRoster(sb, font, detailA, rain, servant, waterPixY, time);

            //5 湖藏
            DrawVault(sb, font, detailA, rain, vault, waterPixY, time);

            //5.5 候选扇浮层（压在诸区之上）
            DrawTalisFan(sb, font, detailA, rain, time);

            //6 掷回册位的飞影与拾在手上的影；拖中的符压最上
            DrawFlyers(sb, detailA, rain, time);
            DrawCarry(sb, detailA, rain, time);
            DrawTalisDrag(sb, detailA, rain, time);

            //7 涟漪 / 批注 / 页脚 / 悬停说明面板
            DrawRipples(sb, detailA, rain);
            DrawNoteAndFooter(sb, font, detailA, rain);
            DrawHoverTipPanel(sb, detailA, rain, servant, vault);
        }

        //====== 题头 ======

        private void DrawHeader(SpriteBatch sb, DynamicSpriteFont font, float a, float rain,
            KikasaDomainPlayer domain, float time) {
            string title = Title.Value;
            const float titleScale = 1.15f;
            Vector2 size = font.MeasureString(title) * titleScale;
            Vector2 pos = KikasaPanoramaTheme.TitlePos - size * 0.5f;
            Utils.DrawBorderString(sb, title, pos, KikasaHudTheme.Text(rain) * a, titleScale);

            //题头右侧一枚伞章，与既有伞章同一支笔
            KikasaVaultRenderer.DrawSeal(sb,
                KikasaPanoramaTheme.TitlePos + new Vector2(size.X * 0.5f + 34f, 4f), 15f,
                0.9f * a, time, reveal: a,
                KikasaHudTheme.TextDim(rain), KikasaHudTheme.Accent(rain), KikasaHudTheme.Glow(rain));
        }

        //====== 祈雨绳 ======

        /// <summary>
        /// 天带祈雨绳：垂弧麻绳 + 三符位。挂符=雨浸符纸随风摆，空位=断续绳环；
        /// 没伞整区沉暗并挂一行声明；定妆/拒绝与席位同一套语汇
        /// </summary>
        private void DrawTalisZone(SpriteBatch sb, DynamicSpriteFont font, float a, float rain, float time) {
            bool holding = HoldingKikasa;
            float zoneA = (holding ? 1f : 0.55f) * a;
            KikasaPanoramaRenderer.DrawTalisRope(sb, rain, zoneA, time);

            //区题挂在左锚上方
            Vector2 left = KikasaPanoramaTheme.TalisRopeLeft;
            Utils.DrawBorderString(sb, TalisTitle.Value, new Vector2(left.X - 6f, left.Y - 34f),
                KikasaHudTheme.Text(rain) * zoneA, 1.0f);
            //没伞：右锚上方沉声明（不可用先于点击可见），超宽换行、块底锚定向上生长
            if (!holding) {
                const float needScale = 0.85f;
                List<string> needLines = VaultUtils.WrapText(TalisNeedUmbrella.Value, font,
                    420f, needScale);
                float needLineH = font.MeasureString("A").Y * needScale + 2f;
                float needY = KikasaPanoramaTheme.TalisRopeRight.Y - 30f
                    - (needLines.Count - 1) * needLineH;
                foreach (string line in needLines) {
                    float lw = font.MeasureString(line).X * needScale;
                    Utils.DrawBorderString(sb, line,
                        new Vector2(KikasaPanoramaTheme.TalisRopeRight.X - lw, needY),
                        KikasaHudTheme.TextDim(rain) * (0.9f * a), needScale);
                    needY += needLineH;
                }
            }

            KikasaTalismanStore store = KikasaTalismanRegistry.DisplayStore;
            for (int i = 0; i < KikasaTalismanStore.SlotCount; i++) {
                Vector2 ropePoint = KikasaPanoramaTheme.TalisRopePoint(KikasaPanoramaTheme.TalisSlotU(i));
                if (talisShake[i] > 0) {
                    ropePoint.X += MathF.Sin(talisShake[i] * 1.3f) * 3f;
                }
                float hover = talisHover[i];
                string key = store?.Get(i);

                if (key == null) {
                    Color dash = (fanSlot == i ? KikasaHudTheme.Glow(rain) : KikasaHudTheme.TextDim(rain))
                        * (0.4f + hover * 0.35f);
                    KikasaPanoramaRenderer.DrawEmptyTalisSlot(sb, ropePoint, dash, zoneA, time);
                    //拒绝红闪落在空结上
                    if (talisDeny[i] > 0.02f) {
                        KikasaVaultRenderer.DrawRing(sb, ropePoint + new Vector2(0f, 14f), 9f, 4f,
                            new Color(226, 74, 60) * (talisDeny[i] * a));
                    }
                    continue;
                }

                //钟摆风摆：悬停微抬，鬼雨态摆得更活
                float sway = MathF.Sin(time * 1.35f + i * 2.13f) * (0.05f + rain * 0.03f)
                    + hover * 0.02f;
                Vector2 size = KikasaPanoramaTheme.TalisStripSize;
                Vector2 down = (MathHelper.PiOver2 + sway).ToRotationVector2();
                Vector2 top = ropePoint + down * KikasaPanoramaTheme.TalisCordLen;
                //拖走的符原结压暗成拓空痕，符身跟着光标走（DrawTalisDrag）
                float stripA = talisDragActive && talisDragSlot == i ? zoneA * 0.35f : zoneA;
                KikasaPanoramaRenderer.DrawTalisCord(sb, ropePoint, top, rain, stripA);

                //符纸活着的湿度：底息 + 鬼雨态整体更湿
                float soak = 0.20f + 0.07f * MathF.Sin(time * 0.9f + i * 2.9f) + rain * 0.28f;
                KikasaTalismanPaperDraw.DrawUI(sb, top, sway, size,
                    stripA * (0.92f + hover * 0.1f), soak, time + i * 1.73f);

                Color accent = KikasaTalismanRegistry.TryGet(key, out KikasaTalismanDefinition def)
                    ? def.InkAccent : KikasaTalismanPaperDraw.Sheen;
                KikasaTalismanGlyph.DrawInk(sb, key, top + down * (size.Y * 0.40f), size.X * 1.18f,
                    stripA, KikasaTalismanPaperDraw.Ink, accent, time, sway);

                Vector2 stripMid = top + down * (size.Y * 0.5f);
                //定妆：挂上当帧一圈白闪
                if (talisStamp[i] > 0.02f) {
                    KikasaVaultRenderer.BeginAdditive(sb);
                    float t = 1f - talisStamp[i];
                    KikasaVaultRenderer.DrawRing(sb, stripMid, 12f + t * 24f, (12f + t * 24f) * 0.45f,
                        KikasaHudTheme.Glow(rain) * (0.6f * talisStamp[i] * a));
                    KikasaVaultRenderer.RestoreUIBatch(sb);
                }
                //拒绝红闪
                if (talisDeny[i] > 0.02f) {
                    KikasaVaultRenderer.DrawRing(sb, stripMid, size.X * 0.75f, size.X * 0.3f,
                        new Color(226, 74, 60) * (talisDeny[i] * a));
                }
            }
        }

        /// <summary>候选扇：符位正下的多行网格迷你符纸，末位可为摘下位；当前挂符带亮环</summary>
        private void DrawTalisFan(SpriteBatch sb, DynamicSpriteFont font, float a, float rain, float time) {
            if (fanSlot < 0 || fanKeys.Count == 0) {
                return;
            }
            string current = KikasaTalismanRegistry.DisplayStore?.Get(fanSlot);
            //符位到扇的引线：终点取网格首行上缘中点
            Vector2 stripBottom = KikasaPanoramaTheme.TalisStripCenter(fanSlot)
                + new Vector2(0f, KikasaPanoramaTheme.TalisStripSize.Y * 0.5f);
            KikasaVaultRenderer.DrawLine(sb, stripBottom,
                KikasaPanoramaTheme.TalisFanTopAnchor(fanSlot, fanKeys.Count), 1.1f,
                KikasaHudTheme.TextDim(rain) * (0.4f * a));

            for (int i = 0; i < fanKeys.Count; i++) {
                Vector2 c = KikasaPanoramaTheme.TalisFanPos(fanSlot, i, fanKeys.Count);
                float hover = i < fanHover.Count ? fanHover[i] : 0f;
                c.Y -= hover * 4f;
                string key = fanKeys[i];

                if (key == null) {
                    //摘下位：断续空结 + 一行小注
                    KikasaPanoramaRenderer.DrawDashedSocket(sb, c, 9f,
                        KikasaHudTheme.TextDim(rain) * ((0.45f + hover * 0.4f) * a), time, 0.25f);
                    string label = TalisTakeDownLabel.Value;
                    Vector2 lSize = font.MeasureString(label) * 0.85f;
                    Utils.DrawBorderString(sb, label,
                        new Vector2(c.X - lSize.X * 0.5f, c.Y + 14f),
                        KikasaHudTheme.TextDim(rain) * ((0.7f + hover * 0.3f) * a), 0.85f);
                    continue;
                }

                bool isCurrent = key == current;
                Vector2 size = KikasaPanoramaTheme.TalisFanSize;
                float sway = MathF.Sin(time * 1.5f + i * 1.9f) * 0.04f;
                Vector2 down = (MathHelper.PiOver2 + sway).ToRotationVector2();
                Vector2 top = c - down * (size.Y * 0.5f);
                float itemA = (isCurrent ? 1f : 0.82f + hover * 0.18f) * a;
                KikasaTalismanPaperDraw.DrawUI(sb, top, sway, size, itemA,
                    0.18f + rain * 0.2f, time + i * 2.31f);
                Color accent = KikasaTalismanRegistry.TryGet(key, out KikasaTalismanDefinition def)
                    ? def.InkAccent : KikasaTalismanPaperDraw.Sheen;
                KikasaTalismanGlyph.DrawInk(sb, key, top + down * (size.Y * 0.42f), size.X * 1.2f,
                    itemA, KikasaTalismanPaperDraw.Ink, accent, time, sway);

                //当前挂符：一圈稳亮环
                if (isCurrent) {
                    KikasaVaultRenderer.BeginAdditive(sb);
                    KikasaVaultRenderer.DrawRing(sb, c, size.X * 0.9f, size.X * 0.34f,
                        KikasaHudTheme.Glow(rain) * (0.3f * a));
                    KikasaVaultRenderer.RestoreUIBatch(sb);
                }
            }

            //网格脚注：牌序规则（换符/选符两入口共用本方法，都看得到）
            //换行 + 暗水衬底，不再单行横贯整画（反馈九·视觉杂乱）
            string rule = TalisStackRule.Value;
            const float ruleScale = 0.85f;
            float ruleMaxW = MathF.Min(520f, KikasaPanoramaTheme.UIScreenW - 32f);
            List<string> ruleLines = VaultUtils.WrapText(rule, font, ruleMaxW, ruleScale);
            if (ruleLines.Count == 0) {
                return;
            }
            float ruleLineH = font.MeasureString("A").Y * ruleScale + 2f;
            float blockW = 0f;
            foreach (string line in ruleLines) {
                blockW = MathF.Max(blockW, font.MeasureString(line).X * ruleScale);
            }
            float blockH = ruleLines.Count * ruleLineH;
            float cx = MathHelper.Clamp(
                KikasaPanoramaTheme.TalisFanTopAnchor(fanSlot, fanKeys.Count).X,
                16f + blockW * 0.5f,
                MathF.Max(16f + blockW * 0.5f, KikasaPanoramaTheme.UIScreenW - 16f - blockW * 0.5f));
            float topY = KikasaPanoramaTheme.TalisFanPos(fanSlot, fanKeys.Count - 1, fanKeys.Count).Y
                + KikasaPanoramaTheme.TalisFanSize.Y * 0.5f + 24f;
            Texture2D pxTex = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Rectangle back = new((int)(cx - blockW * 0.5f) - 10, (int)topY - 6,
                (int)blockW + 20, (int)blockH + 12);
            sb.Draw(pxTex, new Rectangle(back.X + 2, back.Y + 3, back.Width, back.Height), src,
                Color.Black * (0.4f * a));
            sb.Draw(pxTex, back, src, KikasaHudTheme.Void(rain) * (0.92f * a));
            KikasaVaultRenderer.DrawLine(sb, new Vector2(back.X + 2, back.Y),
                new Vector2(back.Right - 2, back.Y), 1.2f, KikasaHudTheme.Glow(rain) * (0.45f * a));
            float ry = topY;
            foreach (string line in ruleLines) {
                float lw = font.MeasureString(line).X * ruleScale;
                Utils.DrawBorderString(sb, line, new Vector2(cx - lw * 0.5f, ry),
                    KikasaHudTheme.TextDim(rain) * (0.9f * a), ruleScale);
                ry += ruleLineH;
            }
        }

        /// <summary>拖在指下的符：迷你符身随光标走，源结垂一缕细引线交代来处</summary>
        private void DrawTalisDrag(SpriteBatch sb, float a, float rain, float time) {
            if (!talisDragActive || talisDragSlot < 0) {
                return;
            }
            string key = KikasaTalismanRegistry.DisplayStore?.Get(talisDragSlot);
            if (key == null) {
                return;
            }
            Vector2 mouse = KikasaPanoramaTheme.UIMouse;
            KikasaVaultRenderer.DrawLine(sb, KikasaPanoramaTheme.TalisStripCenter(talisDragSlot),
                mouse, 1.1f, KikasaHudTheme.TextDim(rain) * (0.35f * a));
            Vector2 size = KikasaPanoramaTheme.TalisFanSize;
            float sway = MathF.Sin(time * 1.8f) * 0.05f;
            Vector2 down = (MathHelper.PiOver2 + sway).ToRotationVector2();
            Vector2 top = mouse - down * (size.Y * 0.5f);
            KikasaTalismanPaperDraw.DrawUI(sb, top, sway, size, 0.95f * a,
                0.2f + rain * 0.2f, time);
            Color accent = KikasaTalismanRegistry.TryGet(key, out KikasaTalismanDefinition def)
                ? def.InkAccent : KikasaTalismanPaperDraw.Sheen;
            KikasaTalismanGlyph.DrawInk(sb, key, top + down * (size.Y * 0.42f), size.X * 1.2f,
                0.95f * a, KikasaTalismanPaperDraw.Ink, accent, time, sway);
        }

        //====== 两鬼 ======

        private void DrawHoundGhost(SpriteBatch sb, float a, float rain,
            KikasaDomainPlayer domain, float waterPixY, float time) {
            //姿态：鬼梦立嚎 > 倒影醒/被注视昂首 > 垂首打盹
            float howl = domain.Phase == KikasaDomainPhase.Dreaming ? 1f : 0f;
            float alert = MathF.Max(domain.HoundReflection ? 1f : 0f, houndHover) * (1f - howl);
            float idle = 1f - MathF.Max(alert, howl);
            Vector2 pos = KikasaPanoramaTheme.HoundPos;
            //倒影只在水位接近满时可见，免得镜像悬在半空
            float reflGate = MathHelper.Clamp((domain.RiseT - 0.9f) / 0.08f, 0f, 1f);
            KikasaPanoramaRenderer.DrawInkHound(sb, pos, KikasaPanoramaTheme.HoundHeight,
                idle, alert, howl, houndHover, rain, MathHelper.Clamp(stir, 0f, 1f),
                domain.FlipBoil, waterPixY, reflGate, a, time);

            //名牌+状态行+动作牌；增益数值与键位路径都在悬停面板里
            DrawGhostPlate(sb, BuildHoundPlate(domain, rain), houndHover, a, rain, time, 3.7f);
        }

        private void DrawWispGhost(SpriteBatch sb, float a, float rain,
            KikasaDomainPlayer domain, float time) {
            Vector2 pos = KikasaPanoramaTheme.WispPos;
            KikasaPanoramaRenderer.DrawWispGhost(sb, pos, domain.WispT, domain.WispQuench,
                rain, wispHover, a, time);

            //名牌+状态行+动作牌（右对齐向火收拢）；数值都在悬停面板里
            DrawGhostPlate(sb, BuildWispPlate(domain, rain), wispHover, a, rain, time, 8.1f);
        }

        //====== 编成区 ======

        /// <summary>组合边：已成的亮水脉具名；持影悬停席位时预演将成的边（虚线）</summary>
        private void DrawEdges(SpriteBatch sb, DynamicSpriteFont font, float a, float rain, float time) {
            KikasaServantPlayer servant = Servant;
            Span<KikasaAffinity> now = stackalloc KikasaAffinity[KikasaServantPlayer.SlotCount];
            for (int i = 0; i < KikasaServantPlayer.SlotCount; i++) {
                now[i] = servant.SlotAffinity(i);
            }

            //将成预演：持影悬停席位时按假设编成先画虚线
            bool preview = carryKey != 0 && hoverKind == HoverKind.Seat;
            Span<KikasaAffinity> next = stackalloc KikasaAffinity[KikasaServantPlayer.SlotCount];
            if (preview) {
                now.CopyTo(next);
                int from = servant.SlotIndexOf(carryKey);
                if (from >= 0) {
                    next[from] = KikasaAffinity.None;
                }
                next[hoverIndex] = KikasaServantPlayer.AffinityOfKey(carryKey);
            }

            DrawEdgePair(sb, font, a, rain, time, now, next, preview,
                KikasaAffinity.Flame, KikasaAffinity.Nightmare, EdgeDreamFire.Value);
            DrawEdgePair(sb, font, a, rain, time, now, next, preview,
                KikasaAffinity.Flame, KikasaAffinity.Rain, EdgeBoilRain.Value);
            DrawEdgePair(sb, font, a, rain, time, now, next, preview,
                KikasaAffinity.Nightmare, KikasaAffinity.Rain, EdgeRainNightmare.Value);

            //三影镇湖：三席满且三系齐，环着三席一圈共鸣
            bool tri = KikasaEffigyBoard.HasTriSeal(player);
            if (tri) {
                float breath = KikasaPanoramaTheme.Breath(time, 5.2f, 1.8f);
                Vector2 c = KikasaPanoramaTheme.SeatPos(1);
                float ringRx = (KikasaPanoramaTheme.SeatPos(2).X - KikasaPanoramaTheme.SeatPos(0).X)
                    * 0.5f + KikasaPanoramaTheme.SeatHitR + 26f;
                KikasaVaultRenderer.BeginAdditive(sb);
                KikasaVaultRenderer.DrawRing(sb, c + new Vector2(0f, 6f),
                    ringRx, 36f + breath * 4f, KikasaHudTheme.Glow(rain) * (0.16f * a));
                KikasaVaultRenderer.RestoreUIBatch(sb);
                string name = EdgeTriSeal.Value;
                Vector2 size = font.MeasureString(name) * 0.9f;
                Utils.DrawBorderString(sb, name,
                    new Vector2(c.X - size.X * 0.5f, c.Y - KikasaPanoramaTheme.SeatHitR - 40f),
                    KikasaHudTheme.Glow(rain) * (0.9f * a), 0.9f);
            }
        }

        /// <summary>找一对能供出该组合的席位画水脉；已成走亮线，预演走虚线</summary>
        private void DrawEdgePair(SpriteBatch sb, DynamicSpriteFont font, float a, float rain,
            float time, Span<KikasaAffinity> now, Span<KikasaAffinity> next, bool preview,
            KikasaAffinity need1, KikasaAffinity need2, string name) {
            static bool Match(KikasaAffinity af, KikasaAffinity need)
                => af == need || af == KikasaAffinity.Wild;
            static bool FindPair(Span<KikasaAffinity> set, KikasaAffinity n1, KikasaAffinity n2,
                out int i1, out int i2) {
                for (int i = 0; i < set.Length; i++) {
                    for (int j = 0; j < set.Length; j++) {
                        if (i != j && Match(set[i], n1) && Match(set[j], n2)) {
                            i1 = i;
                            i2 = j;
                            return true;
                        }
                    }
                }
                i1 = i2 = -1;
                return false;
            }

            bool active = FindPair(now, need1, need2, out int a1, out int a2);
            if (active) {
                Vector2 from = KikasaPanoramaTheme.SeatPos(Math.Min(a1, a2));
                Vector2 to = KikasaPanoramaTheme.SeatPos(Math.Max(a1, a2));
                //相邻席浅垂，跨席（0-2）深垂，两条边不打架
                float sag = Math.Abs(a1 - a2) > 1 ? 40f : 16f;
                KikasaPanoramaRenderer.DrawWaterVein(sb, from + new Vector2(0f, 10f),
                    to + new Vector2(0f, 10f), 1.6f,
                    KikasaHudTheme.Glow(rain) * (0.45f * a), time, sag);
                Vector2 mid = (from + to) * 0.5f + new Vector2(0f, sag + 14f);
                Vector2 size = font.MeasureString(name) * 0.85f;
                Utils.DrawBorderString(sb, name, mid - size * 0.5f,
                    KikasaHudTheme.Glow(rain) * (0.9f * a), 0.85f);
                return;
            }
            //将成之边：预演里成立而现况没有，虚线先亮给你看
            if (preview && FindPair(next, need1, need2, out int p1, out int p2)) {
                Vector2 from = KikasaPanoramaTheme.SeatPos(Math.Min(p1, p2));
                Vector2 to = KikasaPanoramaTheme.SeatPos(Math.Max(p1, p2));
                float sag = Math.Abs(p1 - p2) > 1 ? 40f : 16f;
                KikasaPanoramaRenderer.DrawWaterVein(sb, from + new Vector2(0f, 10f),
                    to + new Vector2(0f, 10f), 1.2f,
                    KikasaHudTheme.Accent(rain) * (0.5f * a), time, sag, dashed: true);
                Vector2 mid = (from + to) * 0.5f + new Vector2(0f, sag + 14f);
                Vector2 size = font.MeasureString(name) * 0.85f;
                Utils.DrawBorderString(sb, name, mid - size * 0.5f,
                    KikasaHudTheme.Accent(rain) * (0.8f * a), 0.85f);
            }
        }

        private void DrawSeats(SpriteBatch sb, DynamicSpriteFont font, float a, float rain,
            KikasaServantPlayer servant, float waterPixY, float time) {
            //区题
            string title = SeatsTitle.Value;
            Vector2 titleSize = font.MeasureString(title) * 1.0f;
            Vector2 mid = KikasaPanoramaTheme.SeatPos(1);
            Utils.DrawBorderString(sb, title,
                new Vector2(mid.X - titleSize.X * 0.5f, mid.Y - KikasaPanoramaTheme.SeatHitR - 74f),
                KikasaHudTheme.Text(rain) * (0.95f * a), 1.0f);

            bool carrying = carryKey != 0;
            for (int i = 0; i < KikasaServantPlayer.SlotCount; i++) {
                Vector2 pos = KikasaPanoramaTheme.SeatPos(i);
                if (seatShake[i] > 0) {
                    pos.X += MathF.Sin(seatShake[i] * 1.3f) * 3f;
                }
                int key = servant.SlotKeyAt(i);
                bool held = servant.SlotHeldAt(i);
                float hover = seatHover[i];

                //席座：泡沫椭圆座圈 + 拒绝红闪
                Color seatRing = Color.Lerp(KikasaHudTheme.Accent(rain),
                    KikasaHudTheme.Glow(rain), hover * 0.6f);
                if (seatDeny[i] > 0.02f) {
                    seatRing = Color.Lerp(seatRing, new Color(226, 74, 60), seatDeny[i]);
                }
                KikasaVaultRenderer.DrawRing(sb, pos + new Vector2(0f, KikasaPanoramaTheme.SeatFit * 0.42f),
                    KikasaPanoramaTheme.SeatFit * 0.44f, KikasaPanoramaTheme.SeatFit * 0.13f,
                    seatRing * ((0.5f + hover * 0.3f) * a));

                if (key == 0) {
                    //空席：虚环；持影时鬼火环呼吸相邀
                    Color dashCol = KikasaHudTheme.TextDim(rain) * ((0.35f + hover * 0.3f) * a);
                    KikasaPanoramaRenderer.DrawDashedSocket(sb, pos, 17f, dashCol, time);
                    if (carrying) {
                        KikasaVaultRenderer.BeginAdditive(sb);
                        float invite = KikasaPanoramaTheme.Breath(time, i * 2.3f, 2.6f);
                        KikasaVaultRenderer.DrawRing(sb, pos, 20f + invite * 5f, (20f + invite * 5f) * 0.4f,
                            KikasaHudTheme.Glow(rain) * ((0.22f + hover * 0.2f) * a));
                        KikasaVaultRenderer.RestoreUIBatch(sb);
                    }
                    continue;
                }

                bool present = servant.FindServantOf(key) != null;
                float submerge = MathHelper.Clamp((pos.Y - waterPixY) / 26f, 0f, 1f);
                float effigyA = (held ? 0.6f : 1f) * a;
                Vector2 drawPos = pos + new Vector2(0f,
                    held ? 4f : MathF.Sin(time * 1.25f + i * 2.1f) * 2.2f * submerge - hover * 4f);
                KikasaVaultRenderer.DrawEffigyByKey(sb, key, drawPos, KikasaPanoramaTheme.SeatFit,
                    effigyA, submerge, tamed: true, absent: present, rain,
                    MathHelper.Clamp(stir + hover * 0.4f, 0f, 1f), KikasaHudTheme.Accent(rain));

                //在外：席上慢旋涡；收起：席下一道沉线
                if (present) {
                    KikasaVaultRenderer.BeginAdditive(sb);
                    for (int ring = 0; ring < 2; ring++) {
                        float rp = (time * 0.35f + ring * 0.5f + i * 0.23f) % 1f;
                        float r = MathHelper.Lerp(16f, 4f, rp);
                        KikasaVaultRenderer.DrawRing(sb, pos, r, r * 0.4f,
                            KikasaHudTheme.Glow(rain) * (0.22f * (1f - rp) * a));
                    }
                    KikasaVaultRenderer.RestoreUIBatch(sb);
                }
                else if (held) {
                    KikasaVaultRenderer.DrawLine(sb,
                        drawPos + new Vector2(-10f, KikasaPanoramaTheme.SeatFit * 0.52f),
                        drawPos + new Vector2(10f, KikasaPanoramaTheme.SeatFit * 0.52f),
                        1.6f, KikasaHudTheme.TextDim(rain) * (0.6f * a));
                }

                //亲和烬点
                KikasaAffinity affinity = servant.SlotAffinity(i);
                if (affinity != KikasaAffinity.None) {
                    float breath = KikasaPanoramaTheme.Breath(time, i * 3.1f, 2.2f);
                    KikasaVaultRenderer.DrawGlowDot(sb,
                        pos + new Vector2(KikasaPanoramaTheme.SeatFit * 0.36f,
                            KikasaPanoramaTheme.SeatFit * 0.30f), 5f,
                        KikasaEffigyBoard.AffinityColor(affinity) * ((0.5f + breath * 0.3f) * a));
                }

                //定妆：落影当帧一圈白闪
                if (seatStamp[i] > 0.02f) {
                    KikasaVaultRenderer.BeginAdditive(sb);
                    float t = 1f - seatStamp[i];
                    KikasaVaultRenderer.DrawRing(sb, pos, 12f + t * 26f, (12f + t * 26f) * 0.45f,
                        KikasaHudTheme.Glow(rain) * (0.6f * seatStamp[i] * a));
                    KikasaVaultRenderer.RestoreUIBatch(sb);
                }
            }

            //亲和计数与伞奴读数：编成区脚注
            float footX = KikasaPanoramaTheme.SeatPos(0).X - KikasaPanoramaTheme.SeatHitR;
            float footY = KikasaPanoramaTheme.SeatPos(1).Y + KikasaPanoramaTheme.SeatHitR + 16f;
            void AffinityCount(KikasaAffinity affinity, string label) {
                int count = KikasaEffigyBoard.CountAffinity(player, affinity);
                if (count <= 0) {
                    return;
                }
                string text = $"{label}\u00d7{count}";
                Utils.DrawBorderString(sb, text, new Vector2(footX, footY),
                    KikasaEffigyBoard.AffinityColor(affinity) * (0.9f * a), 0.85f);
                footX += font.MeasureString(text).X * 0.85f + 14f;
            }
            AffinityCount(KikasaAffinity.Flame, KikasaUIText.AffinityFlame.Value);
            AffinityCount(KikasaAffinity.Nightmare, KikasaUIText.AffinityNightmare.Value);
            AffinityCount(KikasaAffinity.Rain, KikasaUIText.AffinityRain.Value);
            if (Domain.IsRainForm) {
                string thrall = string.Format(ThrallLineFormat.Value,
                    KikasaEffigyBoard.ThrallCap(player),
                    (KikasaEffigyBoard.ThrallConvertGap(player) / 60f).ToString("0.0"));
                Utils.DrawBorderString(sb, thrall, new Vector2(footX, footY),
                    KikasaHudTheme.TextDim(rain) * (0.9f * a), 0.85f);
            }
        }

        private void DrawRoster(SpriteBatch sb, DynamicSpriteFont font, float a, float rain,
            KikasaServantPlayer servant, float waterPixY, float time) {
            //册计数：图鉴收录进度与影位占用并排，两种"满"分开说（反馈三·#36）
            string tally = string.Format(RosterTallyFormat.Value,
                servant.CollectedServantCount, KikasaServantPlayer.ServantCodexTotal);
            float tallyX = KikasaPanoramaTheme.RosterX(0, Math.Max(rosterKeys.Count, 1))
                - KikasaPanoramaTheme.RosterHitR;
            float tallyY = KikasaPanoramaTheme.RosterY - KikasaPanoramaTheme.RosterHitR - 26f;
            Utils.DrawBorderString(sb, tally, new Vector2(tallyX, tallyY),
                KikasaHudTheme.TextDim(rain) * (0.95f * a), 0.9f);
            string seatTally = string.Format(RosterSeatTallyFormat.Value,
                servant.FilledSlotCount, KikasaServantPlayer.SlotCount);
            float tallyW = font.MeasureString(tally).X * 0.9f;
            Utils.DrawBorderString(sb, seatTally, new Vector2(tallyX + tallyW + 18f, tallyY),
                KikasaHudTheme.TextDim(rain) * (0.85f * a), 0.9f);

            for (int i = 0; i < rosterKeys.Count; i++) {
                Vector2 pos = RosterPos(i);
                if (uncollectedShake == i && uncollectedShakeTimer > 0) {
                    pos.X += MathF.Sin(uncollectedShakeTimer * 1.3f) * 3f;
                }
                float hover = i < rosterHover.Count ? rosterHover[i] : 0f;
                int key = rosterKeys[i];

                if (!rosterCollected[i]) {
                    //未沉之影：只有一圈虚座，形不给看
                    KikasaPanoramaRenderer.DrawDashedSocket(sb, pos, 12f,
                        KikasaHudTheme.TextDim(rain) * ((0.28f + hover * 0.3f) * a), time, 0.2f);
                    continue;
                }

                bool seated = servant.SlotIndexOf(key) >= 0;
                bool isCarried = carryKey == key;
                float submerge = MathHelper.Clamp((pos.Y - waterPixY) / 26f, 0f, 1f);
                //在手/已驻席的册位压暗成拓空痕
                float effigyA = (isCarried ? 0.28f : seated ? 0.4f : 0.95f) * a;
                Vector2 drawPos = pos + new Vector2(0f,
                    MathF.Sin(time * 1.1f + i * 1.7f) * 1.6f * submerge - hover * 3f);
                KikasaVaultRenderer.DrawEffigyByKey(sb, key, drawPos, KikasaPanoramaTheme.RosterFit,
                    effigyA, submerge, tamed: true, absent: false, rain,
                    MathHelper.Clamp(stir + hover * 0.4f, 0f, 1f), KikasaHudTheme.Accent(rain));

                //亲和小点缀在影脚
                KikasaAffinity affinity = KikasaServantPlayer.AffinityOfKey(key);
                Color dotCol = key < 0
                    ? KikasaHudTheme.TextDim(rain)
                    : KikasaEffigyBoard.AffinityColor(affinity);
                if (affinity != KikasaAffinity.None || key < 0) {
                    KikasaVaultRenderer.DrawGlowDot(sb,
                        pos + new Vector2(0f, KikasaPanoramaTheme.RosterFit * 0.5f), 3.4f,
                        dotCol * ((seated ? 0.25f : 0.45f + hover * 0.3f) * a));
                }
                //已驻席：影脚一记下沉线
                if (seated) {
                    KikasaVaultRenderer.DrawLine(sb,
                        pos + new Vector2(-5f, KikasaPanoramaTheme.RosterFit * 0.62f),
                        pos + new Vector2(5f, KikasaPanoramaTheme.RosterFit * 0.62f + 2f),
                        1.4f, KikasaHudTheme.Accent(rain) * (0.5f * a));
                }
            }
        }

        //====== 湖藏 ======

        private void DrawVault(SpriteBatch sb, DynamicSpriteFont font, float a, float rain,
            KikasaVaultPlayer vault, float waterPixY, float time) {
            List<Item> stored = vault.Stored;
            //区题与计数
            Vector2 firstCell = KikasaPanoramaTheme.VaultCell(0);
            float headY = KikasaPanoramaTheme.VaultTop - 22f;
            Utils.DrawBorderString(sb, VaultTitle.Value,
                new Vector2(firstCell.X - KikasaPanoramaTheme.VaultFit * 0.5f, headY),
                KikasaHudTheme.Text(rain) * (0.95f * a), 1.0f);
            string count = string.Format(VaultCountFormat.Value, stored.Count, KikasaVaultPlayer.Capacity);
            Vector2 countSize = font.MeasureString(count) * 0.9f;
            Vector2 lastCell = KikasaPanoramaTheme.VaultCell(KikasaPanoramaTheme.VaultCols - 1);
            Utils.DrawBorderString(sb, count,
                new Vector2(lastCell.X + KikasaPanoramaTheme.VaultFit * 0.5f - countSize.X, headY + 4f),
                KikasaHudTheme.TextDim(rain) * a, 0.9f);
            //只读声明
            if (!vault.LakeReady) {
                float breathe = 0.7f + 0.3f * KikasaPanoramaTheme.Breath(time, 2.3f, 1.6f);
                Vector2 vSize = font.MeasureString(VaultViewOnlyLine.Value) * 0.9f;
                Utils.DrawBorderString(sb, VaultViewOnlyLine.Value,
                    new Vector2(KikasaPanoramaTheme.UIScreenW * 0.5f - vSize.X * 0.5f, headY + 4f),
                    KikasaHudTheme.TextDim(rain) * (breathe * a), 0.9f);
            }

            if (stored.Count == 0) {
                float breathe = 0.6f + 0.3f * KikasaPanoramaTheme.Breath(time, 1.7f, 1.6f);
                Vector2 eSize = font.MeasureString(VaultEmptyHint.Value) * 0.9f;
                Utils.DrawBorderString(sb, VaultEmptyHint.Value,
                    new Vector2(KikasaPanoramaTheme.UIScreenW * 0.5f - eSize.X * 0.5f,
                        KikasaPanoramaTheme.VaultTop + 64f),
                    KikasaHudTheme.TextDim(rain) * (breathe * a), 0.9f);
                return;
            }

            //沉物：血水态漂浮，悬停凝出真身
            bool shaderOk = KikasaVaultRenderer.BeginItemBatch(sb, out Effect formEffect);
            List<(Vector2 pos, int stack, float alpha)> stackLabels = null;
            for (int i = 0; i < stored.Count; i++) {
                Item item = stored[i];
                if (item == null || item.IsAir) {
                    continue;
                }
                Vector2 c = KikasaPanoramaTheme.VaultCell(i);
                float hover = i < vaultHover.Count ? vaultHover[i] : 0f;
                float submerge = MathHelper.Clamp((c.Y - waterPixY) / 20f, 0f, 1f);
                float bob = MathF.Sin(time * 1.35f + i * 1.71f) * 2.0f * submerge * (1f - hover * 0.6f);
                Vector2 pos = c + new Vector2(0f, bob - hover * 5f);
                //越深越沉入血水，悬停凝向真身；干床上的沉物同样带血形但更沉
                float depth01 = MathHelper.Clamp((c.Y - waterPixY)
                    / MathF.Max(KikasaPanoramaTheme.UIScreenH - waterPixY, 1f), 0f, 1f);
                float form = MathHelper.Clamp(
                    MathHelper.Lerp(0.74f, 0.88f, depth01) - hover * 0.6f, 0.05f, 1f);
                KikasaVaultRenderer.DrawFormItem(sb, formEffect, shaderOk,
                    item.type, pos, form, i * 2.39f + 0.7f, a);
                if (item.stack > 1) {
                    stackLabels ??= [];
                    stackLabels.Add((pos + new Vector2(9f, 7f), item.stack, a));
                }
            }
            KikasaVaultRenderer.EndItemBatch(sb);

            //叠数
            if (stackLabels != null) {
                foreach ((Vector2 pos, int stack, float la) in stackLabels) {
                    Utils.DrawBorderString(sb, stack.ToString(), pos,
                        KikasaHudTheme.Text(rain) * (0.9f * la), 0.85f);
                }
            }

            //悬停格的水光衬底
            if (hoverKind == HoverKind.Vault && hoverIndex >= 0 && hoverIndex < stored.Count) {
                float hover = hoverIndex < vaultHover.Count ? vaultHover[hoverIndex] : 0f;
                Vector2 hc = KikasaPanoramaTheme.VaultCell(hoverIndex);
                KikasaVaultRenderer.BeginAdditive(sb);
                KikasaVaultRenderer.DrawGlowDot(sb, hc,
                    KikasaPanoramaTheme.VaultFit * 0.7f,
                    KikasaHudTheme.Accent(rain) * (0.18f * hover * a));
                KikasaVaultRenderer.RestoreUIBatch(sb);
            }
        }

        //====== 飞影 / 拾影 ======

        private void DrawFlyers(SpriteBatch sb, float a, float rain, float time) {
            foreach (Flyer f in flyers) {
                float t = f.Timer / (float)FlyerLife;
                float ease = 1f - MathF.Pow(1f - t, 2.2f);
                //抛物：中段抬一口气再落回册位
                Vector2 pos = Vector2.Lerp(f.From, f.To, ease);
                pos.Y -= MathF.Sin(t * MathHelper.Pi) * 34f;
                KikasaVaultRenderer.DrawEffigyByKey(sb, f.Key, pos,
                    KikasaPanoramaTheme.RosterFit, (1f - t * 0.4f) * a,
                    1f, tamed: true, absent: false, rain, 0.4f, KikasaHudTheme.Accent(rain));
            }
        }

        private void DrawCarry(SpriteBatch sb, float a, float rain, float time) {
            if (carryKey == 0) {
                return;
            }
            //衬光把影从底色里托出来
            KikasaVaultRenderer.BeginAdditive(sb);
            KikasaVaultRenderer.DrawGlowDot(sb, carryPos, 30f,
                KikasaHudTheme.Glow(rain) * (0.2f * a));
            KikasaVaultRenderer.RestoreUIBatch(sb);
            KikasaVaultRenderer.DrawEffigyByKey(sb, carryKey, carryPos,
                KikasaPanoramaTheme.SeatFit * 0.9f, a, 1f, tamed: true, absent: false,
                rain, MathHelper.Clamp(0.4f + carryVel.Length() * 0.05f, 0f, 1f),
                KikasaHudTheme.Accent(rain));
        }

        //====== 涟漪 / 批注 / 页脚 / 名牌 ======

        private void DrawRipples(SpriteBatch sb, float a, float rain) {
            //水涟漪走加色
            KikasaVaultRenderer.BeginAdditive(sb);
            foreach (Ripple r in ripples) {
                if (r.Ink) {
                    continue;
                }
                float t = r.Timer / (float)RippleLife;
                KikasaVaultRenderer.DrawRing(sb, r.Pos, 4f + t * 22f, (4f + t * 22f) * 0.38f,
                    KikasaHudTheme.Glow(rain) * (0.35f * (1f - t) * a));
            }
            KikasaVaultRenderer.RestoreUIBatch(sb);
            //墨涟漪走普通批（暗色真阿尔法才压得暗）
            foreach (Ripple r in ripples) {
                if (!r.Ink) {
                    continue;
                }
                float t = r.Timer / (float)RippleLife;
                float radius = 3f + t * 20f;
                KikasaVaultRenderer.DrawRing(sb, r.Pos, radius, radius * 0.5f,
                    KikasaHudTheme.Void(rain) * (0.55f * (1f - t) * a));
            }
        }

        private void DrawNoteAndFooter(SpriteBatch sb, DynamicSpriteFont font, float a, float rain) {
            //批注：屏内回执 + 字下细朱线；超宽换行，块底锚定 NoteY 向上生长不压页脚
            if (noteTimer > 0 && !string.IsNullOrEmpty(noteText)) {
                float noteA = MathHelper.Clamp(noteTimer / 24f, 0f, 1f) * a;
                const float noteScale = 0.9f;
                List<string> noteLines = VaultUtils.WrapText(noteText, font,
                    MathF.Min(KikasaPanoramaTheme.UIScreenW * 0.62f,
                        KikasaPanoramaTheme.UIScreenW - 32f), noteScale);
                float noteLineH = font.MeasureString("A").Y * noteScale + 2f;
                float ny = KikasaPanoramaTheme.NoteY - (noteLines.Count - 1) * noteLineH;
                float widest = 0f;
                foreach (string line in noteLines) {
                    widest = MathF.Max(widest, font.MeasureString(line).X * noteScale);
                }
                foreach (string line in noteLines) {
                    float lw = font.MeasureString(line).X * noteScale;
                    Utils.DrawBorderString(sb, line,
                        new Vector2(KikasaPanoramaTheme.UIScreenW * 0.5f - lw * 0.5f, ny),
                        noteColor * noteA, noteScale);
                    ny += noteLineH;
                }
                float ux = KikasaPanoramaTheme.UIScreenW * 0.5f - widest * 0.5f;
                KikasaVaultRenderer.DrawLine(sb, new Vector2(ux, ny),
                    new Vector2(ux + widest, ny), 1.2f,
                    KikasaHudTheme.Accent(rain) * (0.5f * noteA));
            }

            //页脚：合画/沉入/转盘三个键位一次说全；超宽换行，块底锚定 FooterY
            string footer = string.Format(FooterHintFormat.Value,
                CWRKeySystem.Legend_UIControl.ToTooltipString(CWRKeySystem.Notbound.Value),
                CWRKeySystem.Kikasa_Sink.ToTooltipString(CWRKeySystem.Notbound.Value),
                CWRKeySystem.GetKeybindText(CWRKeySystem.RadialWheel_Key, CWRKeySystem.Notbound.Value));
            const float footScale = 0.85f;
            List<string> footLines = VaultUtils.WrapText(footer, font,
                KikasaPanoramaTheme.UIScreenW - 140f, footScale);
            float footLineH = font.MeasureString("A").Y * footScale + 2f;
            float fy = KikasaPanoramaTheme.FooterY - (footLines.Count - 1) * footLineH;
            foreach (string line in footLines) {
                float lw = font.MeasureString(line).X * footScale;
                Utils.DrawBorderString(sb, line,
                    new Vector2(KikasaPanoramaTheme.UIScreenW * 0.5f - lw * 0.5f, fy),
                    KikasaHudTheme.TextDim(rain) * (0.9f * a), footScale);
                fy += footLineH;
            }

            //页脚右端「?」：重看教程入口，悬停亮环并浮出一行说明
            Rectangle help = HelpRect;
            Vector2 hc = help.Center.ToVector2();
            KikasaVaultRenderer.DrawRing(sb, hc, 13f + helpHover * 2f, 13f + helpHover * 2f,
                KikasaHudTheme.Accent(rain) * ((0.35f + helpHover * 0.5f) * a));
            Vector2 qSize = font.MeasureString("?") * 0.9f;
            Utils.DrawBorderString(sb, "?", hc - qSize * 0.5f,
                (helpHover > 0.5f ? KikasaHudTheme.Text(rain) : KikasaHudTheme.TextDim(rain)) * a,
                0.9f);
            if (helpHover > 0.05f) {
                string tip = KikasaHudLead.HelpHover.Value;
                Vector2 tSize = font.MeasureString(tip) * 0.85f;
                Utils.DrawBorderString(sb, tip,
                    new Vector2(help.X - tSize.X - 10f, hc.Y - tSize.Y * 0.5f),
                    KikasaHudTheme.Text(rain) * (helpHover * a), 0.85f);
            }
        }

        /// <summary>
        /// 悬停说明：全部走 <see cref="KikasaTipPanel"/> 暗水玻璃面板
        /// （自动换行 + 四边钳制），题行 1.0、正文 0.9、次要提示 0.85。
        /// 两鬼的数值与键位路径也收在这里，画内只留名牌
        /// </summary>
        private void DrawHoverTipPanel(SpriteBatch sb, float a, float rain,
            KikasaServantPlayer servant, KikasaVaultPlayer vault) {
            if (hoverKind == HoverKind.None || hoverKind == HoverKind.Help) {
                return;
            }
            Color dim = KikasaHudTheme.TextDim(rain);
            Color glow = KikasaHudTheme.Glow(rain);
            Color accent = KikasaHudTheme.Accent(rain);
            string title = null;
            List<KikasaTipLine> lines = [];

            switch (hoverKind) {
                case HoverKind.Hound: {
                    KikasaDomainPlayer domain = Domain;
                    title = HoundTitle.Value;
                    string mutateKey = CWRKeySystem.Kikasa_DomainMutate
                        .ToTooltipString(CWRKeySystem.Notbound.Value);
                    if (domain.Phase == KikasaDomainPhase.Dreaming) {
                        lines.Add(new KikasaTipLine(InDreamLine.Value, glow));
                        lines.Add(new KikasaTipLine(
                            string.Format(DreamReturnFormat.Value, mutateKey), dim));
                    }
                    else if (domain.DreamPullReady) {
                        //点击与长按两条路都列出，点击在前
                        lines.Add(new KikasaTipLine(DreamEnterClick.Value, glow));
                        lines.Add(new KikasaTipLine(
                            string.Format(DreamEnterKeyFormat.Value, mutateKey), dim, 0.85f));
                    }
                    else {
                        lines.Add(new KikasaTipLine(HoundBlockReason(domain), accent));
                        lines.Add(new KikasaTipLine(
                            string.Format(DreamEnterKeyFormat.Value, mutateKey), dim, 0.85f));
                    }
                    lines.Add(new KikasaTipLine(string.Format(HoundBonusFormat.Value,
                        KikasaEffigyBoard.HoundCap(player),
                        (int)MathF.Round(KikasaEffigyBoard.HoundDamageScale(player) * 100f)),
                        KikasaEffigyBoard.NightmareCount(player) > 0
                            ? KikasaEffigyBoard.AffinityColor(KikasaAffinity.Nightmare) : dim));
                    if (KikasaEffigyBoard.HasDreamFireEdge(player)) {
                        lines.Add(new KikasaTipLine(EdgeDreamFire.Value,
                            KikasaWisps.KikasaWisp.Tint(KikasaWisps.KikasaWisp.GoldBody), 0.85f));
                    }
                    break;
                }
                case HoverKind.Wisp: {
                    KikasaDomainPlayer domain = Domain;
                    title = WispTitle.Value;
                    lines.Add(new KikasaTipLine(KikasaUIText.WispStateLine(domain),
                        domain.WispFireActive
                            ? KikasaWisps.KikasaWisp.Tint(KikasaWisps.KikasaWisp.GoldBody) : dim));
                    string block = KikasaUIText.WispBlockReason(domain);
                    if (domain.WispFireActive) {
                        lines.Add(new KikasaTipLine(WispSnuffClick.Value, glow));
                    }
                    else if (block == null) {
                        lines.Add(new KikasaTipLine(WispIgniteClick.Value, glow));
                    }
                    else {
                        lines.Add(new KikasaTipLine(block, accent));
                    }
                    lines.Add(new KikasaTipLine(string.Format(WispBonusFormat.Value,
                        (KikasaEffigyBoard.WispBurnDuration(player) / 60f).ToString("0.0"),
                        (int)KikasaEffigyBoard.WispFlameReach(player)),
                        KikasaEffigyBoard.FlameCount(player) > 0
                            ? KikasaEffigyBoard.AffinityColor(KikasaAffinity.Flame) : dim));
                    if (KikasaEffigyBoard.HasBoilRainEdge(player)) {
                        lines.Add(new KikasaTipLine(EdgeBoilRain.Value, glow, 0.85f));
                    }
                    break;
                }
                case HoverKind.Seat: {
                    int key = servant.SlotKeyAt(hoverIndex);
                    if (carryKey != 0) {
                        title = KikasaServantPlayer.KeyDisplayName(carryKey);
                        lines.Add(new KikasaTipLine(SeatPlaceHint.Value, glow));
                    }
                    else if (key == 0) {
                        title = KikasaHud.TipSeatEmpty.Value;
                        lines.Add(new KikasaTipLine(SeatEmptyLine.Value, dim));
                    }
                    else {
                        title = KikasaServantPlayer.KeyDisplayName(key);
                        string affinityName = KikasaUIText.AffinityName(
                            servant.SlotAffinity(hoverIndex), key);
                        bool held = servant.SlotHeldAt(hoverIndex);
                        bool present = servant.FindServantOf(key) != null;
                        string state = held ? KikasaUIText.StateHeld.Value
                            : present ? KikasaUIText.StateOut.Value : KikasaUIText.StateAwait.Value;
                        lines.Add(new KikasaTipLine(string.IsNullOrEmpty(affinityName)
                            ? state : $"{affinityName} \u00b7 {state}", dim));
                        //只有收起态需要多解释一句（怎么召回）；在场/候湖状态词已说尽
                        if (held) {
                            lines.Add(new KikasaTipLine(string.Format(SeatHeldLineFormat.Value,
                                CWRKeySystem.GetKeybindText(CWRKeySystem.RadialWheel_Key,
                                    CWRKeySystem.Notbound.Value)), dim));
                        }
                        if (key < 0) {
                            int stock = KikasaServantPlayer.CountStoredArms(vault, -key);
                            lines.Add(new KikasaTipLine(stock > 0
                                ? string.Format(ArmsStockFormat.Value, stock)
                                : NoteArmsNoStock.Value, stock > 0 ? dim : accent));
                        }
                        lines.Add(new KikasaTipLine(SeatPickHint.Value, dim, 0.85f));
                    }
                    break;
                }
                case HoverKind.Roster: {
                    if (hoverIndex < 0 || hoverIndex >= rosterKeys.Count) {
                        return;
                    }
                    if (!rosterCollected[hoverIndex]) {
                        title = RosterUnknown.Value;
                        lines.Add(new KikasaTipLine(string.Format(RosterUnknownHintFormat.Value,
                            CWRKeySystem.Kikasa_Sink.ToTooltipString(CWRKeySystem.Notbound.Value)),
                            dim));
                    }
                    else {
                        int key = rosterKeys[hoverIndex];
                        title = KikasaServantPlayer.KeyDisplayName(key);
                        string affinityName = KikasaUIText.AffinityName(
                            KikasaServantPlayer.AffinityOfKey(key), key);
                        bool seated = servant.SlotIndexOf(key) >= 0;
                        string sub = seated
                            ? (string.IsNullOrEmpty(affinityName) ? RosterSeatedTag.Value
                                : $"{affinityName} \u00b7 {RosterSeatedTag.Value}")
                            : affinityName;
                        if (!string.IsNullOrEmpty(sub)) {
                            lines.Add(new KikasaTipLine(sub, dim));
                        }
                        if (key < 0) {
                            int stock = KikasaServantPlayer.CountStoredArms(vault, -key);
                            lines.Add(new KikasaTipLine(stock > 0
                                ? string.Format(ArmsStockFormat.Value, stock)
                                : NoteArmsNoStock.Value, stock > 0 ? dim : accent));
                        }
                        lines.Add(new KikasaTipLine(RosterPickHint.Value, dim, 0.85f));
                    }
                    break;
                }
                case HoverKind.Talis: {
                    if (!HoldingKikasa) {
                        title = TalisTitle.Value;
                        lines.Add(new KikasaTipLine(TalisNeedUmbrella.Value, accent));
                    }
                    else if (talisDragActive) {
                        //拖符悬在落点上：把松手会发生什么说在前头（原结不提示）
                        if (hoverIndex == talisDragSlot) {
                            return;
                        }
                        string dragKey = KikasaTalismanRegistry.DisplayStore?.Get(talisDragSlot);
                        title = dragKey != null && KikasaTalismanRegistry.TryGet(dragKey,
                            out KikasaTalismanDefinition dragDef)
                            ? dragDef.DisplayName.Value : TalisTitle.Value;
                        string target = KikasaTalismanRegistry.DisplayStore?.Get(hoverIndex);
                        lines.Add(new KikasaTipLine(target == null
                            ? TalisDragMoveHint.Value : TalisDragSwapHint.Value, glow));
                    }
                    else {
                        string key = KikasaTalismanRegistry.DisplayStore?.Get(hoverIndex);
                        if (key == null) {
                            title = TalisTitle.Value;
                            lines.Add(new KikasaTipLine(TalisEmptyLine.Value, dim));
                        }
                        else if (KikasaTalismanRegistry.TryGet(key, out KikasaTalismanDefinition def)) {
                            title = def.DisplayName.Value;
                            lines.Add(new KikasaTipLine(def.Summary.Value, dim));
                            lines.Add(new KikasaTipLine(TalisSwapHint.Value, dim, 0.85f));
                        }
                    }
                    break;
                }
                case HoverKind.TalisFan: {
                    if (hoverIndex < 0 || hoverIndex >= fanKeys.Count) {
                        return;
                    }
                    string key = fanKeys[hoverIndex];
                    if (key == null) {
                        title = TalisTakeDownLabel.Value;
                        break;
                    }
                    if (!KikasaTalismanRegistry.TryGet(key, out KikasaTalismanDefinition def)) {
                        return;
                    }
                    title = def.DisplayName.Value;
                    string current = KikasaTalismanRegistry.DisplayStore?.Get(fanSlot);
                    lines.Add(new KikasaTipLine(def.Power.Value, new Color(126, 168, 196)));
                    lines.Add(new KikasaTipLine(def.Burden.Value, new Color(174, 110, 110)));
                    if (key == current) {
                        lines.Add(new KikasaTipLine(TalisCurrentTag.Value, glow, 0.85f));
                    }
                    else if (KikasaTalismanRegistry.DisplayStore?.Contains(key) == true) {
                        lines.Add(new KikasaTipLine(TalisAlreadyHung.Value, accent, 0.85f));
                    }
                    else {
                        lines.Add(new KikasaTipLine(TalisHangHint.Value, dim, 0.85f));
                    }
                    break;
                }
                case HoverKind.Vault: {
                    List<Item> stored = vault.Stored;
                    if (hoverIndex < 0 || hoverIndex >= stored.Count) {
                        return;
                    }
                    Item item = stored[hoverIndex];
                    title = item.AffixName();
                    if (item.stack > 1) {
                        title += $" \u00d7{item.stack}";
                    }
                    if (KikasaArmsIndex.TryGet(item.type, out _)) {
                        lines.Add(new KikasaTipLine(string.Format(ArmsStockFormat.Value,
                            KikasaServantPlayer.CountStoredArms(vault, item.type)), dim));
                    }
                    lines.Add(new KikasaTipLine(
                        vault.LakeReady ? VaultExtractHint.Value : VaultViewOnlyLine.Value,
                        vault.LakeReady ? glow : dim));
                    break;
                }
            }

            if (string.IsNullOrEmpty(title)) {
                return;
            }
            KikasaTipPanel.Draw(sb, KikasaPanoramaTheme.UIMouse, title, rain, a, [.. lines]);
        }
    }
}
