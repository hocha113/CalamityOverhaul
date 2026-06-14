using System.Collections.Generic;
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

        public override void SetStaticDefaults() {
            IndustrializationGenMessage = this.GetLocalization(nameof(IndustrializationGenMessage), () => "正在让世界工业化");
        }

        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight) {
            int finalIndex = tasks.FindIndex(genpass => genpass.Name.Equals("Final Cleanup"));
            if (finalIndex == -1) {
                return;
            }

            tasks.Insert(finalIndex + 1, new PassLegacy("Industrialization", IndustrializationGen.ApplyPass));
        }
    }
}
