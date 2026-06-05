using InnoVault.GameSystem;
using System.Collections.Generic;
using System.Reflection;
using Terraria.GameContent.Generation;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Structures
{
    internal class WorldGenSystem : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "Structures";

        public static LocalizedText IndustrializationGenMessage { get; private set; }

        public delegate void ModifyWorldGenTasksHook(object obj, List<GenPass> tasks, ref double totalWeight);

        public static void On_ModifyWorldGenTasks_Hook(ModifyWorldGenTasksHook orig, object obj, List<GenPass> tasks, ref double totalWeight) {
            orig.Invoke(obj, tasks, ref totalWeight);
            int FinalIndex = tasks.FindIndex(genpass => genpass.Name.Equals("Final Cleanup"));
            if (FinalIndex != -1) {
                int currentFinalIndex = FinalIndex;
                tasks.Insert(++currentFinalIndex, new PassLegacy("Industrialization", IndustrializationGen.ApplyPass));
            }
        }

        public override void SetStaticDefaults() {
            IndustrializationGenMessage = this.GetLocalization(nameof(IndustrializationGenMessage), () => "正在让世界工业化");
        }

        public override void Load() {
            var type = CWRMod.Instance.calamity?.Code.GetType("CalamityMod.Systems.WorldgenManagementSystem");
            if (type is null) {
                return;
            }
            MethodInfo methodInfo = type.GetMethod("ModifyWorldGenTasks", BindingFlags.Instance | BindingFlags.Public);
            if (methodInfo is null) {
                return;
            }
            VaultHook.Add(methodInfo, On_ModifyWorldGenTasks_Hook);
        }
    }
}
