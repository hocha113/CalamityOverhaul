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
        //新角色约在墙脚带中段收支平衡，废墟带净消耗——贪心半径随 RAM build 增长
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
