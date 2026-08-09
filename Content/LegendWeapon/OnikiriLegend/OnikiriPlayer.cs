using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions.Deeds;
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
    /// 疾走键未绑定时回退右键;表世界按樱流键直接化樱(疾走中按住亦可衔接);交还帧开追斩窗;
    /// 架势过半时可短窗双疾走处决,满势穿身乱舞,否则左键灭世;
    /// 里世界左键点选肢解.
    /// HUD 经 <see cref="OnikiriResourceSource"/> 只读
    /// </summary>
    internal class OnikiriPlayer : ModPlayer
    {
#if DEBUG
        /// <summary>Debug 矩阵测试架势；负值保持原有自动满势行为</summary>
        internal static float DebugStanceOverride = -1f;
        internal static bool DebugAutoRefill = true;
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

        /// <summary>樱流化身每帧耗气(按住持续飞行,松手或气尽自动回卷);满程 180 帧约抽 27 点气</summary>
        private const float SakuraDrainPerTick = 0.15f;
        /// <summary>樱流入飞门槛:低于此气力不受理(樱流键直接起飞与疾走衔接同门槛)</summary>
        private const float SakuraMinVigor = 10f;
        /// <summary>樱流巡航速度(px/帧),模块钳制上限 48;疾走衔接时从高速骤降到此,是"化形"的减速拍</summary>
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
        private const float GuiltDashVigorPerLayer = 4f;
        /// <summary>友切:咎层上限</summary>
        private const int GuiltMaxLayers = 3;
        /// <summary>不动护:每次守护消耗的架势</summary>
        private const float FudoGuardStanceCost = 20f;
        /// <summary>不动护:该次受击的伤害削减比</summary>
        private const float FudoGuardDamageCut = 0.35f;
        /// <summary>不动护:内部冷却(帧,约两秒)</summary>
        private const int FudoGuardCooldownTicks = 150;
        private const float NumbGuardStanceCost = 15f;
        private const float NumbGuardDamageCut = 0.15f;
        private const int NumbGuardCooldownTicks = 90;
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
        private int kurikaraCharges;
        private int fudoGuardCooldown;
        private int numbGuardCooldown;
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
        private uint hollowLastActionSerial;
        /// <summary>空鸣：失焦生效余量</summary>
        private int hollowFocusLossTicks;
        private readonly Dictionary<uint, float> hollowActionMultipliers = [];
        private readonly Queue<uint> hollowActionOrder = [];
        private readonly HashSet<uint> executeRefundedActions = [];
        private readonly Queue<uint> executeRefundOrder = [];
        /// <summary>闲樋/虚吼只看直接刀击，不把疾走穿身计作交战。</summary>
        private int lastDirectBladeHitTick = 1;
        /// <summary>假身：影破真空余量(帧)</summary>
        private int falseBodyVacuumTicks;
        private int falseBodyRearmTicks;
        /// <summary>墨丝：在场丝锚(世界锚点+余寿)，满三枚即闭网</summary>
        private readonly List<SilkAnchor> silkAnchors = [];
        /// <summary>墨丝：闭网门闩(帧)</summary>
        private int silkSnareCooldown;
        /// <summary>墨丝：上一枚锚落在哪个主体上，及其冷却</summary>
        private int silkLastRootId = -1;
        private int silkLastRootCooldown;
        /// <summary>鬼丸：站定累计(帧)，够 SelfCutArmTicks 即进自斩待机</summary>
        private int selfCutStillTicks;
        /// <summary>鬼丸：下一次放刀的倒计时(帧)</summary>
        private int selfCutInterval;
        /// <summary>雷切：落雷门闩(帧)，防一记多段命中刷成雷幕</summary>
        private int thunderCooldown;
        /// <summary>鵺切：落地收势期禁疾走(帧)</summary>
        private int nueDiveRecover;
        /// <summary>空樋：离地后尚未用掉的那次额外疾走</summary>
        private bool airDashCharge;
        /// <summary>空樋：落地沉底，回气归零(帧)</summary>
        private int airGrooveDryTicks;
        /// <summary>空樋：空中疾走收尾的滞空余量(帧)</summary>
        private int airGrooveHover;
        /// <summary>綴樋：本次墨痕引爆的落点收集</summary>
        private readonly List<Vector2> stitchPoints = [];
        /// <summary>綴樋：收集窗余量，归零即结算</summary>
        private int stitchGather;
        private int stitchDamage;
        /// <summary>梵鐘：满架势后的自鸣蓄势(帧)，满即撞钟</summary>
        private int bellCharge;
        /// <summary>般若：鬼面期命中计数，每三次浮一张咬合</summary>
        private int hannyaHitCount;
        /// <summary>般若：上一帧是不是鬼面，用来在翻面那一帧给演出</summary>
        private bool hannyaWasMasked;
        /// <summary>枯山水：立定耙纹累计(帧)</summary>
        private int sandRakeTicks;

        //====在世刀身铭刻层的活仪表(owner 端自治，远端刀只画静态材质)====
        /// <summary>樋内充盈 0~1，各樋语义不同(血位/潮位/烬量/息量)</summary>
        private float engraveHiFill;
        /// <summary>樋内一次性冲击 0~1，逐帧衰减</summary>
        private float engraveHiPulse;
        /// <summary>樋内循环相位 0~1(气丝跑动/烬点爬行/墨珠滑落)，潮樋改读潮相</summary>
        private float engraveHiPhase;
        /// <summary>雕纹点亮 0~1，向条件就绪值缓动</summary>
        private float engraveHoriLit;
        /// <summary>闲樋上一帧的脱战态，用于在窗口开合的那一帧给进出演出</summary>
        private bool engraveQuietCold;

        /// <summary>所持铭 Key 集合(改铭台扇骨门闩);种子含鬼切</summary>
        internal HashSet<string> OwnedMeiKeys = [];

        /// <summary>刀縁进度(跟玩家存档,与所持铭并列)</summary>
        internal OniMeiDeedProgress Deeds { get; } = new();
        /// <summary>刀縁连续态账本(owner 端自治,不进存档不进网络)</summary>
        internal OniMeiDeedTracker DeedTracker { get; } = new();

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

        //====墨丝丝锚:世界锚点,不跟目标走(丝钉在落刀那一刻的位置)====
        private struct SilkAnchor
        {
            public Vector2 Position;
            public int Life;
        }

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
            OnikiriNet.ResetPlayerSession(Player);
            OnikiriNet.RepairDuplicateIdentities(Player);
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
            DeedTracker.Reset();
            OniMeiOwned.EnsureSeed(this);
            OnikiriNet.SendOwnedMeiSnapshot(Player);
            OnikiriNet.SendDeedSnapshot(Player);
        }

        public override void SaveData(TagCompound tag) {
            OniMeiOwned.EnsureSeed(this);
            List<string> keys = OwnedMeiKeys.Where(k => !string.IsNullOrEmpty(k)).Distinct().OrderBy(k => k).ToList();
            tag["OniMeiOwned"] = keys;
            Deeds.Save(tag);
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
            Deeds.Load(tag);
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
            DeedTracker.Reset();
        }

        public override void PreUpdate() {
            OnikiriNet.RepairDuplicateIdentities(Player);
            OnikiriNet.UpdatePending(Player);
            OnikiriNet.ReconcileAuthoritativeState(Player);
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
            kurikaraCharges = 0;
            fudoGuardCooldown = 0;
            numbGuardCooldown = 0;
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
            hollowLastActionSerial = 0;
            hollowFocusLossTicks = 0;
            hollowActionMultipliers.Clear();
            hollowActionOrder.Clear();
            executeRefundedActions.Clear();
            executeRefundOrder.Clear();
            lastDirectBladeHitTick = scaledTime;
            falseBodyVacuumTicks = 0;
            falseBodyRearmTicks = 0;
            silkAnchors.Clear();
            silkSnareCooldown = 0;
            silkLastRootId = -1;
            silkLastRootCooldown = 0;
            selfCutStillTicks = 0;
            selfCutInterval = 0;
            thunderCooldown = 0;
            nueDiveRecover = 0;
            airDashCharge = false;
            airGrooveDryTicks = 0;
            airGrooveHover = 0;
            stitchPoints.Clear();
            stitchGather = 0;
            stitchDamage = 0;
            bellCharge = 0;
            hannyaHitCount = 0;
            hannyaWasMasked = false;
            sandRakeTicks = 0;
        }

        private void ClearRemovedMeiTransient(in OniMeiCombatProfile previous,
            in OniMeiCombatProfile current) {
            if (previous.SilentKill && !current.SilentKill) {
                silentKillWindow = 0;
            }
            if (previous.PlantedStep && !current.PlantedStep) {
                plantedReady = false;
                plantedCharge = 0;
            }
            if (previous.TideBeat && !current.TideBeat) {
                tidePhase = 0;
            }
            if (previous.HollowRoar && !current.HollowRoar) {
                hollowRoarTimer = 0;
                hollowAwayTicks = 0;
                hollowApproachArmed = false;
                hollowDenseHits = 0;
                hollowDenseWindowStart = 0;
                hollowFocusLossTicks = 0;
                hollowLastActionSerial = 0;
                hollowActionMultipliers.Clear();
                hollowActionOrder.Clear();
            }
            if (previous.DragonfireLoop && !current.DragonfireLoop) {
                KurikaraWindow = 0;
                kurikaraCharges = 0;
            }
            if (previous.FalseBody && !current.FalseBody) {
                OniMeiFalseBody.DismissOwned(Player);
            }
            if (previous.SilkSnare && !current.SilkSnare) {
                silkAnchors.Clear();
                silkSnareCooldown = 0;
                silkLastRootId = -1;
                silkLastRootCooldown = 0;
            }
            if (previous.SelfCut && !current.SelfCut) {
                selfCutStillTicks = 0;
                selfCutInterval = 0;
            }
        }

        public override void PostUpdate() {
#if DEBUG
            if (DebugAutoRefill) {
                Vigor = VigorMax;
                Stance = DebugStanceOverride >= 0f
                    ? MathHelper.Clamp(DebugStanceOverride, 0f, StanceMax)
                    : StanceMax;
            }
#endif
            if (Main.dedServ || Player.whoAmI != Main.myPlayer) {
                return;
            }
            executionPreviewTargetId = -1;

            //试炼门禁硬倒计时,与招式无关,反噬僵直也推进
            HimayoStorySync.TickTrialUnlockSafety(Player);

            //铭刻档每帧从手中刀解析:换刀/改铭/收刀即时生效,负担只在手持时存在
            OniMeiCombatProfile previousMei = Mei;
            Mei = OniMeiCombat.ResolveHeld(Player);
            ClearRemovedMeiTransient(in previousMei, in Mei);
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
                    //墨丝：网在织就分心，气回得慢；网一闭或锚过期即恢复
                    if (Mei.SilkSnare && silkAnchors.Count > 0) {
                        regenMul *= OniMeiCombat.SilkWeavingRegenMul;
                    }
                    //空樋：离地回气快，落地沉底那几十帧一点不回
                    if (Mei.AirGroove) {
                        if (airGrooveDryTicks > 0) {
                            regenMul = 0f;
                        }
                        else if (Player.velocity.Y != 0f && !Player.sliding) {
                            regenMul *= OniMeiCombat.AirGrooveAirRegenMul;
                        }
                    }
                    Vigor = Math.Min(VigorMaxCurrent, Vigor + VigorRegenPerTick * regenMul);
                }
                if (dashLock > 0) {
                    dashLock--;
                }
                if (fudoGuardCooldown > 0) {
                    fudoGuardCooldown--;
                }
                if (numbGuardCooldown > 0) {
                    numbGuardCooldown--;
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
                if (falseBodyRearmTicks > 0) {
                    falseBodyRearmTicks--;
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
                if (thunderCooldown > 0) {
                    thunderCooldown--;
                }
                if (nueDiveRecover > 0) {
                    nueDiveRecover--;
                }
                TickPlantedStep();
                TickHollowRoar();
                TickSilkAnchors();
                TickSelfCut();
                TickAirGroove();
                TickMarkStitch();
                TickBellToll();
                TickHannyaMask();
                TickSandGarden();
                TickZanshinWindow();
                TickExecutionFlow();
                TickEngraveGauges();
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
            if (advanceTime) {
                OniMeiDeedEvents.NotifyHeldTick(Player, holding);
            }
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
            //点鬼簿打开时不受理招式输入
            if (OniRegisterUI.Instance?.IsOpen ?? false) {
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
        /// 骇客时停或点鬼簿打开时不受理
        /// </summary>
        private void HandleDomainInput(bool holding) {
            if (Player.dead) {
                return;
            }
            OniDomainPlayer domain = Player.GetModPlayer<OniDomainPlayer>();
            if (HackTime.Active) {
                return;
            }
            if (OniRegisterUI.Instance?.IsOpen ?? false) {
                return;
            }

            if (holding && CWRKeySystem.Legend_Domain.JustPressed) {
                if (!OniDomain.TryToggle(Player, out bool busy,
                    Tutorial.OnikiriDomainCommandSource.Keybind) && busy) {
                    OniTalismanHud.NotifyDomainDenied();
                }
            }
            //中键默认绑定:悬停在鬼眼上时 mouseInterface 为真,让位给眼的点击受理,防同帧双发
            if ((holding || domain.AnyActive) && CWRKeySystem.Onikiri_DomainFlip.JustPressed && !Player.mouseInterface) {
                if (!OniDomain.TryFlip(Player, out bool busy,
                    Tutorial.OnikiriDomainCommandSource.Keybind) && busy) {
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

        /// <summary>
        /// 樱流键的按下沿:表世界稳态下当场化樱起飞,不代偿一段疾走;
        /// 领域或气力不足即原地拒绝并给读数提示
        /// </summary>
        private void HandleSakuraFlightInput(Item item) {
            if (normalDashInFlight || executionDashQueued
                || executionTierInFlight != ExecutionTier.None
                || Player.mount?.Active == true
                || OniSakuraFlight.AnyFor(Player.whoAmI)) {
                return;
            }
            if (!StableOmote) {
                OniTalismanHud.NotifyDomainDenied();
                return;
            }
            if (Vigor < SakuraMinVigor - 0.01f) {
                OniTalismanHud.NotifyVigorDenied();
                return;
            }
            if (!StartSakuraFlight(CaptureRelativeCursorAim(clampToMaxRange: false),
                Player.GetSource_ItemUse(item), seamless: false)) {
                return;
            }
            if (executionAnnihilateWindow > 0) {
                FailExecutionFollowup();
            }
            executionChainWindow = 0;
            //化樱接过刀权:清掉普攻的补发与排拍,别让连段在花瓣里继续挥
            OniBladeOccupancy.FindComboController(Player)?.ConsumeZanshinInput();
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
            queuedExecutionAim = CaptureRelativeCursorAim(clampToMaxRange: true);
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
            queuedExecutionAim = CaptureRelativeCursorAim(clampToMaxRange: true);
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
            //骑乘时位移权在坐骑;樱流握有本体时不受理
            if (Player.mount?.Active == true || OniSakuraFlight.ControlsOwner(Player.whoAmI)) {
                return false;
            }
            //空樋：离地那一次额外疾走可以踏过再触发锁；地面照常吃锁
            bool airDash = false;
            if (dashLock > 0) {
                if (executionDash || !TryConsumeAirDash()) {
                    return false;
                }
                airDash = true;
                dashLock = 0;
            }
            else {
                airDash = !executionDash && TryConsumeAirDash();
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
                if (Mei.PaperEffigy && OniMeiPaperEffigy.AnyOwned(Player)) {
                    dashCost *= OniMeiCombat.PaperEffigyDashCostMul;
                }
                if (Vigor < dashCost - 0.01f) {
                    OniTalismanHud.NotifyVigorDenied();
                    //气不够就把刚吃掉的空中额度还回去，别让玩家白丢一次
                    if (airDash) {
                        airDashCharge = true;
                    }
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
            float maxDash = OnikiriOverride.GetFlashStepMaxDistance(item);
            Vector2 aim = Main.MouseWorld - Player.Center;
            ClampAimToMax(ref aim, maxDash);
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
            IOniComboController combo = OniBladeOccupancy.FindComboController(Player);
            bool interruptCombo = combo != null
                && combo.BeginFlashStepInterrupt(aim, out interruptRotation);
            dashLock = Math.Max(DashRefireLockTicks
                , OniFlashStep.CalculateControlFrames(distance, interruptCombo));
            if (Mei.StickyBind) {
                //滞樋自黏负担:再触发锁加帧(节奏税),不再用落地半速的泥地感
                dashLock += OniMeiCombat.StickyBindDashLockTicks;
                OniMeiStrikes.SpawnStickyDashDrag(Player, aim);
            }
            if (Mei.WindGroove) {
                OniMeiStrikes.SpawnWindGrooveDash(Player, aim);
            }

            Projectile dash = OniFlashStep.Fire(Player, aim
                , (int)(state.WeaponDamage * DashDamageMul * Mei.FlashMarkDamageMul)
                , state.WeaponKnockback, distance, executionDash: executionDash
                , interruptCombo: interruptCombo, interruptRotation: interruptRotation
                , source: Player.GetSource_ItemUse(item), baseWeaponDamage: state.WeaponDamage);
            if (dash == null) {
                dashLock = 0;
                if (airDash) {
                    airDashCharge = true;
                }
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
            float maxDash = FlashStepMaxDistance;
            ClampAimToMax(ref aim, maxDash);
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

        private Vector2 CaptureRelativeCursorAim(bool clampToMaxRange) {
            Vector2 aim = Main.MouseWorld - Player.Center;
            if (clampToMaxRange) {
                ClampAimToMax(ref aim, FlashStepMaxDistance);
            }
            if (aim.LengthSquared() < 1f) {
                aim = Vector2.UnitX * Player.direction;
            }
            return aim;
        }

        private float FlashStepMaxDistance
            => OnikiriOverride.GetFlashStepMaxDistance(Player.GetItem());

        private static void ClampAimToMax(ref Vector2 aim, float maxDistance) {
            float length = aim.Length();
            if (length > maxDistance && maxDistance > 0f) {
                aim *= maxDistance / length;
            }
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
                        > FlashStepMaxDistance + ExecutionCursorRangeSlack) {
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
                if (npc == null || DistanceToHitbox(npc, Player.Center) > FlashStepMaxDistance) {
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
                    || DistanceToHitbox(npc, Player.Center) > FlashStepMaxDistance) {
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

        /// <summary>樱流成立的世界条件:领域稳定在表世界(开域中、翻面中、里世界都不算)</summary>
        private bool StableOmote {
            get {
                OniDomainPlayer domain = Player.GetModPlayer<OniDomainPlayer>();
                return domain.Phase == OniDomainPhase.Omote && !domain.WorldIsUra;
            }
        }

        /// <summary>
        /// 疾走衔樱流,<see cref="OniFlashStep"/> 停止帧(owner);
        /// 需表世界+最低气力,失败静默(疾走照常收势)
        /// </summary>
        internal bool TryChainSakuraFlight(Vector2 direction, IEntitySource source) {
            if (Player.whoAmI != Main.myPlayer || Player.mount?.Active == true
                || !SakuraFlightInputHeld
                || executionDashQueued || executionTierInFlight != ExecutionTier.None) {
                return false;
            }
            if (!StableOmote || Vigor < SakuraMinVigor - 0.01f) {
                return false;
            }
            return StartSakuraFlight(direction, source, seamless: true);
        }

        /// <summary>
        /// 化樱起飞的共用发起路径(owner):气力换航时,余气越足航程越长;
        /// 起飞即作废旧追斩窗,落地(<see cref="OniSakuraFlight"/> 交还帧)会开新窗
        /// </summary>
        private bool StartSakuraFlight(Vector2 direction, IEntitySource source, bool seamless) {
            //上一次飞行的控制器(含余晖期)未消亡则拒绝:模块每玩家仅一个,拿旧实例不算起飞成功
            if (OniSakuraFlight.AnyFor(Player.whoAmI)) {
                return false;
            }
            int flightFrames = (int)(Vigor / (SakuraDrainPerTick * Mei.SakuraDrainMul));
            if (OniSakuraFlight.Fire(Player, direction, SakuraFlightSpeed,
                flightFrames, source, seamless) == null) {
                return false;
            }
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
                //任何收尾（松手/气尽/离表/自然到程）都在此断掉雨程连续计数
                DeedTracker.EndSakuraFlight();
                return;
            }
            vigorRegenDelay = Math.Max(vigorRegenDelay, VigorRegenDelayTicks + Mei.ExtraRegenDelayTicks);
            if (!SakuraFlightInputHeld || Vigor <= 0.01f || !StableOmote) {
                OniSakuraFlight.RequestStop(Player);
                return;
            }
            if (!advanceTime) {
                return;
            }
            Vigor = Math.Max(0f, Vigor - SakuraDrainPerTick * Mei.SakuraDrainMul);
            OniMeiDeedEvents.NotifySakuraTick(Player);
            TryDripInkRain();
        }

        /// <summary>雨樋：樱流沿途甩墨——航线走过哪儿，雨就下在哪儿</summary>
        private void TryDripInkRain() {
            if (!Mei.InkRain || Player.whoAmI != Main.myPlayer
                || scaledTime % OniMeiCombat.InkRainDripInterval != 0) {
                return;
            }
            ShootState state = Player.GetShootState();
            int damage = Math.Max(1, (int)(state.WeaponDamage * OniMeiCombat.InkRainDamageMul));
            OniMeiInkRain.Drip(Player, Player.Center, Player.velocity, damage,
                Player.GetSource_ItemUse(Player.GetItem()));
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
            bool sakura = StableOmote;
            bool synced = zanshinHasMarks && zanshinJudgeCountdown <= 0
                && zanshinJudgeCountdown >= -ZanshinSyncSlackTicks;
            Projectile zanshin = OniZanshinSlash.Fire(Player, aim
                , (int)(state.WeaponDamage * ZanshinDamageMul), state.WeaponKnockback
                , sakura, synced, Player.GetSource_ItemUse(item), state.WeaponDamage);
            if (zanshin == null) {
                return false;
            }
            OniBladeOccupancy.FindComboController(Player)?.ConsumeZanshinInput();
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
        internal void OnZanshinHit(NPC target, bool grantResources, in OniMeiCombatProfile profile) {
            OnPrimaryBladeHit(target, in profile);
            if (GuiltLayers > 0) {
                GuiltLayers = 0;
            }
            if (!grantResources) {
                return;
            }
            Tutorial.OnikiriTutorialEvents.FireZanshinHit(target);
            Stance = Math.Min(StanceMax,
                Stance + StancePerZanshinSlash * profile.StanceGainMul * ResolveSandGardenStanceMul(in profile));
            if (profile.ZanshinHitVigorBonus > 0f) {
                Vigor = Math.Min(VigorMaxCurrent, Vigor + profile.ZanshinHitVigorBonus);
                if (profile.BloodGroove) {
                    OniMeiStrikes.SpawnBloodBackflow(Player, target);
                    NotifyBloodBackflow();
                }
            }
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
            TrySpawnEmberField(focus, state.WeaponDamage, finale);
            //千手：定格期多浮六手同斩；代价是气清零并久不能疾走
            if (Mei.SenjuArms) {
                OniMeiSenjuArm.FireVolley(Player, state.WeaponDamage, finale.GetSource_FromAI());
                Vigor = 0f;
                vigorRegenDelay = Math.Max(vigorRegenDelay, OniMeiCombat.SenjuRecoverTicks);
                dashLock = Math.Max(dashLock, OniMeiCombat.SenjuRecoverTicks);
            }
            OniMeiDeedEvents.NotifyExecutionSpent(Player);
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
            if (Player.whoAmI != Main.myPlayer
                || Tutorial.OnikiriTutorialFlow.TryGetRequiredDismemberTarget(Player, out _)
                || executionAnnihilateWindow <= 0) {
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
            if (Tutorial.OnikiriTutorialFlow.TryGetRequiredDismemberTarget(Player, out _)) {
                executionAnnihilatePending = false;
                return;
            }
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
                , source: Player.GetSource_ItemUse(item), baseWeaponDamage: state.WeaponDamage);
            if (annihilate == null) {
                FailExecutionFollowup();
                return false;
            }

            Stance = Math.Max(0f, Stance - AnnihilateCost);
            ClearExecutionFollowup();
            OniBladeOccupancy.FindComboController(Player)?.ConsumeZanshinInput();
            OniMeiDeedEvents.NotifyExecutionSpent(Player);
            IgniteKurikara();
            Vector2 emberAt = Player.Center + aim * 120f;
            TrySpawnEmberField(emberAt, state.WeaponDamage, annihilate);
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
        private void TrySpawnEmberField(Vector2 at, int weaponDamage, Projectile parent) {
            OniMeiActionContext context = OniMeiActionContext.Get(parent);
            if (context?.HasSnapshot != true || !context.Profile.EmberField) {
                return;
            }
            int dmg = Math.Max(1, (int)(context.BaseWeaponDamage * OniMeiCombat.EmberDamageMul));
            OniMeiGroundBurn.TrySpawnOrRefresh(Player, at, dmg, OniMeiCombat.EmberLifeTicks
                , OniMeiCombat.EmberScale, OniMeiBurnStyle.Ember, parent);
        }

        /// <summary>倶利伽罗:处决消费架势后点燃雕纹,窗口内完成五段连斩即回环</summary>
        private void IgniteKurikara() {
            if (!Mei.DragonfireLoop) {
                return;
            }
            KurikaraWindow = KurikaraWindowTicks;
            kurikaraCharges = 3;
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
            bool tutorialPractice = Tutorial.OnikiriTutorialFlow.TryGetRequiredDismemberTarget(
                Player, out NPC requiredTarget);
            //演出或反噬僵直中不受理:裂成两半的人拔不了刀
            if (Player.ownedProjectileCounts[ModContent.ProjectileType<OniSeverStrike>()] > 0
                || OniPlayerDismember.IsLocked(Player)) {
                return tutorialPractice;
            }
            if (tutorialPractice && !Tutorial.OnikiriTutorialFlow.TryConsumeDismemberInput(Player)) {
                return true;
            }
            //肢解只在里世界成立;表世界左键就是普攻
            OniDomainPlayer domain = Player.GetModPlayer<OniDomainPlayer>();
            if (domain.Phase != OniDomainPhase.Ura || !domain.WorldIsUra) {
                if (tutorialPractice) {
                    Tutorial.OnikiriTutorialFlow.NotifyDismemberMiss(Player);
                    return true;
                }
                return false;
            }

            Vector2 mouse = Main.MouseWorld;
            IEntitySource source = Player.GetSource_ItemUse(item);

            if (tutorialPractice) {
                if (requiredTarget?.active == true
                    && DistanceToHitbox(requiredTarget, mouse) <= DirectPickPad
                    && TryDirectDismember(item, requiredTarget, source)) {
                    return true;
                }
                Tutorial.OnikiriTutorialFlow.NotifyDismemberMiss(Player);
                return true;
            }

            //一层:点在真身碰撞箱上 → 直接肢解,反噬上身
            NPC target = PickDismemberTarget(mouse, DirectPickPad);
            if (target != null && TryDirectDismember(item, target, source)) {
                return true;
            }

            //二层:点在媒介纸面上 → 点锚斩纸(落刀成功同样反噬上身)
            OmokageEntry paper = OniOmokage.PickEntryNear(mouse, PaperMagnetPad);
            if (paper != null && Vector2.Distance(Player.Center, paper.AnchorCenter) <= DismemberRange) {
                GetDismemberStats(item, out int damage, out float knockback);
                //落刀点收拢进纸面有效范围,拔刀方向=玩家→落刀点
                Vector2 local = mouse - paper.AnchorCenter;
                local.X = MathHelper.Clamp(local.X, -paper.PaperHalf.X * 0.4f, paper.PaperHalf.X * 0.4f);
                local.Y = MathHelper.Clamp(local.Y, -paper.PaperHalf.Y * 0.4f, paper.PaperHalf.Y * 0.4f);
                Vector2 cutPoint = paper.AnchorCenter + local;
                OniSeverStrike.FireAtPoint(Player, cutPoint, AimAngleFrom(cutPoint), damage
                    , knockback, scale: OnikiriOverride.GetBladeScale(item), source: source,
                    omokageEntryId: paper.Id);
                CancelExecutionIntent(settleFollowup: false, force: true);
                return true;
            }

            return false;
        }

        /// <summary>显式目标的真身肢解入口</summary>
        internal bool TryDirectDismember(Item item, NPC target, IEntitySource source) {
            bool tutorialTarget = Tutorial.OnikiriTutorialTargetGlobal.IsTutorialTarget(
                target, out _, out _);
            if (Player.whoAmI != Main.myPlayer || item == null || !item.Alives()
                || item.type != ModContent.ItemType<OnikiriItem>() || !Player.HasItem(item.type)
                || target?.active != true || target.life <= 0
                || (!tutorialTarget && !target.CanBeChasedBy())
                || Player.ownedProjectileCounts[ModContent.ProjectileType<OniSeverStrike>()] > 0
                || OniPlayerDismember.IsLocked(Player)) {
                return false;
            }
            OniDomainPlayer domain = Player.GetModPlayer<OniDomainPlayer>();
            if (domain.Phase != OniDomainPhase.Ura || !domain.WorldIsUra
                || DistanceToHitbox(target, Player.Center) > DismemberRange
                || !Tutorial.OnikiriTutorialTargetGlobal.CanPlayerDismember(target, Player)) {
                return false;
            }

            GetDismemberStats(item, out int damage, out float knockback);
            Projectile strike = OniSeverStrike.Fire(Player, target, AimAngleFrom(target.Center), damage,
                knockback, OnikiriOverride.GetBladeScale(item), source);
            if (strike?.active != true) {
                return false;
            }
            CancelExecutionIntent(settleFollowup: false, force: true);
            return true;
        }

        /// <summary>教程助手从背包取刀并演示真身肢解</summary>
        internal bool TryTutorialDismember(NPC target) {
            if (!Tutorial.OnikiriTutorialTargetGlobal.IsTutorialTarget(target,
                out int owner, out _) || owner != Player.whoAmI
                || OniBladeOccupancy.FindComboController(Player) != null
                || OniBladeOccupancy.AnyHardOccupant(Player)
                || OniBladeOccupancy.BladeReserved(Player)) {
                return false;
            }
            Item item = Player.inventory.FirstOrDefault(candidate
                => candidate?.type == ModContent.ItemType<OnikiriItem>() && candidate.Alives());
            return item != null && TryDirectDismember(item, target, Player.GetSource_ItemUse(item));
        }

        private void GetDismemberStats(Item item, out int damage, out float knockback) {
            if (ReferenceEquals(item, Player.GetItem())) {
                ShootState state = Player.GetShootState();
                damage = (int)(state.WeaponDamage * DismemberDamageMul);
                knockback = state.WeaponKnockback;
                return;
            }
            damage = (int)(Player.GetWeaponDamage(item) * DismemberDamageMul);
            knockback = Player.GetWeaponKnockback(item);
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
                if (!Tutorial.OnikiriTutorialTargetGlobal.CanPlayerDismember(npc, Player)) {
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
        internal void OnComboHit(NPC target, bool grantResources,
            in OniMeiCombatProfile profile, bool tideOnBeat) {
            OnPrimaryBladeHit(target, in profile);
            if (!grantResources) {
                return;
            }
            Vigor = Math.Min(VigorMaxCurrent, Vigor + VigorPerComboBeat + profile.ComboHitVigorBonus);
            Stance = Math.Min(StanceMax,
                Stance + StancePerComboBeat * profile.StanceGainMul * ResolveSandGardenStanceMul(in profile));
            if (profile.BloodGroove && profile.ComboHitVigorBonus > 0f) {
                OniMeiStrikes.SpawnBloodBackflow(Player, target);
                NotifyBloodBackflow();
            }
            TryApplyTideOnComboHit(grantResources, in profile, tideOnBeat);
        }

        /// <summary>所有 Primary 直接刀击共用的主体命中、脱战与滞缚入口。</summary>
        internal void OnPrimaryBladeHit(NPC target, in OniMeiCombatProfile profile) {
            RecordHit(target);
            lastDirectBladeHitTick = scaledTime;
            TryApplyStickyBind(target, in profile);
            TryPlantSilkAnchor(target, in profile);
            TryMirrorEcho(target, in profile);
            TryHannyaOnHit(target, in profile, null, Player.GetWeaponDamage(Player.GetItem()));
            OniMeiDeedEvents.NotifyBladeHit(Player);
        }

        /// <summary>鏡樋：你这一刀落下了，镜子跟着落一刀，随后碎</summary>
        private void TryMirrorEcho(NPC target, in OniMeiCombatProfile profile) {
            if (!profile.MirrorEcho || Player.whoAmI != Main.myPlayer || target == null) {
                return;
            }
            float aim = (target.Center - Player.Center).ToRotation();
            if (float.IsNaN(aim)) {
                aim = Player.direction > 0 ? 0f : MathHelper.Pi;
            }
            OniMeiMirrorStand.TryEcho(Player, aim, Player.GetWeaponKnockback(Player.GetItem()), 1f);
        }

        /// <summary>疾走穿身即格挡:每次疾走仅首次格挡固定蓄势,所有目标都记入命中记忆</summary>
        internal void OnDashParry(NPC npc, bool grantResources, in OniMeiCombatProfile profile) {
            RecordHit(npc);
            if (grantResources) {
                Stance = Math.Min(StanceMax, Stance + StancePerDashParry * profile.StanceGainMul);
            }
            if (profile.NumbCounter) {
                OniMeiCombat.TryApplyNumbCounter(Player, npc, in profile);
            }
        }

        /// <summary>疾走自然结束：武装默切默杀窗。开窗一声低吟+身周墨纱罩下</summary>
        internal void ArmSilentKillFromDash(in OniMeiCombatProfile profile) {
            if (Player.whoAmI != Main.myPlayer || !profile.SilentKill) {
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

        internal void ConsumeSilentKillOnDashStart(in OniMeiCombatProfile profile) {
            if (Player.whoAmI == Main.myPlayer && profile.SilentKill && silentKillWindow > 0) {
                ConsumeSilentCore();
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
            if (silentKillWindow <= 0) {
                return false;
            }
            silentKillWindow = 0;
            OniMeiStrikes.SpawnSilentConsumeFX(Player);
            return true;
        }

        /// <summary>止足立定消费核:清充+字形一闪反馈;未装/未就绪 false</summary>
        private bool ConsumePlantedCore() {
            if (!plantedReady) {
                return false;
            }
            plantedReady = false;
            plantedCharge = 0;
            OniMeiStrikes.SpawnPlantedConsumeFX(Player);
            return true;
        }

        internal float ArmMeiAction(in OniMeiCombatProfile profile,
            bool allowSilent, bool allowPlanted) {
            float multiplier = 1f;
            if (allowSilent && profile.SilentKill && ConsumeSilentCore()) {
                multiplier *= OniMeiCombat.SilentKillHitMul;
            }
            if (allowPlanted && profile.PlantedStep && ConsumePlantedCore()) {
                multiplier *= OniMeiCombat.PlantedStepHitMul;
            }
            return Math.Min(multiplier, OniMeiCombat.SilentPlantedSoftCap);
        }

        /// <summary>谢樋：击杀了结溅剪落（门闩防连环；cleave 杀不调此）</summary>
        internal void TryPetalPruneOnKill(NPC killed, int weaponDamage, float knockback,
            Projectile sourceProjectile, in OniMeiCombatProfile profile) {
            if (!profile.PetalPrune || petalPruneCooldown > 0 || killed == null) {
                return;
            }
            NPC killedRoot = OniMeiCombat.ResolveEffectRoot(killed) ?? killed;
            int killedRootId = killedRoot.whoAmI;
            Vector2 origin = killed.Center;
            float radius = OniMeiCombat.PetalPruneRadius;
            float radiusSq = radius * radius;
            NPC pruneTarget = null;
            float bestLifeRatio = float.MaxValue;
            float bestDistanceSq = float.MaxValue;
            HashSet<int> visitedRoots = [];
            foreach (NPC npc in Main.ActiveNPCs) {
                NPC candidate = OniMeiCombat.ResolveEffectRoot(npc);
                if (candidate == null || !candidate.active || candidate.friendly
                    || !candidate.CanBeChasedBy() || candidate.whoAmI == killedRootId
                    || candidate.lifeMax <= 0 || !visitedRoots.Add(candidate.whoAmI)) {
                    continue;
                }
                float distanceSq = candidate.DistanceSQ(origin);
                float lifeRatio = candidate.life / (float)candidate.lifeMax;
                if (distanceSq > radiusSq || lifeRatio > 0.50f) {
                    continue;
                }
                bool better = pruneTarget == null
                    || lifeRatio < bestLifeRatio - 0.0001f
                    || Math.Abs(lifeRatio - bestLifeRatio) <= 0.0001f
                        && (distanceSq < bestDistanceSq - 0.01f
                            || Math.Abs(distanceSq - bestDistanceSq) <= 0.01f
                                && candidate.whoAmI < pruneTarget.whoAmI);
                if (!better) {
                    continue;
                }
                pruneTarget = candidate;
                bestLifeRatio = lifeRatio;
                bestDistanceSq = distanceSq;
            }
            if (pruneTarget == null) {
                return;
            }
            petalPruneCooldown = OniMeiCombat.PetalPruneCooldownTicks;
            float aim = (pruneTarget.Center - origin).ToRotation();
            if (float.IsNaN(aim)) {
                aim = Player.direction > 0 ? 0f : MathHelper.Pi;
            }
            OniMeiStrikes.FirePetalPrune(Player, pruneTarget, origin, aim,
                Math.Max(1, weaponDamage), knockback,
                sourceProjectile?.GetSource_FromAI());
            NotifyPetalPruneEngraved();
        }

        /// <summary>谢樋：空残心微扣气</summary>
        internal void NotifyEmptyZanshin(in OniMeiCombatProfile profile) {
            if (!profile.PetalPrune) {
                return;
            }
            Vigor = Math.Max(0f, Vigor - OniMeiCombat.PetalPruneEmptyZanshinVigor);
        }

        /// <summary>潮拍：当前是否合潮</summary>
        internal bool IsTideOnBeatNow
            => Mei.TideBeat && OniMeiCombat.IsTideOnBeat(tidePhase);

        internal int TidePhaseTicks => tidePhase;

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
        internal void TryApplyTideOnComboHit(bool grantResources,
            in OniMeiCombatProfile profile, bool tideOnBeat) {
            if (!profile.TideBeat || !grantResources || !tideOnBeat) {
                return;
            }
            Vigor = Math.Min(VigorMaxCurrent, Vigor + OniMeiCombat.TideOnBeatVigor);
            engraveHiPulse = 1f;
            OniMeiStrikes.SpawnTideBeatRipple(Player);
        }

        internal float BuildMeiHitMultiplier(NPC target, in OniMeiCombatProfile profile,
            uint actionSerial, bool allowPlanted, bool allowIron, bool zanshin,
            float armedConditionMul = 1f, bool tideOnBeatSnapshot = false,
            bool combo = false) {
            float multiplier = Math.Max(1f, armedConditionMul);
            if (allowIron && profile.IronSever && CWRLoad.NPCValue.ISTheofSteel(target)) {
                multiplier *= OniMeiCombat.IronSeverSteelHitMul;
                //刮擦方向取出刀方向,火舌与卷屑才顺着刃走
                OniMeiStrikes.SpawnIronSeverFX(target, target.Center - Player.Center);
            }
            if (profile.HollowRoar) {
                multiplier *= ResolveHollowActionMultiplier(target, actionSerial);
            }
            if (profile.TideBeat) {
                if (zanshin && tideOnBeatSnapshot) {
                    multiplier *= OniMeiCombat.TideZanshinHitMul;
                }
                else if (combo && !tideOnBeatSnapshot) {
                    multiplier *= OniMeiCombat.TideOffBeatHitMul;
                }
            }
            //表影受创：斩过纸的那一段，这个目标挨刀更疼
            multiplier *= OniMeiCombat.BuildPaperBrandMul(target);
            //般若：翻成鬼面的那段刀更重，加深走命中侧而不是面板
            if (profile.HannyaMask && HannyaMasked) {
                multiplier *= OniMeiCombat.HannyaHitMul;
            }
            return multiplier;
        }

        private float ResolveHollowActionMultiplier(NPC target, uint actionSerial) {
            if (target == null || target.DistanceSQ(Player.Center)
                > OniMeiCombat.HollowNearRadius * OniMeiCombat.HollowNearRadius) {
                return 1f;
            }
            if (actionSerial != 0
                && hollowActionMultipliers.TryGetValue(actionSerial, out float cached)) {
                return cached;
            }

            RecordHollowDenseAction(actionSerial);
            float multiplier = 1f;
            if (hollowFocusLossTicks > 0) {
                multiplier = OniMeiCombat.HollowFocusLossHitMul;
            }
            else if (hollowApproachArmed) {
                hollowApproachArmed = false;
                multiplier = OniMeiCombat.HollowApproachHitMul;
            }
            if (actionSerial != 0) {
                hollowActionMultipliers[actionSerial] = multiplier;
                hollowActionOrder.Enqueue(actionSerial);
                while (hollowActionOrder.Count > 64) {
                    hollowActionMultipliers.Remove(hollowActionOrder.Dequeue());
                }
            }
            return multiplier;
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
            return scaledTime - lastDirectBladeHitTick > OniMeiCombat.HollowRoarColdTicks;
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

        private void RecordHollowDenseAction(uint actionSerial) {
            if (actionSerial != 0 && hollowLastActionSerial == actionSerial) {
                return;
            }
            hollowLastActionSerial = actionSerial;
            int now = scaledTime;
            if (now - hollowDenseWindowStart > OniMeiCombat.HollowFocusLossWindowTicks) {
                hollowDenseWindowStart = now;
                hollowDenseHits = 0;
            }
            hollowDenseHits++;
            if (hollowDenseHits >= OniMeiCombat.HollowFocusLossHitNeed) {
                hollowFocusLossTicks = OniMeiCombat.HollowFocusLossDurationTicks;
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
            if (KurikaraWindow <= 0 || kurikaraCharges <= 0) {
                return false;
            }
            kurikaraCharges--;
            if (kurikaraCharges <= 0) {
                KurikaraWindow = 0;
            }
            return true;
        }

        /// <summary>髭切断首击杀返势(每次招式至多一次,OniMeiCombat 把关)</summary>
        internal void GrantExecuteRefund() {
            Stance = Math.Min(StanceMax, Stance + OniMeiCombat.ExecuteKillStanceRefund);
        }

        internal bool TryClaimExecuteRefund(uint actionSerial) {
            if (actionSerial == 0) {
                return true;
            }
            if (!executeRefundedActions.Add(actionSerial)) {
                return false;
            }
            executeRefundOrder.Enqueue(actionSerial);
            while (executeRefundOrder.Count > 64) {
                executeRefundedActions.Remove(executeRefundOrder.Dequeue());
            }
            return true;
        }

        /// <summary>不动护窗口:连段后两重拍/残心/处决演出中</summary>
        private bool IsInCommittedAction() {
            if (Player.ownedProjectileCounts[ModContent.ProjectileType<OniZanshinSlash>()] > 0
                || Player.ownedProjectileCounts[ModContent.ProjectileType<OniAnnihilate>()] > 0
                || Player.ownedProjectileCounts[ModContent.ProjectileType<OniFinaleSlash>()] > 0) {
                return true;
            }
            return OniBladeOccupancy.FindComboController(Player)?.InCommittedBeats ?? false;
        }

        private static bool TryGetHostileDamageSource(PlayerDeathReason damageSource,
            out Entity causing) {
            causing = null;
            if (!damageSource.TryGetCausingEntity(out Entity resolved)) {
                return false;
            }
            bool hostile = resolved switch {
                Projectile projectile => projectile.active && projectile.hostile && projectile.damage > 0,
                NPC npc => npc.active && !npc.friendly && npc.damage > 0,
                _ => false,
            };
            if (hostile) {
                causing = resolved;
            }
            return hostile;
        }

        public override bool FreeDodge(Player.HurtInfo info) {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer || !Mei.FalseBody
                || OniPlayerDismember.SelfHurtResolving
                || !TryGetHostileDamageSource(info.DamageSource, out _)) {
                return false;
            }
            return OniMeiFalseBody.TryConsumeOwned(Player);
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
            if (Math.Abs(Mei.IncomingDamageMul - 1f) > 0.001f) {
                modifiers.FinalDamage *= Mei.IncomingDamageMul;
            }
            //般若：鬼面越勇越脆，这一档只在翻面期收
            if (Mei.HannyaMask && HannyaMasked) {
                modifiers.FinalDamage *= OniMeiCombat.HannyaIncomingMul;
            }
            if (Mei.FalseBody && OniMeiFalseBody.AnyOwned(Player)) {
                modifiers.FinalDamage *= OniMeiCombat.FalseBodyIncomingMul;
            }
            if (falseBodyVacuumTicks > 0) {
                modifiers.FinalDamage *= OniMeiCombat.FalseBodyVacuumIncomingMul;
            }
            bool hostileSource = TryGetHostileDamageSource(modifiers.DamageSource, out Entity causing);
            bool falseBodyWillDodge = hostileSource && Mei.FalseBody
                && OniMeiFalseBody.AnyOwned(Player);
            if (Mei.StanceGuard && fudoGuardCooldown <= 0
                && Stance >= FudoGuardStanceCost - 0.01f
                && !OniPlayerDismember.IsLocked(Player)
                && IsInCommittedAction() && hostileSource && !falseBodyWillDodge) {
                Stance -= FudoGuardStanceCost;
                fudoGuardCooldown = FudoGuardCooldownTicks;
                modifiers.FinalDamage *= 1f - FudoGuardDamageCut;
                modifiers.Knockback *= 0.25f;
                OniMeiStrikes.SpawnFudoGuard(Player);
                OniTalismanHud.NotifyStanceGuard();
            }
            if (Mei.NumbCounter && numbGuardCooldown <= 0
                && Stance >= NumbGuardStanceCost - 0.01f
                && IsInCommittedAction() && !falseBodyWillDodge
                && causing is NPC numbSource && numbSource.active && !numbSource.friendly) {
                Stance -= NumbGuardStanceCost;
                numbGuardCooldown = NumbGuardCooldownTicks;
                modifiers.FinalDamage *= 1f - NumbGuardDamageCut;
                OniMeiCombat.TryApplyNumbCounter(Player, numbSource, in Mei);
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
            if (!Mei.QuellProjectiles) {
                return;
            }
            modifiers.FinalDamage *= OniMeiCombat.QuellProjectileDamageMul;
            modifiers.Knockback *= OniMeiCombat.QuellProjectileKnockbackMul;
            //离散的「镇」:折扣是连续的,演出必须是一次事件,否则玩家永远看不见这个铭
            Vector2 incoming = proj.velocity.LengthSquared() > 0.01f
                ? proj.velocity
                : Player.Center - proj.Center;
            Vector2 contact = Vector2.Lerp(proj.Center, Player.Center, 0.55f);
            NotifyQuellEngraved();
            OniMeiStrikes.SpawnQuellStruck(Player, contact, incoming);
        }

        /// <summary>刀縁账本：挨了一记就断掉静止与立定两条连续条件（肢解反噬不算敌手打的）</summary>
        public override void OnHurt(Player.HurtInfo info) {
            if (!OniPlayerDismember.SelfHurtResolving) {
                OniMeiDeedEvents.NotifyHurt(Player);
            }
        }

        /// <summary>假身碎裂回调：开真空窗</summary>
        internal void OnFalseBodyShattered() {
            falseBodyVacuumTicks = OniMeiCombat.FalseBodyVacuumTicks;
        }

        internal bool TryArmFalseBody(Vector2 position, float bladeRotation, int bladeFacing,
            in OniMeiCombatProfile profile) {
            if (!profile.FalseBody || falseBodyRearmTicks > 0
                || OniMeiFalseBody.AnyOwned(Player)) {
                return false;
            }
            OniMeiFalseBody.Fire(Player, position, bladeRotation, bladeFacing);
            falseBodyRearmTicks = 240;
            return true;
        }

        //==================== 蜘蛛切 墨丝 ====================

        /// <summary>丝锚余寿推进：过期即掉一枚(网织不成就白费一记)</summary>
        private void TickSilkAnchors() {
            if (silkSnareCooldown > 0) {
                silkSnareCooldown--;
            }
            if (silkLastRootCooldown > 0 && --silkLastRootCooldown <= 0) {
                silkLastRootId = -1;
            }
            if (!Mei.SilkSnare) {
                silkAnchors.Clear();
                return;
            }
            for (int i = silkAnchors.Count - 1; i >= 0; i--) {
                SilkAnchor anchor = silkAnchors[i];
                if (--anchor.Life <= 0) {
                    silkAnchors.RemoveAt(i);
                    SpawnSilkAnchorExpire(anchor.Position);
                    continue;
                }
                silkAnchors[i] = anchor;
                if (!Main.dedServ && anchor.Life % 7 == 0) {
                    //锚在世：一小簇湿墨钉在原地，读得出"网还差几枚"
                    PRTLoader.NewParticle<PRT_OniInkDrop>(
                        anchor.Position + Main.rand.NextVector2Circular(6f, 6f),
                        Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.9f),
                        new Color(52, 14, 20), Main.rand.NextFloat(0.12f, 0.20f))
                        ?.Configure(Main.rand.Next(14, 22));
                }
            }
        }

        /// <summary>在场丝锚数(刀身层读数用)</summary>
        internal int SilkAnchorCount => silkAnchors.Count;

        /// <summary>
        /// 墨丝：直接刀击钉一枚丝锚。同一主体短冷却、与已有锚过近都不算新锚；
        /// 满三枚即当场闭网并清空。owner 端调用
        /// </summary>
        private void TryPlantSilkAnchor(NPC target, in OniMeiCombatProfile profile) {
            if (!profile.SilkSnare || Player.whoAmI != Main.myPlayer
                || target == null || !target.active || silkSnareCooldown > 0) {
                return;
            }
            NPC root = OniMeiCombat.ResolveEffectRoot(target) ?? target;
            if (root.whoAmI == silkLastRootId) {
                return;
            }
            Vector2 at = target.Center;
            float minSpacingSq = OniMeiCombat.SilkAnchorMinSpacing * OniMeiCombat.SilkAnchorMinSpacing;
            foreach (SilkAnchor existing in silkAnchors) {
                if (Vector2.DistanceSquared(existing.Position, at) < minSpacingSq) {
                    return;
                }
            }

            silkAnchors.Add(new SilkAnchor { Position = at, Life = OniMeiCombat.SilkAnchorLifeTicks });
            silkLastRootId = root.whoAmI;
            silkLastRootCooldown = OniMeiCombat.SilkAnchorSameRootCooldown;
            OniMeiStrikes.SpawnSilkAnchor(at, silkAnchors.Count);
            if (silkAnchors.Count < OniMeiCombat.SilkSnareAnchorNeed) {
                return;
            }
            CloseSilkSnare();
        }

        /// <summary>三锚闭网：一枚弹幕持整张三角网，收紧扫过即割</summary>
        private void CloseSilkSnare() {
            List<Vector2> points = new(silkAnchors.Count);
            foreach (SilkAnchor anchor in silkAnchors) {
                points.Add(anchor.Position);
            }
            silkAnchors.Clear();
            silkSnareCooldown = OniMeiCombat.SilkSnareCooldownTicks;
            silkLastRootId = -1;
            silkLastRootCooldown = 0;
            ShootState state = Player.GetShootState();
            OniMeiStrikes.FireSilkSnare(Player, points, state.WeaponDamage, state.WeaponKnockback);
        }

        /// <summary>丝锚白费：墨点无声散开，让"没织成"也有个交代</summary>
        private static void SpawnSilkAnchorExpire(Vector2 at) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_OniInkDrop>(at + Main.rand.NextVector2Circular(8f, 8f),
                    Main.rand.NextVector2Circular(0.8f, 0.5f) + Vector2.UnitY * 0.8f,
                    new Color(40, 12, 16), Main.rand.NextFloat(0.12f, 0.22f))
                    ?.Configure(Main.rand.Next(16, 24));
            }
        }

        //==================== 鬼丸 自斩 ====================

        /// <summary>
        /// 站定累计够久，刀就自己开始动。<br/>
        /// 判据与止足同口径（速度平方阈），但要求"手上确有刀"——刀飞出去的那段不再累计，
        /// 也不再放下一把，所以永远只有一把在外面
        /// </summary>
        private void TickSelfCut() {
            if (!Mei.SelfCut) {
                selfCutStillTicks = 0;
                selfCutInterval = 0;
                return;
            }
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            //刀不在手：既不累计也不再放，等它回来
            if (OniMeiSelfCut.AnyOwned(Player)) {
                return;
            }
            //动了就散：这是"站着不动"的铭，不是自动炮台
            if (Player.velocity.LengthSquared() > OniMeiCombat.PlantedSpeedSq
                || OniBladeOccupancy.AnyHardOccupant(Player)) {
                if (selfCutStillTicks >= OniMeiCombat.SelfCutArmTicks) {
                    SpawnSelfCutDisarmCue();
                }
                selfCutStillTicks = 0;
                selfCutInterval = 0;
                return;
            }

            if (selfCutStillTicks < OniMeiCombat.SelfCutArmTicks) {
                selfCutStillTicks++;
                if (selfCutStillTicks >= OniMeiCombat.SelfCutArmTicks) {
                    //待机就绪：刀在手里自己震一下，这是"它要动了"的预告
                    SpawnSelfCutArmCue();
                    selfCutInterval = OniMeiCombat.SelfCutIntervalTicks / 2;
                }
                return;
            }
            if (selfCutInterval > 0) {
                selfCutInterval--;
                //待机呼吸：刀缘一线白光沿刃爬
                if (!Main.dedServ && selfCutInterval % 18 == 0) {
                    PRTLoader.NewParticle<PRT_CrimsonSpark>(
                        Player.Center + Main.rand.NextVector2Circular(18f, 22f),
                        -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.1f),
                        new Color(255, 243, 226), Main.rand.NextFloat(0.12f, 0.20f))
                        ?.Configure(Main.rand.Next(14, 22), affectedByGravity: false);
                }
                return;
            }
            TryFireSelfCut();
        }

        /// <summary>放刀：气不够就只是空等，不静默扣账</summary>
        private void TryFireSelfCut() {
            if (Vigor < OniMeiCombat.SelfCutVigorCost - 0.01f) {
                return;
            }
            NPC target = FindSelfCutTarget();
            if (target == null) {
                return;
            }
            Item item = Player.GetItem();
            if (item == null || item.type != ModContent.ItemType<OnikiriItem>()) {
                return;
            }
            ShootState state = Player.GetShootState();
            if (OniMeiSelfCut.Fire(Player, target, state.WeaponDamage,
                Player.GetSource_ItemUse(item)) == null) {
                return;
            }
            Vigor -= OniMeiCombat.SelfCutVigorCost;
            selfCutInterval = OniMeiCombat.SelfCutIntervalTicks;
        }

        /// <summary>自斩索敌：范围内最近的可击目标，蠕虫归主体</summary>
        private NPC FindSelfCutTarget() {
            NPC best = null;
            float bestSq = OniMeiCombat.SelfCutRange * OniMeiCombat.SelfCutRange;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.friendly || !npc.CanBeChasedBy()) {
                    continue;
                }
                float distanceSq = npc.DistanceSQ(Player.Center);
                if (distanceSq < bestSq) {
                    bestSq = distanceSq;
                    best = npc;
                }
            }
            return best;
        }

        private void SpawnSelfCutArmCue() {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item35 with { Pitch = 0.65f, Volume = 0.30f }, Player.Center);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_CrimsonSpark>(
                    Player.Center + Main.rand.NextVector2Circular(20f, 24f),
                    Main.rand.NextVector2Circular(1.2f, 1.2f), new Color(232, 186, 110),
                    Main.rand.NextFloat(0.16f, 0.26f))
                    ?.Configure(Main.rand.Next(12, 18), affectedByGravity: false);
            }
        }

        /// <summary>待机被打断：刀"歇下去"的一声，让玩家知道刚才那档没了</summary>
        private void SpawnSelfCutDisarmCue() {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item35 with { Pitch = -0.45f, Volume = 0.22f }, Player.Center);
        }

        //==================== 空樋 浮身 ====================

        /// <summary>
        /// 离地就攒一次额外疾走，落地即还并沉底。<br/>
        /// 滞空是空中疾走的收尾拍：短暂悬住，给玩家一个"再打一拍"的落点
        /// </summary>
        private void TickAirGroove() {
            if (!Mei.AirGroove) {
                airDashCharge = false;
                airGrooveDryTicks = 0;
                airGrooveHover = 0;
                return;
            }
            bool grounded = Player.velocity.Y == 0f || Player.sliding
                || Player.mount?.Active == true;
            if (grounded) {
                if (airDashCharge || airGrooveHover > 0) {
                    //落地：把额外那次还回去，同时沉底一段不回气
                    airGrooveDryTicks = OniMeiCombat.AirGrooveLandingDryTicks;
                }
                airDashCharge = true;
                airGrooveHover = 0;
            }
            if (airGrooveDryTicks > 0) {
                airGrooveDryTicks--;
            }
            if (airGrooveHover > 0) {
                airGrooveHover--;
                //滞空：纵向速度按住，脚下浮一枚纸白环
                Player.velocity.Y *= 0.28f;
                Player.gravity = 0f;
                if (!Main.dedServ && airGrooveHover % 5 == 0) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    PRTLoader.NewParticle<PRT_CrimsonSpark>(
                        Player.Bottom + ang.ToRotationVector2() * new Vector2(20f, 6f),
                        ang.ToRotationVector2() * 0.6f, new Color(255, 243, 226),
                        Main.rand.NextFloat(0.12f, 0.20f))
                        ?.Configure(12, affectedByGravity: false);
                }
            }
        }

        /// <summary>空樋：这次疾走能不能吃掉那枚空中额度（吃掉即无视再触发锁）</summary>
        private bool TryConsumeAirDash() {
            if (!Mei.AirGroove || !airDashCharge) {
                return false;
            }
            bool grounded = Player.velocity.Y == 0f || Player.sliding;
            if (grounded) {
                return false;
            }
            airDashCharge = false;
            return true;
        }

        /// <summary>空樋：空中疾走收尾即滞空</summary>
        internal void OpenAirGrooveHover() {
            if (Mei.AirGroove && Player.velocity.Y != 0f) {
                airGrooveHover = OniMeiCombat.AirGrooveHoverTicks;
            }
        }

        //==================== 綴樋 缀痕 ====================

        /// <summary>墨痕引爆时报一个落点；同一次疾走的墨痕会落在同几帧里，攒齐再连缀</summary>
        internal void NotifyMarkDetonated(Vector2 at, int baseWeaponDamage,
            in OniMeiCombatProfile profile) {
            if (!profile.MarkStitch || Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (stitchPoints.Count < OniMeiInkThread.MaxAnchors) {
                stitchPoints.Add(at);
            }
            stitchDamage = Math.Max(stitchDamage, baseWeaponDamage);
            stitchGather = OniMeiCombat.MarkStitchGatherTicks;
        }

        /// <summary>收集窗一到期就连缀：两枚以上才成串，单枚只是白亏了那 30%</summary>
        private void TickMarkStitch() {
            if (stitchGather <= 0) {
                return;
            }
            if (--stitchGather > 0) {
                return;
            }
            if (stitchPoints.Count >= 2) {
                //按 X 排一遍，连出来的串不会自己打结
                stitchPoints.Sort((a, b) => a.X.CompareTo(b.X));
                int damage = Math.Max(1, (int)(stitchDamage * OniMeiCombat.MarkStitchDamageMul));
                OniMeiInkThread.Fire(Player, stitchPoints, OniMeiThreadStyle.Stitch,
                    damage, 2f, Player.GetSource_ItemUse(Player.GetItem()));
            }
            stitchPoints.Clear();
            stitchDamage = 0;
        }

        //==================== 梵鐘 一撞 ====================

        /// <summary>
        /// 满架势起自鸣，三秒不放终结就自己撞钟。<br/>
        /// 这三秒是玩家的选择窗：想留着终结就现在放，想换一圈控场就憋着
        /// </summary>
        private void TickBellToll() {
            if (!Mei.BellToll) {
                bellCharge = 0;
                return;
            }
            if (Stance < StanceMax - 0.01f) {
                bellCharge = 0;
                return;
            }
            if (++bellCharge < OniMeiCombat.BellChargeTicks) {
                //自鸣：越接近撞钟嗡得越紧，给个听得见的倒计时
                if (!Main.dedServ && bellCharge % 20 == 0) {
                    float t = bellCharge / (float)OniMeiCombat.BellChargeTicks;
                    SoundEngine.PlaySound(SoundID.Item52 with {
                        Pitch = -0.9f + t * 0.35f,
                        Volume = 0.10f + t * 0.14f,
                    }, Player.Center);
                }
                return;
            }
            bellCharge = 0;
            Toll();
        }

        /// <summary>撞钟：架势砍到一半，换一圈钟波</summary>
        private void Toll() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            Item item = Player.GetItem();
            if (item == null || item.type != ModContent.ItemType<OnikiriItem>()) {
                return;
            }
            ShootState state = Player.GetShootState();
            int damage = Math.Max(1, (int)(state.WeaponDamage * OniMeiCombat.BellWaveDamageMul));
            if (OniMeiBellWave.Fire(Player, Player.Center, damage, state.WeaponKnockback,
                Player.GetSource_ItemUse(item)) == null) {
                return;
            }
            Stance = Math.Min(Stance, OniMeiCombat.BellTollStanceLeft);
        }

        /// <summary>梵鐘自鸣进度 0..1，供刀身层与 HUD 读；未装或未满势为 0</summary>
        internal float BellChargeRatio => Mei.BellToll && OniMeiCombat.BellChargeTicks > 0
            ? MathHelper.Clamp(bellCharge / (float)OniMeiCombat.BellChargeTicks, 0f, 1f)
            : 0f;

        //==================== 般若 面变 ====================

        /// <summary>当前是否鬼面（生命跌破线）</summary>
        internal bool HannyaMasked => Mei.HannyaMask && Player.statLifeMax2 > 0
            && Player.statLife / (float)Player.statLifeMax2 <= OniMeiCombat.HannyaMaskThreshold;

        /// <summary>翻面那一帧给一记演出，别让"变强了"只写在数字里</summary>
        private void TickHannyaMask() {
            if (!Mei.HannyaMask) {
                hannyaWasMasked = false;
                hannyaHitCount = 0;
                return;
            }
            bool masked = HannyaMasked;
            if (masked != hannyaWasMasked) {
                hannyaWasMasked = masked;
                hannyaHitCount = 0;
                OniMeiStrikes.SpawnHannyaShift(Player, masked);
            }
            //鬼面期常态：面颊侧偶尔浮一缕血黑，读得出"现在是鬼"
            if (masked && !Main.dedServ && scaledTime % 11 == 0) {
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(
                    Player.Center + Main.rand.NextVector2Circular(16f, 22f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.8f), Color.White, 0.05f)
                    ?.Configure(Main.rand.Next(14, 22), new Color(96, 12, 20), new Color(16, 6, 10));
            }
        }

        /// <summary>鬼面命中：吸血 + 每三次浮一张咬合</summary>
        private void TryHannyaOnHit(NPC target, in OniMeiCombatProfile profile,
            Projectile sourceProjectile, int baseWeaponDamage) {
            if (!profile.HannyaMask || Player.whoAmI != Main.myPlayer
                || target == null || !HannyaMasked) {
                return;
            }
            int heal = Math.Max(1, (int)(Player.statLifeMax2 * OniMeiCombat.HannyaLifestealRatio));
            if (Player.statLife < Player.statLifeMax2) {
                Player.statLife = Math.Min(Player.statLifeMax2, Player.statLife + heal);
                Player.HealEffect(heal, false);
            }
            if (++hannyaHitCount < OniMeiCombat.HannyaBiteEvery) {
                return;
            }
            hannyaHitCount = 0;
            float aim = (target.Center - Player.Center).ToRotation();
            if (float.IsNaN(aim)) {
                aim = Player.direction > 0 ? 0f : MathHelper.Pi;
            }
            OniMeiStrikes.FireHannyaBite(Player, target.Center, aim, baseWeaponDamage,
                Player.GetWeaponKnockback(Player.GetItem()), sourceProjectile?.GetSource_FromAI());
        }

        //==================== 枯山水 砂纹 ====================

        /// <summary>站在自己耙的场里，架势涨得更快——守着它是有回报的</summary>
        private float ResolveSandGardenStanceMul(in OniMeiCombatProfile profile)
            => profile.SandGarden && OniMeiSandGarden.StandingInOwnGarden(Player)
                ? OniMeiCombat.SandGardenStanceBonus
                : 1f;

        /// <summary>立定就耙；耙成一场后走开也留在原地，同时只有一场</summary>
        private void TickSandGarden() {
            if (!Mei.SandGarden || Player.whoAmI != Main.myPlayer) {
                sandRakeTicks = 0;
                return;
            }
            if (Player.velocity.LengthSquared() > OniMeiCombat.PlantedSpeedSq) {
                sandRakeTicks = 0;
                return;
            }
            //场还在就不重复耙：省得站着不动一直刷新
            if (OniMeiSandGarden.StandingInOwnGarden(Player)) {
                sandRakeTicks = 0;
                return;
            }
            if (++sandRakeTicks < OniMeiCombat.SandGardenRakeTicks) {
                //耙纹将成：足边砂粒被推开
                if (!Main.dedServ && sandRakeTicks % 12 == 0) {
                    float side = Main.rand.NextBool() ? 1f : -1f;
                    PRTLoader.NewParticle<PRT_CrimsonSpark>(
                        Player.Bottom + Vector2.UnitX * side * Main.rand.NextFloat(6f, 22f),
                        Vector2.UnitX * side * Main.rand.NextFloat(0.4f, 1.1f),
                        new Color(214, 204, 188), Main.rand.NextFloat(0.08f, 0.14f))
                        ?.Configure(Main.rand.Next(14, 22), affectedByGravity: false);
                }
                return;
            }
            sandRakeTicks = 0;
            Item item = Player.GetItem();
            if (item == null || item.type != ModContent.ItemType<OnikiriItem>()) {
                return;
            }
            ShootState state = Player.GetShootState();
            OniMeiSandGarden.Rake_(Player, Player.Bottom,
                Math.Max(1, (int)(state.WeaponDamage * OniMeiCombat.SandGardenDamageMul)),
                Player.GetSource_ItemUse(item));
        }

        //==================== 雷切 斩雷 ====================

        /// <summary>
        /// 大招命中即引雷。雷暴天多落两道并加宽；晴天只落一道。<br/>
        /// 头顶有遮挡就落不下来——这条硬限制在弹幕侧探顶，玩家能从"没落雷"读出自己在洞里
        /// </summary>
        internal void TryCallThunder(NPC target, in OniMeiCombatProfile profile,
            int baseWeaponDamage, float knockback, Projectile sourceProjectile) {
            if (!profile.ThunderCall || Player.whoAmI != Main.myPlayer
                || target == null || thunderCooldown > 0) {
                return;
            }
            thunderCooldown = OniMeiCombat.ThunderCooldownTicks;
            bool storming = Main.raining && Math.Abs(Main.windSpeedCurrent) >= 0.4f;
            int bolts = storming ? OniMeiCombat.ThunderStormBolts : 1;
            int damage = Math.Max(1, (int)(baseWeaponDamage * OniMeiCombat.ThunderDamageMul));
            IEntitySource source = sourceProjectile?.GetSource_FromAI()
                ?? Player.GetSource_ItemUse(Player.GetItem());

            bool anyLanded = false;
            for (int i = 0; i < bolts; i++) {
                //多道时左右散开，读作"一片雷"而不是同一根画三遍
                float spread = bolts <= 1 ? 0f : (i - (bolts - 1) * 0.5f) * 86f;
                Vector2 at = target.Center + Vector2.UnitX * spread;
                if (OniMeiThunderColumn.TryStrike(Player, at, damage, knockback,
                    storming ? 1.25f : 1f, source)) {
                    anyLanded = true;
                }
            }
            if (!anyLanded) {
                //落不下来也要有交代：刃上憋着的那点电噼一声散掉
                OniMeiStrikes.SpawnThunderChoke(Player);
            }
        }

        //==================== 鵺切 落鵺 ====================

        /// <summary>落鵺收势：砸完这段时间不能疾走</summary>
        internal void LockDashForNueDive(int ticks) {
            nueDiveRecover = Math.Max(nueDiveRecover, ticks);
            dashLock = Math.Max(dashLock, ticks);
        }

        /// <summary>
        /// 空中第五拍改扑击：离地够高才成立，落地前不再有横甩巨弧。<br/>
        /// 由 <see cref="CrimsonRendSlash"/> 在第五拍出手前问一句，接管成功则本拍不走常规弧
        /// </summary>
        internal bool TryNueDive(in OniMeiCombatProfile profile, int baseWeaponDamage,
            float sizeMul, Projectile sourceProjectile) {
            if (!profile.NueDive || Player.whoAmI != Main.myPlayer
                || nueDiveRecover > 0 || !OniMeiNueDive.HasDiveRoom(Player)) {
                return false;
            }
            return OniMeiNueDive.Fire(Player, baseWeaponDamage, sizeMul,
                sourceProjectile?.GetSource_FromAI()) != null;
        }

        /// <summary>滞樋：授权命中叠「滞缚」(自实现墨锚阻尼，boss 减效)</summary>
        private void TryApplyStickyBind(NPC target, in OniMeiCombatProfile profile) {
            if (!profile.StickyBind || target == null || !target.active) {
                return;
            }
            NPC root = OniMeiCombat.ResolveEffectRoot(target);
            root?.AddBuff(ModContent.BuffType<OniBindDebuff>(), OniMeiCombat.StickyBindTargetSlowTicks);
            NotifyStickyBindEngraved();
        }

        /// <summary>闲樋：命中记忆在冷战窗口内无刷新则视为脱战</summary>
        private bool IsCombatCold() {
            return scaledTime - lastDirectBladeHitTick > OniMeiCombat.QuietBreathColdTicks;
        }

        //==================== 在世刀身铭刻层的活仪表 ====================

        /// <summary>刀身铭刻读数推进(owner 端，随缩放帧走)</summary>
        private void TickEngraveGauges() {
            engraveHiPulse *= 0.90f;
            if (engraveHiPulse < 0.01f) {
                engraveHiPulse = 0f;
            }
            engraveHiFill = MathHelper.Lerp(engraveHiFill, ResolveEngraveHiTarget(out float rate), rate);
            if (engraveHiFill < 0.004f) {
                engraveHiFill = 0f;
            }
            engraveHiPhase += ResolveEngraveHiPhaseStep();
            engraveHiPhase -= MathF.Floor(engraveHiPhase);
            engraveHoriLit = MathHelper.Lerp(engraveHoriLit, ResolveEngraveHoriLit(), 0.14f);
            TickQuietBreathShift();
        }

        /// <summary>正在居合疾走(风樋气势与焦樋起烬都跟这个走)</summary>
        private bool IsDashing()
            => Player.ownedProjectileCounts[ModContent.ProjectileType<OniFlashStep>()] > 0;

        /// <summary>
        /// 闲樋:脱战窗的开合各给一记进出演出。未装闲樋时只静默跟踪,
        /// 免得刚凿上闲樋就凭空吐一口息
        /// </summary>
        private void TickQuietBreathShift() {
            bool cold = IsCombatCold();
            if (!Mei.QuietBreath) {
                engraveQuietCold = cold;
                return;
            }
            if (cold == engraveQuietCold) {
                return;
            }
            engraveQuietCold = cold;
            engraveHiPulse = 1f;
            OniMeiStrikes.SpawnQuietBreathShift(Player, cold);
        }

        /// <summary>各樋位的槽内充盈目标与趋近速率；未装樋位排空</summary>
        private float ResolveEngraveHiTarget(out float rate) {
            //血樋：命中把血位顶满(NotifyBloodBackflow)，此后顺槽慢慢排空
            if (Mei.BloodGroove) {
                rate = 0.018f;
                return 0f;
            }
            //风樋：常态就有气流，疾走时吃满
            if (Mei.WindGroove) {
                rate = 0.12f;
                return IsDashing() ? 1f : 0.35f;
            }
            //焦樋：疾走点起余烬，停下后慢慢烧完
            if (Mei.ScorchTrail) {
                rate = IsDashing() ? 0.20f : 0.020f;
                return IsDashing() ? 1f : 0f;
            }
            //闲樋：脱战窗接上才起息，被自己一刀打断就压回去
            if (Mei.QuietBreath) {
                bool cold = IsCombatCold();
                rate = cold ? 0.025f : 0.16f;
                return cold ? 1f : 0f;
            }
            //滞樋：命中挂珠(NotifyStickyBindHit)，无命中则慢慢滴干
            if (Mei.StickyBind) {
                rate = 0.012f;
                return 0f;
            }
            //谢樋：击杀积瓣(NotifyPetalPrune)，久不了结则褪去
            if (Mei.PetalPrune) {
                rate = 0.010f;
                return 0f;
            }
            rate = 0.08f;
            return 0f;
        }

        /// <summary>樋内循环相位步进：各介质自有的流速</summary>
        private float ResolveEngraveHiPhaseStep() {
            if (Mei.WindGroove) {
                return IsDashing() ? 0.085f : 0.048f;
            }
            if (Mei.ScorchTrail) {
                return 0.009f;
            }
            if (Mei.StickyBind) {
                return 0.0055f;
            }
            return 0.012f;
        }

        /// <summary>樋位条件是否成立(供刀身层做"接上了/没接上"的读法)</summary>
        private bool ResolveEngraveHiArmed() {
            if (Mei.QuietBreath) {
                return IsCombatCold();
            }
            return Mei.TideBeat && IsTideOnBeatNow;
        }

        /// <summary>雕位条件就绪度：亮=这一刻雕纹的赋效可以兑现</summary>
        private float ResolveEngraveHoriLit() {
            //不动：架势够且不在内冷，下一记承诺动作里的受击就能挡
            if (Mei.StanceGuard) {
                return fudoGuardCooldown <= 0 && Stance >= FudoGuardStanceCost - 0.01f ? 1f : 0f;
            }
            //痺雕：同为承诺动作里的架势反手
            if (Mei.NumbCounter) {
                return numbGuardCooldown <= 0 && Stance >= NumbGuardStanceCost - 0.01f ? 1f : 0f;
            }
            //止足：立定充能本身就是读数，充到满再亮满
            if (Mei.PlantedStep) {
                return plantedReady
                    ? 1f
                    : MathHelper.Clamp(plantedCharge / (float)OniMeiCombat.PlantedChargeNeedTicks, 0f, 0.85f);
            }
            //倶利伽罗：龙火窗内龙雕持续烧着
            if (Mei.DragonfireLoop) {
                return KurikaraWindow > 0 ? 1f : 0f;
            }
            //余炎：场还在就亮着
            if (Mei.EmberField) {
                return OniMeiGroundBurn.AnyOwnedStyle(Player, OniMeiBurnStyle.Ember) ? 1f : 0f;
            }
            //镇鸣无常态条件，靠 NotifyQuellStruck 打一记脉冲后自行回落
            return 0f;
        }

        /// <summary>血樋回流：命中顶满血位，槽内随后排空</summary>
        private void NotifyBloodBackflow() {
            engraveHiFill = 1f;
            engraveHiPulse = 1f;
        }

        /// <summary>滞樋：授权命中在槽里多挂一批墨珠</summary>
        private void NotifyStickyBindEngraved() {
            engraveHiFill = Math.Min(1f, engraveHiFill + 0.45f);
            engraveHiPulse = 1f;
        }

        /// <summary>谢樋：了结一个便在槽里多压一片瓣痕</summary>
        private void NotifyPetalPruneEngraved() {
            engraveHiFill = Math.Min(1f, engraveHiFill + 0.5f);
            engraveHiPulse = 1f;
        }

        /// <summary>镇鸣：镇下一发弹，雕纹当场一响再自行回落</summary>
        private void NotifyQuellEngraved() => engraveHoriLit = 1f;

        /// <summary>刀身铭刻层取活读数(仅本地玩家有效)</summary>
        internal void FillEngraveGauges(ref OniMeiEngraveState state) {
            state.HiFill = engraveHiFill;
            state.HiPulse = engraveHiPulse;
            //潮樋的相位就是潮相本身，不另起自由相位
            state.HiPhase = Mei.TideBeat ? MathF.Max(TidePhase01, 0f) : engraveHiPhase;
            state.HiArmed = ResolveEngraveHiArmed();
            state.HoriLit = engraveHoriLit;
            //铁截的钝刃直接由茎铭 Key 判定(各端都有)，此处只给需要活读数的咎层
            state.BladeCrack = GuiltLayers / (float)GuiltMaxLayers;
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
