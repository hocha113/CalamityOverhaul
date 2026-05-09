using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules
{
    internal class SHPCModuleLootSystem : ModSystem
    {
        private const string SaveKeyInjected = "SHPCModulesInjected";
        private static bool injected;

        public override void OnWorldLoad() => injected = false;

        public override void LoadWorldData(TagCompound tag) {
            injected = tag != null && tag.TryGet(SaveKeyInjected, out bool value) && value;
        }

        public override void SaveWorldData(TagCompound tag) {
            tag[SaveKeyInjected] = injected;
        }

        public override void PostWorldGen() {
            InjectModules();
        }

        public override void PostUpdateWorld() {
            if (injected || Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }

            InjectModules();
        }

        private static void InjectModules() {
            //收集所有改件类型，方便后续按索引随机抽取
            int[] moduleTypes = [
                ModContent.ItemType<RapidBarrelModule>(),
                ModContent.ItemType<FocusBarrelModule>(),
                ModContent.ItemType<ScattershotBarrelModule>(),
                ModContent.ItemType<HypersonicBarrelModule>(),
                ModContent.ItemType<HeavyBarrelModule>(),
                ModContent.ItemType<OscillatorBarrelModule>(),
                ModContent.ItemType<ScorchBarrelModule>(),
                ModContent.ItemType<ReflectionBarrelModule>(),
                ModContent.ItemType<GraviticBarrelModule>(),
                ModContent.ItemType<PrismaticBarrelModule>(),
                ModContent.ItemType<ConcussionBarrelModule>(),
                ModContent.ItemType<ObsidianBarrelModule>(),
                ModContent.ItemType<LifebloomBarrelModule>(),
                ModContent.ItemType<MagmaVentBarrelModule>(),
                ModContent.ItemType<CumulusBarrelModule>(),
                ModContent.ItemType<MossboundBarrelModule>(),
                ModContent.ItemType<FrostfernBarrelModule>(),
                ModContent.ItemType<SandstormBarrelModule>(),
                ModContent.ItemType<CoralReefBarrelModule>(),
                ModContent.ItemType<HiveBarrelModule>(),
                ModContent.ItemType<MoondewBarrelModule>(),
                ModContent.ItemType<PrecisionOpticModule>(),
                ModContent.ItemType<AdaptiveOpticModule>(),
                ModContent.ItemType<ThermalOpticModule>(),
                ModContent.ItemType<HoloOpticModule>(),
                ModContent.ItemType<EchoOpticModule>(),
                ModContent.ItemType<FrostOpticModule>(),
                ModContent.ItemType<CrosslinkOpticModule>(),
                ModContent.ItemType<ZoomOpticModule>(),
                ModContent.ItemType<PingOpticModule>(),
                ModContent.ItemType<OverloadCoreModule>(),
                ModContent.ItemType<HighVoltageCoreModule>(),
                ModContent.ItemType<CapacitorBankModule>(),
                ModContent.ItemType<PlasmaInjectorModule>(),
                ModContent.ItemType<SingularityCoreModule>(),
                ModContent.ItemType<ScrambleFieldModule>(),
                ModContent.ItemType<CapacitorPulseModule>(),
                ModContent.ItemType<EntropyCoreModule>(),
                ModContent.ItemType<ResonanceReactorModule>(),
                ModContent.ItemType<SteadyStockModule>(),
                ModContent.ItemType<KineticDamperModule>(),
                ModContent.ItemType<LightStockModule>(),
                ModContent.ItemType<AssaultStockModule>(),
                ModContent.ItemType<BraceStockModule>(),
                ModContent.ItemType<LaunchStockModule>(),
                ModContent.ItemType<OverwatchStockModule>(),
                ModContent.ItemType<MomentumStockModule>(),
                ModContent.ItemType<BulwarkStockModule>(),
                ModContent.ItemType<HarmonyGripModule>(),
                ModContent.ItemType<EfficientGripModule>(),
                ModContent.ItemType<CrystalGripModule>(),
                ModContent.ItemType<BalancedGripModule>(),
                ModContent.ItemType<SentinelGripModule>(),
                ModContent.ItemType<ConductorGripModule>(),
                ModContent.ItemType<CombatGripModule>(),
                ModContent.ItemType<TempestGripModule>(),
                ModContent.ItemType<AbsorberGripModule>(),
                ModContent.ItemType<ResonanceFrameModule>(),
                ModContent.ItemType<MultiCellFrameModule>(),
                ModContent.ItemType<QuantumFrameModule>(),
                ModContent.ItemType<VolatileFrameModule>(),
                ModContent.ItemType<PhantomFrameModule>(),
                ModContent.ItemType<RecursiveFrameModule>(),
                ModContent.ItemType<HarmonicFrameModule>(),
                ModContent.ItemType<ReplicatorFrameModule>(),
                ModContent.ItemType<ArchiveFrameModule>(),
            ];

            List<Chest> laboratoryChests = [];
            for (int i = 0; i < Main.maxChests; i++) {
                Chest chest = Main.chest[i];
                if (chest == null || !IsLaboratorySecurityChest(chest)) {
                    continue;
                }

                laboratoryChests.Add(chest);
            }

            Shuffle(laboratoryChests);

            List<int> moduleBag = [];
            int previousModuleType = ItemID.None;
            foreach (Chest chest in laboratoryChests) {
                if (moduleBag.Count == 0) {
                    RefillModuleBag(moduleBag, moduleTypes, previousModuleType);
                }

                int moduleIndex = moduleBag.Count - 1;
                int moduleType = moduleBag[moduleIndex];
                moduleBag.RemoveAt(moduleIndex);
                previousModuleType = moduleType;

                AddChestItem(chest, moduleType);
            }

            injected = true;
        }

        private static void RefillModuleBag(List<int> moduleBag, int[] moduleTypes, int previousModuleType) {
            moduleBag.AddRange(moduleTypes);
            Shuffle(moduleBag);

            if (moduleBag.Count > 1 && moduleBag[^1] == previousModuleType) {
                int swapIndex = WorldGen.genRand.Next(moduleBag.Count - 1);
                (moduleBag[^1], moduleBag[swapIndex]) = (moduleBag[swapIndex], moduleBag[^1]);
            }
        }

        private static void Shuffle<T>(List<T> list) {
            for (int i = list.Count - 1; i > 0; i--) {
                int swapIndex = WorldGen.genRand.Next(i + 1);
                (list[i], list[swapIndex]) = (list[swapIndex], list[i]);
            }
        }

        private static bool IsLaboratorySecurityChest(Chest chest) {
            if (chest.x < 0 || chest.x >= Main.maxTilesX || chest.y < 0 || chest.y >= Main.maxTilesY) {
                return false;
            }

            Tile tile = Main.tile[chest.x, chest.y];
            return tile.HasTile
                && (tile.TileType == CWRID.Tile_SecurityChestTile
                    || tile.TileType == CWRID.Tile_AgedSecurityChestTile);
        }

        private static void AddChestItem(Chest chest, int itemType) {
            for (int i = 0; i < chest.item.Length; i++) {
                Item item = chest.item[i];
                if (item.type != ItemID.None) {
                    continue;
                }

                item.SetDefaults(itemType);
                item.stack = 1;
                return;
            }
        }
    }
}
