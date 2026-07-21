using System;
using Terraria;
using static CalamityOverhaul.CWRMod;
namespace CalamityOverhaul
{
    internal static class ModCall
    {
        internal enum CallType
        {
            SupertableRecipeDate,
            SupertableSetItem,
            SetNoRecipeHasFrme,
            Config_ForceReplaceResetContent,
            Config_AddExtrasContent,
            SupertableRecipeDate_ZenithWorld,
            SetSupertableRecipeDate,
        }

        public static object Hander(params object[] args) {
            int contentCount = args.Length;
            if (contentCount <= 0) {
                Instance.Logger.Info("Call was made with no parameters.");
                return null;
            }

            CallType callType = default;
            //首参非 CallType 则退
            if (Enum.IsDefined(typeof(CallType), args[0])) {
                callType = (CallType)args[0];
            }
            else {
                Instance.Logger.Info("Call was made without the correct CallType.");
                return null;
            }

            //超工台已删，枚举留作兼容，no-op
            if (callType is CallType.SupertableRecipeDate
                or CallType.SupertableSetItem
                or CallType.SupertableRecipeDate_ZenithWorld
                or CallType.SetSupertableRecipeDate
                or CallType.SetNoRecipeHasFrme) {
                Instance.Logger.Info($"Call-{callType} is deprecated: the Supertable system has been removed. This call does nothing.");
                if (callType == CallType.SetNoRecipeHasFrme) {
                    return args.Length > 1 ? args[1] as Recipe : null;
                }
                return null;
            }
            //强制内容替换开关
            else if (callType == CallType.Config_ForceReplaceResetContent) {
                return true;
            }
            //弃用仍回 true，旧模组兼容
            else if (callType == CallType.Config_AddExtrasContent) {
                return true;
            }

            return null;
        }
    }
}
