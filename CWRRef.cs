using CalamityMod;
using CalamityMod.Events;
using CalamityMod.NPCs.ExoMechs.Apollo;
using CalamityMod.NPCs.ExoMechs.Ares;
using CalamityMod.NPCs.ExoMechs.Artemis;
using CalamityMod.NPCs.ExoMechs.Thanatos;
using CalamityMod.Projectiles.Boss;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Tiles.Furniture.CraftingStations;
using CalamityMod.World;
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
        public static void SummonSupCal(Vector2 spawnPos) {
            SoundEngine.PlaySound(SCalAltar.SummonSound, spawnPos);
            Projectile.NewProjectile(new EntitySource_WorldEvent(), spawnPos, Vector2.Zero
                , ModContent.ProjectileType<SCalRitualDrama>(), 0, 0f, Main.myPlayer, 0, 0);
        }
        public static void SummonExo(int exoType, Player player) {
            CalamityWorld.DraedonMechToSummon = (ExoMech)exoType;
            switch (CalamityWorld.DraedonMechToSummon) {
                case ExoMech.Destroyer:
                    Vector2 thanatosSpawnPosition = player.Center + Vector2.UnitY * 2100f;
                    NPC thanatos = CalamityUtils.SpawnBossBetter(thanatosSpawnPosition, ModContent.NPCType<ThanatosHead>());
                    if (thanatos != null)
                        thanatos.velocity = thanatos.SafeDirectionTo(player.Center) * 40f;
                    break;

                case ExoMech.Prime:
                    Vector2 aresSpawnPosition = player.Center - Vector2.UnitY * 1400f;
                    CalamityUtils.SpawnBossBetter(aresSpawnPosition, ModContent.NPCType<AresBody>());
                    break;

                case ExoMech.Twins:
                    Vector2 artemisSpawnPosition = player.Center + new Vector2(-1100f, -1600f);
                    Vector2 apolloSpawnPosition = player.Center + new Vector2(1100f, -1600f);
                    CalamityUtils.SpawnBossBetter(artemisSpawnPosition, ModContent.NPCType<Artemis>());
                    CalamityUtils.SpawnBossBetter(apolloSpawnPosition, ModContent.NPCType<Apollo>());
                    break;
            }
        }
    }
}
