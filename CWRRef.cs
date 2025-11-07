using CalamityMod;
using CalamityMod.Events;
using Terraria;

namespace CalamityOverhaul
{
    internal static class CWRRef
    {
        public static bool GetDownedPrimordialWyrm() => DownedBossSystem.downedPrimordialWyrm;
        public static void SetDownedPrimordialWyrm(bool value) => DownedBossSystem.downedPrimordialWyrm = value;
        public static bool GetBossRushActive() => BossRushEvent.BossRushActive;
        public static float ChargeRatio(Item item) {
            return item.Calamity().ChargeRatio;
        }
    }
}
