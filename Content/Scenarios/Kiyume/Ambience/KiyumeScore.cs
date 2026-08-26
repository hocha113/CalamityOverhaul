using CalamityOverhaul.Content.Scenarios.Kiyume.Gen;
using System;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Ambience
{
    /// <summary>声景点缀条目：带内周期播放的远景声（带归属 + 初相错开 + 全局冷却，由声景包调度）</summary>
    internal readonly struct KiyumeAccentCue
    {
        internal readonly int Band;         //0..4 = 深湖..远山
        internal readonly SoundStyle Style;
        internal readonly float Volume;
        internal readonly float Pitch;
        internal readonly int Period;       //平均周期（tick）
        internal readonly float Jitter;     //周期抖动 ±比例
        internal readonly int Hits;         //连响次数（滴水/枯枝/梆子双连）
        internal readonly int HitGap;       //连响间隔（tick）
        internal readonly float WindGate;   //戏剧风门槛（>0 时风值低于此不响，檐铃用）

        internal KiyumeAccentCue(int band, SoundStyle style, float volume, float pitch,
            int period, float jitter, int hits = 1, int hitGap = 0, float windGate = 0f) {
            Band = band;
            Style = style;
            Volume = volume;
            Pitch = pitch;
            Period = period;
            Jitter = jitter;
            Hits = hits;
            HitGap = hitGap;
            WindGate = windGate;
        }
    }

    /// <summary>
    /// 鬼梦非雾氛围层唯一调音面：床层曲线、点缀表、犬吠参数、导演常量全住这里，调音只改此文件。
    /// 分节纪律（装配令 W1.5）：C/D/E 包只在各自锚注释节内追加，不越节。
    /// </summary>
    internal static class KiyumeScore
    {
        //==== 硬顶（鬼梦无音乐，床层是唯一持续声，比深牢 0.16 再压低，糊不得）====
        internal const float LoopVolCap = 0.14f;
        internal const float AccentVolCap = 0.45f;
        /// <summary>点缀全局最小间隔 2.5s（犬吠提示同吃这条，声不踩声）</summary>
        internal const int AccentGlobalCooldown = 150;

        /// <summary>单发音量进硬顶：本层全部一次性发声（点缀/事件声/犬吠）过此口，调音失手不破帽</summary>
        internal static float CapAccent(float vol) => MathF.Min(vol, AccentVolCap);

        //==== 戏剧风（双频正弦 0..1）====
        //子世界天气停摆（NormalUpdates=false），风是自造的：风床音量、檐铃风门、
        //未来晾衣摆动共用这一条曲线，声画自洽（风声大时铃才响）
        internal const float WindMainPeriod = 3700f;
        internal const float WindSubPeriod = 1300f;
        internal const float WindSubWeight = 0.4f;

        /// <summary>t（tick 计数）处的戏剧风值 0..1</summary>
        internal static float DramaticWindAt(long t) {
            double main = Math.Sin(t * (Math.PI * 2.0 / WindMainPeriod));
            double sub = Math.Sin(t * (Math.PI * 2.0 / WindSubPeriod)) * WindSubWeight;
            return MathHelper.Clamp((float)((main + sub) / (1.0 + WindSubWeight)) * 0.5f + 0.5f, 0f, 1f);
        }

        //==== 床层曲线 ====

        //风床带心锚点音量：湖 0.05 → 滩涂 0.06（取湖村中点）→ 村 0.07 → 枯林 0.11 → 远山 0.09
        private static readonly float[] WindVolByBand = [0.05f, 0.06f, 0.07f, 0.11f, 0.09f];
        internal const float WindPitchBase = -0.55f;
        /// <summary>戏剧风对风床音量的摆幅 ±35%</summary>
        internal const float WindDramaticSwing = 0.35f;
        //雾线下闷压变奏：不加第三条循环，改调风床自己
        internal const float WindPitchSubmerged = -0.85f;
        internal const float WindSubmergedVolMul = 0.55f;
        /// <summary>没入雾线多深算全闷（px）：闷压变奏的空间平滑跨度，出入雾面无爆点</summary>
        internal const float SubmergeSmoothPx = 48f;

        //湖床：近湖淡入的浪涌白噪（Waterfall 压调是替身，正音在新音频愿望清单）
        internal const float LakeVolBase = 0.10f;
        internal const float LakePitch = -0.70f;
        /// <summary>湖床衰减跨度：水面右缘向东 200 tile 内衰减到零</summary>
        internal const float LakeFadeSpanPx = 200f * 16f;

        /// <summary>该列的风床基准音量（带心插值；进硬顶前再乘戏剧风/闷压/presence/duck）</summary>
        internal static float WindVolume(float column) => BandCurve(WindVolByBand, column);

        /// <summary>该世界 x 的湖床基准音量：水面右缘以西全强，向东线性衰减</summary>
        internal static float LakeVolume(float worldX) {
            float t = MathHelper.Clamp((worldX - KiyumeMetrics.WaterRightPx) / LakeFadeSpanPx, 0f, 1f);
            return LakeVolBase * (1f - t);
        }

        //带心列缓存（KiyumeMetrics.Bands 进程内不变）
        private static readonly float[] bandCenterCol = BuildBandCenters();

        private static float[] BuildBandCenters() {
            var bands = KiyumeMetrics.Bands;
            float[] centers = new float[bands.Length];
            for (int i = 0; i < centers.Length; i++) {
                centers[i] = (bands[i].Left + bands[i].Right) * 0.5f;
            }
            return centers;
        }

        /// <summary>带心锚点值 → 列位置连续插值（带间线性过渡，端外钳位）</summary>
        internal static float BandCurve(float[] anchors, float column) {
            var centers = bandCenterCol;
            if (column <= centers[0]) {
                return anchors[0];
            }
            for (int i = 0; i < centers.Length - 1; i++) {
                if (column > centers[i + 1]) {
                    continue;
                }
                float t = (column - centers[i]) / (centers[i + 1] - centers[i]);
                return MathHelper.Lerp(anchors[i], anchors[i + 1], t);
            }
            return anchors[^1];
        }

        //==== 点缀表（带序 0..4 = 深湖..远山；音源落位玩家 12~30 tile 随机方位）====
        //Dig 变调两处（更夫梆子/枯枝断）为试听备选，正音在新音频愿望清单（P5 计划书 §9，交用户裁决）
        internal static readonly KiyumeAccentCue[] Accents = [
            new(0, SoundID.SplashWeak, 0.22f, -0.45f, 20 * 60, 0.40f),               //湖：远水花
            new(1, SoundID.Drip,       0.14f, -0.10f, 14 * 60, 0.35f, 2, 8),         //滩涂：滴水（双连）
            new(2, SoundID.Item35,     0.16f, +0.65f, 24 * 60, 0.50f, 1, 0, 0.45f),  //村：檐铃（有风才响）
            new(2, SoundID.DoorOpen,   0.10f, -0.72f, 48 * 60, 0.40f),               //村：远门吱呀
            new(2, SoundID.Dig,        0.12f, +0.55f, 90 * 60, 0.30f, 2, 14),        //村：更夫梆子（双点）
            new(3, SoundID.Dig,        0.13f, +0.42f, 18 * 60, 0.35f, 2, 5),         //枯林：枯枝断（双连）
            new(3, SoundID.Owl,        0.10f, -0.15f, 55 * 60, 0.40f),               //枯林：夜枭
            new(4, SoundID.Thunder,    0.09f, -0.90f, 40 * 60, 0.35f),               //远山：没落下来的雷
        ];

        //==== 犬吠方位提示（裁决 21：>600px 归本层，≤600px 静默让位给 P2 犬实体声带）====
        internal const float BarkYieldDistPx = 600f;
        internal const int BarkPeriodMin = 600;
        internal const int BarkPeriodMax = 1500;
        /// <summary>真吠音量：600px 处 0.38，向远 1000px 内衰到 0.16（方位真、距离糊）</summary>
        internal const float BarkVolNear = 0.38f;
        internal const float BarkVolFar = 0.16f;
        internal const float BarkVolFalloffPx = 1000f;
        internal const float BarkPitch = -0.12f;
        //无犬保底假吠：雾里总得有狗叫
        internal const int FakeBarkPeriodMin = 2700;
        internal const int FakeBarkPeriodMax = 7200;
        internal const float FakeBarkDistMin = 800f;
        internal const float FakeBarkDistMax = 1400f;
        internal const float FakeBarkVol = 0.16f;
        /// <summary>四分之一概率换撕咬声：远听像犬吠打斗，雾里有狗在咬别的东西</summary>
        internal const int FakeBarkWorryOneIn = 4;

        //==== 导演常量（B 包：紧张度合成/档期门/预算，KiyumeDirector 消费）====

        /// <summary>紧张度 EMA 步长（≈1.5s 半衰）</summary>
        internal const float TensionLerp = 0.02f;
        //合成权重：雾浓 / 没入雾线 / 犬距 / 带偏置
        internal const float WFog = 0.35f;
        internal const float WSubmerged = 0.15f;
        internal const float WHound = 0.40f;
        /// <summary>滩涂/枯林带偏置：前不着村后不着店</summary>
        internal const float WStrand = 0.10f;
        /// <summary>犬距因子跨度：clamp(1 - 最近犬距/此值, 0, 1)</summary>
        internal const float HoundFactorSpanPx = 1200f;

        //公约惊吓窗（事件自报窗的默认参照：太低没铺垫，太高玩家已应激，再吓是噪音）
        internal const float ScareWindowLo = 0.35f;
        internal const float ScareWindowHi = 0.78f;
        /// <summary>共享惊吓冷却 90s</summary>
        internal const int GlobalScareCooldown = 5400;
        /// <summary>入世界热身 60s：落地先看世界再闹鬼</summary>
        internal const int WarmupTicks = 3600;
        /// <summary>近伤不吓 3s</summary>
        internal const int NoHurtTicks = 180;
        /// <summary>P2 让位距离：最近犬小于此值 A/S 全挂起、B 级周期 ×2</summary>
        internal const float HoundYieldDistPx = 900f;
        //吓完泄压：释放后 300f 内紧张度目标压到 0.55 再缓回 1
        internal const int CalmTicks = 300;
        internal const float CalmFloor = 0.55f;
        /// <summary>默认抽签分母：合格状态平均 60s 中一次（事件可自报覆盖，如月轮 2700）</summary>
        internal const int DefaultScareLottery = 3600;

        /// <summary>各档期每次进入预算：S 级 1 / A 级 2 / B 级不占槽给 0（硬复位即回满）</summary>
        internal static int ScareBudget(KiyumeScareId id) => id switch {
            KiyumeScareId.LampFall or KiyumeScareId.Moon => 1,
            KiyumeScareId.LampBehind or KiyumeScareId.StillBell
                or KiyumeScareId.Footprints or KiyumeScareId.Geta => 2,
            _ => 0,
        };

        //──C 灯火事件常量──

        /// <summary>
        /// 登记帽：村带四类灯具（裁决 24 三类 + 挂灯 tile 42，W4 生成域移交）最多登记盏数。
        /// 64→128（W4 抬帽）：灯道 ~30 柱 ×1.6 臂 ≈45 盏挂灯 + 殿内吊灯 + 既有窗火/烛/围炉
        /// 峰值估 ~95，64 会把扫描（x 升序）截在村东半，E1 向东推进即断
        /// </summary>
        internal const int LampScanCap = 128;
        //扫描窗行区间（tile）；列区间取 KiyumeMetrics.VillageLeft/GroveLeft
        internal const int LampScanRowTop = 380;
        internal const int LampScanRowBottom = 470;
        //熄灭拍轻声（Drip @灯位）与烟量
        internal const float LampSnuffVol = 0.22f;
        internal const float LampSnuffPitch = -0.35f;
        internal const int LampSnuffSmoke = 2;

        //E1 窗火蔓延熄灭（S 级）：各家怕的东西来了，都吹灯装睡
        internal const float LampFallWindowLo = 0.40f;
        internal const float LampFallWindowHi = 0.70f;
        /// <summary>逐盏推进间隔</summary>
        internal const int LampFallStep = 22;
        /// <summary>每盏熄灭 ease 帧（1→0）</summary>
        internal const int LampFallFadePerLamp = 6;
        /// <summary>全灭保持</summary>
        internal const int LampFallHold = 480;
        /// <summary>回亮逐盏间隔（反向，无声）</summary>
        internal const int LampRelightStep = 60;
        /// <summary>每盏回亮 ease 帧（0→1）</summary>
        internal const int LampRelightFade = 30;
        /// <summary>留最近盏数：全村只剩你面前这点光</summary>
        internal const int LampFallKeepNearest = 2;
        //选灯窗口（距玩家）
        internal const float LampFallPickMinPx = 300f;
        internal const float LampFallPickMaxPx = 1400f;
        /// <summary>登记少于此数降格 E2 单盏版（预算合并进 LampFall）</summary>
        internal const int LampFallMinLamps = 4;
        /// <summary>村带驻留门 120s（武装跳过）</summary>
        internal const int LampFallVillageStay = 7200;

        //E2 回头灯灭（A 级）：你不看它时它自便
        internal const float LampBehindWindowLo = 0.35f;
        internal const float LampBehindWindowHi = 0.75f;
        /// <summary>背向判定：dot(朝灯方向, 面朝) 低于此值</summary>
        internal const float LampBehindDot = -0.4f;
        internal const float LampBehindMinPx = 400f;
        internal const float LampBehindMaxPx = 900f;
        /// <summary>硬切帧数（1→0）</summary>
        internal const int LampBehindCut = 4;
        internal const int LampBehindHold = 1200;
        internal const int LampBehindRecover = 90;
        /// <summary>事件自冷却（叠加共享惊吓冷却）</summary>
        internal const int LampBehindSelfCooldown = 7200;
        //同帧关门声（有人回屋了）与烟量
        internal const float LampDoorVol = 0.18f;
        internal const float LampDoorPitch = -0.50f;
        internal const int LampBehindSmoke = 3;

        //E3 无风铃（A 级）：无风自响就是有东西路过
        internal const float StillBellWindowLo = 0.35f;
        internal const float StillBellWindowHi = 0.75f;
        /// <summary>戏剧风低于此算「无风」（床层同源，玩家刚亲耳确认过没风）</summary>
        internal const float StillBellWindGate = 0.18f;
        /// <summary>无风须持续帧数（武装门，武装也不跳过）</summary>
        internal const int StillBellGateHold = 90;
        //铃位：玩家侧后
        internal const float StillBellMinPx = 300f;
        internal const float StillBellMaxPx = 500f;
        internal const float StillBellVol = 0.30f;
        internal const float StillBellPitch = 0.65f;
        /// <summary>此半径内最近注册灯应铃微颤</summary>
        internal const float StillBellLampRadiusPx = 500f;
        /// <summary>微颤帧数（0.85±0.15 两个快周期）</summary>
        internal const int StillBellFlicker = 20;

        //──D 天幕事件常量──

        //E4 云后月轮（裁决 20 A 案：骨白体+血晕；S 级预算 1，敬畏拍不是惊吓拍）
        internal const float MoonWindowLo = 0.30f;
        internal const float MoonWindowHi = 0.60f;
        /// <summary>月轮抽签分母：合格状态平均 45s 中一次</summary>
        internal const int MoonLottery = 2700;
        /// <summary>驻留门：村/枯林/远山带连续驻留 ≥150s 才进抽签（湖上视线交给雾墙）</summary>
        internal const int MoonDwellTicks = 9000;
        //包络 12s：2.5s 入 / 6s 持 / 3.5s 出（smoothstep）
        internal const int MoonRiseTicks = 150;
        internal const int MoonHoldTicks = 360;
        internal const int MoonFadeTicks = 210;
        //触发帧世界屏息：床层压到 0.5 共 720f（=包络全长），无 stinger，月不出声
        internal const float MoonDuckAmount = 0.5f;
        internal const int MoonDuckFrames = 720;
        //月位定值：uv x 0.62、地平线上方 0.36（月不动，月只会被看见）
        internal const float MoonPosU = 0.62f;
        internal const float MoonPosAboveHorizon = 0.36f;

        //E5 远山火把队列（B 级：不占槽自走周期，枯林/远山带投放，EMBER 零新色）
        internal const int TorchPeriodMin = 5400;
        internal const int TorchPeriodMax = 9000;
        internal const int TorchCountMin = 5;
        internal const int TorchCountMax = 9;
        /// <summary>点距（uv）：与 KiyumeSky.fx 的 tSpacing（×1.05 入近脊域）同步改</summary>
        internal const float TorchSpacingUv = 0.012f;
        /// <summary>队头行进速度（uv/f）：慢、稳、不回头</summary>
        internal const float TorchSpeedUv = 0.0009f;
        /// <summary>一次行进硬帽 45s（正常按队尾出视野自然收尾，约 33~40s）</summary>
        internal const int TorchCapTicks = 2700;

        //──E 微演出常量──

        //E6 鸦群掠雾（B 级：巡航自走周期 + 导演 NotifyCrowOmen 惊起前置信号）
        internal const int CrowCountMin = 3;
        internal const int CrowCountMax = 6;
        //同向掠过的个体速度带（px/f）
        internal const float CrowSpeedMin = 3.2f;
        internal const float CrowSpeedMax = 4.6f;
        //贴雾高差：翅膀几乎碰到雾面（px，个体各异）
        internal const float CrowFogGapMin = 30f;
        internal const float CrowFogGapMax = 80f;
        /// <summary>队形纵向错落 ±px</summary>
        internal const float CrowScatterPx = 18f;
        internal const int CrowPeriodMin = 3600;
        internal const int CrowPeriodMax = 7200;
        //惊起演出全长（转发发生在档期过门帧，此时长即鸦群先于事件高潮在场的提前量）
        internal const int CrowOmenLeadMin = 180;
        internal const int CrowOmenLeadMax = 300;
        /// <summary>巡航投放的雾存在感门槛</summary>
        internal const float CrowPresenceGate = 0.35f;
        /// <summary>巡航投放门：玩家列雾线密度低于此不投（枯林以东没雾可贴）</summary>
        internal const float CrowCruiseFogGate = 0.25f;
        /// <summary>飞行带对地表的最小净空（低潮时雾线沉进村地之下，鸦不许贴地钻土）</summary>
        internal const float CrowGroundClearPx = 48f;
        /// <summary>巡航入画/出画的屏缘外余量（px）</summary>
        internal const float CrowEdgeMarginPx = 220f;
        //起飞一声夜鸟惊起（裁决 22：Owl 变调替声）
        internal const float CrowOwlVol = 0.12f;
        internal const float CrowOwlPitch = 0.35f;

        //E7 水面无源涟漪（B 级）：湖底下有东西偶尔上来换口气
        internal const int RipplePeriodMin = 2700;
        internal const int RipplePeriodMax = 5400;
        /// <summary>出带/窗口空的短重试</summary>
        internal const int RippleRetryTicks = 300;
        /// <summary>湖可见带：ShoalLeft 东扩此列数以内才投</summary>
        internal const int RippleZoneExtraCols = 200;
        /// <summary>落点视野窗：屏宽 ±此比例</summary>
        internal const float RippleViewFrac = 0.4f;
        internal const float RippleStrength = 1.6f;
        internal static readonly Vector2 RippleSize = new(90f, 42f);
        internal const int RippleBubbles = 2;
        internal const float RippleSplashVol = 0.15f;
        internal const float RippleSplashPitch = -0.60f;

        //E8 泥地脚印（A 级）：看不见的赶路人，泥地替它记账
        internal const float FootprintWindowLo = 0.35f;
        internal const float FootprintWindowHi = 0.70f;
        internal const int FootprintSteps = 8;
        /// <summary>步行节奏：每步间隔（tick）</summary>
        internal const int FootprintStepTicks = 12;
        //起点在玩家雾浓侧方
        internal const float FootprintStartMinPx = 240f;
        internal const float FootprintStartMaxPx = 420f;
        //步幅（px）
        internal const float FootprintStrideMinPx = 28f;
        internal const float FootprintStrideMaxPx = 38f;
        /// <summary>印记寿命（PRT）与尾段渐隐帧</summary>
        internal const int FootprintLife = 300;
        internal const int FootprintFadeTail = 60;
        /// <summary>走完后槽再压一小段（防连吓贴脸）</summary>
        internal const int FootprintReleaseTail = 60;
        internal const float FootprintStepVol = 0.10f;
        internal const float FootprintStepPitch = 0.25f;
        //泥斑体色（暗层真 alpha，AlphaBlend 直染）
        internal static readonly Color FootprintColor = new(26, 12, 12);
        internal const float FootprintColorMul = 0.85f;

        //E9 雾里木屐（A 级纯音频）：嗒、嗒、嗒，转身路是空的
        internal const float GetaWindowLo = 0.40f;
        internal const float GetaWindowHi = 0.75f;
        /// <summary>雾门（武装也拦）：雾不浓这声就没处躲</summary>
        internal const float GetaFogGate = 0.30f;
        internal const int GetaHitsMin = 3;
        internal const int GetaHitsMax = 4;
        internal const int GetaGapTicks = 20;
        //背后定点距离（px）
        internal const float GetaDistMinPx = 250f;
        internal const float GetaDistMaxPx = 450f;
        //Dig 变调为试听备选，正音在新音频愿望清单（P5 计划书 §9，裁决 22）
        internal const float GetaVol = 0.16f;
        internal const float GetaPitch = 0.55f;

        //E10 檐上鸦阵（R2-C：P4 点子 9 栖息层，飞行层=E6；判定与演出在 KiyumeCrowRoost）
        /// <summary>栖息点采样帽（全村 4~8 个，门洞表匀取）</summary>
        internal const int RoostPointMax = 8;
        /// <summary>栖息点最小间距（列）：候选按此收，超帽再匀取摊满全村</summary>
        internal const int RoostSpacingCols = 60;
        /// <summary>上探瓦顶的探程（行）：民居脊 ≤16 行、望楼脊 ≤30 行，留余</summary>
        internal const int RoostProbeUpRows = 32;
        //每点蹲鸦只数
        internal const int RoostCrowMin = 2;
        internal const int RoostCrowMax = 4;
        /// <summary>惊起巡检节拍（tick）：与 FirePulse 寿命 20t 同长，单次开火恰好被采到一次</summary>
        internal const int RoostCheckTicks = 20;
        /// <summary>奔跑惊起半径（px，速度门吃 KiyumeHoundMetrics.RunSpeedGate）</summary>
        internal const float RoostRunDistPx = 200f;
        /// <summary>开火惊起半径（px）：鸦对枪声比对脚步远敏</summary>
        internal const float RoostFireDistPx = 420f;
        /// <summary>惊起后该点冷却 90s：这窝暂时不回来</summary>
        internal const int RoostCooldownTicks = 5400;
        /// <summary>炸窝噪声量（裁决 11 消费）：高于 WeaponImpulse(1.8)，鸦阵炸窝比枪声更远闻</summary>
        internal const float RoostNoiseAmount = 2.2f;
        /// <summary>克制律占闸时长（tick）：全村同刻至多一处惊起演出</summary>
        internal const int RoostShowHoldTicks = 300;
        /// <summary>炸窝散拍（tick）：栖鸦上抛渐隐时长，之后由飞行层接飞</summary>
        internal const int RoostBurstTicks = 36;
        /// <summary>第二声鸟鸣相对炸窝起拍的延迟（tick）</summary>
        internal const int RoostCryGapTicks = 9;
        //炸窝鸣叫（裁决 22 零新音频：Bird 低变调两声 + 接飞层 Owl 一声 = 2~3 声）
        internal const float RoostCryVol = 0.30f;
        internal const float RoostCryPitch = -0.55f;
        /// <summary>栖息态体感透明度（乘雾存在感）</summary>
        internal const float RoostBodyAlpha = 0.85f;
        /// <summary>联机客户端自扫节拍（tick）：降级路径，玩家近旁 ±64 列步进 4 找瓦顶</summary>
        internal const int RoostClientScanTicks = 60;
    }
}
