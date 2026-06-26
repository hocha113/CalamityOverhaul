using InnoVault.GameSystem;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.UIs.OverhaulSettings
{
    internal class OverhaulSettingsButton : MenuOverride, ILocalizedModType
    {
        public string LocalizationCategory => "UI";
        public static LocalizedText OverhaulSettingsButtonText { get; private set; }

        public override void SetStaticDefaults() {
            OverhaulSettingsButtonText = this.GetLocalization(nameof(OverhaulSettingsButtonText), () => "大修设置");
        }

        //主菜单按钮：反射 AddMenuButtons 注入( internal，签名可能变 )
        //思路来自瓶中微光；HoCha113 2026-2-10
        public override void AddMenuButtons(Main main, int selectedMenu, string[] buttonNames, float[] buttonScales, ref int offY, ref int spacing, ref int buttonIndex, ref int numButtons) {
            //插入'大修设置'按钮
            //处于界面设计的考量，取消对于主界面按钮的添加，改为在主页面消息栏添加
            //numButtons++;
            //buttonNames[buttonIndex] = OverhaulSettingsButtonText?.Value ?? "大修设置";
            //buttonScales[buttonIndex] = 1f;

            //if (selectedMenu == buttonIndex) {
            //OnOpen();
            //}

            //buttonIndex++;
        }

        public static void OnOpen() {
            if (Main.menuMode != 0) {
                SoundEngine.PlaySound(SoundID.Unlock);
                return;
            }
            Main.menuMode = 888;
            //激活设置UI
            var instance = OverhaulSettingsUI.Instance;
            if (instance != null) {
                instance._active = true;
            }
        }
    }
}
