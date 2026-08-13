namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Core
{
    /// <summary>战斗调参中心，推进死线的节拍表</summary>
    internal static class WofDirector
    {
        #region 推进
        /// <summary>基础推进速度 px/f</summary>
        public const float BaseAdvanceSpeed = 2.3f;
        /// <summary>随失血追加的推进速度上限</summary>
        public const float LifeAdvanceBonus = 4.6f;
        /// <summary>目标远离时每像素加速</summary>
        public const float CatchUpPerPixel = 0.0012f;
        /// <summary>开始追赶的领先距离</summary>
        public const float CatchUpDistance = 980f;
        /// <summary>目标贴脸时的减速下限系数</summary>
        public const float CloseEaseFloor = 0.42f;
        /// <summary>贴脸减速起算距离</summary>
        public const float CloseEaseDistance = 620f;
        /// <summary>脱屏激怒判定距离</summary>
        public const float FarEnrageDistance = 1500f;
        /// <summary>脱屏激怒累计帧</summary>
        public const int FarEnrageFrames = 540;
        /// <summary>脱屏激怒速度倍率</summary>
        public const float FarEnrageMultiplier = 2.7f;
        #endregion

        #region 突进
        /// <summary>突进预告帧(压缩蓄势)</summary>
        public const int SurgeTelegraph = 52;
        /// <summary>突进冲刺帧</summary>
        public const int SurgeDashFrames = 26;
        /// <summary>突进速度 px/f</summary>
        public const float SurgeSpeed = 27f;
        /// <summary>突进刹车帧</summary>
        public const int SurgeBrakeFrames = 22;
        /// <summary>突进期墙面带状追加伤害(普通模式基准)</summary>
        public const int SurgeBandDamage = 26;
        #endregion

        #region 漩涡
        public const int VortexWindup = 46;
        public const int VortexDuration = 250;
        /// <summary>吸引作用距离</summary>
        public const float VortexRange = 1500f;
        /// <summary>最大吸引加速度 px/f^2</summary>
        public const float VortexPullMax = 0.42f;
        #endregion

        #region 扫描协议
        public const int ScanCharge = 66;
        public const int ScanDuration = 240;
        /// <summary>扫描半弧(弧度)</summary>
        public const float ScanArcHalf = 0.44f;
        #endregion

        #region 饥饿网
        public const int NetWeaveFrames = 80;
        public const int NetHoldFrames = 300;
        /// <summary>网面距墙面距离</summary>
        public const float NetForwardOffset = 540f;
        /// <summary>网链伤害(基准)</summary>
        public const int NetLinkDamage = 14;
        /// <summary>成网最少饥饿者数</summary>
        public const int NetMinHungries = 3;
        #endregion

        #region 水蛭浪
        public const int RetchWindup = 34;
        /// <summary>单场水蛭上限(含原版余量)</summary>
        public const int LeechCap = 10;
        #endregion

        #region 血肉尖刺
        /// <summary>尖刺波数</summary>
        public const int SpikeWaveCount = 5;
        /// <summary>波间隔帧</summary>
        public const int SpikeWaveInterval = 26;
        /// <summary>波内列间距 px</summary>
        public const float SpikeColumnSpacing = 190f;
        public const int SpikeDamage = 22;
        #endregion

        #region 舌鞭
        public const int TongueTelegraph = 38;
        public const int TongueDamage = 18;
        #endregion

        #region 大迁徙
        public const int ExodusWindup = 96;
        public const int ExodusDuration = 560;
        /// <summary>血幕初始落后距离</summary>
        public const float CurtainStartGap = 1650f;
        /// <summary>血幕最小口袋宽(公平阀)</summary>
        public const float CurtainMinGap = 720f;
        /// <summary>血幕相对墙的追近速度 px/f</summary>
        public const float CurtainCloseRate = 1.9f;
        /// <summary>血幕触碰伤害(基准)</summary>
        public const int CurtainDamage = 30;
        /// <summary>大迁徙后的喘息窗口帧</summary>
        public const int ExodusRestFrames = 130;
        #endregion

        #region 通用
        /// <summary>触发死亡演出的生命阈值</summary>
        public const int DeathTriggerLife = 10;
        /// <summary>转阶段生命比例</summary>
        public const float Phase2LifeRatio = 0.66f;
        /// <summary>低血大招生命比例</summary>
        public const float ExodusLifeRatio = 0.33f;
        /// <summary>眼部平射预告帧</summary>
        public const int EyePotshotTelegraph = 42;
        /// <summary>眼部平射基准伤害</summary>
        public const int EyeLaserDamage = 12;
        /// <summary>扫描光束基准伤害</summary>
        public const int ScanBeamDamage = 16;
        /// <summary>血凝块基准伤害</summary>
        public const int BloodClotDamage = 14;
        #endregion

        /// <summary>阶段间隔帧：两招之间的推进喘息</summary>
        public static int AdvanceGapFrames(int phase, bool death) {
            int baseGap = phase switch {
                1 => 150,
                2 => 112,
                _ => 84,
            };
            if (death) {
                baseGap = (int)(baseGap * 0.8f);
            }
            return baseGap;
        }
    }
}
