using CalamityOverhaul.Content.QuestLogs;
using InnoVault.GameSystem;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Reflection;

namespace CalamityOverhaul.OtherMods.Fargowiltas
{
    internal class FargowiltasRef : ICWRLoader
    {
        private static PropertyInfo UserInterfaceManagerProperty;
        public static void CloseStatButton() {
            if (CWRMod.Instance.fargowiltas is null) {
                return;
            }

            UserInterfaceManagerProperty ??= CWRMod.Instance.fargowiltas.GetType()
                    .GetProperty("UserInterfaceManager", BindingFlags.Public | BindingFlags.Static);

            var uiManager = UserInterfaceManagerProperty?.GetValue(null);

            //强制关闭 StatButton
            uiManager?.GetType().GetMethod("CloseStatButton")?.Invoke(uiManager, null);
        }
    }
}
