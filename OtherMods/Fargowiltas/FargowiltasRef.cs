using CalamityOverhaul.Content.QuestLogs;
using InnoVault.GameSystem;
using System;
using System.Reflection;

namespace CalamityOverhaul.OtherMods.Fargowiltas
{
    internal class FargowiltasRef : ICWRLoader
    {
        private static MethodInfo closeStatSheetMethod;
        private static MethodInfo closeStatButtonMethod;
        void ICWRLoader.LoadData() {
            if (CWRMod.Instance.fargowiltas is null) {
                return;
            }

            var uiManagerType = CWRMod.Instance.fargowiltas.Code.GetType("Fargowilta.UIManager");
            if (uiManagerType is null) {
                return;
            }

            //获取 UpdateUI 方法
            var updateUIMethod = uiManagerType.GetMethod("UpdateUI",
                BindingFlags.Public | BindingFlags.Instance);

            closeStatSheetMethod = uiManagerType.GetMethod("CloseStatSheet",
                BindingFlags.Public | BindingFlags.Instance);

            closeStatButtonMethod = uiManagerType.GetMethod("CloseStatButton",
                BindingFlags.Public | BindingFlags.Instance);

            if (updateUIMethod != null) {
                VaultHook.Add(updateUIMethod, UpdateUI_Hook);
            }
        }

        private static void UpdateUI_Hook(Action<object, object> orig, object self, object gameTime) {
            //如果 QuestLog 可见,强制关闭 StatButton
            if (QuestLog.Instance.visible) {
                //关掉这些UI防止冲突
                closeStatSheetMethod?.Invoke(self, null);
                closeStatButtonMethod?.Invoke(self, null);
            }
            else {
                //调用原始方法
                orig(self, gameTime);
            }
        }
    }
}
