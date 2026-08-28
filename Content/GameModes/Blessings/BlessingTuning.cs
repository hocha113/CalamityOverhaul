namespace CalamityOverhaul.Content.GameModes.Blessings
{
    /// <summary>祝福数值表：槽位规则与全部效果常量收拢在此，效果文件不散写数字</summary>
    internal static class BlessingTuning
    {
        //——燃焰槽位：同时点燃的上限，随讨伐数成长——

        /// <summary>燃焰槽基础数</summary>
        public const int SlotBase = 3;
        /// <summary>每讨伐多少尊 Boss 增一槽</summary>
        public const int SlotGrowthStep = 4;
        /// <summary>燃焰槽上限</summary>
        public const int SlotMax = 7;

        //——史莱姆王·御胶之冕——

        /// <summary>接触伤害承伤乘数</summary>
        public const float KingSlimeContactMult = 0.88f;

        //——克苏鲁之眼·渊瞳——

        /// <summary>暴击率加成（百分点）</summary>
        public const float EyeCritBonus = 5f;
        /// <summary>夜间追加暴击率（百分点）</summary>
        public const float EyeNightCritBonus = 3f;

        //——世界吞噬者·腐环——

        /// <summary>侵蚀减防层值</summary>
        public const int EaterErosionDefense = 10;
        /// <summary>侵蚀持续帧数</summary>
        public const int EaterErosionDuration = 300;

        //——克苏鲁之脑·痛觉共感——

        /// <summary>受击后增伤持续帧数</summary>
        public const int BrainSurgeDuration = 300;
        /// <summary>增伤幅度</summary>
        public const float BrainSurgeDamage = 0.12f;

        //——蜂后·蜂巢再生——

        /// <summary>治疗量乘数</summary>
        public const float QueenBeeHealMult = 1.15f;

        //——骷髅王·亡骨守望——

        /// <summary>格挡冷却帧数</summary>
        public const int SkeletronGuardCooldown = 45 * 60;
        /// <summary>格挡时的承伤乘数</summary>
        public const float SkeletronGuardMult = 0.5f;

        //——独眼巨鹿·凛冬之步——

        /// <summary>移速加成</summary>
        public const float DeerclopsMoveSpeed = 0.08f;

        //——血肉之墙·血肉盟约——

        /// <summary>受伤转化为回复池的比例</summary>
        public const float WallFleshRefundRatio = 0.2f;
        /// <summary>回复池排空帧数</summary>
        public const int WallFleshRefundDuration = 8 * 60;

        //——史莱姆皇后·晶羽——

        /// <summary>跳跃速度加成</summary>
        public const float QueenSlimeJumpBoost = 1.2f;

        //——双子魔眼·双瞳协奏——

        /// <summary>每层增伤</summary>
        public const float TwinsStackDamage = 0.02f;
        /// <summary>最大层数</summary>
        public const int TwinsMaxStacks = 5;

        //——毁灭者·探针回路——

        /// <summary>击杀回魔量</summary>
        public const int DestroyerManaOnKill = 3;
        /// <summary>免耗弹药概率（1/N）</summary>
        public const int DestroyerAmmoSaveDenominator = 12;

        //——机械骷髅王·过载骨架——

        /// <summary>使用速度乘数</summary>
        public const float PrimeUseSpeedMult = 1.05f;

        //——世纪之花·荆棘新生——

        /// <summary>常驻生命再生（lifeRegen 单位，2 = 每秒 1 点）</summary>
        public const int PlanteraRegenBase = 2;
        /// <summary>受击后追加再生（lifeRegen 单位）</summary>
        public const int PlanteraRegenSurge = 6;
        /// <summary>受击后追加再生持续帧数</summary>
        public const int PlanteraSurgeDuration = 180;

        //——石巨人·岩心——

        /// <summary>防御加成</summary>
        public const int GolemDefense = 8;

        //——猪龙鱼公爵·怒潮——

        /// <summary>触发的生命比例阈值</summary>
        public const float FishronLifeThreshold = 0.5f;
        /// <summary>移速加成</summary>
        public const float FishronMoveSpeed = 0.12f;
        /// <summary>伤害加成</summary>
        public const float FishronDamage = 0.08f;

        //——光之女皇·昼光裁决——

        /// <summary>白天暴击伤害加成</summary>
        public const float EmpressDayCritDamage = 0.18f;

        //——拜月教邪教徒·月相护盾——

        /// <summary>护盾充能帧数</summary>
        public const int CultistShieldCooldown = 40 * 60;

        //——月球领主·星核共鸣——

        /// <summary>全伤害加成</summary>
        public const float MoonLordDamage = 0.08f;
    }
}
