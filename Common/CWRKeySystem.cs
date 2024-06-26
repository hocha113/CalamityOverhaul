﻿using Terraria.ModLoader;

namespace CalamityOverhaul.Common
{
    internal static class CWRKeySystem
    {
        public static ModKeybind HeavenfallLongbowSkillKey { get; private set; }
        public static ModKeybind InfinitePickSkillKey { get; private set; }
        //public static ModKeybind TOM_OneClickP { get; private set; }
        //public static ModKeybind TOM_QuickFetch { get; private set; }
        public static ModKeybind TOM_GatheringItem { get; private set; }
        //public static ModKeybind TOM_GlobalRecall { get; private set; }
        public static ModKeybind Murasama_TriggerKey { get; private set; }
        public static ModKeybind Murasama_DownKey { get; private set; }
        public static ModKeybind KreLoad_Key { get; private set; }
        public static ModKeybind ADS_Key { get; private set; }

        public static void LoadKeyDate(Mod mod) {
            HeavenfallLongbowSkillKey = KeybindLoader.RegisterKeybind(mod, "HeavenfallLongbowSkillKey", "Q");
            InfinitePickSkillKey = KeybindLoader.RegisterKeybind(mod, "InfinitePickSkillKey", "C");
            //TOM_QuickFetch = KeybindLoader.RegisterKeybind(mod, "TOM_QuickFetch", "LeftShift");
            TOM_GatheringItem = KeybindLoader.RegisterKeybind(mod, "TOM_GatheringItem", "G");
            //TOM_GlobalRecall = KeybindLoader.RegisterKeybind(mod, "TOM_GlobalRecall", "K");
            Murasama_TriggerKey = KeybindLoader.RegisterKeybind(mod, "Murasama_TriggerKey", "F");
            Murasama_DownKey = KeybindLoader.RegisterKeybind(mod, "Murasama_DownKey", "X");
            KreLoad_Key = KeybindLoader.RegisterKeybind(mod, "KreLoad_Key", "R");
            ADS_Key = KeybindLoader.RegisterKeybind(mod, "ADS_Key", "Z");
        }

        public static void Unload() {
            HeavenfallLongbowSkillKey = null;
            InfinitePickSkillKey = null;
            //TOM_OneClickP = null;
            //TOM_QuickFetch = null;
            TOM_GatheringItem = null;
            //TOM_GlobalRecall = null;
            Murasama_TriggerKey = null;
            Murasama_DownKey = null;
            KreLoad_Key = null;
            ADS_Key = null;
        }
    }
}
