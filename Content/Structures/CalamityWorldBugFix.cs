using InnoVault.GameSystem;
using System;
using System.Reflection;
using Terraria;

namespace CalamityOverhaul.Content.Structures
{
    //子世界 HandleTileGrowth：worldSurface 近底部时 genRand.Next min>=max
    internal class CalamityWorldBugFix : ICWRLoader
    {
        private static void On_HandleTileGrowth(Action orig) {
            int surfaceLevel = (int)Main.worldSurface - 1;
            //worldSurface 越界则跳过
            if (surfaceLevel <= 10 || surfaceLevel >= Main.maxTilesY - 20) {
                return;
            }
            orig();
        }

        void ICWRLoader.LoadData() {
            var type = CWRMod.Instance.calamity?.Code.GetType("CalamityMod.Systems.WorldMiscUpdateSystem");
            if (type is null) {
                return;
            }
            var method = type.GetMethod("HandleTileGrowth", BindingFlags.Static | BindingFlags.Public);
            if (method is null) {
                return;
            }
            VaultHook.Add(method, On_HandleTileGrowth);
        }
    }
}
