using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.MoldProcessingTables
{
    /// <summary>模具加工台逻辑，分解/重铸/类别枚举，经济参数集中</summary>
    internal static class MoldRecipeSystem
    {
        /// <summary>分解 1 个模块获得的碎片数</summary>
        public const int DecomposeGain = 3;
        /// <summary>随机重铸消耗的碎片数</summary>
        public const int RandomCost = 4;
        /// <summary>固定重铸碎片消耗，比随机贵50%</summary>
        public const int PinnedCost = 6;

        /// <param name="labPoolOnly">true=仅实验室随机池</param>
        public static IEnumerable<int> EnumerateCategory(SHPCSlotCategory cat, bool labPoolOnly) {
            return ModContent.GetContent<SHPCModuleItem>()
                .Where(m => m.SlotCategory == cat && (!labPoolOnly || m.CanGenerateInLabChest))
                .Select(m => m.Type)
                .OrderBy(t => t);
        }

        /// <summary>完整池含隐藏，图鉴用</summary>
        public static IEnumerable<int> EnumerateCategoryAll(SHPCSlotCategory cat)
            => EnumerateCategory(cat, labPoolOnly: false);

        /// <summary>分解背包槽位模块，stack-1</summary>
        /// <param name="gained">产出碎片数</param>
        public static bool TryDecompose(Player player, int inventorySlot, out int gained) {
            gained = 0;
            if (player == null) {
                return false;
            }
            if (inventorySlot < 0 || inventorySlot >= player.inventory.Length) {
                return false;
            }
            Item it = player.inventory[inventorySlot];
            if (it == null || it.IsAir || it.ModItem is not SHPCModuleItem mod) {
                return false;
            }
            SHPCPlayer sp = SHPCPlayer.Get(player);
            if (sp?.MoldShards == null) {
                return false;
            }
            sp.MoldShards[(int)mod.SlotCategory] += DecomposeGain;
            gained = DecomposeGain;

            //登记图鉴
            sp.RegisterDiscovered(it.type);

            it.stack--;
            if (it.stack <= 0) {
                it.TurnToAir();
            }
            return true;
        }

        /// <summary>随机重铸，未发现优先</summary>
        public static bool TryReforgeRandom(Player player, SHPCSlotCategory cat, out int producedType) {
            producedType = 0;
            if (player == null) {
                return false;
            }
            SHPCPlayer sp = SHPCPlayer.Get(player);
            if (sp?.MoldShards == null) {
                return false;
            }
            int idx = (int)cat;
            if (idx < 0 || idx >= SHPCData.SlotCount) {
                return false;
            }
            if (sp.MoldShards[idx] < RandomCost) {
                return false;
            }

            List<int> allOfCat = EnumerateCategory(cat, labPoolOnly: true).ToList();
            if (allOfCat.Count == 0) {
                return false;
            }
            sp.DiscoveredModules ??= new HashSet<int>();
            List<int> undiscovered = allOfCat.Where(t => !sp.DiscoveredModules.Contains(t)).ToList();
            List<int> pool = undiscovered.Count > 0 ? undiscovered : allOfCat;
            producedType = pool[Main.rand.Next(pool.Count)];

            sp.MoldShards[idx] -= RandomCost;
            GrantModule(player, producedType);
            return true;
        }

        /// <summary>固定重铸，按钉选 ItemType 产出</summary>
        public static bool TryReforgePinned(Player player, SHPCSlotCategory cat, out int producedType) {
            producedType = 0;
            if (player == null) {
                return false;
            }
            SHPCPlayer sp = SHPCPlayer.Get(player);
            if (sp?.MoldShards == null || sp.PinnedReforgeTarget == null) {
                return false;
            }
            int idx = (int)cat;
            if (idx < 0 || idx >= SHPCData.SlotCount) {
                return false;
            }
            int target = sp.PinnedReforgeTarget[idx];
            if (target <= 0) {
                return false;
            }
            //校验目标，防卸载残留type
            if (!ContentSamples.ItemsByType.TryGetValue(target, out Item sample)
                || sample.ModItem is not SHPCModuleItem mod
                || mod.SlotCategory != cat) {
                //目标失效回退随机
                sp.PinnedReforgeTarget[idx] = -1;
                return false;
            }
            if (sp.MoldShards[idx] < PinnedCost) {
                return false;
            }

            sp.MoldShards[idx] -= PinnedCost;
            producedType = target;
            GrantModule(player, producedType);
            return true;
        }

        /// <summary>产出模块入背包并登记图鉴</summary>
        private static void GrantModule(Player player, int type) {
            if (player == null || type <= 0) {
                return;
            }
            //再校验type合法
            if (!ContentSamples.ItemsByType.ContainsKey(type)) {
                return;
            }
            SHPCPlayer.Get(player)?.RegisterDiscovered(type);
            player.GiveItem(player.GetSource_Misc("SHPCModuleReforge"), type);
        }
    }
}
