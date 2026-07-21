using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.OtherMods.Entropys
{
    internal static class EntropyCore
    {
        public static bool Has => ModLoader.HasMod("CalamityEntropy");
        /// <summary>是否持有风暴之心</summary>
        public static bool IsHeartOfStorm(Player player) => HeartOfStormPlayer.GetHeartOfStorm(player);
    }
}
