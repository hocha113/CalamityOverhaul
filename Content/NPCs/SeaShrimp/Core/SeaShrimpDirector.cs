using Terraria;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Core
{
    /// <summary>渊晶海虾战斗与运动学调参中心（占位初值，游戏内验收再调）</summary>
    internal static class SeaShrimpDirector
    {
        //==================== 基础数值（石巨人后基准）====================

        /// <summary>基础生命（普通模式，专家/大师由原版规则自乘）</summary>
        public static int BaseLife => 86000;
        /// <summary>接触基伤（仅冲撞/螯击窗内启用，AI 每帧默认清零）</summary>
        public static int ContactDamage => 88;
        /// <summary>基础防御</summary>
        public static int BaseDefense => 30;

        //==================== 弹幕基伤（normal/expert，走 GetAttackDamage_ForProjectiles）====================

        /// <summary>螯尖判定线</summary>
        public static (float Normal, float Expert) ClawStrikeDamage => (68f, 56f);
        /// <summary>空泡爆缩</summary>
        public static (float Normal, float Expert) CavitationDamage => (78f, 64f);
        /// <summary>水弹</summary>
        public static (float Normal, float Expert) WaterBoltDamage => (56f, 46f);
        /// <summary>晶刺</summary>
        public static (float Normal, float Expert) CrystalSpikeDamage => (62f, 52f);
        /// <summary>泡幕气泡</summary>
        public static (float Normal, float Expert) BubbleDamage => (50f, 42f);
        /// <summary>壳屑弹片</summary>
        public static (float Normal, float Expert) ShellFragDamage => (54f, 45f);
        /// <summary>渊喉水炮</summary>
        public static (float Normal, float Expert) JetDamage => (88f, 72f);
        /// <summary>水龙卷（封场柱与行走小涡共用）</summary>
        public static (float Normal, float Expert) VortexDamage => (72f, 60f);
        /// <summary>间歇泉柱</summary>
        public static (float Normal, float Expert) GeyserDamage => (64f, 53f);
        /// <summary>合钳水刃</summary>
        public static (float Normal, float Expert) CrescentDamage => (66f, 55f);
        /// <summary>巨型雷泡（飞行本体与崩爆共用）</summary>
        public static (float Normal, float Expert) VoltBubbleDamage => (80f, 66f);
        /// <summary>带电小泡起爆</summary>
        public static (float Normal, float Expert) SparkBubbleDamage => (52f, 43f);
        /// <summary>泡间电弧</summary>
        public static (float Normal, float Expert) BubbleArcDamage => (58f, 48f);
        /// <summary>跃空砸落巨浪</summary>
        public static (float Normal, float Expert) WaveCrestDamage => (70f, 58f);

        //==================== 感知与脱战 ====================

        /// <summary>目标失效判定距离</summary>
        public const float MaxFindDistance = 6400f;
        /// <summary>出招的最大交战距离</summary>
        public const float EngageDistance = 1250f;
        /// <summary>超过此距离硬追</summary>
        public const float LeashDistance = 2400f;

        //==================== 脊链（头→体节1→2→3→尾扇，节距按贴图 2x 尺寸估）====================

        /// <summary>节距：头中心→体节1、1→2、2→3、3→尾扇（比贴图裸尺寸收紧一档，弯折时不豁口；
        /// 末段配合尾扇前缘锚再收，保证尾扇咬进体节3）</summary>
        public static readonly float[] SpineGaps = [82f, 37f, 32f, 26f];
        /// <summary>相邻关节最大弯角 rad（防折叠，同时压住弯折豁口）</summary>
        public const float SpineMaxBend = 0.5f;
        /// <summary>节向角平滑速率</summary>
        public const float SpineTurnRate = 0.38f;
        /// <summary>爬行 S 波每节相位差 rad</summary>
        public const float CrawlWaveStep = 1.15f;
        /// <summary>爬行 S 波满速振幅 rad</summary>
        public const float CrawlWaveAmp = 0.085f;
        /// <summary>尾弹蓄力 C 卷每关节角 rad（curl=1 时）</summary>
        public const float CurlPerJoint = 0.44f;

        //==================== 双螯 IK ====================

        /// <summary>肩锚：沿头轴向前 / 垂直头轴偏移</summary>
        public const float ShoulderForward = 26f;
        public const float ShoulderSide = 20f;
        /// <summary>上臂骨长（臂节1）</summary>
        public const float ArmBone1 = 92f;
        /// <summary>前臂骨长（臂节2）</summary>
        public const float ArmBone2 = 80f;
        /// <summary>IK 目标弹簧刚度 / 阻尼（守位）</summary>
        public const float ArmSpring = 0.16f;
        public const float ArmDamping = 0.74f;

        //==================== 六足步态 ====================

        /// <summary>髋-足距超过此值触发迈步 px</summary>
        public const float StepThreshold = 46f;
        /// <summary>一步帧数</summary>
        public const int StepFrames = 9;
        /// <summary>抬脚高度 px</summary>
        public const float StepLift = 16f;
        /// <summary>落足点前探量（沿行进向）px</summary>
        public const float StrideLead = 34f;
        /// <summary>腿可及半径 px（超出则悬空）</summary>
        public const float LegReach = 96f;

        //==================== 凝视逼近（NightmareReaper 式分镜：头恒对玩家，环距弹簧）====================

        /// <summary>驻停环距 px：逼近到此距离停住漂移</summary>
        public const float StalkHoldDistance = 380f;
        /// <summary>超出此距离恢复逼近</summary>
        public const float StalkResumeFar = 580f;
        /// <summary>近于此距离恢复移动（同一弹簧自然后退）</summary>
        public const float StalkResumeNear = 150f;
        /// <summary>每帧最大转向 rad（恒速转头，蓄意感）</summary>
        public const float StalkTurnRate = MathHelper.Pi / 30f;
        /// <summary>体轴离地高度 px（入场/晶刺落点等地形参考仍用）</summary>
        public const float RideHeight = 46f;

        //==================== 双螯空间抓握（手撑屏幕平面，交替抓行）====================

        /// <summary>抓握节拍总长（两手错半拍）</summary>
        public const int GripCycleFrames = 30;
        /// <summary>单次挪抓时长</summary>
        public const int GripLurchFrames = 12;
        /// <summary>休息抓点前伸量（沿头前向）</summary>
        public const float GripForward = 126f;
        /// <summary>休息抓点侧展量</summary>
        public const float GripSide = 128f;

        //==================== 游泳 ====================

        /// <summary>游泳巡航速度 px/f</summary>
        public const float SwimSpeed = 10.5f;
        /// <summary>游泳趋近系数</summary>
        public const float SwimApproach = 0.055f;
        /// <summary>游泳惯性混合</summary>
        public const float SwimInertia = 0.085f;

        //==================== 尾弹（虾式后向爆发，Old Duke 基线虾化：更快更短更硬刹）====================

        /// <summary>蓄力卷曲帧数</summary>
        public const int TailFlipWindup = 34;
        /// <summary>弹射初速 px/f</summary>
        public const float TailFlipSpeed = 40f;
        /// <summary>弹射持续帧数</summary>
        public const int TailFlipFrames = 15;
        /// <summary>弹射后每帧刹车系数</summary>
        public const float TailFlipBrake = 0.85f;

        //==================== 空泡拳 ====================

        /// <summary>空泡爆缩延迟（拳后第二拍）</summary>
        public const int CavitationCollapseDelay = 26;
        /// <summary>空泡爆缩半径（逃逸=离开此圈，可见气泡半径即判定半径）</summary>
        public const float CavitationBubbleRadius = 118f;
        /// <summary>拳伸展距离</summary>
        public const float PunchReach = 275f;

        //==================== 尾扇水弹 ====================

        /// <summary>扇内相邻弹道角距 rad（声明式缺口：弹间即通道）</summary>
        public const float BoltAngleGap = 0.27f;
        /// <summary>单轮弹数</summary>
        public const int BoltsPerVolley = 5;
        /// <summary>水弹初速 px/f</summary>
        public const float WaterBoltSpeed = 11.5f;

        //==================== 扩编批（2026-08）：封场/水炮/间歇泉/涡旋/水刃/犁浪 ====================

        /// <summary>双渊柱场地半宽 px（封场龙卷距场心的距离，场内即安全声明）</summary>
        public const float ArenaHalfWidth = 1150f;
        /// <summary>封场龙卷可见高度 px</summary>
        public const float VortexWallHeight = 920f;
        /// <summary>封场龙卷判定芯半宽 px（判定藏在可见体内：名义可见宽 ~170）</summary>
        public const float VortexWallCoreHalfWidth = 52f;
        /// <summary>行走小龙卷：可见高度 / 行军速度 px每帧 / 生成最小间距（声明式缺口）</summary>
        public const float MiniVortexHeight = 260f;
        public const float MiniVortexSpeed = 2.2f;
        public const float MiniVortexGap = 240f;
        /// <summary>渊喉水炮：可见满宽 / 最大射程 / 扫速 rad每帧（声明式）/ 全宽持续帧</summary>
        public const float JetWidth = 110f;
        public const float JetMaxLength = 1500f;
        public const float JetSweepRate = 0.0042f;
        public const int JetFireFrames = 96;
        /// <summary>水炮判定芯宽 = 可见满宽 × 此系数（判定不宽于可见体）</summary>
        public const float JetCoreFrac = 0.62f;
        /// <summary>间歇泉行军：步距 px（声明式缺口）/ 根数 / 逐根错帧</summary>
        public const float GeyserStep = 170f;
        public const int GeyserCount = 6;
        public const int GeyserStagger = 9;
        /// <summary>犁浪冲锋：初速 / 冲刺帧 / 最大俯仰 rad（贴地冲锋，不追高）</summary>
        public const float PlowSpeed = 30f;
        public const int PlowFrames = 26;
        public const float PlowMaxPitch = 0.30f;

        //==================== 扩编批（2026-08 二期）：雷泡大炮/泡球连拍/跃空砸落 ====================

        /// <summary>雷泡满径 px（生长终值，飞行判定同径）</summary>
        public const float VoltBubbleRadius = 130f;
        /// <summary>雷泡拍出速度 px/f</summary>
        public const float VoltBubbleSpeed = 26f;
        /// <summary>雷泡崩爆环半径 px</summary>
        public const float VoltBlastRadius = 300f;
        /// <summary>雷泡崩爆散出的小泡数（角序单圈，速度交替内外错落）</summary>
        public const int SparkBubbleCount = 14;
        /// <summary>小泡起爆环半径 px</summary>
        public const float SparkBlastRadius = 90f;
        /// <summary>小泡错帧起爆：基础延迟 / 逐个递增帧（基础延迟给足散开时间——先飞出爆区再链爆）</summary>
        public const int SparkBurstBase = 52;
        public const int SparkBurstStep = 7;
        /// <summary>小泡散射初速 px/f（0.965 阻尼下积分距离 ≈ 初速×28.6，散到崩爆环外）</summary>
        public const float SparkScatterSpeed = 12f;
        /// <summary>泡球被拍飞速度 px/f</summary>
        public const float BattedBubbleSpeed = 30f;
        /// <summary>泡球待拍泡半径 px</summary>
        public const float BatBubbleRadius = 30f;
        /// <summary>跃空上跳初速 px/f</summary>
        public const float LeapUpSpeed = 34f;
        /// <summary>跃空下砸速度 px/f</summary>
        public const float LeapSlamSpeed = 46f;
        /// <summary>巨浪行进速度 px/f / 浪体高 px / 行进距离 px</summary>
        public const float WaveCrestSpeed = 9f;
        public const float WaveCrestHeight = 1000f;
        public const float WaveCrestRange = 700f;
        /// <summary>落地水龙卷可见高度 px</summary>
        public const float LeapVortexHeight = 2000f;
        /// <summary>落地喷泉水球数</summary>
        public const int LeapBoltCount = 16;

        //==================== 通用节奏 ====================

        /// <summary>攻击间 connector 帧数</summary>
        public const int ConnectorFrames = 18;

        /// <summary>出招冷却缩放：蜕壳后提速</summary>
        public static int ScaleCooldown(int baseCooldown, int phase)
            => phase >= 3 ? (int)(baseCooldown * 0.62f) : baseCooldown;

        /// <summary>NPC 弹幕伤害换算：普通/专家双基数</summary>
        public static int ScaleProjectileDamage(NPC npc, (float Normal, float Expert) baseDamage)
            => (int)npc.GetAttackDamage_ForProjectiles(baseDamage.Normal, baseDamage.Expert);
    }
}
