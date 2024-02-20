using Microsoft.Xna.Framework.Input;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Common
{
    internal class CWRKeySystem : ModSystem
    {
        public static ModKeybind HeavenfallLongbowSkillKey { get; private set; }
        public static ModKeybind InfinitePickSkillKey { get; private set; }
        public static ModKeybind TOM_OneClickP { get; private set; }
        public static ModKeybind TOM_QuickFetch { get; private set; }
        public static ModKeybind TOM_GatheringItem { get; private set; }
        public static ModKeybind TOM_GlobalRecall { get; private set; }
        public static ModKeybind Murasama_TriggerKey { get; private set; }
        public static ModKeybind Murasama_DownKey { get; private set; }
        public static ModKeybind ADS_Key { get; private set; }

        public override void Load() {
            HeavenfallLongbowSkillKey = KeybindLoader.RegisterKeybind(Mod, nameof(HeavenfallLongbowSkillKey), "Q");
            InfinitePickSkillKey = KeybindLoader.RegisterKeybind(Mod, nameof(InfinitePickSkillKey), "C");
            TOM_OneClickP = KeybindLoader.RegisterKeybind(Mod, nameof(TOM_OneClickP), "L");
            TOM_QuickFetch = KeybindLoader.RegisterKeybind(Mod, nameof(TOM_QuickFetch), "LeftShift");
            TOM_GatheringItem = KeybindLoader.RegisterKeybind(Mod, nameof(TOM_GatheringItem), "G");
            TOM_GlobalRecall = KeybindLoader.RegisterKeybind(Mod, nameof(TOM_GlobalRecall), "K");
            Murasama_TriggerKey = KeybindLoader.RegisterKeybind(Mod, nameof(Murasama_TriggerKey), "R");
            Murasama_DownKey = KeybindLoader.RegisterKeybind(Mod, nameof(Murasama_DownKey), "X");
            ADS_Key = KeybindLoader.RegisterKeybind(Mod, nameof(ADS_Key), "Z");
        }

        public override void Unload() {
            HeavenfallLongbowSkillKey = null;
            InfinitePickSkillKey = null;
            TOM_OneClickP = null;
            TOM_QuickFetch = null;
            TOM_GatheringItem = null;
            TOM_GlobalRecall = null;
            Murasama_TriggerKey = null;
            Murasama_DownKey = null;
            ADS_Key = null;
        }
    }
}
