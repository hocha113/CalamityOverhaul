using Terraria;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents.Core
{
    /// <summary>
    /// 脓蕾沙蟒战斗调参中心。荒花沙蟒的上位变异体：肉后初期、机械三王同级，
    /// 普通模式基数，专家/大师走原版缩放。比原版更长更庞大（26+7 节、scale 1.15），
    /// 出招密度对标残酷世吞，弹幕威胁围绕灵液池场地经济组织。
    /// </summary>
    internal static class FssDirector
    {
        //==================== 编制 ====================

        /// <summary>初始体节数（不含头尾；蜕变生长后 +GrowthSegments）</summary>
        public const int BodyCount = 26;
        /// <summary>P2 蜕变当场长出的体节数</summary>
        public const int GrowthSegments = 7;
        /// <summary>囊肿节间隔：ordinal % CystStep == CystStep-1 的体节带脓疮（发射器）</summary>
        public const int CystStep = 3;
        /// <summary>节距（复用 BSS 体节素材，放大后的链距）</summary>
        public const float SegmentGap = 46f;
        /// <summary>整体放大（更庞大的读数，贴图暂借 BSS）</summary>
        public const float BodyScale = 1.15f;
        /// <summary>链序数组上限（26+7+尾 = 34，留余量）</summary>
        public const int MaxOrdinals = 40;

        //==================== 基础数值 ====================

        /// <summary>头基础生命（统一血池汇总后约 37900，机三档）</summary>
        public const int HeadLife = 16000;
        /// <summary>单体节生命（并入血池；蜕变新增节不再并池）</summary>
        public const int BodyLife = 780;
        /// <summary>尾节生命（并入血池）</summary>
        public const int TailLife = 1600;

        /// <summary>接触伤害（普通基数）：头/体/尾</summary>
        public const int HeadContact = 60;
        public const int BodyContact = 34;
        public const int TailContact = 26;

        /// <summary>防御：头软体硬，鼓励打头</summary>
        public const int HeadDefense = 10;
        public const int BodyDefense = 26;
        public const int TailDefense = 18;

        /// <summary>触发死亡演出的生命阈值</summary>
        public const int DeathTriggerLife = 60;
        /// <summary>蜕变生长转阶段血线（P2）</summary>
        public const float MoltThreshold = 0.62f;
        /// <summary>满溢怒放血线（P3）</summary>
        public const float OverflowThreshold = 0.28f;

        //==================== 弹幕基伤（normal/expert，走 GetAttackDamage_ForProjectiles）====================

        public static (float Normal, float Expert) IchorGlobDamage => (36f, 30f);
        public static (float Normal, float Expert) IchorPoolDamage => (30f, 25f);
        public static (float Normal, float Expert) GeyserDamage => (42f, 35f);
        public static (float Normal, float Expert) MortarShardDamage => (38f, 32f);
        public static (float Normal, float Expert) CascadeDamage => (34f, 28f);
        public static (float Normal, float Expert) HuskDamage => (30f, 25f);
        public static (float Normal, float Expert) RippleDamage => (33f, 28f);

        //==================== 感知与脱战 ====================

        /// <summary>目标失效判定距离</summary>
        public const float MaxFindDistance = 5600f;
        /// <summary>出招最大交战距离</summary>
        public const float EngageDistance = 1500f;
        /// <summary>远距回归阀触发距离（钻地瞬移贴回）</summary>
        public const float FarSnapDistance = 2700f;

        //==================== 爬行 ====================

        /// <summary>巡曳速度</summary>
        public const float CrawlCruiseSpeed = 18f;
        /// <summary>压迫速度（拉远追赶）</summary>
        public const float CrawlChaseSpeed = 27f;
        /// <summary>头心贴地高度（体格更大，抬得更高）</summary>
        public const float CrawlRideHeight = 40f;
        /// <summary>地形前探距离</summary>
        public const float CrawlLookahead = 150f;

        //==================== 入场（污染扩散 + 双弧破土，两拍演出）====================

        /// <summary>污染扩散段帧数（破土预兆同长）</summary>
        public const int IntroStainFrames = 106;
        /// <summary>破土帧</summary>
        public const int IntroBreachFrame = 124;
        /// <summary>破土初速（跃过玩家头顶的高抛）</summary>
        public const float IntroLaunchSpeed = 36.5f;
        /// <summary>破土横向漂移（弧线跨越玩家）</summary>
        public const float IntroArcDriftX = 7.5f;
        /// <summary>污染扩散最大半径（像素）</summary>
        public const float IntroStainRadius = 340f;

        //==================== 灵液弹与脓池（场地经济基座）====================

        /// <summary>痰弹初速</summary>
        public const float IchorGlobSpeed = 15.5f;
        /// <summary>痰弹重力（弹道解算与弹幕本体共用）</summary>
        public const float IchorGlobGravity = 0.27f;
        /// <summary>全场脓池上限（超限最旧的提前干涸）</summary>
        public const int PoolMaxCount = 22;
        /// <summary>脓池寿命（帧，约 20 秒）</summary>
        public const int PoolLifeFrames = 1200;
        /// <summary>脓池干涸段帧数（关伤害的收尾）</summary>
        public const int PoolDryFrames = 70;

        //==================== A1 灵液扫喷（行进齐射，落点播种脓池）====================

        /// <summary>锁定前的跟踪帧数</summary>
        public const int SpitTrackFrames = 10;
        /// <summary>锁定后的吸气帧数（口部收束 + 微后仰）</summary>
        public const int SpitInhaleFrames = 10;
        /// <summary>齐射间隔</summary>
        public const int SpitVolleyGap = 5;
        /// <summary>齐射次数（每轮 2 发对称车道）</summary>
        public const int SpitVolleys = 7;
        /// <summary>车道反转轮次（反转前一拍有音效 + 头闪提示）</summary>
        public const int SpitReverseVolley = 4;
        /// <summary>车道半角基数（弧度；第 k 对车道 = ±(基数 + k·步进)，中缝永空）</summary>
        public const float SpitLaneBase = 0.11f;
        /// <summary>车道步进</summary>
        public const float SpitLaneStep = 0.065f;
        /// <summary>最小射距：贴脸不吐，邀请骑脸压血</summary>
        public const float SpitMinDistance = 240f;

        //==================== A2 掠地毒冲（假动作爆冲 + 尾迹滴灵液）====================

        /// <summary>就位段帧数（拉开冲刺跑道）</summary>
        public const int SkimStalkFrames = 6;
        /// <summary>蓄力后撤帧数（预告主体：反向运动 + 尘线车道）</summary>
        public const int SkimWindupFrames = 16;
        /// <summary>锁向提前量：出手前几帧死向（预告即承诺）</summary>
        public const int SkimLockLead = 6;
        /// <summary>掠冲初速</summary>
        public const float SkimSpeed = 47f;
        /// <summary>飞行帧数</summary>
        public const int SkimFlightFrames = 19;
        /// <summary>硬刹帧数（×0.66/帧）</summary>
        public const int SkimBrakeFrames = 8;
        /// <summary>接触伤害的速度门槛</summary>
        public const float SkimContactSpeed = 24f;
        /// <summary>冲刺跑道最短距离：太近先退开再冲，杀贴脸秒杀</summary>
        public const float SkimRunwayMin = 460f;
        /// <summary>
        /// 掉头助跑最短路程（约 3.5 节距，含放大系数）：蓄力前沿冲刺线前进这么远，
        /// 链条重排到身后，后撤蓄力才是"全身拉弓"而非把脖子甩上冲刺线。
        /// 退开段要在跑道之外多留这份余量。毒冲与疮爆掠航共用。
        /// </summary>
        public const float SkimAlignRunPx = 180f;
        /// <summary>射向相对水平的最大仰角（弧度，贴地掠过的身份）</summary>
        public const float SkimMaxPitch = 0.24f;
        /// <summary>连冲次数：P1 三段，P2 起四段</summary>
        public static int SkimReps(int phase) => phase >= 2 ? 4 : 3;
        /// <summary>尾迹滴落间隔帧（雨滴与留池滴交替）</summary>
        public const int SkimDripGap = 4;

        //==================== A3 黏疮布点（黏附砖面鼓胀 → 竖直灵液泉）====================

        /// <summary>立起蓄势帧数（8 次幂迟滞后仰，一帧甩头齐抛）</summary>
        public const int CystWindupFrames = 24;
        /// <summary>抛疮数：P1 三颗，P2 四颗，P3 五颗</summary>
        public static int CystCount(int phase) => phase >= 3 ? 5 : phase == 2 ? 4 : 3;
        /// <summary>落点间距（逃生声明：疮间站缝宽于泉柱威胁面）</summary>
        public const float CystSpacing = 190f;
        /// <summary>黏附后的鼓胀引信帧数（基数）</summary>
        public const int CystSwellFrames = 46;
        /// <summary>逐颗引信错拍（顺序喷发的可读波）</summary>
        public const int CystFuseStagger = 9;
        /// <summary>抛掷飞行解算帧数（抛物线出手速度反解用）</summary>
        public const int CystLobFrames = 42;
        /// <summary>抛疮收势帧数</summary>
        public const int CystRecoverFrames = 18;

        //==================== A4 破土脓泉（钻沙突袭 + 破口引燃脓池）====================

        /// <summary>破土预告帧数（腐沙隆包 omen 的寿命）</summary>
        public const int BreachTelegraphFrames = 26;
        /// <summary>破土出土初速</summary>
        public const float BreachLaunchSpeed = 35f;
        /// <summary>突袭段重力</summary>
        public const float LungeGravity = 0.6f;
        /// <summary>接触伤害的速度门槛（伤害窗=可见冲势）</summary>
        public const float LungeContactSpeed = 13f;
        /// <summary>地下接近速度</summary>
        public const float LungeDigSpeed = 30f;
        /// <summary>突袭循环数（P2 起三次）</summary>
        public static int BreachCycles(int phase) => phase >= 2 ? 3 : 2;
        /// <summary>破土喷发灵液扇枚数</summary>
        public const int BreachEruptGlobs = 9;
        /// <summary>破土喷发扇面总角（度；贴地两侧各留 80 度逃生道）</summary>
        public const float BreachEruptArcDeg = 200f;
        /// <summary>破口引燃脓池半径（池经济小额兑现）</summary>
        public const float BreachIgniteRadius = 300f;
        /// <summary>引燃引信基数（+距离比例 = 由近及远的小行波）</summary>
        public const int BreachIgniteFuseBase = 10;

        //==================== P2 蜕变壳屑 ====================

        /// <summary>蜕皮波每隔几节甩一片旧壳（壳屑短暂坠落威胁）</summary>
        public const int MoltHuskEvery = 2;
        /// <summary>壳屑甩离初速</summary>
        public const float MoltHuskSpeed = 7.5f;

        //==================== A5 吞沙炮（鼓包沿身蠕动的活体预告 → 巨弹空爆）====================

        /// <summary>埋头吞沙帧数（尘埃向口收束；埋头静止是身份件，但不许拖过一秒）</summary>
        public const int GulpFrames = 36;
        /// <summary>鼓包尾→头蠕动帧数（加速行波 = 预告主体）</summary>
        public const int BulgeTravelFrames = 70;
        /// <summary>出手前锁向帧数（预告即承诺）</summary>
        public const int MortarLockLead = 3;
        /// <summary>炮弹飞行解算帧数（固定飞时 = 空爆时刻可预期）</summary>
        public const int MortarFlightFrames = 55;
        /// <summary>空爆点在玩家预测位上方的高度</summary>
        public const float MortarBurstHeight = 250f;
        /// <summary>炮弹重力（弹道解算与弹幕本体共用）</summary>
        public const float MortarShellGravity = 0.22f;
        /// <summary>霰弹枚数（150 度下锥）</summary>
        public const int MortarShardCount = 13;
        /// <summary>霰弹下锥总角（度）</summary>
        public const float MortarConeDeg = 150f;
        /// <summary>正下方逃生缝半角（弧度）：空爆伞的中央安全眼，发射循环实读跳过</summary>
        public const float MortarGapHalfAngle = 0.22f;
        /// <summary>伴随金雨滴数（同样跳过中央缝）</summary>
        public const int MortarRainDrops = 6;
        /// <summary>炮弹发数：P3 双弹错拍</summary>
        public static int MortarShells(int phase) => phase >= 3 ? 2 : 1;
        /// <summary>双弹错拍帧</summary>
        public const int MortarSecondDelay = 26;
        /// <summary>吞沙炮收势帧数</summary>
        public const int MortarRecoverFrames = 24;

        //==================== A6 环卷瀑洗（大范围绕圈 + 向心管流；地形无关，P1 起）====================

        /// <summary>入环就位帧数上限（提前到位即早退入圈）</summary>
        public const int CoilEntryFrames = 40;
        /// <summary>环径（圈内即战场，圈本身即笼压）</summary>
        public const float CoilRadius = 470f;
        /// <summary>基础角速（弧度/帧；P3 提速档见 CoilOmega）</summary>
        public static float CoilOmega(int phase) => phase >= 3 ? 0.052f : 0.045f;
        /// <summary>圈数：P1 一圈、P2 一圈半、P3 两圈</summary>
        public static float CoilLaps(int phase) => phase >= 3 ? 2f : phase == 2 ? 1.5f : 1f;
        /// <summary>圈心慢跟系数（不追踪只慢跟 = 圈几何稳定可读）</summary>
        public const float CoilCenterLerp = 0.02f;
        /// <summary>喷窗帧数（向心管流的占空循环之"喷"）</summary>
        public const int CoilFireFrames = 36;
        /// <summary>歇窗帧数（断流 + 吸气音 = 声明逃生拍，切向绕行/出圈窗口）</summary>
        public const int CoilRestFrames = 16;
        /// <summary>痰滴链间隔帧</summary>
        public const int CoilDropGap = 3;
        /// <summary>痰滴向心初速（14px/f 自环径 470 到圈心约 33 帧 = 反应窗）</summary>
        public const float CoilDropSpeed = 14f;
        /// <summary>喷向提前角（沿转向偏置，辐条追着转速走而非追玩家）</summary>
        public const float CoilLeadAngle = 0.3f;
        /// <summary>留池滴间隔（向心滴多在空中耗尽，落地留池更稀疏）</summary>
        public const int CoilPoolEvery = 7;
        /// <summary>收束切线冲刺速度（P2 起的出圈标点）</summary>
        public const float CoilExitDashSpeed = 40f;
        /// <summary>切线冲刺飞行帧数</summary>
        public const int CoilExitFlightFrames = 16;

        //==================== A10 灵液门冲（开门隐身传送 + 出门爆冲；地形无关，P2 起）====================

        /// <summary>门冲循环数：P3 三门</summary>
        public static int PortalReps(int phase) => phase >= 3 ? 3 : 2;
        /// <summary>出口门最短预告帧（生成到爆冲；门就是预告实体）</summary>
        public const int PortalOpenLeadFrames = 42;
        /// <summary>进门点在蛇前方的距离</summary>
        public const float PortalEntryOffset = 300f;
        /// <summary>出口门绕玩家半径（生成即锁位锁向）</summary>
        public const float PortalExitRadius = 560f;
        /// <summary>钻入段帧数上限（到不了也强制吞入）</summary>
        public const int PortalDiveMaxFrames = 40;
        /// <summary>门内滞留帧数（双门脉动的吊拍；实际爆冲还须满足预告下限）</summary>
        public const int PortalInsideFrames = 12;
        /// <summary>出门爆冲速度</summary>
        public const float PortalBurstSpeed = 44f;
        /// <summary>爆冲飞行帧数</summary>
        public const int PortalBurstFlightFrames = 16;
        /// <summary>爆冲硬刹帧数</summary>
        public const int PortalBrakeFrames = 8;
        /// <summary>门实体寿命兜底（状态会提前收门）</summary>
        public const int PortalGateLife = 240;

        //==================== A7 疮爆掠航（高速掠过 + 囊肿链式爆裂）====================

        /// <summary>就位帧数（拉开掠航跑道）</summary>
        public const int RippleStalkFrames = 8;
        /// <summary>蓄力后撤帧数（短版，主菜在航过链爆）</summary>
        public const int RippleWindupFrames = 14;
        /// <summary>掠航速度</summary>
        public const float RipplePassSpeed = 41f;
        /// <summary>掠航段帧数上限（越过玩家即早退）</summary>
        public const int RipplePassMaxFrames = 30;
        /// <summary>越身判定距离</summary>
        public const float RippleOvershoot = 240f;
        /// <summary>掠航收势帧数</summary>
        public const int RippleBrakeFrames = 10;
        /// <summary>每颗囊肿的灵液滴数（沿体法向两翼短弧）</summary>
        public const int RippleDropsPerCyst = 3;
        /// <summary>灵液滴初速（慢滴 + 重力 = 身后走廊可读可避）</summary>
        public const float RippleDropSpeed = 8.5f;
        /// <summary>掠航跑道最短距离</summary>
        public const float RippleRunwayMin = 420f;

        //==================== A11 裂躯交叉（P3：中段撕裂双蛇 + 同帧交叉冲刺编舞）====================

        /// <summary>立身撕裂帧数（缝节渐亮到炸开）</summary>
        public const int SunderTearFrames = 30;
        /// <summary>两半分赴对角锚点帧数上限（双双到位即早退）</summary>
        public const int SunderRegroupFrames = 50;
        /// <summary>锚点距玩家距离</summary>
        public const float SunderAnchorDist = 520f;
        /// <summary>锚点相对头顶正上的偏角（两锚点相隔约 90° = 冲线近正交）</summary>
        public const float SunderAnchorSpread = 0.9f;
        /// <summary>交叉冲刺蓄力帧数（双向同时预亮）</summary>
        public const int SunderWindupFrames = 14;
        /// <summary>锁向提前量（预告即承诺）</summary>
        public const int SunderLockLead = 5;
        /// <summary>交叉冲刺速度</summary>
        public const float SunderDashSpeed = 44f;
        /// <summary>冲刺飞行帧数</summary>
        public const int SunderFlightFrames = 18;
        /// <summary>硬刹帧数</summary>
        public const int SunderBrakeFrames = 8;
        /// <summary>交叉次数（每次后两半换边）</summary>
        public const int SunderCrossReps = 2;
        /// <summary>合体引导帧数上限（到时强制焊合）</summary>
        public const int SunderMergeFrames = 80;
        /// <summary>焊合判距（领节贴回前半尾节即恢复跟链）</summary>
        public const float SunderMergeSnapDist = 46f;

        //==================== 脓池引爆（池经济兑现的公共口径）====================

        /// <summary>受燃脓池的泉柱高度档：0 常规 / 1 高柱（P3 满场引爆用高柱）</summary>
        public const float PoolGeyserTall = 1f;

        //==================== A8 满场引爆（P3：池经济的总兑现）====================

        /// <summary>立起至全高帧数（八腿撑地剪影即预告）</summary>
        public const int DetonateRaiseFrames = 34;
        /// <summary>怒吼蓄力帧数（吼即全场引信起跑）</summary>
        public const int DetonateChargeFrames = 40;
        /// <summary>喷发行波波前速度（像素/帧；由近及远可沿波前穿行）</summary>
        public const float DetonateWaveSpeed = 26f;
        /// <summary>开演最少池数：不足先快速环射补种</summary>
        public const int DetonateMinPools = 4;
        /// <summary>补种环射枚数</summary>
        public const int DetonateSeedGlobs = 8;
        /// <summary>引爆有效半径（全场口径）</summary>
        public const float DetonateMaxRadius = 3400f;
        /// <summary>收招呼吸拍（行波过后的整拍留白）</summary>
        public const int DetonateBreathFrames = 30;

        //==================== 通用节奏 ====================

        /// <summary>hub 连接段最短帧数（换招的一口气）</summary>
        public const int ConnectorFrames = 4;

        /// <summary>转阶段后的弹速爬坡帧数（公平阀：新阶段首招八成速起步）</summary>
        public const int PostTransitionRampFrames = 60;

        /// <summary>出招冷却：阶段越深越快（每招自带预告帧兜底可读性，冷却只管衔接）</summary>
        public static int AttackCooldown(int phase) => phase switch {
            >= 3 => 4,
            2 => 6,
            _ => 9,
        };

        /// <summary>骚扰滴射周期（帧，按阶段提速；hub 底噪）</summary>
        public static int HarassGap(int phase) => phase switch {
            >= 3 => 22,
            2 => 28,
            _ => 38,
        };
        /// <summary>骚扰预亮帧数（囊肿节先亮再射 = 预告）</summary>
        public const int HarassGlowLead = 12;
        /// <summary>每次骚扰的慢速灵液滴数</summary>
        public const int HarassDrops = 2;

        //==================== 囊肿资源（疮爆掠航的可读充能）====================

        /// <summary>囊肿爆后的充能帧数（约 8 秒；期间瘪着不发光）</summary>
        public const int CystRechargeFrames = 480;

        /// <summary>NPC 弹幕伤害换算：普通/专家双基数</summary>
        public static int ScaleProjectileDamage(NPC npc, (float Normal, float Expert) baseDamage)
            => (int)npc.GetAttackDamage_ForProjectiles(baseDamage.Normal, baseDamage.Expert);
    }
}
