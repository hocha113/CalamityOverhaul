using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.OtherMods.SubWorld
{
    internal class SubWorldRef
    {
        //帧戳缓存：多处 PostUpdateEverything/PostUpdate 每帧各调一次，
        //"扫全部模组 × Mod.Call"一帧内答案不变，算一次整帧复用
        private static uint anyActiveFrame = uint.MaxValue;
        private static bool anyActiveCache;

        /// <summary>是否有子世界激活</summary>
        internal static bool AnyActiveSubWorld() {
            if (CWRMod.Instance.subworldLibrary is null) {
                return false;
            }
            if (anyActiveFrame == Main.GameUpdateCount) {
                return anyActiveCache;
            }
            anyActiveFrame = Main.GameUpdateCount;
            try {
                bool result = false;
                foreach (var mod in ModLoader.Mods) {
                    if ((bool)CWRMod.Instance.subworldLibrary.Call("AnyActive", mod)) {
                        result = true;
                        break;
                    }
                }
                anyActiveCache = result;
                return result;
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"SubWorldRef.AnyActiveSubWorld An Error Has Cccurred: {ex.Message}");
                VaultUtils.Text("CWRMod Error: SubWorldRef.AnyActiveSubWorld An Error Has Occurred! See Log For Details.", Color.Red);
                anyActiveCache = false;
                return false;
            }
        }
    }
}
