using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Common
{
    internal class CWRKeySystem : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "Keybinds";
        public static LocalizedText Notbound { get; private set; }
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
        public static ModKeybind Murasama_TriggerKey { get; private set; }
        public static ModKeybind Murasama_DownKey { get; private set; }
        public static ModKeybind Onikiri_Execute { get; private set; }
        public static ModKeybind Onikiri_DomainFlip { get; private set; }
        public static ModKeybind WeponSkill_Q { get; private set; }
        public static ModKeybind WeponSkill_R { get; private set; }
        public static ModKeybind Accessory_Skills { get; private set; }
        public static ModKeybind Wraith_Power { get; private set; }

        public override void SetStaticDefaults() {
            Notbound = this.GetLocalization(nameof(Notbound), () => "[未绑定按键]");
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
            Murasama_TriggerKey = KeybindLoader.RegisterKeybind(mod, nameof(Murasama_TriggerKey), "F");
            Murasama_DownKey = KeybindLoader.RegisterKeybind(mod, nameof(Murasama_DownKey), "X");
            Onikiri_Execute = KeybindLoader.RegisterKeybind(mod, nameof(Onikiri_Execute), "R");
            //鬼域表里翻转，默认鼠标中键（与原版 MouseMiddle 触发器同一键名体系，可在控制设置里重绑）
            Onikiri_DomainFlip = KeybindLoader.RegisterKeybind(mod, nameof(Onikiri_DomainFlip), "Mouse3");
            WeponSkill_Q = KeybindLoader.RegisterKeybind(mod, nameof(WeponSkill_Q), "Q");
            WeponSkill_R = KeybindLoader.RegisterKeybind(mod, nameof(WeponSkill_R), "R");
            Accessory_Skills = KeybindLoader.RegisterKeybind(mod, nameof(Accessory_Skills), "V");
            //厉鬼借力/仪式:死机窗口内对鬼行仪式,平时施放共鸣之力
            Wraith_Power = KeybindLoader.RegisterKeybind(mod, nameof(Wraith_Power), "P");
        }

        public override void Unload() {
            QuestLog_Key = null;
            QuestManager_Key = null;
            Murasama_TriggerKey = null;
            Murasama_DownKey = null;
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
            Wraith_Power = null;
        }
    }
}
