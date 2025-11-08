using CalamityMod;
using CalamityMod.Events;
using CalamityMod.Projectiles.Boss;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Tiles.Furniture.CraftingStations;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul
{
    internal static class CWRRef
    {
        public static bool GetDownedPrimordialWyrm() => DownedBossSystem.downedPrimordialWyrm;
        public static void SetDownedPrimordialWyrm(bool value) => DownedBossSystem.downedPrimordialWyrm = value;
        public static bool GetBossRushActive() => BossRushEvent.BossRushActive;
        public static bool GetAcidRainEventIsOngoing() => AcidRainEvent.AcidRainEventIsOngoing;
        public static DamageClass GetTrueMeleeDamageClass() => ModContent.GetInstance<TrueMeleeDamageClass>();
        public static DamageClass GetTrueMeleeNoSpeedDamageClass() => ModContent.GetInstance<TrueMeleeNoSpeedDamageClass>();
        public static float ChargeRatio(Item item) => item.Calamity().ChargeRatio;
        public static bool BladeArmEnchant(this Player player) => player.Calamity().bladeArmEnchant;
        public static bool AdrenalineMode(this Player player) => player.Calamity().adrenalineModeActive;
        public static int GetRandomProjectileType() {
            return Main.rand.Next(4) switch {
                0 => ModContent.ProjectileType<SwordsplosionBlue>(),
                1 => ModContent.ProjectileType<SwordsplosionGreen>(),
                2 => ModContent.ProjectileType<SwordsplosionPurple>(),
                3 => ModContent.ProjectileType<SwordsplosionRed>(),
                _ => ModContent.ProjectileType<SwordsplosionBlue>(),
            };
        }
        public static void SumSupCal(Vector2 spawnPos) {
            SoundEngine.PlaySound(SCalAltar.SummonSound, spawnPos);
            Projectile.NewProjectile(new EntitySource_WorldEvent(), spawnPos, Vector2.Zero
                , ModContent.ProjectileType<SCalRitualDrama>(), 0, 0f, Main.myPlayer, 0, 0);
        }
    }
}
