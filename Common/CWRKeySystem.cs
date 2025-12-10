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
        public static ModKeybind QuestLog_Key { get; private set; }
        public static ModKeybind Halibut_Domain { get; private set; }
        public static ModKeybind Halibut_Clone { get; private set; }
        public static ModKeybind Halibut_Restart { get; private set; }
        public static ModKeybind Halibut_Superposition { get; private set; }
        public static ModKeybind Halibut_Teleport { get; private set; }
        public static ModKeybind Halibut_UIControl { get; private set; }
        public static ModKeybind Halibut_Skill_L { get; private set; }
        public static ModKeybind Halibut_Skill_R { get; private set; }
        public static ModKeybind Pandemonium_Q { get; private set; }
        public static ModKeybind Pandemonium_R { get; private set; }
        public static ModKeybind AriaofTheCosmos_Q { get; private set; }
        public static ModKeybind AriaofTheCosmos_R { get; private set; }

        void ICWRLoader.LoadData() {
            Mod mod = CWRMod.Instance;
            KreLoad_Key = KeybindLoader.RegisterKeybind(mod, "KreLoad_Key", "R");
            ADS_Key = KeybindLoader.RegisterKeybind(mod, "ADS_Key", "Z");
            QuestLog_Key = KeybindLoader.RegisterKeybind(mod, "QuestLog_Key", "L");
            HeavenfallLongbowSkillKey = KeybindLoader.RegisterKeybind(mod, "HeavenfallLongbowSkillKey", "Q");
            InfinitePickSkillKey = KeybindLoader.RegisterKeybind(mod, "InfinitePickSkillKey", "C");
            Murasama_TriggerKey = KeybindLoader.RegisterKeybind(mod, "Murasama_TriggerKey", "F");
            Murasama_DownKey = KeybindLoader.RegisterKeybind(mod, "Murasama_DownKey", "X");
            Halibut_Domain = KeybindLoader.RegisterKeybind(mod, "Halibut_Domain", "Q");
            Halibut_Clone = KeybindLoader.RegisterKeybind(mod, "Halibut_Clone", "J");
            Halibut_Restart = KeybindLoader.RegisterKeybind(mod, "Halibut_Restart", "H");
            Halibut_Superposition = KeybindLoader.RegisterKeybind(mod, "Halibut_Superposition", "F");
            Halibut_Teleport = KeybindLoader.RegisterKeybind(mod, "Halibut_Teleport", "G");
            Halibut_UIControl = KeybindLoader.RegisterKeybind(mod, "Halibut_UIControl", "M");
            Halibut_Skill_L = KeybindLoader.RegisterKeybind(mod, "Halibut_Skill_L", "Q");
            Halibut_Skill_R = KeybindLoader.RegisterKeybind(mod, "Halibut_Skill_R", "E");
            Pandemonium_Q = KeybindLoader.RegisterKeybind(mod, "Pandemonium_Q", "Q");
            Pandemonium_R = KeybindLoader.RegisterKeybind(mod, "Pandemonium_R", "R");
            AriaofTheCosmos_Q = KeybindLoader.RegisterKeybind(mod, "AriaofTheCosmos_Q", "Q");
            AriaofTheCosmos_R = KeybindLoader.RegisterKeybind(mod, "AriaofTheCosmos_R", "R");
        }

        void ICWRLoader.UnLoadData() {
            KreLoad_Key = null;
            ADS_Key = null;
            QuestLog_Key = null;
            HeavenfallLongbowSkillKey = null;
            InfinitePickSkillKey = null;
            Murasama_TriggerKey = null;
            Murasama_DownKey = null;
            Halibut_Domain = null;
            Halibut_Clone = null;
            Halibut_Restart = null;
            Halibut_Superposition = null;
            Halibut_Teleport = null;
            Halibut_UIControl = null;
            Halibut_Skill_L = null;
            Halibut_Skill_R = null;
            Pandemonium_Q = null;
            Pandemonium_R = null;
            AriaofTheCosmos_Q = null;
            AriaofTheCosmos_R = null;
        }
    }
}
