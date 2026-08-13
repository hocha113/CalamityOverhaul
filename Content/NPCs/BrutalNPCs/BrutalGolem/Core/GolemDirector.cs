namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core
{
    /// <summary>石巨人战斗调参中心</summary>
    internal static class GolemDirector
    {
        #region 阶段与全局
        /// <summary>二阶段（头部分离）血量比阈值</summary>
        public const float SunderLifeRatio = 0.55f;
        /// <summary>低血大招血量比阈值</summary>
        public const float UltLifeRatio = 0.22f;
        /// <summary>触发死亡演出的生命阈值</summary>
        public const int DeathTriggerLife = 10;
        /// <summary>目标失效判定距离</summary>
        public const int MaxFindDistance = 4600;
        /// <summary>激怒（神庙外/地表上）节奏乘数</summary>
        public const float EnrageTempo = 0.72f;
        #endregion

        #region 拳
        /// <summary>直拳蓄力帧（一阶段）</summary>
        public const int PunchWindupP1 = 28;
        /// <summary>直拳蓄力帧（二阶段）</summary>
        public const int PunchWindupP2 = 22;
        /// <summary>直拳速度（一阶段）</summary>
        public const float PunchSpeedP1 = 34f;
        /// <summary>直拳速度（二阶段）</summary>
        public const float PunchSpeedP2 = 41f;
        /// <summary>撞墙反弹速度保留</summary>
        public const float BounceKeep = 0.82f;
        /// <summary>反弹后向目标折射的最大修正角</summary>
        public const float BounceSteer = 0.38f;
        /// <summary>拳离锚点过远强制回收距离</summary>
        public const float PunchLeash = 1500f;
        /// <summary>拳返回速度</summary>
        public const float FistReturnSpeed = 30f;
        /// <summary>拳接触伤害生效速度门槛</summary>
        public const float FistContactSpeed = 16f;
        #endregion

        #region 预警帧（按危险层级取常数）
        /// <summary>射线类预警</summary>
        public const int RayTelegraph = 45;
        /// <summary>陨落/大招标记预警</summary>
        public const int MarkTelegraph = 48;
        /// <summary>机关单元起爆预警</summary>
        public const int TrapTelegraph = 40;
        #endregion

        #region 弹幕基础伤害（专家自动翻倍，勿按最终伤害填）
        public const int SunBoltDamage = 26;
        public const int EyeRayDamage = 30;
        public const int MortarDamage = 27;
        public const int EmberDamage = 22;
        public const int SpikeDamage = 33;
        public const int FlameJetDamage = 30;
        public const int ShockwaveDamage = 30;
        public const int ShrapnelDamage = 24;
        public const int MeteorDamage = 34;
        public const int UltSpokeDamage = 36;
        public const int UltBurstDamage = 40;
        #endregion

        /// <summary>死亡模式伤害/密度增压</summary>
        public static int ScaleDamage(int baseDamage, bool death) {
            return death ? (int)(baseDamage * 1.15f) : baseDamage;
        }

        /// <summary>节奏帧数缩放：死亡模式与激怒压缩间隔</summary>
        public static int Tempo(int frames, bool death, bool enraged) {
            float f = frames;
            if (death) {
                f *= 0.8f;
            }
            if (enraged) {
                f *= EnrageTempo;
            }
            return System.Math.Max((int)f, 6);
        }
    }
}
