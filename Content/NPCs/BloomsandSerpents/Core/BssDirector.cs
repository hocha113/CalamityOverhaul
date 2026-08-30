using Terraria;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core
{
    /// <summary>
    /// 荒花沙蟒战斗调参中心。强度对标残酷克眼（冲刺 44px/f、27 帧飞行、三连冲、
    /// 悬停 40~56 帧的出招密度），克眼后档位，普通模式基数，专家/大师走原版缩放。
    /// </summary>
    internal static class BssDirector
    {
        //==================== 编制 ====================

        /// <summary>体节数（不含头尾）</summary>
        public const int BodyCount = 20;
        /// <summary>红花节间隔：ordinal % FlowerStep == FlowerStep-1 的体节开花（发射器）</summary>
        public const int FlowerStep = 3;
        /// <summary>节距（体节帧高 56，插槽重叠后的链距）</summary>
        public const float SegmentGap = 40f;

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
        /// <summary>沙暴转阶段血线</summary>
        public const float StormThreshold = 0.6f;
        /// <summary>繁花怒放血线</summary>
        public const float ApexThreshold = 0.25f;

        //==================== 弹幕基伤（normal/expert，走 GetAttackDamage_ForProjectiles）====================

        public static (float Normal, float Expert) NeedleDamage => (16f, 14f);
        public static (float Normal, float Expert) SandGlobDamage => (15f, 13f);
        public static (float Normal, float Expert) CactusBallDamage => (24f, 20f);
        public static (float Normal, float Expert) PetalDamage => (17f, 15f);

        //==================== 感知与脱战 ====================

        /// <summary>目标失效判定距离</summary>
        public const float MaxFindDistance = 5600f;
        /// <summary>出招最大交战距离</summary>
        public const float EngageDistance = 1400f;
        /// <summary>
        /// 追击阀触发距离：拉远到此距离才插入钻地追击连接件。配合单发闸
        /// （用过一次必须走一轮轮换才能再用），防止机动战里追击无限复读、
        /// 轮换表（含沙柱三招）永远轮不到（真机反馈 2026-08-31）。
        /// </summary>
        public const float ChaseValveDistance = 1900f;
        /// <summary>远距回归阀触发距离（钻地瞬移贴回）</summary>
        public const float FarSnapDistance = 2600f;

        //==================== 爬行 ====================

        /// <summary>巡曳速度</summary>
        public const float CrawlCruiseSpeed = 17f;
        /// <summary>压迫速度（拉远追赶）</summary>
        public const float CrawlChaseSpeed = 26f;
        /// <summary>头心贴地高度</summary>
        public const float CrawlRideHeight = 34f;
        /// <summary>地形前探距离</summary>
        public const float CrawlLookahead = 130f;

        //==================== 沙面掠冲（对标克眼假动作冲刺：蓄力后撤 + 一帧爆发 + 硬刹）====================

        /// <summary>就位段帧数（拉开冲刺跑道）</summary>
        public const int DashStalkFrames = 6;
        /// <summary>蓄力后撤帧数（预告主体：反向运动 + 尘线车道）</summary>
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
        /// 掉头助跑最短路程（约 3.5 节距）：蓄力前沿冲刺线前进这么远，
        /// 链条重排到身后，后撤蓄力才是"全身拉弓"而非把脖子甩上冲刺线。
        /// 退开段要在跑道之外多留这份余量。掠冲与回马甩尾共用。
        /// </summary>
        public const float DashAlignRunPx = 150f;
        /// <summary>射向相对水平的最大仰角（弧度，贴地掠过的身份）</summary>
        public const float DashMaxPitch = 0.24f;
        /// <summary>连冲次数：P1 三段，P2 起四段</summary>
        public static int DashReps(int phase) => phase >= 2 ? 4 : 3;
        /// <summary>尾迹掀沙间隔帧（P2 起沿冲刺路径掀起沙弹）</summary>
        public const int DashWakeGap = 4;

        //==================== 破土突袭 ====================

        /// <summary>破土预告帧数（沙丘隆起 omen 的寿命）</summary>
        public const int BreachTelegraphFrames = 26;
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

        //==================== 天游（空中游荡，收短版：铺垫不超三秒）====================

        /// <summary>游荡时长（帧）</summary>
        public const int WeaveDuration = 168;
        /// <summary>游荡巡速</summary>
        public const float WeaveSpeed = 21f;
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
        /// <summary>起始环径</summary>
        public const float OrbitRadiusStart = 450f;
        /// <summary>收紧后的环径</summary>
        public const float OrbitRadiusEnd = 310f;
        /// <summary>环转角速度（弧度/帧）</summary>
        public static float OrbitAngularSpeed(int phase) => phase >= 3 ? 0.068f : 0.06f;
        /// <summary>向心钉刺节拍（P2 起；预亮 10 帧，射向环心非追踪）</summary>
        public const int OrbitNeedleGap = 28;
        /// <summary>穿心突刺预告帧数</summary>
        public const int OrbitExitTelegraph = 16;
        /// <summary>穿心突刺速度</summary>
        public const float OrbitExitSpeed = 34f;

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
        /// <summary>花瓣出生点相对红花节的横向抖动上限（走廊声明：花道间距≈FlowerStep×节距−2×此值）</summary>
        public const float PetalLaneHalfWidth = 26f;

        //==================== 沙爆漩涡冲刺（P2 起：盘旋搓涡 + 弃涡爆冲 + 漩涡后爆）====================

        /// <summary>就位段帧数上限（脱离玩家去侧上锚点，提前到位即早退入盘）</summary>
        public const int VortexEntryFrames = 20;
        /// <summary>盘旋搓涡帧数（漩涡蓄力同长，状态与弹幕共读此常数）</summary>
        public const int VortexSpinFrames = 66;
        /// <summary>锁向塌缩帧数（漩涡缩小 + 粒子静默 = 爆前吸气，末段锁死射向）</summary>
        public const int VortexCollapseFrames = 12;
        /// <summary>盘旋半径：起始→收紧（体长约 840px，150 半径周长约 942 = 链条几乎缠满整圈）</summary>
        public const float VortexRadiusStart = 300f;
        /// <summary>盘旋收紧后的半径</summary>
        public const float VortexRadiusEnd = 150f;
        /// <summary>盘旋角速度起点（弧度/帧）</summary>
        public const float VortexOmegaStart = 0.12f;
        /// <summary>盘旋角速度终点（越搓越快，末段近两秒转一圈半的暴烈档）</summary>
        public const float VortexOmegaEnd = 0.19f;
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
        public const int LoopEntryFrames = 36;
        /// <summary>环心相对玩家的侧偏（进入画环帧锁定，不追玩家）</summary>
        public const float LoopCenterSide = 420f;
        /// <summary>环心抬升</summary>
        public const float LoopCenterLift = 460f;
        /// <summary>环半径</summary>
        public const float LoopRadius = 250f;
        /// <summary>画满一圈的帧数（角速度 = 2π/此值）</summary>
        public const int LoopLapFrames = 62;
        /// <summary>泻沙节拍（帧/枚，节拍疏密即幕帘逃生缝声明）</summary>
        public const int LoopCascadeGap = 4;
        /// <summary>收环后沿环找切点的帧数上限（切向对准玩家即早退出手）</summary>
        public const int LoopAlignFrames = 30;
        /// <summary>俯冲预告帧数（亮头 + 吼 + 转速减半，锁点即承诺）</summary>
        public const int LoopDiveTelegraph = 12;
        /// <summary>俯冲速度</summary>
        public const float LoopDiveSpeed = 34f;

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
        public const float SweepOvershoot = 260f;
        /// <summary>急转段帧数</summary>
        public const int SweepTurnFrames = 26;
        /// <summary>急转甩针节拍（帧/轮）</summary>
        public const int SweepFlingGap = 4;
        /// <summary>甩针窗口（入弯后前多少帧内甩，后段留给转向收势）</summary>
        public const int SweepFlingWindow = 20;
        /// <summary>甩针速度（方向 = 体节自身运动向 = 物理离心，非瞄准）</summary>
        public const float SweepNeedleSpeed = 10f;

        //==================== 沙丘柱（场地实体，Actor 承载）====================

        /// <summary>同屏柱数上限（怒放波 16 + 入场双柱 + 腾跃应急柱 + 余量）</summary>
        public const int PillarMax = 20;
        /// <summary>柱宽（5 物块 = 80px）</summary>
        public const float PillarWidth = 80f;
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
        public const float SpikeLaneSpacing = 210f;
        /// <summary>落点相对车道槽位的横向抖散（去机械感，幅度不许吃掉走廊）</summary>
        public const float SpikeScatterPx = 24f;
        /// <summary>与最近既有柱的最小间距（柱间走廊 = 声明的逃生道）</summary>
        public const float SpikeMinGapPx = 170f;

        //==================== 沙柱腾跃（盘柱螺旋 + 蹬柱爆冲）====================

        /// <summary>接近柱脚的就位帧数上限（贴到即早退）</summary>
        public const int VaultApproachFrames = 60;
        /// <summary>盘柱螺旋圈时长（帧；升到柱顶的总时长）</summary>
        public const int VaultClimbFrames = 78;
        /// <summary>螺旋角速度（弧度/帧）</summary>
        public const float VaultClimbOmega = 0.13f;
        /// <summary>螺旋半径（相对柱半宽的倍率：贴着柱身绕）</summary>
        public const float VaultOrbitScale = 1.7f;
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
