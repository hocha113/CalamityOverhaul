using Terraria.ModLoader;

namespace CalamityOverhaul.Common
{
    internal class CWRKeySystem : ICWRLoader
    {
        public static ModKeybind HeavenfallLongbowSkillKey { get; private set; }
        public static ModKeybind InfinitePickSkillKey { get; private set; }
        public static ModKeybind Murasama_TriggerKey { get; private set; }
        public static ModKeybind Murasama_DownKey { get; private set; }
        public static ModKeybind KreLoad_Key { get; private set; }
        public static ModKeybind ADS_Key { get; private set; }
        public static ModKeybind Halibut_Domain { get; private set; }
        public static ModKeybind Halibut_Clone { get; private set; }
        public static ModKeybind Halibut_Restart { get; private set; }
        public static ModKeybind Halibut_Superposition { get; private set; }
        public static ModKeybind Halibut_Teleport { get; private set; }

        void ICWRLoader.LoadData() {
            Mod mod = CWRMod.Instance;
            KreLoad_Key = KeybindLoader.RegisterKeybind(mod, "KreLoad_Key", "R");
            ADS_Key = KeybindLoader.RegisterKeybind(mod, "ADS_Key", "Z");
            HeavenfallLongbowSkillKey = KeybindLoader.RegisterKeybind(mod, "HeavenfallLongbowSkillKey", "Q");
            InfinitePickSkillKey = KeybindLoader.RegisterKeybind(mod, "InfinitePickSkillKey", "C");
            Murasama_TriggerKey = KeybindLoader.RegisterKeybind(mod, "Murasama_TriggerKey", "F");
            Murasama_DownKey = KeybindLoader.RegisterKeybind(mod, "Murasama_DownKey", "X");
            Halibut_Domain = KeybindLoader.RegisterKeybind(mod, "Halibut_Domain", "Q");
            Halibut_Clone = KeybindLoader.RegisterKeybind(mod, "Halibut_Clone", "J");
            Halibut_Restart = KeybindLoader.RegisterKeybind(mod, "Halibut_Restart", "H");
            Halibut_Superposition = KeybindLoader.RegisterKeybind(mod, "Halibut_Superposition", "F");
            Halibut_Teleport = KeybindLoader.RegisterKeybind(mod, "Halibut_Teleport", "G");
        }

        void ICWRLoader.UnLoadData() {
            KreLoad_Key = null;
            ADS_Key = null;
            HeavenfallLongbowSkillKey = null;
            InfinitePickSkillKey = null;
            Murasama_TriggerKey = null;
            Murasama_DownKey = null;
            Halibut_Domain = null;
            Halibut_Clone = null;
            Halibut_Restart = null;
            Halibut_Superposition = null;
            Halibut_Teleport = null;
        }
    }
}
