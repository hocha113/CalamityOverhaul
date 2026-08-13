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
            //QuestLog 可见时关 Stat 防止冲突
            if (QuestLog.Instance.IsOpen) {
                closeStatSheetMethod?.Invoke(self, null);
                closeStatButtonMethod?.Invoke(self, null);
            }
            else {
                orig(self, gameTime);
            }
        }
    }
}
