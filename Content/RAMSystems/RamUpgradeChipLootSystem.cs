using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.RAMSystems
{
    internal class RamUpgradeChipLootSystem : ModSystem
    {
        private const string SaveKeyInjected = "RamUpgradeChipsInjected";
        private static bool injected;

        public override void OnWorldUnload() => injected = false;

        public override void LoadWorldData(TagCompound tag) {
            injected = tag != null && tag.TryGet(SaveKeyInjected, out bool value) && value;
        }

        public override void SaveWorldData(TagCompound tag) {
            tag[SaveKeyInjected] = injected;
        }

        public override void PostWorldGen() {
            InjectUpgradeChips();
        }

        public override void PostUpdateWorld() {
            if (injected || Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }

            InjectUpgradeChips();
        }

        private static void InjectUpgradeChips() {
            int capacityChipType = ModContent.ItemType<RamCapacityUpgradeChip>();
            int recoveryChipType = ModContent.ItemType<RamRecoveryUpgradeChip>();

            for (int i = 0; i < Main.maxChests; i++) {
                Chest chest = Main.chest[i];
                if (chest == null || !IsLaboratorySecurityChest(chest)) {
                    continue;
                }

                bool addedCapacityChip = WorldGen.genRand.NextBool(2);
                bool addedRecoveryChip = WorldGen.genRand.NextBool(2);

                if (!addedCapacityChip && !addedRecoveryChip) {
                    addedCapacityChip = WorldGen.genRand.NextBool();
                    addedRecoveryChip = !addedCapacityChip;
                }

                if (addedCapacityChip) {
                    AddChestItem(chest, capacityChipType);
                }
                if (addedRecoveryChip) {
                    AddChestItem(chest, recoveryChipType);
                }
            }

            injected = true;
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
