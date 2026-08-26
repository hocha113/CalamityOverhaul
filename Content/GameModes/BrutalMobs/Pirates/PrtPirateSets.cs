using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Pirates
{
    /// <summary>
    /// 海盗船员类型口径：跳帮号令的提速圈、旗手遴选与船员机制分派共用同一张名单，
    /// 避免三处各写一份类型表日后改岔
    /// </summary>
    internal static class PrtPirateSets
    {
        /// <summary>地面船员（不含鹦鹉与荷兰飞船本体、飞船炮部件）</summary>
        internal static bool IsGroundCrew(int type) => type is NPCID.PirateDeckhand or NPCID.PirateCorsair
            or NPCID.PirateDeadeye or NPCID.PirateCrossbower or NPCID.PirateCaptain;
    }
}
