using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.MoldProcessingTables
{
    /// <summary>
    /// 模具加工台的纯逻辑层：分解 / 随机重铸 / 固定重铸 / 类别枚举
    /// 所有经济参数集中在此，方便后期调平衡
    /// </summary>
    internal static class MoldRecipeSystem
    {
        /// <summary>分解 1 个模块获得的碎片数</summary>
        public const int DecomposeGain = 3;
        /// <summary>随机重铸消耗的碎片数</summary>
        public const int RandomCost = 4;
        /// <summary>固定重铸（按图鉴钉选）消耗的碎片数，比随机贵 50%</summary>
        public const int PinnedCost = 6;

        /// <summary>
        /// 枚举某类别的所有模块物品 type
        /// </summary>
        /// <param name="labPoolOnly">true 时仅返回 <see cref="SHPCModuleItem.CanGenerateInLabChest"/> 为 true 的模块（用于随机池）</param>
        public static IEnumerable<int> EnumerateCategory(SHPCSlotCategory cat, bool labPoolOnly) {
            return ModContent.GetContent<SHPCModuleItem>()
                .Where(m => m.SlotCategory == cat && (!labPoolOnly || m.CanGenerateInLabChest))
                .Select(m => m.Type)
                .OrderBy(t => t);
        }

        /// <summary>
        /// 获取某类别的"完整池"（包含隐藏模块），用于图鉴展示
        /// </summary>
        public static IEnumerable<int> EnumerateCategoryAll(SHPCSlotCategory cat)
            => EnumerateCategory(cat, labPoolOnly: false);

        /// <summary>
        /// 把玩家背包指定槽位上的模块分解掉。会在 inventory 中递减 1 stack。
        /// </summary>
        /// <param name="player">操作的玩家</param>
        /// <param name="inventorySlot">inventory 数组下标</param>
        /// <param name="gained">本次产出的碎片数量</param>
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

            //分解视为"摸过"，登记图鉴
            sp.RegisterDiscovered(it.type);

            it.stack--;
            if (it.stack <= 0) {
                it.TurnToAir();
            }
            return true;
        }

        /// <summary>
        /// 随机重铸：未发现优先，否则在完整 lab 池中均匀抽取
        /// </summary>
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

        /// <summary>
        /// 固定重铸：按 <see cref="SHPCPlayer.PinnedReforgeTarget"/> 指向的 ItemType 产出
        /// </summary>
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
            //再次校验目标合法性（防止 mod 卸载后残留 type）
            if (!ContentSamples.ItemsByType.TryGetValue(target, out Item sample)
                || sample.ModItem is not SHPCModuleItem mod
                || mod.SlotCategory != cat) {
                //目标失效，回退到随机模式
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

        /// <summary>
        /// 统一的"产出物品"：把模块塞进玩家背包，同步图鉴
        /// </summary>
        private static void GrantModule(Player player, int type) {
            if (type <= 0) {
                return;
            }
            SHPCPlayer.Get(player)?.RegisterDiscovered(type);
            Item newItem = new();
            newItem.SetDefaults(type);
            newItem.stack = 1;
            //模具均为 maxStack=1，QuickSpawnItem 已含"先尝试入背包，满则掉落"的逻辑
            player.QuickSpawnItem(player.GetSource_Misc("SHPCModuleReforge"), newItem, 1);
        }
    }
}
