using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniAnnihilates;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDismembers;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDomains;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFlashSteps;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniOmokages;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniSakuraFlights;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniZanshinSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI;
using CalamityOverhaul.Content.Scenarios.Himayo;
using InnoVault.PRT;
using System;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.Graphics.Capture;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend
{
    /// <summary>
    /// 鬼切资源层,气力+架势,owner 端自治不进网络/存档.
    /// 疾走键未绑定时回退右键;表世界可衔樱流;交还帧开追斩窗;
    /// <see cref="CWRKeySystem.Onikiri_Execute"/> 处决;里世界点选肢解.
    /// HUD 经 <see cref="OnikiriResourceSource"/> 只读
    /// </summary>
    internal class OnikiriPlayer : ModPlayer
    {
        //====调参常量====
        public const float VigorMax = 100f;
        /// <summary>神威疾走的气力开销</summary>
        public const float DashVigorCost = 30f;
        /// <summary>每帧自然回气(约 6/s)</summary>
        private const float VigorRegenPerTick = 0.10f;
        /// <summary>消耗后回气延迟(帧),防右键无脑连打</summary>
        private const int VigorRegenDelayTicks = 48;
        /// <summary>连段每拍首次命中回气</summary>
        private const float VigorPerComboBeat = 2f;

        public const float StanceMax = 100f;
        /// <summary>灭世一闪的架势门槛与开销</summary>
        public const float AnnihilateCost = 50f;
        /// <summary>连段每拍首次命中蓄势</summary>
        private const float StancePerComboBeat = 2.5f;
        /// <summary>一次疾走中首次成功穿身格挡的固定蓄势</summary>
        private const float StancePerDashParry = 12f;

        /// <summary>疾走墨痕伤害系数:定位是位移+格挡工具,不与连段争输出</summary>
        private const float DashDamageMul = 0.65f;
        /// <summary>居合越过光标的固定余量(px)</summary>
        private const float DashCursorOvershoot = 20f;
        /// <summary>灭世一闪伤害倍率(单次巨额结算)</summary>
        private const float AnnihilateDamageMul = 5f;
        /// <summary>冲刺基础再触发锁(帧)，长距离时会自动延长到覆盖完整位移</summary>
        private const int DashRefireLockTicks = 14;

        /// <summary>樱流化身每帧耗气(疾走衔接的持续飞行,气尽自动回卷);冲刺后余量约可飞满程(~3s)且只抽十余点气</summary>
        private const float SakuraDrainPerTick = 0.15f;
        /// <summary>樱流入飞门槛:低于此气力不衔接,疾走照常收势</summary>
        private const float SakuraMinVigor = 10f;
        /// <summary>樱流巡航速度(px/帧),模块钳制上限 48;从疾走高速骤降到此,是"化形"的减速拍</summary>
        private const float SakuraFlightSpeed = 40f;

        /// <summary>追斩资格时长(帧),交还操控后保留 1.5 秒</summary>
        private const int ZanshinWindowTicks = 90;
        /// <summary>持续左键自动衔接前的举刀交接帧</summary>
        private const int ZanshinAutoHandoffFrames = 5;
        /// <summary>冲刺期间认定为主动改向的鼠标屏幕位移(px)</summary>
        private const float ZanshinRedirectMouseDistance = 64f;
        /// <summary>追斩伤害倍率:层级卡在连段单拍与灭世一闪(5x)之间</summary>
        private const float ZanshinDamageMul = 2f;
        /// <summary>追斩每刀首次命中回架势,比连段单拍(2.5)厚,喂处决循环</summary>
        private const float StancePerZanshinSlash = 6f;
        /// <summary>锵后仍算"同帧"的宽限(帧):此窗内出刀视同与结算压拍,震屏减半</summary>
        private const int ZanshinSyncSlackTicks = 2;
        /// <summary>终结乱舞焦点距离钳制(与疾走射程同量级,演出保持在可读范围)</summary>
        private const float FinaleFocusMaxDist = 800f;
        /// <summary>终结乱舞光标磁吸半径(按精确碰撞箱距离衡量)</summary>
        private const float FinaleMagnetRadius = 200f;
        /// <summary>光标点名允许略超射程的余量:玩家明确指着谁就成全谁</summary>
        private const float FinaleCursorSlack = 260f;
        /// <summary>命中记忆容量与保鲜期(帧):近 5 秒打过谁,处决就认得谁</summary>
        private const int HitMemoryCapacity = 8;
        private const int HitMemoryLifeTicks = 300;

        /// <summary>肢解伤害倍率(终斩刀线/媒介脉冲单次结算);代价是反噬(僵直+必定伤害)而非资源</summary>
        private const float DismemberDamageMul = 2.5f;
        /// <summary>肢解射程(与处决同量级)</summary>
        private const float DismemberRange = 800f;
        /// <summary>点名真身的贴身容差(碰撞箱边距):点在身上=明确要斩真身,压过挂在它身上的纸</summary>
        private const float DirectPickPad = 16f;
        /// <summary>媒介点选的光标容差(点到纸面矩形距离)</summary>
        private const float PaperMagnetPad = 60f;

        //====铭刻效果层调参(机制常量在此,倍率在 OniMeiCombatProfile)====
        /// <summary>友切:每层「咎」的疾走额外气力(残心命中偿清)</summary>
        private const float GuiltDashVigorPerLayer = 6f;
        /// <summary>友切:咎层上限</summary>
        private const int GuiltMaxLayers = 3;
        /// <summary>不动护:每次守护消耗的架势</summary>
        private const float FudoGuardStanceCost = 20f;
        /// <summary>不动护:该次受击的伤害削减比</summary>
        private const float FudoGuardDamageCut = 0.40f;
        /// <summary>不动护:内部冷却(帧,约两秒)</summary>
        private const int FudoGuardCooldownTicks = 120;
        /// <summary>倶利伽罗:处决后点燃的龙火窗口(帧,约十秒)</summary>
        private const int KurikaraWindowTicks = 600;

        //====状态(owner 端自治)====
        internal float Vigor = VigorMax;
        internal float Stance;
        private int vigorRegenDelay;
        private int dashLock;
        private int readyCueTimer;

        //====铭刻状态(owner 端自治,禁 static)====
        /// <summary>本帧铭刻合成档(手持解析;未持刀=Identity,负担随刀离手消失)</summary>
        internal OniMeiCombatProfile Mei = OniMeiCombatProfile.Identity;
        /// <summary>友切:当前咎层数(0..<see cref="GuiltMaxLayers"/>)</summary>
        internal int GuiltLayers { get; private set; }
        /// <summary>倶利伽罗:龙火窗口余量(帧),>0 时第五拍收束回环斩</summary>
        internal int KurikaraWindow { get; private set; }
        private int fudoGuardCooldown;

        /// <summary>当前气力上限(倶利伽罗压缩至 80)</summary>
        internal float VigorMaxCurrent => VigorMax * Mei.VigorMaxMul;
        //====追斩资格(owner 端自治)====
        private int zanshinWindow;          //剩余帧数,0=关
        private int zanshinJudgeCountdown;  //距锵帧数,窗开着时持续递减(负值=锵已过)
        private bool zanshinHasMarks;       //开窗时疾走带墨痕:锵前按下走缓冲,同帧释放
        private bool zanshinPending;        //左键意图已受理,带墨痕时挂起等锵
        private bool zanshinInputBuffered;  //疾走/樱流控身期间按下左键,交还帧兑现
        private bool zanshinAutoHandoff;    //持续左键自动衔接,走短举刀与稳定方向
        private int zanshinAutoHandoffCountdown;
        private Vector2 zanshinHandoffDirection = Vector2.UnitX;
        private Vector2 zanshinBufferedMouseScreen;
        private bool prevMouseLeft;         //Shoot 路径按下沿鉴别,防资格期自动重用

        //====命中记忆:处决智能选点的第二层依据====
        private struct HitMemory
        {
            public int NpcId;
            public int NpcType;
            public int Tick;
        }
        private readonly HitMemory[] hitMemory = new HitMemory[HitMemoryCapacity];

        private static InputMode FlashStepBindingMode
            => PlayerInput.UsingGamepad ? InputMode.XBoxGamepad : InputMode.Keyboard;

        internal static bool FlashStepInputHeld {
            get {
                ModKeybind keybind = CWRKeySystem.Onikiri_FlashStep;
                return CWRKeySystem.IsKeybindUnbound(keybind, FlashStepBindingMode)
                    ? Main.mouseRight
                    : keybind.Current;
            }
        }

        public override void OnEnterWorld() {
            Vigor = VigorMax;
            Stance = 0f;
            zanshinWindow = 0;
            zanshinPending = false;
            zanshinInputBuffered = false;
            zanshinAutoHandoff = false;
            zanshinAutoHandoffCountdown = 0;
            ResetMeiTransient();
        }

        public override void OnRespawn() {
            Vigor = VigorMax;
            Stance = 0f;
            zanshinWindow = 0;
            zanshinPending = false;
            zanshinInputBuffered = false;
            zanshinAutoHandoff = false;
            zanshinAutoHandoffCountdown = 0;
            ResetMeiTransient();
        }

        private void ResetMeiTransient() {
            GuiltLayers = 0;
            KurikaraWindow = 0;
            fudoGuardCooldown = 0;
        }

        public override void PostUpdate() {
#if DEBUG
            Vigor = VigorMax;
#endif
            if (Main.dedServ || Player.whoAmI != Main.myPlayer) {
                return;
            }

            //试炼门禁硬倒计时,与招式无关,反噬僵直也推进
            HimayoStorySync.TickTrialUnlockSafety(Player);

            //铭刻档每帧从手中刀解析:换刀/改铭/收刀即时生效,负担只在手持时存在
            Mei = OniMeiCombat.ResolveHeld(Player);
            Vigor = Math.Min(Vigor, VigorMaxCurrent);

            if (vigorRegenDelay > 0) {
                vigorRegenDelay--;
            }
            else {
                Vigor = Math.Min(VigorMaxCurrent, Vigor + VigorRegenPerTick * Mei.NaturalRegenMul);
            }
            if (dashLock > 0) {
                dashLock--;
            }
            if (fudoGuardCooldown > 0) {
                fudoGuardCooldown--;
            }
            if (KurikaraWindow > 0) {
                KurikaraWindow--;
            }
            TickZanshinWindow();

            ModKeybind flashStepKey = CWRKeySystem.Onikiri_FlashStep;
            bool flashStepUnbound = CWRKeySystem.IsKeybindUnbound(flashStepKey, FlashStepBindingMode);
            bool flashStepPressed = flashStepUnbound
                ? Main.mouseRight && Main.mouseRightRelease
                : flashStepKey.JustPressed;
            //左键沿供 TryZanshinStrike 的 Shoot 路径鉴别:ItemCheck 先于 PostUpdate,
            //此处更新后,下一帧的物品使用读到的仍是"上一帧是否按着"
            prevMouseLeft = Main.mouseLeft;

            //反噬僵直期间万籁俱寂:招式与领域输入全部静默,规避疾走/翻转拆散钉死
            if (OniPlayerDismember.IsLocked(Player)) {
                return;
            }

            Item item = Player.GetItem();
            bool holding = item != null && item.Alives() && item.type == ModContent.ItemType<OnikiriItem>();
            if (holding && zanshinWindow <= 0 && Main.mouseLeft
                && (dashLock > 0 || OniSakuraFlight.ControlsOwner(Player.whoAmI))) {
                if (!zanshinInputBuffered) {
                    zanshinBufferedMouseScreen = Main.MouseScreen;
                }
                zanshinInputBuffered = true;
            }
            HandleDomainInput(holding);
            if (holding) {
                ManageSakuraFlight();
            }
            if (!holding || Player.dead || Player.CCed) {
                return;
            }
            //点鬼簿/铭刻仪式演出中不受理招式输入
            if ((OniRegisterUI.Instance?.IsOpen ?? false) || (OniEngraveRiteUI.Instance?.Active ?? false)) {
                return;
            }

            ReleaseZanshinPending(item);
            ReadyCue();

            if (flashStepPressed && CanAcceptFlashStepInput()) {
                TryDash(item);
            }
            if (CWRKeySystem.Onikiri_Execute.JustPressed) {
                TryExecute(item);
            }
        }

        //==================== 鬼域 ====================

        /// <summary>
        /// 领域键 <see cref="CWRKeySystem.Legend_Domain"/> 开阖(持刀);
        /// <see cref="CWRKeySystem.Onikiri_DomainFlip"/> 翻转,域开时不持刀也受理;
        /// 骇客时停/点鬼簿/铭刻中不受理
        /// </summary>
        private void HandleDomainInput(bool holding) {
            if (Player.dead) {
                return;
            }
            OniDomainPlayer domain = Player.GetModPlayer<OniDomainPlayer>();
            if (HackTime.Active) {
                return;
            }
            if ((OniRegisterUI.Instance?.IsOpen ?? false) || (OniEngraveRiteUI.Instance?.Active ?? false)) {
                return;
            }

            if (holding && CWRKeySystem.Legend_Domain.JustPressed) {
                if (!OniDomain.TryToggle(Player, out bool busy) && busy) {
                    OniTalismanHud.NotifyDomainDenied();
                }
            }
            //中键默认绑定:悬停在鬼眼上时 mouseInterface 为真,让位给眼的点击受理,防同帧双发
            if ((holding || domain.AnyActive) && CWRKeySystem.Onikiri_DomainFlip.JustPressed && !Player.mouseInterface) {
                if (!OniDomain.TryFlip(Player, out bool busy) && busy) {
                    OniTalismanHud.NotifyDomainDenied();
                }
            }
        }

        //==================== 神威疾走 ====================

        private bool CanAcceptFlashStepInput() {
            if (Main.mapFullscreen || Main.gamePaused || Main.ingameOptionsWindow || Main.inFancyUI
                || Main.drawingPlayerChat || Main.editSign || Main.editChest || Main.blockInput
                || Player.noItems || Player.mouseInterface || Player.talkNPC != -1 || Player.sign != -1
                || CaptureManager.Instance.Active || Player.tileInteractionHappened
                || Main.HoveringOverAnNPC || Main.SmartInteractShowingGenuine
                || CursorOverInteractiveProjectile()) {
                return false;
            }
            return true;
        }

        private bool CursorOverInteractiveProjectile() {
            Point cursor = Main.MouseWorld.ToPoint();
            foreach (int projectileIndex in Player.GetListOfProjectilesToInteractWithHack()) {
                if (projectileIndex < 0 || projectileIndex >= Main.maxProjectiles) {
                    continue;
                }
                Projectile projectile = Main.projectile[projectileIndex];
                if (projectile.active && (projectile.Hitbox.Contains(cursor)
                    || Main.SmartInteractProj == projectile.whoAmI)) {
                    return true;
                }
            }
            return false;
        }

        private void TryDash(Item item) {
            //再触发锁内静默(是节拍不是资源问题);骑乘时位移权在坐骑;樱流握有本体时不受理
            if (dashLock > 0 || Player.mount?.Active == true
                || OniSakuraFlight.ControlsOwner(Player.whoAmI)) {
                return;
            }
            //风樋减耗;友切的咎逐层加价,残心命中偿清
            float dashCost = DashVigorCost * Mei.DashVigorCostMul + GuiltLayers * GuiltDashVigorPerLayer;
            if (Vigor < dashCost - 0.01f) {
                OniTalismanHud.NotifyVigorDenied();
                return;
            }

            Vigor -= dashCost;
            vigorRegenDelay = VigorRegenDelayTicks + Mei.ExtraRegenDelayTicks;
            //新位移开始,上一窗作废
            zanshinWindow = 0;
            zanshinPending = false;
            zanshinInputBuffered = Main.mouseLeft;
            zanshinAutoHandoff = false;
            zanshinAutoHandoffCountdown = 0;

            ShootState state = Player.GetShootState();
            Vector2 aim = Main.MouseWorld - Player.Center;
            zanshinHandoffDirection = aim.SafeNormalize(Vector2.UnitX * Player.direction);
            if (zanshinInputBuffered) {
                zanshinBufferedMouseScreen = Main.MouseScreen;
            }
            float distance = aim.Length() + DashCursorOvershoot;
            float interruptRotation = 0f;
            CrimsonRendSlash combo = CrimsonRendSlash.FindController(Player);
            bool interruptCombo = combo != null
                && combo.BeginFlashStepInterrupt(aim, out interruptRotation);
            dashLock = Math.Max(DashRefireLockTicks
                , OniFlashStep.CalculateControlFrames(distance, interruptCombo));
            OniFlashStep.Fire(Player, aim, (int)(state.WeaponDamage * DashDamageMul * Mei.FlashMarkDamageMul)
                , state.WeaponKnockback, distance, interruptCombo: interruptCombo
                , interruptRotation: interruptRotation, source: Player.GetSource_ItemUse(item));
        }

        //==================== 樱流化身 ====================

        /// <summary>
        /// 疾走衔樱流,<see cref="OniFlashStep"/> 停止帧(owner);
        /// 需表世界+最低气力,失败静默
        /// </summary>
        internal bool TryChainSakuraFlight(Vector2 direction, IEntitySource source) {
            if (Player.whoAmI != Main.myPlayer || Player.mount?.Active == true) {
                return false;
            }
            //上一次飞行的控制器(含余晖期)未消亡则拒绝:模块每玩家仅一个,拿旧实例不算衔接成功
            if (OniSakuraFlight.AnyFor(Player.whoAmI)) {
                return false;
            }
            OniDomainPlayer domain = Player.GetModPlayer<OniDomainPlayer>();
            if (domain.Phase != OniDomainPhase.Omote || domain.WorldIsUra) {
                return false;
            }
            if (Vigor < SakuraMinVigor - 0.01f) {
                return false;
            }
            int flightFrames = (int)(Vigor / (SakuraDrainPerTick * Mei.SakuraDrainMul));
            if (OniSakuraFlight.Fire(Player, direction, SakuraFlightSpeed,
                flightFrames, source, seamless: true) == null) {
                return false;
            }
            //化樱起飞,疾走的旧窗作废;落地(ReleaseOwner)会开新窗
            zanshinWindow = 0;
            zanshinPending = false;
            zanshinAutoHandoff = false;
            zanshinAutoHandoffCountdown = 0;
            return true;
        }

        /// <summary>
        /// 樱流飞行的经济与手势(owner 端,每帧):逐帧抽气并压住回气延迟;
        /// 松手、气尽或领域离开表世界稳态均发出回卷,重组收尾由模块自理
        /// </summary>
        private void ManageSakuraFlight() {
            if (!OniSakuraFlight.IsTraveling(Player.whoAmI)) {
                return;
            }
            vigorRegenDelay = Math.Max(vigorRegenDelay, VigorRegenDelayTicks + Mei.ExtraRegenDelayTicks);
            OniDomainPlayer domain = Player.GetModPlayer<OniDomainPlayer>();
            if (!FlashStepInputHeld || Vigor <= 0.01f
                || domain.Phase != OniDomainPhase.Omote || domain.WorldIsUra) {
                OniSakuraFlight.RequestStop(Player);
                return;
            }
            Vigor = Math.Max(0f, Vigor - SakuraDrainPerTick * Mei.SakuraDrainMul);
        }

        //==================== 残心追斩 ====================

        /// <summary>交还帧开追斩资格(owner),控身期间的左键在此转为挂起</summary>
        internal void OpenZanshinWindow(int judgeDelay, int markCount, Vector2 handoffDirection) {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            bool bufferedInput = zanshinInputBuffered || Main.mouseLeft;
            if (handoffDirection.LengthSquared() > 0.01f) {
                zanshinHandoffDirection = handoffDirection.SafeNormalize(Vector2.UnitX * Player.direction);
            }
            if (bufferedInput && !zanshinInputBuffered) {
                zanshinBufferedMouseScreen = Main.MouseScreen;
            }
            zanshinWindow = ZanshinWindowTicks;
            zanshinJudgeCountdown = Math.Max(judgeDelay, 0);
            zanshinHasMarks = markCount > 0;
            zanshinPending = bufferedInput;
            zanshinAutoHandoff = bufferedInput;
            zanshinAutoHandoffCountdown = bufferedInput ? ZanshinAutoHandoffFrames + 1 : 0;
            zanshinInputBuffered = false;
        }

        internal bool ZanshinAutoHandoffActive
            => zanshinWindow > 0 && zanshinPending && zanshinAutoHandoff;

        internal float ZanshinAutoHandoffProgress => !ZanshinAutoHandoffActive ? 0f
            : 1f - MathHelper.Clamp((zanshinAutoHandoffCountdown - 1f)
                / ZanshinAutoHandoffFrames, 0f, 1f);

        /// <summary>追斩窗每帧推进:锵倒计时递减(负值=锵已过),窗口过期清挂起</summary>
        private void TickZanshinWindow() {
            if (zanshinWindow <= 0) {
                return;
            }
            zanshinWindow--;
            zanshinJudgeCountdown--;
            if (zanshinAutoHandoff && zanshinAutoHandoffCountdown > 0) {
                zanshinAutoHandoffCountdown--;
            }
            if (zanshinWindow <= 0) {
                zanshinPending = false;
                zanshinInputBuffered = false;
                zanshinAutoHandoff = false;
                zanshinAutoHandoffCountdown = 0;
            }
        }

        /// <summary>
        /// 追斩按下沿;<see cref="OnikiriItem.Shoot"/> 传 edgeVerified=false,
        /// <see cref="CrimsonRendSlash"/> 传 true;false 则回退连段
        /// </summary>
        internal bool TryZanshinStrike(Item item, bool edgeVerified) {
            if (Player.whoAmI != Main.myPlayer || zanshinWindow <= 0) {
                return false;
            }
            if (!edgeVerified && (!Main.mouseLeft || prevMouseLeft)) {
                return false;
            }
            if (zanshinPending) {
                //已挂起等锵,窗内重复点击吸收,不落回连段
                return true;
            }
            //硬占刀权的演出(灭世大挥/终结乱舞开场等)期间不抢出手,落回连段的既有让位缓冲;
            //上一刀残心斩可并存,硬占/弹幕计数都不挡连居合第二刀
            if (Player.mount?.Active == true || OniPlayerDismember.IsLocked(Player)
                || OniSakuraFlight.ControlsOwner(Player.whoAmI)
                || OniBladeOccupancy.AnyHardOccupant(Player
                    , ignoreType: ModContent.ProjectileType<OniZanshinSlash>())) {
                return false;
            }

            zanshinAutoHandoff = false;
            zanshinAutoHandoffCountdown = 0;
            if (zanshinHasMarks && zanshinJudgeCountdown > 0) {
                zanshinPending = true;
                return true;
            }
            return FireZanshin(item);
        }

        /// <summary>挂起的追斩在交还后释放,带墨痕则押到锵帧;
        /// 等锵期间玩家另起大招/化樱则弃挂起,大动作优先;旧残心斩硬占不弃窗</summary>
        private void ReleaseZanshinPending(Item item) {
            if (!zanshinPending) {
                return;
            }
            if (OniBladeOccupancy.AnyHardOccupant(Player
                    , ignoreType: ModContent.ProjectileType<OniZanshinSlash>())
                || OniSakuraFlight.ControlsOwner(Player.whoAmI)) {
                zanshinPending = false;
                zanshinWindow = 0;
                zanshinAutoHandoff = false;
                zanshinAutoHandoffCountdown = 0;
                return;
            }
            if (zanshinAutoHandoff && zanshinAutoHandoffCountdown > 0) {
                return;
            }
            if (!zanshinHasMarks || zanshinJudgeCountdown <= 0) {
                FireZanshin(item);
            }
        }

        /// <summary>追斩出刀:瞄准角与领域变体(表世界=樱衣)都在释放帧采样,锵同帧(含宽限)震屏减半</summary>
        private bool FireZanshin(Item item) {
            Vector2 aim = ResolveZanshinAim();
            zanshinWindow = 0;
            zanshinPending = false;
            zanshinInputBuffered = false;
            zanshinAutoHandoff = false;
            zanshinAutoHandoffCountdown = 0;
            ShootState state = Player.GetShootState();
            OniDomainPlayer domain = Player.GetModPlayer<OniDomainPlayer>();
            bool sakura = domain.Phase == OniDomainPhase.Omote && !domain.WorldIsUra;
            bool synced = zanshinHasMarks && zanshinJudgeCountdown <= 0
                && zanshinJudgeCountdown >= -ZanshinSyncSlackTicks;
            Projectile zanshin = OniZanshinSlash.Fire(Player, aim
                , (int)(state.WeaponDamage * ZanshinDamageMul), state.WeaponKnockback
                , sakura, synced, Player.GetSource_ItemUse(item));
            if (zanshin == null) {
                return false;
            }
            CrimsonRendSlash.FindController(Player)?.ConsumeZanshinInput();
            return true;
        }

        private Vector2 ResolveZanshinAim() {
            Vector2 fallback = zanshinHandoffDirection.SafeNormalize(Vector2.UnitX * Player.direction);
            Vector2 liveAim = Main.MouseWorld - Player.Center;
            bool deliberateRedirect = Vector2.DistanceSquared(Main.MouseScreen, zanshinBufferedMouseScreen)
                >= ZanshinRedirectMouseDistance * ZanshinRedirectMouseDistance;
            if (zanshinAutoHandoff && !deliberateRedirect) {
                return fallback;
            }
            return liveAim.SafeNormalize(fallback);
        }

        /// <summary>追斩接触:每刀仅首次命中回架势,所有目标都记入命中记忆;血樋补气,咎在此偿清</summary>
        internal void OnZanshinHit(NPC target, bool grantResources) {
            RecordHit(target);
            if (GuiltLayers > 0) {
                GuiltLayers = 0;
            }
            if (!grantResources) {
                return;
            }
            Stance = Math.Min(StanceMax, Stance + StancePerZanshinSlash * Mei.StanceGainMul);
            if (Mei.ZanshinHitVigorBonus > 0f) {
                Vigor = Math.Min(VigorMaxCurrent, Vigor + Mei.ZanshinHitVigorBonus);
                if (Mei.BloodGroove) {
                    OniMeiStrikes.SpawnBloodBackflow(Player, target);
                }
            }
        }

        //==================== 处决 ====================

        private void TryExecute(Item item) {
            //演出进行中静默忽略:满屏刀光本身就是"正在忙"的答复;化樱期间人不在,刀也不在
            if (Player.ownedProjectileCounts[ModContent.ProjectileType<OniFinaleSlash>()] > 0
                || Player.ownedProjectileCounts[ModContent.ProjectileType<OniAnnihilate>()] > 0
                || OniSakuraFlight.ControlsOwner(Player.whoAmI)) {
                return;
            }

            ShootState state = Player.GetShootState();
            if (Stance >= StanceMax - 0.01f) {
                //蓄满:终结乱舞,焦点=光标定区域+小半径磁吸
                Stance = 0f;
                Vector2 focus = ComputeFinaleFocus(out Vector2 aim);
                OniFinaleSlash.Fire(Player, focus, aim, state.WeaponDamage
                    , state.WeaponKnockback, scale: OnikiriOverride.GetFinaleScale(item)
                    , source: Player.GetSource_ItemUse(item));
                IgniteKurikara();
            }
            else if (Stance >= AnnihilateCost - 0.01f) {
                //过半,灭世一闪,尺寸恒 1.0
                Stance -= AnnihilateCost;
                Vector2 aim = Main.MouseWorld - Player.Center;
                OniAnnihilate.Fire(Player, Player.Center, aim, (int)(state.WeaponDamage * AnnihilateDamageMul)
                    , state.WeaponKnockback, source: Player.GetSource_ItemUse(item));
                IgniteKurikara();
            }
            else {
                OniTalismanHud.NotifyStanceDenied();
            }
        }

        /// <summary>倶利伽罗:处决消费架势后点燃雕纹,窗口内完成五段连斩即回环</summary>
        private void IgniteKurikara() {
            if (!Mei.DragonfireLoop) {
                return;
            }
            KurikaraWindow = KurikaraWindowTicks;
            OniMeiStrikes.SpawnKurikaraIgnite(Player);
        }

        //==================== 肢解 ====================

        /// <summary>
        /// 里世界肢解点选(按下沿),真身或媒介;
        /// 成功落刀后 <see cref="OniPlayerDismember"/> 反噬;miss 回退连段;owner 端
        /// </summary>
        internal bool TryClickDismember(Item item) {
            if (Player.whoAmI != Main.myPlayer) {
                return false;
            }
            //演出或反噬僵直中不受理:裂成两半的人拔不了刀
            if (Player.ownedProjectileCounts[ModContent.ProjectileType<OniSeverStrike>()] > 0
                || OniPlayerDismember.IsLocked(Player)) {
                return false;
            }
            //肢解只在里世界成立;表世界左键就是普攻
            OniDomainPlayer domain = Player.GetModPlayer<OniDomainPlayer>();
            if (domain.Phase != OniDomainPhase.Ura || !domain.WorldIsUra) {
                return false;
            }

            ShootState state = Player.GetShootState();
            int damage = (int)(state.WeaponDamage * DismemberDamageMul);
            Vector2 mouse = Main.MouseWorld;

            //一层:点在真身碰撞箱上 → 直接肢解,反噬上身
            NPC target = PickDismemberTarget(mouse, DirectPickPad);
            if (target != null) {
                OniSeverStrike.Fire(Player, target, AimAngleFrom(target.Center), damage
                    , state.WeaponKnockback, scale: OnikiriOverride.GetBladeScale(item)
                    , source: Player.GetSource_ItemUse(item));
                return true;
            }

            //二层:点在媒介纸面上 → 点锚斩纸(落刀成功同样反噬上身)
            OmokageEntry paper = OniOmokage.PickEntryNear(mouse, PaperMagnetPad);
            if (paper != null && Vector2.Distance(Player.Center, paper.AnchorCenter) <= DismemberRange) {
                //落刀点收拢进纸面有效范围,拔刀方向=玩家→落刀点
                Vector2 local = mouse - paper.AnchorCenter;
                local.X = MathHelper.Clamp(local.X, -paper.PaperHalf.X * 0.4f, paper.PaperHalf.X * 0.4f);
                local.Y = MathHelper.Clamp(local.Y, -paper.PaperHalf.Y * 0.4f, paper.PaperHalf.Y * 0.4f);
                Vector2 cutPoint = paper.AnchorCenter + local;
                OniSeverStrike.FireAtPoint(Player, cutPoint, AimAngleFrom(cutPoint), damage
                    , state.WeaponKnockback, scale: OnikiriOverride.GetBladeScale(item)
                    , source: Player.GetSource_ItemUse(item));
                return true;
            }

            return false;
        }

        /// <summary>拔刀方向:玩家→落点;重合时退回鼠标方向,再退回朝向</summary>
        private float AimAngleFrom(Vector2 point) {
            Vector2 aim = point - Player.Center;
            if (aim.LengthSquared() < 1f) {
                aim = Main.MouseWorld - Player.Center;
            }
            if (aim.LengthSquared() < 1f) {
                aim = Vector2.UnitX * Player.direction;
            }
            return aim.ToRotation();
        }

        /// <summary>肢解目标点选,蠕虫节段排除(头部可)</summary>
        private NPC PickDismemberTarget(Vector2 cursor, float pad) {
            NPC best = null;
            bool bestBoss = false;
            float bestLife = 0f;
            float bestD = float.MaxValue;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy() || CWRLoad.WormBodys.Contains(npc.type)) {
                    continue;
                }
                float d = DistanceToHitbox(npc, cursor);
                if (d > pad) {
                    continue;
                }
                if (Vector2.Distance(Player.Center, npc.Center) > DismemberRange) {
                    continue;
                }
                NPC root = RootOf(npc);
                bool better = best == null
                    || (root.boss != bestBoss
                        ? root.boss
                        : Math.Abs(root.lifeMax - bestLife) > 1f ? root.lifeMax > bestLife : d < bestD);
                if (better) {
                    best = npc;
                    bestBoss = root.boss;
                    bestLife = root.lifeMax;
                    bestD = d;
                }
            }
            return best;
        }

        /// <summary>
        /// 终结乱舞焦点级联,光标直选→命中记忆→在场 boss→光标钳射程
        /// </summary>
        private Vector2 ComputeFinaleFocus(out Vector2 aim) {
            Vector2 mouse = Main.MouseWorld;
            NPC picked = PickAtCursor(mouse) ?? PickFromHitMemory() ?? PickBossInRange(mouse);

            Vector2 focus;
            if (picked != null) {
                focus = picked.Center;
            }
            else {
                focus = mouse;
                Vector2 toMouse = focus - Player.Center;
                float dist = toMouse.Length();
                if (dist > FinaleFocusMaxDist) {
                    focus = Player.Center + toMouse * (FinaleFocusMaxDist / dist);
                }
            }

            aim = focus - Player.Center;
            if (aim.LengthSquared() < 1f) {
                aim = mouse - Player.Center;
            }
            if (aim.LengthSquared() < 1f) {
                aim = Vector2.UnitX * Player.direction;
            }
            return focus;
        }

        /// <summary>光标直选最要紧者,可略超射程(<see cref="FinaleCursorSlack"/>)</summary>
        private NPC PickAtCursor(Vector2 cursor) {
            NPC best = null;
            bool bestBoss = false;
            float bestLife = 0f;
            float bestD = float.MaxValue;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy()) {
                    continue;
                }
                float d = DistanceToHitbox(npc, cursor);
                if (d > FinaleMagnetRadius) {
                    continue;
                }
                if (Vector2.Distance(Player.Center, npc.Center) > FinaleFocusMaxDist + FinaleCursorSlack) {
                    continue;
                }
                NPC root = RootOf(npc);
                bool better = best == null
                    || (root.boss != bestBoss
                        ? root.boss
                        : Math.Abs(root.lifeMax - bestLife) > 1f ? root.lifeMax > bestLife : d < bestD);
                if (better) {
                    best = npc;
                    bestBoss = root.boss;
                    bestLife = root.lifeMax;
                    bestD = d;
                }
            }
            return best;
        }

        /// <summary>命中记忆,近 5 秒,优先 boss</summary>
        private NPC PickFromHitMemory() {
            int now = (int)Main.GameUpdateCount;
            NPC best = null;
            bool bestBoss = false;
            int bestTick = int.MinValue;
            for (int i = 0; i < hitMemory.Length; i++) {
                ref HitMemory mem = ref hitMemory[i];
                if (mem.Tick <= 0 || now - mem.Tick > HitMemoryLifeTicks
                    || mem.NpcId < 0 || mem.NpcId >= Main.maxNPCs) {
                    continue;
                }
                NPC npc = Main.npc[mem.NpcId];
                //槽位可能已被新生的别的 NPC 复用,校验类型防串号
                if (!npc.active || npc.type != mem.NpcType || !npc.CanBeChasedBy()) {
                    continue;
                }
                if (Vector2.Distance(Player.Center, npc.Center) > FinaleFocusMaxDist) {
                    continue;
                }
                NPC root = RootOf(npc);
                bool better = best == null || (root.boss != bestBoss ? root.boss : mem.Tick > bestTick);
                if (better) {
                    best = npc;
                    bestBoss = root.boss;
                    bestTick = mem.Tick;
                }
            }
            return best;
        }

        /// <summary>在场 boss 兜底,取离光标最近</summary>
        private NPC PickBossInRange(Vector2 cursor) {
            NPC best = null;
            float bestD = float.MaxValue;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy() || !RootOf(npc).boss) {
                    continue;
                }
                if (Vector2.Distance(Player.Center, npc.Center) > FinaleFocusMaxDist) {
                    continue;
                }
                float d = Vector2.Distance(cursor, npc.Center);
                if (d < bestD) {
                    bestD = d;
                    best = npc;
                }
            }
            return best;
        }

        /// <summary>点到碰撞箱的精确距离(大体型 boss 的箱边也吸得住,不吃"中心太远"的亏)</summary>
        private static float DistanceToHitbox(NPC npc, Vector2 point) {
            Rectangle box = npc.Hitbox;
            Vector2 nearest = new(MathHelper.Clamp(point.X, box.Left, box.Right),
                MathHelper.Clamp(point.Y, box.Top, box.Bottom));
            return Vector2.Distance(point, nearest);
        }

        /// <summary>蠕虫类归主体(boss 旗/最大生命都看头)</summary>
        private static NPC RootOf(NPC npc)
            => npc.realLife >= 0 && npc.realLife < Main.maxNPCs ? Main.npc[npc.realLife] : npc;

        /// <summary>记入命中记忆:蠕虫归主体,去重刷新,满则顶掉最旧</summary>
        private void RecordHit(NPC npc) {
            if (npc == null) {
                return;
            }
            npc = RootOf(npc);
            if (!npc.active) {
                return;
            }
            int now = (int)Main.GameUpdateCount;
            int slot = -1;
            for (int i = 0; i < hitMemory.Length; i++) {
                if (hitMemory[i].NpcId == npc.whoAmI && hitMemory[i].NpcType == npc.type) {
                    slot = i;
                    break;
                }
            }
            if (slot < 0) {
                slot = 0;
                for (int i = 1; i < hitMemory.Length; i++) {
                    if (hitMemory[i].Tick < hitMemory[slot].Tick) {
                        slot = i;
                    }
                }
            }
            hitMemory[slot] = new HitMemory { NpcId = npc.whoAmI, NpcType = npc.type, Tick = now };
        }

        //==================== 资源增益(玩法挂点调用,owner 端) ====================

        /// <summary>连段接触:每拍仅首次命中回气蓄势,所有目标都记入命中记忆;血樋在此补气(禁多目标套利)</summary>
        internal void OnComboHit(NPC target, bool grantResources) {
            RecordHit(target);
            if (!grantResources) {
                return;
            }
            Vigor = Math.Min(VigorMaxCurrent, Vigor + VigorPerComboBeat + Mei.ComboHitVigorBonus);
            Stance = Math.Min(StanceMax, Stance + StancePerComboBeat * Mei.StanceGainMul);
            if (Mei.BloodGroove && Mei.ComboHitVigorBonus > 0f) {
                OniMeiStrikes.SpawnBloodBackflow(Player, target);
            }
        }

        /// <summary>疾走穿身即格挡:每次疾走仅首次格挡固定蓄势,所有目标都记入命中记忆</summary>
        internal void OnDashParry(NPC npc, bool grantResources) {
            RecordHit(npc);
            if (grantResources) {
                Stance = Math.Min(StanceMax, Stance + StancePerDashParry * Mei.StanceGainMul);
            }
        }

        //==================== 铭刻效果层挂点(owner 端) ====================

        /// <summary>友切咎影已留下:积一层咎,下一次疾走更贵;残心命中偿清</summary>
        internal void OnGuiltEchoSpawned() {
            GuiltLayers = Math.Min(GuiltLayers + 1, GuiltMaxLayers);
        }

        /// <summary>倶利伽罗:第五拍尝试收束龙火(窗口内仅一次)</summary>
        internal bool TryConsumeKurikara() {
            if (KurikaraWindow <= 0) {
                return false;
            }
            KurikaraWindow = 0;
            return true;
        }

        /// <summary>髭切断首击杀返势(每次招式至多一次,OniMeiCombat 把关)</summary>
        internal void GrantExecuteRefund() {
            Stance = Math.Min(StanceMax, Stance + OniMeiCombat.ExecuteKillStanceRefund);
        }

        /// <summary>不动护窗口:连段后两重拍/残心/处决演出中</summary>
        private bool IsInCommittedAction() {
            if (Player.ownedProjectileCounts[ModContent.ProjectileType<OniZanshinSlash>()] > 0
                || Player.ownedProjectileCounts[ModContent.ProjectileType<OniAnnihilate>()] > 0
                || Player.ownedProjectileCounts[ModContent.ProjectileType<OniFinaleSlash>()] > 0) {
                return true;
            }
            return CrimsonRendSlash.FindController(Player)?.InCommittedBeats ?? false;
        }

        /// <summary>
        /// 铭刻承伤挂点:友切+10%,不动护耗架势削减该击并免击退;
        /// 肢解反噬是固定契约,增减一律不碰
        /// </summary>
        public override void ModifyHurt(ref Player.HurtModifiers modifiers) {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer
                || OniPlayerDismember.SelfHurtResolving) {
                return;
            }
            if (Math.Abs(Mei.IncomingDamageMul - 1f) > 0.001f) {
                modifiers.FinalDamage *= Mei.IncomingDamageMul;
            }
            if (Mei.StanceGuard && fudoGuardCooldown <= 0
                && Stance >= FudoGuardStanceCost - 0.01f
                && !OniPlayerDismember.IsLocked(Player)
                && IsInCommittedAction()) {
                Stance -= FudoGuardStanceCost;
                fudoGuardCooldown = FudoGuardCooldownTicks;
                modifiers.FinalDamage *= 1f - FudoGuardDamageCut;
                modifiers.Knockback *= 0f;
                OniMeiStrikes.SpawnFudoGuard(Player);
                OniTalismanHud.NotifyStanceGuard();
            }
        }

        /// <summary>满架势身周绯焰提示</summary>
        private void ReadyCue() {
            if (Stance < StanceMax - 0.01f) {
                readyCueTimer = 0;
                return;
            }
            if (++readyCueTimer < 26) {
                return;
            }
            readyCueTimer = 0;
            Vector2 pos = Player.Center + new Vector2(Main.rand.NextFloat(-16f, 16f), Main.rand.NextFloat(-8f, 20f));
            PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, -Vector2.UnitY * Main.rand.NextFloat(0.7f, 1.5f)
                , new Color(255, 96, 58), Main.rand.NextFloat(0.2f, 0.32f))
                ?.Configure(Main.rand.Next(20, 32), affectedByGravity: false);
        }
    }

    /// <summary>把 <see cref="OnikiriPlayer"/> 的数值接给 HUD 的数据入口(只读)</summary>
    internal sealed class OnikiriResourceSource : IOniVigorSource, IOniStanceSource
    {
        public bool TryGetVigor(Player player, out OniVigorSnapshot snapshot) {
            if (player != null && player.active && player.TryGetModPlayer(out OnikiriPlayer okp)) {
                //上限占比随铭刻变化(倶利伽罗 0.8):墨脉据此留焦黑断口,读数显示真实上限
                snapshot = new OniVigorSnapshot(okp.Vigor, okp.VigorMaxCurrent
                    , okp.VigorMaxCurrent / OnikiriPlayer.VigorMax);
                return true;
            }
            snapshot = default;
            return false;
        }

        public bool TryGetStance(Player player, out OniStanceSnapshot snapshot) {
            if (player != null && player.active && player.TryGetModPlayer(out OnikiriPlayer okp)) {
                snapshot = new OniStanceSnapshot(okp.Stance, OnikiriPlayer.StanceMax);
                return true;
            }
            snapshot = default;
            return false;
        }
    }

    /// <summary>装载期把真实数据源挂进 HUD 入口,演示源退休;卸载时退回</summary>
    internal sealed class OnikiriResourceLoader : ICWRLoader
    {
        void ICWRLoader.SetupData() {
            OnikiriResourceSource source = new();
            OniVigor.SetSource(source);
            OniStance.SetSource(source);
        }

        void ICWRLoader.UnLoadData() {
            OniVigor.SetSource(null);
            OniStance.SetSource(null);
        }
    }
}
