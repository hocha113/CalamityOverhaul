using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.OtherMods.Entropys
{
    internal static class EntropyCore
    {
        public static bool Has => ModLoader.HasMod("CalamityEntropy");
        public static bool IsHeartOfStorm(Player player) {
            if (!Has) {
                return false;
            }
            if (player.Alives()) {
                return false;
            }
            return HeartOfStormRef.IsHeartOfStorm(player);
        }
    }
}
