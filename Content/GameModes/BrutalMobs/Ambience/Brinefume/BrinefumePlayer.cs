using System;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Brinefume
{
    /// <summary>
    /// 硫磺海氛围的逐玩家状态（禁 static 存逐玩家数据的落点）。
    /// 曝露量是本机结算量（各端只写自己的，减益走本机 AddBuff 原生同步），
    /// 调度冷却是权威端决策私产（服务端实例上使用）；都不进存档不走网络
    /// </summary>
    internal class BrinefumePlayer : ModPlayer
    {
        /// <summary>「毒霾潮」滞留累积量（滞留渐涨、离开缓消，决定中毒时长）</summary>
        internal int HazeExposure;

        /// <summary>本帧仍浸在毒霾里（霾潮实体在弹幕更新阶段盖章，比玩家更新晚一拍，无碍）</summary>
        internal bool HazeSoaked;

        /// <summary>「酸沸区」调度冷却（权威端私产）</summary>
        internal int BoilCooldown;

        /// <summary>「毒霾潮」调度冷却（权威端私产）</summary>
        internal int HazeCooldown;

        public override void Initialize() {
            HazeExposure = 0;
            HazeSoaked = false;
            //入场宽限：进档后先安静一阵再开始调度
            BoilCooldown = 480;
            HazeCooldown = 1800;
        }

        public override void PostUpdateMiscEffects() {
            //离开毒霾后累积量缓慢消散；旗标由上一帧霾潮 AI 写入，读后即清
            if (!HazeSoaked && HazeExposure > 0) {
                HazeExposure = Math.Max(0, HazeExposure - 3);
            }
            HazeSoaked = false;
        }

        public override void UpdateDead() {
            HazeExposure = 0;
            HazeSoaked = false;
        }
    }
}
