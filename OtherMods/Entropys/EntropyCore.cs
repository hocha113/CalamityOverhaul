using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.OtherMods.Entropys
{
    internal static class EntropyCore
    {
        public static bool Has => ModLoader.HasMod("CalamityEntropy");
        /// <summary>
        /// 玩家是否拥有风暴之心
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public static bool IsHeartOfStorm(Player player) => HeartOfStormPlayer.GetHeartOfStorm(player);
    }
}
