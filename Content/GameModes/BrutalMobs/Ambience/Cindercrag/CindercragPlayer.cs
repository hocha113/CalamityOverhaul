using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Cindercrag
{
    /// <summary>
    /// 硫火之崖环境机制的逐玩家调度状态。
    /// 冷却是权威端决策私产：不入存档、不走同步、客户端不得用它驱动画面；
    /// 逐玩家状态挂 ModPlayer，禁止用 static 承载
    /// </summary>
    internal class CindercragPlayer : ModPlayer
    {
        /// <summary>「崖口喷焰」冷却（权威端递减）</summary>
        internal int VentCooldown;

        /// <summary>「恸嚎波」冷却（权威端递减）</summary>
        internal int WailCooldown;

        /// <summary>上一帧是否在崖内（进出沿检测，入崖补热身，不让人一进门就挨打）</summary>
        internal bool WasInCrag;
    }
}
