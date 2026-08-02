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
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.Graphics.Capture;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend
{
    /// <summary>
    /// 鬼切资源层,气力+架势 owner 端自治不进网络/存档;
    /// 所持铭库(<see cref="OwnedMeiKeys"/>)例外,跟玩家存档.
    /// 疾走键未绑定时回退右键;表世界可衔樱流;交还帧开追斩窗;
    /// 架势过半时可短窗双疾走处决,满势穿身乱舞,否则左键灭世;
    /// 里世界左键点选肢解.
    /// HUD 经 <see cref="OnikiriResourceSource"/> 只读
    /// </summary>
    internal class OnikiriPlayer : ModPlayer
    {
#if DEBUG
        /// <summary>Debug 矩阵测试架势；负值保持原有自动满势行为</summary>
        internal static float DebugStanceOverride = -1f;
#endif
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
        /// <summary>普通疾走结束后的处决连携资格(缩放帧,约一秒)</summary>
        private const int ExecutionChainWindowTicks = 60;
        /// <summary>处决疾走结束后的灭世一闪输入窗(缩放帧)</summary>
        private const int ExecutionAnnihilateWindowTicks = 90;
        /// <summary>灭世一闪预输入沿用残心的短举刀交接</summary>
        private const int ExecutionAnnihilateHandoffFrames = ZanshinAutoHandoffFrames;
        /// <summary>专用处决的智能锁敌与空放最远距离</summary>
        private const float ExecutionFocusMaxDistance = 800f;
        /// <summary>光标点名目标的碰撞箱磁吸半径</summary>
        private const float ExecutionCursorMagnetRadius = 200f;
        /// <summary>明确点名时允许目标中心略超锁敌距离</summary>
        private const float ExecutionCursorRangeSlack = 260f;
        /// <summary>专用处决落点越过目标碰撞箱投影边缘的余量</summary>
        private const float ExecutionTargetOvershoot = 32f;
        /// <summary>专用处决对移动目标的一次性直线预判上限</summary>
        private const float ExecutionPredictionMax = 96f;
        /// <summary>命中记忆容量与保鲜期(帧):近 5 秒打过谁,供脱战与铭刻判定</summary>
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
        /// <summary>时间齿轮的离散帧余量；所有鬼切自有计时共用同一逻辑时钟</summary>
        private float timeAdvanceCarry;
        /// <summary>受 <see cref="TimeGear"/> 缩放的本地时间戳，供命中记忆与脱战窗口使用</summary>
        private int scaledTime = 1;

        //====铭刻状态(owner 端自治,禁 static)====
        /// <summary>本帧铭刻合成档(手持解析;未持刀=Identity,负担随刀离手消失)</summary>
        internal OniMeiCombatProfile Mei = OniMeiCombatProfile.Identity;
        /// <summary>友切:当前咎层数(0..<see cref="GuiltMaxLayers"/>)</summary>
        internal int GuiltLayers { get; private set; }
        /// <summary>倶利伽罗:龙火窗口余量(帧),>0 时第五拍收束回环斩</summary>
        internal int KurikaraWindow { get; private set; }
        private int fudoGuardCooldown;
        /// <summary>默切：疾走结束后默杀窗余量(帧)</summary>
        private int silentKillWindow;
        /// <summary>止足：低位移累计帧</summary>
        private int plantedCharge;
        /// <summary>止足：立定就绪，待残心/灭世/第五拍消费</summary>
        private bool plantedReady;
        /// <summary>止足：受击后不清充的宽容余量</summary>
        private int plantedKnockbackGrace;
        /// <summary>剪落：连环门闩</summary>
        private int petalPruneCooldown;
        /// <summary>潮拍：潮汐相位</summary>
        private int tidePhase;
        /// <summary>空鸣：威压计时</summary>
        private int hollowRoarTimer;
        /// <summary>空鸣：无近敌累计</summary>
        private int hollowAwayTicks;
        /// <summary>空鸣：远离后再近一刀武装</summary>
        private bool hollowApproachArmed;
        /// <summary>空鸣：失焦窗内授权命中计数</summary>
        private int hollowDenseHits;
        /// <summary>空鸣：失焦统计窗起点</summary>
        private int hollowDenseWindowStart;
        /// <summary>空鸣：失焦生效余量</summary>
        private int hollowFocusLossTicks;
        /// <summary>假身：影破真空余量(帧)</summary>
        private int falseBodyVacuumTicks;

        /// <summary>所持铭 Key 集合(改铭台扇骨门闩);种子含鬼切</summary>
        internal HashSet<string> OwnedMeiKeys = [];

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

        //====处决疾走(owner 端自治)====
        private enum ExecutionTier : byte
        {
            None,
            Half,
            Full,
        }

        internal enum ExecutionTriggerSource : byte
        {
            None,
            ManualChain,
            ExecuteKey,
        }

        private int executionChainWindow;
        private bool normalDashInFlight;
        private bool executionDashQueued;
        private ExecutionTier queuedExecutionTier;
        private ExecutionTriggerSource queuedExecutionSource;
        private ExecutionTier executionTierInFlight;
        private ExecutionTriggerSource executionSourceInFlight;
        /// <summary>按键时记录的相对方向与距离；发射时从玩家实际位置重构</summary>
        private Vector2 queuedExecutionAim;
        private int queuedExecutionTargetId = -1;
        private int queuedExecutionTargetType = -1;
        private int executionTargetId = -1;
        private int executionTargetType = -1;
        private int executionPreviewTargetId = -1;
        private int executionAnnihilateWindow;
        private bool executionAnnihilatePending;
        private int executionAnnihilateHandoffCountdown;
        private Vector2 executionHandoffDirection = Vector2.UnitX;
        private Vector2 executionBufferedMouseScreen;

        //====命中记忆:脱战与铭刻判定====
        private struct HitMemory
        {
            public int NpcId;
            public int NpcType;
            public int Tick;
        }
        private readonly HitMemory[] hitMemory = new HitMemory[HitMemoryCapacity];

        private static InputMode FlashStepBindingMode
            => PlayerInput.UsingGamepad ? InputMode.XBoxGamepad : InputMode.Keyboard;

        internal static bool SakuraFlightInputHeld => CWRKeySystem.Onikiri_SakuraFlight?.Current == true;

        internal bool ExecutionDashQueued => executionDashQueued;
        internal bool ManualChainExecutionInFlight => executionSourceInFlight == ExecutionTriggerSource.ManualChain;
        internal bool ExecuteKeyExecutionInFlight => executionSourceInFlight == ExecutionTriggerSource.ExecuteKey;
        internal int ExecutionLockedTargetId => executionTargetId;
        internal int ExecutionLockedTargetType => executionTargetType;
        internal int ExecutionPreviewTargetId => executionPreviewTargetId;

        public override void OnEnterWorld() {
            Vigor = VigorMax;
            Stance = 0f;
            timeAdvanceCarry = 0f;
            scaledTime = 1;
            Array.Clear(hitMemory, 0, hitMemory.Length);
            zanshinWindow = 0;
            zanshinPending = false;
            zanshinInputBuffered = false;
            zanshinAutoHandoff = false;
            zanshinAutoHandoffCountdown = 0;
            ResetExecutionState();
            ResetMeiTransient();
            OniMeiOwned.EnsureSeed(this);
        }

        public override void SaveData(TagCompound tag) {
            OniMeiOwned.EnsureSeed(this);
            List<string> keys = OwnedMeiKeys.Where(k => !string.IsNullOrEmpty(k)).Distinct().OrderBy(k => k).ToList();
            tag["OniMeiOwned"] = keys;
        }

        public override void LoadData(TagCompound tag) {
            OwnedMeiKeys = [];
            if (tag.TryGet("OniMeiOwned", out List<string> keys) && keys != null) {
                foreach (string key in keys) {
                    if (!string.IsNullOrEmpty(key)) {
                        OwnedMeiKeys.Add(key);
                    }
                }
            }
            OniMeiOwned.EnsureSeed(this);
        }

        public override void OnRespawn() {
            Vigor = VigorMax;
            Stance = 0f;
            timeAdvanceCarry = 0f;
            zanshinWindow = 0;
            zanshinPending = false;
            zanshinInputBuffered = false;
            zanshinAutoHandoff = false;
            zanshinAutoHandoffCountdown = 0;
            ResetExecutionState();
            ResetMeiTransient();
        }

        private void ResetExecutionState() {
            executionChainWindow = 0;
            normalDashInFlight = false;
            executionDashQueued = false;
            queuedExecutionTier = ExecutionTier.None;
            queuedExecutionSource = ExecutionTriggerSource.None;
            executionTierInFlight = ExecutionTier.None;
            executionSourceInFlight = ExecutionTriggerSource.None;
            queuedExecutionAim = Vector2.Zero;
            queuedExecutionTargetId = -1;
            queuedExecutionTargetType = -1;
            executionTargetId = -1;
            executionTargetType = -1;
            executionPreviewTargetId = -1;
            executionAnnihilateWindow = 0;
            executionAnnihilatePending = false;
            executionAnnihilateHandoffCountdown = 0;
            executionHandoffDirection = Vector2.UnitX * Player.direction;
            executionBufferedMouseScreen = Vector2.Zero;
        }

        private void ResetMeiTransient() {
            GuiltLayers = 0;
            KurikaraWindow = 0;
            fudoGuardCooldown = 0;
            silentKillWindow = 0;
            plantedCharge = 0;
            plantedReady = false;
            plantedKnockbackGrace = 0;
            petalPruneCooldown = 0;
            tidePhase = 0;
            hollowRoarTimer = 0;
            hollowAwayTicks = 0;
            hollowApproachArmed = false;
            hollowDenseHits = 0;
            hollowDenseWindowStart = 0;
            hollowFocusLossTicks = 0;
            falseBodyVacuumTicks = 0;
        }

        public override void PostUpdate() {
#if DEBUG
            Vigor = VigorMax;
            Stance = DebugStanceOverride >= 0f
                ? MathHelper.Clamp(DebugStanceOverride, 0f, StanceMax)
                : StanceMax;
#endif
            if (Main.dedServ || Player.whoAmI != Main.myPlayer) {
                return;
            }
            executionPreviewTargetId = -1;

            //试炼门禁硬倒计时,与招式无关,反噬僵直也推进
            HimayoStorySync.TickTrialUnlockSafety(Player);

            //铭刻档每帧从手中刀解析:换刀/改铭/收刀即时生效,负担只在手持时存在
            Mei = OniMeiCombat.ResolveHeld(Player);
            Vigor = Math.Min(Vigor, VigorMaxCurrent);

            //TimeGear 只缩放时间属性；输入与手持铭解析仍逐逻辑帧响应。
            //TimeScale 被约束在 0..1，因此每个主逻辑帧至多放行一个鬼切逻辑帧。
            bool advanceTime = TimeGear.PullFrameAdvance(ref timeAdvanceCarry) > 0;
            if (advanceTime) {
                scaledTime++;
                if (vigorRegenDelay > 0) {
                    vigorRegenDelay--;
                }
                else {
                    float regenMul = Mei.NaturalRegenMul;
                    if (Mei.QuietBreath && IsCombatCold()) {
                        regenMul *= OniMeiCombat.QuietBreathRegenMul;
                    }
                    Vigor = Math.Min(VigorMaxCurrent, Vigor + VigorRegenPerTick * regenMul);
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
                if (silentKillWindow > 0) {
                    silentKillWindow--;
                    //默杀窗内:身周细碎哑黑墨纱,读作"气沉住了"
                    if (!Main.dedServ && silentKillWindow % 8 == 0) {
                        PRTLoader.NewParticle<PRT_CrimsonSmoke>(Player.Center + Main.rand.NextVector2Circular(14f, 20f)
                            , -Vector2.UnitY * 0.3f, Color.White, 0.04f)
                            ?.Configure(Main.rand.Next(10, 16), new Color(46, 16, 22), new Color(14, 8, 12));
                    }
                }
                if (falseBodyVacuumTicks > 0) {
                    falseBodyVacuumTicks--;
                }
                if (plantedKnockbackGrace > 0) {
                    plantedKnockbackGrace--;
                }
                if (petalPruneCooldown > 0) {
                    petalPruneCooldown--;
                }
                if (hollowFocusLossTicks > 0) {
                    hollowFocusLossTicks--;
                }
                if (Mei.TideBeat) {
                    tidePhase++;
                }
                TickPlantedStep();
                TickHollowRoar();
                TickZanshinWindow();
                TickExecutionFlow();
            }

            ModKeybind flashStepKey = CWRKeySystem.Onikiri_FlashStep;
            bool flashStepUnbound = CWRKeySystem.IsKeybindUnbound(flashStepKey, FlashStepBindingMode);
            bool flashStepPressed = flashStepUnbound
                ? Main.mouseRight && Main.mouseRightRelease
                : flashStepKey.JustPressed;
            bool executePressed = CWRKeySystem.Onikiri_Execute?.JustPressed == true;
            bool sakuraPressed = CWRKeySystem.Onikiri_SakuraFlight?.JustPressed == true;
            //左键沿供 TryZanshinStrike 的 Shoot 路径鉴别:ItemCheck 先于 PostUpdate,
            //此处更新后,下一帧的物品使用读到的仍是"上一帧是否按着"
            prevMouseLeft = Main.mouseLeft;

            //反噬僵直期间万籁俱寂:招式与领域输入全部静默,规避疾走/翻转拆散钉死
            if (OniPlayerDismember.IsLocked(Player)) {
                return;
            }

            Item item = Player.GetItem();
            bool holding = item != null && item.Alives() && item.type == ModContent.ItemType<OnikiriItem>();
            if (holding && Main.mouseLeft
                && (executionDashQueued || executionTierInFlight != ExecutionTier.None
                    || executionAnnihilateWindow > 0)) {
                if (!executionAnnihilatePending) {
                    executionBufferedMouseScreen = Main.MouseScreen;
                }
                executionAnnihilatePending = true;
            }
            else if (holding && zanshinWindow <= 0 && Main.mouseLeft
                && (dashLock > 0 || OniSakuraFlight.ControlsOwner(Player.whoAmI))) {
                if (!zanshinInputBuffered) {
                    zanshinBufferedMouseScreen = Main.MouseScreen;
                }
                zanshinInputBuffered = true;
            }
            HandleDomainInput(holding);
            if (!holding || Player.dead || Player.CCed) {
                if (Player.dead) {
                    ResetExecutionState();
                }
                else if (queuedExecutionSource == ExecutionTriggerSource.ExecuteKey) {
                    ClearQueuedExecutionRequest();
                }
                return;
            }
            //点鬼簿/铭刻仪式演出中不受理招式输入
            if ((OniRegisterUI.Instance?.IsOpen ?? false) || (OniEngraveRiteUI.Instance?.Active ?? false)) {
                if (queuedExecutionSource == ExecutionTriggerSource.ExecuteKey) {
                    ClearQueuedExecutionRequest();
                }
                return;
            }

            if (executePressed && CanAcceptExecuteInput()) {
                HandleExecuteInput();
            }
            if (sakuraPressed && CanAcceptSakuraFlightInput()) {
                HandleSakuraFlightInput(item);
            }
            if (flashStepPressed && CanAcceptFlashStepInput()) {
                HandleFlashStepInput(item);
            }
            ManageSakuraFlight(advanceTime);
            TryLaunchQueuedExecutionDash(item);
            ReleaseExecutionAnnihilatePending(item);
            ReleaseZanshinPending(item);
            UpdateExecutionPreview();
            if (advanceTime) {
                ReadyCue();
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
            bool chainInput = normalDashInFlight || executionChainWindow > 0;
            return !HasCommonInputBlock()
                && (chainInput || !HasWorldInteractionInputBlock());
        }

        private bool CanAcceptExecuteInput() {
            if (HasCommonInputBlock() || HasWorldInteractionInputBlock()
                || Player.mount?.Active == true || OniSakuraFlight.ControlsOwner(Player.whoAmI)
                || executionSourceInFlight != ExecutionTriggerSource.None
                || Player.ownedProjectileCounts[ModContent.ProjectileType<OniFinaleSlash>()] > 0
                || Player.ownedProjectileCounts[ModContent.ProjectileType<OniAnnihilate>()] > 0) {
                return false;
            }
            return !HasBlockingExecutionOccupant();
        }

        private bool CanAcceptSakuraFlightInput()
            => !HasCommonInputBlock() && !HasWorldInteractionInputBlock();

        private bool HasCommonInputBlock()
            => Main.mapFullscreen || Main.gamePaused || Main.ingameOptionsWindow || Main.inFancyUI
                || Main.drawingPlayerChat || Main.editSign || Main.editChest || Main.blockInput
                || Player.noItems || Player.talkNPC != -1 || Player.sign != -1
                || CaptureManager.Instance.Active || Player.tileInteractionHappened;

        private bool HasWorldInteractionInputBlock()
            => Player.mouseInterface || Main.HoveringOverAnNPC || Main.SmartInteractShowingGenuine
                || CursorOverInteractiveProjectile();

        /// <summary>普通连段与第一段疾走允许被处决接管，其余硬占刀权演出拒绝缓存</summary>
        private bool HasBlockingExecutionOccupant() {
            foreach (Projectile projectile in Main.ActiveProjectiles) {
                if (projectile.owner != Player.whoAmI
                    || projectile.ModProjectile is not IOniBladeOccupant occupant
                    || !occupant.HardOccupiesBlade) {
                    continue;
                }
                if (projectile.ModProjectile is CrimsonRendSlash) {
                    continue;
                }
                if (normalDashInFlight && projectile.ModProjectile is OniFlashStep) {
                    continue;
                }
                return true;
            }
            return false;
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

        private void HandleFlashStepInput(Item item) {
            if (executionDashQueued || executionTierInFlight != ExecutionTier.None) {
                return;
            }
            if (normalDashInFlight
                || (executionChainWindow > 0 && ResolveExecutionTier() != ExecutionTier.None)) {
                QueueExecutionDash();
                return;
            }
            if (executionAnnihilateWindow > 0) {
                FailExecutionFollowup();
            }
            TryDash(item, executionDash: false, ExecutionTier.None);
        }

        /// <summary>C 的按下沿只负责启动一次普通付费疾走；疾走中按住即为樱流武装</summary>
        private void HandleSakuraFlightInput(Item item) {
            if (normalDashInFlight || executionDashQueued
                || executionTierInFlight != ExecutionTier.None
                || OniSakuraFlight.ControlsOwner(Player.whoAmI)) {
                return;
            }
            if (executionAnnihilateWindow > 0) {
                FailExecutionFollowup();
            }
            TryDash(item, executionDash: false, ExecutionTier.None);
        }

        private ExecutionTier ResolveExecutionTier() {
            if (Stance >= StanceMax - 0.01f) {
                return ExecutionTier.Full;
            }
            return Stance >= AnnihilateCost - 0.01f ? ExecutionTier.Half : ExecutionTier.None;
        }

        private void QueueExecutionDash() {
            ExecutionTier tier = ResolveExecutionTier();
            if (tier == ExecutionTier.None && !normalDashInFlight) {
                return;
            }
            queuedExecutionTier = tier;
            queuedExecutionSource = ExecutionTriggerSource.ManualChain;
            executionDashQueued = true;
            queuedExecutionAim = CaptureRelativeCursorAim(clampToExecutionRange: false);
            queuedExecutionTargetId = -1;
            queuedExecutionTargetType = -1;
            if (!normalDashInFlight) {
                executionChainWindow = Math.Max(executionChainWindow, ExecutionChainWindowTicks);
            }
            ClearZanshinIntent();
            OniTalismanHud.NotifyExecutionDashQueued();
        }

        /// <summary>满势专用处决：冻结级联目标；未锁敌则保存相对光标路线供起步时重构</summary>
        private void HandleExecuteInput() {
            if (Stance < StanceMax - 0.01f) {
                OniTalismanHud.NotifyStanceDenied();
                return;
            }

            NPC target = PickExecutionTarget(Main.MouseWorld);
            executionAnnihilateWindow = 0;
            executionAnnihilatePending = false;
            executionAnnihilateHandoffCountdown = 0;
            queuedExecutionTier = ExecutionTier.Full;
            queuedExecutionSource = ExecutionTriggerSource.ExecuteKey;
            executionDashQueued = true;
            queuedExecutionAim = CaptureRelativeCursorAim(clampToExecutionRange: true);
            queuedExecutionTargetId = target?.whoAmI ?? -1;
            queuedExecutionTargetType = target?.type ?? -1;
            executionChainWindow = 0;
            ClearZanshinIntent();

            if (target != null) {
                executionPreviewTargetId = target.whoAmI;
                OniTalismanHud.NotifyExecutionLocked();
            }
            else {
                OniTalismanHud.NotifyExecutionWhiffQueued();
            }
        }

        private void TryLaunchQueuedExecutionDash(Item item) {
            if (!executionDashQueued || normalDashInFlight) {
                return;
            }
            bool manualChain = queuedExecutionSource == ExecutionTriggerSource.ManualChain;
            if (!manualChain && dashLock > 0) {
                return;
            }
            if (Player.mount?.Active == true || OniSakuraFlight.ControlsOwner(Player.whoAmI)
                || (queuedExecutionSource == ExecutionTriggerSource.ExecuteKey
                    && (HasCommonInputBlock() || HasWorldInteractionInputBlock()
                        || HasBlockingExecutionOccupant()))) {
                if (queuedExecutionSource == ExecutionTriggerSource.ExecuteKey) {
                    ClearQueuedExecutionRequest();
                }
                return;
            }
            if (manualChain) {
                dashLock = 0;
            }
            ExecutionTier tier = queuedExecutionTier;
            TryDash(item, executionDash: true, tier);
        }

        private bool TryDash(Item item, bool executionDash, ExecutionTier executionTier) {
            //再触发锁内静默(是节拍不是资源问题);骑乘时位移权在坐骑;樱流握有本体时不受理
            if (dashLock > 0 || Player.mount?.Active == true
                || OniSakuraFlight.ControlsOwner(Player.whoAmI)) {
                return false;
            }

            if (!executionDash) {
                //风樋减耗;友切的咎逐层加价,残心命中偿清;余烬场在时疾走更烫;假身在场疾走更费
                float dashCost = DashVigorCost * Mei.DashVigorCostMul + GuiltLayers * GuiltDashVigorPerLayer;
                if (Mei.EmberField && OniMeiGroundBurn.AnyOwnedStyle(Player, OniMeiBurnStyle.Ember)) {
                    dashCost *= OniMeiCombat.EmberFieldDashCostMul;
                }
                if (Mei.FalseBody && OniMeiFalseBody.AnyOwned(Player)) {
                    dashCost *= OniMeiCombat.FalseBodyDashCostMul;
                }
                if (Vigor < dashCost - 0.01f) {
                    OniTalismanHud.NotifyVigorDenied();
                    return false;
                }
                Vigor -= dashCost;
                vigorRegenDelay = VigorRegenDelayTicks + Mei.ExtraRegenDelayTicks;
                executionChainWindow = 0;
                ClearQueuedExecutionRequest();
            }

            ClearZanshinIntent();
            zanshinInputBuffered = !executionDash && Main.mouseLeft;
            ShootState state = Player.GetShootState();
            ExecutionTriggerSource triggerSource = executionDash
                ? queuedExecutionSource
                : ExecutionTriggerSource.None;
            Vector2 aim = Main.MouseWorld - Player.Center;
            float distance = aim.Length() + DashCursorOvershoot;
            int lockedTargetId = -1;
            int lockedTargetType = -1;
            if (executionDash) {
                ResolveQueuedExecutionDashPlan(triggerSource, out aim, out distance
                    , out lockedTargetId, out lockedTargetType);
            }
            zanshinHandoffDirection = aim.SafeNormalize(Vector2.UnitX * Player.direction);
            if (zanshinInputBuffered) {
                zanshinBufferedMouseScreen = Main.MouseScreen;
            }
            if (executionDash && triggerSource == ExecutionTriggerSource.ManualChain && Main.mouseLeft) {
                executionAnnihilatePending = true;
                executionBufferedMouseScreen = Main.MouseScreen;
            }

            float interruptRotation = 0f;
            CrimsonRendSlash combo = CrimsonRendSlash.FindController(Player);
            bool interruptCombo = combo != null
                && combo.BeginFlashStepInterrupt(aim, out interruptRotation);
            dashLock = Math.Max(DashRefireLockTicks
                , OniFlashStep.CalculateControlFrames(distance, interruptCombo));
            if (Mei.StickyBind) {
                //滞樋自黏负担:再触发锁加帧(节奏税),不再用落地半速的泥地感
                dashLock += OniMeiCombat.StickyBindDashLockTicks;
            }

            Projectile dash = OniFlashStep.Fire(Player, aim
                , (int)(state.WeaponDamage * DashDamageMul * Mei.FlashMarkDamageMul)
                , state.WeaponKnockback, distance, executionDash: executionDash
                , interruptCombo: interruptCombo, interruptRotation: interruptRotation
                , source: Player.GetSource_ItemUse(item));
            if (dash == null) {
                dashLock = 0;
                if (executionDash) {
                    ClearQueuedExecutionRequest();
                }
                return false;
            }

            normalDashInFlight = !executionDash;
            executionTierInFlight = executionDash ? executionTier : ExecutionTier.None;
            executionSourceInFlight = executionDash ? triggerSource : ExecutionTriggerSource.None;
            executionTargetId = executionDash ? lockedTargetId : -1;
            executionTargetType = executionDash ? lockedTargetType : -1;
            if (executionDash) {
                ClearQueuedExecutionRequest(preserveFollowupInput: true);
            }
            return true;
        }

        private void ResolveQueuedExecutionDashPlan(ExecutionTriggerSource source, out Vector2 aim
            , out float distance, out int targetId, out int targetType) {
            targetId = -1;
            targetType = -1;
            aim = queuedExecutionAim;
            distance = aim.Length() + DashCursorOvershoot;
            if (source != ExecutionTriggerSource.ExecuteKey) {
                return;
            }

            NPC target = ResolveQueuedExecutionTarget();
            if (target == null) {
                return;
            }

            BuildLockedExecutionPath(target, out aim, out distance);
            targetId = target.whoAmI;
            targetType = target.type;
        }

        /// <summary>起步帧刷新锁定实例；失效时只重跑一次完整级联</summary>
        private NPC ResolveQueuedExecutionTarget() {
            NPC target = GetExecutionTarget(queuedExecutionTargetId, queuedExecutionTargetType);
            if (target == null) {
                target = PickExecutionTarget(Main.MouseWorld);
                queuedExecutionTargetId = target?.whoAmI ?? -1;
                queuedExecutionTargetType = target?.type ?? -1;
            }
            return target;
        }

        private void BuildLockedExecutionPath(NPC target, out Vector2 aim, out float distance) {
            Vector2 initial = target.Center - Player.Center;
            Vector2 initialDir = initial.SafeNormalize(Vector2.UnitX * Player.direction);
            float initialExtent = MathF.Abs(initialDir.X) * target.width * 0.5f
                + MathF.Abs(initialDir.Y) * target.height * 0.5f;
            float initialDistance = initial.Length() + initialExtent + ExecutionTargetOvershoot;
            int predictedFrames = OniFlashStep.CalculateTravelFrames(initialDistance);
            Vector2 prediction = target.velocity * predictedFrames;
            if (prediction.LengthSquared() > ExecutionPredictionMax * ExecutionPredictionMax) {
                prediction = prediction.SafeNormalize(Vector2.Zero) * ExecutionPredictionMax;
            }

            Vector2 predicted = target.Center + prediction - Player.Center;
            Vector2 direction = predicted.SafeNormalize(initialDir);
            float projectedExtent = MathF.Abs(direction.X) * target.width * 0.5f
                + MathF.Abs(direction.Y) * target.height * 0.5f;
            distance = MathF.Max(predicted.Length() + projectedExtent + ExecutionTargetOvershoot, 1f);
            aim = direction * distance;
        }

        private Vector2 CaptureRelativeCursorAim(bool clampToExecutionRange) {
            Vector2 aim = Main.MouseWorld - Player.Center;
            float distance = aim.Length();
            if (clampToExecutionRange && distance > ExecutionFocusMaxDistance) {
                aim *= ExecutionFocusMaxDistance / distance;
            }
            if (aim.LengthSquared() < 1f) {
                aim = Vector2.UnitX * Player.direction;
            }
            return aim;
        }

        private void ClearQueuedExecutionRequest(bool preserveFollowupInput = false) {
            executionDashQueued = false;
            queuedExecutionTier = ExecutionTier.None;
            queuedExecutionSource = ExecutionTriggerSource.None;
            queuedExecutionAim = Vector2.Zero;
            queuedExecutionTargetId = -1;
            queuedExecutionTargetType = -1;
            executionChainWindow = 0;
            if (!preserveFollowupInput) {
                executionAnnihilatePending = false;
                executionAnnihilateHandoffCountdown = 0;
                executionBufferedMouseScreen = Vector2.Zero;
            }
        }

        private void UpdateExecutionPreview() {
            if (Stance < StanceMax - 0.01f
                || Player.ownedProjectileCounts[ModContent.ProjectileType<OniFinaleSlash>()] > 0) {
                executionPreviewTargetId = -1;
                return;
            }
            if (queuedExecutionSource == ExecutionTriggerSource.ExecuteKey) {
                executionPreviewTargetId = GetExecutionTarget(queuedExecutionTargetId
                    , queuedExecutionTargetType)?.whoAmI ?? -1;
                return;
            }
            if (executionSourceInFlight == ExecutionTriggerSource.ExecuteKey) {
                executionPreviewTargetId = GetExecutionTarget(executionTargetId
                    , executionTargetType)?.whoAmI ?? -1;
                return;
            }
            executionPreviewTargetId = PickExecutionTarget(Main.MouseWorld)?.whoAmI ?? -1;
        }

        /// <summary>专用处决目标级联：光标磁吸→近五秒命中→范围内 Boss</summary>
        private NPC PickExecutionTarget(Vector2 cursor) {
            return PickExecutionAtCursor(cursor)
                ?? PickExecutionFromHitMemory()
                ?? PickExecutionBoss(cursor);
        }

        private NPC PickExecutionAtCursor(Vector2 cursor) {
            NPC best = null;
            bool bestBoss = false;
            float bestLife = 0f;
            float bestDistance = float.MaxValue;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy()) {
                    continue;
                }
                float cursorDistance = DistanceToHitbox(npc, cursor);
                if (cursorDistance > ExecutionCursorMagnetRadius
                    || Vector2.Distance(Player.Center, npc.Center)
                        > ExecutionFocusMaxDistance + ExecutionCursorRangeSlack) {
                    continue;
                }
                NPC root = RootOf(npc);
                bool better = best == null
                    || (root.boss != bestBoss
                        ? root.boss
                        : Math.Abs(root.lifeMax - bestLife) > 1f
                            ? root.lifeMax > bestLife
                            : cursorDistance < bestDistance);
                if (better) {
                    best = npc;
                    bestBoss = root.boss;
                    bestLife = root.lifeMax;
                    bestDistance = cursorDistance;
                }
            }
            return best;
        }

        private NPC PickExecutionFromHitMemory() {
            NPC best = null;
            bool bestBoss = false;
            int bestTick = int.MinValue;
            for (int i = 0; i < hitMemory.Length; i++) {
                ref HitMemory memory = ref hitMemory[i];
                if (memory.Tick <= 0 || scaledTime - memory.Tick > HitMemoryLifeTicks) {
                    continue;
                }
                NPC npc = GetExecutionTarget(memory.NpcId, memory.NpcType);
                if (npc == null || DistanceToHitbox(npc, Player.Center) > ExecutionFocusMaxDistance) {
                    continue;
                }
                NPC root = RootOf(npc);
                bool better = best == null
                    || (root.boss != bestBoss ? root.boss : memory.Tick > bestTick);
                if (better) {
                    best = npc;
                    bestBoss = root.boss;
                    bestTick = memory.Tick;
                }
            }
            return best;
        }

        private NPC PickExecutionBoss(Vector2 cursor) {
            NPC best = null;
            float bestDistance = float.MaxValue;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy() || !RootOf(npc).boss
                    || DistanceToHitbox(npc, Player.Center) > ExecutionFocusMaxDistance) {
                    continue;
                }
                float cursorDistance = DistanceToHitbox(npc, cursor);
                if (cursorDistance < bestDistance) {
                    best = npc;
                    bestDistance = cursorDistance;
                }
            }
            return best;
        }

        private static NPC GetExecutionTarget(int npcId, int npcType) {
            if (npcId < 0 || npcId >= Main.maxNPCs) {
                return null;
            }
            NPC npc = Main.npc[npcId];
            return npc.active && npc.type == npcType && npc.CanBeChasedBy() ? npc : null;
        }

        private void ClearZanshinIntent() {
            zanshinWindow = 0;
            zanshinPending = false;
            zanshinInputBuffered = false;
            zanshinAutoHandoff = false;
            zanshinAutoHandoffCountdown = 0;
        }

        //==================== 樱流化身 ====================

        /// <summary>
        /// 疾走衔樱流,<see cref="OniFlashStep"/> 停止帧(owner);
        /// 需表世界+最低气力,失败静默
        /// </summary>
        internal bool TryChainSakuraFlight(Vector2 direction, IEntitySource source) {
            if (Player.whoAmI != Main.myPlayer || Player.mount?.Active == true
                || !SakuraFlightInputHeld
                || executionDashQueued || executionTierInFlight != ExecutionTier.None) {
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
            Tutorial.OnikiriTutorialEvents.FireSakuraStarted();
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
        private void ManageSakuraFlight(bool advanceTime) {
            if (!OniSakuraFlight.IsTraveling(Player.whoAmI)) {
                return;
            }
            vigorRegenDelay = Math.Max(vigorRegenDelay, VigorRegenDelayTicks + Mei.ExtraRegenDelayTicks);
            OniDomainPlayer domain = Player.GetModPlayer<OniDomainPlayer>();
            if (!SakuraFlightInputHeld || Vigor <= 0.01f
                || domain.Phase != OniDomainPhase.Omote || domain.WorldIsUra) {
                OniSakuraFlight.RequestStop(Player);
                return;
            }
            if (advanceTime) {
                Vigor = Math.Max(0f, Vigor - SakuraDrainPerTick * Mei.SakuraDrainMul);
            }
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
            CancelExecutionIntent(settleFollowup: false);
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
            Tutorial.OnikiriTutorialEvents.FireZanshinHit(target);
            Stance = Math.Min(StanceMax, Stance + StancePerZanshinSlash * Mei.StanceGainMul);
            if (Mei.ZanshinHitVigorBonus > 0f) {
                Vigor = Math.Min(VigorMaxCurrent, Vigor + Mei.ZanshinHitVigorBonus);
                if (Mei.BloodGroove) {
                    OniMeiStrikes.SpawnBloodBackflow(Player, target);
                }
            }
            TryApplyStickyBind(target);
        }

        //==================== 处决疾走 ====================

        private void TickExecutionFlow() {
            if (executionChainWindow > 0) {
                executionChainWindow--;
                if (executionChainWindow <= 0 && executionDashQueued
                    && queuedExecutionSource == ExecutionTriggerSource.ManualChain) {
                    ClearQueuedExecutionRequest();
                }
            }
            if (executionAnnihilateWindow <= 0) {
                return;
            }
            executionAnnihilateWindow--;
            if (executionAnnihilatePending && executionAnnihilateHandoffCountdown > 0) {
                executionAnnihilateHandoffCountdown--;
            }
            if (executionAnnihilateWindow <= 0) {
                FailExecutionFollowup();
            }
        }

        /// <summary>疾走路径命中的提前结算入口；焦点由调用方明确给出，可为空目标</summary>
        internal bool TryResolveExecutionFinale(NPC target, Vector2 focus, Vector2 direction) {
            if (Player.whoAmI != Main.myPlayer) {
                return false;
            }
            if (executionSourceInFlight == ExecutionTriggerSource.ManualChain
                && executionTierInFlight == ExecutionTier.Half
                && ResolveExecutionTier() == ExecutionTier.Full) {
                executionTierInFlight = ExecutionTier.Full;
            }
            if (executionTierInFlight != ExecutionTier.Full) {
                return false;
            }
            executionHandoffDirection = direction.SafeNormalize(Vector2.UnitX * Player.direction);
            return FireExecutionFinale(target, focus, direction);
        }

        /// <summary>疾走交还操控帧回报；普通疾走开连携窗，处决疾走结算档位</summary>
        internal void OnFlashStepFinished(bool executionDash, NPC directTarget, Vector2 direction) {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            executionHandoffDirection = direction.SafeNormalize(Vector2.UnitX * Player.direction);
            if (!executionDash) {
                normalDashInFlight = false;
                if (queuedExecutionSource == ExecutionTriggerSource.ExecuteKey) {
                    return;
                }
                ExecutionTier tier = ResolveExecutionTier();
                if (executionDashQueued) {
                    if (tier == ExecutionTier.None) {
                        ClearQueuedExecutionRequest();
                    }
                    else {
                        queuedExecutionTier = tier;
                        executionChainWindow = ExecutionChainWindowTicks;
                    }
                }
                else {
                    executionChainWindow = tier == ExecutionTier.None
                        ? 0
                        : ExecutionChainWindowTicks;
                    if (executionChainWindow > 0) {
                        OniTalismanHud.NotifyExecutionChainOpen();
                    }
                }
                return;
            }

            if (executionTierInFlight == ExecutionTier.None) {
                return;
            }
            if (executionSourceInFlight == ExecutionTriggerSource.ExecuteKey) {
                FireExecutionFinale(null, Player.Center, direction);
                return;
            }
            if (executionTierInFlight == ExecutionTier.Full && directTarget?.active == true
                && TryResolveExecutionFinale(directTarget, directTarget.Center, direction)) {
                return;
            }
            executionTierInFlight = ExecutionTier.None;
            executionSourceInFlight = ExecutionTriggerSource.None;
            executionTargetId = -1;
            executionTargetType = -1;
            OpenExecutionAnnihilateWindow();
        }

        /// <summary>主控提前消亡兜底：免费疾走已开始却未交还，按失败结算</summary>
        internal void OnFlashStepAborted(bool executionDash) {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (!executionDash) {
                normalDashInFlight = false;
                executionChainWindow = 0;
                if (queuedExecutionSource == ExecutionTriggerSource.ManualChain) {
                    ClearQueuedExecutionRequest();
                }
                return;
            }
            if (Player.dead) {
                ResetExecutionState();
                return;
            }
            FailExecutionFollowup();
        }

        private bool FireExecutionFinale(NPC target, Vector2 focus, Vector2 direction) {
            Item item = Player.GetItem();
            if (item == null || item.type != ModContent.ItemType<OnikiriItem>()) {
                ClearExecutionFollowup();
                return false;
            }
            ShootState state = Player.GetShootState();
            Vector2 aim = direction.SafeNormalize(Vector2.UnitX * Player.direction);
            Projectile finale = OniFinaleSlash.Fire(Player, focus, aim, state.WeaponDamage
                , state.WeaponKnockback, scale: OnikiriOverride.GetFinaleScale(item)
                , source: Player.GetSource_ItemUse(item));
            if (finale == null) {
                ClearExecutionFollowup();
                return false;
            }
            Stance = 0f;
            ClearExecutionFollowup();
            ClearZanshinIntent();
            IgniteKurikara();
            TrySpawnEmberField(focus, state.WeaponDamage);
            Tutorial.OnikiriTutorialEvents.FireExecutionFinale(target);
            return true;
        }

        private void OpenExecutionAnnihilateWindow() {
            executionAnnihilateWindow = ExecutionAnnihilateWindowTicks;
            executionAnnihilateHandoffCountdown = executionAnnihilatePending
                ? ExecutionAnnihilateHandoffFrames + 1
                : 0;
            executionBufferedMouseScreen = executionAnnihilatePending
                ? executionBufferedMouseScreen
                : Main.MouseScreen;
            ClearZanshinIntent();
        }

        /// <summary>灭世后续左键入口；处决后续优先于残心和普通连段</summary>
        internal bool TryExecutionAnnihilate(Item item, bool edgeVerified) {
            if (Player.whoAmI != Main.myPlayer || executionAnnihilateWindow <= 0) {
                return false;
            }
            if (!edgeVerified && (!Main.mouseLeft || prevMouseLeft)) {
                return false;
            }
            if (executionAnnihilatePending) {
                return true;
            }
            executionAnnihilatePending = true;
            executionAnnihilateHandoffCountdown = 0;
            executionBufferedMouseScreen = Main.MouseScreen;
            FireExecutionAnnihilate(item);
            return true;
        }

        private void ReleaseExecutionAnnihilatePending(Item item) {
            if (!executionAnnihilatePending || executionAnnihilateWindow <= 0
                || executionAnnihilateHandoffCountdown > 0
                || OniBladeOccupancy.AnyHardOccupant(Player)) {
                return;
            }
            FireExecutionAnnihilate(item);
        }

        private bool FireExecutionAnnihilate(Item item) {
            if (item == null || item.type != ModContent.ItemType<OnikiriItem>()) {
                FailExecutionFollowup();
                return false;
            }
            Vector2 fallback = executionHandoffDirection.SafeNormalize(Vector2.UnitX * Player.direction);
            bool deliberateRedirect = Vector2.DistanceSquared(Main.MouseScreen, executionBufferedMouseScreen)
                >= ZanshinRedirectMouseDistance * ZanshinRedirectMouseDistance;
            Vector2 aim = deliberateRedirect
                ? (Main.MouseWorld - Player.Center).SafeNormalize(fallback)
                : fallback;
            ShootState state = Player.GetShootState();
            Projectile annihilate = OniAnnihilate.Fire(Player, Player.Center, aim
                , (int)(state.WeaponDamage * AnnihilateDamageMul), state.WeaponKnockback
                , source: Player.GetSource_ItemUse(item));
            if (annihilate == null) {
                FailExecutionFollowup();
                return false;
            }

            Stance = Math.Max(0f, Stance - AnnihilateCost);
            ClearExecutionFollowup();
            CrimsonRendSlash.FindController(Player)?.ConsumeZanshinInput();
            IgniteKurikara();
            Vector2 emberAt = Player.Center + aim * 120f;
            TrySpawnEmberField(emberAt, state.WeaponDamage);
            Tutorial.OnikiriTutorialEvents.FireExecutionAnnihilate();
            return true;
        }

        private void FailExecutionFollowup() {
            ClearExecutionFollowup();
        }

        private void ClearExecutionFollowup() {
            ClearQueuedExecutionRequest();
            executionTierInFlight = ExecutionTier.None;
            executionSourceInFlight = ExecutionTriggerSource.None;
            executionTargetId = -1;
            executionTargetType = -1;
            executionAnnihilateWindow = 0;
            executionAnnihilatePending = false;
            executionAnnihilateHandoffCountdown = 0;
        }

        /// <summary>普通攻击/肢解接管时取消首段连携；若免费疾走已经结束则当作放弃后续</summary>
        internal void CancelExecutionIntent(bool settleFollowup, bool force = false) {
            if (!force && queuedExecutionSource == ExecutionTriggerSource.ExecuteKey) {
                return;
            }
            if (settleFollowup && executionAnnihilateWindow > 0) {
                FailExecutionFollowup();
                return;
            }
            ClearQueuedExecutionRequest();
        }

        /// <summary>余炎：处决后焦点留余烬场（不走龙火五连）</summary>
        private void TrySpawnEmberField(Vector2 at, int weaponDamage) {
            if (!Mei.EmberField) {
                return;
            }
            int dmg = Math.Max(1, (int)(weaponDamage * OniMeiCombat.EmberDamageMul));
            OniMeiGroundBurn.TrySpawnOrRefresh(Player, at, dmg, OniMeiCombat.EmberLifeTicks
                , OniMeiCombat.EmberScale, OniMeiBurnStyle.Ember);
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
                CancelExecutionIntent(settleFollowup: false, force: true);
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
                CancelExecutionIntent(settleFollowup: false, force: true);
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

        /// <summary>肢解目标点选，蠕虫任意体节均可作为落刀锚点</summary>
        private NPC PickDismemberTarget(Vector2 cursor, float pad) {
            NPC best = null;
            bool bestBoss = false;
            float bestLife = 0f;
            float bestD = float.MaxValue;
            foreach (NPC npc in Main.ActiveNPCs) {
                NPC root = RootOf(npc);
                bool canPick = npc.CanBeChasedBy()
                    || (root != npc && root.CanBeChasedBy());
                if (!canPick) {
                    continue;
                }
                float d = DistanceToHitbox(npc, cursor);
                if (d > pad) {
                    continue;
                }
                if (DistanceToHitbox(npc, Player.Center) > DismemberRange) {
                    continue;
                }
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

        /// <summary>点到碰撞箱的精确距离(大体型 boss 的箱边也吸得住,不吃"中心太远"的亏)</summary>
        private static float DistanceToHitbox(NPC npc, Vector2 point) {
            Rectangle box = npc.Hitbox;
            Vector2 nearest = new(MathHelper.Clamp(point.X, box.Left, box.Right),
                MathHelper.Clamp(point.Y, box.Top, box.Bottom));
            return Vector2.Distance(point, nearest);
        }

        /// <summary>蠕虫类归当前活跃主体</summary>
        private static NPC RootOf(NPC npc) {
            int rootIndex = NpcGroupHelper.GetAnchorIndex(npc);
            return rootIndex >= 0 && rootIndex < Main.maxNPCs ? Main.npc[rootIndex] : npc;
        }

        /// <summary>记入命中记忆:蠕虫归主体,去重刷新,满则顶掉最旧</summary>
        private void RecordHit(NPC npc) {
            if (npc == null) {
                return;
            }
            npc = RootOf(npc);
            if (!npc.active) {
                return;
            }
            int now = scaledTime;
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
            TryApplyStickyBind(target);
            TryApplyTideOnComboHit(grantResources);
        }

        /// <summary>疾走穿身即格挡:每次疾走仅首次格挡固定蓄势,所有目标都记入命中记忆</summary>
        internal void OnDashParry(NPC npc, bool grantResources) {
            RecordHit(npc);
            if (grantResources) {
                Stance = Math.Min(StanceMax, Stance + StancePerDashParry * Mei.StanceGainMul);
                if (Mei.NumbCounter) {
                    OniMeiCombat.TryApplyNumbCounter(Player, npc);
                }
            }
        }

        /// <summary>疾走自然结束：武装默切默杀窗。开窗一声低吟+身周墨纱罩下</summary>
        internal void ArmSilentKillFromDash() {
            if (Player.whoAmI != Main.myPlayer || !Mei.SilentKill) {
                return;
            }
            silentKillWindow = OniMeiCombat.SilentKillWindowTicks;
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.60f, Volume = 0.26f }, Player.Center);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(Player.Center + Main.rand.NextVector2Circular(16f, 22f)
                    , -Vector2.UnitY * Main.rand.NextFloat(0.2f, 0.6f), Color.White, 0.05f)
                    ?.Configure(Main.rand.Next(12, 18), new Color(46, 16, 22), new Color(14, 8, 12));
            }
        }

        /// <summary>止足：低位移充电；超速且无击退宽容则清充。充电/就绪足元墨环可视</summary>
        private void TickPlantedStep() {
            if (!Mei.PlantedStep) {
                plantedCharge = 0;
                plantedReady = false;
                return;
            }
            if (Player.velocity.LengthSquared() <= OniMeiCombat.PlantedSpeedSq) {
                if (!plantedReady) {
                    plantedCharge++;
                    //充电可视:足元墨屑渐聚,越接近就绪越浓
                    if (!Main.dedServ && plantedCharge % 9 == 0) {
                        float t = plantedCharge / (float)OniMeiCombat.PlantedChargeNeedTicks;
                        Vector2 foot = Player.Bottom - Vector2.UnitY * 4f;
                        Vector2 pos = foot + new Vector2(Main.rand.NextFloat(-16f, 16f), Main.rand.NextFloat(-2f, 2f));
                        PRTLoader.NewParticle<PRT_OniInkDrop>(pos, (foot - pos) * 0.08f - Vector2.UnitY * 0.3f
                            , new Color(56, 14, 20), 0.14f + 0.10f * t)?.Configure(12);
                    }
                    if (plantedCharge >= OniMeiCombat.PlantedChargeNeedTicks) {
                        plantedReady = true;
                        plantedCharge = OniMeiCombat.PlantedChargeNeedTicks;
                        SpawnPlantedReadyCue();
                    }
                }
                //就绪呼吸:足元一圈慢息的纸白微光
                else if (!Main.dedServ && scaledTime % 16 == 0) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 foot = Player.Bottom - Vector2.UnitY * 3f;
                    PRTLoader.NewParticle<PRT_CrimsonSpark>(foot + ang.ToRotationVector2() * 14f
                        , -Vector2.UnitY * 0.3f, new Color(255, 243, 226), 0.15f)
                        ?.Configure(14, affectedByGravity: false);
                }
                return;
            }
            if (plantedKnockbackGrace > 0) {
                return;
            }
            plantedCharge = 0;
            plantedReady = false;
        }

        /// <summary>止足立定就绪:足元环亮定 + 轻声(owner 客户端)</summary>
        private void SpawnPlantedReadyCue() {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Tink with { Pitch = -0.15f, Volume = 0.30f }, Player.Center);
            Vector2 foot = Player.Bottom - Vector2.UnitY * 3f;
            for (int i = 0; i < 6; i++) {
                float ang = MathHelper.TwoPi * i / 6f;
                PRTLoader.NewParticle<PRT_CrimsonSpark>(foot + ang.ToRotationVector2() * 14f
                    , ang.ToRotationVector2() * 0.8f - Vector2.UnitY * 0.3f
                    , new Color(255, 243, 226), 0.20f)
                    ?.Configure(14, affectedByGravity: false);
            }
        }

        /// <summary>默杀窗消费核:清窗+消音重击反馈;未装/无窗 false</summary>
        private bool ConsumeSilentCore() {
            if (silentKillWindow <= 0 || !Mei.SilentKill) {
                return false;
            }
            silentKillWindow = 0;
            OniMeiStrikes.SpawnSilentConsumeFX(Player);
            return true;
        }

        /// <summary>止足立定消费核:清充+字形一闪反馈;未装/未就绪 false</summary>
        private bool ConsumePlantedCore() {
            if (!plantedReady || !Mei.PlantedStep) {
                return false;
            }
            plantedReady = false;
            plantedCharge = 0;
            OniMeiStrikes.SpawnPlantedConsumeFX(Player);
            return true;
        }

        /// <summary>灭世/终结乱舞：消费止足立定加深(单独路径,无叠乘)</summary>
        internal bool TryConsumePlantedStep(ref NPC.HitModifiers modifiers) {
            if (!ConsumePlantedCore()) {
                return false;
            }
            modifiers.FinalDamage *= OniMeiCombat.PlantedStepHitMul;
            return true;
        }

        /// <summary>
        /// 默杀×止足统一消费收口：可同帧叠乘,软帽限幅;
        /// 残心(allowPlanted=true)与连段(第五拍才 allowPlanted)同门,不再有绕帽路径
        /// </summary>
        internal void ApplyMeiConsumeMuls(ref NPC.HitModifiers modifiers, bool allowPlanted) {
            float product = 1f;
            if (ConsumeSilentCore()) {
                product *= OniMeiCombat.SilentKillHitMul;
            }
            if (allowPlanted && ConsumePlantedCore()) {
                product *= OniMeiCombat.PlantedStepHitMul;
            }
            if (product > 1.001f) {
                modifiers.FinalDamage *= Math.Min(product, OniMeiCombat.SilentPlantedSoftCap);
            }
        }

        /// <summary>谢樋：击杀了结溅剪落（门闩防连环；cleave 杀不调此）</summary>
        internal void TryPetalPruneOnKill(NPC killed, int weaponDamage, float knockback) {
            if (!Mei.PetalPrune || petalPruneCooldown > 0 || killed == null) {
                return;
            }
            petalPruneCooldown = OniMeiCombat.PetalPruneCooldownTicks;
            Vector2 origin = killed.Center;
            float aim = (origin - Player.Center).ToRotation();
            if (float.IsNaN(aim)) {
                aim = Player.direction > 0 ? 0f : MathHelper.Pi;
            }
            OniMeiStrikes.FirePetalPrune(Player, origin, aim, Math.Max(1, weaponDamage), knockback);
        }

        /// <summary>谢樋：空残心微扣气</summary>
        internal void NotifyEmptyZanshin() {
            if (!Mei.PetalPrune) {
                return;
            }
            Vigor = Math.Max(0f, Vigor - OniMeiCombat.PetalPruneEmptyZanshinVigor);
        }

        /// <summary>潮拍：当前是否合潮</summary>
        internal bool IsTideOnBeatNow
            => Mei.TideBeat && OniMeiCombat.IsTideOnBeat(tidePhase);

        /// <summary>潮拍：潮相 0..1(窗心在 0.5)，HUD 潮痕游标用；未装潮樋 -1</summary>
        internal float TidePhase01 {
            get {
                int period = OniMeiCombat.TidePeriodTicks;
                if (!Mei.TideBeat || period <= 0) {
                    return -1f;
                }
                int phase = ((tidePhase % period) + period) % period;
                return phase / (float)period;
            }
        }

        /// <summary>潮拍：授权命中合潮奖气</summary>
        internal void TryApplyTideOnComboHit(bool grantResources) {
            if (!Mei.TideBeat || !grantResources || !OniMeiCombat.IsTideOnBeat(tidePhase)) {
                return;
            }
            Vigor = Math.Min(VigorMaxCurrent, Vigor + OniMeiCombat.TideOnBeatVigor);
        }

        /// <summary>潮拍：错拍连段授权首击略亏</summary>
        internal bool TryApplyTideOffBeatHit(ref NPC.HitModifiers modifiers) {
            if (!Mei.TideBeat || OniMeiCombat.IsTideOnBeat(tidePhase)) {
                return false;
            }
            modifiers.FinalDamage *= OniMeiCombat.TideOffBeatHitMul;
            return true;
        }

        /// <summary>空鸣：授权命中接近加成 / 失焦惩罚（先失焦）</summary>
        internal void ApplyHollowRoarHitMuls(ref NPC.HitModifiers modifiers) {
            if (!Mei.HollowRoar) {
                return;
            }
            RecordHollowDenseHit();
            if (hollowFocusLossTicks > 0) {
                modifiers.FinalDamage *= OniMeiCombat.HollowFocusLossHitMul;
                return;
            }
            if (hollowApproachArmed) {
                hollowApproachArmed = false;
                modifiers.FinalDamage *= OniMeiCombat.HollowApproachHitMul;
            }
        }

        /// <summary>空鸣：空场威压与远离武装</summary>
        private void TickHollowRoar() {
            if (!Mei.HollowRoar) {
                hollowRoarTimer = 0;
                hollowAwayTicks = 0;
                return;
            }
            bool nearFoe = HasNearbyHostile();
            if (nearFoe) {
                hollowAwayTicks = 0;
            }
            else {
                hollowAwayTicks++;
                if (hollowAwayTicks >= OniMeiCombat.HollowRoarColdTicks && !hollowApproachArmed) {
                    hollowApproachArmed = true;
                    ArmHollowApproachCue();
                }
            }
            //失焦期:身周散灰墨,给 0.88 减伤一个看得见的"散焦"读法
            if (hollowFocusLossTicks > 0 && !Main.dedServ && hollowFocusLossTicks % 12 == 0) {
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(Player.Center + Main.rand.NextVector2Circular(20f, 24f)
                    , -Vector2.UnitY * 0.4f, Color.White, 0.05f)
                    ?.Configure(Main.rand.Next(12, 18), new Color(70, 40, 44), new Color(20, 14, 16));
            }
            bool cold = IsCombatColdForHollow() || !nearFoe;
            if (!cold) {
                hollowRoarTimer = 0;
                return;
            }
            if (++hollowRoarTimer < OniMeiCombat.HollowRoarInterval) {
                return;
            }
            hollowRoarTimer = 0;
            OniMeiStrikes.FireHollowRoarPulse(Player);
        }

        private bool IsCombatColdForHollow() {
            int now = scaledTime;
            for (int i = 0; i < hitMemory.Length; i++) {
                int tick = hitMemory[i].Tick;
                if (tick > 0 && now - tick <= OniMeiCombat.HollowRoarColdTicks) {
                    return false;
                }
            }
            return true;
        }

        private bool HasNearbyHostile() {
            float radiusSq = OniMeiCombat.HollowNearRadius * OniMeiCombat.HollowNearRadius;
            Vector2 center = Player.Center;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy() || npc.friendly) {
                    continue;
                }
                if (npc.DistanceSQ(center) <= radiusSq) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>空鸣远离武装就绪:刀缘纸白一挑 + 轻音(owner 客户端)</summary>
        private void ArmHollowApproachCue() {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = -0.10f, Volume = 0.30f }, Player.Center);
            Vector2 dir = Vector2.UnitX * Player.direction;
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_CrimsonSpark>(Player.Center + dir * Main.rand.NextFloat(14f, 30f)
                    - Vector2.UnitY * Main.rand.NextFloat(4f, 16f)
                    , dir * Main.rand.NextFloat(0.8f, 1.8f) - Vector2.UnitY * 0.6f
                    , new Color(255, 243, 226), Main.rand.NextFloat(0.16f, 0.26f))
                    ?.Configure(Main.rand.Next(12, 18), affectedByGravity: false);
            }
        }

        private void RecordHollowDenseHit() {
            int now = scaledTime;
            if (now - hollowDenseWindowStart > OniMeiCombat.HollowFocusLossWindowTicks) {
                hollowDenseWindowStart = now;
                hollowDenseHits = 0;
            }
            hollowDenseHits++;
            if (hollowDenseHits >= OniMeiCombat.HollowFocusLossHitNeed) {
                hollowFocusLossTicks = OniMeiCombat.HollowFocusLossWindowTicks;
                hollowDenseHits = 0;
                hollowDenseWindowStart = now;
                //失焦入场:闷响+灰墨,让接下来的减伤有个听得见的原因
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.8f, Volume = 0.30f }, Player.Center);
                    for (int i = 0; i < 5; i++) {
                        PRTLoader.NewParticle<PRT_CrimsonSmoke>(Player.Center + Main.rand.NextVector2Circular(18f, 22f)
                            , Main.rand.NextVector2Circular(1f, 1f), Color.White, 0.06f)
                            ?.Configure(Main.rand.Next(14, 20), new Color(70, 40, 44), new Color(20, 14, 16));
                    }
                }
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
        /// 铭刻承伤挂点:假身吸一击、影在/真空税、友切 Incoming、不动护耗架势;
        /// 肢解反噬是固定契约,增减一律不碰
        /// </summary>
        public override void ModifyHurt(ref Player.HurtModifiers modifiers) {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer
                || OniPlayerDismember.SelfHurtResolving) {
                return;
            }
            if (TryAbsorbFalseBody(ref modifiers)) {
                return;
            }
            if (Math.Abs(Mei.IncomingDamageMul - 1f) > 0.001f) {
                modifiers.FinalDamage *= Mei.IncomingDamageMul;
            }
            if (Mei.FalseBody && OniMeiFalseBody.AnyOwned(Player)) {
                modifiers.FinalDamage *= OniMeiCombat.FalseBodyIncomingMul;
            }
            if (falseBodyVacuumTicks > 0) {
                modifiers.FinalDamage *= OniMeiCombat.FalseBodyVacuumIncomingMul;
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
                if (Mei.NumbCounter
                    && modifiers.DamageSource.TryGetCausingEntity(out Entity causing)
                    && causing is NPC src) {
                    OniMeiCombat.TryApplyNumbCounter(Player, src);
                }
            }
            if (Mei.PlantedStep) {
                plantedKnockbackGrace = OniMeiCombat.PlantedKnockbackGraceTicks;
            }
        }

        /// <summary>
        /// 镇鸣：仅削弱弹幕承伤与击退；近战捶仍走普通承伤。
        /// 假身优先：弹幕将由假身吃掉时在此碎影归零，镇鸣不叠该次
        /// </summary>
        public override void ModifyHitByProjectile(Projectile proj, ref Player.HurtModifiers modifiers) {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer
                || OniPlayerDismember.SelfHurtResolving) {
                return;
            }
            if (TryAbsorbFalseBody(ref modifiers)) {
                return;
            }
            if (!Mei.QuellProjectiles) {
                return;
            }
            modifiers.FinalDamage *= OniMeiCombat.QuellProjectileDamageMul;
            modifiers.Knockback *= OniMeiCombat.QuellProjectileKnockbackMul;
        }

        /// <summary>假身碎裂回调：开真空窗</summary>
        internal void OnFalseBodyShattered() {
            falseBodyVacuumTicks = OniMeiCombat.FalseBodyVacuumTicks;
        }

        /// <summary>
        /// 假身吸接触/弹伤一层：碎影并将该击伤害与击退归零。
        /// 非接触/非弹来源不吸，留给影在税
        /// </summary>
        private bool TryAbsorbFalseBody(ref Player.HurtModifiers modifiers) {
            if (!Mei.FalseBody) {
                return false;
            }
            OniMeiFalseBody body = OniMeiFalseBody.TryGetOwned(Player);
            if (body == null) {
                return false;
            }
            if (!modifiers.DamageSource.TryGetCausingEntity(out Entity causing)
                || causing is not (NPC or Projectile)) {
                return false;
            }
            body.Shatter();
            modifiers.FinalDamage *= 0f;
            modifiers.Knockback *= 0f;
            return true;
        }

        /// <summary>滞樋：授权命中叠「滞缚」(自实现墨锚阻尼，boss 减效)</summary>
        private void TryApplyStickyBind(NPC target) {
            if (!Mei.StickyBind || target == null || !target.active) {
                return;
            }
            target.AddBuff(ModContent.BuffType<OniBindDebuff>(), OniMeiCombat.StickyBindTargetSlowTicks);
        }

        /// <summary>闲樋：命中记忆在冷战窗口内无刷新则视为脱战</summary>
        private bool IsCombatCold() {
            int now = scaledTime;
            for (int i = 0; i < hitMemory.Length; i++) {
                int tick = hitMemory[i].Tick;
                if (tick > 0 && now - tick <= OniMeiCombat.QuietBreathColdTicks) {
                    return false;
                }
            }
            return true;
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
