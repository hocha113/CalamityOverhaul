using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria.GameInput;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Common
{
    internal class CWRKeySystem : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "Keybinds";
        public static LocalizedText Notbound { get; private set; }
        public static LocalizedText RightClickFallback { get; private set; }
        public static ModKeybind QuestLog_Key { get; private set; }
        public static ModKeybind QuestManager_Key { get; private set; }
        public static ModKeybind Legend_UIControl { get; private set; }
        public static ModKeybind Legend_Domain { get; private set; }
        public static ModKeybind Legend_Restart { get; private set; }
        public static ModKeybind Legend_Teleport { get; private set; }
        public static ModKeybind HackTime_Toggle { get; private set; }
        public static ModKeybind CyberBanish_Key { get; private set; }
        public static ModKeybind CyberFreeze_Key { get; private set; }
        public static ModKeybind CyberwareSkill_Key { get; private set; }
        public static ModKeybind CyberwareRadial_Key { get; private set; }
        public static ModKeybind VoidTimeShift_Key { get; private set; }
        public static ModKeybind Halibut_Clone { get; private set; }
        public static ModKeybind Halibut_Superposition { get; private set; }
        public static ModKeybind Halibut_SkillWheel { get; private set; }
        public static ModKeybind Onikiri_FlashStep { get; private set; }
        public static ModKeybind Onikiri_Execute { get; private set; }
        public static ModKeybind Onikiri_DomainFlip { get; private set; }
        public static ModKeybind WeponSkill_Q { get; private set; }
        public static ModKeybind WeponSkill_R { get; private set; }
        public static ModKeybind Accessory_Skills { get; private set; }

        public override void SetStaticDefaults() {
            Notbound = this.GetLocalization(nameof(Notbound), () => "[未绑定按键]");
            RightClickFallback = this.GetLocalization(nameof(RightClickFallback), () => "右键");
        }

        public override void Load() {
            Mod mod = CWRMod.Instance;
            QuestLog_Key = KeybindLoader.RegisterKeybind(mod, nameof(QuestLog_Key), "L");
            QuestManager_Key = KeybindLoader.RegisterKeybind(mod, nameof(QuestManager_Key), "K");
            Legend_UIControl = KeybindLoader.RegisterKeybind(mod, nameof(Legend_UIControl), "M");
            Legend_Domain = KeybindLoader.RegisterKeybind(mod, nameof(Legend_Domain), "Q");
            Legend_Teleport = KeybindLoader.RegisterKeybind(mod, nameof(Legend_Teleport), "G");
            Legend_Restart = KeybindLoader.RegisterKeybind(mod, nameof(Legend_Restart), "H");
            HackTime_Toggle = KeybindLoader.RegisterKeybind(mod, nameof(HackTime_Toggle), "N");
            CyberBanish_Key = KeybindLoader.RegisterKeybind(mod, nameof(CyberBanish_Key), "Y");
            CyberFreeze_Key = KeybindLoader.RegisterKeybind(mod, nameof(CyberFreeze_Key), "U");
            CyberwareSkill_Key = KeybindLoader.RegisterKeybind(mod, nameof(CyberwareSkill_Key), "V");
            CyberwareRadial_Key = KeybindLoader.RegisterKeybind(mod, nameof(CyberwareRadial_Key), "B");
            VoidTimeShift_Key = KeybindLoader.RegisterKeybind(mod, nameof(VoidTimeShift_Key), "K");
            Halibut_Clone = KeybindLoader.RegisterKeybind(mod, nameof(Halibut_Clone), "J");
            Halibut_Superposition = KeybindLoader.RegisterKeybind(mod, nameof(Halibut_Superposition), "F");
            Halibut_SkillWheel = KeybindLoader.RegisterKeybind(mod, nameof(Halibut_SkillWheel), "Tab");
            Onikiri_FlashStep = KeybindLoader.RegisterKeybind(mod, nameof(Onikiri_FlashStep), Keys.None);
            Onikiri_Execute = KeybindLoader.RegisterKeybind(mod, nameof(Onikiri_Execute), "R");
            //鬼域翻转，默认 Mouse3
            Onikiri_DomainFlip = KeybindLoader.RegisterKeybind(mod, nameof(Onikiri_DomainFlip), "Mouse3");
            WeponSkill_Q = KeybindLoader.RegisterKeybind(mod, nameof(WeponSkill_Q), "Q");
            WeponSkill_R = KeybindLoader.RegisterKeybind(mod, nameof(WeponSkill_R), "R");
            Accessory_Skills = KeybindLoader.RegisterKeybind(mod, nameof(Accessory_Skills), "V");
        }

        public static bool IsKeybindUnbound(ModKeybind keybind, InputMode mode = InputMode.Keyboard) {
            List<string> assignedKeys = keybind?.GetAssignedKeys(mode);
            return assignedKeys == null || assignedKeys.Count == 0
                || assignedKeys.All(key => string.Equals(key, Keys.None.ToString(), StringComparison.OrdinalIgnoreCase));
        }

        public static string GetKeybindText(ModKeybind keybind, string fallback, InputMode mode = InputMode.Keyboard) {
            List<string> assignedKeys = keybind?.GetAssignedKeys(mode);
            if (assignedKeys == null) {
                return fallback;
            }
            string[] effectiveKeys = assignedKeys
                .Where(key => !string.Equals(key, Keys.None.ToString(), StringComparison.OrdinalIgnoreCase))
                .ToArray();
            return effectiveKeys.Length == 0 ? fallback : string.Join(" / ", effectiveKeys);
        }

        public override void Unload() {
            QuestLog_Key = null;
            QuestManager_Key = null;
            Onikiri_FlashStep = null;
            Onikiri_Execute = null;
            Onikiri_DomainFlip = null;
            Legend_Domain = null;
            Halibut_Clone = null;
            Legend_Restart = null;
            Halibut_Superposition = null;
            Legend_Teleport = null;
            Legend_UIControl = null;
            Halibut_SkillWheel = null;
            WeponSkill_Q = null;
            WeponSkill_R = null;
            Accessory_Skills = null;
            HackTime_Toggle = null;
            CyberBanish_Key = null;
            CyberFreeze_Key = null;
            CyberwareSkill_Key = null;
            CyberwareRadial_Key = null;
            VoidTimeShift_Key = null;
        }
    }
}
