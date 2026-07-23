using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.UIHandles;
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

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 改铭台全屏:解剑横陈+三铭位+鏨盘扇+烙印木牌+右缘刀铭大字;
    /// 与点鬼簿互斥同级;鏨仪式为内嵌演出态(<see cref="OniMeiRite"/>)
    /// </summary>
    internal sealed class OniMeiUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.OnikiriText";
        public static OniMeiUI Instance => UIHandleLoader.GetUIHandleOfType<OniMeiUI>();

        private const string FreezeReason = "OniMei";

        #region 本地化
        public static LocalizedText TitleText { get; private set; }
        public static LocalizedText CloseTagText { get; private set; }
        public static LocalizedText CloseHintFormat { get; private set; }
        public static LocalizedText StatusFormat { get; private set; }
        public static LocalizedText SlotNakago { get; private set; }
        public static LocalizedText SlotHi { get; private set; }
        public static LocalizedText SlotHorimono { get; private set; }
        public static LocalizedText EmptyName { get; private set; }
        public static LocalizedText EmptyHintNakago { get; private set; }
        public static LocalizedText EmptyHintHi { get; private set; }
        public static LocalizedText EmptyHintHorimono { get; private set; }
        public static LocalizedText EraseName { get; private set; }
        public static LocalizedText EraseHint { get; private set; }
        public static LocalizedText OriginLabel { get; private set; }
        public static LocalizedText PowerLabel { get; private set; }
        public static LocalizedText BurdenLabel { get; private set; }
        public static LocalizedText CurrentMark { get; private set; }
        public static LocalizedText GoldMark { get; private set; }
        public static LocalizedText RegisterTabText { get; private set; }
        public static LocalizedText RegisterTabHint { get; private set; }

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "改 铭 台");
            CloseTagText = this.GetLocalization(nameof(CloseTagText), () => "纳 刀");
            CloseHintFormat = this.GetLocalization(nameof(CloseHintFormat), () => "ESC · {0} · 点击台外 收台");
            StatusFormat = this.GetLocalization(nameof(StatusFormat), () => "在铭 {0} 处 · 今名「{1}」");
            SlotNakago = this.GetLocalization(nameof(SlotNakago), () => "茎铭");
            SlotHi = this.GetLocalization(nameof(SlotHi), () => "樋位");
            SlotHorimono = this.GetLocalization(nameof(SlotHorimono), () => "雕位");
            EmptyName = this.GetLocalization(nameof(EmptyName), () => "（铭位空悬）");
            EmptyHintNakago = this.GetLocalization(nameof(EmptyHintNakago), () => "茎上无名。素刃也好，只是夜里没人应它");
            EmptyHintHi = this.GetLocalization(nameof(EmptyHintHi), () => "樋未开。开樋一分，刀轻一分");
            EmptyHintHorimono = this.GetLocalization(nameof(EmptyHintHorimono), () => "雕位素净。请神入刀，先得敬它");
            EraseName = this.GetLocalization(nameof(EraseName), () => "除 铭");
            EraseHint = this.GetLocalization(nameof(EraseHint), () => "锉去此铭——铭可再凿，刀不忘痕");
            OriginLabel = this.GetLocalization(nameof(OriginLabel), () => "出处");
            PowerLabel = this.GetLocalization(nameof(PowerLabel), () => "赋效");
            BurdenLabel = this.GetLocalization(nameof(BurdenLabel), () => "代价");
            CurrentMark = this.GetLocalization(nameof(CurrentMark), () => "现铭");
            GoldMark = this.GetLocalization(nameof(GoldMark), () => "金象嵌");
            RegisterTabText = this.GetLocalization(nameof(RegisterTabText), () => "点鬼簿");
            RegisterTabHint = this.GetLocalization(nameof(RegisterTabHint), () => "点击 移步");
        }
        #endregion

        public override bool CloseOnEscape => true;
        public override float RenderPriority => 2f;
        public override SoundStyle? OpenSound => SoundID.Unlock with { Pitch = -0.2f, Volume = 0.5f };
        public override SoundStyle? CloseSound => SoundID.MenuClose with { Pitch = -0.35f, Volume = 0.5f };

        //====布局(每帧 UI 空间重算)====
        private Vector2 bladeCenter;
        private float bladeW;
        private Vector2 bladeDir;
        private Vector2 bladePerp;
        private Rectangle clothRect;
        private readonly Vector2[] slotPos = new Vector2[3];
        private Vector2 fanPivot;
        private Rectangle tagRect;
        private Vector2 nameColTop;
        private Vector2 closeTagAnchor;
        private Rectangle closeTagRect;

        //====交互状态====
        private int hoverSlot = -1;
        private readonly float[] slotHover = new float[3];
        private readonly float[] slotSelect = new float[3];
        private int selectedSlot = -1;      //-1 未选;0~2 = OniMeiSlotKind
        /// <summary>扇面向上张(下方会压到烙印木牌时翻到刀上方)</summary>
        private bool fanUp;
        private float fanEase;
        private readonly List<OniMeiDefinition> ribs = [];
        private bool ribsHasErase;
        private int hoverRib = -1;
        private readonly float[] ribEase = new float[12];
        private float closeTagHover;
        private bool closeTagWasHovered;
        private readonly OniRope closeTagRope = new(5, 22f);
        //吊挂卷轴:回点鬼簿的门(对面器物的微缩,挂在布左上的梁下)
        private readonly OniHangingSwitch registerSwitch = new(SoundID.MenuTick with { Pitch = -0.2f, Volume = 0.45f });
        private Vector2 registerSwitchAnchor;

        //====动画状态====
        internal float ShaderTime;
        private readonly OniUIParticlePool particles = new(200);
        private bool mekugiPopped;
        private float mekugiAnim;
        private float postRiteNameEase = 1f;
        //开屏涟漪:内容可见后逐位点名三处铭位(帧计数,溢出即停)
        private int slotRevealTimer;

        //====木牌打字机====
        private string tagStamp = "";
        private float typeTimer;
        private int lastTypedChars = -1;
        private float burnAge = 60f;

        //====低频异象====
        private int songCooldown = 900;
        private float songRun = -1f;

        /// <summary>鏨仪式演出态</summary>
        internal readonly OniMeiRite Rite = new();

        public override void OnEnterWorld() {
            if (IsOpen) {
                Close();
            }
            SnapOpenProgress();
        }

        protected override void OnOpen() {
            Main.playerInventory = false;
            //姊妹屏互斥:一台开另一卷收
            if (OniRegisterUI.Instance?.IsOpen ?? false) {
                OniRegisterUI.Instance.Close();
            }
            selectedSlot = -1;
            hoverRib = -1;
            fanEase = 0f;
            mekugiPopped = false;
            mekugiAnim = 0f;
            slotRevealTimer = 0;
            tagStamp = "";
            lastTypedChars = -1;
            postRiteNameEase = 1f;
            registerSwitch.Reset();
            songCooldown = Main.rand.Next(700, 1400);
            songRun = -1f;
            particles.Clear();
            if (VaultUtils.isSinglePlayer) {
                WorldFreezeSystem.Activate(FreezeReason);
            }
        }

        protected override void OnClose() {
            //收台时未完的鏨仪式直接定格,重开不再续播半场
            if (Rite.Active) {
                Rite.Skip();
            }
            if (VaultUtils.isSinglePlayer) {
                WorldFreezeSystem.Deactivate(FreezeReason);
            }
        }

        //====数据视图====

        private static OniMeiSlotKind SlotOf(int index) => (OniMeiSlotKind)index;

        /// <summary>铭位上的定义(展示缓存),空 null</summary>
        private static OniMeiDefinition EngravedAt(int index)
            => OniMeiRegistry.GetEngraved(OniMeiRegistry.DisplayStore, SlotOf(index));

        public override void Update() {
            float a = OpenProgress;
            if (a < 0.01f) {
                return;
            }

            if (IsOpen) {
                player.mouseInterface = true;
                UIInputGuard.SuppressWeaponSwitch();
                if (!player.active || player.dead) {
                    Close();
                }
            }

            ShaderTime += 1f / 60f;
            particles.Update();
            LayoutCompute();

            //开屏编舞:目钉弹出一响,木销飞脱动画计时
            if (IsOpen && a >= 0.55f && !mekugiPopped) {
                mekugiPopped = true;
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.6f, Volume = 0.45f });
                Vector2 mekugi = MekugiPos();
                for (int i = 0; i < 3; i++) {
                    particles.SpawnFiling(mekugi);
                }
            }
            if (mekugiPopped && mekugiAnim < 1f) {
                mekugiAnim = Math.Min(mekugiAnim + 1f / 26f, 1f);
            }
            //开屏涟漪计时:内容可见后开始点名铭位
            if (IsOpen && a >= 0.8f && slotRevealTimer < 400) {
                slotRevealTimer++;
            }

            //收卷木牌摆
            closeTagRope.Update(closeTagAnchor, null, GlobalTimer, 0.24f, endWeight: 0.55f);
            Vector2 tagTop = closeTagRope.End;
            closeTagRect = new Rectangle((int)(tagTop.X - 16f), (int)tagTop.Y - 2, 32, 48);

            //吊挂卷轴:点击预演到帧即移步点鬼簿;簿上有鬼躁动时回声更急
            if (registerSwitch.Update(registerSwitchAnchor, MousePosition, IsOpen && a > 0.9f && !Rite.Active,
                GlobalTimer, OnikiriUITheme.HangScrollHit, keyLeftPressState, OniRegistry.InDanger)) {
                OniRegisterUI.Instance?.Open();
            }

            //鏨仪式推进:期间吞交互,点击可跳
            if (Rite.Active) {
                Vector2 riteAnchor = slotPos[(int)Rite.Slot];
                Rite.Update(riteAnchor, RiteGlyphSize(), OnikiriUITheme.MeiBladeCant, particles);
                postRiteNameEase = 0f;
                if (IsOpen && keyLeftPressState == KeyPressState.Pressed) {
                    Rite.Skip();
                }
                hoverSlot = -1;
                hoverRib = -1;
                EaseArrays();
                UpdateTagTypewriter();
                return;
            }
            postRiteNameEase = MathHelper.Clamp(postRiteNameEase + 0.05f, 0f, 1f);

            UpdateInteraction(a);
            UpdateAnomalies();
            UpdateTagTypewriter();
        }

        private void LayoutCompute() {
            float sw = OnikiriUITheme.UIScreenW;
            float sh = OnikiriUITheme.UIScreenH;

            bladeW = Math.Min(OnikiriUITheme.MeiBladeMaxW, sw * OnikiriUITheme.MeiBladeWidthRatio);
            bladeCenter = new Vector2(sw * OnikiriUITheme.MeiBladeCenterRatio.X, sh * OnikiriUITheme.MeiBladeCenterRatio.Y);
            bladeDir = OnikiriUITheme.MeiBladeCant.ToRotationVector2();
            bladePerp = (OnikiriUITheme.MeiBladeCant + MathHelper.PiOver2).ToRotationVector2();

            Vector2 tip = bladeCenter - bladeDir * (bladeW * 0.5f);
            slotPos[(int)OniMeiSlotKind.Nakago] = tip + bladeDir * (bladeW * OnikiriUITheme.MeiSlotUNakago);
            slotPos[(int)OniMeiSlotKind.Hi] = tip + bladeDir * (bladeW * OnikiriUITheme.MeiSlotUHi);
            slotPos[(int)OniMeiSlotKind.Horimono] = tip + bladeDir * (bladeW * OnikiriUITheme.MeiSlotUHorimono);

            float clothW = bladeW + 150f;
            clothRect = new Rectangle((int)(bladeCenter.X - clothW * 0.5f),
                (int)(bladeCenter.Y - OnikiriUITheme.MeiClothH * 0.5f),
                (int)clothW, (int)OnikiriUITheme.MeiClothH);

            if (selectedSlot >= 0) {
                fanPivot = slotPos[selectedSlot] + bladePerp * (fanUp ? -108f : 108f);
            }

            tagRect = new Rectangle((int)(sw * 0.055f),
                (int)(sh - OnikiriUITheme.MeiTagSize.Y - 76f),
                (int)OnikiriUITheme.MeiTagSize.X, (int)OnikiriUITheme.MeiTagSize.Y);

            nameColTop = new Vector2(sw * OnikiriUITheme.MeiNameColXRatio, sh * 0.16f);
            //吊挂卷轴锚:布左上的梁下,与右上的纳刀牌对称成"一梁两挂"
            registerSwitchAnchor = new Vector2(clothRect.X + 14f, clothRect.Y - 10f);
            closeTagAnchor = new Vector2(clothRect.Right - 30f, clothRect.Y - 6f);
        }

        /// <summary>目钉孔屏幕位(茎段前部)</summary>
        private Vector2 MekugiPos() {
            Vector2 tip = bladeCenter - bladeDir * (bladeW * 0.5f);
            return tip + bladeDir * (bladeW * 0.815f);
        }

        /// <summary>仪式字形尺寸:定鏨时放大,油布抹过后归位</summary>
        private float RiteGlyphSize()
            => OnikiriUITheme.MeiGlyphOnBlade * (1f + 0.45f * Rite.FocusPose * (1f - Rite.OilWipe * 0.9f));

        private void EaseArrays() {
            for (int i = 0; i < 3; i++) {
                float ht = i == hoverSlot ? 1f : 0f;
                slotHover[i] += (ht - slotHover[i]) * (ht > slotHover[i] ? 0.22f : 0.12f);
                float st = i == selectedSlot ? 1f : 0f;
                slotSelect[i] += (st - slotSelect[i]) * 0.15f;
            }
            float fe = selectedSlot >= 0 ? 1f : 0f;
            fanEase += (fe - fanEase) * (fe > fanEase ? 0.13f : 0.2f);
            for (int i = 0; i < ribEase.Length; i++) {
                float rt = i == hoverRib ? 1f : 0f;
                ribEase[i] += (rt - ribEase[i]) * (rt > ribEase[i] ? 0.25f : 0.14f);
            }
        }

        private void UpdateInteraction(float a) {
            bool inputAvailable = IsOpen && a > 0.9f;
            Vector2 mouse = MousePosition;
            Point mp = mouse.ToPoint();

            //铭位悬停
            hoverSlot = -1;
            if (inputAvailable) {
                for (int i = 0; i < 3; i++) {
                    if (Vector2.Distance(mouse, slotPos[i]) < OnikiriUITheme.MeiSlotRadius) {
                        hoverSlot = i;
                        break;
                    }
                }
            }

            //扇骨悬停
            int newHoverRib = -1;
            if (inputAvailable && selectedSlot >= 0 && fanEase > 0.6f) {
                int count = RibCount();
                float hitR = OnikiriUITheme.MeiFanGlyphSize * 0.62f;
                for (int i = 0; i < count; i++) {
                    if (Vector2.Distance(mouse, RibPos(i, count)) < hitR) {
                        newHoverRib = i;
                        break;
                    }
                }
            }
            if (newHoverRib != hoverRib) {
                hoverRib = newHoverRib;
                if (hoverRib >= 0) {
                    SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.25f, Volume = 0.3f });
                }
            }

            //收卷牌悬停
            bool tagHovered = inputAvailable && closeTagRect.Contains(mp);
            closeTagHover += ((tagHovered ? 1f : 0f) - closeTagHover) * 0.2f;
            if (tagHovered && !closeTagWasHovered) {
                closeTagRope.Nudge(Main.rand.NextFloat(0.8f, 1.5f) * (Main.rand.NextBool() ? 1f : -1f));
            }
            closeTagWasHovered = tagHovered;

            EaseArrays();

            if (!inputAvailable || keyLeftPressState != KeyPressState.Pressed) {
                return;
            }

            //====点击====
            if (tagHovered) {
                Close();
                return;
            }
            if (hoverSlot >= 0) {
                SelectSlot(hoverSlot == selectedSlot ? -1 : hoverSlot);
                return;
            }
            if (hoverRib >= 0) {
                HandleRibClick(hoverRib);
                return;
            }
            //扇开着时点空处先收扇,再点才收台
            if (selectedSlot >= 0) {
                SelectSlot(-1);
                return;
            }
            //点台外收台:白布(含余量)/木牌/名字列/吊挂卷轴之外
            Rectangle clothHit = clothRect;
            clothHit.Inflate(30, 46);
            Rectangle nameHit = new((int)(nameColTop.X - 46f), (int)(nameColTop.Y - 20f), 92,
                (int)(OnikiriUITheme.UIScreenH * 0.6f));
            if (!clothHit.Contains(mp) && !tagRect.Contains(mp) && !nameHit.Contains(mp)
                && !registerSwitch.Hovering) {
                Close();
            }
        }

        private void SelectSlot(int index) {
            selectedSlot = index;
            hoverRib = -1;
            Array.Clear(ribEase, 0, ribEase.Length);
            if (index >= 0) {
                //扇面朝向:向下张开会压到烙印木牌时翻到刀上方,骨与牌互不遮挡
                fanUp = FanWouldHitTag(index);
                SoundEngine.PlaySound(CWRSound.ButtonZero with { Volume = 0.5f });
                RebuildRibs();
            }
            else {
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.2f, Volume = 0.3f });
            }
        }

        /// <summary>预判向下张开的扇面外包是否压到烙印木牌(小分辨率下左侧铭位会撞)</summary>
        private bool FanWouldHitTag(int index) {
            Vector2 pivot = slotPos[index] + bladePerp * 108f;
            float reach = OnikiriUITheme.MeiFanRibLen + OnikiriUITheme.MeiFanGlyphSize * 1.4f;
            float halfW = reach * MathF.Sin(OnikiriUITheme.MeiFanSpread * 0.5f) + OnikiriUITheme.MeiFanGlyphSize;
            Rectangle fan = new((int)(pivot.X - halfW), (int)(pivot.Y - OnikiriUITheme.MeiFanGlyphSize * 0.5f),
                (int)(halfW * 2f), (int)(reach + OnikiriUITheme.MeiFanGlyphSize));
            Rectangle tagPad = tagRect;
            tagPad.Inflate(24, 24);
            return fan.Intersects(tagPad);
        }

        private void RebuildRibs() {
            ribs.Clear();
            ribs.AddRange(OniMeiRegistry.GetBySlot(SlotOf(selectedSlot)));
            ribsHasErase = EngravedAt(selectedSlot) != null;
        }

        private int RibCount() => ribs.Count + (ribsHasErase ? 1 : 0);

        /// <summary>扇骨纹章心位置,骨自枢张出(默认向下,防遮挡时向上)</summary>
        private Vector2 RibPos(int index, int count) {
            float ang = fanUp ? -MathHelper.PiOver2 : MathHelper.PiOver2;
            if (count > 1) {
                ang += (index / (count - 1f) - 0.5f) * OnikiriUITheme.MeiFanSpread;
            }
            float lift = index < ribEase.Length ? ribEase[index] * 7f : 0f;
            return fanPivot + ang.ToRotationVector2() * (OnikiriUITheme.MeiFanRibLen + lift);
        }

        /// <summary>骨 index 是否除铭骨(排在最末)</summary>
        private bool IsEraseRib(int index) => ribsHasErase && index == ribs.Count;

        private void HandleRibClick(int index) {
            OniMeiSlotKind slot = SlotOf(selectedSlot);
            string oldKey = OniMeiRegistry.DisplayStore?.Get(slot);

            if (IsEraseRib(index)) {
                if (OniMeiRegistry.EraseHeld(slot)) {
                    Rite.Start(OniMeiRiteKind.Erase, slot, oldKey, null);
                    SelectSlotSilently(-1);
                }
                else {
                    DenyFeedback();
                }
                return;
            }

            OniMeiDefinition def = ribs[index];
            if (def.Key == oldKey) {
                //已是现铭,合扇即可
                SelectSlot(-1);
                return;
            }
            if (OniMeiRegistry.EngraveHeld(slot, def.Key)) {
                Rite.Start(oldKey != null ? OniMeiRiteKind.Rename : OniMeiRiteKind.Engrave, slot, oldKey, def.Key);
                SelectSlotSilently(-1);
            }
            else {
                DenyFeedback();
            }
        }

        /// <summary>收扇不响声(仪式开演自带音)</summary>
        private void SelectSlotSilently(int index) {
            selectedSlot = index;
            hoverRib = -1;
            Array.Clear(ribEase, 0, ribEase.Length);
        }

        private static void DenyFeedback()
            => SoundEngine.PlaySound(SoundID.Unlock with { Pitch = -0.62f, Volume = 0.38f });

        private void UpdateAnomalies() {
            if (!IsOpen) {
                return;
            }
            //刀鸣:低频偶发,刃口白光颤过 + 极轻余韵
            if (songRun >= 0f) {
                songRun += 1f;
                if (songRun > 90f) {
                    songRun = -1f;
                    songCooldown = Main.rand.Next(700, 1500);
                }
            }
            else if (--songCooldown <= 0) {
                songRun = 0f;
                SoundEngine.PlaySound(SoundID.Item35 with { Pitch = 0.55f, Volume = 0.1f, MaxInstances = 1 });
            }
        }

        //====木牌内容解析与打字机====

        /// <summary>本帧木牌应展示的内容,返回(戳,题名,类目,出处,赋效,代价,金阶,除铭)</summary>
        private (string stamp, string title, string kind, string origin, string power, string burden, bool gold, bool erase) ResolveTag() {
            //仪式中:展示正在凿/锉的铭
            if (Rite.Active) {
                string key = Rite.NewKey ?? Rite.OldKey;
                if (key != null && OniMeiRegistry.TryGet(key, out OniMeiDefinition riteDef)) {
                    return ($"rite:{key}", riteDef.DisplayName.Value, SlotLabel(Rite.Slot),
                        riteDef.Origin.Value, riteDef.Power.Value, riteDef.Burden.Value,
                        riteDef.IsGoldTier, Rite.NewKey == null);
                }
            }
            //悬停扇骨:预览(凿前必见真实赋效与代价)
            if (selectedSlot >= 0 && hoverRib >= 0) {
                if (IsEraseRib(hoverRib)) {
                    return ("erase", EraseName.Value, SlotLabel(SlotOf(selectedSlot)), EraseHint.Value,
                        "———", "———", false, true);
                }
                OniMeiDefinition def = ribs[hoverRib];
                return ($"def:{def.Key}", def.DisplayName.Value, SlotLabel(def.SlotKind),
                    def.Origin.Value, def.Power.Value, def.Burden.Value, def.IsGoldTier, false);
            }
            //选中铭位:现铭或空悬文案
            if (selectedSlot >= 0) {
                OniMeiDefinition engraved = EngravedAt(selectedSlot);
                if (engraved != null) {
                    return ($"def:{engraved.Key}", engraved.DisplayName.Value, SlotLabel(SlotOf(selectedSlot)),
                        engraved.Origin.Value, engraved.Power.Value, engraved.Burden.Value,
                        engraved.IsGoldTier, false);
                }
                string hint = SlotOf(selectedSlot) switch {
                    OniMeiSlotKind.Hi => EmptyHintHi.Value,
                    OniMeiSlotKind.Horimono => EmptyHintHorimono.Value,
                    _ => EmptyHintNakago.Value,
                };
                return ($"empty:{selectedSlot}", EmptyName.Value, SlotLabel(SlotOf(selectedSlot)), hint,
                    "———", "———", false, false);
            }
            //默认:今名
            OniMeiDefinition name = OniMeiRegistry.CurrentBladeName(OniMeiRegistry.DisplayStore);
            if (name != null) {
                return ($"name:{name.Key}", name.DisplayName.Value, SlotLabel(OniMeiSlotKind.Nakago),
                    name.Origin.Value, name.Power.Value, name.Burden.Value, name.IsGoldTier, false);
            }
            return ("none", "", "", "", "", "", false, false);
        }

        internal static string SlotLabel(OniMeiSlotKind slot) => slot switch {
            OniMeiSlotKind.Hi => SlotHi.Value,
            OniMeiSlotKind.Horimono => SlotHorimono.Value,
            _ => SlotNakago.Value,
        };

        /// <summary>烙印打字机速度(字/帧):快而仍读得出"烫上去"的次第</summary>
        private const float TagCharsPerFrame = 2.1f;

        private void UpdateTagTypewriter() {
            (string stamp, _, _, _, _, _, _, _) = ResolveTag();
            if (stamp != tagStamp) {
                tagStamp = stamp;
                typeTimer = 0f;
                lastTypedChars = -1;
                burnAge = 60f;
            }
            typeTimer += 1f;
            int chars = (int)(typeTimer * TagCharsPerFrame);
            if (chars != lastTypedChars) {
                lastTypedChars = chars;
                burnAge = 0f;
            }
            burnAge = Math.Min(burnAge + 1f, 60f);
        }

        /// <summary>木牌烙印可见字符数</summary>
        internal int TagVisibleChars => Math.Max(0, (int)(typeTimer * TagCharsPerFrame));
        /// <summary>木牌最新字的灼热度 0~1</summary>
        internal float TagBurnStrength => 1f - MathHelper.Clamp(burnAge / 16f, 0f, 1f);

        //====绘制====

        public override void Draw(SpriteBatch spriteBatch) {
            float a = OpenProgress;
            if (a < 0.01f) {
                return;
            }
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);

            //====压暗世界 + 底缘烛光(视差)====
            Rectangle full = new(0, 0, (int)OnikiriUITheme.UIScreenW + 2, (int)OnikiriUITheme.UIScreenH + 2);
            spriteBatch.Draw(pixel, full, src, Color.Black * (a * 0.68f));
            Vector2 parallax = (OnikiriUITheme.UIMouse - OnikiriUITheme.UIScreenSize * 0.5f) * -0.012f;
            OniMeiRenderer.DrawCandleGlow(spriteBatch, bladeCenter, a, ShaderTime, parallax);

            //====白布====
            float reveal = a;
            OniMeiRenderer.DrawCloth(spriteBatch, clothRect, a, reveal, ShaderTime);

            //====刀身:开屏自右扫入,微震随仪式====
            float slideEase = VaultUtils.EaseOutCubic(MathHelper.Clamp(a / 0.55f, 0f, 1f));
            float slide = (1f - slideEase) * OnikiriUITheme.UIScreenW * 0.42f;
            Vector2 bladeDrawCenter = bladeCenter + bladeDir * slide + Rite.Shake;
            OniMeiRenderer.DrawBlade(spriteBatch, bladeDrawCenter, bladeDir, bladePerp, bladeW,
                OnikiriUITheme.MeiBladeQuadH, a, ShaderTime, slide, songRun);

            //开屏编舞:目钉飞脱 + 柄影褪去
            if (mekugiAnim > 0.001f && mekugiAnim < 1f) {
                OniMeiRenderer.DrawMekugiPop(spriteBatch, MekugiPos() + bladeDir * slide, bladeDir, bladePerp, mekugiAnim, a);
            }
            float tsukaAnim = MathHelper.Clamp((a - 0.55f) / 0.32f, 0f, 1f);
            if (IsOpen && tsukaAnim < 1f && a > 0.55f) {
                Vector2 tangEnd = bladeDrawCenter + bladeDir * (bladeW * 0.5f);
                OniMeiRenderer.DrawTsukaSlideOff(spriteBatch, tangEnd, bladeDir, bladePerp, tsukaAnim, a);
            }

            float contentA = MathHelper.Clamp((a - 0.5f) / 0.5f, 0f, 1f);

            //====仪式压暗与聚光(盖过刀身,衬字形)====
            if (Rite.Dim > 0.01f) {
                spriteBatch.Draw(pixel, full, src, Color.Black * Rite.Dim);
                Vector2 riteAnchor = slotPos[(int)Rite.Slot] + Rite.Shake;
                OniBrush.DrawBacklight(spriteBatch, riteAnchor, 120f, OnikiriUITheme.CandleWarm, Rite.Dim * 1.4f);
            }

            //====烙印木牌(先画:鏨盘扇与铭位交互层压在其上,不再被牌面遮挡)====
            if (contentA > 0.01f) {
                (_, string title, string kind, string origin, string power, string burden, bool gold, bool erase) = ResolveTag();
                if (title.Length > 0) {
                    OniMeiRenderer.DrawWoodTag(spriteBatch, font, tagRect, title, kind, origin, power,
                        burden, gold, erase, TagVisibleChars, TagBurnStrength, contentA, ShaderTime);
                }
            }

            //====铭位与字形====
            if (contentA > 0.01f) {
                DrawSlots(spriteBatch, font, contentA);
            }

            //====鏨盘扇====
            if (contentA > 0.01f && fanEase > 0.02f && selectedSlot >= 0) {
                DrawFan(spriteBatch, contentA);
            }

            //====仪式工具(鏨/锉)====
            if (Rite.Active) {
                DrawRiteTools(spriteBatch);
            }

            //====右缘刀铭大字====
            if (contentA > 0.01f) {
                DrawNameColumn(spriteBatch, font, contentA);
            }

            //====静物 + 题字 + 状态行====
            if (contentA > 0.01f) {
                OniMeiRenderer.DrawStillLife(spriteBatch, clothRect, contentA, ShaderTime);
                OniMeiRenderer.DrawTitle(spriteBatch, font, clothRect, TitleText.Value, contentA);
                DrawStatusLine(spriteBatch, font, contentA);
                OniRegisterRenderer.DrawCloseTag(spriteBatch, font, closeTagRope, contentA, closeTagHover,
                    GlobalTimer, CloseTagText.Value);
                DrawCloseHint(spriteBatch, font, contentA);
                OniMeiRenderer.DrawHangingScroll(spriteBatch, registerSwitch, contentA, GlobalTimer,
                    OniRegistry.InDanger);
            }

            particles.Draw(spriteBatch, a);

            //吊挂卷轴的悬浮说明(最后画,压在一切之上)
            if (registerSwitch.HoverEase > 0.05f) {
                OniMeiRenderer.DrawSwitchHoverTag(spriteBatch, font, MousePosition,
                    RegisterTabText.Value, RegisterTabHint.Value, a * registerSwitch.HoverEase);
            }
        }

        private void DrawSlots(SpriteBatch sb, DynamicSpriteFont font, float a) {
            for (int i = 0; i < 3; i++) {
                Vector2 pos = slotPos[i] + (Rite.Active && (int)Rite.Slot == i ? Rite.Shake : Vector2.Zero);
                OniMeiDefinition engraved = EngravedAt(i);
                bool riteHere = Rite.Active && (int)Rite.Slot == i;

                //常驻标记:暖芒/刻标/巡环/开屏涟漪,先垫在字形之下;仪式位让位给聚光
                if (!riteHere) {
                    float ripple = MathHelper.Clamp((slotRevealTimer - 6 - i * 13) / 34f, 0f, 1f);
                    OniMeiRenderer.DrawSlotMarker(sb, pos, OnikiriUITheme.MeiSlotRadius, engraved != null,
                        slotHover[i], a, ShaderTime, ripple, i);
                }

                //铭位字形
                if (riteHere) {
                    //旧铭锉去中
                    if (Rite.OldKey != null && Rite.OldReveal > 0.01f) {
                        OniMeiGlyphStyle oldStyle = OniMeiGlyphStyle.Engraved(a * Rite.OldReveal, OnikiriUITheme.MeiBladeCant);
                        oldStyle.Time = ShaderTime;
                        OniMeiGlyph.Draw(sb, Rite.OldKey, pos, OnikiriUITheme.MeiGlyphOnBlade, oldStyle);
                    }
                    //新铭凿现中
                    if (Rite.NewKey != null && Rite.NewReveal >= 0f) {
                        OniMeiGlyphStyle style = new() {
                            Alpha = a,
                            Rotation = OnikiriUITheme.MeiBladeCant,
                            ChiselReveal = Rite.NewReveal,
                            Accent = Rite.GoldTier ? OnikiriUITheme.GoldInlay : OnikiriUITheme.Bright,
                            Inlay = Rite.InlayFill,
                            Lit = Rite.OilWipe * 0.5f * (1f - Rite.InlayFill * 0.6f),
                            Time = ShaderTime,
                        };
                        OniMeiGlyph.Draw(sb, Rite.NewKey, pos, RiteGlyphSize(), style);
                        //油布抹过:一线软亮扫过字形(两端没入,不再硬边横条)
                        if (Rite.OilWipe > 0.01f && Rite.OilWipe < 0.99f) {
                            float sweepX = MathHelper.Lerp(-1.2f, 1.2f, Rite.OilWipe);
                            Vector2 wipePos = pos + bladeDir * (sweepX * RiteGlyphSize() * 0.6f);
                            OniBrush.DrawSoftStreak(sb, wipePos, OnikiriUITheme.MeiBladeCant + MathHelper.PiOver2,
                                RiteGlyphSize() * 1.25f, 2.4f, OnikiriUITheme.HotWhite,
                                a * 0.40f * (float)Math.Sin(Rite.OilWipe * MathHelper.Pi), glowMul: 0.7f);
                        }
                    }
                }
                else if (engraved != null) {
                    OniMeiGlyphStyle style = OniMeiGlyphStyle.Engraved(a, OnikiriUITheme.MeiBladeCant);
                    style.Time = ShaderTime;
                    style.Inlay = engraved.IsGoldTier ? 1f : 0f;
                    style.Accent = engraved.IsGoldTier ? OnikiriUITheme.GoldInlay : OnikiriUITheme.Bright;
                    //悬停/选中:槽内微光呼吸
                    style.Lit = Math.Max(slotHover[i] * 0.45f, slotSelect[i] * 0.6f);
                    OniMeiGlyph.Draw(sb, engraved.Key, pos, OnikiriUITheme.MeiGlyphOnBlade, style);
                }
                else {
                    OniMeiRenderer.DrawSlotEmpty(sb, pos, OnikiriUITheme.MeiGlyphOnBlade,
                        slotHover[i], slotSelect[i], a, ShaderTime, OnikiriUITheme.MeiBladeCant);
                }

                //铭位环与标签
                OniMeiRenderer.DrawSlotRing(sb, pos, OnikiriUITheme.MeiSlotRadius,
                    slotHover[i], slotSelect[i], a, ShaderTime);
                string label = SlotLabel(SlotOf(i));
                Vector2 lSize = font.MeasureString(label) * 0.62f;
                Vector2 lPos = pos + bladePerp * (OnikiriUITheme.MeiSlotRadius + 16f) - lSize * 0.5f;
                Utils.DrawBorderString(sb, label, lPos,
                    Color.Lerp(OnikiriUITheme.TextDim, OnikiriUITheme.Paper, slotHover[i]) * (a * 0.8f), 0.62f);
            }

            //悬停扇骨:粉笔稿投影到铭位(试铭)
            if (selectedSlot >= 0 && hoverRib >= 0 && !IsEraseRib(hoverRib)) {
                float pulse = 0.75f + 0.25f * (float)Math.Sin(ShaderTime * 4f);
                OniMeiGlyph.DrawChalk(sb, ribs[hoverRib].Key, slotPos[selectedSlot],
                    OnikiriUITheme.MeiGlyphOnBlade, a * pulse * ribEase[Math.Min(hoverRib, ribEase.Length - 1)],
                    OnikiriUITheme.MeiBladeCant);
            }
        }

        private void DrawFan(SpriteBatch sb, float a) {
            int count = RibCount();
            OniMeiDefinition engraved = EngravedAt(selectedSlot);
            for (int i = 0; i < count; i++) {
                //逐骨错拍展开
                float vis = MathHelper.Clamp((fanEase - i * 0.07f) * 1.5f, 0f, 1f);
                if (vis <= 0.01f) {
                    continue;
                }
                Vector2 pos = RibPos(i, count);
                float hov = i < ribEase.Length ? ribEase[i] : 0f;
                if (IsEraseRib(i)) {
                    OniMeiRenderer.DrawFanRibErase(sb, fanPivot, pos, vis, hov, a, ShaderTime);
                    continue;
                }
                OniMeiDefinition def = ribs[i];
                bool isCurrent = engraved != null && engraved.Key == def.Key;
                OniMeiRenderer.DrawFanRib(sb, fanPivot, pos, def.Key, def.IsGoldTier, isCurrent,
                    vis, hov, a, ShaderTime);
            }
            //枢钉
            Texture2D pixel = VaultAsset.placeholder2.Value;
            sb.Draw(pixel, fanPivot, new Rectangle(0, 0, 1, 1), OnikiriUITheme.Seal * (a * fanEase),
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(4.4f), SpriteEffects.None, 0f);
        }

        private void DrawRiteTools(SpriteBatch sb) {
            Vector2 anchor = slotPos[(int)Rite.Slot] + Rite.Shake;
            //锉拍:锉刀横扫
            if (Rite.OldKey != null && Rite.OldReveal > 0.01f) {
                OniMeiRenderer.DrawFileTool(sb, anchor, OnikiriUITheme.MeiGlyphOnBlade,
                    1f - Rite.OldReveal, 1f, ShaderTime);
            }
            //凿拍:鏨具压在笔锋上
            if (Rite.NewKey != null && Rite.FocusPose > 0.01f && Rite.NewReveal < 1f) {
                Vector2 tip = Rite.NewReveal >= 0f
                    ? OniMeiGlyph.GetChiselPoint(Rite.NewKey, anchor, RiteGlyphSize(), OnikiriUITheme.MeiBladeCant,
                        Math.Max(Rite.NewReveal, 0f))
                    : anchor;
                OniMeiRenderer.DrawChiselTool(sb, tip, Rite.FocusPose, Rite.Shake, ShaderTime);
            }
        }

        private void DrawNameColumn(SpriteBatch sb, DynamicSpriteFont font, float a) {
            bool riteOnName = Rite.Active && Rite.Slot == OniMeiSlotKind.Nakago;
            if (riteOnName) {
                //旧名褪去
                string oldName = Rite.OldKey != null && OniMeiRegistry.TryGet(Rite.OldKey, out OniMeiDefinition oldDef)
                    ? oldDef.DisplayName.Value
                    : OniMeiRegistry.CurrentBladeName(null)?.DisplayName.Value ?? "";
                float oldVis = Rite.OldKey != null ? Rite.NameOldVis : 1f - Rite.FocusPose;
                if (oldVis > 0.01f && oldName.Length > 0) {
                    OniMeiRenderer.DrawNameColumn(sb, font, oldName, nameColTop, a * oldVis, 1f, false, ShaderTime);
                }
                //新名以笔顺写入
                if (Rite.NewKey != null && OniMeiRegistry.TryGet(Rite.NewKey, out OniMeiDefinition newDef)
                    && Rite.NameNewVis > 0.01f) {
                    OniMeiRenderer.DrawNameColumn(sb, font, newDef.DisplayName.Value, nameColTop,
                        a, Rite.NameNewVis, true, ShaderTime);
                }
                return;
            }
            OniMeiDefinition name = OniMeiRegistry.CurrentBladeName(OniMeiRegistry.DisplayStore);
            if (name != null) {
                OniMeiRenderer.DrawNameColumn(sb, font, name.DisplayName.Value, nameColTop,
                    a * postRiteNameEase, 1f, false, ShaderTime);
            }
        }

        private void DrawStatusLine(SpriteBatch sb, DynamicSpriteFont font, float a) {
            int engraved = 0;
            for (int i = 0; i < 3; i++) {
                if (EngravedAt(i) != null) {
                    engraved++;
                }
            }
            string nameText = OniMeiRegistry.CurrentBladeName(OniMeiRegistry.DisplayStore)?.DisplayName.Value ?? "?";
            string status = string.Format(StatusFormat.Value, engraved, nameText);
            Vector2 size = font.MeasureString(status) * 0.7f;
            Utils.DrawBorderString(sb, status,
                new Vector2(clothRect.Center.X - size.X * 0.5f, clothRect.Bottom - 34f),
                OnikiriUITheme.TextDim * (a * 0.85f), 0.7f);
        }

        private void DrawCloseHint(SpriteBatch sb, DynamicSpriteFont font, float a) {
            string keyName = CWRKeySystem.Legend_UIControl.ToTooltipString(CWRKeySystem.Notbound.Value);
            string hint = string.Format(CloseHintFormat.Value, keyName);
            Vector2 size = font.MeasureString(hint) * 0.62f;
            float y = Math.Min(clothRect.Bottom + 16f, OnikiriUITheme.UIScreenH - 24f);
            Utils.DrawBorderString(sb, hint,
                new Vector2(clothRect.Center.X - size.X * 0.5f, y),
                OnikiriUITheme.TextDim * (a * 0.6f), 0.62f);
        }
    }
}
