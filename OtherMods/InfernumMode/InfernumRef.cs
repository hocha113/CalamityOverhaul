namespace CalamityOverhaul.OtherMods.InfernumMode
{
    internal class InfernumRef
    {
        /// <summary>
        /// 炼狱模式是否开启
        /// </summary>
        internal static bool InfernumModeOpenState {
            get {
                if (CWRMod.Instance.infernum == null) {
                    return false;
                }
                if (CWRMod.Instance.infernum.Call("GetInfernumActive") is bool value) {
                    return value;
                }
                return false;
            }
        }
    }
}
