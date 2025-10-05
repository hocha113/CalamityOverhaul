using Terraria.ModLoader;

namespace CalamityOverhaul.Common
{
    internal class CWRKeySystem : ICWRLoader
    {
        public static ModKeybind HeavenfallLongbowSkillKey { get; private set; }
        public static ModKeybind InfinitePickSkillKey { get; private set; }
        public static ModKeybind TOM_GatheringItem { get; private set; }
        public static ModKeybind Murasama_TriggerKey { get; private set; }
        public static ModKeybind Murasama_DownKey { get; private set; }
        public static ModKeybind KreLoad_Key { get; private set; }
        public static ModKeybind ADS_Key { get; private set; }
        public static ModKeybind Halibut_Domain { get; private set; }

        void ICWRLoader.LoadData() {
            Mod mod = CWRMod.Instance;
            HeavenfallLongbowSkillKey = KeybindLoader.RegisterKeybind(mod, "HeavenfallLongbowSkillKey", "Q");
            InfinitePickSkillKey = KeybindLoader.RegisterKeybind(mod, "InfinitePickSkillKey", "C");
            TOM_GatheringItem = KeybindLoader.RegisterKeybind(mod, "TOM_GatheringItem", "G");
            Murasama_TriggerKey = KeybindLoader.RegisterKeybind(mod, "Murasama_TriggerKey", "F");
            Murasama_DownKey = KeybindLoader.RegisterKeybind(mod, "Murasama_DownKey", "X");
            KreLoad_Key = KeybindLoader.RegisterKeybind(mod, "KreLoad_Key", "R");
            ADS_Key = KeybindLoader.RegisterKeybind(mod, "ADS_Key", "Z");
            Halibut_Domain = KeybindLoader.RegisterKeybind(mod, "Halibut_Domain", "Q");
        }

        void ICWRLoader.UnLoadData() {
            HeavenfallLongbowSkillKey = null;
            InfinitePickSkillKey = null;
            TOM_GatheringItem = null;
            Murasama_TriggerKey = null;
            Murasama_DownKey = null;
            KreLoad_Key = null;
            ADS_Key = null;
            Halibut_Domain = null;
        }
    }
}
