using InnoVault.GameSystem;
using System.Collections.Generic;
using System.Reflection;
using Terraria.GameContent.Generation;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Structures
{
    internal class WorldGenSystem : ICWRLoader
    {
        private MethodInfo WorldgenManagementSystem_ModifyWorldGenTasks;
        public delegate void ModifyWorldGenTasksHook(object obj, List<GenPass> tasks, ref double totalWeight);
        public static void ModifyWorldGenTasks(ModifyWorldGenTasksHook orig, object obj, List<GenPass> tasks, ref double totalWeight) {
            orig.Invoke(obj, tasks, ref totalWeight);
            int FinalIndex = tasks.FindIndex(genpass => genpass.Name.Equals("Final Cleanup"));
            if (FinalIndex != -1) {
                int currentFinalIndex = FinalIndex;
                tasks.Insert(++currentFinalIndex, new PassLegacy("Industrialization", IndustrializationGen.ApplyPass));
            }
        }

        void ICWRLoader.LoadData() {
            WorldgenManagementSystem_ModifyWorldGenTasks = CWRMod.Instance.calamity?.Code.GetType("CalamityMod.Systems.WorldgenManagementSystem")
                .GetMethod("ModifyWorldGenTasks", BindingFlags.Instance | BindingFlags.Public);
            if (WorldgenManagementSystem_ModifyWorldGenTasks is not null)
                VaultHook.Add(WorldgenManagementSystem_ModifyWorldGenTasks, ModifyWorldGenTasks);
        }

        void ICWRLoader.UnLoadData() {
            WorldgenManagementSystem_ModifyWorldGenTasks = null;
        }
    }
}
