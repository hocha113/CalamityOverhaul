namespace CalamityOverhaul.Content.NPCs.CrimsonWitchs.Core
{
    /// <summary>红莲魔女战斗常数表：电报/节拍/阀门数值集中存放，整场调音只改这里</summary>
    internal static class WitchBattleConst
    {
        //====电报常数（帧，按危险层级分档，让玩家形成肌肉记忆）====
        /// <summary>花瓣镖类快弹幕的预告帧数</summary>
        public const int DartTelegraph = 36;
        /// <summary>焰柱类地面喷发的魔法阵预告帧数</summary>
        public const int PillarTelegraph = 90;
        /// <summary>响指抬手到落指的预告帧数</summary>
        public const int SnapRaiseTime = 40;

        //====响指节拍器====
        /// <summary>两次响指之间的间隔帧数（约6.7秒，保底爆点节拍）</summary>
        public const int SnapInterval = 400;
        /// <summary>连锁引爆行波扩散速度（像素/帧），由近及远给出可读的躲避方向</summary>
        public const float SnapWaveSpeed = 18f;

        //====开放地形阀门====
        /// <summary>玩家拉开超过该距离时，用裙摆步重新落位到玩家侧前方</summary>
        public const float RecenterDistance = 1200f;
        /// <summary>远遁阈值：超过该距离并持续 <see cref="LeaveGraceTime"/> 帧则礼貌离场</summary>
        public const float LeaveDistance = 2600f;
        /// <summary>远遁判定需要持续的帧数</summary>
        public const int LeaveGraceTime = 240;

        //====变阶血量阈值====
        /// <summary>二阶段（红莲盛开）血量比例</summary>
        public const float Phase2LifeFactor = 0.7f;
        /// <summary>三阶段（炼狱庭园）血量比例</summary>
        public const float Phase3LifeFactor = 0.4f;
        /// <summary>终幕（终焉狂想曲）血量比例</summary>
        public const float FinaleLifeFactor = 0.1f;
    }
}
