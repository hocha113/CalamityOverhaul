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

        //==================== 感知与脱战 ====================

        /// <summary>目标失效判定距离</summary>
        public const float MaxFindDistance = 6400f;
        /// <summary>出招的最大交战距离</summary>
        public const float EngageDistance = 1250f;
        /// <summary>超过此距离硬追</summary>
        public const float LeashDistance = 2400f;

        //==================== 脊链（头→体节1→2→3→尾扇，节距按贴图 2x 尺寸估）====================

        /// <summary>节距：头中心→体节1、1→2、2→3、3→尾扇</summary>
        public static readonly float[] SpineGaps = [96f, 44f, 38f, 46f];
        /// <summary>相邻关节最大弯角 rad（防折叠）</summary>
        public const float SpineMaxBend = 0.62f;
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

        //==================== 地面运动 ====================

        /// <summary>体轴离地高度 px</summary>
        public const float RideHeight = 46f;
        /// <summary>爬行满速 px/f</summary>
        public const float CrawlSpeed = 8.6f;
        /// <summary>爬行加速度 px/f²</summary>
        public const float CrawlAccel = 0.24f;
        /// <summary>贴地弹簧系数</summary>
        public const float SurfaceStick = 0.22f;
        /// <summary>法线平滑速率</summary>
        public const float NormalLerp = 0.14f;

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
