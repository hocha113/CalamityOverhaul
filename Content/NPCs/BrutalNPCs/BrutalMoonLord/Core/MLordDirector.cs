namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core
{
    /// <summary>战斗调参中心 + 天体配色。材质=幻影星质：星尘拖尾/引力弯折/相位明灭</summary>
    internal static class MLordDirector
    {
        //―――― 配色（幻影星质，与三机械的热能红热划清界限）――――
        /// <summary>幽蓝青，幻影能量主色</summary>
        public static Color Phantasmal => new(99, 233, 216);
        /// <summary>深空紫，暗部与外缘</summary>
        public static Color DeepViolet => new(96, 66, 176);
        /// <summary>月白，高光（常驻禁用纯白，仅短脉冲）</summary>
        public static Color MoonWhite => new(226, 244, 255);
        /// <summary>蚀金，日蚀冕环专用点缀</summary>
        public static Color EclipseGold => new(255, 202, 112);
        /// <summary>虚空黑，黑闪大招吞光体（AlphaBlend 真遮挡，禁止加色）</summary>
        public static Color VoidBlack => new(10, 6, 18);
        /// <summary>黑闪红，黑闪大招电弧缘专用（与常驻蓝紫划清界限，只在大招期出现）</summary>
        public static Color BlackFlashRed => new(255, 46, 58);

        //―――― 预警节拍（按危险层级取常数，玩家可内化）――――
        /// <summary>光束类预警帧</summary>
        public static int BeamTelegraphFrames => 88;
        /// <summary>掌击类预警帧</summary>
        public static int SlamTelegraphFrames => 40;
        /// <summary>星陨预兆帧（星图显现到第一颗坠落）</summary>
        public static int StarfallTelegraphFrames => 66;

        //―――― 弹幕基伤（原版口径：难度倍率由受击侧自动结算）――――
        public static int BoltDamage => 32;          //幻影波弹 462
        public static int EyeDamage => 30;           //幻影眼 452
        public static int OrbDamage => 40;           //幻影星球
        public static int ScanRayDamage => 70;       //扫描死光
        public static int ArcRayDamage => 76;        //弧光死光
        public static int UltRayDamage => 82;        //大招追踪死光
        public static int CometDamage => 62;         //星陨彗星
        public static int StarfireDamage => 45;      //星火余留
        public static int PalmContactDamage => 96;   //掌击接触
        public static int EyeLinkDamage => 58;       //真眼链式死光（集群组合技）
        public static int EyeScissorDamage => 64;    //真眼剪式弧光
        //投技连段（被抓者无法闪避：预算刻意压低，另有被抓端 1 血兜底）
        public static int GrabLashDamage => 16;      //处刑触须抽打
        public static int GrabRayDamage => 34;       //处刑贴脸死光
        //黑闪大招（长预告演出级：接触伤害与爆点窗口都对齐可见形体）
        public static int BlackHoleContactDamage => 90;  //黑洞本体接触
        public static int BlackFlashBurstDamage => 132;  //黑闪爆点

        //―――― 部件血量比例（SetProperty 各端确定性执行）――――
        public static float CoreLifeFactor => 0.9f;
        public static float HandLifeFactor => 0.9f;
        public static float HeadLifeFactor => 0.9f;

        //―――― 阵形几何（上对沿用原版剪影，下对自腋下略低外张）――――
        /// <summary>上对肩锚点相对核心偏移（原版口径 (220,-60)）</summary>
        public static Vector2 ShoulderOffset => new(220f, -60f);
        /// <summary>下对肩锚点相对核心偏移（腋下略低，被胸甲/披风半遮以示"次生"）</summary>
        public static Vector2 LowerShoulderOffset => new(152f, 20f);
        /// <summary>上对手常态位相对核心偏移（X 取边位镜像）</summary>
        public static Vector2 HandHomeOffset => new(350f, -100f);
        /// <summary>下对手常态位相对核心偏移（外张，构图呈 X 形展开）</summary>
        public static Vector2 LowerHandHomeOffset => new(444f, 70f);
        /// <summary>头焊接位相对核心偏移</summary>
        public static Vector2 HeadWeldOffset => new(0f, -400f);
        /// <summary>核心悬停位相对目标玩家偏移</summary>
        public static Vector2 CoreHoverOffset => new(0f, 130f);

        //―――― 全局阀 ――――
        /// <summary>远距回归瞬移距离</summary>
        public static float FarSnapDistance => 2600f;
        /// <summary>触发死亡演出的核心生命阈值</summary>
        public static int DeathTriggerLife => 10;
        /// <summary>大招解锁的核心生命比例</summary>
        public static float UltLifeRatio => 0.4f;
        /// <summary>黑闪解锁的核心生命比例（比虚空撕裂更迟，终局底牌）</summary>
        public static float BlackFlashLifeRatio => 0.22f;
        /// <summary>黑闪蓄力打断阈值：揉搓窗内核心失血达最大生命此比例即失手</summary>
        public static float BlackFlashBreakRatio => 0.05f;

        /// <summary>死亡模式/BossRush 节奏倍率：帧数除以它</summary>
        public static float TempoScale(bool deathMode) => deathMode ? 1.22f : 1f;

        /// <summary>按节奏倍率压缩帧数（死亡模式更快）</summary>
        public static int Frames(int baseFrames, bool deathMode) {
            return deathMode ? (int)(baseFrames / TempoScale(true)) : baseFrames;
        }

        /// <summary>死亡模式弹幕加伤</summary>
        public static int ScaleDamage(int damage, bool deathMode) {
            return deathMode ? (int)(damage * 1.15f) : damage;
        }
    }
}
