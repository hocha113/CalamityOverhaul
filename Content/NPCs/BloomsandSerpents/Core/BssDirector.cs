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
        /// <summary>超过此距离（或玩家高飞）不再爬：钻地鱼雷直接压上去</summary>
        public const float PursuitDistance = 820f;

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
        /// <summary>射向相对水平的最大仰角（弧度，贴地掠过的身份）</summary>
        public const float DashMaxPitch = 0.24f;
        /// <summary>连冲次数：P1 三段，P2 起四段</summary>
        public static int DashReps(int phase) => phase >= 2 ? 4 : 3;
        /// <summary>尾迹掀沙间隔帧（P2 起沿冲刺路径掀起沙弹）</summary>
        public const int DashWakeGap = 4;

        //==================== 破土突袭 ====================

        /// <summary>破土预告帧数（沙丘隆起 omen 的寿命）</summary>
        public const int BreachTelegraphFrames = 32;
        /// <summary>破土出土初速</summary>
        public const float BreachLaunchSpeed = 34f;
        /// <summary>突袭段重力</summary>
        public const float LungeGravity = 0.58f;
        /// <summary>接触伤害的速度门槛（伤害窗=可见冲势）</summary>
        public const float LungeContactSpeed = 13f;
        /// <summary>地下接近速度（鱼雷档）</summary>
        public const float LungeDigSpeed = 30f;
        /// <summary>突袭循环数：P1 三次，P2 起四次</summary>
        public static int LungeCycles(int phase) => phase >= 2 ? 4 : 3;
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

        //==================== 天游（空中长时间游荡）====================

        /// <summary>游荡时长（帧）</summary>
        public const int WeaveDuration = 300;
        /// <summary>游荡巡速</summary>
        public const float WeaveSpeed = 21f;
        /// <summary>游荡中喷沙节拍（预亮 10 帧后出手）</summary>
        public const int WeaveSpitGap = 42;
        /// <summary>游荡中洒瓣节拍（P2 起）</summary>
        public const int WeavePetalGap = 56;
        /// <summary>俯冲预告帧数（头亮 + 吼 + 锁点）</summary>
        public const int WeaveDiveTelegraph = 20;
        /// <summary>俯冲速度</summary>
        public const float WeaveDiveSpeed = 31f;

        //==================== 盘天环猎（绕玩家转圈收紧）====================

        /// <summary>环猎时长（帧，P3 加长）</summary>
        public static int OrbitDuration(int phase) => phase >= 3 ? 310 : 260;
        /// <summary>起始环径</summary>
        public const float OrbitRadiusStart = 450f;
        /// <summary>收紧后的环径</summary>
        public const float OrbitRadiusEnd = 310f;
        /// <summary>环转角速度（弧度/帧）</summary>
        public static float OrbitAngularSpeed(int phase) => phase >= 3 ? 0.058f : 0.05f;
        /// <summary>向心钉刺节拍（P2 起；预亮 10 帧，射向环心非追踪）</summary>
        public const int OrbitNeedleGap = 36;
        /// <summary>穿心突刺预告帧数</summary>
        public const int OrbitExitTelegraph = 18;
        /// <summary>穿心突刺速度</summary>
        public const float OrbitExitSpeed = 34f;

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
        /// <summary>花瓣出生点相对红花节的横向抖动上限（走廊声明：花道间距≈FlowerStep×节距−2×此值）</summary>
        public const float PetalLaneHalfWidth = 26f;

        //==================== 通用节奏（推倒版：近乎无缝的出招密度）====================

        /// <summary>hub 连接段最短帧数（换招的一口气）</summary>
        public const int ConnectorFrames = 6;

        /// <summary>出招冷却：阶段越深越快</summary>
        public static int AttackCooldown(int phase) => phase switch {
            >= 3 => 6,
            2 => 10,
            _ => 16,
        };

        /// <summary>NPC 弹幕伤害换算：普通/专家双基数</summary>
        public static int ScaleProjectileDamage(NPC npc, (float Normal, float Expert) baseDamage)
            => (int)npc.GetAttackDamage_ForProjectiles(baseDamage.Normal, baseDamage.Expert);
    }
}
