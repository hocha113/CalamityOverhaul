using InnoVault.GameSystem;
using System;
using System.Reflection;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.UIs.OverhaulSettings
{
    internal class OverhaulSettingsButton : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "UI";
        public static LocalizedText OverhaulSettingsButtonText { get; private set; }

        //必须与原方法签名完全匹配的委托
        private delegate void orig_AddMenuButtons(
            Main main, int selectedMenu,
            string[] buttonNames, float[] buttonScales,
            ref int offY, ref int spacing,
            ref int buttonIndex, ref int numButtons);

        public override void SetStaticDefaults() {
            OverhaulSettingsButtonText = this.GetLocalization(nameof(OverhaulSettingsButtonText), () => "大修设置");
        }

        public override void Load() {
            //通过程序集反射获取 internal 类型
            Type interfaceType = typeof(Main).Assembly.GetType("Terraria.ModLoader.UI.Interface");
            //反射获取 internal 方法
            MethodInfo targetMethod = interfaceType.GetMethod("AddMenuButtons", BindingFlags.Static | BindingFlags.NonPublic);

            VaultHook.Add(targetMethod, OnAddMenuButtonsHook);
        }

        private static void OnAddMenuButtonsHook(
            orig_AddMenuButtons orig,
            Main main, int selectedMenu,
            string[] buttonNames, float[] buttonScales,
            ref int offY, ref int spacing,
            ref int buttonIndex, ref int numButtons) {
            //先调用原方法
            orig(main, selectedMenu, buttonNames, buttonScales,
                 ref offY, ref spacing, ref buttonIndex, ref numButtons);

            //插入'大修设置'按钮
            numButtons++;
            buttonNames[buttonIndex] = OverhaulSettingsButtonText?.Value ?? "大修设置";
            buttonScales[buttonIndex] = 1f;

            if (selectedMenu == buttonIndex) {
                SoundEngine.PlaySound(SoundID.MenuOpen with { Volume = 0.6f, Pitch = 0.1f });
                Main.menuMode = 888;

                //激活设置UI
                var instance = OverhaulSettingsUI.Instance;
                if (instance != null) {
                    instance._active = true;
                }
            }

            buttonIndex++;
        }
    }
}
