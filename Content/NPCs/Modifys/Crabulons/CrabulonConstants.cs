namespace CalamityOverhaul.Content.NPCs.Modifys.Crabulons
{
    /// <summary>
    /// 菌生蟹常量定义
    /// </summary>
    internal static class CrabulonConstants
    {
        //生命值相关
        public const int LifeMaxMaster = 8000;
        public const int LifeMaxNormal = 6000;
        public const int HealAmount = 10;
        public const int HealInterval = 60;

        //驯服相关
        public const float FeedValuePerFeed = 500f;
        public const float MaxFeedValue = 5000f;
        public const int FeedHealAmount = 20;
        public const int DigestTime = 120;

        //移动相关
        public const float MoveSpeed = 4f;
        public const float MoveInertia = 15f;
        public const float FollowDistance = 150f;
        public const float AttackMoveSpeed = 8f;
        public const float AttackFollowDistance = 100f;

        //跳跃相关
        public const float JumpVelocity = -20f;
        public const float HighJumpVelocity = -12f;
        public const int MaxStepHeight = 116;
        public const int StepCheckInterval = 8;

        //传送相关
        public const float TeleportDistance = 2600f;
        public const int TeleportDelay = 160;
        public const int TeleportEffectCount = 132;
        public const float TeleportSpawnHeight = -200f;

        //边界限制
        public const ushort WorldBorder = 560;

        //蹲伏相关
        public const int CrouchAnimationMax = 60;
        public const int CrouchAnimationSpeed = 2;

        //骑乘相关
        public const float BaseAcceleration = 0.4f;
        public const float BaseSpeed = 6f;
        public const float MinSpeed = 3f;
        public const float MaxSpeed = 30f;
        public const float Friction = 0.85f;
        public const float MountJumpMultiplier = -3.6f;
        public const float MinMountJump = -30f;
        public const float MaxMountJump = -3f;
        public const int MountTimeout = 60;
        public const int DismountCooldown = 30;

        //地面检测相关
        public const float MaxGroundDistance = 1000f;
        public const int GroundCheckInterval = 16;

        //落地冲击相关
        public const int MinImpactDistance = 300;
        public const float ImpactSoundVolume = 0.5f;
        public const float ImpactVolumeMultiplier = 600f;
        public const int MinDustCount = 5;
        public const int MaxDustCount = 40;
        public const int ImpactDustDivisor = 30;
        public const int BaseDamage = 120;
        public const int DamagePerImpact = 60;
        public const float ImpactKnockback = 2f;

        //帧动画相关
        public const float FrameLerpSpeed = 0.1f;
        public const float FallFrameLerpSpeed = 0.2f;
        public const int JumpFrame = 1;
        public const int FallFrame = 4;
        public const float RunFrameSpeed = 0.04f;
        public const float IdleFrameSpeed = 0.15f;
        public const float CrouchIdleFrameSpeed = 0.1f;
        public const int MaxIdleFrames = 2;

        //搜敌相关
        public const float SearchRange = 1000f;

        //AI计时器相关
        public const int VerticalChaseTime = 110;
        public const int PlatformFallTime = 10;
        public const int TurnDelayTime = 10;
        public const int ParticleEffectTime1 = 90;
        public const int ParticleEffectTime2 = 30;
        public const int ParticleCount1 = 16;
        public const int ParticleCount2 = 66;
        public const int HealParticleCount = 6;
    }
}
