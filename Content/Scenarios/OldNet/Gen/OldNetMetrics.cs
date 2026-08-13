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
    //蓝图 Doc/plans/OldNet/DESIGN.md，镜像 DungeonworldMetrics 惯例改横向
    internal static class OldNetMetrics
    {
        internal const int Width = 2400;
        internal const int Height = 600;
        //世界四周实心边界厚度
        internal const int BorderThick = 8;

        //════════ 横向带表（列） ════════

        //黑墙体 [0,40)：实心不可入，视觉由 BlackwallRender 接管
        internal const int WallCols = 40;
        //墙脚带 [40,700)：规整几何、安全区起步
        internal const int FootCols = 660;
        //废墟带 [700,1600)：M1 遗址主产区，M0 只有地板与节点
        internal const int RuinCols = 900;
        //信号衰减区 [1600,2400)：M0 实心封死，M3 开放为疯域
        internal const int FadeCols = Width - WallCols - FootCols - RuinCols;

        //════════ 纵向 ════════

        //地板主线（上表面基准行），起伏 ±FloorWobble
        internal const int FloorRow = 380;
        internal const int FloorWobble = 6;
        //地板实体向下浇筑到底部边界，地板以上为开放天空

        //worldSurface 压到地板带以下：玩法层判"地表"，天幕可见（与 Dungeonworld 相反）
        //rockLayer 再往下，只为满足原版分层判定的形式需求
        internal const int WorldSurfaceRow = 430;
        internal const int RockLayerRow = 500;

        //════════ 锚点 ════════

        internal const int SpawnX = 60;
        //登出终端在出生点与墙体之间
        internal const int LogoutX = 48;
        //出生区全平列数（生成 pass 与 ICE 撒布共用基准）
        internal const int SpawnFlatCols = 80;

        //════════ RAM 距离底噪（每秒） ════════
        //标定基准：基础 RAM 8 / 恢复 0.1s（RamSystem.DefaultBase*）
        //墙脚 SafeCols 内零消耗；此后每离墙 100 格 +DrainPer100，
        //新角色约在墙脚带中段收支平衡，废墟带净消耗——贪心半径随 RAM build 增长
        internal const int DrainSafeCols = 150;
        internal const float DrainPer100Tiles = 0.05f;

        //════════ 数据节点 ════════

        //单节点碎片产出；撒布数量见 M1 常量区分级配额
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

        //──── 节点分级撒布（M1b） ────
        internal const int NodePlainCount = 34;
        internal const int NodeEncryptCount = 10;
        internal const int NodeEventCount = 2;
        //废墟带内加密节点占比（墙脚带只出普通）
        internal const float RuinEncryptRatio = 0.4f;
        //加密节点：引导时长、价值倍数、站桩半径
        internal const int EncryptChannelTicks = 180;
        internal const int EncryptValueMul = 3;
        internal const float EncryptChannelRadius = 60f;

        //──── 封锁区与中继站（M1b） ────
        internal const int SealBoxCount = 2;
        internal const int SealBoxW = 14;
        internal const int SealBoxH = 8;
        internal const int SealBoxNodeMin = 6;
        internal const int SealBoxNodeMax = 10;
        //事件节点离封锁区最小距离（列）：拉闸的人要跑一段才能吃到糖
        internal const int EventToSealMinCols = 80;
        //中继站基准列位与抖动
        internal static readonly int[] RelayCols = [1000, 1400];
        internal const int RelayColJitter = 40;

        internal static readonly DistanceBand[] Bands;

        //宏观种子：主世界派生，宏观布局固定的缝（M0 只缓存不使用）
        internal static int MacroSeed { get; private set; }

        static OldNetMetrics() {
            Bands = [
                new DistanceBand("黑墙体", 0, WallCols, TileID.ObsidianBrick),
                new DistanceBand("墙脚带", WallCols, FootCols, TileID.GrayBrick),
                new DistanceBand("废墟带", WallCols + FootCols, RuinCols, TileID.StoneSlab),
                new DistanceBand("信号衰减区", WallCols + FootCols + RuinCols, FadeCols, TileID.ObsidianBrick),
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
    }
}
