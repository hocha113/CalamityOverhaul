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

        //这个方法会在主菜单添加按钮时被调用
        //这个实现思路来自于珊瑚石的一次开发尝试，虽然后续因为各种愿意没有继续下去但这个思路还是挺好的
        //感谢珊瑚石的作者瓶中微光分享思路
        //而 AddMenuButtons 方法本身是 tModLoader 内部的一个方法，负责在主菜单添加 Mod 按钮
        //虽然这个方法是 internal 的，但我们可以通过反射来访问它，我不知道 tModLoader 团队是否会在未来的版本中更改这个方法的签名或访问权限
        //他们为什么不直接提供一个公开的钩子？或者来一个event？保留一个这种空的并且是 internal 的方法实在是让人费解
        //不过不管如何，目前这个方法是可行的
        //——HoCha113 2026-2-10 4:01
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
