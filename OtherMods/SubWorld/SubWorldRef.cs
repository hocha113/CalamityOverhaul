using System;
using Terraria.ModLoader;

namespace CalamityOverhaul.OtherMods.SubWorld
{
    internal class SubWorldRef
    {
        /// <summary>
        /// 检查是否有任何子世界处于激活状态
        /// </summary>
        /// <returns></returns>
        internal static bool AnyActiveSubWorld() {
            try {
                bool result = false;
                foreach (var mod in ModLoader.Mods) {
                    if ((bool)CWRMod.Instance.subworldLibrary.Call("AnyActive", mod)) {
                        result = true;
                        break;
                    }
                }
                return result;
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"SubWorldRef.AnyActiveSubWorld An Error Has Cccurred: {ex.Message}");
                VaultUtils.Text("CWRMod Error: SubWorldRef.AnyActiveSubWorld An Error Has Occurred! See Log For Details.", Color.Red);
                return false;
            }
        }
    }
}
