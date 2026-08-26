using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen
{
    //距离带：自西（黑墙）向东固定列数堆叠，区间半开 [Left,Right)
    internal readonly struct DistanceBand
    {
        internal readonly string Name;
        internal readonly int Left;
        internal readonly int Right;
        internal readonly ushort FloorBrick;

        internal DistanceBand(string name, int left, int cols, ushort floorBrick) {
            Name = name;
            Left = left;
            Right = left + cols;
            FloorBrick = floorBrick;
        }

        internal bool Contains(int x) => x >= Left && x < Right;
    }

    //生成与运行时常量集中声明；扩容/调参只动此处
    //蓝图 Doc/plans/OldNet/DESIGN.md + M2-PLAN.md，镜像 DungeonworldMetrics 惯例改横向
    internal static class OldNetMetrics
    {
        internal const int Width = 2400;
        internal const int Height = 600;
        //世界四周实心边界厚度
        internal const int BorderThick = 8;
        //东侧可玩右界（半开）
        internal const int PlayRight = Width - BorderThick;

        //════════ 横向带表（列） ════════

        //黑墙体 [0,40)：实心不可入，视觉由 BlackwallRender 接管
        internal const int WallCols = 40;
        //墙脚带 [40,700)：规整几何、安全区起步
        internal const int FootCols = 660;
        //废墟带 [700,1600)：遗址主产区
        internal const int RuinCols = 900;
        //信号衰减区 [1600,2400)：M2a 起开放为可入地形，疯域规则 M3 接管
        internal const int FadeCols = Width - WallCols - FootCols - RuinCols;
        //衰减区左缘（=废墟带右缘）
        internal const int FadeLeft = WallCols + FootCols + RuinCols;

        //════════ 纵向剖面（M2a 空间重划） ════════
        //
        //  [BorderThick,120)   高空带：巨构上层/天线阵（Z4，M3 内容）
        //  [120,FloorRow)      地表空域：主可玩层
        //  FloorRow±wobble     地板线
        //  (floor,460)         浅层：遗址内部/地窖，竖井接地表
        //  [460,592)           深层：深网机房/管道
        //
        //worldSurface=430 / rockLayer=500 恰好切出 地表/地下/洞穴 三段原版判定

        //高空带下缘
        internal const int SkyBandBottom = 120;
        //地板主线（上表面基准行），起伏 ±FloorWobble；衰减区 ±FadeWobble
        internal const int FloorRow = 380;
        internal const int FloorWobble = 6;
        internal const int FadeWobble = 14;
        //浅层下界 / 深层下界
        internal const int UnderShallowBottom = 460;
        internal const int UnderDeepBottom = Height - BorderThick;
        //浅层/深层平台厅地板行（竖井落点与挂房基准）
        internal const int UnderShallowFloorRow = 436;
        internal const int UnderDeepFloorRow = 540;

        //worldSurface 压到地板带以下：玩法层判"地表"，天幕可见（与 Dungeonworld 相反）
        //rockLayer 再往下，浅层判"地下"、深层判"洞穴"
        internal const int WorldSurfaceRow = 430;
        internal const int RockLayerRow = 500;

        //════════ 锚点 ════════

        internal const int SpawnX = 60;
        //登出终端在出生点与墙体之间
        internal const int LogoutX = 48;
        //出生区全平列数（生成 pass 与 ICE 撒布共用基准）
        internal const int SpawnFlatCols = 80;

        //════════ 房间/占用/竖井（M2a 生成架构） ════════

        //房间外壳厚度（Bounds 含壳）
        internal const int RoomShellThick = 2;
        //结构预留间距
        internal const int RoomPadding = 4;
        //竖井宽（列）与井内歇脚平台竖距（行）
        internal const int ShaftWidth = 4;
        internal const int ShaftLedgeStep = 7;
        //平台厅尺寸（竖井落点开间）
        internal const int LandingW = 16;
        internal const int LandingH = 8;

        //════════ 锚位规划（P30 裁决，不再是绝对列位） ════════

        //中继站座数（废墟带等分段各一座）
        internal const int RelayCount = 2;
        //锚位与竖井/彼此的最小间距（列），栅格预留兜底
        internal const int AnchorPadding = 10;

        //════════ 入口（M2c） ════════

        //L3 领域下潜：接管中按住下潜键的蓄力帧数
        internal const int L3DiveHoldTicks = 120;

        //════════ RAM 距离底噪（每秒） ════════
        //标定基准：基础 RAM 8 / 恢复 0.1s（RamSystem.DefaultBase*）
        //墙脚 SafeCols 内零消耗；此后每离墙 100 格 +DrainPer100，
        //新角色约在墙脚带中段收支平衡，废墟带净消耗，贪心半径随 RAM build 增长
        internal const int DrainSafeCols = 150;
        internal const float DrainPer100Tiles = 0.05f;

        //════════ 数据节点 ════════

        //单节点碎片产出
        internal const int NodeShardMin = 1;
        internal const int NodeShardMax = 3;

        //════════════════ M1 常量区（威胁与决策，游戏内调参集中地）════════════════

        //──── 噪音源（一次性 / 每秒） ────
        //加密节点引导：站桩 = 主动点亮自己
        internal const float NoiseChannelPerSecond = 7f;
        //事件节点拉闸：直入清剿波档
        internal const float NoiseEventPull = 95f;
        //开火每发（与挥动叠加：枪比刀响）
        internal const float NoiseShoot = 2f;
        //任意武器挥动每次
        internal const float NoiseSwing = 3f;
        //移动（速度 > NoiseMoveSpeedGate 时）每秒
        internal const float NoiseMovePerSecond = 0.35f;
        internal const float NoiseMoveSpeedGate = 4f;
        //采集普通节点一次性
        internal const float NoiseHarvest = 3f;
        //被巡逻 ICE 目击一次性
        internal const float NoiseSpotted = 15f;
        //击杀巡逻 ICE 一次性（打死巡逻是高噪决策）
        internal const float NoisePatrolKill = 20f;
        //中继站结算一次性（上行广播惹注意）
        internal const float NoiseRelaySettle = 25f;
        //时停期间增量系数（时停考古低噪音的落点）
        internal const float NoiseFreezeMul = 0.25f;

        //──── 噪音消散 ────
        //连续无新增该秒数后开始衰减
        internal const int NoiseQuietDelayTicks = 150;
        //低热/高热衰减速率（每秒）；高热难冷却
        internal const float NoiseDecayLowPerSecond = 3f;
        internal const float NoiseDecayHighPerSecond = 1.5f;
        internal const float NoiseDecayHighThreshold = 50f;
        //T4 触发后免疫衰减时长（清剿波要持续够久）
        internal const int NoiseT4DecayImmuneTicks = 20 * 60;

        //──── 四档阈值（迟滞：跌档需再低 Hysteresis 点） ────
        internal const float NoiseT1 = 20f;
        internal const float NoiseT2 = 45f;
        internal const float NoiseT3 = 70f;
        internal const float NoiseT4 = 95f;
        internal const float NoiseTierHysteresis = 8f;

        //──── T4 清剿波 ────
        //补员至场上猎杀者数
        internal const int T4SustainCount = 5;
        //补员间隔
        internal const int T4ReinforceTicks = 12 * 60;
        //噪音降到此值以下解除清剿波（与档位迟滞独立）
        internal const float T4ReleaseBelow = 60f;

        //──── 巡逻 ICE ────
        //布防间距（列）与巡逻半径（列）
        internal const int PatrolSpacingCols = 250;
        internal const int PatrolRangeCols = 60;
        internal const float PatrolSpeed = 1.6f;
        //悬浮高度（px）
        internal const float PatrolHoverHeight = 56f;
        //侦测半径与充能时长；慢速通过 = 潜行
        internal const float PatrolDetectRadius = 240f;
        internal const int PatrolDetectChargeTicks = 72;
        internal const float PatrolSneakSpeedGate = 2f;
        internal const float PatrolSneakRadiusMul = 0.6f;
        //T1 起网在看你：侦测半径与巡速加成
        internal const float PatrolAlertRadiusMul = 1.5f;
        internal const float PatrolAlertSpeedMul = 1.2f;
        //可击杀但高防高血；接触伤害只在冲撞窗口生效
        internal const int PatrolLife = 1200;
        internal const int PatrolDefense = 30;
        internal const int PatrolContactDamage = 20;
        //目击后的冲撞时长与再侦测冷却
        internal const int PatrolLungeTicks = 180;
        internal const int PatrolRedetectCooldown = 300;

        //──── Black ICE 猎杀者 ────
        //极速与转向率："追得上但甩得掉"的手感生死线
        internal const float HunterSpeed = 11f;
        internal const float HunterTurnRate = 0.03f;
        //精确感知距离（+通视）；超出只飞向最后已知位置
        internal const float HunterPerceptionRange = 900f;
        //断视线且超距持续该时长 → 丢失目标回墙
        internal const int HunterLoseTicks = 6 * 60;
        //接触咬合：小额 HP 伤 = 命中事件载体，追加 RAM 扣减
        internal const int HunterContactDamage = 14;
        internal const float HunterBiteRam = 2f;
        //嗅探场：近距持续 RAM 压力（无视无敌帧）
        internal const float HunterSniffRange = 600f;
        internal const float HunterSniffRamPerSecond = 1.5f;
        //协议施放间隔（帧）与前摇
        internal const int HunterCastIntervalMin = 8 * 60;
        internal const int HunterCastIntervalMax = 14 * 60;
        internal const int HunterCastTelegraphTicks = 48;
        //精英变体（T3+）：加速、施放间隔减半、三连咬合触发 RAM 锁定
        internal const float HunterEliteSpeedMul = 1.15f;
        internal const int HunterEliteLockBites = 3;
        internal const int HunterEliteLockFrames = 90;
        //连击窗口：超时咬合计数清零
        internal const int HunterBiteComboWindow = 300;
        //出生列（墙的方向来）
        internal const int HunterSpawnCol = WallCols + 10;

        //──── 账本容量 ────
        //基础 24：约 12 个普通节点或 3-4 个加密节点，强迫中期决策
        internal const int LedgerBaseCapacity = 24;

        //──── 节点分级撒布 ────
        //地表配额（地下房间配额另计）
        internal const int NodePlainCount = 34;
        internal const int NodeEncryptCount = 10;
        internal const int NodeEventCount = 2;
        //结构内普通节点上限（房间/prefab/桅杆顶/方舱共享配额，建造期机会性放置）
        //本轮结构扩容后上调：方舟/冷却塔/尖塔/掩体新增约 10 个机会性槽位
        //P1 新增消费点 4 个：金库 p 槽 1（配额耗尽时直写兜底并入账，见 Z2Prefabs.VaultLegend）
        //+ 坠亡巨物腔内 3（肋腔 1~2 + 头颅核心 1，见 Z3Giant）
        internal const int NodeUnderPlainCount = 32;
        //衰减区地表加密节点（高险高值）
        internal const int NodeFadeEncryptCount = 6;
        //加密节点：引导时长、价值倍数、站桩半径
        internal const int EncryptChannelTicks = 180;
        internal const int EncryptValueMul = 3;
        internal const float EncryptChannelRadius = 60f;

        //──── 封锁区 ────
        internal const int SealBoxCount = 2;
        internal const int SealBoxW = 14;
        internal const int SealBoxH = 8;
        internal const int SealBoxNodeMin = 6;
        internal const int SealBoxNodeMax = 10;
        //事件节点离封锁区最小距离（列）：拉闸的人要跑一段才能吃到糖
        internal const int EventToSealMinCols = 80;

        //════════════════ M3 常量区（内容扩容） ════════════════

        //──── 回声考古（时停显影：NoiseFreezeMul 低噪路线的报偿） ────
        //废墟+衰减区撒布数
        internal const int EchoNodeCount = 7;
        //回声产出倍数；采集零噪音
        internal const int EchoShardMul = 2;

        //──── 深潜模块缓存（CanGenerateInLabChest=false 保留池的兑现口） ────
        //衰减区限定
        internal const int CacheCount = 3;
        //开缓存一次性噪音
        internal const float NoiseCacheOpen = 12f;

        //──── 哨戒炮塔 ICE（地下机房与深层的常驻威胁） ────
        internal const int TurretLife = 900;
        internal const int TurretDefense = 24;
        //扫描半径与锁定充能
        internal const float TurretScanRadius = 300f;
        internal const int TurretLockChargeTicks = 50;
        //锁定后射击间隔与弹速
        internal const int TurretFireInterval = 90;
        internal const float TurretBoltSpeed = 6.5f;
        internal const int TurretBoltDamage = 18;
        //命中追加 RAM 扣减（ICE 家族的牙）
        internal const float TurretBoltRam = 1.5f;
        //锁定完成一次性噪音 / 击毁一次性噪音
        internal const float NoiseTurretSpotted = 10f;
        internal const float NoiseTurretKill = 12f;
        //房间布防概率（浅层）；深层房间必装
        internal const float TurretRoomChance = 0.45f;

        //──── 疯域（衰减区规则异常） ────
        //衰减区内噪音不自然衰减：网在这里永不平静（规则挂 OldNetPlayer）

        //──── 高空巨构（Z4） ────
        internal const int AntennaCount = 4;
        internal const int HulkCount = 5;
        //巨构悬浮行带
        internal const int HulkRowMin = 36;
        internal const int HulkRowMax = 88;

        //════════════════ 结构扩容常量区（地表目录组数集中此处） ════════════════

        //──── Z1 墙脚带 ────
        internal const int ShelterPodCount = 3;
        internal const int DeadPylonGroupCount = 4;
        //──── Z2 废墟带 ────
        internal const int GraveyardCount = 3;
        internal const int BrokenBridgeCount = 4;
        //坠毁数据方舟：断成两截的运载舰残骸，舱内节点+加密节点
        internal const int DataArkCount = 2;
        //冷却塔：中空烟囱竖井，内攀爬横档，废墟带的纵向地标
        internal const int CoolantStackCount = 2;
        //──── Z3 衰减区 ────
        //焦黑尖塔群：信号尽头的烧毁塔林（衰减区首批实体结构）
        internal const int ScorchedSpireGroupCount = 3;
        //坍塌掩体：半埋的破壳避难所，藏加密节点
        internal const int CollapsedBunkerCount = 2;

        //════════════════ 02 交互经济常量区（P2：扩容坞/冷存储/保险契约/破译矩阵） ════════════════

        //──── 账本扩容坞 ────
        //一次性容量加成与噪音价签：15 噪 vs 中继 25 噪 + 折返路费，定价教学件
        internal const int LedgerDockBonus = 8;
        internal const float LedgerDockNoise = 15f;
        //撒布：废墟带 1 + 衰减区 1（两段各一）

        //──── 冷存储节点 ────
        //RAM 换碎片的无声节点：产出 (1-3)×ColdShardMul，连 AddNoise 都不调
        internal const float ColdNodeRamCost = 2f;
        internal const int ColdShardMul = 2;
        internal const int ColdNodeCount = 6;
        //撒布左界：废墟带深段起（RAM 底噪最贵的地方价签才有分量）
        internal const int ColdNodeMinCol = 1100;

        //──── 保险契约终端 ────
        //保费比例（对 PendingTotal 向上取整）与投保上行噪音
        internal const float EscrowPremium = 0.3f;
        internal const float EscrowNoise = 10f;
        internal const int EscrowCount = 2;

        //──── 主控破译矩阵（旗舰：每关翻倍的弃留梯子） ────
        //开台 RAM 座位费；人已在深层，门票之上的第二道价
        internal const float VaultRamCost = 3f;
        //面板开启期每秒噪音（上行链路激活，走 AddNoise 吃时停系数）
        internal const float VaultNoisePerSecond = 2f;
        //每关过关机械音 / 爆仓噪音
        internal const float VaultStageNoise = 3f;
        internal const float VaultBustNoise = 15f;
        //扫描指针角速度（度/秒），逐关反向
        internal const float VaultCursorDegPerSec = 220f;
        //五关闸弧宽度表（度）：S5 ≈ 5-6 帧判定窗
        internal static readonly float[] VaultArcDeg = [96f, 72f, 52f, 34f, 20f];
        //五关彩池碎片增量（S4/S5 的模块实体与 RAM 芯片另行掉落，不走账本）
        internal static readonly int[] VaultPotShards = [4, 6, 8, 0, 0];

        //════════════════ 03 扩展敌人常量区（猎杀敌人包：缢影/灯蛾/循迹猎犬/回收官）════════════════

        //──── 缢影 ICE（垂落伏击者，一次性布防）────
        //布防量与房间顶挂载概率（主源房间顶，备选露天悬垂面）
        internal const int LurkerCount = 10;
        internal const float LurkerRoomChance = 0.35f;
        //悬垂面探测：实心上方 + 至少该行数空气下方
        internal const int LurkerOverhangAirRows = 8;
        //吊点最小横向间距（列）
        internal const int LurkerSpacingCols = 20;
        internal const int LurkerLife = 400;
        internal const int LurkerDefense = 8;
        //俯冲接触伤（唯一伤害窗）+ 咬合 RAM
        internal const int LurkerContactDamage = 25;
        internal const float LurkerBiteRam = 3f;
        //猎杀漏斗：水平半宽 / 向下深度（px）；触发另要求玩家速度 ≥ PatrolSneakSpeedGate
        internal const float LurkerFunnelHalfWidth = 80f;
        internal const float LurkerFunnelDepth = 220f;
        //颤抖前摇 / 俯冲速度 / 最大坠程 / 俯冲硬超时 / 回卷速度 / 回卷兜底 / 再触发冷却
        internal const int LurkerArmTicks = 12;
        internal const float LurkerDropSpeed = 14f;
        internal const float LurkerDropMaxDist = 240f;
        //满坠程 240px÷14px/f≈17t，40t 封顶：任何卡位都收进回卷
        internal const int LurkerDropTimeoutTicks = 40;
        internal const float LurkerReelSpeed = 1.2f;
        internal const int LurkerReelTimeoutTicks = 480;
        internal const int LurkerCooldownTicks = 300;
        //本体悬于丝根下方的垂距（px）
        internal const float LurkerHangOffset = 22f;
        //击杀噪音（伏击者不报信，代价低于巡逻）
        internal const float NoiseLurkerKill = 8f;

        //──── 灯蛾标记体（T1 标记者，热度锁）────
        //附着噪音 ping：间隔须低于 NoiseQuietDelayTicks(150)，兑现"在场时噪音无法自然消散"；
        //规划稿 240t/+2 平均速率不变（+0.5/s），改细分节拍以真正锁死衰减
        internal const int TaggerPingTicks = 120;
        internal const float TaggerPingNoise = 1f;
        internal const float TaggerApproachSpeed = 6f;
        //进入附着的距离 / 环绕轨道半径带
        internal const float TaggerAttachRange = 200f;
        internal const float TaggerOrbitMid = 160f;
        internal const float TaggerOrbitSway = 40f;
        //持械面向且近于此距离 → 折线规避
        internal const float TaggerThreatRange = 300f;
        internal const int TaggerSkitterTicks = 24;
        //断附着：断视线且超此距离持续此时长
        internal const float TaggerDetachRange = 700f;
        internal const int TaggerDetachTicks = 300;
        //重索敌失败离场
        internal const int TaggerReseekTicks = 300;
        //T2+ 维持场上 ≥1 的阵亡补员冷却
        internal const int TaggerRespawnTicks = 45 * 60;
        internal const float NoiseTaggerKill = 4f;

        //──── 循迹猎犬（T1 循迹者：追脚印不追人）────
        internal const int TracerLife = 800;
        internal const int TracerDefense = 16;
        internal const float TracerTrackSpeed = 5.5f;
        //足迹环形缓冲：容量 × 采样间隔 = 12s 记忆
        internal const int TracerTrailCap = 48;
        internal const int TracerSampleTicks = 15;
        //相邻采样点低于此距离跳过（防抖）
        internal const float TracerPointSkipDist = 24f;
        //新采样点与旧段低于此距离 = 路径自交（回踩反制）
        internal const float TracerCrossDist = 32f;
        internal const int TracerConfusedTicks = 90;
        //嚎叫：触发距离（+通视）/ 充能时长 / 打断累伤 / 甩脱距离 / 硬直 / 成功噪音 / 冷却
        internal const float TracerHowlRange = 260f;
        internal const int TracerHowlTicks = 90;
        internal const int TracerHowlInterruptDamage = 120;
        internal const float TracerHowlBreakRange = 500f;
        internal const int TracerStaggerTicks = 60;
        internal const float TracerHowlNoise = 18f;
        internal const int TracerHowlCooldownTicks = 300;
        //失锚嗅探：时长 / 重获半径（缓冲点落入该半径即重上线索）
        internal const int TracerSniffTicks = 180;
        internal const float TracerReacquireRange = 400f;
        //入场：T1 跃迁后延迟（灯蛾先到犬后至）/ 空投在玩家西侧的距离 / 落地嗅探演出
        internal const int TracerSpawnDelayTicks = 30 * 60;
        internal const float TracerSpawnWestPx = 600f;
        internal const int TracerCastTicks = 40;
        //T2+ 阵亡补员冷却
        internal const int TracerRespawnTicks = 60 * 60;
        internal const float NoiseTracerKill = 10f;

        //──── 回收官（T4 升格小 Boss：收束处决）────
        //清剿波持续该时长后升格派遣（每潜一次）；派遣广播到本体入场的延迟
        internal const int WardenEscalateTicks = 45 * 60;
        internal const int WardenSpawnDelayTicks = 180;
        internal const int WardenLife = 9000;
        internal const int WardenDefense = 40;
        //站位纪律：低于 Min 不起手冲锋，高于 Chase 转长距贯穿追近；
        //各状态悬停环距（MaintainStandoff 钳进 [Min,Max] 带内）与悬停上抬
        internal const float WardenStandoffMin = 300f;
        internal const float WardenStandoffMax = 500f;
        internal const float WardenChaseRange = 2000f;
        internal const float WardenEntranceStandoff = 400f;
        internal const float WardenSelectStandoff = 420f;
        internal const float WardenVolleyStandoff = 460f;
        internal const float WardenRainStandoff = 480f;
        internal const float WardenHoverLift = 40f;
        //追近（长距贯穿）：冲速 / 收刹判距 / 飞行超时 / 独立接触伤（位移工具，低于断言冲锋）
        internal const float WardenChaseDashSpeed = 30f;
        internal const float WardenChaseBrakeRange = 600f;
        internal const int WardenChaseTimeoutTicks = 120;
        internal const int WardenChaseContactDamage = 30;
        //断言冲锋：前摇 / 警鸣提前量 / 冲刺时长 / 冲速 / 硬刹 / 接触伤 + RAM
        internal const int WardenDashAnticipationTicks = 45;
        internal const int WardenDashBeepLead = 36;
        internal const int WardenDashTicks = 9;
        internal const float WardenDashSpeed = 24f;
        internal const int WardenDashBrakeTicks = 12;
        internal const int WardenContactDamage = 45;
        internal const float WardenDashRam = 5f;
        //协议齐射：连发数与间隔（走 OldNetHostileHack T4 常规池）
        internal const int WardenVolleyCount = 3;
        internal const int WardenVolleyIntervalTicks = 90;
        //字形雨：投放面宽 / 弹数 / 落速 / 弹伤 + RAM / 安全缝宽（格）
        internal const float WardenRainWidth = 900f;
        internal const int WardenRainBoltCount = 24;
        internal const float WardenGlyphFallSpeed = 3.5f;
        internal const int WardenGlyphDamage = 20;
        internal const float WardenGlyphRam = 1f;
        internal const int WardenRainGapTiles = 3;
        //双缝各掷面心一侧半区、离面心至少此距：间距 ≥2×此值由构造保证
        internal const float WardenRainGapHalfZoneMin = 80f;
        //吞噬牵引（P2+）：时长 / 半径 / 每帧加速度 / 贴身弹开伤
        internal const int WardenPullTicks = 180;
        internal const float WardenPullRadius = 600f;
        internal const float WardenPullAccel = 0.22f;
        internal const int WardenPullTouchDamage = 30;
        //相位阈值（生命比）与换相硬直
        internal const float WardenP2LifeFrac = 0.60f;
        internal const float WardenP3LifeFrac = 0.25f;
        internal const int WardenPhaseStunTicks = 40;
        //终末协议（P3）：前摇（最狠的招给最长的读秒）
        internal const int WardenExecuteTelegraphTicks = 90;
        //击杀奖励：碎片喷付 / 全网静默地板 / 静默余量时长与增量系数
        internal const int WardenShardPayout = 16;
        internal const float WardenSilenceFloor = 30f;
        internal const int WardenGraceTicks = 60 * 60;
        internal const float WardenGraceNoiseMul = 0.5f;

        //════════════════ P1 结构与地标常量区（遗物层/语义房/检疫关卡/坠亡巨物） ════════════════

        //──── 遗物陈设层（ctx.Scatter 首批使用者，P55 撒布配额） ────
        internal const int RelicScatterZ1 = 10;
        internal const int RelicScatterZ2 = 16;
        internal const int RelicScatterZ3 = 10;

        //──── 语义房激活包：金库（深井厅左侧，Role=Vault） ────
        internal const int VaultRoomCount = 1;

        //──── 检疫关卡（疯域规则线上的失守关卡，06 过线事件反查锚） ────
        internal const int CheckpointCol = FadeLeft;
        internal const int CheckpointFootW = 34;

        //──── 坠亡巨物（衰减区旗舰地标，05 天幕巨物的坠地呼应） ────
        internal const int FallenGiantCount = 1;
        internal const int FallenGiantColMin = 1900;
        internal const int FallenGiantColMax = 2250;
        //宽度参数化三档缩比（峰高/腔体随宽度等比换算）
        internal static readonly int[] FallenGiantWidths = [88, 72, 60];

        //════════════════ 04 固定威胁常量区（OldNetThreatField 系装置，游戏内调参集中地）════════════════

        //──── 公共地基（懒扫描注册表）────
        //懒扫描间隔与窗口半径（列/行）；出窗装置弃态，回窗重新登记
        internal const int ThreatScanInterval = 20;
        internal const int ThreatScanCols = 40;
        internal const int ThreatScanRows = 30;

        //──── 光栅绊网 ────
        //每口竖井道数与门洞装网概率
        internal const int TripwirePerShaftMin = 2;
        internal const int TripwirePerShaftMax = 3;
        internal const float TripwireSocketChance = 0.35f;
        //亮 2.4s / 灭 1.2s；相位=坐标哈希，同屏多线错相成通行序列
        internal const int TripwireOnTicks = 144;
        internal const int TripwireOffTicks = 72;
        //亮相前的起搏预告（虚线加速闪）时长
        internal const int TripwireBlinkTicks = 18;
        //过线计费（低于目击 15，高于采集 3）与同线冷却
        internal const float NoiseTripwire = 8f;
        internal const int TripwireRearmTicks = 180;
        //剪断：按住右键站桩时长与代价（为常用路线做开荒保养）
        internal const int TripwireCutTicks = 30;
        internal const float NoiseTripwireCut = 3f;

        //──── 静默哨雷 ────
        internal const int MineCountRuin = 6;
        internal const int MineCountFade = 4;
        //贴糖半径（列）：TryPlace 内验证近旁有加密节点/深潜缓存
        internal const int MineNearLootCols = 12;
        internal const int MineDedupeDist = 18;
        //入场武装：半径内快速移动才触发；慢速接近=潜行，与巡逻潜行门同一套身体语言
        internal const float MineWakeRadius = 90f;
        internal const float MineArmSpeedGate = 2f;
        internal const int MineArmTicks = 30;
        //引爆：HP + RAM + 全网尖叫
        internal const int MineDamage = 25;
        internal const float MineRam = 2f;
        internal const float NoiseMineScream = 14f;
        //拆除站桩时长（静默移除，0 噪音）与玩家弹幕近点引爆半径（px）
        internal const int MineDefuseTicks = 40;
        internal const float MineRemoteDetonateRadius = 30f;

        //──── 扫描哨眼 ────
        internal const int SweepEyeLife = 700;
        internal const int SweepEyeDefense = 20;
        //锥长（px）/ 锥半角（rad）/ 摆动半弧（rad，合 130° 弧）/ 往返周期（tick）
        internal const float SweepEyeConeLen = 340f;
        internal const float SweepEyeConeHalfAngle = 0.35f;
        internal const float SweepEyeArcHalf = 1.134f;
        internal const int SweepEyePeriodTicks = 360;
        //充能 36 tick（窗口可预读，容错给在节律不给在时长）；脱锥快速回落
        internal const int SweepEyeChargeTicks = 36;
        internal const float SweepEyeDecayPerTick = 3f;
        //目击后锁定跟随（追光灯态）时长与期间持续曝光计费
        internal const int SweepEyeLockTicks = 180;
        internal const float NoiseEyeSpotted = 12f;
        internal const float NoiseEyeExposurePerSecond = 1f;
        internal const float NoiseEyeKill = 10f;
        //浅井井口装设概率
        internal const float SweepEyeShaftChance = 0.5f;

        //──── 噪音联动封锁闸 ────
        //激活档位留可写字段：06 导演的区域修饰符可临时改写（规划 §3 集成点）
        internal static int BulkheadWarnTier = 2;
        internal static int BulkheadShutTier = 3;
        //重开条件：档位 ≤1 持续该时长；重开前 1s 薄荷绿脉冲预告
        internal const int BulkheadReopenHoldTicks = 480;
        internal const int BulkheadReopenPulseTicks = 60;
        //与玩家碰撞盒重叠格的延迟落格重试间隔（不夹人）
        internal const int BulkheadRetryTicks = 10;
        //应急泄压杆：开闸时长与噪音代价（用更多噪音买一次通行）
        internal const int BreakerOpenTicks = 480;
        internal const float NoiseBreaker = 10f;

        //════════════════ 06 导演与评分常量区（P6：余震/收网/热断链/深潜评级）════════════════

        //──── 衰减区余震（2.9）────
        //破解成功到猎杀落地的读秒；废墟带加密永不触发（分带规则一句话可教）
        internal const int AftershockDelayTicks = 180;
        internal const float AftershockNoise = 10f;

        //──── 收网协议（2.2）────
        //清剿波累计在场时长达标即触发（累计口径，跨波不清、直到弹出不复位）：
        //50s 标定=干净应对（T4 免疫 20s + 从 95 降到 60 约 23s）不触发，赖场或二进宫才触发
        internal const int DragnetAfterT4Ticks = 50 * 60;
        //噪音棘轮地板：高于 T4ReleaseBelow(60)，清剿波解除条件自此永不满足
        internal const float DragnetNoiseFloor = 70f;
        //收网期清剿波补员目标加压（5 → 7）
        internal const int DragnetSustainBonus = 2;

        //──── 热断链（2.3）────
        //站桩断链时长 / 离台中止半径（px）/ 猎杀波追加间隔（NotifySpotted 补员自动封顶）
        internal const int HotExtractTicks = 600;
        internal const float HotExtractRadius = 90f;
        internal const int HotExtractWaveInterval = 240;

        //──── 深潜评级（2.1；首轮数值必调：标定口径=两次满账铭刻+走到衰减区中段 ≈ A）────
        //基础分权重：铭刻（唯一硬通货，权重最高）/ 深度（全图 2360 列 ≈ 944 分封顶）/ 采集
        internal const int RatingSettledWeight = 12;
        internal const float RatingDepthWeight = 0.4f;
        internal const int RatingHarvestWeight = 6;
        //弹出结算：安全登出加成 / 烧断与死亡的总分折损（不清零，深度与已铭刻的意义保留）
        internal const int RatingSafeExitBonus = 250;
        internal const float RatingDisasterMul = 0.4f;
        //风格加成（可叠加，弹出时判定）
        internal const int RatingStyleGhost = 300;
        internal const int RatingStyleHeat = 200;
        internal const int RatingStyleHotExtract = 150;
        internal const int RatingStyleDragnet = 350;
        //评级阈值（S≥1800 / A≥1200 / B≥700 / C≥350，其余 D）
        internal const int RatingGradeS = 1800;
        internal const int RatingGradeA = 1200;
        internal const int RatingGradeB = 700;
        internal const int RatingGradeC = 350;
        //元奖励：历史最佳 A 级以上，每次进旧网账本容量 +=（与扩容坞同字段叠加，禁覆写）
        internal const int RatingLedgerBonus = 4;

        internal static readonly DistanceBand[] Bands;

        //宏观种子：主世界派生，宏观布局固定的缝（当前只缓存供天幕星野）
        internal static int MacroSeed { get; private set; }

        static OldNetMetrics() {
            Bands = [
                new DistanceBand("黑墙体", 0, WallCols, TileID.ObsidianBrick),
                new DistanceBand("墙脚带", WallCols, FootCols, TileID.GrayBrick),
                new DistanceBand("废墟带", WallCols + FootCols, RuinCols, TileID.StoneSlab),
                new DistanceBand("信号衰减区", FadeLeft, FadeCols, TileID.ObsidianBrick),
            ];
            int sum = 0;
            foreach (DistanceBand band in Bands) {
                sum += band.Right - band.Left;
            }
            if (sum != Width) {
                throw new InvalidOperationException($"[OldNet] 带表列数总和{sum}与Width{Width}不符");
            }
        }

        internal static DistanceBand? BandForColumn(int x) {
            foreach (DistanceBand band in Bands) {
                if (band.Contains(x)) {
                    return band;
                }
            }
            return null;
        }

        /// <summary>带索引（0=黑墙体..3=衰减区），越界给 -1；引导横幅与分带逻辑共用</summary>
        internal static int BandIndexForColumn(int x) {
            for (int i = 0; i < Bands.Length; i++) {
                if (Bands[i].Contains(x)) {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>进入前在主世界缓存宏观种子</summary>
        internal static void CacheMacroSeed() {
            MacroSeed = Main.ActiveWorldFileData?.SeedText?.GetHashCode() ?? 0;
        }

        /// <summary>按玩家所在物块列求每秒 RAM 底噪</summary>
        internal static float DrainPerSecondAt(int tileX) {
            int dist = tileX - WallCols - DrainSafeCols;
            if (dist <= 0) {
                return 0f;
            }
            return dist / 100f * DrainPer100Tiles;
        }

        /// <summary>
        /// 带内腐化度 0~1：墙脚带 0，废墟带缓升至 0.45，衰减区升满 1。
        /// 天幕湍流/调色提边/数据尘密度/环境声共用这一条口径
        /// </summary>
        internal static float CorruptionAt(int tileX) {
            int ruinLeft = WallCols + FootCols;
            if (tileX < ruinLeft) {
                return 0f;
            }
            if (tileX < FadeLeft) {
                float ruinT = (tileX - ruinLeft) / (float)RuinCols;
                return ruinT * 0.45f;
            }
            float fadeT = System.Math.Clamp((tileX - FadeLeft) / (float)FadeCols, 0f, 1f);
            return 0.45f + fadeT * 0.55f;
        }
    }
}
