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
    /// 鏨盘扇骨数自适应布局:骨少单排原张角;骨多张角渐宽、双排交错、菱章缓降;
    /// maxReach 出屏保护整体收缩。绘制/命中/粉笔预览/避牌判定共用同一份
    /// </summary>
    internal readonly struct OniMeiFanLayout
    {
        /// <summary>全张角(弧度)</summary>
        public readonly float Spread;
        /// <summary>菱章尺寸</summary>
        public readonly float GlyphSize;
        /// <summary>双排径向错距(0=单排;偶数位外圈,奇数位内圈)</summary>
        public readonly float RowOffset;
        /// <summary>基准骨长(双排的中线)</summary>
        public readonly float RibLen;

        /// <param name="count">骨数(含除铭骨)</param>
        /// <param name="maxReach">枢到屏缘可用纵深;≤60 视为不限(额定布局)</param>
        public OniMeiFanLayout(int count, float maxReach) {
            float t = MathHelper.Clamp((count - 6) / 5f, 0f, 1f);
            Spread = MathHelper.Lerp(OnikiriUITheme.MeiFanSpread, OnikiriUITheme.MeiFanSpreadMax, t);
            GlyphSize = MathHelper.Lerp(OnikiriUITheme.MeiFanGlyphSize, OnikiriUITheme.MeiFanGlyphSizeMin, t);
            RibLen = MathHelper.Lerp(OnikiriUITheme.MeiFanRibLen, OnikiriUITheme.MeiFanRibLenFar, t);
            RowOffset = count >= 7 ? OnikiriUITheme.MeiFanRowOffset : 0f;
            //出屏保护:外径吃不下时骨长与错距同比收缩(封底,别缩没)
            float outer = OuterReachOf(RibLen, RowOffset, GlyphSize);
            if (maxReach > 60f && outer > maxReach) {
                float k = MathHelper.Max(maxReach / outer, 0.55f);
                RibLen *= k;
                RowOffset *= k;
            }
        }

        /// <summary>纹章心最远径 + 菱章外包余量(避牌/出屏判定用)</summary>
        public float OuterReach => OuterReachOf(RibLen, RowOffset, GlyphSize);

        private static float OuterReachOf(float len, float row, float glyph) => len + row + glyph * 0.9f;

        /// <summary>index 骨的径向长(双排交错:偶外奇内)</summary>
        public float RadiusOf(int index)
            => RibLen + (RowOffset <= 0f ? 0f : (index & 1) == 0 ? RowOffset : -RowOffset);
    }

    /// <summary>
    /// 改铭台全屏:左列黑漆台账主板(题头+三铭位牌+脚注状态),右侧鬼切本体原生姿态 2x 陈列,
    /// 注记引线把牌钉到刀身对应位置;鏨仪式走"检分镜头"——以铭位锚为不动点把刀推近 5x 特写,
    /// 凿毕光包沿引线归牌盖章;鏨盘扇/錾样匣/烙印木牌/右缘大字沿用;
    /// 与点鬼簿互斥同级(互斥收台静默,切换只响一声);仪式演出态见 <see cref="OniMeiRite"/>
    /// </summary>
    internal sealed class OniMeiUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.OnikiriText";
        public static OniMeiUI Instance => UIHandleLoader.GetUIHandleOfType<OniMeiUI>();

        private const string FreezeReason = "OniMei";
        private const float FanRibRevealDelay = 0.07f;
        private const float FanRibRevealDuration = 2f / 3f;
        private const float FanRibMaxRevealDelay = 0.72f;
        private const float FanRibInteractiveReveal = 0.5f;

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
        public static LocalizedText TrayTitle { get; private set; }
        public static LocalizedText TrayEmpty { get; private set; }

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
            TrayTitle = this.GetLocalization(nameof(TrayTitle), () => "行囊錾样");
            TrayEmpty = this.GetLocalization(nameof(TrayEmpty), () => "行囊无此位錾样。扇上所持仍可先凿");
        }
        #endregion

        public override bool CloseOnEscape => true;
        public override float RenderPriority => 2f;
        public override SoundStyle? OpenSound => SoundID.Unlock with { Pitch = -0.2f, Volume = 0.5f };
        public override SoundStyle? CloseSound => SilentSwap ? null
            : SoundID.MenuClose with { Pitch = -0.35f, Volume = 0.5f };
        public override Vector2 MousePosition => OnikiriUITheme.UIMouse;

        /// <summary>姊妹屏互斥收台时置位:抑制本屏关闭音,切换只响新屏开音一声</summary>
        internal bool SilentSwap;
        private bool keepMouseSlot;
        private bool inventoryWasOpen;
        private long mouseSlotInstanceId;
        private int interactionSession;

        //====布局(每帧 UI 空间重算)====
        /// <summary>台账主板(题头+三铭位牌+脚注)</summary>
        private Rectangle panelRect;
        /// <summary>铭位牌心(台账右列)</summary>
        private readonly Vector2[] slotPos = new Vector2[3];
        /// <summary>铭位刀身锚(贴图 px,剪影中线;检分镜头的不动点候选)</summary>
        private readonly Vector2[] anchorPx = new Vector2[3];
        /// <summary>刀身绳结屏幕位(栋侧剪影外扩,随本帧变换)</summary>
        private readonly Vector2[] pinScreen = new Vector2[3];
        /// <summary>注记墨线起点(牌右顶点)</summary>
        private readonly Vector2[] lineStart = new Vector2[3];
        /// <summary>陈列心屏幕位(贴图几何中心的落点)</summary>
        private Vector2 exhibitCenter;
        /// <summary>本帧变换:贴图内不动点(陈列=几何中心,检分=仪式锚)</summary>
        private Vector2 xformOriginPx = OniMeiBladeDraw.SpriteCenter;
        /// <summary>本帧变换:不动点的屏幕位</summary>
        private Vector2 xformPos;
        /// <summary>本帧缩放(陈列 2x ↔ 检分 5x)</summary>
        private float curScale = OnikiriUITheme.MeiExhibitScale;
        /// <summary>陈列外包(收台点外判定)</summary>
        private Rectangle exhibitRect;
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
        private float fanEase;
        private readonly List<OniMeiDefinition> ribs = [];
        private bool ribsHasErase;
        private int hoverRib = -1;
        /// <summary>骨悬停缓动,随 <see cref="RebuildRibs"/> 按骨数扩容</summary>
        private float[] ribEase = new float[12];
        /// <summary>本帧扇布局(LayoutCompute 重算,绘制/命中共用)</summary>
        private OniMeiFanLayout fanLayout = new(1, 0f);
        private float closeTagHover;
        private bool closeTagWasHovered;
        private readonly OniRope closeTagRope = new(5, 22f);
        //錾样匣:与烙印木牌同底边的行囊木板
        private readonly List<OniMeiTrayEntry> tray = [];
        private int hoverTray = -1;
        private readonly float[] trayCellEase = new float[12];
        private int trayPage;
        private Vector2 trayOrigin;
        private Rectangle trayRect;
        private bool trayPageLeftHover;
        private bool trayPageRightHover;
        //吊挂卷轴:回点鬼簿的门(对面器物的微缩,挂在布左上的梁下)
        private readonly OniHangingSwitch registerSwitch = new(SoundID.MenuTick with { Pitch = -0.2f, Volume = 0.45f });
        private Vector2 registerSwitchAnchor;

        //====动画状态====
        internal float ShaderTime;
        private readonly OniUIParticlePool particles = new(200);
        /// <summary>落台编舞:刀自上落定的那一磕(锚钉起尘+醒刀鸣光)</summary>
        private bool settleStarted;
        private float settleAnim;
        private float postRiteNameEase = 1f;

        //====检分镜头(仪式聚焦缩放)====
        /// <summary>0 陈列 → 1 特写,缓动追 Rite.Active;整场以仪式锚为缩放不动点</summary>
        private float zoomEase;
        /// <summary>缩放不动点(贴图 px),开演一帧定格到仪式铭位</summary>
        private Vector2 zoomAnchorPx = OniMeiBladeDraw.SpriteCenter;
        private bool prevRiteActive;
        //====接铭归线(收镜时光包沿引线回牌+盖章)====
        private int receiveSlot = -1;
        private float receiveAnim = 1f;
        private bool receiveGold;
        //开屏涟漪:内容可见后逐位点名三处铭位(帧计数,溢出即停)
        private int slotRevealTimer;

        //====木牌打字机====
        private string tagStamp = "";
        private float typeTimer;
        private int lastTypedChars = -1;
        private float burnAge = 60f;
        /// <summary>木牌高度缓动(实高按内容实测,切换时板体顺滑生长)</summary>
        private float tagHeightEase;

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
            interactionSession++;
            inventoryWasOpen = Main.playerInventory;
            OnikiriData mouseData = OnikiriData.TryGet(Main.mouseItem);
            keepMouseSlot = mouseData != null;
            mouseSlotInstanceId = mouseData?.InstanceId ?? 0;
            Main.playerInventory = keepMouseSlot;
            OniTalismanHud.RememberLedger(OniLedgerView.Mei);
            //姊妹屏互斥:一台开另一卷收;静默收卷,免得开音+关音同帧叠成两声切换
            if (OniRegisterUI.Instance?.IsOpen ?? false) {
                OniRegisterUI.Instance.SilentSwap = true;
                OniRegisterUI.Instance.Close();
                OniRegisterUI.Instance.SilentSwap = false;
            }
            selectedSlot = -1;
            hoverRib = -1;
            hoverTray = -1;
            trayPage = 0;
            fanEase = 0f;
            tray.Clear();
            Array.Clear(trayCellEase, 0, trayCellEase.Length);
            settleStarted = false;
            settleAnim = 0f;
            zoomEase = 0f;
            zoomAnchorPx = OniMeiBladeDraw.SpriteCenter;
            prevRiteActive = false;
            receiveSlot = -1;
            receiveAnim = 1f;
            slotRevealTimer = 0;
            tagStamp = "";
            lastTypedChars = -1;
            tagHeightEase = 0f;
            postRiteNameEase = 1f;
            registerSwitch.Reset();
            songCooldown = Main.rand.Next(700, 1400);
            songRun = -1f;
            particles.Clear();
            LayoutCompute();
            closeTagRope.WarmStart(closeTagAnchor);
            if (VaultUtils.isSinglePlayer) {
                WorldFreezeSystem.Activate(FreezeReason);
            }
        }

        protected override void OnClose() {
            interactionSession++;
            if (keepMouseSlot) {
                Main.playerInventory = inventoryWasOpen;
            }
            keepMouseSlot = false;
            mouseSlotInstanceId = 0;
            //收台时未完的鏨仪式直接定格,重开不再续播半场
            if (Rite.Active) {
                Rite.Skip();
            }
            if (VaultUtils.isSinglePlayer) {
                WorldFreezeSystem.Deactivate(FreezeReason);
            }
            //教程临时刻铭事务：关闭改铭台时恢复快照，保证教程铭不污染存档
            Tutorial.OnikiriTutorialFlow.RestoreMeiSnapshotOnClose();
        }

        //====数据视图====

        private static OniMeiSlotKind SlotOf(int index) => (OniMeiSlotKind)index;

        /// <summary>铭位上的定义(展示缓存),空 null</summary>
        private static OniMeiDefinition EngravedAt(int index)
            => OniMeiRegistry.GetEngraved(OniMeiRegistry.DisplayStore, SlotOf(index));

        public override void Update() {
            if (IsOpen) {
                if (!MaintainMouseSlot()) {
                    return;
                }
                player.mouseInterface = true;
            }
        }

        public override void LogicUpdate() {
            if (IsOpen) {
                if (!MaintainMouseSlot()) {
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
            particles.Update();
            LayoutCompute();

            //开屏编舞:刀自上落定的一磕——锚钉处起尘,一声轻叩,落定过半刃口鸣光醒刀
            if (IsOpen && a >= 0.55f && !settleStarted) {
                settleStarted = true;
                SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.35f, Volume = 0.45f });
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.4f, Volume = 0.35f });
                for (int i = 0; i < 3; i++) {
                    particles.SpawnFiling(pinScreen[0]);
                    particles.SpawnFiling(pinScreen[2]);
                }
            }
            if (settleStarted && settleAnim < 1f) {
                settleAnim = Math.Min(settleAnim + 1f / 26f, 1f);
                if (settleAnim >= 0.5f && songRun < 0f) {
                    songRun = 0f;
                    SoundEngine.PlaySound(SoundID.Item35 with { Pitch = 0.55f, Volume = 0.12f, MaxInstances = 1 });
                }
            }

            //检分镜头:开演一帧把不动点定格到仪式铭位;收演回落时放接铭归线
            if (Rite.Active && !prevRiteActive) {
                zoomAnchorPx = anchorPx[(int)Rite.Slot];
            }
            if (!Rite.Active && prevRiteActive) {
                receiveSlot = (int)Rite.Slot;
                receiveGold = Rite.GoldTier;
                //除铭无新字可归档,不放光包与盖章
                receiveAnim = Rite.NewKey != null ? 0f : 1f;
            }
            prevRiteActive = Rite.Active;
            float zoomTarget = Rite.Active ? 1f : 0f;
            zoomEase += (zoomTarget - zoomEase) * (zoomTarget > zoomEase ? 0.085f : 0.105f);
            if (Math.Abs(zoomEase - zoomTarget) < 0.002f) {
                zoomEase = zoomTarget;
            }
            if (receiveAnim < 1f) {
                receiveAnim = Math.Min(receiveAnim + 1f / 34f, 1f);
            }
            //开屏涟漪计时:内容可见后开始点名铭位
            if (IsOpen && a >= 0.8f && slotRevealTimer < 400) {
                slotRevealTimer++;
            }

            //收卷木牌摆
            closeTagRope.Update(closeTagAnchor, null, ShaderTime, 0.24f, endWeight: 0.55f);
            Vector2 tagTop = closeTagRope.End;
            closeTagRect = new Rectangle((int)(tagTop.X - 16f), (int)tagTop.Y - 2, 32, 48);

            //吊挂卷轴:点击预演到帧即移步点鬼簿;簿上有鬼躁动时回声更急
            bool openRegister = registerSwitch.Update(registerSwitchAnchor, MousePosition,
                IsOpen && a > 0.9f && !Rite.Active, ShaderTime, OnikiriUITheme.HangScrollHit,
                keyLeftPressState, OniRegistry.InDanger);
            if (Tutorial.OnikiriTutorialLead.IsActive) {
                Tutorial.OnikiriTutorialTargets.Publish(
                    Tutorial.OnikiriTutorialTargets.Tag_RegisterSwitch, registerSwitch.HitBox);
            }
            if (openRegister) {
                OniRegisterUI.Instance?.Open();
            }

            //鏨仪式推进:期间吞交互,点击可跳;锚=检分镜头不动点的屏幕位
            if (Rite.Active) {
                Rite.Update(RiteAnchorScreen(), RiteGlyphSize(), OniMeiBladeDraw.GlyphRot, particles);
                postRiteNameEase = 0f;
                if (IsOpen && keyLeftPressState == KeyPressState.Pressed) {
                    Rite.Skip();
                }
                hoverSlot = -1;
                hoverRib = -1;
                hoverTray = -1;
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

            //台账主板:左列;小屏收高给底行(木牌/匣)让位
            float panelY = sh * OnikiriUITheme.MeiPanelYRatio;
            float panelH = Math.Min(OnikiriUITheme.MeiPanelH, sh - panelY - 292f);
            panelRect = new Rectangle((int)(sw * OnikiriUITheme.MeiPanelXRatio), (int)panelY,
                (int)OnikiriUITheme.MeiPanelW, (int)panelH);

            //铭位牌:题头下三行沿内容带均布,牌身靠右缘(引线自右顶点出板)
            float contentTop = panelRect.Y + OnikiriUITheme.MeiPanelHeaderH;
            float contentH = panelH - OnikiriUITheme.MeiPanelHeaderH - OnikiriUITheme.MeiPanelFooterH;
            for (int i = 0; i < 3; i++) {
                slotPos[i] = new Vector2(panelRect.Right - 64f, contentTop + contentH * (0.18f + 0.32f * i));
                anchorPx[i] = OniMeiBladeDraw.SpinePx(SlotU(i));
                if ((OniMeiSlotKind)i == OniMeiSlotKind.Nakago) {
                    anchorPx[i] += OnikiriUITheme.MeiNakagoMarkOffsetPx;
                }
            }

            //陈列心:台账右侧展刀区;底缘向錾样匣行让位。
            //宽屏时木牌居中列,刀再靠右;窄屏(单行摆不下)木牌回落屏左下,刀左移并硬夹在屏内
            bool narrow = sw < 1200f;
            float trayTop = sh - OnikiriUITheme.MeiTrayBottomMargin - OnikiriUITheme.MeiTrayPanelSize.Y;
            float exY = Math.Min(sh * OnikiriUITheme.MeiExhibitYRatio,
                trayTop - OniMeiBladeDraw.SpriteSize.Y * OnikiriUITheme.MeiExhibitScale * 0.5f - 14f);
            float exMinX = panelRect.Right + (narrow ? 260f : 512f);
            float exX = Math.Min(Math.Max(sw * OnikiriUITheme.MeiExhibitXRatio, exMinX), sw - 122f);
            exhibitCenter = new Vector2(exX, exY);

            //本帧陈列/检分变换:恒以 zoomAnchorPx 为不动点(zoomEase=0 时等价居中陈列)
            float zoomK = zoomEase * zoomEase * (3f - 2f * zoomEase);
            curScale = MathHelper.Lerp(OnikiriUITheme.MeiExhibitScale, OnikiriUITheme.MeiZoomScale, zoomK);
            Vector2 anchorRest = exhibitCenter
                + (zoomAnchorPx - OniMeiBladeDraw.SpriteCenter) * OnikiriUITheme.MeiExhibitScale;
            Vector2 focus = new(sw * 0.5f, sh * 0.46f);
            xformOriginPx = zoomAnchorPx;
            xformPos = Vector2.Lerp(anchorRest, focus, zoomK);

            //锚钉与引线端点(随变换,检分时钉随刀走)
            for (int i = 0; i < 3; i++) {
                pinScreen[i] = MapSprite(OniMeiBladeDraw.BackPx(SlotU(i), 6f));
                lineStart[i] = slotPos[i] + new Vector2(OnikiriUITheme.MeiMedallionSize * 0.62f, 0f);
            }

            //陈列外包(收台点外判定)
            Vector2 tl = MapSprite(Vector2.Zero);
            exhibitRect = new Rectangle((int)tl.X, (int)tl.Y,
                (int)(OniMeiBladeDraw.SpriteSize.X * curScale),
                (int)(OniMeiBladeDraw.SpriteSize.Y * curScale));

            if (selectedSlot >= 0) {
                //鏨盘扇向右横开进"台账↔刀"的空档,横向上与牌列彻底分离,骨牌永不压牌
                fanPivot = slotPos[selectedSlot] + Vector2.UnitX * 96f;
                float fanRoom = Math.Min(exhibitRect.Left, sw) - 16f - fanPivot.X;
                fanLayout = new OniMeiFanLayout(RibCount(), fanRoom);
            }

            //烙印木牌:挂在台账右侧,与錾样匣同底边;宽固定,高按当前内容实测(底边锚定向上生长)
            (_, string tagTitle, _, string tagOrigin, string tagPower, string tagBurden, _, _) = ResolveTag();
            float tagH = OnikiriUITheme.MeiTagSize.Y;
            if (tagTitle.Length > 0) {
                tagH = MathHelper.Clamp(OniMeiRenderer.MeasureTagHeight(FontAssets.MouseText.Value,
                    OnikiriUITheme.MeiTagSize.X, tagOrigin, tagPower, tagBurden), 200f, sh * 0.6f);
            }
            tagHeightEase = tagHeightEase <= 0f ? tagH : tagHeightEase + (tagH - tagHeightEase) * 0.3f;
            tagRect = new Rectangle((int)(narrow ? sw * 0.03f : panelRect.Right + 26f),
                (int)(sh - tagHeightEase - OnikiriUITheme.MeiTrayBottomMargin),
                (int)OnikiriUITheme.MeiTagSize.X, (int)tagHeightEase);

            //匣木板:与木牌同底边距,坐其右侧;溢出则夹到屏内
            float trayW = OnikiriUITheme.MeiTrayPanelSize.X;
            float trayH = OnikiriUITheme.MeiTrayPanelSize.Y;
            float trayX = tagRect.Right + OnikiriUITheme.MeiTrayTagGap;
            float trayY = sh - trayH - OnikiriUITheme.MeiTrayBottomMargin;
            if (trayX + trayW > sw - 36f) {
                trayX = Math.Max(36f, sw - trayW - 36f);
            }
            trayRect = new Rectangle((int)trayX, (int)trayY, (int)trayW, (int)trayH);
            //格心行:题头朱线下方
            trayOrigin = new Vector2(trayRect.Center.X, trayRect.Y + 72f);

            nameColTop = new Vector2(sw * OnikiriUITheme.MeiNameColXRatio, sh * 0.16f);
            //顶梁两挂:卷轴左、纳刀牌右,绳自屏顶垂下,与居中题字同带成"一梁两挂一题"
            registerSwitchAnchor = new Vector2(sw * OnikiriUITheme.MeiHangLeftXRatio, -4f);
            closeTagAnchor = new Vector2(sw * OnikiriUITheme.MeiHangRightXRatio, -6f);
        }

        /// <summary>铭位 index 的轴向归一位</summary>
        private static float SlotU(int index) => (OniMeiSlotKind)index switch {
            OniMeiSlotKind.Hi => OnikiriUITheme.MeiSlotUHi,
            OniMeiSlotKind.Horimono => OnikiriUITheme.MeiSlotUHorimono,
            _ => OnikiriUITheme.MeiSlotUNakago,
        };

        /// <summary>贴图 px → 屏幕位(本帧陈列/检分变换)</summary>
        private Vector2 MapSprite(Vector2 px) => xformPos + (px - xformOriginPx) * curScale;

        /// <summary>本帧仪式锚屏幕位(=检分变换不动点)</summary>
        private Vector2 RiteAnchorScreen() => xformPos;

        /// <summary>仪式字形尺寸:刀上刻痕字径×当前缩放,定鏨时再放大一挡,油布抹过后归位</summary>
        private float RiteGlyphSize()
            => OnikiriUITheme.MeiBladeMarkPx * curScale
            * (1f + 0.45f * Rite.FocusPose * (1f - Rite.OilWipe * 0.9f));

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
            for (int i = 0; i < trayCellEase.Length; i++) {
                float tt = i == hoverTray ? 1f : 0f;
                trayCellEase[i] += (tt - trayCellEase[i]) * (tt > trayCellEase[i] ? 0.25f : 0.14f);
            }
        }

        private void UpdateInteraction(float a) {
            //检分镜头收尾余波里先不接交互,拉回陈列后再放开
            bool inputAvailable = IsOpen && a > 0.9f && zoomEase < 0.25f;
            Vector2 mouse = MousePosition;
            Point mp = mouse.ToPoint();

            //铭位悬停:点牌或点刀上绳结等价(双向呼应)
            hoverSlot = -1;
            if (inputAvailable) {
                for (int i = 0; i < 3; i++) {
                    if (Vector2.Distance(mouse, slotPos[i]) < OnikiriUITheme.MeiMedallionHitRadius
                        || Vector2.Distance(mouse, pinScreen[i]) < OnikiriUITheme.MeiAnchorHitRadius) {
                        hoverSlot = i;
                        break;
                    }
                }
            }
            //教程焦点：发布三个铭位和当前选中铭位
            if (Tutorial.OnikiriTutorialLead.IsActive) {
                string[] slotTags = [Tutorial.OnikiriTutorialTargets.Tag_MeiSlotNakago,
                    Tutorial.OnikiriTutorialTargets.Tag_MeiSlotHi, Tutorial.OnikiriTutorialTargets.Tag_MeiSlotHorimono];
                int sr = (int)OnikiriUITheme.MeiMedallionHitRadius;
                for (int i = 0; i < 3; i++) {
                    Tutorial.OnikiriTutorialTargets.Publish(slotTags[i],
                        new Rectangle((int)(slotPos[i].X - sr), (int)(slotPos[i].Y - sr), sr * 2, sr * 2));
                }
            }

            //扇骨悬停
            int newHoverRib = -1;
            if (inputAvailable && selectedSlot >= 0 && fanEase > 0.6f && !Rite.Active) {
                int count = RibCount();
                float hitR = fanLayout.GlyphSize * 0.62f;
                for (int i = 0; i < count; i++) {
                    float reveal = RibReveal(i, count);
                    if (reveal < FanRibInteractiveReveal) {
                        continue;
                    }
                    if (Vector2.Distance(mouse, RibDrawPos(i, count, reveal)) < hitR) {
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

            //錾样匣悬停(+翻页点)
            int newHoverTray = -1;
            trayPageLeftHover = false;
            trayPageRightHover = false;
            if (inputAvailable && selectedSlot >= 0 && fanEase > 0.55f && !Rite.Active) {
                int visible = TrayVisibleCount();
                for (int i = 0; i < visible; i++) {
                    if (Vector2.Distance(mouse, TrayCellPos(i, visible)) < OnikiriUITheme.MeiTrayHitRadius) {
                        newHoverTray = i;
                        break;
                    }
                }
                int pages = TrayPageCount();
                if (pages > 1 && newHoverTray < 0) {
                    Vector2 leftDot = TrayPageDotPos(true);
                    Vector2 rightDot = TrayPageDotPos(false);
                    trayPageLeftHover = Vector2.Distance(mouse, leftDot) < 10f && trayPage > 0;
                    trayPageRightHover = Vector2.Distance(mouse, rightDot) < 10f && trayPage < pages - 1;
                }
            }
            if (newHoverTray != hoverTray) {
                hoverTray = newHoverTray;
                if (hoverTray >= 0) {
                    SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.3f, Volume = 0.28f });
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
            if (trayPageLeftHover) {
                trayPage--;
                hoverTray = -1;
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.1f, Volume = 0.35f });
                return;
            }
            if (trayPageRightHover) {
                trayPage++;
                hoverTray = -1;
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.15f, Volume = 0.35f });
                return;
            }
            if (hoverTray >= 0) {
                HandleTrayClick(hoverTray);
                return;
            }
            //点在匣木板上(非格)吞掉,勿误收扇
            if (selectedSlot >= 0 && trayRect.Contains(mp)) {
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
            //点台外收台:台账/陈列刀域(含余量)/木牌/名字列/吊挂卷轴之外
            Rectangle stand = Rectangle.Union(panelRect, exhibitRect);
            stand.Inflate(30, 40);
            Rectangle nameHit = new((int)(nameColTop.X - 46f), (int)(nameColTop.Y - 20f), 92,
                (int)(OnikiriUITheme.UIScreenH * 0.6f));
            bool trayHit = selectedSlot >= 0 && fanEase > 0.25f && trayRect.Contains(mp);
            if (!stand.Contains(mp) && !tagRect.Contains(mp) && !trayHit && !nameHit.Contains(mp)
                && !registerSwitch.Hovering) {
                Close();
            }
        }

        private void SelectSlot(int index) {
            selectedSlot = index;
            hoverRib = -1;
            hoverTray = -1;
            trayPage = 0;
            Array.Clear(ribEase, 0, ribEase.Length);
            Array.Clear(trayCellEase, 0, trayCellEase.Length);
            if (index >= 0) {
                SoundEngine.PlaySound(CWRSound.ButtonZero with { Volume = 0.5f });
                RebuildRibs();
                RebuildTray();
            }
            else {
                tray.Clear();
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.2f, Volume = 0.3f });
            }
        }

        private void RebuildRibs() {
            ribs.Clear();
            ribs.AddRange(OniMeiOwned.GetBySlotOwned(SlotOf(selectedSlot), Main.LocalPlayer));
            ribsHasErase = EngravedAt(selectedSlot) != null;
            //扩册免疫:骨数超出缓动数组时扩容(新数组自带清零)
            if (RibCount() > ribEase.Length) {
                ribEase = new float[RibCount()];
            }
        }

        private void RebuildTray() {
            tray.Clear();
            if (selectedSlot < 0) {
                return;
            }
            tray.AddRange(OniMeiTrayLogic.CollectForSlot(Main.LocalPlayer, SlotOf(selectedSlot)));
            int pages = TrayPageCount();
            if (trayPage >= pages) {
                trayPage = Math.Max(0, pages - 1);
            }
        }

        private int RibCount() => ribs.Count + (ribsHasErase ? 1 : 0);

        private int TrayPageCount() {
            if (tray.Count <= 0) {
                return 1;
            }
            return (tray.Count + OnikiriUITheme.MeiTrayMaxCols - 1) / OnikiriUITheme.MeiTrayMaxCols;
        }

        private int TrayPageStart() => trayPage * OnikiriUITheme.MeiTrayMaxCols;

        private int TrayVisibleCount() {
            if (tray.Count <= 0) {
                return 0;
            }
            return Math.Min(OnikiriUITheme.MeiTrayMaxCols, tray.Count - TrayPageStart());
        }

        private Vector2 TrayCellPos(int visibleIndex, int visibleCount) {
            float half = (visibleCount - 1) * OnikiriUITheme.MeiTrayCellGap * 0.5f;
            float x = trayOrigin.X - half + visibleIndex * OnikiriUITheme.MeiTrayCellGap;
            float lift = visibleIndex < trayCellEase.Length ? trayCellEase[visibleIndex] * 5f : 0f;
            return new Vector2(x, trayOrigin.Y - lift);
        }

        private Vector2 TrayPageDotPos(bool left) {
            int visible = Math.Max(1, TrayVisibleCount());
            float half = (visible - 1) * OnikiriUITheme.MeiTrayCellGap * 0.5f
                + OnikiriUITheme.MeiTrayGlyphSize * 0.55f;
            float x = left ? trayOrigin.X - half - 18f : trayOrigin.X + half + 18f;
            return new Vector2(x, trayOrigin.Y);
        }

        /// <summary>index 骨的展开进度；保留逐骨错拍，并保证任意骨数最终都能完全展开</summary>
        private float RibReveal(int index, int count) {
            if (count <= 1) {
                return MathHelper.Clamp(fanEase / FanRibRevealDuration, 0f, 1f);
            }
            float totalDelay = Math.Min((count - 1) * FanRibRevealDelay, FanRibMaxRevealDelay);
            float delay = index / (count - 1f) * totalDelay;
            float duration = Math.Min(FanRibRevealDuration, 1f - delay);
            return MathHelper.Clamp((fanEase - delay) / duration, 0f, 1f);
        }

        /// <summary>骨完全展开后的纹章心位置</summary>
        private Vector2 RibRestPos(int index, int count) {
            float ang = 0f;
            if (count > 1) {
                ang += (index / (count - 1f) - 0.5f) * fanLayout.Spread;
            }
            float lift = index < ribEase.Length ? ribEase[index] * 7f : 0f;
            return fanPivot + ang.ToRotationVector2() * (fanLayout.RadiusOf(index) + lift);
        }

        /// <summary>骨当前实际绘制位置；命中判定必须复用此位置</summary>
        private Vector2 RibDrawPos(int index, int count, float reveal) {
            float ease = reveal * (2f - reveal);
            return Vector2.Lerp(fanPivot, RibRestPos(index, count), ease);
        }

        /// <summary>骨 index 是否除铭骨(排在最末)</summary>
        private bool IsEraseRib(int index) => ribsHasErase && index == ribs.Count;

        private void HandleRibClick(int index) {
            OniMeiSlotKind slot = SlotOf(selectedSlot);
            string oldKey = OniMeiRegistry.DisplayStore?.Get(slot);
            int session = interactionSession;

            if (IsEraseRib(index)) {
                if (!OniMeiRegistry.EraseHeld(slot, success => CompleteMeiChange(
                    session, success, OniMeiRiteKind.Erase, slot, oldKey, null))) {
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
            OniMeiRiteKind kind = oldKey != null ? OniMeiRiteKind.Rename : OniMeiRiteKind.Engrave;
            if (!OniMeiRegistry.EngraveHeld(slot, def.Key, success => CompleteMeiChange(
                session, success, kind, slot, oldKey, def.Key))) {
                DenyFeedback();
            }
        }

        /// <summary>匣格点击:持样凿上，拓本不消耗</summary>
        private void HandleTrayClick(int visibleIndex) {
            int abs = TrayPageStart() + visibleIndex;
            if (abs < 0 || abs >= tray.Count) {
                DenyFeedback();
                return;
            }

            OniMeiTrayEntry entry = tray[abs];
            OniMeiSlotKind slot = SlotOf(selectedSlot);
            string oldKey = OniMeiRegistry.DisplayStore?.Get(slot);
            int session = interactionSession;

            if (entry.Key == oldKey) {
                SelectSlot(-1);
                return;
            }

            Player player = Main.LocalPlayer;
            if (!OniMeiTrayLogic.Has(player, entry.Key)) {
                DenyFeedback();
                RebuildTray();
                return;
            }

            OniMeiOwned.Unlock(player, entry.Key);
            OniMeiRiteKind kind = oldKey != null ? OniMeiRiteKind.Rename : OniMeiRiteKind.Engrave;
            if (!OniMeiRegistry.EngraveHeld(slot, entry.Key, success => CompleteMeiChange(
                session, success, kind, slot, oldKey, entry.Key))) {
                DenyFeedback();
                RebuildTray();
                return;
            }
        }

        /// <summary>收扇不响声(仪式开演自带音)</summary>
        private void CompleteMeiChange(int session, bool success, OniMeiRiteKind kind,
            OniMeiSlotKind slot, string oldKey, string newKey) {
            if (!IsOpen || session != interactionSession) {
                return;
            }
            if (!success) {
                if (selectedSlot >= 0) {
                    RebuildRibs();
                    RebuildTray();
                }
                DenyFeedback();
                return;
            }
            RebuildTray();
            Rite.Start(kind, slot, oldKey, newKey);
            SelectSlotSilently(-1);
        }

        private bool MaintainMouseSlot() {
            if (!keepMouseSlot) {
                return true;
            }
            if (OnikiriData.TryGet(Main.mouseItem)?.InstanceId != mouseSlotInstanceId) {
                Close();
                return false;
            }
            Main.playerInventory = true;
            return true;
        }

        private void SelectSlotSilently(int index) {
            selectedSlot = index;
            hoverRib = -1;
            hoverTray = -1;
            Array.Clear(ribEase, 0, ribEase.Length);
            Array.Clear(trayCellEase, 0, trayCellEase.Length);
            if (index < 0) {
                tray.Clear();
            }
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
            //悬停匣格:行囊錾样预览(优先于扇骨)
            if (selectedSlot >= 0 && hoverTray >= 0) {
                int abs = TrayPageStart() + hoverTray;
                if (abs >= 0 && abs < tray.Count) {
                    string key = tray[abs].Key;
                    if (OniMeiRegistry.TryGet(key, out OniMeiDefinition trayDef)) {
                        return ($"tray:{trayDef.Key}", trayDef.DisplayName.Value, SlotLabel(trayDef.SlotKind),
                            trayDef.Origin.Value, trayDef.Power.Value, trayDef.Burden.Value, trayDef.IsGoldTier, false);
                    }
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
            //选中铭位:现铭或空悬文案(匣空时短提示并入空悬出处)
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
                if (tray.Count == 0) {
                    hint = TrayEmpty.Value;
                }
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
            OniMeiRenderer.DrawCandleGlow(spriteBatch, exhibitCenter, a, ShaderTime, parallax);

            float contentA = MathHelper.Clamp((a - 0.5f) / 0.5f, 0f, 1f);
            //检分镜头里外场退后:台账/牌/线让位给特写(烙印木牌保持可读,写着正凿的铭)
            float zoomK = zoomEase * zoomEase * (3f - 2f * zoomEase);
            float chromeA = contentA * (1f - zoomK * 0.85f);

            //====台账主板(题字入题头,状态行入脚注)====
            OniMeiRenderer.DrawLedgerPanel(spriteBatch, font, panelRect, TitleText.Value,
                a * (1f - zoomK * 0.85f), a, ShaderTime);

            //====注记墨线(牌→刀身搭点)====
            if (chromeA > 0.01f) {
                DrawLeaderLines(spriteBatch, chromeA);
            }

            //====陈列刀:开屏自上落定,检分镜头绕锚缩放,微震随仪式====
            float dropEase = VaultUtils.EaseOutCubic(MathHelper.Clamp(a / 0.55f, 0f, 1f));
            Vector2 drop = new(0f, -(1f - dropEase) * 38f);
            OniMeiRenderer.DrawExhibit(spriteBatch, xformOriginPx, xformPos + drop + Rite.Shake, curScale,
                a * MathHelper.Clamp(a / 0.35f, 0f, 1f), ShaderTime, songRun);

            //====刀上锚钉与微缩刻痕(检分时随刀放大,邻位刻痕即可读)====
            if (contentA > 0.01f) {
                DrawBladeMarks(spriteBatch, contentA);
            }

            //====仪式压暗与聚光(盖过刀身,衬字形)====
            if (Rite.Dim > 0.01f) {
                spriteBatch.Draw(pixel, full, src, Color.Black * Rite.Dim);
                OniBrush.DrawBacklight(spriteBatch, RiteAnchorScreen() + Rite.Shake, 42f * curScale,
                    OnikiriUITheme.CandleWarm, Rite.Dim * 1.4f);
            }

            //====检分特写上的凿刻与工具====
            if (Rite.Active) {
                DrawRiteCarving(spriteBatch, a);
                DrawRiteTools(spriteBatch);
            }

            //====烙印木牌(仪式中保持全亮,牌上打字机写着正凿的铭)====
            if (contentA > 0.01f) {
                (_, string title, string kind, string origin, string power, string burden, bool gold, bool erase) = ResolveTag();
                if (title.Length > 0) {
                    OniMeiRenderer.DrawWoodTag(spriteBatch, font, tagRect, title, kind, origin, power,
                        burden, gold, erase, TagVisibleChars, TagBurnStrength, contentA, ShaderTime);
                }
            }

            //====铭位牌(粉笔预览/接铭盖章)====
            if (chromeA > 0.01f) {
                DrawMedallions(spriteBatch, font, chromeA);
            }

            //====接铭归线:光包沿墨线自刀流回牌位(收镜同帧可见)====
            if (receiveSlot >= 0 && receiveAnim < 0.62f) {
                OniMeiRenderer.DrawInkPacket(spriteBatch, lineStart[receiveSlot], pinScreen[receiveSlot],
                    receiveAnim / 0.62f, receiveGold, contentA);
            }

            //====鏨盘扇====
            if (chromeA > 0.01f && fanEase > 0.02f && selectedSlot >= 0) {
                DrawFan(spriteBatch, chromeA);
            }

            //====右缘刀铭大字====
            if (chromeA > 0.01f) {
                DrawNameColumn(spriteBatch, font, chromeA);
            }

            //====静物 + 台下提示 + 挂件====
            if (chromeA > 0.01f) {
                OniMeiRenderer.DrawStillLife(spriteBatch,
                    MapSprite(new Vector2(34f, 224f)) + new Vector2(-6f, 28f), chromeA, ShaderTime);
                DrawCloseHint(spriteBatch, font, chromeA);
                DrawStatusLine(spriteBatch, font, chromeA);
                OniRegisterRenderer.DrawCloseTag(spriteBatch, font, closeTagRope, chromeA, closeTagHover,
                    GlobalTimer, CloseTagText.Value);
                OniMeiRenderer.DrawHangingScroll(spriteBatch, registerSwitch, chromeA, GlobalTimer,
                    OniRegistry.InDanger);
            }

            //====錾样匣====
            if (chromeA > 0.01f && fanEase > 0.02f && selectedSlot >= 0) {
                DrawTray(spriteBatch, font, chromeA);
            }

            particles.Draw(spriteBatch, a);

            //吊挂卷轴的悬浮说明(最后画,压在一切之上)
            if (registerSwitch.HoverEase > 0.05f) {
                OniMeiRenderer.DrawSwitchHoverTag(spriteBatch, MousePosition,
                    RegisterTabText.Value, RegisterTabHint.Value, a * registerSwitch.HoverEase);
            }
        }

        /// <summary>注记墨线:三条牌→刀错拍走笔;扇横开时旁线退后让位,选中线略亮</summary>
        private void DrawLeaderLines(SpriteBatch sb, float a) {
            for (int i = 0; i < 3; i++) {
                float drawEase = MathHelper.Clamp((slotRevealTimer - 8 - i * 12) / 24f, 0f, 1f);
                float lit = Math.Max(slotHover[i], slotSelect[i]);
                float lineA = a;
                if (selectedSlot >= 0) {
                    lineA *= i == selectedSlot ? 0.55f : 0.28f;
                }
                bool gold = EngravedAt(i)?.IsGoldTier ?? false;
                OniMeiRenderer.DrawLeaderInk(sb, lineStart[i], pinScreen[i], drawEase, lit, gold,
                    lineA, ShaderTime, i);
            }
        }

        /// <summary>台账铭位牌:菱章+标签,悬停扇骨/匣格时粉笔稿投影到选中牌上(试铭)</summary>
        private void DrawMedallions(SpriteBatch sb, DynamicSpriteFont font, float a) {
            for (int i = 0; i < 3; i++) {
                OniMeiDefinition engraved = EngravedAt(i);
                float ripple = MathHelper.Clamp((slotRevealTimer - 6 - i * 13) / 34f, 0f, 1f);
                float stamp = receiveSlot == i && receiveAnim >= 0.62f && receiveAnim < 1f
                    ? (receiveAnim - 0.62f) / 0.38f
                    : 1f;
                OniMeiRenderer.DrawMedallion(sb, slotPos[i], engraved?.Key, engraved?.IsGoldTier ?? false,
                    slotHover[i], slotSelect[i], a, ShaderTime, ripple, stamp);

                //标签:牌左侧右对齐,与牌同轴
                string label = SlotLabel(SlotOf(i));
                Vector2 lSize = font.MeasureString(label) * 0.8f;
                Vector2 lPos = new(slotPos[i].X - OnikiriUITheme.MeiMedallionSize * 0.62f - 14f - lSize.X,
                    slotPos[i].Y - lSize.Y * 0.5f);
                Utils.DrawBorderString(sb, label, lPos,
                    Color.Lerp(OnikiriUITheme.TextDim, OnikiriUITheme.Paper, slotHover[i]) * (a * 0.9f), 0.8f);
            }

            //悬停扇骨:粉笔稿投影到选中牌位(试铭)
            if (selectedSlot >= 0 && hoverRib >= 0 && !IsEraseRib(hoverRib)) {
                float pulse = 0.75f + 0.25f * (float)Math.Sin(ShaderTime * 4f);
                OniMeiGlyph.DrawChalk(sb, ribs[hoverRib].Key, slotPos[selectedSlot],
                    OnikiriUITheme.MeiMedallionGlyph, a * pulse * ribEase[Math.Min(hoverRib, ribEase.Length - 1)]);
            }
            //悬停匣格:粉笔稿试铭
            if (selectedSlot >= 0 && hoverTray >= 0 && hoverRib < 0) {
                int abs = TrayPageStart() + hoverTray;
                if (abs >= 0 && abs < tray.Count) {
                    float pulse = 0.75f + 0.25f * (float)Math.Sin(ShaderTime * 4f);
                    float hov = hoverTray < trayCellEase.Length ? trayCellEase[hoverTray] : 1f;
                    OniMeiGlyph.DrawChalk(sb, tray[abs].Key, slotPos[selectedSlot],
                        OnikiriUITheme.MeiMedallionGlyph, a * pulse * hov);
                }
            }
        }

        /// <summary>
        /// 刀上锚钉+已铭微缩刻痕:刀积累着被凿过的痕,可读版本住在牌里;
        /// 检分镜头下随刀放大,邻位刻痕即可读;仪式位让位给凿刻本体
        /// </summary>
        private void DrawBladeMarks(SpriteBatch sb, float a) {
            for (int i = 0; i < 3; i++) {
                bool riteHere = Rite.Active && (int)Rite.Slot == i;
                if (riteHere) {
                    continue;
                }
                OniMeiDefinition engraved = EngravedAt(i);
                if (engraved != null) {
                    OniMeiGlyphStyle style = OniMeiGlyphStyle.Engraved(a * 0.6f, OniMeiBladeDraw.GlyphRot);
                    style.Time = ShaderTime;
                    style.Inlay = engraved.IsGoldTier ? 1f : 0f;
                    style.Accent = engraved.IsGoldTier ? OnikiriUITheme.GoldInlay : OnikiriUITheme.Bright;
                    style.Lit = 0.22f + slotHover[i] * 0.4f;
                    OniMeiGlyph.Draw(sb, engraved.Key, MapSprite(anchorPx[i]),
                        OnikiriUITheme.MeiBladeMarkPx * curScale, style);
                }
                OniMeiRenderer.DrawAnchorKnot(sb, pinScreen[i], engraved != null,
                    engraved?.IsGoldTier ?? false, slotHover[i], a * (1f - zoomEase * 0.5f), ShaderTime);
            }
        }

        /// <summary>检分特写上的凿刻本体:旧铭锉去/新铭凿现/油布抹光,全部落在缩放锚上</summary>
        private void DrawRiteCarving(SpriteBatch sb, float a) {
            Vector2 pos = RiteAnchorScreen() + Rite.Shake;
            float rot = OniMeiBladeDraw.GlyphRot;
            //旧铭锉去中
            if (Rite.OldKey != null && Rite.OldReveal > 0.01f) {
                OniMeiGlyphStyle oldStyle = OniMeiGlyphStyle.Engraved(a * Rite.OldReveal, rot);
                oldStyle.Time = ShaderTime;
                oldStyle.Lit = 0.26f;
                OniMeiGlyph.Draw(sb, Rite.OldKey, pos, OnikiriUITheme.MeiBladeMarkPx * curScale, oldStyle);
            }
            //新铭凿现中
            if (Rite.NewKey != null && Rite.NewReveal >= 0f) {
                OniMeiGlyphStyle style = new() {
                    Alpha = a,
                    Rotation = rot,
                    ChiselReveal = Rite.NewReveal,
                    Accent = Rite.GoldTier ? OnikiriUITheme.GoldInlay : OnikiriUITheme.Bright,
                    Inlay = Rite.InlayFill,
                    Lit = 0.24f + Rite.OilWipe * 0.5f * (1f - Rite.InlayFill * 0.6f),
                    Time = ShaderTime,
                };
                OniMeiGlyph.Draw(sb, Rite.NewKey, pos, RiteGlyphSize(), style);
                //油布抹过:一线软亮沿刀轴扫过字形(两端没入)
                if (Rite.OilWipe > 0.01f && Rite.OilWipe < 0.99f) {
                    float sweepX = MathHelper.Lerp(-1.2f, 1.2f, Rite.OilWipe);
                    Vector2 wipePos = pos + OniMeiBladeDraw.AxisDir * (sweepX * RiteGlyphSize() * 0.6f);
                    OniBrush.DrawSoftStreak(sb, wipePos, OniMeiBladeDraw.AxisAngle + MathHelper.PiOver2,
                        RiteGlyphSize() * 1.25f, 2.4f, OnikiriUITheme.HotWhite,
                        a * 0.40f * (float)Math.Sin(Rite.OilWipe * MathHelper.Pi), glowMul: 0.7f);
                }
            }
        }

        private void DrawFan(SpriteBatch sb, float a) {
            int count = RibCount();
            OniMeiDefinition engraved = EngravedAt(selectedSlot);
            for (int i = 0; i < count; i++) {
                //逐骨错拍展开；绘制与命中共用当前实际位置
                float vis = RibReveal(i, count);
                if (vis <= 0.01f) {
                    continue;
                }
                Vector2 pos = RibDrawPos(i, count, vis);
                float hov = i < ribEase.Length ? ribEase[i] : 0f;
                if (IsEraseRib(i)) {
                    OniMeiRenderer.DrawFanRibErase(sb, fanPivot, pos, vis, hov, a, ShaderTime,
                        fanLayout.GlyphSize);
                    continue;
                }
                OniMeiDefinition def = ribs[i];
                bool isCurrent = engraved != null && engraved.Key == def.Key;
                OniMeiRenderer.DrawFanRib(sb, fanPivot, pos, def.Key, def.IsGoldTier, isCurrent,
                    vis, hov, a, ShaderTime, fanLayout.GlyphSize);
            }
            //枢钉
            Texture2D pixel = VaultAsset.placeholder2.Value;
            sb.Draw(pixel, fanPivot, new Rectangle(0, 0, 1, 1), OnikiriUITheme.Seal * (a * fanEase),
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(4.4f), SpriteEffects.None, 0f);
        }

        private void DrawTray(SpriteBatch sb, DynamicSpriteFont font, float a) {
            float trayA = a * fanEase;
            if (trayA <= 0.01f) {
                return;
            }

            OniMeiRenderer.DrawTrayPlank(sb, trayRect, trayA, ShaderTime);

            int visible = TrayVisibleCount();
            int pages = TrayPageCount();
            OniMeiDefinition engraved = EngravedAt(selectedSlot);

            Vector2 titleSize = font.MeasureString(TrayTitle.Value) * 0.78f;
            Utils.DrawBorderString(sb, TrayTitle.Value,
                new Vector2(trayRect.Center.X - titleSize.X * 0.5f, trayRect.Y + 10f),
                OnikiriUITheme.Paper * (trayA * 0.95f), 0.78f);

            if (visible <= 0) {
                Vector2 emptySize = font.MeasureString(TrayEmpty.Value) * 0.68f;
                //窄板内折行观感:居中短提示
                Utils.DrawBorderString(sb, TrayEmpty.Value,
                    new Vector2(trayRect.Center.X - emptySize.X * 0.5f, trayOrigin.Y - emptySize.Y * 0.5f),
                    OnikiriUITheme.TextDim * (trayA * 0.9f), 0.68f);
                return;
            }

            float halfSpan = (visible - 1) * OnikiriUITheme.MeiTrayCellGap * 0.5f
                + OnikiriUITheme.MeiTrayGlyphSize * 0.55f;
            OniMeiRenderer.DrawTrayRail(sb,
                trayOrigin + new Vector2(-halfSpan, 18f),
                trayOrigin + new Vector2(halfSpan, 18f),
                trayA * 0.75f, ShaderTime, trayPage, pages);

            int start = TrayPageStart();
            for (int i = 0; i < visible; i++) {
                float vis = MathHelper.Clamp((fanEase - i * 0.06f) * 1.5f, 0f, 1f);
                if (vis <= 0.01f) {
                    continue;
                }
                OniMeiTrayEntry entry = tray[start + i];
                Vector2 pos = TrayCellPos(i, visible);
                float hov = i < trayCellEase.Length ? trayCellEase[i] : 0f;
                bool isCurrent = engraved != null && engraved.Key == entry.Key;
                OniMeiRenderer.DrawTrayCell(sb, pos, entry.Key, entry.Gold, isCurrent, entry.Stack,
                    vis, hov, a, ShaderTime);
            }

            if (pages > 1) {
                Vector2 leftDot = TrayPageDotPos(true);
                Vector2 rightDot = TrayPageDotPos(false);
                if (trayPage > 0) {
                    OniBrush.DrawSoftDot(sb, leftDot, trayPageLeftHover ? 4.2f : 3.2f,
                        OnikiriUITheme.Seal, trayA * (trayPageLeftHover ? 1f : 0.65f));
                }
                if (trayPage < pages - 1) {
                    OniBrush.DrawSoftDot(sb, rightDot, trayPageRightHover ? 4.2f : 3.2f,
                        OnikiriUITheme.Seal, trayA * (trayPageRightHover ? 1f : 0.65f));
                }
            }
        }

        private void DrawRiteTools(SpriteBatch sb) {
            Vector2 anchor = RiteAnchorScreen() + Rite.Shake;
            //锉拍:锉刀横扫
            if (Rite.OldKey != null && Rite.OldReveal > 0.01f) {
                OniMeiRenderer.DrawFileTool(sb, anchor, RiteGlyphSize(),
                    1f - Rite.OldReveal, 1f, ShaderTime);
            }
            //凿拍:鏨具压在笔锋上
            if (Rite.NewKey != null && Rite.FocusPose > 0.01f && Rite.NewReveal < 1f) {
                Vector2 tip = Rite.NewReveal >= 0f
                    ? OniMeiGlyph.GetChiselPoint(Rite.NewKey, anchor, RiteGlyphSize(), OniMeiBladeDraw.GlyphRot,
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
            Vector2 size = font.MeasureString(status) * 0.72f;
            //状态行住台账脚注带,与题头/牌列同板成一账
            Utils.DrawBorderString(sb, status,
                new Vector2(panelRect.Center.X - size.X * 0.5f,
                    panelRect.Bottom - OnikiriUITheme.MeiPanelFooterH * 0.5f - size.Y * 0.5f + 2f),
                OnikiriUITheme.TextDim * (a * 0.92f), 0.72f);
        }

        private void DrawCloseHint(SpriteBatch sb, DynamicSpriteFont font, float a) {
            string keyName = CWRKeySystem.Legend_UIControl.ToTooltipString(CWRKeySystem.Notbound.Value);
            string hint = string.Format(CloseHintFormat.Value, keyName);
            Vector2 size = font.MeasureString(hint) * 0.7f;
            //收台提示贴台账板下,一行收束
            float y = Math.Min(panelRect.Bottom + 10f, OnikiriUITheme.UIScreenH - 26f);
            Utils.DrawBorderString(sb, hint,
                new Vector2(panelRect.Center.X - size.X * 0.5f, y),
                OnikiriUITheme.TextDim * (a * 0.6f), 0.7f);
        }
    }
}
