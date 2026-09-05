using System.Collections.Generic;
using System.Linq;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core
{
    /// <summary>
    /// 荒花沙蟒战斗调参中心。身份：贴地爬行的节肢巨蟒，中速巡曳把八腿步态与
    /// 鳌足姿势展示出来，威胁靠喷沙与带预警线的冲刺/扑击，不靠持续高速。
    /// 克眼后档位，普通模式基数，专家/大师走原版缩放。
    /// </summary>
    internal static class BssDirector
    {
        //==================== 编制 ====================

        /// <summary>体节数（不含头尾）</summary>
        public const int BodyCount = 20;

        /// <summary>红花节款式号（<c>BSS/Body</c> 帧 2：橙囊发射器，钉刺/花瓣从这里出）</summary>
        public const int StyleBloom = 2;
        /// <summary>
        /// 体节款式排布（链序 → <c>BSS/Body</c> 帧序：0 绿赘 / 1 干净 / 2 橙囊红花 / 3 褐叶 / 4 青掌）。
        /// 红花每三节一朵（链序 2、5、8、11、14、17），花间两节是声明的空间缺口；
        /// 其余款式错落排布去机械感。长度 = <see cref="BodyCount"/>，尾节不查表
        /// </summary>
        public static readonly int[] BodyStyleLayout = {
            1, 0, 2, 3, 1, 2, 4, 0, 2, 1,
            3, 2, 0, 4, 2, 1, 3, 2, 0, 4,
        };
        /// <summary>按链序查款式（越界回干净款）</summary>
        public static int BodyStyle(int ordinal)
            => ordinal >= 0 && ordinal < BodyStyleLayout.Length ? BodyStyleLayout[ordinal] : 1;
        /// <summary>全部红花节链序（图鉴散瓣与发射器枚举共用）</summary>
        public static IEnumerable<int> BloomOrdinals
            => Enumerable.Range(0, BodyStyleLayout.Length).Where(o => BodyStyleLayout[o] == StyleBloom);
        /// <summary>
        /// 整体放大（头/体/尾 NPC.scale，节距与腿爪骨长同乘）。贴图为 2x 像素稿，
        /// 原尺寸下整条蛇细而短，放大后头宽约 160px、体节扇冠约 90px
        /// </summary>
        public const float BodyScale = 1.35f;
        /// <summary>
        /// 节距（贴图像素，世界距离再乘 <see cref="BodyScale"/>）。体节帧内容高 98：
        /// 扇冠 0~32、主体 32~70、腰 70~84、前端圆叶 84~97。前节压后节绘制，
        /// 82 让后节露出扇冠 + 主体 + 整段腰，腰的收细成为可见的关节读数；
        /// 70 时腰整段藏在前节扇冠下，整条链读作一根贴满扇冠的管子
        /// </summary>
        public const float SegmentGap = 82f;
        /// <summary>
        /// 颈距（头心 → 0 号体节心，贴图像素）。头贴图背部只有两侧仙人掌臂伸到边缘，
        /// 中轴实心轮廓到头心后 33px 就断了（第 34 行起才全宽实心）；0 号节前端圆叶
        /// 后沿在节心前 34px，64 让圆叶后沿落在头心后 30px，藏进实心区留 3px 余量。
        /// 旧值 88 把圆叶整个露在头后，静止就有缝、拉伸时裂成明显空档。
        /// 颈段的节距动态系数只许收不许放（见 BssBody.FollowChain），否则余量守不住
        /// </summary>
        public const float NeckGap = 64f;
        /// <summary>
        /// 尾节绘制原点前移（像素）：尾贴图 138 高，基部扇冠在下半段；按包围盒中心画会被前一节整个盖住，
        /// 原点前移让基部扇冠对齐体节扇冠的位置，T 形尾端多出的部分向后伸。
        /// 14 是上限：尾前端尖在原点前 54px，节距 82 时落在前节心后 28px，刚好压在前节扇冠（18~50）之下
        /// </summary>
        public const float TailOriginShift = 14f;

        //==================== 基础数值 ====================

        /// <summary>头基础生命（统一血池在生成体节时汇总，总池约 4200）</summary>
        public const int HeadLife = 1800;
        /// <summary>单体节生命（并入血池）</summary>
        public const int BodyLife = 110;
        /// <summary>尾节生命（并入血池）</summary>
        public const int TailLife = 200;

        /// <summary>接触伤害（普通基数）：头/体/尾</summary>
        public const int HeadContact = 32;
        public const int BodyContact = 20;
        public const int TailContact = 16;

        /// <summary>防御：头软体硬，鼓励打头</summary>
        public const int HeadDefense = 4;
        public const int BodyDefense = 12;
        public const int TailDefense = 8;

        /// <summary>触发死亡演出的生命阈值</summary>
        public const int DeathTriggerLife = 30;
        /// <summary>沙暴加剧转阶段血线</summary>
        public const float StormThreshold = 0.6f;
        /// <summary>繁花怒放血线</summary>
        public const float ApexThreshold = 0.25f;

        //==================== 弹幕基伤（normal/expert，走 GetAttackDamage_ForProjectiles）====================

        public static (float Normal, float Expert) NeedleDamage => (16f, 14f);
        public static (float Normal, float Expert) SandGlobDamage => (15f, 13f);
        public static (float Normal, float Expert) CactusBallDamage => (24f, 20f);
        public static (float Normal, float Expert) PetalDamage => (17f, 15f);
        /// <summary>掀沙浪：矮浪可跳，命中价高</summary>
        public static (float Normal, float Expert) SurgeDamage => (22f, 18f);
        /// <summary>旋沙龙卷：慢漂高柱，命中还掀人上天</summary>
        public static (float Normal, float Expert) DustDevilDamage => (18f, 15f);
        /// <summary>横风花刃：快瓣直飞</summary>
        public static (float Normal, float Expert) GalePetalDamage => (16f, 14f);
        /// <summary>巨钳合击：贴脸惩罚，全招最重的一口</summary>
        public static (float Normal, float Expert) PincerSnapDamage => (26f, 22f);

        //==================== 感知与脱战 ====================

        /// <summary>目标失效判定距离</summary>
        public const float MaxFindDistance = 5600f;
        /// <summary>
        /// 追击阀触发距离：拉远到此距离才插入钻地追击连接件。配合单发闸
        /// （用过一次必须走一轮轮换才能再用），防止机动战里追击无限复读
        /// </summary>
        public const float ChaseValveDistance = 1500f;
        /// <summary>远距回归阀触发距离（钻地瞬移贴回）</summary>
        public const float FarSnapDistance = 2600f;

        //==================== 爬行（中速：步态是主角）====================

        /// <summary>巡曳速度（屏内来回爬的常速，八腿换步清晰可读）</summary>
        public const float CrawlCruiseSpeed = 8f;
        /// <summary>追赶速度（拉远时的快步，仍在步行档，不进滑刹）</summary>
        public const float CrawlChaseSpeed = 12f;
        /// <summary>转身减速：接近巡曳折返点时的爬速</summary>
        public const float CrawlTurnSpeed = 4.5f;
        /// <summary>头心贴地高度（随 <see cref="BodyScale"/> 同比：体节下缘离地约 12px，腿有迈步余地，头下半截照旧埋沙）</summary>
        public const float CrawlRideHeight = 46f;
        /// <summary>地形前探距离</summary>
        public const float CrawlLookahead = 130f;

        //==================== 巡曳（hub：在玩家两侧折返爬行，整条蛇始终在屏内）====================

        /// <summary>折返点相对玩家的横向距离（屏宽一半以内，整链在屏内爬过）</summary>
        public const float PatrolOffset = 480f;
        /// <summary>到达折返点的判定带</summary>
        public const float PatrolArriveBand = 70f;
        /// <summary>折返减速带：距折返点此距离内降到转身速</summary>
        public const float PatrolSlowBand = 200f;
        /// <summary>单程最长帧数（地形卡住也要折返）</summary>
        public const int PatrolLegMaxFrames = 150;
        /// <summary>拉远追赶阈：玩家离得比这远就不折返，直线追</summary>
        public const float PatrolChaseDistance = 820f;

        //==================== 沙面掠冲（蓄力后撤 + 预警线 + 一帧爆发 + 硬刹）====================

        /// <summary>就位段帧数（拉开冲刺跑道）</summary>
        public const int DashStalkFrames = 6;
        /// <summary>蓄力后撤帧数（预告主体：反向运动 + 黄色预警线）</summary>
        public const int DashWindupFrames = 26;
        /// <summary>锁向提前量：出手前几帧死向（预告即承诺，预警线同拍白闪锁定）</summary>
        public const int DashLockLead = 9;
        /// <summary>掠冲初速（中速身份的爆发档：与巡曳 8 形成四倍反差）</summary>
        public const float DashSpeed = 34f;
        /// <summary>飞行帧数</summary>
        public const int DashFlightFrames = 16;
        /// <summary>硬刹帧数（×0.66/帧）</summary>
        public const int DashBrakeFrames = 8;
        /// <summary>接触伤害的速度门槛</summary>
        public const float DashContactSpeed = 20f;
        /// <summary>冲刺跑道最短距离：太近先退开再冲，杀贴脸秒杀</summary>
        public const float DashRunwayMin = 380f;
        /// <summary>
        /// 掉头助跑最短路程（约 1.5 节距）：蓄力前沿冲刺线前进这么远，
        /// 链条重排到身后，后撤蓄力才是"全身拉弓"而非把脖子甩上冲刺线
        /// </summary>
        public const float DashAlignRunPx = 120f;
        /// <summary>射向相对水平的最大仰角（弧度，贴地掠过的身份）</summary>
        public const float DashMaxPitch = 0.24f;
        /// <summary>连冲次数：P1 两段，P2 起三段</summary>
        public static int DashReps(int phase) => phase >= 2 ? 3 : 2;
        /// <summary>尾迹掀沙间隔帧（P2 起沿冲刺路径掀起沙弹）</summary>
        public const int DashWakeGap = 4;
        /// <summary>预警线长度（像素）</summary>
        public const float DashOmenLength = 1500f;

        //==================== 蹲伏扑击（八腿蹲紧 + 预警线 + 抛物跃扑 + 落地沙爆）====================

        /// <summary>就位帧数上限（转向面对玩家、爬到起跳距离）</summary>
        public const int PounceStalkFrames = 30;
        /// <summary>起跳最短横距：太近先退开（扑击要有可读的弧线）</summary>
        public const float PounceMinRange = 260f;
        /// <summary>起跳最远横距：太远先贴近</summary>
        public const float PounceMaxRange = 760f;
        /// <summary>蹲伏蓄势帧数（预警线寿命）</summary>
        public const int PounceCrouchFrames = 30;
        /// <summary>锁向提前量（出手前死向）</summary>
        public const int PounceLockLead = 9;
        /// <summary>扑击抛物飞行帧数（弹道反解用）</summary>
        public const float PounceFlightTime = 30f;
        /// <summary>扑击重力</summary>
        public const float PounceGravity = 0.62f;
        /// <summary>起跳速度上限（弹道反解后钳制）</summary>
        public const float PounceMaxSpeed = 32f;
        /// <summary>接触伤害的速度门槛</summary>
        public const float PounceContactSpeed = 14f;
        /// <summary>落地收势帧数</summary>
        public const int PounceRecoverFrames = 16;
        /// <summary>落地沙爆沙球数（P2 起；200 度上扇，两侧贴地留逃生道）</summary>
        public const int PounceLandGlobs = 6;
        /// <summary>扑击次数：P3 两连扑</summary>
        public static int PounceReps(int phase) => phase >= 3 ? 2 : 1;

        //==================== 破土突袭 ====================

        /// <summary>破土预告帧数（沙丘隆起 omen 与出土预警线的寿命）</summary>
        public const int BreachTelegraphFrames = 30;
        /// <summary>破土出土初速</summary>
        public const float BreachLaunchSpeed = 30f;
        /// <summary>突袭段重力</summary>
        public const float LungeGravity = 0.58f;
        /// <summary>接触伤害的速度门槛（伤害窗=可见冲势）</summary>
        public const float LungeContactSpeed = 13f;
        /// <summary>地下接近速度（鱼雷档）</summary>
        public const float LungeDigSpeed = 26f;
        /// <summary>突袭循环数：单招收短（P1 两次，P2 起三次），把时长还给轮换密度</summary>
        public static int LungeCycles(int phase) => phase >= 2 ? 3 : 2;
        /// <summary>破土喷发沙弹数（200 度上扇，贴地两侧留逃生道，声明见状态）</summary>
        public const int BreachEruptGlobs = 8;
        /// <summary>破土喷发扇面总角（度）</summary>
        public const float BreachEruptArcDeg = 200f;

        //==================== 喷沙（行进间齐射，不站桩）====================

        /// <summary>锁定前的跟踪帧数</summary>
        public const int SpitTrackFrames = 12;
        /// <summary>锁定后的吸气帧数</summary>
        public const int SpitInhaleFrames = 10;
        /// <summary>齐射间隔</summary>
        public const int SpitVolleyGap = 5;
        /// <summary>齐射次数（每轮 2 发，沿扇面轮转车道）</summary>
        public const int SpitVolleys = 6;
        /// <summary>最小射距：贴脸不吐沙，邀请骑脸压血</summary>
        public const float SpitMinDistance = 230f;
        /// <summary>沙团初速</summary>
        public const float SandGlobSpeed = 15f;
        /// <summary>沙团重力（弹道解算与弹幕本体共用）</summary>
        public const float SandGlobGravity = 0.30f;
        /// <summary>高抛沙团（ai[0]=1 变体）的重力倍率：低重力长滞空，弧顶更高</summary>
        public const float RainGlobGravityMul = 0.7f;
        /// <summary>齐射期爬速（边喷边挪，不站桩也不追着人跑）</summary>
        public const float SpitCrawlSpeed = 3.5f;

        //==================== hub 骚扰刺（攻击欲望的底噪：巡曳中也在咬）====================

        /// <summary>骚扰甩刺周期（帧，按阶段提速）</summary>
        public static int HarassGap(int phase) => phase switch {
            >= 3 => 26,
            2 => 34,
            _ => 46,
        };
        /// <summary>骚扰预亮帧数（红花节先亮再射 = 预告）</summary>
        public const int HarassGlowLead = 12;
        /// <summary>每次骚扰的钉刺数</summary>
        public const int HarassNeedles = 2;

        //==================== 仙人掌刺球 ====================

        /// <summary>刺球重力</summary>
        public const float BallGravity = 0.30f;
        /// <summary>落地弹跳次数上限</summary>
        public const int BallBounces = 2;
        /// <summary>引爆前的闪烁预告帧数</summary>
        public const int BallFuseFrames = 26;
        /// <summary>爆裂钉刺数（240 度上半扇，贴地两侧留逃生道，声明见弹幕类）</summary>
        public const int BallBurstNeedles = 10;
        /// <summary>抛球数：P3 五颗，其余四颗</summary>
        public static int BallCount(int phase) => phase >= 3 ? 5 : 4;

        //==================== 针刺涟漪 ====================

        /// <summary>预告波时长（红花节逐节亮起）</summary>
        public const int RippleTelegraphFrames = 24;
        /// <summary>发射波时长（波前扫过红花节即发射）</summary>
        public const int RippleFireFrames = 44;
        /// <summary>钉刺初速</summary>
        public const float NeedleSpeed = 12.5f;
        /// <summary>每朵红花的钉刺数（法向扇 ±NeedleFanHalf）</summary>
        public const int NeedlesPerFlower = 3;
        /// <summary>花刺扇半角（弧度）</summary>
        public const float NeedleFanHalf = 0.26f;

        //==================== 抖擞花瓣 ====================

        /// <summary>抖动节拍数（P3 加一拍）</summary>
        public const int ShakeBeats = 3;
        /// <summary>单拍：蓄势/抖动/歇止帧数</summary>
        public const int ShakeWindup = 8;
        public const int ShakeBurst = 10;
        public const int ShakeRest = 4;
        /// <summary>每拍每朵红花的花瓣数</summary>
        public const int PetalsPerFlower = 3;
        /// <summary>花瓣出生点相对红花节的横向抖动上限（走廊声明：花道间距≈红花链序差×节距−2×此值）</summary>
        public const float PetalLaneHalfWidth = 26f;

        //==================== 沙泉行军（立起砸地，冲击波沿地行军接连喷发）====================

        /// <summary>就位接近帧数上限（贴到出手距离即早退）</summary>
        public const int GeyserApproachFrames = 40;
        /// <summary>立起蓄势帧数（立起剪影本身即预告）</summary>
        public const int GeyserRaiseFrames = 24;
        /// <summary>砸地下坠初速</summary>
        public const float GeyserSlamSpeed = 26f;
        /// <summary>行军泉数（单向；P3 双向各此数）</summary>
        public const int GeyserCount = 6;
        /// <summary>泉距（步距即站缝逃生道：泉威胁面窄于缝宽）</summary>
        public const float GeyserStepPx = 120f;
        /// <summary>行军步进间隔帧</summary>
        public const int GeyserStepGap = 8;
        /// <summary>单泉隆包预告帧数（短版 omen，脚下鼓包即警报）</summary>
        public const int GeyserOmenFrames = 20;
        /// <summary>单泉喷发沙球数（近竖直上抛，回落是第二拍威胁）</summary>
        public const int GeyserGlobsEach = 3;

        //==================== 沙暴（入场即起，全程压场）====================

        /// <summary>入场破土后沙暴拉满的帧数</summary>
        public const int StormRiseFrames = 40;
        /// <summary>各阶段沙暴底线：P1 就是满级沙暴，P2/P3 靠加密风沙与色调加剧</summary>
        public static float StormFloor(int phase) => phase >= 2 ? 1f : 0.9f;

        //==================== 鳌足扬沙（双螯掘沙 → 过顶抡起 → 高弧沙雨落点包夹）====================

        /// <summary>就位帧数上限（贴到出手距离即早退）</summary>
        public const int FlingApproachFrames = 30;
        /// <summary>出手距离带上限：太远先贴近（高弧要够得着）</summary>
        public const float FlingRange = 640f;
        /// <summary>掘沙帧数（螯尖插沙 + 沙面渗沙 = 预告主体）</summary>
        public const int FlingScoopFrames = 22;
        /// <summary>过顶抡掷帧数（前段向后抡起拉弓，后段鞭向前上）</summary>
        public const int FlingHurlFrames = 12;
        /// <summary>出手帧（抡到前上方释放点）</summary>
        public const int FlingReleaseFrame = 8;
        /// <summary>高抛竖直初速：配合低重力变体，滞空约 2 秒、弧顶约 400px，整段弧线可读</summary>
        public const float RainGlobLaunchVy = -13f;
        /// <summary>落点间距（声明的站缝：相邻沙团落点之间可站人）</summary>
        public const float RainSpacing = 130f;
        /// <summary>每次扬沙的沙团数（奇数，中心落在预测位）</summary>
        public static int RainGlobs(int phase) => phase >= 3 ? 7 : 5;
        /// <summary>连掷次数：P2 起两掷</summary>
        public static int FlingReps(int phase) => phase >= 2 ? 2 : 1;
        /// <summary>收势帧数</summary>
        public const int FlingRecoverFrames = 16;

        //==================== 翻身掀浪（潜沙翻身 → 沙浪沿地行进；矮浪单跳可越）====================

        /// <summary>入土前摇帧数</summary>
        public const int SurgeDiveFrames = 10;
        /// <summary>沙下翻身蓄势帧数（地表震颤 + 渗沙 + 沙暴脉冲 = 预告）</summary>
        public const int SurgeRollFrames = 26;
        /// <summary>浪高（像素；低于单跳，原地起跳即可越过）</summary>
        public const float SurgeWaveHeight = 78f;
        /// <summary>浪速（高于步行速，逼玩家跳而不是跑）</summary>
        public const float SurgeWaveSpeed = 9.5f;
        /// <summary>浪行进帧数</summary>
        public const int SurgeWaveTravelFrames = 150;
        /// <summary>P3 第二浪延迟帧（落地再跳的二段节奏）</summary>
        public const int SurgeSecondWaveDelay = 42;
        /// <summary>浪数：P3 双浪</summary>
        public static int SurgeWaves(int phase) => phase >= 3 ? 2 : 1;
        /// <summary>浪起点相对翻身点的横向偏移</summary>
        public const float SurgeLaunchOffset = 90f;
        /// <summary>翻身后出土帧数上限</summary>
        public const int SurgeEmergeFrames = 60;

        //==================== 沙鳍追猎（沙下追踪鳍浪 → 隆包 → 竖直咬起）====================

        /// <summary>沙下追踪横速（略高于步行档，逼玩家保持移动）</summary>
        public const float FinTrackSpeed = 6.6f;
        /// <summary>单次追踪帧数上限（追不上就地咬）</summary>
        public const int FinTrackMaxFrames = 50;
        /// <summary>头位与玩家横向对齐持续帧数即锁定</summary>
        public const int FinLockFrames = 10;
        /// <summary>对齐判定带（像素）</summary>
        public const float FinLockBand = 48f;
        /// <summary>咬前隆包预告帧数</summary>
        public const int FinBulgeFrames = 18;
        /// <summary>竖直咬起初速</summary>
        public const float FinBiteSpeed = 27f;
        /// <summary>咬起段重力</summary>
        public const float FinBiteGravity = 0.95f;
        /// <summary>接触伤害的速度门槛</summary>
        public const float FinContactSpeed = 12f;
        /// <summary>追踪深度（头心在地表下，像素）</summary>
        public const float FinDepth = 130f;
        /// <summary>咬起次数：P2 起三咬</summary>
        public static int FinBites(int phase) => phase >= 2 ? 3 : 2;

        //==================== 旋沙龙卷（P2 起：怒吼拧出沙柱，顺风漂移）====================

        /// <summary>立起怒吼帧数</summary>
        public const int DevilRoarFrames = 26;
        /// <summary>沙柱成形帧数（地面旋沙无伤 = 预告）</summary>
        public const int DevilFormFrames = 34;
        /// <summary>沙柱高度</summary>
        public const float DevilHeight = 300f;
        /// <summary>沙柱半宽</summary>
        public const float DevilHalfWidth = 34f;
        /// <summary>顺风漂速</summary>
        public const float DevilDriftSpeed = 4.2f;
        /// <summary>成形后寿命</summary>
        public const int DevilLifeFrames = 260;
        /// <summary>龙卷间距（声明的逃生道：柱间可穿）</summary>
        public const float DevilSpacing = 380f;
        /// <summary>龙卷数：P3 三柱</summary>
        public static int DevilCount(int phase) => phase >= 3 ? 3 : 2;
        /// <summary>命中掀飞的竖直初速</summary>
        public const float DevilLift = -7f;
        /// <summary>首柱在玩家上风侧的起点偏移</summary>
        public const float DevilLeadOffset = 220f;
        /// <summary>P3 沙柱甩瓣间隔帧</summary>
        public const int DevilPetalGap = 26;

        //==================== 巨钳合击（贴脸惩罚：张螯 → 前扑 → 钳合）====================

        /// <summary>选招距离阀：玩家在此距离内才选合击，否则换喷沙（近钳远喷）</summary>
        public const float SnapTriggerRange = 380f;
        /// <summary>张螯预告帧数</summary>
        public const int SnapSpreadFrames = 20;
        /// <summary>连击第二次张螯（已被咬过一次，缩短但不省）</summary>
        public const int SnapRespreadFrames = 14;
        /// <summary>锁向提前量（出手前死向）</summary>
        public const int SnapLockLead = 8;
        /// <summary>前扑帧数</summary>
        public const int SnapLungeFrames = 6;
        /// <summary>前扑速度</summary>
        public const float SnapLungeSpeed = 22f;
        /// <summary>钳点距出手时头心的最远距离（前扑路程 132 + 嘴位 89 + 螯伸展，不超出螯的视觉触及）</summary>
        public const float SnapReach = 320f;
        /// <summary>钳击判定半径</summary>
        public const float SnapRadius = 60f;
        /// <summary>钳击判定存活帧数</summary>
        public const int SnapHitFrames = 6;
        /// <summary>收势帧数</summary>
        public const int SnapRecoverFrames = 18;
        /// <summary>钳击次数：P2 起两钳</summary>
        public static int SnapReps(int phase) => phase >= 2 ? 2 : 1;

        //==================== 横风花刃（P2 起：绕到上风侧 → 立起怒吼 → 花瓣顺风成车道横飞）====================

        /// <summary>上风侧就位距离</summary>
        public const float GaleUpwindOffset = 560f;
        /// <summary>就位帧数上限</summary>
        public const int GaleApproachFrames = 50;
        /// <summary>立起帧数</summary>
        public const int GaleRaiseFrames = 20;
        /// <summary>怒吼到首波的帧数</summary>
        public const int GaleRoarLead = 10;
        /// <summary>花刃波数</summary>
        public const int GaleBursts = 3;
        /// <summary>波间隔</summary>
        public const int GaleBurstGap = 16;
        /// <summary>花刃横飞速度</summary>
        public const float GalePetalSpeed = 11f;
        /// <summary>车道离地高度表（声明：站立被低道打、跳跃处于低中道之间的净空）</summary>
        public static readonly float[] GaleLaneHeights = { 36f, 196f, 336f };
        /// <summary>车道内抖动半高</summary>
        public const float GaleLaneHalfHeight = 10f;
        /// <summary>横风对本地玩家的推力（每帧，轻推不控场）</summary>
        public const float GalePushForce = 0.1f;
        /// <summary>横风持续帧数</summary>
        public const int GaleWindFrames = 70;
        /// <summary>每朵红花每波花刃数</summary>
        public const int GalePetalsPerFlower = 2;

        //==================== 流沙陷阱（P2 起：潜到脚下 → 沙面向心流沙拉人 → 破土喷发）====================

        /// <summary>入土前摇帧数</summary>
        public const int QuickDiveFrames = 10;
        /// <summary>流沙半径（圈缘由流沙尘声明）</summary>
        public const float QuickRadius = 300f;
        /// <summary>拉沙帧数（隆包寿命同值）</summary>
        public const int QuickPullFrames = 80;
        /// <summary>圈心拉力峰值（每帧横速增量；步行能挣脱，早走轻松晚走吃紧）</summary>
        public const float QuickPullForce = 0.075f;
        /// <summary>受拉高度：离地此高度内才算陷在沙里（起跳即脱困）</summary>
        public const float QuickPullHeight = 130f;
        /// <summary>潜行深度</summary>
        public const float QuickDepth = 170f;
        /// <summary>破土初速</summary>
        public const float QuickEruptSpeed = 30f;
        /// <summary>破土喷发沙球数（200 度上扇）</summary>
        public const int QuickEruptGlobs = 8;

        //==================== 盘身刺阵（P3 压轴：绕玩家盘成巨环 → 红花沿辐条齐射圈心）====================

        /// <summary>就位帧数上限</summary>
        public const int RingApproachFrames = 40;
        /// <summary>环半径（受颈段弯角钳制下限约 316，取 360 让链恰好合环）</summary>
        public const float RingRadius = 360f;
        /// <summary>圆心离地高度（环下弧埋沙，上弧越过玩家头顶）</summary>
        public const float RingCenterLift = 126f;
        /// <summary>沿环头速</summary>
        public const float RingHeadSpeed = 24f;
        /// <summary>上环并轨帧数</summary>
        public const int RingMergeFrames = 12;
        /// <summary>辐条预告帧数</summary>
        public const int RingSpokeTelegraph = 30;
        /// <summary>辐条锁定帧</summary>
        public const int RingSpokeLock = 8;
        /// <summary>每根辐条钉刺数</summary>
        public const int RingNeedlesPerSpoke = 4;
        /// <summary>辐条散布半角（近乎一线）</summary>
        public const float RingSpokeSpread = 0.05f;
        /// <summary>齐射轮数：P3 两轮（第二轮前沿环推进换位）</summary>
        public static int RingVolleys(int phase) => phase >= 3 ? 2 : 1;
        /// <summary>两轮之间沿环推进帧数（红花换到新方位）</summary>
        public const int RingShiftFrames = 28;
        /// <summary>解环帧数</summary>
        public const int RingUnwindFrames = 40;

        //==================== 通用节奏 ====================

        /// <summary>hub 连接段最短帧数（换招的一口气；巡曳本身就是看点，不必抢拍）</summary>
        public const int ConnectorFrames = 6;

        /// <summary>出招冷却：阶段越深越快（每招自带预告帧兜底可读性，冷却只管衔接）</summary>
        public static int AttackCooldown(int phase) => phase switch {
            >= 3 => 18,
            2 => 30,
            _ => 44,
        };

        /// <summary>NPC 弹幕伤害换算：普通/专家双基数</summary>
        public static int ScaleProjectileDamage(NPC npc, (float Normal, float Expert) baseDamage)
            => (int)npc.GetAttackDamage_ForProjectiles(baseDamage.Normal, baseDamage.Expert);
    }
}
