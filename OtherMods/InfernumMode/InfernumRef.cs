using Terraria;

namespace CalamityOverhaul.OtherMods.InfernumMode
{
    internal class InfernumRef
    {
        //帧戳缓存：至尊灾厄/嘉登 AI 每帧多次读取，一帧内不会变，Mod.Call 一次后整帧复用
        private static uint stateFrame = uint.MaxValue;
        private static bool stateCache;

        /// <summary>炼狱模式是否开启</summary>
        internal static bool InfernumModeOpenState {
            get {
                if (CWRMod.Instance.infernum == null) {
                    return false;
                }
                if (stateFrame != Main.GameUpdateCount) {
                    stateFrame = Main.GameUpdateCount;
                    stateCache = CWRMod.Instance.infernum.Call("GetInfernumActive") is bool value && value;
                }
                return stateCache;
            }
        }
    }
}
