using CalamityOverhaul.Content.Industrials.MaterialFlow.Pipelines;
using InnoVault.GameSystem;
using System;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Structures
{
    internal class ModifyStructure : ICWRLoader
    {
        internal delegate void SetChest_Delegate(Chest chest, int type, bool hasPlacedLogAndSchematic);
        void ICWRLoader.LoadData() {
            Type draedonStructures = CWRMod.Instance.calamity?.Code.GetType("CalamityMod.World.DraedonStructures");
            MethodInfo getMethod(string key) => draedonStructures.GetMethod(key, BindingFlags.Public | BindingFlags.Static);
            if (draedonStructures is not null)
                VaultHook.Add(getMethod("FillPlanetoidLaboratoryChest"), OnPlanetoidChest);
        }

        private static void AddChestContent(Chest chest, int type, int stack, string text) {
            foreach (var item in chest.item) {
                if (item.type != ItemID.None) {
                    continue;
                }
                CWRMod.Instance.Logger.Info(text);
                item.SetDefaults(type);
                item.stack = stack;
                break;
            }
        }

        private static void OnPlanetoidChest(SetChest_Delegate orig, Chest chest, int type, bool hasPlacedLogAndSchematic) {
            orig.Invoke(chest, type, hasPlacedLogAndSchematic);
            if (hasPlacedLogAndSchematic) {
                AddChestContent(chest, CWRID.Item_SHPC, 1, "Shoving SHPC into the chest.");
                AddChestContent(chest, ModContent.ItemType<UEPipeline>(), WorldGen.genRand.Next(288, 326), "Shoving Energy Input Pipeline into the chest.");
            }
        }
    }
}
