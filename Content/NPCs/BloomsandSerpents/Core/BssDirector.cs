using System.Collections.Generic;
using System.Linq;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core
{
    /// <summary>
    /// 荒花沙蟒战斗调参中心。身份：沙漠里的游龙，贴地快爬与破空腾跃两套身体语言交替，
    /// 强度对标残酷克眼（冲刺 46px/f、三连冲、连接段 4 帧 + 冷却 10/6/4 的出招密度），
    /// 八腿步态与鳌足姿势在爬行段和站桩招里展示，不靠拖慢节奏换取可读性。
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
        /// 整体倍率（头/体/尾 NPC.scale，节距与腿爪骨长同乘）。锁 1：贴图按原生像素画才自然，
        /// 曾放大到 1.35（头宽 159、链长约 2300px 超一屏）被用户裁定回退（2026-09-06）。
        /// 原生尺度：头 118×134、体节 106 宽、节距 82、颈距 64，
        /// 全链 64 + 19×82 + 82 ≈ 1704px（约 106 格，0.9 屏宽）。运动常量按这个体长标定，见 <see cref="ChainLength"/>
        /// </summary>
        public const float BodyScale = 1f;
        /// <summary>全链长（头心到尾心，世界像素）：颈距 + 19 节距 + 尾距，运动几何的标尺</summary>
        public const float ChainLength = (NeckGap + (BodyCount - 1) * SegmentGap + SegmentGap) * BodyScale;
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
        /// （用过一次必须走一轮轮换才能再用），防止机动战里追击无限复读、
        /// 轮换表永远轮不到（真机反馈 2026-08-31）。爬速 17/26 档下不需要更近的阀
        /// </summary>
        public const float ChaseValveDistance = 1900f;
        /// <summary>远距回归阀触发距离（钻地瞬移贴回）</summary>
        public const float FarSnapDistance = 2600f;

        //==================== 爬行（hub 直线压迫：贴地快爬是常态，不是展示步态的慢镜头）====================

        /// <summary>巡曳速度（hub 贴地压向玩家的常速）</summary>
        public const float CrawlCruiseSpeed = 17f;
        /// <summary>追赶速度（拉远追赶）</summary>
        public const float CrawlChaseSpeed = 26f;
        /// <summary>站定转身速：扑击/扬沙/合击等招在出手距离带内面向玩家时的慢爬</summary>
        public const float CrawlTurnSpeed = 4.5f;
        /// <summary>头心贴地高度（随 <see cref="BodyScale"/> 同比：体节下缘离地约 12px，腿有迈步余地，头下半截照旧埋沙）</summary>
        public const float CrawlRideHeight = 34f * BodyScale;
        /// <summary>地形前探距离</summary>
        public const float CrawlLookahead = 130f;
        /// <summary>就位减速带：距就位点此距离内从追赶速降到转身速（花刃绕上风侧等就位段共用）</summary>
        public const float ApproachSlowBand = 200f;

        //==================== 头部转向（大身体的运动法则：头是火车头，不是甩鞭的手腕）====================

        /// <summary>
        /// 头部航迹最小转弯半径（像素）。颈段弯角上限 0.35 弧度配节距 82，几何下限
        /// 82 / (2·sin 0.175) ≈ 236：头转得比这紧，颈段被钳制拉直、身体只能跟着甩——
        /// "灵活脑袋甩庞大身体"的机械根源。所有寻的转向按半径而非角速度计，这里是全局地板
        /// </summary>
        public const float MinTurnRadius = 240f * BodyScale;
        /// <summary>寻的转向的角速度地板（弧度/帧）：近停时仍能慢慢重新对准，不至于卡死</summary>
        public const float MinTurnRate = 0.02f;
        /// <summary>
        /// 头部朝向每帧最大变化（弧度）：任何模式下头都不许一帧翻身。0.22 ≈ 12.6°/帧，
        /// 180° 掉头至少 14 帧，颈段（刚度 0.34）每帧最多跟转 4.3°——甩颈从机制上消失
        /// </summary>
        public const float HeadTurnRateMax = 0.22f;
        /// <summary>地下（看不见的）航段允许的转弯半径：链在沙里，几何折角无人看见</summary>
        public const float BuriedTurnRadius = 130f * BodyScale;
        /// <summary>出土交还段的转弯半径（头贴地表抬头出面，半可见，介于地下与地板之间）</summary>
        public const float EmergeTurnRadius = 200f * BodyScale;

        //==================== 沙面掠冲（对标克眼假动作冲刺：蓄力后撤 + 一帧爆发 + 硬刹）====================

        /// <summary>就位段帧数（拉开冲刺跑道）</summary>
        public const int DashStalkFrames = 6;
        /// <summary>蓄力后撤帧数（预告主体：反向运动 + 贴地尘线；运动冲刺不画预判线，用户裁定 2026-09-06）</summary>
        public const int DashWindupFrames = 16;
        /// <summary>锁向提前量：出手前几帧死向（预告即承诺）</summary>
        public const int DashLockLead = 6;
        /// <summary>掠冲初速（克眼 44 档）</summary>
        public const float DashSpeed = 46f;
        /// <summary>飞行帧数</summary>
        public const int DashFlightFrames = 18;
        /// <summary>硬刹帧数（×0.66/帧）</summary>
        public const int DashBrakeFrames = 8;
        /// <summary>接触伤害的速度门槛</summary>
        public const float DashContactSpeed = 24f;
        /// <summary>冲刺跑道最短距离：太近先退开再冲，杀贴脸秒杀</summary>
        public const float DashRunwayMin = 440f;
        /// <summary>
        /// 掉头助跑最短路程（三个节距）：蓄力前沿冲刺线前进这么远，
        /// 颈段重排到身后，后撤蓄力才是"全身拉弓"而非把脖子甩上冲刺线。
        /// 退开段要在跑道之外多留这份余量。掠冲与回马甩尾共用
        /// </summary>
        public const float DashAlignRunPx = SegmentGap * 3f * BodyScale;
        /// <summary>射向相对水平的最大仰角（弧度，贴地掠过的身份）</summary>
        public const float DashMaxPitch = 0.24f;
        /// <summary>连冲次数：P1 三段，P2 起四段</summary>
        public static int DashReps(int phase) => phase >= 2 ? 4 : 3;
        /// <summary>尾迹掀沙间隔帧（P2 起沿冲刺路径掀起沙弹）</summary>
        public const int DashWakeGap = 4;
        /// <summary>定向预警线长度（像素；仅盘身刺阵的辐条在用，运动冲刺不画线）</summary>
        public const float DashOmenLength = 1500f;

        //==================== 蹲伏扑击（八腿蹲紧 + 抛物跃扑 + 落地沙爆）====================

        /// <summary>就位帧数上限（转向面对玩家、爬到起跳距离）</summary>
        public const int PounceStalkFrames = 30;
        /// <summary>起跳最短横距：太近先退开（扑击要有可读的弧线）</summary>
        public const float PounceMinRange = 260f;
        /// <summary>起跳最远横距：太远先贴近</summary>
        public const float PounceMaxRange = 760f;
        /// <summary>蹲伏蓄势帧数（八腿蹲紧 + 双螯举张就是预告）</summary>
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

        /// <summary>破土预告帧数（沙丘隆起 omen 的寿命；出土方向不画线，隆起点即预告）</summary>
        public const int BreachTelegraphFrames = 30;
        /// <summary>破土出土初速</summary>
        public const float BreachLaunchSpeed = 34f;
        /// <summary>突袭段重力</summary>
        public const float LungeGravity = 0.58f;
        /// <summary>接触伤害的速度门槛（伤害窗=可见冲势）</summary>
        public const float LungeContactSpeed = 13f;
        /// <summary>地下接近速度（鱼雷档）</summary>
        public const float LungeDigSpeed = 30f;
        /// <summary>突袭循环数：单招收短（P1 两次，P2 起三次），把时长还给轮换密度</summary>
        public static int LungeCycles(int phase) => phase >= 2 ? 3 : 2;
        /// <summary>破土喷发沙弹数（200 度上扇，贴地两侧留逃生道，声明见状态）</summary>
        public const int BreachEruptGlobs = 8;
        /// <summary>破土喷发扇面总角（度）</summary>
        public const float BreachEruptArcDeg = 200f;

        //==================== 喷沙（行进间齐射，不站桩）====================

        /// <summary>锁定前的跟踪帧数</summary>
        public const int SpitTrackFrames = 10;
        /// <summary>锁定后的吸气帧数</summary>
        public const int SpitInhaleFrames = 8;
        /// <summary>齐射间隔</summary>
        public const int SpitVolleyGap = 4;
        /// <summary>齐射次数（每轮 2 发，沿扇面轮转车道）</summary>
        public const int SpitVolleys = 8;
        /// <summary>最小射距：贴脸不吐沙，邀请骑脸压血</summary>
        public const float SpitMinDistance = 230f;
        /// <summary>沙团初速</summary>
        public const float SandGlobSpeed = 16f;
        /// <summary>沙团重力（弹道解算与弹幕本体共用）</summary>
        public const float SandGlobGravity = 0.30f;
        /// <summary>高抛沙团（ai[0]=1 变体）的重力倍率：低重力长滞空，弧顶更高</summary>
        public const float RainGlobGravityMul = 0.7f;
        /// <summary>齐射期爬速（边喷边挪，不站桩也不追着人跑）</summary>
        public const float SpitCrawlSpeed = 5f;

        //==================== 天游（空中游荡，收短版：铺垫不超三秒）====================

        /// <summary>游荡时长（帧）</summary>
        public const int WeaveDuration = 168;
        /// <summary>游荡巡速</summary>
        public const float WeaveSpeed = 21f;
        /// <summary>游荡航迹转弯半径（利萨如锚点在动，头以宽弧追它，链在天上铺成 S 形游龙）</summary>
        public const float WeaveTurnRadius = 320f * BodyScale;
        /// <summary>游荡中喷沙节拍（预亮 10 帧后出手）</summary>
        public const int WeaveSpitGap = 30;
        /// <summary>游荡中洒瓣节拍（P2 起）</summary>
        public const int WeavePetalGap = 42;
        /// <summary>俯冲预告帧数（头亮 + 吼 + 锁点）</summary>
        public const int WeaveDiveTelegraph = 16;
        /// <summary>俯冲速度</summary>
        public const float WeaveDiveSpeed = 31f;

        //==================== 盘天环猎（绕玩家转圈收紧）====================

        /// <summary>环猎时长（帧，P3 加长；收短版：环住三秒即收束）</summary>
        public static int OrbitDuration(int phase) => phase >= 3 ? 210 : 180;
        /// <summary>
        /// 起始环径。链长 1704 对 2π·400 = 2513 的收紧环只盖七成，头尾之间三成弧是随环转的逃生门；
        /// 旧值 450→310 按 840px 体长定，现体长翻倍照比例放大
        /// </summary>
        public const float OrbitRadiusStart = 560f * BodyScale;
        /// <summary>收紧后的环径</summary>
        public const float OrbitRadiusEnd = 400f * BodyScale;
        /// <summary>环转角速度（弧度/帧）：0.04×480 ≈ 19 切速，追点系数后头速约 31，比掠冲慢一档是刻意的</summary>
        public static float OrbitAngularSpeed(int phase) => phase >= 3 ? 0.046f : 0.04f;
        /// <summary>向心钉刺节拍（P2 起；预亮 10 帧，射向环心非追踪）</summary>
        public const int OrbitNeedleGap = 28;
        /// <summary>穿心突刺预告帧数</summary>
        public const int OrbitExitTelegraph = 16;
        /// <summary>穿心突刺速度</summary>
        public const float OrbitExitSpeed = 34f;
        /// <summary>J 弯切出转弯半径（从环切向掰进穿心线的最紧弧，压在转弯地板附近）</summary>
        public const float OrbitHookRadius = 260f * BodyScale;

        //==================== hub 骚扰刺（攻击欲望的底噪：巡曳中也在咬）====================

        /// <summary>骚扰甩刺周期（帧，按阶段提速）</summary>
        public static int HarassGap(int phase) => phase switch {
            >= 3 => 20,
            2 => 26,
            _ => 36,
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

        //==================== 沙爆漩涡冲刺（P2 起：盘旋搓涡 + 弃涡爆冲 + 漩涡后爆）====================

        /// <summary>就位段帧数上限（脱离玩家去侧上锚点，提前到位即早退入盘）</summary>
        public const int VortexEntryFrames = 24;
        /// <summary>
        /// 盘旋搓涡帧数（漩涡蓄力同长，状态与弹幕共读此常数）。
        /// 角速度按大身体压低后，96 帧扫约 1.2 圈；蓄力越长输出窗越长，是公平阀不是拖沓
        /// </summary>
        public const int VortexSpinFrames = 96;
        /// <summary>锁向塌缩帧数（漩涡缩小 + 粒子静默 = 爆前吸气，末段锁死射向）</summary>
        public const int VortexCollapseFrames = 14;
        /// <summary>
        /// 盘旋半径：起始→收紧。收紧半径 280 的周长 1759 ≈ 链长 1704 = 链条恰好缠满整圈
        /// （旧 300→150 按 840px 体长定，150 还低于颈段 236 的转弯下限，头在硬掰）
        /// </summary>
        public const float VortexRadiusStart = 420f * BodyScale;
        /// <summary>盘旋收紧后的半径</summary>
        public const float VortexRadiusEnd = 280f * BodyScale;
        /// <summary>盘旋角速度起点（弧度/帧）：0.055×420 ≈ 23 切速，追点后头速约 37</summary>
        public const float VortexOmegaStart = 0.055f;
        /// <summary>盘旋角速度终点（越搓越快：0.10×280 ≈ 28 切速，头速约 44，仍低于 50 的爆冲 = 速度分层）</summary>
        public const float VortexOmegaEnd = 0.10f;
        /// <summary>塌缩弧转弯半径（切向掰进穿刺线，压在地板附近）</summary>
        public const float VortexCollapseRadius = 250f * BodyScale;
        /// <summary>锚点相对玩家的侧向距离（漩涡必须在屏内被看见才算预告）</summary>
        public const float VortexAnchorSide = 500f;
        /// <summary>锚点抬升（取玩家与地面较高者再上抬此值）</summary>
        public const float VortexAnchorLift = 250f;
        /// <summary>爆冲速度（招牌招，高于掠冲 46 = 速度分层）</summary>
        public const float VortexDashSpeed = 50f;
        /// <summary>爆冲飞行帧数</summary>
        public const int VortexFlightFrames = 20;
        /// <summary>出手后漩涡引爆延迟（蛇先冲走、涡在身后爆：先躲冲刺再看沙雨）</summary>
        public const int VortexDetonateDelay = 10;
        /// <summary>后爆沙球环枚数（径向均匀、重力弧线，从玩家盯了一秒半的固定点爆出）</summary>
        public const int VortexGlobRing = 16;
        /// <summary>P3 第二波慢环枚数（角度错半步）</summary>
        public const int VortexGlobRingSecond = 10;
        /// <summary>P3 第二波相对首爆的延迟帧</summary>
        public const int VortexSecondWaveDelay = 10;
        /// <summary>沙球环速度下限（快慢分层 = 内外两圈落点）</summary>
        public const float VortexGlobSpeedMin = 6f;
        /// <summary>沙球环速度上限</summary>
        public const float VortexGlobSpeedMax = 13f;

        //==================== 回环沙瀑（P2 起：天上画正圆泻沙成帘，收环离心俯冲）====================

        /// <summary>入环就位帧数上限（提前到位即早退入环）</summary>
        public const int LoopEntryFrames = 40;
        /// <summary>环心相对玩家的侧偏（进入画环帧锁定，不追玩家）</summary>
        public const float LoopCenterSide = 460f;
        /// <summary>环心抬升（环底 = 抬升 − 半径 ≈ 210px 离地，环整个悬在头顶）</summary>
        public const float LoopCenterLift = 540f;
        /// <summary>环半径（旧 250 低于颈段转弯下限 236 的余量线；330 让一圈正好铺下约 2/3 链长）</summary>
        public const float LoopRadius = 330f * BodyScale;
        /// <summary>画满一圈的帧数（角速度 = 2π/此值；96 帧配 330 半径头速约 35，62 帧会飙到 51）</summary>
        public const int LoopLapFrames = 96;
        /// <summary>泻沙节拍（帧/枚，节拍疏密即幕帘逃生缝声明；一圈 16 枚）</summary>
        public const int LoopCascadeGap = 6;
        /// <summary>收环后沿环找切点的帧数上限（切向对准玩家即早退出手）</summary>
        public const int LoopAlignFrames = 40;
        /// <summary>俯冲预告帧数（亮头 + 吼 + 转速减半，锁点即承诺）</summary>
        public const int LoopDiveTelegraph = 14;
        /// <summary>俯冲速度</summary>
        public const float LoopDiveSpeed = 34f;
        /// <summary>入环前腾空段转弯半径</summary>
        public const float LoopAscendRadius = 300f * BodyScale;

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

        //==================== 回马甩尾（P3：擦身而过 + 过顶急转离心甩针 + 回马枪连段）====================

        /// <summary>就位帧数（拉开擦身跑道）</summary>
        public const int SweepStalkFrames = 8;
        /// <summary>蓄力帧数（短版后撤，主菜在急转不在首冲）</summary>
        public const int SweepWindupFrames = 12;
        /// <summary>擦身冲刺速度</summary>
        public const float SweepPassSpeed = 40f;
        /// <summary>擦身飞行帧数上限（越过玩家即早退入弯）</summary>
        public const int SweepPassFrames = 22;
        /// <summary>越身判定距离（沿冲刺向越过玩家此距离即入弯）</summary>
        public const float SweepOvershoot = 300f;
        /// <summary>急转段帧数（U 弯半径 280 配 26 速：半圈约 34 帧，留两帧收势余量）</summary>
        public const int SweepTurnFrames = 36;
        /// <summary>U 弯转弯半径（过顶回瞄的弧，颈段能跟得上的最紧档）</summary>
        public const float SweepTurnRadius = 280f * BodyScale;
        /// <summary>入弯拉升点：沿冲刺向前伸 + 向上抬（弧顶高度约等于转弯半径，弯才是"过顶"而不是原地抽头）</summary>
        public const float SweepTurnLiftX = 200f * BodyScale;
        public const float SweepTurnLiftY = 300f * BodyScale;
        /// <summary>急转甩针节拍（帧/轮）</summary>
        public const int SweepFlingGap = 4;
        /// <summary>甩针窗口（入弯后前多少帧内甩，后段留给转向收势）</summary>
        public const int SweepFlingWindow = 26;
        /// <summary>甩针速度（方向 = 体节自身运动向 = 物理离心，非瞄准）</summary>
        public const float SweepNeedleSpeed = 10f;

        //==================== 沙丘柱（场地实体，Actor 承载）====================

        /// <summary>同屏柱数上限（怒放波 16 + 入场双柱 + 腾跃应急柱 + 余量）</summary>
        public const int PillarMax = 20;
        /// <summary>柱宽（6 物块 = 96px；体节 106 宽，柱不能比爬它的身体窄太多）</summary>
        public const float PillarWidth = 96f;
        /// <summary>柱高下限/上限（随机档；参差天际线是怒放波的沸腾读数）</summary>
        public const float PillarHeightMin = 700f;
        public const float PillarHeightMax = 940f;
        /// <summary>钻出帧数（唯一伤害窗：极锐缓出一口气升满）</summary>
        public const int PillarEruptFrames = 9;
        /// <summary>缓沉帧数（缓慢落回地面消失）</summary>
        public const int PillarSinkFrames = 80;
        /// <summary>置景柱滞留（入场双柱：站到 P2 爆震首秀当燃料）</summary>
        public const int PillarIntroLinger = 60 * 45;
        /// <summary>突刺柱滞留（腾跃/爆震的燃料窗口）</summary>
        public const int PillarSpikeLinger = 60 * 16;
        /// <summary>柱体钻出接触伤害（normal/expert，走 GetAttackDamage_ForProjectiles 换算）</summary>
        public static (float Normal, float Expert) PillarContactDamage => (26f, 22f);

        //==================== 沙柱突刺（跺地锁心，全场怒放式钻出）====================

        /// <summary>立起跺地蓄势帧数（立起剪影 + 跺地即预告主体，跺地帧锁定花心）</summary>
        public const int SpikeStompFrames = 20;
        /// <summary>逐根点名间隔帧（快节奏滚开：鼓包波扫过全场、柱群按同序轰起）</summary>
        public const int SpikeStepGap = 8;
        /// <summary>单根鼓包预告帧数</summary>
        public const int SpikeOmenFrames = 22;
        /// <summary>怒放根数：P1 十二根，P2 十四根，P3 十六根（全场沸腾档）</summary>
        public static int SpikeCount(int phase) => phase >= 3 ? 16 : phase == 2 ? 14 : 12;
        /// <summary>怒放车道间距（0/+1/-1/+2/-2 扩散序的槽距；槽距−抖散 ≥ 走廊宽）</summary>
        public const float SpikeLaneSpacing = 220f;
        /// <summary>落点相对车道槽位的横向抖散（去机械感，幅度不许吃掉走廊）</summary>
        public const float SpikeScatterPx = 24f;
        /// <summary>与最近既有柱的最小间距（柱间走廊 = 声明的逃生道）</summary>
        public const float SpikeMinGapPx = 180f;

        //==================== 沙柱腾跃（盘柱而上 + 蹬柱爆冲）====================

        /// <summary>接近柱脚的就位帧数上限（贴到即早退）</summary>
        public const int VaultApproachFrames = 60;
        /// <summary>盘柱而上的总时长（帧；升到柱顶）</summary>
        public const int VaultClimbFrames = 84;
        /// <summary>
        /// 盘柱横摆角速度（弧度/帧）。106 宽的身体绕不了 96 宽的柱，"螺旋"是头沿柱面左右
        /// 扫着往上爬、八腿抓壁的近似；摆幅小、摆得慢，头才不会在柱顶甩脖子
        /// </summary>
        public const float VaultClimbOmega = 0.09f;
        /// <summary>横摆半径（相对柱半宽的倍率：贴着柱身扫）</summary>
        public const float VaultOrbitScale = 1.4f;
        /// <summary>柱顶盘紧静止拍（爆发前的收势：静止即预告）</summary>
        public const int VaultCoilFrames = 26;
        /// <summary>蹬柱上抛滞空帧数（跳到空中再冲：滞空前段可重瞄，末段死向）</summary>
        public const int VaultHopFrames = 14;
        /// <summary>蹬柱上抛初速（竖直向）</summary>
        public const float VaultHopKick = 17f;
        /// <summary>锁向提前量（出手前死向，预告即承诺）</summary>
        public const int VaultLockLead = 8;
        /// <summary>蹬柱爆冲速度（速度分层：掠冲 46 < 本招 48 < 漩涡 50）</summary>
        public const float VaultDashSpeed = 48f;
        /// <summary>爆冲飞行帧数</summary>
        public const int VaultFlightFrames = 19;
        /// <summary>爆冲硬刹帧数</summary>
        public const int VaultBrakeFrames = 9;
        /// <summary>接触伤害的速度门槛</summary>
        public const float VaultContactSpeed = 24f;

        //==================== 沙柱爆震（怒吼声波环 + 逐柱引爆）====================

        /// <summary>选招门槛：场上可点名柱数不足此值时该槽位落到替补招</summary>
        public const int BurstMinPillars = 2;
        /// <summary>后仰怒吼帧数（声波环 + 立起剪影即预告）</summary>
        public const int BurstRoarFrames = 42;
        /// <summary>裂纹预闪帧数（怒吼后全柱同亮，错拍延迟另加）</summary>
        public const int BurstCrackFrames = 30;
        /// <summary>逐柱错拍间隔帧（近柱先爆，波次可读）</summary>
        public const int BurstStaggerGap = 9;
        /// <summary>每柱径向沙球枚数（球环缺口 + 柱间走廊 = 逃生道）</summary>
        public const int BurstGlobRing = 14;
        /// <summary>沙球环速度下限（快慢双速分层 = 内外两圈落点）</summary>
        public const float BurstGlobSpeedMin = 6.5f;
        /// <summary>沙球环速度上限</summary>
        public const float BurstGlobSpeedMax = 12.5f;
        /// <summary>无柱可爆时先种的应急柱数（保底演出：两翼各两根再吼）</summary>
        public const int BurstFallbackPillars = 4;

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
        /// <summary>沙下翻身小圈半径与深度（头在翻身点下方绕一小圈，链在沙里绞成一团；地下不受转弯地板约束）</summary>
        public const float SurgeRollRadius = 150f * BodyScale;
        public const float SurgeRollDepth = 230f * BodyScale;

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
        /// <summary>钳点距出手时头心的最远距离（前扑路程 132 + 嘴位 66 + 螯伸展，不超出螯的视觉触及；原生倍率下的账）</summary>
        public const float SnapReach = 290f;
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
        /// <summary>
        /// 环半径。链长 1704 合环半径 = 1704/2π ≈ 271，取 290 让头尾留约 120px 缺口（头压过尾不叠画）；
        /// 高于颈段转弯下限 236 有余量。旧 360 是按 1.35 倍体长（2300px）算的
        /// </summary>
        public const float RingRadius = 290f * BodyScale;
        /// <summary>圆心离地高度（环下弧埋沙约三成，上弧越过玩家头顶约 400px）</summary>
        public const float RingCenterLift = 110f * BodyScale;
        /// <summary>沿环头速（24/290 ≈ 0.083 弧度/帧，合围一圈约 76 帧）</summary>
        public const float RingHeadSpeed = 24f;
        /// <summary>解环爬走的转弯半径</summary>
        public const float RingUnwindRadius = 260f * BodyScale;
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

        //==================== 通用节奏（推倒版：近乎无缝的出招密度）====================

        /// <summary>hub 连接段最短帧数（换招的一口气）</summary>
        public const int ConnectorFrames = 4;

        /// <summary>出招冷却：阶段越深越快（每招自带预告帧兜底可读性，冷却只管衔接）</summary>
        public static int AttackCooldown(int phase) => phase switch {
            >= 3 => 4,
            2 => 6,
            _ => 10,
        };

        /// <summary>NPC 弹幕伤害换算：普通/专家双基数</summary>
        public static int ScaleProjectileDamage(NPC npc, (float Normal, float Expert) baseDamage)
            => (int)npc.GetAttackDamage_ForProjectiles(baseDamage.Normal, baseDamage.Expert);
    }
}
