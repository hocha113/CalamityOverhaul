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
        /// <summary>任务书总开关，委托卷宗已并为书内站点，不再单设一把键</summary>
        public static ModKeybind QuestLog_Key { get; private set; }
        public static ModKeybind Legend_UIControl { get; private set; }
        public static ModKeybind Legend_Domain { get; private set; }
        /// <summary>全部快捷转盘共用的开关键，分发见 RadialWheelHub</summary>
        public static ModKeybind RadialWheel_Key { get; private set; }
        public static ModKeybind Legend_Restart { get; private set; }
        public static ModKeybind Legend_Teleport { get; private set; }
        public static ModKeybind HackTime_Toggle { get; private set; }
        public static ModKeybind CyberBanish_Key { get; private set; }
        public static ModKeybind CyberFreeze_Key { get; private set; }
        public static ModKeybind CyberwareSkill_Key { get; private set; }
        public static ModKeybind VoidTimeShift_Key { get; private set; }
        public static ModKeybind Halibut_Clone { get; private set; }
        public static ModKeybind Halibut_Superposition { get; private set; }
        public static ModKeybind Onikiri_FlashStep { get; private set; }
        public static ModKeybind Onikiri_Execute { get; private set; }
        public static ModKeybind Onikiri_SakuraFlight { get; private set; }
        public static ModKeybind Onikiri_DomainFlip { get; private set; }
        public static ModKeybind Kikasa_Sink { get; private set; }
        public static ModKeybind Kikasa_DomainMutate { get; private set; }
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
            Legend_UIControl = KeybindLoader.RegisterKeybind(mod, nameof(Legend_UIControl), "M");
            Legend_Domain = KeybindLoader.RegisterKeybind(mod, nameof(Legend_Domain), "Q");
            //比目鱼技能盘、SHPC 领域盘、义体技能盘共用此键，够格的一起开
            RadialWheel_Key = KeybindLoader.RegisterKeybind(mod, nameof(RadialWheel_Key), "B");
            Legend_Teleport = KeybindLoader.RegisterKeybind(mod, nameof(Legend_Teleport), "G");
            Legend_Restart = KeybindLoader.RegisterKeybind(mod, nameof(Legend_Restart), "H");
            HackTime_Toggle = KeybindLoader.RegisterKeybind(mod, nameof(HackTime_Toggle), "N");
            CyberBanish_Key = KeybindLoader.RegisterKeybind(mod, nameof(CyberBanish_Key), "Y");
            CyberFreeze_Key = KeybindLoader.RegisterKeybind(mod, nameof(CyberFreeze_Key), "U");
            CyberwareSkill_Key = KeybindLoader.RegisterKeybind(mod, nameof(CyberwareSkill_Key), "V");
            VoidTimeShift_Key = KeybindLoader.RegisterKeybind(mod, nameof(VoidTimeShift_Key), "K");
            Halibut_Clone = KeybindLoader.RegisterKeybind(mod, nameof(Halibut_Clone), "J");
            Halibut_Superposition = KeybindLoader.RegisterKeybind(mod, nameof(Halibut_Superposition), "F");
            Onikiri_FlashStep = KeybindLoader.RegisterKeybind(mod, nameof(Onikiri_FlashStep), Keys.None);
            Onikiri_Execute = KeybindLoader.RegisterKeybind(mod, nameof(Onikiri_Execute), "F");
            Onikiri_SakuraFlight = KeybindLoader.RegisterKeybind(mod, nameof(Onikiri_SakuraFlight), "C");
            //鬼域翻转，默认 Mouse3
            Onikiri_DomainFlip = KeybindLoader.RegisterKeybind(mod, nameof(Onikiri_DomainFlip), "Mouse3");
            Kikasa_Sink = KeybindLoader.RegisterKeybind(mod, nameof(Kikasa_Sink), "I");
            //血湖领域鬼雨异化，默认 Mouse3；被清空绑定时输入层回退原生中键。
            //短按=开域/血雨翻转，魇影驻湖长按=拉入鬼梦——鬼伞灵异全走沉影盘自动门控，
            //旧 Kikasa_Summon/DreamReflect/DreamPull/WispFire 四键已随编成制删除
            Kikasa_DomainMutate = KeybindLoader.RegisterKeybind(mod, nameof(Kikasa_DomainMutate), "Mouse3");
            //鬼伞大范围重启复用 Legend_Restart，与比目鱼/赛博/绯嫁同键、按各自形态门互斥
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
            Onikiri_FlashStep = null;
            Onikiri_Execute = null;
            Onikiri_SakuraFlight = null;
            Onikiri_DomainFlip = null;
            Kikasa_Sink = null;
            Kikasa_DomainMutate = null;
            Legend_Domain = null;
            RadialWheel_Key = null;
            Halibut_Clone = null;
            Legend_Restart = null;
            Halibut_Superposition = null;
            Legend_Teleport = null;
            Legend_UIControl = null;
            WeponSkill_Q = null;
            WeponSkill_R = null;
            Accessory_Skills = null;
            HackTime_Toggle = null;
            CyberBanish_Key = null;
            CyberFreeze_Key = null;
            CyberwareSkill_Key = null;
            VoidTimeShift_Key = null;
        }
    }
}
