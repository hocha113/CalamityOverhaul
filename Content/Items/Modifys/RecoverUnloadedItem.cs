using CalamityOverhaul.Content.Industrials.MaterialFlow.Pipelines;
using CalamityOverhaul.Content.Items.Accessories;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Items.Melee.SpearOfLonginuses;
using CalamityOverhaul.Content.Items.Ranged.NeutronBows;
using InnoVault.GameSystem;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Default;

namespace CalamityOverhaul.Content.Items.Modifys
{
    //补救卸载副本物品，防更新后满背包卸载物
    internal class RecoverUnloadedItem : ICWRLoader
    {
        public static int TargetID { get; private set; }
        /// <summary>
        /// 需要恢复的卸载物品，从字符键对应到目标物品的ID
        /// </summary>
        internal static Dictionary<string, int> RecoverUnloadedItemDic { get; private set; } = [];
        void ICWRLoader.SetupData() {
            foreach (var rItem in ItemOverride.Instances) {
                if (rItem.Mod != CWRMod.Instance) {
                    continue;
                }
                Item ectypeItem = new Item(rItem.TargetID);
                if (ectypeItem.ModItem != null) {
                    string key = "CalamityOverhaul/" + ectypeItem.ModItem.Name + "EcType";
                    RecoverUnloadedItemDic.TryAdd(key, rItem.TargetID);
                }
            }
            RecoverUnloadedItemDic.Add("CalamityOverhaul/BlackMatterStick", ModContent.ItemType<NeutronStarIngot>());
            RecoverUnloadedItemDic.Add("CalamityOverhaul/Gangarus", ModContent.ItemType<SpearOfLonginus>());
            RecoverUnloadedItemDic.Add("CalamityOverhaul/UEPipelineInput", ModContent.ItemType<UEPipeline>());
            RecoverUnloadedItemDic.Add("CalamityOverhaul/NeutronStarMuzzleBrake", ModContent.ItemType<EyeOfSingularity>());
            AddInfiniteSeriesRecovers();
            TargetID = ModContent.ItemType<UnloadedItem>();
        }

        /// <summary>
        /// 无尽系列与超级工作台移除后的替补映射：材料折算为链上仍然存在的等价物，
        /// 灾厄不在场时回落到原版物品，保证旧存档不留卸载占位
        /// </summary>
        private static void AddInfiniteSeriesRecovers() {
            int neutronIngot = ModContent.ItemType<NeutronStarIngot>();
            RecoverUnloadedItemDic.Add("CalamityOverhaul/InfiniteIngot", neutronIngot);
            RecoverUnloadedItemDic.Add("CalamityOverhaul/InfinityCatalyst", neutronIngot);
            RecoverUnloadedItemDic.Add("CalamityOverhaul/DarkMatterBall",
                CWRID.Item_ShadowspecBar > 0 ? CWRID.Item_ShadowspecBar : neutronIngot);
            RecoverUnloadedItemDic.Add("CalamityOverhaul/SpectralMatter",
                CWRID.Item_DarkPlasma > 0 ? CWRID.Item_DarkPlasma : ItemID.LunarBar);
            RecoverUnloadedItemDic.Add("CalamityOverhaul/DecayParticles", ItemID.ChlorophyteBar);
            RecoverUnloadedItemDic.Add("CalamityOverhaul/DecaySubstance", ItemID.LunarBar);
            RecoverUnloadedItemDic.Add("CalamityOverhaul/DissipationSubstance", ItemID.LunarBar);
            RecoverUnloadedItemDic.Add("CalamityOverhaul/HeavenfallLongbow", ModContent.ItemType<NeutronBow>());
            RecoverUnloadedItemDic.Add("CalamityOverhaul/InfinitePick",
                CWRID.Item_CrystylCrusher > 0 ? CWRID.Item_CrystylCrusher : ItemID.SolarFlarePickaxe);
            RecoverUnloadedItemDic.Add("CalamityOverhaul/InfiniteToiletItem", ItemID.GoldenToilet);
            RecoverUnloadedItemDic.Add("CalamityOverhaul/DarkMatterCompressorItem", ItemID.LunarCraftingStation);
            RecoverUnloadedItemDic.Add("CalamityOverhaul/TransmutationOfMatterItem",
                CWRID.Item_DraedonsForge > 0 ? CWRID.Item_DraedonsForge : ItemID.LunarCraftingStation);
        }

        void ICWRLoader.UnLoadData() => RecoverUnloadedItemDic?.Clear();
        public static void UpdateInventory(Item item) {
            if (item.type != TargetID) {
                return;
            }
            UnloadedItem unloadedItem = item.ModItem as UnloadedItem;
            string key = unloadedItem.ModName + "/" + unloadedItem.ItemName;
            int origStack = item.stack;
            if (RecoverUnloadedItemDic.TryGetValue(key, out int targetItemID)) {
                item.ChangeItemType(targetItemID);
                item.stack = origStack;
            }
        }
    }
}
