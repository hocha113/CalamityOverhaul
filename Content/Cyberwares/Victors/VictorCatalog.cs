using CalamityOverhaul.Content.Cyberwares.Implementation.CstmVisualEyes;
using CalamityOverhaul.Content.Cyberwares.Implementation.MimicPerchedAuxBrains;
using CalamityOverhaul.Content.Cyberwares.Implementation.OmniElectricFoots;
using CalamityOverhaul.Content.Cyberwares.Implementation.PlowSteelClampArms;
using CalamityOverhaul.Content.Cyberwares.Implementation.PrimePlasamas;
using CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans;
using CalamityOverhaul.Content.Cyberwares.Implementation.SCCA32CRPs;
using CalamityOverhaul.Content.Cyberwares.Implementation.SelfHackCrystals;
using CalamityOverhaul.Content.Cyberwares.Implementation.SelfHealingSkelents;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Victors
{
    internal readonly record struct VictorCatalogEntry(
        int ItemType,
        int SlotIndex,
        long Price);

    internal static class VictorCatalog
    {
        private const int PriceMultiplier = 3;
        private const long MaxPrice = 100_000_000L;

        private static readonly int[] EmptyTypes = [];
        private static Dictionary<int, VictorCatalogEntry> entriesByType;
        private static Dictionary<int, List<int>> typesBySlot;

        internal static IReadOnlyList<int> GetTypesForSlot(int slotIndex) {
            EnsureLoaded();
            return slotIndex >= 0 && slotIndex < CyberwarePlayer.SlotCount
                && typesBySlot.TryGetValue(slotIndex, out List<int> types)
                ? types
                : EmptyTypes;
        }

        internal static bool TryGetEntry(int itemType,
            out VictorCatalogEntry entry) {
            entry = default;
            EnsureLoaded();
            return itemType > 0 && itemType < ItemLoader.ItemCount
                && entriesByType.TryGetValue(itemType, out entry);
        }

        internal static long GetPrice(int itemType)
            => TryGetEntry(itemType, out VictorCatalogEntry entry)
                ? entry.Price
                : 0L;

        internal static void Reset() {
            entriesByType = null;
            typesBySlot = null;
        }

        private static void EnsureLoaded() {
            if (entriesByType != null) {
                return;
            }

            int[] catalogTypes = [
                ModContent.ItemType<MimicPerchedAuxBrain>(),
                ModContent.ItemType<CstmVisualEye>(),
                ModContent.ItemType<SCCA32CRP>(),
                ModContent.ItemType<PlowSteelClampArm>(),
                ModContent.ItemType<OmniElectricFoot>(),
                ModContent.ItemType<SelfHackCrystal>(),
                ModContent.ItemType<SandevistansItem>(),
                ModContent.ItemType<PrimePlasama>(),
                ModContent.ItemType<SelfHealingSkelent>(),
            ];

            entriesByType = [];
            typesBySlot = [];
            foreach (int type in catalogTypes) {
                if (type <= 0 || type >= ItemLoader.ItemCount
                    || !ContentSamples.ItemsByType.TryGetValue(type, out Item sample)
                    || sample?.ModItem is not BaseCyberware cyberware) {
                    continue;
                }

                int slotIndex = (int)cyberware.SlotCategory;
                long baseValue = sample.value > 0
                    ? sample.value
                    : Item.buyPrice(gold: 5);
                long price = baseValue * PriceMultiplier;
                if (slotIndex < 0 || slotIndex >= CyberwarePlayer.SlotCount
                    || price <= 0L || price > MaxPrice) {
                    continue;
                }

                VictorCatalogEntry entry = new(type, slotIndex, price);
                entriesByType[type] = entry;
                if (!typesBySlot.TryGetValue(slotIndex, out List<int> slotTypes)) {
                    slotTypes = [];
                    typesBySlot[slotIndex] = slotTypes;
                }
                slotTypes.Add(type);
            }
        }
    }
}
