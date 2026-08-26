using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Fleshfen
{
    /// <summary>
    /// 猩红氛围的逐玩家状态（挂 ModPlayer，禁 static 存逐玩家数据）。
    /// 两个字段都是权威端决策私产，不同步：客户端一切可见结果来自弹幕实体的原生同步
    /// </summary>
    internal class FleshfenPlayer : ModPlayer
    {
        /// <summary>血露冷却（帧）；由 <see cref="FleshfenBloodDew"/> 在权威端推进</summary>
        internal int DewCooldown;

        /// <summary>连续处于猩红之地的帧数（0 = 不在；入界错拍与首触宽限用）</summary>
        internal int InZoneStreak;
    }
}
