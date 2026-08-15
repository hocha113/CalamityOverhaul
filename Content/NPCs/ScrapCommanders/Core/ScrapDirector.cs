using Terraria;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders.Core
{
    /// <summary>废钢统帅战斗调参中心（占位初值，验收再调）</summary>
    internal static class ScrapDirector
    {
        //==================== 基础数值 ====================

        /// <summary>基础生命（普通模式，专家/大师由原版规则自乘）</summary>
        public static int BaseLife => 52000;
        /// <summary>接触基伤（仅头锤等状态窗内启用）</summary>
        public static int ContactDamage => 72;
        /// <summary>基础防御</summary>
        public static int BaseDefense => 26;

        //==================== 弹幕基伤（normal/expert，走 GetAttackDamage_ForProjectiles）====================

        /// <summary>突刺臂击判定线</summary>
        public static (float Normal, float Expert) ArmStrikeDamage => (44f, 38f);
        /// <summary>迫击弹</summary>
        public static (float Normal, float Expert) MortarDamage => (36f, 30f);
        /// <summary>脱链地锯</summary>
        public static (float Normal, float Expert) GroundSawDamage => (30f, 26f);
        /// <summary>镭射短脉冲</summary>
        public static (float Normal, float Expert) LaserPulseDamage => (32f, 27f);

        //==================== 感知与脱战 ====================

        /// <summary>目标失效判定距离</summary>
        public const float MaxFindDistance = 6000f;
        /// <summary>悬停出招的最大交战距离</summary>
        public const float EngageDistance = 1150f;
        /// <summary>超过此距离硬贴回目标侧</summary>
        public const float LeashDistance = 2600f;

        //==================== 突刺 ====================

        /// <summary>突刺蓄力回缩帧数（长预警，敌对版比鬼奴更慢）</summary>
        public const int DartWindup = 40;
        /// <summary>弹出段帧数</summary>
        public const int DartExtendFrames = 10;
        /// <summary>弹出初速 px/f</summary>
        public const float DartLaunchSpeed = 44f;
        /// <summary>链长上限</summary>
        public const float DartMaxReach = 470f;

        //==================== 迫击 ====================

        public const int MortarPoseFrames = 24;
        public const int MortarShotGap = 14;
        public const int MortarShots = 3;
        /// <summary>迫击弹重力，弹道解算与弹幕本体共用同一常数</summary>
        public const float MortarGravity = 0.42f;

        //==================== 钳爪 ====================

        /// <summary>钳爪突刺预警帧数（比锯更长，配红色预警线）</summary>
        public const int ViceWindup = 45;
        /// <summary>钳爪链长上限（比锯伸得更远）</summary>
        public const float ViceMaxReach = 520f;

        //==================== 镭射 ====================

        /// <summary>滑步帧数</summary>
        public const int LaserStrafeFrames = 12;
        /// <summary>单组双发窗口帧数（每发出膛前 8 帧积光）</summary>
        public const int LaserVolleyFrames = 20;
        /// <summary>脉冲满速 px/f（首组 80% 热身）</summary>
        public const float LaserPulseSpeed = 26f;

        //==================== 头锤摆荡 ====================

        /// <summary>反向拉起蓄势帧数</summary>
        public const int SwingWindup = 36;
        /// <summary>摆荡出手初速 px/f</summary>
        public const float SwingLaunchSpeed = 34f;
        /// <summary>接触伤害的速度门槛 px/f（对齐可见冲势）</summary>
        public const float SwingContactSpeed = 20f;

        //==================== 通用节奏 ====================

        /// <summary>攻击间垂链泄压 connector 帧数（快节奏版：喘息靠招内结构，不靠站桩）</summary>
        public const int ConnectorFrames = 14;

        /// <summary>出招冷却缩放：过载阶段提速</summary>
        public static int ScaleCooldown(int baseCooldown, int phase)
            => phase >= 3 ? (int)(baseCooldown * 0.6f) : baseCooldown;

        /// <summary>NPC 弹幕伤害换算：普通/专家双基数</summary>
        public static int ScaleProjectileDamage(NPC npc, (float Normal, float Expert) baseDamage)
            => (int)npc.GetAttackDamage_ForProjectiles(baseDamage.Normal, baseDamage.Expert);
    }
}
