using CalamityOverhaul.Content.Scenarios.Kiyume.Stealth;
using System.Collections.Generic;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.NPCs
{
    /// <summary>
    /// 恶犬与潜行框架的唯一调音面（进度锚：专家困难模式中期，生产门轮统一重标）。
    /// 世界几何常量住 Gen/KiyumeMetrics，犬类与潜行数值不进那边
    /// </summary>
    internal static class KiyumeHoundMetrics
    {
        //════════ 视线通道 ════════

        /// <summary>基础视距（px）</summary>
        internal const float SightRangePx = 720f;
        /// <summary>视锥点积门（±81°）</summary>
        internal const float ConeDot = 0.15f;
        /// <summary>满雾砍掉的视距比例</summary>
        internal const float FogSightCut = 0.78f;
        /// <summary>浓度低于此不砍视距</summary>
        internal const float FogFloor = 0.12f;
        /// <summary>FogFloor 起多宽浓度把视距砍满</summary>
        internal const float FogSpan = 0.58f;
        /// <summary>静止时的视觉增益折减</summary>
        internal const float StillSightMul = 0.25f;
        /// <summary>静止判定速度门（px/t）</summary>
        internal const float StillSpeedGate = 0.3f;
        /// <summary>静止判定持续（tick）</summary>
        internal const int StillGateTicks = 30;
        /// <summary>藏身折减（也是 ShelterFactor 的命中返回值，1=露天）</summary>
        internal const float ShelterSightMul = 0.3f;
        /// <summary>满档光源的视距增幅（×1.8）</summary>
        internal const float LightBoost = 0.8f;

        //════════ 听觉通道 ════════

        /// <summary>基础听距（px）</summary>
        internal const float HearRangePx = 560f;
        /// <summary>行走响度档</summary>
        internal const float WalkLevel = 0.35f;
        /// <summary>奔跑响度档</summary>
        internal const float RunLevel = 1.0f;
        /// <summary>行走档速度门（px/t）</summary>
        internal const float WalkSpeedGate = 0.5f;
        /// <summary>奔跑档速度门（px/t）</summary>
        internal const float RunSpeedGate = 4.0f;
        /// <summary>落地脉冲响度倍率</summary>
        internal const float LandImpulse = 2.5f;
        /// <summary>开火脉冲响度倍率</summary>
        internal const float WeaponImpulse = 1.8f;
        /// <summary>隔实心的闷响折减</summary>
        internal const float OcclusionMul = 0.45f;

        //════════ 警觉（AwarenessMeter 消费） ════════

        /// <summary>视觉满暴露每 tick 增益</summary>
        internal const float GainSight = 1.9f;
        /// <summary>听觉满暴露每 tick 增益</summary>
        internal const float GainHear = 1.1f;
        /// <summary>起疑阈值</summary>
        internal const float AlertThreshold = 25f;
        /// <summary>搜索阈值</summary>
        internal const float SearchThreshold = 60f;
        /// <summary>追击阈值（也是警觉上限）</summary>
        internal const float ChaseThreshold = 100f;
        /// <summary>巡逻态每 tick 衰减</summary>
        internal const float DecayPatrol = 0.5f;
        /// <summary>起疑态每 tick 衰减</summary>
        internal const float DecayAlert = 0.35f;
        /// <summary>搜索态每 tick 衰减</summary>
        internal const float DecaySearch = 0.15f;

        //════════ 贴地残雾（裁决 1：计入侦测掩体，不进 DensityAt）════════
        //与 P1 的 KiyumeFogDebug.GroundFogHeightPx=110 / GroundFogExposeSpanPx=96 数值同源，改一处必改两处

        /// <summary>贴地残雾带高（地表以上 px）</summary>
        internal const float GroundFogBandPx = 110f;
        /// <summary>雾面沉到地表以下多深（px）残雾长到满强</summary>
        internal const float GroundExposeSpanPx = 96f;
        /// <summary>残雾满强时的掩体浓度</summary>
        internal const float GroundConcealBase = 0.45f;
        /// <summary>客户端探地行数上限（带高 110px≈7 行，留余；探不到=不在带内）</summary>
        internal const int GroundProbeRows = 12;

        //════════ 框架实现常量 ════════

        /// <summary>光源档/藏身因子的缓存重算间隔（tick）</summary>
        internal const int SenseCacheTicks = 6;
        /// <summary>藏身几何：头顶多少 tile 内要有实心</summary>
        internal const int ShelterRoofRows = 4;
        /// <summary>反向观测保守视窗半宽（px，裁决 10：最大变焦可见半屏+余量）</summary>
        internal const int ObserveHalfWidthPx = 1010;
        /// <summary>反向观测保守视窗半高（px）</summary>
        internal const int ObserveHalfHeightPx = 640;
        /// <summary>噪声环形缓冲容量（裁决 11）</summary>
        internal const int NoiseRingCapacity = 32;
        /// <summary>噪声事件衰减半衰期（tick）</summary>
        internal const float NoiseHalfLifeTicks = 45f;
        /// <summary>触发落地脉冲的最小下落速度（px/t）</summary>
        internal const float LandFallGate = 3f;
        /// <summary>落地脉冲满格的下落速度（px/t）</summary>
        internal const float LandFallFull = 9f;
        /// <summary>落地/开火脉冲线性衰减时长（tick）</summary>
        internal const float PulseFadeTicks = 20f;

        /// <summary>
        /// held 光源补充表（item.flame 与火把/蜡烛 createTile 之外的常见手持光）。
        /// 覆盖不全属可接受漏报（染料发光、模组光源不进表），补条目只动这里
        /// </summary>
        internal static readonly HashSet<int> HeldLightItems = [
            ItemID.Glowstick, ItemID.StickyGlowstick, ItemID.BouncyGlowstick,
            ItemID.FairyGlowstick, ItemID.SpelunkerGlowstick,
        ];

        //════════ 恶犬默认感官档案（P2-C 消费；P4 自带档案得到不同感官性格） ════════

        internal static SightProfile HoundSight => new() {
            RangePx = SightRangePx, ConeDot = ConeDot, FogCut = FogSightCut,
            StillMul = StillSightMul, ShelterMul = ShelterSightMul, LightBoost = LightBoost,
        };

        internal static HearingProfile HoundHearing => new() {
            RangePx = HearRangePx, WalkLevel = WalkLevel, RunLevel = RunLevel,
            LandImpulse = LandImpulse, WeaponImpulse = WeaponImpulse, OcclusionMul = OcclusionMul,
        };

        //════════ 恶犬本体（P2 计划书 S3 状态机数值表，P2-C 落地）════════

        /// <summary>体格：生命</summary>
        internal const int HoundLife = 3200;
        /// <summary>体格：防御</summary>
        internal const int HoundDefense = 30;
        /// <summary>体格：击退抗性（0=不吃击退）</summary>
        internal const float HoundKBResist = 0f;
        /// <summary>扑咬接触伤害（仅扑出窗生效）</summary>
        internal const int LungeDamage = 70;
        /// <summary>拖咬期每跳撕咬伤害（Drag 期接触值）</summary>
        internal const int DragBiteDamage = 25;
        /// <summary>巡行速度（px/t）</summary>
        internal const float PatrolSpeed = 1.6f;
        /// <summary>搜索小跑速度（px/t）</summary>
        internal const float SearchSpeed = 2.6f;
        /// <summary>追击奔袭速度（px/t）</summary>
        internal const float ChaseSpeed = 7.2f;
        /// <summary>扑出速度（px/t）</summary>
        internal const float LungeSpeed = 10.5f;
        /// <summary>巡逻锚半径（tile 列）</summary>
        internal const int PatrolRangeCols = 24;
        /// <summary>嗅地间隔下限（tick）</summary>
        internal const int SniffIntervalMinTicks = 240;
        /// <summary>嗅地间隔上限（tick）</summary>
        internal const int SniffIntervalMaxTicks = 420;
        /// <summary>嗅地定格时长（tick）</summary>
        internal const int SniffHoldTicks = 72;
        /// <summary>嗅地时听觉增益折减（它是聋的，这是玩家的移动窗口）</summary>
        internal const float SniffHearingMul = 0.3f;
        /// <summary>凝实入场时长（tick）</summary>
        internal const int EmergeTicks = 120;
        /// <summary>起疑凝视时长下限（tick）</summary>
        internal const int AlertHoldMinTicks = 90;
        /// <summary>起疑凝视时长上限（tick）</summary>
        internal const int AlertHoldMaxTicks = 150;
        /// <summary>搜索一轮预算（tick）</summary>
        internal const int SearchTicks = 480;
        /// <summary>追击丢失宽限（视线断且听觉&lt;0.2 持续，tick）</summary>
        internal const int LostGraceTicks = 180;
        /// <summary>化雾退场时长（tick）</summary>
        internal const int FadeTicks = 90;
        /// <summary>追击起步后蹲蓄力（前摇→长嚎，tick）</summary>
        internal const int ChaseWindupTicks = 24;
        /// <summary>追击中扑咬触发距离（px）</summary>
        internal const float LungeTriggerPx = 90f;
        /// <summary>扑咬蹲伏读帧（tick，伤害 0）</summary>
        internal const int LungeCrouchTicks = 15;
        /// <summary>扑出飞行窗（tick，唯一接触伤害窗）</summary>
        internal const int LungeFlightTicks = 10;
        /// <summary>扑空落地硬直（tick）</summary>
        internal const int LungeRecoverTicks = 20;
        /// <summary>拖咬时长（tick）</summary>
        internal const int DragTicks = 72;
        /// <summary>转入拖咬的受害者血量比门槛</summary>
        internal const float DragHpGate = 0.5f;
        /// <summary>拖咬拉力（受害端本地施加，px/t²）</summary>
        internal const float DragPullAccel = 0.55f;
        /// <summary>撕咬节拍（tick；实际落伤受玩家受击无敌帧钳制）</summary>
        internal const int DragBiteIntervalTicks = 24;
        /// <summary>拖咬倒拖行进速度（px/t；S3 表未列，实施补）</summary>
        internal const float DragCarrySpeed = 1.8f;
        /// <summary>受害者被拉开此距即视为脱口（px；S3 表未列，实施补）</summary>
        internal const float DragBreakDistPx = 280f;
        /// <summary>松口硬直时长（tick）</summary>
        internal const int StaggerTicks = 36;
        /// <summary>连锁警觉分享：长嚎（进追击）与哀鸣（被杀）对全图同类的警觉加值</summary>
        internal const float HowlAwarenessShare = 40f;
        /// <summary>侦测采样节流（tick，服务器）</summary>
        internal const int HoundSenseTicks = 6;

        //════════ 导演与潮汐犬势（P2 计划书 S4，P2-D 落地）════════

        /// <summary>导演巡检间隔（tick）</summary>
        internal const int DirectorCheckTicks = 20;
        /// <summary>首入宽限（tick）：保住「涨潮退去、村子交还」的入场演出</summary>
        internal const int EntryGraceTicks = 3600;
        /// <summary>场上犬数硬上限（含正在化雾离场的）</summary>
        internal const int MaxAlive = 4;
        /// <summary>高潮门（Tide≥此值雾淹村落，犬进村）</summary>
        internal const float TideHighGate = 0.62f;
        /// <summary>低潮门（Tide&lt;此值犬归湖，只在残留带活动）</summary>
        internal const float TideLowGate = 0.30f;
        /// <summary>高潮目标犬数</summary>
        internal const int TargetCountHigh = 3;
        /// <summary>中潮目标犬数</summary>
        internal const int TargetCountMid = 2;
        /// <summary>低潮目标犬数（限残留带内）</summary>
        internal const int TargetCountLow = 1;
        /// <summary>生成点距所有玩家的最小距离（px，视野外）</summary>
        internal const float SpawnMinDistPx = 1200f;
        /// <summary>生成选点带：距锚定玩家的距离下限（px）</summary>
        internal const float SpawnBandMinPx = 1400f;
        /// <summary>生成选点带：距锚定玩家的距离上限（px）</summary>
        internal const float SpawnBandMaxPx = 2400f;
        /// <summary>选点西侧偏置（雾源方向来）</summary>
        internal const float WestBias = 0.65f;
        /// <summary>生成点名义浓度门（犬从雾里来，与犬影出没门 0.42 同源）</summary>
        internal const float SpawnFogGate = 0.42f;
        /// <summary>每次巡检的选点尝试预算（全败 fail quiet，下轮再试）</summary>
        internal const int SpawnAttempts = 10;
        /// <summary>梦压：浓雾区奔跑增益（每 tick）</summary>
        internal const float DreamHeatRunGain = 0.06f;
        /// <summary>梦压：浓雾区开火增益（每次）</summary>
        internal const float DreamHeatFireGain = 3f;
        /// <summary>梦压：自然衰减（每 tick）</summary>
        internal const float DreamHeatDecay = 0.02f;
        /// <summary>梦压计入的名义浓度门</summary>
        internal const float DreamHeatFogGate = 0.5f;
        /// <summary>梦压解锁门：目标数 +1 且解锁双犬合围</summary>
        internal const float PackGate = 70f;
        /// <summary>低潮残留带西缘（tile 列，滩涂西起，犬不入湖）</summary>
        internal const int LowTideBandLeftCol = 320;
        /// <summary>低潮残留带东缘（tile 列，村西为界，不含）</summary>
        internal const int LowTideBandRightCol = 900;
        /// <summary>哀鸣抚恤解除潮位（上穿沿恢复补员）</summary>
        internal const float RecruitHoldReleaseTide = 0.5f;
        /// <summary>双犬合围：绕后点在玩家背侧的偏移（px）</summary>
        internal const float FlankBehindPx = 240f;
        /// <summary>绕后点钳制：雾不够浓再外退的步长（px）</summary>
        internal const float FlankFogStepPx = 80f;
        /// <summary>绕后点钳制：最多外退步数（钳在玩家可见雾外）</summary>
        internal const int FlankFogScanSteps = 5;
        /// <summary>绕后驻停容差（px）：进入即松油门吊在雾里</summary>
        internal const float FlankHoldSlackPx = 40f;

        //════════ 嗅迹追踪（P2 点子 13：气味场 KiyumeScentTrail + 搜索态沿迹）════════

        /// <summary>气味环形缓冲容量（全玩家共池）</summary>
        internal const int ScentRingCapacity = 192;
        /// <summary>记点间隔（tick）：奔跑且贴地时每隔此值留一点</summary>
        internal const int ScentRecordIntervalTicks = 12;
        /// <summary>气味点寿命（tick，8s 线性衰减到无）</summary>
        internal const int ScentLifeTicks = 480;
        /// <summary>搜索态嗅迹查询间隔（tick）</summary>
        internal const int ScentQueryIntervalTicks = 30;
        /// <summary>嗅迹查询半径（px，首查以最后感知锚为心，沿迹后以上一迹点为心）</summary>
        internal const float ScentSniffRadiusPx = 360f;
        /// <summary>沿迹推进门（px）：犬走近当前迹点至此距内才认领下一点（真沿地走迹，不隔空跳锚）</summary>
        internal const float ScentAdvanceGatePx = 120f;
        /// <summary>迹感保持（tick）：最后一次确认活迹后多久算迹断，断后恢复原折返与超时判定</summary>
        internal const int ScentHoldTicks = 120;
        /// <summary>沿迹演出保持（tick）：锚被迹点推进后鼻尖尘粒加密时长（客户端从 ai[3] 变沿读出）</summary>
        internal const int ScentDustHoldTicks = 60;

        //════════ 白毛望乡犬（点子 11 压仓，R2-D 落地）════════

        /// <summary>白犬泵冷却（tick，≈7.5 分钟；入梦即满装）</summary>
        internal const int WhiteHoundCooldownTicks = 27000;
        /// <summary>冷却到期抽签（NextBool 分母，1/3 中签）</summary>
        internal const int WhiteHoundLotteryChance = 3;
        /// <summary>生成环带：距锚定玩家水平距离下限（px，视野边缘）</summary>
        internal const float WhiteHoundSpawnMinPx = 700f;
        /// <summary>生成环带：距锚定玩家水平距离上限（px）</summary>
        internal const float WhiteHoundSpawnMaxPx = 1000f;
        /// <summary>生成点距所有玩家的最小实距（px，须大于走近化雾距免得落地即散）</summary>
        internal const float WhiteHoundPlayerClearPx = 480f;
        /// <summary>生成点距最近恶犬的最小距离（px，不与恶犬同屏）</summary>
        internal const float WhiteHoundHoundClearPx = 1600f;
        /// <summary>高地偏好：前半程尝试硬性要求地面高于玩家中心此值（px）</summary>
        internal const float WhiteHoundElevatePx = 48f;
        /// <summary>每次泵巡检的选点尝试预算（全败静默下拍再试）</summary>
        internal const int WhiteHoundSpawnAttempts = 8;
        /// <summary>走近化雾距离（px）</summary>
        internal const float WhiteHoundApproachPx = 340f;
        /// <summary>攻击命中判定的犬框外扩（px）</summary>
        internal const int WhiteHoundStrikeInflatePx = 24;
        /// <summary>在场预算下限（tick）</summary>
        internal const int WhiteHoundStayMinTicks = 900;
        /// <summary>在场预算上限（tick）</summary>
        internal const int WhiteHoundStayMaxTicks = 1500;
        /// <summary>被看见判定的雾盲阈值（ObservedByAnyPlayer 第二参）</summary>
        internal const float WhiteHoundFogBlind = 0.62f;
        /// <summary>被看见坐实的连续时长门（tick）</summary>
        internal const int WhiteHoundSeenGateTicks = 60;
        /// <summary>凝现时长（tick，uDissolve 1→0）</summary>
        internal const int WhiteHoundEmergeTicks = 60;
        /// <summary>化雾退场时长（tick）</summary>
        internal const int WhiteHoundFadeTicks = 90;
    }
}
